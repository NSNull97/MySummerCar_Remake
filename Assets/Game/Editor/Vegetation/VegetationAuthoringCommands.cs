using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    public static class VegetationAuthoringCommands
    {
        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Surface",
            false,
            40)]
        public static void MarkSelectionAsSurface()
        {
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                EnsureCollider(gameObject);
                if (gameObject.GetComponent<VegetationSurface>() == null)
                {
                    Undo.AddComponent<VegetationSurface>(gameObject);
                }

                VegetationBlocker blocker =
                    gameObject.GetComponent<VegetationBlocker>();
                if (blocker != null)
                {
                    Undo.DestroyObjectImmediate(blocker);
                }
            }
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Ground Road Blocker",
            false,
            41)]
        public static void MarkAsGroundRoad()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.GroundRoad);
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Bridge Deck Blocker",
            false,
            42)]
        public static void MarkAsBridgeDeck()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.BridgeDeck);
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Bridge Pillar Blocker",
            false,
            43)]
        public static void MarkAsBridgePillar()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.BridgePillar);
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Building Blocker",
            false,
            44)]
        public static void MarkAsBuilding()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.Building);
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Water Blocker",
            false,
            45)]
        public static void MarkAsWater()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.Water);
        }

        [MenuItem(
            "GameObject/MSC Vegetation/Mark Selection as Foundation Blocker",
            false,
            46)]
        public static void MarkAsFoundation()
        {
            MarkSelectionAsBlocker(VegetationBlockerKind.Foundation);
        }

        private static void MarkSelectionAsBlocker(
            VegetationBlockerKind kind)
        {
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                EnsureCollider(gameObject);
                VegetationBlocker blocker =
                    gameObject.GetComponent<VegetationBlocker>();
                if (blocker == null)
                {
                    blocker = Undo.AddComponent<VegetationBlocker>(gameObject);
                }

                Undo.RecordObject(blocker, "Configure vegetation blocker");
                blocker.ConfigureForAuthoring(kind);
                EditorUtility.SetDirty(blocker);

                VegetationSurface surface =
                    gameObject.GetComponent<VegetationSurface>();
                if (surface != null)
                {
                    Undo.DestroyObjectImmediate(surface);
                }
            }
        }

        private static void EnsureCollider(GameObject gameObject)
        {
            if (gameObject.GetComponent<Collider>() != null)
            {
                return;
            }

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning(
                    gameObject.name +
                    " has no collider or MeshFilter; marker was added but " +
                    "raycasts cannot use it.",
                    gameObject);
                return;
            }

            MeshCollider collider = Undo.AddComponent<MeshCollider>(gameObject);
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }
}
