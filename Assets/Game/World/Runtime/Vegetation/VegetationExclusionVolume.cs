using UnityEngine;

namespace MSC.World.Vegetation
{
    [DisallowMultipleComponent]
    public sealed class VegetationExclusionVolume : MonoBehaviour
    {
        [SerializeField] private VegetationExclusionShape shape =
            VegetationExclusionShape.Box;
        [SerializeField] private Vector3 size = new Vector3(5f, 5f, 5f);
        [SerializeField, Min(0f)] private float radius = 2.5f;
        [SerializeField] private int channelMask = -1;

        public bool Excludes(VegetationDensityChannel channel, Vector3 worldPosition)
        {
            int channelBit = 1 << (int)channel;
            if ((channelMask & channelBit) == 0)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (shape == VegetationExclusionShape.Sphere)
            {
                return local.sqrMagnitude <= radius * radius;
            }

            Vector3 halfSize = size * 0.5f;
            return Mathf.Abs(local.x) <= halfSize.x &&
                   Mathf.Abs(local.y) <= halfSize.y &&
                   Mathf.Abs(local.z) <= halfSize.z;
        }

        public Bounds GetWorldBounds()
        {
            if (shape == VegetationExclusionShape.Sphere)
            {
                float diameter = radius * 2f;
                return new Bounds(
                    transform.position,
                    new Vector3(diameter, diameter, diameter));
            }

            Vector3 lossy = transform.lossyScale;
            return new Bounds(
                transform.position,
                new Vector3(
                    Mathf.Abs(size.x * lossy.x),
                    Mathf.Abs(size.y * lossy.y),
                    Mathf.Abs(size.z * lossy.z)));
        }
    }
}
