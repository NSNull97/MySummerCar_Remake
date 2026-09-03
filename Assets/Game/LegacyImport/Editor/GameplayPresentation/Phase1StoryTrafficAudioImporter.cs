using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Builds the private R2B story-traffic Unity Audio fallback library from
    /// hash-locked donor evidence. Runtime authority remains the project-owned
    /// audio event/RTPC contract and the payload remains replaceable in Phase 2.
    /// </summary>
    public static class Phase1StoryTrafficAudioImporter
    {
        private const string ConfigurationPath = "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/Phase1StoryTrafficR2BAudioManifest.json";
        private const string OutputRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Audio/StoryTrafficR2B";
        private const string ClipRoot = OutputRoot + "/Clips";
        private const string ResourceRoot = OutputRoot +
            "/Resources/Phase1StoryTrafficR2B";
        private const string LibraryPath = ResourceRoot +
            "/Phase1StoryTrafficR2BAudioEventLibrary.asset";
        private const string BuildReportPath = OutputRoot +
            "/Phase1StoryTrafficR2BAudioBuildReport.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/Build Phase 1 Story Traffic R2B Audio")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 story traffic audio",
                "The sanitized private R2B story-traffic audio library was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static UnityAudioEventLibrary LoadGeneratedLibrary() =>
            AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath) ??
            throw new InvalidOperationException(
                "Generated Phase 1 story-traffic audio library is missing. Run the R2B audio importer first.");

        public static void Build()
        {
            Manifest manifest = LoadManifest();
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string donorAssetsRoot = Path.Combine(
                paths.DonorStagingDirectory,
                manifest.source.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireHash(
                ResolveSourcePath(donorAssetsRoot, manifest.source.sceneRelativePath),
                manifest.source.sceneSha256,
                "donor scene");

            ResetOutput();
            EnsureAssetFolder(ClipRoot);
            EnsureAssetFolder(ResourceRoot);

            foreach (ClipSpec clip in manifest.clips)
            {
                string source = ResolveSourcePath(
                    donorAssetsRoot,
                    clip.sourceRelativePath);
                RequireHash(source, clip.sourceSha256, clip.clipId);
                if (!string.IsNullOrWhiteSpace(clip.metadataRelativePath))
                {
                    RequireHash(
                        ResolveSourcePath(
                            donorAssetsRoot,
                            clip.metadataRelativePath),
                        clip.metadataSha256,
                        clip.clipId + " metadata");
                }
                string destinationAssetPath =
                    ClipRoot + "/" + clip.generatedFileName;
                string destinationFile = ToFileSystemPath(destinationAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(source, destinationFile, overwrite: true);
                WriteAudioImporterMeta(
                    destinationFile + ".meta",
                    clip.generatedGuid);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var clipsById = new Dictionary<string, AudioClip>(
                StringComparer.Ordinal);
            foreach (ClipSpec clip in manifest.clips)
            {
                string path = ClipRoot + "/" + clip.generatedFileName;
                string actualGuid = AssetDatabase.AssetPathToGUID(path);
                if (!string.Equals(
                        actualGuid,
                        clip.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated GUID mismatch for {clip.clipId}. Expected {clip.generatedGuid}, got {actualGuid}.");
                }

                clipsById.Add(
                    clip.clipId,
                    AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
                    throw new InvalidOperationException(
                        $"Unity could not import story-traffic clip '{path}'."));
            }

            var definitions = new List<UnityAudioEventDefinition>(
                manifest.events.Length);
            foreach (EventSpec entry in manifest.events)
            {
                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring(
                    entry.eventId,
                    clipsById[entry.clipId],
                    ParseCategory(entry.category),
                    entry.loop,
                    entry.volume,
                    configuredPitch: 1f,
                    configuredSpatialBlend: 1f,
                    entry.minimumDistanceMeters,
                    entry.maximumDistanceMeters);
                definitions.Add(definition);
            }

            var library = ScriptableObject.CreateInstance<
                UnityAudioEventLibrary>();
            library.ConfigureForAuthoring(definitions.ToArray());
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            WriteReport(manifest);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureGeneratedForBuild();
            Debug.Log(
                $"Phase 1 story-traffic R2B audio built: {manifest.clips.Length} clips, {manifest.events.Length} events.");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            BuildReport report = JsonUtility.FromJson<BuildReport>(
                File.ReadAllText(ToFileSystemPath(BuildReportPath)));
            if (report == null ||
                report.schemaVersion != manifest.schemaVersion ||
                !string.Equals(
                    report.manifestId,
                    manifest.manifestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    report.sourceManifestSha256,
                    ComputeHash(ToFileSystemPath(ManifestPath)),
                    StringComparison.OrdinalIgnoreCase) ||
                report.clipCount != manifest.clips.Length ||
                report.eventCount != manifest.events.Length ||
                !report.excludesDonorControllers)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 story-traffic audio is stale or invalid.");
            }

            UnityAudioEventLibrary library = LoadGeneratedLibrary();
            if (!library.Validate(out string[] failures) ||
                library.DefinitionCount != manifest.events.Length)
            {
                throw new InvalidOperationException(
                    "Generated story-traffic audio library is invalid: " +
                    string.Join(" | ", failures));
            }

            foreach (ClipSpec clip in manifest.clips)
            {
                string path = ClipRoot + "/" + clip.generatedFileName;
                if (!File.Exists(ToFileSystemPath(path)) ||
                    !string.Equals(
                        ComputeHash(ToFileSystemPath(path)),
                        clip.sourceSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        AssetDatabase.AssetPathToGUID(path),
                        clip.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated story-traffic clip is missing or stale: {clip.clipId}.");
                }
            }

            foreach (EventSpec entry in manifest.events)
            {
                if (!library.TryResolve(
                        new AudioEventId(entry.eventId),
                        out UnityAudioEventDefinition definition) ||
                    definition?.Clip == null ||
                    definition.Loop != entry.loop ||
                    definition.SpatialBlend < 0.999f ||
                    Mathf.Abs(
                        definition.MinimumDistanceMeters -
                        entry.minimumDistanceMeters) > 0.001f ||
                    Mathf.Abs(
                        definition.MaximumDistanceMeters -
                        entry.maximumDistanceMeters) > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"Generated story-traffic event is missing or not spatialized: {entry.eventId}.");
                }
            }
        }

        private static Manifest LoadManifest()
        {
            string path = ToFileSystemPath(ManifestPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Phase 1 story-traffic audio manifest is missing.",
                    path);
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(path));
            if (manifest == null ||
                manifest.schemaVersion != 1 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifest.manifestId) ||
                string.IsNullOrWhiteSpace(manifest.productionReplacementKey) ||
                manifest.source == null ||
                manifest.clips == null || manifest.clips.Length == 0 ||
                manifest.events == null || manifest.events.Length == 0)
            {
                throw new InvalidDataException(
                    "Phase 1 story-traffic audio manifest is malformed.");
            }

            ValidateManifest(manifest);
            return manifest;
        }

        private static void ValidateManifest(Manifest manifest)
        {
            if (!IsSha256(manifest.source.sceneSha256) ||
                !IsSafeRelativePath(manifest.source.stagingRootRelativePath) ||
                !IsSafeRelativePath(manifest.source.sceneRelativePath))
            {
                throw new InvalidDataException(
                    "Story-traffic audio source declaration is invalid.");
            }

            var clipIds = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ClipSpec clip in manifest.clips)
            {
                if (clip == null ||
                    string.IsNullOrWhiteSpace(clip.clipId) ||
                    !clipIds.Add(clip.clipId) ||
                    !IsSafeRelativePath(clip.sourceRelativePath) ||
                    !IsSha256(clip.sourceSha256) ||
                    string.IsNullOrWhiteSpace(clip.metadataRelativePath) !=
                    string.IsNullOrWhiteSpace(clip.metadataSha256) ||
                    !string.IsNullOrWhiteSpace(clip.metadataRelativePath) &&
                    (!IsSafeRelativePath(clip.metadataRelativePath) ||
                     !IsSha256(clip.metadataSha256)) ||
                    Path.GetFileName(clip.generatedFileName) !=
                    clip.generatedFileName ||
                    !names.Add(clip.generatedFileName) ||
                    !IsGuid(clip.generatedGuid) ||
                    !guids.Add(clip.generatedGuid))
                {
                    throw new InvalidDataException(
                        $"Invalid or duplicate story-traffic clip '{clip?.clipId}'.");
                }
            }

            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventSpec entry in manifest.events)
            {
                bool validId;
                try
                {
                    _ = new AudioEventId(entry?.eventId);
                    validId = true;
                }
                catch (ArgumentException)
                {
                    validId = false;
                }

                if (entry == null || !validId ||
                    !eventIds.Add(entry.eventId) ||
                    !clipIds.Contains(entry.clipId) ||
                    !Enum.TryParse(
                        entry.category,
                        ignoreCase: false,
                        out UnityAudioCategory _) ||
                    !float.IsFinite(entry.volume) ||
                    entry.volume < 0f || entry.volume > 1f ||
                    !float.IsFinite(entry.minimumDistanceMeters) ||
                    !float.IsFinite(entry.maximumDistanceMeters) ||
                    entry.minimumDistanceMeters <= 0f ||
                    entry.maximumDistanceMeters <
                    entry.minimumDistanceMeters ||
                    entry.maximumDistanceMeters > 100f)
                {
                    throw new InvalidDataException(
                        $"Invalid or duplicate story-traffic event '{entry?.eventId}'.");
                }
            }
        }

        private static UnityAudioCategory ParseCategory(string value)
        {
            if (Enum.TryParse(
                    value,
                    ignoreCase: false,
                    out UnityAudioCategory category))
            {
                return category;
            }

            throw new InvalidDataException(
                $"Unknown Unity audio category '{value}'.");
        }

        private static string ResolveSourcePath(string root, string relative)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string resolved = Path.GetFullPath(Path.Combine(
                normalizedRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(resolved))
            {
                throw new FileNotFoundException(
                    $"Hash-locked story-traffic audio source is missing: {relative}.",
                    resolved);
            }

            return resolved;
        }

        private static void RequireHash(
            string path,
            string expected,
            string description)
        {
            string actual = ComputeHash(path);
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Hash mismatch for {description}. Expected {expected}, got {actual}.");
            }
        }

        private static void ResetOutput()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                if (!AssetDatabase.DeleteAsset(OutputRoot))
                {
                    throw new IOException(
                        $"Unity could not clear generated story-traffic audio '{OutputRoot}'.");
                }
            }
            else
            {
                string output = ToFileSystemPath(OutputRoot);
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, recursive: true);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureAssetFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length)
                         .Split('/'))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }

        private static void WriteAudioImporterMeta(string path, string guid)
        {
            string text =
                "fileFormatVersion: 2\n" +
                "guid: " + guid + "\n" +
                "AudioImporter:\n" +
                "  externalObjects: {}\n" +
                "  serializedVersion: 8\n" +
                "  defaultSettings:\n" +
                "    serializedVersion: 2\n" +
                "    loadType: 0\n" +
                "    sampleRateSetting: 0\n" +
                "    sampleRateOverride: 44100\n" +
                "    compressionFormat: 1\n" +
                "    quality: 1\n" +
                "    conversionMode: 0\n" +
                "    preloadAudioData: 1\n" +
                "  platformSettingOverrides: {}\n" +
                "  forceToMono: 1\n" +
                "  normalize: 1\n" +
                "  loadInBackground: 0\n" +
                "  ambisonic: 0\n" +
                "  3D: 1\n" +
                "  userData:\n" +
                "  assetBundleName:\n" +
                "  assetBundleVariant:\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static void WriteReport(Manifest manifest)
        {
            string report = JsonUtility.ToJson(new BuildReport
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                classification = manifest.classification,
                sourceManifestSha256 = ComputeHash(
                    ToFileSystemPath(ManifestPath)),
                clipCount = manifest.clips.Length,
                eventCount = manifest.events.Length,
                eventIds = manifest.events
                    .Select(entry => entry.eventId)
                    .ToArray(),
                excludesDonorControllers = true,
            }, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(BuildReportPath),
                report,
                new UTF8Encoding(false));
        }

        private static string ComputeHash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string ToFileSystemPath(string projectRelativePath) =>
            Path.GetFullPath(projectRelativePath);

        private static bool IsSafeRelativePath(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            !Path.IsPathRooted(value) &&
            !value.Split('/', '\\').Contains("..", StringComparer.Ordinal);

        private static bool IsSha256(string value) =>
            value != null && value.Length == 64 &&
            value.All(character => Uri.IsHexDigit(character));

        private static bool IsGuid(string value) =>
            value != null && value.Length == 32 &&
            value.All(character => Uri.IsHexDigit(character));

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string productionReplacementKey;
            public SourceSpec source;
            public ClipSpec[] clips;
            public EventSpec[] events;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
        }

        [Serializable]
        private sealed class ClipSpec
        {
            public string clipId;
            public string sourceRelativePath;
            public string sourceSha256;
            public string metadataRelativePath;
            public string metadataSha256;
            public string generatedFileName;
            public string generatedGuid;
        }

        [Serializable]
        private sealed class EventSpec
        {
            public string eventId;
            public string clipId;
            public string category;
            public bool loop;
            public float volume;
            public float minimumDistanceMeters;
            public float maximumDistanceMeters;
        }

        [Serializable]
        private sealed class BuildReport
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string sourceManifestSha256;
            public int clipCount;
            public int eventCount;
            public string[] eventIds;
            public bool excludesDonorControllers;
        }
    }
}
