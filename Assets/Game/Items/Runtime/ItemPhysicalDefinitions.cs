using System;
using UnityEngine;

namespace MSC.Items
{
    public enum ItemColliderShapeKind
    {
        Box = 0,
        Sphere = 1,
        Capsule = 2,
    }

    /// <summary>
    /// Immutable primitive collision copied from reviewed donor evidence.
    /// Complex donor mesh colliders deliberately continue through the safe
    /// project proxy path until a convex replacement is reviewed.
    /// </summary>
    [Serializable]
    public sealed class ItemColliderShapeDefinition
    {
        [SerializeField] private ItemColliderShapeKind kind;
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one * 0.1f;
        [SerializeField, Min(0.001f)] private float radius = 0.05f;
        [SerializeField, Min(0.001f)] private float height = 0.1f;
        [SerializeField, Range(0, 2)] private int direction = 1;
        [SerializeField] private bool isTrigger;

        public ItemColliderShapeKind Kind => kind;
        public Vector3 Center => center;
        public Vector3 Size => size;
        public float Radius => radius;
        public float Height => height;
        public int Direction => direction;
        public bool IsTrigger => isTrigger;

        public bool TryValidate(out string failure)
        {
            if (!Enum.IsDefined(typeof(ItemColliderShapeKind), kind) ||
                !IsFinite(center) || !IsFinitePositive(size) ||
                !float.IsFinite(radius) || radius <= 0f ||
                !float.IsFinite(height) || height <= 0f ||
                direction < 0 || direction > 2 ||
                kind == ItemColliderShapeKind.Capsule &&
                height + 0.0001f < radius * 2f)
            {
                failure = "Item collider shape is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ItemColliderShapeKind configuredKind,
            Vector3 configuredCenter,
            Vector3 configuredSize,
            float configuredRadius,
            float configuredHeight,
            int configuredDirection,
            bool configuredTrigger = false)
        {
            kind = configuredKind;
            center = configuredCenter;
            size = configuredSize;
            radius = configuredRadius;
            height = configuredHeight;
            direction = configuredDirection;
            isTrigger = configuredTrigger;
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinitePositive(Vector3 value) =>
            IsFinite(value) && value.x > 0f && value.y > 0f && value.z > 0f;
    }
}
