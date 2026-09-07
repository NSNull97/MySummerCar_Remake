using System;
using System.IO;
using System.Linq;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>One fixed hard-line fitting; never changes the 302 assembly fasteners.</summary>
    public static class Phase1SatsumaFuelLineConnectionAuthoring
    {
        public const string NutMeshSourceGuid = "e711c8a15b1135c4089caad19b8f56e8";
        public const string MaterialSourceGuid = "98697bae08a8c114ba9774c487f2658d";
        public const string ReplacementKey = "legacy.vehicle.satsuma.fuel-line-fitting";
        public const string SourceSceneSha256 = "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";
        public const long SourceMarkerId = 45288;
        public const long SourceScrewFsmId = 106716;
        public const long SourceBoltCheckFsmId = 110473;
        public const float ColliderRadius = .0144f; // Source .012 sphere * maximum marker scale1.2.
        public static Pose TankLocalPose => new(new Vector3(.027247787f, .30196226f, -.07855269f),
            new Quaternion(-.0000019548993f, .70710653f, .70710707f, .0000025231745f).normalized);
        public static Vector3 PhysicalScale => new(1.2f, 1.2f, .8f);

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Fixed Fuel Line Fitting Only")]
        public static void RefreshFuelLineConnectionBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before the fixed fuel-line fitting refresh.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            if (!File.Exists(path)) throw new InvalidDataException("Existing canonical Satsuma is required.");
            string folder = Path.Combine("Logs", "fuel-line-fitting-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            Directory.CreateDirectory(folder);
            File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), false);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaStartableCarNightBatch.ValidateTopology(assembly, true);
                int changed = ApplyToInstance(assembly);
                if (ApplyToInstance(assembly) != 0) throw new InvalidDataException("Fuel-line fitting repeat must change zero bindings.");
                Phase1SatsumaStartableCarNightBatch.ValidateTopology(assembly, true);
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Could not save fixed fuel-line fitting.");
                Debug.Log($"SATSUMA_FUEL_LINE_FITTING_OK changed={changed} repeat=0 graphFasteners=302 " +
                    $"tankMountBolts=7 leakSimulation=false fullRebuild=false nativeSaveWrites=false backup={folder}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            string root = generatedRoot ?? Phase1SatsumaStartableCarNightBatch.GeneratedRoot;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + NutMeshSourceGuid + ".asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/" + MaterialSourceGuid + ".mat");
            return Configure(assembly, mesh, material, LayerMask.NameToLayer(FastenerToolRaycastLayer.Name));
        }

        /// <summary>Same preflight for typed tests and canonical authoring; no graph initialization or definition writes.</summary>
        public static int Configure(VehicleAssemblyController assembly, Mesh mesh, Material material, int layer)
        {
            if (Application.isPlaying || assembly == null || mesh == null || material == null || layer < 0 || layer > 31)
                throw new InvalidDataException("Explicit fuel-line fitting authoring sources are required outside Play.");
            PartInstance[] tanks = assembly.Parts.Where(p => p != null && p.Definition != null &&
                p.Definition.DefinitionId == SatsumaFuelLineConnection.TankPartId).ToArray();
            MountPointAuthoring[] mounts = assembly.MountPoints.Where(m => m != null &&
                m.MountId == SatsumaFuelLineConnection.TankMountId).ToArray();
            if (tanks.Length != 1 || mounts.Length != 1 || mounts[0].Definition == null ||
                !tanks[0].transform.IsChildOf(assembly.transform))
                throw new InvalidDataException("Exactly one owned stock tank and its mount are required.");
            PartInstance tank = tanks[0];
            MountPointDefinition definition = mounts[0].Definition;
            string[] sevenIds = Phase1SatsumaStockMountFastenerAuthoring.GetBindings()
                .Where(b => b.MountId == SatsumaFuelLineConnection.TankMountId).Select(b => b.FastenerId).ToArray();
            var group = definition.FastenerGroup;
            if (sevenIds.Length != 7 || !definition.Fasteners.Select(f => f != null ? f.DefinitionId : "").SequenceEqual(sevenIds) ||
                group == null || !group.FastenerDefinitionIds.SequenceEqual(sevenIds) ||
                group.AggregateMaximumTightness != 56 || group.BoltedOnThreshold != 12 || group.BoltedOffThreshold != 0)
                throw new InvalidDataException("Apply the reviewed seven tank mounting bolts before the independent fuel-line fitting.");
            var persistence = assembly.GetComponent<VehiclePersistenceBinding>();
            if (persistence == null) throw new InvalidDataException("Fuel-line fitting requires the existing vehicle persistence binding.");
            if (assembly.Tools.Count(t => t != null && t.ToolType == "Wrench" &&
                    t.Size == FastenerSize.Millimeter12) != 1)
                throw new InvalidDataException("Exactly one registered stock Wrench12 is required for the fuel-line fitting.");
            var states = assembly.GetComponentsInChildren<SatsumaFuelLineConnection>(true);
            var targets = assembly.GetComponentsInChildren<SatsumaFuelLineFastenerInteractionTarget>(true);
            if (states.Length > 1 || targets.Length > 1 || states.Length != targets.Length)
                throw new InvalidDataException("Partial or duplicate fixed fuel-line fitting authoring.");
            MeshFilter[] original = tank.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh == mesh &&
                f.GetComponentInParent<PartInstance>(true) == tank &&
                f.GetComponentInParent<SatsumaFuelLineFastenerInteractionTarget>(true) == null &&
                f.GetComponentInParent<AssemblyFastenerInteractionTarget>(true) == null &&
                MatrixMatches(RelativeMatrix(f.transform, tank.transform), Matrix4x4.TRS(TankLocalPose.position, TankLocalPose.rotation, PhysicalScale)))
                .ToArray();
            if (original.Length != 1 || original[0].GetComponent<MeshRenderer>() == null ||
                original[0].GetComponent<MeshRenderer>().sharedMaterial != material)
                throw new InvalidDataException("Missing/ambiguous measured tank-local fuel-line nut source45288/47938.");

            if (states.Length == 1)
            {
                SatsumaFuelLineConnection state = states[0];
                SatsumaFuelLineFastenerInteractionTarget target = targets[0];
                if (state.gameObject != assembly.gameObject || state.Assembly != assembly || state.Tank != tank ||
                    persistence.FuelLineConnection != state || !state.CaptureSaveData().TryValidate(out _) ||
                    !IsReviewedTarget(target, state, tank, mesh, material, layer) || original[0].gameObject.activeSelf)
                    throw new InvalidDataException("Existing fuel-line fitting differs from its reviewed binding.");
                return 0;
            }
            if (persistence.FuelLineConnection != null)
                throw new InvalidDataException("Foreign fuel-line persistence binding must not be replaced.");

            // All mutable work follows the complete source/ownership/persistence preflight.
            var connection = assembly.gameObject.AddComponent<SatsumaFuelLineConnection>();
            connection.Configure(assembly, tank);
            var marker = new GameObject(SatsumaFuelLineConnection.ConnectionId) { layer = layer };
            marker.transform.SetParent(tank.transform, false);
            marker.transform.SetLocalPositionAndRotation(TankLocalPose.position, TankLocalPose.rotation);
            var visible = new GameObject(ReplacementKey) { layer = layer };
            visible.transform.SetParent(marker.transform, false);
            visible.transform.localScale = PhysicalScale;
            visible.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visible.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false;
            var collider = marker.AddComponent<SphereCollider>(); collider.isTrigger = true;
            collider.radius = ColliderRadius; collider.enabled = false;
            var interaction = marker.AddComponent<SatsumaFuelLineFastenerInteractionTarget>();
            interaction.Configure(connection, visible.transform, renderer, collider);
            var host = marker.AddComponent<InteractionTargetHost>(); host.Configure(interaction);
            host.ConfigureOutlineRenderers(renderer); host.ConfigureSelectionPriority(40);
            original[0].gameObject.SetActive(false);
            persistence.ConfigureFuelLineConnection(connection);
            EditorUtility.SetDirty(persistence); EditorUtility.SetDirty(connection);
            return 3;
        }

        private static bool IsReviewedTarget(SatsumaFuelLineFastenerInteractionTarget target, SatsumaFuelLineConnection state,
            PartInstance tank, Mesh mesh, Material material, int layer)
        {
            Transform visible = target.Presentation;
            if (target.Connection != state || target.transform.parent != tank.transform || target.gameObject.layer != layer ||
                !MatrixMatches(Matrix4x4.TRS(target.transform.localPosition, target.transform.localRotation, target.transform.localScale),
                    Matrix4x4.TRS(TankLocalPose.position, TankLocalPose.rotation, Vector3.one)) ||
                visible == null || visible.parent != target.transform || visible.gameObject.layer != layer ||
                !MatrixMatches(Matrix4x4.TRS(visible.localPosition, visible.localRotation, visible.localScale),
                    Matrix4x4.TRS(Vector3.back * (SatsumaFuelLineFastenerInteractionTarget.StageTravelMeters * state.Stage),
                        Quaternion.AngleAxis(45f * state.Stage, Vector3.forward), PhysicalScale))) return false;
            var filter = visible.GetComponent<MeshFilter>();
            var collider = target.GetComponent<SphereCollider>();
            var host = target.GetComponent<InteractionTargetHost>();
            return filter != null && filter.sharedMesh == mesh && target.FastenerRenderer != null &&
                target.FastenerRenderer.transform == visible && target.FastenerRenderer.sharedMaterial == material &&
                collider != null && collider.isTrigger && collider.center == Vector3.zero &&
                Mathf.Abs(collider.radius - ColliderRadius) < .000001f && host != null &&
                host.OutlineRenderers.Count == 1 && host.OutlineRenderers[0] == target.FastenerRenderer &&
                new SerializedObject(target).FindProperty("interactionCollider").objectReferenceValue == collider;
        }

        private static Matrix4x4 RelativeMatrix(Transform child, Transform root)
        {
            Matrix4x4 result = Matrix4x4.identity;
            while (child != root)
            {
                if (child == null) throw new InvalidDataException("Fuel-line source is outside its explicit tank.");
                result = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * result;
                child = child.parent;
            }
            return result;
        }

        private static bool MatrixMatches(Matrix4x4 actual, Matrix4x4 expected)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(actual[i] - expected[i]) > .0001f) return false;
            return true;
        }
    }
}
