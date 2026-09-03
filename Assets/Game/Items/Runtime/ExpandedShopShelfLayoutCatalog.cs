using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items.Presentation
{
    /// <summary>
    /// Project-owned runtime description of the reviewed Expanded Shop shelf
    /// layout. The generated presentation prefabs contain meshes only; no mod
    /// scripts, physics or purchasing state are retained.
    /// </summary>
    [CreateAssetMenu(
        fileName = ResourceName,
        menuName = "MSC/Items/Expanded Shop Shelf Layout")]
    public sealed class ExpandedShopShelfLayoutCatalog : ScriptableObject
    {
        public const string ResourceName = "ExpandedShopShelfLayoutCatalog";
        public const int CurrentSchemaVersion = 1;
        public const int ExpectedGroupCount = 21;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string classification =
            "TemporaryDirectImport";
        [SerializeField] private string sourceDllSha256 = string.Empty;
        [SerializeField] private string sourceBundleSha256 = string.Empty;
        [SerializeField] private string sourceShelfPrefabSha256 = string.Empty;
        [SerializeField] private Vector3 storeRootWorldPosition;
        [SerializeField] private Quaternion storeRootWorldRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 storeRootWorldScale = Vector3.one;
        [SerializeField] private ExpandedShopShelfGroupDefinition[] groups =
            Array.Empty<ExpandedShopShelfGroupDefinition>();

        public int SchemaVersion => schemaVersion;
        public string Classification => classification;
        public string SourceDllSha256 => sourceDllSha256;
        public string SourceBundleSha256 => sourceBundleSha256;
        public string SourceShelfPrefabSha256 => sourceShelfPrefabSha256;
        public Vector3 StoreRootWorldPosition => storeRootWorldPosition;
        public Quaternion StoreRootWorldRotation => storeRootWorldRotation;
        public Vector3 StoreRootWorldScale => storeRootWorldScale;
        public IReadOnlyList<ExpandedShopShelfGroupDefinition> Groups => groups;

        public Vector3 GetGroupWorldPosition(
            ExpandedShopShelfGroupDefinition group) =>
            storeRootWorldPosition +
            storeRootWorldRotation * Vector3.Scale(
                group.SourceLocalPosition,
                storeRootWorldScale);

        public Quaternion GetGroupWorldRotation(
            ExpandedShopShelfGroupDefinition group) =>
            storeRootWorldRotation * group.SourceLocalRotation;

        public Vector3 GetGroupWorldScale(
            ExpandedShopShelfGroupDefinition group) =>
            Vector3.Scale(storeRootWorldScale, group.SourceLocalScale);

        public bool TryValidate(out string failure)
        {
            failure = string.Empty;
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure =
                    $"Expanded Shop shelf schema {schemaVersion} is not " +
                    $"supported (expected {CurrentSchemaVersion}).";
                return false;
            }

            if (!string.Equals(
                    classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                !IsSha256(sourceDllSha256) ||
                !IsSha256(sourceBundleSha256) ||
                !IsSha256(sourceShelfPrefabSha256))
            {
                failure =
                    "Expanded Shop shelf provenance is incomplete or invalid.";
                return false;
            }

            if (!HasPositiveScale(storeRootWorldScale) ||
                !HasUsableRotation(storeRootWorldRotation) ||
                groups == null || groups.Length != ExpectedGroupCount)
            {
                failure =
                    "Expanded Shop shelf root transform or physical group " +
                    "count is invalid.";
                return false;
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceNames = new HashSet<string>(StringComparer.Ordinal);
            var occupiedStockIndices = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal);
            for (int index = 0; index < groups.Length; index++)
            {
                ExpandedShopShelfGroupDefinition group = groups[index];
                if (group == null ||
                    !group.TryValidate(out failure) ||
                    !groupIds.Add(group.GroupId) ||
                    !sourceNames.Add(group.SourceGroupName))
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? "Expanded Shop shelf group identities are not unique."
                        : failure;
                    return false;
                }

                if (!occupiedStockIndices.TryGetValue(
                        group.OfferId,
                        out HashSet<int> indices))
                {
                    indices = new HashSet<int>();
                    occupiedStockIndices.Add(group.OfferId, indices);
                }

                for (int unitIndex = 0;
                     unitIndex < group.StockUnitCount;
                     unitIndex++)
                {
                    int globalIndex = group.StockIndexOffset + unitIndex;
                    if (!indices.Add(globalIndex))
                    {
                        failure =
                            $"Expanded Shop shelf stock index {globalIndex} " +
                            $"overlaps for '{group.OfferId}'.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        public void ConfigureForAuthoring(
            string configuredDllSha256,
            string configuredBundleSha256,
            string configuredShelfPrefabSha256,
            Vector3 configuredStoreRootWorldPosition,
            Quaternion configuredStoreRootWorldRotation,
            Vector3 configuredStoreRootWorldScale,
            ExpandedShopShelfGroupDefinition[] configuredGroups)
        {
            schemaVersion = CurrentSchemaVersion;
            classification = "TemporaryDirectImport";
            sourceDllSha256 = configuredDllSha256 ?? string.Empty;
            sourceBundleSha256 = configuredBundleSha256 ?? string.Empty;
            sourceShelfPrefabSha256 =
                configuredShelfPrefabSha256 ?? string.Empty;
            storeRootWorldPosition = configuredStoreRootWorldPosition;
            storeRootWorldRotation = configuredStoreRootWorldRotation;
            storeRootWorldScale = configuredStoreRootWorldScale;
            groups = configuredGroups ??
                Array.Empty<ExpandedShopShelfGroupDefinition>();
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool hexadecimal = character is >= '0' and <= '9' or
                    >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!hexadecimal)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasPositiveScale(Vector3 value) =>
            value.x > 0f && value.y > 0f && value.z > 0f;

        public static bool HasUsableRotation(Quaternion value) =>
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w > 0.5f;
    }

    [Serializable]
    public sealed class ExpandedShopShelfGroupDefinition
    {
        [SerializeField] private string groupId = string.Empty;
        [SerializeField] private string sourceGroupName = string.Empty;
        [SerializeField] private string offerId = string.Empty;
        [SerializeField] private Vector3 sourceLocalPosition;
        [SerializeField] private Quaternion sourceLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 sourceLocalScale = Vector3.one;
        [SerializeField] private Vector3 colliderLocalPosition;
        [SerializeField] private Quaternion colliderLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 colliderLocalScale = Vector3.one;
        [SerializeField] private Vector3 colliderCenter;
        [SerializeField] private Vector3 colliderSize = Vector3.one;
        [SerializeField] private bool colliderIsTrigger = true;
        [SerializeField, Min(0)] private int stockIndexOffset;
        [SerializeField, Min(0)] private int stockUnitCount;
        [SerializeField] private GameObject presentationPrefab;

        public string GroupId => groupId;
        public string SourceGroupName => sourceGroupName;
        public string OfferId => offerId;
        public Vector3 SourceLocalPosition => sourceLocalPosition;
        public Quaternion SourceLocalRotation => sourceLocalRotation;
        public Vector3 SourceLocalScale => sourceLocalScale;
        public Vector3 ColliderLocalPosition => colliderLocalPosition;
        public Quaternion ColliderLocalRotation => colliderLocalRotation;
        public Vector3 ColliderLocalScale => colliderLocalScale;
        public Vector3 ColliderCenter => colliderCenter;
        public Vector3 ColliderSize => colliderSize;
        public bool ColliderIsTrigger => colliderIsTrigger;
        public int StockIndexOffset => stockIndexOffset;
        public int StockUnitCount => stockUnitCount;
        public GameObject PresentationPrefab => presentationPrefab;

        public bool TryValidate(out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(groupId) ||
                string.IsNullOrWhiteSpace(sourceGroupName) ||
                string.IsNullOrWhiteSpace(offerId) ||
                !offerId.StartsWith(
                    "service.store.expanded-shop-",
                    StringComparison.Ordinal) ||
                !ExpandedShopShelfLayoutCatalog.HasPositiveScale(
                    sourceLocalScale) ||
                !ExpandedShopShelfLayoutCatalog.HasUsableRotation(
                    sourceLocalRotation) ||
                !ExpandedShopShelfLayoutCatalog.HasPositiveScale(
                    colliderLocalScale) ||
                !ExpandedShopShelfLayoutCatalog.HasUsableRotation(
                    colliderLocalRotation) ||
                colliderSize.x <= 0f || colliderSize.y <= 0f ||
                colliderSize.z <= 0f || stockIndexOffset < 0 ||
                stockUnitCount < 0 ||
                stockUnitCount > 0 && presentationPrefab == null)
            {
                failure =
                    $"Expanded Shop shelf group '{sourceGroupName}' is invalid.";
                return false;
            }

            if (presentationPrefab != null)
            {
                ExpandedShopShelfGroupPresentation presentation =
                    presentationPrefab.GetComponent<
                        ExpandedShopShelfGroupPresentation>();
                if (presentation == null ||
                    !presentation.TryValidate(out failure) ||
                    presentation.StockUnits.Count != stockUnitCount)
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? $"Expanded Shop shelf group '{sourceGroupName}' " +
                          "has a mismatched presentation prefab."
                        : failure;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public void ConfigureForAuthoring(
            string configuredGroupId,
            string configuredSourceGroupName,
            string configuredOfferId,
            Vector3 configuredSourceLocalPosition,
            Quaternion configuredSourceLocalRotation,
            Vector3 configuredSourceLocalScale,
            Vector3 configuredColliderLocalPosition,
            Quaternion configuredColliderLocalRotation,
            Vector3 configuredColliderLocalScale,
            Vector3 configuredColliderCenter,
            Vector3 configuredColliderSize,
            bool configuredColliderIsTrigger,
            int configuredStockIndexOffset,
            int configuredStockUnitCount,
            GameObject configuredPresentationPrefab)
        {
            groupId = configuredGroupId ?? string.Empty;
            sourceGroupName = configuredSourceGroupName ?? string.Empty;
            offerId = configuredOfferId ?? string.Empty;
            sourceLocalPosition = configuredSourceLocalPosition;
            sourceLocalRotation = configuredSourceLocalRotation;
            sourceLocalScale = configuredSourceLocalScale;
            colliderLocalPosition = configuredColliderLocalPosition;
            colliderLocalRotation = configuredColliderLocalRotation;
            colliderLocalScale = configuredColliderLocalScale;
            colliderCenter = configuredColliderCenter;
            colliderSize = configuredColliderSize;
            colliderIsTrigger = configuredColliderIsTrigger;
            stockIndexOffset = configuredStockIndexOffset;
            stockUnitCount = configuredStockUnitCount;
            presentationPrefab = configuredPresentationPrefab;
        }
    }

}
