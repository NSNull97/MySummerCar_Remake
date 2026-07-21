using System;
using System.Collections;
using System.IO;
using MSC.UI.Runtime.Routing;
using UnityEngine;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Captures the six locked Milestone 08A screens and the six separately
    /// identified ReferencePending screens from a development player.
    /// The probe is never compiled into a non-development player and does not read
    /// approved reference images; it writes implementation-only evidence.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class Ui08AReviewCaptureProbe : MonoBehaviour
    {
        private const string CaptureArgument = "-msc-ui08a-capture";
        private const int CaptureWidth = 1672;
        private const int CaptureHeight = 941;

        private static readonly CaptureStep[] Steps =
        {
            new CaptureStep(UiRouteId.MainMenu, "MainMenu_Implementation.png"),
            new CaptureStep(UiRouteId.SettingsGraphics, "Graphics_Implementation.png"),
            new CaptureStep(UiRouteId.SettingsAudio, "Audio_Implementation.png"),
            new CaptureStep(UiRouteId.SettingsControls, "Controls_Implementation.png"),
            new CaptureStep(UiRouteId.SettingsGameplay, "Gameplay_Implementation.png"),
            new CaptureStep(UiRouteId.Loading, "Loading_ReferencePending.png"),
            new CaptureStep(UiRouteId.Pause, "Pause_ReferencePending.png"),
            new CaptureStep(UiRouteId.ConfirmationDialog, "ConfirmationDialog_ReferencePending.png"),
            new CaptureStep(UiRouteId.SaveStatus, "SaveStatus_ReferencePending.png"),
            new CaptureStep(UiRouteId.SettingsAccessibility, "Accessibility_ReferencePending.png"),
            new CaptureStep(UiRouteId.SettingsMods, "Mods_ReferencePending.png"),
            new CaptureStep(UiRouteId.InGameHud, "HUD_Implementation.png"),
        };

        private GameUiRoot root;
        private string outputDirectory;

        public static void TryAttach(GameUiRoot root)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (root == null || !TryReadOutputDirectory(out string directory))
            {
                return;
            }

            Ui08AReviewCaptureProbe probe =
                root.gameObject.AddComponent<Ui08AReviewCaptureProbe>();
            probe.root = root;
            probe.outputDirectory = directory;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);

            float deadline = Time.realtimeSinceStartup + 45f;
            while ((Screen.width != CaptureWidth || Screen.height != CaptureHeight) &&
                   Time.realtimeSinceStartup < deadline)
            {
                Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
                yield return null;
            }

            if (Screen.width != CaptureWidth || Screen.height != CaptureHeight)
            {
                FailAndQuit(
                    $"M08A capture backbuffer is {Screen.width}x{Screen.height}; " +
                    $"expected {CaptureWidth}x{CaptureHeight}.");
                yield break;
            }

            deadline = Time.realtimeSinceStartup + 180f;
            while (!root.IsWorldReadyForCapture && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!root.IsWorldReadyForCapture)
            {
                FailAndQuit("M08A production world/UI did not become ready before the capture timeout.");
                yield break;
            }

            for (int worldSettleFrame = 0; worldSettleFrame < 30; worldSettleFrame++)
            {
                yield return null;
            }

            root.SetReviewDataEnabled(true);
            for (int index = 0; index < Steps.Length; index++)
            {
                CaptureStep step = Steps[index];
                string outputPath = Path.Combine(outputDirectory, step.FileName);
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                if (UiRouteCatalog.Get(step.Route).HasLockedVisualReference)
                {
                    root.ShowReviewScreen(step.Route);
                }
                else
                {
                    root.ShowReferencePendingReviewScreen(step.Route);
                }
                for (int settleFrame = 0; settleFrame < 8; settleFrame++)
                {
                    yield return new WaitForEndOfFrame();
                }

                ScreenCapture.CaptureScreenshot(outputPath, 1);
                float writeDeadline = Time.realtimeSinceStartup + 30f;
                while ((!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0L) &&
                       Time.realtimeSinceStartup < writeDeadline)
                {
                    yield return null;
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0L)
                {
                    FailAndQuit("M08A capture was not written: " + outputPath);
                    yield break;
                }

                Debug.Log("M08A implementation capture: " + outputPath, root);
            }

            Debug.Log("M08A implementation capture set complete: " + outputDirectory, root);
            yield return null;
            Quit(0);
        }

        private static bool TryReadOutputDirectory(out string outputDirectory)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], CaptureArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                outputDirectory = Path.GetFullPath(arguments[index + 1]);
                return true;
            }

            outputDirectory = string.Empty;
            return false;
        }

        private static void FailAndQuit(string message)
        {
            Debug.LogError(message);
            Quit(2);
        }

        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit(exitCode);
#endif
        }
#endif

        private readonly struct CaptureStep
        {
            public CaptureStep(UiRouteId route, string fileName)
            {
                Route = route;
                FileName = fileName;
            }

            public UiRouteId Route { get; }

            public string FileName { get; }
        }
    }
}
