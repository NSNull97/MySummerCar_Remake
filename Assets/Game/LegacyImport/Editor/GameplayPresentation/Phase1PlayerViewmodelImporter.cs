using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Needs;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Deterministically builds the private Phase 1 first-person hand
    /// presentation from the locked AssetRipper export. Only the selected hand
    /// mesh, hand textures and legacy AnimationClips cross the boundary.
    /// </summary>
    public static class Phase1PlayerViewmodelImporter
    {
        private const string LocalConfigurationPath =
            "Config/DonorPaths.local.json";

        private const string ManifestAssetPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1PlayerViewmodelManifest.json";

        private const string OutputRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel";

        private const string ImportedSourceRoot = OutputRoot + "/Source";

        private const string GeneratedRoot = OutputRoot + "/Generated";

        private const string ResourcesRoot = OutputRoot +
            "/Resources/Phase1PlayerViewmodel";

        private const string PrefabAssetPath = ResourcesRoot +
            "/Phase1PlayerViewmodelBinding.prefab";

        private const string MaterialAssetPath = GeneratedRoot +
            "/Phase1PlayerHand.mat";

        private const string BuildReportAssetPath = OutputRoot +
            "/Phase1PlayerViewmodelBuildReport.json";

        private static readonly Regex MetaGuidPattern = new Regex(
            @"^guid:\s*(?<guid>[0-9a-fA-F]+)\s*$",
            RegexOptions.Compiled |
            RegexOptions.Multiline |
            RegexOptions.CultureInvariant);

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/" +
            "Build Phase 1 Player Viewmodel")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 player viewmodel",
                "The sanitized private Phase 1 player viewmodel was rebuilt.",
                "OK");
        }

        /// <summary>
        /// Batch entry point:
        /// -executeMethod
        /// MSC.LegacyImport.Editor.GameplayPresentation.
        /// Phase1PlayerViewmodelImporter.BuildFromBatch
        /// </summary>
        public static void BuildFromBatch()
        {
            Build();
        }

        public static void Build()
        {
            Phase1PlayerViewmodelManifest manifest =
                LoadAndValidateManifest();
            DonorAssetSpec[] assetSpecs = CreateAssetSpecs(manifest);
            ActionSpec[] actionSpecs = CreateActionSpecs(manifest);
            string handMeshGuid = RequireSpec(
                    assetSpecs,
                    "handMesh")
                .Guid;
            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(LocalConfigurationPath);
            string donorAssetsRoot = Path.Combine(
                configuration.DonorStagingDirectory,
                manifest.source.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string scenePath = Path.Combine(
                donorAssetsRoot,
                manifest.source.sceneRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

            ValidateLockedScene(
                scenePath,
                manifest.source.sceneSha256);
            ValidateSourceAssets(donorAssetsRoot, assetSpecs);
            ResetOutputBoundary();

            IReadOnlyDictionary<string, string> importedAssets =
                CopySourceAssets(donorAssetsRoot, assetSpecs);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ValidateImportedGuids(importedAssets, assetSpecs);
            ConfigureTextureImporters(importedAssets);

            IReadOnlyDictionary<string, AnimationClip> clips =
                LoadAndSanitizeClips(importedAssets, assetSpecs);
            Mesh handMesh = RequireAsset<Mesh>(
                importedAssets["handMesh"]);
            Texture2D handAlbedo = RequireAsset<Texture2D>(
                importedAssets["handAlbedo"]);
            Texture2D handNormal = RequireAsset<Texture2D>(
                importedAssets["handNormal"]);
            Material handMaterial = CreateHandMaterial(
                handAlbedo,
                handNormal);

            DonorUnitySceneModel scene =
                DonorUnitySceneModel.Parse(scenePath);
            Mesh licensedHandMesh = BuildPrefab(
                scene,
                manifest,
                handMesh,
                handMaterial,
                clips,
                actionSpecs,
                handMeshGuid);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedPrefab(
                handMesh,
                licensedHandMesh,
                actionSpecs);
            WriteBuildReport(
                manifest,
                scenePath,
                importedAssets,
                clips);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "Phase 1 player viewmodel build complete. " +
                $"Prefab: '{PrefabAssetPath}'. " +
                "Donor gameplay scripts, FSMs, controllers, bottle meshes " +
                "and cigarette meshes were not imported.");
        }

        public static void EnsureGeneratedForBuild()
        {
            if (!TryValidateExistingGeneratedPayload(out string reason))
            {
                throw new InvalidOperationException(
                    "Required private Phase 1 player viewmodel payload is " +
                    $"missing, stale or incomplete: {reason}. Run 'Tools > " +
                    "My Summer Car > Legacy Import > Build Phase 1 Player " +
                    "Viewmodel' before building. The build is intentionally " +
                    "blocked instead of silently shipping primitive fallback " +
                    "arms.");
            }
        }

        private static Phase1PlayerViewmodelManifest
            LoadAndValidateManifest()
        {
            string manifestPath = ToFileSystemPath(ManifestAssetPath);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    "Authoritative Phase 1 player viewmodel manifest is missing.",
                    manifestPath);
            }

            Phase1PlayerViewmodelManifest manifest =
                JsonUtility.FromJson<Phase1PlayerViewmodelManifest>(
                    File.ReadAllText(manifestPath, Encoding.UTF8));
            if (manifest == null)
            {
                throw new FormatException(
                    $"Manifest '{ManifestAssetPath}' could not be parsed.");
            }

            if (manifest.schemaVersion != 2 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.bindingId,
                    FirstPersonLifeActionViewmodelBinding.ExpectedBindingId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.productionReplacementKey,
                    FirstPersonLifeActionViewmodelBinding.
                        ExpectedReplacementKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest metadata does not match " +
                    "the reviewed runtime boundary.");
            }

            if (manifest.source == null)
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest has no source lock.");
            }

            if (manifest.drinkGripTransformFileId <= 0)
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest has no locked drink " +
                    "grip transform.");
            }

            ValidateDrinkReadyPresentation(
                manifest.drinkReadyPresentation);

            ValidateRelativeManifestPath(
                manifest.source.stagingRootRelativePath,
                "source.stagingRootRelativePath");
            ValidateRelativeManifestPath(
                manifest.source.sceneRelativePath,
                "source.sceneRelativePath");
            ValidateHex(
                manifest.source.sceneSha256,
                64,
                "source.sceneSha256");

            if (manifest.generatedOutput == null ||
                !string.Equals(
                    manifest.generatedOutput.boundary,
                    OutputRoot,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.generatedOutput.prefab,
                    PrefabAssetPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Manifest output paths do not match the fail-closed " +
                    "RuntimeBaseline allowlist.");
            }

            DonorAssetSpec[] assetSpecs = CreateAssetSpecs(manifest);
            ActionSpec[] actionSpecs = CreateActionSpecs(manifest);
            string[] requiredAssetKeys =
            {
                "handMesh",
                "handAlbedo",
                "handNormal",
                "drinkRotate",
                "drinkRotateShort",
                "drinkSpray",
                "drinkThrow",
                "smokeIn",
                "smokeLightUp",
                "smokeOut",
                "smokePutOff",
                "smokeReset",
                "hello",
                "middleFinger",
                "pushOn",
                "pushOff",
                "fist",
            };
            foreach (string key in requiredAssetKeys)
            {
                RequireSpec(assetSpecs, key);
            }

            string[] requiredActionKeys =
            {
                "Drink",
                "Smoke",
                "Hello",
                "MiddleFinger",
                "Push",
                "Fist",
            };
            foreach (string key in requiredActionKeys)
            {
                if (!actionSpecs.Any(action =>
                        string.Equals(
                            action.Key,
                            key,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Manifest is missing required action '{key}'.");
                }
            }

            if (actionSpecs.Length != requiredActionKeys.Length)
            {
                throw new InvalidOperationException(
                    "Manifest contains an unreviewed player-viewmodel action.");
            }

            return manifest;
        }

        private static DonorAssetSpec[] CreateAssetSpecs(
            Phase1PlayerViewmodelManifest manifest)
        {
            if (manifest.assets == null || manifest.assets.Length == 0)
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest contains no assets.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var specs = new DonorAssetSpec[manifest.assets.Length];
            for (int index = 0; index < manifest.assets.Length; index++)
            {
                ManifestAssetEntry entry = manifest.assets[index] ??
                    throw new InvalidOperationException(
                        $"Manifest asset entry {index} is null.");
                string key = ToIdentifier(entry.role, pascalCase: false);
                ValidateRelativeManifestPath(
                    entry.relativePath,
                    $"assets[{index}].relativePath");
                ValidateHex(entry.guid, 32, $"assets[{index}].guid");
                ValidateHex(entry.sha256, 64, $"assets[{index}].sha256");
                string destinationFolder =
                    GetDestinationFolder(entry.relativePath);
                if (!keys.Add(key) ||
                    !roles.Add(entry.role) ||
                    !paths.Add(entry.relativePath) ||
                    !guids.Add(entry.guid))
                {
                    throw new InvalidOperationException(
                        $"Manifest asset '{entry.role}' duplicates a key, " +
                        "role, path or GUID.");
                }

                specs[index] = new DonorAssetSpec(
                    key,
                    entry.relativePath,
                    destinationFolder,
                    entry.guid.ToLowerInvariant(),
                    entry.sha256.ToLowerInvariant());
            }

            return specs;
        }

        private static ActionSpec[] CreateActionSpecs(
            Phase1PlayerViewmodelManifest manifest)
        {
            if (manifest.actions == null || manifest.actions.Length == 0)
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest contains no actions.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var specs = new ActionSpec[manifest.actions.Length];
            for (int index = 0; index < manifest.actions.Length; index++)
            {
                ManifestActionEntry entry = manifest.actions[index] ??
                    throw new InvalidOperationException(
                        $"Manifest action entry {index} is null.");
                string key = ToIdentifier(entry.id, pascalCase: true);
                if (!keys.Add(key) ||
                    entry.rootTransformFileId <= 0 ||
                    entry.animationTargetTransformFileId <= 0 ||
                    entry.rendererComponentFileIds == null ||
                    entry.rendererComponentFileIds.Length == 0 ||
                    entry.rendererComponentFileIds.Any(id => id <= 0) ||
                    entry.rendererComponentFileIds.Distinct().Count() !=
                    entry.rendererComponentFileIds.Length)
                {
                    throw new InvalidOperationException(
                        $"Manifest action '{entry.id}' has duplicate or invalid " +
                        "donor file IDs.");
                }

                specs[index] = new ActionSpec(
                    key,
                    entry.rootTransformFileId,
                    entry.animationTargetTransformFileId,
                    entry.rendererComponentFileIds);
            }

            return specs;
        }

        private static DonorAssetSpec RequireSpec(
            IEnumerable<DonorAssetSpec> assetSpecs,
            string key)
        {
            DonorAssetSpec match = assetSpecs.SingleOrDefault(spec =>
                string.Equals(spec.Key, key, StringComparison.Ordinal));
            return match ?? throw new InvalidOperationException(
                $"Manifest is missing required asset role '{key}'.");
        }

        private static string GetDestinationFolder(string relativePath)
        {
            if (relativePath.StartsWith("Mesh/", StringComparison.Ordinal))
            {
                return "Mesh";
            }

            if (relativePath.StartsWith(
                    "Texture2D/",
                    StringComparison.Ordinal))
            {
                return "Textures";
            }

            if (relativePath.StartsWith(
                    "AnimationClip/",
                    StringComparison.Ordinal))
            {
                return "AnimationClips";
            }

            throw new InvalidOperationException(
                $"Manifest source type is not allowlisted: '{relativePath}'.");
        }

        private static string ToIdentifier(string value, bool pascalCase)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    "Manifest role/action identifier is empty.");
            }

            string[] parts = value.Split(
                new[] { '-' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 ||
                parts.Any(part => part.Any(character =>
                    !char.IsLetterOrDigit(character))))
            {
                throw new InvalidOperationException(
                    $"Manifest identifier '{value}' is invalid.");
            }

            var builder = new StringBuilder(value.Length);
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index].ToLowerInvariant();
                bool upperInitial = pascalCase || index > 0;
                builder.Append(
                    upperInitial
                        ? char.ToUpperInvariant(part[0])
                        : part[0]);
                if (part.Length > 1)
                {
                    builder.Append(part, 1, part.Length - 1);
                }
            }

            return builder.ToString();
        }

        private static void ValidateRelativeManifestPath(
            string value,
            string field)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Path.IsPathRooted(value.Replace(
                    '/',
                    Path.DirectorySeparatorChar)) ||
                value.Split('/').Any(segment =>
                    string.Equals(
                        segment,
                        "..",
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Manifest field '{field}' is not a safe relative path.");
            }
        }

        private static void ValidateHex(
            string value,
            int expectedLength,
            string field)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length != expectedLength ||
                value.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidOperationException(
                    $"Manifest field '{field}' must contain exactly " +
                    $"{expectedLength} hexadecimal characters.");
            }
        }

        private static void ValidateDrinkReadyPresentation(
            ManifestDrinkReadyPresentation presentation)
        {
            if (presentation == null)
            {
                throw new InvalidOperationException(
                    "Phase 1 player viewmodel manifest has no project-owned " +
                    "drink-ready presentation.");
            }

            if (!float.IsFinite(presentation.readyPoseTimeSeconds) ||
                presentation.readyPoseTimeSeconds < 0f ||
                !IsFinite(
                    presentation.readyRootLocalPositionOffset) ||
                !IsFinite(
                    presentation.readyRootLocalEulerOffset) ||
                !IsFinite(
                    presentation.entryRootLocalPositionOffset) ||
                !float.IsFinite(
                    presentation.transitionDurationSeconds) ||
                presentation.transitionDurationSeconds < 0.05f)
            {
                throw new InvalidOperationException(
                    "Project-owned drink-ready presentation values must be " +
                    "finite, use a non-negative donor sample time and a " +
                    "transition of at least 0.05 seconds.");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool TryValidateExistingGeneratedPayload(
            out string reason)
        {
            try
            {
                Phase1PlayerViewmodelManifest manifest =
                    LoadAndValidateManifest();
                DonorAssetSpec[] assetSpecs = CreateAssetSpecs(manifest);
                ActionSpec[] actionSpecs = CreateActionSpecs(manifest);
                var importedAssets = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                foreach (DonorAssetSpec spec in assetSpecs)
                {
                    string assetPath =
                        ImportedSourceRoot + "/" +
                        spec.DestinationFolder + "/" +
                        Path.GetFileName(spec.RelativePath);
                    if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                    {
                        reason = $"generated source '{assetPath}' is missing";
                        return false;
                    }

                    importedAssets.Add(spec.Key, assetPath);
                }

                ValidateImportedGuids(importedAssets, assetSpecs);
                Mesh handMesh = RequireAsset<Mesh>(
                    importedAssets["handMesh"]);
                LicensedAxisNeutralArmsImporter.
                    ValidateGeneratedSourceAndAssets();
                Mesh licensedHandMesh =
                    LicensedAxisNeutralArmsImporter.
                        RequireImportedHandMesh();
                ValidateGeneratedPrefab(
                    handMesh,
                    licensedHandMesh,
                    actionSpecs);

                string reportPath =
                    ToFileSystemPath(BuildReportAssetPath);
                if (!File.Exists(reportPath))
                {
                    reason = "generated build report is missing";
                    return false;
                }

                Phase1PlayerViewmodelBuildReport report =
                    JsonUtility.FromJson<Phase1PlayerViewmodelBuildReport>(
                        File.ReadAllText(reportPath, Encoding.UTF8));
                string manifestSha256 = ComputeSha256(
                    ToFileSystemPath(ManifestAssetPath));
                if (report == null ||
                    report.schemaVersion != manifest.schemaVersion ||
                    !string.Equals(
                        report.manifestId,
                        manifest.manifestId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.sourceManifestSha256,
                        manifestSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        report.sourceSceneSha256,
                        manifest.source.sceneSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        report.bindingId,
                        manifest.bindingId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.productionReplacementKey,
                        manifest.productionReplacementKey,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.classification,
                        manifest.classification,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.generatedPrefab,
                        PrefabAssetPath,
                        StringComparison.Ordinal) ||
                    report.importedAssetCount != assetSpecs.Length ||
                    report.clipCount != assetSpecs.Count(spec =>
                        spec.RelativePath.EndsWith(
                            ".anim",
                            StringComparison.OrdinalIgnoreCase)) ||
                    report.licensedSourceAssetCount !=
                        LicensedAxisNeutralArmsImporter.
                            LicensedSourceAssetCount ||
                    !string.Equals(
                        report.licensedPackageSha256,
                        LicensedAxisNeutralArmsImporter.SourceBlendSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !report.excludesDonorScripts ||
                    !report.excludesDonorFsm ||
                    !report.excludesDonorControllers ||
                    !report.excludesDonorBottleAndCigaretteGeometry)
                {
                    reason =
                        "generated build report does not match the current " +
                        "authoritative manifest";
                    return false;
                }

                ValidateGeneratedAssetHashes(
                    report.generatedAssetHashes,
                    GetHashedGeneratedAssetPaths(importedAssets));
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static void ValidateLockedScene(
            string scenePath,
            string expectedSceneSha256)
        {
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    "Locked donor scene is missing.",
                    scenePath);
            }

            string actualHash = ComputeSha256(scenePath);
            if (!string.Equals(
                    actualHash,
                    expectedSceneSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Locked donor scene hash mismatch. Expected " +
                    $"{expectedSceneSha256}, got {actualHash}. " +
                    "Do not silently rebuild from a different donor version.");
            }
        }

        private static void ValidateSourceAssets(
            string donorAssetsRoot,
            IReadOnlyList<DonorAssetSpec> assetSpecs)
        {
            foreach (DonorAssetSpec spec in assetSpecs)
            {
                string sourcePath = ToSourcePath(
                    donorAssetsRoot,
                    spec.RelativePath);
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException(
                        $"Required donor source '{spec.Key}' is missing.",
                        sourcePath);
                }

                string metaPath = sourcePath + ".meta";
                if (!File.Exists(metaPath))
                {
                    throw new FileNotFoundException(
                        $"Required donor meta for '{spec.Key}' is missing.",
                        metaPath);
                }

                string actualHash = ComputeSha256(sourcePath);
                if (!string.Equals(
                        actualHash,
                        spec.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Hash mismatch for donor source '{spec.Key}'. " +
                        $"Expected {spec.Sha256}, got {actualHash}.");
                }

                string actualGuid = ReadMetaGuid(metaPath);
                if (!string.Equals(
                        actualGuid,
                        spec.Guid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"GUID mismatch for donor source '{spec.Key}'. " +
                        $"Expected {spec.Guid}, got {actualGuid}.");
                }
            }
        }

        private static void ResetOutputBoundary()
        {
            if (!OutputRoot.StartsWith(
                    "Assets/Game/LegacyImport/RuntimeBaseline/",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to reset a path outside the ignored runtime " +
                    "baseline boundary.");
            }

            string outputPath = ToFileSystemPath(OutputRoot);
            string allowedRoot = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Game",
                    "LegacyImport",
                    "RuntimeBaseline"))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(
                    allowedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Resolved output '{outputPath}' is outside the allowed " +
                    $"runtime baseline root '{allowedRoot}'.");
            }

            if (AssetDatabase.IsValidFolder(OutputRoot) &&
                !AssetDatabase.DeleteAsset(OutputRoot))
            {
                throw new IOException(
                    $"Could not reset generated output '{OutputRoot}'.");
            }

            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }

            string outputMetaPath = outputPath + ".meta";
            if (File.Exists(outputMetaPath))
            {
                File.Delete(outputMetaPath);
            }

            Directory.CreateDirectory(ToFileSystemPath(ImportedSourceRoot));
            Directory.CreateDirectory(ToFileSystemPath(GeneratedRoot));
            Directory.CreateDirectory(ToFileSystemPath(ResourcesRoot));
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static IReadOnlyDictionary<string, string> CopySourceAssets(
            string donorAssetsRoot,
            IReadOnlyList<DonorAssetSpec> assetSpecs)
        {
            var imported = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (DonorAssetSpec spec in assetSpecs)
            {
                string sourcePath = ToSourcePath(
                    donorAssetsRoot,
                    spec.RelativePath);
                string destinationAssetPath =
                    ImportedSourceRoot + "/" +
                    spec.DestinationFolder + "/" +
                    Path.GetFileName(spec.RelativePath);
                string destinationPath =
                    ToFileSystemPath(destinationAssetPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(destinationPath) ??
                    throw new InvalidOperationException(
                        "Generated destination has no parent directory."));

                string conflictingAssetPath =
                    AssetDatabase.GUIDToAssetPath(spec.Guid);
                if (!string.IsNullOrEmpty(conflictingAssetPath) &&
                    !conflictingAssetPath.StartsWith(
                        OutputRoot + "/",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Donor GUID {spec.Guid} for '{spec.Key}' conflicts " +
                        $"with project asset '{conflictingAssetPath}'.");
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                File.Copy(
                    sourcePath + ".meta",
                    destinationPath + ".meta",
                    overwrite: true);
                imported.Add(spec.Key, destinationAssetPath);
            }

            return imported;
        }

        private static void ValidateImportedGuids(
            IReadOnlyDictionary<string, string> importedAssets,
            IReadOnlyList<DonorAssetSpec> assetSpecs)
        {
            foreach (DonorAssetSpec spec in assetSpecs)
            {
                string assetPath = importedAssets[spec.Key];
                string actualGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.Equals(
                        actualGuid,
                        spec.Guid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Imported GUID mismatch for '{spec.Key}' at " +
                        $"'{assetPath}'. Expected {spec.Guid}, got " +
                        $"{actualGuid}.");
                }
            }
        }

        private static void ConfigureTextureImporters(
            IReadOnlyDictionary<string, string> importedAssets)
        {
            ConfigureTexture(
                importedAssets["handAlbedo"],
                TextureImporterType.Default,
                sRgb: true);
            ConfigureTexture(
                importedAssets["handNormal"],
                TextureImporterType.NormalMap,
                sRgb: false);
        }

        private static void ConfigureTexture(
            string assetPath,
            TextureImporterType textureType,
            bool sRgb)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is
                    TextureImporter importer))
            {
                throw new InvalidOperationException(
                    $"Texture importer is unavailable for '{assetPath}'.");
            }

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        private static IReadOnlyDictionary<string, AnimationClip>
            LoadAndSanitizeClips(
                IReadOnlyDictionary<string, string> importedAssets,
                IReadOnlyList<DonorAssetSpec> assetSpecs)
        {
            var clips = new Dictionary<string, AnimationClip>(
                StringComparer.Ordinal);
            foreach (DonorAssetSpec spec in assetSpecs)
            {
                if (!spec.RelativePath.EndsWith(
                        ".anim",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath = importedAssets[spec.Key];
                AnimationClip clip = RequireAsset<AnimationClip>(assetPath);
                clip.legacy = true;
                AnimationUtility.SetAnimationEvents(
                    clip,
                    Array.Empty<AnimationEvent>());

                float maxKeyTime = GetMaxKeyTime(clip);
                if (maxKeyTime <= 0f)
                {
                    // player_smoking_reset is an intentional one-key reset
                    // pose: every curve key is authored at t=0. Preserve it
                    // as one frame instead of rejecting it or trusting the
                    // misleading extracted one-second stop time.
                    bool isStaticResetPose =
                        string.Equals(
                            spec.Key,
                            "smokeReset",
                            StringComparison.Ordinal) &&
                        AnimationUtility.GetCurveBindings(clip).Length > 0;
                    if (!isStaticResetPose)
                    {
                        throw new InvalidOperationException(
                            $"Legacy clip '{clip.name}' has no positive key time.");
                    }

                    maxKeyTime =
                        1f / Mathf.Max(1f, clip.frameRate);
                }

                ApplyClipTimeSettings(clip, maxKeyTime);
                EditorUtility.SetDirty(clip);
                clips.Add(spec.Key, clip);
            }

            AssetDatabase.SaveAssets();
            foreach (DonorAssetSpec spec in assetSpecs)
            {
                if (!spec.RelativePath.EndsWith(
                        ".anim",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath = importedAssets[spec.Key];
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                clips[spec.Key] = RequireAsset<AnimationClip>(assetPath);
            }

            foreach (KeyValuePair<string, AnimationClip> entry in clips)
            {
                if (!entry.Value.legacy ||
                    AnimationUtility.GetAnimationEvents(entry.Value).Length !=
                    0)
                {
                    throw new InvalidOperationException(
                        $"Sanitized clip '{entry.Key}' retained non-legacy " +
                        "state or animation events.");
                }
            }

            if (GetMaxKeyTime(clips["drinkRotate"]) < 5.8f ||
                clips["drinkRotate"].length < 5.8f)
            {
                throw new InvalidOperationException(
                    "Sanitized drink_rotate duration is too short. " +
                    "The donor YAML m_StopTime=1 must not truncate its " +
                    "approximately 5.87 second curve data.");
            }

            return clips;
        }

        private static float GetMaxKeyTime(AnimationClip clip)
        {
            float maximum = 0f;
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve =
                    AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null && curve.length > 0)
                {
                    maximum = Mathf.Max(
                        maximum,
                        curve.keys[curve.length - 1].time);
                }
            }

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys != null && keys.Length > 0)
                {
                    maximum = Mathf.Max(
                        maximum,
                        keys[keys.Length - 1].time);
                }
            }

            return maximum;
        }

        private static void ApplyClipTimeSettings(
            AnimationClip clip,
            float stopTime)
        {
            var serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty(
                "m_AnimationClipSettings");
            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"Animation clip settings are unavailable for '{clip.name}'.");
            }

            SerializedProperty start =
                settings.FindPropertyRelative("m_StartTime");
            SerializedProperty stop =
                settings.FindPropertyRelative("m_StopTime");
            SerializedProperty loop =
                settings.FindPropertyRelative("m_LoopTime");
            if (start == null || stop == null || loop == null)
            {
                throw new InvalidOperationException(
                    $"Animation clip time fields are unavailable for " +
                    $"'{clip.name}'.");
            }

            start.floatValue = 0f;
            stop.floatValue = stopTime;
            loop.boolValue = false;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateHandMaterial(
            Texture2D albedo,
            Texture2D normal)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable. The Phase 1 viewmodel " +
                    "builder requires the established HDRP project.");
            }

            var material = new Material(shader)
            {
                name = "Phase1 Legacy Player Hand",
                enableInstancing = true,
            };
            material.SetTexture("_BaseColorMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_NormalMap", normal);
            material.SetFloat("_NormalScale", 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.28f);
            material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
            return material;
        }

        private static Mesh BuildPrefab(
            DonorUnitySceneModel scene,
            Phase1PlayerViewmodelManifest manifest,
            Mesh handMesh,
            Material handMaterial,
            IReadOnlyDictionary<string, AnimationClip> clips,
            IReadOnlyList<ActionSpec> actionSpecs,
            string handMeshGuid)
        {
            var prefabRoot = new GameObject(
                "Phase 1 Legacy Player Viewmodel Binding");
            Mesh licensedHandMesh = null;
            try
            {
                FirstPersonLifeActionViewmodelBinding binding =
                    prefabRoot.AddComponent<
                        FirstPersonLifeActionViewmodelBinding>();
                binding.ConfigureMetadata(
                    FirstPersonLifeActionViewmodelBinding.ExpectedBindingId,
                    FirstPersonLifeActionViewmodelBinding.
                        ExpectedReplacementKey);

                var built = new Dictionary<string, BuiltAction>(
                    StringComparer.Ordinal);
                foreach (ActionSpec action in actionSpecs)
                {
                    DonorActionSlice slice = scene.CreateHandActionSlice(
                        action.RootTransformId,
                        action.AnimationTargetTransformId,
                        handMeshGuid,
                        action.RendererComponentIds);
                    built.Add(
                        action.Key,
                        BuildAction(
                            scene,
                            prefabRoot.transform,
                            action,
                            slice,
                            handMesh,
                            handMaterial));
                }

                Transform donorDrinkGrip = CreateDonorReferenceTransform(
                    scene,
                    built["Drink"],
                    manifest.drinkGripTransformFileId,
                    "Donor Drink Grip Reference");
                // Preserve the donor grip transform as read-only audit evidence.
                // The replacement AXIS presentation is authored independently
                // and never consumes donor joint or grip trajectories.
                _ = donorDrinkGrip;
                LicensedAxisNeutralArmsImporter.AxisHandsBuildResult
                    licensedHands =
                        LicensedAxisNeutralArmsImporter.
                            BuildViewmodelActions(prefabRoot.transform);
                licensedHandMesh = licensedHands.HandMesh;
                ReplaceAction(
                    built,
                    "Drink",
                    licensedHands.DrinkRoot,
                    licensedHands.DrinkAnimation);
                ReplaceAction(
                    built,
                    "Hello",
                    licensedHands.WaveRoot,
                    licensedHands.WaveAnimation);
                ReplaceAction(
                    built,
                    "MiddleFinger",
                    licensedHands.MiddleFingerRoot,
                    licensedHands.MiddleFingerAnimation);
                ReplaceAction(
                    built,
                    "Smoke",
                    licensedHands.SmokeRoot,
                    licensedHands.SmokeAnimation);
                ReplaceAction(
                    built,
                    "Push",
                    licensedHands.PushRoot,
                    licensedHands.PushAnimation);
                ReplaceAction(
                    built,
                    "Fist",
                    licensedHands.ThumbUpRoot,
                    licensedHands.ThumbUpAnimation);

                binding.ConfigureDrink(
                    built["Drink"].Root,
                    built["Drink"].Animation,
                    licensedHands.DrinkClip,
                    licensedHands.DrinkShortClip,
                    licensedHands.DrinkThrowClip,
                    licensedHands.DrinkSprayClip,
                    licensedHands.DrinkGripAnchor);
                binding.ConfigureDrinkReadyPresentation(
                    // The replacement owns its authored ready sample
                    // independently of donor root offsets.
                    LicensedAxisNeutralArmsImporter.
                        DrinkReadyPoseTimeSeconds,
                    Vector3.zero,
                    Vector3.zero,
                    manifest.drinkReadyPresentation.
                        entryRootLocalPositionOffset,
                    manifest.drinkReadyPresentation.
                        transitionDurationSeconds);
                binding.ConfigureSmoke(
                    built["Smoke"].Root,
                    built["Smoke"].Animation,
                    licensedHands.SmokeInClip,
                    licensedHands.SmokeLightUpClip,
                    licensedHands.SmokeOutClip,
                    licensedHands.SmokePutOffClip,
                    licensedHands.SmokeResetClip,
                    licensedHands.CigaretteGripAnchor);
                binding.ConfigureHello(
                    built["Hello"].Root,
                    built["Hello"].Animation,
                    licensedHands.WaveClip);
                binding.ConfigureMiddleFinger(
                    built["MiddleFinger"].Root,
                    built["MiddleFinger"].Animation,
                    licensedHands.MiddleFingerClip);
                binding.ConfigurePush(
                    built["Push"].Root,
                    built["Push"].Animation,
                    licensedHands.PushOnClip,
                    licensedHands.PushOffClip);
                binding.ConfigureFist(
                    built["Fist"].Root,
                    built["Fist"].Animation,
                    licensedHands.ThumbUpClip);

                ValidateBoundClipPaths(
                    built["Drink"],
                    licensedHands.DrinkClip,
                    licensedHands.DrinkShortClip,
                    licensedHands.DrinkThrowClip,
                    licensedHands.DrinkSprayClip);
                ValidateBoundClipPaths(
                    built["Smoke"],
                    licensedHands.SmokeInClip,
                    licensedHands.SmokeLightUpClip,
                    licensedHands.SmokeOutClip,
                    licensedHands.SmokePutOffClip,
                    licensedHands.SmokeResetClip);
                ValidateBoundClipPaths(
                    built["Hello"],
                    licensedHands.WaveClip);
                ValidateBoundClipPaths(
                    built["MiddleFinger"],
                    licensedHands.MiddleFingerClip);
                ValidateBoundClipPaths(
                    built["Push"],
                    licensedHands.PushOnClip,
                    licensedHands.PushOffClip);
                ValidateBoundClipPaths(
                    built["Fist"],
                    licensedHands.ThumbUpClip);

                binding.Stop();
                EditorUtility.SetDirty(binding);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    PrefabAssetPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to save generated prefab '{PrefabAssetPath}'.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }

            return licensedHandMesh ?? throw new InvalidOperationException(
                "Licensed FPS-hands mesh was not generated.");
        }

        private static void ReplaceAction(
            IDictionary<string, BuiltAction> built,
            string key,
            GameObject root,
            Animation animation)
        {
            if (!built.TryGetValue(key, out BuiltAction previous))
            {
                throw new InvalidOperationException(
                    $"Cannot replace unknown viewmodel action '{key}'.");
            }

            UnityEngine.Object.DestroyImmediate(previous.Root);
            built[key] = new BuiltAction(root, animation);
        }

        private static BuiltAction BuildAction(
            DonorUnitySceneModel scene,
            Transform prefabRoot,
            ActionSpec action,
            DonorActionSlice slice,
            Mesh handMesh,
            Material handMaterial)
        {
            var created = new Dictionary<long, Transform>();
            foreach (DonorTransformRecord source in slice.Transforms)
            {
                Transform parent = created.TryGetValue(
                    source.FatherTransformId,
                    out Transform createdParent)
                    ? createdParent
                    : prefabRoot;
                var gameObject = new GameObject(
                    scene.GetGameObjectName(source.GameObjectId));
                Transform transform = gameObject.transform;
                transform.SetParent(parent, false);
                transform.localPosition = source.LocalPosition;
                transform.localRotation = source.LocalRotation;
                transform.localScale = source.LocalScale;
                created.Add(source.TransformId, transform);
            }

            foreach (DonorSkinnedRendererRecord sourceRenderer in
                     slice.Renderers)
            {
                DonorTransformRecord rendererTransformRecord =
                    slice.Transforms.First(record =>
                        record.GameObjectId ==
                        sourceRenderer.GameObjectId);
                Transform rendererTransform =
                    created[rendererTransformRecord.TransformId];
                var renderer =
                    rendererTransform.gameObject.AddComponent<
                        SkinnedMeshRenderer>();
                renderer.sharedMesh = handMesh;
                renderer.sharedMaterial = handMaterial;
                renderer.rootBone = RequireCreatedTransform(
                    created,
                    sourceRenderer.RootBoneTransformId,
                    action.Key);
                renderer.bones = sourceRenderer.BoneTransformIds
                    .Select(boneTransformId =>
                        RequireCreatedTransform(
                            created,
                            boneTransformId,
                            action.Key))
                    .ToArray();
                renderer.localBounds = sourceRenderer.LocalBounds;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.updateWhenOffscreen = false;
                renderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.Object;
            }

            GameObject actionRoot =
                created[slice.RootTransformId].gameObject;
            Transform animationTarget =
                created[slice.AnimationTargetTransformId];
            Animation animation =
                animationTarget.gameObject.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.animatePhysics = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            return new BuiltAction(actionRoot, animation, created);
        }

        private static Transform CreateDonorReferenceTransform(
            DonorUnitySceneModel scene,
            BuiltAction action,
            long transformId,
            string fallbackName)
        {
            DonorTransformRecord source = scene.GetTransform(transformId);
            Transform parent = action.RequireTransform(
                source.FatherTransformId);
            var referenceObject = new GameObject(
                string.IsNullOrWhiteSpace(fallbackName)
                    ? scene.GetGameObjectName(source.GameObjectId)
                    : fallbackName);
            Transform reference = referenceObject.transform;
            reference.SetParent(parent, false);
            reference.localPosition = source.LocalPosition;
            reference.localRotation = source.LocalRotation;
            reference.localScale = source.LocalScale;
            return reference;
        }

        private static Transform RequireCreatedTransform(
            IReadOnlyDictionary<long, Transform> created,
            long transformId,
            string actionKey)
        {
            if (transformId == 0 ||
                !created.TryGetValue(transformId, out Transform transform))
            {
                throw new InvalidOperationException(
                    $"Action '{actionKey}' is missing required donor bone " +
                    $"transform {transformId}.");
            }

            return transform;
        }

        private static void ValidateBoundClipPaths(
            BuiltAction action,
            params AnimationClip[] clips)
        {
            foreach (AnimationClip clip in clips)
            {
                foreach (EditorCurveBinding binding in
                         AnimationUtility.GetCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(binding.path) &&
                        action.Animation.transform.Find(binding.path) == null)
                    {
                        throw new InvalidOperationException(
                            $"Sanitized action '{action.Root.name}' cannot " +
                            $"resolve path '{binding.path}' required by clip " +
                            $"'{clip.name}'.");
                    }
                }

                foreach (EditorCurveBinding binding in
                         AnimationUtility.
                             GetObjectReferenceCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(binding.path) &&
                        action.Animation.transform.Find(binding.path) == null)
                    {
                        throw new InvalidOperationException(
                            $"Sanitized action '{action.Root.name}' cannot " +
                            $"resolve object curve path '{binding.path}' " +
                            $"required by clip '{clip.name}'.");
                    }
                }
            }
        }

        private static void WriteBuildReport(
            Phase1PlayerViewmodelManifest manifest,
            string scenePath,
            IReadOnlyDictionary<string, string> importedAssets,
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var report = new Phase1PlayerViewmodelBuildReport
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                bindingId =
                    manifest.bindingId,
                productionReplacementKey =
                    manifest.productionReplacementKey,
                classification = manifest.classification,
                sourceManifestSha256 = ComputeSha256(
                    ToFileSystemPath(ManifestAssetPath)),
                sourceSceneSha256 = ComputeSha256(scenePath),
                generatedPrefab = PrefabAssetPath,
                importedAssetCount = importedAssets.Count,
                clipCount = clips.Count,
                licensedSourceAssetCount =
                    LicensedAxisNeutralArmsImporter.
                        LicensedSourceAssetCount,
                licensedPackageSha256 =
                    LicensedAxisNeutralArmsImporter.SourceBlendSha256,
                generatedAssetHashes = CaptureGeneratedAssetHashes(
                    GetHashedGeneratedAssetPaths(importedAssets)),
                excludesDonorScripts = true,
                excludesDonorFsm = true,
                excludesDonorControllers = true,
                excludesDonorBottleAndCigaretteGeometry = true,
            };
            File.WriteAllText(
                ToFileSystemPath(BuildReportAssetPath),
                JsonUtility.ToJson(report, prettyPrint: true) +
                Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string[] GetHashedGeneratedAssetPaths(
            IReadOnlyDictionary<string, string> importedAssets)
        {
            return importedAssets.Values
                .Concat(new[]
                {
                    MaterialAssetPath,
                    PrefabAssetPath,
                })
                .Concat(
                    LicensedAxisNeutralArmsImporter.HashedAssetPaths)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static GeneratedAssetHash[] CaptureGeneratedAssetHashes(
            IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Select(assetPath =>
                {
                    string filePath = ToFileSystemPath(assetPath);
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException(
                            "Generated payload file is missing while writing " +
                            "the build report.",
                            filePath);
                    }

                    return new GeneratedAssetHash
                    {
                        assetPath = assetPath,
                        sha256 = ComputeSha256(filePath),
                    };
                })
                .ToArray();
        }

        private static void ValidateGeneratedAssetHashes(
            IReadOnlyList<GeneratedAssetHash> recordedHashes,
            IReadOnlyList<string> expectedAssetPaths)
        {
            if (recordedHashes == null ||
                recordedHashes.Count != expectedAssetPaths.Count)
            {
                throw new InvalidOperationException(
                    "Generated payload hash inventory is missing or incomplete.");
            }

            var recorded = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (GeneratedAssetHash entry in recordedHashes)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.assetPath) ||
                    !recorded.TryAdd(entry.assetPath, entry.sha256))
                {
                    throw new InvalidOperationException(
                        "Generated payload hash inventory contains an invalid " +
                        "or duplicate path.");
                }

                ValidateHex(
                    entry.sha256,
                    64,
                    $"generatedAssetHashes[{entry.assetPath}]");
            }

            foreach (string assetPath in expectedAssetPaths)
            {
                if (!recorded.TryGetValue(
                        assetPath,
                        out string expectedSha256))
                {
                    throw new InvalidOperationException(
                        $"Generated payload hash is missing for '{assetPath}'.");
                }

                string filePath = ToFileSystemPath(assetPath);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(
                        "Generated payload file is missing.",
                        filePath);
                }

                string actualSha256 = ComputeSha256(filePath);
                if (!string.Equals(
                        actualSha256,
                        expectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated payload hash mismatch for '{assetPath}'. " +
                        "Rebuild the Phase 1 player viewmodel.");
                }
            }
        }

        private static void ValidateGeneratedPrefab(
            Mesh expectedHandMesh,
            Mesh expectedLicensedHandMesh,
            IReadOnlyList<ActionSpec> actionSpecs)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Generated prefab '{PrefabAssetPath}' is unavailable.");
            }

            if (!prefab.TryGetComponent(
                    out FirstPersonLifeActionViewmodelBinding binding) ||
                !binding.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Generated viewmodel wrapper is not configured.");
            }

            SkinnedMeshRenderer[] renderers =
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // Every action now owns one side-filtered licensed AXIS renderer.
            // The old donor Push slice happened to contain two renderers; that
            // implementation detail must not leak into the replacement count.
            int expectedRendererCount = actionSpecs.Count;
            if (renderers.Length != expectedRendererCount)
            {
                throw new InvalidOperationException(
                    $"Generated viewmodel must contain exactly " +
                    $"{expectedRendererCount} sanitized hand renderers; found " +
                    $"{renderers.Length}.");
            }

            int licensedRendererCount = 0;
            Mesh expectedLicensedLeftHandMesh =
                LicensedAxisNeutralArmsImporter.
                    RequireImportedLeftHandMesh();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer.sharedMesh == expectedLicensedHandMesh ||
                    renderer.sharedMesh == expectedLicensedLeftHandMesh)
                {
                    licensedRendererCount++;
                    continue;
                }

                if (renderer.sharedMesh != expectedHandMesh)
                {
                    throw new InvalidOperationException(
                        $"Generated renderer '{renderer.name}' uses an " +
                        "unexpected mesh.");
                }
            }

            if (licensedRendererCount != actionSpecs.Count)
            {
                throw new InvalidOperationException(
                    "Generated viewmodel must use licensed AXIS Neutral Arms " +
                    "for every bounded action; expected " +
                    $"{actionSpecs.Count}, found {licensedRendererCount}.");
            }

            MonoBehaviour[] behaviours =
                prefab.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours.Length != 1 || behaviours[0] != binding)
            {
                throw new InvalidOperationException(
                    "Generated viewmodel contains an unauthorized " +
                    "MonoBehaviour. Only the project-owned wrapper is allowed.");
            }

            if (prefab.GetComponentInChildren<Animator>(true) != null)
            {
                throw new InvalidOperationException(
                    "Generated viewmodel must not contain a donor Animator " +
                    "or AnimatorController.");
            }
        }

        private static T RequireAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required imported {typeof(T).Name} is unavailable at " +
                    $"'{assetPath}'.");
            }

            return asset;
        }

        private static string ToSourcePath(
            string donorAssetsRoot,
            string relativePath)
        {
            return Path.Combine(
                donorAssetsRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }

        private static string ToFileSystemPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ??
                throw new InvalidOperationException(
                    "Unity project root could not be resolved.");
            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static string ReadMetaGuid(string metaPath)
        {
            Match match = MetaGuidPattern.Match(File.ReadAllText(metaPath));
            if (!match.Success)
            {
                throw new FormatException(
                    $"Unity meta GUID is missing from '{metaPath}'.");
            }

            return match.Groups["guid"].Value.ToLowerInvariant();
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(
                        value.ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private sealed class DonorAssetSpec
        {
            public DonorAssetSpec(
                string key,
                string relativePath,
                string destinationFolder,
                string guid,
                string sha256)
            {
                Key = key;
                RelativePath = relativePath;
                DestinationFolder = destinationFolder;
                Guid = guid;
                Sha256 = sha256;
            }

            public string Key { get; }

            public string RelativePath { get; }

            public string DestinationFolder { get; }

            public string Guid { get; }

            public string Sha256 { get; }
        }

        private sealed class ActionSpec
        {
            public ActionSpec(
                string key,
                long rootTransformId,
                long animationTargetTransformId,
                params long[] rendererComponentIds)
            {
                Key = key;
                RootTransformId = rootTransformId;
                AnimationTargetTransformId =
                    animationTargetTransformId;
                RendererComponentIds = rendererComponentIds ??
                    throw new ArgumentNullException(
                        nameof(rendererComponentIds));
                if (RendererComponentIds.Count == 0)
                {
                    throw new ArgumentException(
                        "At least one renderer component ID is required.",
                        nameof(rendererComponentIds));
                }
            }

            public string Key { get; }

            public long RootTransformId { get; }

            public long AnimationTargetTransformId { get; }

            public IReadOnlyList<long> RendererComponentIds { get; }
        }

        private sealed class BuiltAction
        {
            private readonly IReadOnlyDictionary<long, Transform> transforms;

            public BuiltAction(
                GameObject root,
                Animation animation,
                IReadOnlyDictionary<long, Transform> transforms = null)
            {
                Root = root;
                Animation = animation;
                this.transforms = transforms;
            }

            public GameObject Root { get; }

            public Animation Animation { get; }

            public Transform RequireTransform(long transformId)
            {
                if (transforms == null ||
                    !transforms.TryGetValue(transformId, out Transform value))
                {
                    throw new InvalidOperationException(
                        $"Built action '{Root.name}' does not contain donor " +
                        $"transform {transformId}.");
                }

                return value;
            }
        }

        [Serializable]
        private sealed class Phase1PlayerViewmodelManifest
        {
            public int schemaVersion;
            public string manifestId = string.Empty;
            public string classification = string.Empty;
            public string bindingId = string.Empty;
            public string productionReplacementKey = string.Empty;
            public long drinkGripTransformFileId;
            public ManifestDrinkReadyPresentation
                drinkReadyPresentation;
            public ManifestSource source;
            public ManifestGeneratedOutput generatedOutput;
            public ManifestActionEntry[] actions =
                Array.Empty<ManifestActionEntry>();
            public ManifestAssetEntry[] assets =
                Array.Empty<ManifestAssetEntry>();
        }

        [Serializable]
        private sealed class ManifestDrinkReadyPresentation
        {
            public float readyPoseTimeSeconds;
            public Vector3 readyRootLocalPositionOffset;
            public Vector3 readyRootLocalEulerOffset;
            public Vector3 entryRootLocalPositionOffset;
            public float transitionDurationSeconds;
        }

        [Serializable]
        private sealed class ManifestSource
        {
            public string donorVersionAuthority = string.Empty;
            public string stagingRootRelativePath = string.Empty;
            public string sceneRelativePath = string.Empty;
            public string sceneSha256 = string.Empty;
        }

        [Serializable]
        private sealed class ManifestGeneratedOutput
        {
            public string boundary = string.Empty;
            public string prefab = string.Empty;
            public string gitPolicy = string.Empty;
        }

        [Serializable]
        private sealed class ManifestActionEntry
        {
            public string id = string.Empty;
            public long rootTransformFileId;
            public long animationTargetTransformFileId;
            public long[] rendererComponentFileIds = Array.Empty<long>();
        }

        [Serializable]
        private sealed class ManifestAssetEntry
        {
            public string role = string.Empty;
            public string relativePath = string.Empty;
            public string guid = string.Empty;
            public string sha256 = string.Empty;
        }

        [Serializable]
        private sealed class GeneratedAssetHash
        {
            public string assetPath = string.Empty;
            public string sha256 = string.Empty;
        }

        [Serializable]
        private sealed class Phase1PlayerViewmodelBuildReport
        {
            public int schemaVersion;
            public string manifestId = string.Empty;
            public string bindingId = string.Empty;
            public string productionReplacementKey = string.Empty;
            public string classification = string.Empty;
            public string sourceManifestSha256 = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public string generatedPrefab = string.Empty;
            public int importedAssetCount;
            public int clipCount;
            public int licensedSourceAssetCount;
            public string licensedPackageSha256 = string.Empty;
            public GeneratedAssetHash[] generatedAssetHashes =
                Array.Empty<GeneratedAssetHash>();
            public bool excludesDonorScripts;
            public bool excludesDonorFsm;
            public bool excludesDonorControllers;
            public bool excludesDonorBottleAndCigaretteGeometry;
        }
    }

    /// <summary>
    /// Fails player builds closed when the private ignored Phase 1 payload has
    /// not been generated from the current reviewed manifest. Import is kept
    /// outside the build callback because it performs synchronous AssetDatabase
    /// refreshes and deliberately resets its generated boundary.
    /// </summary>
    public sealed class Phase1PlayerViewmodelBuildGuard :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                Phase1PlayerViewmodelImporter.EnsureGeneratedForBuild();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "Mandatory Phase 1 player viewmodel validation failed. " +
                    exception.Message);
            }
        }
    }
}
