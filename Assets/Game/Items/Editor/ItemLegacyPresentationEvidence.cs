using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.LegacyImport.Editor.Configuration;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.Items.Editor
{
    internal sealed class ItemLegacyPresentationPlan
    {
        public ItemLegacyPresentationPlan(
            ItemDefinitionCatalog definitions,
            string entityTableSha256,
            string donorRevision,
            IReadOnlyList<ItemLegacyPresentationPlanEntry> entries)
        {
            Definitions = definitions;
            EntityTableSha256 = entityTableSha256;
            DonorRevision = donorRevision;
            Entries = entries;
        }

        public ItemDefinitionCatalog Definitions { get; }
        public string EntityTableSha256 { get; }
        public string DonorRevision { get; }
        public IReadOnlyList<ItemLegacyPresentationPlanEntry> Entries { get; }

        public IReadOnlyCollection<string> MeshGuids => Entries
            .SelectMany(entry => entry.Geometry)
            .Select(record => record.MeshGuid)
            .Where(ItemLegacyPresentationEvidence.IsUsableMeshGuid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyCollection<string> WorldMeshGuids => Entries
            .Where(entry => !entry.Source.IsDirectDonorPrefab)
            .SelectMany(entry => entry.Geometry)
            .Select(record => record.MeshGuid)
            .Where(ItemLegacyPresentationEvidence.IsUsableMeshGuid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    internal sealed class ItemLegacyPresentationPlanEntry
    {
        public ItemLegacyPresentationPlanEntry(
            ItemDefinitionRecord definition,
            ItemLegacyPresentationSource source,
            ItemLegacyEvidenceRecord sourceRoot,
            IReadOnlyList<ItemLegacyEvidenceRecord> geometry)
        {
            Definition = definition;
            Source = source;
            SourceRoot = sourceRoot;
            Geometry = geometry;
        }

        public ItemDefinitionRecord Definition { get; }
        public ItemLegacyPresentationSource Source { get; }
        public ItemLegacyEvidenceRecord SourceRoot { get; }
        public IReadOnlyList<ItemLegacyEvidenceRecord> Geometry { get; }
    }

    internal sealed class ItemLegacyEvidenceRecord
    {
        public string StableId { get; set; } = string.Empty;
        public string ParentStableId { get; set; } = string.Empty;
        public string HierarchyPath { get; set; } = string.Empty;
        public Vector3 SourcePosition { get; set; }
        public Quaternion SourceRotation { get; set; }
        public Vector3 SourceScale { get; set; }
        public string MeshGuid { get; set; } = string.Empty;
        public IReadOnlyList<string> MaterialGuids { get; set; } =
            Array.Empty<string>();
        public string SourceSha256 { get; set; } = string.Empty;
        public string ComponentClassIds { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    internal static class ItemLegacyPresentationEvidence
    {
        private static readonly CultureInfo Invariant =
            CultureInfo.InvariantCulture;
        internal const string BuiltInMeshGuid =
            "0000000000000000e000000000000000";
        private static readonly HashSet<string> ReviewedBuiltInVisualPaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "ITEMS/parts magazine(itemx)/mesh",
            };

        public static ItemLegacyPresentationPlan CreatePlan()
        {
            string absoluteTablePath = ToAbsoluteProjectPath(
                ItemLegacyPresentationPaths.EntityTableAssetPath);
            if (!File.Exists(absoluteTablePath))
            {
                throw new FileNotFoundException(
                    "Tracked M04A1 entity table is missing.",
                    absoluteTablePath);
            }

            string tableHash = ComputeSha256(absoluteTablePath);
            if (!string.Equals(
                    tableHash,
                    ItemLegacyPresentationPaths.ExpectedEntityTableSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "M04A1_WorldEntities.csv hash differs from the reviewed " +
                    "09B source lock. Refuse to normalize unreviewed evidence.");
            }

            ItemDefinitionCatalog definitions = FindDefinitionCatalog();
            IReadOnlyList<string> definitionFailures =
                definitions.ValidateConfiguration();
            if (definitionFailures.Count > 0)
            {
                throw new InvalidDataException(
                    "Item definition catalog is invalid: " +
                    string.Join(" | ", definitionFailures));
            }

            List<ItemLegacyEvidenceRecord> records = ReadRecords(
                absoluteTablePath);
            var definitionsByFeature = definitions.Definitions.ToDictionary(
                definition => definition.FeatureId,
                StringComparer.Ordinal);
            var seenFeatureVariants = new HashSet<string>(
                StringComparer.Ordinal);
            var seenRoots = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<ItemLegacyPresentationPlanEntry>();

            foreach (ItemLegacyPresentationSource source in
                     ItemLegacyPresentationPaths.Sources)
            {
                string featureVariantIdentity = source.FeatureId + "\n" +
                                                source.VariantIndex;
                string sourceIdentity = source.IsDirectDonorPrefab
                    ? source.DonorPrefabRelativePath + "\n" +
                      featureVariantIdentity
                    : source.SourceHierarchyRoot;
                if (!seenFeatureVariants.Add(featureVariantIdentity) ||
                    !seenRoots.Add(sourceIdentity))
                {
                    throw new InvalidDataException(
                        "Duplicate 09B item presentation allowlist identity: " +
                        source.FeatureId);
                }

                if (!definitionsByFeature.TryGetValue(
                        source.FeatureId,
                        out ItemDefinitionRecord definition))
                {
                    throw new InvalidDataException(
                        "The item definition catalog has no reviewed feature " +
                        source.FeatureId + ".");
                }

                if (source.VariantIndex < -1 ||
                    source.VariantIndex >= definition.ToolVariants.Count &&
                    source.VariantIndex >= 0)
                {
                    throw new InvalidDataException(
                        $"Reviewed item presentation variant " +
                        $"{source.VariantIndex} is outside '{source.FeatureId}' " +
                        "definition variants.");
                }

                if (source.IsDirectDonorPrefab)
                {
                    entries.Add(CreateDirectDonorPrefabEntry(
                        definition,
                        source));
                    continue;
                }

                ItemLegacyEvidenceRecord[] roots = records
                    .Where(record => string.Equals(
                        record.HierarchyPath,
                        source.SourceHierarchyRoot,
                        StringComparison.Ordinal))
                    .OrderBy(record => record.StableId, StringComparer.Ordinal)
                    .ToArray();
                if (roots.Length == 0)
                {
                    throw new InvalidDataException(
                        $"Reviewed source root '{source.SourceHierarchyRoot}' " +
                        "did not resolve to frozen evidence.");
                }

                ItemLegacyEvidenceRecord sourceRoot = roots[0];
                string descendantPrefix = source.SourceHierarchyRoot + "/";
                ItemLegacyEvidenceRecord[] geometryCandidates = records
                    .Where(record =>
                        IsRenderableEvidence(record) &&
                        HasRenderableTransform(record) &&
                        (string.Equals(
                             record.HierarchyPath,
                             source.SourceHierarchyRoot,
                             StringComparison.Ordinal) ||
                         record.HierarchyPath.StartsWith(
                             descendantPrefix,
                             StringComparison.Ordinal)))
                    .ToArray();

                if (source.PreferUnpackedGeometry)
                {
                    geometryCandidates = geometryCandidates
                        .Where(record => !record.HierarchyPath.EndsWith(
                            "/_gfx",
                            StringComparison.Ordinal))
                        .ToArray();

                    ItemLegacyEvidenceRecord[] activeParts =
                        geometryCandidates
                            .Where(record => record.Active)
                            .ToArray();
                    if (activeParts.Length > 0)
                    {
                        geometryCandidates = activeParts;
                    }
                }
                else
                {
                    geometryCandidates = geometryCandidates
                        .Where(record =>
                            record.Active || source.IncludeInactiveGeometry)
                        .ToArray();
                }

                IReadOnlyList<ItemLegacyEvidenceRecord> geometry =
                    geometryCandidates
                    .GroupBy(
                        record => record.HierarchyPath + "\n" + record.MeshGuid,
                        StringComparer.Ordinal)
                    .Select(group => group
                        .OrderBy(record =>
                            (record.SourcePosition - sourceRoot.SourcePosition)
                            .sqrMagnitude)
                        .ThenBy(record => record.StableId, StringComparer.Ordinal)
                        .First())
                    .OrderBy(record => record.HierarchyPath, StringComparer.Ordinal)
                    .ThenBy(record => record.StableId, StringComparer.Ordinal)
                    .ToArray();
                if (geometry.Count == 0)
                {
                    throw new InvalidDataException(
                        "Reviewed item source contains no active visual mesh: " +
                        source.SourceHierarchyRoot);
                }

                ValidateSourceRevision(sourceRoot, source.SourceHierarchyRoot);
                ValidateTransform(sourceRoot);
                foreach (ItemLegacyEvidenceRecord record in geometry)
                {
                    ValidateSourceRevision(record, source.SourceHierarchyRoot);
                    ValidateTransform(record);
                }

                entries.Add(new ItemLegacyPresentationPlanEntry(
                    definition,
                    source,
                    sourceRoot,
                    geometry));
            }

            return new ItemLegacyPresentationPlan(
                definitions,
                tableHash,
                ItemLegacyPresentationPaths.ExpectedDonorRevision,
                entries);
        }

        private static ItemLegacyPresentationPlanEntry
            CreateDirectDonorPrefabEntry(
                ItemDefinitionRecord definition,
                ItemLegacyPresentationSource source)
        {
            if (source.DirectMeshes.Length == 0 ||
                !IsSha256(source.DonorPrefabSha256))
            {
                throw new InvalidDataException(
                    "Direct donor item source is incomplete: " +
                    source.FeatureId);
            }

            string prefabPath = ResolveDirectSourcePath(
                source.DonorPrefabRelativePath);
            RequireHash(
                prefabPath,
                source.DonorPrefabSha256,
                source.DonorPrefabRelativePath);

            var sourceRoot = new ItemLegacyEvidenceRecord
            {
                StableId = "prefab-" +
                           source.DonorPrefabSha256.Substring(0, 24),
                ParentStableId = string.Empty,
                HierarchyPath = "DONOR_PREFAB/" +
                                source.DonorPrefabRelativePath,
                SourcePosition = Vector3.zero,
                SourceRotation = Quaternion.identity,
                SourceScale = Vector3.one,
                SourceSha256 = source.DonorPrefabSha256,
                Active = true,
            };

            var geometry = new List<ItemLegacyEvidenceRecord>(
                source.DirectMeshes.Length);
            for (int index = 0; index < source.DirectMeshes.Length; index++)
            {
                ItemLegacyPresentationMeshSource mesh =
                    source.DirectMeshes[index];
                if (!IsUsableMeshGuid(mesh.MeshGuid) ||
                    !IsSha256(mesh.Sha256) ||
                    mesh.MaterialGuids.Length == 0 ||
                    mesh.MaterialGuids.Any(value => !IsGuid(value)))
                {
                    throw new InvalidDataException(
                        "Direct donor mesh identity is invalid for " +
                        source.FeatureId + ".");
                }

                RequireHash(
                    ResolveDirectSourcePath(mesh.AssetRelativePath),
                    mesh.Sha256,
                    mesh.AssetRelativePath);
                var record = new ItemLegacyEvidenceRecord
                {
                    StableId = "prefab-mesh-" + mesh.MeshGuid,
                    ParentStableId = sourceRoot.StableId,
                    HierarchyPath = sourceRoot.HierarchyPath +
                                    "/mesh-" + index,
                    SourcePosition = mesh.LocalPosition,
                    SourceRotation = mesh.LocalRotation,
                    SourceScale = mesh.LocalScale,
                    MeshGuid = mesh.MeshGuid,
                    MaterialGuids = mesh.MaterialGuids,
                    SourceSha256 = mesh.Sha256,
                    ComponentClassIds = "23;33",
                    Active = true,
                };
                ValidateTransform(record);
                geometry.Add(record);
            }

            return new ItemLegacyPresentationPlanEntry(
                definition,
                source,
                sourceRoot,
                geometry);
        }

        internal static string ResolveDirectSourcePath(string relativePath)
        {
            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(
                    "Config/DonorPaths.local.json");
            string assetsRoot = Path.GetFullPath(Path.Combine(
                configuration.DonorStagingDirectory,
                ItemLegacyPresentationPaths.DonorExportedAssetsRelativePath
                    .Replace('/', Path.DirectorySeparatorChar)));
            string normalizedRoot = assetsRoot.TrimEnd(
                Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                assetsRoot,
                (relativePath ?? string.Empty).Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Direct donor item source escapes staging: " +
                    relativePath);
            }

            return candidate;
        }

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Locked direct donor item source is missing: " + label,
                    path);
            }

            string actual = ComputeSha256(path);
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked direct donor item source '{label}' hash " +
                    $"mismatch. Expected {expected}, got {actual}.");
            }
        }

        private static bool IsGuid(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length == 32 &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static bool IsSha256(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length == 64 &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        public static bool IsUsableMeshGuid(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length == 32 &&
            !string.Equals(value, BuiltInMeshGuid, StringComparison.Ordinal) &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static bool IsRenderableEvidence(
            ItemLegacyEvidenceRecord record) =>
            IsUsableMeshGuid(record.MeshGuid) ||
            string.Equals(
                record.MeshGuid,
                BuiltInMeshGuid,
                StringComparison.Ordinal) &&
            ReviewedBuiltInVisualPaths.Contains(record.HierarchyPath);

        public static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                "Unity project root could not be resolved.");
            string normalizedRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                projectRoot,
                projectRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Item presentation path escapes the Unity project: " +
                    projectRelativePath);
            }

            return candidate;
        }

        private static ItemDefinitionCatalog FindDefinitionCatalog()
        {
            string[] paths = AssetDatabase.FindAssets(
                    "t:ItemDefinitionCatalog")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected exactly one ItemDefinitionCatalog, found " +
                    paths.Length + ".");
            }

            return AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                       paths[0]) ??
                   throw new InvalidDataException(
                       "ItemDefinitionCatalog could not be loaded: " +
                       paths[0]);
        }

        private static List<ItemLegacyEvidenceRecord> ReadRecords(string path)
        {
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            var indices = headers
                .Select((name, index) => (name, index))
                .ToDictionary(pair => pair.name, pair => pair.index,
                    StringComparer.Ordinal);
            RequireColumns(
                indices,
                "StableId",
                "ParentStableId",
                "HierarchyPath",
                "SourcePositionX",
                "SourcePositionY",
                "SourcePositionZ",
                "SourceRotationX",
                "SourceRotationY",
                "SourceRotationZ",
                "SourceRotationW",
                "SourceScaleX",
                "SourceScaleY",
                "SourceScaleZ",
                "MeshGuid",
                "MaterialGuids",
                "SourceSha256",
                "ComponentClassIds",
                "Active");

            var records = new List<ItemLegacyEvidenceRecord>();
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        $"M04A1 entity line {lineNumber} has " +
                        $"{values.Count} values; expected {headers.Count}.");
                }

                string Get(string name) => values[indices[name]];
                float Number(string name) => float.Parse(
                    Get(name),
                    NumberStyles.Float,
                    Invariant);
                Quaternion rotation = Normalize(new Quaternion(
                    Number("SourceRotationX"),
                    Number("SourceRotationY"),
                    Number("SourceRotationZ"),
                    Number("SourceRotationW")), lineNumber);

                records.Add(new ItemLegacyEvidenceRecord
                {
                    StableId = Get("StableId"),
                    ParentStableId = Get("ParentStableId"),
                    HierarchyPath = Get("HierarchyPath"),
                    SourcePosition = new Vector3(
                        Number("SourcePositionX"),
                        Number("SourcePositionY"),
                        Number("SourcePositionZ")),
                    SourceRotation = rotation,
                    SourceScale = new Vector3(
                        Number("SourceScaleX"),
                        Number("SourceScaleY"),
                        Number("SourceScaleZ")),
                    MeshGuid = Get("MeshGuid").ToLowerInvariant(),
                    MaterialGuids = Get("MaterialGuids")
                        .Split(
                            new[] { ';', '|', ',' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => value.Trim().ToLowerInvariant())
                        .Where(value => value.Length == 32)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    SourceSha256 = Get("SourceSha256").ToLowerInvariant(),
                    ComponentClassIds = Get("ComponentClassIds"),
                    Active = string.Equals(
                        Get("Active"),
                        "1",
                        StringComparison.Ordinal),
                });
            }

            return records;
        }

        private static void ValidateSourceRevision(
            ItemLegacyEvidenceRecord record,
            string sourceRoot)
        {
            if (!string.Equals(
                    record.SourceSha256,
                    ItemLegacyPresentationPaths.ExpectedDonorRevision,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Item source revision differs from the reviewed 04A1 " +
                    "baseline: " + sourceRoot);
            }
        }

        private static void ValidateTransform(ItemLegacyEvidenceRecord record)
        {
            if (!HasRenderableTransform(record))
            {
                throw new InvalidDataException(
                    "Item evidence has an invalid source transform: " +
                    record.StableId);
            }
        }

        private static bool HasRenderableTransform(
            ItemLegacyEvidenceRecord record) =>
            record != null &&
            IsFinite(record.SourcePosition) &&
            IsFinite(record.SourceRotation) &&
            IsFinite(record.SourceScale) &&
            Mathf.Abs(record.SourceScale.x) >= 0.000001f &&
            Mathf.Abs(record.SourceScale.y) >= 0.000001f &&
            Mathf.Abs(record.SourceScale.z) >= 0.000001f;

        private static void RequireColumns(
            IReadOnlyDictionary<string, int> indices,
            params string[] required)
        {
            foreach (string name in required)
            {
                if (!indices.ContainsKey(name))
                {
                    throw new FormatException(
                        "M04A1 entity table lacks required column: " + name);
                }
            }
        }

        private static Quaternion Normalize(
            Quaternion value,
            int lineNumber)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (!float.IsFinite(magnitude) || magnitude < 0.000001f)
            {
                throw new FormatException(
                    $"M04A1 entity line {lineNumber} has invalid rotation.");
            }

            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}
