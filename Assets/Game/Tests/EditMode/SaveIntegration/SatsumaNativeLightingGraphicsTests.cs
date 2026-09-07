using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void ReadOnlyNativeCockpitSwitchAndServicedHeadlightsRenderOnActualCanonicalCar()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("This audit requires D3D11, not -nographics.");
            WithReadOnlyNativeEngine(document =>
            {
                using var fixture = new Fixture("item.light-bulb", "mount.satsuma.headlight-left.light-bulb",
                    usePreviewScene: false, allEngineConsumables: true, useCurrentEditorScene: true);
                RestoreEngineDocument(fixture, document);
                var owned = new List<Object>();
                var mutedExternalLights = new List<Light>();
                Camera camera = null;
                RenderTexture previous = RenderTexture.active;
                const string directory = "Logs/native-headlight-graphics-20260906";
                try
                {
                    // All servicing below is this disposable restored copy only.
                    // No simulation tick, source-save rewrite or automatic repair.
                    fixture.Assembly.transform.parent.gameObject.SetActive(true);
                    Transform car = fixture.Assembly.transform;
                    var controls = car.GetComponent<SatsumaDashboardControlsController>();
                    var lighting = car.GetComponent<SatsumaDashboardLightingPresenter>();
                    Assert.That(controls.CanOperate, Is.True);
                    Assert.That(controls.Electrical.ElectricsOk, Is.True);
                    foreach (PartInstance part in fixture.Assembly.Parts)
                        if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);
                    GameObject NewRoot(string name)
                    {
                        var go = new GameObject("TEST ONLY " + name); owned.Add(go);
                        SceneManager.MoveGameObjectToScene(go, fixture.Scene); return go;
                    }
                    camera = NewRoot("native lighting camera").AddComponent<Camera>();
                    var hdCamera = camera.gameObject.AddComponent<HDAdditionalCameraData>();
                    hdCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                    hdCamera.backgroundColorHDR = Color.black;
                    camera.nearClipPlane = .015f; camera.farClipPlane = 100f; camera.fieldOfView = 65f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f, .02f, .025f);
                    var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); owned.Add(target);
                    target.Create(); camera.targetTexture = target;
                    var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); owned.Add(pixels);
                    var volume = NewRoot("native lighting exposure").AddComponent<Volume>();
                    volume.isGlobal = true; volume.priority = 20000f;
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
                    var exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(11f);
                    Light Daylight(Vector3 euler, float intensity)
                    {
                        var light = NewRoot("daylight").AddComponent<Light>();
                        light.type = LightType.Directional; light.intensity = intensity;
                        light.transform.rotation = car.rotation * Quaternion.Euler(euler); return light;
                    }
                    var sun = Daylight(new Vector3(35f, 150f, 0f), 45000f);
                    var fill = Daylight(new Vector3(45f, -20f, 0f), 18000f);
                    Directory.CreateDirectory(directory);
                    Color32[] Capture(string name)
                    {
                        lighting.RefreshOutputs(); camera.Render(); camera.Render(); RenderTexture.active = target;
                        var stack = HDCamera.GetOrCreate(camera).volumeStack;
                        TestContext.WriteLine("LIGHT_CAPTURE " + name + " exposure=" + stack.GetComponent<Exposure>().fixedExposure.value +
                            " sky=" + stack.GetComponent<VisualEnvironment>().skyType.value +
                            " indirect=" + stack.GetComponent<IndirectLightingController>().indirectDiffuseLightingMultiplier.value +
                            " clear=" + hdCamera.clearColorMode);
                        pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0); pixels.Apply();
                        File.WriteAllBytes(directory + "/" + name + ".png", pixels.EncodeToPNG());
                        return pixels.GetPixels32();
                    }
                    void Aim(Vector3 from, Vector3 toward)
                    {
                        camera.transform.position = car.TransformPoint(from);
                        camera.transform.rotation = Quaternion.LookRotation(car.TransformPoint(toward) - camera.transform.position, car.up);
                    }
                    Vector3 knobPosition = car.InverseTransformPoint(controls.LightsKnob.GetComponent<Renderer>().bounds.center);
                    TestContext.WriteLine("NATIVE_LIGHT_SWITCH carLocal=" + knobPosition.ToString("F6") +
                        " enabled=" + controls.LightsKnob.GetComponent<Renderer>().enabled +
                        " active=" + controls.LightsKnob.gameObject.activeInHierarchy);
                    Aim(new Vector3(-.25f, .68f, .02f), new Vector3(-.12f, .44f, .63f)); Capture("cockpit-overview");
                    Aim(new Vector3(-.25f, .68f, .02f), knobPosition); Capture("cockpit-lights-knob");
                    Assert.That(controls.TryRestore(null, out _), Is.True);
                    Assert.That(controls.TryCycleLights(), Is.True); Assert.That(controls.TryCycleLights(), Is.True);
                    lighting.RefreshOutputs();
                    var lamps = lighting.Lamps.Where(value => value.Function == SatsumaDashboardLampFunction.Headlights).ToArray();
                    Assert.That(lamps, Has.Length.EqualTo(2));
                    foreach (var lamp in lamps)
                    {
                        Assert.That(lamp.LensRenderer, Is.Not.Null, "The road beam and visible bulb surface must both be bound.");
                        Assert.That(fixture.Assembly.Graph.TryGetMount(lamp.OwnerMountId, out var mount), Is.True);
                        TestContext.WriteLine("NATIVE_HEADLIGHT " + lamp.StableLampId + " stages=" +
                            string.Join(",", mount.Fasteners.Select(value => value.Stage)) + " enabled=" + lamp.Light.enabled);
                        Assert.That(mount.FastenerGroup.IsBolted, Is.False, "Expected user's unfastened source, never silently serviced.");
                        Assert.That(lamp.Light.enabled, Is.False);
                        Assert.That(lamp.IsLensEmitting, Is.False);
                        foreach (var fastener in mount.Fasteners)
                        {
                            ToolDefinition tool = fixture.Assembly.Tools.First(value => fastener.Definition.ToolRule.Matches(value));
                            TestContext.WriteLine("HEADLIGHT_TOOL " + tool.name);
                            while (fastener.Stage < fastener.Definition.MaximumStage)
                                Assert.That(fixture.Assembly.TryOperateFastener(mount.MountId,
                                    fastener.Definition.DefinitionId, tool, true).Succeeded, Is.True);
                        }
                        Assert.That(lamp.IsReady(fixture.Assembly, controls.Electrical), Is.True);
                    }
                    lighting.RefreshOutputs(); Assert.That(lamps.All(value => value.Light.enabled && value.IsLensEmitting), Is.True);
                    foreach (var lamp in lamps)
                    {
                        Light light = lamp.Light;
                        TestContext.WriteLine("HEADLIGHT_RENDER " + lamp.StableLampId + " active=" + light.gameObject.activeInHierarchy +
                            " pos=" + car.InverseTransformPoint(light.transform.position).ToString("F4") +
                            " forward=" + car.InverseTransformDirection(light.transform.forward).ToString("F4") +
                            " intensity=" + light.intensity + " range=" + light.range + " unit=" + light.lightUnit);
                        Assert.That(light.isActiveAndEnabled, Is.True, "Enabled flag on a hidden light object is not illumination.");
                    }
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); owned.Add(floor);
                    SceneManager.MoveGameObjectToScene(floor, fixture.Scene);
                    floor.transform.SetPositionAndRotation(car.TransformPoint(new Vector3(0f, -.42f, 11f)), car.rotation);
                    floor.transform.localScale = new Vector3(5f, 1f, 5f);
                    var material = new Material(Shader.Find("HDRP/Lit")); owned.Add(material);
                    material.SetColor("_BaseColor", new Color(.18f, .18f, .18f)); material.SetFloat("_Smoothness", 0f);
                    floor.GetComponent<Renderer>().sharedMaterial = material;
                    sun.enabled = fill.enabled = false; exposure.fixedExposure.Override(4f);
                    foreach (Light light in Object.FindObjectsByType<Light>())
                    {
                        if (!light.isActiveAndEnabled || lighting.Lamps.Any(value => value.Light == light)) continue;
                        TestContext.WriteLine("TEST_MUTED_EXTERNAL_LIGHT " + light.name + " type=" + light.type + " intensity=" + light.intensity);
                        mutedExternalLights.Add(light); light.enabled = false;
                    }
                    // The default HDRP sky still provides daylight when our
                    // two explicit lights are disabled. Isolate nighttime
                    // illumination without changing the project's sky/weather.
                    profile.Add<VisualEnvironment>().skyType.Override(0);
                    var indirect = profile.Add<IndirectLightingController>();
                    indirect.indirectDiffuseLightingMultiplier.Override(0f);
                    indirect.reflectionLightingMultiplier.Override(0f);
                    profile.Add<Fog>().enabled.Override(false);
                    controls.TryRestore(null, out _);
                    Aim(new Vector3(3.1f, 1.8f, 5.8f), new Vector3(0f, 0f, 2.5f));
                    Color32[] off = Capture("front-night-off");
                    Assert.That(lamps.All(value => !value.Light.enabled && !value.IsLensEmitting), Is.True);
                    Assert.That(controls.TryCycleLights(), Is.True); Capture("front-night-parking");
                    Assert.That(controls.TryCycleLights(), Is.True); Color32[] on = Capture("front-night-headlights");
                    foreach (var lamp in lamps)
                    {
                        Vector3 pixel = camera.WorldToScreenPoint(lamp.LensRenderer.bounds.center);
                        int brightLensPixels = 0;
                        for (int dy = -12; dy <= 12; dy++)
                            for (int dx = -12; dx <= 12; dx++)
                            {
                                int x = Mathf.Clamp(Mathf.RoundToInt(pixel.x) + dx, 0, 1279);
                                int y = Mathf.Clamp(Mathf.RoundToInt(pixel.y) + dy, 0, 719);
                                if (on[y * 1280 + x].r + on[y * 1280 + x].g + on[y * 1280 + x].b > 150) brightLensPixels++;
                            }
                        Assert.That(brightLensPixels, Is.GreaterThan(8), lamp.StableLampId + " has an invisible lit lens.");
                    }
                    int brighter = 0;
                    for (int i = 0; i < off.Length; i++)
                        if (on[i].r + on[i].g + on[i].b > off[i].r + off[i].g + off[i].b + 45) brighter++;
                    Assert.That(brighter, Is.GreaterThan(5000), "Enabled components must produce visible road illumination.");
                    Assert.That(brighter, Is.LessThan(on.Length * .55f), "Lens emission must not bloom over most of the frame.");
                    Aim(new Vector3(-.25f, .68f, .02f), new Vector3(0f, -.25f, 12f)); Capture("driver-night-headlights");
                    Debug.Log("NATIVE_HEADLIGHT_GRAPHICS_OK sourceUnfastened=true servicedCopyOnly=true frames=6 brighterPixels=" + brighter);
                }
                finally
                {
                    RenderTexture.active = previous;
                    foreach (Light light in mutedExternalLights) if (light != null) light.enabled = true;
                    if (camera != null) camera.targetTexture = null;
                    foreach (Object value in owned.AsEnumerable().Reverse())
                        if (value != null) { if (value is RenderTexture rt) rt.Release(); Object.DestroyImmediate(value); }
                }
            });
        }
    }
}
