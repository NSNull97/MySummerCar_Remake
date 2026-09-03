using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Items
{
    [Serializable]
    public sealed class ItemPlacementRecord
    {
        [SerializeField] private string placementId = string.Empty;
        [SerializeField] private string stableEntityId = string.Empty;
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Quaternion worldRotation = Quaternion.identity;
        [SerializeField] private string donorStableId = string.Empty;
        [SerializeField] private string donorSourceObjectId = string.Empty;
        [SerializeField] private string donorHierarchyPath = string.Empty;
        [SerializeField] private string donorSourceHash = string.Empty;
        [SerializeField] private int initialVariantIndex;
        [SerializeField] private Vector3 initialLocalScale = Vector3.one;
        [SerializeField] private string donorParentStableId = string.Empty;
        [SerializeField] private int donorLayer = 19;
        [SerializeField] private bool donorHasRigidbody = true;
        [SerializeField] private bool initialUseGravity = true;
        [SerializeField] private bool initialIsKinematic;
        [SerializeField] private bool initialDetectCollisions = true;
        [SerializeField] private CollisionDetectionMode initialCollisionMode =
            CollisionDetectionMode.Continuous;
        [SerializeField] private RigidbodyInterpolation initialInterpolation =
            RigidbodyInterpolation.None;
        [SerializeField] private RigidbodyConstraints initialConstraints =
            RigidbodyConstraints.None;
        [SerializeField, Min(0f)] private float initialLinearDamping;
        [SerializeField, Min(0f)] private float initialAngularDamping = 0.05f;

        public string PlacementId => placementId;
        public string StableEntityId => stableEntityId;
        public string DefinitionId => definitionId;
        public Vector3 WorldPosition => worldPosition;
        public Quaternion WorldRotation => worldRotation;
        public string DonorStableId => donorStableId;
        public string DonorSourceObjectId => donorSourceObjectId;
        public string DonorHierarchyPath => donorHierarchyPath;
        public string DonorSourceHash => donorSourceHash;
        public int InitialVariantIndex => initialVariantIndex;
        public Vector3 InitialLocalScale => initialLocalScale;
        public string DonorParentStableId => donorParentStableId;
        public int DonorLayer => donorLayer;
        public bool DonorHasRigidbody => donorHasRigidbody;
        public bool InitialUseGravity => initialUseGravity;
        public bool InitialIsKinematic => initialIsKinematic;
        public bool InitialDetectCollisions => initialDetectCollisions;
        public CollisionDetectionMode InitialCollisionMode =>
            initialCollisionMode;
        public RigidbodyInterpolation InitialInterpolation =>
            initialInterpolation;
        public RigidbodyConstraints InitialConstraints => initialConstraints;
        public float InitialLinearDamping => initialLinearDamping;
        public float InitialAngularDamping => initialAngularDamping;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(placementId) ||
                placementId.Length > 128 ||
                !MSC.Core.Identity.StableEntityId.TryParse(stableEntityId, out _) ||
                !ItemDefinitionId.IsValid(definitionId) ||
                !IsFinite(worldPosition) ||
                !IsFinite(worldRotation) ||
                !IsFinitePositive(initialLocalScale) ||
                donorLayer < 0 || donorLayer > 31 ||
                !Enum.IsDefined(
                    typeof(CollisionDetectionMode),
                    initialCollisionMode) ||
                !Enum.IsDefined(
                    typeof(RigidbodyInterpolation),
                    initialInterpolation) ||
                !float.IsFinite(initialLinearDamping) ||
                initialLinearDamping < 0f ||
                !float.IsFinite(initialAngularDamping) ||
                initialAngularDamping < 0f ||
                initialVariantIndex < 0)
            {
                failure = $"Item placement '{placementId}' is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredPlacementId,
            string configuredStableEntityId,
            string configuredDefinitionId,
            Vector3 configuredWorldPosition,
            Quaternion configuredWorldRotation,
            string configuredDonorStableId,
            string configuredDonorSourceObjectId,
            string configuredDonorHierarchyPath,
            string configuredDonorSourceHash,
            int configuredInitialVariantIndex)
        {
            placementId = configuredPlacementId ?? string.Empty;
            stableEntityId = configuredStableEntityId ?? string.Empty;
            definitionId = configuredDefinitionId ?? string.Empty;
            worldPosition = configuredWorldPosition;
            worldRotation = configuredWorldRotation;
            donorStableId = configuredDonorStableId ?? string.Empty;
            donorSourceObjectId = configuredDonorSourceObjectId ?? string.Empty;
            donorHierarchyPath = configuredDonorHierarchyPath ?? string.Empty;
            donorSourceHash = configuredDonorSourceHash ?? string.Empty;
            initialVariantIndex = configuredInitialVariantIndex;
        }

        public void ConfigurePhysicsForAuthoring(
            Vector3 configuredLocalScale,
            string configuredDonorParentStableId,
            int configuredDonorLayer,
            bool configuredDonorHasRigidbody,
            bool configuredUseGravity,
            bool configuredIsKinematic,
            bool configuredDetectCollisions,
            CollisionDetectionMode configuredCollisionMode,
            RigidbodyInterpolation configuredInterpolation,
            RigidbodyConstraints configuredConstraints,
            float configuredLinearDamping,
            float configuredAngularDamping)
        {
            initialLocalScale = configuredLocalScale;
            donorParentStableId = configuredDonorParentStableId ?? string.Empty;
            donorLayer = configuredDonorLayer;
            donorHasRigidbody = configuredDonorHasRigidbody;
            initialUseGravity = configuredUseGravity;
            initialIsKinematic = configuredIsKinematic;
            initialDetectCollisions = configuredDetectCollisions;
            initialCollisionMode = configuredCollisionMode;
            initialInterpolation = configuredInterpolation;
            initialConstraints = configuredConstraints;
            initialLinearDamping = configuredLinearDamping;
            initialAngularDamping = configuredAngularDamping;
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinitePositive(Vector3 value) =>
            IsFinite(value) && value.x > 0f && value.y > 0f && value.z > 0f;

        private static bool IsFinite(Quaternion value)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) ||
                !float.IsFinite(value.w))
            {
                return false;
            }

            float magnitude = value.x * value.x + value.y * value.y +
                              value.z * value.z + value.w * value.w;
            return magnitude > 0.000001f;
        }
    }

}
