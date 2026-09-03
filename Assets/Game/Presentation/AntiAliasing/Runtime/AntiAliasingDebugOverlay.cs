using UnityEngine;

namespace MSC.Presentation.AntiAliasing
{
    [DisallowMultipleComponent]
    internal sealed class AntiAliasingDebugOverlay : MonoBehaviour
    {
        private const float Width = 360f;
        private bool expanded;
        private Rect windowRect = new Rect(12f, 12f, Width, 275f);

        private void OnGUI()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            if (!expanded)
            {
                if (GUI.Button(new Rect(12f, 12f, 92f, 24f), "AA debug"))
                {
                    expanded = true;
                }

                return;
            }

            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "MSC Anti-Aliasing");
        }

        private void DrawWindow(int id)
        {
            AntiAliasingController controller = AntiAliasingController.Instance;
            AntiAliasingTelemetry telemetry = controller.GetTelemetry();

            GUILayout.Label($"Preset: {telemetry.Preset}");
            GUILayout.Label($"AA: {telemetry.RequestedMode} -> {telemetry.EffectiveMode}");
            GUILayout.Label(
                $"Upscaler: {telemetry.Upscaler} ({UpscalerQualityText(telemetry)})");
            GUILayout.Label(
                $"Resolution: {telemetry.InternalResolution.x}x{telemetry.InternalResolution.y} -> " +
                $"{telemetry.OutputResolution.x}x{telemetry.OutputResolution.y} ({telemetry.DynamicResolutionScale:0.000})");
            GUILayout.Label($"Motion vectors: {(telemetry.MotionVectors ? "on" : "off")}");
            GUILayout.Label($"Sharpening: {telemetry.Sharpening:0.00}");
            GUILayout.Label($"Managed cameras: {telemetry.ManagedCameraCount}");
            if (!string.IsNullOrEmpty(telemetry.FallbackReason))
            {
                GUILayout.Label($"Fallback: {telemetry.FallbackReason}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Preset"))
            {
                int next = ((int)controller.SelectedPreset + 1) % 4;
                controller.SetPreset((AntiAliasingPreset)next);
            }
            if (GUILayout.Button("Mode"))
            {
                int next = ((int)controller.SelectedMode + 1) % 6;
                controller.SetMode((AntiAliasingMode)next);
            }
            if (GUILayout.Button("Reset history"))
            {
                controller.ResetAllTemporalHistory();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Sharpen", GUILayout.Width(58f));
            float sharpen = GUILayout.HorizontalSlider(
                telemetry.Sharpening,
                0f,
                AntiAliasingController.MaximumSharpening);
            if (!Mathf.Approximately(sharpen, telemetry.Sharpening))
            {
                controller.SetSharpening(sharpen, persist: false);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Close"))
            {
                expanded = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, Width, 22f));
        }

        private static string UpscalerQualityText(AntiAliasingTelemetry telemetry)
        {
            if (telemetry.Upscaler == TemporalUpscalerKind.None)
            {
                return "native";
            }

            switch (telemetry.UpscalerQuality)
            {
                case 0u:
                    return "Performance";
                case 2u:
                    return "Quality";
                case 3u:
                    return "Ultra Performance";
                case 4u:
                    return "DLAA";
                default:
                    return "Balanced";
            }
        }
    }
}
