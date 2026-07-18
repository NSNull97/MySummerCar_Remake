using System;
using System.Linq;
using Enviro;
using MSC.Weather.Enviro3Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class Enviro3BuildSettingsTests
    {
        private const string WeatherLabVolumeProfilePath =
            "Assets/Game/Development/WeatherLab/Content/Profiles/WeatherLabHDRPVolume.asset";
        private const string WeatherLabBindingsPath =
            "Assets/Game/Development/WeatherLab/Content/Profiles/WeatherLabEnviro3Bindings.asset";
        private const string QualityRoot =
            "Assets/Enviro 3 - Sky and Weather/Profiles/Quality/";

        [Test]
        public void WeatherLab_IsAbsentFromEditorBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                string normalizedPath = (scenes[index].path ?? string.Empty).Replace('\\', '/');
                Assert.That(
                    normalizedPath.IndexOf("/WeatherLab/", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0),
                    "WeatherLab is a development-only scene and must not be present in EditorBuildSettings: " +
                    normalizedPath);
            }
        }

        [Test]
        public void WeatherLabVolumeProfile_PersistsRequiredComponentsAsSubAssets()
        {
            VolumeProfile profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(WeatherLabVolumeProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.components, Has.Count.EqualTo(4));
            Assert.That(profile.components, Has.None.Null);

            string[] componentTypes = profile.components
                .Select(component => component.GetType().Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                componentTypes,
                Is.EqualTo(new[] { "EnviroHDRPSky", "Exposure", "Fog", "VisualEnvironment" }));

            VolumeComponent[] persistedComponents = AssetDatabase
                .LoadAllAssetsAtPath(WeatherLabVolumeProfilePath)
                .OfType<VolumeComponent>()
                .ToArray();
            Assert.That(
                persistedComponents.Select(component => component.GetType().Name)
                    .OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(componentTypes),
                "The profile must contain exactly the four referenced component sub-assets.");

            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    profile,
                    out string profileGuid,
                    out long profileLocalId),
                Is.True);
            foreach (VolumeComponent component in profile.components)
            {
                Assert.That(
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        component,
                        out string componentGuid,
                        out long componentLocalId),
                    Is.True,
                    $"{component.GetType().Name} has no persistent asset identity.");
                Assert.That(componentGuid, Is.EqualTo(profileGuid));
                Assert.That(componentLocalId, Is.Not.EqualTo(profileLocalId));
                Assert.That(
                    AssetDatabase.GetAssetPath(component),
                    Is.EqualTo(WeatherLabVolumeProfilePath));
            }
        }

        [Test]
        public void WeatherLabBindings_UseDirectLowMediumHighVendorQualityAssets()
        {
            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(
                    WeatherLabBindingsPath);

            Assert.That(bindings, Is.Not.Null);
            AssertQualityPath(bindings.Low, QualityRoot + "Low.asset");
            AssertQualityPath(bindings.Medium, QualityRoot + "Medium.asset");
            AssertQualityPath(bindings.High, QualityRoot + "High.asset");
        }

        private static void AssertQualityPath(
            EnviroQuality quality,
            string expectedPath)
        {
            Assert.That(quality, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(quality), Is.EqualTo(expectedPath));
        }
    }
}
