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
    /// <summary>Hash-locked, repeatable private media import; never deletes a shared output.</summary>
    public static class Phase1SatsumaEngineAudioImporter
    {
        public const string ManifestPath = "Assets/Game/LegacyImport/Manifests/Phase1SatsumaEngineAudioManifest.json";
        public const string OutputRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Audio/SatsumaEngine";
        public const string LibraryPath = OutputRoot +
            "/Resources/Phase1SatsumaEngineAudio/Phase1SatsumaEngineAudioEventLibrary.asset";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Build Engine Audio Only")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before importing engine feedback.");
            Manifest manifest = LoadManifest();
            var paths = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string sourceRoot = Path.Combine(paths.DonorStagingDirectory, manifest.source.stagingRootRelativePath);
            RequireHash(Resolve(sourceRoot, manifest.source.sceneRelativePath), manifest.source.sceneSha256);
            // Preflight all sources before touching any generated media.
            foreach (ClipSpec clip in manifest.clips)
                RequireHash(Resolve(sourceRoot, clip.sourceRelativePath), clip.sourceSha256);
            EnsureFolder(OutputRoot + "/Clips");
            EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
            int changed = 0;
            foreach (ClipSpec clip in manifest.clips)
            {
                string destination = ClipPath(clip);
                if (File.Exists(destination + ".meta") && !File.ReadAllText(destination + ".meta")
                    .Contains("guid: " + clip.generatedGuid))
                    throw new InvalidDataException("Engine audio GUID drift: " + destination);
                if (!File.Exists(destination) || Hash(destination) != clip.sourceSha256)
                {
                    File.Copy(Resolve(sourceRoot, clip.sourceRelativePath), destination, true);
                    changed++;
                }
                if (!File.Exists(destination + ".meta"))
                {
                    File.WriteAllText(destination + ".meta", "fileFormatVersion: 2\nguid: " + clip.generatedGuid +
                        "\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n" +
                        "  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n" +
                        "    sampleRateSetting: 0\n    sampleRateOverride: 44100\n" +
                        "    compressionFormat: 1\n    quality: 1\n    conversionMode: 0\n" +
                        "    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 1\n" +
                        "  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n" +
                        "  userData: TemporaryDirectImport\n  assetBundleName:\n  assetBundleVariant:\n",
                        new UTF8Encoding(false));
                    changed++;
                }
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            }
            var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            if (!Matches(library, manifest))
            {
                library.ConfigureForAuthoring(manifest.clips.Select(CreateDefinition).ToArray());
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
                changed++;
            }
            EnsureGeneratedForBuild();
            Debug.Log("SATSUMA_ENGINE_AUDIO_BUILD_OK clips=" + manifest.clips.Length + " events=" +
                manifest.clips.Length + " changed=" + changed + " fullRebuild=false");
        }

        public static void EnsureGeneratedForBuild()
        {
            Manifest manifest = LoadManifest();
            var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            if (library == null)
                throw new InvalidDataException("The scoped Satsuma engine audio library is missing.");
            if (!library.Validate(out var failures))
                throw new InvalidDataException("Invalid scoped Satsuma engine audio: " + string.Join("; ", failures));
            if (!Matches(library, manifest))
                throw new InvalidDataException("The scoped Satsuma engine audio library is missing or stale.");
            foreach (ClipSpec clip in manifest.clips)
            {
                RequireHash(ClipPath(clip), clip.sourceSha256);
                if (AssetDatabase.AssetPathToGUID(ClipPath(clip)) != clip.generatedGuid)
                    throw new InvalidDataException("Generated engine audio GUID drift: " + clip.clipId);
            }
        }

        private static bool Matches(UnityAudioEventLibrary library, Manifest manifest)
        {
            if (library.DefinitionCount != manifest.clips.Length) return false;
            foreach (ClipSpec clip in manifest.clips)
            {
                if (!library.TryResolve(new AudioEventId(clip.eventId), out var entry) ||
                    AssetDatabase.GetAssetPath(entry.Clip) != ClipPath(clip) || entry.Loop != clip.loop ||
                    entry.Category != UnityAudioCategory.Vehicle || entry.Volume != clip.volume ||
                    entry.MixGainDb != clip.mixGainDb || entry.CalibrationGainDb != 0f ||
                    entry.MixBoostCeiling != clip.mixBoostCeiling ||
                    entry.Pitch != clip.EffectivePitch || entry.SpatialBlend != 1f ||
                    entry.MinimumDistanceMeters != clip.minimumDistanceMeters ||
                    entry.MaximumDistanceMeters != clip.maximumDistanceMeters ||
                    entry.RolloffMode != (clip.logarithmic ? AudioRolloffMode.Logarithmic : AudioRolloffMode.Linear) ||
                    (entry.VolumeParameterId.Value ?? "") != clip.volumeParameterId ||
                    (entry.PitchParameterId.Value ?? "") != clip.pitchParameterId) return false;
            }
            return true;
        }

        private static UnityAudioEventDefinition CreateDefinition(ClipSpec clip)
        {
            var definition = new UnityAudioEventDefinition();
            AudioClip media = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(clip)) ??
                throw new InvalidDataException("Unity failed to import engine clip " + clip.clipId);
            definition.ConfigureForAuthoring(clip.eventId, media, UnityAudioCategory.Vehicle, clip.loop,
                clip.volume, clip.EffectivePitch, 1f, clip.minimumDistanceMeters, clip.maximumDistanceMeters);
            definition.ConfigureMixGainForAuthoring(clip.mixGainDb, clip.mixBoostCeiling);
            definition.ConfigureDistanceRolloffForAuthoring(clip.logarithmic
                ? UnityAudioDistanceRolloff.Logarithmic : UnityAudioDistanceRolloff.Linear);
            definition.ConfigureParameterBindingsForAuthoring(
                string.IsNullOrEmpty(clip.volumeParameterId) ? default : new AudioParameterId(clip.volumeParameterId),
                string.IsNullOrEmpty(clip.pitchParameterId) ? default : new AudioParameterId(clip.pitchParameterId));
            return definition;
        }

        private static Manifest LoadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.clips == null || manifest.clips.Length != 17 ||
                manifest.classification != "TemporaryDirectImport" || manifest.source == null ||
                string.IsNullOrEmpty(manifest.productionReplacementKey)) throw new InvalidDataException("Invalid engine audio manifest.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ClipSpec clip in manifest.clips)
            {
                if (clip == null || !ids.Add(clip.eventId) || !AudioIdValidation.TryValidate(clip.eventId, out _) ||
                    clip.generatedGuid?.Length != 32 || clip.sourceSha256?.Length != 64 ||
                    Path.GetFileName(clip.generatedFileName) != clip.generatedFileName ||
                    string.IsNullOrEmpty(clip.generatedFileName) || !float.IsFinite(clip.EffectivePitch) ||
                    clip.EffectivePitch < .1f || clip.EffectivePitch > 3f ||
                    !float.IsFinite(clip.mixGainDb) || clip.mixGainDb < -12f || clip.mixGainDb > 18f ||
                    !float.IsFinite(clip.mixBoostCeiling) || clip.mixBoostCeiling < 0f || clip.mixBoostCeiling > 1f)
                    throw new InvalidDataException("Invalid engine clip entry.");
            }
            return manifest;
        }

        private static string ClipPath(ClipSpec clip) => OutputRoot + "/Clips/" + clip.generatedFileName;
        private static string Resolve(string root, string relative)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("Missing or out-of-bound engine media: " + relative);
            return path;
        }
        private static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path) || !string.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Engine media hash mismatch: " + path);
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        [Serializable] private sealed class Manifest
        {
            public int schemaVersion; public string classification; public string productionReplacementKey;
            public Source source; public ClipSpec[] clips;
        }
        [Serializable] private sealed class Source
        {
            public string stagingRootRelativePath; public string sceneRelativePath; public string sceneSha256;
        }
        [Serializable] private sealed class ClipSpec
        {
            public string clipId; public string sourceRelativePath; public string sourceSha256;
            public string generatedFileName; public string generatedGuid; public string eventId;
            public bool loop; public float volume; public float minimumDistanceMeters; public float maximumDistanceMeters;
            public bool logarithmic;
            public float mixGainDb;
            public float mixBoostCeiling;
            public float pitch;
            public float EffectivePitch => pitch == 0f ? 1f : pitch;
            public string volumeParameterId; public string pitchParameterId;
        }
    }
}
