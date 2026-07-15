using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using MSC.World.Data;
using MSC.World.Partition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public sealed class WorldRemasterValidationResult
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool Passed => errors.Count == 0;

        public void AddError(string message) => errors.Add(message);
        public void AddWarning(string message) => warnings.Add(message);
    }

    public static class ProductionCellValidationTool
    {
        private static readonly string[] ProductionAssets =
        {
            WorldRemasterPaths.GaragePrefab,
            WorldRemasterPaths.HousePrefab,
            WorldRemasterPaths.InteriorPrefab,
            WorldRemasterPaths.TerrainRoadPrefab,
            WorldRemasterPaths.TreePrefab,
            WorldRemasterPaths.VegetationPrefab,
            WorldRemasterPaths.PropsPrefab,
            WorldRemasterPaths.PilotZonePrefab,
            WorldRemasterPaths.PilotCellScene,
            WorldRemasterPaths.PilotPlaytestScene
        };

        [MenuItem("Tools/MSC Remake/World Remaster/Validate Selected Zone")]
        public static WorldRemasterValidationResult Validate()
        {
            var result = new WorldRemasterValidationResult();
            ValidateRequiredAssets(result);
            ValidateRegistry(result);
            ValidateDependencies(result);
            ValidatePilotPrefab(result);
            ValidateProductionCell(result);
            ValidateAssemblyIntegration(result);
            ValidateBuildSettings(result);
            WriteReports(result);
            if (result.Passed)
            {
                Debug.Log($"WORLD_REMASTER_05A_VALIDATION_OK warnings={result.Warnings.Count}");
            }
            else
            {
                Debug.LogError($"WORLD_REMASTER_05A_VALIDATION_FAILED errors={result.Errors.Count} warnings={result.Warnings.Count}");
            }

            return result;
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Validate Selected Replacement")]
        public static void ValidateSelectedReplacement() => ShowResult(Validate());

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("World Remaster batch validation requires batch mode.");
            }

            WorldRemasterValidationResult result = Validate();
            if (!result.Passed)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
            }
        }

        public static IReadOnlyList<string> FindDonorDependencies()
        {
            var violations = new List<string>();
            foreach (string asset in ProductionAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                {
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(asset, recursive: true))
                {
                    string normalized = dependency.Replace('\\', '/');
                    if (normalized.Contains("/LegacyImport/ReferenceOnly/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("/Imported/DonorGenerated/", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add(asset + " -> " + normalized);
                    }
                }
            }

            return violations;
        }

        private static void ValidateRequiredAssets(WorldRemasterValidationResult result)
        {
            foreach (string asset in ProductionAssets.Append(WorldRemasterPaths.RegistryAsset))
            {
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                {
                    result.AddError("Required 05A asset is missing: " + asset);
                }
            }

            foreach (string path in new[]
                     {
                         WorldRemasterPaths.ReplacementLedger,
                         WorldRemasterPaths.ArtBacklog,
                         WorldRemasterPaths.ZoneStatus
                     })
            {
                if (!File.Exists(path))
                {
                    result.AddError("Required machine-readable report is missing: " + path);
                }
            }
        }

        private static void ValidateRegistry(WorldRemasterValidationResult result)
        {
            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            foreach (string error in WorldProductionRegistryValidation.Validate(registry))
            {
                result.AddError(error);
            }

            if (registry == null)
            {
                return;
            }

            IReadOnlyList<WorldEntityPlacement> entities = WorldRemasterRegistryBuilder.LoadEntities();
            if (registry.Records.Count != entities.Count)
            {
                result.AddError($"Registry record count {registry.Records.Count} does not match reference count {entities.Count}.");
            }

            HashSet<string> sourceIds = entities.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            int mapped = 0;
            foreach (WorldProductionAssetRecord record in registry.Records)
            {
                if (!sourceIds.Contains(record.StableWorldId))
                {
                    result.AddError("Registry binding has no reference record: " + record.StableWorldId);
                }

                if (record.HasProductionReplacement)
                {
                    mapped++;
                    if (AssetDatabase.LoadMainAssetAtPath(record.ProductionPrefab) == null)
                    {
                        result.AddError("Mapped production prefab is missing: " + record.ProductionPrefab);
                    }
                }
                else if (string.IsNullOrWhiteSpace(record.ManualArtDependency))
                {
                    result.AddError("Unassigned record has no art-backlog dependency: " + record.StableWorldId);
                }
            }

            if (mapped != WorldRemasterRegistryBuilder.PilotMappedRecordCount)
            {
                result.AddError($"Expected {WorldRemasterRegistryBuilder.PilotMappedRecordCount} pilot bindings, found {mapped}.");
            }

            int sourceZoneCount = entities.Select(entity => entity.CellId).Distinct(StringComparer.Ordinal).Count();
            if (registry.Zones.Count != sourceZoneCount)
            {
                result.AddError($"Zone registry count {registry.Zones.Count} does not match discovered set {sourceZoneCount}.");
            }

            WorldProductionZoneRecord pilot = registry.Zones.FirstOrDefault(zone => zone.ZoneId == WorldRemasterPaths.PilotZoneId);
            if (pilot == null || pilot.ReferenceRecordCount != 671 || pilot.MappedRecordCount != mapped)
            {
                result.AddError("Pilot zone registry counts are inconsistent.");
            }

            if (registry.Records.Any(record => record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified))
            {
                result.AddError("First 05A pass must not label pilot content Approved/Verified before manual review.");
            }
        }

        private static void ValidateDependencies(WorldRemasterValidationResult result)
        {
            foreach (string violation in FindDonorDependencies())
            {
                result.AddError("Production donor dependency: " + violation);
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { WorldRemasterPaths.MaterialRoot });
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    result.AddError("Production material could not be loaded: " + path);
                    continue;
                }

                if (!path.EndsWith("WR_ReferenceOverlay.mat", StringComparison.Ordinal) &&
                    (material.shader == null || !material.shader.name.StartsWith("HDRP/", StringComparison.Ordinal)))
                {
                    result.AddError("Production material is not HDRP-compatible: " + path);
                }
            }
        }

        private static void ValidatePilotPrefab(WorldRemasterValidationResult result)
        {
            GameObject pilot = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            if (pilot == null)
            {
                return;
            }

            WorldRemasterPilotMarker marker = pilot.GetComponent<WorldRemasterPilotMarker>();
            if (marker == null || marker.ZoneId != WorldRemasterPaths.PilotZoneId || !marker.DonorBinaryIndependent)
            {
                result.AddError("Pilot marker is missing or invalid.");
            }
            else if (marker.MappedReferenceRecordCount != WorldRemasterRegistryBuilder.PilotMappedRecordCount ||
                     marker.TotalReferenceRecordCount != 671)
            {
                result.AddError("Pilot marker coverage is inconsistent with the registry.");
            }

            WorldHingedArchitecture[] hinges = pilot.GetComponentsInChildren<WorldHingedArchitecture>(true);
            if (hinges.Length != 6)
            {
                result.AddError($"Pilot must contain 6 explicit moving-architecture hinges; found {hinges.Length}.");
            }

            foreach (WorldHingedArchitecture hinge in hinges)
            {
                InteractionTargetHost host = hinge.GetComponent<InteractionTargetHost>();
                if (host == null || !host.TryGetCapability<WorldHingedArchitecture>(out _))
                {
                    result.AddError("Moving architecture is not exposed through InteractionTargetHost: " + hinge.name);
                }
            }

            LODGroup[] lodGroups = pilot.GetComponentsInChildren<LODGroup>(true);
            if (lodGroups.Length < 64)
            {
                result.AddError($"Pilot vegetation LOD coverage is incomplete; expected at least 64 groups, found {lodGroups.Length}.");
            }

            MeshCollider[] meshColliders = pilot.GetComponentsInChildren<MeshCollider>(true);
            if (meshColliders.Count(collider => collider.sharedMesh != null) < 3)
            {
                result.AddError("Terrain, road and driveway production mesh colliders are required.");
            }

            if (WorldRemasterPaths.GarageDoorClearWidthMeters < WorldRemasterPaths.RepresentativeVehicleWidthMeters + 0.5f ||
                WorldRemasterPaths.GarageDoorClearHeightMeters < WorldRemasterPaths.RepresentativeVehicleHeightMeters + 0.25f)
            {
                result.AddError("Authored garage opening does not meet representative vehicle clearance.");
            }
        }

        private static void ValidateProductionCell(WorldRemasterValidationResult result)
        {
            Scene scene = EditorSceneManager.OpenScene(WorldRemasterPaths.PilotCellScene, OpenSceneMode.Single);
            WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .FirstOrDefault();
            if (marker == null)
            {
                result.AddError("Production cell does not contain a pilot marker.");
                return;
            }

            Transform cellRoot = marker.transform;
            if (Vector3.Distance(cellRoot.position, WorldRemasterPaths.HomeGarageAnchor) > 0.01f ||
                Quaternion.Angle(cellRoot.rotation, WorldRemasterPaths.HomeGarageRotation) > 0.1f)
            {
                result.AddError("Production cell world anchor/rotation drifted from the reviewed home-garage placement.");
            }

            WorldCellIndex assigned = WorldCellMembershipUtility.FromPosition(cellRoot.position, 512f);
            if (assigned.Id != WorldRemasterPaths.PilotZoneId)
            {
                result.AddError($"Pilot anchor is assigned to {assigned.Id}, expected {WorldRemasterPaths.PilotZoneId}.");
            }

            ValidateGarageDoorParity(scene, result);
        }

        private static void ValidateGarageDoorParity(Scene scene, WorldRemasterValidationResult result)
        {
            IReadOnlyList<WorldEntityPlacement> entities = WorldRemasterRegistryBuilder.LoadEntities();
            Dictionary<string, WorldEntityPlacement> byId = entities.ToDictionary(entity => entity.StableId, StringComparer.Ordinal);
            ValidateDoor(
                scene,
                "GarageDoorLeft",
                byId["250d1cb74558e7c7e27e5e860981c938"].Bounds,
                result);
            ValidateDoor(
                scene,
                "GarageDoorRight",
                byId["e8660bda40e1d2946e004456e245c803"].Bounds,
                result);
        }

        private static void ValidateDoor(
            Scene scene,
            string doorName,
            Bounds expected,
            WorldRemasterValidationResult result)
        {
            WorldHingedArchitecture hinge = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldHingedArchitecture>(true))
                .FirstOrDefault(candidate => candidate.name == doorName);
            Renderer panel = hinge != null ? hinge.GetComponentInChildren<Renderer>(true) : null;
            if (panel == null)
            {
                result.AddError("Garage door renderer is missing: " + doorName);
                return;
            }

            float centerDeviation = Vector3.Distance(expected.center, panel.bounds.center);
            if (centerDeviation > 0.35f)
            {
                result.AddError($"{doorName} center deviation {centerDeviation:0.###}m exceeds 0.35m pilot tolerance.");
            }
        }

        private static void ValidateAssemblyIntegration(WorldRemasterValidationResult result)
        {
            Scene scene = EditorSceneManager.OpenScene(WorldRemasterPaths.VehicleAssemblyScene, OpenSceneMode.Single);
            VehicleAssemblyController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleAssemblyController>(true))
                .FirstOrDefault();
            WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .FirstOrDefault();
            if (controller == null || marker == null)
            {
                result.AddError("M05 assembly scene is not integrated with the 05A pilot world.");
                return;
            }

            Transform root = controller.transform.root;
            if (Vector3.Distance(root.position, WorldRemasterPaths.HomeGarageAnchor) > 0.01f ||
                Quaternion.Angle(root.rotation, WorldRemasterPaths.HomeGarageRotation) > 0.1f)
            {
                result.AddError("Integrated M05 root is not aligned to the home-garage world anchor.");
            }

            if (root.Cast<Transform>().Any(child => child.name == "AssemblyWorkshopFloor"))
            {
                result.AddError("Legacy M05 workshop floor still overlaps the production terrain.");
            }
        }

        private static void ValidateBuildSettings(WorldRemasterValidationResult result)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int bootstrap = Array.FindIndex(scenes, scene => scene.enabled && scene.path == "Assets/Game/Bootstrap/Bootstrap.unity");
            if (bootstrap != 0)
            {
                result.AddError("Bootstrap must remain the first enabled build scene.");
            }

            foreach (string path in new[] { WorldRemasterPaths.PilotPlaytestScene, WorldRemasterPaths.PilotCellScene })
            {
                if (!scenes.Any(scene => scene.enabled && scene.path == path))
                {
                    result.AddError("Production world scene is missing from enabled Build Settings: " + path);
                }
            }

            if (scenes.Any(scene => scene.enabled && scene.path == WorldRemasterPaths.ComparisonScene))
            {
                result.AddError("World Remaster comparison scene must remain excluded from builds.");
            }
        }

        private static void WriteReports(WorldRemasterValidationResult result)
        {
            Directory.CreateDirectory(WorldRemasterPaths.DocumentationRoot);
            GameObject pilot = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            Renderer[] renderers = pilot != null ? pilot.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            Collider[] colliders = pilot != null ? pilot.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
            LODGroup[] lodGroups = pilot != null ? pilot.GetComponentsInChildren<LODGroup>(true) : Array.Empty<LODGroup>();
            int triangles = pilot == null
                ? 0
                : pilot.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null)
                    .Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                        .Sum(subMesh => (int)filter.sharedMesh.GetIndexCount(subMesh) / 3));
            long meshBytesEstimate = pilot == null
                ? 0
                : pilot.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null)
                    .Select(filter => filter.sharedMesh)
                    .Distinct()
                    .Sum(mesh => (long)mesh.vertexCount * 48L + (long)mesh.triangles.Length * sizeof(int));

            var validation = new StringBuilder();
            validation.AppendLine("# World Remaster 05A Validation Report");
            validation.AppendLine();
            validation.AppendLine("Result: **" + (result.Passed ? "PASS" : "FAIL") + "**");
            validation.AppendLine();
            validation.AppendLine($"Errors: `{result.Errors.Count}`; warnings: `{result.Warnings.Count}`.");
            validation.AppendLine();
            validation.AppendLine("## Errors");
            validation.AppendLine();
            validation.AppendLine(result.Errors.Count == 0 ? "- None." : string.Join(Environment.NewLine, result.Errors.Select(error => "- " + error)));
            validation.AppendLine();
            validation.AppendLine("## Warnings / manual gates");
            validation.AppendLine();
            validation.AppendLine("- Manual Scene/Game View visual review remains pending.");
            validation.AppendLine("- GPU/FPS and player-build memory capture were not executed by this static Editor audit.");
            foreach (string warning in result.Warnings)
            {
                validation.AppendLine("- " + warning);
            }
            AtomicWrite(WorldRemasterPaths.ValidationReport, validation.ToString());

            var performance = new StringBuilder();
            performance.AppendLine("# World Remaster Performance Report");
            performance.AppendLine();
            performance.AppendLine("Scope: static Editor audit of `WR_HomeYardPilot.prefab`; not a GPU or standalone-player capture.");
            performance.AppendLine();
            performance.AppendLine("| Metric | Value |");
            performance.AppendLine("|---|---:|");
            performance.AppendLine($"| Renderers | {renderers.Length} |");
            performance.AppendLine($"| Colliders | {colliders.Length} |");
            performance.AppendLine($"| LOD groups | {lodGroups.Length} |");
            performance.AppendLine($"| Mesh triangles (instance-counted) | {triangles} |");
            performance.AppendLine($"| Unique mesh memory estimate | {meshBytesEstimate / (1024f * 1024f):0.00} MiB |");
            performance.AppendLine();
            performance.AppendLine("Target remains 60 FPS at 1920×1080 on a mid-range Windows PC. Actual CPU/GPU time, draw calls, VRAM and frame pacing are **not measured yet**; capture is a manual gate after visual acceptance.");
            AtomicWrite(WorldRemasterPaths.PerformanceReport, performance.ToString());
        }

        private static void AtomicWrite(string path, string content)
        {
            string absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? throw new InvalidOperationException());
            string temporary = absolute + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(absolute))
            {
                File.Replace(temporary, absolute, null);
            }
            else
            {
                File.Move(temporary, absolute);
            }
        }

        private static void ShowResult(WorldRemasterValidationResult result)
        {
            string message = result.Passed
                ? $"PASS. Warnings: {result.Warnings.Count}."
                : $"FAIL. Errors: {result.Errors.Count}. See {WorldRemasterPaths.ValidationReport}.";
            EditorUtility.DisplayDialog("World Remaster validation", message, "OK");
        }
    }
}
