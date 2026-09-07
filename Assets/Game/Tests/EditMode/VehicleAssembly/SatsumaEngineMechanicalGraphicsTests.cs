using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineMechanicalGraphicsTests
    {
        [Test]
        public void RenderActualMechanicalGeometryAtMeasuredCyclePhases()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Run the mechanical graphic audit with a real graphics device.");
            var owned = new List<Object>();
            Fixture f = null; Camera camera = null; RenderTexture prior = RenderTexture.active;
            try
            {
                f = new Fixture(physicalScene: true);
                var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
                var cameraObject = new GameObject("TEST ONLY mechanical audit camera"); owned.Add(cameraObject);
                camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 5f;
                camera.backgroundColor = new Color(.035f, .045f, .065f); camera.clearFlags = CameraClearFlags.SolidColor;
                var volumeObject = new GameObject("TEST ONLY mechanical audit exposure"); owned.Add(volumeObject);
                Volume volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20000f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
                Exposure exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(12f);
                var lightObject = new GameObject("TEST ONLY mechanical audit sun"); owned.Add(lightObject);
                Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 45000f;
                lightObject.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
                var fillObject = new GameObject("TEST ONLY mechanical audit fill"); owned.Add(fillObject);
                Light fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 18000f;
                fillObject.transform.rotation = Quaternion.Euler(45f, 155f, 0f);
                Material Metal(Color color)
                {
                    var mat = new Material(Shader.Find("HDRP/Lit")); owned.Add(mat);
                    mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", .15f); mat.SetFloat("_Smoothness", .35f); return mat;
                }
                Material steel = Metal(new Color(.26f, .37f, .46f));
                Material piston = Metal(new Color(.72f, .64f, .42f));
                Material rockers = Metal(new Color(.58f, .62f, .65f));
                var target = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32); owned.Add(target); target.Create(); camera.targetTexture = target;
                var image = new Texture2D(1280, 800, TextureFormat.RGBA32, false); owned.Add(image);
                Directory.CreateDirectory("Logs/engine-motion-graphics-20260906");
                var all = f.Root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in all) renderer.enabled = false;
                void Show(MeshFilter filter, Material material)
                {
                    filter.gameObject.SetActive(true); var renderer = filter.GetComponent<Renderer>(); renderer.enabled = true;
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                }
                string PathOf(MeshFilter filter) => AssetDatabase.GetAssetPath(filter.sharedMesh);
                foreach (var binding in motion.Reciprocating)
                    if (binding.Kind == SatsumaReciprocatingKind.Piston &&
                        (PathOf(binding.Filter).EndsWith("/d915bf4de02b02045b3a48956fe9f783.asset") ||
                         PathOf(binding.Filter).EndsWith("/f48a6a1b2ea4b164d9d920ab78966d86.asset"))) Show(binding.Filter, piston);
                foreach (var binding in motion.Shafts)
                    if (binding.Drive == SatsumaMechanicalDrive.Crankshaft && PathOf(binding.Leaf.GetComponent<MeshFilter>()).EndsWith("/cef82582b4c013a4eab6bf26895cc151.asset"))
                        Show(binding.Leaf.GetComponent<MeshFilter>(), steel);

                void Frame(string name)
                {
                    camera.Render(); camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                    Color32[] pixels = image.GetPixels32(); int visible = 0;
                    foreach (Color32 pixel in pixels) if (pixel.r > 65 || pixel.g > 65 || pixel.b > 65) visible++;
                    Assert.That(visible, Is.GreaterThan(5000), "The HDRP mechanical audit must contain rendered geometry, not an empty frame.");
                    File.WriteAllBytes("Logs/engine-motion-graphics-20260906/" + name + ".png", image.EncodeToPNG());
                }
                Transform engine = f.Part("engine-block").transform;
                Vector3 focus = engine.TransformPoint(new Vector3(0f, 0f, .01f));
                camera.transform.position = focus + engine.TransformVector(new Vector3(.3f, -.75f, .24f));
                camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, engine.forward); camera.orthographicSize = .21f;
                var dto = f.Host.State.CaptureDto(); dto.engineRpm = 600f;
                Assert.That(f.Host.TryRestoreSimulationState(dto, out string failure), Is.True, failure);
                motion.ApplyFrame(0f); Frame("crank-000");
                for (int i = 1; i <= 3; i++) { motion.ApplyFrame(.025f); Frame("crank-" + (i * 90).ToString("000")); }
                motion.ResetPresentation();
                foreach (Renderer renderer in all) renderer.enabled = false;
                foreach (var binding in motion.Reciprocating)
                    if (binding.Kind != SatsumaReciprocatingKind.Piston) Show(binding.Filter, rockers);
                Transform rocker = f.Part("rocker-shaft").transform;
                focus = rocker.TransformPoint(new Vector3(0f, .012f, .01f));
                camera.transform.position = focus + rocker.TransformVector(new Vector3(.26f, -.5f, .3f));
                camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, rocker.forward); camera.orthographicSize = .14f;
                motion.ApplyFrame(0f); Frame("valves-000");
                for (int i = 1; i <= 3; i++) { motion.ApplyFrame(.05f); Frame("valves-" + (i * 180).ToString("000")); }
                Debug.Log("SATSUMA_MOTION_GRAPHICS_OK frames=8 actualMeshes=true auditOnlyMaterials=true sourceAssetsChanged=false");
            }
            finally
            {
                RenderTexture.active = prior;
                if (camera != null) camera.targetTexture = null;
                f?.Dispose();
                foreach (Object value in owned.AsEnumerable().Reverse())
                    if (value != null) { if (value is RenderTexture rt) rt.Release(); Object.DestroyImmediate(value); }
            }
        }
    }
}
