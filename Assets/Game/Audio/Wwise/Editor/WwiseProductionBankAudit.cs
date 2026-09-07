using System;
using System.Collections.Generic;
using System.IO;
using MSC.Audio.UnityFallback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.Wwise.Editor
{
    /// <summary>
    /// Read-only static production-route audit. Does not start the game, modify
    /// its scenes or save data, generate banks, or certify real audio output.
    /// </summary>
    public static class WwiseProductionBankAudit
    {
        private const string BootstrapPath = "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string EventMapPath = "Assets/Game/Audio/Content/AudioEventMap.asset";

        // These are the runtime Resources loads in the production
        // installer. NPC/traffic and other authored libraries are collected
        // from the actual Bootstrap backend arrays, not invented here.
        private static readonly string[] RuntimeLibraryResources =
        {
            "Phase1SatsumaAssemblyAudio/Phase1SatsumaAssemblyAudioEventLibrary",
            "Phase1SatsumaEngineAudio/Phase1SatsumaEngineAudioEventLibrary",
            "Phase1PlayerVoice/Phase1PlayerVoiceAudioEventLibrary",
            "Phase1LightingSwitchAudio/Phase1LightingSwitchAudioEventLibrary",
        };

        // The read-only WWU audit proves these old Events have no Action.
        // Keeping them named here labels debt; it does NOT remove them from
        // the unresolved route report or pronounce their content complete.
        private static readonly HashSet<string> KnownEmptyAuthoring = new HashSet<string>(StringComparer.Ordinal)
        {
            "audio.event.vehicle.transmission.loop", "audio.event.vehicle.body.rattle",
            "audio.event.vehicle.suspension.impact", "audio.event.vehicle.tire.skid",
            "audio.event.world.garage.roomtone", "audio.event.world.interior.roomtone",
            "audio.event.world.distant_traffic", "audio.event.interaction.tool.use",
            "audio.event.interaction.fastener.insert", "audio.event.interaction.fastener.tighten",
            "audio.event.interaction.fastener.loosen", "audio.event.interaction.part.install",
            "audio.event.interaction.part.remove", "audio.event.ui.navigate",
            "audio.event.ui.confirm", "audio.event.ui.cancel",
            "audio.event.ui.save.feedback", "audio.event.ui.load.feedback",
        };

        [MenuItem("Tools/MSC Remake/Audio/Audit Production Source Banks (Read Only)")]
        public static void RunSourceBankAudit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run the static source-bank audit outside Play Mode.");

            AudioEventMap eventMap = AssetDatabase.LoadAssetAtPath<AudioEventMap>(EventMapPath);
            if (eventMap == null) throw new FileNotFoundException("Production AudioEventMap is missing.", EventMapPath);
            string bankRoot = Path.Combine(
                Path.GetDirectoryName(AkWwiseEditorSettings.WwiseProjectAbsolutePath) ?? string.Empty,
                "GeneratedSoundBanks", "Windows");

            WwiseBankContentReport raw = WwiseBankContentValidator.Inspect(bankRoot, eventMap.Entries);
            foreach (WwiseBankContentIssue issue in raw.Issues)
                Debug.Log("MSC_WWISE_RAW_CONTENT_GAP " + issue);

            var explicitLibraries = new List<UnityAudioEventLibrary>();
            Scene preview = EditorSceneManager.OpenPreviewScene(BootstrapPath);
            try
            {
                int backendCount = 0;
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    foreach (UnityAudioBackend backend in root.GetComponentsInChildren<UnityAudioBackend>(true))
                    {
                        backendCount++;
                        var serialized = new SerializedObject(backend);
                        CollectLibraryArray(serialized, "supplementalEventLibraries", explicitLibraries);
                        CollectLibraryArray(serialized, "overrideEventLibraries", explicitLibraries);
                    }
                }

                if (backendCount != 1)
                    throw new InvalidDataException($"Expected one production Unity audio backend, found {backendCount}.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            foreach (string resourcesPath in RuntimeLibraryResources)
            {
                UnityAudioEventLibrary library = Resources.Load<UnityAudioEventLibrary>(resourcesPath);
                if (library == null)
                    throw new InvalidDataException($"Production runtime audio library is missing: {resourcesPath}.");
                if (!explicitLibraries.Contains(library)) explicitLibraries.Add(library);
            }

            var explicitOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UnityAudioEventLibrary library in explicitLibraries)
            {
                if (!library.Validate(out string[] failures))
                    throw new InvalidDataException(AssetDatabase.GetAssetPath(library) + ": " + string.Join("; ", failures));
                string libraryPath = AssetDatabase.GetAssetPath(library);
                foreach (UnityAudioEventDefinition definition in library.Definitions)
                {
                    if (definition.Clip.samples <= 0 || definition.Clip.length <= 0f)
                        throw new InvalidDataException($"Clip has no sample/length evidence: {definition.EventId} in {libraryPath}.");
                    if (explicitOwners.TryGetValue(definition.EventId, out string previousOwner) &&
                        !string.Equals(previousOwner, libraryPath, StringComparison.Ordinal))
                        throw new InvalidDataException($"Ambiguous declared Unity ownership: {definition.EventId}: {previousOwner}, {libraryPath}.");
                    explicitOwners[definition.EventId] = libraryPath;
                }
            }

            var expectedWwiseRoutes = new List<AudioEventMapEntry>();
            int routedUnityMapped = 0;
            foreach (AudioEventMapEntry entry in eventMap.Entries)
            {
                if (entry != null && explicitOwners.TryGetValue(entry.StableId, out string owner))
                {
                    routedUnityMapped++;
                    Debug.Log($"MSC_WWISE_EXPLICIT_UNITY_ROUTE {entry.StableId} library={owner}");
                }
                else expectedWwiseRoutes.Add(entry);
            }

            foreach (KeyValuePair<string, string> owner in explicitOwners)
                Debug.Log($"MSC_UNITY_DECLARED_CONTENT {owner.Key} library={owner.Value}");

            WwiseBankContentReport resolved = WwiseBankContentValidator.Inspect(bankRoot, expectedWwiseRoutes);
            int unexpectedIssues = 0;
            foreach (WwiseBankContentIssue issue in resolved.Issues)
            {
                bool known = issue.Kind == WwiseBankContentFailure.EmptyPlaybackEvent &&
                    KnownEmptyAuthoring.Contains(issue.StableId);
                if (!known) unexpectedIssues++;
                Debug.Log((known ? "MSC_WWISE_KNOWN_UNRESOLVED_CONTENT " : "MSC_WWISE_UNEXPECTED_CONTENT_FAILURE ") + issue);
            }

            Debug.Log($"MSC_WWISE_SOURCE_AUDIT_COMPLETED rawRoutes={raw.InspectedRouteCount} rawIssues={raw.Issues.Count} " +
                $"explicitUnityMapped={routedUnityMapped} explicitUnityTotal={explicitOwners.Count} " +
                $"wwiseRoutes={resolved.InspectedRouteCount} unresolvedContent={resolved.Issues.Count} unexpected={unexpectedIssues} " +
                "notAudibleVerification=true bankGeneration=false saveWrites=false");
            if (unexpectedIssues != 0)
                throw new InvalidDataException($"Production audio audit has {unexpectedIssues} unexpected Wwise route failures; inspect the complete audit log.");
        }

        private static void CollectLibraryArray(SerializedObject owner, string field,
            List<UnityAudioEventLibrary> libraries)
        {
            SerializedProperty array = owner.FindProperty(field);
            if (array == null || !array.isArray)
                throw new InvalidDataException($"Production Unity audio field is missing: {field}.");
            for (int index = 0; index < array.arraySize; index++)
            {
                UnityAudioEventLibrary library =
                    array.GetArrayElementAtIndex(index).objectReferenceValue as UnityAudioEventLibrary;
                if (library == null) throw new InvalidDataException($"Missing {field}[{index}] audio library.");
                if (!libraries.Contains(library)) libraries.Add(library);
            }
        }
    }
}
