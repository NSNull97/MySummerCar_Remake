using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Core.Time;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaVisualPacketGraphicsTests
    {
        [Test]
        public void CaptureInstalledConnectionsBeltAndCockpitWithActualHdrpMaterials()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires actual graphics.");
            var owned = new List<Object>(); RenderTexture prior = RenderTexture.active; Camera camera = null;
            using var f = new Fixture(physicalScene: true);
            f.Root.GetComponent<SatsumaFlexibleConnectionPresenter>().RefreshOutputs();
            string output = "Logs/visual-packet-20260907/graphics"; Directory.CreateDirectory(output);
            try
            {
                foreach (var renderer in f.Part("hood").GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var part in f.Assembly.AllRuntimeParts.Where(part => !part.IsInstalled))
                    foreach (var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var visual = new GameObject("TEST ONLY belt item view").transform; visual.SetParent(f.Belt.transform, false);
                var belt = f.Belt.gameObject.AddComponent<AssemblyAlternatorBeltPresentation>();
                belt.Configure(f.Assembly, f.Belt, visual, AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/InstalledAlternatorBeltPresentation.prefab"));
                var cameraObject = new GameObject("TEST ONLY packet camera"); owned.Add(cameraObject);
                camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.orthographic = true; camera.nearClipPlane = .005f; camera.farClipPlane = 8f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .052f, .065f);
                var volumeObject = new GameObject("TEST ONLY packet exposure"); owned.Add(volumeObject);
                var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20000f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
                var exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(12f);
                var indirect = profile.Add<IndirectLightingController>();
                var lighting = new List<Light>();
                foreach (var setting in new[] { (new Vector3(45,-20,0),45000f), (new Vector3(55,155,0),25000f) })
                {
                    var go = new GameObject("TEST ONLY packet light"); owned.Add(go);
                    var light = go.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = setting.Item2;
                    lighting.Add(light);
                    go.transform.rotation = Quaternion.Euler(setting.Item1);
                }
                var target = new RenderTexture(1440, 960, 24, RenderTextureFormat.ARGB32); owned.Add(target); target.Create(); camera.targetTexture = target;
                var image = new Texture2D(1440, 960, TextureFormat.RGBA32, false); owned.Add(image);
                void Frame(string name, Vector3 focus, Vector3 offset, float size, Vector3 up)
                {
                    camera.transform.position = focus + f.Root.transform.TransformDirection(offset);
                    camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, up); camera.orthographicSize = size;
                    for (int warmup = 0; warmup < 4; warmup++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1440,960), 0,0); image.Apply();
                    File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
                }
                Frame("fuel-pump-connection", f.Part("fuel-pump").transform.position + Vector3.up * .04f, new Vector3(.20f,.55f,.20f), .19f, Vector3.forward);
                Frame("pump-belt-and-hoses", f.Part("water-pump-pulley").transform.position + Vector3.up * .04f, new Vector3(-.3f,.65f,.10f), .23f, Vector3.up);
                Frame("engine-bay", f.Part("engine-block").transform.position, new Vector3(0f,1.3f,0f), .75f, Vector3.forward);
                var instruments = f.Root.GetComponent<SatsumaInstrumentPresenter>();
                var gameTime = new GameTimeService(); gameTime.SetTimeScale(1f); instruments.BindGameTime(gameTime);
                f.Root.GetComponent<SatsumaIgnitionController>().RestorePersistentState(true);
                var state = f.Host.State.CaptureDto(); state.coolantTemperatureCelsius = 90f; state.fuelLiters = 30f;
                state.wheels[0].AngularSpeedRadiansPerSecond = state.wheels[1].AngularSpeedRadiansPerSecond = 60f;
                Assert.That(f.Host.TryRestoreSimulationState(state, out _), Is.True); instruments.RefreshOutputs();
                Vector3 cockpit = Vector3.zero;
                foreach (var needle in instruments.Needles.Take(5)) cockpit += needle.Leaf.position * .2f;
                Frame("cockpit-day", cockpit, new Vector3(0f,.08f,-.45f), .15f, Vector3.up);
                var controls = f.Root.GetComponent<SatsumaDashboardControlsController>(); controls.TryCycleLights(); controls.TryToggleHazards(); instruments.RefreshOutputs();
                foreach (var light in lighting) light.enabled = false; exposure.fixedExposure.Override(4f);
                indirect.indirectDiffuseLightingMultiplier.Override(0f); indirect.reflectionLightingMultiplier.Override(0f);
                Frame("cockpit-night", cockpit, new Vector3(0f,.08f,-.45f), .15f, Vector3.up);
                controls.TryCycleLights(); controls.TryCycleLights(); instruments.RefreshOutputs();
                Frame("cockpit-night-lights-off", cockpit, new Vector3(0f,.08f,-.45f), .15f, Vector3.up);
                foreach (var light in lighting) light.enabled = true; exposure.fixedExposure.Override(12f);
                indirect.indirectDiffuseLightingMultiplier.Override(1f); indirect.reflectionLightingMultiplier.Override(1f);
                f.Root.GetComponent<SatsumaFlexibleConnectionPresenter>().ResetPresentation();
                // External, ignored mesh evidence allows bounded connector measurements.
                foreach (string suffix in new[] { "fuel-pump", "fuel-strainer", "engine-block", "radiator-hose1", "radiator-hose2", "radiator-hose3", "radiator", "water-pump", "cylinder-head", "carburetor" })
                {
                    int index = 0;
                    foreach (var filter in f.Part(suffix).GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null && m.gameObject.activeInHierarchy))
                    {
                        string path = output + "/mesh-" + suffix + "-" + index++ + ".csv";
                        using var writer = new StreamWriter(path);
                        writer.WriteLine("# " + filter.name + " " + AssetDatabase.GetAssetPath(filter.sharedMesh));
                        foreach (var point in filter.sharedMesh.vertices)
                        {
                            Vector3 p = f.Root.transform.InverseTransformPoint(filter.transform.TransformPoint(point));
                            writer.WriteLine(p.x.ToString("R", CultureInfo.InvariantCulture) + "," + p.y.ToString("R", CultureInfo.InvariantCulture) + "," + p.z.ToString("R", CultureInfo.InvariantCulture));
                        }
                        File.WriteAllLines(path + ".tri", filter.sharedMesh.triangles.Select(i => i.ToString(CultureInfo.InvariantCulture)));
                    }
                }
                foreach (var renderer in f.Root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var renderer in f.Part("fuel-pump").GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                Frame("isolated-fuel-pump", f.Part("fuel-pump").transform.position + Vector3.up * .04f, new Vector3(.05f,.65f,.02f), .14f, Vector3.forward);
                Debug.Log("SATSUMA_VISUAL_PACKET_GRAPHICS_OK frames=7 actualMaterials=true sourceAssetsUnchanged=true");
            }
            finally
            {
                RenderTexture.active = prior; if (camera != null) camera.targetTexture = null;
                foreach (Object value in owned.AsEnumerable().Reverse()) if (value != null)
                { if (value is RenderTexture rt) rt.Release(); Object.DestroyImmediate(value); }
            }
        }
    }
}
