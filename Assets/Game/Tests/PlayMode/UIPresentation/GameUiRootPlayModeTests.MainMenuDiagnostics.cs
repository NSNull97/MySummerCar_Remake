using System;
using System.Collections;
using MSC.Presentation.AntiAliasing;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuDiagnosticSuppression_RestoresOtherScreensAndHonoursIndependentOwner()
        {
            AntiAliasingController controller = AntiAliasingController.Instance;
            Assert.That(controller.IsDebugOverlaySuppressed, Is.False);
            Type overlayType = typeof(AntiAliasingController).Assembly.GetType(
                "MSC.Presentation.AntiAliasing.AntiAliasingDebugOverlay", throwOnError: true);
            var overlay = (Behaviour)controller.GetComponent(overlayType);
            Assert.That(overlay, Is.Not.Null, "The existing development overlay must be owned by the AA controller.");
            bool originallyEnabled = overlay.enabled;
            UiFixture fixture = default;
            IDisposable otherOwner = null;
            try
            {
                overlay.enabled = true;
                fixture = CreateFixture(startInMainMenu: true);
                yield return null;
                Assert.That(controller.IsDebugOverlaySuppressed, Is.True);
                Assert.That(overlay.enabled, Is.False,
                    "MainMenu must stop the IMGUI component, not only hide its drawing.");
                AntiAliasingPreset preset = controller.SelectedPreset;
                AntiAliasingMode mode = controller.SelectedMode;

                otherOwner = controller.SuppressDebugOverlay();
                FindRequired(fixture.Root.transform, "Settings").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGraphics));
                Assert.That(controller.IsDebugOverlaySuppressed, Is.True,
                    "Leaving MainMenu must not release another owner's suppression.");
                Assert.That(overlay.enabled, Is.False);
                otherOwner.Dispose();
                otherOwner.Dispose();
                otherOwner = null;
                Assert.That(controller.IsDebugOverlaySuppressed, Is.False,
                    "The settings screen restores the original debug-overlay policy.");
                Assert.That(overlay.enabled, Is.True,
                    "The last scope restores the enabled state captured by the first scope.");

                Transform settingsRoute = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
                FindRequired(settingsRoute, "Back").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(controller.IsDebugOverlaySuppressed, Is.True);
                Assert.That(overlay.enabled, Is.False);
                Assert.That(controller.SelectedPreset, Is.EqualTo(preset));
                Assert.That(controller.SelectedMode, Is.EqualTo(mode));
                yield return DestroyFixture(fixture);
                Assert.That(controller.IsDebugOverlaySuppressed, Is.False,
                    "Destroying an active MainMenu must release its transient scope.");
                Assert.That(overlay.enabled, Is.True);

                overlay.enabled = false;
                using (controller.SuppressDebugOverlay())
                {
                    Assert.That(controller.IsDebugOverlaySuppressed, Is.True);
                    Assert.That(overlay.enabled, Is.False);
                }
                Assert.That(controller.IsDebugOverlaySuppressed, Is.False);
                Assert.That(overlay.enabled, Is.False,
                    "An overlay disabled before suppression must remain disabled after the final scope.");
            }
            finally
            {
                otherOwner?.Dispose();
                if (fixture.Root != null) UnityEngine.Object.DestroyImmediate(fixture.Root.gameObject);
                if (fixture.GameplayRoot != null) UnityEngine.Object.DestroyImmediate(fixture.GameplayRoot);
                if (fixture.PlayerActions != null) UnityEngine.Object.DestroyImmediate(fixture.PlayerActions);
                if (fixture.VehicleActions != null) UnityEngine.Object.DestroyImmediate(fixture.VehicleActions);
                if (overlay != null) overlay.enabled = originallyEnabled;
            }
        }
    }
}
