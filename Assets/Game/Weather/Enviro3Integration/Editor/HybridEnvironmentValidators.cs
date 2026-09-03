using System;
using System.Collections.Generic;
using System.IO;
using Enviro;
using MSC.Audio.Composition;
using MSC.Weather.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public enum EnvironmentValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct EnvironmentValidationIssue
    {
        public EnvironmentValidationIssue(
            EnvironmentValidationSeverity severity,
            string code,
            string message,
            string hierarchyPath)
        {
            Severity = severity;
            Code = code;
            Message = message;
            HierarchyPath = hierarchyPath;
        }

        public EnvironmentValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string HierarchyPath { get; }

        public override string ToString() =>
            $"[{Severity}] {Code}: {Message} ({HierarchyPath})";
    }

    public static class EnvironmentOwnershipValidator
    {
        [MenuItem("Tools/MSC/Environment/Validate Environment Ownership")]
        public static void ValidateMenu()
        {
            IReadOnlyList<EnvironmentValidationIssue> issues = ValidateLoaded();
            Log("Environment ownership", issues);
        }

        public static IReadOnlyList<EnvironmentValidationIssue> ValidateLoaded()
        {
            var issues = new List<EnvironmentValidationIssue>(32);
            EnviroManager[] managers = Object.FindObjectsByType<EnviroManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            NativeHdrpWeatherBridge[] nativeBridges =
                Object.FindObjectsByType<NativeHdrpWeatherBridge>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HybridEnvironmentMarker[] markers =
                Object.FindObjectsByType<HybridEnvironmentMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            WeatherZoneStreamingBinder[] zoneStreamingBinders =
                Object.FindObjectsByType<WeatherZoneStreamingBinder>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int authoredManagerCount = CountAuthoredEnabled(managers);

            AddCountIssue(
                issues,
                "OWN-MANAGER",
                "enabled authored EnviroManager",
                authoredManagerCount,
                1);
            if (authoredManagerCount != 1)
            {
                for (int index = 0; index < managers.Length; index++)
                {
                    EnviroManager candidate = managers[index];
                    issues.Add(Issue(
                        EnvironmentValidationSeverity.Info,
                        "OWN-MANAGER-STATE",
                        "EnviroManager state: enabled=" + candidate.enabled +
                        ", activeSelf=" + candidate.gameObject.activeSelf +
                        ", activeInHierarchy=" +
                        candidate.gameObject.activeInHierarchy +
                        ", sceneLoaded=" + candidate.gameObject.scene.isLoaded + ".",
                        candidate));
                }
            }
            AddCountIssue(
                issues,
                "OWN-HDRP-BRIDGE",
                "active NativeHdrpWeatherBridge",
                CountActive(nativeBridges),
                markers.Length > 0 ? 1 : 0);
            AddCountIssue(
                issues,
                "OWN-HYBRID-MARKER",
                "active HybridEnvironmentMarker",
                CountActive(markers),
                markers.Length > 0 ? 1 : 0);
            AddCountIssue(
                issues,
                "OWN-ZONE-STREAMING",
                "active WeatherZoneStreamingBinder",
                CountActive(zoneStreamingBinders),
                markers.Length > 0 ? 1 : 0);

            Volume[] volumes = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            int fogOwners = 0;
            int exposureOwners = 0;
            int hdrpCloudOwners = 0;
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume volume = volumes[index];
                VolumeProfile profile = volume.sharedProfile;
                if (!volume.enabled || profile == null)
                {
                    continue;
                }

                if (profile.TryGet(out Fog fog) && fog.active &&
                    fog.enabled.overrideState && fog.enabled.value)
                {
                    fogOwners++;
                }

                if (profile.TryGet(out Exposure exposure) && exposure.active)
                {
                    exposureOwners++;
                    if (!exposure.mode.overrideState ||
                        exposure.mode.value != ExposureMode.Fixed)
                    {
                        issues.Add(Issue(
                            EnvironmentValidationSeverity.Error,
                            "OWN-EXPOSURE-METERING",
                            "Production Exposure must use camera-independent Fixed mode.",
                            volume));
                    }
                }

                if ((profile.TryGet(out VolumetricClouds volumetricClouds) &&
                     volumetricClouds.active) ||
                    (profile.TryGet(out CloudLayer cloudLayer) && cloudLayer.active))
                {
                    hdrpCloudOwners++;
                }
            }

            AddCountIssue(issues, "OWN-FOG", "active native HDRP Fog", fogOwners, 1);
            AddCountIssue(
                issues,
                "OWN-EXPOSURE",
                "active native HDRP Exposure",
                exposureOwners,
                1);

            bool enviroCloudsActive = false;
            for (int index = 0; index < managers.Length; index++)
            {
                EnviroManager manager = managers[index];
                if (!manager.enabled || !manager.gameObject.activeSelf)
                {
                    continue;
                }

                Enviro3EnvironmentAdapter adapter =
                    manager.GetComponent<Enviro3EnvironmentAdapter>();
                if (adapter != null && adapter.HybridNativeHdrpOwnership)
                {
                    if (manager.Fog != null && manager.Fog.Settings != null &&
                        (manager.Fog.Settings.controlHDRPFog ||
                         manager.Fog.Settings.controlHDRPVolumetrics ||
                         manager.Fog.Settings.fog))
                    {
                        issues.Add(Issue(
                            EnvironmentValidationSeverity.Error,
                            "OWN-ENVIRO-FOG",
                            "Hybrid Enviro adapter still has an active Fog writer.",
                            manager));
                    }

                    if (manager.Lighting != null &&
                        manager.Lighting.Settings != null &&
                        manager.Lighting.Settings.controlExposure)
                    {
                        issues.Add(Issue(
                            EnvironmentValidationSeverity.Error,
                            "OWN-ENVIRO-EXPOSURE",
                            "Hybrid Enviro adapter still controls HDRP Exposure.",
                            manager));
                    }
                }

                enviroCloudsActive |= manager.VolumetricClouds != null ||
                                      manager.FlatClouds != null;
                AudioSource[] sources = manager.GetComponentsInChildren<AudioSource>(true);
                for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    if (sources[sourceIndex].enabled &&
                        sources[sourceIndex].clip != null &&
                        sources[sourceIndex].volume > 0f)
                    {
                        issues.Add(Issue(
                            EnvironmentValidationSeverity.Warning,
                            "OWN-ENVIRO-AUDIO",
                            "Enviro AudioSource can duplicate project weather audio.",
                            sources[sourceIndex]));
                    }
                }
            }

            if (enviroCloudsActive && hdrpCloudOwners > 0)
            {
                issues.Add(new EnvironmentValidationIssue(
                    EnvironmentValidationSeverity.Error,
                    "OWN-CLOUDS",
                    "Enviro clouds and native HDRP clouds are both active.",
                    "loaded scenes"));
            }

            if (markers.Length > 0 &&
                Object.FindObjectsByType<Enviro3ShelterRemovalBridge>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0)
            {
                issues.Add(new EnvironmentValidationIssue(
                    EnvironmentValidationSeverity.Error,
                    "OWN-LEGACY-HYBRID",
                    "Legacy shelter removal and hybrid environment are active together.",
                    "loaded scenes"));
            }

            return issues;
        }

        private static int CountActive<T>(T[] components) where T : Component
        {
            int count = 0;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index].gameObject.activeInHierarchy &&
                    (!(components[index] is Behaviour behaviour) || behaviour.enabled))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountAuthoredEnabled<T>(T[] components)
            where T : Behaviour
        {
            int count = 0;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index].enabled &&
                    components[index].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddCountIssue(
            ICollection<EnvironmentValidationIssue> issues,
            string code,
            string label,
            int actual,
            int expected)
        {
            if (actual != expected)
            {
                issues.Add(new EnvironmentValidationIssue(
                    EnvironmentValidationSeverity.Error,
                    code,
                    $"Expected {expected} {label}, found {actual}.",
                    "loaded scenes"));
            }
        }

        internal static EnvironmentValidationIssue Issue(
            EnvironmentValidationSeverity severity,
            string code,
            string message,
            Component component) => new EnvironmentValidationIssue(
                severity,
                code,
                message,
                component != null ? HierarchyPath(component.transform) : "missing");

        internal static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        internal static void Log(
            string title,
            IReadOnlyList<EnvironmentValidationIssue> issues)
        {
            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].Severity == EnvironmentValidationSeverity.Error)
                {
                    errors++;
                    Debug.LogError(issues[index].ToString());
                }
                else if (issues[index].Severity == EnvironmentValidationSeverity.Warning)
                {
                    warnings++;
                    Debug.LogWarning(issues[index].ToString());
                }
                else
                {
                    Debug.Log(issues[index].ToString());
                }
            }

            Debug.Log($"{title}: errors={errors}, warnings={warnings}, total={issues.Count}");
        }
    }

    public static class WeatherZoneValidator
    {
        private const string HomeHouseShelterStableId =
            "weather.shelter.home.house.interior.v1";

        [MenuItem("Tools/MSC/Environment/Validate Weather Zones")]
        public static void ValidateMenu()
        {
            IReadOnlyList<EnvironmentValidationIssue> issues = ValidateLoaded();
            EnvironmentOwnershipValidator.Log("Weather zones", issues);
        }

        public static IReadOnlyList<EnvironmentValidationIssue> ValidateLoaded()
        {
            var issues = new List<EnvironmentValidationIssue>(64);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            WeatherZone[] zones = Object.FindObjectsByType<WeatherZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            WeatherFogVoidAuthoring[] fogVoids =
                Object.FindObjectsByType<WeatherFogVoidAuthoring>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            WeatherZoneStreamingBinder[] binders =
                Object.FindObjectsByType<WeatherZoneStreamingBinder>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int binderIndex = 0;
                 binderIndex < binders.Length;
                 binderIndex++)
            {
                WeatherZoneStreamingBinder binder = binders[binderIndex];
                if (binder.Catalog == null)
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "ZONE-CATALOG-MISSING",
                        "WeatherZoneStreamingBinder has no project-owned catalog.",
                        binder));
                    continue;
                }

                IReadOnlyList<string> catalogIssues =
                    binder.Catalog.ValidateConfiguration();
                for (int issueIndex = 0;
                     issueIndex < catalogIssues.Count;
                     issueIndex++)
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "ZONE-CATALOG-CONFIG",
                        catalogIssues[issueIndex],
                        binder));
                }
            }
            for (int index = 0; index < zones.Length; index++)
            {
                WeatherZone zone = zones[index];
                if (!zone.IsConfigured)
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "ZONE-CONFIG",
                        "WeatherZone is missing ID, profile or explicit colliders.",
                        zone));
                }

                if (!ids.Add(zone.StableId))
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "ZONE-DUPLICATE",
                        "Duplicate WeatherZone stable ID: " + zone.StableId,
                        zone));
                }

                if (zone.Profile != null &&
                    (zone.Profile.Kind == WeatherZoneKind.ClosedInterior ||
                     zone.Profile.Kind == WeatherZoneKind.VehicleCabin) &&
                    !HasFogVoid(zone, fogVoids))
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "ZONE-FOG-VOID",
                        "Closed zone has no explicit Local Volumetric Fog void.",
                        zone));
                }

                if (string.Equals(
                        zone.StableId,
                        HomeHouseShelterStableId,
                        StringComparison.Ordinal))
                {
                    ValidateHomeHouseCompoundCoverage(zone, issues);
                }
            }

            WeatherPortal[] portals = Object.FindObjectsByType<WeatherPortal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            ids.Clear();
            for (int index = 0; index < portals.Length; index++)
            {
                WeatherPortal portal = portals[index];
                if (!portal.IsConfigured)
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "PORTAL-CONFIG",
                        "WeatherPortal is missing a zone, provider or geometry.",
                        portal));
                }

                if (!ids.Add(portal.StableId))
                {
                    issues.Add(EnvironmentOwnershipValidator.Issue(
                        EnvironmentValidationSeverity.Error,
                        "PORTAL-DUPLICATE",
                        "Duplicate WeatherPortal stable ID: " + portal.StableId,
                        portal));
                }
            }

            Enviro3WeatherZoneRemovalBridge[] removalBridges =
                Object.FindObjectsByType<Enviro3WeatherZoneRemovalBridge>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (zones.Length > 0 && removalBridges.Length != 1)
            {
                issues.Add(new EnvironmentValidationIssue(
                    EnvironmentValidationSeverity.Error,
                    "ZONE-PRECIPITATION",
                    "Weather zones require exactly one Enviro precipitation-removal bridge.",
                    "loaded scenes"));
            }
            else if (removalBridges.Length == 1 &&
                     removalBridges[0].ExposureResolver == null)
            {
                issues.Add(EnvironmentOwnershipValidator.Issue(
                    EnvironmentValidationSeverity.Error,
                    "ZONE-PARTICLE-EXPOSURE",
                    "Enviro precipitation removal is not connected to the " +
                    "listener weather-exposure resolver.",
                    removalBridges[0]));
            }

            ProductionAudioComposition[] audioCompositions =
                Object.FindObjectsByType<ProductionAudioComposition>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (audioCompositions.Length == 1 &&
                audioCompositions[0].WeatherExposureResolver == null)
            {
                issues.Add(EnvironmentOwnershipValidator.Issue(
                    EnvironmentValidationSeverity.Error,
                    "ZONE-AUDIO-EXPOSURE",
                    "Production audio is not connected to the listener " +
                    "weather-exposure resolver.",
                    audioCompositions[0]));
            }

            return issues;
        }

        private static void ValidateHomeHouseCompoundCoverage(
            WeatherZone zone,
            ICollection<EnvironmentValidationIssue> issues)
        {
            int enabledBoxes = 0;
            for (int index = 0; index < zone.VolumeColliders.Count; index++)
            {
                if (zone.VolumeColliders[index] is BoxCollider box &&
                    box.enabled && box.isTrigger)
                {
                    enabledBoxes++;
                }
            }

            if (enabledBoxes != 3)
            {
                issues.Add(EnvironmentOwnershipValidator.Issue(
                    EnvironmentValidationSeverity.Error,
                    "ZONE-HOME-COMPOUND",
                    "Home interior requires three enabled donor NoRain " +
                    "compound BoxCollider volumes; found " + enabledBoxes + ".",
                    zone));
                return;
            }

            Vector3[] elevatedInteriorProbes =
            {
                new Vector3(162.06999f, 3.8f, -1037.315f),
                new Vector3(154.79999f, 3.8f, -1038.375f),
                new Vector3(165.29572f, 3.8f, -1035.0538f),
            };
            for (int index = 0; index < elevatedInteriorProbes.Length; index++)
            {
                if (zone.ContainsAuthoredGeometry(
                        elevatedInteriorProbes[index]))
                {
                    continue;
                }

                issues.Add(EnvironmentOwnershipValidator.Issue(
                    EnvironmentValidationSeverity.Error,
                    "ZONE-HOME-ELEVATED-PROBE",
                    "Home compound zone misses elevated interior probe " +
                    (index + 1) + ".",
                    zone));
            }
        }

        private static bool HasFogVoid(
            WeatherZone zone,
            WeatherFogVoidAuthoring[] fogVoids)
        {
            for (int index = 0; index < fogVoids.Length; index++)
            {
                if (fogVoids[index].WeatherZone == zone)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class EnvironmentAuditGenerator
    {
        private const string OutputPath =
            "Assets/Documentation/Environment/Enviro_HDRP_Current_State_Audit.generated.md";

        [MenuItem("Tools/MSC/Environment/Generate Environment Audit")]
        public static void Generate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            IReadOnlyList<EnvironmentValidationIssue> ownership =
                EnvironmentOwnershipValidator.ValidateLoaded();
            IReadOnlyList<EnvironmentValidationIssue> zones =
                WeatherZoneValidator.ValidateLoaded();
            using (var writer = new StreamWriter(OutputPath, false))
            {
                writer.WriteLine("# Generated Environment Audit");
                writer.WriteLine();
                writer.WriteLine("Unity: `" + Application.unityVersion + "`");
                writer.WriteLine("Generated UTC: `" +
                                 DateTime.UtcNow.ToString("O") + "`");
                writer.WriteLine();
                writer.WriteLine("## Ownership issues");
                WriteIssues(writer, ownership);
                writer.WriteLine();
                writer.WriteLine("## Zone issues");
                WriteIssues(writer, zones);
            }

            AssetDatabase.ImportAsset(OutputPath);
            Debug.Log("Environment audit generated: " + OutputPath);
        }

        private static void WriteIssues(
            TextWriter writer,
            IReadOnlyList<EnvironmentValidationIssue> issues)
        {
            if (issues.Count == 0)
            {
                writer.WriteLine("No issues in currently loaded scenes.");
                return;
            }

            for (int index = 0; index < issues.Count; index++)
            {
                writer.WriteLine("- " + issues[index]);
            }
        }
    }

    public static class HybridEnvironmentValidationBatch
    {
        public static void ValidateBootstrap()
        {
            Scene existing = SceneManager.GetSceneByPath(
                HybridEnvironmentMigrationTool.BootstrapScenePath);
            bool openedHere = !existing.IsValid() || !existing.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(
                    HybridEnvironmentMigrationTool.BootstrapScenePath,
                    OpenSceneMode.Additive)
                : existing;
            try
            {
                IReadOnlyList<EnvironmentValidationIssue> ownership =
                    EnvironmentOwnershipValidator.ValidateLoaded();
                IReadOnlyList<EnvironmentValidationIssue> zones =
                    WeatherZoneValidator.ValidateLoaded();
                EnvironmentOwnershipValidator.Log(
                    "Bootstrap environment ownership",
                    ownership);
                EnvironmentOwnershipValidator.Log(
                    "Bootstrap weather zones",
                    zones);
                int errors = CountErrors(ownership) + CountErrors(zones);
                if (errors > 0)
                {
                    throw new InvalidOperationException(
                        "Bootstrap hybrid environment validation failed with " +
                        errors + " error(s).");
                }
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static int CountErrors(
            IReadOnlyList<EnvironmentValidationIssue> issues)
        {
            int count = 0;
            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].Severity == EnvironmentValidationSeverity.Error)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
