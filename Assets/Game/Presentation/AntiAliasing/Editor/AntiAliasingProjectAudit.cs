using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Presentation.AntiAliasing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Presentation.AntiAliasing.Editor
{
    public static class AntiAliasingProjectAudit
    {
        private const string SettingsAssetPath =
            "Assets/Game/Presentation/AntiAliasing/Resources/AntiAliasing/AntiAliasingSettings.asset";
        private const string ReportDirectory = "Reports/AntiAliasing";

        [MenuItem("MSC/Rendering/Anti-Aliasing/Create or Repair Settings Asset")]
        public static void CreateOrRepairSettingsAsset()
        {
            AntiAliasingSettings existing = AssetDatabase.LoadAssetAtPath<AntiAliasingSettings>(
                SettingsAssetPath);
            if (existing != null)
            {
                Debug.Log($"[AntiAliasing] Settings asset already exists at {SettingsAssetPath}.");
                return;
            }

            EnsureAssetFolder("Assets/Game/Presentation/AntiAliasing/Resources");
            EnsureAssetFolder("Assets/Game/Presentation/AntiAliasing/Resources/AntiAliasing");
            AntiAliasingSettings settings = ScriptableObject.CreateInstance<AntiAliasingSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AntiAliasing] Created settings asset at {SettingsAssetPath}.", settings);
        }

        [MenuItem("MSC/Rendering/Anti-Aliasing/Run Project Audit")]
        public static void RunProjectAudit()
        {
            CreateOrRepairSettingsAsset();
            AuditReport report = BuildReport();
            WriteReports(report);
            Debug.Log(
                $"[AntiAliasing] Audit complete: {report.Cameras.Count} cameras, " +
                $"{report.Materials.Total} materials, {report.Warnings.Count} warnings. " +
                $"See {ReportDirectory}.");
        }

        public static void RunBatchAudit()
        {
            RunProjectAudit();
        }

        private static AuditReport BuildReport()
        {
            AuditReport report = new AuditReport
            {
                GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityVersion = Application.unityVersion,
                HdrpVersion = ResolvePackageVersion(
                    "Packages/com.unity.render-pipelines.high-definition/package.json"),
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                ActiveQualityLevel = QualitySettings.GetQualityLevel(),
                ActiveQualityName = QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                    : "Unknown",
                AnisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
            };

            AuditPipelines(report);
            AuditGlobalSettings(report);
            AuditCameras(report);
            AuditVolumes(report);
            AuditMaterials(report);
            AuditShaders(report);
            Validate(report);
            return report;
        }

        private static void AuditGlobalSettings(AuditReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:HDRenderPipelineGlobalSettings");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    continue;
                }

                ulong cameraBitsLow = 0ul;
                ulong cameraBitsHigh = 0ul;
                bool foundLow = false;
                bool foundHigh = false;
                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty iterator = serialized.GetIterator();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;
                    if (!foundLow && iterator.propertyPath.EndsWith(
                            "m_Camera.bitDatas.data1",
                            StringComparison.Ordinal))
                    {
                        cameraBitsLow = iterator.ulongValue;
                        foundLow = true;
                    }
                    else if (!foundHigh && iterator.propertyPath.EndsWith(
                                 "m_Camera.bitDatas.data2",
                                 StringComparison.Ordinal))
                    {
                        cameraBitsHigh = iterator.ulongValue;
                        foundHigh = true;
                    }

                    if (foundLow && foundHigh)
                    {
                        break;
                    }
                }

                report.GlobalSettings.Add(new GlobalSettingsAudit
                {
                    Path = path,
                    CameraAntialiasing = IsFrameSettingEnabled(
                        cameraBitsLow, cameraBitsHigh, FrameSettingsField.Antialiasing),
                    CameraMotionVectors = IsFrameSettingEnabled(
                        cameraBitsLow, cameraBitsHigh, FrameSettingsField.MotionVectors),
                    CameraObjectMotionVectors = IsFrameSettingEnabled(
                        cameraBitsLow, cameraBitsHigh, FrameSettingsField.ObjectMotionVectors),
                    CameraTransparentMotionVectors = IsFrameSettingEnabled(
                        cameraBitsLow,
                        cameraBitsHigh,
                        FrameSettingsField.TransparentsWriteMotionVector),
                    CameraMotionBlur = IsFrameSettingEnabled(
                        cameraBitsLow, cameraBitsHigh, FrameSettingsField.MotionBlur),
                    CameraDepthOfField = IsFrameSettingEnabled(
                        cameraBitsLow, cameraBitsHigh, FrameSettingsField.DepthOfField),
                });
            }
        }

        private static void AuditPipelines(AuditReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:HDRenderPipelineAsset");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                HDRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;
                GlobalDynamicResolutionSettings dynamicResolution = settings.dynamicResolutionSettings;
                PipelineAudit entry = new PipelineAudit
                {
                    Path = path,
                    Name = asset.name,
                    ActiveGlobal = ReferenceEquals(GraphicsSettings.defaultRenderPipeline, asset),
                    ActiveForCurrentQuality = ReferenceEquals(GraphicsSettings.currentRenderPipeline, asset),
                    MotionVectors = settings.supportMotionVectors,
                    MsaaSamples = settings.msaaSampleCount.ToString(),
                    DynamicResolutionEnabled = dynamicResolution.enabled,
                    DynamicResolutionMipBias = dynamicResolution.useMipBias,
                    DlssDlaaPreset = dynamicResolution.DLSSRenderPresetForDLAA,
                    DynamicResolutionMinimumPercent = dynamicResolution.minPercentage,
                    DynamicResolutionMaximumPercent = dynamicResolution.maxPercentage,
                    UpsampleFilter = dynamicResolution.upsampleFilter.ToString(),
                    AdvancedUpscalers = dynamicResolution.advancedUpscalerNames == null
                        ? Array.Empty<string>()
                        : dynamicResolution.advancedUpscalerNames.ToArray(),
                };
                report.Pipelines.Add(entry);
            }

            for (int qualityIndex = 0; qualityIndex < QualitySettings.names.Length; qualityIndex++)
            {
                RenderPipelineAsset qualityAsset = QualitySettings.GetRenderPipelineAssetAt(qualityIndex);
                report.QualityLevels.Add(new QualityAudit
                {
                    Index = qualityIndex,
                    Name = QualitySettings.names[qualityIndex],
                    PipelineAsset = qualityAsset == null
                        ? "Project default"
                        : AssetDatabase.GetAssetPath(qualityAsset),
                });
            }
        }

        private static void AuditCameras(AuditReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Game" });
            for (int index = 0; index < sceneGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                if (!YamlMayContainCamera(projectRoot, path))
                {
                    continue;
                }

                Scene preview = default;
                try
                {
                    preview = EditorSceneManager.OpenPreviewScene(path);
                    GameObject[] roots = preview.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
                        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                        {
                            report.Cameras.Add(CaptureCamera(cameras[cameraIndex], path, "Scene"));
                        }
                    }
                }
                catch (Exception exception)
                {
                    report.Warnings.Add($"Could not inspect scene {path}: {exception.Message}");
                }
                finally
                {
                    if (preview.IsValid())
                    {
                        EditorSceneManager.ClosePreviewScene(preview);
                    }
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game" });
            for (int index = 0; index < prefabGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                if (!YamlMayContainCamera(projectRoot, path))
                {
                    continue;
                }

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                    for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                    {
                        report.Cameras.Add(CaptureCamera(cameras[cameraIndex], path, "Prefab"));
                    }
                }
                catch (Exception exception)
                {
                    report.Warnings.Add($"Could not inspect prefab {path}: {exception.Message}");
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
        }

        private static CameraAudit CaptureCamera(Camera camera, string path, string sourceKind)
        {
            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();
            AntiAliasingCameraPolicy policy = camera.GetComponent<AntiAliasingCameraPolicy>();
            return new CameraAudit
            {
                AssetPath = path,
                SourceKind = sourceKind,
                HierarchyPath = BuildHierarchyPath(camera.transform),
                CameraType = camera.cameraType.ToString(),
                Enabled = camera.enabled,
                TargetTexture = camera.targetTexture != null,
                AllowMsaa = camera.allowMSAA,
                AllowDynamicResolution = camera.allowDynamicResolution,
                HasHdrpAdditionalData = hdCamera != null,
                SerializedAntiAliasing = hdCamera == null
                    ? "Runtime managed"
                    : hdCamera.antialiasing.ToString(),
                SerializedDlssAllowed = hdCamera != null && hdCamera.allowDeepLearningSuperSampling,
                SerializedFsr2Allowed = hdCamera != null && hdCamera.allowFidelityFX2SuperResolution,
                PolicyRole = policy == null ? "Auto (implicit)" : policy.Role.ToString(),
            };
        }

        private static void AuditVolumes(AuditReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets/Game", "Assets/Settings" });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                VolumeAudit entry = new VolumeAudit
                {
                    Path = path,
                    Name = profile.name,
                };
                for (int componentIndex = 0; componentIndex < profile.components.Count; componentIndex++)
                {
                    VolumeComponent component = profile.components[componentIndex];
                    if (component == null)
                    {
                        continue;
                    }

                    string componentName = component.GetType().Name;
                    entry.Components.Add(componentName + (component.active ? " (active)" : " (inactive)"));
                    if (component is MotionBlur motionBlur)
                    {
                        entry.HasActiveMotionBlur = motionBlur.active &&
                                                    motionBlur.intensity.overrideState &&
                                                    motionBlur.intensity.value > 0.0001f;
                        entry.MotionBlur =
                            $"active={motionBlur.active}, intensity={motionBlur.intensity.value:0.###}, " +
                            $"override={motionBlur.intensity.overrideState}";
                    }
                    else if (component is DepthOfField depthOfField)
                    {
                        entry.DepthOfField =
                            $"active={depthOfField.active}, mode={depthOfField.focusMode.value}";
                    }
                }

                report.Volumes.Add(entry);
            }
        }

        private static void AuditMaterials(AuditReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Game" });
            HashSet<string> auditedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            report.Materials.Total = guids.Length;

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    report.Materials.MissingOrInvalidShader++;
                    report.Materials.HighRiskAssets.Add(path + " (missing/invalid shader)");
                    continue;
                }

                bool alphaClipped = HasEnabledFloat(material, "_AlphaCutoffEnable") ||
                                    HasEnabledFloat(material, "_AlphaClip");
                bool transparent = material.HasProperty("_SurfaceType") &&
                                   material.GetFloat("_SurfaceType") > 0.5f;
                bool geometricSpecularAa = HasEnabledFloat(material, "_EnableGeometricSpecularAA");
                bool motionVectorPass = material.GetShaderPassEnabled("MotionVectors");

                if (alphaClipped)
                {
                    report.Materials.AlphaClipped++;
                }
                if (transparent)
                {
                    report.Materials.Transparent++;
                }
                if (geometricSpecularAa)
                {
                    report.Materials.GeometricSpecularAaEnabled++;
                }
                if (motionVectorPass)
                {
                    report.Materials.MotionVectorPassEnabled++;
                }

                bool alphaClipTextureMissingMips = false;
                Shader shader = material.shader;
                int propertyCount = shader.GetPropertyCount();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    if (shader.GetPropertyType(propertyIndex) != ShaderPropertyType.Texture)
                    {
                        continue;
                    }

                    string propertyName = shader.GetPropertyName(propertyIndex);
                    Texture texture = material.GetTexture(propertyName);
                    if (texture == null)
                    {
                        continue;
                    }

                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    if (!string.IsNullOrEmpty(texturePath) && auditedTextures.Add(texturePath))
                    {
                        report.Materials.ReferencedTextures++;
                        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
                        {
                            if (importer.mipmapEnabled)
                            {
                                report.Materials.TexturesWithMipmaps++;
                            }
                            else
                            {
                                report.Materials.TexturesWithoutMipmaps++;
                            }
                        }
                    }

                    if (alphaClipped && AssetImporter.GetAtPath(texturePath) is TextureImporter alphaImporter &&
                        !alphaImporter.mipmapEnabled)
                    {
                        alphaClipTextureMissingMips = true;
                    }
                }

                if (alphaClipTextureMissingMips)
                {
                    report.Materials.AlphaClipMaterialsWithNonMipTexture++;
                    report.Materials.HighRiskAssets.Add(path + " (alpha clip references a non-mip texture)");
                }
            }
        }

        private static void AuditShaders(AuditReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Game" });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string absolutePath = Path.Combine(projectRoot, path);
                string source = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
                ShaderAudit entry = new ShaderAudit
                {
                    Path = path,
                    HasMotionVectorsPass =
                        source.IndexOf("MotionVectors", StringComparison.OrdinalIgnoreCase) >= 0,
                    HasVertexAnimationTime =
                        source.IndexOf("_TimeParameters", StringComparison.Ordinal) >= 0,
                    HasPreviousFrameTime =
                        source.IndexOf("_LastTimeParameters", StringComparison.Ordinal) >= 0,
                };
                report.Shaders.Add(entry);
            }
        }

        private static void Validate(AuditReport report)
        {
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
            {
                report.Errors.Add("The active render pipeline is not HDRP.");
            }

            if (AssetDatabase.LoadAssetAtPath<AntiAliasingSettings>(SettingsAssetPath) == null)
            {
                report.Errors.Add("The project anti-aliasing settings asset is missing.");
            }

            for (int index = 0; index < report.Pipelines.Count; index++)
            {
                PipelineAudit pipeline = report.Pipelines[index];
                if (!pipeline.MotionVectors)
                {
                    report.Errors.Add($"Motion vectors are disabled in HDRP asset {pipeline.Path}.");
                }

                if (!string.Equals(pipeline.MsaaSamples, "None", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(pipeline.MsaaSamples, "1", StringComparison.OrdinalIgnoreCase))
                {
                    report.Warnings.Add(
                        $"HDRP asset {pipeline.Path} has MSAA {pipeline.MsaaSamples}; do not stack it with deferred TAA.");
                }

                if (pipeline.DynamicResolutionMipBias)
                {
                    report.Warnings.Add(
                        $"HDRP asset {pipeline.Path} enables global dynamic-resolution mip bias; " +
                        "this can amplify DLSS texture, normal-map and specular shimmer.");
                }

                if (ContainsUpscaler(pipeline.AdvancedUpscalers, "DLSS") &&
                    pipeline.DlssDlaaPreset != 1u)
                {
                    report.Warnings.Add(
                        $"HDRP asset {pipeline.Path} uses DLAA render preset " +
                        $"{pipeline.DlssDlaaPreset}; Preset F (1) is required for the " +
                        "project's DLAA stability policy.");
                }
            }

            for (int index = 0; index < report.GlobalSettings.Count; index++)
            {
                GlobalSettingsAudit global = report.GlobalSettings[index];
                if (!global.CameraMotionVectors || !global.CameraObjectMotionVectors)
                {
                    report.Errors.Add(
                        $"Default camera motion-vector frame settings are disabled in {global.Path}.");
                }
            }

            for (int index = 0; index < report.Cameras.Count; index++)
            {
                CameraAudit camera = report.Cameras[index];
                if (!camera.HasHdrpAdditionalData)
                {
                    report.Notes.Add(
                        $"{camera.AssetPath}:{camera.HierarchyPath} has no serialized HD camera data; " +
                        "the runtime controller will add non-persistent data when instantiated.");
                }
                if (camera.AllowMsaa)
                {
                    report.Notes.Add(
                        $"{camera.AssetPath}:{camera.HierarchyPath} serializes allowMSAA=true; " +
                        "the runtime controller disables it for managed HDRP game cameras.");
                }
            }

            if (report.Materials.AlphaClipMaterialsWithNonMipTexture > 0)
            {
                report.Warnings.Add(
                    $"{report.Materials.AlphaClipMaterialsWithNonMipTexture} alpha-clipped materials reference " +
                    "at least one texture without mipmaps. Review only the listed assets; no bulk mutation was made.");
            }


            for (int index = 0; index < report.Volumes.Count; index++)
            {
                VolumeAudit volume = report.Volumes[index];
                if (volume.HasActiveMotionBlur)
                {
                    report.Notes.Add(
                        $"{volume.Path} contains active Motion Blur ({volume.MotionBlur}); " +
                        "AA comparison captures must override it locally rather than using blur to hide artifacts.");
                }
            }
        }

        private static void WriteReports(AuditReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absoluteReportDirectory = Path.Combine(projectRoot, ReportDirectory);
            Directory.CreateDirectory(absoluteReportDirectory);
            File.WriteAllText(
                Path.Combine(absoluteReportDirectory, "ProjectAudit.json"),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(absoluteReportDirectory, "ProjectAudit.md"),
                BuildMarkdown(report),
                new UTF8Encoding(false));
        }

        private static string BuildMarkdown(AuditReport report)
        {
            StringBuilder builder = new StringBuilder(16384);
            builder.AppendLine("# Anti-Aliasing Project Audit");
            builder.AppendLine();
            builder.AppendLine($"Generated: `{report.GeneratedUtc}`");
            builder.AppendLine($"Unity / HDRP: `{report.UnityVersion}` / `{report.HdrpVersion}`");
            builder.AppendLine($"GPU: `{report.GraphicsDevice}` (`{report.GraphicsApi}`)");
            builder.AppendLine($"Quality: `{report.ActiveQualityName}` ({report.ActiveQualityLevel})");
            builder.AppendLine();
            builder.AppendLine("## Pipeline assets");
            builder.AppendLine();
            builder.AppendLine("| Asset | Motion vectors | MSAA | Dynamic resolution | Mip bias | DLAA preset | Range | Filter | Upscalers |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            for (int index = 0; index < report.Pipelines.Count; index++)
            {
                PipelineAudit pipeline = report.Pipelines[index];
                builder.AppendLine(
                    $"| `{pipeline.Path}` | {pipeline.MotionVectors} | {pipeline.MsaaSamples} | " +
                    $"{pipeline.DynamicResolutionEnabled} | {pipeline.DynamicResolutionMipBias} | " +
                    $"{pipeline.DlssDlaaPreset} | " +
                    $"{pipeline.DynamicResolutionMinimumPercent:0.#}–" +
                    $"{pipeline.DynamicResolutionMaximumPercent:0.#}% | {pipeline.UpsampleFilter} | " +
                    $"{string.Join(", ", pipeline.AdvancedUpscalers)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## HDRP Global Settings");
            builder.AppendLine();
            builder.AppendLine("| Asset | AA frame setting | Camera MV | Object MV | Transparent MV | Motion Blur | DoF |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            for (int index = 0; index < report.GlobalSettings.Count; index++)
            {
                GlobalSettingsAudit global = report.GlobalSettings[index];
                builder.AppendLine(
                    $"| `{global.Path}` | {global.CameraAntialiasing} | {global.CameraMotionVectors} | " +
                    $"{global.CameraObjectMotionVectors} | {global.CameraTransparentMotionVectors} | " +
                    $"{global.CameraMotionBlur} | {global.CameraDepthOfField} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Cameras");
            builder.AppendLine();
            builder.AppendLine("| Source | Camera | HD data | Serialized AA | DynRes | DLSS | Policy |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            for (int index = 0; index < report.Cameras.Count; index++)
            {
                CameraAudit camera = report.Cameras[index];
                builder.AppendLine(
                    $"| `{camera.AssetPath}` | `{camera.HierarchyPath}` | {camera.HasHdrpAdditionalData} | " +
                    $"{camera.SerializedAntiAliasing} | {camera.AllowDynamicResolution} | " +
                    $"{camera.SerializedDlssAllowed} | {camera.PolicyRole} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Material and shader risk summary");
            builder.AppendLine();
            builder.AppendLine($"- Materials: {report.Materials.Total}");
            builder.AppendLine($"- Alpha clipped: {report.Materials.AlphaClipped}");
            builder.AppendLine($"- Transparent: {report.Materials.Transparent}");
            builder.AppendLine($"- Geometric specular AA enabled: {report.Materials.GeometricSpecularAaEnabled}");
            builder.AppendLine($"- Motion-vector pass enabled: {report.Materials.MotionVectorPassEnabled}");
            builder.AppendLine(
                $"- Referenced textures with/without mipmaps: {report.Materials.TexturesWithMipmaps}/" +
                $"{report.Materials.TexturesWithoutMipmaps}");
            builder.AppendLine(
                $"- Alpha-clip materials referencing non-mip textures: " +
                $"{report.Materials.AlphaClipMaterialsWithNonMipTexture}");
            builder.AppendLine();
            builder.AppendLine("Custom shaders:");
            for (int index = 0; index < report.Shaders.Count; index++)
            {
                ShaderAudit shader = report.Shaders[index];
                builder.AppendLine(
                    $"- `{shader.Path}` — MotionVectors={shader.HasMotionVectorsPass}, " +
                    $"vertexTime={shader.HasVertexAnimationTime}, previousTime={shader.HasPreviousFrameTime}");
            }

            AppendSection(builder, "Errors", report.Errors);
            AppendSection(builder, "Warnings", report.Warnings);
            AppendSection(builder, "Notes", report.Notes);

            if (report.Materials.HighRiskAssets.Count > 0)
            {
                AppendSection(builder, "Material assets requiring local review", report.Materials.HighRiskAssets);
            }

            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string heading, List<string> entries)
        {
            builder.AppendLine();
            builder.AppendLine("## " + heading);
            builder.AppendLine();
            if (entries.Count == 0)
            {
                builder.AppendLine("None.");
                return;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                builder.AppendLine("- " + entries[index]);
            }
        }

        private static bool HasEnabledFloat(Material material, string property)
        {
            return material.HasProperty(property) && material.GetFloat(property) > 0.5f;
        }

        private static bool ContainsUpscaler(string[] upscalers, string expected)
        {
            if (upscalers == null)
            {
                return false;
            }

            for (int index = 0; index < upscalers.Length; index++)
            {
                if (string.Equals(upscalers[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFrameSettingEnabled(
            ulong lowBits,
            ulong highBits,
            FrameSettingsField field)
        {
            int bit = (int)field;
            return bit < 64
                ? ((lowBits >> bit) & 1ul) != 0ul
                : ((highBits >> (bit - 64)) & 1ul) != 0ul;
        }

        private static bool YamlMayContainCamera(string projectRoot, string assetPath)
        {
            string absolutePath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(absolutePath))
            {
                return false;
            }

            string source = File.ReadAllText(absolutePath);
            return source.IndexOf("--- !u!20", StringComparison.Ordinal) >= 0;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            StringBuilder builder = new StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string ResolvePackageVersion(string packagePath)
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(packagePath);
            return package == null ? "Unknown" : package.version;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }

        [Serializable]
        private sealed class AuditReport
        {
            public string GeneratedUtc;
            public string UnityVersion;
            public string HdrpVersion;
            public string GraphicsDevice;
            public string GraphicsApi;
            public int ActiveQualityLevel;
            public string ActiveQualityName;
            public string AnisotropicFiltering;
            public List<PipelineAudit> Pipelines = new List<PipelineAudit>();
            public List<GlobalSettingsAudit> GlobalSettings = new List<GlobalSettingsAudit>();
            public List<QualityAudit> QualityLevels = new List<QualityAudit>();
            public List<CameraAudit> Cameras = new List<CameraAudit>();
            public List<VolumeAudit> Volumes = new List<VolumeAudit>();
            public MaterialAudit Materials = new MaterialAudit();
            public List<ShaderAudit> Shaders = new List<ShaderAudit>();
            public List<string> Errors = new List<string>();
            public List<string> Warnings = new List<string>();
            public List<string> Notes = new List<string>();
        }

        [Serializable]
        private sealed class GlobalSettingsAudit
        {
            public string Path;
            public bool CameraAntialiasing;
            public bool CameraMotionVectors;
            public bool CameraObjectMotionVectors;
            public bool CameraTransparentMotionVectors;
            public bool CameraMotionBlur;
            public bool CameraDepthOfField;
        }

        [Serializable]
        private sealed class PipelineAudit
        {
            public string Path;
            public string Name;
            public bool ActiveGlobal;
            public bool ActiveForCurrentQuality;
            public bool MotionVectors;
            public string MsaaSamples;
            public bool DynamicResolutionEnabled;
            public bool DynamicResolutionMipBias;
            public uint DlssDlaaPreset;
            public float DynamicResolutionMinimumPercent;
            public float DynamicResolutionMaximumPercent;
            public string UpsampleFilter;
            public string[] AdvancedUpscalers;
        }

        [Serializable]
        private sealed class QualityAudit
        {
            public int Index;
            public string Name;
            public string PipelineAsset;
        }

        [Serializable]
        private sealed class CameraAudit
        {
            public string AssetPath;
            public string SourceKind;
            public string HierarchyPath;
            public string CameraType;
            public bool Enabled;
            public bool TargetTexture;
            public bool AllowMsaa;
            public bool AllowDynamicResolution;
            public bool HasHdrpAdditionalData;
            public string SerializedAntiAliasing;
            public bool SerializedDlssAllowed;
            public bool SerializedFsr2Allowed;
            public string PolicyRole;
        }

        [Serializable]
        private sealed class VolumeAudit
        {
            public string Path;
            public string Name;
            public List<string> Components = new List<string>();
            public bool HasActiveMotionBlur;
            public string MotionBlur = "not present";
            public string DepthOfField = "not present";
        }

        [Serializable]
        private sealed class MaterialAudit
        {
            public int Total;
            public int MissingOrInvalidShader;
            public int AlphaClipped;
            public int Transparent;
            public int GeometricSpecularAaEnabled;
            public int MotionVectorPassEnabled;
            public int ReferencedTextures;
            public int TexturesWithMipmaps;
            public int TexturesWithoutMipmaps;
            public int AlphaClipMaterialsWithNonMipTexture;
            public List<string> HighRiskAssets = new List<string>();
        }

        [Serializable]
        private sealed class ShaderAudit
        {
            public string Path;
            public bool HasMotionVectorsPass;
            public bool HasVertexAnimationTime;
            public bool HasPreviousFrameTime;
        }
    }
}
