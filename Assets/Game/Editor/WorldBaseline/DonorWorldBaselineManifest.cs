using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Editor.WorldTransfer;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    [Serializable]
    public sealed class DonorWorldBaselineSourceRootRecord
    {
        public string rootId = string.Empty;
        public string configuredRelativePath = string.Empty;
        public string purpose = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineSourceFileRecord
    {
        public string rootId = string.Empty;
        public string relativePath = string.Empty;
        public long lengthBytes;
        public string sha256 = string.Empty;
        public string role = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineContainerRecord
    {
        public string donorRelativePath = string.Empty;
        public string sha256 = string.Empty;
        public string availability = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineToolRecord
    {
        public string tool = string.Empty;
        public string version = string.Empty;
        public string role = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineCoordinateRecord
    {
        public string axes = string.Empty;
        public string handedness = string.Empty;
        public string units = string.Empty;
        public Vector3 sourceToProjectTranslation;
        public Quaternion sourceToProjectRotation = Quaternion.identity;
        public Vector3 sourceToProjectScale = Vector3.one;
        public Vector3 canonicalSceneRootPosition;
        public Quaternion canonicalSceneRootRotation = Quaternion.identity;
        public Vector3 canonicalSceneRootScale = Vector3.one;
        public string originPolicy = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineBoundsRecord
    {
        public Vector3 sourceMin;
        public Vector3 sourceMax;
        public Vector3 convertedMin;
        public Vector3 convertedMax;
    }

    [Serializable]
    public sealed class DonorWorldBaselineSanitationCounts
    {
        public int sourcePlacementCount;
        public int geometryEntityCount;
        public int eligibleEntityCount;
        public int sourceActiveSelfEntityCount;
        public int sourceInactiveSelfEntityCount;
        public int effectiveActiveEntityCount;
        public int effectiveInactiveEntityCount;
        public int rendererAcceptedCount;
        public int activeRendererCount;
        public int inactiveRendererCount;
        public int metadataOnlyCount;
        public int metadataOnlyMissingMeshCount;
        public int metadataOnlySkinnedMeshCount;
        public int metadataOnlyCharacterHierarchyCount;
        public int sourceColliderRecordCount;
        public int runtimeColliderCount;
        public int sourceUnsupportedClassIdCount;
        public int sourceBoundsReviewCount;
        public int partitionCellCount;
        public int globalEntityCount;
    }

    [Serializable]
    public sealed class DonorWorldBaselinePrototypeCellRecord
    {
        public string cellId = string.Empty;
        public string productionScene = string.Empty;
        public string disposition = string.Empty;
        public string featureParityProfileState = string.Empty;
        public string activationSwitch = string.Empty;
    }

    [Serializable]
    public sealed class DonorWorldBaselineSourceManifestData
    {
        public int schemaVersion = 1;
        public string manifestId = "donor-world-baseline-source";
        public string sourceRevisionId = string.Empty;
        public string classification = "TemporaryDirectImport";
        public string distributionPolicy = "PrivateLocalFeatureParityOnly";
        public string activationState = "PreparedNotActiveUntil06B2";
        public string sourcePolicy = "FrozenStagingOnly_DoNotMixWithCurrentDonor";
        public string canonicalScenePath = string.Empty;
        public string generatedPayloadBoundary = string.Empty;
        public string databaseVersion = string.Empty;
        public string sanitationPolicyVersion = string.Empty;
        public string extractedSceneSha256 = string.Empty;
        public string semanticFingerprintSha256 = string.Empty;
        public string sourceFilesFingerprintSha256 = string.Empty;
        public string runtimePayloadFingerprintSha256 = string.Empty;
        public DonorWorldBaselineSourceRootRecord[] sourceRoots =
            Array.Empty<DonorWorldBaselineSourceRootRecord>();
        public DonorWorldBaselineContainerRecord[] historicalSourceContainers =
            Array.Empty<DonorWorldBaselineContainerRecord>();
        public DonorWorldBaselineToolRecord[] tools =
            Array.Empty<DonorWorldBaselineToolRecord>();
        public DonorWorldBaselineCoordinateRecord coordinateSystem =
            new DonorWorldBaselineCoordinateRecord();
        public DonorWorldBaselineBoundsRecord mapBounds =
            new DonorWorldBaselineBoundsRecord();
        public DonorWorldBaselineSanitationCounts counts =
            new DonorWorldBaselineSanitationCounts();
        public string[] whitelistRuntimeComponentTypes = Array.Empty<string>();
        public string[] forbiddenRuntimeFamilies = Array.Empty<string>();
        public string[] knownGlobalObjects = Array.Empty<string>();
        public string[] knownMissingOrUnsupported = Array.Empty<string>();
        public string[] knownLegacyVisualDefects = Array.Empty<string>();
        public DonorWorldBaselinePrototypeCellRecord[] prototypeCells =
            Array.Empty<DonorWorldBaselinePrototypeCellRecord>();
        public DonorWorldBaselineSourceFileRecord[] sourceFiles =
            Array.Empty<DonorWorldBaselineSourceFileRecord>();
    }

    public static class DonorWorldBaselineManifest
    {
        private const string RawExportAssetsRelativePath =
            "assetripper-unity-project/ExportedProject/Assets";
        private const string ExtractedSceneRelativePath =
            RawExportAssetsRelativePath + "/_Scenes/GAME.unity";
        private const string PathIdMapRelativePath =
            "assetripper-unity-project/AuxiliaryFiles/path_id_map.json";

        private static readonly string[] NormalizedFiles =
        {
            "WorldColliderManifest.csv",
            "WorldGeometryManifest.json",
            "WorldInteriorManifest.csv",
            "WorldLandmarkManifest.csv",
            "WorldMeshManifest.csv",
            "WorldMissingReferences.csv",
            "WorldObjectPlacements.csv",
            "WorldRoadManifest.json",
            "WorldTerrainManifest.json",
            "WorldUnsupportedObjects.csv",
            "WorldVegetationManifest.csv",
            "WorldWaterManifest.json"
        };

        private static readonly string[] ProjectSourceFiles =
        {
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json",
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv",
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldColliders.csv",
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldLandmarks.csv",
            "Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv",
            "Config/WorldTransferRules.example.json"
        };

        private static readonly IReadOnlyDictionary<string, string>
            NormalizedFileHashes =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["WorldColliderManifest.csv"] =
                        "5de8f612ff4dd6a23d9e18e23ecbba88e520a1334533dfee57a2bb14e839cd79",
                    ["WorldGeometryManifest.json"] =
                        "6b3161dd4d5e3ed37ea35d57c1ab4fd6a8c653352eac62640cf0cd7b31539bee",
                    ["WorldInteriorManifest.csv"] =
                        "7c38e4ce0af0c09f2bd0d04b7b716b4b788f50c2552e3c7b6546ee1d9767aa99",
                    ["WorldLandmarkManifest.csv"] =
                        "f55d88c326d1b3a981a13c51e13f426733bed14754f764757ec53e22bed30115",
                    ["WorldMeshManifest.csv"] =
                        "d91370e086626381af44f5be3bffe6067deef693527c630099e970a53f395f8e",
                    ["WorldMissingReferences.csv"] =
                        "33093bf25c912ee9785d1d9d5461c7ecd65d9c9d11620160650faa309b6c0e6c",
                    ["WorldObjectPlacements.csv"] =
                        "b894b8a6416fc1b74eabb1821b71e407cbedbd3ac9afabeee262c7891004758a",
                    ["WorldRoadManifest.json"] =
                        "db1ebce35ae0e8587506e0f79d35cc4ba45fad388b2b7e6374f055508250b6b2",
                    ["WorldTerrainManifest.json"] =
                        "4b20748caf38dbb42a74aecfdc62b26be4825bd6c3adc50fea49bdc7693dc9f4",
                    ["WorldUnsupportedObjects.csv"] =
                        "8a699d6e669dd4231344bde37c0f888ad048858d270146d4932697c9277916f7",
                    ["WorldVegetationManifest.csv"] =
                        "279ebac82b1c9a2c27a0f1aae96936e7875ce71fff986f923b4753e8e876bdb1",
                    ["WorldWaterManifest.json"] =
                        "9dbf1576f7517e224661a438fe7347be188cde947cb25e4cef1ad5d734dc0729"
                };

        private static readonly IReadOnlyDictionary<string, string>
            ProjectSourceFileHashes =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json"] =
                        "706a4303a715539ac2d45a5ab0dff487b685bfa3f336c57a10a477a45a454fa8",
                    ["Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv"] =
                        "a0f45c9eb50b2f7dff90caa9d848a0a20be463ce333eae19da576f91dd31a408",
                    ["Assets/Game/World/Content/WorldTransfer/M04A1_WorldColliders.csv"] =
                        "5de8f612ff4dd6a23d9e18e23ecbba88e520a1334533dfee57a2bb14e839cd79",
                    ["Assets/Game/World/Content/WorldTransfer/M04A1_WorldLandmarks.csv"] =
                        "f55d88c326d1b3a981a13c51e13f426733bed14754f764757ec53e22bed30115",
                    ["Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv"] =
                        "69d3d20d88315528cfa028dc0117e6ef39edd966b4358b6ee6b1fe3b84c4dd12",
                    ["Config/WorldTransferRules.example.json"] =
                        "75f3d36dd48ac3f5f767becf53133368ba22348b1e45a50431274fd6c4a230b7"
                };

        public static DonorWorldBaselineSourceManifestData Write(
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            string semanticFingerprint,
            string runtimePayloadFingerprint)
        {
            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            WorldBaselineSanitationEntry[] entries = plan.ToArray();
            var files = new List<DonorWorldBaselineSourceFileRecord>();
            AddFile(files, "rawExtraction", configuration.RawExtractionPath,
                ExtractedSceneRelativePath, "CanonicalExtractedScene");
            AddFile(files, "rawExtraction", configuration.RawExtractionPath,
                PathIdMapRelativePath, "AssetRipperPathIdMap");

            foreach (string file in NormalizedFiles)
            {
                AddFile(files, "normalizedData", configuration.NormalizedDataPath,
                    file, "NormalizedWorldManifest");
            }

            foreach (string file in ProjectSourceFiles)
            {
                AddFile(files, "unityProject", WorldTransferPaths.ProjectRoot,
                    file, "ProjectOwnedFrozenTransferInput");
            }

            IReadOnlyDictionary<string, string> meshManifest =
                WorldReferenceMeshLibrarySync.ReadResolvedManifest(
                    Path.Combine(
                        configuration.NormalizedDataPath,
                        WorldReferenceMeshLibrarySync.MeshManifestFileName));
            string[] whitelistedMeshGuids = entries
                .Where(entry => entry.IncludeRenderer)
                .Select(entry => entry.Placement.MeshGuid)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string guid in whitelistedMeshGuids)
            {
                if (!meshManifest.TryGetValue(guid, out string assetRelativePath))
                {
                    throw new InvalidDataException(
                        "Whitelisted mesh GUID is absent from the frozen mesh manifest: " + guid);
                }

                string relativePath = RawExportAssetsRelativePath + "/" +
                    assetRelativePath.Replace('\\', '/');
                AddFile(files, "rawExtraction", configuration.RawExtractionPath,
                    relativePath, "WhitelistedMeshAsset");
                AddFile(files, "rawExtraction", configuration.RawExtractionPath,
                    relativePath + ".meta", "WhitelistedMeshImporterMetadata");
            }

            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            foreach (WorldBaselineSanitationEntry entry in entries
                         .Where(entry =>
                             entry.IncludeRenderer &&
                             staticBatchSubsets.ContainsKey(
                                 entry.Placement.SourceObjectId))
                         .OrderBy(entry =>
                             entry.Placement.StableId,
                             StringComparer.Ordinal))
            {
                string relativePath =
                    WorldTransferPaths.GeneratedMeshRoot + "/" +
                    entry.Placement.StableId + ".asset";
                AddFile(files, "unityProject", WorldTransferPaths.ProjectRoot,
                    relativePath, "AuditedDerivedStaticBatchMesh");
            }

            DonorWorldBaselineSourceFileRecord[] orderedFiles = files
                .OrderBy(record => record.rootId, StringComparer.Ordinal)
                .ThenBy(record => record.relativePath, StringComparer.Ordinal)
                .ToArray();
            string sourceFingerprint = ComputeFileSetFingerprint(orderedFiles);

            var manifest = new DonorWorldBaselineSourceManifestData
            {
                sourceRevisionId = WorldBaselinePaths.SourceRevisionId,
                canonicalScenePath = WorldBaselinePaths.CanonicalScene,
                generatedPayloadBoundary = WorldBaselinePaths.RuntimeRoot,
                databaseVersion = WorldPartitionBuilder.DatabaseVersion,
                sanitationPolicyVersion = WorldBaselineSanitationPlan.PolicyVersion,
                extractedSceneSha256 = WorldBaselinePaths.SourceSceneSha256,
                semanticFingerprintSha256 = semanticFingerprint,
                sourceFilesFingerprintSha256 = sourceFingerprint,
                runtimePayloadFingerprintSha256 = runtimePayloadFingerprint,
                sourceRoots = new[]
                {
                    new DonorWorldBaselineSourceRootRecord
                    {
                        rootId = "rawExtraction",
                        configuredRelativePath = "raw/world/milestone-04a1",
                        purpose = "Frozen AssetRipper export and auxiliary metadata"
                    },
                    new DonorWorldBaselineSourceRootRecord
                    {
                        rootId = "normalizedData",
                        configuredRelativePath = "normalized/world/milestone-04a1",
                        purpose = "Frozen normalized world manifests"
                    },
                    new DonorWorldBaselineSourceRootRecord
                    {
                        rootId = "unityProject",
                        configuredRelativePath = ".",
                        purpose = "Project-owned normalized database and sanitation inputs"
                    }
                },
                historicalSourceContainers = new[]
                {
                    new DonorWorldBaselineContainerRecord
                    {
                        donorRelativePath = "mysummercar_Data/level2",
                        sha256 = "39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31",
                        availability = "CurrentDonorStillMatches"
                    },
                    new DonorWorldBaselineContainerRecord
                    {
                        donorRelativePath = "mysummercar_Data/sharedassets3.assets",
                        sha256 = "1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684",
                        availability = "HistoricalHashOnly_CurrentDonorDrifted"
                    },
                    new DonorWorldBaselineContainerRecord
                    {
                        donorRelativePath = "mysummercar_Data/sharedassets3.resource",
                        sha256 = "19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b",
                        availability = "HistoricalHashOnly_CurrentDonorDrifted"
                    }
                },
                tools = new[]
                {
                    new DonorWorldBaselineToolRecord
                    {
                        tool = "AssetRipper",
                        version = "1.3.14",
                        role = "Read-only frozen external Unity export"
                    },
                    new DonorWorldBaselineToolRecord
                    {
                        tool = "WorldTransferExtractor",
                        version = "1.0.0",
                        role = "Deterministic YAML normalization"
                    },
                    new DonorWorldBaselineToolRecord
                    {
                        tool = "WorldPartitionBuilder",
                        version = WorldPartitionBuilder.GeneratorVersion,
                        role = "Existing reference mesh and static-batch subset preparation"
                    },
                    new DonorWorldBaselineToolRecord
                    {
                        tool = "DonorWorldBaselineBuilder",
                        version = DonorWorldBaselineBuilder.GeneratorVersion,
                        role = "Whitelist sanitation and canonical Unity 6 scene generation"
                    }
                },
                coordinateSystem = new DonorWorldBaselineCoordinateRecord
                {
                    axes = "X,Y,Z -> X,Y,Z",
                    handedness = "Unity left-handed Y-up retained",
                    units = "1 source unit = 1 project metre",
                    sourceToProjectTranslation = new Vector3(169.98f, 1.611f, -1040.625f),
                    sourceToProjectRotation = Quaternion.identity,
                    sourceToProjectScale = Vector3.one,
                    canonicalSceneRootPosition = Vector3.zero,
                    canonicalSceneRootRotation = Quaternion.identity,
                    canonicalSceneRootScale = Vector3.one,
                    originPolicy =
                        "Historical CABIN/Shed garage-roof anchor retained; do not recenter"
                },
                mapBounds = new DonorWorldBaselineBoundsRecord
                {
                    sourceMin = new Vector3(-3163.1853f, -414.44f, -2254.6733f),
                    sourceMax = new Vector3(3383.1853f, 911.76f, 3638.5908f),
                    convertedMin = new Vector3(-2993.2053f, -412.829f, -3295.2983f),
                    convertedMax = new Vector3(3553.1653f, 913.371f, 2597.9658f)
                },
                counts = BuildCounts(entries),
                whitelistRuntimeComponentTypes = new[]
                {
                    "UnityEngine.Transform",
                    "UnityEngine.MeshFilter",
                    "UnityEngine.MeshRenderer",
                    "UnityEngine.Light (project-owned neutral development light only)",
                    "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData (neutral development light only)",
                    "MSC.LegacyImport.DonorWorldBaselineSceneMetadata",
                    "MSC.LegacyImport.DonorWorldBaselineEntityMetadata"
                },
                forbiddenRuntimeFamilies = new[]
                {
                    "donor MonoBehaviour and PlayMaker FSM",
                    "donor runtime assemblies and legacy UnityEngine assemblies",
                    "Rigidbody, joints and gameplay triggers",
                    "SkinnedMeshRenderer, Animation, Animator and Cloth",
                    "Camera, AudioListener, AudioSource and donor lighting",
                    "particle effects, weather, UI, save, platform, Steam and DRM logic",
                    "runtime donor hierarchy-name or object-path lookup"
                },
                knownGlobalObjects = entries
                    .Where(entry => entry.Placement.CellId == "global")
                    .OrderBy(entry => entry.Placement.StableId, StringComparer.Ordinal)
                    .Select(entry =>
                        $"{entry.Placement.StableId}|{entry.Placement.Category}|" +
                        entry.Placement.OriginalName)
                    .ToArray(),
                knownMissingOrUnsupported = new[]
                {
                    "1237 eligible records are metadata-only after sanitation.",
                    "1058 eligible records have no usable static mesh GUID.",
                    "62 SkinnedMeshRenderer records are excluded from the canonical scene.",
                    "117 static renderer records below character skeleton hierarchies are excluded from map presentation.",
                    "1065 eligible records are inactive in the donor hierarchy and remain inactive.",
                    "850 records have activeSelf=true below an inactive ancestor; their effective inactive state is preserved after hierarchy flattening.",
                    "482 whitelisted static renderers are retained but disabled because they are inactive in the donor hierarchy.",
                    "37 serialized component class IDs lacked dedicated 04A processors and remain excluded.",
                    "2007 records have bounds-review metadata from combined/static mesh ambiguity.",
                    "One unrelated Texture2D was unreadable during AssetRipper export.",
                    "Runtime collision is intentionally zero in 06B1; safe collider candidates are deferred to 06B2."
                },
                knownLegacyVisualDefects = new[]
                {
                    "Donor terrain voids behind tree-wall cards remain unchanged.",
                    "Sprite forests, flat proxy fields and under-map water hacks remain legacy visual debt.",
                    "Temporary category materials do not reproduce donor textures or final HDRP materials.",
                    "LOD membership, source TextMesh presentation and inactive runtime alternatives are not reconstructed.",
                    "The canonical scene is not an active build/streaming profile until Milestone 06B2."
                },
                prototypeCells = new[]
                {
                    new DonorWorldBaselinePrototypeCellRecord
                    {
                        cellId = "cell_0_-3",
                        productionScene =
                            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity",
                        disposition =
                            "PrototypeOnly / RejectedForFidelity / NotProductionReady",
                        featureParityProfileState = "Inactive",
                        activationSwitch =
                            "FeatureParityDonorBaseline selects legacy visuals; wiring deferred to 06B2"
                    },
                    new DonorWorldBaselinePrototypeCellRecord
                    {
                        cellId = "cell_0_-2",
                        productionScene =
                            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity",
                        disposition =
                            "PrototypeOnly / RejectedForFidelity / NotProductionReady",
                        featureParityProfileState = "Inactive",
                        activationSwitch =
                            "FeatureParityDonorBaseline selects legacy visuals; wiring deferred to 06B2"
                    }
                },
                sourceFiles = orderedFiles
            };

            string outputPath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.SourceManifest);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ??
                throw new InvalidOperationException("Source manifest has no directory."));
            File.WriteAllText(
                outputPath,
                JsonUtility.ToJson(manifest, true) + Environment.NewLine,
                new UTF8Encoding(false));
            return manifest;
        }

        public static void AssertCanonicalFrozenInputsMatchRevision()
        {
            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            AssertFileHash(
                configuration.RawExtractionPath,
                ExtractedSceneRelativePath,
                WorldBaselinePaths.SourceSceneSha256);
            AssertFileHash(
                configuration.RawExtractionPath,
                PathIdMapRelativePath,
                "dfdbd71aac8a942c78f27dfd54d73ff3f1fdce3670271482a6fd2c6aa8c73165");

            foreach (string file in NormalizedFiles)
            {
                AssertFileHash(
                    configuration.NormalizedDataPath,
                    file,
                    NormalizedFileHashes[file]);
            }

            foreach (string file in ProjectSourceFiles)
            {
                AssertFileHash(
                    WorldTransferPaths.ProjectRoot,
                    file,
                    ProjectSourceFileHashes[file]);
            }
        }

        public static string ComputeRuntimePayloadFingerprint(
            IReadOnlyList<WorldBaselineSanitationEntry> plan)
        {
            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldBaselineSanitationEntry entry in
                     plan.Where(entry => entry.IncludeRenderer))
            {
                paths.Add(
                    subsets.ContainsKey(entry.Placement.SourceObjectId)
                        ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                          entry.Placement.StableId + ".asset"
                        : WorldBaselinePaths.SourceMeshRoot + "/" +
                          entry.Placement.MeshGuid + ".asset");
                paths.Add(
                    WorldBaselinePaths.CategoryMaterial(
                        entry.Placement.Category));
            }

            var records = new List<DonorWorldBaselineSourceFileRecord>(
                paths.Count);
            foreach (string path in paths.OrderBy(
                         value => value, StringComparer.Ordinal))
            {
                string absolutePath =
                    WorldBaselinePaths.ToAbsoluteProjectPath(path);
                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException(
                        "Expected generated runtime-baseline payload is missing.",
                        absolutePath);
                }

                var info = new FileInfo(absolutePath);
                records.Add(new DonorWorldBaselineSourceFileRecord
                {
                    rootId = "runtimeGenerated",
                    relativePath = path,
                    lengthBytes = info.Length,
                    sha256 = ComputeFileSha256(absolutePath),
                    role = "SanitizedRuntimePayload"
                });
            }

            return ComputeFileSetFingerprint(records);
        }

        public static void AssertDeterministicOutputMatchesPrevious(
            DonorWorldBaselineSourceManifestData previous,
            string semanticFingerprint,
            string runtimePayloadFingerprint)
        {
            if (previous == null ||
                !string.Equals(
                    previous.sourceRevisionId,
                    WorldBaselinePaths.SourceRevisionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.sanitationPolicyVersion,
                    WorldBaselineSanitationPlan.PolicyVersion,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    previous.semanticFingerprintSha256) ||
                string.IsNullOrWhiteSpace(
                    previous.runtimePayloadFingerprintSha256))
            {
                throw new InvalidDataException(
                    "Previous manifest for the same source revision and " +
                    "sanitation policy lacks deterministic output fingerprints.");
            }

            if (!string.Equals(
                    previous.semanticFingerprintSha256,
                    semanticFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.runtimePayloadFingerprintSha256,
                    runtimePayloadFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Repeated donor baseline generation produced different " +
                    "semantic or payload fingerprints for the same source " +
                    "revision and sanitation policy.");
            }
        }

        public static void AssertSourceLockCoversPlan(
            DonorWorldBaselineSourceManifestData manifest,
            IReadOnlyList<WorldBaselineSanitationEntry> plan)
        {
            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                SourceRecordKey(
                    "rawExtraction",
                    ExtractedSceneRelativePath,
                    "CanonicalExtractedScene"),
                SourceRecordKey(
                    "rawExtraction",
                    PathIdMapRelativePath,
                    "AssetRipperPathIdMap")
            };
            foreach (string file in NormalizedFiles)
            {
                expected.Add(SourceRecordKey(
                    "normalizedData",
                    file,
                    "NormalizedWorldManifest"));
            }
            foreach (string file in ProjectSourceFiles)
            {
                expected.Add(SourceRecordKey(
                    "unityProject",
                    file,
                    "ProjectOwnedFrozenTransferInput"));
            }

            IReadOnlyDictionary<string, string> meshManifest =
                WorldReferenceMeshLibrarySync.ReadResolvedManifest(
                    Path.Combine(
                        configuration.NormalizedDataPath,
                        WorldReferenceMeshLibrarySync.MeshManifestFileName));
            foreach (string guid in plan
                         .Where(entry => entry.IncludeRenderer)
                         .Select(entry => entry.Placement.MeshGuid)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!meshManifest.TryGetValue(
                        guid, out string assetRelativePath))
                {
                    throw new InvalidDataException(
                        "Current sanitation plan references a mesh GUID absent " +
                        "from the frozen mesh manifest: " + guid);
                }

                string relativePath = RawExportAssetsRelativePath + "/" +
                    assetRelativePath.Replace('\\', '/');
                expected.Add(SourceRecordKey(
                    "rawExtraction",
                    relativePath,
                    "WhitelistedMeshAsset"));
                expected.Add(SourceRecordKey(
                    "rawExtraction",
                    relativePath + ".meta",
                    "WhitelistedMeshImporterMetadata"));
            }

            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            foreach (WorldBaselineSanitationEntry entry in plan.Where(entry =>
                         entry.IncludeRenderer &&
                         subsets.ContainsKey(entry.Placement.SourceObjectId)))
            {
                expected.Add(SourceRecordKey(
                    "unityProject",
                    WorldTransferPaths.GeneratedMeshRoot + "/" +
                    entry.Placement.StableId + ".asset",
                    "AuditedDerivedStaticBatchMesh"));
            }

            var actual = (manifest.sourceFiles ??
                          Array.Empty<DonorWorldBaselineSourceFileRecord>())
                .Select(record => SourceRecordKey(
                    record.rootId,
                    record.relativePath,
                    record.role))
                .ToHashSet(StringComparer.Ordinal);
            if (expected.SetEquals(actual))
            {
                return;
            }

            string missing = string.Join(
                ", ",
                expected.Except(actual, StringComparer.Ordinal).Take(5));
            string stale = string.Join(
                ", ",
                actual.Except(expected, StringComparer.Ordinal).Take(5));
            throw new InvalidDataException(
                "Approved donor world source lock does not exactly cover the " +
                "current sanitation plan. Re-baseline the committed source " +
                $"manifest explicitly before generation. Missing=[{missing}] " +
                $"Stale=[{stale}]");
        }

        public static DonorWorldBaselineSourceManifestData Read()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.SourceManifest);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Donor world baseline source manifest is missing.", path);
            }

            DonorWorldBaselineSourceManifestData manifest =
                JsonUtility.FromJson<DonorWorldBaselineSourceManifestData>(
                    File.ReadAllText(path));
            return manifest ?? throw new FormatException(
                "Donor world baseline source manifest could not be parsed.");
        }

        public static void AssertExistingSourceLockUnchanged()
        {
            string manifestPath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.SourceManifest);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    "Normal donor baseline generation requires the committed " +
                    "approved source manifest. Re-baselining must be an " +
                    "explicit reviewed operation.",
                    manifestPath);
            }

            DonorWorldBaselineSourceManifestData manifest = Read();
            if (manifest.schemaVersion != 1 ||
                !string.Equals(
                    manifest.sourceRevisionId,
                    WorldBaselinePaths.SourceRevisionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.extractedSceneSha256,
                    WorldBaselinePaths.SourceSceneSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Existing donor world source lock belongs to a different " +
                    "schema or source revision. Do not re-baseline implicitly.");
            }

            DonorWorldBaselineSourceFileRecord[] lockedRecords =
                manifest.sourceFiles ??
                Array.Empty<DonorWorldBaselineSourceFileRecord>();
            if (lockedRecords.Length == 0)
            {
                throw new InvalidDataException(
                    "Existing donor world source lock contains no source files.");
            }

            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            var actualRecords =
                new List<DonorWorldBaselineSourceFileRecord>(lockedRecords.Length);
            foreach (DonorWorldBaselineSourceFileRecord record in lockedRecords)
            {
                string root;
                switch (record.rootId)
                {
                    case "rawExtraction":
                        root = configuration.RawExtractionPath;
                        break;
                    case "normalizedData":
                        root = configuration.NormalizedDataPath;
                        break;
                    case "unityProject":
                        root = WorldTransferPaths.ProjectRoot;
                        break;
                    default:
                        throw new InvalidDataException(
                            "Existing donor world source lock contains an " +
                            "unknown root ID: " + record.rootId);
                }

                string absolutePath = SafeCombine(root, record.relativePath);
                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException(
                        "Locked donor world source file is missing.",
                        absolutePath);
                }

                var info = new FileInfo(absolutePath);
                string hash = ComputeFileSha256(absolutePath);
                if (info.Length != record.lengthBytes ||
                    !string.Equals(
                        hash, record.sha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Locked donor world source drifted without a revision " +
                        "change: " + record.rootId + "/" + record.relativePath);
                }

                actualRecords.Add(new DonorWorldBaselineSourceFileRecord
                {
                    rootId = record.rootId,
                    relativePath = record.relativePath,
                    lengthBytes = info.Length,
                    sha256 = hash,
                    role = record.role
                });
            }

            string actualFingerprint =
                ComputeFileSetFingerprint(actualRecords);
            if (!string.Equals(
                    actualFingerprint,
                    manifest.sourceFilesFingerprintSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Locked donor world source fingerprint drifted without a " +
                    "revision change.");
            }
        }

        public static string ComputeFileSetFingerprint(
            IEnumerable<DonorWorldBaselineSourceFileRecord> records)
        {
            var builder = new StringBuilder();
            foreach (DonorWorldBaselineSourceFileRecord record in records
                         .OrderBy(value => value.rootId, StringComparer.Ordinal)
                         .ThenBy(value => value.relativePath, StringComparer.Ordinal))
            {
                builder.Append(record.rootId).Append('|')
                    .Append(record.relativePath).Append('|')
                    .Append(record.lengthBytes.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(record.sha256).Append('\n');
            }

            return Sha256Text(builder.ToString());
        }

        public static string ComputeFileSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return ToLowerHex(sha.ComputeHash(stream));
        }

        public static string Sha256Text(string value)
        {
            using SHA256 sha = SHA256.Create();
            return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string ToLowerHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", string.Empty)
                .ToLowerInvariant();

        private static DonorWorldBaselineSanitationCounts BuildCounts(
            IReadOnlyList<WorldBaselineSanitationEntry> entries)
        {
            return new DonorWorldBaselineSanitationCounts
            {
                sourcePlacementCount = 36045,
                geometryEntityCount = 13509,
                eligibleEntityCount = entries.Count,
                sourceActiveSelfEntityCount =
                    entries.Count(entry => entry.SourceActiveSelf),
                sourceInactiveSelfEntityCount =
                    entries.Count(entry => !entry.SourceActiveSelf),
                effectiveActiveEntityCount =
                    entries.Count(entry => entry.EffectiveActive),
                effectiveInactiveEntityCount =
                    entries.Count(entry => !entry.EffectiveActive),
                rendererAcceptedCount =
                    entries.Count(entry => entry.IncludeRenderer),
                activeRendererCount =
                    entries.Count(entry =>
                        entry.IncludeRenderer && entry.EffectiveActive),
                inactiveRendererCount =
                    entries.Count(entry =>
                        entry.IncludeRenderer && !entry.EffectiveActive),
                metadataOnlyCount =
                    entries.Count(entry => !entry.IncludeRenderer),
                metadataOnlyMissingMeshCount =
                    entries.Count(entry => entry.Reason == "NoUsableMeshGuid"),
                metadataOnlySkinnedMeshCount =
                    entries.Count(entry => entry.Reason == "SkinnedMeshRendererExcluded"),
                metadataOnlyCharacterHierarchyCount =
                    entries.Count(entry =>
                        entry.Reason == "CharacterHierarchyExcluded"),
                sourceColliderRecordCount = 5001,
                runtimeColliderCount = 0,
                sourceUnsupportedClassIdCount = 37,
                sourceBoundsReviewCount = 2007,
                partitionCellCount = 49,
                globalEntityCount =
                    entries.Count(entry => entry.Placement.CellId == "global")
            };
        }

        private static void AddFile(
            ICollection<DonorWorldBaselineSourceFileRecord> records,
            string rootId,
            string root,
            string relativePath,
            string role)
        {
            string normalizedRelative = relativePath.Replace('\\', '/');
            string absolutePath = SafeCombine(root, normalizedRelative);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Canonical donor baseline source file is missing.", absolutePath);
            }

            var info = new FileInfo(absolutePath);
            records.Add(new DonorWorldBaselineSourceFileRecord
            {
                rootId = rootId,
                relativePath = normalizedRelative,
                lengthBytes = info.Length,
                sha256 = ComputeFileSha256(absolutePath),
                role = role
            });
        }

        private static void AssertFileHash(
            string root,
            string relativePath,
            string expectedSha256)
        {
            string absolutePath = SafeCombine(root, relativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Canonical frozen donor baseline input is missing.",
                    absolutePath);
            }

            string actualSha256 = ComputeFileSha256(absolutePath);
            if (!string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Canonical frozen donor baseline input hash does not " +
                    "match source revision " +
                    WorldBaselinePaths.SourceRevisionId + ": " +
                    relativePath);
            }
        }

        private static string SourceRecordKey(
            string rootId,
            string relativePath,
            string role) =>
            rootId + "|" + relativePath.Replace('\\', '/') + "|" + role;

        private static string SafeCombine(string root, string relativePath)
        {
            if (Path.IsPathRooted(relativePath) ||
                relativePath.Split('/').Any(part => part is "" or "." or ".."))
            {
                throw new InvalidDataException(
                    "Unsafe donor baseline source-relative path: " + relativePath);
            }

            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Donor baseline source path escapes its configured root: " +
                    relativePath);
            }

            return candidate;
        }
    }
}
