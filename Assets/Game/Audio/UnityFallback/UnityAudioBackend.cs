#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine.Networking;
#endif
using MSC.Audio;
using UnityEngine;

namespace MSC.Audio.UnityFallback
{
    /// <summary>
    /// Temporary diagnostic vehicle mix. Donor clips are loaded read-only from
    /// ignored external staging in the Editor and are never serialized into Assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityAudioBackend : MonoBehaviour, IVehicleAudioBackend
    {
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.65f;
        [SerializeField, Min(0.1f)] private float volumeResponsePerSecond = 4f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.9f;
        [SerializeField, Min(0.1f)] private float minimumDistanceMeters = 1.5f;
        [SerializeField, Min(1f)] private float maximumDistanceMeters = 45f;

        private AudioSource idleSource;
        private AudioSource middleSource;
        private AudioSource highSource;
        private AudioSource starterMotorLoopSource;
        private AudioSource starterWhineLoopSource;
        private AudioSource starterOneShotSource;
        private AudioClip[] loadedClips;
        private VehicleAudioParameters parameters = VehicleAudioParameters.Silent;
        private bool pendingStarterEngaged;
        private bool pendingEngineStarted;
        private bool starterSequenceActive;

        public bool IsReady { get; private set; }

        public string FailureReason { get; private set; } = string.Empty;

        private void Awake()
        {
            idleSource = CreateSource(true);
            middleSource = CreateSource(true);
            highSource = CreateSource(true);
            starterMotorLoopSource = CreateSource(true);
            starterWhineLoopSource = CreateSource(true);
            starterOneShotSource = CreateSource(false);
        }

#if UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return LoadLocalDiagnosticClips();
        }
#else
        private void Start()
        {
            Fail("Local donor diagnostic clips are intentionally disabled outside the Unity Editor.");
        }
#endif

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            float deltaTime = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            bool running = parameters.EngineState == VehicleAudioEngineState.Running;
            bool cranking = starterSequenceActive &&
                            parameters.EngineState == VehicleAudioEngineState.Cranking;
            float rpm01 = Mathf.Clamp01(parameters.EngineRpm / parameters.RedlineRpm);
            float lowWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.46f, rpm01));
            float highWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.82f, rpm01));
            float middleWeight = Mathf.Max(0f, 1f - lowWeight - highWeight);
            float load = Mathf.Max(parameters.EngineLoad01, parameters.Throttle01 * 0.7f);
            float engineGain = running ? masterVolume * Mathf.Lerp(0.55f, 1f, load) : 0f;

            SetVolume(idleSource, engineGain * lowWeight, deltaTime);
            SetVolume(middleSource, engineGain * middleWeight, deltaTime);
            SetVolume(highSource, engineGain * highWeight, deltaTime);
            SetVolume(starterMotorLoopSource, cranking ? masterVolume * 0.56f : 0f, deltaTime);
            SetVolume(starterWhineLoopSource, cranking ? masterVolume * 0.38f : 0f, deltaTime);

            idleSource.pitch = Mathf.Lerp(0.72f, 1.35f, Mathf.InverseLerp(500f, 1900f, parameters.EngineRpm));
            middleSource.pitch = Mathf.Lerp(0.72f, 1.38f, Mathf.InverseLerp(1200f, 4800f, parameters.EngineRpm));
            highSource.pitch = Mathf.Lerp(0.72f, 1.4f, Mathf.InverseLerp(3200f, parameters.RedlineRpm, parameters.EngineRpm));
            float starterPitch = Mathf.Lerp(
                0.9f,
                1.08f,
                Mathf.InverseLerp(0f, 700f, parameters.EngineRpm));
            starterMotorLoopSource.pitch = starterPitch;
            starterWhineLoopSource.pitch = starterPitch;
        }

        private void OnDestroy()
        {
            if (loadedClips == null)
            {
                return;
            }

            for (int index = 0; index < loadedClips.Length; index++)
            {
                if (loadedClips[index] != null)
                {
                    Destroy(loadedClips[index]);
                }
            }
        }

        public void SetVehicleParameters(in VehicleAudioParameters value)
        {
            parameters = value;
        }

        public void PostVehicleEvent(VehicleAudioEvent audioEvent)
        {
            switch (audioEvent)
            {
                case VehicleAudioEvent.StarterEngaged:
                    starterSequenceActive = true;
                    pendingEngineStarted = false;
                    if (IsReady)
                    {
                        PlayStarterOneShot(3);
                    }
                    else
                    {
                        pendingStarterEngaged = true;
                    }
                    break;
                case VehicleAudioEvent.StarterDisengaged:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    break;
                case VehicleAudioEvent.EngineStarted:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    if (IsReady)
                    {
                        PlayStarterOneShot(5);
                    }
                    else
                    {
                        pendingEngineStarted = true;
                    }
                    break;
                case VehicleAudioEvent.EngineStopped:
                case VehicleAudioEvent.EngineStalled:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    pendingEngineStarted = false;
                    break;
                case VehicleAudioEvent.Reset:
                    parameters = VehicleAudioParameters.Silent;
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    pendingEngineStarted = false;
                    StopImmediately();
                    break;
            }
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0.15f;
            source.minDistance = minimumDistanceMeters;
            source.maxDistance = Mathf.Max(minimumDistanceMeters, maximumDistanceMeters);
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private void SetVolume(AudioSource source, float target, float deltaTime)
        {
            target = Mathf.Clamp01(target);
            if (target > 0.0001f && !source.isPlaying)
            {
                source.Play();
            }

            source.volume = Mathf.MoveTowards(
                source.volume,
                target,
                volumeResponsePerSecond * deltaTime);
            if (target <= 0.0001f && source.volume <= 0.0001f && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void PlayStarterOneShot(int clipIndex)
        {
            if (!IsReady || loadedClips == null)
            {
                return;
            }

            starterOneShotSource.PlayOneShot(loadedClips[clipIndex], masterVolume * 0.8f);
        }

        private void StopImmediately()
        {
            if (idleSource == null)
            {
                return;
            }

            idleSource.volume = 0f;
            middleSource.volume = 0f;
            highSource.volume = 0f;
            starterMotorLoopSource.volume = 0f;
            starterWhineLoopSource.volume = 0f;
            idleSource.Stop();
            middleSource.Stop();
            highSource.Stop();
            starterMotorLoopSource.Stop();
            starterWhineLoopSource.Stop();
            starterOneShotSource.Stop();
        }

        private void Fail(string reason)
        {
            IsReady = false;
            FailureReason = reason;
            StopImmediately();
            Debug.LogWarning(
                "M06 local diagnostic vehicle audio is unavailable: " + reason +
                " Simulation continues with a silent fallback.",
                this);
        }

#if UNITY_EDITOR
        private const string DonorPathConfiguration = "Config/DonorPaths.local.json";
        private const string AudioRootRelative =
            "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/AudioClip";

        private static readonly DiagnosticClipSpec[] ClipSpecs =
        {
            new DiagnosticClipSpec("850_idle5.ogg", "41296478D8828AF8E7840FE66F9FE2C9A0F05D78127B2BFB20ED35C403894CF9"),
            new DiagnosticClipSpec("850_mid3.ogg", "EF0F7E94F7FB08B1D8F1EA16AEE7BDDE5A27F54FB12FB6357A26DB11493F1FA5"),
            new DiagnosticClipSpec("850_mid13.ogg", "464DA9DBCE2219040522A18B481B4E5C1C285303152F56975D167E5F54044480"),
            new DiagnosticClipSpec("motor_start_1.ogg", "5F9EEB889C660E7AE1F08F9474951ECB3938878AE2A5C13038952A6DDD5892AE"),
            new DiagnosticClipSpec("motor_start_2.ogg", "854EA1EECC2E9DBFC37674CA0968F3FFACE2F75AE8B85A73F06C40FD1198F5AB"),
            new DiagnosticClipSpec("motor_start_3.ogg", "9D9ABE53A8C253E8C571FE1E99B8B34509B2944D6428FBFCA8412413FE551CD2"),
            new DiagnosticClipSpec("starter_whine.ogg", "215329D05D5C5C1BE5F0AE1B831AA20E01A02DD0418856C56808DE52CD310CE3")
        };

        private IEnumerator LoadLocalDiagnosticClips()
        {
            string[] resolvedFiles;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string configurationPath = ResolveContainedPath(projectRoot, DonorPathConfiguration);
                if (!File.Exists(configurationPath))
                {
                    Fail("Config/DonorPaths.local.json is missing.");
                    yield break;
                }

                DonorPathsData localPaths = JsonUtility.FromJson<DonorPathsData>(
                    File.ReadAllText(configurationPath));
                if (localPaths == null || string.IsNullOrWhiteSpace(localPaths.DonorStagingDirectory))
                {
                    Fail("DonorStagingDirectory is missing from the local path configuration.");
                    yield break;
                }

                string stagingRoot = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(localPaths.DonorStagingDirectory.Trim()));
                if (!Directory.Exists(stagingRoot))
                {
                    Fail("The configured donor staging directory does not exist.");
                    yield break;
                }

                string audioRoot = ResolveContainedPath(stagingRoot, AudioRootRelative);
                resolvedFiles = new string[ClipSpecs.Length];
                for (int index = 0; index < ClipSpecs.Length; index++)
                {
                    DiagnosticClipSpec spec = ClipSpecs[index];
                    string clipPath = ResolveContainedPath(audioRoot, spec.FileName);
                    if (!File.Exists(clipPath))
                    {
                        Fail("Required staged clip is missing: " + spec.FileName + ".");
                        yield break;
                    }

                    string actualHash = ComputeSha256(clipPath);
                    if (!string.Equals(actualHash, spec.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Fail("SHA-256 mismatch for staged clip: " + spec.FileName + ".");
                        yield break;
                    }

                    resolvedFiles[index] = clipPath;
                }
            }
            catch (Exception exception)
            {
                Fail("Local path or hash validation failed: " + exception.Message);
                yield break;
            }

            loadedClips = new AudioClip[ClipSpecs.Length];
            for (int index = 0; index < resolvedFiles.Length; index++)
            {
                string clipUri = new Uri(resolvedFiles[index]).AbsoluteUri;
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                           clipUri,
                           AudioType.OGGVORBIS))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Fail("OGG loading failed for " + ClipSpecs[index].FileName + ": " + request.error);
                        yield break;
                    }

                    loadedClips[index] = DownloadHandlerAudioClip.GetContent(request);
                    loadedClips[index].name = "LocalDiagnostic_" + ClipSpecs[index].FileName;
                }
            }

            idleSource.clip = loadedClips[0];
            middleSource.clip = loadedClips[1];
            highSource.clip = loadedClips[2];
            starterMotorLoopSource.clip = loadedClips[4];
            starterWhineLoopSource.clip = loadedClips[6];
            idleSource.Play();
            middleSource.Play();
            highSource.Play();
            starterMotorLoopSource.Play();
            starterWhineLoopSource.Play();
            FailureReason = string.Empty;
            IsReady = true;
            Debug.Log(
                $"M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips={ClipSpecs.Length} " +
                "source=ExternalDonorStaging hashes=Verified",
                this);
            if (pendingStarterEngaged)
            {
                pendingStarterEngaged = false;
                PlayStarterOneShot(3);
            }
            else if (pendingEngineStarted)
            {
                pendingEngineStarted = false;
                PlayStarterOneShot(5);
            }
        }

        private static string ResolveContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException("A contained path must use a non-empty relative path.");
            }

            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string portableRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, portableRelative));
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A local diagnostic path resolves outside its configured root.");
            }

            return candidate;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        [Serializable]
        private sealed class DonorPathsData
        {
            public string DonorStagingDirectory = string.Empty;
        }

        private readonly struct DiagnosticClipSpec
        {
            public DiagnosticClipSpec(string fileName, string sha256)
            {
                FileName = fileName;
                Sha256 = sha256;
            }

            public string FileName { get; }
            public string Sha256 { get; }
        }
#endif
    }
}
