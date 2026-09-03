using System;
using System.Collections;
using MSC.Audio;
using MSC.Bootstrap;
using MSC.Core.Lifecycle;
using MSC.Home;
using MSC.Services;
using UnityEngine;

namespace MSC.Lighting.Production
{
    [DefaultExecutionOrder(-30900)]
    [DisallowMultipleComponent]
    public sealed class ProductionLightingInstaller : MonoBehaviour,
        IGameSessionLifetime
    {
        private const int MaximumStartupFrames = 180;

        [SerializeField] private LightingProfileCatalog profileCatalog;
        [SerializeField] private WorldLightingBindingCatalog bindingCatalog;
        [SerializeField] private LightingRuntimeManager runtimeManager;
        [SerializeField] private EnviroLightingBridge environmentBridge;
        [SerializeField] private WorldLightingFixtureAdapter worldAdapter;
        [SerializeField]
        private HomeLightSwitchPresentationAdapter switchPresentationAdapter;
        [SerializeField]
        private PortableFlashlightLightingAdapter flashlightAdapter;
        [SerializeField]
        private TrafficVehicleLightingAdapter trafficVehicleAdapter;
        [SerializeField] private LightingValidationRunner validationRunner;

        private readonly ElectricalGridService grid =
            new ElectricalGridService();
        private bool initialized;
        private ProductionWorldStreamingInstaller worldInstaller;
        private ServiceLightingBusinessAdapter businessAdapter;
        private HomeSystemRuntime homeRuntime;

        public ElectricalGridService Grid => grid;
        public LightingRuntimeManager RuntimeManager => runtimeManager;
        public bool IsInitialized => initialized;
        public ProductionWorldStreamingInstaller WorldInstaller =>
            worldInstaller;
        public LightingProfileCatalog ProfileCatalog => profileCatalog;
        public ServiceLightingBusinessAdapter BusinessAdapter =>
            businessAdapter;
        public HomeLightSwitchPresentationAdapter SwitchPresentationAdapter =>
            switchPresentationAdapter;
        public PortableFlashlightLightingAdapter FlashlightAdapter =>
            flashlightAdapter;
        public TrafficVehicleLightingAdapter TrafficVehicleAdapter =>
            trafficVehicleAdapter;

        private void Awake()
        {
            switchPresentationAdapter =
                GetComponent<HomeLightSwitchPresentationAdapter>() ??
                gameObject.AddComponent<HomeLightSwitchPresentationAdapter>();
            flashlightAdapter =
                GetComponent<PortableFlashlightLightingAdapter>() ??
                gameObject.AddComponent<PortableFlashlightLightingAdapter>();
            trafficVehicleAdapter =
                GetComponent<TrafficVehicleLightingAdapter>() ??
                gameObject.AddComponent<TrafficVehicleLightingAdapter>();
            ValidateAuthoring();
            SeedGrid();
            LightingSessionAuthority.Install(grid);
        }

        private void SeedGrid()
        {
            for (int index = 0; index < bindingCatalog.Bindings.Count; index++)
            {
                WorldLightingFixtureBinding binding =
                    bindingCatalog.Bindings[index];
                if (!grid.HasSource(binding.PowerSourceId))
                {
                    grid.RegisterSource(binding.PowerSourceId, true);
                }

                if (!grid.HasCircuit(binding.CircuitId))
                {
                    grid.RegisterCircuit(
                        binding.CircuitId,
                        binding.PowerSourceId,
                        true);
                }

                if (!string.IsNullOrWhiteSpace(binding.SwitchId) &&
                    !grid.HasSwitch(binding.SwitchId))
                {
                    grid.RegisterSwitch(binding.SwitchId, false);
                }
            }
        }

        private IEnumerator Start()
        {
            ProductionWorldStreamingInstaller world =
                GetComponent<ProductionWorldStreamingInstaller>();
            if (world == null)
            {
                throw new InvalidOperationException(
                    "Production lighting requires the world streaming installer.");
            }
            worldInstaller = world;

            for (int frame = 0; frame < MaximumStartupFrames; frame++)
            {
                if (world.Environment != null &&
                    world.Environment.AreDomainsInitialized &&
                    world.SpawnedPlayer != null &&
                    world.ServiceRuntime != null &&
                    world.ServiceRuntime.IsInitialized)
                {
                    InitializeRuntime(world);
                    yield break;
                }

                yield return null;
            }

            throw new InvalidOperationException(
                "Production lighting dependencies did not initialize in time.");
        }

        private void InitializeRuntime(
            ProductionWorldStreamingInstaller world)
        {
            environmentBridge.Initialize(world.Environment);
            Transform focus = world.SpawnedPlayer.transform;
            runtimeManager.ConfigureProfiles(
                profileCatalog.Calibration,
                profileCatalog.GetRequiredQuality(LightingQualityTier.Low),
                profileCatalog.GetRequiredQuality(LightingQualityTier.Medium),
                profileCatalog.GetRequiredQuality(LightingQualityTier.High),
                profileCatalog.GetRequiredQuality(LightingQualityTier.Ultra),
                focus);
            IAudioBackend audio = world.AudioComposition != null
                ? world.AudioComposition.Backend
                : null;
            businessAdapter = new ServiceLightingBusinessAdapter(
                world.ServiceRuntime,
                profileCatalog.Calibration.BusinessShutdownDelaySeconds);
            runtimeManager.Initialize(
                grid,
                focus,
                environmentBridge.GetSunElevationDegrees,
                environmentBridge,
                businessAdapter,
                new WwiseLightingAudioAdapter(audio));
            switchPresentationAdapter.Initialize(runtimeManager);
            flashlightAdapter.Initialize(
                world.ItemWorldRuntime,
                profileCatalog,
                grid);
            trafficVehicleAdapter.Initialize(
                profileCatalog,
                environmentBridge);
            homeRuntime = world.HomeRuntime;
            grid.StateChanged += HandleElectricalStateChanged;
            SynchronizeHomeElectricity();
            initialized = true;
        }

        private void HandleElectricalStateChanged(
            ElectricalStateChanged state)
        {
            SynchronizeHomeElectricity();
        }

        private void SynchronizeHomeElectricity()
        {
            homeRuntime?.SetElectricityAvailable(grid.IsActuallyOn(
                "grid.home.kitchen",
                string.Empty,
                runtimePolicyAllows: true,
                fixtureIsAvailable: true));
        }

        private void ValidateAuthoring()
        {
            if (profileCatalog == null || bindingCatalog == null ||
                runtimeManager == null || environmentBridge == null ||
                worldAdapter == null || validationRunner == null)
            {
                throw new InvalidOperationException(
                    "Production lighting authoring is incomplete.");
            }

            bindingCatalog.Validate();
        }

        private void OnDestroy()
        {
            EndGameSession();
        }

        public void EndGameSession()
        {
            // GameCompositionRoot replacement destroys the outgoing hierarchy at
            // the end of the frame. Release this process-session authority during
            // the synchronous lifetime handoff so the incoming installer can run
            // Awake without colliding with the already-retired grid.
            LightingSessionAuthority.Uninstall(grid);
            grid.StateChanged -= HandleElectricalStateChanged;
            homeRuntime = null;
            switchPresentationAdapter?.Shutdown();
            flashlightAdapter?.Shutdown();
            trafficVehicleAdapter?.Shutdown();
            initialized = false;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            LightingProfileCatalog configuredProfiles,
            WorldLightingBindingCatalog configuredBindings)
        {
            profileCatalog = configuredProfiles;
            bindingCatalog = configuredBindings;
            runtimeManager = GetComponent<LightingRuntimeManager>() ??
                gameObject.AddComponent<LightingRuntimeManager>();
            environmentBridge = GetComponent<EnviroLightingBridge>() ??
                gameObject.AddComponent<EnviroLightingBridge>();
            worldAdapter = GetComponent<WorldLightingFixtureAdapter>() ??
                gameObject.AddComponent<WorldLightingFixtureAdapter>();
            switchPresentationAdapter =
                GetComponent<HomeLightSwitchPresentationAdapter>() ??
                gameObject.AddComponent<HomeLightSwitchPresentationAdapter>();
            flashlightAdapter =
                GetComponent<PortableFlashlightLightingAdapter>() ??
                gameObject.AddComponent<PortableFlashlightLightingAdapter>();
            trafficVehicleAdapter =
                GetComponent<TrafficVehicleLightingAdapter>() ??
                gameObject.AddComponent<TrafficVehicleLightingAdapter>();
            validationRunner = GetComponent<LightingValidationRunner>() ??
                gameObject.AddComponent<LightingValidationRunner>();
            worldAdapter.ConfigureForAuthoring(
                configuredProfiles,
                configuredBindings);
            runtimeManager.ConfigureForAuthoring(
                configuredProfiles.Calibration,
                configuredProfiles.GetRequiredQuality(LightingQualityTier.Low),
                configuredProfiles.GetRequiredQuality(LightingQualityTier.Medium),
                configuredProfiles.GetRequiredQuality(LightingQualityTier.High),
                configuredProfiles.GetRequiredQuality(LightingQualityTier.Ultra),
                null);
            validationRunner.ConfigureForAuthoring(this);
        }
#endif
    }
}
