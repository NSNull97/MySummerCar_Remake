using System.Collections.Generic;
using Enviro;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class Enviro3EnvironmentBindingsTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        private Enviro3EnvironmentBindings bindings;
        private EnviroWeatherType clear;
        private EnviroWeatherType partlyCloudy;
        private EnviroWeatherType overcast;
        private EnviroWeatherType rain;
        private EnviroWeatherType storm;
        private EnviroWeatherType fog;
        private EnviroQuality low;
        private EnviroQuality medium;
        private EnviroQuality high;
        private EnviroEffectsModule effectsSource;

        [SetUp]
        public void SetUp()
        {
            bindings = Create<Enviro3EnvironmentBindings>();
            EnviroConfiguration configuration = Create<EnviroConfiguration>();
            effectsSource = Create<EnviroEffectsModule>();
            clear = Create<EnviroWeatherType>();
            partlyCloudy = Create<EnviroWeatherType>();
            overcast = Create<EnviroWeatherType>();
            rain = Create<EnviroWeatherType>();
            storm = Create<EnviroWeatherType>();
            fog = Create<EnviroWeatherType>();
            low = Create<EnviroQuality>();
            medium = Create<EnviroQuality>();
            high = Create<EnviroQuality>();

            bindings.ConfigureForAuthoring(
                configuration,
                effectsSource,
                clear,
                partlyCloudy,
                overcast,
                rain,
                storm,
                fog,
                low,
                medium,
                high);
        }

        [Test]
        public void EffectsSource_ResolvesToExactAssignedModule()
        {
            Assert.That(bindings.EffectsSource, Is.SameAs(effectsSource));
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void StableWeatherIds_ResolveToExactAssignedAssetsAndPresetKinds()
        {
            AssertWeather(
                Enviro3EnvironmentBindings.ClearIdValue,
                clear,
                EnvironmentPresentationPresetKind.Clear);
            AssertWeather(
                Enviro3EnvironmentBindings.PartlyCloudyIdValue,
                partlyCloudy,
                EnvironmentPresentationPresetKind.Overcast);
            AssertWeather(
                Enviro3EnvironmentBindings.OvercastIdValue,
                overcast,
                EnvironmentPresentationPresetKind.Overcast);
            AssertWeather(
                Enviro3EnvironmentBindings.DrizzleIdValue,
                rain,
                EnvironmentPresentationPresetKind.Rain);
            AssertWeather(
                Enviro3EnvironmentBindings.RainIdValue,
                rain,
                EnvironmentPresentationPresetKind.Rain);
            AssertWeather(
                Enviro3EnvironmentBindings.HeavyRainIdValue,
                rain,
                EnvironmentPresentationPresetKind.Rain);
            AssertWeather(
                Enviro3EnvironmentBindings.StormIdValue,
                storm,
                EnvironmentPresentationPresetKind.Storm);
            AssertWeather(
                Enviro3EnvironmentBindings.NightIdValue,
                clear,
                EnvironmentPresentationPresetKind.Night);
            AssertWeather(
                Enviro3EnvironmentBindings.FogIdValue,
                fog,
                EnvironmentPresentationPresetKind.Mist);
            AssertWeather(
                Enviro3EnvironmentBindings.DenseFogIdValue,
                fog,
                EnvironmentPresentationPresetKind.Mist);
        }

        [Test]
        public void QualityTierNumericValues_PreserveExistingSerializedContract()
        {
            Assert.That((int)EnvironmentQualityTier.Low, Is.Zero);
            Assert.That((int)EnvironmentQualityTier.High, Is.EqualTo(1));
            Assert.That((int)EnvironmentQualityTier.Medium, Is.EqualTo(2));
        }

        [Test]
        public void LowMediumAndHighQualityTiers_ResolveToExactAssignedAssets()
        {
            Assert.That(
                EnvironmentBindingId.TryParse(
                    Enviro3EnvironmentBindings.LowQualityIdValue,
                    out EnvironmentBindingId lowId),
                Is.True);
            Assert.That(lowId.Value, Is.EqualTo(Enviro3EnvironmentBindings.LowQualityIdValue));
            Assert.That(bindings.TryResolveQuality(EnvironmentQualityTier.Low, out EnviroQuality actualLow), Is.True);
            Assert.That(actualLow, Is.SameAs(low));

            Assert.That(
                EnvironmentBindingId.TryParse(
                    Enviro3EnvironmentBindings.MediumQualityIdValue,
                    out EnvironmentBindingId mediumId),
                Is.True);
            Assert.That(
                mediumId.Value,
                Is.EqualTo(Enviro3EnvironmentBindings.MediumQualityIdValue));
            Assert.That(
                bindings.TryResolveQuality(
                    EnvironmentQualityTier.Medium,
                    out EnviroQuality actualMedium),
                Is.True);
            Assert.That(actualMedium, Is.SameAs(medium));

            Assert.That(
                EnvironmentBindingId.TryParse(
                    Enviro3EnvironmentBindings.HighQualityIdValue,
                    out EnvironmentBindingId highId),
                Is.True);
            Assert.That(highId.Value, Is.EqualTo(Enviro3EnvironmentBindings.HighQualityIdValue));
            Assert.That(bindings.TryResolveQuality(EnvironmentQualityTier.High, out EnviroQuality actualHigh), Is.True);
            Assert.That(actualHigh, Is.SameAs(high));
        }

        [Test]
        public void Pre07CCompatibilityOverload_MapsMediumToExistingHighAsset()
        {
            bindings.ConfigureForAuthoring(
                bindings.SourceConfiguration,
                effectsSource,
                clear,
                partlyCloudy,
                overcast,
                rain,
                storm,
                fog,
                low,
                high);

            Assert.That(bindings.Medium, Is.SameAs(high));
            Assert.That(
                bindings.TryResolveQuality(
                    EnvironmentQualityTier.Medium,
                    out EnviroQuality resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(high));
        }

        [Test]
        public void UnknownWeatherAndQualityValues_FailClosed()
        {
            Assert.That(EnvironmentBindingId.TryParse("weather.unknown", out EnvironmentBindingId unknown), Is.True);
            Assert.That(
                bindings.TryResolveWeather(unknown, out EnviroWeatherType weather, out _),
                Is.False);
            Assert.That(weather, Is.Null);

            Assert.That(
                bindings.TryResolveQuality((EnvironmentQualityTier)99, out EnviroQuality quality),
                Is.False);
            Assert.That(quality, Is.Null);
        }

        private void AssertWeather(
            string serializedId,
            EnviroWeatherType expectedWeather,
            EnvironmentPresentationPresetKind expectedKind)
        {
            Assert.That(EnvironmentBindingId.TryParse(serializedId, out EnvironmentBindingId id), Is.True);
            Assert.That(
                bindings.TryResolveWeather(
                    id,
                    out EnviroWeatherType actualWeather,
                    out EnvironmentPresentationPresetKind actualKind),
                Is.True);
            Assert.That(actualWeather, Is.SameAs(expectedWeather));
            Assert.That(actualKind, Is.EqualTo(expectedKind));
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
