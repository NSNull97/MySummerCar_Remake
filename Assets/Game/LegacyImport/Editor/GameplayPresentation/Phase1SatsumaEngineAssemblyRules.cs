using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Applies the reviewed E1 engine access predicates without rebuilding the
    /// donor-derived Satsuma presentation or changing its assembly schema.
    /// </summary>
    internal static class Phase1SatsumaEngineAssemblyRules
    {
        private const string RulesVersion = "E1";
        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string EngineBlockPartId =
            "vehicle.satsuma.part.engine-block";

        private static readonly EngineAccessRule[] Rules =
        {
            new EngineAccessRule(
                "crankshaft",
                new[]
                {
                    MountId("oilpan"),
                    MountId("main-bearing1"),
                    MountId("main-bearing2"),
                    MountId("main-bearing3"),
                    MountId("timing-cover"),
                },
                new[]
                {
                    MountId("main-bearing1"),
                    MountId("main-bearing2"),
                    MountId("main-bearing3"),
                    MountId("timing-cover"),
                }),
            new EngineAccessRule(
                "main-bearing1",
                new[] { MountId("oilpan") },
                new[] { MountId("oilpan") }),
            new EngineAccessRule(
                "main-bearing2",
                new[] { MountId("oilpan") },
                new[] { MountId("oilpan") }),
            new EngineAccessRule(
                "main-bearing3",
                new[] { MountId("oilpan") },
                new[] { MountId("oilpan") }),
            new EngineAccessRule(
                "piston1",
                new[] { MountId("cylinder-head") },
                new[] { MountId("cylinder-head") }),
            new EngineAccessRule(
                "piston2",
                new[] { MountId("cylinder-head") },
                new[] { MountId("cylinder-head") }),
            new EngineAccessRule(
                "piston3",
                new[] { MountId("cylinder-head") },
                new[] { MountId("cylinder-head") }),
            new EngineAccessRule(
                "piston4",
                new[] { MountId("cylinder-head") },
                new[] { MountId("cylinder-head") }),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Access Rules")]
        public static void RefreshEngineAccessRulesBatch()
        {
            ValidateManifestIdentity();

            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                MountPointDefinition[] changed = RefreshEngineAccessRules(contents);
                foreach (MountPointDefinition definition in changed)
                {
                    AssetDatabase.SaveAssetIfDirty(definition);
                }

                Debug.Log(
                    "PHASE1_SATSUMA_ENGINE_ACCESS_RULES_REFRESH_OK rulesVersion=" +
                    RulesVersion + " changedMountDefinitions=" + changed.Length +
                    " fullRebuild=false prefabUnchanged=true manifestUnchanged=true" +
                    " sourceSceneSha256=" +
                    Phase1SatsumaBaselineBuilder.LockedSceneSha256);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static MountPointDefinition[] RefreshEngineAccessRules(
            GameObject prefabContents)
        {
            VehicleAssemblyController assembly = prefabContents != null
                ? prefabContents.GetComponent<VehicleAssemblyController>()
                : null;
            MountPointAuthoring[] mountPoints = assembly != null
                ? assembly.MountPoints
                : Array.Empty<MountPointAuthoring>();
            if (assembly == null || mountPoints.Any(value => value == null) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(mountPoints.Select(value => value.MountId)))
            {
                throw new InvalidDataException(
                    "Engine access refresh requires the reviewed base or additive Satsuma mount roster.");
            }

            foreach (EngineAccessRule rule in Rules)
            {
                MountPointAuthoring authoring = mountPoints.SingleOrDefault(value =>
                    string.Equals(value.MountId, rule.MountDefinitionId,
                        StringComparison.Ordinal));
                MountPointDefinition definition = authoring != null
                    ? authoring.Definition
                    : null;
                string expectedPath = GeneratedRoot + "/MountDefinitions/" +
                                      rule.MountDefinitionId + ".asset";
                if (definition == null ||
                    !string.Equals(definition.DefinitionId,
                        rule.MountDefinitionId, StringComparison.Ordinal) ||
                    !string.Equals(definition.OwnerPartDefinitionId,
                        EngineBlockPartId, StringComparison.Ordinal) ||
                    !definition.AcceptedPartDefinitionIds.SequenceEqual(
                        new[] { rule.AcceptedPartDefinitionId }) ||
                    !string.Equals(AssetDatabase.GetAssetPath(definition),
                        expectedPath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Unexpected engine definition binding during scoped refresh: " +
                        rule.MountDefinitionId + ".");
                }
            }

            return ApplyEngineAccessRules(
                mountPoints.Select(value => value.Definition).ToArray());
        }

        internal static MountPointDefinition[] ApplyEngineAccessRules(
            IReadOnlyList<MountPointDefinition> definitions)
        {
            MountPointDefinition[] all = definitions?.ToArray() ??
                                         Array.Empty<MountPointDefinition>();
            if (all.Any(value => value == null) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(all.Select(value => value.DefinitionId)))
            {
                throw new InvalidDataException(
                    "E1 engine access rules require the reviewed base or additive mount identities.");
            }

            var plans = new List<EngineAccessPlan>(Rules.Length);
            var availableMountIds = new HashSet<string>(
                all.Select(value => value.DefinitionId), StringComparer.Ordinal);
            foreach (EngineAccessRule rule in Rules)
            {
                foreach (string blocker in rule.InstallationBlockers.Concat(rule.RemovalBlockers))
                {
                    if (!availableMountIds.Contains(blocker))
                    {
                        throw new InvalidDataException(
                            "Missing E1 engine access blocker: " + blocker + ".");
                    }
                }

                MountPointDefinition definition = all.SingleOrDefault(value =>
                    string.Equals(value.DefinitionId, rule.MountDefinitionId,
                        StringComparison.Ordinal));
                if (definition == null ||
                    !string.Equals(definition.OwnerPartDefinitionId,
                        EngineBlockPartId, StringComparison.Ordinal) ||
                    !definition.AcceptedPartDefinitionIds.SequenceEqual(
                        new[] { rule.AcceptedPartDefinitionId }))
                {
                    throw new InvalidDataException(
                        "Missing or drifted E1 engine mount definition: " +
                        rule.MountDefinitionId + ".");
                }

                string[] installationBlockers = AppendUnique(
                    definition.BlockedWhileOccupiedMountIds,
                    rule.InstallationBlockers);
                string[] removalBlockers = AppendUnique(
                    definition.RemovalBlockedWhileOccupiedMountIds,
                    rule.RemovalBlockers);
                ValidateOldOrCurrentShape(definition, rule);
                plans.Add(new EngineAccessPlan(
                    definition,
                    installationBlockers,
                    removalBlockers));
            }

            var changed = new List<MountPointDefinition>(Rules.Length);
            foreach (EngineAccessPlan plan in plans)
            {
                MountPointDefinition definition = plan.Definition;
                if (definition.BlockedWhileOccupiedMountIds.SequenceEqual(
                        plan.InstallationBlockers) &&
                    definition.RemovalBlockedWhileOccupiedMountIds.SequenceEqual(
                        plan.RemovalBlockers))
                {
                    continue;
                }

                definition.ConfigureSequence(
                    definition.RequiredOccupiedMountIds,
                    definition.RequiredAnyOccupiedMountIds,
                    plan.InstallationBlockers);
                definition.ConfigureRemovalBlockers(plan.RemovalBlockers);
                EditorUtility.SetDirty(definition);
                changed.Add(definition);
            }

            return changed.ToArray();
        }

        private static void ValidateManifestIdentity()
        {
            string manifestPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                Phase1SatsumaBaselineBuilder.ManifestPath.Replace(
                    '/', Path.DirectorySeparatorChar));
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    "The canonical Satsuma manifest is missing.", manifestPath);
            }

            ManifestIdentity manifest = JsonUtility.FromJson<ManifestIdentity>(
                File.ReadAllText(manifestPath));
            bool supportedBuilder = manifest != null &&
                (string.Equals(manifest.builderVersion, "11A-V1d.64",
                     StringComparison.Ordinal) ||
                 string.Equals(manifest.builderVersion, "11A-V1d.65",
                     StringComparison.Ordinal) ||
                 string.Equals(manifest.builderVersion,
                     Phase1SatsumaBaselineBuilder.BuilderVersion,
                     StringComparison.Ordinal));
            if (!supportedBuilder ||
                !string.Equals(manifest.sourceSceneSha256,
                    Phase1SatsumaBaselineBuilder.LockedSceneSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.stableVehicleId,
                    Phase1SatsumaBaselineBuilder.StableVehicleId,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.runtimePrefabPath,
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.featureId, "P1.CAR.001",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Engine access refresh requires the frozen V64/V65/V66 Satsuma identity.");
            }
        }

        private static void ValidateOldOrCurrentShape(
            MountPointDefinition definition,
            EngineAccessRule rule)
        {
            bool legacy = definition.BlockedWhileOccupiedMountIds.Length == 0 &&
                          definition.RemovalBlockedWhileOccupiedMountIds.Length == 0;
            bool current = definition.BlockedWhileOccupiedMountIds.SequenceEqual(
                               rule.InstallationBlockers) &&
                           definition.RemovalBlockedWhileOccupiedMountIds.SequenceEqual(
                               rule.RemovalBlockers);
            if (!legacy && !current)
            {
                throw new InvalidDataException(
                    "E1 refuses a partially migrated or independently edited access shape on " +
                    rule.MountDefinitionId + ".");
            }
        }

        private static string[] AppendUnique(
            IReadOnlyList<string> existing,
            IReadOnlyList<string> additions)
        {
            var result = new List<string>(
                (existing?.Count ?? 0) + (additions?.Count ?? 0));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (existing != null)
            {
                foreach (string value in existing)
                {
                    if (!string.IsNullOrEmpty(value) && seen.Add(value))
                    {
                        result.Add(value);
                    }
                }
            }

            if (additions != null)
            {
                foreach (string value in additions)
                {
                    if (!string.IsNullOrEmpty(value) && seen.Add(value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result.ToArray();
        }

        private static string MountId(string slug) =>
            "mount.satsuma.engine-block." + slug;

        [Serializable]
        private sealed class ManifestIdentity
        {
            public string builderVersion = string.Empty;
            public string featureId = string.Empty;
            public string stableVehicleId = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public string runtimePrefabPath = string.Empty;
        }

        private sealed class EngineAccessRule
        {
            public EngineAccessRule(
                string partSlug,
                string[] installationBlockers,
                string[] removalBlockers)
            {
                MountDefinitionId = MountId(partSlug);
                AcceptedPartDefinitionId = "vehicle.satsuma.part." + partSlug;
                InstallationBlockers = installationBlockers ?? Array.Empty<string>();
                RemovalBlockers = removalBlockers ?? Array.Empty<string>();
            }

            public string MountDefinitionId { get; }
            public string AcceptedPartDefinitionId { get; }
            public string[] InstallationBlockers { get; }
            public string[] RemovalBlockers { get; }
        }

        private sealed class EngineAccessPlan
        {
            public EngineAccessPlan(
                MountPointDefinition definition,
                string[] installationBlockers,
                string[] removalBlockers)
            {
                Definition = definition;
                InstallationBlockers = installationBlockers;
                RemovalBlockers = removalBlockers;
            }

            public MountPointDefinition Definition { get; }
            public string[] InstallationBlockers { get; }
            public string[] RemovalBlockers { get; }
        }
    }
}
