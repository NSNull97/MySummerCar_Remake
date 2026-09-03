using MSC.Editor.WorldBaseline;
using MSC.Editor.Vegetation;
using MSC.World.Presentation;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class Phase1TreePlacementPolicyEditModeTests
    {
        [Test]
        public void TreeHeightScale_ReducesTreesByRequestedFactor()
        {
            float reductionFactor =
                1f / Phase1ForestRemediationBuilder.TreeHeightScale;

            Assert.That(reductionFactor, Is.InRange(1.25f, 1.5f));
        }

        [Test]
        public void TreeSpeciesMix_UsesRequestedSpruceShare()
        {
            Assert.That(
                Phase1ForestRemediationBuilder.SpruceDistributionPercent,
                Is.EqualTo(65));
            Assert.That(Phase1ForestRemediationBuilder.PineDistributionPercent, Is.EqualTo(20));
            Assert.That(Phase1ForestRemediationBuilder.BirchDistributionPercent, Is.EqualTo(7.5f));
            Assert.That(Phase1ForestRemediationBuilder.AspenDistributionPercent, Is.EqualTo(7.5f));
        }

        [TestCase("1", 0.32f)]
        [TestCase("2", 0.20f)]
        [TestCase("5", 0.19f)]
        [TestCase("8", 0.15f)]
        [TestCase("9", 0.14f)]
        public void EngelmannGrounding_UsesVariantSpecificRootProfile(
            string variantId,
            float expectedFraction)
        {
            Assert.That(
                Phase1VegetationAssetBuilder
                    .GetEngelmannRootBurialFraction(variantId),
                Is.EqualTo(expectedFraction).Within(0.0001f));
        }

        [Test]
        public void SpruceWind_UsesDedicatedGpuShaderContract()
        {
            Assert.That(
                Phase1VegetationAssetBuilder.NorwaySpruceWindShaderName,
                Is.EqualTo(Phase1SpruceWindController.ShaderName));
            Assert.That(
                Phase1SpruceWindController.DirectionProperty,
                Does.StartWith("_MSC_SpruceWind"));
            Assert.That(
                Phase1SpruceWindController.IntensityProperty,
                Does.StartWith("_MSC_SpruceWind"));
        }

        [Test]
        public void SpruceWind_UsesLitNonEmissiveMaterialContract()
        {
            Shader shader = Shader.Find(
                Phase1SpruceWindController.ShaderName);
            Assert.That(shader, Is.Not.Null);

            string[] materialGuids = AssetDatabase.FindAssets(
                "t:Material",
                new[]
                {
                    "Assets/Game/Presentation/Vegetation/Generated/" +
                    "Phase1/Materials/EngelmannSpruce",
                    "Assets/Game/Presentation/Vegetation/Generated/" +
                    "Phase1/Materials/NorwaySpruceHD"
                });
            int windMaterialCount = 0;
            foreach (string guid in materialGuids)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader != shader)
                {
                    continue;
                }

                windMaterialCount++;
                Assert.That(
                    material.GetTag("RenderType", false, string.Empty),
                    Is.EqualTo("HDLitShader"));
                Assert.That(
                    material.FindPass("ForwardOnly"),
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(material.HasProperty("_BaseColorMap"), Is.True);
                Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null);
                Assert.That(material.HasProperty("_UnlitColorMap"), Is.False);
                Assert.That(
                    material.GetColor("_EmissiveColor").maxColorComponent,
                    Is.Zero.Within(0.0001f));
                Assert.That(
                    material.GetColor("_EmissionColor").maxColorComponent,
                    Is.Zero.Within(0.0001f));
                Assert.That(
                    material.GetFloat("_WindStrengthMeters"),
                    Is.LessThanOrEqualTo(0.36f));
            }
            Assert.That(windMaterialCount, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void SpruceWind_SmoothingIsContinuousAndFrameRateIndependent()
        {
            MethodInfo method = typeof(Phase1SpruceWindController).GetMethod(
                "CalculateExponentialBlend",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float sixtyFpsStep = (float)method.Invoke(
                null,
                new object[] { 1f / 60f, 0.65f });
            float twentyFpsStep = (float)method.Invoke(
                null,
                new object[] { 1f / 20f, 0.65f });
            float sixtyFpsAfterOneSecond =
                1f - Mathf.Pow(1f - sixtyFpsStep, 60f);
            float twentyFpsAfterOneSecond =
                1f - Mathf.Pow(1f - twentyFpsStep, 20f);

            Assert.That(sixtyFpsStep, Is.InRange(0.001f, 0.1f));
            Assert.That(twentyFpsStep, Is.GreaterThan(sixtyFpsStep));
            Assert.That(
                sixtyFpsAfterOneSecond,
                Is.EqualTo(twentyFpsAfterOneSecond).Within(0.0001f));
        }

        [Test]
        public void SpruceWind_StandardPrefabRendersVisibleNeedleCoverage()
        {
            const string prefabPath =
                "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
                "Prefabs/NorwaySpruceHD/" +
                "NorwaySpruce_Standard0.prefab";
            AssertSprucePrefabRendersVisibleNeedleCoverage(
                prefabPath,
                lodIndex: 0,
                requireGreenTint: false);
        }

        [Test]
        public void SpruceSpeciesMix_UsesFiveReviewedAlpVariants()
        {
            string[] paths =
                Phase1ForestRemediationBuilder.SprucePrefabs;
            var uniquePaths =
                new System.Collections.Generic.HashSet<string>(paths);

            Assert.That(paths, Has.Length.EqualTo(5));
            Assert.That(uniquePaths, Has.Count.EqualTo(5));
            Assert.That(paths, Has.None.Contains("ConiferTreeSmall02"));
            Assert.That(paths, Has.None.Contains("ConiferTreeBig03"));
            Assert.That(paths, Has.None.Contains("ConiferTreeSmall01"));
            foreach (string path in paths)
            {
                Assert.That(
                    path,
                    Does.StartWith(MapVegetationAlpSpruceBindings.OutputRoot));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    Is.Not.Null,
                    path);
            }
        }

        [TestCase("1", 1f)]
        [TestCase("2", 0.36f)]
        [TestCase("5", 1f)]
        [TestCase("8", 0.44f)]
        [TestCase("9", 1f)]
        public void EngelmannFoliageCardScale_TargetsOnlyOversizedVariants(
            string variantId,
            float expectedScale)
        {
            Assert.That(
                Phase1VegetationAssetBuilder
                    .GetEngelmannFoliageCardScale(variantId),
                Is.EqualTo(expectedScale).Within(0.0001f));
        }

        [TestCase("2")]
        [TestCase("8")]
        public void EngelmannOversizedFoliage_UsesLocalCardAdjustedMeshes(
            string variantId)
        {
            string prefabPath =
                Phase1VegetationAssetBuilder.EngelmannSprucePrefabRoot +
                "/EngelmannSpruce_" + variantId + ".prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
            string expectedScaleTag = Mathf.RoundToInt(
                    Phase1VegetationAssetBuilder
                        .GetEngelmannFoliageCardScale(variantId) * 100f)
                .ToString("D3");
            for (int lodIndex = 0; lodIndex <= 1; lodIndex++)
            {
                Renderer[] foliage = lods[lodIndex].renderers
                    .Where(renderer =>
                        HasWindMaterial(new[] { renderer }))
                    .ToArray();
                Assert.That(foliage, Is.Not.Empty);
                foreach (Renderer renderer in foliage)
                {
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned
                        ? skinned.sharedMesh
                        : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(
                        AssetDatabase.GetAssetPath(mesh),
                        Does.StartWith(
                            Phase1VegetationAssetBuilder
                                .EngelmannAdjustedMeshRoot + "/"));
                    Assert.That(
                        AssetDatabase.GetAssetPath(mesh),
                        Does.EndWith(
                            "_CardScale" + expectedScaleTag + ".asset"));
                }
            }
        }

        [TestCase("1")]
        [TestCase("2")]
        [TestCase("5")]
        [TestCase("8")]
        [TestCase("9")]
        public void EngelmannSprucePrefab_HasWindAndBoundedLods(
            string variantId)
        {
            string prefabPath =
                Phase1VegetationAssetBuilder.EngelmannSprucePrefabRoot +
                "/EngelmannSpruce_" + variantId + ".prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            LODGroup group = prefab.GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null, prefabPath);
            LOD[] lods = group.GetLODs();
            Assert.That(lods, Has.Length.EqualTo(3));
            Transform groundedVisuals = prefab.transform.Find(
                "GroundedVisuals_Engelmann_" + variantId);
            Assert.That(groundedVisuals, Is.Not.Null, prefabPath);
            Bounds groundedHighBounds = CalculateBounds(
                lods[0].renderers);
            float burialFraction = -groundedVisuals.localPosition.y /
                                   groundedHighBounds.size.y;
            Assert.That(
                burialFraction,
                Is.EqualTo(
                    Phase1VegetationAssetBuilder
                        .GetEngelmannRootBurialFraction(variantId))
                    .Within(0.001f));
            CapsuleCollider trunkCollider =
                prefab.GetComponent<CapsuleCollider>();
            Assert.That(trunkCollider, Is.Not.Null);
            Assert.That(
                trunkCollider.center.y - trunkCollider.height * 0.5f,
                Is.Zero.Within(0.001f),
                "The prefab root and its collider must remain on the " +
                "measured terrain surface.");
            Assert.That(
                lods[0].screenRelativeTransitionHeight,
                Is.EqualTo(0.32f).Within(0.0001f));
            Assert.That(
                lods[1].screenRelativeTransitionHeight,
                Is.EqualTo(0.045f).Within(0.0001f));

            int highTriangleCount = CountTriangles(lods[0].renderers);
            int lowTriangleCount = CountTriangles(lods[1].renderers);
            Assert.That(highTriangleCount, Is.InRange(50000, 320000));
            Assert.That(lowTriangleCount, Is.LessThanOrEqualTo(30000));
            Assert.That(
                HasWindMaterial(lods[0].renderers),
                Is.True,
                prefabPath);
            Assert.That(
                HasWindMaterial(lods[1].renderers),
                Is.True,
                prefabPath);
            Assert.That(
                HasWindMaterial(lods[2].renderers),
                Is.True,
                prefabPath);
            for (int lodIndex = 1;
                 lodIndex < lods.Length;
                 lodIndex++)
            {
                bool hasRequiredEngelmannMesh = false;
                foreach (Renderer renderer in lods[lodIndex].renderers)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(
                            AssetDatabase.GetAssetPath(material),
                            Does.StartWith(
                                "Assets/Game/Presentation/Vegetation/" +
                                "Generated/Phase1/Materials/" +
                                "EngelmannSpruce/"),
                            $"Old spruce material remains in LOD " +
                            $"{lodIndex} of {prefabPath}.");
                    }
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned
                        ? skinned.sharedMesh
                        : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    string meshPath = AssetDatabase.GetAssetPath(mesh);
                    Assert.That(
                        meshPath,
                        Does.Not.Contain("/NorwaySpruceHD/"),
                        $"Old spruce geometry remains in LOD {lodIndex} " +
                        $"of {prefabPath}.");
                    if (lodIndex == 1 &&
                        Phase1VegetationAssetBuilder
                            .GetEngelmannFoliageCardScale(variantId) < 1f)
                    {
                        hasRequiredEngelmannMesh |= meshPath.StartsWith(
                            Phase1VegetationAssetBuilder
                                .EngelmannAdjustedMeshRoot + "/",
                            System.StringComparison.Ordinal);
                    }
                    else
                    {
                        string requiredPath = lodIndex == 1
                            ? "Assets/Game/Presentation/Vegetation/" +
                              "ThirdParty/EngelmannSpruce/Source/Models/" +
                              "picea-engelmanni-glauca.fbx"
                            : "Assets/Game/Presentation/Vegetation/" +
                              "Generated/Phase1/Meshes/EngelmannSpruce/" +
                              "EngelmannSpruce_TieredCrossBillboard.asset";
                        hasRequiredEngelmannMesh |= meshPath == requiredPath;
                    }
                }
                Assert.That(
                    hasRequiredEngelmannMesh,
                    Is.True,
                    $"Engelmann geometry is missing from LOD {lodIndex} " +
                    $"of {prefabPath}.");
            }
        }

        [TestCase("1")]
        [TestCase("2")]
        [TestCase("5")]
        [TestCase("8")]
        [TestCase("9")]
        public void SpruceWind_EngelmannPrefabsRenderVisibleNeedleCoverage(
            string variantId)
        {
            string prefabPath =
                Phase1VegetationAssetBuilder.EngelmannSprucePrefabRoot +
                "/EngelmannSpruce_" + variantId + ".prefab";
            AssertSprucePrefabRendersVisibleNeedleCoverage(prefabPath);
        }

        [TestCase("1", 1)]
        [TestCase("5", 1)]
        [TestCase("9", 1)]
        [TestCase("1", 2)]
        [TestCase("5", 2)]
        [TestCase("9", 2)]
        public void SpruceWind_EngelmannFarLodsRenderNewNeedleCoverage(
            string variantId,
            int lodIndex)
        {
            string prefabPath =
                Phase1VegetationAssetBuilder.EngelmannSprucePrefabRoot +
                "/EngelmannSpruce_" + variantId + ".prefab";
            AssertSprucePrefabRendersVisibleNeedleCoverage(
                prefabPath,
                lodIndex);
        }

        private static void AssertSprucePrefabRendersVisibleNeedleCoverage(
            string prefabPath,
            int lodIndex = 0,
            bool requireGreenTint = true)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            GameObject instance = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            GameObject volumeObject = null;
            VolumeProfile volumeProfile = null;
            RenderTexture target = null;
            Texture2D capture = null;
            RenderTexture previousTarget = RenderTexture.active;
            const int fixtureLayer = 31;
            try
            {
                instance = Object.Instantiate(prefab);
                instance.transform.position = Vector3.zero;
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = fixtureLayer;
                Bounds bounds = EnableLodAndGetBounds(
                    instance,
                    lodIndex);
                Renderer[] windRenderers = instance
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(renderer =>
                        renderer.enabled &&
                        HasWindMaterial(new[] { renderer }))
                    .ToArray();
                Assert.That(
                    windRenderers,
                    Is.Not.Empty,
                    "The selected LOD has no wind-enabled foliage.");
                foreach (Renderer renderer in instance
                    .GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = windRenderers.Contains(renderer);
                }
                bounds = CalculateBounds(windRenderers);

                volumeObject = new GameObject(
                    "Spruce Render Test Volume");
                volumeObject.layer = fixtureLayer;
                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10000f;
                volumeProfile =
                    ScriptableObject.CreateInstance<VolumeProfile>();
                Exposure exposure = volumeProfile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(14f);
                // Colour coverage must not depend on the previously open scene's fog
                // or tonemapping. The thresholds below remain unchanged.
                volumeProfile.Add<Fog>().enabled.Override(false);
                volumeProfile.Add<Tonemapping>().mode.Override(TonemappingMode.None);
                volume.sharedProfile = volumeProfile;

                lightObject = new GameObject(
                    "Spruce Render Test Directional Light");
                Light directionalLight = lightObject.AddComponent<Light>();
                lightObject.layer = fixtureLayer;
                directionalLight.type = LightType.Directional;
                directionalLight.cullingMask = 1 << fixtureLayer;
                // Register the HDRP light before setting calibrated daylight values.
                // Otherwise the first render adds it and resets intensity/temperature.
                HDAdditionalLightData hdLight = lightObject.AddComponent<HDAdditionalLightData>();
                directionalLight.lightUnit = LightUnit.Lux;
                directionalLight.useColorTemperature = false;
                directionalLight.color = Color.white;
                directionalLight.intensity = 100000f;
                lightObject.transform.rotation =
                    Quaternion.Euler(48f, -32f, 0f);
                hdLight.UpdateAllLightValues();

                cameraObject = new GameObject("Spruce Render Test Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                HDAdditionalCameraData cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
                cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                cameraData.backgroundColorHDR = new Color(1f, 0f, 1f, 1f);
                cameraData.volumeLayerMask = 1 << fixtureLayer;
                cameraData.volumeAnchorOverride = camera.transform;
                camera.cullingMask = 1 << fixtureLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(1f, 0f, 1f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.y * 1.08f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                Vector3 viewDirection =
                    new Vector3(0.72f, 0.08f, -1f).normalized;
                camera.transform.position =
                    bounds.center - viewDirection * 40f;
                camera.transform.LookAt(bounds.center);
                // This is a source-colour coverage test. Illuminate the visible
                // foliage from the viewing direction, not from behind the tree.
                lightObject.transform.rotation = camera.transform.rotation;
                hdLight.UpdateAllLightValues();

                target = new RenderTexture(
                    512,
                    512,
                    24,
                    RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                camera.Render();

                RenderTexture.active = target;
                capture = new Texture2D(
                    512,
                    512,
                    TextureFormat.RGBA32,
                    false);
                capture.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
                capture.Apply();

                int foliagePixelCount = 0;
                int greenPixelCount = 0;
                Color32[] pixels = capture.GetPixels32();
                var foliageMask = new bool[pixels.Length];
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    if (pixel.g > 8 || pixel.r < 245 || pixel.b < 245)
                    {
                        foliageMask[index] = true;
                        foliagePixelCount++;
                    }
                    if (pixel.g > pixel.r + 8 &&
                        pixel.g > pixel.b + 4)
                    {
                        greenPixelCount++;
                    }
                }

                SaveSpruceRenderEvidence(prefabPath, lodIndex, capture, pixels,
                    foliagePixelCount, greenPixelCount, windRenderers,
                    camera, cameraData, directionalLight, bounds, "_Coverage", 14f);

                Assert.That(
                    foliagePixelCount,
                    Is.GreaterThan(500),
                    "The wind shader compiled, but the spruce needle " +
                    "geometry did not produce visible foliage pixels.");

                // Alpha coverage and anti-aliasing blend fine needle edges with
                // the clear colour. Magenta is useful for silhouette detection,
                // but it biases a green-colour test. Measure the unchanged lit
                // material again on black, at a fixed studio exposure, and count
                // only pixels belonging to the separately verified silhouette.
                cameraData.backgroundColorHDR = Color.black;
                camera.backgroundColor = Color.black;
                exposure.fixedExposure.Override(12f);
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
                capture.Apply();
                pixels = capture.GetPixels32();
                greenPixelCount = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    if (foliageMask[index] && pixel.g > pixel.r + 8 && pixel.g > pixel.b + 4)
                        greenPixelCount++;
                }
                SaveSpruceRenderEvidence(prefabPath, lodIndex, capture, pixels,
                    foliagePixelCount, greenPixelCount, windRenderers,
                    camera, cameraData, directionalLight, bounds, "", 12f);
                if (requireGreenTint)
                {
                    Assert.That(
                        greenPixelCount,
                        Is.GreaterThan(500),
                        "Active Engelmann foliage lost its restrained " +
                        "green source colour.");
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (target != null)
                {
                    if (cameraObject != null)
                    {
                        cameraObject.GetComponent<Camera>().targetTexture =
                            null;
                    }
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (capture != null)
                {
                    Object.DestroyImmediate(capture);
                }
                if (cameraObject != null)
                {
                    Object.DestroyImmediate(cameraObject);
                }
                if (lightObject != null)
                {
                    Object.DestroyImmediate(lightObject);
                }
                if (volumeObject != null)
                {
                    Object.DestroyImmediate(volumeObject);
                }
                if (volumeProfile != null)
                {
                    Object.DestroyImmediate(volumeProfile);
                }
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void SaveSpruceRenderEvidence(string prefabPath, int lodIndex,
            Texture2D capture, Color32[] pixels, int foliagePixels, int greenPixels,
            Renderer[] renderers, Camera camera, HDAdditionalCameraData cameraData,
            Light light, Bounds bounds, string passSuffix, float fixedExposureValue)
        {
            string directory = System.IO.Path.GetFullPath(
                "Artifacts/VegetationRebuild/TreePresentationAudit/TestRenders");
            System.IO.Directory.CreateDirectory(directory);
            string basename = System.IO.Path.Combine(directory,
                System.IO.Path.GetFileNameWithoutExtension(prefabPath) + "_LOD" + lodIndex + passSuffix);
            System.IO.File.WriteAllBytes(basename + ".png", capture.EncodeToPNG());
            var report = new SpruceRenderEvidence
            {
                utc = System.DateTime.UtcNow.ToString("O"), prefab = prefabPath,
                lod = lodIndex, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                foliagePixels = foliagePixels, greenPixels = greenPixels,
                totalPixels = pixels.Length, cameraPosition = camera.transform.position,
                cameraEuler = camera.transform.eulerAngles, lightEuler = light.transform.eulerAngles, bounds = bounds,
                hdrpClearMode = cameraData.clearColorMode.ToString(),
                hdrpBackground = cameraData.backgroundColorHDR,
                cameraCullingMask = camera.cullingMask, volumeLayerMask = cameraData.volumeLayerMask.value,
                lightIntensityAfterRender = light.intensity, lightUnit = light.lightUnit.ToString(),
                lightColor = light.color, useColorTemperature = light.useColorTemperature,
                fixedExposure = fixedExposureValue, tonemapping = "None", fogEnabled = false,
                capturePass = passSuffix == "_Coverage" ? "Magenta silhouette, EV14" : "Black colour readback, EV12; green count restricted to magenta silhouette mask",
                blackPixels = pixels.Count(p => p.r <= 12 && p.g <= 12 && p.b <= 12),
                whitePixels = pixels.Count(p => p.r >= 245 && p.g >= 245 && p.b >= 245),
                magentaPixels = pixels.Count(p => p.r >= 245 && p.g <= 8 && p.b >= 245),
                meanRgb = new Vector3((float)pixels.Average(p => p.r),
                    (float)pixels.Average(p => p.g), (float)pixels.Average(p => p.b)),
                cornersAndCenter = new[] { (Color)pixels[0], (Color)pixels[511],
                    (Color)pixels[511 * 512], (Color)pixels[pixels.Length - 1], (Color)pixels[256 * 512 + 256] },
                materialBindings = renderers.SelectMany(r => r.sharedMaterials.Select(m =>
                    r.name + " | " + AssetDatabase.GetAssetPath(m) + " | shader=" +
                    (m != null && m.shader != null ? m.shader.name : "null") +
                    " | supported=" + (m != null && m.shader != null && m.shader.isSupported) +
                    " | texture=" + (m != null && m.HasProperty("_BaseColorMap") ?
                        AssetDatabase.GetAssetPath(m.GetTexture("_BaseColorMap")) : "none") +
                    " | tint=" + (m != null && m.HasProperty("_BaseColor") ?
                        m.GetColor("_BaseColor").ToString() : "none"))).ToArray()
            };
            System.IO.File.WriteAllText(basename + ".json", JsonUtility.ToJson(report, true));
            Debug.Log("SPRUCE_RENDER_EVIDENCE png=" + basename + ".png foliage=" + foliagePixels +
                " green=" + greenPixels + " black=" + report.blackPixels + " white=" + report.whitePixels);
        }

        [System.Serializable]
        private sealed class SpruceRenderEvidence
        {
            public string utc, prefab, graphicsApi, graphicsDevice, colorSpace, hdrpClearMode, lightUnit, tonemapping, capturePass;
            public int lod, foliagePixels, greenPixels, totalPixels, blackPixels, whitePixels, magentaPixels, cameraCullingMask, volumeLayerMask;
            public Vector3 cameraPosition, cameraEuler, lightEuler, meanRgb;
            public Bounds bounds;
            public Color hdrpBackground, lightColor;
            public Color[] cornersAndCenter;
            public float lightIntensityAfterRender, fixedExposure;
            public bool useColorTemperature, fogEnabled;
            public string[] materialBindings;
        }

        private static int CountTriangles(Renderer[] renderers)
        {
            int count = 0;
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer != null
                    ? renderer.GetComponent<MeshFilter>()
                    : null;
                Mesh mesh = filter != null
                    ? filter.sharedMesh
                    : null;
                if (mesh == null)
                {
                    continue;
                }
                for (int subMeshIndex = 0;
                     subMeshIndex < mesh.subMeshCount;
                     subMeshIndex++)
                {
                    count += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);
                }
            }
            return count;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static bool HasWindMaterial(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null &&
                        material.shader != null &&
                        material.shader.name ==
                            Phase1SpruceWindController.ShaderName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [Test]
        public void TriangleClearance_BlocksSurfaceAndRoadShoulder()
        {
            Vector2 a = new Vector2(0f, 0f);
            Vector2 b = new Vector2(10f, 0f);
            Vector2 c = new Vector2(0f, 10f);

            Assert.That(
                Phase1TreePlacementExclusionIndex
                    .IsPointWithinTriangleClearance(
                        new Vector2(2f, 2f),
                        a,
                        b,
                        c,
                        Phase1TreePlacementExclusionIndex
                            .RoadClearanceMeters),
                Is.True);
            Assert.That(
                Phase1TreePlacementExclusionIndex
                    .IsPointWithinTriangleClearance(
                        new Vector2(7f, 7f),
                        a,
                        b,
                        c,
                        Phase1TreePlacementExclusionIndex
                            .RoadClearanceMeters),
                Is.True,
                "A tree canopy must not overhang the road edge.");
            Assert.That(
                Phase1TreePlacementExclusionIndex
                    .IsPointWithinTriangleClearance(
                        new Vector2(20f, 20f),
                        a,
                        b,
                        c,
                        Phase1TreePlacementExclusionIndex
                            .RoadClearanceMeters),
                Is.False);
        }

        [TestCase("Road", "MAP/MESH/TERRAIN_OBJ/Asphalt", "Road")]
        [TestCase("Bridge", "MAP/MESH/BRIDGE_highway", "Road")]
        [TestCase("RoadSign", "MAP/TrafficSigns/3/sign_pole 10", "None")]
        [TestCase("BuildingExterior", "YARD/Building/Garage", "Building")]
        [TestCase("BuildingExterior", "PERAJARVI/MailBox/boxmesh", "None")]
        [TestCase("BuildingExterior", "STORE/LOD/PRODUCTS/Sausages/1", "None")]
        [TestCase("BuildingExterior", "RYKIPOHJA/FIELD/field_mesh", "None")]
        [TestCase("BuildingInterior", "STORE/LOD/PRODUCTS/Chips/7", "None")]
        [TestCase("BuildingExterior", "WATERFACILITY/MESH/facility_office_wall", "Building")]
        [TestCase("Roof", "STORE/MESH/store_roof", "Building")]
        [TestCase("Water", "MAP/MESH/LAKE", "Water")]
        [TestCase("Water", "MAP/LakeWaterUnder1", "None")]
        [TestCase("Water", "MAP/LakeWaterColor", "None")]
        [TestCase("Water", "MAP/MESH/LAKEBED", "None")]
        [TestCase("Water", "MAP/MESH/FOLIAGE/LAKE_VEGETATION", "None")]
        [TestCase("VegetationTree", "MAP/MESH/FOLIAGE/TREES", "None")]
        public void SemanticClassification_CoversRequiredTreeExclusions(
            string semanticCategory,
            string hierarchyPath,
            string expected)
        {
            Assert.That(
                Phase1TreePlacementExclusionIndex.ClassifyForTests(
                    semanticCategory,
                    hierarchyPath),
                Is.EqualTo(expected));
        }

        [TestCase(
            "phase1-job-location-presentation-10b-r1-v3",
            "JOBS/HouseShit2/WasteWell_2000litre/waste_well",
            "Building")]
        [TestCase(
            "phase1-job-location-presentation-10b-r1-v3",
            "JOBS/StrawberryField/Rows/Row1/BerryTrigger",
            "Building")]
        [TestCase(
            "phase1-job-location-presentation-10b-r1-v3",
            "JOBS/StrawberryField/LOD/Tent",
            "Building")]
        [TestCase(
            "phase1-job-location-presentation-10b-r1-v3",
            "JOBS/Farm/Farmhouse/base",
            "Building")]
        [TestCase(
            "another-overlay",
            "MISC/Unrelated/Prop",
            "None")]
        public void SupplementalClassification_CoversNewJobLocations(
            string manifestId,
            string hierarchyPath,
            string expected)
        {
            Assert.That(
                Phase1TreePlacementExclusionIndex
                    .ClassifySupplementalForTests(
                        manifestId,
                        hierarchyPath),
                Is.EqualTo(expected));
        }

        private static Bounds EnableLodAndGetBounds(
            GameObject instance,
            int lodIndex)
        {
            LODGroup lodGroup = instance.GetComponent<LODGroup>();
            Assert.That(lodGroup, Is.Not.Null);
            LOD[] lods = lodGroup.GetLODs();
            Assert.That(lods, Is.Not.Empty);
            Assert.That(lodIndex, Is.InRange(0, lods.Length - 1));

            Renderer[] allRenderers =
                instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in allRenderers)
            {
                renderer.enabled = false;
            }
            lodGroup.enabled = false;

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in lods[lodIndex].renderers)
            {
                Assert.That(renderer, Is.Not.Null);
                renderer.enabled = true;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Assert.That(hasBounds, Is.True);
            return bounds;
        }
    }
}
