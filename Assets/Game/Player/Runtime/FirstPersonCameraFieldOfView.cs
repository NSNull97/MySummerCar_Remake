using MSC.Core.Lifecycle;
using UnityEngine;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FirstPersonCameraFieldOfView :
        MonoBehaviour,
        IPlayerCameraSettingsSink
    {
        private const float MinimumAspect = 0.01f;
        private const float MinimumHorizontalFieldOfViewDegrees = 30f;
        private const float MaximumHorizontalFieldOfViewDegrees = 160f;
        private const float MinimumClipPlaneSeparationMeters = 0.01f;

        [SerializeField]
        private Camera targetCamera;

        [SerializeField, Range(30f, 160f)]
        private float horizontalFieldOfViewDegrees = 120f;

        [SerializeField, Range(0.1f, 1f)]
        private float zoomMultiplier = 0.5f;

        [SerializeField, Min(0f)]
        private float zoomTransitionSeconds = 0.08f;

        private bool zoomRequested;
        private float currentHorizontalFieldOfView;

        public float HorizontalFieldOfViewDegrees =>
            horizontalFieldOfViewDegrees;

        public float FarClipPlaneMeters =>
            targetCamera == null ? 0f : targetCamera.farClipPlane;

        public bool IsZoomRequested => zoomRequested;

        public void Configure(
            Camera camera,
            float horizontalFieldOfView,
            float configuredZoomMultiplier,
            float transitionSeconds)
        {
            targetCamera = camera;
            horizontalFieldOfViewDegrees = Mathf.Clamp(
                horizontalFieldOfView,
                MinimumHorizontalFieldOfViewDegrees,
                MaximumHorizontalFieldOfViewDegrees);
            zoomMultiplier = Mathf.Clamp(configuredZoomMultiplier, 0.1f, 1f);
            zoomTransitionSeconds = Mathf.Max(0f, transitionSeconds);
            currentHorizontalFieldOfView = horizontalFieldOfViewDegrees;
            ApplyFieldOfView();
        }

        public void ApplyCameraSettings(
            float configuredHorizontalFieldOfViewDegrees,
            float farClipPlaneMeters)
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            horizontalFieldOfViewDegrees = Mathf.Clamp(
                configuredHorizontalFieldOfViewDegrees,
                MinimumHorizontalFieldOfViewDegrees,
                MaximumHorizontalFieldOfViewDegrees);
            if (targetCamera != null)
            {
                targetCamera.farClipPlane = Mathf.Max(
                    targetCamera.nearClipPlane +
                    MinimumClipPlaneSeparationMeters,
                    farClipPlaneMeters);
            }

            currentHorizontalFieldOfView = zoomRequested
                ? horizontalFieldOfViewDegrees * zoomMultiplier
                : horizontalFieldOfViewDegrees;
            ApplyFieldOfView();
        }

        public void SetZoomRequested(bool requested)
        {
            zoomRequested = requested;
        }

        public static float HorizontalToVerticalDegrees(
            float horizontalDegrees,
            float aspect)
        {
            float safeHorizontal = Mathf.Clamp(horizontalDegrees, 1f, 179f);
            float safeAspect = Mathf.Max(MinimumAspect, aspect);
            return Camera.HorizontalToVerticalFieldOfView(
                safeHorizontal,
                safeAspect);
        }

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            currentHorizontalFieldOfView = horizontalFieldOfViewDegrees;
            ApplyFieldOfView();
        }

        private void OnEnable()
        {
            currentHorizontalFieldOfView = horizontalFieldOfViewDegrees;
            ApplyFieldOfView();
        }

        private void OnDisable()
        {
            zoomRequested = false;
            currentHorizontalFieldOfView = horizontalFieldOfViewDegrees;
            ApplyFieldOfView();
        }

        private void LateUpdate()
        {
            float targetHorizontalFieldOfView = zoomRequested
                ? horizontalFieldOfViewDegrees * zoomMultiplier
                : horizontalFieldOfViewDegrees;
            if (zoomTransitionSeconds <= 0f)
            {
                currentHorizontalFieldOfView = targetHorizontalFieldOfView;
            }
            else
            {
                float transitionSpeed =
                    horizontalFieldOfViewDegrees *
                    (1f - zoomMultiplier) /
                    zoomTransitionSeconds;
                currentHorizontalFieldOfView = Mathf.MoveTowards(
                    currentHorizontalFieldOfView,
                    targetHorizontalFieldOfView,
                    transitionSpeed * Time.unscaledDeltaTime);
            }

            ApplyFieldOfView();
        }

        private void ApplyFieldOfView()
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.fieldOfView = HorizontalToVerticalDegrees(
                currentHorizontalFieldOfView,
                targetCamera.aspect);
        }
    }
}
