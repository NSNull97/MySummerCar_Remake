using System;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Reviewed cockpit authoring corrections, independent of presentation
    /// rebuilds. C1a corrects the meters' latch; C1b corrects the wheel's
    /// installation-only column prerequisite and Bolted-on threshold.
    /// </summary>
    internal static class Phase1SatsumaCockpitRules
    {
        internal const string DashboardMeterMountId = "mount.satsuma.dashboard.meters";
        internal const string SteeringWheelMountId = "mount.satsuma.steering-wheel";
        internal const string SteeringColumnMountId = "mount.satsuma.steering-column";
        private const string DefinitionPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
            "MountDefinitions/" + DashboardMeterMountId + ".asset";

        private static readonly string[] FastenerIds =
        {
            "fastener.satsuma.dashboard-meters.boltpm-1",
            "fastener.satsuma.dashboard-meters.boltpm-2",
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Dashboard Meter Latch")]
        public static void RefreshDashboardMeterLatchBatch()
        {
            ValidateManifestIdentity();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            VehicleAssemblyController assembly = prefab != null
                ? prefab.GetComponent<VehicleAssemblyController>()
                : null;
            MountPointAuthoring[] mounts = assembly != null
                ? assembly.MountPoints
                : Array.Empty<MountPointAuthoring>();
            if (mounts.Length != 117 || mounts.Any(value => value == null) ||
                mounts.Select(value => value.MountId)
                    .Distinct(StringComparer.Ordinal).Count() != 117)
            {
                throw new InvalidDataException(
                    "C1a refresh requires the canonical unique 117-mount Satsuma.");
            }

            MountPointAuthoring authoring = mounts.SingleOrDefault(value =>
                string.Equals(value.MountId, DashboardMeterMountId,
                    StringComparison.Ordinal));
            MountPointDefinition definition = authoring != null
                ? authoring.Definition
                : null;
            if (definition == null ||
                !string.Equals(AssetDatabase.GetAssetPath(definition),
                    DefinitionPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "C1a refresh refuses an unexpected dashboard definition binding.");
            }

            bool changed = ApplyDashboardMeterLatch(definition);
            if (changed)
            {
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            Debug.Log("PHASE1_SATSUMA_COCKPIT_C1A_REFRESH_OK changedMountDefinitions=" +
                (changed ? 1 : 0) +
                " fullRebuild=false prefabUnchanged=true manifestUnchanged=true");
        }

        internal static bool ApplyDashboardMeterLatch(MountPointDefinition definition)
        {
            FastenerDefinition[] fasteners = definition != null
                ? definition.Fasteners
                : Array.Empty<FastenerDefinition>();
            FastenerGroupDefinition group = definition != null
                ? definition.FastenerGroup
                : null;
            if (definition == null ||
                !string.Equals(definition.DefinitionId, DashboardMeterMountId,
                    StringComparison.Ordinal) ||
                !string.Equals(definition.OwnerPartDefinitionId,
                    "vehicle.satsuma.part.dashboard", StringComparison.Ordinal) ||
                !definition.AcceptedPartDefinitionIds.SequenceEqual(
                    new[] { "vehicle.satsuma.part.dashboard-meters" }) ||
                fasteners == null || fasteners.Length != 2 ||
                fasteners.Any(value => value == null ||
                    value.Size != FastenerSize.Millimeter6 ||
                    value.MaximumStage != 8 || !value.RequiredForRemoval ||
                    !value.InsertedOnInstall) ||
                !fasteners.Select(value => value.DefinitionId).SequenceEqual(FastenerIds) ||
                group == null || !group.FastenerDefinitionIds.SequenceEqual(FastenerIds) ||
                group.AggregateMaximumTightness != 16 ||
                group.BoltedOnThreshold is not (1 or 12) ||
                group.BoltedOffThreshold != 0 ||
                group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                group.BreakAction != FastenerBreakAction.None)
            {
                throw new InvalidDataException(
                    "C1a refuses drifted dashboard meters identity, fasteners or latch authoring.");
            }

            if (group.BoltedOnThreshold == 12)
            {
                return false;
            }

            // Donor BoltCheck108742: ON12/OFF0. Preserve the existing group
            // membership and every retention field, including inactive ones.
            group.Configure(group.FastenerDefinitionIds,
                group.AggregateMaximumTightness, 12, group.BoltedOffThreshold,
                group.SpeedRetentionPolicy, group.LooseBreakSpeedKph,
                group.PartialCheckSpeedKph, group.ChanceDivisor, group.BreakAction);
            EditorUtility.SetDirty(definition);
            return true;
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Steering Wheel Rules")]
        public static void RefreshSteeringWheelRulesBatch()
        {
            ValidateManifestIdentity();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            VehicleAssemblyController assembly = prefab != null
                ? prefab.GetComponent<VehicleAssemblyController>()
                : null;
            MountPointAuthoring[] mounts = assembly != null
                ? assembly.MountPoints
                : Array.Empty<MountPointAuthoring>();
            if (mounts.Length != 117 || mounts.Any(value => value == null) ||
                mounts.Select(value => value.MountId)
                    .Distinct(StringComparer.Ordinal).Count() != 117)
            {
                throw new InvalidDataException(
                    "C1b refresh requires the canonical unique 117-mount Satsuma.");
            }

            MountPointDefinition definition = null;
            foreach (string mountId in new[] { SteeringWheelMountId, SteeringColumnMountId })
            {
                MountPointAuthoring authoring = mounts.SingleOrDefault(value =>
                    string.Equals(value.MountId, mountId, StringComparison.Ordinal));
                string expectedPath =
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                    "MountDefinitions/" + mountId + ".asset";
                if (authoring == null || authoring.Definition == null ||
                    !string.Equals(authoring.Definition.DefinitionId, mountId,
                        StringComparison.Ordinal) ||
                    !string.Equals(AssetDatabase.GetAssetPath(authoring.Definition),
                        expectedPath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "C1b refresh refuses an unexpected wheel/column definition binding.");
                }

                if (mountId == SteeringWheelMountId)
                {
                    definition = authoring.Definition;
                }
            }

            bool changed = ApplySteeringWheelRules(definition);
            if (changed)
            {
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            Debug.Log("PHASE1_SATSUMA_COCKPIT_C1B_REFRESH_OK changedMountDefinitions=" +
                (changed ? 1 : 0) +
                " fullRebuild=false prefabUnchanged=true manifestUnchanged=true");
        }

        internal static bool ApplySteeringWheelRules(MountPointDefinition definition)
        {
            const string fastenerId = "fastener.satsuma.steering-wheel.boltpm-1";
            FastenerDefinition[] fasteners = definition != null
                ? definition.Fasteners
                : Array.Empty<FastenerDefinition>();
            FastenerGroupDefinition group = definition != null
                ? definition.FastenerGroup
                : null;
            if (definition == null ||
                definition.DefinitionId != SteeringWheelMountId ||
                definition.OwnerPartDefinitionId != "vehicle.satsuma.part.body-shell" ||
                definition.AcceptedPartDefinitionIds == null ||
                !definition.AcceptedPartDefinitionIds.SequenceEqual(new[]
                {
                    "vehicle.satsuma.part.gt-gt-steering-wheel",
                    "vehicle.satsuma.part.stock-steering-wheel",
                }) ||
                fasteners == null || fasteners.Length != 1 || fasteners[0] == null ||
                fasteners[0].DefinitionId != fastenerId ||
                fasteners[0].Size != FastenerSize.Millimeter10 ||
                fasteners[0].MaximumStage != 8 || !fasteners[0].InsertedOnInstall ||
                !fasteners[0].RequiredForRemoval ||
                fasteners[0].ToolRule == null ||
                fasteners[0].ToolRule.ToolType != "Wrench" ||
                fasteners[0].ToolRule.FastenerSize != FastenerSize.Millimeter10 ||
                group == null || group.FastenerDefinitionIds == null ||
                !group.FastenerDefinitionIds.SequenceEqual(new[] { fastenerId }) ||
                group.AggregateMaximumTightness != 8 ||
                group.BoltedOnThreshold is not (1 or 2) ||
                group.BoltedOffThreshold != 0 ||
                group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                group.BreakAction != FastenerBreakAction.None ||
                !float.IsFinite(group.LooseBreakSpeedKph) ||
                !float.IsFinite(group.PartialCheckSpeedKph) ||
                !float.IsFinite(group.ChanceDivisor) ||
                group.LooseBreakSpeedKph < 0f ||
                group.PartialCheckSpeedKph < group.LooseBreakSpeedKph ||
                group.ChanceDivisor < 0.0001f ||
                (definition.InstallationRequiredOccupiedMountIds.Length != 0 &&
                 !definition.InstallationRequiredOccupiedMountIds.SequenceEqual(
                     new[] { SteeringColumnMountId })) ||
                definition.RequiredOccupiedMountIds.Length != 0 ||
                definition.RequiredAnyOccupiedMountIds.Length != 0 ||
                definition.RequiredBoltedMountIds.Length != 0 ||
                definition.RequiredAnyBoltedMountIds.Length != 0 ||
                definition.BlockedWhileOccupiedMountIds.Length != 0 ||
                definition.RemovalBlockedWhileOccupiedMountIds.Length != 0 ||
                definition.RemovalBlockedWhileBoltedMountIds.Length != 0 ||
                definition.RemovalIgnoredDependentMountIds.Length != 0 ||
                definition.InstallationBlockedWhileBoltedMountIds.Length != 0 ||
                !string.IsNullOrEmpty(definition.InstallAttemptBoltedSupportMountId))
            {
                throw new InvalidDataException(
                    "C1b refuses drifted steering-wheel identity, fasteners or assembly rules.");
            }

            if (group.BoltedOnThreshold == 2 &&
                definition.InstallationRequiredOccupiedMountIds.Length == 1)
            {
                return false;
            }

            // Donor trigger is under installed column, not necessarily bolted.
            // Do not turn this access condition into ownership/removal/collapse.
            definition.ConfigureInstallationOccupancy(new[] { SteeringColumnMountId });
            // Existing true latches atT1 remain valid through IsLatchConsistent;
            // only future false->true transitions now require donor ON2.
            group.Configure(group.FastenerDefinitionIds,
                group.AggregateMaximumTightness, 2, group.BoltedOffThreshold,
                group.SpeedRetentionPolicy, group.LooseBreakSpeedKph,
                group.PartialCheckSpeedKph, group.ChanceDivisor, group.BreakAction);
            EditorUtility.SetDirty(definition);
            return true;
        }

        internal static void ValidateManifestIdentity()
        {
            ManifestIdentity manifest = JsonUtility.FromJson<ManifestIdentity>(
                File.ReadAllText(Phase1SatsumaBaselineBuilder.ManifestPath));
            if (manifest == null ||
                manifest.builderVersion is not ("11A-V1d.64" or "11A-V1d.65" or "11A-V1d.66") ||
                !string.Equals(manifest.featureId, "P1.CAR.001", StringComparison.Ordinal) ||
                !string.Equals(manifest.stableVehicleId,
                    Phase1SatsumaBaselineBuilder.StableVehicleId, StringComparison.Ordinal) ||
                !string.Equals(manifest.sourceSceneSha256,
                    Phase1SatsumaBaselineBuilder.LockedSceneSha256, StringComparison.Ordinal) ||
                !string.Equals(manifest.runtimePrefabPath,
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Cockpit refresh requires the frozen V64/V65/V66 Satsuma identity.");
            }
        }

        [Serializable]
        private sealed class ManifestIdentity
        {
            public string builderVersion = string.Empty;
            public string featureId = string.Empty;
            public string stableVehicleId = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public string runtimePrefabPath = string.Empty;
        }
    }
}
