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

        public bool IsValid =>
            schemaVersion == CurrentSchemaVersion &&
            (!hasCarriedObject || MSC.Core.Identity.StableEntityId.TryParse(stableEntityId, out _));

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
                anchorLocalRotation = localRotation
            };
        }
    }
}
