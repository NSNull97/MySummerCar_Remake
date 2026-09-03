using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Lighting
{
    [DisallowMultipleComponent]
    public sealed class LightSwitchInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        IInteractionDisplayTarget,
        IPrimaryInteractionOnlyTarget
    {
        [SerializeField] private string switchId = string.Empty;
        [SerializeField] private LightingRuntimeManager lighting;
        [SerializeField] private Transform visualPivot;
        [SerializeField] private Vector3 offLocalEulerAngles;
        [SerializeField] private Vector3 onLocalEulerAngles =
            new Vector3(-12f, 0f, 0f);
        [SerializeField] private string displayName = "LIGHT SWITCH";
        [SerializeField] private string turnOnPrompt = "TURN ON";
        [SerializeField] private string turnOffPrompt = "TURN OFF";
        [SerializeField] private bool hideProxyRenderers = true;

        private ElectricalGridService subscribedGrid;
        private Quaternion capturedMountRotation = Quaternion.identity;
        private bool useCapturedMountRotation;

        public string InteractionDisplayName => displayName;
        public string SwitchId => switchId;
        public Transform VisualPivot => visualPivot;
        public bool HidesProxyRenderers => hideProxyRenderers;
        public float CurrentVisualAngleDegrees
        {
            get
            {
                if (visualPivot == null)
                {
                    return 0f;
                }

                Quaternion relative = useCapturedMountRotation
                    ? Quaternion.Inverse(capturedMountRotation) *
                      visualPivot.localRotation
                    : visualPivot.localRotation;
                return Mathf.DeltaAngle(0f, relative.eulerAngles.x);
            }
        }
        public string InteractionPrompt
        {
            get
            {
                bool isOn = lighting != null &&
                    lighting.Grid != null &&
                    lighting.Grid.TryGetSwitchState(switchId, out bool state) &&
                    state;
                return isOn ? turnOffPrompt : turnOnPrompt;
            }
        }

        private void Awake()
        {
            ApplyProxyPresentationPolicy();
        }

        private void Start()
        {
            TrySubscribeToGrid();
            RefreshVisual();
        }

        private void OnEnable()
        {
            TrySubscribeToGrid();
        }

        private void OnDisable()
        {
            UnsubscribeFromGrid();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGrid();
        }

        private void ApplyProxyPresentationPolicy()
        {
            if (!hideProxyRenderers)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = false;
            }
        }

        public bool CanInteract(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy && lighting != null &&
            lighting.Grid != null && !string.IsNullOrWhiteSpace(switchId);

        public void Interact(in InteractionContext context)
        {
            lighting.ToggleSwitch(switchId, transform.position);
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (visualPivot == null || lighting?.Grid == null)
            {
                return;
            }

            bool isOn = lighting.Grid.TryGetSwitchState(
                switchId,
                out bool state) && state;
            Quaternion switchRotation = Quaternion.Euler(
                isOn ? onLocalEulerAngles : offLocalEulerAngles);
            visualPivot.localRotation = useCapturedMountRotation
                ? capturedMountRotation * switchRotation
                : switchRotation;
        }

        public void InitializeRuntime(
            string configuredSwitchId,
            LightingRuntimeManager configuredLighting,
            Transform configuredVisualPivot,
            Vector3 configuredOffAngles,
            Vector3 configuredOnAngles,
            string configuredDisplayName,
            bool hideRenderers)
        {
            UnsubscribeFromGrid();
            switchId = configuredSwitchId ?? string.Empty;
            lighting = configuredLighting;
            visualPivot = configuredVisualPivot;
            offLocalEulerAngles = configuredOffAngles;
            onLocalEulerAngles = configuredOnAngles;
            displayName = configuredDisplayName ?? "LIGHT SWITCH";
            hideProxyRenderers = hideRenderers;
            ApplyRendererPresentationPolicy();
            TrySubscribeToGrid();
            RefreshVisual();
        }

        /// <summary>
        /// Initializes a donor button whose hierarchy was flattened during the
        /// sanitized world import. Its captured local rotation contains both
        /// the wall mount and the original rocker angle; preserving the mount
        /// prevents wall switches from turning sideways or edge-on.
        /// </summary>
        public void InitializeDonorRuntime(
            string configuredSwitchId,
            LightingRuntimeManager configuredLighting,
            Transform configuredVisualPivot,
            float capturedSwitchAngleDegrees,
            Vector3 configuredOffAngles,
            Vector3 configuredOnAngles,
            string configuredDisplayName)
        {
            if (configuredVisualPivot == null)
            {
                throw new System.ArgumentNullException(
                    nameof(configuredVisualPivot));
            }

            if (!useCapturedMountRotation ||
                visualPivot != configuredVisualPivot)
            {
                capturedMountRotation = configuredVisualPivot.localRotation *
                    Quaternion.Inverse(Quaternion.Euler(
                        capturedSwitchAngleDegrees,
                        0f,
                        0f));
                useCapturedMountRotation = true;
            }
            InitializeRuntime(
                configuredSwitchId,
                configuredLighting,
                configuredVisualPivot,
                configuredOffAngles,
                configuredOnAngles,
                configuredDisplayName,
                false);
        }

        private void ApplyRendererPresentationPolicy()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = !hideProxyRenderers;
            }
        }

        private void TrySubscribeToGrid()
        {
            ElectricalGridService candidate = lighting != null
                ? lighting.Grid
                : null;
            if (candidate == null || ReferenceEquals(candidate, subscribedGrid))
            {
                return;
            }

            UnsubscribeFromGrid();
            subscribedGrid = candidate;
            subscribedGrid.StateChanged += HandleElectricalStateChanged;
        }

        private void UnsubscribeFromGrid()
        {
            if (subscribedGrid == null)
            {
                return;
            }

            subscribedGrid.StateChanged -= HandleElectricalStateChanged;
            subscribedGrid = null;
        }

        private void HandleElectricalStateChanged(ElectricalStateChanged change)
        {
            if (string.Equals(change.StateId, switchId,
                    System.StringComparison.Ordinal))
            {
                RefreshVisual();
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredSwitchId,
            LightingRuntimeManager configuredLighting,
            Transform configuredVisualPivot,
            Vector3 configuredOffAngles,
            Vector3 configuredOnAngles,
            string configuredDisplayName,
            bool hideGeneratedProxyRenderers = true)
        {
            switchId = configuredSwitchId;
            lighting = configuredLighting;
            visualPivot = configuredVisualPivot;
            offLocalEulerAngles = configuredOffAngles;
            onLocalEulerAngles = configuredOnAngles;
            displayName = configuredDisplayName;
            hideProxyRenderers = hideGeneratedProxyRenderers;
            ApplyRendererPresentationPolicy();
        }
#endif
    }
}
