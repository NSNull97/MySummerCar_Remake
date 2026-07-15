using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public static class WorldRemasterRegistryBuilder
    {
        private const string BaselineValidationTimestamp = "2026-07-14T00:00:00+05:00";
        private const string Batch01ValidationTimestamp = "2026-07-15T00:00:00+05:00";
        private const string PilotManualValidation =
            "DoorGatePass;LightingReadabilityLow;M4TraversalPass;PerformancePending";
        private const string Batch01ManualValidation =
            "TraversalPass;WaterPass;HedgePass;VisualReferencePending;PerformancePending";
        private const string ProductionMaterials =
            "Assets/Game/World/Production/Materials/WR_* (project-authored; M3 procedural texture basis)";

        private static readonly IReadOnlyDictionary<string, Mapping> PilotMappings =
            new Dictionary<string, Mapping>(StringComparer.Ordinal)
            {
                ["250d1cb74558e7c7e27e5e860981c938"] = new Mapping(WorldRemasterPaths.GaragePrefab, "GarageDoorLeft", WorldReplacementStatus.ProductionCandidate),
                ["e8660bda40e1d2946e004456e245c803"] = new Mapping(WorldRemasterPaths.GaragePrefab, "GarageDoorRight", WorldReplacementStatus.ProductionCandidate),
                ["419f49d30da6bff0fcfa679c84d652ce"] = new Mapping(WorldRemasterPaths.HousePrefab, "HouseGarageShell", WorldReplacementStatus.ProductionCandidate),
                ["b5e7b987d5aac9da197a6ce662beb64c"] = new Mapping(WorldRemasterPaths.HousePrefab, "HouseGarageRoof", WorldReplacementStatus.FirstPass),
                ["674681b73ae9d9ea1030a148f70648e4"] = new Mapping(WorldRemasterPaths.HousePrefab, "GarageRoofInner", WorldReplacementStatus.FirstPass),
                ["f7f5381e1e99a73ec53e01cfc34110aa"] = new Mapping(WorldRemasterPaths.HousePrefab, "HouseWindowFamily", WorldReplacementStatus.FirstPass),
                ["9c5d1f176cabbc3945c75ebc533e5b36"] = new Mapping(WorldRemasterPaths.HousePrefab, "HouseFrontDoor", WorldReplacementStatus.ProductionCandidate),
                ["6565829bc23d922257712654b4607ecf"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageWorkbench", WorldReplacementStatus.FirstPass),
                ["20b94503abb28f5b1d40f4817cbb5245"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesA", WorldReplacementStatus.FirstPass),
                ["ea6d046fbf56abd80120a480ede030ce"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesB", WorldReplacementStatus.FirstPass),
                ["1f9b558b6b736aff90218d6f698262fe"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesC", WorldReplacementStatus.FirstPass),
                ["5b3e05ba0c1362f86ceb10538db83035"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesD", WorldReplacementStatus.FirstPass),
                ["35617a145297c4d6cb70a8ab9478bcda"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesE", WorldReplacementStatus.FirstPass),
                ["f886e748a2d6fefc994d59272ed46fa2"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesF", WorldReplacementStatus.FirstPass),
                ["3d723f0edfad5b10af107ef24845322d"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesG", WorldReplacementStatus.FirstPass),
                ["89912bcee006371d2641f9faa328e371"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "GarageShelvesH", WorldReplacementStatus.FirstPass),
                ["eebd5ee197a22fbc9bc38d8af5b32e1a"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "RepresentativeLivingRoom", WorldReplacementStatus.FirstPass),
                ["16d106ad9e02f79366ef6f57515f256d"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "RepresentativeLivingRoomFloor", WorldReplacementStatus.FirstPass),
                ["c5f236e95738088bfd0c4f3925b1e6a4"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "RepresentativeSauna", WorldReplacementStatus.FirstPass),
                ["e78dd29f9ccbde760e86f6fca6a901d1"] = new Mapping(WorldRemasterPaths.InteriorPrefab, "RepresentativeKitchenStove", WorldReplacementStatus.FirstPass),
                ["52fa266116cb5c138c2e90b8dd4b6bad"] = new Mapping(WorldRemasterPaths.PropsPrefab, "YardFlag", WorldReplacementStatus.FirstPass),
                ["366dd19254f921ef78e095702f5ed505"] = new Mapping(WorldRemasterPaths.PropsPrefab, "GarageBatteryCharger", WorldReplacementStatus.FirstPass),
                ["b2f988842fbd9f52370994c7d6548f52"] = new Mapping(WorldRemasterPaths.PropsPrefab, "ChargerWirePlus", WorldReplacementStatus.FirstPass),
                ["fc7439d36774290084d16e553de0f117"] = new Mapping(WorldRemasterPaths.PropsPrefab, "ChargerWireMinus", WorldReplacementStatus.FirstPass)
            };

        private static readonly IReadOnlyDictionary<string, Mapping> NextZoneMappings =
            new Dictionary<string, Mapping>(StringComparer.Ordinal)
            {
                ["345dc7662dae9f1f01d77b15f74e5f8f"] = new Mapping(WorldRemasterPaths.PierPrefab, "PierDeckAndSupports", WorldReplacementStatus.ProductionCandidate),
                ["56a7aa7c66146248d6c820c31a6b99fd"] = new Mapping(WorldRemasterPaths.PierPrefab, "PierPontoons", WorldReplacementStatus.ProductionCandidate),
                ["de5d5682cbef7d27a48a473d5e85877d"] = new Mapping(WorldRemasterPaths.PierPrefab, "WalkableCollisionNorth", WorldReplacementStatus.ProductionCandidate),
                ["e0fa39e1ceeed93727dd86e749c6d115"] = new Mapping(WorldRemasterPaths.PierPrefab, "WalkableCollisionSouth", WorldReplacementStatus.ProductionCandidate),
                ["b412961b75cb019e74a83b24faac32a4"] = new Mapping(WorldRemasterPaths.ShorelinePrefab, "BoundedLakeSurface", WorldReplacementStatus.ProductionCandidate),
                ["f700b12cf5c75a3906dd079acea3f274"] = new Mapping(WorldRemasterPaths.ShorelinePrefab, "BoundedLakeBottom", WorldReplacementStatus.ProductionCandidate),
                ["b8de7336e204fae3ba333227b3e94d19"] = new Mapping(WorldRemasterPaths.HedgePrefab, "HedgeSegmentA", WorldReplacementStatus.ProductionCandidate),
                ["449b18de0c10f87887e3f3304a90366e"] = new Mapping(WorldRemasterPaths.HedgePrefab, "HedgeSegmentB", WorldReplacementStatus.ProductionCandidate),
                ["847f56ce8c1be238f4bcae514bb55fdf"] = new Mapping(WorldRemasterPaths.HedgePrefab, "HedgeSegmentC", WorldReplacementStatus.ProductionCandidate)
            };

        public static int PilotMappedRecordCount => PilotMappings.Count;
        public static int NextZoneMappedRecordCount => NextZoneMappings.Count;
        public static int TotalMappedRecordCount => PilotMappings.Count + NextZoneMappings.Count;

        public static WorldProductionAssetRegistry BuildRegistryAndMachineReadableFiles()
        {
            IReadOnlyList<WorldEntityPlacement> entities = LoadEntities();
            Dictionary<string, string> artTaskByKey = BuildArtTaskIds(entities);
            WorldProductionAssetRecord[] records = entities
                .Select(entity => BuildRecord(entity, artTaskByKey))
                .ToArray();
            WorldProductionZoneRecord[] zones = BuildZones(entities, records);
            WorldArtTaskRecord[] tasks = BuildArtTasks(entities, artTaskByKey);

            EnsureAssetFolder(WorldRemasterPaths.RegistryAsset);
            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<WorldProductionAssetRegistry>();
                registry.name = "WR_WorldProductionAssetRegistry";
                AssetDatabase.CreateAsset(registry, WorldRemasterPaths.RegistryAsset);
            }

            registry.Configure(
                WorldRemasterPaths.BuilderVersion,
                WorldRemasterPaths.SourceDatabaseVersion,
                WorldRemasterPaths.PilotZoneId,
                records,
                zones,
                tasks);
            EditorUtility.SetDirty(registry);

            Directory.CreateDirectory(WorldRemasterPaths.DocumentationRoot);
            AtomicWrite(WorldRemasterPaths.ReplacementLedger, BuildReplacementLedger(records));
            AtomicWrite(WorldRemasterPaths.ArtBacklog, BuildArtBacklog(entities, tasks));
            AtomicWrite(WorldRemasterPaths.ZoneStatus, BuildZoneStatus(zones));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return registry;
        }

        public static IReadOnlyList<WorldEntityPlacement> LoadEntities()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldRemasterPaths.EntityTable);
            if (source == null)
            {
                throw new InvalidOperationException("World entity table is missing: " + WorldRemasterPaths.EntityTable);
            }

            return WorldEntityTable.Parse(source.text);
        }

        private static WorldProductionAssetRecord BuildRecord(
            WorldEntityPlacement entity,
            IReadOnlyDictionary<string, string> artTaskByKey)
        {
            bool mapped = TryGetMapping(entity.StableId, out Mapping mapping);
            bool nextZone = mapped && NextZoneMappings.ContainsKey(entity.StableId);
            string taskKey = BuildTaskKey(entity.CellId, entity.Category);
            string manualTask = mapped ? string.Empty : artTaskByKey[taskKey];
            string validation = mapped ? "AutomatedStructurePass;ManualVisualPending" : "NotRun";
            string notes = mapped
                ? nextZone
                    ? $"05A Batch 01 HomeShorelinePier binding: {mapping.ProductionElement}. Project-authored geometry; donor binary is not copied and direct visual parity remains manual-reviewable."
                    : $"05A pilot composite binding: {mapping.ProductionElement}. Donor geometry is not copied; detailed fit remains manual-reviewable."
                : "Reference record registered; no production replacement assigned in the bounded first 05A pass.";
            var record = new WorldProductionAssetRecord();
            record.Configure(
                entity.StableId,
                entity.HierarchyPath,
                entity.Category,
                mapped ? mapping.Prefab : string.Empty,
                mapped ? ProductionMaterials : string.Empty,
                mapped ? nextZone ? BatchCollisionDescription(mapping.Prefab) : mapping.Prefab + "::authored colliders" : string.Empty,
                mapped ? nextZone ? BatchLodDescription(mapping.Prefab) : mapping.Prefab + "::LOD policy" : string.Empty,
                mapped ? "Preserve donor world anchor; project-authored local pivot" : "Unassigned",
                "1 Unity unit = 1 metre; no negative production scale",
                entity.Bounds,
                default,
                mapped ? 0.35f : 0f,
                Vector3.zero,
                mapped ? mapping.Status : WorldReplacementStatus.Unassigned,
                mapped ? "GeneratedProductionCandidate" : "Backlog",
                validation,
                entity.CellId,
                mapped ? nextZone ? "Batch01Near" : "PilotNear" : "Unclassified",
                mapped ? "GlobalWetnessParameterReady" : "Unknown",
                mapped ? "Neutral/Evening/OvercastCompatible" : "Unknown",
                notes,
                mapped ? "TrackedProjectAsset" : "NoProductionAsset",
                manualTask,
                nextZone ? Batch01ValidationTimestamp : BaselineValidationTimestamp,
                nextZone ? WorldRemasterPaths.BuilderVersion : mapped ? "05A.1" : "0");
            return record;
        }

        private static WorldProductionZoneRecord[] BuildZones(
            IReadOnlyList<WorldEntityPlacement> entities,
            IReadOnlyList<WorldProductionAssetRecord> records)
        {
            Dictionary<string, int> mappedByZone = records
                .Where(record => record.HasProductionReplacement)
                .GroupBy(record => record.ProductionZone, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            return entities
                .GroupBy(entity => entity.CellId, StringComparer.Ordinal)
                .OrderBy(group => ZoneSortKey(group.Key), StringComparer.Ordinal)
                .Select(group =>
                {
                    int mapped = mappedByZone.TryGetValue(group.Key, out int count) ? count : 0;
                    bool pilot = string.Equals(group.Key, WorldRemasterPaths.PilotZoneId, StringComparison.Ordinal);
                    bool nextZone = string.Equals(group.Key, WorldRemasterPaths.NextZoneId, StringComparison.Ordinal);
                    bool excluded = string.Equals(group.Key, "excluded", StringComparison.Ordinal);
                    var zone = new WorldProductionZoneRecord();
                    zone.Configure(
                        group.Key,
                        group.Count(),
                        mapped,
                        pilot || nextZone
                            ? WorldReplacementStatus.ProductionCandidate
                            : excluded ? WorldReplacementStatus.ReferenceOnly : WorldReplacementStatus.Unassigned,
                        pilot || nextZone ? "Generated;ValidationRequired" : excluded ? "ReferenceClassificationPass" : "RegistryPass",
                        pilot ? PilotManualValidation : nextZone ? Batch01ManualValidation : "NotStarted",
                        pilot ? WorldRemasterPaths.PilotCellScene : nextZone ? WorldRemasterPaths.NextZoneCellScene : string.Empty,
                        pilot
                            ? "Bounded home/garage pilot; raw-record coverage is intentionally not full-zone completion."
                            : nextZone
                                ? "Batch 01 bounded HomeShorelinePier sub-zone; traversal/water/hedge manual review passed; visual reference/performance and six unrelated gameplay records remain pending."
                            : excluded ? "Not a streaming cell; retained for registry completeness." : "Awaiting a later zone batch.");
                    return zone;
                })
                .ToArray();
        }

        private static WorldArtTaskRecord[] BuildArtTasks(
            IReadOnlyList<WorldEntityPlacement> entities,
            IReadOnlyDictionary<string, string> taskIds)
        {
            HashSet<string> mappedIds = BuildMappedIdSet();
            return entities
                .Where(entity => !mappedIds.Contains(entity.StableId))
                .GroupBy(entity => new { entity.CellId, entity.Category })
                .OrderBy(group => ZoneSortKey(group.Key.CellId), StringComparer.Ordinal)
                .ThenBy(group => group.Key.Category, StringComparer.Ordinal)
                .Select(group =>
                {
                    var task = new WorldArtTaskRecord();
                    task.Configure(
                        taskIds[BuildTaskKey(group.Key.CellId, group.Key.Category)],
                        group.Key.CellId,
                        group.Key.Category,
                        PriorityFor(group.Key.Category),
                        "Open",
                        RequiredToolFor(group.Key.Category),
                        $"Author, validate and bind {group.Count().ToString(CultureInfo.InvariantCulture)} reference records without donor runtime dependencies.");
                    return task;
                })
                .ToArray();
        }

        private static Dictionary<string, string> BuildArtTaskIds(IReadOnlyList<WorldEntityPlacement> entities)
        {
            return entities
                .GroupBy(entity => new { entity.CellId, entity.Category })
                .ToDictionary(
                    group => BuildTaskKey(group.Key.CellId, group.Key.Category),
                    group => Hash128.Compute("wr05a.art." + BuildTaskKey(group.Key.CellId, group.Key.Category)).ToString(),
                    StringComparer.Ordinal);
        }

        private static string BuildReplacementLedger(IReadOnlyList<WorldProductionAssetRecord> records)
        {
            var output = new StringBuilder(records.Count * 320);
            output.AppendLine("StableWorldId,DonorSourceReference,SemanticCategory,ProductionPrefab,ProductionMaterials,CollisionReference,LodGroup,PivotMode,ScaleMode,ExpectedBoundsCenter,ExpectedBoundsSize,MeasuredBoundsCenter,MeasuredBoundsSize,AllowedDeviationMeters,TransformOffset,ReplacementStatus,AuthoringStatus,ValidationStatus,ProductionZone,PerformanceTier,WetnessCompatibility,WeatherCompatibility,Notes,SourceControlStatus,ManualArtDependency,LastValidationTimestamp,AssetVersion");
            foreach (WorldProductionAssetRecord record in records)
            {
                AppendCsvRow(output,
                    record.StableWorldId,
                    record.DonorSourceReference,
                    record.SemanticCategory,
                    record.ProductionPrefab,
                    record.ProductionMaterials,
                    record.CollisionReference,
                    record.LodGroup,
                    record.PivotMode,
                    record.ScaleMode,
                    Vector(record.ExpectedBounds.center),
                    Vector(record.ExpectedBounds.size),
                    Vector(record.MeasuredBounds.center),
                    Vector(record.MeasuredBounds.size),
                    record.AllowedDeviationMeters.ToString("0.###", CultureInfo.InvariantCulture),
                    Vector(record.TransformOffset),
                    record.ReplacementStatus.ToString(),
                    record.AuthoringStatus,
                    record.ValidationStatus,
                    record.ProductionZone,
                    record.PerformanceTier,
                    record.WetnessCompatibility,
                    record.WeatherCompatibility,
                    record.Notes,
                    record.SourceControlStatus,
                    record.ManualArtDependency,
                    record.LastValidationTimestamp,
                    record.AssetVersion);
            }

            return output.ToString();
        }

        private static string BuildArtBacklog(
            IReadOnlyList<WorldEntityPlacement> entities,
            IReadOnlyList<WorldArtTaskRecord> tasks)
        {
            Dictionary<string, int> counts = entities
                .Where(entity => !TryGetMapping(entity.StableId, out _))
                .GroupBy(entity => BuildTaskKey(entity.CellId, entity.Category), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var output = new StringBuilder(tasks.Count * 220);
            output.AppendLine("TaskId,ZoneId,Category,ReferenceRecordCount,Priority,Status,RequiredTool,Acceptance,Dependencies,Notes");
            foreach (WorldArtTaskRecord task in tasks)
            {
                string key = BuildTaskKey(task.ZoneId, task.Category);
                AppendCsvRow(output,
                    task.TaskId,
                    task.ZoneId,
                    task.Category,
                    counts[key].ToString(CultureInfo.InvariantCulture),
                    task.Priority,
                    task.Status,
                    task.RequiredTool,
                    task.Acceptance,
                    "World replacement registry; donor reference metadata",
                    "Grouped manual-art task; every unassigned ledger row links to this task ID.");
            }

            return output.ToString();
        }

        private static string BuildZoneStatus(IReadOnlyList<WorldProductionZoneRecord> zones)
        {
            var output = new StringBuilder(zones.Count * 180);
            output.AppendLine("ZoneId,ReferenceRecordCount,MappedRecordCount,CoveragePercent,ReplacementStatus,AutomatedValidation,ManualValidation,ProductionCellPath,Notes");
            foreach (WorldProductionZoneRecord zone in zones)
            {
                AppendCsvRow(output,
                    zone.ZoneId,
                    zone.ReferenceRecordCount.ToString(CultureInfo.InvariantCulture),
                    zone.MappedRecordCount.ToString(CultureInfo.InvariantCulture),
                    zone.CoveragePercent.ToString("0.000", CultureInfo.InvariantCulture),
                    zone.Status.ToString(),
                    zone.AutomatedValidation,
                    zone.ManualValidation,
                    zone.ProductionCellPath,
                    zone.Notes);
            }

            return output.ToString();
        }

        private static void AppendCsvRow(StringBuilder output, params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                output.Append('"');
                output.Append((values[index] ?? string.Empty).Replace("\"", "\"\""));
                output.Append('"');
            }

            output.AppendLine();
        }

        private static string Vector(Vector3 value) => string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.######}|{1:0.######}|{2:0.######}",
            value.x,
            value.y,
            value.z);

        private static string BuildTaskKey(string zone, string category) =>
            (zone ?? string.Empty) + "|" + (category ?? string.Empty);

        private static string ZoneSortKey(string zone) =>
            string.Equals(zone, WorldRemasterPaths.PilotZoneId, StringComparison.Ordinal)
                ? "0_" + zone
                : string.Equals(zone, WorldRemasterPaths.NextZoneId, StringComparison.Ordinal)
                    ? "1_" + zone
                    : "2_" + zone;

        private static bool TryGetMapping(string stableId, out Mapping mapping)
        {
            if (PilotMappings.TryGetValue(stableId, out mapping))
            {
                return true;
            }

            return NextZoneMappings.TryGetValue(stableId, out mapping);
        }

        private static HashSet<string> BuildMappedIdSet()
        {
            var ids = new HashSet<string>(PilotMappings.Keys, StringComparer.Ordinal);
            ids.UnionWith(NextZoneMappings.Keys);
            return ids;
        }

        private static string BatchCollisionDescription(string prefab) => prefab switch
        {
            WorldRemasterPaths.HedgePrefab => prefab + "::simplified BoxCollider",
            WorldRemasterPaths.PierPrefab => prefab + "::walkable primitive colliders",
            WorldRemasterPaths.ShorelinePrefab => prefab + "::non-blocking water; primitive shore/path colliders",
            _ => prefab + "::authored collision policy"
        };

        private static string BatchLodDescription(string prefab) => prefab switch
        {
            WorldRemasterPaths.HedgePrefab => prefab + "::two-stage LODGroup",
            WorldRemasterPaths.PierPrefab => "Close-range asset; LOD deferred pending profiling",
            WorldRemasterPaths.ShorelinePrefab => "Bounded cell surface; HLOD deferred pending profiling",
            _ => "Category-specific LOD pending profiling"
        };

        private static string PriorityFor(string category)
        {
            return category switch
            {
                "Terrain" or "Road" or "Bridge" or "BuildingExterior" or "Roof" => "P0",
                "BuildingInterior" or "Door" or "Gate" or "Water" or "UtilityPole" => "P1",
                "Window" or "Fence" or "StaticProp" or "VegetationTree" => "P2",
                _ => "P3"
            };
        }

        private static string RequiredToolFor(string category)
        {
            return category switch
            {
                "Terrain" or "Road" => "Unity terrain/spline authoring plus DCC review",
                "VegetationTree" or "VegetationBush" or "VegetationGrass" => "Vegetation DCC/SpeedTree-equivalent manual authoring",
                "BuildingExterior" or "BuildingInterior" or "Roof" or "Door" or "Window" or "Bridge" => "Blender or equivalent DCC (not found in PATH during 05A pre-flight)",
                _ => "Blender/Substance-equivalent manual art pass"
            };
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string current = "Assets";
            string[] segments = directory.Split('/');
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static void AtomicWrite(string path, string content)
        {
            string absolutePath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? throw new InvalidOperationException());
            string temporary = absolutePath + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(absolutePath))
            {
                File.Replace(temporary, absolutePath, null);
            }
            else
            {
                File.Move(temporary, absolutePath);
            }
        }

        private readonly struct Mapping
        {
            internal Mapping(string prefab, string productionElement, WorldReplacementStatus status)
            {
                Prefab = prefab;
                ProductionElement = productionElement;
                Status = status;
            }

            internal string Prefab { get; }
            internal string ProductionElement { get; }
            internal WorldReplacementStatus Status { get; }
        }
    }
}
