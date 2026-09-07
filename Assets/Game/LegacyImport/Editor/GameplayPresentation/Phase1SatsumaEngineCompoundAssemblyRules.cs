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
    /// Reviewed floor-assembly access rules. See the engine compound assembly
    /// report for action-level donor evidence and the narrowly scoped migration.
    /// </summary>
    internal static class Phase1SatsumaEngineCompoundAssemblyRules
    {
        internal const string GasketMount = "mount.satsuma.engine-block.head-gasket";
        internal const string PlateMount = "mount.satsuma.engine-block.engine-plate";
        internal const string HeadMount = "mount.satsuma.engine-block.cylinder-head";
        internal const string GearboxMount = "mount.satsuma.engine-block.gearbox";
        internal const string FlywheelMount = "mount.satsuma.crankshaft.flywheel";
        internal const string CoverMount = "mount.satsuma.flywheel.clutch-cover-plate";
        internal const string PressureMount = "mount.satsuma.clutch-cover-plate.clutch-pressure-plate";
        internal const string DiscMount = "mount.satsuma.clutch-cover-plate.clutch-disc";
        internal const string EngineMount = "mount.satsuma.engine-assembly";
        internal const string SubframeMount = "mount.satsuma.sub-frame";
        internal const string OilpanMount = "mount.satsuma.engine-block.oilpan";
        internal const string ClutchLiningMount = "mount.satsuma.clutch-lining";
        internal const string HalfshaftLeftMount = "mount.satsuma.halfshaft-fl";
        internal const string HalfshaftRightMount = "mount.satsuma.halfshaft-fr";
        internal const string GearLinkageMount = "mount.satsuma.gear-linkage";
        internal const string ExhaustMount = "mount.satsuma.exhaust-pipe";
        internal const string TimingCoverMount = "mount.satsuma.engine-block.timing-cover";
        internal const string TimingChainMount = "mount.satsuma.camshaft-gear.timing-chain";
        private const string DiscPart = "vehicle.satsuma.part.clutch";
        private const string CoverPart = "vehicle.satsuma.part.clutch-cover-plate";
        private const string BlockPart = "vehicle.satsuma.part.engine-block";

        // This is an audited whitelist, not a runtime "all owned children"
        // inference. New sockets remain blocking until explicitly reviewed.
        private static readonly string[] RetainedEngineChildren =
        {
            "mount.satsuma.engine-block.alternator",
            "mount.satsuma.engine-block.camshaft",
            "mount.satsuma.engine-block.crankshaft",
            HeadMount,
            "mount.satsuma.engine-block.distributor",
            PlateMount,
            "mount.satsuma.engine-block.fuel-pump",
            GearboxMount,
            GasketMount,
            "mount.satsuma.engine-block.main-bearing1",
            "mount.satsuma.engine-block.main-bearing2",
            "mount.satsuma.engine-block.main-bearing3",
            "mount.satsuma.engine-block.oil-filter",
            OilpanMount,
            "mount.satsuma.engine-block.piston1",
            "mount.satsuma.engine-block.piston2",
            "mount.satsuma.engine-block.piston3",
            "mount.satsuma.engine-block.piston4",
            "mount.satsuma.engine-block.radiator-hose2",
            "mount.satsuma.engine-block.timing-cover",
        };

        internal static string[] EngineToCarReferencedMountIds => RetainedEngineChildren.Concat(new[]
        {
            EngineMount, SubframeMount, ClutchLiningMount, HalfshaftLeftMount,
            HalfshaftRightMount, GearLinkageMount, ExhaustMount,
        }).ToArray();

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Compound Assembly Rules")]
        public static void RefreshEngineCompoundAssemblyRulesBatch()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController controller =
                    contents.GetComponent<VehicleAssemblyController>();
                if (controller == null || controller.MountPoints.Length != 117)
                {
                    throw new InvalidDataException("Expected the canonical 117-mount Satsuma.");
                }

                MountPointDefinition[] definitions =
                    controller.MountPoints.Select(value => value.Definition).ToArray();
                MountPointDefinition[] changed = ApplyEngineCompoundRules(definitions)
                    .Concat(ApplyEngineToCarRules(definitions)).Distinct().ToArray();
                foreach (MountPointDefinition definition in changed)
                {
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }

                var serialized = new SerializedObject(controller);
                SerializedProperty dependencies = serialized.FindProperty("dependencies");
                int removed = 0;
                for (int index = dependencies.arraySize - 1; index >= 0; index--)
                {
                    SerializedProperty edge = dependencies.GetArrayElementAtIndex(index);
                    if (!IsIncorrectImportedDependency(
                            edge.FindPropertyRelative("dependentPartDefinitionId").stringValue,
                            edge.FindPropertyRelative("relatedPartDefinitionId").stringValue,
                            (AssemblyDependencyKind)edge.FindPropertyRelative("kind").intValue))
                    {
                        continue;
                    }

                    dependencies.DeleteArrayElementAtIndex(index);
                    removed++;
                }

                if (removed != 0)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents,
                        Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                }

                Debug.Log("PHASE1_SATSUMA_ENGINE_COMPOUND_RULES_REFRESH_OK changedMountDefinitions=" +
                          changed.Length + " removedIncorrectDependencies=" + removed +
                          " fullRebuild=false sourceSceneSha256=" +
                          Phase1SatsumaBaselineBuilder.LockedSceneSha256);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static MountPointDefinition[] ApplyEngineCompoundRules(
            IReadOnlyList<MountPointDefinition> definitions)
        {
            if (definitions == null || definitions.Any(value => value == null) ||
                definitions.Select(value => value.DefinitionId).Distinct(StringComparer.Ordinal)
                    .Count() != definitions.Count)
            {
                throw new InvalidDataException("Compound assembly requires unique valid mount definitions.");
            }

            var byId = definitions.ToDictionary(value => value.DefinitionId, StringComparer.Ordinal);
            foreach (string id in new[] { GasketMount, PlateMount, HeadMount, GearboxMount,
                         FlywheelMount, CoverMount, PressureMount, DiscMount, TimingCoverMount, TimingChainMount })
            {
                if (!byId.ContainsKey(id))
                {
                    throw new InvalidDataException("Missing engine compound assembly socket: " + id);
                }
            }

            ValidateOwner(byId[GasketMount], "vehicle.satsuma.part.engine-block");
            ValidateOwner(byId[PlateMount], "vehicle.satsuma.part.engine-block");
            ValidateOwner(byId[CoverMount], "vehicle.satsuma.part.flywheel");
            ValidateOwner(byId[PressureMount], CoverPart);
            ValidateOwner(byId[DiscMount], CoverPart);
            ValidateOwner(byId[TimingChainMount], "vehicle.satsuma.part.camshaft-gear");
            var touched = new[] { byId[GasketMount], byId[PlateMount], byId[PressureMount],
                byId[DiscMount], byId[CoverMount], byId[TimingChainMount] };
            string[] before = touched.Select(value => EditorJsonUtility.ToJson(value)).ToArray();

            AddOccupancyBlockers(byId[GasketMount], new[] { HeadMount }, new[] { HeadMount });
            AddOccupancyBlockers(byId[PlateMount], new[] { GearboxMount, FlywheelMount },
                new[] { FlywheelMount });
            AddOccupancyBlockers(byId[PressureMount], new[] { CoverMount }, new[] { DiscMount });
            AddOccupancyBlockers(byId[DiscMount], new[] { CoverMount }, Array.Empty<string>());
            byId[DiscMount].ConfigureInstallationOccupancy(Append(
                byId[DiscMount].InstallationRequiredOccupiedMountIds, new[] { PressureMount }));
            byId[DiscMount].ConfigureRemovalChecks(Append(
                    byId[DiscMount].RemovalBlockedWhileBoltedMountIds, new[] { CoverMount }),
                byId[DiscMount].RemovalIgnoredDependentMountIds);
            byId[CoverMount].ConfigureRetainedRemovalChildren(Append(
                byId[CoverMount].RemovalRetainedChildMountIds, new[] { PressureMount, DiscMount }));
            AddOccupancyBlockers(byId[TimingChainMount], new[] { TimingCoverMount },
                new[] { TimingCoverMount });

            return touched.Where((value, index) =>
                !string.Equals(before[index], EditorJsonUtility.ToJson(value),
                    StringComparison.Ordinal)).ToArray();
        }

        internal static AssemblyDependency[] FilterEngineDependencies(
            IEnumerable<AssemblyDependency> dependencies)
        {
            return (dependencies ?? Array.Empty<AssemblyDependency>()).Where(value =>
                value == null || !IsIncorrectImportedDependency(value.DependentPartDefinitionId,
                    value.RelatedPartDefinitionId, value.Kind)).ToArray();
        }

        internal static MountPointDefinition[] ApplyEngineToCarRules(
            IReadOnlyList<MountPointDefinition> definitions)
        {
            if (definitions == null || definitions.Any(value => value == null) ||
                definitions.Select(value => value.DefinitionId).Distinct(StringComparer.Ordinal)
                    .Count() != definitions.Count)
            {
                throw new InvalidDataException("Engine-to-car rules require unique valid mount definitions.");
            }

            var byId = definitions.ToDictionary(value => value.DefinitionId, StringComparer.Ordinal);
            foreach (string id in EngineToCarReferencedMountIds)
            {
                if (!byId.ContainsKey(id))
                {
                    throw new InvalidDataException("Missing engine-to-car rule socket: " + id);
                }
            }

            MountPointDefinition engine = byId[EngineMount];
            ValidateOwner(engine, "vehicle.satsuma.part.body-shell");
            if (!engine.AcceptedPartDefinitionIds.SequenceEqual(new[] { BlockPart }))
            {
                throw new InvalidDataException("Unexpected engine-to-car accepted part.");
            }

            foreach (string id in RetainedEngineChildren)
            {
                ValidateOwner(byId[id], BlockPart);
            }

            string before = EditorJsonUtility.ToJson(engine);
            engine.ConfigureInstallationOccupancy(Append(engine.InstallationRequiredOccupiedMountIds,
                new[] { GearboxMount, OilpanMount, SubframeMount }));
            engine.ConfigureRetainedRemovalChildren(Append(engine.RemovalRetainedChildMountIds,
                RetainedEngineChildren));
            engine.ConfigureRemovalBlockers(Append(engine.RemovalBlockedWhileOccupiedMountIds,
                new[] { ClutchLiningMount }));
            engine.ConfigureRemovalChecks(Append(engine.RemovalBlockedWhileBoltedMountIds,
                    new[] { HalfshaftRightMount, HalfshaftLeftMount, GearLinkageMount, ExhaustMount }),
                engine.RemovalIgnoredDependentMountIds);

            return string.Equals(before, EditorJsonUtility.ToJson(engine), StringComparison.Ordinal)
                ? Array.Empty<MountPointDefinition>() : new[] { engine };
        }

        private static bool IsIncorrectImportedDependency(string dependent, string related,
            AssemblyDependencyKind kind)
        {
            return IsIncorrectDependencyPair(dependent, related, kind, DiscPart, CoverPart) ||
                   IsIncorrectDependencyPair(dependent, related, kind,
                       "vehicle.satsuma.part.timing-chain", "vehicle.satsuma.part.timing-cover");
        }

        private static bool IsIncorrectDependencyPair(string dependent, string related,
            AssemblyDependencyKind kind, string innerPart, string outerPart) =>
            kind == AssemblyDependencyKind.InstallRequiresInstalled &&
            dependent == innerPart && related == outerPart ||
            kind == AssemblyDependencyKind.RemovalBlockedWhileInstalled &&
            dependent == outerPart && related == innerPart;

        private static void ValidateOwner(MountPointDefinition definition, string owner)
        {
            if (!string.Equals(definition.OwnerPartDefinitionId, owner, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unexpected engine compound mount owner: " +
                                               definition.DefinitionId);
            }
        }

        private static void AddOccupancyBlockers(MountPointDefinition definition,
            string[] installation, string[] removal)
        {
            definition.ConfigureSequence(definition.RequiredOccupiedMountIds,
                definition.RequiredAnyOccupiedMountIds,
                Append(definition.BlockedWhileOccupiedMountIds, installation));
            definition.ConfigureRemovalBlockers(Append(
                definition.RemovalBlockedWhileOccupiedMountIds, removal));
        }

        private static string[] Append(IEnumerable<string> existing, IEnumerable<string> additions) =>
            existing.Concat(additions).Distinct(StringComparer.Ordinal).ToArray();
    }
}
