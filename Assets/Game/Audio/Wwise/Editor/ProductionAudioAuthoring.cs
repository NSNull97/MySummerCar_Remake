using System;
using System.Collections.Generic;
using System.IO;
using MSC.Audio.Composition;
using MSC.Audio.UnityFallback;
using MSC.Audio.WeatherIntegration;
using MSC.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.Wwise.Editor
{
    public static class ProductionAudioAuthoring
    {
        private const string BootstrapScenePath = "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string ContentRoot = "Assets/Game/Audio/Content";
        private const string FallbackRoot =
            "Assets/Game/Audio/UnityFallback/Content/GeneratedDiagnostic";
        private const string EventMapPath = ContentRoot + "/AudioEventMap.asset";
        private const string ParameterMapPath = ContentRoot + "/AudioParameterMap.asset";
        private const string NameMapPath = ContentRoot + "/WwiseBackendNameMap.asset";
        private const string FallbackLibraryPath =
            ContentRoot + "/UnityAudioEventLibrary.asset";

        private static readonly string[] RequiredBanks =
        {
            "Init",
            "MSC_Vehicle",
            "MSC_Weather",
            "MSC_World",
            "MSC_Interaction",
            "MSC_UI",
        };

        [MenuItem("Tools/MSC Remake/Audio/Build Production Audio Composition")]
        public static void BuildProductionAudioComposition()
        {
            EnsureFolder(ContentRoot);
            EnsureFolder(FallbackRoot);
            Dictionary<FallbackClipKind, AudioClip> clips = BuildFallbackClips();
            AudioEventMap eventMap = CreateOrReplaceAsset<AudioEventMap>(EventMapPath);
            eventMap.ConfigureForTests(BuildEventEntries());
            AudioParameterMap parameterMap =
                CreateOrReplaceAsset<AudioParameterMap>(ParameterMapPath);
            parameterMap.ConfigureForTests(BuildParameterEntries());
            WwiseBackendNameMap nameMap =
                CreateOrReplaceAsset<WwiseBackendNameMap>(NameMapPath);
            nameMap.ConfigureForTests(BuildNameEntries());
            UnityAudioEventLibrary fallbackLibrary =
                CreateOrReplaceAsset<UnityAudioEventLibrary>(FallbackLibraryPath);
            fallbackLibrary.ConfigureForAuthoring(BuildFallbackEntries(clips));

            EditorUtility.SetDirty(eventMap);
            EditorUtility.SetDirty(parameterMap);
            EditorUtility.SetDirty(nameMap);
            EditorUtility.SetDirty(fallbackLibrary);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>();
            if (installer == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap has no ProductionWorldStreamingInstaller.");
            }

            GameObject root = installer.gameObject;
            AkInitializer initializer = EnsureComponent<AkInitializer>(root);
            initializer.InitializationSettings = AkWwiseInitializationSettings.Instance;
            WwiseAudioBackend wwise = EnsureComponent<WwiseAudioBackend>(root);
            UnityAudioBackend fallback = EnsureComponent<UnityAudioBackend>(root);
            AudioBackendRouter router = EnsureComponent<AudioBackendRouter>(root);
            WwiseRuntimeBankOwner bankOwner =
                EnsureComponent<WwiseRuntimeBankOwner>(root);
            AudioEmitterAuthoring ambienceEmitter =
                EnsureComponent<AudioEmitterAuthoring>(root);
            WeatherAudioPresenter weatherPresenter =
                EnsureComponent<WeatherAudioPresenter>(root);
            ProductionAudioComposition composition =
                EnsureComponent<ProductionAudioComposition>(root);

            wwise.ConfigureForAuthoring(
                eventMap,
                parameterMap,
                nameMap,
                true,
                RequiredBanks);
            fallback.ConfigureForAuthoring(fallbackLibrary);
            bankOwner.ConfigureForAuthoring(wwise, RequiredBanks);
            router.Configure(wwise, fallback, scanLoadedScenes: true);
            ambienceEmitter.Configure(
                "audio.emitter.environment.production",
                router,
                root.transform);
            weatherPresenter.Configure(
                installer.Environment,
                router,
                ambienceEmitter);
            composition.ConfigureForAuthoring(
                router,
                wwise,
                fallback,
                bankOwner,
                ambienceEmitter,
                weatherPresenter);
            installer.ConfigureAudioForAuthoring(composition);

            EditorUtility.SetDirty(initializer);
            EditorUtility.SetDirty(wwise);
            EditorUtility.SetDirty(fallback);
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(bankOwner);
            EditorUtility.SetDirty(ambienceEmitter);
            EditorUtility.SetDirty(weatherPresenter);
            EditorUtility.SetDirty(composition);
            EditorUtility.SetDirty(installer);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "M08 production audio composition authored: Wwise preferred, " +
                "Unity Audio fallback, project-owned maps, diagnostic fallback WAVs.");
        }

        private static AudioEventMapEntry[] BuildEventEntries() => new[]
        {
            Event(AudioProjectIds.Events.VehicleStarterEngaged, "Play_MSC_Vehicle_Starter_Engage", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleStarterDisengaged, "Stop_MSC_Vehicle_Starter", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleEngineStarted, "Play_MSC_Vehicle_Engine_Start", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleEngineStopped, "Stop_MSC_Vehicle_Engine", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleEngineStalled, "Play_MSC_Vehicle_Engine_Stall", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleReset, "Stop_MSC_Vehicle_All", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleEngineIntake, "Play_MSC_Vehicle_Engine_Intake", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleEngineExhaust, "Play_MSC_Vehicle_Engine_Exhaust", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleEngineMechanical, "Play_MSC_Vehicle_Engine_Mechanical", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleTransmission, "Play_MSC_Vehicle_Transmission", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleBodyRattle, "Play_MSC_Vehicle_Body_Rattle", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleSuspensionImpact, "Play_MSC_Vehicle_Suspension_Impact", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleTireRoll, "Play_MSC_Vehicle_Tire_Roll", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.VehicleTireSkid, "Play_MSC_Vehicle_Tire_Skid", "MSC_Vehicle", allowMultiple: false),
            Event(AudioProjectIds.Events.WeatherRainExterior, "Play_MSC_Weather_Rain_Exterior", "MSC_Weather", false, false),
            Event(AudioProjectIds.Events.WeatherRainSheltered, "Play_MSC_Weather_Rain_Sheltered", "MSC_Weather", false, false),
            Event(AudioProjectIds.Events.WeatherRainInterior, "Play_MSC_Weather_Rain_Interior", "MSC_Weather", false, false),
            Event(AudioProjectIds.Events.WeatherWind, "Play_MSC_Weather_Wind", "MSC_Weather", false, false),
            Event(AudioProjectIds.Events.WeatherThunder, "Play_MSC_Weather_Thunder", "MSC_Weather"),
            Event(AudioProjectIds.Events.WeatherThunderDistant, "Play_MSC_Weather_Thunder_Distant", "MSC_Weather"),
            Event(AudioProjectIds.Events.WeatherThunderStrike, "Play_MSC_Weather_Thunder_Strike", "MSC_Weather"),
            Event(AudioProjectIds.Events.WorldForestAmbience, "Play_MSC_World_Forest_Ambience", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldLakeAmbience, "Play_MSC_World_Lake_Ambience", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldGarageRoomTone, "Play_MSC_World_Garage_RoomTone", "MSC_World", false, false),
            Event(AudioProjectIds.Events.WorldInteriorRoomTone, "Play_MSC_World_Interior_RoomTone", "MSC_World", false, false),
            Event(AudioProjectIds.Events.WorldDistantTraffic, "Play_MSC_World_Distant_Traffic", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirds, "Play_MSC_World_Birds", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirdsMorning, "Play_MSC_World_Birds_Morning", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirdsDay, "Play_MSC_World_Birds_Day", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirdsEvening, "Play_MSC_World_Birds_Evening", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirdsNight, "Play_MSC_World_Birds_Night", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldBirdsSwamp, "Play_MSC_World_Birds_Swamp", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldMeadow, "Play_MSC_World_Meadow", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldDog, "Play_MSC_World_Dog", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldChainsaw, "Play_MSC_World_Chainsaw", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldInsects, "Play_MSC_World_Insects", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldWindChime, "Play_MSC_World_Wind_Chime", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldMosquito, "Play_MSC_World_Mosquito", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldFly, "Play_MSC_World_Fly", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldFlyVariant, "Play_MSC_World_Fly_Variant", "MSC_World", true, false),
            Event(AudioProjectIds.Events.WorldWasp, "Play_MSC_World_Wasp", "MSC_World", true, false),
            Event(AudioProjectIds.Events.InteractionPickup, "Play_MSC_Interaction_Pickup", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionDrop, "Play_MSC_Interaction_Drop", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionThrow, "Play_MSC_Interaction_Throw", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionPlace, "Play_MSC_Interaction_Place", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionMountHandoff, "Play_MSC_Interaction_Mount_Handoff", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionImpact, "Play_MSC_Interaction_Impact", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionToolUse, "Play_MSC_Interaction_Tool_Use", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionFastenerInsert, "Play_MSC_Interaction_Fastener_Insert", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionFastenerTighten, "Play_MSC_Interaction_Fastener_Tighten", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionFastenerLoosen, "Play_MSC_Interaction_Fastener_Loosen", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionPartInstall, "Play_MSC_Interaction_Part_Install", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionPartRemove, "Play_MSC_Interaction_Part_Remove", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionDoorOpen, "Play_MSC_Interaction_Door_Open", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionDoorClose, "Play_MSC_Interaction_Door_Close", "MSC_Interaction"),
            Event(AudioProjectIds.Events.VehicleBodyImpactLow01, "Play_MSC_Vehicle_Body_Impact_Low_01", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleBodyImpactLow02, "Play_MSC_Vehicle_Body_Impact_Low_02", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleBodyImpactHigh01, "Play_MSC_Vehicle_Body_Impact_High_01", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.VehicleBodyImpactHigh02, "Play_MSC_Vehicle_Body_Impact_High_02", "MSC_Vehicle"),
            Event(AudioProjectIds.Events.InteractionGateOpen, "Play_MSC_Interaction_Gate_Open", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionGateClose, "Play_MSC_Interaction_Gate_Close", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionWindowOpen, "Play_MSC_Interaction_Window_Open", "MSC_Interaction"),
            Event(AudioProjectIds.Events.InteractionWindowClose, "Play_MSC_Interaction_Window_Close", "MSC_Interaction"),
            Event(AudioProjectIds.Events.PlayerFootstep, "Play_MSC_Player_Footstep", "MSC_Interaction"),
            Event(AudioProjectIds.Events.UiNavigate, "Play_MSC_UI_Navigate", "MSC_UI", false),
            Event(AudioProjectIds.Events.UiConfirm, "Play_MSC_UI_Confirm", "MSC_UI", false),
            Event(AudioProjectIds.Events.UiCancel, "Play_MSC_UI_Cancel", "MSC_UI", false),
            Event(AudioProjectIds.Events.UiSaveFeedback, "Play_MSC_UI_Save_Feedback", "MSC_UI", false),
            Event(AudioProjectIds.Events.UiLoadFeedback, "Play_MSC_UI_Load_Feedback", "MSC_UI", false),
        };

        private static AudioParameterMapEntry[] BuildParameterEntries() => new[]
        {
            Parameter(AudioProjectIds.Parameters.Master, "MSC_Mixer_Master", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.Vehicle, "MSC_Mixer_Vehicle", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.Effects, "MSC_Mixer_Effects", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.Ambience, "MSC_Mixer_Ambience", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.Music, "MSC_Mixer_Music", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.Ui, "MSC_Mixer_UI", 0f, 1f, 1f),
            Parameter(AudioProjectIds.Parameters.VehicleRpm, "MSC_Vehicle_RPM", 0f, 9000f, 0f, 1f),
            Parameter(AudioProjectIds.Parameters.VehicleRpmNormalized, "MSC_Vehicle_RPM_Normalized", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleEngineLoad, "MSC_Vehicle_EngineLoad", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleThrottle, "MSC_Vehicle_Throttle", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleGear, "MSC_Vehicle_Gear", -1f, 6f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleClutchSlipRpm, "MSC_Vehicle_ClutchSlipRPM", 0f, 9000f, 0f, 1f),
            Parameter(AudioProjectIds.Parameters.VehicleSpeed, "MSC_Vehicle_Speed", 0f, 80f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleWheelSpeed, "MSC_Vehicle_WheelSpeedRadPS", 0f, 250f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleWheelSlip, "MSC_Vehicle_WheelSlip", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleBrake, "MSC_Vehicle_Brake", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleSuspensionImpact, "MSC_Vehicle_SuspensionImpact", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleBatteryVoltage, "MSC_Vehicle_BatteryVoltage", 0f, 18f, 12.6f),
            Parameter(AudioProjectIds.Parameters.VehicleEngineTemperature, "MSC_Vehicle_EngineTemperature", -50f, 180f, 20f),
            Parameter(AudioProjectIds.Parameters.VehicleDamage, "MSC_Vehicle_Damage", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleInteriorBlend, "MSC_Vehicle_InteriorBlend", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleDoorOpenness, "MSC_Vehicle_DoorOpenness", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.VehicleWindowOpenness, "MSC_Vehicle_WindowOpenness", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.WeatherPrecipitation, "MSC_Weather_Precipitation", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.WeatherWind, "MSC_Weather_Wind", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.WeatherThunderRisk, "MSC_Weather_ThunderRisk", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.EnvironmentShelter, "MSC_Environment_Shelter", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.EnvironmentTimeOfDay, "MSC_Environment_TimeOfDay", 0f, 1f, 0.5f),
            Parameter(AudioProjectIds.Parameters.LightningIntensity, "MSC_Lightning_Intensity", 0f, 1f, 0f),
            Parameter(AudioProjectIds.Parameters.LightningDistance, "MSC_Lightning_DistanceMeters", 0f, 5000f, 0f, 0.1f),
            Parameter(AudioProjectIds.Parameters.LightningDelay, "MSC_Lightning_DelaySeconds", 0f, 20f, 0f),
            Parameter(AudioProjectIds.Parameters.InteractionImpactIntensity, "MSC_Interaction_ImpactIntensity", 0f, 1f, 0f),
        };

        private static WwiseBackendNameMapEntry[] BuildNameEntries() => new[]
        {
            Name(AudioProjectIds.Switches.SurfaceGroup.Value, "MSC_Vehicle_Surface"),
            Name(AudioProjectIds.Switches.SurfaceUnknown.Value, "Unknown"),
            Name(AudioProjectIds.Switches.SurfacePaved.Value, "Paved"),
            Name(AudioProjectIds.Switches.SurfaceGravel.Value, "Gravel"),
            Name(AudioProjectIds.Switches.SurfaceDirt.Value, "Dirt"),
            Name(AudioProjectIds.Switches.SurfaceGrass.Value, "Grass"),
            Name(AudioProjectIds.Switches.SurfaceMudWet.Value, "MudWet"),
            Name(AudioProjectIds.Switches.WeatherPrecipitationGroup.Value, "MSC_Weather_PrecipitationType"),
            Name(AudioProjectIds.Switches.WeatherPrecipitationNone.Value, "None_01"),
            Name(AudioProjectIds.Switches.WeatherPrecipitationDrizzle.Value, "Drizzle"),
            Name(AudioProjectIds.Switches.WeatherPrecipitationRain.Value, "Rain"),
            Name(AudioProjectIds.Switches.WeatherPrecipitationSnow.Value, "Snow"),
            Name(AudioProjectIds.Switches.InteractionMaterialGroup.Value, "MSC_Interaction_Material"),
            Name(AudioProjectIds.Switches.InteractionMaterialUnknown.Value, "Unknown"),
            Name(AudioProjectIds.Switches.InteractionMaterialMetal.Value, "Metal"),
            Name(AudioProjectIds.Switches.InteractionMaterialWood.Value, "Wood"),
            Name(AudioProjectIds.Switches.InteractionMaterialPlastic.Value, "Plastic"),
            Name(AudioProjectIds.Switches.InteractionMaterialGlass.Value, "Glass"),
            Name(AudioProjectIds.Switches.InteractionMaterialConcrete.Value, "Concrete"),
            Name(AudioProjectIds.Switches.InteractionMaterialFabric.Value, "Fabric"),
            Name(AudioProjectIds.Switches.FootstepSurfaceGroup.Value, "MSC_Player_FootstepSurface"),
            Name(AudioProjectIds.Switches.FootstepSurfaceUnknown.Value, "Unknown"),
            Name(AudioProjectIds.Switches.FootstepSurfacePaved.Value, "Paved"),
            Name(AudioProjectIds.Switches.FootstepSurfaceGravel.Value, "Gravel"),
            Name(AudioProjectIds.Switches.FootstepSurfaceDirt.Value, "Dirt"),
            Name(AudioProjectIds.Switches.FootstepSurfaceGrass.Value, "Grass"),
            Name(AudioProjectIds.Switches.FootstepSurfaceWood.Value, "Wood"),
            Name(AudioProjectIds.Switches.FootstepSurfaceConcrete.Value, "Concrete"),
            Name(AudioProjectIds.Switches.FootstepSurfaceMetal.Value, "Metal"),
            Name(AudioProjectIds.Switches.FootstepSurfaceWet.Value, "Wet"),
            Name(AudioProjectIds.States.EnvironmentGroup.Value, "MSC_Environment"),
            Name(AudioProjectIds.States.EnvironmentExterior.Value, "Exterior"),
            Name(AudioProjectIds.States.EnvironmentSheltered.Value, "Sheltered"),
            Name(AudioProjectIds.States.EnvironmentInterior.Value, "Interior"),
            Name(AudioProjectIds.States.EnvironmentVehicleInterior.Value, "VehicleInterior"),
            Name(AudioProjectIds.States.DayPhaseGroup.Value, "MSC_Time_DayPhase"),
            Name(AudioProjectIds.States.DayPhaseDawn.Value, "Dawn"),
            Name(AudioProjectIds.States.DayPhaseDay.Value, "Day"),
            Name(AudioProjectIds.States.DayPhaseEvening.Value, "Evening"),
            Name(AudioProjectIds.States.DayPhaseNight.Value, "Night"),
            Name(AudioProjectIds.States.VehicleEngineGroup.Value, "MSC_Vehicle_EngineState"),
            Name(AudioProjectIds.States.VehicleEngineOff.Value, "Off"),
            Name(AudioProjectIds.States.VehicleEngineCranking.Value, "Cranking"),
            Name(AudioProjectIds.States.VehicleEngineRunning.Value, "Running"),
            Name(AudioProjectIds.States.VehicleEngineStalled.Value, "Stalled"),
        };

        private static UnityAudioEventDefinition[] BuildFallbackEntries(
            IReadOnlyDictionary<FallbackClipKind, AudioClip> clips)
        {
            AudioEventMapEntry[] events = BuildEventEntries();
            var definitions = new List<UnityAudioEventDefinition>(events.Length);
            for (int index = 0; index < events.Length; index++)
            {
                AudioEventMapEntry source = events[index];
                if (source.StableId.Contains(".ui."))
                {
                    continue;
                }
                bool vehicle = source.StableId.Contains(".vehicle.");
                bool weather = source.StableId.Contains(".weather.");
                bool world = source.StableId.Contains(".world.");
                bool interaction = source.StableId.Contains(".interaction.");
                bool player = source.StableId.Contains(".player.");
                bool loop = source.StableId.Contains(".rain.") ||
                            source.StableId.EndsWith(".wind", StringComparison.Ordinal) ||
                            source.StableId.Contains(".ambience") ||
                            source.StableId.Contains(".roomtone") ||
                            IsWorldAmbientLoop(source.StableId) ||
                            source.StableId.Contains(".engine.intake") ||
                            source.StableId.Contains(".engine.exhaust") ||
                            source.StableId.Contains(".engine.mechanical") ||
                            source.StableId.Contains(".tire.roll");
                FallbackClipKind clipKind = vehicle
                    ? loop ? FallbackClipKind.VehicleLoop : FallbackClipKind.VehicleStart
                    : weather
                        ? source.StableId.Contains("rain")
                            ? FallbackClipKind.Rain
                            : source.StableId.Contains("wind")
                                ? FallbackClipKind.Wind
                                : FallbackClipKind.Interaction
                        : world ? FallbackClipKind.World : FallbackClipKind.Interaction;
                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring(
                    source.StableId,
                    clips[clipKind],
                    vehicle ? UnityAudioCategory.Vehicle :
                    interaction || player
                        ? UnityAudioCategory.Effects
                        : UnityAudioCategory.Ambience,
                    loop,
                    GetFallbackVolume(source.StableId, vehicle),
                    1f,
                    source.Spatialized ? 1f : 0f,
                    1f,
                    world ? GetWorldFallbackMaximumDistance(source.StableId) : 45f);
                definitions.Add(definition);
            }

            return definitions.ToArray();
        }

        private static bool IsWorldAmbientLoop(string stableEventId) =>
            stableEventId.StartsWith("audio.event.world.birds", StringComparison.Ordinal) ||
            stableEventId == AudioProjectIds.Events.WorldMeadow.Value ||
            stableEventId == AudioProjectIds.Events.WorldDog.Value ||
            stableEventId == AudioProjectIds.Events.WorldInsects.Value ||
            stableEventId == AudioProjectIds.Events.WorldWindChime.Value ||
            stableEventId == AudioProjectIds.Events.WorldMosquito.Value ||
            stableEventId == AudioProjectIds.Events.WorldFly.Value ||
            stableEventId == AudioProjectIds.Events.WorldFlyVariant.Value ||
            stableEventId == AudioProjectIds.Events.WorldWasp.Value;

        private static float GetWorldFallbackMaximumDistance(string stableEventId)
        {
            if (stableEventId == AudioProjectIds.Events.WorldWindChime.Value ||
                stableEventId == AudioProjectIds.Events.WorldMosquito.Value ||
                stableEventId == AudioProjectIds.Events.WorldFly.Value ||
                stableEventId == AudioProjectIds.Events.WorldFlyVariant.Value ||
                stableEventId == AudioProjectIds.Events.WorldWasp.Value)
            {
                return 20f;
            }

            if (stableEventId == AudioProjectIds.Events.WorldLakeAmbience.Value)
            {
                return 150f;
            }

            return 1200f;
        }

        private static float GetFallbackVolume(string stableEventId, bool vehicle)
        {
            if (vehicle)
            {
                return 0.25f;
            }

            if (string.Equals(
                    stableEventId,
                    AudioProjectIds.Events.WeatherRainSheltered.Value,
                    StringComparison.Ordinal))
            {
                return 0.08f;
            }

            if (string.Equals(
                    stableEventId,
                    AudioProjectIds.Events.WeatherRainInterior.Value,
                    StringComparison.Ordinal))
            {
                return 0.03f;
            }

            return 0.18f;
        }

        private static Dictionary<FallbackClipKind, AudioClip> BuildFallbackClips()
        {
            var result = new Dictionary<FallbackClipKind, AudioClip>();
            foreach (FallbackClipKind kind in Enum.GetValues(typeof(FallbackClipKind)))
            {
                string path = $"{FallbackRoot}/{kind}.wav";
                File.WriteAllBytes(ToAbsolutePath(path), CreateWave(kind));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                result.Add(kind, AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            return result;
        }

        private static byte[] CreateWave(FallbackClipKind kind)
        {
            const int sampleRate = 22050;
            float seconds = kind == FallbackClipKind.VehicleStart ||
                            kind == FallbackClipKind.Interaction
                ? 0.35f
                : 2f;
            int sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            var pcm = new short[sampleCount];
            uint noise = 0x6d2b79f5u + (uint)kind * 7919u;
            float filtered = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                noise = noise * 1664525u + 1013904223u;
                float white = ((noise >> 8) / 8388607.5f) - 1f;
                filtered = Mathf.Lerp(filtered, white, kind == FallbackClipKind.Rain ? 0.42f : 0.045f);
                float sample;
                switch (kind)
                {
                    case FallbackClipKind.VehicleLoop:
                        sample = Mathf.Sin(time * 2f * Mathf.PI * 88f) * 0.22f +
                                 Mathf.Sin(time * 2f * Mathf.PI * 176f) * 0.08f;
                        break;
                    case FallbackClipKind.VehicleStart:
                        sample = Mathf.Sin(time * 2f * Mathf.PI * (70f + time * 520f)) *
                                 Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / seconds)) * 0.3f;
                        break;
                    case FallbackClipKind.Rain:
                        sample = filtered * 0.16f;
                        break;
                    case FallbackClipKind.Wind:
                        sample = filtered * 0.18f;
                        break;
                    case FallbackClipKind.World:
                        sample = filtered * 0.07f +
                                 Mathf.Sin(time * 2f * Mathf.PI * 880f) *
                                 Mathf.Max(0f, Mathf.Sin(time * 2f * Mathf.PI * 0.7f)) * 0.035f;
                        break;
                    default:
                        sample = filtered * Mathf.Exp(-time * 18f) * 0.35f;
                        break;
                }

                pcm[index] = (short)Mathf.RoundToInt(
                    Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = sampleCount * sizeof(short);
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                for (int index = 0; index < pcm.Length; index++)
                {
                    writer.Write(pcm[index]);
                }

                return stream.ToArray();
            }
        }

        private static AudioEventMapEntry Event(
            AudioEventId id,
            string name,
            string bank,
            bool spatialized = true,
            bool allowMultiple = true) =>
            new AudioEventMapEntry(id.Value, name, bank, spatialized, allowMultiple);

        private static AudioParameterMapEntry Parameter(
            AudioParameterId id,
            string name,
            float minimum,
            float maximum,
            float defaultValue,
            float deadband = 0.001f) =>
            new AudioParameterMapEntry(
                id.Value,
                name,
                minimum,
                maximum,
                defaultValue,
                deadband);

        private static WwiseBackendNameMapEntry Name(string id, string name) =>
            new WwiseBackendNameMapEntry(id, name);

        private static T CreateOrReplaceAsset<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T EnsureComponent<T>(GameObject owner)
            where T : Component => owner.GetComponent<T>() ?? owner.AddComponent<T>();

        private static void EnsureFolder(string assetFolder)
        {
            Directory.CreateDirectory(ToAbsolutePath(assetFolder));
            AssetDatabase.Refresh();
        }

        private static string ToAbsolutePath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private enum FallbackClipKind
        {
            VehicleLoop,
            VehicleStart,
            Rain,
            Wind,
            World,
            Interaction,
        }
    }
}
