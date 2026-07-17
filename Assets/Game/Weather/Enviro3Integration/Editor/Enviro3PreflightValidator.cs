using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Enviro;
using MSC.Development.WeatherLab;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public static class Enviro3PreflightValidator
    {
        public const int BaselineFileCount = 538;
        public const long BaselineTotalBytes = 305967931L;
        public const string BaselineFingerprint =
            "8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44";
        public const string HdrpRendererRegistration =
            "Enviro.EnviroHDRPRenderer, Enviro3.Runtime";

        private const string HdrpGlobalSettingsPath =
            "Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset";
        private const string IntegrationBoundaryPrefix =
            "Assets/Game/Weather/Enviro3Integration/";
        private const string IntegrationTestsBoundaryPrefix =
            "Assets/Game/Tests/EditMode/Enviro3Integration/";

        // The 07B baseline is the source-package prefab graph after canonical Unity
        // 6000.3.11f1 serialization. See ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md.
        // Its manifest was captured under ru-RU; pin that comparer so the fingerprint
        // remains reproducible when Unity runs under a different host culture.
        private static readonly StringComparer VendorBaselinePathComparer =
            StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: false);

        [Serializable]
        public sealed class Result
        {
            public bool passed;
            public int vendorFileCount;
            public long vendorTotalBytes;
            public string vendorFingerprint = string.Empty;
            public string[] enviroReferencingAssemblies = Array.Empty<string>();
            public string[] errors = Array.Empty<string>();
            public string[] warnings = Array.Empty<string>();
        }

        public readonly struct VendorSnapshot
        {
            public VendorSnapshot(int fileCount, long totalBytes, string fingerprint)
            {
                FileCount = fileCount;
                TotalBytes = totalBytes;
                Fingerprint = fingerprint;
            }

            public int FileCount { get; }
            public long TotalBytes { get; }
            public string Fingerprint { get; }
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Validate Full Preflight")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
        }

        public static Result Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            VendorSnapshot snapshot;
            try
            {
                snapshot = ComputeVendorSnapshot();
                if (snapshot.FileCount != BaselineFileCount ||
                    snapshot.TotalBytes != BaselineTotalBytes ||
                    !string.Equals(snapshot.Fingerprint, BaselineFingerprint, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Vendor baseline drift: expected " +
                        $"{BaselineFileCount}/{BaselineTotalBytes}/{BaselineFingerprint}, got " +
                        $"{snapshot.FileCount}/{snapshot.TotalBytes}/{snapshot.Fingerprint}.");
                }
            }
            catch (Exception exception)
            {
                snapshot = new VendorSnapshot(0, 0L, string.Empty);
                errors.Add("Vendor snapshot failed: " + exception.Message);
            }

            ValidatePackagesAndDefines(errors);
            ValidateCustomPostProcess(errors);
            ValidateGitBoundary(errors);
            string[] referencingAssemblies = FindEnviroReferencingAssemblies(errors);
            ValidateWeatherLab(errors, warnings);

            return new Result
            {
                passed = errors.Count == 0,
                vendorFileCount = snapshot.FileCount,
                vendorTotalBytes = snapshot.TotalBytes,
                vendorFingerprint = snapshot.Fingerprint,
                enviroReferencingAssemblies = referencingAssemblies,
                errors = errors.ToArray(),
                warnings = warnings.ToArray()
            };
        }

        public static Result ValidateOrThrow()
        {
            Result result = Validate();
            if (!result.passed)
            {
                throw new InvalidOperationException(
                    "Enviro 3 preflight failed:\n- " + string.Join("\n- ", result.errors));
            }

            Debug.Log(
                "M07A_ENVIRO_PREFLIGHT_OK " +
                $"vendorFiles={result.vendorFileCount} vendorBytes={result.vendorTotalBytes} " +
                $"vendorFingerprint={result.vendorFingerprint} " +
                $"integrationAssemblies={result.enviroReferencingAssemblies.Length} " +
                "weatherLab=excluded owners=single");
            return result;
        }

        public static VendorSnapshot ComputeVendorSnapshot()
        {
            string root = Path.GetFullPath(WeatherLabBuilder.VendorRoot);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(
                    "Expected Enviro vendor root is missing: " + root);
            }

            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(path => NormalizeRelative(root, path), VendorBaselinePathComparer)
                .ToArray();
            long totalBytes = 0L;
            var manifest = new StringBuilder(files.Length * 128);
            using (SHA256 sha256 = SHA256.Create())
            {
                foreach (string file in files)
                {
                    var info = new FileInfo(file);
                    totalBytes += info.Length;
                    string fileHash;
                    using (FileStream stream = File.OpenRead(file))
                    {
                        fileHash = ToHex(sha256.ComputeHash(stream));
                    }

                    manifest.Append(NormalizeRelative(root, file));
                    manifest.Append('|');
                    manifest.Append(info.Length);
                    manifest.Append('|');
                    manifest.Append(fileHash);
                    manifest.Append('\n');
                }

                byte[] manifestHash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(manifest.ToString()));
                return new VendorSnapshot(files.Length, totalBytes, ToHex(manifestHash));
            }
        }

        public static string ExportDiagnostics(string destinationPath = "Logs/M07A_Enviro3PreflightDiagnostics.json")
        {
            Result result = Validate();
            string fullPath = Path.GetFullPath(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Path.GetFullPath("Logs"));
            File.WriteAllText(fullPath, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            Debug.Log("M07A Enviro diagnostics exported: " + fullPath);
            return fullPath;
        }

        private static void ValidatePackagesAndDefines(ICollection<string> errors)
        {
            string manifest = File.ReadAllText(Path.GetFullPath("Packages/manifest.json"));
            if (!manifest.Contains(
                    "\"com.unity.render-pipelines.universal\": \"17.3.0\"",
                    StringComparison.Ordinal))
            {
                errors.Add("Official URP 17.3.0 compatibility dependency is not pinned.");
            }

            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            var defineSet = new HashSet<string>(
                defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            if (!defineSet.Contains("ENVIRO_3") || !defineSet.Contains("ENVIRO_HDRP"))
            {
                errors.Add("Standalone must define ENVIRO_3 and ENVIRO_HDRP.");
            }

            if (defineSet.Contains("ENVIRO_URP"))
            {
                errors.Add("ENVIRO_URP must remain disabled in the HDRP project.");
            }

            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
            {
                errors.Add("The active render pipeline is not HDRP.");
            }
        }

        private static void ValidateCustomPostProcess(ICollection<string> errors)
        {
            string fullPath = Path.GetFullPath(HdrpGlobalSettingsPath);
            if (!File.Exists(fullPath) ||
                !File.ReadAllText(fullPath).Contains(HdrpRendererRegistration, StringComparison.Ordinal))
            {
                errors.Add(
                    "EnviroHDRPRenderer is not registered in HDRP Before Transparent custom post processes.");
            }
        }

        private static void ValidateGitBoundary(ICollection<string> errors)
        {
            string ignorePath = Path.GetFullPath(".gitignore");
            string ignore = File.Exists(ignorePath) ? File.ReadAllText(ignorePath) : string.Empty;
            if (!ignore.Contains("/Assets/Enviro 3 - Sky and Weather/", StringComparison.Ordinal))
            {
                errors.Add("Enviro paid vendor root is not excluded by .gitignore.");
            }
        }

        private static string[] FindEnviroReferencingAssemblies(ICollection<string> errors)
        {
            string assetsRoot = Path.GetFullPath("Assets/Game");
            string[] assemblyPaths = Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("Enviro3.Runtime", StringComparison.Ordinal))
                .Select(path => NormalizeRelative(Path.GetFullPath("."), path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            foreach (string path in assemblyPaths)
            {
                if (!IsAllowedEnviroBoundaryPath(path))
                {
                    errors.Add("Enviro assembly reference escaped integration boundary: " + path);
                }
            }

            string[] sourcePaths = Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories);
            foreach (string sourcePath in sourcePaths)
            {
                string source = File.ReadAllText(sourcePath);
                bool referencesEnviro = source.Contains("using Enviro;", StringComparison.Ordinal) ||
                                        source.Contains("Enviro.", StringComparison.Ordinal);
                string relative = NormalizeRelative(Path.GetFullPath("."), sourcePath);
                if (referencesEnviro && !IsAllowedEnviroBoundaryPath(relative))
                {
                    errors.Add("Enviro source reference escaped integration boundary: " + relative);
                }
            }

            return assemblyPaths;
        }

        private static void ValidateWeatherLab(
            ICollection<string> errors,
            ICollection<string> warnings)
        {
            try
            {
                WeatherLabBuilder.AssertExcludedFromBuildSettings();
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }

            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(
                    WeatherLabBuilder.BindingsAssetPath);
            if (bindings == null || bindings.EffectsSource == null)
            {
                errors.Add("WeatherLab requires an explicit Enviro base Effects source binding.");
            }
            else if (!string.Equals(
                         AssetDatabase.GetAssetPath(bindings.EffectsSource),
                         WeatherLabBuilder.EffectsSourceAssetPath,
                         StringComparison.Ordinal))
            {
                errors.Add("WeatherLab Effects source is not the validated base Enviro effects preset.");
            }

            if (!File.Exists(Path.GetFullPath(WeatherLabSceneMarker.SceneAssetPath)))
            {
                errors.Add("WeatherLab scene has not been built.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(WeatherLabSceneMarker.SceneAssetPath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    WeatherLabSceneMarker.SceneAssetPath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                EnviroManager[] managers = GetComponents<EnviroManager>(roots);
                Enviro3EnvironmentAdapter[] adapters = GetComponents<Enviro3EnvironmentAdapter>(roots);
                WeatherLabSceneMarker[] markers = GetComponents<WeatherLabSceneMarker>(roots);
                Light[] lights = GetComponents<Light>(roots);
                Volume[] volumes = GetComponents<Volume>(roots);

                if (managers.Length != 1)
                {
                    errors.Add("WeatherLab requires exactly one EnviroManager; found " + managers.Length + ".");
                }

                if (adapters.Length != 1)
                {
                    errors.Add("WeatherLab requires exactly one Enviro3EnvironmentAdapter; found " + adapters.Length + ".");
                }

                string markerFailure = string.Empty;
                if (markers.Length != 1 || !markers[0].TryValidate(out markerFailure))
                {
                    errors.Add(markers.Length != 1
                        ? "WeatherLab requires exactly one scene marker."
                        : markerFailure);
                }

                int directionalOwners = lights.Count(light =>
                    light != null && light.enabled && light.type == LightType.Directional);
                if (directionalOwners != 1)
                {
                    errors.Add(
                        "WeatherLab requires exactly one enabled directional sun/moon owner; found " +
                        directionalOwners + ".");
                }

                int enviroSkyProfiles = 0;
                int fogProfiles = 0;
                int exposureProfiles = 0;
                int nativeCloudProfiles = 0;
                int physicalSkyProfiles = 0;
                foreach (Volume volume in volumes)
                {
                    VolumeProfile profile = volume != null ? volume.sharedProfile : null;
                    if (profile == null)
                    {
                        continue;
                    }

                    if (profile.TryGet(out EnviroHDRPSky _)) enviroSkyProfiles++;
                    if (profile.TryGet(out Fog _)) fogProfiles++;
                    if (profile.TryGet(out Exposure _)) exposureProfiles++;
                    if (profile.TryGet(out VolumetricClouds _)) nativeCloudProfiles++;
                    if (profile.TryGet(out PhysicallyBasedSky _)) physicalSkyProfiles++;
                }

                if (enviroSkyProfiles != 1 || fogProfiles != 1 || exposureProfiles != 1)
                {
                    errors.Add(
                        "WeatherLab volume ownership must be Enviro sky=1, fog=1, exposure=1; got " +
                        $"{enviroSkyProfiles}/{fogProfiles}/{exposureProfiles}.");
                }

                if (nativeCloudProfiles != 0 || physicalSkyProfiles != 0)
                {
                    errors.Add(
                        "WeatherLab contains duplicate HDRP native cloud/physical sky owners.");
                }

                if (managers.Length == 1)
                {
                    EnviroManager manager = managers[0];
                    if (manager.dontDestroyOnLoad)
                    {
                        errors.Add("WeatherLab Enviro manager must not use DontDestroyOnLoad.");
                    }

                    if (manager.volumeHDRP == null ||
                        !string.Equals(
                            AssetDatabase.GetAssetPath(manager.volumeHDRP.sharedProfile),
                            WeatherLabBuilder.VolumeProfilePath,
                            StringComparison.Ordinal))
                    {
                        errors.Add("Enviro manager is not bound to the project-owned WeatherLab HDRP profile.");
                    }
                }

                if (GetComponents<AudioListener>(roots).Length > 0)
                {
                    warnings.Add(
                        "WeatherLab contains an AudioListener; Enviro audio policy still requires zeroed module output.");
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static T[] GetComponents<T>(IEnumerable<GameObject> roots) where T : Component
        {
            return roots
                .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
                .ToArray();
        }

        private static string NormalizeRelative(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }

        private static bool IsAllowedEnviroBoundaryPath(string relativePath)
        {
            return relativePath.StartsWith(IntegrationBoundaryPrefix, StringComparison.Ordinal) ||
                   relativePath.StartsWith(
                       IntegrationTestsBoundaryPrefix,
                       StringComparison.Ordinal);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
