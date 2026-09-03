using System;
using System.Collections.Generic;
using MSC.Interaction.Query;
using MSC.LegacyImport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Lighting.Production
{
    /// <summary>
    /// Binds the project-owned electrical state to the eight donor-world switch
    /// buttons used by the Phase 1 home presentation. Donor metadata locates the
    /// removable visual only; the electrical grid remains authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeLightSwitchPresentationAdapter : MonoBehaviour
    {
        private const float OffAngleDegrees = 12f;
        private const float OnAngleDegrees = -12f;
        private static readonly Vector3 MinimumInteractionSize =
            new Vector3(0.28f, 0.34f, 0.16f);

        private static readonly SwitchBinding[] Bindings =
        {
            new SwitchBinding(
                "329462cf877198fa8c4c6e7dd9742128",
                "switch.home.kitchen",
                "LIGHT SWITCH — KITCHEN",
                OffAngleDegrees),
            new SwitchBinding(
                "7cc6d4604bf93dfdeadbff777dc06861",
                "switch.home.hallway",
                "LIGHT SWITCH — HALLWAY",
                OffAngleDegrees),
            new SwitchBinding(
                "0347be03a1a816e7cc67a1806622fdfe",
                "switch.home.hallway",
                "LIGHT SWITCH — ENTRY / HALL",
                OffAngleDegrees),
            new SwitchBinding(
                "6e6ad0dba818168150e0d7d37f7ba017",
                "switch.home.bathroom",
                "LIGHT SWITCH — BATHROOM",
                OffAngleDegrees),
            new SwitchBinding(
                "583a992d864ed65d4e6f3cddf913ae57",
                "switch.home.toilet",
                "LIGHT SWITCH — TOILET",
                OnAngleDegrees),
            new SwitchBinding(
                "4dc23a15e7c3cb8574eed21e49961afa",
                "switch.home.garage",
                "LIGHT SWITCH — GARAGE",
                OffAngleDegrees),
            new SwitchBinding(
                "14b2ac42ffe1be19060bf61d61a959f3",
                "switch.home.bedroom-parents",
                "LIGHT SWITCH — PARENTS BEDROOM",
                OffAngleDegrees),
            new SwitchBinding(
                "62d7bb916f77586ff2ae900fd540c406",
                "switch.home.bedroom-boy",
                "LIGHT SWITCH — BEDROOM",
                OffAngleDegrees)
        };

        private readonly Dictionary<string, LightSwitchInteractionTarget>
            targetsByStableId =
                new Dictionary<string, LightSwitchInteractionTarget>(
                    StringComparer.Ordinal);

        private LightingRuntimeManager lighting;
        private bool initialized;

        public int ExpectedTargetCount => Bindings.Length;
        public int BoundTargetCount
        {
            get
            {
                RemoveDestroyedTargets();
                return targetsByStableId.Count;
            }
        }

        public void Initialize(LightingRuntimeManager configuredLighting)
        {
            if (configuredLighting == null)
            {
                throw new ArgumentNullException(nameof(configuredLighting));
            }

            if (initialized)
            {
                if (!ReferenceEquals(lighting, configuredLighting))
                {
                    throw new InvalidOperationException(
                        "Home switch presentation is already initialized.");
                }

                ScanLoadedScenes();
                return;
            }

            lighting = configuredLighting;
            DisableGeneratedSwitchProxies();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            initialized = true;
            ScanLoadedScenes();
        }

        public bool TryGetTarget(
            string switchId,
            out LightSwitchInteractionTarget target)
        {
            RemoveDestroyedTargets();
            foreach (LightSwitchInteractionTarget candidate in
                     targetsByStableId.Values)
            {
                if (candidate != null && string.Equals(
                        candidate.SwitchId,
                        switchId,
                        StringComparison.Ordinal))
                {
                    target = candidate;
                    return true;
                }
            }

            target = null;
            return false;
        }

        public bool TryGetTargetByStableId(
            string stableId,
            out LightSwitchInteractionTarget target)
        {
            if (targetsByStableId.TryGetValue(stableId, out target) &&
                target != null)
            {
                return true;
            }

            targetsByStableId.Remove(stableId);
            target = null;
            return false;
        }

        public void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            targetsByStableId.Clear();
            lighting = null;
            initialized = false;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindScene(scene);
        }

        private void ScanLoadedScenes()
        {
            RemoveDestroyedTargets();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                BindScene(SceneManager.GetSceneAt(index));
            }
        }

        private void BindScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] entities =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true);
                for (int entityIndex = 0;
                     entityIndex < entities.Length;
                     entityIndex++)
                {
                    DonorWorldBaselineEntityMetadata entity =
                        entities[entityIndex];
                    if (TryResolveBinding(
                            entity.StableId,
                            out SwitchBinding binding))
                    {
                        BindButton(entity.gameObject, binding);
                    }
                }
            }
        }

        private void BindButton(GameObject button, SwitchBinding binding)
        {
            LightSwitchInteractionTarget target =
                button.GetComponent<LightSwitchInteractionTarget>() ??
                button.AddComponent<LightSwitchInteractionTarget>();
            target.InitializeDonorRuntime(
                binding.SwitchId,
                lighting,
                button.transform,
                binding.CapturedSwitchAngleDegrees,
                new Vector3(OffAngleDegrees, 0f, 0f),
                new Vector3(OnAngleDegrees, 0f, 0f),
                binding.DisplayName);

            InteractionTargetHost host =
                button.GetComponent<InteractionTargetHost>() ??
                button.AddComponent<InteractionTargetHost>();
            host.AddCapability(target);
            EnsureInteractionCollider(button);
            targetsByStableId[binding.StableId] = target;
        }

        private void DisableGeneratedSwitchProxies()
        {
            LightSwitchInteractionTarget[] targets =
                GetComponentsInChildren<LightSwitchInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index].HidesProxyRenderers)
                {
                    targets[index].gameObject.SetActive(false);
                }
            }
        }

        private static void EnsureInteractionCollider(GameObject button)
        {
            Bounds localBounds = new Bounds(Vector3.zero,
                MinimumInteractionSize);
            MeshFilter meshFilter = button.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                localBounds = meshFilter.sharedMesh.bounds;
            }
            else
            {
                Renderer renderer = button.GetComponent<Renderer>();
                if (renderer != null)
                {
                    localBounds = renderer.localBounds;
                }
            }

            // Donor switches may already carry a tiny mesh collider. The old
            // early return therefore left the usable ray target approximately
            // the size of the animated rocker. A dedicated box gives every
            // switch the same forgiving interaction envelope without changing
            // the global reach of the interaction system.
            BoxCollider collider = button.GetComponent<BoxCollider>();
            // Unity's destroyed native component can still be a non-null CLR
            // reference, so do not use ?? here.
            if (collider == null)
            {
                collider = button.AddComponent<BoxCollider>();
            }
            collider.isTrigger = false;
            collider.center = localBounds.center;
            collider.size = new Vector3(
                Mathf.Max(localBounds.size.x, MinimumInteractionSize.x),
                Mathf.Max(localBounds.size.y, MinimumInteractionSize.y),
                Mathf.Max(localBounds.size.z, MinimumInteractionSize.z));
        }

        private static bool TryResolveBinding(
            string stableId,
            out SwitchBinding binding)
        {
            for (int index = 0; index < Bindings.Length; index++)
            {
                if (string.Equals(
                        Bindings[index].StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    binding = Bindings[index];
                    return true;
                }
            }

            binding = default;
            return false;
        }

        private void RemoveDestroyedTargets()
        {
            if (targetsByStableId.Count == 0)
            {
                return;
            }

            var missing = new List<string>();
            foreach (KeyValuePair<string, LightSwitchInteractionTarget> pair in
                     targetsByStableId)
            {
                if (pair.Value == null)
                {
                    missing.Add(pair.Key);
                }
            }

            for (int index = 0; index < missing.Count; index++)
            {
                targetsByStableId.Remove(missing[index]);
            }
        }

        private readonly struct SwitchBinding
        {
            public SwitchBinding(
                string stableId,
                string switchId,
                string displayName,
                float capturedSwitchAngleDegrees)
            {
                StableId = stableId;
                SwitchId = switchId;
                DisplayName = displayName;
                CapturedSwitchAngleDegrees = capturedSwitchAngleDegrees;
            }

            public string StableId { get; }
            public string SwitchId { get; }
            public string DisplayName { get; }
            public float CapturedSwitchAngleDegrees { get; }
        }
    }
}
