using System.Collections;
using MSC.Bootstrap;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using MSC.Weather.System;
using MSC.Weather.System.NativeHDRP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WeatherSystem
{
    public sealed class BootstrapNativeWeatherPlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";

        [UnityTest]
        public IEnumerator Bootstrap_CanSwitchFromHybridToNativeFallbackAndPresentRainAndLightning()
        {
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return null;
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            ProductionEnvironmentController environment =
                Object.FindFirstObjectByType<ProductionEnvironmentController>(
                    FindObjectsInactive.Include);
            GameWeatherSystem router =
                Object.FindFirstObjectByType<GameWeatherSystem>(
                    FindObjectsInactive.Include);
            NativeHDRPWeatherBackend native =
                Object.FindFirstObjectByType<NativeHDRPWeatherBackend>(
                    FindObjectsInactive.Include);

            Assert.That(installer, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(native, Is.Not.Null);
            Assert.That(
                router.SelectedBackend,
                Is.EqualTo(WeatherBackendType.EnviroLegacy),
                "Production must boot through Enviro celestial/sky presentation.");
            Assert.That(environment.AreDomainsInitialized, Is.True);
            Assert.That(
                WeatherStateIds.AllKnown,
                Does.Contain(environment.AuthoritativeWeather.CurrentState.Id));

            // The authored spawn is indoors, where precipitation is correctly
            // suppressed. Move the runtime-only probe above all roofs so this
            // smoke test exercises the outdoor emitter path explicitly.
            installer.SpawnedPlayer.transform.position =
                new Vector3(0f, 500f, 0f);
            Camera camera = installer.SpawnedPlayer
                .GetComponentInChildren<Camera>(true);
            Assert.That(camera, Is.Not.Null);
            Assert.That(environment.BindPresentationCamera(camera), Is.True,
                environment.LastFailure);

            yield return environment.InitializeBeforeWorldReveal();

            Assert.That(environment.IsWorldRevealReady, Is.True,
                environment.LastFailure);
            Assert.That(environment.PresentationStatus.IsOperational, Is.True);
            Assert.That(router.IsAttached, Is.True);
            Assert.That(
                router.ActiveBackend.BackendType,
                Is.EqualTo(WeatherBackendType.EnviroLegacy));
            Assert.That(
                router.SetBackend(WeatherBackendType.NativeHDRP).IsOperational,
                Is.True,
                "The full Native HDRP path must remain available as a reversible fallback.");
            Assert.That(router.ActiveBackend, Is.SameAs(native));
            Assert.That(native.IsAttached, Is.True);
            Assert.That(native.RuntimeWeatherVolume, Is.Not.Null);
            Assert.That(native.RuntimeWeatherVolume.enabled, Is.True);
            Assert.That(native.RuntimeWeatherVolume.weight, Is.EqualTo(1f));
            Assert.That(native.RuntimeProfile, Is.Not.Null);
            Assert.That(
                native.RuntimeProfile.TryGet(out Exposure exposure),
                Is.True);
            Assert.That(
                exposure.mode.value,
                Is.EqualTo(ExposureMode.Automatic));
            Assert.That(native.DirectionalSun, Is.Not.Null);
            Assert.That(native.DirectionalSun.gameObject.activeInHierarchy, Is.True,
                "The Native HDRP sun owner must be active while the backend is attached.");

            ParticleSystem[] precipitation =
                router.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(precipitation.Length, Is.GreaterThanOrEqualTo(2));
            var collisionProbe = new GameObject("Rain Collision Probe")
            {
                hideFlags = HideFlags.DontSave,
            };
            BoxCollider probeCollider = collisionProbe.AddComponent<BoxCollider>();
            probeCollider.size = new Vector3(16f, 0.2f, 12f);
            collisionProbe.transform.position =
                camera.transform.position + Vector3.down * 2.5f;
            Assert.That(
                environment.DevTryApplyWeatherOverride(
                    WeatherStateIds.HeavyRain.Value,
                    0f,
                    out string weatherFailure),
                Is.True,
                weatherFailure);

            // The camera-local emitter starts after the next presentation tick;
            // allow one full 12 m fall before sampling collision impacts.
            yield return new WaitForSecondsRealtime(1.35f);

            bool hasActiveRain = false;
            int liveRainParticleCount = 0;
            bool anyRainSystemPlaying = false;
            bool allRainSystemsActive = true;
            for (int index = 0; index < precipitation.Length; index++)
            {
                ParticleSystem.EmissionModule emission =
                    precipitation[index].emission;
                if (emission.enabled && emission.rateOverTimeMultiplier > 0f)
                {
                    hasActiveRain = true;
                    liveRainParticleCount += precipitation[index].particleCount;
                    anyRainSystemPlaying |= precipitation[index].isPlaying;
                    allRainSystemsActive &=
                        precipitation[index].gameObject.activeInHierarchy;
                    Assert.That(
                        precipitation[index].main.startSize.constantMax,
                        Is.LessThan(0.02f));
                    Assert.That(precipitation[index].collision.enabled, Is.True);
                    Assert.That(
                        precipitation[index].collision.quality,
                        Is.EqualTo(ParticleSystemCollisionQuality.Low));
                    Assert.That(
                        precipitation[index].collision.maxCollisionShapes,
                        Is.LessThanOrEqualTo(128));
                    Assert.That(
                        precipitation[index].collision.enableDynamicColliders,
                        Is.False);
                    Assert.That(
                        precipitation[index].main.maxParticles,
                        Is.LessThanOrEqualTo(5200));
                    Assert.That(
                        precipitation[index]
                            .GetComponent<ParticleSystemRenderer>()
                            .lengthScale,
                        Is.LessThan(0.6f));
                    Vector3 expectedAnchor =
                        camera.transform.position + Vector3.up * 12f;
                    Assert.That(
                        Vector3.Distance(
                            precipitation[index].transform.position,
                            expectedAnchor),
                        Is.LessThan(0.05f));
                }
            }

            Assert.That(hasActiveRain, Is.True,
                "Heavy-rain presentation did not enable a camera-anchored emitter.");
            bool hasGraphicsDevice =
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (hasGraphicsDevice)
            {
                Assert.That(
                    liveRainParticleCount,
                    Is.GreaterThan(0),
                    $"Heavy-rain emitters produced no live particles; " +
                    $"playing={anyRainSystemPlaying}, " +
                    $"active={allRainSystemsActive}, " +
                    $"timeScale={Time.timeScale:0.###}.");
            }
            Assert.That(native.SurfaceImpactSystem, Is.Not.Null);
            Assert.That(
                native.SurfaceImpactSystem.main.maxParticles,
                Is.LessThanOrEqualTo(600));
            if (hasGraphicsDevice)
            {
                Assert.That(
                    native.SurfaceImpactSystem.particleCount,
                    Is.GreaterThan(0),
                    $"Rain collisions did not produce bounded surface-impact " +
                    $"droplets; liveRainParticles={liveRainParticleCount}.");
            }
            RainSurfaceImpactPresenter[] impactPresenters =
                router.GetComponentsInChildren<RainSurfaceImpactPresenter>(true);
            Assert.That(impactPresenters, Is.Not.Empty);
            int totalImpactBudgetPerFrame = 0;
            for (int index = 0; index < impactPresenters.Length; index++)
            {
                totalImpactBudgetPerFrame +=
                    impactPresenters[index].MaximumDropletsPerFrame;
                Assert.That(
                    impactPresenters[index].MaximumDropletsPerFrame,
                    Is.LessThanOrEqualTo(28));
            }
            Assert.That(totalImpactBudgetPerFrame, Is.LessThanOrEqualTo(40));

            Assert.That(
                environment.DevTryTriggerAmbientLightningAtListener(
                    out string lightningFailure),
                Is.True,
                lightningFailure);
            Light lightning = null;
            Light[] lights = router.GetComponentsInChildren<Light>(true);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index].name == "Lightning Flash")
                {
                    lightning = lights[index];
                    break;
                }
            }

            Assert.That(lightning, Is.Not.Null);
            Assert.That(lightning.enabled, Is.True);
            Assert.That(lightning.intensity, Is.GreaterThan(0f));
            Assert.That(native.LightningBolt, Is.Not.Null);
            Assert.That(native.LightningBolt.enabled, Is.True);
            Assert.That(native.LightningBolt.positionCount, Is.EqualTo(17));
            Time.timeScale = previousTimeScale;
            Object.Destroy(collisionProbe);
        }
    }
}
