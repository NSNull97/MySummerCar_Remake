using System.Collections;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Activates the inactive authored backend only after GameCompositionRoot
    /// has won session ownership. Keeping it inactive in the scene prevents the
    /// vendor EnviroManager.OnEnable Editor hook from unpacking the source prefab
    /// into committed scene YAML.
    /// </summary>
    [DefaultExecutionOrder(-31500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameCompositionRoot))]
    public sealed class ProductionEnvironmentBackendActivator : MonoBehaviour
    {
        [SerializeField]
        private ProductionEnvironmentBackendMarker backendMarker;

        public ProductionEnvironmentBackendMarker BackendMarker =>
            backendMarker;

        public bool HasValidBinding =>
            backendMarker != null &&
            backendMarker.IsValid &&
            backendMarker.transform.IsChildOf(transform);

        public bool IsBackendAuthoredInactive =>
            backendMarker != null && !backendMarker.gameObject.activeSelf;

        public bool IsBackendRuntimeActive =>
            backendMarker != null &&
            backendMarker.gameObject.activeInHierarchy;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ProductionEnvironmentBackendMarker authoredMarker)
        {
            backendMarker = authoredMarker;
        }
#endif

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            GameCompositionRoot compositionRoot =
                GetComponent<GameCompositionRoot>();
            if (compositionRoot == null || !compositionRoot.IsPrimaryRoot)
            {
                return;
            }

            if (compositionRoot.ReplacedPreviousSession)
            {
                StartCoroutine(ActivateAfterPreviousSessionDestruction());
                return;
            }

            ActivateBackend(compositionRoot);
        }

        private IEnumerator ActivateAfterPreviousSessionDestruction()
        {
            // Enviro keeps a static manager reference. A Single-scene reload
            // destroys the previous persistent root at the end of the load
            // frame, so wait until that reference compares null before enabling
            // the replacement manager.
            yield return null;
            ActivateBackend(GetComponent<GameCompositionRoot>());
        }

        private void ActivateBackend(GameCompositionRoot compositionRoot)
        {
            if (compositionRoot == null || !compositionRoot.IsPrimaryRoot)
            {
                return;
            }

            if (!HasValidBinding || !IsBackendAuthoredInactive)
            {
                Debug.LogError(
                    "Production environment backend activation binding is invalid.",
                    this);
                enabled = false;
                return;
            }

            backendMarker.gameObject.SetActive(true);
        }
    }
}
