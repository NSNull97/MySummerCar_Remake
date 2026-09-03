using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Project-owned replacement switch for temporary legacy renderers and
    /// colliders. Production overrides address entries by ReplacementKey and
    /// never by donor hierarchy names or Unity instance IDs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldLegacyReplacementRegistry : MonoBehaviour
    {
        internal static DonorWorldLegacyReplacementRegistry Active { get; private set; }
        internal static event Action ActiveChanged;
        private readonly Dictionary<string, LegacyEntry> entries =
            new Dictionary<string, LegacyEntry>(StringComparer.Ordinal);
        private readonly HashSet<string> activeProductionOverrides =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeRendererOverrides =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, HashSet<string>> keysBySceneHandle =
            new Dictionary<int, HashSet<string>>();
        private bool legacyVisibleByDefault = true;

        public int LoadedReplacementCount => entries.Count;
        public int ActiveProductionOverrideCount =>
            activeProductionOverrides.Count;
        public int ActiveRendererOverrideCount =>
            activeRendererOverrides.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            ActiveChanged = null;
        }

        public bool SetProductionOverrideActive(
            string replacementKey,
            bool productionOverrideActive)
        {
            if (string.IsNullOrWhiteSpace(replacementKey))
            {
                throw new ArgumentException(
                    "Replacement key must not be empty.",
                    nameof(replacementKey));
            }

            if (productionOverrideActive)
            {
                activeProductionOverrides.Add(replacementKey);
            }
            else
            {
                activeProductionOverrides.Remove(replacementKey);
            }

            if (!entries.TryGetValue(
                    replacementKey,
                    out LegacyEntry entry))
            {
                return false;
            }

            ApplyEntryState(replacementKey, entry);
            return true;
        }

        public bool IsProductionOverrideActive(string replacementKey) =>
            !string.IsNullOrWhiteSpace(replacementKey) &&
            activeProductionOverrides.Contains(replacementKey);

        /// <summary>
        /// Hides only donor renderers while retaining the donor collider state.
        /// This is used when a production visual replaces exact legacy collision
        /// that cannot safely be approximated by the new presentation prefab.
        /// </summary>
        public bool SetProductionRendererOverrideActive(
            string replacementKey,
            bool productionRendererOverrideActive)
        {
            if (string.IsNullOrWhiteSpace(replacementKey))
            {
                throw new ArgumentException(
                    "Replacement key must not be empty.",
                    nameof(replacementKey));
            }

            if (productionRendererOverrideActive)
                activeRendererOverrides.Add(replacementKey);
            else
                activeRendererOverrides.Remove(replacementKey);

            if (!entries.TryGetValue(replacementKey, out LegacyEntry entry))
                return false;
            ApplyEntryState(replacementKey, entry);
            return true;
        }

        public bool IsProductionRendererOverrideActive(
            string replacementKey) =>
            !string.IsNullOrWhiteSpace(replacementKey) &&
            activeRendererOverrides.Contains(replacementKey);

        public void SetAllLegacyVisible(bool visible)
        {
            legacyVisibleByDefault = visible;
            foreach (KeyValuePair<string, LegacyEntry> pair in entries)
                ApplyEntryState(pair.Key, pair.Value);
        }

        private void Awake()
        {
            Active = this;
            ActiveChanged?.Invoke();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RebuildIndex();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (Active == this)
            {
                Active = null;
                ActiveChanged?.Invoke();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IndexScene(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            RemoveScene(scene.handle);
        }

        private void RebuildIndex()
        {
            entries.Clear();
            keysBySceneHandle.Clear();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded)
                {
                    IndexScene(scene);
                }
            }
        }

        private void IndexScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] metadata =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(
                        includeInactive: true);
                IndexMetadata(scene.handle, metadata);
                DonorWorldSupplementalEntityMetadata[] supplemental =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldSupplementalEntityMetadata>(
                        includeInactive: true);
                IndexMetadata(scene.handle, supplemental);
            }
        }

        private void IndexMetadata(
            int sceneHandle,
            IReadOnlyList<DonorWorldSupplementalEntityMetadata> metadata)
        {
            for (int index = 0; index < metadata.Count; index++)
            {
                DonorWorldSupplementalEntityMetadata entity = metadata[index];
                if (entity == null ||
                    string.IsNullOrWhiteSpace(entity.ReplacementKey))
                {
                    continue;
                }

                IndexEntity(
                    sceneHandle,
                    entity.ReplacementKey,
                    entity);
            }
        }

        private void IndexMetadata(
            int sceneHandle,
            IReadOnlyList<DonorWorldBaselineEntityMetadata> metadata)
        {
            for (int index = 0; index < metadata.Count; index++)
            {
                DonorWorldBaselineEntityMetadata entity = metadata[index];
                if (entity == null ||
                    string.IsNullOrWhiteSpace(entity.ReplacementKey))
                {
                    continue;
                }

                IndexEntity(sceneHandle, entity.ReplacementKey, entity);
            }
        }

        private void IndexEntity(
            int sceneHandle,
            string replacementKey,
            Component entity)
        {
            if (entries.TryGetValue(
                    replacementKey,
                    out LegacyEntry existing))
            {
                if (existing.IsFor(entity))
                {
                    return;
                }

                Debug.LogError(
                    "Duplicate loaded legacy replacement key: " +
                    replacementKey,
                    entity);
                return;
            }

            var entry = new LegacyEntry(entity);
            entries.Add(replacementKey, entry);
            if (!keysBySceneHandle.TryGetValue(
                    sceneHandle,
                    out HashSet<string> sceneKeys))
            {
                sceneKeys = new HashSet<string>(StringComparer.Ordinal);
                keysBySceneHandle.Add(sceneHandle, sceneKeys);
            }

            sceneKeys.Add(replacementKey);
            ApplyEntryState(replacementKey, entry);
        }

        private void ApplyEntryState(string replacementKey, LegacyEntry entry)
        {
            bool fullOverride = activeProductionOverrides.Contains(
                replacementKey);
            bool rendererOverride = activeRendererOverrides.Contains(
                replacementKey);
            entry.ApplyRenderers(legacyVisibleByDefault && !fullOverride &&
                !rendererOverride);
            entry.ApplyColliders(legacyVisibleByDefault && !fullOverride);
        }

        private void RemoveScene(int sceneHandle)
        {
            if (!keysBySceneHandle.TryGetValue(
                    sceneHandle,
                    out HashSet<string> sceneKeys))
            {
                return;
            }

            foreach (string key in sceneKeys)
            {
                entries.Remove(key);
            }

            keysBySceneHandle.Remove(sceneHandle);
        }

        private sealed class LegacyEntry
        {
            private readonly Component metadata;
            private readonly RendererState[] rendererStates;
            private readonly ColliderState[] colliderStates;

            public LegacyEntry(
                Component entity)
            {
                metadata = entity;
                Renderer[] renderers =
                    entity.GetComponentsInChildren<Renderer>(
                        includeInactive: true);
                rendererStates = new RendererState[renderers.Length];
                for (int index = 0; index < renderers.Length; index++)
                {
                    rendererStates[index] =
                        new RendererState(
                            renderers[index],
                            renderers[index].enabled);
                }

                Collider[] colliders =
                    entity.GetComponentsInChildren<Collider>(
                        includeInactive: true);
                colliderStates = new ColliderState[colliders.Length];
                for (int index = 0; index < colliders.Length; index++)
                {
                    colliderStates[index] =
                        new ColliderState(
                            colliders[index],
                            colliders[index].enabled);
                }
            }

            public bool IsFor(
                Component entity) =>
                metadata == entity;

            public void ApplyRenderers(bool enabled)
            {
                for (int index = 0; index < rendererStates.Length; index++)
                    rendererStates[index].Apply(enabled);
            }

            public void ApplyColliders(bool enabled)
            {
                for (int index = 0; index < colliderStates.Length; index++)
                    colliderStates[index].Apply(enabled);
            }
        }

        private readonly struct RendererState
        {
            private readonly Renderer renderer;
            private readonly bool initiallyEnabled;

            public RendererState(Renderer value, bool enabled)
            {
                renderer = value;
                initiallyEnabled = enabled;
            }

            public void Apply(bool legacyEnabled)
            {
                if (renderer != null)
                {
                    renderer.enabled =
                        legacyEnabled && initiallyEnabled;
                }
            }
        }

        private readonly struct ColliderState
        {
            private readonly Collider collider;
            private readonly bool initiallyEnabled;

            public ColliderState(Collider value, bool enabled)
            {
                collider = value;
                initiallyEnabled = enabled;
            }

            public void Apply(bool legacyEnabled)
            {
                if (collider != null)
                {
                    collider.enabled =
                        legacyEnabled && initiallyEnabled;
                }
            }
        }
    }
}
