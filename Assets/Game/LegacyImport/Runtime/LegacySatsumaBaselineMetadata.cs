using System;
using UnityEngine;

namespace MSC.LegacyImport
{
    [DisallowMultipleComponent]
    public sealed class LegacySatsumaBaselineMetadata : MonoBehaviour
    {
        [SerializeField] private string featureId = string.Empty;
        [SerializeField] private string vehicleContentId = string.Empty;
        [SerializeField] private string stableVehicleId = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private string sourceSceneSha256 = string.Empty;
        [SerializeField] private long sourceRootTransformId;
        [SerializeField] private string sourceHierarchyPath = string.Empty;
        [SerializeField] private Vector3 defaultWorldPosition;
        [SerializeField] private Vector3 defaultWorldEulerAngles;
        [SerializeField] private int sanitizedRendererCount;
        [SerializeField] private int sanitizedColliderCount;
        [SerializeField] private int loosePartCount;
        [SerializeField] private int activeLoosePartCount;
        [SerializeField] private DonorTransferClassification classification;

        public string FeatureId => featureId;
        public string VehicleContentId => vehicleContentId;
        public string StableVehicleId => stableVehicleId;
        public string ReplacementKey => replacementKey;
        public string SourceSceneSha256 => sourceSceneSha256;
        public long SourceRootTransformId => sourceRootTransformId;
        public string SourceHierarchyPath => sourceHierarchyPath;
        public Vector3 DefaultWorldPosition => defaultWorldPosition;
        public Quaternion DefaultWorldRotation =>
            Quaternion.Euler(defaultWorldEulerAngles);
        public int SanitizedRendererCount => sanitizedRendererCount;
        public int SanitizedColliderCount => sanitizedColliderCount;
        public int LoosePartCount => loosePartCount;
        public int ActiveLoosePartCount => activeLoosePartCount;
        public DonorTransferClassification Classification => classification;

        public void Configure(
            string configuredStableVehicleId,
            string sceneSha256,
            long donorRootTransformId,
            string donorHierarchyPath,
            Vector3 spawnPosition,
            Quaternion spawnRotation,
            int rendererCount,
            int colliderCount)
        {
            featureId = "P1.CAR.001";
            vehicleContentId = "vehicle.satsuma";
            stableVehicleId = configuredStableVehicleId ?? string.Empty;
            replacementKey = "legacy.vehicle.satsuma.presentation";
            sourceSceneSha256 = sceneSha256 ?? string.Empty;
            sourceRootTransformId = donorRootTransformId;
            sourceHierarchyPath = donorHierarchyPath ?? string.Empty;
            defaultWorldPosition = spawnPosition;
            defaultWorldEulerAngles = spawnRotation.eulerAngles;
            sanitizedRendererCount = Mathf.Max(0, rendererCount);
            sanitizedColliderCount = Mathf.Max(0, colliderCount);
            classification = DonorTransferClassification.TemporaryDirectImport;
        }

        public void ConfigureLoosePartRoster(
            int configuredLoosePartCount,
            int configuredActiveLoosePartCount)
        {
            loosePartCount = Mathf.Max(0, configuredLoosePartCount);
            activeLoosePartCount = Mathf.Clamp(
                configuredActiveLoosePartCount,
                0,
                loosePartCount);
        }

        public bool TryValidate(out string failure)
        {
            if (!string.Equals(featureId, "P1.CAR.001", StringComparison.Ordinal) ||
                !string.Equals(vehicleContentId, "vehicle.satsuma", StringComparison.Ordinal) ||
                stableVehicleId.Length != 32 ||
                !string.Equals(
                    replacementKey,
                    "legacy.vehicle.satsuma.presentation",
                    StringComparison.Ordinal) ||
                sourceSceneSha256.Length != 64 ||
                sourceRootTransformId <= 0L ||
                string.IsNullOrWhiteSpace(sourceHierarchyPath) ||
                sanitizedRendererCount <= 0 ||
                sanitizedColliderCount <= 0 ||
                classification != DonorTransferClassification.TemporaryDirectImport ||
                !IsFinite(defaultWorldPosition))
            {
                failure = "Legacy Satsuma baseline metadata is incomplete.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
