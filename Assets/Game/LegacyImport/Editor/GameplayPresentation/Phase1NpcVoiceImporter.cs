using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Characters;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public sealed class Phase1NpcVoiceDialogueSpec
    {
        internal Phase1NpcVoiceDialogueSpec(
            string characterDefinitionId,
            string lineId,
            string localizationKey,
            string fallbackSubtitle,
            string audioEventId,
            string emittedEventId,
            string scheduleBlockId)
        {
            CharacterDefinitionId = characterDefinitionId;
            LineId = lineId;
            LocalizationKey = localizationKey;
            FallbackSubtitle = fallbackSubtitle;
            AudioEventId = audioEventId;
            EmittedEventId = emittedEventId;
            ScheduleBlockId = scheduleBlockId;
        }

        public string CharacterDefinitionId { get; }
        public string LineId { get; }
        public string LocalizationKey { get; }
        public string FallbackSubtitle { get; }
        public string AudioEventId { get; }
        public string EmittedEventId { get; }
        public string ScheduleBlockId { get; }
    }

    /// <summary>
    /// Copies only hash-locked PCM WAV payloads selected by the R1 voice
    /// manifest. Donor AudioClip assets, MasterAudio objects, PlayMaker FSMs and
    /// controllers remain read-only evidence and never enter runtime.
    /// </summary>
    public static class Phase1NpcVoiceImporter
    {
        private const string ConfigurationPath = "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/Phase1NpcR1VoiceManifest.json";
        private const string OutputRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Audio/NpcR1";
        private const string ClipRoot = OutputRoot + "/Clips";
        private const string ResourceRoot = OutputRoot + "/Resources/Phase1NpcR1";
        private const string LibraryPath = ResourceRoot +
            "/Phase1NpcR1UnityAudioEventLibrary.asset";
        private const string BuildReportPath = OutputRoot +
            "/Phase1NpcR1VoiceBuildReport.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/Build Phase 1 NPC R1 Voices")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 NPC voices",
                "The sanitized private R1 NPC voice library was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static IReadOnlyList<Phase1NpcVoiceDialogueSpec> LoadDialogueSpecs()
        {
            Manifest manifest = LoadManifest();
            return manifest.entries
                .Select(entry => new Phase1NpcVoiceDialogueSpec(
                    entry.characterDefinitionId,
                    entry.lineId,
                    entry.localizationKey,
                    entry.fallbackSubtitle,
                    entry.audioEventId,
                    entry.emittedEventId,
                    entry.scheduleBlockId))
                .ToArray();
        }

        public static UnityAudioEventLibrary LoadGeneratedLibrary() =>
            AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath) ??
            throw new InvalidOperationException(
                "Generated Phase 1 NPC voice library is missing. Run the R1 voice importer first.");

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
            string scenePath = ResolveSourcePath(
                donorAssetsRoot,
                manifest.source.sceneRelativePath);
            RequireHash(scenePath, manifest.source.sceneSha256, "donor scene");

            ResetOutput();
            EnsureAssetFolder(ClipRoot);
            EnsureAssetFolder(ResourceRoot);

            foreach (VoiceEntrySpec entry in manifest.entries)
            {
                string sourceAsset = ResolveSourcePath(
                    donorAssetsRoot,
                    entry.sourceAssetRelativePath);
                string sourceResource = ResolveSourcePath(
                    donorAssetsRoot,
                    entry.sourceResourceRelativePath);
                RequireHash(
                    sourceAsset,
                    entry.sourceAssetSha256,
                    entry.audioEventId + " AudioClip metadata");
                RequireHash(
                    sourceResource,
                    entry.sourceResourceSha256,
                    entry.audioEventId + " PCM resource");
                ValidateDonorAudioAsset(entry, sourceAsset, sourceResource);

                string destinationAssetPath =
                    ClipRoot + "/" + entry.generatedFileName;
                string destinationFile = ToFileSystemPath(destinationAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(sourceResource, destinationFile, overwrite: true);
                WriteAudioImporterMeta(
                    destinationFile + ".meta",
                    entry.generatedGuid);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var definitions = new List<UnityAudioEventDefinition>(
                manifest.entries.Length);
            foreach (VoiceEntrySpec entry in manifest.entries)
            {
                string clipPath = ClipRoot + "/" + entry.generatedFileName;
                string actualGuid = AssetDatabase.AssetPathToGUID(clipPath);
                if (!string.Equals(
                        actualGuid,
                        entry.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated GUID mismatch for {entry.audioEventId}. Expected {entry.generatedGuid}, got {actualGuid}.");
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) ??
                    throw new InvalidOperationException(
                        $"Unity could not import the generated voice clip '{clipPath}'.");
                if (Mathf.Abs(clip.length - entry.sourcePcmLengthSeconds) > 0.025f)
                {
                    throw new InvalidDataException(
                        $"Generated clip length mismatch for {entry.audioEventId}. Expected PCM {entry.sourcePcmLengthSeconds:F6}s, got {clip.length:F6}s.");
                }

                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring(
                    entry.audioEventId,
                    clip,
                    UnityAudioCategory.Dialogue,
                    configuredLoop: false,
                    configuredVolume: 1f,
                    configuredPitch: 1f,
                    configuredSpatialBlend: 1f,
                    entry.minimumDistanceMeters,
                    entry.maximumDistanceMeters);
                definitions.Add(definition);
            }

            var library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            library.ConfigureForAuthoring(definitions.ToArray());
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WriteReport(manifest, scenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureGeneratedForBuild();
            Debug.Log(
                $"Phase 1 NPC R1 voice library built: {manifest.entries.Length} hash-locked clips.");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            string reportPath = ToFileSystemPath(BuildReportPath);
            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Phase 1 NPC voice build report is missing. Run the R1 voice importer.");
            }

            BuildReportData report = JsonUtility.FromJson<BuildReportData>(
                File.ReadAllText(reportPath));
            string manifestHash = ComputeHash(ToFileSystemPath(ManifestPath));
            if (report == null ||
                report.schemaVersion != manifest.schemaVersion ||
                !string.Equals(
                    report.manifestId,
                    manifest.manifestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    report.sourceManifestSha256,
                    manifestHash,
                    StringComparison.OrdinalIgnoreCase) ||
                report.clipCount != manifest.entries.Length ||
                !report.excludesDonorAudioClipAssetsFsmControllers)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 NPC voices were built from a stale or invalid manifest.");
            }

            UnityAudioEventLibrary library = LoadGeneratedLibrary();
            if (!library.Validate(out string[] failures) ||
                library.DefinitionCount != manifest.entries.Length)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 NPC voice library is invalid: " +
                    string.Join(" | ", failures));
            }

            foreach (VoiceEntrySpec entry in manifest.entries)
            {
                string clipPath = ClipRoot + "/" + entry.generatedFileName;
                string clipFile = ToFileSystemPath(clipPath);
                if (!File.Exists(clipFile) ||
                    !string.Equals(
                        ComputeHash(clipFile),
                        entry.sourceResourceSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        AssetDatabase.AssetPathToGUID(clipPath),
                        entry.generatedGuid,
                        StringComparison.OrdinalIgnoreCase) ||
                    !library.TryResolve(
                        new AudioEventId(entry.audioEventId),
                        out UnityAudioEventDefinition definition) ||
                    definition?.Clip == null)
                {
                    throw new InvalidOperationException(
                        $"Generated Phase 1 NPC voice entry is missing or stale: {entry.audioEventId}.");
                }
            }
        }

        private static Manifest LoadManifest()
        {
            string path = ToFileSystemPath(ManifestPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Phase 1 NPC voice manifest is missing.",
                    path);
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest == null ||
                manifest.schemaVersion != 1 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifest.manifestId) ||
                string.IsNullOrWhiteSpace(manifest.productionReplacementKey) ||
                manifest.source == null ||
                manifest.entries == null ||
                manifest.entries.Length == 0)
            {
                throw new InvalidDataException(
                    "Phase 1 NPC voice manifest is malformed.");
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
                    "Phase 1 NPC voice source declaration is invalid.");
            }

            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generatedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VoiceEntrySpec entry in manifest.entries)
            {
                string failure = string.Empty;
                bool idsValid = entry != null &&
                    CharacterStableId.TryValidate(
                        entry.characterDefinitionId,
                        "character.",
                        out failure) &&
                    CharacterStableId.TryValidate(
                        entry.lineId,
                        "line.",
                        out failure) &&
                    CharacterStableId.TryValidate(
                        entry.localizationKey,
                        "npc.",
                        out failure) &&
                    CharacterStableId.TryValidate(
                        entry.audioEventId,
                        "audio.",
                        out failure) &&
                    CharacterStableId.TryValidate(
                        entry.emittedEventId,
                        "event.",
                        out failure) &&
                    (string.IsNullOrEmpty(entry.scheduleBlockId) ||
                     CharacterStableId.TryValidate(
                         entry.scheduleBlockId,
                         "schedule.",
                         out failure));

                if (!idsValid ||
                    string.IsNullOrWhiteSpace(entry.fallbackSubtitle) ||
                    string.IsNullOrWhiteSpace(entry.masterAudioGroup) ||
                    string.IsNullOrWhiteSpace(entry.variationName) ||
                    entry.fsmComponentFileId <= 0 ||
                    entry.sourceGameObjectId <= 0 ||
                    entry.sourceAudioSourceId <= 0 ||
                    !IsGuid(entry.sourceClipGuid) ||
                    !IsGuid(entry.generatedGuid) ||
                    !IsSha256(entry.sourceAssetSha256) ||
                    !IsSha256(entry.sourceResourceSha256) ||
                    !IsSafeRelativePath(entry.sourceAssetRelativePath) ||
                    !IsSafeRelativePath(entry.sourceResourceRelativePath) ||
                    !entry.sourceAssetRelativePath.EndsWith(
                        ".audioclip",
                        StringComparison.OrdinalIgnoreCase) ||
                    !entry.sourceResourceRelativePath.EndsWith(
                        ".audioclip.resS",
                        StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(entry.generatedFileName) !=
                    entry.generatedFileName ||
                    !entry.generatedFileName.EndsWith(
                        ".wav",
                        StringComparison.OrdinalIgnoreCase) ||
                    !float.IsFinite(entry.donorSerializedLengthSeconds) ||
                    entry.donorSerializedLengthSeconds <= 0f ||
                    !float.IsFinite(entry.sourcePcmLengthSeconds) ||
                    entry.sourcePcmLengthSeconds <= 0f ||
                    !float.IsFinite(entry.minimumDistanceMeters) ||
                    !float.IsFinite(entry.maximumDistanceMeters) ||
                    entry.minimumDistanceMeters <= 0f ||
                    entry.maximumDistanceMeters < entry.minimumDistanceMeters ||
                    !lineIds.Add(entry.lineId) ||
                    !eventIds.Add(entry.audioEventId) ||
                    !generatedNames.Add(entry.generatedFileName) ||
                    !generatedGuids.Add(entry.generatedGuid))
                {
                    throw new InvalidDataException(
                        $"Invalid or duplicate Phase 1 NPC voice entry '{entry?.audioEventId}': {failure}");
                }
            }
        }

        private static void ValidateDonorAudioAsset(
            VoiceEntrySpec entry,
            string sourceAsset,
            string sourceResource)
        {
            string metaPath = sourceAsset + ".meta";
            if (!File.Exists(metaPath) ||
                !File.ReadAllText(metaPath).Contains(
                    "guid: " + entry.sourceClipGuid,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Donor AudioClip GUID mismatch for {entry.audioEventId}.");
            }

            string yaml = File.ReadAllText(sourceAsset);
            Match lengthMatch = Regex.Match(
                yaml,
                @"(?m)^  m_Length: ([0-9.Ee+-]+)$");
            if (!lengthMatch.Success ||
                !float.TryParse(
                    lengthMatch.Groups[1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float sourceLength) ||
                Mathf.Abs(sourceLength - entry.donorSerializedLengthSeconds) > 0.0001f ||
                !yaml.Contains(
                    Path.GetFileName(entry.sourceResourceRelativePath),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Donor AudioClip metadata mismatch for {entry.audioEventId}.");
            }

            using FileStream stream = File.OpenRead(sourceResource);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (ReadFourCc(reader) != "RIFF")
            {
                throw new InvalidDataException(
                    $"Selected donor resource is not a PCM WAV container: {entry.audioEventId}.");
            }

            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
            {
                throw new InvalidDataException(
                    $"Selected donor resource is not a PCM WAV container: {entry.audioEventId}.");
            }

            ushort audioFormat = 0;
            ushort channels = 0;
            uint sampleRate = 0;
            uint byteRate = 0;
            ushort blockAlign = 0;
            ushort bitsPerSample = 0;
            uint pcmDataSize = 0;
            bool foundFormat = false;
            bool foundData = false;
            while (stream.Position + 8 <= stream.Length)
            {
                string chunkId = ReadFourCc(reader);
                uint chunkSize = reader.ReadUInt32();
                long chunkEnd = stream.Position + chunkSize;
                if (chunkEnd > stream.Length)
                {
                    throw new InvalidDataException(
                        $"Truncated WAV chunk '{chunkId}' for {entry.audioEventId}.");
                }

                if (chunkId == "fmt ")
                {
                    if (chunkSize < 16)
                    {
                        throw new InvalidDataException(
                            $"Invalid WAV format chunk for {entry.audioEventId}.");
                    }

                    audioFormat = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadUInt32();
                    byteRate = reader.ReadUInt32();
                    blockAlign = reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                    foundFormat = true;
                }
                else if (chunkId == "data")
                {
                    pcmDataSize = chunkSize;
                    foundData = true;
                }

                stream.Position = chunkEnd + (chunkSize & 1u);
            }

            if (!foundFormat ||
                !foundData ||
                audioFormat != 1 ||
                channels != 1 ||
                sampleRate != 22050 ||
                byteRate != 44100 ||
                blockAlign != 2 ||
                bitsPerSample != 16)
            {
                throw new InvalidDataException(
                    $"Unexpected WAV PCM format for {entry.audioEventId}.");
            }

            float pcmLengthSeconds = pcmDataSize / (float)(sampleRate * blockAlign);
            if (Mathf.Abs(pcmLengthSeconds - entry.sourcePcmLengthSeconds) > 0.0001f)
            {
                throw new InvalidDataException(
                    $"Donor WAV PCM duration mismatch for {entry.audioEventId}. Expected {entry.sourcePcmLengthSeconds:F6}s, got {pcmLengthSeconds:F6}s.");
            }
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            byte[] value = reader.ReadBytes(4);
            return value.Length == 4
                ? Encoding.ASCII.GetString(value)
                : string.Empty;
        }

        private static void ResetOutput()
        {
            string output = ToFileSystemPath(OutputRoot);
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                if (!AssetDatabase.DeleteAsset(OutputRoot))
                {
                    throw new IOException(
                        $"Unity could not clear the generated NPC voice output '{OutputRoot}'.");
                }
            }
            else if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
                "    sampleRateOverride: 22050\n" +
                "    compressionFormat: 1\n" +
                "    quality: 1\n" +
                "    conversionMode: 0\n" +
                "    preloadAudioData: 1\n" +
                "  platformSettingOverrides: {}\n" +
                "  forceToMono: 0\n" +
                "  normalize: 1\n" +
                "  loadInBackground: 0\n" +
                "  ambisonic: 0\n" +
                "  3D: 1\n" +
                "  userData:\n" +
                "  assetBundleName:\n" +
                "  assetBundleVariant:\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static void WriteReport(Manifest manifest, string scenePath)
        {
            string report = JsonUtility.ToJson(new BuildReportData
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                classification = manifest.classification,
                sourceSceneSha256 = ComputeHash(scenePath),
                sourceManifestSha256 = ComputeHash(ToFileSystemPath(ManifestPath)),
                clipCount = manifest.entries.Length,
                eventIds = manifest.entries
                    .Select(entry => entry.audioEventId)
                    .ToArray(),
                excludesDonorAudioClipAssetsFsmControllers = true,
            }, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(BuildReportPath),
                report,
                new UTF8Encoding(false));
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ResolveSourcePath(string root, string relativePath)
        {
            if (!IsSafeRelativePath(relativePath))
            {
                throw new InvalidDataException(
                    $"Unsafe donor-relative path '{relativePath}'.");
            }

            string resolvedRoot = Path.GetFullPath(root);
            string resolved = Path.GetFullPath(Path.Combine(
                resolvedRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = resolvedRoot.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Donor-relative path escapes its staging root: '{relativePath}'.");
            }

            return resolved;
        }

        private static bool IsSafeRelativePath(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            !Path.IsPathRooted(value) &&
            value.Split('/', '\\').All(part =>
                part.Length > 0 && part != "." && part != "..");

        private static bool IsGuid(string value) =>
            !string.IsNullOrEmpty(value) &&
            Regex.IsMatch(value, "^[0-9a-fA-F]{32}$");

        private static bool IsSha256(string value) =>
            !string.IsNullOrEmpty(value) &&
            Regex.IsMatch(value, "^[0-9a-fA-F]{64}$");

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Locked {label} is missing.", path);
            }

            string actual = ComputeHash(path);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked {label} hash mismatch. Expected {expected}, got {actual}.");
            }
        }

        private static string ComputeHash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return string.Concat(
                sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string ToFileSystemPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string productionReplacementKey;
            public SourceSpec source;
            public VoiceEntrySpec[] entries;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
        }

        [Serializable]
        internal sealed class VoiceEntrySpec
        {
            public string characterDefinitionId;
            public string lineId;
            public string localizationKey;
            public string fallbackSubtitle;
            public string audioEventId;
            public string emittedEventId;
            public string scheduleBlockId;
            public string masterAudioGroup;
            public string variationName;
            public long fsmComponentFileId;
            public long sourceGameObjectId;
            public long sourceAudioSourceId;
            public string sourceClipGuid;
            public string sourceAssetRelativePath;
            public string sourceAssetSha256;
            public string sourceResourceRelativePath;
            public string sourceResourceSha256;
            public float donorSerializedLengthSeconds;
            public float sourcePcmLengthSeconds;
            public float minimumDistanceMeters;
            public float maximumDistanceMeters;
            public string generatedFileName;
            public string generatedGuid;
        }

        [Serializable]
        private sealed class BuildReportData
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public string sourceSceneSha256;
            public string sourceManifestSha256;
            public int clipCount;
            public string[] eventIds;
            public bool excludesDonorAudioClipAssetsFsmControllers;
        }
    }

    public sealed class Phase1NpcVoiceBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -889;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                Phase1NpcVoiceImporter.EnsureGeneratedForBuild();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "Mandatory Phase 1 NPC voice validation failed. " +
                    exception.Message);
            }
        }
    }
}
