using System;
using System.IO;
using MSC.Audio.Composition;
using MSC.Audio.UnityFallback;
using MSC.Audio.VehicleIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.Wwise.Editor
{
    public static class VehicleAudioPlaytestAuthoring
    {
        public const string ScenePath =
            "Assets/Game/Audio/Content/Scenes/M08_VehicleAudioPlaytest.unity";

        private static readonly string[] RequiredBanks =
        {
            "Init",
            "MSC_Vehicle",
            "MSC_Weather",
            "MSC_World",
            "MSC_Interaction",
            "MSC_UI",
        };

        [MenuItem("Tools/MSC Remake/Audio/Build M08 Vehicle Audio Playtest")]
        public static void Build()
        {
            AudioEventMap eventMap = LoadRequired<AudioEventMap>(
                "Assets/Game/Audio/Content/AudioEventMap.asset");
            AudioParameterMap parameterMap = LoadRequired<AudioParameterMap>(
                "Assets/Game/Audio/Content/AudioParameterMap.asset");
            WwiseBackendNameMap nameMap = LoadRequired<WwiseBackendNameMap>(
                "Assets/Game/Audio/Content/WwiseBackendNameMap.asset");
            UnityAudioEventLibrary fallbackLibrary =
                LoadRequired<UnityAudioEventLibrary>(
                    "Assets/Game/Audio/Content/UnityAudioEventLibrary.asset");

            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Assets/Game/Audio/Content/Scenes")));
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("M08_VehicleAudioPlaytest");
            AkInitializer initializer = root.AddComponent<AkInitializer>();
            initializer.InitializationSettings = AkWwiseInitializationSettings.Instance;
            WwiseAudioBackend wwise = root.AddComponent<WwiseAudioBackend>();
            UnityAudioBackend fallback = root.AddComponent<UnityAudioBackend>();
            AudioBackendRouter router = root.AddComponent<AudioBackendRouter>();
            WwiseRuntimeBankOwner bankOwner = root.AddComponent<WwiseRuntimeBankOwner>();
            VehicleAudioPlaytestBootstrap playtest =
                root.AddComponent<VehicleAudioPlaytestBootstrap>();

            wwise.ConfigureForAuthoring(
                eventMap,
                parameterMap,
                nameMap,
                true,
                RequiredBanks);
            fallback.ConfigureForAuthoring(fallbackLibrary);
            bankOwner.ConfigureForAuthoring(wwise, RequiredBanks);
            router.Configure(wwise, fallback, scanLoadedScenes: true);
            playtest.ConfigureForAuthoring(
                router,
                bankOwner,
                VehicleAudioPlaytestBootstrap.DefaultPrototypeSceneName);

            EditorUtility.SetDirty(initializer);
            EditorUtility.SetDirty(wwise);
            EditorUtility.SetDirty(fallback);
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(bankOwner);
            EditorUtility.SetDirty(playtest);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    "Failed to save the M08 vehicle audio playtest scene.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "M08_VEHICLE_AUDIO_PLAYTEST_BUILD_OK scene=" + ScenePath +
                " source=VehicleSimulationPrototype route=AudioBackendRouter");
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required M08 audio asset is missing: {path}. " +
                    "Run Build Production Audio Composition first.");
            }

            return asset;
        }
    }
}
