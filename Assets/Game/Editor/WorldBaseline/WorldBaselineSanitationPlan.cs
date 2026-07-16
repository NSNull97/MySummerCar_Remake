using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;

namespace MSC.Editor.WorldBaseline
{
    public sealed class WorldBaselineSanitationEntry
    {
        public WorldBaselineSanitationEntry(
            WorldEntityPlacement placement,
            string sourceParentStableId,
            string[] sourceMaterialGuids,
            bool effectiveActive,
            int[] componentClassIds,
            bool includeRenderer,
            string disposition,
            string reason)
        {
            Placement = placement;
            SourceParentStableId = sourceParentStableId;
            SourceMaterialGuids = sourceMaterialGuids;
            EffectiveActive = effectiveActive;
            ComponentClassIds = componentClassIds;
            IncludeRenderer = includeRenderer;
            Disposition = disposition;
            Reason = reason;
        }

        public WorldEntityPlacement Placement { get; }
        public string SourceParentStableId { get; }
        public string[] SourceMaterialGuids { get; }
        public string SourceMaterialGuidsText =>
            string.Join(";", SourceMaterialGuids);
        public bool SourceActiveSelf => Placement.Active;
        public bool EffectiveActive { get; }
        public int[] ComponentClassIds { get; }
        public bool IncludeRenderer { get; }
        public string Disposition { get; }
        public string Reason { get; }
        public string ComponentClassIdsText => string.Join(";", ComponentClassIds);
    }

    public static class WorldBaselineSanitationPlan
    {
        public const string PolicyVersion = "06B1.4";
        public const int ExpectedSourceEntityCount = 3842;
        public const int ExpectedRendererEntityCount = 2605;
        public const int ExpectedMetadataOnlyEntityCount = 1237;
        public const int ExpectedEffectiveActiveEntityCount = 2777;
        public const int ExpectedEffectiveInactiveEntityCount = 1065;
        public const int ExpectedActiveRendererEntityCount = 2123;
        public const int ExpectedInactiveRendererEntityCount = 482;
        public const int ExpectedActiveSelfUnderInactiveAncestorCount = 850;
        public const int ExpectedCharacterHierarchyEntityCount = 117;

        private const int TransformClassId = 4;
        private const int MeshRendererClassId = 23;
        private const int MeshFilterClassId = 33;
        private const int SkinnedMeshRendererClassId = 137;

        public static IReadOnlyList<WorldBaselineSanitationEntry> Load()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldTransferPaths.EntityTableAssetPath);
            string csv = File.ReadAllText(path);
            IReadOnlyList<WorldEntityPlacement> placements =
                WorldEntityTable.Parse(csv);
            Dictionary<string, int[]> components = ParseComponentClassIds(csv);
            Dictionary<string, string[]> materials =
                ParseSourceMaterialGuids(csv);
            Dictionary<string, SourceActivationRecord> activation =
                ParseSourceActivation();
            var effectiveActivation =
                new Dictionary<string, bool>(StringComparer.Ordinal);

            WorldBaselineSanitationEntry[] result = placements
                .Where(placement => placement.ReferenceWorldEligible)
                .OrderBy(placement => placement.StableId, StringComparer.Ordinal)
                .Select(placement =>
                {
                    if (!components.TryGetValue(placement.StableId, out int[] classIds))
                    {
                        throw new InvalidDataException(
                            "Component-class metadata is missing for " + placement.StableId);
                    }
                    if (!activation.TryGetValue(
                            placement.StableId,
                            out SourceActivationRecord activationRecord))
                    {
                        throw new InvalidDataException(
                            "Full source activation metadata is missing for " +
                            placement.StableId);
                    }
                    if (!materials.TryGetValue(
                            placement.StableId,
                            out string[] sourceMaterialGuids))
                    {
                        throw new InvalidDataException(
                            "Source material-slot metadata is missing for " +
                            placement.StableId);
                    }
                    if (activationRecord.ActiveSelf != placement.Active)
                    {
                        throw new InvalidDataException(
                            "Project entity Active value drifted from the frozen " +
                            "full placement inventory for " + placement.StableId);
                    }

                    string reason;
                    bool includeRenderer;
                    if (!WorldReferenceMeshLibrarySync.IsUsableMeshGuid(
                            placement.MeshGuid))
                    {
                        includeRenderer = false;
                        reason = "NoUsableMeshGuid";
                    }
                    else if (classIds.Contains(SkinnedMeshRendererClassId))
                    {
                        includeRenderer = false;
                        reason = "SkinnedMeshRendererExcluded";
                    }
                    else if (placement.HierarchyPath.IndexOf(
                                 "/skeleton/",
                                 StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        includeRenderer = false;
                        reason = "CharacterHierarchyExcluded";
                    }
                    else if (!classIds.Contains(MeshRendererClassId) ||
                             !classIds.Contains(MeshFilterClassId))
                    {
                        includeRenderer = false;
                        reason = "MissingStaticMeshRendererContract";
                    }
                    else
                    {
                        includeRenderer = true;
                        bool effectiveActive = ResolveEffectiveActive(
                            placement.StableId,
                            activation,
                            effectiveActivation,
                            new HashSet<string>(StringComparer.Ordinal));
                        string baseReason =
                            classIds.All(IsTransferredSourceClass)
                                ? "WhitelistedStaticMesh"
                                : "WhitelistedStaticMeshWithSourceComponentsStripped";
                        reason = effectiveActive
                            ? baseReason
                            : baseReason + "InactiveInDonorHierarchy";
                    }

                    bool isEffectivelyActive = ResolveEffectiveActive(
                        placement.StableId,
                        activation,
                        effectiveActivation,
                        new HashSet<string>(StringComparer.Ordinal));
                    return new WorldBaselineSanitationEntry(
                        placement,
                        activationRecord.ParentStableId,
                        sourceMaterialGuids,
                        isEffectivelyActive,
                        classIds,
                        includeRenderer,
                        includeRenderer ? "RendererAccepted" : "MetadataOnly",
                        reason);
                })
                .ToArray();

            int rendererCount = result.Count(entry => entry.IncludeRenderer);
            if (result.Length != ExpectedSourceEntityCount ||
                rendererCount != ExpectedRendererEntityCount ||
                result.Length - rendererCount != ExpectedMetadataOnlyEntityCount ||
                result.Count(entry => entry.EffectiveActive) !=
                    ExpectedEffectiveActiveEntityCount ||
                result.Count(entry => !entry.EffectiveActive) !=
                    ExpectedEffectiveInactiveEntityCount ||
                result.Count(entry =>
                    entry.IncludeRenderer && entry.EffectiveActive) !=
                    ExpectedActiveRendererEntityCount ||
                result.Count(entry =>
                    entry.IncludeRenderer && !entry.EffectiveActive) !=
                    ExpectedInactiveRendererEntityCount ||
                result.Count(entry =>
                    entry.SourceActiveSelf && !entry.EffectiveActive) !=
                    ExpectedActiveSelfUnderInactiveAncestorCount ||
                result.Count(entry =>
                    entry.Reason == "CharacterHierarchyExcluded") !=
                    ExpectedCharacterHierarchyEntityCount)
            {
                throw new InvalidDataException(
                    $"Sanitation-plan count drift: entities={result.Length}, " +
                    $"renderers={rendererCount}, metadataOnly={result.Length - rendererCount}.");
            }

            return result;
        }

        private static Dictionary<string, string[]>
            ParseSourceMaterialGuids(string csv)
        {
            using var reader = new StringReader(csv);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int stableIdIndex = headers.IndexOf("StableId");
            int materialIndex = headers.IndexOf("MaterialGuids");
            if (stableIdIndex < 0 || materialIndex < 0)
            {
                throw new FormatException(
                    "World entity table lacks StableId or MaterialGuids.");
            }

            var result = new Dictionary<string, string[]>(
                StringComparer.Ordinal);
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
                        $"World entity table line {lineNumber} has an invalid column count.");
                }

                string[] materialGuids = values[materialIndex]
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .ToArray();
                result.Add(values[stableIdIndex], materialGuids);
            }

            return result;
        }

        private static bool IsTransferredSourceClass(int classId) =>
            classId is TransformClassId or MeshRendererClassId or MeshFilterClassId;

        private static Dictionary<string, int[]> ParseComponentClassIds(string csv)
        {
            using var reader = new StringReader(csv);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int stableIdIndex = headers.IndexOf("StableId");
            int componentIndex = headers.IndexOf("ComponentClassIds");
            if (stableIdIndex < 0 || componentIndex < 0)
            {
                throw new FormatException(
                    "World entity table lacks StableId or ComponentClassIds.");
            }

            var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
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
                        $"World entity table line {lineNumber} has an invalid column count.");
                }

                int[] classIds = values[componentIndex]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
                result.Add(values[stableIdIndex], classIds);
            }

            return result;
        }

        private static Dictionary<string, SourceActivationRecord>
            ParseSourceActivation()
        {
            string path = Path.Combine(
                WorldTransferEditorConfiguration.Load().NormalizedDataPath,
                "WorldObjectPlacements.csv");
            using var reader = new StringReader(File.ReadAllText(path));
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int stableIdIndex = headers.IndexOf("StableId");
            int parentStableIdIndex = headers.IndexOf("ParentStableId");
            int activeIndex = headers.IndexOf("Active");
            if (stableIdIndex < 0 ||
                parentStableIdIndex < 0 ||
                activeIndex < 0)
            {
                throw new FormatException(
                    "Full world placement inventory lacks StableId, " +
                    "ParentStableId or Active.");
            }

            var result = new Dictionary<string, SourceActivationRecord>(
                StringComparer.Ordinal);
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
                        $"Full world placement inventory line {lineNumber} " +
                        "has an invalid column count.");
                }

                result.Add(
                    values[stableIdIndex],
                    new SourceActivationRecord(
                        values[parentStableIdIndex],
                        values[activeIndex] == "1"));
            }

            if (result.Count != 36045)
            {
                throw new InvalidDataException(
                    "Full world placement inventory count drifted: " +
                    result.Count);
            }

            foreach (KeyValuePair<string, SourceActivationRecord> record in result)
            {
                if (!string.IsNullOrEmpty(record.Value.ParentStableId) &&
                    !result.ContainsKey(record.Value.ParentStableId))
                {
                    throw new InvalidDataException(
                        "Full world placement inventory has an unresolved " +
                        "parent for " + record.Key);
                }
            }

            return result;
        }

        private static bool ResolveEffectiveActive(
            string stableId,
            IReadOnlyDictionary<string, SourceActivationRecord> records,
            IDictionary<string, bool> resolved,
            ISet<string> visiting)
        {
            if (resolved.TryGetValue(stableId, out bool value))
            {
                return value;
            }
            if (!records.TryGetValue(
                    stableId,
                    out SourceActivationRecord record))
            {
                throw new InvalidDataException(
                    "Source activation record is missing for " + stableId);
            }
            if (!visiting.Add(stableId))
            {
                throw new InvalidDataException(
                    "Source activation hierarchy contains a cycle at " +
                    stableId);
            }

            bool effective = record.ActiveSelf &&
                (string.IsNullOrEmpty(record.ParentStableId) ||
                 ResolveEffectiveActive(
                     record.ParentStableId,
                     records,
                     resolved,
                     visiting));
            visiting.Remove(stableId);
            resolved[stableId] = effective;
            return effective;
        }

        private readonly struct SourceActivationRecord
        {
            public SourceActivationRecord(
                string parentStableId,
                bool activeSelf)
            {
                ParentStableId = parentStableId;
                ActiveSelf = activeSelf;
            }

            public string ParentStableId { get; }
            public bool ActiveSelf { get; }
        }
    }
}
