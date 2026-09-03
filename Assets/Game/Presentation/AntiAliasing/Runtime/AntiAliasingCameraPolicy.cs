using UnityEngine;

namespace MSC.Presentation.AntiAliasing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class AntiAliasingCameraPolicy : MonoBehaviour
    {
        [SerializeField] private AntiAliasingCameraRole role = AntiAliasingCameraRole.Auto;
        [SerializeField] private bool overrideMode;
        [SerializeField] private AntiAliasingMode mode = AntiAliasingMode.Smaa;

        public AntiAliasingCameraRole Role => role;
        public bool OverrideMode => overrideMode;
        public AntiAliasingMode Mode => mode;

        public void Configure(
            AntiAliasingCameraRole cameraRole,
            bool useModeOverride = false,
            AntiAliasingMode overrideValue = AntiAliasingMode.Smaa)
        {
            role = cameraRole;
            overrideMode = useModeOverride;
            mode = overrideValue;
            AntiAliasingController.TryRefreshCamera(GetComponent<Camera>());
        }

        private void OnEnable()
        {
            AntiAliasingController.TryRefreshCamera(GetComponent<Camera>());
        }

        private void OnDisable()
        {
            AntiAliasingController.TryReleaseCamera(GetComponent<Camera>());
        }
    }
}
