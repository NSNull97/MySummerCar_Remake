using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Bounded, project-owned wrapper for the reviewed donor ignition presentation.</summary>
    internal static class Phase1SatsumaIgnitionAuthoring
    {
        internal const string GeneratedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        internal const string KeyMeshGuid = "106152c393ea3d944bcccd352b6ee602";
        internal const string SocketMeshGuid = "d99cd1a03153439488c620e94c76032d";
        internal const string AtlasMaterialGuid = "ad2f7b6e8cc080845a7a7fd4264fbb83";
        internal const string ColumnPartId = "vehicle.satsuma.part.steering-column";
        internal static readonly Vector3 LockPosition = new(-0.09685037f, -0.2685372f, 0.023809155f);
        internal static readonly Quaternion LockRotation = new(0.040687773f, 0.25343287f, -0.2046824f, 0.9445748f);
        internal static readonly Vector3 TriggerPosition = new(-0.10124879f, -0.2754797f, 0.009919457f);
        internal static readonly Quaternion TriggerRotation = new(-0.16877113f, -0.65749615f, -0.7329113f, -0.04534463f);
        internal static readonly Vector3 TriggerScale = new(0.026748622f, 0.026748618f, 0.026748622f);

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Physical Ignition")]
        public static void RefreshPhysicalIgnitionBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string prefabPath = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ValidateRoot(asset);
            ImportReviewedMeshes();
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = ApplyBindings(root, GeneratedRoot);
                if (changed)
                {
                    // Keep the exact old asset recoverable without touching user saves.
                    string backup = "Logs/codex-ignition-prefab-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".txt";
                    Directory.CreateDirectory("Logs");
                    File.Copy(prefabPath, backup, overwrite: false);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    {
                        throw new InvalidDataException("C2 could not save the scoped ignition prefab.");
                    }
                }

                Debug.Log("PHASE1_SATSUMA_COCKPIT_C2_REFRESH_OK changedPrefabs=" +
                    (changed ? 1 : 0) + " fullRebuild=false mountDefinitionsUnchanged=true");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static bool ApplyBindings(GameObject root, string generatedRoot)
        {
            ValidateRoot(root);
            var assembly = root.GetComponent<VehicleAssemblyController>();
            var simulation = root.GetComponent<VehicleSimulationHost>();
            var input = root.GetComponent<VehicleInputRouter>();
            var electrical = root.GetComponent<SatsumaElectricalSystem>();
            var persistence = root.GetComponent<VehiclePersistenceBinding>();
            PartInstance column = assembly.Parts.Single(part =>
                part != null && part.Definition != null && part.Definition.DefinitionId == ColumnPartId);
            SatsumaIgnitionController[] controllers = root.GetComponents<SatsumaIgnitionController>();
            SatsumaIgnitionInputAdapter[] adapters = root.GetComponents<SatsumaIgnitionInputAdapter>();
            SatsumaIgnitionInteractionTarget[] targets = root.GetComponentsInChildren<SatsumaIgnitionInteractionTarget>(true);
            if (controllers.Length != 0 || adapters.Length != 0 || targets.Length != 0)
            {
                if (controllers.Length != 1 || adapters.Length != 1 || targets.Length != 1 ||
                    simulation.InputSourceComponent != adapters[0] ||
                    persistence.IgnitionController != controllers[0] ||
                    adapters[0].Router != input || adapters[0].Ignition != controllers[0] ||
                    controllers[0].Assembly != assembly || controllers[0].Electrical != electrical ||
                    controllers[0].SimulationHost != simulation ||
                    targets[0].transform.parent != column.transform)
                {
                    throw new InvalidDataException("C2 refuses partial/duplicate ignition bindings.");
                }

                ValidatePresentation(controllers[0], targets[0], column, generatedRoot);
                return false;
            }

            if (simulation.InputSourceComponent != input)
            {
                throw new InvalidDataException("C2 refuses an unexpected existing vehicle input owner.");
            }

            Mesh keyMesh = RequireAsset<Mesh>(generatedRoot + "/Meshes/" + KeyMeshGuid + ".asset");
            Mesh socketMesh = RequireAsset<Mesh>(generatedRoot + "/Meshes/" + SocketMeshGuid + ".asset");
            Material atlas = RequireAsset<Material>(generatedRoot + "/Materials/" + AtlasMaterialGuid + ".mat");
            Transform presentation = CreateChild(column.transform, "Ignition presentation", LockPosition, LockRotation);
            Transform pivot = CreateChild(presentation, "Ignition key pivot", Vector3.zero, Quaternion.identity);
            Renderer socket = CreateMesh(pivot, "Ignition socket", socketMesh, atlas,
                Vector3.zero, Quaternion.identity);
            Renderer key = CreateMesh(pivot, "Ignition key", keyMesh, atlas,
                new Vector3(-0.00013179361f, -0.000003356341f, 0.0015042042f),
                new Quaternion(1.505056e-7f, -0.73956585f, 9.091005e-8f, 0.6730842f));
            key.gameObject.SetActive(false);
            var ignition = root.AddComponent<SatsumaIgnitionController>();
            // Author references without running Graph's lazy runtime initialization:
            // that would reparent mount owners and mutate accepted prefab physics.
            SetReferences(ignition, ("assembly", assembly), ("electrical", electrical),
                ("simulationHost", simulation), ("keyPivot", pivot),
                ("visibleKey", key.gameObject), ("installedPresentation", presentation.gameObject));
            presentation.gameObject.SetActive(false);
            var adapter = root.AddComponent<SatsumaIgnitionInputAdapter>();
            adapter.Configure(input, ignition);
            var hostSerialized = new SerializedObject(simulation);
            hostSerialized.FindProperty("inputSourceComponent").objectReferenceValue = adapter;
            hostSerialized.ApplyModifiedPropertiesWithoutUndo();
            persistence.ConfigureIgnition(ignition);

            Transform interaction = CreateChild(column.transform, "Ignition held interaction", TriggerPosition, TriggerRotation);
            interaction.localScale = TriggerScale;
            var sphere = interaction.gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.7f;
            sphere.center = new Vector3(0f, 0.5f, 0f);
            var target = interaction.gameObject.AddComponent<SatsumaIgnitionInteractionTarget>();
            SetReferences(target, ("ignition", ignition), ("interactionCollider", sphere));
            sphere.enabled = false;
            var targetHost = interaction.gameObject.AddComponent<InteractionTargetHost>();
            targetHost.Configure(target);
            targetHost.ConfigureOutlineRenderers(socket, key);
            targetHost.ConfigureSelectionPriority(45);
            return true;
        }

        private static void ValidateRoot(GameObject root)
        {
            var assembly = root != null ? root.GetComponent<VehicleAssemblyController>() : null;
            var persistence = root != null ? root.GetComponent<VehiclePersistenceBinding>() : null;
            var input = root != null ? root.GetComponent<VehicleInputRouter>() : null;
            if (assembly == null ||
                assembly.MountPoints.Any(mount => mount == null) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(assembly.MountPoints.Select(mount => mount.MountId)) ||
                assembly.Parts.Count(part => part != null && part.Definition != null &&
                    part.Definition.DefinitionId == ColumnPartId) != 1 ||
                persistence == null || persistence.StableVehicleId != Phase1SatsumaBaselineBuilder.StableVehicleId ||
                input == null || input.enabled || root.GetComponent<VehicleSimulationHost>() == null ||
                root.GetComponent<SatsumaElectricalSystem>() == null ||
                root.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null))
            {
                throw new InvalidDataException("C2 requires the reviewed typed Satsuma mount roster and disabled device router.");
            }
        }

        private static void ValidatePresentation(SatsumaIgnitionController ignition,
            SatsumaIgnitionInteractionTarget target, PartInstance column, string generatedRoot)
        {
            Transform pivot = ignition.KeyPivot;
            GameObject presentation = ignition.InstalledPresentation;
            GameObject key = ignition.VisibleKey;
            SphereCollider shape = target.GetComponent<SphereCollider>();
            var targetData = new SerializedObject(target);
            if (pivot == null || presentation == null || key == null ||
                pivot.parent != presentation.transform || presentation.transform.parent != column.transform ||
                key.transform.parent != pivot || shape == null || !shape.isTrigger ||
                Mathf.Abs(shape.radius - 0.7f) > 0.000001f || shape.center != new Vector3(0f, 0.5f, 0f) ||
                Vector3.Distance(presentation.transform.localPosition, LockPosition) > 0.000001f ||
                Quaternion.Angle(presentation.transform.localRotation, LockRotation) > 0.001f ||
                Vector3.Distance(target.transform.localPosition, TriggerPosition) > 0.000001f ||
                Quaternion.Angle(target.transform.localRotation, TriggerRotation) > 0.001f ||
                Vector3.Distance(target.transform.localScale, TriggerScale) > 0.000001f ||
                targetData.FindProperty("ignition")?.objectReferenceValue != ignition ||
                targetData.FindProperty("interactionCollider")?.objectReferenceValue != shape)
            {
                throw new InvalidDataException("C2 refuses drifted ignition presentation/interaction bindings.");
            }

            MeshFilter[] meshes = presentation.GetComponentsInChildren<MeshFilter>(true);
            string[] paths = meshes.Select(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh)).OrderBy(path => path).ToArray();
            string[] expectedPaths = new[] { KeyMeshGuid, SocketMeshGuid }
                .Select(guid => generatedRoot + "/Meshes/" + guid + ".asset").OrderBy(path => path).ToArray();
            InteractionTargetHost targetHost = target.GetComponent<InteractionTargetHost>();
            if (!paths.SequenceEqual(expectedPaths) || targetHost == null ||
                !targetHost.TryGetCapability<SatsumaIgnitionInteractionTarget>(out var capability) || capability != target ||
                targetHost.OutlineRenderers.Count != 2 || meshes.Any(mesh =>
                    !targetHost.OutlineRenderers.Contains(mesh.GetComponent<Renderer>()) ||
                    AssetDatabase.GetAssetPath(mesh.GetComponent<Renderer>().sharedMaterial) !=
                        generatedRoot + "/Materials/" + AtlasMaterialGuid + ".mat"))
            {
                throw new InvalidDataException("C2 refuses drifted runtime mesh/material/outline references.");
            }
        }

        private static Transform CreateChild(Transform parent, string label, Vector3 position, Quaternion rotation)
        {
            Transform child = new GameObject(label).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localRotation = rotation;
            return child;
        }

        private static Renderer CreateMesh(Transform parent, string label, Mesh mesh, Material material,
            Vector3 position, Quaternion rotation)
        {
            Transform child = CreateChild(parent, label, position, rotation);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidDataException("Missing reviewed C2 asset: " + path);

        private static void SetReferences(UnityEngine.Object owner,
            params (string field, UnityEngine.Object value)[] references)
        {
            var serialized = new SerializedObject(owner);
            foreach (var reference in references)
            {
                SerializedProperty property = serialized.FindProperty(reference.field) ??
                    throw new InvalidDataException("C2 serialized binding contract drift: " + reference.field);
                property.objectReferenceValue = reference.value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ImportReviewedMeshes()
        {
            DonorPathConfiguration config = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string meshSourceRoot = Path.Combine(config.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/Mesh");
            var specifications = new[]
            {
                (file: "key_satsuma.asset", guid: KeyMeshGuid, sha: "8F5225BFA68B54D8BEF99D30981091CA69CFBA69606464F50A319ED3DDE8FEBF"),
                (file: "ignition_hole.asset", guid: SocketMeshGuid, sha: "2DE11AD71D85749C566D1D2E9FDC7AB6E8FFB60BA7D998D85D93191930132403"),
            };
            // Preflight both sources/destinations before copying either payload.
            foreach (var spec in specifications)
            {
                string source = Path.Combine(meshSourceRoot, spec.file);
                string target = GeneratedRoot + "/Meshes/" + spec.guid + ".asset";
                if (Hash(source) != spec.sha ||
                    !File.ReadAllText(source + ".meta").Contains("guid: " + spec.guid) ||
                    (File.Exists(target) && Hash(target) != spec.sha))
                {
                    throw new InvalidDataException("C2 ignition mesh source/target drift: " + spec.file);
                }
            }

            foreach (var spec in specifications)
            {
                string source = Path.Combine(meshSourceRoot, spec.file);
                string target = GeneratedRoot + "/Meshes/" + spec.guid + ".asset";
                if (!File.Exists(target))
                {
                    File.Copy(source, target, overwrite: false);
                    using var sha = SHA256.Create();
                    string generatedGuid = BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes("satsuma-mesh:" + spec.guid))).Replace("-", "")
                        .Substring(0, 32).ToLowerInvariant();
                    File.WriteAllText(target + ".meta", File.ReadAllText(source + ".meta")
                        .Replace("guid: " + spec.guid, "guid: " + generatedGuid), new UTF8Encoding(false));
                }

                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                RequireAsset<Mesh>(target);
            }
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
