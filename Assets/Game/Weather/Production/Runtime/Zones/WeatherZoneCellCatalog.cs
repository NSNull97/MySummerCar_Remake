using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.Production
{
    [Serializable]
    public sealed class WeatherZoneCellDefinition
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField] private Vector3 worldCenter;
        [SerializeField] private Quaternion worldRotation = Quaternion.identity;
        [SerializeField] private Vector3 sizeMeters = Vector3.one;
        [SerializeField] private int priority = 200;
        [SerializeField] private bool createFogVoid = true;
        [SerializeField] private WeatherZoneProfile profile;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public string ScenePath => scenePath;
        public Vector3 WorldCenter => worldCenter;
        public Quaternion WorldRotation => worldRotation;
        public Vector3 SizeMeters => sizeMeters;
        public int Priority => priority;
        public bool CreateFogVoid => createFogVoid;
        public WeatherZoneProfile Profile => profile;

        public bool MatchesScene(string loadedScenePath) =>
            !string.IsNullOrWhiteSpace(loadedScenePath) &&
            string.Equals(
                NormalizePath(scenePath),
                NormalizePath(loadedScenePath),
                StringComparison.OrdinalIgnoreCase);

        public void Validate(ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                issues.Add("A streamed weather-zone definition has no stable ID.");
            }

            if (string.IsNullOrWhiteSpace(scenePath) ||
                !NormalizePath(scenePath).StartsWith(
                    "Assets/",
                    StringComparison.OrdinalIgnoreCase) ||
                !scenePath.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(stableId + ": scene path must be an Assets/*.unity path.");
            }

            if (profile == null)
            {
                issues.Add(stableId + ": weather-zone profile is missing.");
            }

            if (!IsFinite(worldCenter) || !IsFinite(worldRotation))
            {
                issues.Add(stableId + ": transform contains a non-finite value.");
            }

            if (!IsFinite(sizeMeters) || sizeMeters.x <= 0f ||
                sizeMeters.y <= 0f || sizeMeters.z <= 0f)
            {
                issues.Add(stableId + ": zone size must be finite and positive.");
            }

            if (createFogVoid && profile != null &&
                profile.Kind != WeatherZoneKind.ClosedInterior &&
                profile.Kind != WeatherZoneKind.VehicleCabin)
            {
                issues.Add(
                    stableId +
                    ": fog void is only supported for a closed interior or vehicle cabin.");
            }
        }

#if UNITY_EDITOR
        public static WeatherZoneCellDefinition CreateForAuthoring(
            string authoredStableId,
            string authoredDisplayName,
            string authoredScenePath,
            Vector3 authoredWorldCenter,
            Quaternion authoredWorldRotation,
            Vector3 authoredSizeMeters,
            int authoredPriority,
            bool authoredCreateFogVoid,
            WeatherZoneProfile authoredProfile) =>
            new WeatherZoneCellDefinition
            {
                stableId = authoredStableId,
                displayName = authoredDisplayName,
                scenePath = NormalizePath(authoredScenePath),
                worldCenter = authoredWorldCenter,
                worldRotation = authoredWorldRotation.normalized,
                sizeMeters = authoredSizeMeters,
                priority = authoredPriority,
                createFogVoid = authoredCreateFogVoid,
                profile = authoredProfile,
            };
#endif

        private static string NormalizePath(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\\', '/').Trim();

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
    }

    [CreateAssetMenu(
        fileName = "WeatherZoneCellCatalog",
        menuName = "MSC Remake/Environment/Weather Zone Cell Catalog")]
    public sealed class WeatherZoneCellCatalog : ScriptableObject
    {
        [SerializeField] private WeatherZoneCellDefinition[] definitions =
            Array.Empty<WeatherZoneCellDefinition>();

        public IReadOnlyList<WeatherZoneCellDefinition> Definitions => definitions;

        public int CopyDefinitionsForScene(
            string scenePath,
            ICollection<WeatherZoneCellDefinition> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = 0;
            for (int index = 0; index < definitions.Length; index++)
            {
                WeatherZoneCellDefinition definition = definitions[index];
                if (definition != null && definition.MatchesScene(scenePath))
                {
                    destination.Add(definition);
                    count++;
                }
            }

            return count;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var issues = new List<string>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            if (definitions == null || definitions.Length == 0)
            {
                issues.Add("The streamed weather-zone catalog is empty.");
                return issues;
            }

            for (int index = 0; index < definitions.Length; index++)
            {
                WeatherZoneCellDefinition definition = definitions[index];
                if (definition == null)
                {
                    issues.Add("The catalog contains a null definition.");
                    continue;
                }

                definition.Validate(issues);
                if (!stableIds.Add(definition.StableId))
                {
                    issues.Add(
                        "Duplicate streamed weather-zone stable ID: " +
                        definition.StableId + ".");
                }
            }

            return issues;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            params WeatherZoneCellDefinition[] authoredDefinitions)
        {
            definitions = authoredDefinitions ??
                          Array.Empty<WeatherZoneCellDefinition>();
        }
#endif
    }
}
