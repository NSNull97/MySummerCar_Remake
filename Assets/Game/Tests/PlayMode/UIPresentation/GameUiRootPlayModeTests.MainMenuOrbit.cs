#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuOrbit_ClampsFramesAndResetsCameraWithoutMovingGeometry()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Artifacts/MainMenuRedesign/Captures/Orbit"));
            Directory.CreateDirectory(directory);
            using (var resolution = new MainMenuGameViewResolutionScope())
            {
                UiFixture fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab());
                try
                {
                    // This gate drives the renderer API directly. The separate
                    // mouse gate below owns real UI input; live physical input
                    // here could legitimately request another orbit/paint frame
                    // between our clamp call and the idle-render assertion.
                    EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled = false;
                    MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                    yield return AwaitMenuVehicleFrame(preview, 1);
                    Vector3 modelPosition = preview.Model.transform.position;
                    Quaternion modelRotation = preview.Model.transform.rotation;
                    Vector3 environmentPosition = preview.Environment.transform.position;
                    Quaternion environmentRotation = preview.Environment.transform.rotation;
                    var inputs = new[] { new Vector2(10f, 10f), new Vector2(-10f, 10f),
                        new Vector2(10f, -10f), new Vector2(-10f, -10f) };
                    var expected = new[]
                    {
                        new Vector2(MainMenuVehiclePreview.MinOrbitYaw, MainMenuVehiclePreview.MinOrbitPitch),
                        new Vector2(MainMenuVehiclePreview.MaxOrbitYaw, MainMenuVehiclePreview.MinOrbitPitch),
                        new Vector2(MainMenuVehiclePreview.MinOrbitYaw, MainMenuVehiclePreview.MaxOrbitPitch),
                        new Vector2(MainMenuVehiclePreview.MaxOrbitYaw, MainMenuVehiclePreview.MaxOrbitPitch)
                    };
                    var sizes = new[] { new Vector2Int(1920, 1200), new Vector2Int(3840, 1080) };
                    foreach (Vector2Int size in sizes)
                    {
                        yield return ResizeMenuCaptureViewport(resolution, preview, size);
                        preview.ResetOrbit();
                        yield return null;
                        Vector3 defaultPosition = preview.PreviewCamera.transform.position;
                        Quaternion defaultRotation = preview.PreviewCamera.transform.rotation;
                        Vector3 vehicleCenter = preview.Model.transform.TransformPoint(preview.Model.LocalBounds.center);
                        float defaultRadius = Vector3.Distance(defaultPosition, vehicleCenter);
                        if (size.x == 1920)
                            yield return CaptureMenuOrbitNative(directory, "Default-1920x1200");
                        for (int index = 0; index < inputs.Length; index++)
                        {
                            int nextFrame = preview.RenderCount + 1;
                            preview.Orbit(inputs[index]);
                            Assert.That(preview.OrbitAngles, Is.EqualTo(expected[index]));
                            yield return AwaitMenuVehicleFrame(preview, nextFrame);
                            AssertMenuVehicleInsideFullViewport(preview);
                            AssertMenuOrbitCarSlot(preview);
                            AssertMenuDisplaysRenderedEnvironment(fixture, preview);
                            AssertMenuVehicleOnAuthoredGround(preview);
                            Assert.That(preview.PreviewCamera.enabled, Is.False,
                                "Orbit must retain the bounded render-request architecture.");
                            Assert.That(Quaternion.Angle(defaultRotation, preview.PreviewCamera.transform.rotation),
                                Is.GreaterThan(1f), "The actual preview camera must rotate.");
                            Assert.That(Vector3.Distance(preview.PreviewCamera.transform.position, vehicleCenter),
                                Is.EqualTo(defaultRadius).Within(0.002f), "Dragging must not pump the camera zoom.");
                            Assert.That(preview.Model.transform.position, Is.EqualTo(modelPosition));
                            Assert.That(preview.Model.transform.rotation, Is.EqualTo(modelRotation));
                            Assert.That(preview.Environment.transform.position, Is.EqualTo(environmentPosition));
                            Assert.That(preview.Environment.transform.rotation, Is.EqualTo(environmentRotation));
                            int settledCount = preview.RenderCount;
                            preview.Orbit(inputs[index]);
                            yield return null;
                            yield return null;
                            Assert.That(preview.OrbitAngles, Is.EqualTo(expected[index]),
                                "A saturated outward delta must leave the clamp angles unchanged.");
                            Assert.That(preview.RenderCount, Is.EqualTo(settledCount),
                                "Repeated outward movement at the clamp must not render or blur again.");
                            if (size.x == 1920)
                                yield return CaptureMenuOrbitNative(directory, "Corner" + index + "-1920x1200");
                        }
                        int resetFrame = preview.RenderCount + 1;
                        preview.ResetOrbit();
                        yield return AwaitMenuVehicleFrame(preview, resetFrame);
                        Assert.That(preview.OrbitAngles, Is.EqualTo(Vector2.zero));
                        Assert.That(Vector3.Distance(preview.PreviewCamera.transform.position, defaultPosition),
                            Is.LessThan(0.002f));
                        Assert.That(Quaternion.Angle(preview.PreviewCamera.transform.rotation, defaultRotation),
                            Is.LessThan(0.001f));
                    }

                    preview.Orbit(new Vector2(0.1f, 0.02f));
                    yield return AwaitMenuVehicleFrame(preview, preview.RenderCount + 1);
                    Vector2 beforeHide = preview.OrbitAngles;
                    int beforeHideCount = preview.RenderCount;
                    preview.SetVisible(false);
                    preview.Orbit(new Vector2(10f, 10f));
                    preview.ResetOrbit();
                    yield return null;
                    yield return null;
                    Assert.That(preview.OrbitAngles, Is.EqualTo(beforeHide));
                    Assert.That(preview.RenderCount, Is.EqualTo(beforeHideCount));
                    preview.SetVisible(true);
                    yield return AwaitMenuVehicleFrame(preview, beforeHideCount + 1);
                    int settled = preview.RenderCount;
                    preview.Orbit(new Vector2(float.NaN, 0f));
                    preview.Orbit(new Vector2(0f, float.PositiveInfinity));
                    yield return null;
                    yield return null;
                    Assert.That(preview.OrbitAngles, Is.EqualTo(beforeHide));
                    Assert.That(preview.RenderCount, Is.EqualTo(settled));
                    File.WriteAllText(Path.Combine(directory, "ORBIT_CONTEXT.txt"),
                        "Executed isolated native UI capture. Not user visual approval.\n" +
                        "Default plus four yaw/pitch clamp corners at 1920x1200; all eight vehicle bounds corners\n" +
                        "also checked at 3840x1080. Camera orbit leaves model/environment transforms unchanged.\n" +
                        "This direct renderer gate disables only its fixture UI input module; actual mouse input is tested separately.\n" +
                        "No player save or settings used. Captured UTC: " + DateTime.UtcNow.ToString("O") + "\n");
                    yield return DestroyFixture(fixture);
                    yield return null;
                }
                finally { DestroyFailedMenuInputFixture(fixture); }
            }
        }

        [UnityTest]
        public IEnumerator MainMenuOrbit_RealMouseDragHonoursControlsModalReleaseAndFocusLoss()
        {
            InputSettings.UpdateMode oldMode = InputSystem.settings.updateMode;
            InputSettings.BackgroundBehavior oldBackground = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse oldMouse = Mouse.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            UiFixture fixture = default;
            InputActionAsset actions = null;
            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices = null;
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab());
                actions = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;
                oldDevices = actions.devices;
                actions.devices = new InputDevice[] { mouse };
                fixture.PlayerActions.devices = Array.Empty<InputDevice>();
                MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                yield return AwaitMenuVehicleFrame(preview, 1);
                yield return new WaitForSecondsRealtime(0.15f);
                PrepareMenuPointerChecks(fixture);
                MainMenuOrbitDrag adapter = FindRequired(fixture.Root.transform, "StaticMenuBackdrop")
                    .GetComponent<MainMenuOrbitDrag>();
                Assert.That(adapter, Is.Not.Null);
                // The automated Editor can be unfocused. Only this callback is
                // simulated; every pointer movement/button uses the real module.
                adapter.OnApplicationFocus(true);
                Vector2 start = new Vector2(Screen.width * 0.51f, Screen.height * 0.42f);
                Vector2 end = start + new Vector2(Screen.height * 0.08f, Screen.height * 0.03f);
                AssertMenuOrbitRaycast(start, adapter.gameObject);
                AssertMenuOrbitRaycast(end, adapter.gameObject);
                int nextFrame = preview.RenderCount + 1;
                yield return SendMenuMouseState(mouse, new MouseState { position = start });
                yield return SendMenuMouseState(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = end, delta = end - start }.WithButton(MouseButton.Left));
                Assert.That(adapter.IsDragging, Is.True);
                Assert.That(preview.OrbitAngles.sqrMagnitude, Is.GreaterThan(1f));
                yield return AwaitMenuVehicleFrame(preview, nextFrame);
                Vector2 afterDrag = preview.OrbitAngles;
                yield return SendMenuMouseState(mouse, new MouseState { position = end });
                Assert.That(adapter.IsDragging, Is.False);
                int settledCount = preview.RenderCount;
                yield return SendMenuMouseState(mouse, new MouseState { position = start, delta = start - end });
                Assert.That(preview.OrbitAngles, Is.EqualTo(afterDrag));
                Assert.That(preview.RenderCount, Is.EqualTo(settledCount));

                foreach (string control in new[] { "Credits", "Colour3", "CarColourCard" })
                {
                    Transform target = FindRequired(fixture.Root.transform, control);
                    Vector2 controlPoint = MenuPointerPosition(target);
                    if (control == "CarColourCard")
                    {
                        var rect = (RectTransform)target;
                        controlPoint = RectTransformUtility.WorldToScreenPoint(null,
                            rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMax - 12f)));
                    }
                    yield return DragMenuOrbitMouse(mouse, controlPoint, start, MouseButton.Left);
                    Assert.That(preview.OrbitAngles, Is.EqualTo(afterDrag), control + " must own its pointer area.");
                    Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                }
                yield return DragMenuOrbitMouse(mouse, start, end, MouseButton.Right);
                Assert.That(preview.OrbitAngles, Is.EqualTo(afterDrag));

                // Starting on free space then crossing a control also cancels
                // ownership; returning with LMB held cannot resume the orbit.
                Vector2 creditsPoint = MenuPointerPosition(FindRequired(fixture.Root.transform, "Credits"));
                yield return SendMenuMouseState(mouse, new MouseState { position = start });
                yield return SendMenuMouseState(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = creditsPoint, delta = creditsPoint - start }.WithButton(MouseButton.Left));
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = end, delta = end - creditsPoint }.WithButton(MouseButton.Left));
                Assert.That(preview.OrbitAngles, Is.EqualTo(afterDrag));
                yield return SendMenuMouseState(mouse, new MouseState { position = end });

                FindRequired(fixture.Root.transform, "Credits").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(FindRequired(fixture.Root.transform, "CreditsOverlay").gameObject.activeInHierarchy, Is.True);
                yield return DragMenuOrbitMouse(mouse, start, end, MouseButton.Left);
                Assert.That(preview.OrbitAngles, Is.EqualTo(afterDrag), "A modal must block background orbit.");
                FindRequired(fixture.Root.transform, "CreditsBack").GetComponent<Button>().onClick.Invoke();
                yield return null;
                adapter.OnApplicationFocus(true);

                yield return SendMenuMouseState(mouse, new MouseState { position = start });
                yield return SendMenuMouseState(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = end, delta = end - start }.WithButton(MouseButton.Left));
                Assert.That(adapter.IsDragging, Is.True);
                Vector2 beforeFocusLoss = preview.OrbitAngles;
                adapter.OnApplicationFocus(false);
                Assert.That(adapter.IsDragging, Is.False);
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = start, delta = start - end }.WithButton(MouseButton.Left));
                adapter.OnApplicationFocus(true);
                yield return SendMenuMouseState(mouse,
                    new MouseState { position = end, delta = end - start }.WithButton(MouseButton.Left));
                Assert.That(preview.OrbitAngles, Is.EqualTo(beforeFocusLoss),
                    "Focus restore must require a new LMB press, not resume a stale drag.");
                yield return SendMenuMouseState(mouse, new MouseState { position = end });
                // A real double click on free background returns to the authored view.
                yield return new WaitForSecondsRealtime(0.4f);
                for (int click = 0; click < 2; click++)
                {
                    yield return SendMenuMouseState(mouse,
                        new MouseState { position = end }.WithButton(MouseButton.Left));
                    yield return SendMenuMouseState(mouse, new MouseState { position = end });
                }
                Assert.That(preview.OrbitAngles, Is.EqualTo(Vector2.zero));
                yield return DestroyFixture(fixture);
                yield return null;
            }
            finally
            {
                if (actions != null) actions.devices = oldDevices;
                DestroyFailedMenuInputFixture(fixture);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
                InputSystem.settings.updateMode = oldMode;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldRouting;
            }
        }

        private static IEnumerator DragMenuOrbitMouse(Mouse mouse, Vector2 from, Vector2 to, MouseButton button)
        {
            yield return SendMenuMouseState(mouse, new MouseState { position = from });
            yield return SendMenuMouseState(mouse, new MouseState { position = from }.WithButton(button));
            yield return SendMenuMouseState(mouse, new MouseState { position = to, delta = to - from }.WithButton(button));
            yield return SendMenuMouseState(mouse, new MouseState { position = to });
        }

        private static void AssertMenuOrbitRaycast(Vector2 position, GameObject expected)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.EqualTo(expected), "The test must start on actual free background.");
        }

        private static void AssertMenuOrbitCarSlot(MainMenuVehiclePreview preview)
        {
            Bounds bounds = preview.Model.LocalBounds;
            float tolerance = 2f / Mathf.Min(preview.OutputTexture.width, preview.OutputTexture.height);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 world = preview.Model.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                Vector3 screen = preview.PreviewCamera.WorldToViewportPoint(world);
                Assert.That(screen.x, Is.InRange(0.13f - tolerance, 0.62f + tolerance),
                    "Orbit must keep the complete car in its left-side presentation slot, away from main actions.");
                Assert.That(screen.y, Is.InRange(0.175f - tolerance, 0.795f + tolerance));
            }
        }

        private static IEnumerator CaptureMenuOrbitNative(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(directory, "MainMenu-Orbit-" + name + ".png"), screenshot.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }
    }
}
#endif
