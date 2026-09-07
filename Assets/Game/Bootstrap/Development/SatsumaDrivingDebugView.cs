using MSC.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Bootstrap.Development
{
    /// <summary>
    /// Opt-in development view only. Reuses the existing HDRP camera, listener,
    /// input and driver session; never moves the player or the physical car.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public sealed class SatsumaDrivingDebugView : MonoBehaviour
    {
        private SatsumaDrivingSessionController session;
        private PlayerInputRouter input;
        private Camera view;
        private Rigidbody body;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private bool cameraOverridden;
        private float frameSeconds;
        private ProfilerRecorder mainThread;
        private string overlay = string.Empty;
        private float nextOverlayTime;

        public int ViewMode { get; private set; }
        public bool IsCameraOverridden => cameraOverridden;

        public void Initialize(SatsumaDrivingSessionController drivingSession,
            PlayerInputRouter playerInput, Camera camera, Rigidbody chassis)
        {
            session = drivingSession; input = playerInput; view = camera; body = chassis;
            enabled = Application.isEditor || Debug.isDebugBuild;
        }

        public void SetViewMode(int mode)
        {
            RestoreCameraPose();
            ViewMode = enabled && session != null && session.IsDriving ? Mathf.Clamp(mode, 0, 3) : 0;
            if (ViewMode == 0) mainThread.Dispose();
            else if (!mainThread.Valid)
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        }

        // Called before the driver's authoritative eye-anchor calculation.
        // The temporary camera offset must never feed back into player pose.
        public void RestoreCameraPose()
        {
            if (!cameraOverridden) return;
            if (view != null) view.transform.SetLocalPositionAndRotation(originalLocalPosition, originalLocalRotation);
            cameraOverridden = false;
        }

        private void Update()
        {
            RestoreCameraPose();
            if (session == null || !session.IsDriving) { if (ViewMode != 0) SetViewMode(0); return; }
            if (input != null && input.IsGameplayInputEnabled && Time.timeScale > 0f &&
                Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
                SetViewMode((ViewMode + 1) % 4);
        }

        // Called explicitly after the session's LateUpdate eye anchoring.
        // Update runs early so gameplay rays never see yesterday's debug pose.
        public void ApplyCameraPose()
        {
            if (ViewMode == 0 || session == null || !session.IsDriving || view == null || body == null) return;
            RestoreCameraPose();
            originalLocalPosition = view.transform.localPosition;
            originalLocalRotation = view.transform.localRotation;
            Vector3 offset = ViewMode == 1 ? new Vector3(0, 2.2f, -5.5f) :
                new Vector3(ViewMode == 2 ? -4f : 4f, 1.2f, .2f);
            Vector3 position = body.transform.TransformPoint(offset);
            Vector3 target = body.transform.TransformPoint(new Vector3(0, .05f, .2f));
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            cameraOverridden = true;
            frameSeconds = Mathf.Lerp(frameSeconds, Time.unscaledDeltaTime, .1f);
            if (Time.unscaledTime < nextOverlayTime) return;
            nextOverlayTime = Time.unscaledTime + .2f;
            float fps = frameSeconds > 0 ? 1f / frameSeconds : 0;
            string cpu = mainThread.Valid ? (mainThread.LastValue * .000001).ToString("F1") + " ms" : "unavailable";
            overlay = "DEV ROAD VIEW / F9: rear → left → right → cabin\n" +
                $"Speed {body.linearVelocity.magnitude * 3.6f:F1} km/h   Tilt {Vector3.Angle(body.transform.up, Vector3.up):F1}°\n" +
                $"Frame {frameSeconds * 1000:F1} ms / {fps:F0} FPS   Main {cpu}\n" +
                $"Angular {body.angularVelocity.magnitude:F2} rad/s   CCD {body.collisionDetectionMode}\n" +
                "Camera only; no force, pose, input or save override.";
        }

        private void OnGUI()
        {
            if (ViewMode != 0 && session != null && session.IsDriving)
                GUI.Box(new Rect(Mathf.Max(12, Screen.width - 510), 12, 498, 108), overlay);
        }

        private void OnDisable() { RestoreCameraPose(); ViewMode = 0; mainThread.Dispose(); }
        private void OnDestroy() { RestoreCameraPose(); mainThread.Dispose(); }
    }
}
