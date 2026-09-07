using System;
using UnityEngine;

namespace MSC.Vehicle.ItemsIntegration
{
    /// <summary>
    /// Measures presentation geometry when an item interaction proxy is rebuilt.
    /// Skinned meshes are baked once per call; this is not a per-frame query.
    /// </summary>
    public static class ItemPartInteractionBounds
    {
        public static bool TryGetPartLocalBounds(
            Transform partRoot,
            GameObject visualRoot,
            out Bounds bounds)
        {
            bounds = default;
            if (partRoot == null || visualRoot == null) return false;

            bool hasBounds = false;
            Bounds combined = default;
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!TryGetRendererLocalBounds(renderer, out Bounds localBounds)) return false;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = localBounds.center + new Vector3(
                        (corner & 1) == 0 ? -localBounds.extents.x : localBounds.extents.x,
                        (corner & 2) == 0 ? -localBounds.extents.y : localBounds.extents.y,
                        (corner & 4) == 0 ? -localBounds.extents.z : localBounds.extents.z);
                    Vector3 local = partRoot.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!IsFinite(local)) return false;
                    if (!hasBounds)
                    {
                        combined = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else combined.Encapsulate(local);
                }
            }

            if (!hasBounds || !IsFinite(combined.center) || !IsFinite(combined.size)) return false;
            bounds = combined;
            return true;
        }

        private static bool TryGetRendererLocalBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = default;
            if (!(renderer is SkinnedMeshRenderer skinned))
            {
                bounds = renderer.localBounds;
                return IsFinite(bounds.center) && IsFinite(bounds.extents);
            }

            // Imported skinning/culling AABBs can be expressed in a different
            // bone frame. A failed bake must use the caller's authored proxy size,
            // never that AABB or the undeformed shared-mesh bounds.
            if (skinned.sharedMesh == null || skinned.sharedMesh.vertexCount == 0) return false;
            Mesh baked = null;
            try
            {
                baked = new Mesh { name = "Temporary item interaction bounds", hideFlags = HideFlags.HideAndDontSave };
                // Compensate the renderer's world scale in the baked vertices;
                // TransformPoint above applies that scale once. The false mode
                // retains it and would scale both the bounds and their offset twice.
                skinned.BakeMesh(baked, useScale: true);
                if (baked.vertexCount == 0) return false;
                baked.RecalculateBounds();
                Bounds measured = baked.bounds;
                if (!IsFinite(measured.center) || !IsFinite(measured.extents)) return false;
                bounds = measured;
                return true;
            }
            catch (UnityException) { return false; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            finally
            {
                if (baked != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(baked);
                    else UnityEngine.Object.DestroyImmediate(baked);
                }
            }
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
