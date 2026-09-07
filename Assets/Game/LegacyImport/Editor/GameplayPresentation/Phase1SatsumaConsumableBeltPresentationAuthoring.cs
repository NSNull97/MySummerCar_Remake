using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Frozen GAME 101788 presentation rig only; no donor animation controllers or FSMs.</summary>
    public static class Phase1SatsumaConsumableBeltPresentationAuthoring
    {
        public const string MeshSourceGuid = "7dab2df4b56946b4c9f8892f57e8e978";
        public const string MaterialSourceGuid = "1dc990c84250ce844b24b6439ec55445";

        public static GameObject GetOrCreatePrefab(string generatedRoot)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot + "/Meshes/" + MeshSourceGuid + ".asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(generatedRoot + "/Materials/" + MaterialSourceGuid + ".mat");
            ValidateAssets(mesh, material);
            string path = generatedRoot + "/InstalledAlternatorBeltPresentation.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                ValidatePrefab(existing, mesh, material);
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidDataException("Installed-belt prefab path has an unexpected asset.");
            GameObject root = CreateRig(mesh, material);
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        public static GameObject CreateRig(Mesh mesh, Material material)
        {
            ValidateAssets(mesh, material);
            var root = new GameObject("Satsuma installed belt TemporaryDirectImport");
            Transform meshParent = Child(root.transform, "Reviewed belt rig", new Vector3(0f, -.02970028f, -.012494144f),
                new Quaternion(.50000006f, .49999994f, -.49999997f, .50000006f),
                new Vector3(.020506203f, .020506203f, .020506198f));
            Transform bone = Child(meshParent, "Fixed bone", new Vector3(1.0000284f, 0f, 0f), Quaternion.identity, Vector3.one);
            // Frozen Jumping.Reset sets ScaleBone X/Z to Scale, initial 1.1;
            // Y remains 1. Running-engine vibration/wear is not this assembly presenter.
            Transform scaleBone = Child(meshParent, "Parked belt scale bone", Vector3.zero,
                new Quaternion(3.890561e-9f, 2.9547284e-8f, -1.4901161e-8f, 1f), new Vector3(1.1f, 1f, 1.1f));
            Transform meshObject = Child(meshParent, "Belt mesh", new Vector3(-62.64f, 1.13f, 72.16f),
                new Quaternion(1.0251827e-7f, 3.756168e-8f, .7071067f, .7071068f),
                new Vector3(48.76573f, 48.76573f, 48.76574f));
            var renderer = meshObject.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
            renderer.bones = new[] { bone, scaleBone }; renderer.rootBone = bone;
            renderer.localBounds = new Bounds(new Vector3(-1.0237055f, -.09810415f, .63133097f),
                new Vector3(11.7842388f, .57460672f, 11.5553332f));
            renderer.updateWhenOffscreen = true;
            root.SetActive(false);
            return root;
        }

        private static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.SetLocalPositionAndRotation(position, rotation); child.localScale = scale;
            return child;
        }

        private static void ValidateAssets(Mesh mesh, Material material)
        {
            if (mesh == null || material == null || mesh.bindposes.Length != 2 || mesh.boneWeights.Length != mesh.vertexCount)
                throw new InvalidDataException("The reviewed belt requires its imported mesh, material, two bind poses and original skin weights.");
        }

        private static void ValidatePrefab(GameObject prefab, Mesh mesh, Material material)
        {
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1 || renderers[0].sharedMesh != mesh || renderers[0].sharedMaterial != material ||
                renderers[0].bones.Length != 2 || renderers[0].rootBone != renderers[0].bones[0] ||
                prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                Vector3.Distance(renderers[0].bones[1].localScale, new Vector3(1.1f, 1f, 1.1f)) > .000001f)
                throw new InvalidDataException("The installed belt presentation differs from the reviewed rig.");
        }
    }
}
