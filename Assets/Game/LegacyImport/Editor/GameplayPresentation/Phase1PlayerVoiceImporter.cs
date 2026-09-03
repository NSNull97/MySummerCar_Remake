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
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Builds a removable private Phase 1 player-voice fallback from the 16
    /// hash-locked Swearing variations and the separate 11-way middle-finger
    /// Fuck group. No donor controller, FSM, AudioSource or AudioClip asset is
    /// imported into runtime.
    /// </summary>
    public static class Phase1PlayerVoiceImporter
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/Phase1PlayerVoiceManifest.json";
        private const string OutputRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Audio/PlayerVoice";
        private const string ClipRoot = OutputRoot + "/Clips";
        private const string ResourceRoot = OutputRoot +
            "/Resources/Phase1PlayerVoice";
        private const string LibraryPath = ResourceRoot +
            "/Phase1PlayerVoiceAudioEventLibrary.asset";
        private const string BuildReportPath = OutputRoot +
            "/Phase1PlayerVoiceBuildReport.json";

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/Build Phase 1 Player Voices")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Phase 1 player voices",
                "The sanitized private player voice library was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static UnityAudioEventLibrary LoadGeneratedLibrary() =>
            AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath) ??
            throw new InvalidOperationException(
                "Generated Phase 1 player voice library is missing. Run the player voice importer first.");

        public static void Build()
        {
            Manifest manifest = LoadManifest();
            EntrySpec[] allEntries = GetAllEntries(manifest);
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
            ValidateDonorCatalogEvidence(scenePath, manifest);

            ResetOutput();
            EnsureAssetFolder(ClipRoot);
            EnsureAssetFolder(ResourceRoot);

            foreach (EntrySpec entry in allEntries)
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
                ValidateDonorAudioAsset(
                    entry,
                    sourceAsset,
                    sourceResource);

                string destinationAssetPath =
                    ClipRoot + "/" + entry.generatedFileName;
                string destinationFile =
                    ToFileSystemPath(destinationAssetPath);
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
                allEntries.Length);
            foreach (EntrySpec entry in allEntries)
            {
                string clipPath = ClipRoot + "/" + entry.generatedFileName;
                if (!string.Equals(
                        AssetDatabase.AssetPathToGUID(clipPath),
                        entry.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated GUID mismatch for {entry.audioEventId}.");
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) ??
                    throw new InvalidOperationException(
                        $"Unity could not import player voice clip '{clipPath}'.");
                if (Mathf.Abs(clip.length - entry.sourcePcmLengthSeconds) > 0.025f)
                {
                    throw new InvalidDataException(
                        $"Generated clip length mismatch for {entry.audioEventId}. Expected {entry.sourcePcmLengthSeconds:F6}s, got {clip.length:F6}s.");
                }

                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring(
                    entry.audioEventId,
                    clip,
                    UnityAudioCategory.Dialogue,
                    configuredLoop: false,
                    configuredVolume: 1f,
                    configuredPitch: 1f,
                    configuredSpatialBlend: 0f,
                    configuredMinimumDistanceMeters: 1f,
                    configuredMaximumDistanceMeters: 10f);
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
                $"Phase 1 player voice library built: {allEntries.Length} hash-locked clips.");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            EntrySpec[] allEntries = GetAllEntries(manifest);
            string reportPath = ToFileSystemPath(BuildReportPath);
            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Phase 1 player voice build report is missing. Run the player voice importer.");
            }

            BuildReportData report = JsonUtility.FromJson<BuildReportData>(
                File.ReadAllText(reportPath));
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
                !string.Equals(
                    report.sourceSceneSha256,
                    manifest.source.sceneSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                report.clipCount != allEntries.Length ||
                !report.excludesDonorControllersAndAudioClipAssets)
            {
                throw new InvalidOperationException(
                    "Generated Phase 1 player voices were built from a stale or invalid manifest.");
            }

            UnityAudioEventLibrary library = LoadGeneratedLibrary();
            if (!library.Validate(out string[] failures) ||
                library.DefinitionCount != allEntries.Length)
            {
                throw new InvalidOperationException(
                    "Generated player voice library is invalid: " +
                    string.Join(" | ", failures));
            }

            foreach (EntrySpec entry in allEntries)
            {
                string clipPath = ClipRoot + "/" + entry.generatedFileName;
                if (!File.Exists(ToFileSystemPath(clipPath)) ||
                    !string.Equals(
                        ComputeHash(ToFileSystemPath(clipPath)),
                        entry.sourceResourceSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        AssetDatabase.AssetPathToGUID(clipPath),
                        entry.generatedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generated player voice clip is missing or stale: {entry.audioEventId}.");
                }

                if (!library.TryResolve(
                        new AudioEventId(entry.audioEventId),
                        out UnityAudioEventDefinition definition) ||
                    definition?.Clip == null ||
                    definition.Loop ||
                    definition.SpatialBlend > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"Generated player voice event is missing or not 2D: {entry.audioEventId}.");
                }
            }
        }

        private static Manifest LoadManifest()
        {
            string path = ToFileSystemPath(ManifestPath);
            Manifest manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(path));
            if (manifest == null ||
                manifest.schemaVersion != 2 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.masterAudioGroup,
                    "Swearing",
                    StringComparison.Ordinal) ||
                manifest.masterAudioGroupComponentFileId <= 0 ||
                !string.Equals(
                    manifest.middleFingerMasterAudioGroup,
                    "Fuck",
                    StringComparison.Ordinal) ||
                manifest.middleFingerMasterAudioGroupComponentFileId <= 0 ||
                manifest.middleFingerSubtitleTableComponentFileId <= 0 ||
                string.IsNullOrWhiteSpace(manifest.manifestId) ||
                string.IsNullOrWhiteSpace(manifest.productionReplacementKey) ||
                manifest.speechFsmComponentFileId <= 0 ||
                manifest.stressSimulationFsmComponentFileId <= 0 ||
                manifest.playerFunctionsFsmComponentFileId <= 0 ||
                manifest.source == null ||
                manifest.entries == null ||
                manifest.entries.Length !=
                    AudioProjectIds.Events.PlayerSwearVariantCount ||
                manifest.middleFingerEntries == null ||
                manifest.middleFingerEntries.Length !=
                    AudioProjectIds.Events.PlayerMiddleFingerVariantCount)
            {
                throw new InvalidDataException(
                    "Phase 1 player voice manifest is malformed.");
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
                    "Player voice source declaration is invalid.");
            }

            var sourceGuids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var generatedGuids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            ValidateEntries(
                manifest.entries,
                AudioProjectIds.Events.GetPlayerSwearVariant,
                "swear",
                sourceGuids,
                generatedGuids,
                names);
            ValidateEntries(
                manifest.middleFingerEntries,
                AudioProjectIds.Events.GetPlayerMiddleFingerVariant,
                "middle-finger",
                sourceGuids,
                generatedGuids,
                names);
        }

        private static void ValidateDonorCatalogEvidence(
            string scenePath,
            Manifest manifest)
        {
            Dictionary<long, string> blocks = ReadYamlObjectBlocks(
                scenePath,
                manifest.masterAudioGroupComponentFileId,
                manifest.middleFingerMasterAudioGroupComponentFileId,
                manifest.middleFingerSubtitleTableComponentFileId);
            ValidateVariationOrder(
                blocks[manifest.masterAudioGroupComponentFileId],
                manifest.entries,
                manifest.masterAudioGroup);
            ValidateVariationOrder(
                blocks[manifest.middleFingerMasterAudioGroupComponentFileId],
                manifest.middleFingerEntries,
                manifest.middleFingerMasterAudioGroup);

            string[] donorSubtitles = ParseStringList(
                blocks[manifest.middleFingerSubtitleTableComponentFileId],
                "preFillStringList");
            string[] expectedSubtitles = manifest.middleFingerEntries
                .Select(entry => entry.fallbackSubtitleEnglish)
                .ToArray();
            if (!donorSubtitles.SequenceEqual(
                    expectedSubtitles,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Locked Finger subtitle table does not match the manifest's ordered 11-line catalog.");
            }
        }

        private static Dictionary<long, string> ReadYamlObjectBlocks(
            string scenePath,
            params long[] requiredFileIds)
        {
            var required = new HashSet<long>(requiredFileIds);
            var blocks = new Dictionary<long, string>();
            long activeFileId = 0;
            StringBuilder activeBlock = null;

            using StreamReader reader = File.OpenText(scenePath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Match header = Regex.Match(
                    line,
                    @"^--- !u![0-9]+ &(?<fileId>[0-9]+)$");
                if (header.Success)
                {
                    if (activeBlock != null)
                    {
                        blocks.Add(activeFileId, activeBlock.ToString());
                    }

                    activeBlock = null;
                    activeFileId = long.Parse(
                        header.Groups["fileId"].Value,
                        CultureInfo.InvariantCulture);
                    if (required.Contains(activeFileId))
                    {
                        activeBlock = new StringBuilder();
                        activeBlock.AppendLine(line);
                    }

                    continue;
                }

                activeBlock?.AppendLine(line);
            }

            if (activeBlock != null)
            {
                blocks.Add(activeFileId, activeBlock.ToString());
            }

            foreach (long fileId in required)
            {
                if (!blocks.ContainsKey(fileId))
                {
                    throw new InvalidDataException(
                        $"Locked player voice evidence component &{fileId} is missing from GAME.unity.");
                }
            }

            return blocks;
        }

        private static void ValidateVariationOrder(
            string componentBlock,
            EntrySpec[] entries,
            string groupName)
        {
            long[] donorVariationIds = Regex.Matches(
                    componentBlock,
                    @"(?m)^  - \{fileID: (?<fileId>[0-9]+)\}\r?$")
                .Cast<Match>()
                .Select(match => long.Parse(
                    match.Groups["fileId"].Value,
                    CultureInfo.InvariantCulture))
                .ToArray();
            long[] expectedVariationIds = entries
                .Select(entry => entry.sourceVariationComponentFileId)
                .ToArray();
            if (!donorVariationIds.SequenceEqual(expectedVariationIds))
            {
                throw new InvalidDataException(
                    $"Locked MasterAudio group '{groupName}' variation order does not match the manifest.");
            }
        }

        private static string[] ParseStringList(
            string componentBlock,
            string propertyName)
        {
            string[] lines = componentBlock.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None);
            var values = new List<string>();
            bool reading = false;
            string header = "  " + propertyName + ":";
            foreach (string line in lines)
            {
                if (!reading)
                {
                    reading = string.Equals(
                        line,
                        header,
                        StringComparison.Ordinal);
                    continue;
                }

                if (!line.StartsWith("  - ", StringComparison.Ordinal))
                {
                    break;
                }

                string value = line.Substring(4).Trim();
                if (value.Length >= 2 &&
                    value[0] == '\'' &&
                    value[value.Length - 1] == '\'')
                {
                    value = value.Substring(1, value.Length - 2)
                        .Replace("''", "'");
                }

                if (value.Length >= 2 &&
                    value[0] == '"' &&
                    value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }

                values.Add(value);
            }

            if (!reading || values.Count == 0)
            {
                throw new InvalidDataException(
                    $"Locked player voice evidence list '{propertyName}' is missing or empty.");
            }

            return values.ToArray();
        }

        private static void ValidateEntries(
            EntrySpec[] entries,
            Func<int, AudioEventId> expectedEventAt,
            string catalogName,
            HashSet<string> sourceGuids,
            HashSet<string> generatedGuids,
            HashSet<string> names)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                EntrySpec entry = entries[index];
                string expectedEventId = expectedEventAt(index).Value;
                if (entry == null ||
                    entry.variantIndex != index + 1 ||
                    !string.Equals(
                        entry.audioEventId,
                        expectedEventId,
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.fallbackSubtitleEnglish) ||
                    string.IsNullOrWhiteSpace(entry.fallbackSubtitleRussian) ||
                    entry.sourceVariationComponentFileId <= 0 ||
                    entry.sourceGameObjectId <= 0 ||
                    entry.sourceAudioSourceId <= 0 ||
                    !IsGuid(entry.sourceClipGuid) ||
                    !sourceGuids.Add(entry.sourceClipGuid) ||
                    !IsSafeRelativePath(entry.sourceAssetRelativePath) ||
                    !IsSafeRelativePath(entry.sourceResourceRelativePath) ||
                    !IsSha256(entry.sourceAssetSha256) ||
                    !IsSha256(entry.sourceResourceSha256) ||
                    !float.IsFinite(entry.donorSerializedLengthSeconds) ||
                    entry.donorSerializedLengthSeconds <= 0f ||
                    !float.IsFinite(entry.sourcePcmLengthSeconds) ||
                    entry.sourcePcmLengthSeconds <= 0f ||
                    Path.GetFileName(entry.generatedFileName) !=
                        entry.generatedFileName ||
                    !names.Add(entry.generatedFileName) ||
                    !IsGuid(entry.generatedGuid) ||
                    !generatedGuids.Add(entry.generatedGuid))
                {
                    throw new InvalidDataException(
                        $"Invalid {catalogName} player voice entry at index {index}.");
                }
            }
        }

        private static void ValidateDonorAudioAsset(
            EntrySpec entry,
            string sourceAsset,
            string sourceResource)
        {
            string yaml = File.ReadAllText(sourceAsset);
            string expectedName = Path.GetFileNameWithoutExtension(sourceAsset);
            Match nameMatch = Regex.Match(
                yaml,
                @"(?m)^  m_Name: (?<value>.+)$");
            Match lengthMatch = Regex.Match(
                yaml,
                @"(?m)^  m_Length: (?<value>[-+0-9.eE]+)$");
            Match sizeMatch = Regex.Match(
                yaml,
                @"(?m)^    m_Size: (?<value>[0-9]+)$");
            if (!nameMatch.Success ||
                !string.Equals(
                    nameMatch.Groups["value"].Value.Trim(),
                    expectedName,
                    StringComparison.Ordinal) ||
                !lengthMatch.Success ||
                !float.TryParse(
                    lengthMatch.Groups["value"].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float serializedLength) ||
                Mathf.Abs(
                    serializedLength -
                    entry.donorSerializedLengthSeconds) > 0.0001f ||
                !sizeMatch.Success ||
                !long.TryParse(
                    sizeMatch.Groups["value"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long serializedSize) ||
                serializedSize != new FileInfo(sourceResource).Length)
            {
                throw new InvalidDataException(
                    $"Donor AudioClip metadata mismatch for {entry.audioEventId}.");
            }

            string sourceMeta = sourceAsset + ".meta";
            Match guidMatch = File.Exists(sourceMeta)
                ? Regex.Match(
                    File.ReadAllText(sourceMeta),
                    @"(?m)^guid: (?<value>[0-9a-fA-F]{32})$")
                : Match.Empty;
            if (!guidMatch.Success ||
                !string.Equals(
                    guidMatch.Groups["value"].Value,
                    entry.sourceClipGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Donor AudioClip GUID mismatch for {entry.audioEventId}.");
            }

            ValidatePcmWave(entry, sourceResource);
        }

        private static void ValidatePcmWave(
            EntrySpec entry,
            string sourceResource)
        {
            using FileStream stream = File.OpenRead(sourceResource);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (ReadFourCc(reader) != "RIFF")
            {
                throw new InvalidDataException(
                    $"Player voice resource is not a WAV container: {entry.audioEventId}.");
            }

            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
            {
                throw new InvalidDataException(
                    $"Player voice resource is not a WAV container: {entry.audioEventId}.");
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

            float pcmLength = pcmDataSize / (float)(sampleRate * blockAlign);
            if (Mathf.Abs(pcmLength - entry.sourcePcmLengthSeconds) > 0.0001f)
            {
                throw new InvalidDataException(
                    $"Donor WAV duration mismatch for {entry.audioEventId}. Expected {entry.sourcePcmLengthSeconds:F6}s, got {pcmLength:F6}s.");
            }
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            byte[] value = reader.ReadBytes(4);
            return value.Length == 4
                ? Encoding.ASCII.GetString(value)
                : string.Empty;
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
                    $"Hash-locked player voice source is missing: {relative}.",
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
                        $"Unity could not clear generated player voice output '{OutputRoot}'.");
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

        private static void WriteReport(Manifest manifest)
        {
            EntrySpec[] allEntries = GetAllEntries(manifest);
            string report = JsonUtility.ToJson(new BuildReportData
            {
                schemaVersion = manifest.schemaVersion,
                manifestId = manifest.manifestId,
                classification = manifest.classification,
                sourceSceneSha256 = manifest.source.sceneSha256,
                sourceManifestSha256 = ComputeHash(
                    ToFileSystemPath(ManifestPath)),
                clipCount = allEntries.Length,
                eventIds = allEntries
                    .Select(entry => entry.audioEventId)
                    .ToArray(),
                excludesDonorControllersAndAudioClipAssets = true,
            }, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(BuildReportPath),
                report,
                new UTF8Encoding(false));
        }

        private static EntrySpec[] GetAllEntries(Manifest manifest) =>
            manifest.entries.Concat(manifest.middleFingerEntries).ToArray();

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
            public string masterAudioGroup;
            public long masterAudioGroupComponentFileId;
            public long speechFsmComponentFileId;
            public long stressSimulationFsmComponentFileId;
            public long playerFunctionsFsmComponentFileId;
            public string middleFingerMasterAudioGroup;
            public long middleFingerMasterAudioGroupComponentFileId;
            public long middleFingerSubtitleTableComponentFileId;
            public SourceSpec source;
            public EntrySpec[] entries;
            public EntrySpec[] middleFingerEntries;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
        }

        [Serializable]
        private sealed class EntrySpec
        {
            public int variantIndex;
            public string audioEventId;
            public string fallbackSubtitleEnglish;
            public string fallbackSubtitleRussian;
            public long sourceVariationComponentFileId;
            public long sourceGameObjectId;
            public long sourceAudioSourceId;
            public string sourceClipGuid;
            public string sourceAssetRelativePath;
            public string sourceAssetSha256;
            public string sourceResourceRelativePath;
            public string sourceResourceSha256;
            public float donorSerializedLengthSeconds;
            public float sourcePcmLengthSeconds;
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
            public bool excludesDonorControllersAndAudioClipAssets;
        }
    }

    public sealed class Phase1PlayerVoiceBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -888;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                Phase1PlayerVoiceImporter.EnsureGeneratedForBuild();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "Mandatory Phase 1 player voice validation failed. " +
                    exception.Message);
            }
        }
    }
}
