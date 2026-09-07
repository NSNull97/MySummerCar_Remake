using System;
using System.IO;
using System.Security.Cryptography;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Imports one reviewed light-switch clip; never imports a donor runtime owner.</summary>
    public static class Phase1LightingSwitchAudioImporter
    {
        public const string ManifestPath = "Assets/Game/LegacyImport/Manifests/Phase1LightingSwitchAudioManifest.json";
        public const string OutputRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Audio/LightingSwitch";
        public const string ResourcesPath = "Phase1LightingSwitchAudio/Phase1LightingSwitchAudioEventLibrary";
        public const string LibraryPath = OutputRoot + "/Resources/" + ResourcesPath + ".asset";
        public const string ClipPath = OutputRoot + "/Clips/house_light_switch.ogg";
        public const string EventId = "audio.event.lighting.switch";

        [MenuItem("Tools/MSC Remake/Phase 1/Audio/Build Lighting Switch Audio Only")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before importing lighting audio.");

            Specification spec = LoadSpecification();
            var paths = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string sourceRoot = ResolveChild(paths.DonorStagingDirectory, spec.stagingRootRelativePath);
            // Complete source preflight before touching any generated content.
            RequireHash(ResolveChild(sourceRoot, spec.sceneRelativePath), spec.sceneSha256);
            string sourceClip = ResolveChild(sourceRoot, spec.clipRelativePath);
            RequireHash(sourceClip, spec.clipSha256);
            bool clipExists = File.Exists(ClipPath);
            if (clipExists) RequireHash(ClipPath, spec.clipSha256);

            EnsureFolder(OutputRoot + "/Clips");
            EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
            int changed = 0;
            if (!clipExists)
            {
                File.Copy(sourceClip, ClipPath, overwrite: false);
                changed++;
            }

            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter ??
                throw new InvalidDataException("The lighting switch clip did not import as audio.");
            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
            if (!importer.forceToMono || importer.loadInBackground ||
                sampleSettings.loadType != AudioClipLoadType.DecompressOnLoad ||
                !sampleSettings.preloadAudioData || importer.userData != "TemporaryDirectImport")
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                sampleSettings.preloadAudioData = true;
                importer.defaultSampleSettings = sampleSettings;
                importer.userData = "TemporaryDirectImport";
                importer.SaveAndReimport();
                changed++;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath) ??
                throw new InvalidDataException("The lighting switch AudioClip is unavailable.");
            var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            if (library == null)
            {
                if (File.Exists(LibraryPath))
                    throw new InvalidDataException("Lighting audio library path is occupied by another asset.");
                library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                changed++;
            }

            if (!Matches(library, clip))
            {
                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring(EventId, clip, UnityAudioCategory.Effects,
                    false, 1f, 1f, 1f, 1f, 10f);
                library.ConfigureForAuthoring(new[] { definition });
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
                changed++;
            }

            EnsureGeneratedForBuild();
            Debug.Log("LIGHTING_SWITCH_AUDIO_BUILD_OK clips=1 events=1 changed=" + changed +
                " fullRebuild=false nativeSaveWrites=false");
        }

        public static void EnsureGeneratedForBuild()
        {
            Specification spec = LoadSpecification();
            RequireHash(ClipPath, spec.clipSha256);
            var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(LibraryPath);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null || library == null || !library.Validate(out _) || !Matches(library, clip))
                throw new InvalidDataException("The lighting switch event library is missing or stale.");
        }

        private static bool Matches(UnityAudioEventLibrary library, AudioClip clip) =>
            library.DefinitionCount == 1 &&
            library.TryResolve(new AudioEventId(EventId), out var entry) &&
            entry.Clip == clip && entry.Category == UnityAudioCategory.Effects && !entry.Loop &&
            entry.Volume == 1f && entry.Pitch == 1f && entry.SpatialBlend == 1f &&
            entry.MinimumDistanceMeters == 1f && entry.MaximumDistanceMeters == 10f &&
            entry.VolumeParameterId.IsEmpty && entry.PitchParameterId.IsEmpty;

        private static Specification LoadSpecification()
        {
            var spec = JsonUtility.FromJson<Specification>(File.ReadAllText(ManifestPath));
            if (spec == null || spec.schemaVersion != 1 || spec.eventId != EventId ||
                spec.classification != "TemporaryDirectImport" ||
                string.IsNullOrWhiteSpace(spec.productionReplacementKey) ||
                spec.sceneSha256?.Length != 64 || spec.clipSha256?.Length != 64)
                throw new InvalidDataException("Invalid lighting switch audio manifest.");
            return spec;
        }

        private static string ResolveChild(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("A relative lighting audio source path is required.");
            string absoluteRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string absolute = Path.GetFullPath(Path.Combine(absoluteRoot, relative));
            if (!absolute.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lighting audio source path escapes its configured root.");
            return absolute;
        }

        private static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Lighting audio source is missing.", path);
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lighting audio content hash mismatch: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Populated from the reviewed JSON manifest by JsonUtility.
#pragma warning disable CS0649
        [Serializable]
        private sealed class Specification
        {
            public int schemaVersion;
            public string eventId;
            public string classification;
            public string productionReplacementKey;
            public string stagingRootRelativePath;
            public string sceneRelativePath;
            public string sceneSha256;
            public string clipRelativePath;
            public string clipSha256;
        }
#pragma warning restore CS0649
    }
}
