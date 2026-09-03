using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Saved authoring evidence for one renderer-only canonical rock override.
    /// Legacy MAP/MESH/ROCKS remains the collision authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanonicalRockReplacementAnchor : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private Vector3 worldBottomCenter;
        [SerializeField] private Vector3 mappedSourceSize;
        [SerializeField] private float principalYawDegrees;
        [SerializeField] private float authoredUniformScale;

        public string StableId => stableId;
        public Vector3 WorldBottomCenter => worldBottomCenter;
        public Vector3 MappedSourceSize => mappedSourceSize;
        public float PrincipalYawDegrees => principalYawDegrees;
        public float AuthoredUniformScale => authoredUniformScale;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(string id, Vector3 bottomCenter,
            Vector3 sourceSize, float yawDegrees, float uniformScale)
        {
            stableId = id;
            worldBottomCenter = bottomCenter;
            mappedSourceSize = sourceSize;
            principalYawDegrees = yawDegrees;
            authoredUniformScale = uniformScale;
        }
#endif
    }
}
