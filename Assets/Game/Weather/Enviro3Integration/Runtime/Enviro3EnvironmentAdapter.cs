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
        private const string InstantTransitionCode = "ENVIRO3-FRAME-002";
        private const string RefreshUnsupportedCode = "ENVIRO3-FRAME-003";
        private const string VendorCallFailureCode = "ENVIRO3-RUNTIME-001";
        private const string RequiredRainEffectName = "Rain";

        private const EnvironmentPresentationCapabilities SupportedCapabilities =
            EnvironmentPresentationCapabilities.TimeOfDay |
            EnvironmentPresentationCapabilities.Sky |
            EnvironmentPresentationCapabilities.SunMoonLighting |
            EnvironmentPresentationCapabilities.Clouds |
            EnvironmentPresentationCapabilities.Precipitation |
            EnvironmentPresentationCapabilities.Fog |
            EnvironmentPresentationCapabilities.Wind |
            EnvironmentPresentationCapabilities.LightningVisual |
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
        private EnviroWeatherType runtimeStorm;
        private Material runtimeLightningFlashMaterial;
        private Camera previousCamera;
        private Coroutine delayedAttachRoutine;
        private bool runtimeIsolationPrepared;
        private bool startupBarrierPassed;
        private bool attachRequested;
        private bool isAttached;
        private ulong lastAppliedRevision;
        private uint lastLightningSequence;
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
            if (isAttached && manager != null)
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

        private void LateUpdate()
        {
            if (Application.isPlaying && runtimeIsolationPrepared)
            {
                // Weather blending rewrites Enviro audio modifiers during Update.
                // Reassert the silent/autonomy contract after all Update calls.
                ApplyAutonomyAndAudioSafety();
            }
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
            ValidateAttachRequirements();
            if (HasErrors())
            {
                return SetStatus(EnvironmentPresentationState.Faulted, false);
            }

            try
            {
                runtimeStorm = CloneOwned(bindings.Storm);
                runtimeStorm.name = bindings.Storm.name + " (Runtime Visual Storm)";
                if (runtimeStorm.lightningOverride == null)
                {
                    runtimeStorm.lightningOverride = new EnviroWeatherTypeLightningOverride();
                }

                runtimeStorm.lightningOverride.lightningStorm = false;
                previousCamera = manager.Camera;
                manager.ChangeCamera(presentationCamera);
                ApplyAutonomyAndAudioSafety();
                isAttached = true;
                lastAppliedRevision = 0;
                lastLightningSequence = 0;
                return SetStatus(EnvironmentPresentationState.Ready, true);
            }
            catch (Exception exception)
            {
                DestroyRuntimeStorm();
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

            if (presetKind == EnvironmentPresentationPresetKind.Storm)
            {
                weatherType = runtimeStorm;
            }

            try
            {
                manager.Weather.ChangeWeatherInstant(weatherType);
                manager.Quality.Settings.defaultQuality = quality;
                manager.Quality.UpdateModule();
                ApplyDateAndTime(frame);

                if (frame.LightningVisual.IsRequested &&
                    frame.LightningVisual.Sequence != lastLightningSequence)
                {
                    CastLightningVisual();
                    lastLightningSequence = frame.LightningVisual.Sequence;
                }

                if (frame.TransitionDurationSeconds > 0f)
                {
                    AddWarning(
                        InstantTransitionCode,
                        "Milestone 07A maps smoke states with ChangeWeatherInstant; transition duration is not applied.",
                        frame.BindingId);
                }

                if (frame.EnvironmentRefresh.Targets != EnvironmentRefreshTarget.None)
                {
                    AddWarning(
                        RefreshUnsupportedCode,
                        "Explicit reflection/environment refresh is not owned by the bounded 07A adapter.",
                        frame.BindingId);
                }

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
            if (!isAttached)
            {
                status = EnvironmentPresentationStatus.Detached;
                return;
            }

            ApplyAutonomyAndAudioSafety();
            if (manager != null && manager.Weather != null &&
                manager.Weather.targetWeatherType == runtimeStorm &&
                bindings != null && bindings.Clear != null)
            {
                manager.Weather.ChangeWeatherInstant(bindings.Clear);
            }

            if (manager != null)
            {
                manager.ChangeCamera(previousCamera);
            }

            isAttached = false;
            lastAppliedRevision = 0;
            lastLightningSequence = 0;
            previousCamera = null;
            DestroyRuntimeStorm();
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

            if (bindings.Clear == null || bindings.Overcast == null || bindings.Rain == null ||
                bindings.Storm == null || bindings.Fog == null ||
                bindings.Low == null || bindings.High == null ||
                bindings.EffectsSource == null)
            {
                AddError(
                    MissingReferenceCode,
                    "All direct effects, clear/overcast/rain/storm/fog, and low/high assets must be assigned.");
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

        private void ApplyDateAndTime(in EnvironmentPresentationFrame frame)
        {
            int totalSeconds = Mathf.Clamp(
                Mathf.FloorToInt(frame.NormalizedTimeOfDay01 * 86400f),
                0,
                86399);
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            manager.Time.Settings.simulate = false;
            manager.Time.SetDateTime(
                seconds,
                minutes,
                hours,
                frame.Day,
                frame.Month,
                frame.Year);
        }

        private void ApplyAutonomyAndAudioSafety()
        {
            if (manager == null)
            {
                return;
            }

            if (manager.Time != null && manager.Time.Settings != null)
            {
                manager.Time.Settings.simulate = false;
            }

            if (manager.Lightning != null && manager.Lightning.Settings != null)
            {
                manager.Lightning.Settings.lightningStorm = false;
            }

            if (manager.Fog != null && manager.Fog.Settings != null)
            {
                manager.Fog.Settings.controlHDRPFog = true;
                manager.Fog.Settings.controlHDRPVolumetrics = true;
            }

            SilenceAudio(manager.Audio);
        }

        private static void SilenceAudio(EnviroAudioModule audio)
        {
            if (audio == null || audio.Settings == null)
            {
                return;
            }

            audio.Settings.ambientMasterVolume = 0f;
            audio.Settings.weatherMasterVolume = 0f;
            audio.Settings.thunderMasterVolume = 0f;
            audio.ambientVolumeModifier = 0f;
            audio.weatherVolumeModifier = 0f;
            audio.thunderVolumeModifier = 0f;
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

                clip.volume = 0f;
                if (clip.myAudioSource != null)
                {
                    clip.myAudioSource.volume = 0f;
                    clip.myAudioSource.Stop();
                }
            }
        }

        private void CastLightningVisual()
        {
            Lightning prefab = manager.Lightning.Settings.prefab;
            Material sourceFlashMaterial = prefab.planeMat;
            if (runtimeLightningFlashMaterial == null)
            {
                runtimeLightningFlashMaterial = new Material(sourceFlashMaterial)
                {
                    name = sourceFlashMaterial.name + " (Runtime Lightning Visual)",
                    hideFlags = HideFlags.DontSave
                };
                AddOwnedObject(runtimeLightningFlashMaterial);
            }
            else
            {
                // The vendor bolt animates _Intensity on the supplied material. Reset
                // the one adapter-owned clone before reuse instead of allocating a new
                // native Material for every visual request.
                runtimeLightningFlashMaterial.CopyPropertiesFromMaterial(sourceFlashMaterial);
            }

            EnviroAudioModule runtimeAudio = manager.Audio;
            try
            {
                // Enviro's Lightning component writes directly to planeMat. Temporarily
                // inject a runtime-only clone before Instantiate copies the component,
                // then restore the paid prefab immediately. Audio is also removed for
                // the duration of the public visual call so no vendor thunder event starts.
                prefab.planeMat = runtimeLightningFlashMaterial;
                manager.Audio = null;
                manager.Lightning.CastLightningBolt(
                    lightningOrigin.position,
                    lightningTarget.position);
            }
            finally
            {
                manager.Audio = runtimeAudio;
                prefab.planeMat = sourceFlashMaterial;
            }
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

        private void DestroyRuntimeStorm()
        {
            if (runtimeStorm == null)
            {
                return;
            }

            ownedRuntimeObjects.Remove(runtimeStorm);
            Destroy(runtimeStorm);
            runtimeStorm = null;
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
