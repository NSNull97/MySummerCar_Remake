using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Reference-counted presentation ownership for a generated production
    /// override. Multiple streamed pieces may address one aggregate donor
    /// renderer without re-enabling it while another replacement stays live.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldLegacyReplacementOverride : MonoBehaviour
    {
        [SerializeField] private string[] replacementKeys = Array.Empty<string>();

        private static readonly Dictionary<string, int> ActiveKeyReferences =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> registeredKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> ReplacementKeys =>
            replacementKeys ?? Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveKeyReferences.Clear();
        }

        private void OnEnable()
        {
            DonorWorldLegacyReplacementRegistry.ActiveChanged +=
                ReapplyActiveOverrides;
            RegisterKeys();
        }

        private void OnDisable()
        {
            DonorWorldLegacyReplacementRegistry.ActiveChanged -=
                ReapplyActiveOverrides;
            UnregisterKeys();
        }

        private void RegisterKeys()
        {
            string[] configured = replacementKeys ?? Array.Empty<string>();
            for (int index = 0; index < configured.Length; index++)
            {
                string key = configured[index]?.Trim();
                if (string.IsNullOrEmpty(key) || !registeredKeys.Add(key))
                {
                    continue;
                }

                ActiveKeyReferences.TryGetValue(key, out int count);
                ActiveKeyReferences[key] = count + 1;
                DonorWorldLegacyReplacementRegistry.Active?
                    .SetProductionOverrideActive(key, true);
            }
        }

        private void UnregisterKeys()
        {
            foreach (string key in registeredKeys)
            {
                if (!ActiveKeyReferences.TryGetValue(key, out int count) ||
                    count <= 1)
                {
                    ActiveKeyReferences.Remove(key);
                    DonorWorldLegacyReplacementRegistry.Active?
                        .SetProductionOverrideActive(key, false);
                }
                else
                {
                    ActiveKeyReferences[key] = count - 1;
                }
            }
            registeredKeys.Clear();
        }

        private static void ReapplyActiveOverrides()
        {
            DonorWorldLegacyReplacementRegistry registry =
                DonorWorldLegacyReplacementRegistry.Active;
            if (registry == null) return;
            foreach (KeyValuePair<string, int> pair in ActiveKeyReferences)
            {
                if (pair.Value > 0)
                {
                    registry.SetProductionOverrideActive(pair.Key, true);
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(IEnumerable<string> configuredKeys)
        {
            if (configuredKeys == null)
            {
                replacementKeys = Array.Empty<string>();
                return;
            }

            var unique = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in configuredKeys)
            {
                if (!string.IsNullOrWhiteSpace(key)) unique.Add(key.Trim());
            }
            replacementKeys = new string[unique.Count];
            unique.CopyTo(replacementKeys);
        }
#endif
    }
}
