using System;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Interaction.Carrying
{
    /// <summary>
    /// Domain-owned snapshot consumed by a future versioned save aggregate. Resolution by stable ID belongs to Save.
    /// </summary>
    [Serializable]
    public sealed class CarriedObjectSaveState
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField]
        private bool hasCarriedObject;

        [SerializeField]
        private string stableEntityId = string.Empty;

        [SerializeField]
        private Vector3 anchorLocalPosition;

        [SerializeField]
        private Quaternion anchorLocalRotation = Quaternion.identity;

        public int SchemaVersion => schemaVersion;

        public bool HasCarriedObject => hasCarriedObject;

        public string StableEntityId => stableEntityId;

        public Vector3 AnchorLocalPosition => anchorLocalPosition;

        public Quaternion AnchorLocalRotation => anchorLocalRotation;

        public bool IsValid => TryValidate(out _);

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported carried-object schema {schemaVersion}.";
                return false;
            }

            if (!hasCarriedObject)
            {
                failure = string.Empty;
                return true;
            }

            if (!MSC.Core.Identity.StableEntityId.TryParse(stableEntityId, out _))
            {
                failure = "Carried object has no valid stable entity ID.";
                return false;
            }

            if (!IsFinite(anchorLocalPosition) ||
                anchorLocalPosition.sqrMagnitude > 2500f)
            {
                failure = "Carried-object anchor position is invalid.";
                return false;
            }

            if (!IsValidRotation(anchorLocalRotation))
            {
                failure = "Carried-object anchor rotation is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static CarriedObjectSaveState Empty()
        {
            return new CarriedObjectSaveState();
        }

        public static CarriedObjectSaveState Create(
            StableEntityId entityId,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            if (!entityId.IsValid)
            {
                throw new ArgumentException("A valid stable entity ID is required.", nameof(entityId));
            }

            return new CarriedObjectSaveState
            {
                hasCarriedObject = true,
                stableEntityId = entityId.Value,
                anchorLocalPosition = localPosition,
                anchorLocalRotation = Normalize(localRotation)
            };
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsValidRotation(Quaternion value)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) ||
                !float.IsFinite(value.w))
            {
                return false;
            }

            float magnitudeSquared = value.x * value.x + value.y * value.y +
                                     value.z * value.z + value.w * value.w;
            return magnitudeSquared > 0.000001f &&
                   magnitudeSquared < 1000000f;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            if (!IsValidRotation(value))
            {
                throw new ArgumentException(
                    "A finite, non-zero rotation is required.",
                    nameof(value));
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }
    }
}
