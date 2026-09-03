using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Items
{
    [Serializable]
    public sealed class ItemRuntimeSaveRecord
    {
        public ItemInstanceState state;
        public bool isCanonicalPlacement;
        public string sourceCellId = string.Empty;
        public Vector3 materializationPosition;
        public Quaternion materializationRotation = Quaternion.identity;

        public ItemRuntimeSaveRecord DeepClone()
        {
            return new ItemRuntimeSaveRecord
            {
                state = state?.DeepClone(),
                isCanonicalPlacement = isCanonicalPlacement,
                sourceCellId = sourceCellId,
                materializationPosition = materializationPosition,
                materializationRotation = materializationRotation,
            };
        }

        public bool TryValidate(
            ItemDefinitionCatalog definitions,
            out string failure)
        {
            failure = string.Empty;
            if (definitions == null || state == null ||
                !definitions.TryGet(
                    state.definitionId,
                    out ItemDefinitionRecord definition) ||
                !state.TryValidate(definition, out failure) ||
                sourceCellId == null || sourceCellId.Length > 128 ||
                sourceCellId.IndexOf('/') >= 0 ||
                sourceCellId.IndexOf('\\') >= 0 ||
                !IsFinite(materializationPosition) ||
                !IsFinite(materializationRotation))
            {
                failure ??= "Item runtime save record is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

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

    [Serializable]
    public sealed class ItemDomainSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentConfigurationId = "items.instances.native.v1";

        public int schemaVersion = CurrentSchemaVersion;
        public string configurationId = CurrentConfigurationId;
        public ItemRuntimeSaveRecord[] instances =
            Array.Empty<ItemRuntimeSaveRecord>();

        public ItemDomainSaveDto DeepClone()
        {
            var copy = new ItemRuntimeSaveRecord[instances?.Length ?? 0];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = instances[index]?.DeepClone();
            }

            return new ItemDomainSaveDto
            {
                schemaVersion = schemaVersion,
                configurationId = configurationId,
                instances = copy,
            };
        }

        public bool TryValidate(
            ItemDefinitionCatalog definitions,
            out string failure)
        {
            failure = string.Empty;
            if (schemaVersion != CurrentSchemaVersion ||
                !string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal) ||
                instances == null || instances.Length > 4096)
            {
                failure = "Item save domain schema or collection is invalid.";
                return false;
            }

            var topLevelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemRuntimeSaveRecord instance in instances)
            {
                if (instance == null ||
                    !instance.TryValidate(definitions, out failure) ||
                    !topLevelIds.Add(instance.state.stableEntityId))
                {
                    failure ??= "Item save domain contains a duplicate identity.";
                    return false;
                }
            }

            var allIds = new HashSet<string>(
                topLevelIds,
                StringComparer.Ordinal);
            foreach (ItemRuntimeSaveRecord instance in instances)
            {
                foreach (string containedId in
                         instance.state.containedStableIds)
                {
                    if (!allIds.Add(containedId))
                    {
                        failure =
                            $"Contained item identity '{containedId}' is also " +
                            "top-level or owned by more than one container.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        public static ItemDomainSaveDto Empty() => new ItemDomainSaveDto();
    }
}
