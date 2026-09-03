using System;
using MSC.Presentation.AntiAliasing;
using MSC.UI.Runtime.Settings;
using UnityEngine;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Applies project-owned graphics settings to HDRP cameras without making
    /// the persisted UI document depend on NVIDIA or HDRP types.
    /// </summary>
    internal sealed class HdrpDlssRuntimeAdapter : IDisposable
    {
        private bool disposed;

        public static bool IsHardwareSupported =>
            AntiAliasingCapabilities.Evaluate().DlssAvailable;

        public void Apply(GraphicsSettingsDto settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            AntiAliasingController controller = AntiAliasingController.Instance;
            if (settings.AntiAliasingPreset == UiAntiAliasingPreset.Custom)
            {
                controller.SetMode(
                    ToRuntimeMode(settings.AntiAliasingMode),
                    persist: false);
                controller.SetSharpening(
                    settings.AntiAliasingSharpening,
                    persist: false);
            }
            else
            {
                controller.SetPreset(
                    ToRuntimePreset(settings.AntiAliasingPreset),
                    persist: false);
            }

            bool useDlss =
                controller.SelectedMode == AntiAliasingMode.TemporalUpscaler;
            controller.SetExternalDlssRequest(
                useDlss,
                ToNvidiaQuality(settings.DlssQuality));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        private static AntiAliasingMode ToRuntimeMode(
            UiAntiAliasingMode value)
        {
            switch (value)
            {
                case UiAntiAliasingMode.Off:
                    return AntiAliasingMode.Off;
                case UiAntiAliasingMode.Fxaa:
                    return AntiAliasingMode.Fxaa;
                case UiAntiAliasingMode.Smaa:
                    return AntiAliasingMode.Smaa;
                case UiAntiAliasingMode.TemporalUpscaler:
                    return AntiAliasingMode.TemporalUpscaler;
                case UiAntiAliasingMode.MaximumQuality:
                    return AntiAliasingMode.MaximumQuality;
                default:
                    return AntiAliasingMode.Taa;
            }
        }

        private static AntiAliasingPreset ToRuntimePreset(
            UiAntiAliasingPreset value)
        {
            switch (value)
            {
                case UiAntiAliasingPreset.Low:
                    return AntiAliasingPreset.Low;
                case UiAntiAliasingPreset.Medium:
                    return AntiAliasingPreset.Medium;
                case UiAntiAliasingPreset.Ultra:
                    return AntiAliasingPreset.Ultra;
                default:
                    return AntiAliasingPreset.High;
            }
        }

        private static uint ToNvidiaQuality(UiDlssQuality value)
        {
            switch (value)
            {
                case UiDlssQuality.Quality:
                    return 2u;
                case UiDlssQuality.Performance:
                    return 0u;
                case UiDlssQuality.UltraPerformance:
                    return 3u;
                case UiDlssQuality.Dlaa:
                    return 4u;
                default:
                    return 1u;
            }
        }
    }
}
