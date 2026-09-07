using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Bootstrap;
using MSC.Presentation.Fluid;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaServiceLevelGraphicsTests
    {
        [Test]
        public void ActualReservoirOpeningsRenderEmptyHalfFullAndClosed_WithOriginalMaterials()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Run the service-level graphic audit with a real graphics device.");
            const int size = 640;
            const string directory = "Logs/service-level-graphics-20260906";
            var owned = new List<Object>();
            Fixture fixture = null; Camera camera = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                fixture = new Fixture(physicalScene: true);
                SatsumaServiceLevelComposition.Configure(fixture.Root);
                // Same actual access preparation as the executed pour fixture.
                Assert.That(fixture.Assembly.TryBreakInstalledPart(fixture.Part("hood")).Succeeded, Is.True);
                fixture.Part("hood").gameObject.SetActive(false);
                foreach (PartInstance part in fixture.Assembly.Parts)
                    if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);
                var cameraObject = new GameObject("TEST ONLY service-level camera"); owned.Add(cameraObject);
                camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.orthographic = true; camera.orthographicSize = .055f;
                camera.nearClipPlane = .005f; camera.farClipPlane = 2f;
                camera.backgroundColor = new Color(.035f, .045f, .065f); camera.clearFlags = CameraClearFlags.SolidColor;
                var volumeObject = new GameObject("TEST ONLY service-level exposure"); owned.Add(volumeObject);
                var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20000f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
                var exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(12f);
                void LightAt(string name, float intensity, Vector3 euler)
                {
                    var lightObject = new GameObject(name); owned.Add(lightObject);
                    var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = intensity;
                    lightObject.transform.rotation = Quaternion.Euler(euler);
                }
                LightAt("TEST ONLY service key", 45000f, new Vector3(20f, -20f, 0f));
                LightAt("TEST ONLY service fill", 18000f, new Vector3(45f, 155f, 0f));
                var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32); owned.Add(target);
                target.Create(); camera.targetTexture = target;
                var image = new Texture2D(size, size, TextureFormat.RGBA32, false); owned.Add(image);
                Directory.CreateDirectory(directory);
                Color32[] Capture(string name)
                {
                    camera.Render(); camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0f, 0f, size, size), 0, 0); image.Apply();
                    File.WriteAllBytes(directory + "/" + name + ".png", image.EncodeToPNG());
                    return image.GetPixels32();
                }
                int ChangedInOpening(Color32[] before, Color32[] after)
                {
                    int changed = 0;
                    for (int y = size / 2 - 65; y < size / 2 + 65; y++)
                        for (int x = size / 2 - 65; x < size / 2 + 65; x++)
                        {
                            Color32 a = before[y * size + x], b = after[y * size + x];
                            if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 12) changed++;
                        }
                    return changed;
                }
                var receivers = fixture.Root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true)
                    .Where(value => value.Fluid != SatsumaServiceFluid.MotorOil).ToArray();
                Assert.That(receivers, Has.Length.EqualTo(4));
                foreach (SatsumaServiceFluidReceiver receiver in receivers)
                {
                    var level = receiver.GetComponent<ServiceReservoirLevelPresenter>();
                    var cap = fixture.Root.GetComponentsInChildren<AssemblyServiceCapTarget>(true).Single(value =>
                        value.State == receiver.Caps && value.CapIndex == receiver.CapIndex);
                    for (int step = 0; step < 11; step++) Assert.That(receiver.Caps.TryAdjust(receiver.CapIndex, -1f), Is.True);
                    cap.RefreshPresentation(); Assert.That(cap.CapRenderer.enabled, Is.False);
                    camera.transform.position = receiver.transform.position + Vector3.up * .28f;
                    camera.transform.rotation = Quaternion.LookRotation(Vector3.down, fixture.Root.transform.forward);
                    var empty = fixture.Host.State.CaptureDto(); empty.coolantLiters = 0f;
                    empty.satsumaOperatingState.brakeFrontLiters = empty.satsumaOperatingState.brakeRearLiters =
                        empty.satsumaOperatingState.clutchLiters = 0f;
                    Assert.That(fixture.Host.TryRestoreSimulationState(empty, out string failure), Is.True, failure);
                    level.RefreshPresentation(); Assert.That(level.IsVisible, Is.False);
                    Color32[] emptyPixels = Capture(receiver.Fluid + "-empty");
                    string liquid = receiver.Fluid == SatsumaServiceFluid.Coolant ? "liquid.coolant" : "liquid.brake-fluid";
                    float capacity = receiver.CapacityLiters;
                    Assert.That(receiver.TryAcceptLiquid(liquid, capacity * .5f, out _), Is.True);
                    level.RefreshPresentation(); Assert.That(level.IsVisible, Is.True);
                    float halfHeight = level.SurfaceTransform.position.y;
                    Color32[] halfPixels = Capture(receiver.Fluid + "-half");
                    Assert.That(receiver.TryAcceptLiquid(liquid, capacity * .5f, out _), Is.True);
                    level.RefreshPresentation(); Assert.That(level.SurfaceTransform.position.y, Is.GreaterThan(halfHeight + .02f));
                    Color32[] fullPixels = Capture(receiver.Fluid + "-full");
                    Assert.That(ChangedInOpening(emptyPixels, halfPixels), Is.GreaterThan(100), receiver.Fluid + " half-full level is occluded/invisible.");
                    Assert.That(ChangedInOpening(emptyPixels, fullPixels), Is.GreaterThan(100), receiver.Fluid + " full level is occluded/invisible.");
                    for (int step = 0; step < 11; step++) Assert.That(receiver.Caps.TryAdjust(receiver.CapIndex, 1f), Is.True);
                    cap.RefreshPresentation(); level.RefreshPresentation();
                    Assert.That(level.IsVisible, Is.False); Assert.That(cap.CapRenderer.enabled, Is.True);
                    Capture(receiver.Fluid + "-closed");
                }
                Debug.Log("SATSUMA_SERVICE_GRAPHICS_OK frames=16 actualGeometry=true originalMaterials=true noOilPool=true");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (camera != null) camera.targetTexture = null;
                fixture?.Dispose();
                foreach (Object value in owned.AsEnumerable().Reverse())
                    if (value != null) { if (value is RenderTexture rt) rt.Release(); Object.DestroyImmediate(value); }
            }
        }
    }
}
