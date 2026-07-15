using System;
using System.Collections.Generic;
using MSC.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Foundation
{
    public static class FoundationSceneBuilder
    {
        public const string BootstrapScenePath = "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string BootstrapVolumeProfilePath =
            "Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset";
        public const float BootstrapFixedExposure = 14f;

        [MenuItem("Tools/My Summer Car/Foundation/Create Missing Bootstrap Content")]
        public static void EnsureBootstrapContent()
        {
            VolumeProfile profile = CreateOrUpdateVolumeProfile();
            CreateBootstrapSceneIfMissing(profile);
            EnsureBootstrapIsFirstBuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Foundation: Bootstrap scene, HDRP volume profile, and build-scene entry are ready.");
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BootstrapVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "BootstrapGlobalVolume";
                AssetDatabase.CreateAsset(profile, BootstrapVolumeProfilePath);
            }

            VisualEnvironment visualEnvironment = GetOrAdd<VisualEnvironment>(profile);
            visualEnvironment.active = true;
            visualEnvironment.skyType.Override((int)SkyType.PhysicallyBased);
            visualEnvironment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            PhysicallyBasedSky sky = GetOrAdd<PhysicallyBasedSky>(profile);
            sky.active = true;
            sky.type.Override(PhysicallyBasedSkyModel.EarthSimple);
            sky.atmosphericScattering.Override(true);

            Exposure exposure = GetOrAdd<Exposure>(profile);
            exposure.active = true;
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(BootstrapFixedExposure);
            exposure.compensation.Override(0f);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Fog fog = GetOrAdd<Fog>(profile);
            fog.active = true;
            fog.enabled.Override(true);
            fog.colorMode.Override(FogColorMode.SkyColor);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(120f);
            fog.meanFreePath.Override(500f);
            fog.maxFogDistance.Override(3000f);
            fog.enableVolumetricFog.Override(true);
            fog.depthExtent.Override(256f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void CreateBootstrapSceneIfMissing(VolumeProfile profile)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null)
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var compositionRootObject = new GameObject("Game Composition Root");
            compositionRootObject.AddComponent<GameCompositionRoot>();

            var sunObject = new GameObject("Directional Sun");
            Light sun = sunObject.AddComponent<Light>();
            sunObject.AddComponent<HDAdditionalLightData>();
            sun.type = LightType.Directional;
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 100000f;
            sun.color = new Color(1f, 0.956f, 0.839f);
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;

            var volumeObject = new GameObject("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException($"Could not save Bootstrap scene to {BootstrapScenePath}.");
            }
        }

        private static void EnsureBootstrapIsFirstBuildScene()
        {
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            var updatedScenes = new List<EditorBuildSettingsScene>(existingScenes.Length + 1)
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };

            foreach (EditorBuildSettingsScene existingScene in existingScenes)
            {
                if (string.Equals(existingScene.path, BootstrapScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                updatedScenes.Add(existingScene);
            }

            EditorBuildSettings.scenes = updatedScenes.ToArray();
        }
    }
}
