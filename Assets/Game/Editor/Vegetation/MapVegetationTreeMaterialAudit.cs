using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Isolated, graphics-backed stress render for tree materials. The light is
    /// low and nearly camera-facing on purpose: the old metallic bark and broad
    /// foliage lobe were most visible under this alignment.
    /// </summary>
    public static class MapVegetationTreeMaterialAudit
    {
        public const string OutputRoot =
            "Artifacts/VegetationRebuild/TreeMaterialAudit";
        public const string ReportPath = OutputRoot + "/report.json";

        private const int Width = 640;
        private const int Height = 640;
        private static readonly float[] NearAzimuths = { 35f, 215f };
        private static readonly float[] MiddleAzimuths = { 35f };
        private static readonly float[] FarAzimuths =
            { 22.5f, 112.5f, 202.5f, 292.5f };
        private static readonly string[] Species =
            { "Spruce", "Pine", "Birch", "Aspen" };

        [MenuItem("Tools/MSC/Vegetation/Prepare And Capture Tree Material Audit")]
        public static void PrepareAndCaptureBatch()
        {
            MapVegetationMaterialBindings.BuildMaterials();
            MapVegetationTreePresentation.BuildAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CaptureBatch();
        }

        public static void CaptureBatch()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException(
                    "Tree material audit requires a graphics-enabled Unity process; " +
                    "run batch mode without -nographics.");
            }
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
            {
                throw new InvalidOperationException(
                    "Tree material audit requires the project's HDRP asset.");
            }

            Directory.CreateDirectory(OutputRoot);
            foreach (string stale in Directory.GetFiles(OutputRoot, "*.png"))
            {
                File.Delete(stale);
            }
            if (File.Exists(ReportPath)) File.Delete(ReportPath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var report = new AuditReport
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                width = Width,
                height = Height,
                fixedExposureEv100 = 12.3f,
                sunIlluminanceLux = 100000f
            };
            var temporary = new List<Object>();
            Camera camera = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            Light previousSun = RenderSettings.sun;
            try
            {
                camera = CreateCamera(temporary);
                Light sun = CreateLighting(temporary);
                target = new RenderTexture(
                    Width,
                    Height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(
                    Width,
                    Height,
                    TextureFormat.RGBA32,
                    false,
                    false);

                foreach (string species in Species)
                {
                    foreach (GameObject prefab in MapVegetationTreePresentation
                                 .LoadSpeciesPrefabs(species))
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefab);
                        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                        string variantKey = FileSafe(species + "_" + prefab.name +
                            "_" + (prefabGuid.Length >= 8
                                ? prefabGuid.Substring(0, 8)
                                : prefabGuid));
                        GameObject instance = Object.Instantiate(prefab);
                        instance.name = "TreeMaterialAudit_" + variantKey;
                        temporary.Add(instance);
                        MapVegetationMaterialBindings.Apply(instance);
                        MapVegetationTreePresentation.Apply(instance, prefab);
                        TreeAcceptanceEvidence accepted =
                            MapVegetationTreeAcceptance.AuditPrefab(
                                instance, species, prefabPath);
                        report.approvedPrefabs.Add(accepted);

                        LODGroup group = instance.GetComponentInChildren<LODGroup>(true);
                        LOD[] lods = group.GetLODs();
                        for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                        {
                            bool near = lodIndex == 0;
                            bool far = lodIndex == lods.Length - 1;
                            CaptureLod(
                                species,
                                prefab,
                                variantKey,
                                instance,
                                group,
                                lods,
                                lodIndex,
                                near ? NearAzimuths :
                                    far ? FarAzimuths : MiddleAzimuths,
                                near ? "near" : far ? "far" : "middle",
                                camera,
                                sun,
                                target,
                                pixels,
                                report);
                        }
                        Object.DestroyImmediate(instance);
                        temporary.Remove(instance);
                    }
                }

                string prototypeRoot = MapVegetationRebuildOptions.GeneratedRoot +
                    "/PackedWoody/Prototypes";
                foreach (string guid in AssetDatabase.FindAssets(
                             "t:PackedWoodyPrototypeAsset",
                             new[] { prototypeRoot }))
                {
                    PackedWoodyPrototypeAsset prototype =
                        AssetDatabase.LoadAssetAtPath<PackedWoodyPrototypeAsset>(
                            AssetDatabase.GUIDToAssetPath(guid));
                    if (prototype == null ||
                        prototype.Species < PackedWoodySpecies.Spruce ||
                        prototype.Species > PackedWoodySpecies.Aspen)
                        continue;
                    report.packedTreePrototypes.Add(
                        MapVegetationTreeAcceptance.AuditPackedPrototype(prototype));
                }

                Validate(report);
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                Debug.Log(
                    "MAP_TREE_MATERIAL_AUDIT_OK approvedPrefabs=" +
                    report.approvedPrefabs.Count +
                    " packedTreePrototypes=" + report.packedTreePrototypes.Count +
                    " materialSlots=" + report.approvedPrefabs.Sum(prefab =>
                        prefab.materialSlots.Count) +
                    " views=" + report.views.Count +
                    " report=" + ReportPath);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderSettings.sun = previousSun;
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                for (int index = temporary.Count - 1; index >= 0; index--)
                {
                    if (temporary[index] != null)
                    {
                        Object.DestroyImmediate(temporary[index]);
                    }
                }
            }
        }

        private static void CaptureLod(
            string species,
            GameObject prefab,
            string variantKey,
            GameObject instance,
            LODGroup group,
            LOD[] lods,
            int lodIndex,
            IEnumerable<float> azimuths,
            string label,
            Camera camera,
            Light sun,
            RenderTexture target,
            Texture2D pixels,
            AuditReport report)
        {
            Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in allRenderers)
            {
                renderer.enabled = false;
            }
            Renderer[] selected = lods[lodIndex].renderers;
            if (selected.Length == 0 || selected.Any(renderer => renderer == null))
            {
                throw new InvalidDataException(
                    species + " " + label + " LOD has no valid renderers.");
            }
            group.enabled = false;
            foreach (Renderer renderer in selected)
            {
                renderer.enabled = true;
            }

            Bounds bounds = selected[0].bounds;
            for (int index = 1; index < selected.Length; index++)
            {
                bounds.Encapsulate(selected[index].bounds);
            }
            if (bounds.extents.sqrMagnitude < 0.01f)
            {
                throw new InvalidDataException(
                    species + " " + label + " LOD has invalid bounds.");
            }

            foreach (float azimuth in azimuths)
            {
                ConfigureCamera(camera, sun, bounds, azimuth);
                string fileName = variantKey + "_" + label + "_LOD" + lodIndex +
                                  "_A" + azimuth.ToString("000.0",
                                      System.Globalization.CultureInfo.InvariantCulture)
                                      .Replace('.', '_') + ".png";
                ViewReport view = Capture(
                    camera,
                    target,
                    pixels,
                    selected,
                    species,
                    label,
                    lodIndex,
                    azimuth,
                    OutputRoot + "/" + fileName);
                view.sourcePrefabPath = AssetDatabase.GetAssetPath(prefab);
                view.sourcePrefabGuid = AssetDatabase.AssetPathToGUID(
                    view.sourcePrefabPath);
                view.variantKey = variantKey;
                report.views.Add(view);
            }
        }

        private static Camera CreateCamera(List<Object> temporary)
        {
            var owner = new GameObject("Tree material audit camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(owner);
            Camera camera = owner.AddComponent<Camera>();
            HDAdditionalCameraData hdCamera = owner.AddComponent<HDAdditionalCameraData>();
            hdCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.fieldOfView = 34f;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            return camera;
        }

        private static Light CreateLighting(List<Object> temporary)
        {
            var volumeOwner = new GameObject("Tree material audit volume")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(volumeOwner);
            Volume volume = volumeOwner.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100000f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            temporary.Add(profile);
            VisualEnvironment environment = profile.Add<VisualEnvironment>();
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            GradientSky sky = profile.Add<GradientSky>();
            sky.top.Override(new Color(0.24f, 0.36f, 0.52f));
            sky.middle.Override(new Color(0.56f, 0.64f, 0.70f));
            sky.bottom.Override(new Color(0.15f, 0.18f, 0.16f));
            sky.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            sky.exposure.Override(12f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            Exposure exposure = profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(12.3f);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            Fog fog = profile.Add<Fog>();
            fog.enabled.Override(false);
            volume.sharedProfile = profile;

            var sunOwner = new GameObject("Tree material audit low sun")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(sunOwner);
            Light sun = sunOwner.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 100000f;
            sun.color = new Color(1f, 0.965f, 0.91f);
            sun.shadows = LightShadows.Soft;
            HDAdditionalLightData hdLight = sunOwner.AddComponent<HDAdditionalLightData>();
            hdLight.UpdateAllLightValues();
            RenderSettings.sun = sun;
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline)
            {
                pipeline.RequestSkyEnvironmentUpdate();
            }
            return sun;
        }

        private static void ConfigureCamera(
            Camera camera,
            Light sun,
            Bounds bounds,
            float azimuth)
        {
            float radians = azimuth * Mathf.Deg2Rad;
            Vector3 horizontal = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 viewDirection = (horizontal + Vector3.up * 0.14f).normalized;
            float radius = Mathf.Max(bounds.extents.magnitude, 1f);
            float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.18f;
            camera.transform.position = bounds.center + viewDirection * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = Mathf.Max(0.05f, distance - radius * 1.5f);
            camera.farClipPlane = distance + radius * 2.5f;

            // Surface-to-sun direction is close to the view direction, with a
            // deliberately low 16 degree elevation to stress broad highlights.
            Vector3 towardSun = (horizontal + Vector3.up * 0.287f).normalized;
            sun.transform.rotation = Quaternion.LookRotation(-towardSun, Vector3.up);
        }

        private static ViewReport Capture(
            Camera camera,
            RenderTexture target,
            Texture2D pixels,
            Renderer[] renderers,
            string species,
            string lodLabel,
            int lodIndex,
            float azimuth,
            string path)
        {
            foreach (Renderer renderer in renderers) renderer.enabled = false;
            Color32[] background = Read(camera, target, pixels);
            foreach (Renderer renderer in renderers) renderer.enabled = true;
            Color32[] image = Read(camera, target, pixels);
            File.WriteAllBytes(path, pixels.EncodeToPNG());

            var luminance = new List<int>();
            int neutralBright = 0;
            int clipped = 0;
            int greenDominant = 0;
            int borealHue = 0;
            int warmOrMagenta = 0;
            for (int index = 0; index < image.Length; index++)
            {
                Color32 value = image[index];
                Color32 clear = background[index];
                int delta = Math.Abs(value.r - clear.r) +
                            Math.Abs(value.g - clear.g) +
                            Math.Abs(value.b - clear.b);
                if (delta <= 8) continue;
                int luma = (54 * value.r + 183 * value.g + 19 * value.b) >> 8;
                luminance.Add(luma);
                int maximum = Math.Max(value.r, Math.Max(value.g, value.b));
                int minimum = Math.Min(value.r, Math.Min(value.g, value.b));
                if (luma >= 220 && maximum - minimum <= 32) neutralBright++;
                if (luma >= 245 && maximum >= 253) clipped++;
                if (value.g > value.r * 1.02f && value.g > value.b * 1.05f)
                    greenDominant++;
                int chroma = maximum - minimum;
                if (chroma >= 10)
                {
                    float hue = HueDegrees(value.r, value.g, value.b, maximum, chroma);
                    if (hue >= 55f && hue <= 170f) borealHue++;
                    if (hue < 35f || hue > 190f) warmOrMagenta++;
                }
            }
            if (luminance.Count == 0)
            {
                throw new InvalidDataException(
                    species + " " + lodLabel + " rendered no pixels at azimuth " +
                    azimuth + ".");
            }
            luminance.Sort();
            float mean = (float)luminance.Average();
            return new ViewReport
            {
                species = species,
                lodLabel = lodLabel,
                lodIndex = lodIndex,
                azimuthDegrees = azimuth,
                file = path,
                visiblePixels = luminance.Count,
                coverageFraction = luminance.Count / (float)image.Length,
                meanLuminance = mean / 255f,
                p95Luminance = Percentile(luminance, 0.95f) / 255f,
                p99Luminance = Percentile(luminance, 0.99f) / 255f,
                neutralBrightFraction = neutralBright / (float)luminance.Count,
                clippedHighlightFraction = clipped / (float)luminance.Count,
                greenDominantFraction = greenDominant / (float)luminance.Count,
                borealHueFraction = borealHue / (float)luminance.Count,
                warmOrMagentaFraction = warmOrMagenta / (float)luminance.Count
            };
        }

        private static float HueDegrees(
            int red,
            int green,
            int blue,
            int maximum,
            int chroma)
        {
            float hue;
            if (maximum == red)
                hue = (green - blue) / (float)chroma;
            else if (maximum == green)
                hue = 2f + (blue - red) / (float)chroma;
            else
                hue = 4f + (red - green) / (float)chroma;
            hue *= 60f;
            return hue < 0f ? hue + 360f : hue;
        }

        private static Color32[] Read(
            Camera camera,
            RenderTexture target,
            Texture2D pixels)
        {
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            pixels.Apply(false, false);
            return pixels.GetPixels32();
        }

        private static float Percentile(List<int> sorted, float percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt(sorted.Count * percentile) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }

        private static void Validate(AuditReport report)
        {
            int expectedPrefabCount = Species.Sum(species =>
                MapVegetationTreePresentation.LoadSpeciesPrefabs(species).Length);
            if (report.approvedPrefabs.Count != expectedPrefabCount)
                throw new InvalidDataException(
                    "Tree material audit did not inspect every approved prefab variant: " +
                    report.approvedPrefabs.Count + "/" + expectedPrefabCount + ".");
            if (report.approvedPrefabs.Select(prefab => prefab.sourcePrefabPath)
                    .Distinct(StringComparer.Ordinal).Count() != expectedPrefabCount)
                throw new InvalidDataException(
                    "Tree material audit contains duplicate/missing approved prefab paths.");

            foreach (TreeAcceptanceEvidence prefab in report.approvedPrefabs)
            {
                ViewReport[] near = report.views.Where(view =>
                    view.sourcePrefabPath == prefab.sourcePrefabPath &&
                    view.lodLabel == "near").ToArray();
                ViewReport[] middle = report.views.Where(view =>
                    view.sourcePrefabPath == prefab.sourcePrefabPath &&
                    view.lodLabel == "middle").ToArray();
                ViewReport[] far = report.views.Where(view =>
                    view.sourcePrefabPath == prefab.sourcePrefabPath &&
                    view.lodLabel == "far").ToArray();
                int expectedMiddle = Mathf.Max(0, prefab.lodCount - 2) *
                    MiddleAzimuths.Length;
                if (near.Length != NearAzimuths.Length ||
                    middle.Length != expectedMiddle ||
                    far.Length != FarAzimuths.Length)
                    throw new InvalidDataException(
                        prefab.species + " " + prefab.sourcePrefabPath +
                        " material audit view set is incomplete.");
                if (near.Any(view => view.coverageFraction < 0.0025f) ||
                    middle.Any(view => view.coverageFraction < 0.0015f) ||
                    far.Any(view => view.coverageFraction < 0.001f))
                    throw new InvalidDataException(
                        prefab.species + " " + prefab.sourcePrefabPath +
                        " disappears in a material audit view.");
                float farMaximum = far.Max(view => view.coverageFraction);
                float farMinimum = far.Min(view => view.coverageFraction);
                if (farMinimum / Mathf.Max(farMaximum, 0.000001f) < 0.12f)
                    throw new InvalidDataException(
                        prefab.species + " " + prefab.sourcePrefabPath +
                        " far LOD loses most of its silhouette from one azimuth; " +
                        "check billboard winding and double-sided culling.");
                if (near.Any(view => view.neutralBrightFraction > 0.22f ||
                                     view.clippedHighlightFraction > 0.08f))
                    throw new InvalidDataException(
                        prefab.species + " " + prefab.sourcePrefabPath +
                        " still has broad neutral/clipped low-sun highlights.");

                foreach (ViewReport view in near.Concat(middle).Concat(far))
                {
                    float minimumGreen = view.lodLabel == "near" ? 0.22f :
                        view.lodLabel == "far" ? 0.42f : 0.28f;
                    float minimumBorealHue = view.lodLabel == "near" ? 0.20f :
                        view.lodLabel == "far" ? 0.40f : 0.25f;
                    if (view.greenDominantFraction < minimumGreen ||
                        view.borealHueFraction < minimumBorealHue ||
                        view.warmOrMagentaFraction > 0.16f)
                        throw new InvalidDataException(
                            prefab.species + " " + prefab.sourcePrefabPath +
                            " LOD" + view.lodIndex +
                            " falls outside the boreal hue gate at azimuth " +
                            view.azimuthDegrees + ".");
                    if (view.meanLuminance < 0.08f ||
                        view.meanLuminance > 0.68f ||
                        view.p95Luminance > 0.92f ||
                        view.p99Luminance > 0.97f)
                        throw new InvalidDataException(
                            prefab.species + " " + prefab.sourcePrefabPath +
                            " LOD" + view.lodIndex +
                            " falls outside the boreal luminance gate at azimuth " +
                            view.azimuthDegrees + ".");
                }
            }
        }

        private static string FileSafe(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }

        [Serializable]
        private sealed class AuditReport
        {
            public string generator = "msc.tree-material-audit.v2";
            public string purpose =
                "Every approved tree prefab variant and every authored LOD renderer/material slot receive structural/PBR/hue-luminance acceptance. Every LOD is rendered under low-angle 100 klux HDRP light; far LODs additionally receive a 360-degree silhouette guard. Pixel metrics use a separately rendered sky background and therefore measure only tree coverage. Packed GPU tree prototypes are audited when generated assets exist.";
            public string unityVersion;
            public string graphicsDevice;
            public int width;
            public int height;
            public float fixedExposureEv100;
            public float sunIlluminanceLux;
            public List<TreeAcceptanceEvidence> approvedPrefabs =
                new List<TreeAcceptanceEvidence>();
            public List<TreeAcceptanceEvidence> packedTreePrototypes =
                new List<TreeAcceptanceEvidence>();
            public List<ViewReport> views = new List<ViewReport>();
        }

        [Serializable]
        private sealed class ViewReport
        {
            public string species;
            public string sourcePrefabPath;
            public string sourcePrefabGuid;
            public string variantKey;
            public string lodLabel;
            public int lodIndex;
            public float azimuthDegrees;
            public string file;
            public int visiblePixels;
            public float coverageFraction;
            public float meanLuminance;
            public float p95Luminance;
            public float p99Luminance;
            public float neutralBrightFraction;
            public float clippedHighlightFraction;
            public float greenDominantFraction;
            public float borealHueFraction;
            public float warmOrMagentaFraction;
        }
    }
}
