using System;
using System.Collections;
using System.Collections.Generic;
using Enviro;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration
{
    /// <summary>
    /// Narrow runtime-only Enviro 3 presentation boundary. It owns isolated module
    /// clones and never writes into the vendor configuration or preset assets.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class Enviro3EnvironmentAdapter : MonoBehaviour,
        IEnvironmentPresentationAdapter,
        IEnvironmentPresentationCameraTarget
    {
        private const string MissingReferenceCode = "ENVIRO3-ATTACH-001";
        private const string MissingModuleCode = "ENVIRO3-ATTACH-002";
        private const string DuplicateManagerCode = "ENVIRO3-ATTACH-003";
        private const string IsolationFailureCode = "ENVIRO3-ISOLATION-001";
        private const string StartupPendingCode = "ENVIRO3-LIFECYCLE-001";
        private const string UnknownBindingCode = "ENVIRO3-BINDING-001";
        private const string StaleRevisionCode = "ENVIRO3-FRAME-001";
        private const string TransitionApproximationCode = "ENVIRO3-FRAME-002";
        private const string RefreshUnsupportedCode = "ENVIRO3-FRAME-003";
        private const string LightningIntensityApproximationCode = "ENVIRO3-FRAME-004";
        private const string VendorCallFailureCode = "ENVIRO3-RUNTIME-001";
        private const string RequiredRainEffectName = "Rain";
        private const float TransitionSettledRate = 4.60517019f;
        private const float MinimumTransitionRate = 0.0001f;
        private const float MaximumTransitionRate = 1000f;
        private const float RefreshCooldownSeconds = 1f;

        private const EnvironmentPresentationCapabilities SupportedCapabilities =
            EnvironmentPresentationCapabilities.TimeOfDay |
            EnvironmentPresentationCapabilities.Sky |
            EnvironmentPresentationCapabilities.SunMoonLighting |
            EnvironmentPresentationCapabilities.Clouds |
            EnvironmentPresentationCapabilities.Precipitation |
            EnvironmentPresentationCapabilities.Fog |
            EnvironmentPresentationCapabilities.Wind |
            EnvironmentPresentationCapabilities.LightningVisual |
            EnvironmentPresentationCapabilities.EnvironmentRefresh |
            EnvironmentPresentationCapabilities.QualityTiers;

        [Header("Direct scene references")]
        [SerializeField] private EnviroManager manager;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private Enviro3EnvironmentBindings bindings;
        [SerializeField] private Transform lightningOrigin;
        [SerializeField] private Transform lightningTarget;
        [SerializeField] private bool attachOnStart = true;

        private readonly List<EnvironmentPresentationDiagnostic> diagnostics =
            new List<EnvironmentPresentationDiagnostic>();
        private readonly List<UnityEngine.Object> ownedRuntimeObjects =
            new List<UnityEngine.Object>();

        private EnviroConfiguration runtimeConfiguration;
        private EnviroWeatherType runtimeDrizzle;
        private EnviroWeatherType runtimeRain;
        private EnviroWeatherType runtimeHeavyRain;
        private EnviroWeatherType runtimeStorm;
        private Enviro.Lightning runtimeLightningPrefab;
        private Material sourceLightningFlashMaterial;
        private Material runtimeLightningFlashMaterial;
        private Camera previousCamera;
        private Coroutine delayedAttachRoutine;
        private Coroutine delayedRefreshRoutine;
        private bool runtimeIsolationPrepared;
        private bool startupBarrierPassed;
        private bool attachRequested;
        private bool isAttached;
        private bool hasAppliedWeatherBinding;
        private bool hasAppliedPrecipitationIntensity;
        private bool hasAppliedQualityTier;
        private bool hasAppliedDateTime;
        private ulong lastAppliedRevision;
        private uint lastLightningSequence;
        private uint lastRefreshSequence;
        private uint pendingRefreshSequence;
        private EnvironmentRefreshTarget pendingRefreshTargets;
        private EnvironmentBindingId lastWeatherBindingId;
        private EnviroWeatherType lastPrecipitationWeather;
        private float lastPrecipitationIntensity;
        private EnvironmentQualityTier lastQualityTier;
        private int lastYear;
        private int lastMonth;
        private int lastDay;
        private int lastTimeOfDaySeconds;
        private float nextRefreshAllowedRealtime;
        private EnvironmentPresentationStatus status = EnvironmentPresentationStatus.Detached;

        public bool IsAttached => isAttached;

        public EnvironmentPresentationCapabilities Capabilities =>
            isAttached ? SupportedCapabilities : EnvironmentPresentationCapabilities.None;

        public EnvironmentPresentationStatus Status => status;

        public IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics => diagnostics;

        public void ConfigureForAuthoring(
            EnviroManager authoredManager,
            Camera authoredCamera,
            Enviro3EnvironmentBindings authoredBindings,
            Transform authoredLightningOrigin,
            Transform authoredLightningTarget)
        {
            manager = authoredManager;
            presentationCamera = authoredCamera;
            bindings = authoredBindings;
            lightningOrigin = authoredLightningOrigin;
            lightningTarget = authoredLightningTarget;
            attachOnStart = true;
        }

        public bool TrySetPresentationCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            presentationCamera = camera;
            if (isAttached && manager != null && manager.Camera != camera)
            {
                manager.ChangeCamera(camera);
            }

            return true;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            runtimeIsolationPrepared = TryPrepareRuntimeIsolation();
            if (!runtimeIsolationPrepared)
            {
                FailClosedManager();
            }
        }

        private IEnumerator Start()
        {
            if (!Application.isPlaying || !runtimeIsolationPrepared)
            {
                yield break;
            }

            // EnviroManager.Start clones all module ScriptableObjects a second time.
            // Waiting one frame guarantees those live clones exist before attachment.
            yield return null;
            startupBarrierPassed = true;
            CaptureLiveManagerModules();
            EnsureRuntimeLightningPrefab();
            ApplyAutonomyAndAudioSafety();

            if (attachOnStart || attachRequested)
            {
                Attach();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !startupBarrierPassed ||
                (!attachOnStart && !attachRequested))
            {
                return;
            }

            delayedAttachRoutine = StartCoroutine(AttachAfterManagerEnable());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (delayedAttachRoutine != null)
            {
                StopCoroutine(delayedAttachRoutine);
                delayedAttachRoutine = null;
            }

            Detach();
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Detach();
            TearDownRuntimeIsolation();
        }

        public EnvironmentPresentationStatus Attach()
        {
            attachRequested = true;
            if (isAttached)
            {
                return status;
            }

            diagnostics.Clear();
            if (!Application.isPlaying)
            {
                AddError(MissingReferenceCode, "Enviro integration can attach only in Play Mode.");
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }

            if (!runtimeIsolationPrepared)
            {
                AddError(IsolationFailureCode, "The isolated runtime Enviro configuration was not prepared.");
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }

            if (!startupBarrierPassed)
            {
                AddWarning(
                    StartupPendingCode,
                    "Attachment is queued until EnviroManager.Start has produced its live module clones.");
                // Detached deliberately reports IsOperational=false while the request is queued.
                return SetStatus(EnvironmentPresentationState.Detached, false);
            }

            CaptureLiveManagerModules();
            EnsureRuntimeLightningPrefab();
            ValidateAttachRequirements();
            if (HasErrors())
            {
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }

            try
            {
                CreateRuntimeWeatherBindings();
                previousCamera = manager.Camera;
                if (manager.Camera != presentationCamera)
                {
                    manager.ChangeCamera(presentationCamera);
                }

                ApplyAutonomyAndAudioSafety();
                isAttached = true;
                ResetAppliedState();
                return SetStatus(EnvironmentPresentationState.Ready, true);
            }
            catch (Exception exception)
            {
                DestroyRuntimeWeatherBindings();
                AddError(
                    VendorCallFailureCode,
                    "Enviro attachment failed: " + exception.GetType().Name + ".");
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }
        }

        public EnvironmentPresentationStatus Present(in EnvironmentPresentationFrame frame)
        {
            diagnostics.Clear();
            if (!isAttached)
            {
                AddError(MissingReferenceCode, "Present was called while the Enviro adapter was detached.");
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }

            CopyDiagnostics(EnvironmentPresentationValidator.Validate(frame));
            if (HasErrors())
            {
                return SetStatus(EnvironmentPresentationState.Faulted, true);
            }

            ApplyAutonomyAndAudioSafety();
            if (!frame.Enabled)
            {
                return SetStatus(EnvironmentPresentationState.Disabled, true);
            }

            if (frame.Revision < lastAppliedRevision)
            {
                AddWarning(
                    StaleRevisionCode,
                    "A stale presentation revision was ignored.",
                    frame.BindingId);
                return SetStatus(EnvironmentPresentationState.Degraded, true);
            }

            if (frame.Revision == lastAppliedRevision)
            {
                return SetStatus(EnvironmentPresentationState.Ready, true);
            }

            if (!bindings.TryResolveWeather(
                    frame.BindingId,
                    out EnviroWeatherType weatherType,
                    out EnvironmentPresentationPresetKind presetKind))
            {
                AddError(
                    UnknownBindingCode,
                    "No direct Enviro weather asset is assigned for this stable binding ID.",
                    frame.BindingId);
                return SetStatus(EnvironmentPresentationState.Faulted, true);
            }

            if (!bindings.TryResolveQuality(frame.QualityTier, out EnviroQuality quality))
            {
                AddError(
                    UnknownBindingCode,
                    "No direct Enviro quality asset is assigned for the requested tier.",
                    frame.BindingId);
                return SetStatus(EnvironmentPresentationState.Faulted, true);
            }

            weatherType = ResolveRuntimeWeather(weatherType, frame.BindingId, presetKind);
            if (weatherType == null)
            {
                AddError(
                    UnknownBindingCode,
                    "The resolved Enviro weather binding has no isolated runtime clone.",
                    frame.BindingId);
                return SetStatus(EnvironmentPresentationState.Faulted, true);
            }

            try
            {
                ApplyPrecipitationIntensityIfDirty(
                    weatherType,
                    frame.PrecipitationIntensity01);

                if (!hasAppliedWeatherBinding || frame.BindingId != lastWeatherBindingId)
                {
                    ApplyWeatherBinding(
                        weatherType,
                        frame.BindingId,
                        frame.TransitionDurationSeconds);
                }

                if (!hasAppliedQualityTier || frame.QualityTier != lastQualityTier)
                {
                    ApplyQuality(frame.QualityTier, quality);
                }

                ApplyDateAndTimeIfDirty(frame);

                if (frame.LightningVisual.IsRequested &&
                    frame.LightningVisual.Sequence != lastLightningSequence)
                {
                    CastLightningVisual(frame.LightningVisual);
                    lastLightningSequence = frame.LightningVisual.Sequence;
                    if (!Mathf.Approximately(frame.LightningVisual.Intensity01, 1f))
                    {
                        AddWarning(
                            LightningIntensityApproximationCode,
                            "Enviro bolt intensity is approximated through the runtime flash-material color; the vendor-authored line, plane intensity and point-light animation keep their own curves.",
                            frame.BindingId);
                    }
                }

                AcceptEnvironmentRefresh(frame.EnvironmentRefresh, frame.BindingId);

                lastAppliedRevision = frame.Revision;
                ApplyAutonomyAndAudioSafety();
                return SetStatus(
                    diagnostics.Count == 0
                        ? EnvironmentPresentationState.Ready
                        : EnvironmentPresentationState.Degraded,
                    true);
            }
            catch (Exception exception)
            {
                AddError(
                    VendorCallFailureCode,
                    "Enviro rejected a presentation command: " + exception.GetType().Name + ".",
                    frame.BindingId);
                return SetStatus(EnvironmentPresentationState.Faulted, true);
            }
        }

        public void Detach()
        {
            CancelPendingRefresh();
            if (!isAttached)
            {
                status = EnvironmentPresentationStatus.Detached;
                return;
            }

            ApplyAutonomyAndAudioSafety();
            if (manager != null && manager.Weather != null &&
                IsRuntimeWeather(manager.Weather.targetWeatherType) &&
                bindings != null && bindings.Clear != null)
            {
                manager.Weather.ChangeWeatherInstant(bindings.Clear);
            }

            if (manager != null && manager.Camera != previousCamera)
            {
                manager.ChangeCamera(previousCamera);
            }

            isAttached = false;
            ResetAppliedState();
            previousCamera = null;
            DestroyRuntimeWeatherBindings();
            diagnostics.Clear();
            status = EnvironmentPresentationStatus.Detached;
        }

        private IEnumerator AttachAfterManagerEnable()
        {
            yield return null;
            delayedAttachRoutine = null;
            CaptureLiveManagerModules();
            ApplyAutonomyAndAudioSafety();
            Attach();
        }

        private bool TryPrepareRuntimeIsolation()
        {
            diagnostics.Clear();
            if (manager == null || bindings == null || bindings.SourceConfiguration == null)
            {
                AddError(
                    MissingReferenceCode,
                    "Manager, bindings, and a source Enviro configuration are required before Awake.");
                SetStatus(EnvironmentPresentationState.Faulted, false);
                return false;
            }

            try
            {
                EnviroConfiguration source = bindings.SourceConfiguration;
                runtimeConfiguration = CloneOwned(source);
                runtimeConfiguration.name = source.name + " (Runtime Isolated)";
                runtimeConfiguration.timeModule = CloneOwned(source.timeModule);
                runtimeConfiguration.lightingModule = CloneOwned(source.lightingModule);
                runtimeConfiguration.reflectionsModule = CloneOwned(source.reflectionsModule);
                runtimeConfiguration.Sky = CloneOwned(source.Sky);
                runtimeConfiguration.fogModule = CloneOwned(source.fogModule);
                runtimeConfiguration.volumetricCloudModule = CloneOwned(source.volumetricCloudModule);
                runtimeConfiguration.flatCloudModule = CloneOwned(source.flatCloudModule);
                runtimeConfiguration.Weather = CloneOwned(source.Weather);
                runtimeConfiguration.Aurora = CloneOwned(source.Aurora);
                runtimeConfiguration.Audio = CloneOwned(source.Audio);
                // The installed default configuration contains effect entries from the
                // optional weather pack, while the base Rain/Storm profiles target the
                // exact effect key "Rain". Use the explicitly bound base-package module
                // so precipitation remains deterministic without mutating vendor assets.
                runtimeConfiguration.Effects = CloneOwned(bindings.EffectsSource);
                runtimeConfiguration.Lightning = CloneOwned(source.Lightning);
                runtimeConfiguration.Quality = CloneOwned(source.Quality);
                runtimeConfiguration.Environment = CloneOwned(source.Environment);

                AssignConfigurationToManager(runtimeConfiguration);
                if (!ValidatePreEnableSafety())
                {
                    SetStatus(EnvironmentPresentationState.Faulted, false);
                    return false;
                }

                ApplyAutonomyAndAudioSafety();
                status = EnvironmentPresentationStatus.Detached;
                return true;
            }
            catch (Exception exception)
            {
                AddError(
                    IsolationFailureCode,
                    "Runtime configuration cloning failed: " + exception.GetType().Name + ".");
                SetStatus(EnvironmentPresentationState.Faulted, false);
                return false;
            }
        }

        private bool ValidatePreEnableSafety()
        {
            bool valid = true;
            valid &= RequireModule(runtimeConfiguration.timeModule, "Time");
            valid &= RequireModule(runtimeConfiguration.Sky, "Sky");
            valid &= RequireModule(runtimeConfiguration.lightingModule, "Lighting");
            valid &= RequireModule(runtimeConfiguration.fogModule, "Fog");
            valid &= RequireModule(runtimeConfiguration.volumetricCloudModule, "VolumetricClouds");
            valid &= RequireModule(runtimeConfiguration.Weather, "Weather");
            valid &= RequireModule(runtimeConfiguration.Effects, "Effects");
            valid &= RequireModule(runtimeConfiguration.Lightning, "Lightning");
            valid &= RequireModule(runtimeConfiguration.Quality, "Quality");
            valid &= RequireModule(runtimeConfiguration.Environment, "Environment");

            if (runtimeConfiguration.timeModule != null &&
                runtimeConfiguration.timeModule.Settings == null)
            {
                AddError(MissingModuleCode, "The cloned Time module has no Settings object.");
                valid = false;
            }

            if (runtimeConfiguration.Weather != null &&
                runtimeConfiguration.Weather.Settings == null)
            {
                AddError(MissingModuleCode, "The cloned Weather module has no Settings object.");
                valid = false;
            }

            if (runtimeConfiguration.Audio != null &&
                runtimeConfiguration.Audio.Settings == null)
            {
                AddError(MissingModuleCode, "The cloned Audio module has no Settings object.");
                valid = false;
            }

            if (runtimeConfiguration.Lightning != null &&
                runtimeConfiguration.Lightning.Settings == null)
            {
                AddError(MissingModuleCode, "The cloned Lightning module has no Settings object.");
                valid = false;
            }

            if (runtimeConfiguration.Quality != null &&
                runtimeConfiguration.Quality.Settings == null)
            {
                AddError(MissingModuleCode, "The cloned Quality module has no Settings object.");
                valid = false;
            }

            if (!HasCompatibleRainEffect(runtimeConfiguration.Effects))
            {
                AddError(
                    MissingModuleCode,
                    "The explicit Effects source must contain one usable exact 'Rain' effect binding.");
                valid = false;
            }

            return valid;
        }

        private static bool HasCompatibleRainEffect(EnviroEffectsModule effects)
        {
            if (effects == null || effects.Settings == null || effects.Settings.effectTypes == null)
            {
                return false;
            }

            int matchingEffects = 0;
            for (int index = 0; index < effects.Settings.effectTypes.Count; index++)
            {
                EnviroEffectTypes effect = effects.Settings.effectTypes[index];
                if (effect == null ||
                    !string.Equals(effect.name, RequiredRainEffectName, StringComparison.Ordinal))
                {
                    continue;
                }

                matchingEffects++;
                if (effect.prefab == null ||
                    effect.prefab.GetComponent<ParticleSystem>() == null ||
                    effect.maxEmission <= 0f)
                {
                    return false;
                }
            }

            return matchingEffects == 1;
        }

        private void ValidateAttachRequirements()
        {
            if (manager == null || presentationCamera == null || bindings == null ||
                lightningOrigin == null || lightningTarget == null)
            {
                AddError(
                    MissingReferenceCode,
                    "Manager, camera, bindings, and deterministic lightning anchors are required.");
            }

            EnviroManager[] managers = FindObjectsByType<EnviroManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (managers.Length != 1 || managers[0] != manager || EnviroManager.instance != manager)
            {
                AddError(
                    DuplicateManagerCode,
                    "WeatherLab requires exactly one explicit active EnviroManager instance.");
            }

            if (manager == null || manager.configuration != runtimeConfiguration)
            {
                AddError(
                    IsolationFailureCode,
                    "EnviroManager is not using the adapter-owned runtime configuration clone.");
                return;
            }

            if (ContainsSourceModuleReference())
            {
                AddError(
                    IsolationFailureCode,
                    "A live EnviroManager module still references a vendor configuration subasset.");
            }

            RequireModule(manager.Time, "Time");
            RequireModule(manager.Sky, "Sky");
            RequireModule(manager.Lighting, "Lighting");
            RequireModule(manager.Fog, "Fog");
            RequireModule(manager.VolumetricClouds, "VolumetricClouds");
            RequireModule(manager.Weather, "Weather");
            RequireModule(manager.Effects, "Effects");
            RequireModule(manager.Lightning, "Lightning");
            RequireModule(manager.Quality, "Quality");
            RequireModule(manager.Environment, "Environment");

            if (manager.Time != null && manager.Time.Settings == null ||
                manager.Weather != null && manager.Weather.Settings == null ||
                manager.Lightning != null && manager.Lightning.Settings == null ||
                manager.Quality != null && manager.Quality.Settings == null)
            {
                AddError(MissingModuleCode, "A required live Enviro module has no Settings object.");
            }

            if (manager.Lightning != null && manager.Lightning.Settings != null &&
                manager.Lightning.Settings.prefab == null)
            {
                AddError(MissingModuleCode, "The Lightning module has no visual bolt prefab.");
            }
            else if (manager.Lightning != null && manager.Lightning.Settings != null &&
                     manager.Lightning.Settings.prefab != null &&
                     manager.Lightning.Settings.prefab.planeMat == null)
            {
                AddError(MissingModuleCode, "The Lightning visual prefab has no flash material.");
            }

            if (bindings.Clear == null || bindings.PartlyCloudy == null ||
                bindings.Overcast == null || bindings.Rain == null ||
                bindings.Storm == null || bindings.Fog == null ||
                bindings.Low == null || bindings.High == null ||
                bindings.EffectsSource == null)
            {
                AddError(
                    MissingReferenceCode,
                    "All direct effects, clear/partly-cloudy/overcast/rain/storm/fog, and low/high assets must be assigned.");
            }
        }

        private bool RequireModule(ScriptableObject module, string moduleName)
        {
            if (module != null)
            {
                return true;
            }

            AddError(MissingModuleCode, "Required Enviro module is missing: " + moduleName + ".");
            return false;
        }

        private void ApplyWeatherBinding(
            EnviroWeatherType weatherType,
            EnvironmentBindingId bindingId,
            float transitionDurationSeconds)
        {
            if (transitionDurationSeconds <= 0f)
            {
                manager.Weather.ChangeWeatherInstant(weatherType);
            }
            else
            {
                ConfigureTransitionApproximation(transitionDurationSeconds);
                manager.Weather.ChangeWeather(weatherType);
                AddWarning(
                    TransitionApproximationCode,
                    "Enviro uses exponential per-field blending. The adapter maps the requested duration to an approximately 99% settled transition, so the exact finish time remains frame-rate and profile dependent.",
                    bindingId);
            }

            lastWeatherBindingId = bindingId;
            hasAppliedWeatherBinding = true;
        }

        /// <summary>
        /// Enviro applies Lerp(current, target, speed * deltaTime), not a finite-duration
        /// transition. A rate of -ln(0.01) / duration leaves about one percent residual
        /// at the requested duration in the continuous approximation.
        /// </summary>
        private void ConfigureTransitionApproximation(float durationSeconds)
        {
            float rate = Mathf.Clamp(
                TransitionSettledRate / durationSeconds,
                MinimumTransitionRate,
                MaximumTransitionRate);
            EnviroWeather settings = manager.Weather.Settings;
            settings.cloudsTransitionSpeed = rate;
            settings.fogTransitionSpeed = rate;
            settings.lightingTransitionSpeed = rate;
            settings.effectsTransitionSpeed = rate;
            settings.auroraTransitionSpeed = rate;
            settings.environmentTransitionSpeed = rate;
            settings.audioTransitionSpeed = rate;
        }

        private void ApplyQuality(EnvironmentQualityTier qualityTier, EnviroQuality quality)
        {
            manager.Quality.Settings.defaultQuality = quality;
            manager.Quality.UpdateModule();
            lastQualityTier = qualityTier;
            hasAppliedQualityTier = true;
        }

        private void ApplyDateAndTimeIfDirty(in EnvironmentPresentationFrame frame)
        {
            int totalSeconds = Mathf.Clamp(
                Mathf.FloorToInt(frame.NormalizedTimeOfDay01 * 86400f),
                0,
                86399);
            if (hasAppliedDateTime &&
                frame.Year == lastYear &&
                frame.Month == lastMonth &&
                frame.Day == lastDay &&
                totalSeconds == lastTimeOfDaySeconds)
            {
                return;
            }

            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            manager.Time.SetDateTime(
                seconds,
                minutes,
                hours,
                frame.Day,
                frame.Month,
                frame.Year);

            lastYear = frame.Year;
            lastMonth = frame.Month;
            lastDay = frame.Day;
            lastTimeOfDaySeconds = totalSeconds;
            hasAppliedDateTime = true;
        }

        private void ApplyAutonomyAndAudioSafety()
        {
            if (manager == null)
            {
                return;
            }

            if (manager.Time != null && manager.Time.Settings != null)
            {
                if (manager.Time.Settings.simulate)
                {
                    manager.Time.Settings.simulate = false;
                }
            }

            if (manager.Lightning != null && manager.Lightning.Settings != null)
            {
                if (manager.Lightning.Settings.lightningStorm)
                {
                    manager.Lightning.Settings.lightningStorm = false;
                }
            }

            if (manager.Fog != null && manager.Fog.Settings != null)
            {
                if (!manager.Fog.Settings.controlHDRPFog)
                {
                    manager.Fog.Settings.controlHDRPFog = true;
                }

                if (!manager.Fog.Settings.controlHDRPVolumetrics)
                {
                    manager.Fog.Settings.controlHDRPVolumetrics = true;
                }
            }

            if (manager.Reflections != null && manager.Reflections.Settings != null)
            {
                if (manager.Reflections.Settings.globalReflectionsUpdateOnGameTime)
                {
                    manager.Reflections.Settings.globalReflectionsUpdateOnGameTime = false;
                }

                if (manager.Reflections.Settings.globalReflectionsUpdateOnPosition)
                {
                    manager.Reflections.Settings.globalReflectionsUpdateOnPosition = false;
                }
            }

            SilenceAudio(manager.Audio);
        }

        private static void SilenceAudio(EnviroAudioModule audio)
        {
            if (audio == null || audio.Settings == null)
            {
                return;
            }

            if (!Mathf.Approximately(audio.Settings.ambientMasterVolume, 0f))
            {
                audio.Settings.ambientMasterVolume = 0f;
            }

            if (!Mathf.Approximately(audio.Settings.weatherMasterVolume, 0f))
            {
                audio.Settings.weatherMasterVolume = 0f;
            }

            if (!Mathf.Approximately(audio.Settings.thunderMasterVolume, 0f))
            {
                audio.Settings.thunderMasterVolume = 0f;
            }

            if (!Mathf.Approximately(audio.ambientVolumeModifier, 0f))
            {
                audio.ambientVolumeModifier = 0f;
            }

            if (!Mathf.Approximately(audio.weatherVolumeModifier, 0f))
            {
                audio.weatherVolumeModifier = 0f;
            }

            if (!Mathf.Approximately(audio.thunderVolumeModifier, 0f))
            {
                audio.thunderVolumeModifier = 0f;
            }
            SilenceClips(audio.Settings.ambientClips);
            SilenceClips(audio.Settings.weatherClips);
            SilenceClips(audio.Settings.thunderClips);
        }

        private static void SilenceClips(IList<EnviroAudioClip> clips)
        {
            if (clips == null)
            {
                return;
            }

            for (int index = 0; index < clips.Count; index++)
            {
                EnviroAudioClip clip = clips[index];
                if (clip == null)
                {
                    continue;
                }

                if (!Mathf.Approximately(clip.volume, 0f))
                {
                    clip.volume = 0f;
                }

                if (clip.myAudioSource != null)
                {
                    if (!Mathf.Approximately(clip.myAudioSource.volume, 0f))
                    {
                        clip.myAudioSource.volume = 0f;
                    }

                    if (clip.myAudioSource.isPlaying)
                    {
                        clip.myAudioSource.Stop();
                    }
                }
            }
        }

        private void CastLightningVisual()
        {
            CastLightningVisual(new EnvironmentLightningVisualRequest(
                true,
                1,
                lightningTarget.position,
                1f));
        }

        private bool EnsureRuntimeLightningPrefab()
        {
            if (runtimeLightningPrefab != null && manager != null &&
                manager.Lightning != null && manager.Lightning.Settings != null &&
                manager.Lightning.Settings.prefab == runtimeLightningPrefab)
            {
                return true;
            }

            Enviro.Lightning sourcePrefab = manager?.Lightning?.Settings?.prefab;
            if (sourcePrefab == null || sourcePrefab.planeMat == null)
            {
                AddError(
                    MissingModuleCode,
                    "The Lightning module requires a visual prefab with a flash material.");
                return false;
            }

            sourceLightningFlashMaterial = sourcePrefab.planeMat;
            runtimeLightningFlashMaterial = new Material(sourceLightningFlashMaterial)
            {
                name = sourceLightningFlashMaterial.name + " (Runtime Lightning Visual)",
                hideFlags = HideFlags.DontSave
            };

            var inactiveHost = new GameObject("Enviro Lightning Runtime Prefab Host")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            inactiveHost.SetActive(false);
            GameObject cloneObject = Instantiate(
                sourcePrefab.gameObject,
                inactiveHost.transform,
                worldPositionStays: false);
            cloneObject.name = sourcePrefab.gameObject.name + " (Runtime Isolated)";
            cloneObject.hideFlags = HideFlags.HideAndDontSave;
            runtimeLightningPrefab = cloneObject.GetComponent<Enviro.Lightning>();
            if (runtimeLightningPrefab == null)
            {
                Destroy(inactiveHost);
                Destroy(runtimeLightningFlashMaterial);
                runtimeLightningFlashMaterial = null;
                sourceLightningFlashMaterial = null;
                AddError(
                    MissingModuleCode,
                    "The cloned lightning prefab has no Enviro.Lightning component.");
                return false;
            }

            runtimeLightningPrefab.planeMat = runtimeLightningFlashMaterial;
            manager.Lightning.Settings.prefab = runtimeLightningPrefab;
            AddOwnedObject(inactiveHost);
            AddOwnedObject(runtimeLightningFlashMaterial);
            return true;
        }

        private void CastLightningVisual(in EnvironmentLightningVisualRequest request)
        {
            if (runtimeLightningPrefab == null ||
                manager.Lightning.Settings.prefab != runtimeLightningPrefab ||
                runtimeLightningFlashMaterial == null ||
                sourceLightningFlashMaterial == null)
            {
                throw new InvalidOperationException(
                    "The adapter-owned lightning prefab and flash material are unavailable.");
            }

            // The vendor bolt animates _Intensity on the supplied material. Reset the
            // one adapter-owned clone before reuse instead of allocating a new native
            // Material for every visual request or touching the paid prefab asset.
            runtimeLightningFlashMaterial.CopyPropertiesFromMaterial(
                sourceLightningFlashMaterial);

            EnviroAudioModule runtimeAudio = manager.Audio;
            Vector3 targetPosition = request.WorldPosition;
            Vector3 originPosition = targetPosition +
                                     (lightningOrigin.position - lightningTarget.position);
            ApplyLightningIntensityApproximation(
                runtimeLightningFlashMaterial,
                request.Intensity01);
            try
            {
                // Enviro instantiates the adapter-owned inactive runtime prefab. Audio is
                // removed for the duration of the public visual call so no vendor thunder
                // event starts. The paid prefab and its serialized component stay read-only.
                manager.Audio = null;
                manager.Lightning.CastLightningBolt(
                    originPosition,
                    targetPosition);
            }
            finally
            {
                manager.Audio = runtimeAudio;
            }
        }

        private static void ApplyLightningIntensityApproximation(
            Material material,
            float intensity01)
        {
            // The installed Enviro API hard-codes the bolt line, plane _Intensity and
            // point-light animation. Scaling supported color inputs on the runtime-only
            // flash material is the narrow approximation available without a vendor patch.
            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                material.SetColor("_BaseColor", color * intensity01);
            }

            if (material.HasProperty("_EmissiveColor"))
            {
                Color color = material.GetColor("_EmissiveColor");
                material.SetColor("_EmissiveColor", color * intensity01);
            }
        }

        private void CreateRuntimeWeatherBindings()
        {
            runtimeDrizzle = CreateRuntimeWeatherBinding(bindings.Rain, "Drizzle");
            runtimeRain = CreateRuntimeWeatherBinding(bindings.Rain, "Rain");
            runtimeHeavyRain = CreateRuntimeWeatherBinding(bindings.Rain, "Heavy Rain");
            runtimeStorm = CreateRuntimeWeatherBinding(bindings.Storm, "Storm");
        }

        private EnviroWeatherType CreateRuntimeWeatherBinding(
            EnviroWeatherType source,
            string label)
        {
            EnviroWeatherType runtimeWeather = CloneOwned(source);
            runtimeWeather.name = source.name + " (Runtime Visual " + label + ")";
            if (runtimeWeather.lightningOverride == null)
            {
                runtimeWeather.lightningOverride = new EnviroWeatherTypeLightningOverride();
            }

            runtimeWeather.lightningOverride.lightningStorm = false;
            return runtimeWeather;
        }

        private EnviroWeatherType ResolveRuntimeWeather(
            EnviroWeatherType sourceWeather,
            EnvironmentBindingId bindingId,
            EnvironmentPresentationPresetKind presetKind)
        {
            if (sourceWeather == null)
            {
                return null;
            }

            switch (bindingId.Value)
            {
                case Enviro3EnvironmentBindings.DrizzleIdValue:
                    return runtimeDrizzle;
                case Enviro3EnvironmentBindings.RainIdValue:
                    return runtimeRain;
                case Enviro3EnvironmentBindings.HeavyRainIdValue:
                    return runtimeHeavyRain;
                case Enviro3EnvironmentBindings.StormIdValue:
                    return runtimeStorm;
                default:
                    return sourceWeather;
            }
        }

        private bool IsRuntimeWeather(EnviroWeatherType weather)
        {
            return weather != null &&
                   (weather == runtimeDrizzle ||
                    weather == runtimeRain ||
                    weather == runtimeHeavyRain ||
                    weather == runtimeStorm);
        }

        private void ApplyPrecipitationIntensityIfDirty(
            EnviroWeatherType weather,
            float intensity01)
        {
            if (!IsRuntimeWeather(weather))
            {
                return;
            }

            if (hasAppliedPrecipitationIntensity &&
                lastPrecipitationWeather == weather &&
                Mathf.Approximately(lastPrecipitationIntensity, intensity01))
            {
                return;
            }

            if (!TrySetRainEmission(weather, intensity01))
            {
                throw new InvalidOperationException(
                    "The runtime precipitation binding has no single exact 'Rain' override.");
            }

            lastPrecipitationWeather = weather;
            lastPrecipitationIntensity = intensity01;
            hasAppliedPrecipitationIntensity = true;
        }

        private static bool TrySetRainEmission(
            EnviroWeatherType weather,
            float intensity01)
        {
            if (weather == null || weather.effectsOverride == null ||
                weather.effectsOverride.effectsOverride == null)
            {
                return false;
            }

            int matches = 0;
            for (int index = 0; index < weather.effectsOverride.effectsOverride.Count; index++)
            {
                EnviroEffectsOverrideType effect =
                    weather.effectsOverride.effectsOverride[index];
                if (effect == null ||
                    !string.Equals(
                        effect.name,
                        RequiredRainEffectName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                effect.emission = intensity01;
                matches++;
            }

            return matches == 1;
        }

        private void AcceptEnvironmentRefresh(
            in EnvironmentRefreshRequest request,
            EnvironmentBindingId bindingId)
        {
            if (request.Targets == EnvironmentRefreshTarget.None ||
                request.Sequence == lastRefreshSequence)
            {
                return;
            }

            lastRefreshSequence = request.Sequence;
            EnvironmentRefreshTarget supportedTargets = request.Targets &
                (EnvironmentRefreshTarget.Ambient | EnvironmentRefreshTarget.Reflections);

            if ((request.Targets & EnvironmentRefreshTarget.Sky) != 0)
            {
                AddWarning(
                    RefreshUnsupportedCode,
                    "The installed Enviro API has no bounded explicit sky-refresh command; regular Enviro sky updates remain active.",
                    bindingId);
            }

            if ((supportedTargets & EnvironmentRefreshTarget.Ambient) != 0 &&
                manager.Lighting == null)
            {
                supportedTargets &= ~EnvironmentRefreshTarget.Ambient;
                AddWarning(
                    RefreshUnsupportedCode,
                    "Ambient refresh was requested but the live Enviro Lighting module is unavailable.",
                    bindingId);
            }

            if ((supportedTargets & EnvironmentRefreshTarget.Reflections) != 0 &&
                (manager.Reflections == null ||
                 manager.Objects == null ||
                 manager.Objects.globalReflectionProbe == null))
            {
                supportedTargets &= ~EnvironmentRefreshTarget.Reflections;
                AddWarning(
                    RefreshUnsupportedCode,
                    "Reflection refresh was requested but the live Enviro reflection probe is unavailable.",
                    bindingId);
            }

            if (supportedTargets == EnvironmentRefreshTarget.None)
            {
                return;
            }

            pendingRefreshTargets |= supportedTargets;
            pendingRefreshSequence = request.Sequence;
            float remainingSeconds = nextRefreshAllowedRealtime - Time.realtimeSinceStartup;
            if (remainingSeconds <= 0f)
            {
                ExecutePendingEnvironmentRefresh();
                return;
            }

            if (delayedRefreshRoutine == null)
            {
                delayedRefreshRoutine = StartCoroutine(
                    ExecutePendingEnvironmentRefreshAfterDelay(remainingSeconds));
            }
        }

        private IEnumerator ExecutePendingEnvironmentRefreshAfterDelay(float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            delayedRefreshRoutine = null;
            ExecutePendingEnvironmentRefresh();
        }

        private void ExecutePendingEnvironmentRefresh()
        {
            EnvironmentRefreshTarget targets = pendingRefreshTargets;
            pendingRefreshTargets = EnvironmentRefreshTarget.None;
            pendingRefreshSequence = 0;

            if ((targets & EnvironmentRefreshTarget.Ambient) != 0)
            {
                manager.Lighting.UpdateAmbientLighting(true);
            }

            if ((targets & EnvironmentRefreshTarget.Reflections) != 0)
            {
                manager.Reflections.RenderGlobalReflectionProbe(true);
            }

            nextRefreshAllowedRealtime = Time.realtimeSinceStartup + RefreshCooldownSeconds;
        }

        private void CancelPendingRefresh()
        {
            if (delayedRefreshRoutine != null)
            {
                StopCoroutine(delayedRefreshRoutine);
                delayedRefreshRoutine = null;
            }

            pendingRefreshTargets = EnvironmentRefreshTarget.None;
            pendingRefreshSequence = 0;
        }

        private void ResetAppliedState()
        {
            hasAppliedWeatherBinding = false;
            hasAppliedPrecipitationIntensity = false;
            hasAppliedQualityTier = false;
            hasAppliedDateTime = false;
            lastAppliedRevision = 0;
            lastLightningSequence = 0;
            lastRefreshSequence = 0;
            pendingRefreshSequence = 0;
            pendingRefreshTargets = EnvironmentRefreshTarget.None;
            lastWeatherBindingId = default;
            lastPrecipitationWeather = null;
            lastPrecipitationIntensity = 0f;
            lastQualityTier = default;
            lastYear = 0;
            lastMonth = 0;
            lastDay = 0;
            lastTimeOfDaySeconds = 0;
            nextRefreshAllowedRealtime = 0f;
        }

        private void AssignConfigurationToManager(EnviroConfiguration configuration)
        {
            manager.configuration = configuration;
            manager.Time = configuration.timeModule;
            manager.Lighting = configuration.lightingModule;
            manager.Reflections = configuration.reflectionsModule;
            manager.Sky = configuration.Sky;
            manager.Fog = configuration.fogModule;
            manager.VolumetricClouds = configuration.volumetricCloudModule;
            manager.FlatClouds = configuration.flatCloudModule;
            manager.Weather = configuration.Weather;
            manager.Aurora = configuration.Aurora;
            manager.Audio = configuration.Audio;
            manager.Effects = configuration.Effects;
            manager.Lightning = configuration.Lightning;
            manager.Quality = configuration.Quality;
            manager.Environment = configuration.Environment;
        }

        private void CaptureLiveManagerModules()
        {
            if (manager == null)
            {
                return;
            }

            AddOwnedIfRuntime(manager.Time);
            AddOwnedIfRuntime(manager.Lighting);
            AddOwnedIfRuntime(manager.Reflections);
            AddOwnedIfRuntime(manager.Sky);
            AddOwnedIfRuntime(manager.Fog);
            AddOwnedIfRuntime(manager.VolumetricClouds);
            AddOwnedIfRuntime(manager.FlatClouds);
            AddOwnedIfRuntime(manager.Weather);
            AddOwnedIfRuntime(manager.Aurora);
            AddOwnedIfRuntime(manager.Audio);
            AddOwnedIfRuntime(manager.Effects);
            AddOwnedIfRuntime(manager.Lightning);
            AddOwnedIfRuntime(manager.Quality);
            AddOwnedIfRuntime(manager.Environment);
        }

        private void AddOwnedIfRuntime(ScriptableObject module)
        {
            if (module != null && !IsSourceModule(module))
            {
                AddOwnedObject(module);
                module.hideFlags = HideFlags.DontSave;
            }
        }

        private bool ContainsSourceModuleReference()
        {
            return IsSourceModule(manager.Time) ||
                   IsSourceModule(manager.Lighting) ||
                   IsSourceModule(manager.Reflections) ||
                   IsSourceModule(manager.Sky) ||
                   IsSourceModule(manager.Fog) ||
                   IsSourceModule(manager.VolumetricClouds) ||
                   IsSourceModule(manager.FlatClouds) ||
                   IsSourceModule(manager.Weather) ||
                   IsSourceModule(manager.Aurora) ||
                   IsSourceModule(manager.Audio) ||
                   IsSourceModule(manager.Effects) ||
                   IsSourceModule(manager.Lightning) ||
                   IsSourceModule(manager.Quality) ||
                   IsSourceModule(manager.Environment);
        }

        private bool IsSourceModule(ScriptableObject module)
        {
            if (module == null || bindings == null || bindings.SourceConfiguration == null)
            {
                return false;
            }

            EnviroConfiguration source = bindings.SourceConfiguration;
            return module == bindings.EffectsSource ||
                   module == source.timeModule ||
                   module == source.lightingModule ||
                   module == source.reflectionsModule ||
                   module == source.Sky ||
                   module == source.fogModule ||
                   module == source.volumetricCloudModule ||
                   module == source.flatCloudModule ||
                   module == source.Weather ||
                   module == source.Aurora ||
                   module == source.Audio ||
                   module == source.Effects ||
                   module == source.Lightning ||
                   module == source.Quality ||
                   module == source.Environment;
        }

        private T CloneOwned<T>(T source) where T : ScriptableObject
        {
            if (source == null)
            {
                return null;
            }

            T clone = Instantiate(source);
            clone.hideFlags = HideFlags.DontSave;
            AddOwnedObject(clone);
            return clone;
        }

        private void AddOwnedObject(UnityEngine.Object runtimeObject)
        {
            if (runtimeObject != null && !ownedRuntimeObjects.Contains(runtimeObject))
            {
                ownedRuntimeObjects.Add(runtimeObject);
            }
        }

        private void DestroyRuntimeWeatherBindings()
        {
            DestroyRuntimeWeather(ref runtimeDrizzle);
            DestroyRuntimeWeather(ref runtimeRain);
            DestroyRuntimeWeather(ref runtimeHeavyRain);
            DestroyRuntimeWeather(ref runtimeStorm);
        }

        private void DestroyRuntimeWeather(ref EnviroWeatherType runtimeWeather)
        {
            if (runtimeWeather == null)
            {
                return;
            }

            ownedRuntimeObjects.Remove(runtimeWeather);
            Destroy(runtimeWeather);
            runtimeWeather = null;
        }

        private void FailClosedManager()
        {
            if (manager == null)
            {
                return;
            }

            manager.enabled = false;
            ClearManagerReferences();
        }

        private void TearDownRuntimeIsolation()
        {
            if (manager != null)
            {
                ApplyAutonomyAndAudioSafety();
                DisableManagerModulesForTeardown();
                ClearManagerReferences();
            }

            for (int index = ownedRuntimeObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object runtimeObject = ownedRuntimeObjects[index];
                if (runtimeObject != null)
                {
                    Destroy(runtimeObject);
                }
            }

            ownedRuntimeObjects.Clear();
            runtimeLightningPrefab = null;
            sourceLightningFlashMaterial = null;
            runtimeLightningFlashMaterial = null;
            runtimeConfiguration = null;
            runtimeIsolationPrepared = false;
        }

        private void DisableManagerModulesForTeardown()
        {
            // EnviroManager.OnDisable only disables Fog and releases zone buffers. The
            // volumetric-cloud module owns native Material/RenderTexture objects that a
            // scene unload will not release merely by destroying its ScriptableObject.
            // Other module Disable() methods destroy scene components/GameObjects with
            // DestroyImmediate, which is unsafe once the manager hierarchy is unloading.
            if (!manager.gameObject.activeInHierarchy)
            {
                manager.VolumetricClouds?.Disable();
                return;
            }

            manager.DisableModules();
            if (manager.enabled)
            {
                // DisableModules handled Fog. Clear it before disabling the component so
                // EnviroManager.OnDisable only releases its zone buffers.
                manager.Fog = null;
                manager.enabled = false;
            }
        }

        private void ClearManagerReferences()
        {
            manager.configuration = null;
            manager.Time = null;
            manager.Lighting = null;
            manager.Reflections = null;
            manager.Sky = null;
            manager.Fog = null;
            manager.VolumetricClouds = null;
            manager.FlatClouds = null;
            manager.Weather = null;
            manager.Aurora = null;
            manager.Audio = null;
            manager.Effects = null;
            manager.Lightning = null;
            manager.Quality = null;
            manager.Environment = null;
        }

        private void CopyDiagnostics(IReadOnlyList<EnvironmentPresentationDiagnostic> source)
        {
            for (int index = 0; index < source.Count; index++)
            {
                diagnostics.Add(source[index]);
            }
        }

        private void AddWarning(
            string code,
            string message,
            EnvironmentBindingId bindingId = default)
        {
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Warning,
                code,
                message,
                bindingId));
        }

        private void AddError(
            string code,
            string message,
            EnvironmentBindingId bindingId = default)
        {
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                code,
                message,
                bindingId));
        }

        private bool HasErrors()
        {
            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].Severity == EnvironmentDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private EnvironmentPresentationStatus SetStatus(
            EnvironmentPresentationState state,
            bool exposeCapabilities)
        {
            int warningCount = 0;
            int errorCount = 0;
            for (int index = 0; index < diagnostics.Count; index++)
            {
                switch (diagnostics[index].Severity)
                {
                    case EnvironmentDiagnosticSeverity.Warning:
                        warningCount++;
                        break;
                    case EnvironmentDiagnosticSeverity.Error:
                        errorCount++;
                        break;
                }
            }

            status = new EnvironmentPresentationStatus(
                state,
                exposeCapabilities ? SupportedCapabilities : EnvironmentPresentationCapabilities.None,
                lastAppliedRevision,
                warningCount,
                errorCount);
            return status;
        }
    }
}
