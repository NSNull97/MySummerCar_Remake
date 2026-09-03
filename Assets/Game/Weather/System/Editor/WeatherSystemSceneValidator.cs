using System;
using System.Collections.Generic;
using MSC.Interaction.Architecture;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using MSC.Weather.System.Local;
using MSC.Weather.System.NativeHDRP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Weather.System.Editor
{
    public enum WeatherValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct WeatherValidationIssue
    {
        public WeatherValidationIssue(
            WeatherValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
        }

        public WeatherValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
    }

    public sealed class WeatherSystemSceneValidator : EditorWindow
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";

        private readonly List<WeatherValidationIssue> issues =
            new List<WeatherValidationIssue>(64);
        private Vector2 scroll;

        [MenuItem("Tools/MSC/Weather System/Scene Validator")]
        public static void Open()
        {
            var window = GetWindow<WeatherSystemSceneValidator>();
            window.titleContent = new GUIContent("Weather Validator");
            window.minSize = new Vector2(600f, 320f);
            window.Refresh();
        }

        [MenuItem("Tools/MSC/Weather System/Validate Loaded Scenes")]
        public static void ValidateAndLog()
        {
            var result = new List<WeatherValidationIssue>(64);
            ValidateLoadedScenes(result);
            LogResult(result);
        }

        public static void ValidateBootstrapBatch()
        {
            Scene bootstrap = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            if (!bootstrap.IsValid() || !bootstrap.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Could not open Bootstrap scene at '{BootstrapScenePath}'.");
            }

            var result = new List<WeatherValidationIssue>(64);
            ValidateLoadedScenes(result);
            int errors = LogResult(result);
            if (errors > 0)
            {
                throw new InvalidOperationException(
                    $"Bootstrap weather validation found {errors} error(s).");
            }
        }

        private static int LogResult(
            IReadOnlyList<WeatherValidationIssue> result)
        {
            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < result.Count; index++)
            {
                WeatherValidationIssue issue = result[index];
                string line = $"[{issue.Code}] {issue.Message}";
                switch (issue.Severity)
                {
                    case WeatherValidationSeverity.Error:
                        errors++;
                        Debug.LogError(line, issue.Context);
                        break;
                    case WeatherValidationSeverity.Warning:
                        warnings++;
                        Debug.LogWarning(line, issue.Context);
                        break;
                    default:
                        Debug.Log(line, issue.Context);
                        break;
                }
            }

            Debug.Log(
                $"Weather System validation finished: {errors} error(s), " +
                $"{warnings} warning(s), {result.Count} total issue(s).");
            return errors;
        }

        public static void ValidateLoadedScenes(
            ICollection<WeatherValidationIssue> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            ValidateOwners(output);
            ValidateZones(output);
            ValidatePortalsAndDoors(output);
            ValidateBuildingMarkers(output);
            ValidateWwiseSpatialAudio(output);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Refresh();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(Summary(), EditorStyles.miniLabel);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < issues.Count; index++)
            {
                WeatherValidationIssue issue = issues[index];
                MessageType type = issue.Severity switch
                {
                    WeatherValidationSeverity.Error => MessageType.Error,
                    WeatherValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox(
                    $"[{issue.Code}] {issue.Message}",
                    type);
                if (issue.Context != null &&
                    GUILayout.Button("Select: " + issue.Context.name))
                {
                    Selection.activeObject = issue.Context;
                    EditorGUIUtility.PingObject(issue.Context);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            issues.Clear();
            ValidateLoadedScenes(issues);
            Repaint();
        }

        private string Summary()
        {
            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < issues.Count; index++)
            {
                errors += issues[index].Severity ==
                    WeatherValidationSeverity.Error ? 1 : 0;
                warnings += issues[index].Severity ==
                    WeatherValidationSeverity.Warning ? 1 : 0;
            }

            return $"{errors} errors, {warnings} warnings";
        }

        private static void ValidateOwners(
            ICollection<WeatherValidationIssue> output)
        {
            GameWeatherSystem[] routers = Find<GameWeatherSystem>();
            if (routers.Length > 1)
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-OWNER-001",
                    $"Loaded scenes contain {routers.Length} GameWeatherSystem routers; exactly one process owner is allowed.",
                    routers[1]);
            }

            Volume[] volumes = Find<Volume>();
            var activeWeatherVolumes = new List<Volume>(4);
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume volume = volumes[index];
                if (IsActiveGlobalWeatherVolume(volume))
                {
                    activeWeatherVolumes.Add(volume);
                }
            }

            if (activeWeatherVolumes.Count > 1)
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-OWNER-002",
                    $"{activeWeatherVolumes.Count} active global HDRP weather volumes own sky/cloud/fog simultaneously.",
                    activeWeatherVolumes[1]);
            }

            Light[] lights = Find<Light>();
            var activeSuns = new List<Light>(4);
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light.type == LightType.Directional && light.enabled &&
                    light.gameObject.activeInHierarchy &&
                    light.name.IndexOf("moon", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    activeSuns.Add(light);
                }
            }

            if (activeSuns.Count > 1)
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-OWNER-003",
                    $"{activeSuns.Count} active directional lights can act as suns. Assign one owner and classify moon lights explicitly.",
                    activeSuns[1]);
            }

            Enviro3EnvironmentAdapter[] enviro = Find<Enviro3EnvironmentAdapter>();
            NativeHDRPWeatherBackend[] native = Find<NativeHDRPWeatherBackend>();
            for (int nativeIndex = 0; nativeIndex < native.Length; nativeIndex++)
            {
                if (!native[nativeIndex].IsAttached)
                {
                    continue;
                }

                for (int enviroIndex = 0; enviroIndex < enviro.Length; enviroIndex++)
                {
                    if (enviro[enviroIndex].IsAttached)
                    {
                        Add(
                            output,
                            WeatherValidationSeverity.Error,
                            "WEATHER-OWNER-004",
                            "Enviro and Native HDRP backends are attached together. Sky, clouds, fog and exposure must have one owner.",
                            native[nativeIndex]);
                    }
                }
            }
        }

        private static void ValidateZones(
            ICollection<WeatherValidationIssue> output)
        {
            var stableIds = new Dictionary<string, UnityEngine.Object>(
                StringComparer.Ordinal);
            InteriorZone[] zones = Find<InteriorZone>();
            for (int index = 0; index < zones.Length; index++)
            {
                InteriorZone zone = zones[index];
                if (zone.Profile == null)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-ZONE-001",
                        "InteriorZone has no EnvironmentZoneProfile.",
                        zone);
                }

                ValidateStableId(zone.StableId, zone, stableIds, output);
            }

            WeatherZone[] legacyZones = Find<WeatherZone>();
            for (int index = 0; index < legacyZones.Length; index++)
            {
                WeatherZone zone = legacyZones[index];
                if (zone.GetComponent<LegacyWeatherZoneAdapter>() == null)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Warning,
                        "WEATHER-ZONE-002",
                        "Accepted WeatherZone has no non-destructive LegacyWeatherZoneAdapter, so it is invisible to the new portal graph.",
                        zone);
                }
            }
        }

        private static void ValidatePortalsAndDoors(
            ICollection<WeatherValidationIssue> output)
        {
            DoorWeatherPortalAdapter[] portals =
                Find<DoorWeatherPortalAdapter>();
            var portalIds = new Dictionary<string, UnityEngine.Object>(
                StringComparer.Ordinal);
            for (int index = 0; index < portals.Length; index++)
            {
                DoorWeatherPortalAdapter portal = portals[index];
                ValidateStableId(portal.StableId, portal, portalIds, output);
                bool hasA = portal.ZoneAComponent is IInteriorZone;
                bool hasB = portal.ZoneBComponent is IInteriorZone;
                if (!hasA && !hasB)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-PORTAL-001",
                        "Door portal has neither an interior zone nor an Outdoor side.",
                        portal);
                }

                if ((portal.ZoneAComponent != null && !hasA) ||
                    (portal.ZoneBComponent != null && !hasB))
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-PORTAL-002",
                        "Door portal endpoint does not implement IInteriorZone; it may reference an unloaded or wrong object.",
                        portal);
                }

                bool hasExplicitOutdoorEndpoint = hasA != hasB;
                if (portal.ConnectsToOutdoor != hasExplicitOutdoorEndpoint)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-PORTAL-003",
                        portal.ConnectsToOutdoor
                            ? "Door is marked as exterior, but it does not have exactly one explicit Outdoor (null) endpoint."
                            : "Door has one null endpoint but is not explicitly marked as connecting to Outdoor; author the missing side instead of guessing.",
                        portal);
                }

                if (portal.Door == null)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-PORTAL-004",
                        "DoorWeatherPortalAdapter has no HingedDoorInteractionTarget.",
                        portal);
                }
            }

            HingedDoorInteractionTarget[] doors =
                Find<HingedDoorInteractionTarget>();
            for (int index = 0; index < doors.Length; index++)
            {
                HingedDoorInteractionTarget door = doors[index];
                if (door.GetComponent<DoorWeatherPortalAdapter>() == null)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Warning,
                        "WEATHER-PORTAL-005",
                        "Gameplay door has no weather portal adapter. Door behavior is untouched, but weather cannot leak through it.",
                        door);
                }
            }
        }

        private static void ValidateBuildingMarkers(
            ICollection<WeatherValidationIssue> output)
        {
            var stableIds = new Dictionary<string, UnityEngine.Object>(
                StringComparer.Ordinal);
            WeatherBuildingEnvelope[] buildings =
                Find<WeatherBuildingEnvelope>();
            for (int index = 0; index < buildings.Length; index++)
            {
                WeatherBuildingEnvelope building = buildings[index];
                ValidateStableId(
                    building.StableId,
                    building,
                    stableIds,
                    output);
                if (!building.HasValidInteriorZone)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-BUILDING-001",
                        "WeatherBuildingEnvelope has no valid interior zone.",
                        building);
                }
            }

            WeatherPrecipitationBlocker[] blockers =
                Find<WeatherPrecipitationBlocker>();
            for (int index = 0; index < blockers.Length; index++)
            {
                WeatherPrecipitationBlocker blocker = blockers[index];
                ValidateStableId(
                    blocker.StableId,
                    blocker,
                    stableIds,
                    output);
                if (blocker.OwningZone == null)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-BUILDING-002",
                        "Precipitation blocker has no corresponding interior/shelter zone.",
                        blocker);
                }

                Collider[] colliders = blocker.BlockerColliders;
                if (colliders == null || colliders.Length == 0)
                {
                    Add(
                        output,
                        WeatherValidationSeverity.Error,
                        "WEATHER-BUILDING-003",
                        "Precipitation blocker marker has no collider geometry.",
                        blocker);
                }
            }
        }

        private static void ValidateWwiseSpatialAudio(
            ICollection<WeatherValidationIssue> output)
        {
            MonoBehaviour[] behaviours = Find<MonoBehaviour>();
            int roomCount = 0;
            var roomPortals = new List<MonoBehaviour>(8);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (string.Equals(typeName, "AkRoom", StringComparison.Ordinal))
                {
                    roomCount++;
                }
                else if (string.Equals(
                             typeName,
                             "AkRoomPortal",
                             StringComparison.Ordinal))
                {
                    roomPortals.Add(behaviour);
                }
            }

            if (roomPortals.Count > 0 && roomCount == 0)
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-WWISE-001",
                    $"Found {roomPortals.Count} Wwise portal(s), but no Wwise Room in loaded scenes.",
                    roomPortals[0]);
            }
        }

        private static bool IsActiveGlobalWeatherVolume(Volume volume)
        {
            if (volume == null || !volume.isGlobal || !volume.enabled ||
                !volume.gameObject.activeInHierarchy || volume.weight <= 0f ||
                volume.sharedProfile == null)
            {
                return false;
            }

            IReadOnlyList<VolumeComponent> components =
                volume.sharedProfile.components;
            for (int index = 0; index < components.Count; index++)
            {
                string name = components[index]?.GetType().Name ?? string.Empty;
                if (name == "VisualEnvironment" || name == "PhysicallyBasedSky" ||
                    name == "VolumetricClouds" || name == "Fog" ||
                    name == "Exposure" || name == "WhiteBalance" ||
                    name == "ColorAdjustments" || name == "Tonemapping" ||
                    name == "IndirectLightingController")
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateStableId(
            string stableId,
            UnityEngine.Object owner,
            IDictionary<string, UnityEngine.Object> seen,
            ICollection<WeatherValidationIssue> output)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-ID-001",
                    "Weather authoring component has an empty stable ID.",
                    owner);
                return;
            }

            if (seen.TryGetValue(stableId, out UnityEngine.Object previous))
            {
                Add(
                    output,
                    WeatherValidationSeverity.Error,
                    "WEATHER-ID-002",
                    $"Duplicate weather stable ID '{stableId}' (also used by '{previous.name}').",
                    owner);
                return;
            }

            seen.Add(stableId, owner);
        }

        private static T[] Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        private static void Add(
            ICollection<WeatherValidationIssue> output,
            WeatherValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context) =>
            output.Add(new WeatherValidationIssue(
                severity,
                code,
                message,
                context));
    }
}
