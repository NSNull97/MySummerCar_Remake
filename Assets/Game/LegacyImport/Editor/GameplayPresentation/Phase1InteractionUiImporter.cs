using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Rebuilds the one hash-locked donor HUD icon permitted by the private
    /// Phase 1 presentation baseline. Gameplay and interaction logic remain
    /// project-owned; only the open-palm pixels are copied.
    /// </summary>
    public static class Phase1InteractionUiImporter
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1UiInteractionPresentationManifest.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/Build Phase 1 Interaction UI")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 interaction UI",
                "The hash-locked donor pickup-hand icon was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static void Build()
        {
            Manifest manifest = LoadManifest();
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string sourcePath = ResolveContainedPath(
                paths.DonorStagingDirectory,
                manifest.source.stagingRelativePath,
                "donor staging");
            RequireHash(sourcePath, manifest.source.sha256);

            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");
            string destinationPath = ResolveContainedPath(
                projectRoot,
                manifest.generatedOutput.path,
                "Unity project");
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath) ?? projectRoot);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            WriteDeterministicMeta(
                destinationPath + ".meta",
                manifest.generatedOutput.guid,
                manifest.productionReplacementKey);
            WriteProvenance(destinationPath, manifest);

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(manifest);
            ValidateGenerated(manifest);
            Debug.Log(
                "Phase 1 interaction UI rebuilt from the hash-locked " +
                "gui_uset donor icon.");
        }

        private static Manifest LoadManifest()
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(ManifestPath));
            if (manifest == null ||
                manifest.schemaVersion != 1 ||
                manifest.source == null ||
                manifest.generatedOutput == null ||
                manifest.runtimeBinding == null ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifest.productionReplacementKey) ||
                string.IsNullOrWhiteSpace(manifest.source.stagingRelativePath) ||
                string.IsNullOrWhiteSpace(manifest.source.sha256) ||
                string.IsNullOrWhiteSpace(manifest.generatedOutput.path) ||
                string.IsNullOrWhiteSpace(manifest.generatedOutput.guid))
            {
                throw new InvalidDataException(
                    "Phase 1 interaction UI manifest is incomplete or invalid.");
            }

            return manifest;
        }

        private static void ConfigureTextureImporter(Manifest manifest)
        {
            var importer = AssetImporter.GetAtPath(
                manifest.generatedOutput.path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create a texture importer for the donor " +
                    "pickup-hand icon.");
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            (int width, int height) = ParseDimensions(
                manifest.source.dimensions);
            importer.maxTextureSize = Math.Max(width, height);
            importer.userData =
                "TemporaryDirectImport; replacementKey=" +
                manifest.productionReplacementKey;
            importer.SaveAndReimport();
        }

        private static void ValidateGenerated(Manifest manifest)
        {
            string actualGuid = AssetDatabase.AssetPathToGUID(
                manifest.generatedOutput.path);
            if (!string.Equals(
                    actualGuid,
                    manifest.generatedOutput.guid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Generated pickup-hand GUID mismatch: {actualGuid}.");
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                manifest.generatedOutput.path) ??
                throw new InvalidOperationException(
                    "Unity could not load the generated pickup-hand icon.");
            (int width, int height) = ParseDimensions(
                manifest.source.dimensions);
            if (texture.width != width || texture.height != height)
            {
                throw new InvalidDataException(
                    $"Generated pickup-hand dimensions are " +
                    $"{texture.width}x{texture.height}; expected " +
                    $"{width}x{height}.");
            }

            string prefabPath = ToProjectFileSystemPath(
                manifest.runtimeBinding.prefab);
            string prefabYaml = File.ReadAllText(prefabPath);
            if (!prefabYaml.Contains(
                    "guid: " + manifest.generatedOutput.guid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The player prefab is not bound to the generated donor " +
                    "pickup-hand icon.");
            }
        }

        private static (int width, int height) ParseDimensions(string value)
        {
            string[] components = (value ?? string.Empty).Split('x');
            if (components.Length != 2 ||
                !int.TryParse(components[0], out int width) ||
                !int.TryParse(components[1], out int height) ||
                width <= 0 ||
                height <= 0)
            {
                throw new InvalidDataException(
                    $"Invalid source dimensions '{value}'.");
            }

            return (width, height);
        }

        private static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Donor pickup-hand source is missing.",
                    path);
            }

            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string actual = BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty);
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Donor pickup-hand SHA-256 mismatch. Expected " +
                    $"{expected}, got {actual}.");
            }
        }

        private static string ResolveContainedPath(
            string root,
            string relativePath,
            string description)
        {
            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string result = Path.GetFullPath(Path.Combine(
                fullRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(
                    fullRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Path '{relativePath}' escapes {description}.");
            }

            return result;
        }

        private static string ToProjectFileSystemPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");
            return ResolveContainedPath(projectRoot, assetPath, "Unity project");
        }

        private static void WriteDeterministicMeta(
            string path,
            string guid,
            string replacementKey)
        {
            if (guid.Length != 32)
            {
                throw new InvalidDataException(
                    $"Invalid generated GUID '{guid}'.");
            }

            string contents = string.Join("\n", new[]
            {
                "fileFormatVersion: 2",
                "guid: " + guid,
                "TextureImporter:",
                "  externalObjects: {}",
                "  serializedVersion: 13",
                "  userData: TemporaryDirectImport; replacementKey=" +
                    replacementKey,
                "  assetBundleName:",
                "  assetBundleVariant:",
                string.Empty,
            });
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static void WriteProvenance(
            string destinationPath,
            Manifest manifest)
        {
            string text =
                "# Donor pickup-hand icon provenance\n\n" +
                $"- Classification: `{manifest.classification}`\n" +
                $"- Production replacement key: `{manifest.productionReplacementKey}`\n" +
                $"- Runtime asset: `{Path.GetFileName(destinationPath)}`\n" +
                $"- Donor asset name: `{manifest.source.assetName}`\n" +
                "- Donor staging relative path: `" +
                manifest.source.stagingRelativePath + "`\n" +
                $"- Source SHA-256: `{manifest.source.sha256}`\n" +
                "- Transformation: byte-for-byte copy; Unity import disables " +
                "mipmaps and texture compression.\n" +
                "- Scope: private Phase 1 contextual pickup indicator only.\n" +
                "- Authority boundary: presentation only; no donor code, " +
                "controller, FSM, or runtime dependency is imported.\n" +
                "- Phase 2 disposition: replace with a newly authored icon " +
                "while preserving the replacement key and HUD binding.\n";
            File.WriteAllText(
                Path.ChangeExtension(destinationPath, ".provenance.md"),
                text,
                new UTF8Encoding(false));
        }

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string classification = string.Empty;
            public string productionReplacementKey = string.Empty;
            public Source source;
            public GeneratedOutput generatedOutput;
            public RuntimeBinding runtimeBinding;
        }

        [Serializable]
        private sealed class Source
        {
            public string stagingRelativePath = string.Empty;
            public string assetName = string.Empty;
            public string sha256 = string.Empty;
            public string dimensions = string.Empty;
        }

        [Serializable]
        private sealed class GeneratedOutput
        {
            public string path = string.Empty;
            public string guid = string.Empty;
        }

        [Serializable]
        private sealed class RuntimeBinding
        {
            public string prefab = string.Empty;
        }
    }
}
