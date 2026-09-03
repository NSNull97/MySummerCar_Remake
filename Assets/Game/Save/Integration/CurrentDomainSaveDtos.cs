using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Weather.Persistence;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Save.Integration
{
    [Serializable]
    public sealed class WeatherEnvironmentDomainSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentConfigurationId =
            "weather.environment.native.v1";

        public int schemaVersion = CurrentSchemaVersion;
        public string configurationId = CurrentConfigurationId;
        public WeatherDomainSaveDto weatherDomain;
        public int qualityTier = (int)EnvironmentQualityTier.Medium;

        public bool TryValidateEnvelope(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                !string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal) ||
                weatherDomain == null ||
                !Enum.IsDefined(
                    typeof(EnvironmentQualityTier),
                    (EnvironmentQualityTier)qualityTier))
            {
                failure =
                    "Weather environment schema, configuration, or quality tier is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class WorldEntityStateDto
    {
        public string stableEntityId = string.Empty;
        public string sourceCellId = string.Empty;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public bool isKinematic;
        public bool useGravity;
        public bool sleeping;
        public bool activeSelf = true;

        public bool TryValidate(out string failure)
        {
            if (!StableEntityId.TryParse(stableEntityId, out _))
            {
                failure = "World entity has no valid stable ID.";
                return false;
            }

            if (sourceCellId == null ||
                sourceCellId.Length > 128 ||
                sourceCellId.IndexOf('/') >= 0 ||
                sourceCellId.IndexOf('\\') >= 0)
            {
                failure =
                    $"World entity '{stableEntityId}' has an invalid source cell ID.";
                return false;
            }

            if (!IsFinite(worldPosition) ||
                !IsFinite(linearVelocity) ||
                !IsFinite(angularVelocity) ||
                !IsValidRotation(worldRotation) ||
                linearVelocity.sqrMagnitude > 250000f ||
                angularVelocity.sqrMagnitude > 40000f)
            {
                failure = $"World entity '{stableEntityId}' has invalid physics state.";
                return false;
            }

            failure = string.Empty;
            return true;
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
            return magnitudeSquared > 0.000001f && magnitudeSquared < 1000000f;
        }
    }

    [Serializable]
    public sealed class WorldEntityDomainSaveDto
    {
        public const int CurrentSchemaVersion = 2;
        public const string CurrentConfigurationId = "world.entities.native.v2";

        public int schemaVersion = CurrentSchemaVersion;
        public string configurationId = CurrentConfigurationId;
        public WorldEntityStateDto[] entities = Array.Empty<WorldEntityStateDto>();

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                !string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal) ||
                entities == null ||
                entities.Length > SaveLimits.MaximumDeferredEntities)
            {
                failure = "World entity domain schema, configuration, or collection is invalid.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entities.Length; index++)
            {
                WorldEntityStateDto entity = entities[index];
                if (entity == null)
                {
                    failure = $"World entity record {index} is missing.";
                    return false;
                }

                if (!entity.TryValidate(out failure))
                {
                    return false;
                }

                if (!ids.Add(entity.stableEntityId))
                {
                    failure = $"Duplicate world entity ID '{entity.stableEntityId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
