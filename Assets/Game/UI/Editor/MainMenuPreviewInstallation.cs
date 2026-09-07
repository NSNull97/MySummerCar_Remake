using System;
using MSC.Bootstrap;
using MSC.UI.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.UI.EditorTools
{
    /// <summary>Bounded menu authoring: generated presentation plus two explicit Bootstrap references.</summary>
    public static class MainMenuPreviewInstallation
    {
        public const string BackdropShaderPath =
            "Assets/Game/UI/Presentation/Content/Shaders/MainMenuGaragePlate.shader";

        [MenuItem("Tools/My Summer Car/UI/Main Menu/Build and Wire Vehicle Preview")]
        public static void BuildAndWire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before authoring the menu preview.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save open scene changes before authoring the menu preview.");

            MainMenuVehicleAuthoring.Build();
            MainMenuEnvironmentAuthoring.Build();
            Scene scene = SceneManager.GetSceneByPath(ProductionUiAuthoring.BootstrapScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            bool replaceCleanUntitled = SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetSceneAt(0).path);
            try
            {
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(ProductionUiAuthoring.BootstrapScenePath,
                        replaceCleanUntitled ? OpenSceneMode.Single : OpenSceneMode.Additive);
                ProductionUiInstaller installer = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (ProductionUiInstaller candidate in root.GetComponentsInChildren<ProductionUiInstaller>(true))
                    {
                        if (installer != null) throw new InvalidOperationException("Duplicate production UI installer.");
                        installer = candidate;
                    }
                if (installer == null) throw new InvalidOperationException("Bootstrap has no production UI installer.");
                var prefab = AssetDatabase.LoadAssetAtPath<MainMenuVehicleModel>(MainMenuVehicleAuthoring.PrefabPath);
                var environment = AssetDatabase.LoadAssetAtPath<MainMenuEnvironmentModel>(MainMenuEnvironmentAuthoring.PrefabPath);
                if (prefab == null || environment == null)
                    throw new InvalidOperationException("Generated menu vehicle or home environment is missing.");
                installer.ConfigureMenuEnvironmentForAuthoring(prefab, environment);
                if (!installer.TryValidateAuthoringConfiguration(out string failure))
                    throw new InvalidOperationException(failure);
                EditorUtility.SetDirty(installer);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ProductionUiAuthoring.BootstrapScenePath))
                    throw new InvalidOperationException("Could not save the menu preview references in Bootstrap.");
                AssetDatabase.SaveAssets();
                Debug.Log("Main menu vehicle preview generated and wired; build scene configuration untouched.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                {
                    if (replaceCleanUntitled)
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
