using System.Collections;
using System.IO;
using System.Linq;
using MSC.Needs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class FirstPersonLifeActionViewmodelSkinningPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedViewmodel_RealtimePlaybackUsesFreshSkinMatrices()
        {
            var cameraObject = new GameObject("Viewmodel realtime test camera");
            var keyLightObject = new GameObject("Viewmodel realtime key light");
            var fillLightObject = new GameObject("Viewmodel realtime fill light");
            var volumeObject = new GameObject("Viewmodel realtime exposure");
            var target = new RenderTexture(
                1280,
                720,
                24,
                RenderTextureFormat.ARGB32);
            FirstPersonLifeActionViewmodelBinding binding = null;
            VolumeProfile volumeProfile = null;
            GameObject bottleProxy = null;
            GameObject cigaretteProxy = null;
            Material bottleProxyMaterial = null;
            Material cigaretteProxyMaterial = null;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
                camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(
                    120f,
                    16f / 9f);
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 5f;
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.targetTexture = target;
                target.Create();

                ConfigureDirectionalLight(
                    keyLightObject,
                    new Vector3(42f, -32f, 0f),
                    100000f,
                    new Color(1f, 0.94f, 0.87f));
                ConfigureDirectionalLight(
                    fillLightObject,
                    new Vector3(325f, 148f, 0f),
                    28000f,
                    new Color(0.56f, 0.72f, 1f));
                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100000f;
                volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                Exposure exposure = volumeProfile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(14f);
                volume.sharedProfile = volumeProfile;

                Assert.That(
                    FirstPersonLifeActionViewmodelBinding.
                        TryInstantiateLocalPhase1(
                            camera.transform,
                            out binding),
                    Is.True);

                bottleProxyMaterial = CreateUnlitMaterial(
                    "Bottle grip audit material",
                    new Color(0.35f, 0.16f, 0.055f, 1f));
                bottleProxy = CreateBottleProp(
                    binding.DrinkGripAnchor,
                    bottleProxyMaterial);
                cigaretteProxyMaterial = CreateUnlitMaterial(
                    "Cigarette grip audit material",
                    new Color(0.91f, 0.88f, 0.80f, 1f));
                cigaretteProxy = CreateCylinderProp(
                    "Cigarette grip audit proxy",
                    binding.CigaretteGripAnchor,
                    new Vector3(0f, 0f, -0.028f),
                    Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(0.008f, 0.045f, 0.008f),
                    cigaretteProxyMaterial);

                Assert.That(
                    binding.CigaretteGripAnchor.parent.name,
                    Is.EqualTo("DEF-f_index.02.R"));

                foreach (FirstPersonLifeActionVisual visual in new[]
                         {
                             FirstPersonLifeActionVisual.Drink,
                             FirstPersonLifeActionVisual.Smoke,
                             FirstPersonLifeActionVisual.Hello,
                             FirstPersonLifeActionVisual.MiddleFinger,
                             FirstPersonLifeActionVisual.Push,
                             FirstPersonLifeActionVisual.Fist,
                         })
                {
                    bottleProxy.SetActive(
                        visual == FirstPersonLifeActionVisual.Drink);
                    cigaretteProxy.SetActive(
                        visual == FirstPersonLifeActionVisual.Smoke);
                    Assert.That(binding.Play(visual), Is.True, visual.ToString());
                    float visualStartedAt = Time.unscaledTime;
                    float captureAt = visualStartedAt + GetAuditTime(visual);
                    while (Time.unscaledTime < captureAt)
                    {
                        yield return null;
                    }

                    // HDRP's first explicit render can be a warm-up frame.
                    camera.Render();
                    camera.Render();
                    WriteAuditFrame(target, visual.ToString());

                    if (visual == FirstPersonLifeActionVisual.Drink)
                    {
                        WriteAuditFrame(target, "Drink_00_Mouth");
                        foreach ((float sampleTime, string label) in new[]
                                 {
                                     (2.52f, "Drink_01_FirstSip"),
                                     (4.52f, "Drink_02_SecondSip"),
                                     (6.52f, "Drink_03_ThirdSip"),
                                     (8.52f, "Drink_04_FourthSip"),
                                     (10.75f, "Drink_05_Return"),
                                 })
                        {
                            float nextCaptureAt = visualStartedAt + sampleTime;
                            while (Time.unscaledTime < nextCaptureAt)
                            {
                                yield return null;
                            }

                            camera.Render();
                            camera.Render();
                            WriteAuditFrame(target, label);
                        }
                    }

                    if (visual == FirstPersonLifeActionVisual.Smoke)
                    {
                        Transform smokeRoot = binding.transform.Find("Smoke");
                        Assert.That(smokeRoot, Is.Not.Null);
                        Transform indexMiddle = smokeRoot
                            .GetComponentsInChildren<Transform>(true)
                            .Single(candidate =>
                                candidate.name == "DEF-f_index.02.R");
                        Transform middleMiddle = smokeRoot
                            .GetComponentsInChildren<Transform>(true)
                            .Single(candidate =>
                                candidate.name == "DEF-f_middle.02.R");
                        Assert.That(
                            Vector3.Distance(
                                binding.CigaretteGripAnchor.position,
                                indexMiddle.position),
                            Is.LessThan(0.05f));
                        Assert.That(
                            Vector3.Distance(
                                binding.CigaretteGripAnchor.position,
                                middleMiddle.position),
                            Is.LessThan(0.05f));
                    }

                    SkinnedMeshRenderer[] renderers = binding
                        .GetComponentsInChildren<SkinnedMeshRenderer>(false);
                    Assert.That(renderers, Is.Not.Empty, visual.ToString());
                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        Assert.That(renderer.updateWhenOffscreen, Is.True);
                        Assert.That(
                            renderer.forceMatrixRecalculationPerRender,
                            Is.True);
                        Assert.That(renderer.skinnedMotionVectors, Is.False);

                        var baked = new Mesh();
                        try
                        {
                            renderer.BakeMesh(baked, useScale: true);
                            Vector3[] vertices = baked.vertices;
                            int[] usedIndices = Enumerable.Range(
                                    0,
                                    baked.subMeshCount)
                                .SelectMany(baked.GetTriangles)
                                .Distinct()
                                .ToArray();
                            Assert.That(usedIndices, Is.Not.Empty);
                            Bounds cameraLocal = new Bounds(
                                camera.transform.InverseTransformPoint(
                                    renderer.transform.TransformPoint(
                                        vertices[usedIndices[0]])),
                                Vector3.zero);
                            foreach (int index in usedIndices)
                            {
                                cameraLocal.Encapsulate(
                                    camera.transform.InverseTransformPoint(
                                        renderer.transform.TransformPoint(
                                            vertices[index])));
                            }

                            Assert.That(
                                cameraLocal.size.x,
                                Is.LessThan(0.75f),
                                $"{visual} widened across the camera.");
                            Assert.That(
                                cameraLocal.size.y,
                                Is.LessThan(0.85f),
                                $"{visual} stretched vertically.");
                            Assert.That(
                                cameraLocal.size.z,
                                Is.LessThan(0.85f),
                                $"{visual} stretched through the camera.");
                            Assert.That(
                                cameraLocal.center.z,
                                Is.GreaterThan(0.02f),
                                $"{visual} was centred behind the near plane.");
                        }
                        finally
                        {
                            Object.Destroy(baked);
                        }
                    }

                    binding.Stop();
                    yield return null;
                }
            }
            finally
            {
                if (binding != null)
                {
                    Object.Destroy(binding.gameObject);
                }

                Object.Destroy(bottleProxy);
                Object.Destroy(cigaretteProxy);
                Object.Destroy(bottleProxyMaterial);
                Object.Destroy(cigaretteProxyMaterial);

                if (volumeProfile != null)
                {
                    Object.Destroy(volumeProfile);
                }

                target.Release();
                Object.Destroy(target);
                Object.Destroy(volumeObject);
                Object.Destroy(fillLightObject);
                Object.Destroy(keyLightObject);
                Object.Destroy(cameraObject);
            }
        }

        private static float GetAuditTime(FirstPersonLifeActionVisual visual)
        {
            return visual switch
            {
                FirstPersonLifeActionVisual.Drink => 0.73f,
                // The first 0.5 seconds are the entrance clip. Capture its
                // readable end pose instead of the deliberately hidden midpoint.
                FirstPersonLifeActionVisual.Smoke => 0.48f,
                FirstPersonLifeActionVisual.Hello => 0.8f,
                FirstPersonLifeActionVisual.MiddleFinger => 0.45f,
                FirstPersonLifeActionVisual.Push => 0.39f,
                FirstPersonLifeActionVisual.Fist => 0.6f,
                _ => 0f,
            };
        }

        private static void ConfigureDirectionalLight(
            GameObject lightObject,
            Vector3 eulerAngles,
            float intensityLux,
            Color color)
        {
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.lightUnit = LightUnit.Lux;
            light.intensity = intensityLux;
            light.color = color;
            light.shadows = LightShadows.None;
            lightObject.AddComponent<HDAdditionalLightData>();
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            Shader shader =
                Shader.Find("HDRP/Unlit") ??
                Shader.Find("HDRP/Lit") ??
                Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                name = name,
                color = color,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static GameObject CreateCylinderProp(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject prop = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            prop.name = name;
            Object.Destroy(prop.GetComponent<Collider>());
            prop.transform.SetParent(parent, worldPositionStays: false);
            prop.transform.SetLocalPositionAndRotation(
                localPosition,
                localRotation);
            prop.transform.localScale = localScale;
            prop.GetComponent<MeshRenderer>().sharedMaterial = material;
            return prop;
        }

        private static GameObject CreateBottleProp(
            Transform parent,
            Material fallbackMaterial)
        {
#if UNITY_EDITOR
            Mesh bottleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                "Meshes/LegacyItemMesh_5103d9d7418206a4da09260262c716a0.asset");
            if (bottleMesh != null)
            {
                var bottle = new GameObject("Beer bottle grip audit proxy");
                bottle.transform.SetParent(parent, worldPositionStays: false);
                bottle.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                bottle.AddComponent<MeshFilter>().sharedMesh = bottleMesh;
                Material bottleMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
                    "Materials/LegacyItemMaterial_505c0da5d59fb1a4fbc38445d0494ef3.mat");
                bottle.AddComponent<MeshRenderer>().sharedMaterial =
                    bottleMaterial != null
                        ? bottleMaterial
                        : fallbackMaterial;
                return bottle;
            }
#endif
            return CreateCylinderProp(
                "Bottle grip audit fallback",
                parent,
                Vector3.zero,
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.0608f, 0.0844f, 0.0608f),
                fallbackMaterial);
        }

        private static void WriteAuditFrame(
            RenderTexture target,
            string label)
        {
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(
                target.width,
                target.height,
                TextureFormat.RGB24,
                mipChain: false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(
                    new Rect(0f, 0f, target.width, target.height),
                    0,
                    0,
                    recalculateMipMaps: false);
                pixels.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                Color32[] colors = pixels.GetPixels32();
                int foregroundPixels = colors.Count(color =>
                    color.r > 70 && color.g > 45 && color.b > 32);
                string projectRoot = Directory.GetParent(Application.dataPath)
                    ?.FullName ?? Application.dataPath;
                string outputDirectory = Path.Combine(
                    projectRoot,
                    "Logs",
                    "AxisHandsRealtimePlaybackAudit");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, label + ".png"),
                    pixels.EncodeToPNG());
                Assert.That(
                    foregroundPixels,
                    Is.GreaterThan(1500),
                    $"{label} disappeared from the realtime camera frame.");
                Assert.That(
                    foregroundPixels,
                    Is.LessThan(target.width * target.height * 0.28f),
                    $"{label} exploded across the realtime camera frame.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(pixels);
            }
        }
    }
}
