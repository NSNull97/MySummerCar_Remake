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
        private readonly Dictionary<string, LegacyEntry> entries =
            new Dictionary<string, LegacyEntry>(StringComparer.Ordinal);
        private readonly HashSet<string> activeProductionOverrides =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, HashSet<string>> keysBySceneHandle =
            new Dictionary<int, HashSet<string>>();
        private bool legacyVisibleByDefault = true;

        public int LoadedReplacementCount => entries.Count;
        public int ActiveProductionOverrideCount =>
            activeProductionOverrides.Count;

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

            entry.ApplyLegacyEnabled(
                legacyVisibleByDefault &&
                !productionOverrideActive);
            return true;
        }

        public bool IsProductionOverrideActive(string replacementKey) =>
            !string.IsNullOrWhiteSpace(replacementKey) &&
            activeProductionOverrides.Contains(replacementKey);

        public void SetAllLegacyVisible(bool visible)
        {
            legacyVisibleByDefault = visible;
            foreach (KeyValuePair<string, LegacyEntry> pair in entries)
            {
                pair.Value.ApplyLegacyEnabled(
                    visible &&
                    !activeProductionOverrides.Contains(pair.Key));
            }
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RebuildIndex();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
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

                if (entries.TryGetValue(
                        entity.ReplacementKey,
                        out LegacyEntry existing))
                {
                    if (existing.IsFor(entity))
                    {
                        continue;
                    }

                    Debug.LogError(
                        "Duplicate loaded legacy replacement key: " +
                        entity.ReplacementKey,
                        entity);
                    continue;
                }

                var entry = new LegacyEntry(entity);
                entries.Add(entity.ReplacementKey, entry);
                if (!keysBySceneHandle.TryGetValue(
                        sceneHandle,
                        out HashSet<string> sceneKeys))
                {
                    sceneKeys = new HashSet<string>(
                        StringComparer.Ordinal);
                    keysBySceneHandle.Add(
                        sceneHandle,
                        sceneKeys);
                }

                sceneKeys.Add(entity.ReplacementKey);
                entry.ApplyLegacyEnabled(
                    legacyVisibleByDefault &&
                    !activeProductionOverrides.Contains(
                        entity.ReplacementKey));
            }
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
            private readonly DonorWorldBaselineEntityMetadata metadata;
            private readonly RendererState[] rendererStates;
            private readonly ColliderState[] colliderStates;

            public LegacyEntry(
                DonorWorldBaselineEntityMetadata entity)
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
                DonorWorldBaselineEntityMetadata entity) =>
                metadata == entity;

            public void ApplyLegacyEnabled(bool enabled)
            {
                for (int index = 0; index < rendererStates.Length; index++)
                {
                    rendererStates[index].Apply(enabled);
                }

                for (int index = 0; index < colliderStates.Length; index++)
                {
                    colliderStates[index].Apply(enabled);
                }
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
