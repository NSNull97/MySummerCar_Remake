#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuCapture_SupportedAspectRatiosUseActualOverlayAndProductionArt()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/MainMenuRedesign/Captures"));
            Directory.CreateDirectory(directory);
            var captures = new List<string>();
            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png");
            Shader blur = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader");
            MainMenuVehicleModel vehicle = LoadMenuVehiclePrefab();
            MainMenuEnvironmentModel environment = LoadMenuEnvironmentPrefab();
            Assert.That(logo, Is.Not.Null);
            Assert.That(blur, Is.Not.Null);

            using (var resolution = new MainMenuGameViewResolutionScope())
            {
                UiFixture fixture = CreateFixture(
                    startInMainMenu: true,
                    saveService: new SaveServiceProbe(
                        CreateSlot("capture-older", "Earlier capture fixture", "2026-09-01T08:00:00Z"),
                        CreateSlot("capture-latest", "Latest capture fixture", "2026-09-02T09:00:00Z")),
                    requestLoad: AcceptLoadRequest,
                    openDeveloperTools: () => { },
                    menuBackdropTexture: null, menuLogoTexture: logo, uiBlurShader: blur,
                    menuVehiclePrefab: vehicle, menuPreviewBackdropShader: null,
                    menuEnvironmentPrefab: environment);
                try
                {
                    MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                    yield return AwaitMenuVehicleFrame(preview, 1);
                    AssertMenuModelHasNoGameplay(preview.Model);
                    AssertMenuEnvironmentGeometry(environment, preview.Environment, preview.Model);
                    AssertMenuVehicleOnAuthoredGround(preview);
                    Assert.That(UnityEngine.Object.FindObjectsByType<GameUiRoot>(FindObjectsSortMode.None),
                        Has.Length.EqualTo(1), "Another test overlay would invalidate the captured backbuffer.");
                    var sizes = new[]
                    {
                        new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                        new Vector2Int(1920, 1200), new Vector2Int(2560, 1080),
                        new Vector2Int(3840, 1080), new Vector2Int(3840, 2160)
                    };
                    foreach (Vector2Int size in sizes)
                    {
                        yield return ResizeMenuCaptureViewport(resolution, preview, size);
                        AssertMenuVehicleInsideFullViewport(preview);
                        AssertMenuDisplaysRenderedEnvironment(fixture, preview);
                        Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                        AssertActiveGraphicsInsideViewport(fixture.Root, UiRouteId.MainMenu);
                        yield return new WaitForEndOfFrame();
                        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        try
                        {
                            Assert.That(screenshot.width, Is.EqualTo(size.x));
                            Assert.That(screenshot.height, Is.EqualTo(size.y));
                            string name = "MainMenu-Fixture-" + size.x + "x" + size.y + ".png";
                            File.WriteAllBytes(Path.Combine(directory, name), screenshot.EncodeToPNG());
                            captures.Add(name);
                        }
                        finally { UnityEngine.Object.Destroy(screenshot); }
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(fixture.Root.gameObject);
                    UnityEngine.Object.Destroy(fixture.GameplayRoot);
                    UnityEngine.Object.Destroy(fixture.PlayerActions);
                    UnityEngine.Object.Destroy(fixture.VehicleActions);
                }
                yield return null;
                yield return null;
                resolution.SetSize(1920, 1080);
                UiFixture empty = CreateFixture(
                    startInMainMenu: true,
                    saveService: new SaveServiceProbe(),
                    requestLoad: AcceptLoadRequest,
                    menuBackdropTexture: null, menuLogoTexture: logo, uiBlurShader: blur,
                    menuVehiclePrefab: vehicle, menuPreviewBackdropShader: null,
                    menuEnvironmentPrefab: environment);
                try
                {
                    yield return AwaitMenuVehicleFrame(RequireMenuVehiclePreview(empty), 1);
                    yield return new WaitForSecondsRealtime(0.45f);
                    Assert.That(Screen.width, Is.EqualTo(1920));
                    Assert.That(Screen.height, Is.EqualTo(1080));
                    AssertMenuVehicleInsideFullViewport(RequireMenuVehiclePreview(empty));
                    AssertMenuDisplaysRenderedEnvironment(empty, RequireMenuVehiclePreview(empty));
                    Transform menu = FindRequired(empty.Root.transform, UiRouteId.MainMenu.ToString());
                    Assert.That(FindRequired(menu, "Continue").GetComponent<Button>().interactable, Is.False);
                    Assert.That(FindRequired(menu, "LoadGame").GetComponent<Button>().interactable, Is.True,
                        "An available empty save service still opens the save browser.");
                    yield return new WaitForEndOfFrame();
                    Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        const string name = "MainMenu-EmptySlots-1920x1080.png";
                        File.WriteAllBytes(Path.Combine(directory, name), screenshot.EncodeToPNG());
                        captures.Add(name);
                    }
                    finally { UnityEngine.Object.Destroy(screenshot); }
                }
                finally
                {
                    UnityEngine.Object.Destroy(empty.Root.gameObject);
                    UnityEngine.Object.Destroy(empty.GameplayRoot);
                    UnityEngine.Object.Destroy(empty.PlayerActions);
                    UnityEngine.Object.Destroy(empty.VehicleActions);
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(directory, "CAPTURE_CONTEXT.txt"),
                "Executed isolated PlayMode UI fixture. These are implementation captures, not visual approval.\n" +
                "Uses the actual generated home-yard meshes, menu logo/blur, real UI runtime, native ScreenSpaceOverlay canvas, " +
                "CanvasScaler and GameView backbuffer. No photo background or photo-plane shader is supplied. The mesh-only Satsuma " +
                "is captured only after its actual FrameReady signal. Aspect changes require a fresh FrameReady, " +
                "and all eight vehicle bounds corners are checked against the actual full viewport. " +
                "Save rows are explicit in-memory test fixtures; " +
                "user saves, user UI settings and production scenes are never read or written.\n" +
                "Temporary GameView sizes and selection restored after capture.\n" +
                "Captured UTC: " + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", captures));
        }

        private static IEnumerator ResizeMenuCaptureViewport(
            MainMenuGameViewResolutionScope resolution, MainMenuVehiclePreview preview, Vector2Int size)
        {
            float previousAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float requestedAspect = size.x / (float)size.y;
            bool aspectChanged = Mathf.Abs(previousAspect - requestedAspect) > 1e-5f;
            int previousRenderCount = preview.RenderCount;
            bool freshFrameReady = false;
            Action onFrameReady = () =>
            {
                if (preview.RenderCount > previousRenderCount && Screen.width == size.x && Screen.height == size.y)
                    freshFrameReady = true;
            };
            // Subscribe before changing GameView: a render may complete while
            // Unity is settling its new backbuffer and UI layout.
            preview.FrameReady += onFrameReady;
            try
            {
                resolution.SetSize(size.x, size.y);
                float deadline = Time.realtimeSinceStartup + 10f;
                while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Screen.width, Is.EqualTo(size.x), "Actual GameView render width");
                Assert.That(Screen.height, Is.EqualTo(size.y), "Actual GameView render height");
                if (aspectChanged)
                {
                    deadline = Time.realtimeSinceStartup + 20f;
                    while ((!freshFrameReady || !preview.IsReady) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(freshFrameReady && preview.IsReady, Is.True,
                        "Changing aspect ratio must produce a new FrameReady before capturing the reframed vehicle.");
                }
                else
                {
                    yield return AwaitMenuVehicleFrame(preview, 1);
                }
                yield return new WaitForSecondsRealtime(0.45f);
            }
            finally
            {
                if (preview != null) preview.FrameReady -= onFrameReady;
            }
        }

        private static void AssertMenuVehicleInsideFullViewport(MainMenuVehiclePreview preview)
        {
            float actualAspect = Screen.width / (float)Screen.height;
            Texture output = preview.OutputTexture;
            Assert.That(output.width, Is.InRange(1, 1920));
            Assert.That(output.height, Is.InRange(1, 1080));
            float outputAspect = output.width / (float)output.height;
            Assert.That(preview.PreviewCamera.aspect, Is.EqualTo(outputAspect).Within(1e-5f));
            float pixelAspectTolerance = (1f + actualAspect) / output.height;
            Assert.That(outputAspect, Is.EqualTo(actualAspect).Within(pixelAspectTolerance),
                "Render target must preserve the actual viewport aspect within one-pixel rounding.");
            Bounds bounds = preview.Model.LocalBounds;
            float toleranceX = 1f / preview.OutputTexture.width;
            float toleranceY = 1f / preview.OutputTexture.height;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                Vector3 world = preview.Model.transform.TransformPoint(bounds.center + offset);
                Vector3 point = preview.PreviewCamera.WorldToViewportPoint(world);
                string context = Screen.width + "x" + Screen.height + " vehicle bounds corner " + corner;
                Assert.That(point.z, Is.GreaterThan(0f), context + " must be in front of the preview camera.");
                Assert.That(point.x, Is.InRange(-toleranceX, 1f + toleranceX), context + " x");
                Assert.That(point.y, Is.InRange(-toleranceY, 1f + toleranceY), context + " y");
            }
        }

        /// <summary>
        /// Unity has no public GameView resolution API. Keep this small audited
        /// reflection boundary Editor-only, restore the previous selection and
        /// remove only size entries created by this scope.
        /// </summary>
        private sealed class MainMenuGameViewResolutionScope : IDisposable
        {
            private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly EditorWindow view;
            private readonly bool createdView;
            private readonly PropertyInfo selection;
            private readonly object group;
            private readonly Type sizeType;
            private readonly Type fixedSizeType;
            private readonly int previousSelection;
            private readonly List<int> createdCustomIndices = new List<int>();

            public MainMenuGameViewResolutionScope()
            {
                Assembly editor = typeof(EditorWindow).Assembly;
                Type viewType = editor.GetType("UnityEditor.GameView", throwOnError: true);
                UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(viewType);
                createdView = views.Length == 0;
                view = createdView ? EditorWindow.GetWindow(viewType) : (EditorWindow)views[0];
                selection = viewType.GetProperty("selectedSizeIndex", InstanceMembers);
                if (selection == null) throw new MissingMemberException("Unity GameView.selectedSizeIndex is unavailable.");
                int priorSelection = (int)selection.GetValue(view);
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes", throwOnError: true);
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType", throwOnError: true);
                group = sizesType.GetMethod("GetGroup", InstanceMembers).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                sizeType = editor.GetType("UnityEditor.GameViewSize", throwOnError: true);
                fixedSizeType = editor.GetType("UnityEditor.GameViewSizeType", throwOnError: true);
                // Recover only this harness's named temporary entries if an
                // earlier capture was interrupted during cleanup.
                string[] sizeLabels = (string[])group.GetType().GetMethod("GetDisplayTexts", InstanceMembers).Invoke(group, null);
                int builtins = (int)group.GetType().GetMethod("GetBuiltinCount", InstanceMembers).Invoke(group, null);
                for (int index = sizeLabels.Length - 1; index >= builtins; index--)
                {
                    if (!sizeLabels[index].StartsWith("MSC menu capture ", StringComparison.Ordinal)) continue;
                    group.GetType().GetMethod("RemoveCustomSize", InstanceMembers).Invoke(group, new object[] { index });
                    if (priorSelection == index) priorSelection = 0;
                    else if (priorSelection > index) priorSelection--;
                }
                previousSelection = priorSelection;
                selection.SetValue(view, previousSelection);
            }

            public void SetSize(int width, int height)
            {
                Type groupType = group.GetType();
                int builtinCount = (int)groupType.GetMethod("GetBuiltinCount", InstanceMembers).Invoke(group, null);
                int customCount = (int)groupType.GetMethod("GetCustomCount", InstanceMembers).Invoke(group, null);
                ConstructorInfo constructor = sizeType.GetConstructor(
                    InstanceMembers, null, new[] { fixedSizeType, typeof(int), typeof(int), typeof(string) }, null);
                if (constructor == null) throw new MissingMethodException("Unity GameViewSize constructor is unavailable.");
                object size = constructor.Invoke(new[]
                {
                    Enum.Parse(fixedSizeType, "FixedResolution"), (object)width, height, "MSC menu capture " + width + "x" + height
                });
                groupType.GetMethod("AddCustomSize", InstanceMembers).Invoke(group, new[] { size });
                // Unity's removal API accepts the total index, including builtins.
                createdCustomIndices.Add(builtinCount + customCount);
                view.GetType().GetMethod("SizeSelectionCallback", InstanceMembers)
                    .Invoke(view, new object[] { builtinCount + customCount, null });
                view.Focus();
                view.Repaint();
            }

            public void Dispose()
            {
                if (view != null) selection.SetValue(view, previousSelection);
                MethodInfo remove = group.GetType().GetMethod("RemoveCustomSize", InstanceMembers);
                for (int i = createdCustomIndices.Count - 1; i >= 0; i--)
                    remove.Invoke(group, new object[] { createdCustomIndices[i] });
                if (view != null)
                {
                    if (createdView) view.Close();
                    else view.Repaint();
                }
            }
        }
    }
}
#endif
