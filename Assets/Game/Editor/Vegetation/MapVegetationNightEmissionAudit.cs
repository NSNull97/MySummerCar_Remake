using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.World.Partition;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Renders saved generated grass three times from the same camera: under a
    /// deterministic directional light, under a local HDRP spot light, and with
    /// zero scene radiance. This catches emissive, effectively-unlit, or
    /// directional-only grass without changing generated scenes, profiles,
    /// materials or runtime renderers.
    /// </summary>
    public static class MapVegetationNightEmissionAudit
    {
        public const string ValidatorId =
            "msc.map-vegetation-night-emission-audit.v1";
        public const float FixedExposureEv100 = 7.25f;
        public const float ControlSunLux = 100000f;
        public const float PunctualControlCandela = 43000f;
        public const float PunctualControlRangeMeters = 35f;
        public const float PunctualControlInnerAngleDegrees = 34f;
        public const float PunctualControlOuterAngleDegrees = 52f;
        public const float BrightPixelThreshold = 0.04f;
        public const float MinimumControlMeanLuminance = 0.0015f;
        public const float MinimumControlBrightPixelFraction = 0.001f;
        public const float MaximumZeroToControlMeanRatio = 0.12f;
        public const float MaximumZeroToControlBrightPixelRatio = 0.15f;
        public const float MaximumZeroMeanLuminanceFloor = 0.0005f;
        public const float MaximumZeroBrightPixelFractionFloor = 0.0001f;

        private const string OutputRoot =
            "Artifacts/VegetationRebuild/NightEmissionAudit";
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Tools/MSC/Vegetation/Capture Night Emission Audit")]
        public static void CaptureBatch()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Night emission audit requires graphics; remove -nographics.");
            if (!(GraphicsSettings.currentRenderPipeline is
                    HDRenderPipelineAsset))
                throw new InvalidOperationException(
                    "Night emission audit requires the project's HDRP asset.");

            MapVegetationRebuildOptions options = AssetDatabase
                .LoadAssetAtPath<MapVegetationRebuildOptions>(
                    MapVegetationRebuildOptions.AssetPath);
            if (options == null)
                throw new InvalidOperationException(
                    "Vegetation rebuild settings are missing.");

            WorldCellIndex cell = RequestedCell(
                Environment.GetCommandLineArgs(), options.SelectedCell);
            string generatedScene = MapVegetationRebuild.CellScenePath(cell);
            if (!File.Exists(generatedScene))
                throw new FileNotFoundException(
                    "Saved generated vegetation cell is missing.",
                    generatedScene);
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException(
                        "Save or discard scene edits before the night emission audit.");

            string runId;
            string outputPath = CreateUniqueOutputDirectory(out runId);
            var report = new NightEmissionReport
            {
                schemaVersion = 1,
                gateRevision = 2,
                validatorId = ValidatorId,
                runId = runId,
                capturedUtc = DateTime.UtcNow.ToString(
                    "O", CultureInfo.InvariantCulture),
                cellId = cell.Id,
                generatedScene = generatedScene,
                outputDirectory = outputPath,
                bindingVersion = MapVegetationGrassBindings.BindingVersion,
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                fixedExposureEv100 = FixedExposureEv100,
                controlSunLux = ControlSunLux,
                punctualControlCandela = PunctualControlCandela,
                punctualControlRangeMeters = PunctualControlRangeMeters,
                punctualControlInnerAngleDegrees =
                    PunctualControlInnerAngleDegrees,
                punctualControlOuterAngleDegrees =
                    PunctualControlOuterAngleDegrees,
                zeroRadianceSunLux = 0f,
                zeroRadiancePunctualCandela = 0f,
                brightPixelThreshold = BrightPixelThreshold,
                minimumControlMeanLuminance =
                    MinimumControlMeanLuminance,
                minimumControlBrightPixelFraction =
                    MinimumControlBrightPixelFraction,
                maximumZeroToControlMeanRatio =
                    MaximumZeroToControlMeanRatio,
                maximumZeroToControlBrightPixelRatio =
                    MaximumZeroToControlBrightPixelRatio,
                limitations = new[]
                {
                    "This is a grass-only directional, local-punctual and " +
                    "zero-radiance material regression probe, not a subjective " +
                    "gameplay-night brightness acceptance image.",
                    "It uses the selected saved vegetation cell and its actual generated " +
                    "profiles, meshes, materials and packed instance records without regeneration.",
                    "The black environment deliberately removes terrain, trees, sky, moon, " +
                    "fog and local lights so surviving grass radiance is attributable to its shader."
                }
            };

            SceneSetup[] originalSetup =
                EditorSceneManager.GetSceneManagerSetup();
            var temporary = new List<Object>();
            RenderTexture target = null;
            Texture2D pixels = null;
            Camera camera = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                Scene vegetationScene = EditorSceneManager.OpenScene(
                    generatedScene, OpenSceneMode.Single);
                GameObject[] roots = vegetationScene.GetRootGameObjects();
                GameObject[] grassRoots = roots.Where(IsGeneratedGrassRoot)
                    .ToArray();
                if (grassRoots.Length != 1)
                    throw new InvalidDataException(
                        "Expected exactly one generated GrassCoverage root in " +
                        generatedScene + ", found " + grassRoots.Length + ".");

                foreach (GameObject root in roots)
                    if (root != grassRoots[0])
                        root.SetActive(false);

                GeneratedVegetationGroup group =
                    grassRoots[0].GetComponent<GeneratedVegetationGroup>();
                report.generatedFingerprint = group.Fingerprint;
                VegetationWorldRenderer[] renderers = grassRoots[0]
                    .GetComponentsInChildren<VegetationWorldRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidDataException(
                        "Generated GrassCoverage has no VegetationWorldRenderer.");

                var positions = new List<Vector3>();
                var profilePaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (VegetationWorldRenderer renderer in renderers)
                {
                    if (renderer.Catalog == null)
                        throw new InvalidDataException(
                            "Generated grass renderer has no saved catalog.");
                    string catalogPath = AssetDatabase.GetAssetPath(
                        renderer.Catalog);
                    report.catalogs.Add(new AssetReference
                    {
                        asset = catalogPath,
                        dependencyHash = AssetDatabase.GetAssetDependencyHash(
                            catalogPath).ToString()
                    });
                    var usedProfileIndices = new SortedSet<int>();
                    foreach (VegetationCellAsset savedCell in
                             renderer.Catalog.Cells)
                    {
                        if (savedCell == null) continue;
                        foreach (VegetationTileRecord tile in savedCell.Tiles)
                        {
                            if (tile == null) continue;
                            foreach (VegetationProfileTileInstances batch in
                                     tile.ProfileInstances)
                            {
                                if (batch == null) continue;
                                usedProfileIndices.Add(batch.ProfileIndex);
                                foreach (VegetationInstanceRecord instance in
                                         batch.Instances)
                                    positions.Add(instance.WorldPosition);
                            }
                        }
                    }
                    foreach (int profileIndex in usedProfileIndices)
                    {
                        if (profileIndex < 0 || profileIndex >=
                            renderer.Catalog.Profiles.Count)
                            throw new InvalidDataException(
                                "Saved grass batch references invalid profile " +
                                "index " + profileIndex + ".");
                        VegetationProfile profile =
                            renderer.Catalog.Profiles[profileIndex];
                        if (profile == null)
                            throw new InvalidDataException(
                                "Used saved grass profile is null at index " +
                                profileIndex + ".");
                        string profilePath = AssetDatabase.GetAssetPath(profile);
                        if (!profilePaths.Add(profilePath)) continue;
                        Material material = profile.Material;
                        string materialPath = material != null
                            ? AssetDatabase.GetAssetPath(material)
                            : string.Empty;
                        report.profiles.Add(new GrassProfileReference
                        {
                            catalogProfileIndex = profileIndex,
                            profileId = profile.ProfileId,
                            profileAsset = profilePath,
                            profileDependencyHash = AssetDatabase
                                .GetAssetDependencyHash(profilePath).ToString(),
                            materialAsset = materialPath,
                            materialDependencyHash =
                                string.IsNullOrEmpty(materialPath)
                                    ? string.Empty
                                    : AssetDatabase.GetAssetDependencyHash(
                                        materialPath).ToString(),
                            shader = material != null && material.shader != null
                                ? material.shader.name
                                : string.Empty
                        });
                    }
                    renderer.RebuildGpuResources();
                    report.gpuBatchCount += renderer.GpuBatchCount;
                }

                report.savedGrassInstances = positions.Count;
                if (positions.Count == 0 || report.gpuBatchCount == 0)
                    throw new InvalidDataException(
                        "Saved generated grass has no renderable instances.");
                if (report.profiles.Count == 0 || report.profiles.Any(profile =>
                        string.IsNullOrEmpty(profile.profileAsset) ||
                        string.IsNullOrEmpty(profile.materialAsset) ||
                        string.IsNullOrEmpty(profile.shader)))
                    throw new InvalidDataException(
                        "Saved generated grass profile/material references are incomplete.");

                Vector3 center = HorizontalBoundsCenter(positions);
                MapVegetationVisualAudit.GrassAuditSelection selection =
                    MapVegetationVisualAudit.SelectDenseGrassAuditPoint(
                        positions, center, positions[0]);
                report.selectionPosition = selection.Position;
                report.selectionLookDirection = selection.LookDirection;
                report.selectionNearbyInstances = selection.NearbyInstances;
                report.selectionForwardInstances = selection.ForwardInstances;
                report.selectionNearestForwardInstanceMeters =
                    selection.NearestForwardInstanceMeters;
                if (selection.NearbyInstances <
                        MapVegetationVisualAudit
                            .GrassAuditMinimumNearbyInstances ||
                    selection.ForwardInstances <
                        MapVegetationVisualAudit
                            .GrassAuditMinimumForwardInstances ||
                    selection.NearestForwardInstanceMeters < 0f ||
                    selection.NearestForwardInstanceMeters >
                        MapVegetationVisualAudit
                            .GrassAuditMaximumNearestForwardMeters)
                    throw new InvalidDataException(
                        "The saved cell cannot provide a representative dense " +
                        "forward grass view for the emission audit.");

                Scene auditScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SetActiveScene(auditScene);
                camera = CreateCamera(temporary);
                LightingRig lighting = CreateBlackEnvironment(temporary);
                target = new RenderTexture(
                    Width, Height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(
                    Width, Height, TextureFormat.RGBA32, false);

                Vector3 direction = selection.LookDirection.sqrMagnitude >
                    0.0001f
                    ? selection.LookDirection.normalized
                    : Vector3.forward;
                Vector3 eye = selection.Position - direction * 1.25f +
                    Vector3.up * 0.45f;
                Vector3 look = selection.Position + direction * 7f +
                    Vector3.up * 0.10f;
                camera.transform.position = eye;
                camera.transform.LookAt(look);
                report.cameraPosition = eye;
                report.cameraRotationEuler = camera.transform.eulerAngles;
                ConfigurePunctualControl(lighting, camera.transform, look);
                report.punctualControlPosition =
                    lighting.Punctual.transform.position;
                report.punctualControlRotationEuler =
                    lighting.Punctual.transform.eulerAngles;

                SetControlLighting(
                    lighting, ControlLighting.Directional);
                report.control = CaptureFrame(
                    camera, target, pixels,
                    Path.Combine(outputPath, "01_LitControl.png"));
                SetControlLighting(
                    lighting, ControlLighting.Punctual);
                report.punctualControl = CaptureFrame(
                    camera, target, pixels,
                    Path.Combine(outputPath, "02_PunctualControl.png"));
                SetControlLighting(lighting, ControlLighting.None);
                report.zeroRadiance = CaptureFrame(
                    camera, target, pixels,
                    Path.Combine(outputPath, "03_ZeroRadiance.png"));

                GateEvaluation gate = EvaluateGate(
                    report.control, report.punctualControl,
                    report.zeroRadiance);
                report.controlVisible = gate.ControlVisible;
                report.directionalControlVisible =
                    gate.DirectionalControlVisible;
                report.punctualControlVisible =
                    gate.PunctualControlVisible;
                report.allControlsVisible = gate.AllControlsVisible;
                report.meanLuminanceRatio = gate.MeanLuminanceRatio;
                report.brightPixelFractionRatio =
                    gate.BrightPixelFractionRatio;
                report.punctualMeanLuminanceRatio =
                    gate.PunctualMeanLuminanceRatio;
                report.punctualBrightPixelFractionRatio =
                    gate.PunctualBrightPixelFractionRatio;
                report.directionalZeroRadianceSuppressed =
                    gate.DirectionalZeroRadianceSuppressed;
                report.punctualZeroRadianceSuppressed =
                    gate.PunctualZeroRadianceSuppressed;
                report.zeroRadianceSuppressed =
                    gate.ZeroRadianceSuppressed;
                report.passed = gate.Passed;
                report.notes.Add(gate.Diagnostic);
                WriteReport(outputPath, report);
                if (!report.passed)
                    throw new InvalidOperationException(
                        "Generated grass night-emission gate failed " +
                        "(directionalControlVisible=" +
                        report.directionalControlVisible +
                        ", punctualControlVisible=" +
                        report.punctualControlVisible +
                        ", zeroRadianceSuppressed=" +
                        report.zeroRadianceSuppressed +
                        "); inspect the three PNGs and report at " +
                        Path.GetFullPath(outputPath) + ".");

                Debug.Log("MAP_VEGETATION_NIGHT_EMISSION_AUDIT_OK cell=" +
                    cell.Id + " instances=" + report.savedGrassInstances +
                    " directionalMeanRatio=" +
                    report.meanLuminanceRatio.ToString(
                        "F4", CultureInfo.InvariantCulture) +
                    " punctualMeanRatio=" +
                    report.punctualMeanLuminanceRatio.ToString(
                        "F4", CultureInfo.InvariantCulture) + " output=" +
                    Path.GetFullPath(outputPath));
            }
            catch (Exception exception)
            {
                report.passed = false;
                report.notes.Add("Audit failed: " + exception);
                WriteReport(outputPath, report);
                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (camera != null) camera.targetTexture = null;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (pixels != null) Object.DestroyImmediate(pixels);
                for (int index = temporary.Count - 1; index >= 0; index--)
                    if (temporary[index] != null)
                        Object.DestroyImmediate(temporary[index]);
                if (originalSetup.Any(setup =>
                        !string.IsNullOrEmpty(setup.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        internal static GateEvaluation EvaluateGate(
            FrameMetrics control, FrameMetrics zeroRadiance)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (zeroRadiance == null)
                throw new ArgumentNullException(nameof(zeroRadiance));
            ControlGate directional = EvaluateControl(
                control, zeroRadiance);
            string diagnostic = string.Format(
                CultureInfo.InvariantCulture,
                "controlVisible={0}; zeroRadianceSuppressed={1}; " +
                "meanRatio={2:F4} (max {3:F2}); brightPixelRatio={4:F4} " +
                "(max {5:F2}).",
                directional.Visible, directional.ZeroRadianceSuppressed,
                directional.MeanLuminanceRatio,
                MaximumZeroToControlMeanRatio,
                directional.BrightPixelFractionRatio,
                MaximumZeroToControlBrightPixelRatio);
            return new GateEvaluation(
                directional.Visible && directional.ZeroRadianceSuppressed,
                false, directional.Visible, false,
                directional.ZeroRadianceSuppressed, false,
                directional.ZeroRadianceSuppressed,
                directional.MeanLuminanceRatio,
                directional.BrightPixelFractionRatio, 0f, 0f,
                diagnostic);
        }

        internal static GateEvaluation EvaluateGate(
            FrameMetrics directionalControl,
            FrameMetrics punctualControl,
            FrameMetrics zeroRadiance)
        {
            if (directionalControl == null)
                throw new ArgumentNullException(nameof(directionalControl));
            if (punctualControl == null)
                throw new ArgumentNullException(nameof(punctualControl));
            if (zeroRadiance == null)
                throw new ArgumentNullException(nameof(zeroRadiance));

            ControlGate directional = EvaluateControl(
                directionalControl, zeroRadiance);
            ControlGate punctual = EvaluateControl(
                punctualControl, zeroRadiance);
            bool controlsVisible = directional.Visible && punctual.Visible;
            bool zeroRadianceSuppressed =
                directional.ZeroRadianceSuppressed &&
                punctual.ZeroRadianceSuppressed;
            string diagnostic = string.Format(
                CultureInfo.InvariantCulture,
                "controlVisible={0}; directionalControlVisible={0}; " +
                "punctualControlVisible={1}; " +
                "zeroRadianceSuppressed={2}; " +
                "directionalMeanRatio={3:F4}; " +
                "directionalBrightPixelRatio={4:F4}; " +
                "punctualMeanRatio={5:F4}; " +
                "punctualBrightPixelRatio={6:F4}; " +
                "ratio maxima mean={7:F2}, bright={8:F2}.",
                directional.Visible, punctual.Visible,
                zeroRadianceSuppressed,
                directional.MeanLuminanceRatio,
                directional.BrightPixelFractionRatio,
                punctual.MeanLuminanceRatio,
                punctual.BrightPixelFractionRatio,
                MaximumZeroToControlMeanRatio,
                MaximumZeroToControlBrightPixelRatio);
            return new GateEvaluation(
                controlsVisible && zeroRadianceSuppressed,
                true, directional.Visible, punctual.Visible,
                directional.ZeroRadianceSuppressed,
                punctual.ZeroRadianceSuppressed,
                zeroRadianceSuppressed,
                directional.MeanLuminanceRatio,
                directional.BrightPixelFractionRatio,
                punctual.MeanLuminanceRatio,
                punctual.BrightPixelFractionRatio,
                diagnostic);
        }

        private static ControlGate EvaluateControl(
            FrameMetrics control, FrameMetrics zeroRadiance)
        {
            bool visible = control.meanLuminance >=
                    MinimumControlMeanLuminance &&
                control.brightPixelFraction >=
                    MinimumControlBrightPixelFraction;
            float meanRatio = control.meanLuminance > 0f
                ? zeroRadiance.meanLuminance / control.meanLuminance
                : 1000000f;
            float brightRatio = control.brightPixelFraction > 0f
                ? zeroRadiance.brightPixelFraction /
                    control.brightPixelFraction
                : 1000000f;
            bool suppressed = zeroRadiance.meanLuminance <= Mathf.Max(
                    MaximumZeroMeanLuminanceFloor,
                    control.meanLuminance * MaximumZeroToControlMeanRatio) &&
                zeroRadiance.brightPixelFraction <= Mathf.Max(
                    MaximumZeroBrightPixelFractionFloor,
                    control.brightPixelFraction *
                    MaximumZeroToControlBrightPixelRatio);
            return new ControlGate(
                visible, suppressed, meanRatio, brightRatio);
        }

        private static string CreateUniqueOutputDirectory(out string runId)
        {
            runId = DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                "-p" + System.Diagnostics.Process.GetCurrentProcess().Id +
                "-" + Guid.NewGuid().ToString("N");
            string outputPath = Path.Combine(OutputRoot, runId);
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static WorldCellIndex RequestedCell(
            IReadOnlyList<string> arguments, string fallback)
        {
            string requested = fallback;
            bool found = false;
            for (int index = 0; index < arguments.Count; index++)
            {
                if (!string.Equals(
                        arguments[index], "-vegetationNightAuditCell",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found || index + 1 >= arguments.Count ||
                    !arguments[index + 1].StartsWith(
                        "cell_", StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Use -vegetationNightAuditCell cell_X_Z exactly once.");
                requested = arguments[++index];
                found = true;
            }
            return MapVegetationRebuild.ParseCell(requested);
        }

        private static bool IsGeneratedGrassRoot(GameObject root)
        {
            if (root == null) return false;
            GeneratedVegetationGroup group =
                root.GetComponent<GeneratedVegetationGroup>();
            return group != null &&
                group.GeneratorId == MapVegetationRebuildOptions.GeneratorId &&
                group.Category ==
                MapVegetationCategories.GrassCoverage.ToString();
        }

        private static Vector3 HorizontalBoundsCenter(
            IReadOnlyList<Vector3> positions)
        {
            Vector3 minimum = positions[0];
            Vector3 maximum = positions[0];
            for (int index = 1; index < positions.Count; index++)
            {
                minimum = Vector3.Min(minimum, positions[index]);
                maximum = Vector3.Max(maximum, positions[index]);
            }
            Vector3 center = (minimum + maximum) * 0.5f;
            center.y = positions[0].y;
            return center;
        }

        private static Camera CreateCamera(List<Object> temporary)
        {
            var owner = new GameObject("Night emission audit camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(owner);
            Camera camera = owner.AddComponent<Camera>();
            HDAdditionalCameraData hdCamera =
                owner.AddComponent<HDAdditionalCameraData>();
            hdCamera.clearColorMode =
                HDAdditionalCameraData.ClearColorMode.Color;
            hdCamera.backgroundColorHDR = Color.black;
            hdCamera.antialiasing =
                HDAdditionalCameraData.AntialiasingMode.None;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.aspect = Width / (float)Height;
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            return camera;
        }

        private static LightingRig CreateBlackEnvironment(
            List<Object> temporary)
        {
            var volumeOwner = new GameObject(
                "Night emission audit black environment")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(volumeOwner);
            Volume volume = volumeOwner.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000000f;
            VolumeProfile profile = ScriptableObject
                .CreateInstance<VolumeProfile>();
            temporary.Add(profile);
            VisualEnvironment environment = profile.Add<VisualEnvironment>();
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            GradientSky sky = profile.Add<GradientSky>();
            sky.top.Override(Color.black);
            sky.middle.Override(Color.black);
            sky.bottom.Override(Color.black);
            sky.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            sky.exposure.Override(0f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            Exposure exposure = profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(FixedExposureEv100);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            Fog fog = profile.Add<Fog>();
            fog.enabled.Override(false);
            IndirectLightingController indirect =
                profile.Add<IndirectLightingController>();
            indirect.indirectDiffuseLightingMultiplier.Override(0f);
            indirect.reflectionLightingMultiplier.Override(0f);
            indirect.reflectionProbeIntensityMultiplier.Override(0f);
            volume.sharedProfile = profile;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;

            var sunOwner = new GameObject("Night emission audit control sun")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(sunOwner);
            Light directional = sunOwner.AddComponent<Light>();
            directional.type = LightType.Directional;
            HDAdditionalLightData hdDirectional =
                sunOwner.AddComponent<HDAdditionalLightData>();
            directional.lightUnit = LightUnit.Lux;
            directional.color = Color.white;
            directional.shadows = LightShadows.None;
            directional.bounceIntensity = 0f;
            sunOwner.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            RenderSettings.sun = directional;

            var punctualOwner = new GameObject(
                "Night emission audit headlight-like spot")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporary.Add(punctualOwner);
            Light punctual = punctualOwner.AddComponent<Light>();
            punctual.type = LightType.Spot;
            HDAdditionalLightData hdPunctual =
                punctualOwner.AddComponent<HDAdditionalLightData>();
            punctual.lightUnit = LightUnit.Candela;
            punctual.color = Color.white;
            punctual.range = PunctualControlRangeMeters;
            punctual.innerSpotAngle =
                PunctualControlInnerAngleDegrees;
            punctual.spotAngle = PunctualControlOuterAngleDegrees;
            punctual.enableSpotReflector = true;
            punctual.shadows = LightShadows.None;
            punctual.bounceIntensity = 0f;
            hdPunctual.affectsVolumetric = false;
            hdPunctual.volumetricDimmer = 0f;
            hdPunctual.volumetricShadowDimmer = 0f;

            var lighting = new LightingRig(
                directional, hdDirectional, punctual, hdPunctual);
            SetControlLighting(lighting, ControlLighting.None);
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline)
                pipeline.RequestSkyEnvironmentUpdate();
            return lighting;
        }

        private static void ConfigurePunctualControl(
            LightingRig lighting, Transform cameraTransform, Vector3 look)
        {
            Transform source = lighting.Punctual.transform;
            source.position = cameraTransform.position +
                cameraTransform.right * 0.18f -
                cameraTransform.up * 0.12f +
                cameraTransform.forward * 0.05f;
            Vector3 direction = look - source.position;
            source.rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : cameraTransform.rotation;
            lighting.PunctualAdditional.UpdateAllLightValues();
        }

        private static void SetControlLighting(
            LightingRig lighting, ControlLighting enabledControl)
        {
            bool directionalEnabled =
                enabledControl == ControlLighting.Directional;
            lighting.Directional.enabled = directionalEnabled;
            lighting.Directional.intensity = directionalEnabled
                ? ControlSunLux
                : 0f;
            lighting.DirectionalAdditional.UpdateAllLightValues();

            bool punctualEnabled =
                enabledControl == ControlLighting.Punctual;
            lighting.Punctual.enabled = punctualEnabled;
            lighting.Punctual.intensity = punctualEnabled
                ? PunctualControlCandela
                : 0f;
            lighting.PunctualAdditional.UpdateAllLightValues();
        }

        private static FrameMetrics CaptureFrame(
            Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            camera.Render();
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());

            Color32[] data = pixels.GetPixels32();
            double luminanceSum = 0d;
            float maximum = 0f;
            int bright = 0;
            for (int index = 0; index < data.Length; index++)
            {
                Color32 color = data[index];
                float luminance =
                    (0.2126f * color.r + 0.7152f * color.g +
                     0.0722f * color.b) / 255f;
                luminanceSum += luminance;
                maximum = Mathf.Max(maximum, luminance);
                if (luminance >= BrightPixelThreshold) bright++;
            }
            return new FrameMetrics
            {
                file = path,
                meanLuminance = (float)(luminanceSum / data.Length),
                maximumLuminance = maximum,
                brightPixelFraction = bright / (float)data.Length,
                brightPixelCount = bright,
                pixelCount = data.Length
            };
        }

        private static void WriteReport(
            string outputPath, NightEmissionReport report)
        {
            Directory.CreateDirectory(outputPath);
            File.WriteAllText(
                Path.Combine(outputPath, "night-emission-report.json"),
                JsonUtility.ToJson(report, true));
        }

        private enum ControlLighting
        {
            None,
            Directional,
            Punctual
        }

        private readonly struct LightingRig
        {
            public readonly Light Directional;
            public readonly HDAdditionalLightData DirectionalAdditional;
            public readonly Light Punctual;
            public readonly HDAdditionalLightData PunctualAdditional;

            public LightingRig(
                Light directional,
                HDAdditionalLightData directionalAdditional,
                Light punctual,
                HDAdditionalLightData punctualAdditional)
            {
                Directional = directional;
                DirectionalAdditional = directionalAdditional;
                Punctual = punctual;
                PunctualAdditional = punctualAdditional;
            }
        }

        private readonly struct ControlGate
        {
            public readonly bool Visible;
            public readonly bool ZeroRadianceSuppressed;
            public readonly float MeanLuminanceRatio;
            public readonly float BrightPixelFractionRatio;

            public ControlGate(
                bool visible, bool zeroRadianceSuppressed,
                float meanLuminanceRatio,
                float brightPixelFractionRatio)
            {
                Visible = visible;
                ZeroRadianceSuppressed = zeroRadianceSuppressed;
                MeanLuminanceRatio = meanLuminanceRatio;
                BrightPixelFractionRatio = brightPixelFractionRatio;
            }
        }

        internal readonly struct GateEvaluation
        {
            public readonly bool Passed;
            public readonly bool HasPunctualControl;
            public readonly bool ControlVisible;
            public readonly bool DirectionalControlVisible;
            public readonly bool PunctualControlVisible;
            public readonly bool AllControlsVisible;
            public readonly bool ZeroRadianceSuppressed;
            public readonly bool DirectionalZeroRadianceSuppressed;
            public readonly bool PunctualZeroRadianceSuppressed;
            public readonly float MeanLuminanceRatio;
            public readonly float BrightPixelFractionRatio;
            public readonly float PunctualMeanLuminanceRatio;
            public readonly float PunctualBrightPixelFractionRatio;
            public readonly string Diagnostic;

            public GateEvaluation(
                bool passed, bool hasPunctualControl,
                bool directionalControlVisible,
                bool punctualControlVisible,
                bool directionalZeroRadianceSuppressed,
                bool punctualZeroRadianceSuppressed,
                bool zeroRadianceSuppressed,
                float meanLuminanceRatio,
                float brightPixelFractionRatio,
                float punctualMeanLuminanceRatio,
                float punctualBrightPixelFractionRatio,
                string diagnostic)
            {
                Passed = passed;
                HasPunctualControl = hasPunctualControl;
                ControlVisible = directionalControlVisible;
                DirectionalControlVisible = directionalControlVisible;
                PunctualControlVisible = punctualControlVisible;
                AllControlsVisible = directionalControlVisible &&
                    (!hasPunctualControl || punctualControlVisible);
                ZeroRadianceSuppressed = zeroRadianceSuppressed;
                DirectionalZeroRadianceSuppressed =
                    directionalZeroRadianceSuppressed;
                PunctualZeroRadianceSuppressed =
                    punctualZeroRadianceSuppressed;
                MeanLuminanceRatio = meanLuminanceRatio;
                BrightPixelFractionRatio = brightPixelFractionRatio;
                PunctualMeanLuminanceRatio =
                    punctualMeanLuminanceRatio;
                PunctualBrightPixelFractionRatio =
                    punctualBrightPixelFractionRatio;
                Diagnostic = diagnostic;
            }
        }

        [Serializable]
        internal sealed class FrameMetrics
        {
            public string file;
            public float meanLuminance;
            public float maximumLuminance;
            public float brightPixelFraction;
            public int brightPixelCount;
            public int pixelCount;
        }

        [Serializable]
        private sealed class NightEmissionReport
        {
            public int schemaVersion;
            public int gateRevision;
            public string validatorId;
            public string runId;
            public string capturedUtc;
            public bool passed;
            public string cellId;
            public string generatedScene;
            public string generatedFingerprint;
            public string outputDirectory;
            public string bindingVersion;
            public string unityVersion;
            public string graphicsDevice;
            public float fixedExposureEv100;
            public float controlSunLux;
            public float zeroRadianceSunLux;
            public int savedGrassInstances;
            public int gpuBatchCount;
            public Vector3 selectionPosition;
            public Vector3 selectionLookDirection;
            public int selectionNearbyInstances;
            public int selectionForwardInstances;
            public float selectionNearestForwardInstanceMeters;
            public Vector3 cameraPosition;
            public Vector3 cameraRotationEuler;
            public float brightPixelThreshold;
            public float minimumControlMeanLuminance;
            public float minimumControlBrightPixelFraction;
            public float maximumZeroToControlMeanRatio;
            public float maximumZeroToControlBrightPixelRatio;
            public bool controlVisible;
            public bool zeroRadianceSuppressed;
            public float meanLuminanceRatio;
            public float brightPixelFractionRatio;
            public FrameMetrics control;
            public FrameMetrics zeroRadiance;
            public float punctualControlCandela;
            public float punctualControlRangeMeters;
            public float punctualControlInnerAngleDegrees;
            public float punctualControlOuterAngleDegrees;
            public float zeroRadiancePunctualCandela;
            public Vector3 punctualControlPosition;
            public Vector3 punctualControlRotationEuler;
            public bool directionalControlVisible;
            public bool punctualControlVisible;
            public bool allControlsVisible;
            public bool directionalZeroRadianceSuppressed;
            public bool punctualZeroRadianceSuppressed;
            public float punctualMeanLuminanceRatio;
            public float punctualBrightPixelFractionRatio;
            public FrameMetrics punctualControl;
            public List<AssetReference> catalogs = new List<AssetReference>();
            public List<GrassProfileReference> profiles =
                new List<GrassProfileReference>();
            public List<string> notes = new List<string>();
            public string[] limitations = Array.Empty<string>();
        }

        [Serializable]
        private sealed class AssetReference
        {
            public string asset;
            public string dependencyHash;
        }

        [Serializable]
        private sealed class GrassProfileReference
        {
            public int catalogProfileIndex;
            public string profileId;
            public string profileAsset;
            public string profileDependencyHash;
            public string materialAsset;
            public string materialDependencyHash;
            public string shader;
        }
    }
}
