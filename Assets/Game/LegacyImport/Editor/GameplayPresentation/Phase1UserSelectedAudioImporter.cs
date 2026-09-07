using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>One explicit private user selection; never mutates donor/default media.</summary>
    public static class Phase1UserSelectedAudioImporter
    {
        public const string ManifestPath = "Assets/Game/LegacyImport/Manifests/Phase1UserSelectedAudioManifest.json";
        public const string OutputRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Audio/UserSelected";
        public const string LibraryPath = OutputRoot +
            "/Resources/Phase1UserSelectedAudio/Phase1UserSelectedAudioEventLibrary.asset";

        [MenuItem("Tools/MSC Remake/Phase 1/Audio/Build User Selected Audio Only")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before selecting audio content.");
            Manifest manifest = ReadManifest();
            var local = JsonUtility.FromJson<LocalPaths>(File.ReadAllText("Config/UserAudioPack.local.json"));
            if (string.IsNullOrWhiteSpace(local?.SourceDirectory))
                throw new InvalidDataException("Configure SourceDirectory in ignored Config/UserAudioPack.local.json.");
            string sourceRoot = Path.GetFullPath(local.SourceDirectory);
            var files = manifest.AllFiles.ToDictionary(value => value.fileName, StringComparer.Ordinal);
            var templates = new Dictionary<string, UnityAudioEventDefinition>(StringComparer.Ordinal);
            var preparedAudio = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            // All hashes and mappings are checked before the first output write.
            foreach (SourceFile file in manifest.AllFiles)
                RequireHash(Contained(sourceRoot, file.relativePath), file.sha256);
            foreach (Mapping mapping in manifest.mappings)
                templates.Add(mapping.eventId, ReadTemplate(mapping));

            // Check each explicitly reviewed derivative before any output write.
            foreach (SourceFile file in manifest.AllFiles.Where(value => value.preparedEndFrame > 0))
            {
                Mapping[] uses = manifest.mappings.Where(value => value.sourceFileName == file.fileName).ToArray();
                if (uses.Length == 0 || file.preparedLoopCrossfadeFrames > 0 &&
                    uses.Any(value => !value.forceLoop && !templates[value.eventId].Loop))
                    throw new InvalidDataException("Prepared loop requires explicit looping event mappings.");
                byte[] prepared = UserAudioWavePreparation.Prepare(
                    File.ReadAllBytes(Contained(sourceRoot, file.relativePath)),
                    file.preparedEndFrame, file.preparedLoopCrossfadeFrames, file.preparedStartFrame);
                using var sha = SHA256.Create();
                string preparedHash = BitConverter.ToString(sha.ComputeHash(prepared)).Replace("-", "").ToLowerInvariant();
                if (!preparedHash.Equals(file.preparedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Prepared audio hash differs from the reviewed manifest: " + file.fileName);
                preparedAudio.Add(file.fileName, prepared);
            }

            EnsureFolder(OutputRoot + "/Clips");
            EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
            string[] selected = manifest.mappings.Select(value => value.sourceFileName).Distinct().ToArray();
            int changed = 0;
            foreach (string name in selected)
            {
                SourceFile spec = files[name];
                string destination = ClipPath(spec.fileName);
                string guid = FileGuid(spec);
                if (File.Exists(destination + ".meta") && !File.ReadAllText(destination + ".meta").Contains("guid: " + guid))
                    throw new InvalidDataException("User audio GUID drift: " + destination);
                if (!File.Exists(destination) || !Hash(destination).Equals(spec.PlaybackSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (preparedAudio.TryGetValue(name, out byte[] prepared)) File.WriteAllBytes(destination, prepared);
                    else File.Copy(Contained(sourceRoot, spec.relativePath), destination, true);
                    changed++;
                }
                if (!File.Exists(destination + ".meta"))
                    File.WriteAllText(destination + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(destination) as AudioImporter ??
                    throw new InvalidDataException("Unity cannot import user audio: " + name);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool modified = settings.loadType != AudioClipLoadType.CompressedInMemory ||
                    settings.compressionFormat != AudioCompressionFormat.Vorbis || settings.quality != 1f ||
                    !importer.forceToMono || importer.userData != "TemporaryDirectImport";
                if (modified)
                {
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 1f;
                    importer.defaultSampleSettings = settings;
                    // Spatial emitters require a point source. Original stereo
                    // source WAV bytes remain intact; only explicit manifested
                    // prepared copies remove reviewed starter silence.
                    importer.forceToMono = true;
                    importer.userData = "TemporaryDirectImport";
                    importer.SaveAndReimport();
                    changed++;
                }
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
                if (clip == null || clip.length <= 0f || clip.samples == 0)
                    throw new InvalidDataException("Imported user audio is empty: " + name);
            }
            UnityAudioEventLibrary library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            var definitions = manifest.mappings.Select(value => CreateDefinition(value, templates[value.eventId])).ToArray();
            if (!Matches(library, definitions))
            {
                library.ConfigureForAuthoring(definitions);
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
                changed++;
            }
            EnsureGeneratedForBuild();
            Debug.Log($"USER_SELECTED_AUDIO_BUILD_OK files={selected.Length} events={definitions.Length} " +
                $"derivatives={manifest.derivatives?.Length ?? 0} changed={changed} sourceUntouched=true");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = ReadManifest();
            UnityAudioEventLibrary library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            UnityAudioEventDefinition[] expected = manifest.mappings.Select(value => CreateDefinition(value, ReadTemplate(value))).ToArray();
            if (library == null || !library.Validate(out _) || !Matches(library, expected))
                throw new InvalidDataException("User-selected audio library is missing or stale.");
            var selected = new HashSet<string>(manifest.mappings.Select(value => value.sourceFileName));
            foreach (SourceFile source in manifest.AllFiles)
            {
                if (!selected.Contains(source.fileName)) continue;
                string path = ClipPath(source.fileName);
                RequireHash(path, source.PlaybackSha256);
                if (AssetDatabase.AssetPathToGUID(path) != FileGuid(source))
                    throw new InvalidDataException("User-selected audio GUID differs: " + source.fileName);
            }
        }

        private static UnityAudioEventDefinition ReadTemplate(Mapping mapping)
        {
            if (!mapping.templateLibrary.StartsWith("Assets/Game/", StringComparison.Ordinal) ||
                mapping.templateLibrary.Contains("..") || mapping.templateLibrary.StartsWith(OutputRoot, StringComparison.Ordinal))
                throw new InvalidDataException("Audio template must be an existing project library, not the replacement output.");
            var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(mapping.templateLibrary);
            if (library == null || !library.TryResolve(new AudioEventId(mapping.eventId), out var definition) || definition.Clip == null)
                throw new InvalidDataException("No existing corresponding audio definition: " + mapping.eventId);
            return definition;
        }

        private static UnityAudioEventDefinition CreateDefinition(Mapping mapping, UnityAudioEventDefinition template)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(mapping.sourceFileName)) ??
                throw new InvalidDataException("Replacement audio clip is not imported: " + mapping.sourceFileName);
            var entry = new UnityAudioEventDefinition();
            entry.ConfigureForAuthoring(mapping.eventId, clip, template.Category, template.Loop || mapping.forceLoop,
                template.Volume, template.Pitch, template.SpatialBlend, template.MinimumDistanceMeters, template.MaximumDistanceMeters);
            entry.ConfigureParameterBindingsForAuthoring(template.VolumeParameterId, template.PitchParameterId);
            entry.ConfigureCalibrationForAuthoring(mapping.calibrationGainDb);
            entry.ConfigureMixGainForAuthoring(template.MixGainDb, template.MixBoostCeiling);
            entry.ConfigureOutputGainCeilingForAuthoring(mapping.outputGainCeiling);
            entry.ConfigureDistanceRolloffForAuthoring(mapping.logarithmic || template.RolloffMode == AudioRolloffMode.Logarithmic
                ? UnityAudioDistanceRolloff.Logarithmic : UnityAudioDistanceRolloff.Linear);
            return entry;
        }

        private static bool Matches(UnityAudioEventLibrary library, UnityAudioEventDefinition[] expected)
        {
            if (library.DefinitionCount != expected.Length) return false;
            foreach (UnityAudioEventDefinition value in expected)
            {
                if (!library.TryResolve(new AudioEventId(value.EventId), out var actual) ||
                    actual.Clip != value.Clip || actual.Category != value.Category || actual.Loop != value.Loop ||
                    actual.Volume != value.Volume || actual.Pitch != value.Pitch || actual.SpatialBlend != value.SpatialBlend ||
                    actual.MinimumDistanceMeters != value.MinimumDistanceMeters || actual.MaximumDistanceMeters != value.MaximumDistanceMeters ||
                    actual.VolumeParameterId != value.VolumeParameterId || actual.PitchParameterId != value.PitchParameterId ||
                    actual.RolloffMode != value.RolloffMode ||
                    actual.CalibrationGainDb != value.CalibrationGainDb ||
                    actual.MixGainDb != value.MixGainDb || actual.MixBoostCeiling != value.MixBoostCeiling ||
                    actual.OutputGainCeiling != value.OutputGainCeiling) return false;
            }
            return true;
        }

        private static Manifest ReadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.classification != "TemporaryDirectImport" ||
                string.IsNullOrWhiteSpace(manifest.productionReplacementKey) || manifest.files == null || manifest.mappings == null)
                throw new InvalidDataException("Invalid user audio manifest.");
            var files = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceFile file in manifest.AllFiles)
                if (file == null || string.IsNullOrEmpty(file.fileName) || Path.GetFileName(file.fileName) != file.fileName ||
                    !files.Add(file.fileName) || file.sha256?.Length != 64 ||
                    file.preparedStartFrame < 0 || file.preparedEndFrame < 0 || file.preparedLoopCrossfadeFrames < 0 ||
                    file.preparedStartFrame > 0 && file.preparedEndFrame <= file.preparedStartFrame ||
                    file.preparedEndFrame == 0 && (file.preparedLoopCrossfadeFrames != 0 || !string.IsNullOrEmpty(file.preparedSha256)) ||
                    file.preparedEndFrame > 0 && (file.preparedSha256?.Length != 64 ||
                        !file.preparedSha256.All(Uri.IsHexDigit) || file.preparedLoopCrossfadeFrames >= file.preparedEndFrame - file.preparedStartFrame))
                    throw new InvalidDataException("Invalid user audio source record.");
            foreach (Mapping mapping in manifest.mappings)
                if (mapping == null || !files.Contains(mapping.sourceFileName) || !ids.Add(mapping.eventId) ||
                    !AudioIdValidation.TryValidate(mapping.eventId, out _) || string.IsNullOrEmpty(mapping.templateLibrary) ||
                    !float.IsFinite(mapping.calibrationGainDb) || Math.Abs(mapping.calibrationGainDb) > 12f ||
                    !float.IsFinite(mapping.outputGainCeiling) || mapping.outputGainCeiling < 0f || mapping.outputGainCeiling > 1f)
                    throw new InvalidDataException("Invalid or duplicate user audio event mapping.");
            return manifest;
        }

        private static string ClipPath(string name) => OutputRoot + "/Clips/" + name;
        private static string FileGuid(SourceFile file) => StableGuid(file.relativePath +
            (file.preparedStartFrame > 0 ? "/reviewed-derivative/" + file.fileName : string.Empty));
        private static string Contained(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Relative source path required.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(prefix, relative));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("Missing or out-of-bound user audio source: " + relative);
            return path;
        }
        private static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path) || !Hash(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("User audio hash mismatch: " + path);
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string StableGuid(string relative)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("msc.user-audio.20260906/" + relative)))
                .Replace("-", "").ToLowerInvariant().Substring(0, 32);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        [Serializable] private sealed class LocalPaths { public string SourceDirectory; }
        [Serializable] private sealed class Manifest
        {
            public int schemaVersion; public string classification; public string productionReplacementKey;
            public SourceFile[] files; public SourceFile[] derivatives; public Mapping[] mappings;
            public IEnumerable<SourceFile> AllFiles => files.Concat(derivatives ?? Array.Empty<SourceFile>());
        }
        [Serializable] private sealed class SourceFile
        {
            public string fileName; public string relativePath; public string sha256;
            public int preparedStartFrame; public int preparedEndFrame; public int preparedLoopCrossfadeFrames; public string preparedSha256;
            public string PlaybackSha256 => preparedEndFrame > 0 ? preparedSha256 : sha256;
        }
        [Serializable] private sealed class Mapping
        {
            public string sourceFileName; public string templateLibrary; public string eventId;
            public bool logarithmic; public bool forceLoop;
            public float calibrationGainDb;
            public float outputGainCeiling;
        }
    }
}
