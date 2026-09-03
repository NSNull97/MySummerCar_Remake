using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Characters
{
    [Serializable]
    public struct CharacterPresentationCatalogEntry
    {
        [SerializeField] private string bindingId;
        [SerializeField] private string productionReplacementKey;
        [SerializeField] private GameObject wrapperPrefab;

        public CharacterPresentationCatalogEntry(
            string configuredBindingId,
            string configuredReplacementKey,
            GameObject configuredWrapperPrefab)
        {
            bindingId = configuredBindingId ?? string.Empty;
            productionReplacementKey = configuredReplacementKey ?? string.Empty;
            wrapperPrefab = configuredWrapperPrefab;
        }

        public string BindingId => bindingId;
        public string ProductionReplacementKey => productionReplacementKey;
        public GameObject WrapperPrefab => wrapperPrefab;
    }

    [CreateAssetMenu(
        fileName = "CharacterPresentationCatalog",
        menuName = "MSC/Characters/Legacy Presentation Catalog")]
    public sealed class CharacterPresentationCatalog : ScriptableObject
    {
        [SerializeField] private CharacterPresentationCatalogEntry[] entries =
            Array.Empty<CharacterPresentationCatalogEntry>();

        public IReadOnlyList<CharacterPresentationCatalogEntry> Entries =>
            entries ?? Array.Empty<CharacterPresentationCatalogEntry>();

        public bool TryGet(
            string bindingId,
            out CharacterPresentationCatalogEntry entry)
        {
            CharacterPresentationCatalogEntry[] configured =
                entries ?? Array.Empty<CharacterPresentationCatalogEntry>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index].BindingId,
                        bindingId,
                        StringComparison.Ordinal))
                {
                    entry = configured[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var failures = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterPresentationCatalogEntry entry in Entries)
            {
                string failure = string.Empty;
                if (!CharacterStableId.TryValidate(
                        entry.BindingId,
                        "presentation.character.",
                        out failure) ||
                    !ids.Add(entry.BindingId))
                {
                    failures.Add(string.IsNullOrEmpty(failure)
                        ? $"Duplicate character presentation ID '{entry.BindingId}'."
                        : failure);
                    continue;
                }

                if (entry.WrapperPrefab == null)
                {
                    failures.Add(
                        $"Character presentation '{entry.BindingId}' has no wrapper prefab.");
                    continue;
                }

                LegacyCharacterPresentationBinding binding =
                    entry.WrapperPrefab.GetComponent<
                        LegacyCharacterPresentationBinding>();
                if (binding == null)
                {
                    failures.Add(
                        $"Character presentation '{entry.BindingId}' has no project-owned wrapper component.");
                    continue;
                }

                if (!string.Equals(
                        binding.BindingId,
                        entry.BindingId,
                        StringComparison.Ordinal) ||
                    !binding.TryValidate(out failure))
                {
                    failures.Add(
                        $"Character presentation '{entry.BindingId}' is invalid: {failure}");
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            IEnumerable<CharacterPresentationCatalogEntry> configuredEntries)
        {
            entries = (configuredEntries ??
                    Enumerable.Empty<CharacterPresentationCatalogEntry>())
                .OrderBy(entry => entry.BindingId, StringComparer.Ordinal)
                .ToArray();
        }
#endif
    }
}
