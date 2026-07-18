using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using MSC.Audio;
using MSC.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WeatherProduction
{
    public sealed class AudioPerformanceCapturePlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string EvidenceRelativePath =
            "PerformanceCaptures/Milestone08/M08_Audio_Performance.json";

        [UnityTest]
        [Category("PerformanceCapture")]
        public IEnumerator LiveWwise_RecordsBoundedAudioPerformanceEvidence()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("MSC_CAPTURE_M08_AUDIO_PERF"),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Set MSC_CAPTURE_M08_AUDIO_PERF=1 and use " +
                    "-wwiseEnableWithNoGraphics for the explicit M08 capture.");
            }

            yield return DestroyPersistentRootIfPresent();
            long processBytesBeforeBootstrap = GetProcessWorkingSetBytes();
            long unityAllocatedBeforeBootstrap =
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            ProductionWorldStreamingInstaller installer = null;
            for (int frame = 0; frame < 900; frame++)
            {
                installer = Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(FindObjectsInactive.Include);
                if (installer != null && installer.IsReady)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(installer, Is.Not.Null);
            Assert.That(installer.IsReady, Is.True);

            AudioBackendRouter router = Object.FindFirstObjectByType<AudioBackendRouter>(
                FindObjectsInactive.Include);
            Assert.That(router, Is.Not.Null);
            for (int frame = 0; frame < 360 && router.Kind != AudioBackendKind.Wwise; frame++)
            {
                yield return null;
            }

            AudioRuntimeSnapshot baseline = router.CaptureSnapshot();
            Assert.That(baseline.Kind, Is.EqualTo(AudioBackendKind.Wwise));
            Assert.That(baseline.LoadedBankCount, Is.EqualTo(6));
            Assert.That(baseline.MissingBanks, Is.Empty);

            GameObject probeObject = new GameObject("M08_AudioPerformanceProbe");
            probeObject.SetActive(false);
            AudioEmitterAuthoring emitter =
                probeObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.performance.m08", router, probeObject.transform);
            VehicleAudioEmitterBackend vehicle =
                probeObject.AddComponent<VehicleAudioEmitterBackend>();
            vehicle.Configure(router, emitter);
            probeObject.SetActive(true);
            yield return null;
            Assert.That(vehicle.TryInitialize(out string vehicleFailure), Is.True, vehicleFailure);

            var warmupParameters = CreateVehicleParameters(0);
            vehicle.SetVehicleParameters(in warmupParameters);
            IAudioEventHandle auditionHandle = router.PostEvent(new AudioEventRequest(
                AudioProjectIds.Events.InteractionPickup,
                emitter,
                allowMultiple: true));
            Assert.That(auditionHandle.IsValid, Is.True, router.FailureReason);
            yield return null;

            AudioRuntimeSnapshot active = router.CaptureSnapshot();
            Assert.That(active.ActiveVoiceCount, Is.GreaterThan(baseline.ActiveVoiceCount));
            Assert.That(active.RegisteredEmitterCount, Is.EqualTo(baseline.RegisteredEmitterCount + 1));

            TimingCapture globalRtpc = Measure(
                512,
                index => router.SetParameter(
                    AudioProjectIds.Parameters.LightningIntensity,
                    (index & 1) == 0 ? 0.25f : 0.75f));
            yield return null;
            TimingCapture vehicleUpdate = Measure(
                64,
                index =>
                {
                    VehicleAudioParameters parameters = CreateVehicleParameters(index);
                    vehicle.SetVehicleParameters(in parameters);
                });
            yield return null;
            TimingCapture weatherUpdate = Measure(
                128,
                index =>
                {
                    float value = (index & 1) == 0 ? 0.2f : 0.8f;
                    router.SetParameter(AudioProjectIds.Parameters.WeatherPrecipitation, value);
                    router.SetParameter(AudioProjectIds.Parameters.WeatherWind, 1f - value);
                    router.SetParameter(AudioProjectIds.Parameters.WeatherThunderRisk, value);
                    router.SetSwitch(
                        AudioProjectIds.Switches.WeatherPrecipitationGroup,
                        (index & 1) == 0
                            ? AudioProjectIds.Switches.WeatherPrecipitationDrizzle
                            : AudioProjectIds.Switches.WeatherPrecipitationRain);
                });

            auditionHandle.Stop(0f);
            auditionHandle.Dispose();
            vehicle.PostVehicleEvent(VehicleAudioEvent.Reset);
            Object.Destroy(probeObject);
            yield return null;
            yield return null;

            AudioRuntimeSnapshot cleaned = router.CaptureSnapshot();
            Assert.That(cleaned.RegisteredEmitterCount, Is.EqualTo(baseline.RegisteredEmitterCount));
            Assert.That(cleaned.ActiveVoiceCount, Is.LessThanOrEqualTo(baseline.ActiveVoiceCount));

            var evidence = new AudioPerformanceEvidence
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                wwiseSdk = "2025.1.9.9197",
                backendId = baseline.BackendId,
                mode = "Editor PlayMode batch with -wwiseEnableWithNoGraphics",
                audioThreadCpuAvailable = false,
                audioThreadCpuLimitation =
                    "Wwise suspends the audio output thread in this headless batch capture; " +
                    "real-device Wwise Profiler CPU remains a manual follow-up.",
                occlusionRaycastBudgetPerFrame = 0,
                loadedBankCount = baseline.LoadedBankCount,
                generatedBankBytes = SumGeneratedBankBytes(),
                baselineEmitterCount = baseline.RegisteredEmitterCount,
                baselineActiveVoiceCount = baseline.ActiveVoiceCount,
                peakObservedEmitterCount = active.RegisteredEmitterCount,
                peakObservedActiveVoiceCount = active.ActiveVoiceCount,
                cleanupEmitterCount = cleaned.RegisteredEmitterCount,
                cleanupActiveVoiceCount = cleaned.ActiveVoiceCount,
                processWorkingSetAvailable = processBytesBeforeBootstrap > 0L,
                processWorkingSetLimitation = processBytesBeforeBootstrap > 0L
                    ? string.Empty
                    : "System.Diagnostics.Process.WorkingSet64 returned zero in Unity batch mode; " +
                      "Unity Profiler allocated-memory counters are recorded instead.",
                processWorkingSetBytesBeforeBootstrap = processBytesBeforeBootstrap,
                processWorkingSetBytesAfterBootstrap = GetProcessWorkingSetBytes(),
                unityAllocatedBytesBeforeBootstrap = unityAllocatedBeforeBootstrap,
                unityAllocatedBytesAfterCapture =
                    UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                globalRtpcUpdate = globalRtpc,
                vehicleParameterUpdate = vehicleUpdate,
                weatherParameterBatch = weatherUpdate,
            };
            WriteEvidence(evidence);

            Assert.That(globalRtpc.microsecondsPerIteration, Is.LessThan(1000d));
            Assert.That(vehicleUpdate.microsecondsPerIteration, Is.LessThan(2000d));
            Assert.That(weatherUpdate.microsecondsPerIteration, Is.LessThan(2000d));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyPersistentRootIfPresent();
        }

        private static VehicleAudioParameters CreateVehicleParameters(int index)
        {
            float phase = (index & 1) == 0 ? 0.35f : 0.75f;
            var supplemental = new VehicleAudioSupplementalParameters(
                ignitionOn: true,
                starterRequested: false,
                starterActive: false,
                brake01: 1f - phase,
                aggregateWheelSpeedRadiansPerSecond: 20f + (30f * phase),
                signedVehicleSpeedMetersPerSecond: 5f + (15f * phase),
                suspensionImpact01: 0.1f * phase,
                damageAvailable: true,
                damage01: 0.2f,
                interiorContextAvailable: true,
                interiorBlend01: 0.25f,
                openingsContextAvailable: true,
                doorOpenness01: 0.1f,
                windowOpenness01: 0.2f);
            return new VehicleAudioParameters(
                VehicleAudioEngineState.Running,
                1200f + (4200f * phase),
                7000f,
                phase,
                phase,
                3,
                200f * phase,
                5f + (15f * phase),
                0.15f * phase,
                VehicleAudioSurface.Gravel,
                13.8f,
                90f,
                supplemental);
        }

        private static TimingCapture Measure(int iterations, Action<int> operation)
        {
            int warmupCount = Math.Min(8, iterations);
            for (int index = 0; index < warmupCount; index++)
            {
                operation(index);
            }

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int index = 0; index < iterations; index++)
            {
                operation(index);
            }

            long stopped = Stopwatch.GetTimestamp();
            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double totalMicroseconds =
                (stopped - started) * 1_000_000d / Stopwatch.Frequency;
            return new TimingCapture
            {
                iterations = iterations,
                totalMilliseconds = totalMicroseconds / 1000d,
                microsecondsPerIteration = totalMicroseconds / iterations,
                allocatedBytes = allocatedBytes,
                allocatedBytesPerIteration = (double)allocatedBytes / iterations,
            };
        }

        private static long SumGeneratedBankBytes()
        {
            string bankDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "MySummerCar_Remake_WwiseProject",
                "GeneratedSoundBanks",
                "Windows");
            long bytes = 0L;
            string[] banks = Directory.GetFiles(bankDirectory, "*.bnk", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < banks.Length; index++)
            {
                bytes += new FileInfo(banks[index]).Length;
            }

            return bytes;
        }

        private static long GetProcessWorkingSetBytes()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                return process.WorkingSet64;
            }
        }

        private static void WriteEvidence(AudioPerformanceEvidence evidence)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, EvidenceRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
        }

        private static IEnumerator DestroyPersistentRootIfPresent()
        {
            GameCompositionRoot[] roots = Object.FindObjectsByType<GameCompositionRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < roots.Length; index++)
            {
                Object.Destroy(roots[index].gameObject);
            }

            if (roots.Length > 0)
            {
                yield return null;
                yield return null;
            }
        }

        [Serializable]
        private sealed class AudioPerformanceEvidence
        {
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string wwiseSdk = string.Empty;
            public string backendId = string.Empty;
            public string mode = string.Empty;
            public bool audioThreadCpuAvailable;
            public string audioThreadCpuLimitation = string.Empty;
            public int occlusionRaycastBudgetPerFrame;
            public int loadedBankCount;
            public long generatedBankBytes;
            public int baselineEmitterCount;
            public int baselineActiveVoiceCount;
            public int peakObservedEmitterCount;
            public int peakObservedActiveVoiceCount;
            public int cleanupEmitterCount;
            public int cleanupActiveVoiceCount;
            public bool processWorkingSetAvailable;
            public string processWorkingSetLimitation = string.Empty;
            public long processWorkingSetBytesBeforeBootstrap;
            public long processWorkingSetBytesAfterBootstrap;
            public long unityAllocatedBytesBeforeBootstrap;
            public long unityAllocatedBytesAfterCapture;
            public TimingCapture globalRtpcUpdate = new TimingCapture();
            public TimingCapture vehicleParameterUpdate = new TimingCapture();
            public TimingCapture weatherParameterBatch = new TimingCapture();
        }

        [Serializable]
        private sealed class TimingCapture
        {
            public int iterations;
            public double totalMilliseconds;
            public double microsecondsPerIteration;
            public long allocatedBytes;
            public double allocatedBytesPerIteration;
        }
    }
}
