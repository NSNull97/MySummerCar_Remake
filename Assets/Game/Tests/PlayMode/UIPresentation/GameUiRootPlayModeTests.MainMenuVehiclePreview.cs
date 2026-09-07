#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        private const string MenuVehiclePrefabPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MenuPreview/SatsumaMenuPreview.prefab";
        private const string MenuEnvironmentPrefabPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/MainMenu/HomeYardMenuEnvironment.prefab";

        [UnityTest]
        public IEnumerator MainMenuVehiclePreview_PaintChangesPropertyBlocksAndRenderedPixelsOnly()
        {
            MainMenuVehicleModel prefab = LoadMenuVehiclePrefab();
            MainMenuEnvironmentModel environmentPrefab = LoadMenuEnvironmentPrefab();
            Dictionary<Material, string> canonicalMaterials = SnapshotMenuCanonicalMaterials(prefab);
            Dictionary<Material, string> environmentMaterials = SnapshotMenuCanonicalMaterials(environmentPrefab);
            UiFixture fixture = CreateMenuVehicleFixture(prefab, environmentPrefab);
            try
            {
                MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                LogMenuRenderContext(preview);
                yield return AwaitMenuVehicleFrame(preview, 1);
                AssertMenuModelHasNoGameplay(prefab);
                AssertMenuModelHasNoGameplay(preview.Model);
                AssertMenuEnvironmentGeometry(environmentPrefab, preview.Environment, preview.Model);
                AssertMenuVehicleOnAuthoredGround(preview);
                AssertMenuDisplaysRenderedEnvironment(fixture, preview);
                string directory = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "../Artifacts/MainMenuRedesign/Captures"));
                Directory.CreateDirectory(directory);

                int nextFrame = preview.RenderCount + 1;
                FindRequired(fixture.Root.transform, "Colour3").GetComponent<Button>().onClick.Invoke();
                yield return AwaitMenuVehicleFrame(preview, nextFrame);
                AssertMenuPaintPropertyBlocks(preview.Model, new Color32(202, 9, 0, 255));
                Color32[] redPixels = ReadMenuPreviewPixels(preview,
                    Path.Combine(directory, "MainMenu-Vehicle-Paint-Red.png"));

                nextFrame = preview.RenderCount + 1;
                FindRequired(fixture.Root.transform, "Colour6").GetComponent<Button>().onClick.Invoke();
                yield return AwaitMenuVehicleFrame(preview, nextFrame);
                AssertMenuPaintPropertyBlocks(preview.Model, new Color32(3, 38, 69, 255));
                Color32[] bluePixels = ReadMenuPreviewPixels(preview,
                    Path.Combine(directory, "MainMenu-Vehicle-Paint-Blue.png"));
                Assert.That(bluePixels.Length, Is.EqualTo(redPixels.Length));

                int changedPixels = 0;
                long redBlueDifference = 0;
                for (int index = 0; index < redPixels.Length; index++)
                {
                    int red = redPixels[index].r - bluePixels[index].r;
                    int green = redPixels[index].g - bluePixels[index].g;
                    int blue = redPixels[index].b - bluePixels[index].b;
                    if (Math.Abs(red) + Math.Abs(green) + Math.Abs(blue) < 24) continue;
                    changedPixels++;
                    redBlueDifference += red - blue;
                }
                Assert.That(changedPixels, Is.GreaterThan(Math.Max(16, redPixels.Length / 5000)),
                    "Changing the real palette must visibly repaint geometry in the camera output.");
                double meanRedBlueDifference = redBlueDifference / (255d * changedPixels);
                Assert.That(meanRedBlueDifference, Is.GreaterThan(0.01d),
                    "The changed pixels must move from blue towards red, not merely differ through render noise.");
                yield return AssertMenuEnvironmentContributesRenderedPixels(preview, new Color32(3, 38, 69, 255));
                AssertCanonicalMenuMaterialsUnchanged(preview.Model, canonicalMaterials);
                AssertCanonicalMenuMaterialsUnchanged(preview.Environment, environmentMaterials);
                File.WriteAllText(Path.Combine(directory, "MainMenu-Vehicle-Paint-Evidence.txt"),
                    "Executed real menu palette and rendered-preview comparison; not visual approval.\n" +
                    "PNG and compared pixels: linear HDR render-target readback converted to sRGB; no extra exposure or tonemapping.\n" +
                    "Changed pixels: " + changedPixels + " / " + redPixels.Length + "\n" +
                    "Mean normalized red-minus-blue change: " +
                    meanRedBlueDifference.ToString("F6", CultureInfo.InvariantCulture) + "\n" +
                    "MPB paint surfaces checked: " + preview.Model.PaintSurfaceCount + "\n" +
                    "Unchanged canonical shared materials: " + canonicalMaterials.Count + "\n" +
                    "Unchanged environment shared materials: " + environmentMaterials.Count + "\n" +
                    "Environment mesh renderers: " + preview.Environment.RendererCount + "; no photo background supplied.\n" +
                    "Settings use the isolated UI fixture temporary directory; no user save was loaded.\n");
                yield return DestroyFixture(fixture);
                yield return null;
            }
            finally
            {
                DestroyFailedMenuInputFixture(fixture);
            }
        }

        [UnityTest]
        public IEnumerator MainMenuVehiclePreview_QualitySwitchKeepsOpaqueEnvironmentAndVehicleOutput()
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            RenderPipelineAsset originalDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            string[] qualityNames = { "High Fidelity", "Balanced", "Performant" };
            var qualityIndices = new int[qualityNames.Length];
            var pipelines = new RenderPipelineAsset[qualityNames.Length];
            var pipelineSnapshots = new string[qualityNames.Length];
            Dictionary<Material, string> environmentMaterials = SnapshotMenuCanonicalMaterials(LoadMenuEnvironmentPrefab());
            string atmosphereEvidence = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Artifacts/MainMenuRedesign/Captures/ATMOSPHERE_EVIDENCE.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(atmosphereEvidence));
            File.WriteAllText(atmosphereEvidence,
                "Isolated real HDRP lamp/fog comparisons; completed XML establishes pass/fail. No user settings or world lighting changed.\n");
            for (int index = 0; index < qualityNames.Length; index++)
            {
                qualityIndices[index] = Array.IndexOf(QualitySettings.names, qualityNames[index]);
                Assert.That(qualityIndices[index], Is.GreaterThanOrEqualTo(0), qualityNames[index]);
                pipelines[index] = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    "Assets/Settings/HDRP " + qualityNames[index] + ".asset");
                Assert.That(pipelines[index], Is.Not.Null, qualityNames[index]);
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(qualityIndices[index]),
                    Is.SameAs(pipelines[index]), qualityNames[index]);
                pipelineSnapshots[index] = EditorJsonUtility.ToJson(pipelines[index]);
            }

            UiFixture fixture = default;
            GameObject worldLighting = null;
            try
            {
                // Production keeps its world sun alive behind the front end.
                // Reproduce that boundary: a second directional shadow caster
                // previously exhausted HDRP's single cascade atlas in the menu.
                GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_LateDay.prefab");
                Assert.That(lightingPrefab, Is.Not.Null);
                worldLighting = Object.Instantiate(lightingPrefab);
                Light worldSun = worldLighting.GetComponentInChildren<Light>();
                Assert.That(worldSun, Is.Not.Null);
                Assert.That(worldSun.shadows, Is.Not.EqualTo(LightShadows.None));
                // Native Light JSON includes an inactive bounding-sphere cache
                // containing uninitialized bytes; compare observable settings.
                var worldSunState = (worldSun.type, worldSun.shadows, worldSun.intensity,
                    worldSun.color, worldSun.cullingMask, worldSun.enabled);
                Quaternion worldSunRotation = worldSun.transform.rotation;
                fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab());
                UnityEngine.EventSystems.EventSystem.current
                    .GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().enabled = false;
                fixture.PlayerActions.devices = Array.Empty<UnityEngine.InputSystem.InputDevice>();
                MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                LogMenuRenderContext(preview);
                yield return AwaitMenuVehicleFrame(preview, 1);
                for (int index = 0; index < qualityNames.Length; index++)
                {
                    FindRequired(fixture.Root.transform, "Settings").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                    Assert.That(preview.Model.gameObject.activeInHierarchy, Is.False);
                    Assert.That(preview.Environment.gameObject.activeInHierarchy, Is.False);
                    int nextFrame = preview.RenderCount + 1;
                    QualitySettings.SetQualityLevel(qualityIndices[index], applyExpensiveChanges: true);
                    yield return null;
                    yield return null;
                    Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipelines[index]), qualityNames[index]);

                    Transform settings = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
                    FindRequired(settings, "Back").GetComponent<Button>().onClick.Invoke();
                    yield return AwaitMenuVehicleFrame(preview, nextFrame);
                    Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipelines[index]),
                        "The camera comparison must still use the explicitly selected quality: " + qualityNames[index]);
                    AssertMenuDisplaysRenderedEnvironment(fixture, preview);
                    var output = (RenderTexture)preview.OutputTexture;
                    Color[] pixels = ReadMenuPreviewLinearPixels(output);
                    AssertMenuPreviewOpaqueFinitePixels(pixels, qualityNames[index]);
                    AssertMenuEnvironmentIntersectsCamera(preview);
                    Vector3 vehicleCenter = preview.PreviewCamera.WorldToViewportPoint(
                        preview.Model.transform.TransformPoint(preview.Model.LocalBounds.center));
                    Assert.That(vehicleCenter.z, Is.GreaterThan(0f));
                    AssertMenuPreviewCropHasColour(pixels, output.width, output.height,
                        new Rect(vehicleCenter.x - 0.03f, vehicleCenter.y - 0.03f, 0.06f, 0.06f),
                        qualityNames[index] + " vehicle center");
                    Assert.That((worldSun.type, worldSun.shadows, worldSun.intensity,
                        worldSun.color, worldSun.cullingMask, worldSun.enabled), Is.EqualTo(worldSunState),
                        "Menu illumination must coexist with the world sun without modifying it.");
                    Assert.That(Quaternion.Angle(worldSun.transform.rotation, worldSunRotation), Is.LessThan(0.001f));
                    AssertMenuAtmospherePlacement(preview);
                    var hdrp = (HDRenderPipelineAsset)pipelines[index];
                    yield return AssertMenuAtmosphereRenderedPixels(preview, qualityNames[index],
                        hdrp.currentPlatformRenderPipelineSettings.supportVolumetrics, atmosphereEvidence);
                }

                for (int index = 0; index < pipelines.Length; index++)
                    Assert.That(EditorJsonUtility.ToJson(pipelines[index]), Is.EqualTo(pipelineSnapshots[index]),
                        "Preview rendering must not modify shared HDRP settings: " + qualityNames[index]);
                Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(originalDefaultPipeline));
                AssertCanonicalMenuMaterialsUnchanged(preview.Environment, environmentMaterials);
                yield return DestroyFixture(fixture);
                yield return null;
            }
            finally
            {
                DestroyFailedMenuInputFixture(fixture);
                if (worldLighting != null) Object.DestroyImmediate(worldLighting);
                QualitySettings.SetQualityLevel(originalQuality, applyExpensiveChanges: true);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuVehiclePreview_RouteHideResumeAndDisposeReleaseOwnedRendering()
        {
            UiFixture fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab());
            try
            {
                MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                LogMenuRenderContext(preview);
                yield return AwaitMenuVehicleFrame(preview, 1);
                MainMenuVehicleModel model = preview.Model;
                MainMenuEnvironmentModel environment = preview.Environment;
                Camera camera = preview.PreviewCamera;
                Texture output = preview.OutputTexture;
                Assert.That(model, Is.Not.Null);
                Assert.That(environment, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);
                Assert.That(output, Is.Not.Null);
                int renderedBeforeHide = preview.RenderCount;

                FindRequired(fixture.Root.transform, "Settings").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGraphics));
                Assert.That(model.gameObject.activeInHierarchy, Is.False);
                Assert.That(environment.gameObject.activeInHierarchy, Is.False);
                Assert.That(camera.gameObject.activeInHierarchy, Is.False);
                yield return null;
                yield return null;
                Assert.That(preview.RenderCount, Is.EqualTo(renderedBeforeHide),
                    "Hidden menu geometry must not keep issuing render requests.");

                Transform settings = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
                FindRequired(settings, "Back").GetComponent<Button>().onClick.Invoke();
                yield return AwaitMenuVehicleFrame(preview, renderedBeforeHide + 1);
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                Assert.That(model.gameObject.activeInHierarchy, Is.True);
                Assert.That(environment.gameObject.activeInHierarchy, Is.True);
                Assert.That(camera.gameObject.activeInHierarchy, Is.True);
                AssertMenuModelHasNoGameplay(model);

                fixture.Root.EndGameSession();
                Assert.That(model.gameObject.activeInHierarchy, Is.False);
                Assert.That(environment.gameObject.activeInHierarchy, Is.False);
                Assert.That(camera.gameObject.activeInHierarchy, Is.False);
                yield return DestroyFixture(fixture);
                yield return null;
                Assert.That(preview == null, Is.True);
                Assert.That(model == null, Is.True, "The preview must release its instantiated model.");
                Assert.That(environment == null, Is.True, "The preview must release its instantiated environment.");
                Assert.That(camera == null, Is.True, "The preview must release its camera.");
                Assert.That(output == null, Is.True, "The preview must release its owned render texture.");
            }
            finally
            {
                DestroyFailedMenuInputFixture(fixture);
            }
        }

        // Keep existing captures independent of the machine clock and test start time.
        private static DateTime FixedMenuFixtureLocalTime() => new DateTime(2026, 9, 4, 16, 0, 0);

        private UiFixture CreateMenuVehicleFixture(
            MainMenuVehicleModel prefab, MainMenuEnvironmentModel environmentPrefab = null,
            Func<DateTime> menuLocalTimeProvider = null, MSC.Core.Time.IGameTimeService gameTime = null) =>
            CreateFixture(startInMainMenu: true,
                menuBackdropTexture: null,
                menuLogoTexture: AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png"),
                uiBlurShader: AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader"),
                menuVehiclePrefab: prefab, menuPreviewBackdropShader: null,
                menuEnvironmentPrefab: environmentPrefab != null ? environmentPrefab : LoadMenuEnvironmentPrefab(),
                menuLocalTimeProvider: menuLocalTimeProvider ?? FixedMenuFixtureLocalTime, gameTime: gameTime);

        private static MainMenuVehicleModel LoadMenuVehiclePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuVehiclePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Generate the explicitly authored private menu-preview prefab before this capture.");
            MainMenuVehicleModel model = prefab.GetComponent<MainMenuVehicleModel>();
            Assert.That(model, Is.Not.Null);
            return model;
        }

        private static MainMenuEnvironmentModel LoadMenuEnvironmentPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuEnvironmentPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Generate the explicit mesh-only home environment before this capture.");
            MainMenuEnvironmentModel environment = prefab.GetComponent<MainMenuEnvironmentModel>();
            Assert.That(environment, Is.Not.Null);
            string[] dependencies = AssetDatabase.GetDependencies(MenuEnvironmentPrefabPath, recursive: true);
            Assert.That(dependencies, Does.Not.Contain(
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png"));
            Assert.That(dependencies, Does.Not.Contain(
                "Assets/Game/UI/Presentation/Content/Shaders/MainMenuGaragePlate.shader"));
            return environment;
        }

        private static void AssertMenuDisplaysRenderedEnvironment(UiFixture fixture, MainMenuVehiclePreview preview)
        {
            var output = preview.OutputTexture as RenderTexture;
            Assert.That(output, Is.Not.Null);
            Assert.That(output.IsCreated(), Is.True);
            RawImage image = FindRequired(fixture.Root.transform, "StaticMenuBackdrop").GetComponent<RawImage>();
            Assert.That(image.isActiveAndEnabled, Is.True,
                "A live 3D menu must stay visible when no photo background is supplied.");
            Assert.That(image.texture, Is.SameAs(output));
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert.That(bottomLeft.x, Is.EqualTo(0f).Within(1f));
            Assert.That(bottomLeft.y, Is.EqualTo(0f).Within(1f));
            Assert.That(topRight.x, Is.EqualTo(Screen.width).Within(1f));
            Assert.That(topRight.y, Is.EqualTo(Screen.height).Within(1f),
                "The live viewport must not inherit the old photograph's EnvelopeParent crop.");
        }

        private static void AssertMenuEnvironmentGeometry(
            MainMenuEnvironmentModel prefab, MainMenuEnvironmentModel environment, MainMenuVehicleModel vehicle)
        {
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.Not.SameAs(prefab));
            Assert.That(environment.RendererCount, Is.GreaterThan(0));
            Assert.That(environment.RendererCount, Is.EqualTo(prefab.RendererCount));
            var canonicalMeshes = new HashSet<Mesh>();
            foreach (Renderer renderer in prefab.Renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null);
                Assert.That(filter.sharedMesh, Is.Not.Null);
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True);
                canonicalMeshes.Add(filter.sharedMesh);
            }
            foreach (Component component in environment.GetComponentsInChildren<Component>(true))
            {
                // The separately checked car is parked beneath VehicleAnchor;
                // it is not part of the borrowed static environment prefab.
                if (component != null && component.transform.IsChildOf(vehicle.transform)) continue;
                Assert.That(component != null && (component is Transform || component is MeshFilter ||
                    component is MeshRenderer || component == environment), Is.True,
                    "The environment must only contain real static mesh presentation: " + component);
            }
            foreach (Renderer renderer in environment.Renderers)
            {
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.True);
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null);
                Assert.That(canonicalMeshes.Contains(filter.sharedMesh), Is.True,
                    "The environment must preserve its canonical shared mesh references.");
                Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0));
            }
        }

        private static void AssertMenuVehicleOnAuthoredGround(MainMenuVehiclePreview preview)
        {
            MainMenuEnvironmentModel environment = preview.Environment;
            Transform anchor = environment.VehicleAnchor;
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.IsChildOf(environment.transform), Is.True);
            Vector3 anchorLocal = environment.transform.InverseTransformPoint(anchor.position);
            Assert.That(anchorLocal.y + environment.SourceOrigin.y,
                Is.EqualTo(environment.GroundHeightWorld).Within(0.001f),
                "The vehicle anchor must retain the ground height measured from the real world meshes.");
            Vector3 modelFromAnchor = anchor.InverseTransformPoint(preview.Model.transform.position);
            Assert.That(modelFromAnchor.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(modelFromAnchor.z, Is.EqualTo(0f).Within(0.001f));
            float lowestRenderedY = float.PositiveInfinity;
            foreach (Renderer renderer in preview.Model.GetComponentsInChildren<Renderer>(true))
                lowestRenderedY = Mathf.Min(lowestRenderedY, renderer.bounds.min.y);
            Assert.That(lowestRenderedY, Is.EqualTo(anchor.position.y).Within(0.02f),
                "Actual car mesh bounds must sit on the measured ground plane, without floating or sinking.");
            AssertMenuEnvironmentIntersectsCamera(preview);
        }

        private static void AssertMenuEnvironmentIntersectsCamera(MainMenuVehiclePreview preview)
        {
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(preview.PreviewCamera);
            int visibleMeshBounds = 0;
            foreach (Renderer renderer in preview.Environment.Renderers)
                if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) visibleMeshBounds++;
            Assert.That(visibleMeshBounds, Is.GreaterThan(0),
                "The real environment meshes must intersect the menu camera frustum.");
        }

        private static IEnumerator AssertMenuEnvironmentContributesRenderedPixels(
            MainMenuVehiclePreview preview, Color currentPaint)
        {
            var output = (RenderTexture)preview.OutputTexture;
            Color[] withEnvironment = ReadMenuPreviewLinearPixels(output);
            Bounds bounds = preview.Model.LocalBounds;
            Vector2 minimum = Vector2.one * float.PositiveInfinity;
            Vector2 maximum = Vector2.one * float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = preview.PreviewCamera.WorldToViewportPoint(
                    preview.Model.transform.TransformPoint(bounds.center + offset));
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            var vehiclePixels = Rect.MinMaxRect(minimum.x * output.width - 2f, minimum.y * output.height - 2f,
                maximum.x * output.width + 2f, maximum.y * output.height + 2f);
            IReadOnlyList<Renderer> renderers = preview.Environment.Renderers;
            var priorEnabled = new bool[renderers.Count];
            for (int index = 0; index < renderers.Count; index++) priorEnabled[index] = renderers[index].enabled;
            int restoreFrame = preview.RenderCount + 1;
            try
            {
                // Only instantiated environment renderers change. This proves
                // visible world geometry rather than a nonzero sky/clear pixel.
                for (int index = 0; index < renderers.Count; index++) renderers[index].enabled = false;
                int nextFrame = preview.RenderCount + 1;
                preview.SetPaint(currentPaint);
                yield return AwaitMenuVehicleFrame(preview, nextFrame);
                Color[] withoutEnvironment = ReadMenuPreviewLinearPixels((RenderTexture)preview.OutputTexture);
                Assert.That(withoutEnvironment.Length, Is.EqualTo(withEnvironment.Length));
                int changedOutsideVehicle = 0;
                for (int index = 0; index < withEnvironment.Length; index++)
                {
                    if (vehiclePixels.Contains(new Vector2(index % output.width, index / output.width))) continue;
                    Color before = withEnvironment[index], after = withoutEnvironment[index];
                    if (Mathf.Abs(before.r - after.r) + Mathf.Abs(before.g - after.g) +
                        Mathf.Abs(before.b - after.b) > 0.01f) changedOutsideVehicle++;
                }
                Assert.That(changedOutsideVehicle, Is.GreaterThan(Math.Max(16, withEnvironment.Length / 5000)),
                    "Actual environment meshes must contribute visible pixels outside the vehicle silhouette.");
            }
            finally
            {
                for (int index = 0; index < renderers.Count; index++)
                    if (renderers[index] != null) renderers[index].enabled = priorEnabled[index];
                restoreFrame = preview.RenderCount + 1;
                preview.SetPaint(currentPaint);
            }
            yield return AwaitMenuVehicleFrame(preview, restoreFrame);
        }

        private static MainMenuVehiclePreview RequireMenuVehiclePreview(UiFixture fixture)
        {
            MainMenuVehiclePreview preview = fixture.Root.GetComponentInChildren<MainMenuVehiclePreview>(true);
            Assert.That(preview, Is.Not.Null, "The actual GameUiRoot must compose its explicitly supplied model.");
            return preview;
        }

        private static void LogMenuRenderContext(MainMenuVehiclePreview preview)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            int directionalCount = 0;
            foreach (Light light in lights)
                if (light.enabled && light.gameObject.activeInHierarchy && light.type == LightType.Directional)
                    directionalCount++;

            int qualityIndex = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityIndex >= 0 && qualityIndex < qualityNames.Length
                ? qualityNames[qualityIndex] : "<unavailable>";
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            TestContext.WriteLine($"[MenuRenderContext] test={TestContext.CurrentContext.Test.FullName}; " +
                $"frame={Time.frameCount}; activeDirectionalCount={directionalCount}; " +
                $"quality={qualityIndex}:{qualityName}; pipeline={(pipeline != null ? pipeline.name : "<null>")}; " +
                $"pipelinePath={(pipeline != null ? AssetDatabase.GetAssetPath(pipeline) : "<null>")}; " +
                $"screen={Screen.width}x{Screen.height}; previewActive={preview.isActiveAndEnabled}; " +
                $"cameraActive={(preview.PreviewCamera != null && preview.PreviewCamera.gameObject.activeInHierarchy)}; " +
                $"cameraEnabled={(preview.PreviewCamera != null && preview.PreviewCamera.enabled)}; " +
                $"isReady={preview.IsReady}; renderCount={preview.RenderCount}");
            foreach (Light light in lights)
            {
                if (!light.enabled || !light.gameObject.activeInHierarchy || light.type != LightType.Directional)
                    continue;
                TestContext.WriteLine($"[MenuRenderDirectional] name={light.name}; type={light.type}; " +
                    $"intensity={light.intensity.ToString("R", CultureInfo.InvariantCulture)}; shadows={light.shadows}; " +
                    $"scenePath={light.gameObject.scene.path}; sceneName={light.gameObject.scene.name}; " +
                    $"cullingMask=0x{unchecked((uint)light.cullingMask):X8}; " +
                    $"entityId={EntityId.ToULong(light.GetEntityId())}");
            }
        }

        private static IEnumerator AwaitMenuVehicleFrame(MainMenuVehiclePreview preview, int minimumRenderCount)
        {
            bool frameReady = preview.IsReady && preview.RenderCount >= minimumRenderCount;
            Action onReady = () => frameReady = true;
            preview.FrameReady += onReady;
            try
            {
                float deadline = Time.realtimeSinceStartup + 20f;
                while ((!frameReady || !preview.IsReady || preview.RenderCount < minimumRenderCount) &&
                    Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(frameReady && preview.IsReady && preview.RenderCount >= minimumRenderCount,
                    Is.True, "A real preview FrameReady signal is required before inspecting or capturing its output. " +
                    $"previewIsReady={preview.IsReady}; renderCount={preview.RenderCount}; minimum={minimumRenderCount}; " +
                    $"pendingFrameReadySignal={!frameReady}; pendingReady={!preview.IsReady}; " +
                    $"pendingRenderCount={preview.RenderCount < minimumRenderCount}; " +
                    $"previewActive={preview.isActiveAndEnabled}; " +
                    $"cameraActive={(preview.PreviewCamera != null && preview.PreviewCamera.gameObject.activeInHierarchy)}; " +
                    $"cameraEnabled={(preview.PreviewCamera != null && preview.PreviewCamera.enabled)}");
            }
            finally
            {
                if (preview != null) preview.FrameReady -= onReady;
            }
        }

        private static void AssertMenuModelHasNoGameplay(MainMenuVehicleModel model)
        {
            Assert.That(model, Is.Not.Null);
            Assert.That(model.RendererCount, Is.GreaterThan(0));
            Assert.That(model.PaintSurfaceCount, Is.GreaterThan(0));
            foreach (Component component in model.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component != null &&
                    (component is Transform || component is MeshFilter || component is MeshRenderer || component == model),
                    Is.True, "The menu model may only contain mesh presentation and its paint binding: " + component);
            }
        }

        private static void AssertMenuPaintPropertyBlocks(MainMenuVehicleModel model, Color expected)
        {
            var block = new MaterialPropertyBlock();
            int slotsChecked = 0;
            foreach (MainMenuVehiclePaintBinding binding in model.PaintBindings)
            {
                foreach (int slot in binding.MaterialSlots)
                {
                    block.Clear();
                    binding.Renderer.GetPropertyBlock(block, slot);
                    AssertMenuPaintColour(block.GetColor("_BaseColor"), expected, binding.SurfaceId + " _BaseColor");
                    AssertMenuPaintColour(block.GetColor("_Color"), expected, binding.SurfaceId + " _Color");
                    slotsChecked++;
                }
            }
            Assert.That(slotsChecked, Is.GreaterThan(0));
        }

        private static void AssertMenuPaintColour(Color actual, Color expected, string context)
        {
            const float tolerance = 1e-5f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), context + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), context + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), context + " blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), context + " alpha");
        }

        private static Dictionary<Material, string> SnapshotMenuCanonicalMaterials(Component prefab)
        {
            var materials = new Dictionary<Material, string>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(material), Is.Not.Empty,
                        "The preview borrows explicit canonical material assets.");
                    if (!materials.ContainsKey(material)) materials.Add(material, EditorJsonUtility.ToJson(material));
                }
            }
            return materials;
        }

        private static void AssertCanonicalMenuMaterialsUnchanged(
            Component model, Dictionary<Material, string> snapshots)
        {
            IEnumerable<Renderer> renderers = model is MainMenuEnvironmentModel environment
                ? environment.Renderers : model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    Assert.That(snapshots.ContainsKey(material), Is.True,
                        "Menu paint must retain the explicitly borrowed material references.");
            foreach (KeyValuePair<Material, string> snapshot in snapshots)
                Assert.That(EditorJsonUtility.ToJson(snapshot.Key), Is.EqualTo(snapshot.Value),
                    "Menu paint must not change the shared material asset: " + AssetDatabase.GetAssetPath(snapshot.Key));
        }

        private static Color32[] ReadMenuPreviewPixels(MainMenuVehiclePreview preview, string outputPath)
        {
            var renderTexture = preview.OutputTexture as RenderTexture;
            Assert.That(renderTexture, Is.Not.Null);
            Color[] linearPixels = ReadMenuPreviewLinearPixels(renderTexture);
            var displayPixels = new Color32[linearPixels.Length];
            // The owned ARGBHalf target is linear. Encode display values explicitly
            // so diagnostic PNGs and pixel assertions use the same sRGB buffer.
            for (int index = 0; index < linearPixels.Length; index++)
                displayPixels[index] = linearPixels[index].gamma;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(displayPixels);
                texture.Apply(updateMipmaps: false);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                return displayPixels;
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static Color[] ReadMenuPreviewLinearPixels(RenderTexture renderTexture)
        {
            Assert.That(renderTexture, Is.Not.Null);
            Assert.That(renderTexture.IsCreated(), Is.True);
            Assert.That(renderTexture.sRGB, Is.False, "The diagnostic conversion expects the owned linear render target.");
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false, linear: true);
            try
            {
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply(updateMipmaps: false);
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(texture);
            }
        }

        private static void AssertMenuPreviewOpaqueFinitePixels(Color[] pixels, string context)
        {
            bool allFinite = true;
            float minimumAlpha = float.PositiveInfinity;
            float maximumAlpha = float.NegativeInfinity;
            foreach (Color pixel in pixels)
            {
                allFinite &= !float.IsNaN(pixel.r) && !float.IsInfinity(pixel.r) &&
                    !float.IsNaN(pixel.g) && !float.IsInfinity(pixel.g) &&
                    !float.IsNaN(pixel.b) && !float.IsInfinity(pixel.b) &&
                    !float.IsNaN(pixel.a) && !float.IsInfinity(pixel.a);
                minimumAlpha = Mathf.Min(minimumAlpha, pixel.a);
                maximumAlpha = Mathf.Max(maximumAlpha, pixel.a);
            }
            Assert.That(allFinite, Is.True, context + " render output must contain finite colour values.");
            Assert.That(minimumAlpha, Is.EqualTo(1f).Within(1e-5f), context + " minimum alpha");
            Assert.That(maximumAlpha, Is.EqualTo(1f).Within(1e-5f), context + " maximum alpha");
        }

        private static void AssertMenuPreviewCropHasColour(
            Color[] pixels, int width, int height, Rect viewportCrop, string context)
        {
            int left = Mathf.Clamp(Mathf.FloorToInt(viewportCrop.xMin * width), 0, width);
            int right = Mathf.Clamp(Mathf.CeilToInt(viewportCrop.xMax * width), 0, width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(viewportCrop.yMin * height), 0, height);
            int top = Mathf.Clamp(Mathf.CeilToInt(viewportCrop.yMax * height), 0, height);
            int colouredPixels = 0;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    Color pixel = pixels[y * width + x];
                    if (pixel.r > 0f || pixel.g > 0f || pixel.b > 0f) colouredPixels++;
                }
            Assert.That(colouredPixels, Is.GreaterThan(0), context + " must contain rendered RGB content.");
        }
    }
}
#endif
