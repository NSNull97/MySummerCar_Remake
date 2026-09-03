using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Characters
{
    [Serializable]
    public struct CharacterFlagVisibilityBinding
    {
        [SerializeField] private string flagId;
        [SerializeField] private GameObject presentationRoot;
        [SerializeField] private bool visibleWhenSet;

        public CharacterFlagVisibilityBinding(
            string configuredFlagId,
            GameObject configuredPresentationRoot,
            bool visibleWhenSet)
        {
            flagId = configuredFlagId ?? string.Empty;
            presentationRoot = configuredPresentationRoot;
            this.visibleWhenSet = visibleWhenSet;
        }

        public string FlagId => flagId;
        public GameObject PresentationRoot => presentationRoot;
        public bool VisibleWhenSet => visibleWhenSet;
    }

    /// <summary>
    /// Presentation-only visibility driven by project-owned character flags.
    /// It keeps conditional donor props out of hierarchy-name lookups.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterFlagPresentationBinding : MonoBehaviour
    {
        [SerializeField] private CharacterFlagVisibilityBinding[] bindings =
            Array.Empty<CharacterFlagVisibilityBinding>();

        public IReadOnlyList<CharacterFlagVisibilityBinding> Bindings =>
            bindings ?? Array.Empty<CharacterFlagVisibilityBinding>();

        public bool TryValidate(out string failure)
        {
            CharacterFlagVisibilityBinding[] configured = bindings ??
                Array.Empty<CharacterFlagVisibilityBinding>();
            if (configured.Length == 0)
            {
                failure = "Conditional character presentation has no bindings.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterFlagVisibilityBinding binding in configured)
            {
                if (!CharacterStableId.TryValidate(
                        binding.FlagId,
                        "flag.",
                        out failure) ||
                    binding.PresentationRoot == null)
                {
                    failure = string.IsNullOrWhiteSpace(failure)
                        ? $"Conditional character prop '{binding.FlagId}' has no presentation root."
                        : failure;
                    return false;
                }

                if (!ids.Add(binding.FlagId))
                {
                    failure =
                        $"Conditional character presentation duplicates flag '{binding.FlagId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public void Apply(CharacterInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            foreach (CharacterFlagVisibilityBinding binding in
                     bindings ?? Array.Empty<CharacterFlagVisibilityBinding>())
            {
                if (binding.PresentationRoot != null)
                {
                    bool isSet = instance.GetFlag(binding.FlagId);
                    binding.PresentationRoot.SetActive(
                        isSet == binding.VisibleWhenSet);
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            IEnumerable<CharacterFlagVisibilityBinding> configuredBindings)
        {
            bindings = (configuredBindings ??
                    Enumerable.Empty<CharacterFlagVisibilityBinding>())
                .ToArray();
        }
#endif
    }
}
