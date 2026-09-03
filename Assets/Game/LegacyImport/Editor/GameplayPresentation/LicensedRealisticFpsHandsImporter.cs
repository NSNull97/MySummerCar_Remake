using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Imports the explicitly licensed Realistic FPS Hands package into the
    /// private generated presentation boundary. Package assets remain local;
    /// project-owned wrappers and provenance stay reviewable in source.
    /// </summary>
    public static class LicensedRealisticFpsHandsImporter
    {
        public const string PackageSha256 =
            "bd4b657ad2c2817c0ed83deca2ec4a00" +
            "fa16802e428ff2e79f4afeb6844f8a5b";

        public const int LicensedSourceAssetCount = 5;

        private const string LocalConfigurationPath =
            "Config/PresentationAssets.local.json";

        private const string SourceRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/LicensedRealisticFpsHands/Source";

        public const string ModelAssetPath =
            SourceRoot + "/RealisticFPSHands.fbx";

        private const string AlbedoAssetPath =
            SourceRoot + "/RealisticFPSHands_Albedo.png";

        private const string AmbientOcclusionAssetPath =
            SourceRoot + "/RealisticFPSHands_AO.png";

        private const string NormalAssetPath =
            SourceRoot + "/RealisticFPSHands_Normal.png";

        private const string SpecularSmoothnessAssetPath =
            SourceRoot + "/RealisticFPSHands_SpecularSmoothness.png";

        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel/Generated/" +
            "LicensedRealisticFpsHands";

        private const string MaskMapAssetPath =
            GeneratedRoot + "/RealisticFPSHands_MaskMap.png";

        private const string MaterialAssetPath =
            GeneratedRoot + "/RealisticFPSHands_HDRP.mat";

        private const string RightArmMeshAssetPath =
            GeneratedRoot + "/RealisticFPSHands_RightArm.asset";

        private const string DrinkClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_Drink.anim";

        private const string DrinkShortClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_DrinkShort.anim";

        private const string DrinkThrowClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_DrinkThrow.anim";

        private const string DrinkSprayClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_DrinkSpray.anim";

        private const string WaveClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_Wave.anim";

        private const string MiddleFingerClipAssetPath =
            GeneratedRoot + "/RealisticFPSHands_MiddleFinger.anim";

        private const float ImportedModelScale = 10f;

        private static readonly Regex MetaGuidPattern = new Regex(
            @"^guid:\s*(?<guid>[0-9a-fA-F]+)\s*$",
            RegexOptions.Compiled |
            RegexOptions.Multiline |
            RegexOptions.CultureInvariant);

        private static readonly LicensedSourceSpec[] SourceSpecs =
        {
            new LicensedSourceSpec(
                "model",
                "d0c31c107e334b244a499ccd14c403bf",
                ModelAssetPath,
                "128b8ed8cbe188bbb995041c00e6dacf" +
                "4e24383ad17aa7822bdbb1deaea8746e"),
            new LicensedSourceSpec(
                "albedo",
                "8013ae9b9a3baf84ab328f810d28e034",
                AlbedoAssetPath,
                "9c49e9461ddc7f1650a3ad79bf861eb7" +
                "d7e49899458c190d202d3bbb497d304a"),
            new LicensedSourceSpec(
                "ambient-occlusion",
                "a4cf0204f1684fd4091c8abfa7002e08",
                AmbientOcclusionAssetPath,
                "70140a3509b22ecf21bcfa36da41ea84" +
                "88b2c5e68eae311bd258efcaca4cd3ac"),
            new LicensedSourceSpec(
                "normal",
                "c210965e7bc2c7940bb13961e5ae048b",
                NormalAssetPath,
                "0f5c676f03e21067b6426d056e2a3165" +
                "0e8e83b3d14872cf2deb28dd1e3c69e1"),
            new LicensedSourceSpec(
                "specular-smoothness",
                "bdc72f6cbb9d3d54c8902a5402e02cf1",
                SpecularSmoothnessAssetPath,
                "ee3211234c7e764abaef7857362a0fb81" +
                "856ce5b6c9c0b7ac6d2d463a86306e2"),
        };

        public static IReadOnlyList<string> HashedAssetPaths { get; } =
            SourceSpecs.Select(spec => spec.AssetPath)
                .Concat(new[]
                {
                    MaskMapAssetPath,
                    MaterialAssetPath,
                    RightArmMeshAssetPath,
                    DrinkClipAssetPath,
                    DrinkShortClipAssetPath,
                    DrinkThrowClipAssetPath,
                    DrinkSprayClipAssetPath,
                    WaveClipAssetPath,
                    MiddleFingerClipAssetPath,
                })
                .ToArray();

        [MenuItem(
            "Tools/My Summer Car/Presentation/" +
            "Install Licensed Realistic FPS Hands")]
        public static void InstallFromMenu()
        {
            string packagePath = EditorUtility.OpenFilePanel(
                "Select Realistic FPS Hands v1.0.unitypackage",
                string.Empty,
                "unitypackage");
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return;
            }

            InstallFromPackage(packagePath);
            WriteLocalConfiguration(packagePath);
            EditorUtility.DisplayDialog(
                "Realistic FPS Hands",
                "The licensed hand source was installed into the private " +
                "local presentation boundary. Rebuild the Phase 1 player " +
                "viewmodel to apply it.",
                "OK");
        }

        public static void InstallFromConfiguredPackageForBatch()
        {
            PresentationAssetConfiguration configuration =
                LoadLocalConfiguration();
            InstallFromPackage(configuration.realisticFpsHandsUnityPackage);
        }

        internal static LicensedHandsBuildResult BuildViewmodelActions(
            Transform prefabRoot,
            DonorViewmodelMotionSet donorMotions)
        {
            if (prefabRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabRoot));
            }

            if (donorMotions == null)
            {
                throw new ArgumentNullException(nameof(donorMotions));
            }

            EnsureSourceInstalled();
            ConfigureSourceImporters();
            EnsureAssetFolder(GeneratedRoot);
            Texture2D maskMap = CreateMaskMap();
            Material material = CreateHandMaterial(maskMap);
            GameObject model = RequireAsset<GameObject>(ModelAssetPath);
            Mesh rightArmMesh = CreateRightArmMesh(model);

            LicensedAction drink = CreateAction(
                "Drink",
                prefabRoot,
                model,
                material,
                rightArmMesh);
            LicensedDrinkClips drinkClips = BuildDrinkClips(
                drink,
                donorMotions);

            LicensedAction wave = CreateAction(
                "Hello",
                prefabRoot,
                model,
                material,
                rightArmMesh);
            AnimationClip waveClip = BuildWaveClip(
                wave,
                donorMotions.WaveClip);

            LicensedAction middleFinger = CreateAction(
                "MiddleFinger",
                prefabRoot,
                model,
                material,
                rightArmMesh);
            AnimationClip middleFingerClip =
                BuildMiddleFingerClip(
                    middleFinger,
                    donorMotions.MiddleFingerClip);

            drink.Root.SetActive(false);
            wave.Root.SetActive(false);
            middleFinger.Root.SetActive(false);
            return new LicensedHandsBuildResult(
                drink,
                drinkClips,
                wave,
                waveClip,
                middleFinger,
                middleFingerClip);
        }

        public static void ValidateGeneratedSourceAndAssets()
        {
            ValidateInstalledSource();
            foreach (string assetPath in HashedAssetPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Licensed FPS-hands generated asset is missing: " +
                        $"'{assetPath}'. Rebuild the Phase 1 player viewmodel.");
                }
            }
        }

        public static Mesh RequireImportedHandMesh()
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath)
                .OfType<Mesh>()
                .Single(mesh => string.Equals(
                    mesh.name,
                    "FPS Hands",
                    StringComparison.Ordinal));
        }

        internal static void WriteDonorMotionAudit(
            DonorViewmodelMotionSet motions)
        {
            if (motions == null)
            {
                throw new ArgumentNullException(nameof(motions));
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            string outputPath = Path.Combine(
                projectRoot,
                "Logs",
                "OriginalViewmodelMotionAudit.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ??
                throw new InvalidOperationException(
                    "Original viewmodel audit folder is unavailable."));

            var report = new StringBuilder(32768);
            report.AppendLine("Original My Summer Car viewmodel motion audit");
            report.AppendLine(
                "Read-only donor presentation sampled before licensed-rig " +
                "replacement.");
            AppendDonorActionAudit(
                report,
                "Drink",
                motions.Drink.Root,
                motions.Drink.AnimationTarget,
                motions.Drink.Grip,
                new[]
                {
                    new DonorClipAudit(
                        motions.DrinkClip,
                        0f,
                        0.5f,
                        1f,
                        2f,
                        3f,
                        4f,
                        motions.DrinkClip.length),
                    new DonorClipAudit(
                        motions.DrinkShortClip,
                        0f,
                        motions.DrinkShortClip.length * 0.5f,
                        motions.DrinkShortClip.length),
                    new DonorClipAudit(
                        motions.DrinkSprayClip,
                        0f,
                        motions.DrinkSprayClip.length * 0.5f,
                        motions.DrinkSprayClip.length),
                    new DonorClipAudit(
                        motions.DrinkThrowClip,
                        0f,
                        motions.DrinkThrowClip.length * 0.5f,
                        motions.DrinkThrowClip.length),
                });
            AppendDonorActionAudit(
                report,
                "Hello",
                motions.Wave.Root,
                motions.Wave.AnimationTarget,
                null,
                new[]
                {
                    new DonorClipAudit(
                        motions.WaveClip,
                        0f,
                        motions.WaveClip.length * 0.25f,
                        motions.WaveClip.length * 0.5f,
                        motions.WaveClip.length * 0.75f,
                        motions.WaveClip.length),
                });
            AppendDonorActionAudit(
                report,
                "MiddleFinger",
                motions.MiddleFinger.Root,
                motions.MiddleFinger.AnimationTarget,
                null,
                new[]
                {
                    new DonorClipAudit(
                        motions.MiddleFingerClip,
                        0f,
                        motions.MiddleFingerClip.length * 0.25f,
                        motions.MiddleFingerClip.length * 0.5f,
                        motions.MiddleFingerClip.length * 0.75f,
                        motions.MiddleFingerClip.length),
                });
            File.WriteAllText(
                outputPath,
                report.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void AppendDonorActionAudit(
            StringBuilder report,
            string label,
            GameObject actionRoot,
            Transform animationTarget,
            Transform grip,
            IReadOnlyList<DonorClipAudit> clips)
        {
            report.AppendLine();
            report.AppendLine($"=== {label} ===");
            report.AppendLine(
                $"root={GetHierarchyPath(actionRoot.transform, actionRoot.transform)} " +
                $"animationTarget={GetHierarchyPath(actionRoot.transform, animationTarget)} " +
                $"grip={(grip == null ? "<none>" : GetHierarchyPath(actionRoot.transform, grip))}");

            Transform[] hierarchy = actionRoot
                .GetComponentsInChildren<Transform>(true)
                .OrderBy(transform =>
                    GetHierarchyPath(actionRoot.transform, transform),
                    StringComparer.Ordinal)
                .ToArray();
            report.AppendLine("Hierarchy:");
            foreach (Transform transform in hierarchy)
            {
                report.AppendLine(
                    $"  {GetHierarchyPath(actionRoot.transform, transform)} | " +
                    $"localP={Format(transform.localPosition)} | " +
                    $"localR={Format(transform.localRotation)} | " +
                    $"worldP={Format(transform.position)}");
            }

            SkinnedMeshRenderer[] renderers = actionRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                report.AppendLine(
                    $"Renderer[{rendererIndex}] mesh={renderer.sharedMesh?.name} " +
                    $"rootBone={(renderer.rootBone == null ? "<none>" : GetHierarchyPath(actionRoot.transform, renderer.rootBone))}");
                for (int boneIndex = 0;
                     boneIndex < renderer.bones.Length;
                     boneIndex++)
                {
                    Transform bone = renderer.bones[boneIndex];
                    report.AppendLine(
                        $"  bone[{boneIndex}]={GetHierarchyPath(actionRoot.transform, bone)} " +
                        $"worldP={Format(bone.position)}");
                }
            }

            var rest = hierarchy.Select(transform =>
                    new DonorTransformPose(
                        transform,
                        transform.localPosition,
                        transform.localRotation,
                        transform.localScale))
                .ToArray();
            foreach (DonorClipAudit clipAudit in clips)
            {
                report.AppendLine(
                    $"Clip '{clipAudit.Clip.name}' length={clipAudit.Clip.length.ToString("0.0000", CultureInfo.InvariantCulture)}");
                foreach (float requestedTime in clipAudit.Times)
                {
                    foreach (DonorTransformPose pose in rest)
                    {
                        pose.Restore();
                    }

                    float time = Mathf.Clamp(
                        requestedTime,
                        0f,
                        clipAudit.Clip.length);
                    clipAudit.Clip.SampleAnimation(
                        animationTarget.gameObject,
                        time);
                    report.AppendLine(
                        $"  t={time.ToString("0.0000", CultureInfo.InvariantCulture)} " +
                        $"targetP={Format(animationTarget.position)} " +
                        $"targetR={Format(animationTarget.rotation)}" +
                        (grip == null
                            ? string.Empty
                            : $" gripP={Format(grip.position)} gripR={Format(grip.rotation)}"));
                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        report.AppendLine(
                            $"    boundsCenter={Format(renderer.bounds.center)} " +
                            $"boundsSize={Format(renderer.bounds.size)}");
                        for (int boneIndex = 0;
                             boneIndex < renderer.bones.Length;
                             boneIndex++)
                        {
                            Transform bone = renderer.bones[boneIndex];
                            report.AppendLine(
                                $"    bone[{boneIndex}]={bone.name} " +
                                $"P={Format(bone.position)} R={Format(bone.rotation)}");
                        }
                    }
                }
            }

            foreach (DonorTransformPose pose in rest)
            {
                pose.Restore();
            }
        }

        private static string GetHierarchyPath(
            Transform root,
            Transform target)
        {
            if (target == root)
            {
                return "<root>";
            }

            var parts = new Stack<string>();
            Transform cursor = target;
            while (cursor != null && cursor != root)
            {
                parts.Push(cursor.name);
                cursor = cursor.parent;
            }

            return cursor == root
                ? string.Join("/", parts)
                : "<outside>/" + target.name;
        }

        private static string Format(Quaternion value) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.00000},{1:0.00000},{2:0.00000},{3:0.00000})",
                value.x,
                value.y,
                value.z,
                value.w);

        private static void EnsureSourceInstalled()
        {
            try
            {
                ValidateInstalledSource();
            }
            catch (Exception sourceException)
            {
                if (!File.Exists(ToFileSystemPath(LocalConfigurationPath)))
                {
                    throw new InvalidOperationException(
                        "Licensed Realistic FPS Hands source is unavailable " +
                        "or changed, and Config/PresentationAssets.local.json " +
                        "does not provide the purchased .unitypackage path. " +
                        "Use 'Tools > My Summer Car > Presentation > Install " +
                        "Licensed Realistic FPS Hands'.",
                        sourceException);
                }

                PresentationAssetConfiguration configuration =
                    LoadLocalConfiguration();
                InstallFromPackage(
                    configuration.realisticFpsHandsUnityPackage);
            }
        }

        private static void InstallFromPackage(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new InvalidOperationException(
                    "Realistic FPS Hands package path is empty.");
            }

            string resolvedPackagePath = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(packagePath.Trim()));
            if (!File.Exists(resolvedPackagePath))
            {
                throw new FileNotFoundException(
                    "Realistic FPS Hands package was not found.",
                    resolvedPackagePath);
            }

            string packageHash = ComputeSha256(resolvedPackagePath);
            if (!string.Equals(
                    packageHash,
                    PackageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Realistic FPS Hands package hash does not match the " +
                    $"reviewed v1.0 package. Expected {PackageSha256}, got " +
                    $"{packageHash}.");
            }

            string temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "MSC_RealisticFpsHands_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                ExtractUnityPackage(resolvedPackagePath, temporaryRoot);
                ResetSourceBoundary();
                string destinationRoot = ToFileSystemPath(SourceRoot);
                Directory.CreateDirectory(destinationRoot);
                foreach (LicensedSourceSpec spec in SourceSpecs)
                {
                    string packageAssetRoot = Path.Combine(
                        temporaryRoot,
                        spec.PackageGuid);
                    string sourceAsset = Path.Combine(
                        packageAssetRoot,
                        "asset");
                    string sourceMeta = Path.Combine(
                        packageAssetRoot,
                        "asset.meta");
                    if (!File.Exists(sourceAsset) ||
                        !File.Exists(sourceMeta))
                    {
                        throw new InvalidOperationException(
                            $"Reviewed package asset '{spec.Role}' " +
                            $"({spec.PackageGuid}) is missing.");
                    }

                    string destinationAsset =
                        ToFileSystemPath(spec.AssetPath);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destinationAsset) ??
                        destinationRoot);
                    File.Copy(sourceAsset, destinationAsset, overwrite: true);
                    File.Copy(
                        sourceMeta,
                        destinationAsset + ".meta",
                        overwrite: true);
                }

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                ValidateInstalledSource();
                ConfigureSourceImporters();
            }
            finally
            {
                string resolvedTemporaryRoot =
                    Path.GetFullPath(temporaryRoot);
                string allowedTemporaryRoot =
                    Path.GetFullPath(Path.GetTempPath());
                if (resolvedTemporaryRoot.StartsWith(
                        allowedTemporaryRoot,
                        StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(resolvedTemporaryRoot).StartsWith(
                        "MSC_RealisticFpsHands_",
                        StringComparison.Ordinal) &&
                    Directory.Exists(resolvedTemporaryRoot))
                {
                    Directory.Delete(resolvedTemporaryRoot, recursive: true);
                }
            }
        }

        private static void ExtractUnityPackage(
            string packagePath,
            string outputDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar.exe",
                Arguments =
                    "-xf " + QuoteProcessArgument(packagePath) +
                    " -C " + QuoteProcessArgument(outputDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException(
                    "Could not start tar.exe for Unity package extraction.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Could not extract the licensed Unity package. " +
                    $"tar.exe exited with {process.ExitCode}. " +
                    standardOutput + standardError);
            }
        }

        private static string QuoteProcessArgument(string value) =>
            "\"" + value.Replace("\"", "\\\"") + "\"";

        private static void ResetSourceBoundary()
        {
            string resolvedSource = ToFileSystemPath(SourceRoot);
            string allowedRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Game",
                "LegacyImport",
                "RuntimeBaseline",
                "GameplayPresentation")) +
                Path.DirectorySeparatorChar;
            if (!resolvedSource.StartsWith(
                    allowedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Refusing to reset licensed source outside the private " +
                    "gameplay-presentation boundary.");
            }

            if (AssetDatabase.IsValidFolder(SourceRoot) &&
                !AssetDatabase.DeleteAsset(SourceRoot))
            {
                throw new IOException(
                    $"Could not reset licensed source '{SourceRoot}'.");
            }

            if (Directory.Exists(resolvedSource))
            {
                Directory.Delete(resolvedSource, recursive: true);
            }

            string metaPath = resolvedSource + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static void ValidateInstalledSource()
        {
            foreach (LicensedSourceSpec spec in SourceSpecs)
            {
                string filePath = ToFileSystemPath(spec.AssetPath);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(
                        $"Licensed FPS-hands source '{spec.Role}' is missing.",
                        filePath);
                }

                string actualHash = ComputeSha256(filePath);
                if (!string.Equals(
                        actualHash,
                        spec.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Licensed FPS-hands source '{spec.Role}' hash " +
                        $"mismatch. Expected {spec.Sha256}, got {actualHash}.");
                }

                string metaPath = filePath + ".meta";
                if (!File.Exists(metaPath))
                {
                    throw new FileNotFoundException(
                        $"Licensed FPS-hands meta '{spec.Role}' is missing.",
                        metaPath);
                }

                Match match = MetaGuidPattern.Match(
                    File.ReadAllText(metaPath));
                if (!match.Success ||
                    !string.Equals(
                        match.Groups["guid"].Value,
                        spec.PackageGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Licensed FPS-hands GUID mismatch for '{spec.Role}'.");
                }
            }
        }

        private static void ConfigureSourceImporters()
        {
            if (!(AssetImporter.GetAtPath(ModelAssetPath) is
                    ModelImporter modelImporter))
            {
                throw new InvalidOperationException(
                    "Licensed FPS-hands model importer is unavailable.");
            }

            modelImporter.animationType = ModelImporterAnimationType.Generic;
            modelImporter.importAnimation = false;
            modelImporter.materialImportMode =
                ModelImporterMaterialImportMode.None;
            modelImporter.globalScale = ImportedModelScale;
            modelImporter.isReadable = false;
            modelImporter.importBlendShapes = true;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.addCollider = false;
            modelImporter.meshCompression = ModelImporterMeshCompression.Off;
            modelImporter.optimizeGameObjects = false;
            modelImporter.SaveAndReimport();

            ConfigureTexture(
                AlbedoAssetPath,
                TextureImporterType.Default,
                sRgb: true,
                preserveAlpha: false,
                readable: false);
            ConfigureTexture(
                NormalAssetPath,
                TextureImporterType.NormalMap,
                sRgb: false,
                preserveAlpha: false,
                readable: false);
            ConfigureTexture(
                AmbientOcclusionAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                preserveAlpha: false,
                readable: true);
            ConfigureTexture(
                SpecularSmoothnessAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                preserveAlpha: true,
                readable: true);
        }

        private static void ConfigureTexture(
            string assetPath,
            TextureImporterType textureType,
            bool sRgb,
            bool preserveAlpha,
            bool readable)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is
                    TextureImporter importer))
            {
                throw new InvalidOperationException(
                    $"Texture importer is unavailable for '{assetPath}'.");
            }

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = preserveAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static Texture2D CreateMaskMap()
        {
            Texture2D ao = RequireAsset<Texture2D>(
                AmbientOcclusionAssetPath);
            Texture2D specularSmoothness = RequireAsset<Texture2D>(
                SpecularSmoothnessAssetPath);
            if (ao.width != specularSmoothness.width ||
                ao.height != specularSmoothness.height)
            {
                throw new InvalidOperationException(
                    "Licensed hand AO and smoothness dimensions differ.");
            }

            Color32[] aoPixels = ao.GetPixels32();
            Color32[] specularPixels = specularSmoothness.GetPixels32();
            var maskPixels = new Color32[aoPixels.Length];
            for (int index = 0; index < maskPixels.Length; index++)
            {
                maskPixels[index] = new Color32(
                    0,
                    aoPixels[index].r,
                    byte.MaxValue,
                    specularPixels[index].a);
            }

            var mask = new Texture2D(
                ao.width,
                ao.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "Realistic FPS Hands HDRP Mask Map",
            };
            try
            {
                mask.SetPixels32(maskPixels);
                mask.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(
                    ToFileSystemPath(MaskMapAssetPath),
                    mask.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mask);
            }

            AssetDatabase.ImportAsset(
                MaskMapAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ConfigureTexture(
                MaskMapAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                preserveAlpha: true,
                readable: false);
            ConfigureTexture(
                AmbientOcclusionAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                preserveAlpha: false,
                readable: false);
            ConfigureTexture(
                SpecularSmoothnessAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                preserveAlpha: true,
                readable: false);
            return RequireAsset<Texture2D>(MaskMapAssetPath);
        }

        private static Material CreateHandMaterial(Texture2D maskMap)
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable for licensed hands.");
            var material = new Material(shader)
            {
                name = "Licensed Realistic FPS Hands HDRP",
                enableInstancing = true,
            };
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(AlbedoAssetPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(NormalAssetPath));
            material.SetFloat("_NormalScale", 0.85f);
            material.SetTexture("_MaskMap", maskMap);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.55f);
            material.SetFloat("_SmoothnessRemapMin", 0f);
            material.SetFloat("_SmoothnessRemapMax", 0.6f);
            material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            material.EnableKeyword("_MASKMAP");
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
            return material;
        }

        private static Mesh CreateRightArmMesh(GameObject sourceModel)
        {
            GameObject instance = UnityEngine.Object.Instantiate(sourceModel);
            try
            {
                SkinnedMeshRenderer renderer = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(candidate =>
                        candidate.sharedMesh != null &&
                        string.Equals(
                            candidate.sharedMesh.name,
                            "FPS Hands",
                            StringComparison.Ordinal));
                Mesh source = renderer.sharedMesh;
                BoneWeight[] weights = source.boneWeights;
                if (weights == null || weights.Length != source.vertexCount)
                {
                    throw new InvalidOperationException(
                        "Licensed FPS-hands mesh has no compatible skin " +
                        "weights for the right-arm extraction.");
                }

                var rightBones = new HashSet<int>();
                var viewmodelBones = new HashSet<int>();
                var leftBones = new HashSet<int>();
                for (int index = 0; index < renderer.bones.Length; index++)
                {
                    Transform bone = renderer.bones[index];
                    if (bone == null)
                    {
                        continue;
                    }

                    if (bone.name.StartsWith(
                            "CATRigRArm",
                            StringComparison.Ordinal))
                    {
                        rightBones.Add(index);
                        if (string.Equals(
                                bone.name,
                                "CATRigRArm21",
                                StringComparison.Ordinal) ||
                            string.Equals(
                                bone.name,
                                "CATRigRArm22",
                                StringComparison.Ordinal) ||
                            string.Equals(
                                bone.name,
                                "CATRigRArmPalm",
                                StringComparison.Ordinal) ||
                            bone.name.StartsWith(
                                "CATRigRArmDigit",
                                StringComparison.Ordinal))
                        {
                            viewmodelBones.Add(index);
                        }
                    }
                    else if (bone.name.StartsWith(
                                 "CATRigLArm",
                                 StringComparison.Ordinal))
                    {
                        leftBones.Add(index);
                    }
                }

                if (rightBones.Count == 0 ||
                    viewmodelBones.Count == 0 ||
                    leftBones.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Licensed FPS-hands arm bone classification failed.");
                }

                float[] rightInfluence = new float[weights.Length];
                float[] viewmodelInfluence = new float[weights.Length];
                float[] leftInfluence = new float[weights.Length];
                for (int vertex = 0; vertex < weights.Length; vertex++)
                {
                    BoneWeight weight = weights[vertex];
                    AccumulateArmWeight(
                        weight.boneIndex0,
                        weight.weight0,
                        rightBones,
                        leftBones,
                        ref rightInfluence[vertex],
                        ref leftInfluence[vertex]);
                    AccumulateSelectedWeight(
                        weight.boneIndex0,
                        weight.weight0,
                        viewmodelBones,
                        ref viewmodelInfluence[vertex]);
                    AccumulateArmWeight(
                        weight.boneIndex1,
                        weight.weight1,
                        rightBones,
                        leftBones,
                        ref rightInfluence[vertex],
                        ref leftInfluence[vertex]);
                    AccumulateSelectedWeight(
                        weight.boneIndex1,
                        weight.weight1,
                        viewmodelBones,
                        ref viewmodelInfluence[vertex]);
                    AccumulateArmWeight(
                        weight.boneIndex2,
                        weight.weight2,
                        rightBones,
                        leftBones,
                        ref rightInfluence[vertex],
                        ref leftInfluence[vertex]);
                    AccumulateSelectedWeight(
                        weight.boneIndex2,
                        weight.weight2,
                        viewmodelBones,
                        ref viewmodelInfluence[vertex]);
                    AccumulateArmWeight(
                        weight.boneIndex3,
                        weight.weight3,
                        rightBones,
                        leftBones,
                        ref rightInfluence[vertex],
                        ref leftInfluence[vertex]);
                    AccumulateSelectedWeight(
                        weight.boneIndex3,
                        weight.weight3,
                        viewmodelBones,
                        ref viewmodelInfluence[vertex]);
                }

                Mesh rightArm = UnityEngine.Object.Instantiate(source);
                rightArm.name = "Realistic FPS Hands Right Arm";
                int retainedTriangles = 0;
                for (int subMesh = 0;
                     subMesh < source.subMeshCount;
                     subMesh++)
                {
                    int[] sourceTriangles = source.GetTriangles(subMesh);
                    var triangles = new List<int>(sourceTriangles.Length / 2);
                    for (int triangle = 0;
                         triangle < sourceTriangles.Length;
                         triangle += 3)
                    {
                        int a = sourceTriangles[triangle];
                        int b = sourceTriangles[triangle + 1];
                        int c = sourceTriangles[triangle + 2];
                        float right = rightInfluence[a] +
                                      rightInfluence[b] +
                                      rightInfluence[c];
                        float viewmodel = viewmodelInfluence[a] +
                                          viewmodelInfluence[b] +
                                          viewmodelInfluence[c];
                        float upperArm = Mathf.Max(
                            0f,
                            right - viewmodel);
                        float left = leftInfluence[a] +
                                     leftInfluence[b] +
                                     leftInfluence[c];
                        if (viewmodel <= 0.01f ||
                            viewmodel <= upperArm ||
                            viewmodel <= left)
                        {
                            continue;
                        }

                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        retainedTriangles++;
                    }

                    rightArm.SetTriangles(
                        triangles,
                        subMesh,
                        calculateBounds: false);
                }

                if (retainedTriangles == 0)
                {
                    UnityEngine.Object.DestroyImmediate(rightArm);
                    throw new InvalidOperationException(
                        "Licensed FPS-hands right-arm extraction produced " +
                        "no triangles.");
                }

                rightArm.RecalculateBounds();
                AssetDatabase.CreateAsset(rightArm, RightArmMeshAssetPath);
                return rightArm;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AccumulateArmWeight(
            int boneIndex,
            float weight,
            HashSet<int> rightBones,
            HashSet<int> leftBones,
            ref float right,
            ref float left)
        {
            if (rightBones.Contains(boneIndex))
            {
                right += weight;
            }
            else if (leftBones.Contains(boneIndex))
            {
                left += weight;
            }
        }

        private static void AccumulateSelectedWeight(
            int boneIndex,
            float weight,
            HashSet<int> selectedBones,
            ref float selected)
        {
            if (selectedBones.Contains(boneIndex))
            {
                selected += weight;
            }
        }

        private static LicensedAction CreateAction(
            string name,
            Transform prefabRoot,
            GameObject sourceModel,
            Material material,
            Mesh rightArmMesh)
        {
            var actionRoot = new GameObject(name);
            actionRoot.transform.SetParent(prefabRoot, false);
            GameObject model = UnityEngine.Object.Instantiate(sourceModel);
            model.name = "Licensed Realistic FPS Hands";
            model.transform.SetParent(actionRoot.transform, false);

            foreach (Animator animator in
                     model.GetComponentsInChildren<Animator>(true))
            {
                UnityEngine.Object.DestroyImmediate(animator);
            }

            SkinnedMeshRenderer[] renderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer primary = renderers.Single(renderer =>
                renderer.sharedMesh != null &&
                string.Equals(
                    renderer.sharedMesh.name,
                    "FPS Hands",
                    StringComparison.Ordinal));
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == primary)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            }

            primary.sharedMaterial = material;
            primary.sharedMesh = rightArmMesh;
            primary.shadowCastingMode = ShadowCastingMode.On;
            primary.receiveShadows = true;
            primary.updateWhenOffscreen = false;
            // The viewmodel is camera-local presentation. Object motion
            // vectors make quick gestures look like several translucent
            // fingers in HDRP motion blur/TAA.
            primary.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            Animation animation = actionRoot.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.animatePhysics = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            return new LicensedAction(
                actionRoot,
                animation,
                new LicensedHandsRig(actionRoot.transform, model.transform),
                primary);
        }

        private const float AuthoredFrameRate = 30f;

        private static LicensedDrinkClips BuildDrinkClips(
            LicensedAction action,
            DonorViewmodelMotionSet motions)
        {
            LicensedHandsRig rig = action.Rig;
            AuthoredSample[] drinkSamples = AuthorClip(
                rig,
                motions.DrinkClip.length,
                time => EvaluateDrinkFull(time, motions.DrinkClip.length));
            AuthoredSample[] shortSamples = AuthorClip(
                rig,
                motions.DrinkShortClip.length,
                time => EvaluateDrinkShort(
                    time,
                    motions.DrinkShortClip.length));
            AuthoredSample[] spraySamples = AuthorClip(
                rig,
                motions.DrinkSprayClip.length,
                time => EvaluateDrinkSpray(
                    time,
                    motions.DrinkSprayClip.length));
            AuthoredSample[] throwSamples = AuthorClip(
                rig,
                motions.DrinkThrowClip.length,
                time => EvaluateDrinkThrow(
                    time,
                    motions.DrinkThrowClip.length));

            AnimationClip drink = CreateAuthoredClip(
                "Realistic FPS Hands Drink — Authored Anatomical Motion",
                DrinkClipAssetPath,
                rig,
                drinkSamples);
            AnimationClip shortDrink = CreateAuthoredClip(
                "Realistic FPS Hands Drink Short — Authored Anatomical Motion",
                DrinkShortClipAssetPath,
                rig,
                shortSamples);
            AnimationClip spray = CreateAuthoredClip(
                "Realistic FPS Hands Drink Spray — Authored Anatomical Motion",
                DrinkSprayClipAssetPath,
                rig,
                spraySamples);
            AnimationClip throwClip = CreateAuthoredClip(
                "Realistic FPS Hands Drink Throw — Authored Anatomical Motion",
                DrinkThrowClipAssetPath,
                rig,
                throwSamples);

            AuthoredSample ready = FindNearestSample(
                drinkSamples,
                0.5f);
            ApplyPose(rig, ready.Pose);
            Transform grip = CreatePalmLocalBottleGrip(rig);
            return new LicensedDrinkClips(
                drink,
                shortDrink,
                throwClip,
                spray,
                grip);
        }

        private static Transform CreatePalmLocalBottleGrip(
            LicensedHandsRig rig)
        {
            var gripObject = new GameObject("Beer Bottle Grip Anchor");
            Transform grip = gripObject.transform;
            PalmFrame frame = PalmFrame.FromLicensed(rig);
            Vector3 worldPosition = rig.RightPalm.position +
                                    frame.Forward * 0.078f +
                                    frame.Normal * -0.025f +
                                    frame.Across * 0.015f;
            Quaternion worldRotation = Quaternion.LookRotation(
                Vector3.up,
                Vector3.forward);
            grip.SetParent(rig.RightPalm, false);
            grip.SetPositionAndRotation(worldPosition, worldRotation);
            return grip;
        }

        private static AnimationClip BuildWaveClip(
            LicensedAction action,
            AnimationClip donorClip)
        {
            AuthoredSample[] samples = AuthorClip(
                action.Rig,
                donorClip.length,
                time => EvaluateWave(time, donorClip.length));
            AnimationClip clip = CreateAuthoredClip(
                "Realistic FPS Hands Wave — Authored Anatomical Motion",
                WaveClipAssetPath,
                action.Rig,
                samples);
            ApplyPose(action.Rig, samples[0].Pose);
            return clip;
        }

        private static AnimationClip BuildMiddleFingerClip(
            LicensedAction action,
            AnimationClip donorClip)
        {
            AuthoredSample[] samples = AuthorClip(
                action.Rig,
                donorClip.length,
                time => EvaluateMiddleFinger(time, donorClip.length));
            AnimationClip clip = CreateAuthoredClip(
                "Realistic FPS Hands Middle Finger — Authored Anatomical Motion",
                MiddleFingerClipAssetPath,
                action.Rig,
                samples);
            ApplyPose(action.Rig, samples[0].Pose);
            return clip;
        }

        private static AuthoredSample[] AuthorClip(
            LicensedHandsRig rig,
            float length,
            Func<float, AuthoredArmState> evaluate)
        {
            if (!float.IsFinite(length) || length <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int frameCount = Mathf.Max(
                1,
                Mathf.CeilToInt(length * AuthoredFrameRate));
            var samples = new AuthoredSample[frameCount + 1];
            for (int frame = 0; frame <= frameCount; frame++)
            {
                float time = frame == frameCount
                    ? length
                    : Mathf.Min(length, frame / AuthoredFrameRate);
                PoseLicensedRigFromAuthoredState(rig, evaluate(time));
                samples[frame] = new AuthoredSample(
                    time,
                    PoseState.Capture(rig));
            }

            rig.RestoreRestPose();
            return samples;
        }

        private static AuthoredArmState EvaluateDrinkFull(
            float time,
            float length)
        {
            float entry = SmoothRange(time, 0f, 0.5f);
            float sip = SmoothRange(time, 0.5f, 1.45f);
            float lower = SmoothRange(
                time,
                Mathf.Max(1.45f, length - 1.15f),
                Mathf.Max(1.7f, length - 0.45f));
            float exit = SmoothRange(
                time,
                Mathf.Max(1.7f, length - 0.45f),
                length);
            AuthoredArmState ready = DrinkReadyState();
            AuthoredArmState mouth = DrinkMouthState();
            AuthoredArmState hidden = DrinkHiddenState();
            AuthoredArmState state = AuthoredArmState.Lerp(
                hidden,
                ready,
                entry);
            state = AuthoredArmState.Lerp(state, mouth, sip);
            state = AuthoredArmState.Lerp(state, ready, lower);
            state = AuthoredArmState.Lerp(state, hidden, exit);
            return state;
        }

        private static AuthoredArmState EvaluateDrinkShort(
            float time,
            float length)
        {
            float raise = SmoothRange(
                time,
                0f,
                Mathf.Min(0.55f, length));
            float lower = SmoothRange(
                time,
                Mathf.Max(0.55f, length - 0.7f),
                length);
            AuthoredArmState state = AuthoredArmState.Lerp(
                DrinkReadyState(),
                DrinkMouthState(),
                raise);
            return AuthoredArmState.Lerp(
                state,
                DrinkReadyState(),
                lower);
        }

        private static AuthoredArmState EvaluateDrinkSpray(
            float time,
            float length)
        {
            float aim = SmoothRange(time, 0f, Mathf.Min(0.7f, length));
            AuthoredArmState spray = DrinkReadyState()
                .WithPalmPosition(new Vector3(0.08f, 0.07f, 0.34f))
                .WithForearmDirection(
                    new Vector3(0f, 0.7f, 0.72f).normalized)
                .WithPalmFrame(
                    new Vector3(0f, 0.7f, 0.72f).normalized,
                    Vector3.left);
            return AuthoredArmState.Lerp(
                DrinkReadyState(),
                spray,
                aim);
        }

        private static AuthoredArmState EvaluateDrinkThrow(
            float time,
            float length)
        {
            float throwWeight = SmoothRange(time, 0f, length);
            AuthoredArmState followThrough = DrinkReadyState()
                .WithPalmPosition(new Vector3(-0.04f, 0.2f, 0.48f))
                .WithForearmDirection(
                    new Vector3(0.1f, 0.4f, 0.91f).normalized)
                .WithPalmFrame(
                    new Vector3(0.1f, 0.4f, 0.91f).normalized,
                    Vector3.left);
            return AuthoredArmState.Lerp(
                DrinkReadyState(),
                followThrough,
                throwWeight);
        }

        private static AuthoredArmState DrinkReadyState() =>
            new AuthoredArmState(
                new Vector3(0.42f, -0.42f, 0.16f),
                new Vector3(0.26f, -0.18f, 0.4f),
                new Vector3(-0.72f, 0.55f, 0.42f).normalized,
                new Vector3(-0.94f, 0f, 0.34f).normalized,
                new Vector3(-0.34f, 0f, -0.94f).normalized,
                AuthoredFingerPose.DrinkGrip);

        private static AuthoredArmState DrinkHiddenState() =>
            new AuthoredArmState(
                new Vector3(0.42f, -0.42f, 0.16f),
                new Vector3(0.34f, -0.38f, 0.36f),
                new Vector3(-0.5f, 0.65f, 0.57f).normalized,
                new Vector3(-0.94f, 0f, 0.34f).normalized,
                new Vector3(-0.34f, 0f, -0.94f).normalized,
                AuthoredFingerPose.DrinkGrip);

        private static AuthoredArmState DrinkMouthState()
        {
            Quaternion bottleTilt = Quaternion.AngleAxis(
                75f,
                Vector3.right);
            Vector3 readyForward =
                new Vector3(-0.94f, 0f, 0.34f).normalized;
            Vector3 readyNormal =
                new Vector3(-0.34f, 0f, -0.94f).normalized;
            return new AuthoredArmState(
                new Vector3(0.38f, -0.36f, 0.12f),
                new Vector3(0.09f, -0.1f, 0.31f),
                new Vector3(-0.72f, 0.24f, 0.65f).normalized,
                bottleTilt * readyForward,
                bottleTilt * readyNormal,
                AuthoredFingerPose.DrinkGrip);
        }

        private static AuthoredArmState EvaluateWave(
            float time,
            float length)
        {
            float enter = SmoothRange(time, 0f, 0.2f);
            float exit = 1f - SmoothRange(
                time,
                Mathf.Max(0.2f, length - 0.2f),
                length);
            float visibility = Mathf.Min(enter, exit);
            float wavePhase = Mathf.Clamp01(
                Mathf.InverseLerp(0.2f, length - 0.2f, time));
            float swing = Mathf.Sin(wavePhase * Mathf.PI * 3f) *
                          visibility;
            Vector3 fingerDirection = Quaternion.AngleAxis(
                    swing * 2f,
                    Vector3.forward) *
                Vector3.up;
            Vector3 raised = new Vector3(0.2f, -0.065f, 0.38f) +
                             new Vector3(0.035f, 0.008f, 0f) * swing;
            Vector3 hidden = new Vector3(0.34f, -0.38f, 0.36f);
            Vector3 forearmDirection = Quaternion.AngleAxis(
                    swing * 8f,
                    Vector3.forward) *
                new Vector3(-0.36f, 0.82f, 0.44f).normalized;
            return new AuthoredArmState(
                new Vector3(0.42f + swing * 0.01f, -0.42f, 0.16f),
                Vector3.Lerp(hidden, raised, visibility),
                forearmDirection,
                fingerDirection,
                Vector3.back,
                AuthoredFingerPose.Open);
        }

        private static AuthoredArmState EvaluateMiddleFinger(
            float time,
            float length)
        {
            float enter = SmoothRange(time, 0f, 0.2f);
            float exit = 1f - SmoothRange(
                time,
                Mathf.Max(0.2f, length - 0.2f),
                length);
            float visibility = Mathf.Min(enter, exit);
            Vector3 raised = new Vector3(0.2f, -0.075f, 0.38f);
            Vector3 hidden = new Vector3(0.34f, -0.38f, 0.36f);
            return new AuthoredArmState(
                new Vector3(0.42f, -0.42f, 0.16f),
                Vector3.Lerp(hidden, raised, visibility),
                new Vector3(-0.36f, 0.82f, 0.44f).normalized,
                Vector3.up,
                Vector3.forward,
                AuthoredFingerPose.MiddleFinger);
        }

        private static float SmoothRange(
            float value,
            float start,
            float end)
        {
            if (end <= start)
            {
                return value >= end ? 1f : 0f;
            }

            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(start, end, value));
        }

        private static void PoseLicensedRigFromAuthoredState(
            LicensedHandsRig rig,
            AuthoredArmState state)
        {
            if (Vector3.Angle(
                    state.ForearmDirection,
                    state.PalmForward) > 50f)
            {
                throw new InvalidOperationException(
                    "Authored wrist exceeds the 50-degree pose limit.");
            }

            rig.RestoreRestPose();
            rig.Model.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            float upperArmLength = Vector3.Distance(
                rig.RightUpperArm.position,
                rig.RightForearm.position);
            float forearmLength = Vector3.Distance(
                                      rig.RightForearm.position,
                                      rig.RightForearmTwist.position) +
                                  Vector3.Distance(
                                      rig.RightForearmTwist.position,
                                      rig.RightPalm.position);
            Vector3 elbow = state.PalmPosition -
                            state.ForearmDirection * forearmLength;
            Vector3 upperDirection = elbow - state.ShoulderPosition;
            if (upperDirection.sqrMagnitude <= 0.000001f)
            {
                throw new InvalidOperationException(
                    "Authored shoulder guide overlaps the elbow.");
            }

            Vector3 shoulder = elbow -
                               upperDirection.normalized * upperArmLength;
            rig.Model.position += shoulder - rig.RightUpperArm.position;
            AimBoneAt(
                rig.RightUpperArm,
                rig.RightForearm,
                upperDirection);
            AimBoneAt(
                rig.RightForearm,
                rig.RightForearmTwist,
                state.ForearmDirection);
            AimBoneAt(
                rig.RightForearmTwist,
                rig.RightPalm,
                state.ForearmDirection);

            Vector3 desiredAcross = Vector3.Cross(
                state.PalmNormal,
                state.PalmForward);
            PalmFrame desiredPalm = PalmFrame.Create(
                state.PalmForward,
                desiredAcross);
            DistributeForearmRoll(
                rig,
                state.ForearmDirection,
                desiredPalm.Normal);
            PalmFrame currentPalm = PalmFrame.FromLicensed(rig);
            rig.RightPalm.rotation =
                desiredPalm.Rotation *
                Quaternion.Inverse(currentPalm.Rotation) *
                rig.RightPalm.rotation;
            PoseLicensedDigits(rig, state.FingerPose);
        }

        private static void DistributeForearmRoll(
            LicensedHandsRig rig,
            Vector3 forearmDirection,
            Vector3 desiredPalmNormal)
        {
            Vector3 axis = forearmDirection.normalized;
            PalmFrame currentPalm = PalmFrame.FromLicensed(rig);
            Vector3 currentNormal = Vector3.ProjectOnPlane(
                currentPalm.Normal,
                axis);
            Vector3 desiredNormal = Vector3.ProjectOnPlane(
                desiredPalmNormal,
                axis);
            if (currentNormal.sqrMagnitude <= 0.000001f ||
                desiredNormal.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            float roll = Vector3.SignedAngle(
                currentNormal,
                desiredNormal,
                axis);
            Quaternion halfRoll = Quaternion.AngleAxis(roll * 0.5f, axis);
            rig.RightForearm.rotation =
                halfRoll * rig.RightForearm.rotation;
            rig.RightForearmTwist.rotation =
                halfRoll * rig.RightForearmTwist.rotation;
        }

        private static void PoseLicensedDigits(
            LicensedHandsRig rig,
            AuthoredFingerPose pose)
        {
            switch (pose)
            {
                case AuthoredFingerPose.Open:
                    ApplyDigitDelta(
                        rig.RightDigits[0],
                        new Vector3(0f, -8f, -7f),
                        new Vector3(0f, 0f, -5f),
                        new Vector3(0f, 0f, -4f));
                    for (int digit = 1;
                         digit < rig.RightDigits.Length;
                         digit++)
                    {
                        ApplyDigitDelta(
                            rig.RightDigits[digit],
                            new Vector3(0f, 0f, -4f),
                            new Vector3(0f, 0f, -15f),
                            new Vector3(0f, 0f, -10f));
                    }

                    break;

                case AuthoredFingerPose.DrinkGrip:
                    ApplyDigitDelta(
                        rig.RightDigits[0],
                        new Vector3(-15f, -8f, 35f),
                        new Vector3(0f, 12f, 50f),
                        new Vector3(0f, 0f, 20f));
                    for (int digit = 1;
                         digit < rig.RightDigits.Length;
                         digit++)
                    {
                        float baseCurl = 28f + (digit - 1) * 4f;
                        float middleCurl = 78f + (digit - 1) * 4f;
                        float tipCurl = 45f + (digit - 1) * 5f;
                        ApplyDigitDelta(
                            rig.RightDigits[digit],
                            new Vector3(0f, 0f, baseCurl),
                            new Vector3(0f, 0f, middleCurl),
                            new Vector3(0f, 0f, tipCurl));
                    }

                    break;

                case AuthoredFingerPose.MiddleFinger:
                    ApplyDigitDelta(
                        rig.RightDigits[0],
                        new Vector3(10f, 15f, 45f),
                        new Vector3(0f, 20f, 90f),
                        new Vector3(0f, 0f, 70f));
                    ApplyDigitDelta(
                        rig.RightDigits[1],
                        new Vector3(0f, -4f, 78f),
                        new Vector3(0f, 0f, 100f),
                        new Vector3(0f, 0f, 65f));
                    ApplyDigitDelta(
                        rig.RightDigits[2],
                        new Vector3(0f, 0f, -4f),
                        new Vector3(0f, 0f, -16f),
                        new Vector3(0f, 0f, -10f));
                    ApplyDigitDelta(
                        rig.RightDigits[3],
                        new Vector3(0f, 0f, 90f),
                        new Vector3(0f, 0f, 120f),
                        new Vector3(0f, 0f, 80f));
                    ApplyDigitDelta(
                        rig.RightDigits[4],
                        new Vector3(0f, 0f, 95f),
                        new Vector3(0f, 0f, 125f),
                        new Vector3(0f, 0f, 85f));
                    break;
            }
        }

        private static void ApplyDigitDelta(
            IReadOnlyList<Transform> chain,
            Vector3 baseEuler,
            Vector3 middleEuler,
            Vector3 tipEuler)
        {
            chain[0].localRotation *= Quaternion.Euler(baseEuler);
            chain[1].localRotation *= Quaternion.Euler(middleEuler);
            chain[2].localRotation *= Quaternion.Euler(tipEuler);
        }

        private static void AimBoneAt(
            Transform bone,
            Transform child,
            Vector3 desiredWorldDirection)
        {
            Vector3 currentDirection = child.position - bone.position;
            if (currentDirection.sqrMagnitude <= 0.000001f)
            {
                throw new InvalidOperationException(
                    $"Licensed hand bone '{bone.name}' has zero length.");
            }

            bone.rotation = Quaternion.FromToRotation(
                    currentDirection.normalized,
                    desiredWorldDirection.normalized) *
                bone.rotation;
        }

        private static AnimationClip CreateAuthoredClip(
            string name,
            string assetPath,
            LicensedHandsRig rig,
            IReadOnlyList<AuthoredSample> samples)
        {
            return CreateClip(
                name,
                assetPath,
                rig,
                samples.Select(sample =>
                    (sample.Time, sample.Pose)).ToArray());
        }

        private static AuthoredSample FindNearestSample(
            IReadOnlyList<AuthoredSample> samples,
            float time)
        {
            return samples
                .OrderBy(sample => Mathf.Abs(sample.Time - time))
                .First();
        }

        private static void ApplyPose(
            LicensedHandsRig rig,
            PoseState pose)
        {
            rig.Model.SetLocalPositionAndRotation(
                pose.ModelPosition,
                pose.ModelRotation);
            rig.Model.localScale = pose.ModelScale;
            for (int index = 0; index < rig.AnimatedBones.Count; index++)
            {
                rig.AnimatedBones[index].localRotation =
                    pose.BoneRotations[index];
            }
        }

        private static AnimationClip CreateClip(
            string name,
            string assetPath,
            LicensedHandsRig rig,
            params (float Time, PoseState Pose)[] keys)
        {
            if (keys == null || keys.Length < 2)
            {
                throw new ArgumentException(
                    "A licensed hand clip requires at least two poses.",
                    nameof(keys));
            }

            var clip = new AnimationClip
            {
                name = name,
                legacy = true,
                frameRate = AuthoredFrameRate,
                wrapMode = WrapMode.Once,
            };
            string modelPath = GetRelativePath(
                rig.ActionRoot,
                rig.Model);
            SetVector3Curves(
                clip,
                modelPath,
                "localPosition",
                keys.Select(key =>
                    (key.Time, key.Pose.ModelPosition)).ToArray());
            SetQuaternionCurves(
                clip,
                modelPath,
                keys.Select(key =>
                    (key.Time, key.Pose.ModelRotation)).ToArray());
            SetVector3Curves(
                clip,
                modelPath,
                "localScale",
                keys.Select(key =>
                    (key.Time, key.Pose.ModelScale)).ToArray());

            for (int boneIndex = 0;
                 boneIndex < rig.AnimatedBones.Count;
                 boneIndex++)
            {
                Transform bone = rig.AnimatedBones[boneIndex];
                string path = GetRelativePath(rig.ActionRoot, bone);
                SetQuaternionCurves(
                    clip,
                    path,
                    keys.Select(key =>
                        (key.Time, key.Pose.BoneRotations[boneIndex]))
                        .ToArray());
            }

            clip.EnsureQuaternionContinuity();
            AnimationUtility.SetAnimationEvents(
                clip,
                Array.Empty<AnimationEvent>());
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        private static void SetVector3Curves(
            AnimationClip clip,
            string path,
            string property,
            IReadOnlyList<(float Time, Vector3 Value)> keys)
        {
            clip.SetCurve(
                path,
                typeof(Transform),
                property + ".x",
                BuildCurve(keys.Select(key =>
                    (key.Time, key.Value.x)).ToArray()));
            clip.SetCurve(
                path,
                typeof(Transform),
                property + ".y",
                BuildCurve(keys.Select(key =>
                    (key.Time, key.Value.y)).ToArray()));
            clip.SetCurve(
                path,
                typeof(Transform),
                property + ".z",
                BuildCurve(keys.Select(key =>
                    (key.Time, key.Value.z)).ToArray()));
        }

        private static void SetQuaternionCurves(
            AnimationClip clip,
            string path,
            IReadOnlyList<(float Time, Quaternion Value)> sourceKeys)
        {
            var rotations = new (float Time, Quaternion Value)[
                sourceKeys.Count];
            Quaternion previous = sourceKeys[0].Value.normalized;
            for (int index = 0; index < sourceKeys.Count; index++)
            {
                Quaternion current = sourceKeys[index].Value.normalized;
                if (index > 0 && Quaternion.Dot(previous, current) < 0f)
                {
                    current = new Quaternion(
                        -current.x,
                        -current.y,
                        -current.z,
                        -current.w);
                }

                rotations[index] = (sourceKeys[index].Time, current);
                previous = current;
            }

            clip.SetCurve(
                path,
                typeof(Transform),
                "localRotation.x",
                BuildCurve(rotations.Select(key =>
                    (key.Time, key.Value.x)).ToArray()));
            clip.SetCurve(
                path,
                typeof(Transform),
                "localRotation.y",
                BuildCurve(rotations.Select(key =>
                    (key.Time, key.Value.y)).ToArray()));
            clip.SetCurve(
                path,
                typeof(Transform),
                "localRotation.z",
                BuildCurve(rotations.Select(key =>
                    (key.Time, key.Value.z)).ToArray()));
            clip.SetCurve(
                path,
                typeof(Transform),
                "localRotation.w",
                BuildCurve(rotations.Select(key =>
                    (key.Time, key.Value.w)).ToArray()));
        }

        private static AnimationCurve BuildCurve(
            IReadOnlyList<(float Time, float Value)> values)
        {
            var keys = new Keyframe[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                keys[index] = new Keyframe(
                    values[index].Time,
                    values[index].Value);
            }

            var curve = new AnimationCurve(keys);
            for (int index = 0; index < keys.Length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(
                    curve,
                    index,
                    AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(
                    curve,
                    index,
                    AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        private static string GetRelativePath(
            Transform root,
            Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            var parts = new Stack<string>();
            Transform cursor = target;
            while (cursor != null && cursor != root)
            {
                parts.Push(cursor.name);
                cursor = cursor.parent;
            }

            if (cursor != root)
            {
                throw new InvalidOperationException(
                    $"Transform '{target.name}' is outside action root " +
                    $"'{root.name}'.");
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// Diagnostic batch entry point used while authoring the integration.
        /// It is intentionally read-only and records no Unity object IDs in
        /// runtime code.
        /// </summary>
        public static void AuditSourceFromBatch()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                ModelAssetPath);
            if (model == null)
            {
                throw new FileNotFoundException(
                    "Licensed FPS-hands source model was not imported.",
                    ModelAssetPath);
            }

            var report = new StringBuilder(8192);
            report.AppendLine("Licensed Realistic FPS Hands source audit");
            report.AppendLine($"Model: {ModelAssetPath}");
            foreach (UnityEngine.Object asset in
                     AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath))
            {
                report.AppendLine(
                    $"SUBASSET|{asset.GetType().Name}|{asset.name}");
                if (asset is AnimationClip clip)
                {
                    report.AppendLine(
                        $"CLIP|length={Format(clip.length)}|" +
                        $"frameRate={Format(clip.frameRate)}|" +
                        $"legacy={clip.legacy}|" +
                        $"curves={AnimationUtility.GetCurveBindings(clip).Length}");
                    foreach (EditorCurveBinding binding in
                             AnimationUtility.GetCurveBindings(clip))
                    {
                        report.AppendLine(
                            $"CURVE|{binding.path}|{binding.type.Name}|" +
                            binding.propertyName);
                    }
                }
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(model) as
                GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(model);
            }

            try
            {
                AppendTransform(report, instance.transform, string.Empty);
                foreach (SkinnedMeshRenderer renderer in
                         instance.GetComponentsInChildren<
                             SkinnedMeshRenderer>(true))
                {
                    string materialNames = string.Join(
                        ",",
                        renderer.sharedMaterials.Select(material =>
                            material != null ? material.name : "<null>"));
                    report.AppendLine(
                        $"SKIN|{GetPath(instance.transform, renderer.transform)}|" +
                        $"mesh={renderer.sharedMesh?.name}|" +
                        $"vertices={renderer.sharedMesh?.vertexCount ?? 0}|" +
                        $"bones={renderer.bones.Length}|" +
                        $"root={renderer.rootBone?.name}|" +
                        $"boundsCenter={Format(renderer.localBounds.center)}|" +
                        $"boundsSize={Format(renderer.localBounds.size)}|" +
                        $"worldCenter={Format(renderer.bounds.center)}|" +
                        $"worldSize={Format(renderer.bounds.size)}|" +
                        $"materials={materialNames}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            string reportPath = Path.Combine(
                projectRoot,
                "Logs",
                "licensed_realistic_fps_hands_audit.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ??
                projectRoot);
            File.WriteAllText(
                reportPath,
                report.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Debug.Log($"Licensed FPS-hands audit written to '{reportPath}'.");
        }

        /// <summary>
        /// Produces local review frames for the package's single long take.
        /// The images are audit evidence only and remain under ignored Logs.
        /// </summary>
        public static void RenderSourceContactSheetFromBatch()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                ModelAssetPath);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(
                    ModelAssetPath)
                .OfType<AnimationClip>()
                .Single(candidate => candidate.name == "Take 001");
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                SourceRoot + "/RealisticFPSHands_Albedo.png");
            if (model == null || clip == null || albedo == null)
            {
                throw new InvalidOperationException(
                    "Licensed FPS-hands review assets are incomplete.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            string output = Path.Combine(
                projectRoot,
                "Logs",
                "RealisticFpsHandsFrames");
            Directory.CreateDirectory(output);

            GameObject instance = UnityEngine.Object.Instantiate(model);
            var cameraObject = new GameObject("Licensed Hands Audit Camera");
            var lightObject = new GameObject("Licensed Hands Audit Light");
            var fillObject = new GameObject("Licensed Hands Audit Fill");
            Material material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                instance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                SkinnedMeshRenderer primary = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(renderer => renderer.sharedMesh != null &&
                        renderer.sharedMesh.name == "FPS Hands");
                foreach (SkinnedMeshRenderer renderer in instance
                             .GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.enabled = renderer == primary;
                }

                Shader shader = Shader.Find("HDRP/Unlit") ??
                    Shader.Find("Standard");
                material = new Material(shader)
                {
                    name = "Licensed Hands Audit Material",
                };
                if (material.HasProperty("_UnlitColorMap"))
                {
                    material.SetTexture("_UnlitColorMap", albedo);
                    material.SetColor("_UnlitColor", Color.white);
                }
                else if (material.HasProperty("_BaseColorMap"))
                {
                    material.SetTexture("_BaseColorMap", albedo);
                    material.SetColor("_BaseColor", Color.white);
                }
                else
                {
                    material.mainTexture = albedo;
                    material.color = Color.white;
                }

                primary.sharedMaterial = material;
                primary.updateWhenOffscreen = true;

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                Light key = lightObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 0f;
                key.color = new Color(1f, 0.9f, 0.78f);
                lightObject.transform.rotation =
                    Quaternion.Euler(35f, -35f, 0f);
                Light fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0f;
                fill.color = new Color(0.62f, 0.76f, 1f);
                fillObject.transform.rotation =
                    Quaternion.Euler(320f, 145f, 0f);

                target = new RenderTexture(
                    640,
                    360,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 2,
                    name = "Licensed Hands Audit Target",
                };
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGB24,
                    mipChain: false);

                int frameCount = Mathf.CeilToInt(clip.length / 2f) + 1;
                for (int index = 0; index < frameCount; index++)
                {
                    float time = Mathf.Min(index * 2f, clip.length);
                    clip.SampleAnimation(instance, time);
                    Bounds bounds = primary.bounds;
                    float radius = Mathf.Max(
                        0.15f,
                        bounds.extents.magnitude);
                    float distance = radius /
                        Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) *
                        1.12f;
                    Vector3 direction = new Vector3(0f, 0.12f, -1f)
                        .normalized;
                    camera.transform.position =
                        bounds.center + direction * distance;
                    camera.transform.LookAt(bounds.center, Vector3.up);
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixels.ReadPixels(
                        new Rect(0f, 0f, target.width, target.height),
                        0,
                        0,
                        recalculateMipMaps: false);
                    pixels.Apply(updateMipmaps: false);
                    RenderTexture.active = previous;
                    File.WriteAllBytes(
                        Path.Combine(
                            output,
                            $"frame_{index:00}_{time:00.0}s.png"),
                        pixels.EncodeToPNG());
                }
            }
            finally
            {
                if (target != null)
                {
                    target.Release();
                }

                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(fillObject);
            }

            Debug.Log($"Licensed FPS-hands review frames written to '{output}'.");
        }

        public static void RenderGeneratedActionsFromBatch()
        {
            const string prefabPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/" +
                "GameplayPresentation/PlayerViewmodel/Resources/" +
                "Phase1PlayerViewmodel/Phase1PlayerViewmodelBinding.prefab";
            GameObject prefab = RequireAsset<GameObject>(prefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.SetActive(true);
            var cameraObject = new GameObject("Generated Hands Audit Camera");
            Material previewMaterial = null;
            Material bottleMaterial = null;
            GameObject bottle = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                Shader shader = Shader.Find("HDRP/Unlit") ??
                    Shader.Find("Standard");
                previewMaterial = new Material(shader);
                Texture2D albedo = RequireAsset<Texture2D>(AlbedoAssetPath);
                if (previewMaterial.HasProperty("_UnlitColorMap"))
                {
                    previewMaterial.SetTexture("_UnlitColorMap", albedo);
                    previewMaterial.SetColor("_UnlitColor", Color.white);
                }
                else
                {
                    previewMaterial.mainTexture = albedo;
                    previewMaterial.color = Color.white;
                }

                foreach (SkinnedMeshRenderer renderer in instance
                             .GetComponentsInChildren<
                                 SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh != null)
                    {
                        renderer.sharedMaterial = previewMaterial;
                        renderer.updateWhenOffscreen = true;
                        renderer.forceMatrixRecalculationPerRender = true;
                    }
                }

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.21f, 0.24f);
                camera.fieldOfView = 72f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                target = new RenderTexture(
                    1280,
                    720,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 2,
                };
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGB24,
                    mipChain: false);

                Transform grip = instance.GetComponentsInChildren<Transform>(
                        true)
                    .Single(transform =>
                        transform.name == "Beer Bottle Grip Anchor");
                bottle = new GameObject("Audit Beer Bottle");
                bottle.name = "Audit Beer Bottle";
                bottle.transform.SetPositionAndRotation(
                    grip.position,
                    grip.rotation);
                bottle.transform.localScale = Vector3.one;
                Mesh bottleMesh = RequireAsset<Mesh>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
                    "Items/Meshes/" +
                    "LegacyItemMesh_5103d9d7418206a4da09260262c716a0.asset");
                bottle.AddComponent<MeshFilter>().sharedMesh = bottleMesh;
                MeshRenderer bottleRenderer =
                    bottle.AddComponent<MeshRenderer>();
                bottleMaterial = new Material(shader);
                Material sourceBottleMaterial = RequireAsset<Material>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
                    "Items/Materials/" +
                    "LegacyItemMaterial_505c0da5d59fb1a4fbc38445d0494ef3.mat");
                Texture bottleTexture =
                    sourceBottleMaterial.HasProperty("_BaseColorMap")
                        ? sourceBottleMaterial.GetTexture("_BaseColorMap")
                        : sourceBottleMaterial.mainTexture;
                Color bottleColor =
                    sourceBottleMaterial.HasProperty("_BaseColor")
                        ? sourceBottleMaterial.GetColor("_BaseColor")
                        : new Color(0.34f, 0.12f, 0.025f, 1f);
                if (bottleMaterial.HasProperty("_UnlitColorMap"))
                {
                    bottleMaterial.SetTexture(
                        "_UnlitColorMap",
                        bottleTexture);
                }
                if (bottleMaterial.HasProperty("_UnlitColor"))
                {
                    bottleMaterial.SetColor("_UnlitColor", bottleColor);
                }
                else
                {
                    bottleMaterial.color = bottleColor;
                }

                bottleRenderer.sharedMaterial = bottleMaterial;

                string output = Path.Combine(
                    Directory.GetParent(Application.dataPath)?.FullName ??
                    throw new InvalidOperationException(
                        "Unity project root is unavailable."),
                    "Logs",
                    "GeneratedRealisticFpsHandsFrames");
                Directory.CreateDirectory(output);
                var reviews = new[]
                {
                    new GeneratedActionReview(
                        "Drink",
                        DrinkClipAssetPath,
                        new[] { 0f, 0.5f, 2f, 4f, 5.866667f }),
                    new GeneratedActionReview(
                        "Hello",
                        WaveClipAssetPath,
                        new[] { 0f, 0.333333f, 0.666667f, 1f, 1.333333f }),
                    new GeneratedActionReview(
                        "MiddleFinger",
                        MiddleFingerClipAssetPath,
                        new[] { 0f, 0.35f, 0.7f, 1.05f, 1.4f }),
                };
                Transform[] actionRoots = reviews.Select(review =>
                        instance.transform.Find(review.ActionName) ??
                        throw new InvalidOperationException(
                            $"Generated action root '{review.ActionName}' " +
                            "is missing."))
                    .ToArray();
                for (int reviewIndex = 0;
                     reviewIndex < reviews.Length;
                     reviewIndex++)
                {
                    GeneratedActionReview review = reviews[reviewIndex];
                    AnimationClip clip = RequireAsset<AnimationClip>(
                        review.ClipAssetPath);
                    for (int actionIndex = 0;
                         actionIndex < actionRoots.Length;
                         actionIndex++)
                    {
                        actionRoots[actionIndex].gameObject.SetActive(
                            actionIndex == reviewIndex);
                    }

                    bottle.SetActive(review.ActionName == "Drink");
                    for (int frameIndex = 0;
                         frameIndex < review.Times.Length;
                         frameIndex++)
                    {
                        float time = review.Times[frameIndex];
                        clip.SampleAnimation(actionRoots[reviewIndex].gameObject, time);
                        if (review.ActionName == "Drink")
                        {
                            // The licensed skeleton is imported with a scale on
                            // its hierarchy. Runtime carrying follows the
                            // anchor's world pose without parenting the item, so
                            // the audit bottle must do the same or it appears at
                            // 1.5% scale and gives a false alignment result.
                            bottle.transform.SetPositionAndRotation(
                                grip.position,
                                grip.rotation);
                        }

                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = target;
                        pixels.ReadPixels(
                            new Rect(0f, 0f, target.width, target.height),
                            0,
                            0,
                            recalculateMipMaps: false);
                        pixels.Apply(updateMipmaps: false);
                        RenderTexture.active = previous;
                        File.WriteAllBytes(
                            Path.Combine(
                                output,
                                $"{review.ActionName}_{frameIndex:00}_" +
                                time.ToString(
                                    "0.00",
                                    CultureInfo.InvariantCulture) +
                                "s.png"),
                            pixels.EncodeToPNG());
                    }
                }

                Debug.Log(
                    $"Generated licensed FPS-hands review frames written " +
                    $"to '{output}'.");
            }
            finally
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                if (target != null)
                {
                    target.Release();
                }

                UnityEngine.Object.DestroyImmediate(bottle);
                UnityEngine.Object.DestroyImmediate(bottleMaterial);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(previewMaterial);
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static PresentationAssetConfiguration
            LoadLocalConfiguration()
        {
            string path = ToFileSystemPath(LocalConfigurationPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Presentation asset local configuration is missing.",
                    path);
            }

            PresentationAssetConfiguration configuration =
                JsonUtility.FromJson<PresentationAssetConfiguration>(
                    File.ReadAllText(path, Encoding.UTF8));
            if (configuration == null ||
                string.IsNullOrWhiteSpace(
                    configuration.realisticFpsHandsUnityPackage))
            {
                throw new InvalidOperationException(
                    "PresentationAssets.local.json does not contain " +
                    "realisticFpsHandsUnityPackage.");
            }

            return configuration;
        }

        private static void WriteLocalConfiguration(string packagePath)
        {
            var configuration = new PresentationAssetConfiguration
            {
                realisticFpsHandsUnityPackage =
                    Path.GetFullPath(packagePath),
            };
            File.WriteAllText(
                ToFileSystemPath(LocalConfigurationPath),
                JsonUtility.ToJson(configuration, prettyPrint: true) +
                Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            Directory.CreateDirectory(ToFileSystemPath(assetPath));
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static T RequireAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required licensed {typeof(T).Name} is unavailable at " +
                    $"'{assetPath}'.");
            }

            return asset;
        }

        private static string ToFileSystemPath(string assetOrRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetOrRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public sealed class LicensedHandsBuildResult
        {
            private readonly LicensedAction drink;
            private readonly LicensedDrinkClips drinkClips;
            private readonly LicensedAction wave;
            private readonly LicensedAction middleFinger;

            internal LicensedHandsBuildResult(
                LicensedAction configuredDrink,
                LicensedDrinkClips configuredDrinkClips,
                LicensedAction configuredWave,
                AnimationClip configuredWaveClip,
                LicensedAction configuredMiddleFinger,
                AnimationClip configuredMiddleFingerClip)
            {
                drink = configuredDrink;
                drinkClips = configuredDrinkClips;
                wave = configuredWave;
                WaveClip = configuredWaveClip;
                middleFinger = configuredMiddleFinger;
                MiddleFingerClip = configuredMiddleFingerClip;
            }

            public GameObject DrinkRoot => drink.Root;

            public Animation DrinkAnimation => drink.Animation;

            public AnimationClip DrinkClip => drinkClips.Drink;

            public AnimationClip DrinkShortClip => drinkClips.Short;

            public AnimationClip DrinkThrowClip => drinkClips.Throw;

            public AnimationClip DrinkSprayClip => drinkClips.Spray;

            public Transform DrinkGripAnchor => drinkClips.Grip;

            public GameObject WaveRoot => wave.Root;

            public Animation WaveAnimation => wave.Animation;

            public AnimationClip WaveClip { get; }

            public GameObject MiddleFingerRoot => middleFinger.Root;

            public Animation MiddleFingerAnimation =>
                middleFinger.Animation;

            public AnimationClip MiddleFingerClip { get; }

            public Mesh HandMesh => drink.Renderer.sharedMesh;
        }

        private sealed class LicensedSourceSpec
        {
            public LicensedSourceSpec(
                string role,
                string packageGuid,
                string assetPath,
                string sha256)
            {
                Role = role;
                PackageGuid = packageGuid;
                AssetPath = assetPath;
                Sha256 = sha256;
            }

            public string Role { get; }

            public string PackageGuid { get; }

            public string AssetPath { get; }

            public string Sha256 { get; }
        }

        internal sealed class DonorViewmodelMotionSet
        {
            public DonorViewmodelMotionSet(
                DonorActionMotion drink,
                AnimationClip drinkClip,
                AnimationClip drinkShortClip,
                AnimationClip drinkSprayClip,
                AnimationClip drinkThrowClip,
                DonorActionMotion wave,
                AnimationClip waveClip,
                DonorActionMotion middleFinger,
                AnimationClip middleFingerClip)
            {
                Drink = drink ??
                    throw new ArgumentNullException(nameof(drink));
                DrinkClip = drinkClip ??
                    throw new ArgumentNullException(nameof(drinkClip));
                DrinkShortClip = drinkShortClip ??
                    throw new ArgumentNullException(nameof(drinkShortClip));
                DrinkSprayClip = drinkSprayClip ??
                    throw new ArgumentNullException(nameof(drinkSprayClip));
                DrinkThrowClip = drinkThrowClip ??
                    throw new ArgumentNullException(nameof(drinkThrowClip));
                Wave = wave ??
                    throw new ArgumentNullException(nameof(wave));
                WaveClip = waveClip ??
                    throw new ArgumentNullException(nameof(waveClip));
                MiddleFinger = middleFinger ??
                    throw new ArgumentNullException(nameof(middleFinger));
                MiddleFingerClip = middleFingerClip ??
                    throw new ArgumentNullException(nameof(middleFingerClip));
            }

            public DonorActionMotion Drink { get; }

            public AnimationClip DrinkClip { get; }

            public AnimationClip DrinkShortClip { get; }

            public AnimationClip DrinkSprayClip { get; }

            public AnimationClip DrinkThrowClip { get; }

            public DonorActionMotion Wave { get; }

            public AnimationClip WaveClip { get; }

            public DonorActionMotion MiddleFinger { get; }

            public AnimationClip MiddleFingerClip { get; }
        }

        internal sealed class DonorActionMotion
        {
            private readonly DonorTransformPose[] restPose;

            public DonorActionMotion(
                GameObject root,
                Transform animationTarget,
                Transform grip = null)
            {
                Root = root ??
                    throw new ArgumentNullException(nameof(root));
                AnimationTarget = animationTarget ??
                    throw new ArgumentNullException(nameof(animationTarget));
                if (!animationTarget.IsChildOf(root.transform) &&
                    animationTarget != root.transform)
                {
                    throw new InvalidOperationException(
                        "Original animation target is outside its action root.");
                }

                Grip = grip;
                if (grip != null &&
                    !grip.IsChildOf(root.transform) &&
                    grip != root.transform)
                {
                    throw new InvalidOperationException(
                        "Original drink grip is outside its action root.");
                }

                restPose = root.GetComponentsInChildren<Transform>(true)
                    .Select(transform => new DonorTransformPose(
                        transform,
                        transform.localPosition,
                        transform.localRotation,
                        transform.localScale))
                    .ToArray();
                Rig = new DonorHandRig(root);
            }

            public GameObject Root { get; }

            public Transform AnimationTarget { get; }

            public Transform Grip { get; }

            public DonorHandRig Rig { get; }

            public void RestoreRestPose()
            {
                foreach (DonorTransformPose pose in restPose)
                {
                    pose.Restore();
                }
            }
        }

        internal sealed class DonorHandRig
        {
            public DonorHandRig(GameObject actionRoot)
            {
                SkinnedMeshRenderer[] renderers = actionRoot
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.sharedMesh != null &&
                        string.Equals(
                            renderer.sharedMesh.name,
                            "hand_rigged",
                            StringComparison.Ordinal))
                    .ToArray();
                if (renderers.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Original action '{actionRoot.name}' must contain " +
                        $"one locked hand_rigged renderer; found " +
                        $"{renderers.Length}.");
                }

                Transform[] bones = renderers[0].bones;
                if (bones.Length != 21 ||
                    !string.Equals(bones[0].name, "Bone", StringComparison.Ordinal) ||
                    !string.Equals(bones[1].name, "Bone_001", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Original action '{actionRoot.name}' no longer " +
                        "matches the locked 21-bone viewmodel hand rig.");
                }

                Elbow = bones[0];
                Palm = bones[1];
                Digits = new[]
                {
                    new[] { bones[2], bones[3], bones[4] },
                    new[] { bones[17], bones[18], bones[19], bones[20] },
                    new[] { bones[9], bones[10], bones[11], bones[12] },
                    new[] { bones[5], bones[6], bones[7], bones[8] },
                    new[] { bones[13], bones[14], bones[15], bones[16] },
                };
            }

            public Transform Elbow { get; }

            public Transform Palm { get; }

            public Transform[][] Digits { get; }

            public PalmFrame GetPalmFrame()
            {
                Vector3 forward = Vector3.zero;
                for (int digit = 1; digit < Digits.Length; digit++)
                {
                    forward += Digits[digit][1].position - Palm.position;
                }

                Vector3 across =
                    Digits[4][1].position - Digits[1][1].position;
                return PalmFrame.Create(forward, across);
            }

            public Vector3[] GetDigitDirections(int digit)
            {
                Transform[] chain = Digits[digit];
                if (chain.Length == 3)
                {
                    Vector3 first = RequireDirection(
                        chain[1].position - chain[0].position,
                        chain[0].name);
                    Vector3 second = RequireDirection(
                        chain[2].position - chain[1].position,
                        chain[1].name);
                    return new[] { first, second, second };
                }

                return new[]
                {
                    RequireDirection(
                        chain[1].position - chain[0].position,
                        chain[0].name),
                    RequireDirection(
                        chain[2].position - chain[1].position,
                        chain[1].name),
                    RequireDirection(
                        chain[3].position - chain[2].position,
                        chain[2].name),
                };
            }

            private static Vector3 RequireDirection(
                Vector3 value,
                string boneName)
            {
                if (value.sqrMagnitude <= 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"Original finger bone '{boneName}' has zero length.");
                }

                return value.normalized;
            }
        }

        internal readonly struct PalmFrame
        {
            private PalmFrame(
                Vector3 forward,
                Vector3 across,
                Vector3 normal)
            {
                Forward = forward;
                Across = across;
                Normal = normal;
                Rotation = Quaternion.LookRotation(forward, normal);
            }

            public Vector3 Forward { get; }

            public Vector3 Across { get; }

            public Vector3 Normal { get; }

            public Quaternion Rotation { get; }

            public static PalmFrame FromLicensed(LicensedHandsRig rig)
            {
                Vector3 forward = Vector3.zero;
                for (int digit = 1; digit < rig.RightDigits.Length; digit++)
                {
                    forward +=
                        rig.RightDigits[digit][0].position -
                        rig.RightPalm.position;
                }

                Vector3 across =
                    rig.RightDigits[4][0].position -
                    rig.RightDigits[1][0].position;
                return Create(forward, across);
            }

            public static PalmFrame Create(
                Vector3 forward,
                Vector3 across)
            {
                if (forward.sqrMagnitude <= 0.000001f ||
                    across.sqrMagnitude <= 0.000001f)
                {
                    throw new InvalidOperationException(
                        "Viewmodel palm basis is degenerate.");
                }

                Vector3 acrossAxis = across.normalized;
                Vector3 forwardAxis = Vector3.ProjectOnPlane(
                    forward,
                    acrossAxis).normalized;
                Vector3 normal = Vector3.Cross(
                    forwardAxis,
                    acrossAxis).normalized;
                if (forwardAxis.sqrMagnitude <= 0.000001f ||
                    normal.sqrMagnitude <= 0.000001f)
                {
                    throw new InvalidOperationException(
                        "Viewmodel palm axes are parallel.");
                }

                acrossAxis = Vector3.Cross(normal, forwardAxis).normalized;
                return new PalmFrame(
                    forwardAxis,
                    acrossAxis,
                    normal);
            }
        }

        private enum AuthoredFingerPose
        {
            Open = 0,
            DrinkGrip = 1,
            MiddleFinger = 2,
        }

        private readonly struct AuthoredArmState
        {
            public AuthoredArmState(
                Vector3 shoulderPosition,
                Vector3 palmPosition,
                Vector3 forearmDirection,
                Vector3 palmForward,
                Vector3 palmNormal,
                AuthoredFingerPose fingerPose)
            {
                ShoulderPosition = shoulderPosition;
                PalmPosition = palmPosition;
                ForearmDirection = forearmDirection.normalized;
                PalmForward = palmForward.normalized;
                PalmNormal = Vector3.ProjectOnPlane(
                        palmNormal,
                        PalmForward)
                    .normalized;
                FingerPose = fingerPose;
            }

            public Vector3 ShoulderPosition { get; }

            public Vector3 PalmPosition { get; }

            public Vector3 ForearmDirection { get; }

            public Vector3 PalmForward { get; }

            public Vector3 PalmNormal { get; }

            public AuthoredFingerPose FingerPose { get; }

            public AuthoredArmState WithPalmPosition(Vector3 position) =>
                new AuthoredArmState(
                    ShoulderPosition,
                    position,
                    ForearmDirection,
                    PalmForward,
                    PalmNormal,
                    FingerPose);

            public AuthoredArmState WithPalmFrame(
                Vector3 forward,
                Vector3 normal) =>
                new AuthoredArmState(
                    ShoulderPosition,
                    PalmPosition,
                    ForearmDirection,
                    forward,
                    normal,
                    FingerPose);

            public AuthoredArmState WithForearmDirection(
                Vector3 direction) =>
                new AuthoredArmState(
                    ShoulderPosition,
                    PalmPosition,
                    direction,
                    PalmForward,
                    PalmNormal,
                    FingerPose);

            public static AuthoredArmState Lerp(
                AuthoredArmState from,
                AuthoredArmState to,
                float weight)
            {
                float clamped = Mathf.Clamp01(weight);
                Vector3 forward = Vector3.Slerp(
                    from.PalmForward,
                    to.PalmForward,
                    clamped).normalized;
                Vector3 normal = Vector3.Slerp(
                    from.PalmNormal,
                    to.PalmNormal,
                    clamped);
                normal = Vector3.ProjectOnPlane(normal, forward).normalized;
                Vector3 forearmDirection = Vector3.Slerp(
                    from.ForearmDirection,
                    to.ForearmDirection,
                    clamped).normalized;
                return new AuthoredArmState(
                    Vector3.Lerp(
                        from.ShoulderPosition,
                        to.ShoulderPosition,
                        clamped),
                    Vector3.Lerp(
                        from.PalmPosition,
                        to.PalmPosition,
                        clamped),
                    forearmDirection,
                    forward,
                    normal,
                    clamped < 0.5f
                        ? from.FingerPose
                        : to.FingerPose);
            }
        }

        private readonly struct AuthoredSample
        {
            public AuthoredSample(float time, PoseState pose)
            {
                Time = time;
                Pose = pose ??
                    throw new ArgumentNullException(nameof(pose));
            }

            public float Time { get; }

            public PoseState Pose { get; }
        }

        internal sealed class LicensedAction
        {
            public LicensedAction(
                GameObject root,
                Animation animation,
                LicensedHandsRig rig,
                SkinnedMeshRenderer renderer)
            {
                Root = root;
                Animation = animation;
                Rig = rig;
                Renderer = renderer;
            }

            public GameObject Root { get; }

            public Animation Animation { get; }

            public LicensedHandsRig Rig { get; }

            public SkinnedMeshRenderer Renderer { get; }
        }

        internal sealed class LicensedDrinkClips
        {
            public LicensedDrinkClips(
                AnimationClip drink,
                AnimationClip shortClip,
                AnimationClip throwClip,
                AnimationClip spray,
                Transform grip)
            {
                Drink = drink;
                Short = shortClip;
                Throw = throwClip;
                Spray = spray;
                Grip = grip;
            }

            public AnimationClip Drink { get; }

            public AnimationClip Short { get; }

            public AnimationClip Throw { get; }

            public AnimationClip Spray { get; }

            public Transform Grip { get; }
        }

        private readonly struct DonorClipAudit
        {
            public DonorClipAudit(
                AnimationClip clip,
                params float[] times)
            {
                Clip = clip ?? throw new ArgumentNullException(nameof(clip));
                Times = times ?? throw new ArgumentNullException(nameof(times));
            }

            public AnimationClip Clip { get; }

            public IReadOnlyList<float> Times { get; }
        }

        private readonly struct DonorTransformPose
        {
            public DonorTransformPose(
                Transform transform,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Transform = transform ??
                    throw new ArgumentNullException(nameof(transform));
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            private Transform Transform { get; }

            private Vector3 LocalPosition { get; }

            private Quaternion LocalRotation { get; }

            private Vector3 LocalScale { get; }

            public void Restore()
            {
                Transform.localPosition = LocalPosition;
                Transform.localRotation = LocalRotation;
                Transform.localScale = LocalScale;
            }
        }

        internal sealed class LicensedHandsRig
        {
            private readonly Quaternion[] restBoneRotations;

            public LicensedHandsRig(
                Transform actionRoot,
                Transform model)
            {
                ActionRoot = actionRoot;
                Model = model;
                RestModelScale = model.localScale;
                RightCollarbone = RequireTransform(
                    model,
                    "CATRigHub001/CATRigRArmCollarbone");
                RightUpperArm = RequireTransform(
                    model,
                    "CATRigHub001/CATRigRArmCollarbone/CATRigRArm1");
                RightForearm = RequireTransform(
                    model,
                    "CATRigHub001/CATRigRArmCollarbone/CATRigRArm1/" +
                    "CATRigRArm21");
                RightForearmTwist = RequireTransform(
                    model,
                    "CATRigHub001/CATRigRArmCollarbone/CATRigRArm1/" +
                    "CATRigRArm21/CATRigRArm22");
                RightPalm = RequireTransform(
                    model,
                    "CATRigHub001/CATRigRArmCollarbone/CATRigRArm1/" +
                    "CATRigRArm21/CATRigRArm22/CATRigRArmPalm");
                RightDigits = new Transform[5][];
                for (int digit = 1; digit <= 5; digit++)
                {
                    string basePath =
                        "CATRigHub001/CATRigRArmCollarbone/CATRigRArm1/" +
                        "CATRigRArm21/CATRigRArm22/CATRigRArmPalm/" +
                        $"CATRigRArmDigit{digit}1";
                    RightDigits[digit - 1] = new[]
                    {
                        RequireTransform(model, basePath),
                        RequireTransform(
                            model,
                            basePath + $"/CATRigRArmDigit{digit}2"),
                        RequireTransform(
                            model,
                            basePath + $"/CATRigRArmDigit{digit}2/" +
                            $"CATRigRArmDigit{digit}3"),
                    };
                }

                AnimatedBones = new[]
                    {
                        RightCollarbone,
                        RightUpperArm,
                        RightForearm,
                        RightForearmTwist,
                        RightPalm,
                    }
                    .Concat(RightDigits.SelectMany(chain => chain))
                    .ToArray();
                restBoneRotations = AnimatedBones
                    .Select(bone => bone.localRotation)
                    .ToArray();
            }

            public Transform ActionRoot { get; }

            public Transform Model { get; }

            public Vector3 RestModelScale { get; }

            public Transform RightCollarbone { get; }

            public Transform RightUpperArm { get; }

            public Transform RightForearm { get; }

            public Transform RightForearmTwist { get; }

            public Transform RightPalm { get; }

            public Transform[][] RightDigits { get; }

            public IReadOnlyList<Transform> AnimatedBones { get; }

            public void RestoreRestPose()
            {
                Model.localScale = RestModelScale;
                for (int index = 0;
                     index < AnimatedBones.Count;
                     index++)
                {
                    AnimatedBones[index].localRotation =
                        restBoneRotations[index];
                }
            }

            private static Transform RequireTransform(
                Transform root,
                string path)
            {
                Transform transform = root.Find(path);
                return transform ?? throw new InvalidOperationException(
                    $"Licensed hand rig path is missing: '{path}'.");
            }
        }

        private sealed class PoseState
        {
            private PoseState(
                Vector3 modelPosition,
                Quaternion modelRotation,
                Vector3 modelScale,
                Quaternion[] boneRotations)
            {
                ModelPosition = modelPosition;
                ModelRotation = modelRotation.normalized;
                ModelScale = modelScale;
                BoneRotations = boneRotations;
            }

            public Vector3 ModelPosition { get; }

            public Quaternion ModelRotation { get; }

            public Vector3 ModelScale { get; }

            public Quaternion[] BoneRotations { get; }

            public static PoseState Capture(LicensedHandsRig rig) =>
                new PoseState(
                    rig.Model.localPosition,
                    rig.Model.localRotation,
                    rig.Model.localScale,
                    rig.AnimatedBones
                        .Select(bone => bone.localRotation)
                        .ToArray());
        }

        private sealed class GeneratedActionReview
        {
            public GeneratedActionReview(
                string actionName,
                string clipAssetPath,
                float[] times)
            {
                ActionName = actionName;
                ClipAssetPath = clipAssetPath;
                Times = times;
            }

            public string ActionName { get; }

            public string ClipAssetPath { get; }

            public float[] Times { get; }
        }

        [Serializable]
        private sealed class PresentationAssetConfiguration
        {
            public string realisticFpsHandsUnityPackage = string.Empty;
        }

        private static void AppendTransform(
            StringBuilder report,
            Transform transform,
            string parentPath)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? transform.name
                : parentPath + "/" + transform.name;
            report.AppendLine(
                $"TRANSFORM|{path}|position={Format(transform.localPosition)}|" +
                $"rotation={Format(transform.localEulerAngles)}|" +
                $"scale={Format(transform.localScale)}|" +
                $"world={Format(transform.position)}");
            for (int index = 0; index < transform.childCount; index++)
            {
                AppendTransform(report, transform.GetChild(index), path);
            }
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root)
            {
                return root.name;
            }

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null && cursor != root)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return root.name + "/" + path;
        }

        private static string Format(Vector3 value) =>
            $"({Format(value.x)},{Format(value.y)},{Format(value.z)})";

        private static string Format(float value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
