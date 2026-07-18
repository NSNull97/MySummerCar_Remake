using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport;
using MSC.Weather.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Enviro3Integration.Editor
{
    /// <summary>
    /// Read-only authoring evidence for project-owned shelter volumes. Donor
    /// hierarchy paths remain Editor-only provenance and never become runtime
    /// lookup keys.
    /// </summary>
    public static class ProductionShelterMeasurementTool
    {
        public const string HomeCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-3_Legacy.unity";
        public const string EvidenceAssetPath =
            "Assets/Game/Weather/Enviro3Integration/Editor/Evidence/ProductionShelterAuthoringEvidence.json";
        public const string LogOutputPath =
            "Logs/M07C_ProductionShelterMeasurements.json";
        public const string SourceRevisionId =
            "msc-world-baseline-04a1.1-c3f2f337";
        public const string ExpectedSourceSceneSha256 =
            "6253afec3050187d41bab2d7417238a61341d86c55898c185eab319d85fee3e8";
        public const string DerivationPolicy =
            "frozen-renderer-aabb-inset-v1";
        public const string HomeHouseShelterStableId =
            "weather.shelter.home.house.interior.v1";
        public const string HomeGarageShelterStableId =
            "weather.shelter.home.garage.interior.v1";
        public const int ExpectedShelterCount = 2;
        public const float HorizontalInsetMeters = 0.10f;
        public const float FloorInsetMeters = 0.05f;
        public const float CeilingInsetMeters = 0.10f;

        private const float MinimumInteriorExtentMeters = 0.25f;

        private static readonly Candidate[] HomeCandidates =
        {
            new Candidate(
                "home-main-roof-audit-only",
                "1704d967080fa78707fe486f215b2811",
                "audit-only; its aggregate AABB reaches below the floor"),
            new Candidate(
                "home-secondary-roof",
                "a71d9deddebb37f4a37bd02b13cef9f9",
                "house-ceiling"),
            new Candidate(
                "home-living-roof",
                "3f48c3f8da113c762f5ef4d32409e45c",
                "house-ceiling"),
            new Candidate(
                "home-floor-bathroom",
                "42aa8c997b8afde59d68e3ee7f9f73b7",
                "house-floor"),
            new Candidate(
                "home-floor-carpet",
                "f818a8da820952a72d428d74815488b4",
                "house-floor"),
            new Candidate(
                "home-floor-tiles",
                "2b391ab6d5dd0500a5a06391e45b7150",
                "house-floor"),
            new Candidate(
                "home-garage-shell",
                "419f49d30da6bff0fcfa679c84d652ce",
                "garage-shell-and-floor"),
            new Candidate(
                "home-garage-roof",
                "b5e7b987d5aac9da197a6ce662beb64c",
                "garage-ceiling"),
        };

        private static readonly string[] HouseFloorSourceIds =
        {
            "42aa8c997b8afde59d68e3ee7f9f73b7",
            "f818a8da820952a72d428d74815488b4",
            "2b391ab6d5dd0500a5a06391e45b7150",
        };

        private static readonly string[] HouseCeilingSourceIds =
        {
            "a71d9deddebb37f4a37bd02b13cef9f9",
            "3f48c3f8da113c762f5ef4d32409e45c",
        };

        private static readonly string[] GarageSourceIds =
        {
            "419f49d30da6bff0fcfa679c84d652ce",
            "b5e7b987d5aac9da197a6ce662beb64c",
        };

        [MenuItem(
            "Tools/MSC Remake/Production Weather/Measure Frozen Home Shelter Candidates")]
        public static void MeasureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            MeasureOrThrow();
        }

        public static void RunBatch() => MeasureOrThrow();

        public static MeasurementReport MeasureOrThrow()
        {
            string fullSourcePath = Path.GetFullPath(HomeCellScenePath);
            if (!File.Exists(fullSourcePath))
            {
                throw new FileNotFoundException(
                    "Frozen home donor-baseline cell is unavailable.",
                    HomeCellScenePath);
            }

            string currentSourceSha256 = ComputeSha256(fullSourcePath);
            if (!string.Equals(
                    currentSourceSha256,
                    ExpectedSourceSceneSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Frozen home donor-baseline cell hash drifted from the " +
                    "pinned 07C shelter source.");
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(HomeCellScenePath);
            bool openedForMeasurement = !scene.IsValid() || !scene.isLoaded;
            if (openedForMeasurement)
            {
                scene = EditorSceneManager.OpenScene(
                    HomeCellScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                return MeasureLoadedSceneOrThrow(
                    scene,
                    currentSourceSha256);
            }
            finally
            {
                if (openedForMeasurement && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if (previousActiveScene.IsValid() &&
                    previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        private static MeasurementReport MeasureLoadedSceneOrThrow(
            Scene scene,
            string sourceSceneSha256)
        {
            DonorWorldBaselineEntityMetadata[] metadata = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    DonorWorldBaselineEntityMetadata>(true))
                .ToArray();
            var byStableId = metadata.ToDictionary(
                value => value.StableId,
                StringComparer.Ordinal);
            var records = new List<MeasurementRecord>(HomeCandidates.Length);
            for (int index = 0; index < HomeCandidates.Length; index++)
            {
                Candidate candidate = HomeCandidates[index];
                if (!byStableId.TryGetValue(
                        candidate.StableId,
                        out DonorWorldBaselineEntityMetadata entity))
                {
                    throw new InvalidOperationException(
                        "Frozen shelter candidate is missing: " +
                        candidate.StableId);
                }

                if (!entity.HasSanitizedRenderer ||
                    entity.Classification !=
                    DonorWorldBaselineClassification.TemporaryDirectImport ||
                    !string.Equals(
                        entity.SourceCellId,
                        "cell_0_-3",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Shelter candidate is outside the frozen sanitized home baseline: " +
                        candidate.StableId);
                }

                Renderer renderer = entity.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        "Frozen shelter candidate has no active renderer: " +
                        candidate.StableId);
                }

                Bounds bounds = renderer.bounds;
                records.Add(new MeasurementRecord
                {
                    label = candidate.Label,
                    stableId = candidate.StableId,
                    derivationRole = candidate.DerivationRole,
                    sourceHierarchyPath = entity.SourceHierarchyPath,
                    center = bounds.center,
                    extents = bounds.extents,
                });
            }

            ShelterMeasurementRecord[] shelters =
                DeriveSheltersOrThrow(records.ToArray());
            var report = new MeasurementReport
            {
                schemaVersion = 1,
                sourceRevisionId = SourceRevisionId,
                sourceScene = HomeCellScenePath,
                sourceSceneSha256 = sourceSceneSha256,
                derivationPolicy = DerivationPolicy,
                horizontalInsetMeters = HorizontalInsetMeters,
                floorInsetMeters = FloorInsetMeters,
                ceilingInsetMeters = CeilingInsetMeters,
                records = records.ToArray(),
                shelters = shelters,
            };
            ValidateReportAgainstFrozenSourceOrThrow(report);
            WriteJson(LogOutputPath, report, importAsset: false);
            WriteJson(EvidenceAssetPath, report, importAsset: true);
            Debug.Log(
                "M07C_PRODUCTION_SHELTER_MEASUREMENTS_OK " +
                $"sources={records.Count} shelters={shelters.Length} " +
                $"evidence={EvidenceAssetPath} log={LogOutputPath}");
            return report;
        }

        public static ShelterMeasurementRecord[] DeriveSheltersOrThrow(
            MeasurementRecord[] records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            var byStableId = new Dictionary<string, MeasurementRecord>(
                records.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < records.Length; index++)
            {
                MeasurementRecord record = records[index] ??
                    throw new InvalidOperationException(
                        "Shelter measurement record is null.");
                ValidateMeasuredBounds(record);
                if (!byStableId.TryAdd(record.stableId, record))
                {
                    throw new InvalidOperationException(
                        "Duplicate shelter source stable ID: " +
                        record.stableId);
                }
            }

            Bounds houseFloorBounds = UnionBounds(
                byStableId,
                HouseFloorSourceIds);
            float houseCeilingY = float.PositiveInfinity;
            for (int index = 0;
                 index < HouseCeilingSourceIds.Length;
                 index++)
            {
                Bounds ceiling = RequireBounds(
                    byStableId,
                    HouseCeilingSourceIds[index]);
                houseCeilingY = Mathf.Min(
                    houseCeilingY,
                    ceiling.min.y);
            }

            Bounds garageShellBounds = RequireBounds(
                byStableId,
                GarageSourceIds[0]);
            Bounds garageRoofBounds = RequireBounds(
                byStableId,
                GarageSourceIds[1]);

            return new[]
            {
                CreateShelter(
                    HomeHouseShelterStableId,
                    houseFloorBounds,
                    houseFloorBounds.max.y,
                    houseCeilingY,
                    HouseFloorSourceIds.Concat(
                        HouseCeilingSourceIds).ToArray()),
                CreateShelter(
                    HomeGarageShelterStableId,
                    garageShellBounds,
                    garageShellBounds.min.y,
                    garageRoofBounds.min.y,
                    GarageSourceIds),
            };
        }

        public static void ValidateReportAgainstFrozenSourceOrThrow(
            MeasurementReport report)
        {
            if (report == null || report.schemaVersion != 1 ||
                !string.Equals(
                    report.sourceRevisionId,
                    SourceRevisionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    report.sourceScene,
                    HomeCellScenePath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    report.derivationPolicy,
                    DerivationPolicy,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    report.sourceSceneSha256,
                    ExpectedSourceSceneSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !Mathf.Approximately(
                    report.horizontalInsetMeters,
                    HorizontalInsetMeters) ||
                !Mathf.Approximately(
                    report.floorInsetMeters,
                    FloorInsetMeters) ||
                !Mathf.Approximately(
                    report.ceilingInsetMeters,
                    CeilingInsetMeters) ||
                report.records == null ||
                report.records.Length != HomeCandidates.Length ||
                report.shelters == null ||
                report.shelters.Length != ExpectedShelterCount)
            {
                throw new InvalidOperationException(
                    "Production shelter evidence has an invalid schema or scope.");
            }

            string fullSourcePath = Path.GetFullPath(HomeCellScenePath);
            if (!File.Exists(fullSourcePath) ||
                !string.Equals(
                    ExpectedSourceSceneSha256,
                    ComputeSha256(fullSourcePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production shelter evidence does not match the frozen source scene hash.");
            }

            ShelterMeasurementRecord[] derived =
                DeriveSheltersOrThrow(report.records);
            for (int index = 0; index < derived.Length; index++)
            {
                ShelterMeasurementRecord expected = derived[index];
                ShelterMeasurementRecord recorded = report.shelters[index];
                if (!string.Equals(
                        expected.stableId,
                        recorded.stableId,
                        StringComparison.Ordinal) ||
                    expected.shelterKind != recorded.shelterKind ||
                    !Approximately(expected.center, recorded.center) ||
                    !Approximately(expected.extents, recorded.extents))
                {
                    throw new InvalidOperationException(
                        "Production shelter evidence contains stale derived bounds for " +
                        expected.stableId);
                }
            }
        }

        private static ShelterMeasurementRecord CreateShelter(
            string stableId,
            Bounds horizontalSource,
            float sourceFloorY,
            float sourceCeilingY,
            string[] sourceStableIds)
        {
            float minimumX = horizontalSource.min.x + HorizontalInsetMeters;
            float maximumX = horizontalSource.max.x - HorizontalInsetMeters;
            float minimumZ = horizontalSource.min.z + HorizontalInsetMeters;
            float maximumZ = horizontalSource.max.z - HorizontalInsetMeters;
            float minimumY = sourceFloorY + FloorInsetMeters;
            float maximumY = sourceCeilingY - CeilingInsetMeters;
            Vector3 extents = new Vector3(
                (maximumX - minimumX) * 0.5f,
                (maximumY - minimumY) * 0.5f,
                (maximumZ - minimumZ) * 0.5f);
            if (!IsFinite(extents) ||
                extents.x < MinimumInteriorExtentMeters ||
                extents.y < MinimumInteriorExtentMeters ||
                extents.z < MinimumInteriorExtentMeters)
            {
                throw new InvalidOperationException(
                    "Measured source bounds do not form a valid interior for " +
                    stableId);
            }

            return new ShelterMeasurementRecord
            {
                stableId = stableId,
                shelterKind = (int)ProductionShelterKind.Interior,
                center = new Vector3(
                    (minimumX + maximumX) * 0.5f,
                    (minimumY + maximumY) * 0.5f,
                    (minimumZ + maximumZ) * 0.5f),
                extents = extents,
                sourceStableIds = (string[])sourceStableIds.Clone(),
            };
        }

        private static Bounds UnionBounds(
            IReadOnlyDictionary<string, MeasurementRecord> records,
            string[] stableIds)
        {
            Bounds result = RequireBounds(records, stableIds[0]);
            for (int index = 1; index < stableIds.Length; index++)
            {
                result.Encapsulate(RequireBounds(records, stableIds[index]));
            }

            return result;
        }

        private static Bounds RequireBounds(
            IReadOnlyDictionary<string, MeasurementRecord> records,
            string stableId)
        {
            if (!records.TryGetValue(stableId, out MeasurementRecord record))
            {
                throw new InvalidOperationException(
                    "Required shelter source is missing: " + stableId);
            }

            return new Bounds(record.center, record.extents * 2f);
        }

        private static void ValidateMeasuredBounds(MeasurementRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.stableId) ||
                !IsFinite(record.center) || !IsFinite(record.extents) ||
                record.extents.x <= 0f || record.extents.y <= 0f ||
                record.extents.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Shelter source has invalid measured bounds: " +
                    record.stableId);
            }
        }

        private static void WriteJson(
            string relativePath,
            MeasurementReport report,
            bool importAsset)
        {
            if (importAsset)
            {
                string assetFolder = Path.GetDirectoryName(relativePath)
                    ?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(assetFolder))
                {
                    throw new InvalidOperationException(
                        "Shelter evidence asset path has no parent folder.");
                }

                EnsureAssetFolder(assetFolder);
            }

            string fullPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullPath) ??
                Path.GetFullPath("Logs"));
            File.WriteAllText(
                fullPath,
                JsonUtility.ToJson(report, true) + Environment.NewLine,
                new UTF8Encoding(false));
            if (importAsset)
            {
                AssetDatabase.ImportAsset(
                    relativePath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    "Invalid shelter evidence asset folder: " + path);
            }

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ComputeSha256(string path)
        {
            string canonicalText = File.ReadAllText(path, Encoding.UTF8)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            byte[] payload = Encoding.UTF8.GetBytes(canonicalText);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(payload);
            var builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }

            return builder.ToString();
        }

        private static bool Approximately(Vector3 left, Vector3 right) =>
            (left - right).sqrMagnitude <= 0.000001f;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private readonly struct Candidate
        {
            public Candidate(
                string label,
                string stableId,
                string derivationRole)
            {
                Label = label;
                StableId = stableId;
                DerivationRole = derivationRole;
            }

            public string Label { get; }
            public string StableId { get; }
            public string DerivationRole { get; }
        }

        [Serializable]
        public sealed class MeasurementReport
        {
            public int schemaVersion;
            public string sourceRevisionId = string.Empty;
            public string sourceScene = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public string derivationPolicy = string.Empty;
            public float horizontalInsetMeters;
            public float floorInsetMeters;
            public float ceilingInsetMeters;
            public MeasurementRecord[] records =
                Array.Empty<MeasurementRecord>();
            public ShelterMeasurementRecord[] shelters =
                Array.Empty<ShelterMeasurementRecord>();
        }

        [Serializable]
        public sealed class MeasurementRecord
        {
            public string label = string.Empty;
            public string stableId = string.Empty;
            public string derivationRole = string.Empty;
            public string sourceHierarchyPath = string.Empty;
            public Vector3 center;
            public Vector3 extents;
        }

        [Serializable]
        public sealed class ShelterMeasurementRecord
        {
            public string stableId = string.Empty;
            public int shelterKind;
            public Vector3 center;
            public Vector3 extents;
            public string[] sourceStableIds = Array.Empty<string>();
        }
    }
}
