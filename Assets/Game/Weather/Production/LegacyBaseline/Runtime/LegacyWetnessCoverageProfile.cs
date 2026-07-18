using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.Production.LegacyBaseline
{
    public enum LegacyWetnessSurfaceCategory
    {
        Ground = 0,
        Road = 1,
        Exterior = 2,
        Vegetation = 3
    }

    [Serializable]
    public struct LegacyWetnessMaterialCoverageEntry
    {
        [SerializeField] private string sourceMaterialGuid;
        [SerializeField] private LegacyWetnessSurfaceCategory category;

        public LegacyWetnessMaterialCoverageEntry(
            string sourceMaterialGuid,
            LegacyWetnessSurfaceCategory category)
        {
            this.sourceMaterialGuid = sourceMaterialGuid;
            this.category = category;
        }

        public string SourceMaterialGuid => sourceMaterialGuid;
        public LegacyWetnessSurfaceCategory Category => category;
    }

    /// <summary>
    /// Explicit project-owned allowlist for temporary legacy material wetness.
    /// Shader compatibility alone never opts a donor-derived material into the
    /// production effect; each source material GUID needs a reviewed category.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LegacyWetnessCoverageProfile",
        menuName = "MSC/Weather/Legacy Wetness Coverage Profile")]
    public sealed class LegacyWetnessCoverageProfile : ScriptableObject
    {
        [SerializeField]
        private LegacyWetnessMaterialCoverageEntry[] entries =
            Array.Empty<LegacyWetnessMaterialCoverageEntry>();

        private readonly Dictionary<
            string,
            LegacyWetnessSurfaceCategory> categoriesByGuid =
                new Dictionary<string, LegacyWetnessSurfaceCategory>(
                    StringComparer.OrdinalIgnoreCase);
        private bool lookupInitialized;
        private string validationFailure = string.Empty;

        public int EntryCount => entries?.Length ?? 0;

        public bool IsValid
        {
            get
            {
                EnsureLookup();
                return string.IsNullOrEmpty(validationFailure);
            }
        }

        public bool TryGetCategory(
            string sourceMaterialGuid,
            out LegacyWetnessSurfaceCategory category)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(validationFailure) ||
                string.IsNullOrWhiteSpace(sourceMaterialGuid))
            {
                category = default;
                return false;
            }

            return categoriesByGuid.TryGetValue(
                sourceMaterialGuid,
                out category);
        }

        public bool TryValidate(out string failureReason)
        {
            EnsureLookup();
            failureReason = validationFailure;
            return string.IsNullOrEmpty(failureReason);
        }

        public void Configure(
            IReadOnlyList<LegacyWetnessMaterialCoverageEntry>
                authoredEntries)
        {
            if (authoredEntries == null)
            {
                throw new ArgumentNullException(nameof(authoredEntries));
            }

            entries = new LegacyWetnessMaterialCoverageEntry[
                authoredEntries.Count];
            for (int index = 0; index < authoredEntries.Count; index++)
            {
                entries[index] = authoredEntries[index];
            }

            InvalidateLookup();
            if (!TryValidate(out string failureReason))
            {
                throw new ArgumentException(
                    failureReason,
                    nameof(authoredEntries));
            }
        }

        private void OnEnable()
        {
            InvalidateLookup();
        }

        private void OnValidate()
        {
            InvalidateLookup();
        }

        private void EnsureLookup()
        {
            if (lookupInitialized)
            {
                return;
            }

            lookupInitialized = true;
            validationFailure = string.Empty;
            categoriesByGuid.Clear();
            if (entries == null)
            {
                validationFailure = "Coverage entries array is null.";
                return;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                LegacyWetnessMaterialCoverageEntry entry = entries[index];
                if (!IsSourceMaterialGuid(entry.SourceMaterialGuid))
                {
                    validationFailure =
                        "Coverage source material GUID is invalid at index " +
                        index + ".";
                    categoriesByGuid.Clear();
                    return;
                }

                if (!Enum.IsDefined(
                        typeof(LegacyWetnessSurfaceCategory),
                        entry.Category))
                {
                    validationFailure =
                        "Coverage category is invalid at index " +
                        index + ".";
                    categoriesByGuid.Clear();
                    return;
                }

                if (!categoriesByGuid.TryAdd(
                        entry.SourceMaterialGuid,
                        entry.Category))
                {
                    validationFailure =
                        "Duplicate coverage source material GUID: " +
                        entry.SourceMaterialGuid + ".";
                    categoriesByGuid.Clear();
                    return;
                }
            }
        }

        private void InvalidateLookup()
        {
            lookupInitialized = false;
            validationFailure = string.Empty;
            categoriesByGuid.Clear();
        }

        private static bool IsSourceMaterialGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!Uri.IsHexDigit(value[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
