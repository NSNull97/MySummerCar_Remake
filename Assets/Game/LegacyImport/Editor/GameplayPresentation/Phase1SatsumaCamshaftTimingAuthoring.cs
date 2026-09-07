using System;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaCamshaftTimingAuthoring
    {
        public const string GearMeshSourceGuid = "f846a4e5b79ba9c4ea14694fae7bb75b";
        public const string GeneratedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Camshaft Timing Only")]
        public static void RefreshCamshaftTimingBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refreshing camshaft timing.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                VehicleAssemblyController assembly = contents.GetComponent<VehicleAssemblyController>();
                MountPointDefinition definition = RequireGearMount(assembly).Definition;
                bool changedThreshold = definition.FastenerGroup.BoltedOnThreshold != 5;
                int changedBindings = ApplyToInstance(assembly);
                if (changedBindings > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-camshaft-timing-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save scoped camshaft timing bindings.");
                }
                if (changedThreshold) AssetDatabase.SaveAssetIfDirty(definition);
                Debug.Log("SATSUMA_CAMSHAFT_TIMING_REFRESH_OK changedBindings=" + changedBindings +
                    " changedThresholds=" + (changedThreshold ? 1 : 0) + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // Full-builder integration: call after fastener target authoring and
        // before saving the prefab. This method never initializes the graph.
        public static int ApplyToInstance(VehicleAssemblyController assembly,
            string generatedRoot = GeneratedRoot)
        {
            MountPointAuthoring mount = RequireGearMount(assembly);
            PartInstance gear = assembly.Parts.Single(value => value != null &&
                value.Definition != null && value.Definition.DefinitionId ==
                    AssemblyCamshaftTimingState.PartDefinitionId);
            Mesh sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                generatedRoot.TrimEnd('/', '\\') + "/Meshes/" + GearMeshSourceGuid + ".asset");
            if (sourceMesh == null)
                throw new InvalidDataException("The reviewed camshaft gear mesh is missing.");
            MeshFilter[] meshes = gear.GetComponentsInChildren<MeshFilter>(true)
                .Where(value => value.sharedMesh == sourceMesh).ToArray();
            if (meshes.Length != 1 || meshes[0].transform == gear.transform)
                throw new InvalidDataException("Camshaft timing requires one explicit donor gear mesh child.");
            Transform mesh = meshes[0].transform;
            AssemblyFastenerInteractionTarget target = assembly
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Single(value =>
                    value.Controller == assembly && value.MountId == AssemblyCamshaftTimingState.GearMountId &&
                    value.FastenerDefinitionId == AssemblyCamshaftTimingState.GearFastenerId);
            if (!assembly.MountPoints.Any(value => value != null &&
                    value.MountId == AssemblyCamshaftTimingState.ChainMountId))
                throw new InvalidDataException("Camshaft timing chain gate is missing.");
            AssemblyCamshaftTimingState state = gear.GetComponent<AssemblyCamshaftTimingState>();
            if (target.PostTighteningAction != null && !ReferenceEquals(target.PostTighteningAction, state))
                throw new InvalidDataException("The camshaft bolt already belongs to a different post-tightening action.");

            int changed = 0;
            if (state == null)
            {
                state = gear.gameObject.AddComponent<AssemblyCamshaftTimingState>();
                changed++;
            }
            // The donor mesh69544 has identity rest rotation. Preserve the
            // importer's explicit rest basis as an adapter: donor local-X delta
            // is post-multiplied; only its normalized delta produces Data.Angle.
            if (state.Part != gear || state.Assembly != assembly || state.GearMesh != mesh ||
                Quaternion.Angle(state.MeshBaseLocalRotation, mesh.localRotation) > 0.001f)
            {
                state.Configure(gear, assembly, mesh, mesh.localRotation);
                changed++;
            }
            if (!ReferenceEquals(target.PostTighteningAction, state))
            {
                target.ConfigurePostTighteningAction(state);
                changed++;
            }
            ApplyThreshold(mount.Definition);
            return changed;
        }

        private static MountPointAuthoring RequireGearMount(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new InvalidDataException("Satsuma assembly is missing.");
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value != null &&
                value.MountId == AssemblyCamshaftTimingState.GearMountId);
            MountPointDefinition definition = mount.Definition;
            if (definition == null || definition.DefinitionId != AssemblyCamshaftTimingState.GearMountId ||
                definition.OwnerPartDefinitionId != "vehicle.satsuma.part.camshaft" ||
                !definition.AcceptedPartDefinitionIds.SequenceEqual(new[] { AssemblyCamshaftTimingState.PartDefinitionId }) ||
                definition.Fasteners.Length != 1 || definition.Fasteners[0] == null)
                throw new InvalidDataException("Camshaft gear mount identity/ownership drifted.");
            FastenerDefinition bolt = definition.Fasteners[0];
            FastenerGroupDefinition group = definition.FastenerGroup;
            if (bolt.DefinitionId != AssemblyCamshaftTimingState.GearFastenerId ||
                bolt.MaximumStage != 8 || bolt.Size != FastenerSize.Millimeter10 ||
                bolt.ToolRule.ToolType != "Wrench" || group == null ||
                !group.FastenerDefinitionIds.SequenceEqual(new[] { bolt.DefinitionId }) ||
                group.AggregateMaximumTightness != 8 || group.BoltedOffThreshold != 0 ||
                group.BoltedOnThreshold != 1 && group.BoltedOnThreshold != 5)
                throw new InvalidDataException("Camshaft gear fastener contract drifted.");
            return mount;
        }

        private static void ApplyThreshold(MountPointDefinition definition)
        {
            FastenerGroupDefinition old = definition.FastenerGroup;
            if (old.BoltedOnThreshold == 5) return;
            var updated = new FastenerGroupDefinition();
            updated.Configure(old.FastenerDefinitionIds, old.AggregateMaximumTightness, 5,
                old.BoltedOffThreshold, old.SpeedRetentionPolicy, old.LooseBreakSpeedKph,
                old.PartialCheckSpeedKph, old.ChanceDivisor, old.BreakAction);
            definition.ConfigureFastenerGroup(updated);
            EditorUtility.SetDirty(definition);
        }
    }
}
