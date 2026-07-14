using MSC.Bootstrap;
using MSC.Editor.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.Foundation
{
    public sealed class FoundationContentTests
    {
        [Test]
        public void BootstrapScene_ContainsCompositionRootSunAndGlobalVolume()
        {
            Scene scene = EditorSceneManager.OpenScene(
                FoundationSceneBuilder.BootstrapScenePath,
                OpenSceneMode.Single);

            GameCompositionRoot compositionRoot = null;
            Light directionalSun = null;
            Volume globalVolume = null;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                compositionRoot ??= rootObject.GetComponent<GameCompositionRoot>();

                Light light = rootObject.GetComponent<Light>();
                if (light != null && light.type == LightType.Directional)
                {
                    directionalSun = light;
                }

                Volume volume = rootObject.GetComponent<Volume>();
                if (volume != null && volume.isGlobal)
                {
                    globalVolume = volume;
                }
            }

            Assert.That(compositionRoot, Is.Not.Null);
            Assert.That(compositionRoot.IsInitialized, Is.False);
            Assert.That(directionalSun, Is.Not.Null);
            Assert.That(directionalSun.intensity, Is.GreaterThan(0f));
            Assert.That(globalVolume, Is.Not.Null);
            Assert.That(globalVolume.sharedProfile, Is.Not.Null);
        }

        [Test]
        public void BootstrapVolume_UsesPhysicalSkyAcesExposureAndVolumetricFog()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                FoundationSceneBuilder.BootstrapVolumeProfilePath);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryGet(out VisualEnvironment visualEnvironment), Is.True);
            Assert.That(visualEnvironment.skyType.value, Is.EqualTo((int)SkyType.PhysicallyBased));
            Assert.That(profile.TryGet(out PhysicallyBasedSky _), Is.True);
            Assert.That(profile.TryGet(out Exposure exposure), Is.True);
            Assert.That(exposure.mode.value, Is.EqualTo(ExposureMode.Fixed));
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
            Assert.That(profile.TryGet(out Fog fog), Is.True);
            Assert.That(fog.enabled.value, Is.True);
            Assert.That(fog.enableVolumetricFog.value, Is.True);
        }

        [Test]
        public void BootstrapScene_IsFirstEnabledBuildScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Is.Not.Empty);
            Assert.That(scenes[0].path, Is.EqualTo(FoundationSceneBuilder.BootstrapScenePath));
            Assert.That(scenes[0].enabled, Is.True);
        }
    }
}
