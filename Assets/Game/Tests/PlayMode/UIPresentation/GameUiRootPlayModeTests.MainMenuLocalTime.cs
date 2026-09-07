#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using MSC.Core.Time;
using MSC.UI.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenuLocalTime_UpdatesDayEveningNightWithoutChangingPaintGeometryOrGameClock()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Artifacts/MainMenuRedesign/Captures/TimeOfDay"));
            Directory.CreateDirectory(directory);
            string evidencePath = Path.Combine(directory, "TIME_OF_DAY_CONTEXT.txt");
            File.WriteAllText(evidencePath,
                "Isolated actual UI/3D render fixture with injected local wall-clock values. " +
                "No operating-system clock, user settings, saves or production scenes changed. " +
                "Existing visual fixtures use fixed 2026-09-04 16:00. This test uses noon, 20:00 and next midnight. " +
                "Balanced HDRP enables local fog; the existing M3 late-day world sun remains active and unchanged.\n");
            var localTime = new DateTime(2026, 9, 4, 12, 0, 0);
            var gameTime = new GameTimeService();
            gameTime.Advance(37.25d);
            UiFixture fixture = default;
            int originalQuality = QualitySettings.GetQualityLevel();
            GameObject worldLighting = null;
            using (var resolution = new MainMenuGameViewResolutionScope())
            {
                try
                {
                    GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_LateDay.prefab");
                    Assert.That(lightingPrefab, Is.Not.Null);
                    worldLighting = UnityEngine.Object.Instantiate(lightingPrefab);
                    Light worldSun = worldLighting.GetComponentInChildren<Light>();
                    Assert.That(worldSun, Is.Not.Null);
                    Assert.That(worldSun.isActiveAndEnabled, Is.True);
                    Assert.That(worldSun.shadows, Is.Not.EqualTo(LightShadows.None));
                    var worldSunState = (worldSun.type, worldSun.shadows, worldSun.intensity,
                        worldSun.color, worldSun.cullingMask, worldSun.enabled);
                    Quaternion worldSunRotation = worldSun.transform.rotation;
                    fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab(),
                        menuLocalTimeProvider: () => localTime, gameTime: gameTime);
                    // This is a renderer/clock integration gate. The separate
                    // virtual-device tests exercise input without live-device leakage.
                    EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled = false;
                    fixture.PlayerActions.devices = Array.Empty<InputDevice>();
                    MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                    Color paint = new Color32(232, 212, 120, 255);
                    preview.SetPaint(paint);
                    yield return AwaitMenuVehicleFrame(preview, 1);
                    // Settings initialization applies its fixture preset first.
                    // Select the supported fog profile afterwards, then reveal
                    // the same owned stage through its normal warmup path.
                    int balanced = Array.IndexOf(QualitySettings.names, "Balanced");
                    Assert.That(balanced, Is.GreaterThanOrEqualTo(0));
                    preview.SetVisible(false);
                    QualitySettings.SetQualityLevel(balanced, applyExpensiveChanges: true);
                    yield return null;
                    yield return null;
                    HDRenderPipelineAsset balancedPipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(
                        "Assets/Settings/HDRP Balanced.asset");
                    Assert.That(balancedPipeline, Is.Not.Null);
                    Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(balancedPipeline));
                    Assert.That(balancedPipeline.currentPlatformRenderPipelineSettings.supportVolumetrics, Is.True);
                    int qualityFrame = preview.RenderCount + 1;
                    preview.SetVisible(true);
                    yield return AwaitMenuVehicleFrame(preview, qualityFrame);
                    yield return ResizeMenuCaptureViewport(resolution, preview, new Vector2Int(1920, 1080));
                    Assert.That(preview.UsesSystemTime, Is.False, "The capture must use the explicitly injected clock.");
                    string worldClockSnapshot = JsonUtility.ToJson(gameTime.CaptureDto());
                    float pausedScale = Time.timeScale;
                    Assert.That(pausedScale, Is.Zero, "The wall-clock refresh must work while gameplay is paused by Main Menu.");
                    Texture originalOutput = preview.OutputTexture;
                    Vector3 modelPosition = preview.Model.transform.position;
                    Quaternion modelRotation = preview.Model.transform.rotation;
                    Vector3 environmentPosition = preview.Environment.transform.position;
                    Quaternion environmentRotation = preview.Environment.transform.rotation;
                    Vector3 cameraPosition = preview.PreviewCamera.transform.position;
                    Quaternion cameraRotation = preview.PreviewCamera.transform.rotation;
                    Color[] noonPixels = null;
                    float noonVehicleMean = 0f;
                    var times = new[] { localTime, localTime.Date.AddHours(20), localTime.Date.AddDays(1) };
                    string[] labels = { "Noon-1200", "Evening-2000", "Midnight-0000" };
                    for (int index = 0; index < times.Length; index++)
                    {
                        if (index > 0)
                        {
                            int expectedFrame = preview.RenderCount + 1;
                            localTime = times[index];
                            // Cross the real one-second poll using unscaled time;
                            // never call SetPaint to force the time refresh under test.
                            yield return new WaitForSecondsRealtime(1.2f);
                            yield return AwaitMenuVehicleFrame(preview, expectedFrame);
                        }
                        Assert.That(preview.LightingLocalTime, Is.EqualTo(times[index]));
                        Assert.That(preview.OutputTexture, Is.SameAs(originalOutput));
                        Assert.That(preview.Model.transform.position, Is.EqualTo(modelPosition));
                        Assert.That(preview.Model.transform.rotation, Is.EqualTo(modelRotation));
                        Assert.That(preview.Environment.transform.position, Is.EqualTo(environmentPosition));
                        Assert.That(preview.Environment.transform.rotation, Is.EqualTo(environmentRotation));
                        Assert.That(preview.PreviewCamera.transform.position, Is.EqualTo(cameraPosition));
                        Assert.That(preview.PreviewCamera.transform.rotation, Is.EqualTo(cameraRotation));
                        Assert.That(JsonUtility.ToJson(gameTime.CaptureDto()), Is.EqualTo(worldClockSnapshot));
                        Assert.That(Time.timeScale, Is.EqualTo(pausedScale));
                        Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(balancedPipeline));
                        Assert.That(preview.RearFog.isActiveAndEnabled, Is.True);
                        Assert.That((worldSun.type, worldSun.shadows, worldSun.intensity,
                            worldSun.color, worldSun.cullingMask, worldSun.enabled), Is.EqualTo(worldSunState));
                        Assert.That(worldSun.transform.rotation, Is.EqualTo(worldSunRotation),
                            "Local menu time must never move or reconfigure the world's sun.");
                        AssertMenuPaintPropertyBlocks(preview.Model, paint);
                        AssertMenuVehicleLampsAtTime(preview, lit: index > 0);
                        AssertMenuVehicleInsideFullViewport(preview);
                        AssertMenuOrbitCarSlot(preview);
                        AssertMenuVehicleOnAuthoredGround(preview);
                        Color[] pixels = ReadMenuPreviewLinearPixels((RenderTexture)preview.OutputTexture);
                        AssertMenuPreviewOpaqueFinitePixels(pixels, labels[index]);
                        var visibility = MeasureMenuVehicleReadability(preview, pixels);
                        File.AppendAllText(evidencePath, labels[index] + ": applied local=" +
                            preview.LightingLocalTime.ToString("O") + "; car crop mean luminance=" +
                            visibility.meanLuminance.ToString("G9") + "; readable fraction=" +
                            visibility.readableFraction.ToString("G9") + "; luminance deviation=" +
                            visibility.deviation.ToString("G9") + "; render count=" + preview.RenderCount + "\n");
                        yield return new WaitForEndOfFrame();
                        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        try { File.WriteAllBytes(Path.Combine(directory, "MainMenu-" + labels[index] + "-1920x1080.png"), screenshot.EncodeToPNG()); }
                        finally { UnityEngine.Object.Destroy(screenshot); }
                        // Broad visibility gates, not pixel-perfect art assertions.
                        Assert.That(visibility.meanLuminance, Is.GreaterThan(0.002f), labels[index] + ": car crop must remain readable.");
                        Assert.That(visibility.readableFraction, Is.GreaterThan(0.15f), labels[index] + ": night cannot hide the whole car.");
                        Assert.That(visibility.deviation, Is.GreaterThan(0.001f), labels[index] + ": car crop must contain visible shape variation.");
                        if (index == 0)
                        {
                            Assert.That(preview.LightingState.Daylight, Is.EqualTo(1f));
                            noonPixels = pixels;
                            noonVehicleMean = visibility.meanLuminance;
                        }
                        else
                        {
                            Assert.That(MeasureMenuAtmosphereDifference(noonPixels, pixels).meanDelta,
                                Is.GreaterThan(0.005f), labels[index] + ": applied time must visibly change the actual scene.");
                            if (index == 1) Assert.That(preview.LightingState.Twilight, Is.EqualTo(1f));
                            if (index == 2)
                            {
                                Assert.That(preview.LightingState.Night, Is.EqualTo(1f));
                                Assert.That(visibility.meanLuminance, Is.LessThan(noonVehicleMean * 0.8f),
                                    "Night must read as darker than noon while preserving a visible car.");
                            }
                        }


                        if (index == 2)
                            yield return AssertMenuVehicleLampRendering(preview, paint, directory, evidencePath);

                        int idleRenderCount = preview.RenderCount;
                        localTime = times[index].AddSeconds(45);
                        yield return new WaitForSecondsRealtime(1.2f);
                        Assert.That(preview.RenderCount, Is.EqualTo(idleRenderCount),
                            "A clock poll within the same applied minute must not issue additional render/blur requests.");
                        Assert.That(preview.LightingLocalTime, Is.EqualTo(times[index]));
                    }
                    // Also exercise night -> day; initial daytime-off alone
                    // cannot catch lamps that fail to switch back off.
                    localTime = times[2].Date.AddHours(12);
                    int dayReturnFrame = preview.RenderCount + 1;
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return AwaitMenuVehicleFrame(preview, dayReturnFrame);
                    Assert.That(preview.LightingLocalTime, Is.EqualTo(localTime));
                    AssertMenuVehicleLampsAtTime(preview, lit: false);
                    Assert.That(preview.OutputTexture, Is.SameAs(originalOutput));
                    Assert.That(preview.Model.transform.position, Is.EqualTo(modelPosition));
                    Assert.That(preview.Model.transform.rotation, Is.EqualTo(modelRotation));
                    Assert.That(preview.PreviewCamera.transform.position, Is.EqualTo(cameraPosition));
                    Assert.That(preview.PreviewCamera.transform.rotation, Is.EqualTo(cameraRotation));
                    Assert.That(JsonUtility.ToJson(gameTime.CaptureDto()), Is.EqualTo(worldClockSnapshot));
                    AssertMenuPaintPropertyBlocks(preview.Model, paint);
                    File.AppendAllText(evidencePath, "Returned to next noon through clock poll: vehicle lights/emission off; game clock, paint, camera and RT preserved. Rear images use a test-only diagnostic camera pose, restored afterwards.\n");
                    yield return DestroyFixture(fixture);
                    fixture = default;
                }
                finally
                {
                    DestroyFailedMenuInputFixture(fixture);
                    if (worldLighting != null) UnityEngine.Object.DestroyImmediate(worldLighting);
                    QualitySettings.SetQualityLevel(originalQuality, applyExpensiveChanges: true);
                }
            }
        }

        private static (float meanLuminance, float readableFraction, float deviation) MeasureMenuVehicleReadability(
            MainMenuVehiclePreview preview, Color[] pixels)
        {
            Bounds bounds = preview.Model.LocalBounds;
            Vector2 minimum = Vector2.one * float.PositiveInfinity;
            Vector2 maximum = Vector2.one * float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = preview.PreviewCamera.WorldToViewportPoint(preview.Model.transform.TransformPoint(
                    bounds.center + Vector3.Scale(bounds.extents, sign)));
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            int width = preview.OutputTexture.width, height = preview.OutputTexture.height;
            int left = Mathf.Clamp(Mathf.FloorToInt(minimum.x * width), 0, width - 1);
            int right = Mathf.Clamp(Mathf.CeilToInt(maximum.x * width), left + 1, width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(minimum.y * height), 0, height - 1);
            int top = Mathf.Clamp(Mathf.CeilToInt(maximum.y * height), bottom + 1, height);
            int count = 0, readable = 0;
            double sum = 0d, squares = 0d;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    Color pixel = pixels[y * width + x];
                    float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    count++;
                    sum += luminance;
                    squares += luminance * luminance;
                    if (luminance > 0.003f) readable++;
                }
            double mean = sum / count;
            return ((float)mean, readable / (float)count, (float)Math.Sqrt(Math.Max(0d, squares / count - mean * mean)));
        }
    }
}
#endif
