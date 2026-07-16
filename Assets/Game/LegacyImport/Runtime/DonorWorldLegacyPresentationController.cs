using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Event-driven owner of the active legacy material presentation mode.
    /// Place it on the persistent Bootstrap composition root so global and
    /// additive cell scenes receive the selected mode as they load or reload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldLegacyPresentationController :
        MonoBehaviour
    {
        [SerializeField] private DonorWorldLegacyPresentationMode mode =
            DonorWorldLegacyPresentationMode.LegacyTextured;

        public DonorWorldLegacyPresentationMode Mode => mode;
        public int LastAppliedBindingCount { get; private set; }
        public int LastRejectedBindingCount { get; private set; }

        public void SetMode(DonorWorldLegacyPresentationMode value)
        {
            if (!Enum.IsDefined(
                    typeof(DonorWorldLegacyPresentationMode),
                    value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unsupported donor world presentation mode.");
            }

            mode = value;
            ApplyToLoadedScenes();
        }

        public int ApplyToLoadedScenes()
        {
            int appliedCount = 0;
            int rejectedCount = 0;

            for (int index = 0;
                 index < SceneManager.sceneCount;
                 index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                ApplyToScene(
                    scene,
                    ref appliedCount,
                    ref rejectedCount);
            }

            LastAppliedBindingCount = appliedCount;
            LastRejectedBindingCount = rejectedCount;
            return appliedCount;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyToLoadedScenes();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadMode)
        {
            int appliedCount = 0;
            int rejectedCount = 0;
            ApplyToScene(
                scene,
                ref appliedCount,
                ref rejectedCount);
            LastAppliedBindingCount = appliedCount;
            LastRejectedBindingCount = rejectedCount;
        }

        private void ApplyToScene(
            Scene scene,
            ref int appliedCount,
            ref int rejectedCount)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0;
                 rootIndex < roots.Length;
                 rootIndex++)
            {
                DonorWorldLegacyMaterialBinding[] bindings =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldLegacyMaterialBinding>(
                        includeInactive: true);
                for (int bindingIndex = 0;
                     bindingIndex < bindings.Length;
                     bindingIndex++)
                {
                    DonorWorldLegacyMaterialBinding binding =
                        bindings[bindingIndex];
                    if (binding != null && binding.Apply(mode))
                    {
                        appliedCount++;
                    }
                    else
                    {
                        rejectedCount++;
                    }
                }
            }
        }
    }
}
