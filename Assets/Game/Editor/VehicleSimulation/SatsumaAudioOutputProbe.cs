using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    /// <summary>
    /// Explicit command-line diagnostic for the real Editor audio device. It
    /// opens an unsaved empty scene, never opens a save, and exits its own Editor.
    /// Not a listening approval or a replacement for production-scene tests.
    /// </summary>
    [InitializeOnLoad]
    public static class SatsumaAudioOutputProbe
    {
        private const string SessionKey = "MSC.SatsumaAudioOutputProbe.Output";
        private const string StartedKey = "MSC.SatsumaAudioOutputProbe.Started";
        private static readonly List<Sample> Results = new();
        private static readonly float[] Buffer = new float[2048];
        private static UnityAudioBackend backend;
        private static AudioEmitterAuthoring emitter;
        private static AudioListener listener;
        private static List<Stage> stages;
        private static Stage stage;
        private static AudioSource source;
        private static int stageIndex;
        private static double stageStart;
        private static double stageDspStart;
        private static double energy;
        private static double listenerEnergy;
        private static float peak;
        private static float listenerPeak;
        private static int count;
        private static int captures;
        private static string output;
        private static double nextCapture;
        private static float observedOverflow;
        private static double quietCaptureStart;
        private static double longestQuietCaptureRun;

        static SatsumaAudioOutputProbe()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(SessionKey, string.Empty)))
                EditorApplication.update += Update;
        }

        public static void Run()
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Run this device probe without -batchmode, -nographics or -quit.");
            string[] arguments = Environment.GetCommandLineArgs();
            int methodIndex = Array.IndexOf(arguments, "-executeMethod");
            if (methodIndex < 0 || methodIndex + 1 >= arguments.Length ||
                arguments[methodIndex + 1] != typeof(SatsumaAudioOutputProbe).FullName + ".Run" &&
                arguments[methodIndex + 1] != typeof(SatsumaAudioOutputProbe).FullName + ".RunAfterRefreshingLibraries")
                throw new InvalidOperationException("This exit-on-completion probe may only own its explicit command-line Editor session.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("The command-line probe requires a fresh, stopped Editor.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Refusing to replace an unsaved user scene.");
            output = Path.GetFullPath("Logs/satsuma-audio-output-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            SessionState.SetString(SessionKey, output);
            SessionState.SetString(StartedKey, DateTime.UtcNow.ToString("O"));
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void RunAfterRefreshingLibraries()
        {
            RefreshLibrariesBatch();
            Run();
        }

        public static void RefreshLibrariesBatch()
        {
            Phase1SatsumaEngineAudioImporter.Build();
            Phase1UserSelectedAudioImporter.Build();
        }

        private static void Initialize()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            listener = new GameObject("Explicit audio probe listener").AddComponent<AudioListener>();
            backend = new GameObject("Real audio probe backend").AddComponent<UnityAudioBackend>();
            var engine = Resources.Load<UnityAudioEventLibrary>(SatsumaEngineFeedbackPresenter.FallbackResourcesPath);
            if (engine == null) throw new InvalidDataException("Missing engine library.");
            backend.ConfigureForAuthoring(engine);
            var replacement = Resources.Load<UnityAudioEventLibrary>("Phase1UserSelectedAudio/Phase1UserSelectedAudioEventLibrary");
            if (replacement == null || !backend.TrySetReplacementEventLibrary(replacement, out _))
                throw new InvalidDataException("Missing/invalid selected library.");
            backend.ApplySettings(new AudioSettingsState(1f, 1f, 1f, 1f, 1f, 1f,
                AudioDynamicRangeMode.Wide, false, false, false, false));
            emitter = new GameObject("Explicit audio probe source").AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.satsuma.output-probe", null, emitter.transform);
            if (!backend.RegisterEmitter(emitter, out string failure)) throw new InvalidDataException(failure);

            var sine = AudioClip.Create("Project-owned output probe sine", 48000, 1, 48000, false);
            var pcm = new float[48000];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = .05f * Mathf.Sin(2f * Mathf.PI * 330f * i / 48000f);
            sine.SetData(pcm, 0);
            var control = new UnityAudioEventDefinition();
            control.ConfigureForAuthoring("audio.event.satsuma.output-probe.control", sine,
                UnityAudioCategory.Vehicle, true, .5f, 1f, 0f, 1f, 40f);
            var supplemental = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            var supplementalDefinitions = new List<UnityAudioEventDefinition> { control };
            var amplifiedControl = CopyDefinition(control, control.EventId + ".mix", 6f);
            supplementalDefinitions.Add(amplifiedControl);
            stages = new List<Stage> { new("control", control, 0f, 1f, 1f),
                new("control-mix", amplifiedControl, 0f, 1f, 1f),
                new("control-muted", amplifiedControl, 0f, 1f, 1f, master: 0f) };
            var mix = SatsumaEngineFeedbackRules.Evaluate(VehicleEngineStatus.Running, 800f, 0f, SatsumaExhaustOutlet.Muffler);
            foreach (float distance in new[] { 1f, 2f })
                foreach (var id in new[] { SatsumaEngineAudioIds.StarterEngaged, SatsumaEngineAudioIds.StarterLoop,
                    SatsumaEngineAudioIds.EngineCaught, SatsumaEngineAudioIds.EngineThrottleLoop,
                    SatsumaEngineAudioIds.EngineCoastLoop, SatsumaEngineAudioIds.ExhaustLoop,
                    SatsumaEngineAudioIds.IntakeSpit, SatsumaEngineAudioIds.ExhaustBackfire })
                {
                    if (!replacement.TryResolve(id, out var definition) && !engine.TryResolve(id, out definition))
                        throw new InvalidDataException("Missing " + id);
                    float gain = id == SatsumaEngineAudioIds.EngineThrottleLoop ? mix.ThrottleGain :
                        id == SatsumaEngineAudioIds.EngineCoastLoop ? mix.CoastGain :
                        id == SatsumaEngineAudioIds.ExhaustLoop ? .2f : 1f;
                    float pitch = id == SatsumaEngineAudioIds.EngineThrottleLoop ? mix.ThrottlePitch :
                        id == SatsumaEngineAudioIds.EngineCoastLoop ? mix.CoastPitch :
                        id == SatsumaEngineAudioIds.ExhaustLoop ? .63f : 1f;
                    var unbalanced = CopyDefinition(definition, id.Value + ".probe-unmixed", 0f);
                    if (!supplementalDefinitions.Any(value => value.EventId == unbalanced.EventId))
                        supplementalDefinitions.Add(unbalanced);
                    stages.Add(new Stage("before/" + id.Value, unbalanced, distance, gain, pitch));
                    stages.Add(new Stage("after/" + id.Value, definition, distance, gain, pitch));
                }
            foreach (var point in new[] { (rpm: 800f, throttle: 0f), (rpm: 1500f, throttle: .2f),
                (rpm: 3000f, throttle: .45f), (rpm: 3000f, throttle: 1f), (rpm: 6000f, throttle: 1f),
                (rpm: 6000f, throttle: 0f), (rpm: 8000f, throttle: 0f), (rpm: 8000f, throttle: 1f) })
            {
                var operatingMix = SatsumaEngineFeedbackRules.Evaluate(VehicleEngineStatus.Running,
                    point.rpm, point.throttle, SatsumaExhaustOutlet.Muffler);
                replacement.TryResolve(SatsumaEngineAudioIds.EngineThrottleLoop, out var throttleDefinition);
                replacement.TryResolve(SatsumaEngineAudioIds.EngineCoastLoop, out var coastDefinition);
                replacement.TryResolve(SatsumaEngineAudioIds.ExhaustLoop, out var exhaustDefinition);
                stages.Add(new Stage("combined-coincident-" + point.rpm + "-pedal-" + point.throttle, throttleDefinition, 1f,
                    operatingMix.ThrottleGain, operatingMix.ThrottlePitch)
                {
                    OtherDefinitions = new[] { coastDefinition, exhaustDefinition },
                    OtherGains = new[] { operatingMix.CoastGain, .2f },
                    OtherPitches = new[] { operatingMix.CoastPitch, SatsumaExhaustFeedbackRules.Pitch(point.rpm) }
                });
                var previousThrottle = PreviousRunningMix(throttleDefinition, 10f, .5f, supplementalDefinitions);
                var previousCoast = PreviousRunningMix(coastDefinition, 12f, .6f, supplementalDefinitions);
                foreach (bool previous in new[] { true, false })
                    stages.Add(new Stage("combined-coincident-2m-" + (previous ? "previous-" : "current-") +
                        point.rpm + "-pedal-" + point.throttle, previous ? previousThrottle : throttleDefinition, 2f,
                        operatingMix.ThrottleGain, operatingMix.ThrottlePitch)
                    {
                        OtherDefinitions = new[] { previous ? previousCoast : coastDefinition, exhaustDefinition },
                        OtherGains = new[] { operatingMix.CoastGain, .2f },
                        OtherPitches = new[] { operatingMix.CoastPitch, SatsumaExhaustFeedbackRules.Pitch(point.rpm) }
                    });
            }
            replacement.TryResolve(SatsumaEngineAudioIds.StarterLoop, out var continuousStarter);
            stages.Add(new Stage("prepared-starter-loop-two-seams", continuousStarter, 1f, 1f, 1f)
                { DurationSeconds = 7f });
            supplemental.ConfigureForAuthoring(supplementalDefinitions.ToArray());
            backend.ConfigureSupplementalEventLibrariesForAuthoring(supplemental);
            BeginStage();
        }

        private static UnityAudioEventDefinition PreviousRunningMix(UnityAudioEventDefinition original,
            float mixDb, float ceiling, List<UnityAudioEventDefinition> definitions)
        {
            string id = original.EventId + ".probe-previous-balance";
            UnityAudioEventDefinition copy = definitions.Find(value => value.EventId == id);
            if (copy != null) return copy;
            copy = CopyDefinition(original, id, mixDb);
            copy.ConfigureForAuthoring(id, original.Clip, original.Category, original.Loop, original.Volume,
                original.Pitch, original.SpatialBlend, 1f, original.MaximumDistanceMeters);
            copy.ConfigureMixGainForAuthoring(mixDb, ceiling);
            definitions.Add(copy);
            return copy;
        }

        private static UnityAudioEventDefinition CopyDefinition(UnityAudioEventDefinition original, string id, float mixDb)
        {
            var copy = new UnityAudioEventDefinition();
            copy.ConfigureForAuthoring(id, original.Clip, original.Category, original.Loop, original.Volume,
                original.Pitch, original.SpatialBlend, original.MinimumDistanceMeters, original.MaximumDistanceMeters);
            copy.ConfigureDistanceRolloffForAuthoring(original.RolloffMode == AudioRolloffMode.Logarithmic
                ? UnityAudioDistanceRolloff.Logarithmic : UnityAudioDistanceRolloff.Linear);
            copy.ConfigureParameterBindingsForAuthoring(original.VolumeParameterId, original.PitchParameterId);
            copy.ConfigureCalibrationForAuthoring(original.CalibrationGainDb);
            copy.ConfigureMixGainForAuthoring(mixDb, mixDb == 0f ? 0f : original.MixBoostCeiling);
            copy.ConfigureOutputGainCeilingForAuthoring(mixDb == 0f ? 0f : original.OutputGainCeiling);
            return copy;
        }

        private static void BeginStage()
        {
            stage = stages[stageIndex];
            backend.StopAll();
            source = null;
            stageStart = EditorApplication.timeSinceStartup;
            listener.transform.position = Vector3.forward * stage.Distance;
        }

        private static void BeginPlayback()
        {
            backend.ApplySettings(new AudioSettingsState(stage.Master, 1f, 1f, 1f, 1f, 1f,
                AudioDynamicRangeMode.Wide, false, false, false, false));
            void Play(UnityAudioEventDefinition definition, float gain, float pitch)
            {
                if (!definition.VolumeParameterId.IsEmpty) backend.SetParameter(definition.VolumeParameterId, gain, emitter);
                if (!definition.PitchParameterId.IsEmpty) backend.SetParameter(definition.PitchParameterId, pitch, emitter);
                var request = new AudioEventRequest(new AudioEventId(definition.EventId), emitter, allowMultiple: false);
                if (!backend.PostEvent(in request).IsValid) throw new InvalidOperationException("Post failed: " + stage.Name);
            }
            Play(stage.Definition, stage.Gain, stage.Pitch);
            for (int i = 0; i < stage.OtherDefinitions.Length; i++)
                Play(stage.OtherDefinitions[i], stage.OtherGains[i], stage.OtherPitches[i]);
            source = backend.GetComponentsInChildren<AudioSource>().Single(value => value.clip == stage.Definition.Clip);
            stageStart = EditorApplication.timeSinceStartup;
            nextCapture = stageStart + .025d;
            stageDspStart = UnityEngine.AudioSettings.dspTime;
            observedOverflow = source.GetComponent<UnityAudioCalibrationFilter>().OverflowGain;
            energy = listenerEnergy = 0d; peak = listenerPeak = 0f; count = captures = 0;
            quietCaptureStart = -1d; longestQuietCaptureRun = 0d;
            // Prime the history ring before collecting an actual output block.
            source.GetOutputData(Buffer, 0);
            AudioListener.GetOutputData(Buffer, 0);
        }

        private static void Update()
        {
            output = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(output)) return;
            if (DateTime.TryParse(SessionState.GetString(StartedKey, string.Empty), out var started) &&
                (DateTime.UtcNow - started.ToUniversalTime()).TotalSeconds > 180d)
            { Finish(4, "Audio probe timed out; no listening/output approval."); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (backend == null) { Initialize(); return; }
                double elapsed = EditorApplication.timeSinceStartup - stageStart;
                if (source == null)
                {
                    // Flush the previous event's DSP/history buffers before a
                    // transient starts. Never count its tail as the next sound.
                    if (elapsed > .15d) BeginPlayback();
                    return;
                }
                if (EditorApplication.timeSinceStartup >= nextCapture && source.isPlaying)
                {
                    nextCapture = EditorApplication.timeSinceStartup + .02d;
                    observedOverflow = Mathf.Max(observedOverflow, source.GetComponent<UnityAudioCalibrationFilter>().OverflowGain);
                    source.GetOutputData(Buffer, 0);
                    foreach (float value in Buffer) { energy += value * value; peak = Mathf.Max(peak, Mathf.Abs(value)); }
                    AudioListener.GetOutputData(Buffer, 0);
                    float blockPeak = 0f;
                    foreach (float value in Buffer) { listenerEnergy += value * value; listenerPeak = Mathf.Max(listenerPeak, Mathf.Abs(value));
                        blockPeak = Mathf.Max(blockPeak, Mathf.Abs(value)); }
                    if (elapsed > .15d)
                    {
                        if (blockPeak < .005f)
                        {
                            if (quietCaptureStart < 0d) quietCaptureStart = elapsed;
                            longestQuietCaptureRun = Math.Max(longestQuietCaptureRun, elapsed - quietCaptureStart);
                        }
                        else quietCaptureStart = -1d;
                    }
                    count += Buffer.Length; captures++;
                }
                double duration = stage.DurationSeconds > 0f ? stage.DurationSeconds : stage.Definition.Loop
                    ? 1.0d : Math.Min(1.0d, stage.Definition.Clip.length / stage.Definition.Pitch + .15d);
                if (elapsed < duration) return;
                Results.Add(new Sample { name = stage.Name, distanceMeters = stage.Distance,
                    sourceVolume = source.volume, contentGainDb = stage.Definition.CalibrationGainDb,
                    mixGainDb = stage.Definition.MixGainDb, mixBoostCeiling = stage.Definition.MixBoostCeiling,
                    outputGainCeiling = stage.Definition.OutputGainCeiling,
                    parameterGain = stage.Gain, sourcePitch = source.pitch, sourcePeak = peak,
                    listenerPeak = listenerPeak, sourceRmsDbfs = Db(energy), listenerRmsDbfs = Db(listenerEnergy),
                    dspSeconds = UnityEngine.AudioSettings.dspTime - stageDspStart, captures = captures,
                    overflowGain = observedOverflow,
                    listenerVolume = AudioListener.volume, listenerPaused = AudioListener.pause,
                    longestQuietCaptureRunSeconds = longestQuietCaptureRun,
                    clip = stage.Definition.Clip.name });
                Debug.Log("SATSUMA_AUDIO_OUTPUT_SAMPLE " + JsonUtility.ToJson(Results.Last()));
                if (++stageIndex < stages.Count) { BeginStage(); return; }
                bool device = Results[0].sourcePeak > .001f && Results[0].listenerPeak > .001f;
                float ratio = device ? Results[1].listenerPeak / Results[0].listenerPeak : 0f;
                bool calibrated = Mathf.Abs(ratio - Mathf.Pow(10f, 6f / 20f)) < .08f && Results[2].listenerPeak < .00001f;
                bool noCombinedClipping = Results.Where(value => value.name.StartsWith("combined-coincident-", StringComparison.Ordinal))
                    .All(value => value.listenerPeak < .9999f);
                bool continuous = Results.Last().name == "prepared-starter-loop-two-seams" &&
                    Results.Last().dspSeconds >= 6d && Results.Last().longestQuietCaptureRunSeconds < .08d;
                Finish(device && calibrated ? noCombinedClipping ? continuous ? 0 : 6 : 5 : 2, device && calibrated
                    ? "Device PCM, +6dB output and mute captured; coincident layers unclipped=" + noCombinedClipping +
                        "; prepared starter two seams continuous=" + continuous + "; listening acceptance is separate."
                    : "Device PCM/gain/mute control failed; not a passed output check.");
            }
            catch (Exception exception) { Finish(3, exception.ToString()); }
        }

        private static double Db(double sum) => 10d * Math.Log10(Math.Max(1e-12d, sum / Math.Max(1, count)));

        private static void Finish(int exitCode, string message)
        {
            SessionState.EraseString(SessionKey);
            SessionState.EraseString(StartedKey);
            EditorApplication.update -= Update;
            if (backend != null) backend.StopAll();
            File.WriteAllText(output, JsonUtility.ToJson(new Report { unityVersion = Application.unityVersion,
                exitCode = exitCode, message = message, samples = Results.ToArray() }, true));
            Debug.Log("SATSUMA_AUDIO_OUTPUT_PROBE exit=" + exitCode + " report=" + output + " " + message);
            EditorApplication.Exit(exitCode);
        }

        private sealed class Stage
        {
            public readonly string Name;
            public readonly UnityAudioEventDefinition Definition;
            public readonly float Distance, Gain, Pitch, Master;
            public UnityAudioEventDefinition[] OtherDefinitions = Array.Empty<UnityAudioEventDefinition>();
            public float[] OtherGains = Array.Empty<float>();
            public float[] OtherPitches = Array.Empty<float>();
            public float DurationSeconds;
            public Stage(string name, UnityAudioEventDefinition definition, float distance, float gain, float pitch, float master = 1f)
            { Name = name; Definition = definition; Distance = distance; Gain = gain; Pitch = pitch; Master = master; }
        }
        [Serializable] private sealed class Report
        { public string unityVersion, message; public int exitCode; public Sample[] samples; }
        [Serializable] private sealed class Sample
        {
            public string name, clip;
            public float distanceMeters, sourceVolume, contentGainDb, mixGainDb, mixBoostCeiling, outputGainCeiling, parameterGain, sourcePitch, sourcePeak,
                listenerPeak, overflowGain, listenerVolume;
            public bool listenerPaused;
            public double sourceRmsDbfs, listenerRmsDbfs, dspSeconds, longestQuietCaptureRunSeconds;
            public int captures;
        }
    }
}
