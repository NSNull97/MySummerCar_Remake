using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Remaster
{
    public enum WorldReplacementStatus
    {
        Unassigned,
        ReferenceOnly,
        Blockout,
        FirstPass,
        ProductionCandidate,
        NeedsReview,
        Approved,
        Verified,
        Blocked,
        Rejected,
        Deprecated
    }

    public enum WorldComparisonMode
    {
        ProductionOnly,
        ReferenceOnly,
        OverlayComparison
    }

    [Serializable]
    public sealed class WorldReplacementBinding
    {
        [SerializeField] private string stableWorldId = string.Empty;
        [SerializeField] private string productionPrefab = string.Empty;
        [SerializeField] private string productionZone = string.Empty;

        public string StableWorldId => stableWorldId;
        public string ProductionPrefab => productionPrefab;
        public string ProductionZone => productionZone;

        public void Configure(string worldId, string prefab, string zone)
        {
            stableWorldId = worldId ?? string.Empty;
            productionPrefab = prefab ?? string.Empty;
            productionZone = zone ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class WorldProductionAssetRecord
    {
        [SerializeField] private string stableWorldId = string.Empty;
        [SerializeField] private string donorSourceReference = string.Empty;
        [SerializeField] private string semanticCategory = string.Empty;
        [SerializeField] private string productionPrefab = string.Empty;
        [SerializeField] private string productionMaterials = string.Empty;
        [SerializeField] private string collisionReference = string.Empty;
        [SerializeField] private string lodGroup = string.Empty;
        [SerializeField] private string pivotMode = string.Empty;
        [SerializeField] private string scaleMode = string.Empty;
        [SerializeField] private Bounds expectedBounds;
        [SerializeField] private Bounds measuredBounds;
        [SerializeField, Min(0f)] private float allowedDeviationMeters;
        [SerializeField] private Vector3 transformOffset;
        [SerializeField] private WorldReplacementStatus replacementStatus;
        [SerializeField] private string authoringStatus = string.Empty;
        [SerializeField] private string validationStatus = string.Empty;
        [SerializeField] private string productionZone = string.Empty;
        [SerializeField] private string performanceTier = string.Empty;
        [SerializeField] private string wetnessCompatibility = string.Empty;
        [SerializeField] private string weatherCompatibility = string.Empty;
        [SerializeField] private string notes = string.Empty;
        [SerializeField] private string sourceControlStatus = string.Empty;
        [SerializeField] private string manualArtDependency = string.Empty;
        [SerializeField] private string lastValidationTimestamp = string.Empty;
        [SerializeField] private string assetVersion = string.Empty;

        public string StableWorldId => stableWorldId;
        public string DonorSourceReference => donorSourceReference;
        public string SemanticCategory => semanticCategory;
        public string ProductionPrefab => productionPrefab;
        public string ProductionMaterials => productionMaterials;
        public string CollisionReference => collisionReference;
        public string LodGroup => lodGroup;
        public string PivotMode => pivotMode;
        public string ScaleMode => scaleMode;
        public Bounds ExpectedBounds => expectedBounds;
        public Bounds MeasuredBounds => measuredBounds;
        public float AllowedDeviationMeters => allowedDeviationMeters;
        public Vector3 TransformOffset => transformOffset;
        public WorldReplacementStatus ReplacementStatus => replacementStatus;
        public string AuthoringStatus => authoringStatus;
        public string ValidationStatus => validationStatus;
        public string ProductionZone => productionZone;
        public string PerformanceTier => performanceTier;
        public string WetnessCompatibility => wetnessCompatibility;
        public string WeatherCompatibility => weatherCompatibility;
        public string Notes => notes;
        public string SourceControlStatus => sourceControlStatus;
        public string ManualArtDependency => manualArtDependency;
        public string LastValidationTimestamp => lastValidationTimestamp;
        public string AssetVersion => assetVersion;
        public bool HasProductionReplacement => !string.IsNullOrWhiteSpace(productionPrefab);

        public WorldReplacementBinding CreateBinding()
        {
            var binding = new WorldReplacementBinding();
            binding.Configure(stableWorldId, productionPrefab, productionZone);
            return binding;
        }

        public void Configure(
            string worldId,
            string donorReference,
            string category,
            string prefab,
            string materials,
            string collision,
            string lod,
            string pivot,
            string scale,
            Bounds expected,
            Bounds measured,
            float allowedDeviation,
            Vector3 offset,
            WorldReplacementStatus status,
            string authoring,
            string validation,
            string zone,
            string performance,
            string wetness,
            string weather,
            string recordNotes,
            string sourceControl,
            string manualArt,
            string validationTimestamp,
            string version)
        {
            stableWorldId = worldId ?? string.Empty;
            donorSourceReference = donorReference ?? string.Empty;
            semanticCategory = category ?? string.Empty;
            productionPrefab = prefab ?? string.Empty;
            productionMaterials = materials ?? string.Empty;
            collisionReference = collision ?? string.Empty;
            lodGroup = lod ?? string.Empty;
            pivotMode = pivot ?? string.Empty;
            scaleMode = scale ?? string.Empty;
            expectedBounds = expected;
            measuredBounds = measured;
            allowedDeviationMeters = Mathf.Max(0f, allowedDeviation);
            transformOffset = offset;
            replacementStatus = status;
            authoringStatus = authoring ?? string.Empty;
            validationStatus = validation ?? string.Empty;
            productionZone = zone ?? string.Empty;
            performanceTier = performance ?? string.Empty;
            wetnessCompatibility = wetness ?? string.Empty;
            weatherCompatibility = weather ?? string.Empty;
            notes = recordNotes ?? string.Empty;
            sourceControlStatus = sourceControl ?? string.Empty;
            manualArtDependency = manualArt ?? string.Empty;
            lastValidationTimestamp = validationTimestamp ?? string.Empty;
            assetVersion = version ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class WorldProductionZoneRecord
    {
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private int referenceRecordCount;
        [SerializeField] private int mappedRecordCount;
        [SerializeField] private WorldReplacementStatus status;
        [SerializeField] private string automatedValidation = string.Empty;
        [SerializeField] private string manualValidation = string.Empty;
        [SerializeField] private string productionCellPath = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string ZoneId => zoneId;
        public int ReferenceRecordCount => referenceRecordCount;
        public int MappedRecordCount => mappedRecordCount;
        public WorldReplacementStatus Status => status;
        public string AutomatedValidation => automatedValidation;
        public string ManualValidation => manualValidation;
        public string ProductionCellPath => productionCellPath;
        public string Notes => notes;
        public float CoveragePercent => referenceRecordCount <= 0
            ? 0f
            : 100f * mappedRecordCount / referenceRecordCount;

        public void Configure(
            string id,
            int referenceCount,
            int mappedCount,
            WorldReplacementStatus replacementStatus,
            string automated,
            string manual,
            string cellPath,
            string recordNotes)
        {
            zoneId = id ?? string.Empty;
            referenceRecordCount = Mathf.Max(0, referenceCount);
            mappedRecordCount = Mathf.Clamp(mappedCount, 0, referenceRecordCount);
            status = replacementStatus;
            automatedValidation = automated ?? string.Empty;
            manualValidation = manual ?? string.Empty;
            productionCellPath = cellPath ?? string.Empty;
            notes = recordNotes ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class WorldArtTaskRecord
    {
        [SerializeField] private string taskId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string priority = string.Empty;
        [SerializeField] private string status = string.Empty;
        [SerializeField] private string requiredTool = string.Empty;
        [SerializeField] private string acceptance = string.Empty;

        public string TaskId => taskId;
        public string ZoneId => zoneId;
        public string Category => category;
        public string Priority => priority;
        public string Status => status;
        public string RequiredTool => requiredTool;
        public string Acceptance => acceptance;

        public void Configure(
            string id,
            string zone,
            string taskCategory,
            string taskPriority,
            string taskStatus,
            string tool,
            string acceptanceCriteria)
        {
            taskId = id ?? string.Empty;
            zoneId = zone ?? string.Empty;
            category = taskCategory ?? string.Empty;
            priority = taskPriority ?? string.Empty;
            status = taskStatus ?? string.Empty;
            requiredTool = tool ?? string.Empty;
            acceptance = acceptanceCriteria ?? string.Empty;
        }
    }

    [CreateAssetMenu(menuName = "MSC Remake/World Remaster/Production Asset Registry")]
    public sealed class WorldProductionAssetRegistry : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string registryVersion = string.Empty;
        [SerializeField] private string sourceDatabaseVersion = string.Empty;
        [SerializeField] private string pilotZoneId = string.Empty;
        [SerializeField] private WorldProductionAssetRecord[] records = Array.Empty<WorldProductionAssetRecord>();
        [SerializeField] private WorldProductionZoneRecord[] zones = Array.Empty<WorldProductionZoneRecord>();
        [SerializeField] private WorldArtTaskRecord[] artTasks = Array.Empty<WorldArtTaskRecord>();

        public int SchemaVersion => schemaVersion;
        public string RegistryVersion => registryVersion;
        public string SourceDatabaseVersion => sourceDatabaseVersion;
        public string PilotZoneId => pilotZoneId;
        public IReadOnlyList<WorldProductionAssetRecord> Records => records;
        public IReadOnlyList<WorldProductionZoneRecord> Zones => zones;
        public IReadOnlyList<WorldArtTaskRecord> ArtTasks => artTasks;

        public void Configure(
            string version,
            string databaseVersion,
            string pilotZone,
            WorldProductionAssetRecord[] assetRecords,
            WorldProductionZoneRecord[] zoneRecords,
            WorldArtTaskRecord[] tasks)
        {
            schemaVersion = CurrentSchemaVersion;
            registryVersion = version ?? string.Empty;
            sourceDatabaseVersion = databaseVersion ?? string.Empty;
            pilotZoneId = pilotZone ?? string.Empty;
            records = assetRecords ?? Array.Empty<WorldProductionAssetRecord>();
            zones = zoneRecords ?? Array.Empty<WorldProductionZoneRecord>();
            artTasks = tasks ?? Array.Empty<WorldArtTaskRecord>();
        }
    }

    public static class WorldProductionRegistryValidation
    {
        public static IReadOnlyList<string> Validate(WorldProductionAssetRegistry registry)
        {
            var errors = new List<string>();
            if (registry == null)
            {
                errors.Add("Production registry is missing.");
                return errors;
            }

            if (registry.SchemaVersion != WorldProductionAssetRegistry.CurrentSchemaVersion)
            {
                errors.Add($"Unsupported production registry schema {registry.SchemaVersion}.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < registry.Records.Count; index++)
            {
                WorldProductionAssetRecord record = registry.Records[index];
                if (record == null || string.IsNullOrWhiteSpace(record.StableWorldId))
                {
                    errors.Add($"Record {index} has no stable world ID.");
                    continue;
                }

                if (!ids.Add(record.StableWorldId))
                {
                    errors.Add("Duplicate stable world ID: " + record.StableWorldId);
                }

                if (record.HasProductionReplacement &&
                    (record.ProductionPrefab.Contains("/LegacyImport/ReferenceOnly/", StringComparison.OrdinalIgnoreCase) ||
                     record.ProductionPrefab.Contains("/Imported/DonorGenerated/", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add("Production replacement points to donor/reference content: " + record.StableWorldId);
                }

                if (record.ReplacementStatus >= WorldReplacementStatus.ProductionCandidate &&
                    record.ReplacementStatus <= WorldReplacementStatus.Verified &&
                    !record.HasProductionReplacement)
                {
                    errors.Add("Production status has no prefab: " + record.StableWorldId);
                }
            }

            var zoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < registry.Zones.Count; index++)
            {
                WorldProductionZoneRecord zone = registry.Zones[index];
                if (zone == null || string.IsNullOrWhiteSpace(zone.ZoneId) || !zoneIds.Add(zone.ZoneId))
                {
                    errors.Add($"Zone record {index} is missing or duplicated.");
                }
            }

            if (!zoneIds.Contains(registry.PilotZoneId))
            {
                errors.Add("Pilot zone is absent from zone registry: " + registry.PilotZoneId);
            }

            return errors;
        }

        public static float MaximumBoundsDeviation(Bounds expected, Bounds measured)
        {
            Vector3 centerDelta = expected.center - measured.center;
            Vector3 sizeDelta = expected.size - measured.size;
            return Mathf.Max(
                Mathf.Max(Mathf.Abs(centerDelta.x), Mathf.Abs(centerDelta.y), Mathf.Abs(centerDelta.z)),
                Mathf.Max(Mathf.Abs(sizeDelta.x), Mathf.Abs(sizeDelta.y), Mathf.Abs(sizeDelta.z)));
        }

        public static float PivotDeviation(Vector3 expected, Vector3 measured) =>
            Vector3.Distance(expected, measured);

        public static float MaximumRoadDeviation(
            IReadOnlyList<Vector3> reference,
            IReadOnlyList<Vector3> production)
        {
            if (reference == null || production == null || reference.Count == 0 || reference.Count != production.Count)
            {
                return float.PositiveInfinity;
            }

            float maximum = 0f;
            for (int index = 0; index < reference.Count; index++)
            {
                maximum = Mathf.Max(maximum, Vector3.Distance(reference[index], production[index]));
            }

            return maximum;
        }
    }
}
