using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Migration = MSC.Vehicle.Assembly.SatsumaRockerShaftFastenerMigration;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaValveAdjustmentsAuthoring
    {
        public const string ScrewMeshSourceGuid = "d42cbf361095bad4f9091a43a07fe91a";
        // Project order 1in,1ex,2in,2ex,3in,3ex,4in,4ex. These are frozen
        // marker positions relative to rocker root57286, including parent49925.
        private static readonly Vector3[] MarkerPositions =
        {
            new(.10784042f, .046406764f, .019587456f), new(.13926208f, .04644682f, .019590556f),
            new(.052043676f, .046426784f, .01958901f), new(.020552516f, .04642679f, .01958902f),
            new(-.06757891f, .046406764f, .019587456f), new(-.035745263f, .04644777f, .019590615f),
            new(-.12279421f, .046426784f, .01958901f), new(-.15428317f, .04642679f, .01958869f),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Valve Adjustments Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before valve authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                // Preserve a recovery copy before changing shared generated definitions.
                Directory.CreateDirectory("Logs");
                File.Copy(path, "Logs/codex-valves-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                MountPointDefinition mount = assembly.MountPoints.Single(value => value.MountId == Migration.MountId).Definition;
                string mountPath = AssetDatabase.GetAssetPath(mount);
                File.Copy(mountPath, "Logs/codex-rocker-mount-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".asset", false);
                int changes = ApplyToInstance(assembly);
                if (changes > 0)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new InvalidDataException("Could not save valve bindings.");
                    AssetDatabase.SaveAssetIfDirty(mount);
                }
                Debug.Log("SATSUMA_VALVE_ADJUSTMENTS_OK changed=" + changes + " retainedMountBolts=5 valveSettings=8 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null || Application.isPlaying) throw new ArgumentException("Explicit stopped-editor assembly required.");
            PartInstance part = assembly.Parts.Single(value => value?.Definition?.DefinitionId == AssemblyValveAdjustmentState.PartId);
            MountPointDefinition mount = assembly.MountPoints.Single(value => value.MountId == Migration.MountId).Definition;
            string[] ids = mount.Fasteners.Select(value => value.DefinitionId).ToArray();
            var oldTargets = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(value => value.MountId == Migration.MountId).ToArray();
            var newTargets = part.GetComponentsInChildren<AssemblyValveAdjustmentTarget>(true);
            AssemblyValveAdjustmentState existing = part.GetComponent<AssemblyValveAdjustmentState>();
            if (Migration.IsCanonicalShape(ids))
            {
                if (existing == null || existing.Part != part || existing.Assembly != assembly ||
                    newTargets.Length != 8 || newTargets.Select(value => value.ValveIndex).Distinct().Count() != 8 ||
                    newTargets.Any(value => value.State != existing || value.ScrewRenderer == null) ||
                    !Migration.IsCanonicalShape(oldTargets.Select(value => value.FastenerDefinitionId).ToArray()) ||
                    mount.FastenerGroup.AggregateMaximumTightness != 40 || mount.FastenerGroup.BoltedOnThreshold != 16)
                    throw new InvalidDataException("Canonical valve/retention bindings drifted.");
                return 0;
            }
            if (!Migration.IsLegacyShape(ids) || !Migration.IsLegacyShape(oldTargets.Select(value => value.FastenerDefinitionId).ToArray()) ||
                existing != null || newTargets.Length != 0 || mount.FastenerGroup.AggregateMaximumTightness != 104 ||
                mount.FastenerGroup.BoltedOnThreshold != 1 || mount.FastenerGroup.BoltedOffThreshold != 0)
                throw new InvalidDataException("Expected exact old thirteen-marker rocker-shaft contract.");
            var retained = new HashSet<string>(Migration.CanonicalIds, StringComparer.Ordinal);
            foreach (FastenerDefinition definition in mount.Fasteners)
                if (definition.MaximumStage != 8 || !definition.RequiredForRemoval ||
                    definition.Size != (retained.Contains(definition.DefinitionId) ? FastenerSize.Millimeter8 : FastenerSize.Millimeter6))
                    throw new InvalidDataException("Unexpected rocker fastener size or stage contract.");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Phase1SatsumaEngineAdjustmentsAuthoring.GeneratedRoot + "/Meshes/" + ScrewMeshSourceGuid + ".asset");
            if (mesh == null) throw new InvalidDataException("Missing reviewed valve screw mesh.");
            MeshFilter[] screws = part.GetComponentsInChildren<MeshFilter>(true).Where(value => value.sharedMesh == mesh &&
                value.GetComponentInParent<PartInstance>() == part).ToArray();
            if (screws.Length != 8) throw new InvalidDataException("Expected eight existing part-owned valve screw renderers.");
            var ordered = new MeshFilter[8];
            var used = new HashSet<MeshFilter>();
            for (int i = 0; i < 8; i++)
            {
                MeshFilter[] matches = screws.Where(value => Vector3.Distance(part.transform.InverseTransformPoint(value.transform.position),
                    MarkerPositions[i]) < 0.012f).ToArray();
                if (matches.Length != 1 || !used.Add(matches[0]) || matches[0].GetComponent<Renderer>() == null ||
                    matches[0].GetComponents<Collider>().Length != 0 ||
                    oldTargets.Any(value => matches[0].transform.IsChildOf(value.transform)))
                    throw new InvalidDataException("Ambiguous/non-visual frozen valve screw " + i + ".");
                ordered[i] = matches[0];
            }
            // All source/target validation precedes the first mutation.
            AssemblyValveAdjustmentState settings = part.gameObject.AddComponent<AssemblyValveAdjustmentState>(); settings.Configure(part, assembly);
            for (int i = 0; i < 8; i++)
            {
                var control = new GameObject("Project-owned valve adjustment " + i); control.layer = part.gameObject.layer;
                control.transform.SetParent(part.transform, false); control.transform.localPosition = MarkerPositions[i];
                SphereCollider collider = control.AddComponent<SphereCollider>(); collider.radius = 0.012f; collider.isTrigger = true;
                var target = control.AddComponent<AssemblyValveAdjustmentTarget>(); Renderer renderer = ordered[i].GetComponent<Renderer>();
                target.Configure(settings, i, renderer);
                var host = control.AddComponent<InteractionTargetHost>(); host.Configure(target); host.ConfigureSelectionPriority(45);
                host.ConfigureOutlineRenderers(renderer);
            }
            foreach (AssemblyFastenerInteractionTarget target in oldTargets)
                if (!retained.Contains(target.FastenerDefinitionId)) Object.DestroyImmediate(target.gameObject);
            FastenerDefinition[] canonical = Migration.CanonicalIds.Select(id => mount.Fasteners.Single(value => value.DefinitionId == id)).ToArray();
            mount.Configure(mount.DefinitionId, mount.DisplayName, mount.SocketType, mount.OwnerPartDefinitionId,
                mount.AcceptedPartDefinitionIds, mount.Constraint, mount.ReferenceCandidateRadiusMeters, canonical);
            var group = new FastenerGroupDefinition(); group.Configure(Migration.CanonicalIds, 40, 16, 0);
            mount.ConfigureFastenerGroup(group); EditorUtility.SetDirty(mount);
            return 18;
        }
    }
}
