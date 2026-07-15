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

    public sealed class WorldRemasterStaticMetrics
    {
        public int RendererCount { get; internal set; }
        public int ColliderCount { get; internal set; }
        public int LodGroupCount { get; internal set; }
        public int TriangleCount { get; internal set; }
        public long UniqueMeshBytesEstimate { get; internal set; }
        public int PilotRendererCount { get; internal set; }
        public int PilotColliderCount { get; internal set; }
        public int PilotLodGroupCount { get; internal set; }
        public int PilotTriangleCount { get; internal set; }
        public int NextZoneRendererCount { get; internal set; }
        public int NextZoneColliderCount { get; internal set; }
        public int NextZoneLodGroupCount { get; internal set; }
        public int NextZoneTriangleCount { get; internal set; }
        public int DefaultLayerColliderCount { get; internal set; }
        public int NullPhysicsMaterialColliderCount { get; internal set; }
        public float HomePierSeamOverlapMeters { get; internal set; } = float.NaN;
        public float PierMaximumColliderGapMeters { get; internal set; } = float.NaN;
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
            WorldRemasterPaths.PilotPlaytestScene,
            WorldRemasterPaths.HedgePrefab,
            WorldRemasterPaths.PierPrefab,
            WorldRemasterPaths.ShorelinePrefab,
            WorldRemasterPaths.NextZonePrefab,
            WorldRemasterPaths.NextZoneCellScene,
            WorldRemasterPaths.NextZonePlaytestScene
        };

        [MenuItem("Tools/MSC Remake/World Remaster/Validate Selected Zone")]
        public static WorldRemasterValidationResult Validate() => Validate(writeReports: true);

        public static WorldRemasterValidationResult Validate(bool writeReports)
        {
            var result = new WorldRemasterValidationResult();
            ValidateRequiredAssets(result);
            ValidateRegistry(result);
            ValidateDependencies(result);
            ValidatePilotPrefab(result);
            ValidateNextZonePrefab(result);
            ValidateProductionCell(result);
            ValidateNextZoneCell(result);
            ValidateAssemblyIntegration(result);
            ValidateBuildSettings(result);
            ValidateBatchCaptures(result);
            if (writeReports)
            {
                WriteReports(result);
            }
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

        public static WorldRemasterStaticMetrics MeasureStaticContent()
        {
            GameObject pilot = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            GameObject nextZone = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NextZonePrefab);
            GameObject[] scopedPrefabs = new[] { pilot, nextZone }.Where(prefab => prefab != null).ToArray();
            Renderer[] renderers = scopedPrefabs
                .SelectMany(prefab => prefab.GetComponentsInChildren<Renderer>(true))
                .ToArray();
            Collider[] colliders = scopedPrefabs
                .SelectMany(prefab => prefab.GetComponentsInChildren<Collider>(true))
                .ToArray();
            LODGroup[] lodGroups = scopedPrefabs
                .SelectMany(prefab => prefab.GetComponentsInChildren<LODGroup>(true))
                .ToArray();
            MeshFilter[] meshFilters = scopedPrefabs
                .SelectMany(prefab => prefab.GetComponentsInChildren<MeshFilter>(true))
                .Where(filter => filter.sharedMesh != null)
                .ToArray();
            MeshFilter[] pilotMeshFilters = pilot != null
                ? pilot.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null).ToArray()
                : Array.Empty<MeshFilter>();
            MeshFilter[] nextMeshFilters = nextZone != null
                ? nextZone.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null).ToArray()
                : Array.Empty<MeshFilter>();

            var metrics = new WorldRemasterStaticMetrics
            {
                RendererCount = renderers.Length,
                ColliderCount = colliders.Length,
                LodGroupCount = lodGroups.Length,
                TriangleCount = CountTriangles(meshFilters),
                UniqueMeshBytesEstimate = meshFilters.Select(filter => filter.sharedMesh)
                    .Distinct()
                    .Sum(mesh => (long)mesh.vertexCount * 48L + (long)mesh.triangles.Length * sizeof(int)),
                PilotRendererCount = pilot != null ? pilot.GetComponentsInChildren<Renderer>(true).Length : 0,
                PilotColliderCount = pilot != null ? pilot.GetComponentsInChildren<Collider>(true).Length : 0,
                PilotLodGroupCount = pilot != null ? pilot.GetComponentsInChildren<LODGroup>(true).Length : 0,
                PilotTriangleCount = CountTriangles(pilotMeshFilters),
                NextZoneRendererCount = nextZone != null ? nextZone.GetComponentsInChildren<Renderer>(true).Length : 0,
                NextZoneColliderCount = nextZone != null ? nextZone.GetComponentsInChildren<Collider>(true).Length : 0,
                NextZoneLodGroupCount = nextZone != null ? nextZone.GetComponentsInChildren<LODGroup>(true).Length : 0,
                NextZoneTriangleCount = CountTriangles(nextMeshFilters),
                DefaultLayerColliderCount = colliders.Count(collider => collider.gameObject.layer == 0),
                NullPhysicsMaterialColliderCount = colliders.Count(collider => collider.sharedMaterial == null)
            };

            if (pilot != null && nextZone != null)
            {
                Mesh pilotTerrain = AssetDatabase.LoadAssetAtPath<Mesh>(WorldRemasterPaths.TerrainMesh);
                BoxCollider footpath = nextZone.GetComponentsInChildren<BoxCollider>(true)
                    .FirstOrDefault(collider => collider.name == "FootpathToPier");
                if (pilotTerrain != null && footpath != null)
                {
                    float pilotEdgeA = (WorldRemasterPaths.HomeGarageAnchor +
                                        WorldRemasterPaths.HomeGarageRotation *
                                        new Vector3(0f, 0f, pilotTerrain.bounds.min.z)).z;
                    float pilotEdgeB = (WorldRemasterPaths.HomeGarageAnchor +
                                        WorldRemasterPaths.HomeGarageRotation *
                                        new Vector3(0f, 0f, pilotTerrain.bounds.max.z)).z;
                    metrics.HomePierSeamOverlapMeters =
                        Mathf.Max(pilotEdgeA, pilotEdgeB) - GetIntendedWorldZBounds(nextZone.transform, footpath).min;
                }

                BoxCollider[] deckColliders = nextZone.GetComponentsInChildren<BoxCollider>(true)
                    .Where(collider => collider.name.StartsWith("DeckPlank_", StringComparison.Ordinal))
                    .OrderBy(collider => collider.transform.position.z)
                    .ToArray();
                if (deckColliders.Length > 1)
                {
                    float maximumGap = 0f;
                    for (int index = 1; index < deckColliders.Length; index++)
                    {
                        BoxCollider previous = deckColliders[index - 1];
                        BoxCollider current = deckColliders[index];
                        float previousHalfLength = previous.size.z * Mathf.Abs(previous.transform.localScale.z) * 0.5f;
                        float currentHalfLength = current.size.z * Mathf.Abs(current.transform.localScale.z) * 0.5f;
                        float previousMaximum = previous.transform.localPosition.z + previous.center.z + previousHalfLength;
                        float currentMinimum = current.transform.localPosition.z + current.center.z - currentHalfLength;
                        maximumGap = Mathf.Max(maximumGap, currentMinimum - previousMaximum);
                    }

                    metrics.PierMaximumColliderGapMeters = maximumGap;
                }
            }

            return metrics;
        }

        private static int CountTriangles(IEnumerable<MeshFilter> filters) => filters.Sum(filter =>
            Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                .Sum(subMesh => (int)filter.sharedMesh.GetIndexCount(subMesh) / 3));

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

            if (mapped != WorldRemasterRegistryBuilder.TotalMappedRecordCount)
            {
                result.AddError($"Expected {WorldRemasterRegistryBuilder.TotalMappedRecordCount} bounded 05A bindings, found {mapped}.");
            }

            int sourceZoneCount = entities.Select(entity => entity.CellId).Distinct(StringComparer.Ordinal).Count();
            if (registry.Zones.Count != sourceZoneCount)
            {
                result.AddError($"Zone registry count {registry.Zones.Count} does not match discovered set {sourceZoneCount}.");
            }

            WorldProductionZoneRecord pilot = registry.Zones.FirstOrDefault(zone => zone.ZoneId == WorldRemasterPaths.PilotZoneId);
            if (pilot == null || pilot.ReferenceRecordCount != 671 ||
                pilot.MappedRecordCount != WorldRemasterRegistryBuilder.PilotMappedRecordCount)
            {
                result.AddError("Pilot zone registry counts are inconsistent.");
            }

            WorldProductionZoneRecord nextZone = registry.Zones.FirstOrDefault(zone => zone.ZoneId == WorldRemasterPaths.NextZoneId);
            if (nextZone == null || nextZone.ReferenceRecordCount != 15 ||
                nextZone.MappedRecordCount != WorldRemasterRegistryBuilder.NextZoneMappedRecordCount ||
                nextZone.Status != WorldReplacementStatus.ProductionCandidate)
            {
                result.AddError("Batch 01 zone registry counts/status are inconsistent.");
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

        private static void ValidateNextZonePrefab(WorldRemasterValidationResult result)
        {
            GameObject zone = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NextZonePrefab);
            if (zone == null)
            {
                return;
            }

            WorldRemasterPilotMarker marker = zone.GetComponent<WorldRemasterPilotMarker>();
            if (marker == null || marker.ZoneId != WorldRemasterPaths.NextZoneId || !marker.DonorBinaryIndependent ||
                marker.MappedReferenceRecordCount != WorldRemasterRegistryBuilder.NextZoneMappedRecordCount ||
                marker.TotalReferenceRecordCount != 15)
            {
                result.AddError("Batch 01 HomeShorelinePier marker is missing or inconsistent.");
            }

            LODGroup[] lodGroups = zone.GetComponentsInChildren<LODGroup>(true);
            if (lodGroups.Length != 3 || lodGroups.Any(group => group.GetLODs().Length < 2))
            {
                result.AddError("Batch 01 requires three hedge LOD groups with at least two levels each.");
            }

            Transform water = zone.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "BoundedLakeSurface");
            if (water == null || water.GetComponent<Collider>() != null)
            {
                result.AddError("Bounded lake surface must exist without a blocking collider.");
            }

            Mesh pilotTerrain = AssetDatabase.LoadAssetAtPath<Mesh>(WorldRemasterPaths.TerrainMesh);
            BoxCollider footpath = zone.GetComponentsInChildren<BoxCollider>(true)
                .FirstOrDefault(collider => collider.name == "FootpathToPier");
            if (pilotTerrain == null || footpath == null)
            {
                result.AddError("Home-to-pier seam validation requires the pilot terrain mesh and FootpathToPier collider.");
            }
            else
            {
                float pilotEdgeA = (WorldRemasterPaths.HomeGarageAnchor +
                                    WorldRemasterPaths.HomeGarageRotation *
                                    new Vector3(0f, 0f, pilotTerrain.bounds.min.z)).z;
                float pilotEdgeB = (WorldRemasterPaths.HomeGarageAnchor +
                                    WorldRemasterPaths.HomeGarageRotation *
                                    new Vector3(0f, 0f, pilotTerrain.bounds.max.z)).z;
                float pilotNorthEdge = Mathf.Max(pilotEdgeA, pilotEdgeB);
                float footpathSouthEdge = GetIntendedWorldZBounds(zone.transform, footpath).min;
                float seamOverlap = pilotNorthEdge - footpathSouthEdge;
                if (seamOverlap < 1f)
                {
                    result.AddError(
                        $"Home-to-pier walkable seam overlap {seamOverlap:0.###}m is below the required 1m.");
                }
            }

            BoxCollider[] deckColliders = zone.GetComponentsInChildren<BoxCollider>(true)
                .Where(collider => collider.name.StartsWith("DeckPlank_", StringComparison.Ordinal))
                .OrderBy(collider => collider.transform.position.z)
                .ToArray();
            if (deckColliders.Length != 14)
            {
                result.AddError($"Pier deck requires 14 explicit walkable plank colliders; found {deckColliders.Length}.");
            }
            else
            {
                float maximumGap = 0f;
                for (int index = 1; index < deckColliders.Length; index++)
                {
                    BoxCollider previous = deckColliders[index - 1];
                    BoxCollider current = deckColliders[index];
                    float previousHalfLength = previous.size.z * Mathf.Abs(previous.transform.localScale.z) * 0.5f;
                    float currentHalfLength = current.size.z * Mathf.Abs(current.transform.localScale.z) * 0.5f;
                    float previousMaximum = previous.transform.localPosition.z + previous.center.z + previousHalfLength;
                    float currentMinimum = current.transform.localPosition.z + current.center.z - currentHalfLength;
                    maximumGap = Mathf.Max(maximumGap, currentMinimum - previousMaximum);
                }

                if (maximumGap > 0.1f)
                {
                    result.AddError($"Pier walkable collision gap {maximumGap:0.###}m exceeds 0.1m.");
                }
            }

            if (zone.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.sharedMaterial == null))
            {
                result.AddError("Batch 01 contains a renderer without an assigned production material.");
            }
        }

        private static (float min, float max) GetIntendedWorldZBounds(Transform zoneRoot, BoxCollider collider)
        {
            Vector3 halfSize = collider.size * 0.5f;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 colliderLocalCorner = collider.center + Vector3.Scale(
                            halfSize,
                            new Vector3(x, y, z));
                        Vector3 prefabPoint = collider.transform.TransformPoint(colliderLocalCorner);
                        Vector3 zoneLocalPoint = zoneRoot.InverseTransformPoint(prefabPoint);
                        float intendedWorldZ = (WorldRemasterPaths.HomePierAnchor +
                                                WorldRemasterPaths.HomePierRotation * zoneLocalPoint).z;
                        minimum = Mathf.Min(minimum, intendedWorldZ);
                        maximum = Mathf.Max(maximum, intendedWorldZ);
                    }
                }
            }

            return (minimum, maximum);
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

        private static void ValidateNextZoneCell(WorldRemasterValidationResult result)
        {
            Scene scene = EditorSceneManager.OpenScene(WorldRemasterPaths.NextZoneCellScene, OpenSceneMode.Single);
            WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .FirstOrDefault(candidate => candidate.ZoneId == WorldRemasterPaths.NextZoneId);
            if (marker == null)
            {
                result.AddError("Batch 01 production cell does not contain its zone marker.");
                return;
            }

            Transform cellRoot = marker.transform;
            if (Vector3.Distance(cellRoot.position, WorldRemasterPaths.HomePierAnchor) > 0.05f ||
                Quaternion.Angle(cellRoot.rotation, WorldRemasterPaths.HomePierRotation) > 0.1f)
            {
                result.AddError("Batch 01 cell root drifted from the frozen home-pier anchor.");
            }

            WorldCellIndex assigned = WorldCellMembershipUtility.FromPosition(cellRoot.position, 512f);
            if (assigned.Id != WorldRemasterPaths.NextZoneId)
            {
                result.AddError($"Batch 01 anchor is assigned to {assigned.Id}, expected {WorldRemasterPaths.NextZoneId}.");
            }

            string[] hedgeIds =
            {
                "b8de7336e204fae3ba333227b3e94d19",
                "449b18de0c10f87887e3f3304a90366e",
                "847f56ce8c1be238f4bcae514bb55fdf"
            };
            for (int index = 0; index < hedgeIds.Length; index++)
            {
                Transform hedge = cellRoot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => candidate.name == "Hedge_" + hedgeIds[index]);
                if (hedge == null)
                {
                    result.AddError("Batch 01 hedge instance is missing: " + hedgeIds[index]);
                    continue;
                }

                float deviation = Vector3.Distance(hedge.position, WorldRemasterPaths.HomeHedgeAnchors[index]);
                if (deviation > 0.05f)
                {
                    result.AddError($"Batch 01 hedge {hedgeIds[index]} anchor deviation {deviation:0.###}m exceeds 0.05m.");
                }
            }

            Transform pier = cellRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "HomePier");
            if (pier == null || Vector3.Distance(pier.position, WorldRemasterPaths.HomePierAnchor) > 0.05f)
            {
                result.AddError("Batch 01 production pier is missing or its anchor exceeds 0.05m tolerance.");
            }
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

            foreach (string path in new[]
                     {
                         WorldRemasterPaths.PilotPlaytestScene,
                         WorldRemasterPaths.PilotCellScene,
                         WorldRemasterPaths.NextZonePlaytestScene,
                         WorldRemasterPaths.NextZoneCellScene
                     })
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

            if (scenes.Any(scene => scene.enabled && scene.path == WorldRemasterPaths.NextZoneComparisonScene))
            {
                result.AddError("Batch 01 comparison scene must remain excluded from builds.");
            }
        }

        private static void ValidateBatchCaptures(WorldRemasterValidationResult result)
        {
            string root = Path.GetFullPath(WorldRemasterPaths.NextZoneVisualCaptureRoot);
            string[] required =
            {
                "Pier_ReferenceOnly.png",
                "Pier_ProductionOnly.png",
                "Pier_OverlayComparison.png",
                "Hedge_ReferenceOnly.png",
                "Hedge_ProductionOnly.png",
                "Hedge_OverlayComparison.png",
                "Seam_ReferenceOnly.png",
                "Seam_ProductionOnly.png",
                "Seam_OverlayComparison.png",
                "capture_manifest.csv"
            };
            foreach (string file in required)
            {
                string path = Path.Combine(root, file);
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    result.AddError("Batch 01 comparison capture is missing or empty: " + path);
                }
            }
        }

        private static void WriteReports(WorldRemasterValidationResult result)
        {
            Directory.CreateDirectory(WorldRemasterPaths.DocumentationRoot);
            WorldRemasterStaticMetrics metrics = MeasureStaticContent();

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
            performance.AppendLine("Scope: static Editor audit of the bounded 05A pilot plus Batch 01 `WR_HomeShorelinePier.prefab`; not a GPU or standalone-player capture.");
            performance.AppendLine();
            performance.AppendLine("| Metric | Value |");
            performance.AppendLine("|---|---:|");
            performance.AppendLine($"| Renderers | {metrics.RendererCount} |");
            performance.AppendLine($"| Colliders | {metrics.ColliderCount} |");
            performance.AppendLine($"| LOD groups | {metrics.LodGroupCount} |");
            performance.AppendLine($"| Mesh triangles (instance-counted) | {metrics.TriangleCount} |");
            performance.AppendLine($"| Unique mesh memory estimate | {metrics.UniqueMeshBytesEstimate / (1024f * 1024f):0.00} MiB |");
            performance.AppendLine();
            performance.AppendLine("Batch 01 contribution:");
            performance.AppendLine();
            performance.AppendLine("| Batch 01 metric | Value |");
            performance.AppendLine("|---|---:|");
            performance.AppendLine($"| Renderers | {metrics.NextZoneRendererCount} |");
            performance.AppendLine($"| Colliders | {metrics.NextZoneColliderCount} |");
            performance.AppendLine($"| LOD groups | {metrics.NextZoneLodGroupCount} |");
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
