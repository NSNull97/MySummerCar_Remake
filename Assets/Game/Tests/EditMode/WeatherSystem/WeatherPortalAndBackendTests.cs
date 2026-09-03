using System;
using System.Collections.Generic;
using System.Reflection;
using MSC.Weather.Presentation;
using MSC.Weather.System;
using MSC.Weather.System.Local;
using MSC.Weather.System.NativeHDRP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Tests.EditMode.WeatherSystem
{
    public sealed class WeatherPortalAndBackendTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<UnityEngine.Object> transientAssets =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[index]);
                }
            }

            objects.Clear();
            for (int index = transientAssets.Count - 1; index >= 0; index--)
            {
                if (transientAssets[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientAssets[index]);
                }
            }

            transientAssets.Clear();
        }

        [Test]
        public void PortalGraph_UsesStrongestPathPerIndependentChannel()
        {
            WeatherPortalSystem graph = Add<WeatherPortalSystem>("Graph");
            TestZone zoneA = Add<TestZone>("A");
            zoneA.Configure("zone.a");
            TestZone zoneB = Add<TestZone>("B");
            zoneB.Configure("zone.b");
            TestZone zoneC = Add<TestZone>("C");
            zoneC.Configure("zone.c");
            graph.RegisterZone(zoneA);
            graph.RegisterZone(zoneB);
            graph.RegisterZone(zoneC);

            TestPortal outdoorA = Add<TestPortal>("Outdoor-A");
            outdoorA.Configure(
                "portal.outdoor.a",
                null,
                zoneA,
                new PortalTransmission(0.9f, 0.2f, 0.3f, 0.6f, 0.1f, 0.5f));
            TestPortal aC = Add<TestPortal>("A-C");
            aC.Configure(
                "portal.a.c",
                zoneA,
                zoneC,
                new PortalTransmission(0.9f, 1f, 0.5f, 0.5f, 1f, 1f));
            TestPortal outdoorB = Add<TestPortal>("Outdoor-B");
            outdoorB.Configure(
                "portal.outdoor.b",
                null,
                zoneB,
                new PortalTransmission(0.3f, 0.9f, 0.8f, 0.2f, 0.7f, 0.4f));
            TestPortal bC = Add<TestPortal>("B-C");
            bC.Configure(
                "portal.b.c",
                zoneB,
                zoneC,
                new PortalTransmission(1f, 0.9f, 0.9f, 1f, 0.8f, 1f));
            graph.RegisterPortal(outdoorA);
            graph.RegisterPortal(aC);
            graph.RegisterPortal(outdoorB);
            graph.RegisterPortal(bC);

            Assert.That(
                graph.TryResolveOutdoorTransmission(zoneC, out PortalTransmission result),
                Is.True);
            Assert.That(result.Visual01, Is.EqualTo(0.81f).Within(0.0001f));
            Assert.That(result.Audio01, Is.EqualTo(0.81f).Within(0.0001f));
            Assert.That(result.Wind01, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(result.Fog01, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(result.Precipitation01, Is.EqualTo(0.56f).Within(0.0001f));
            Assert.That(result.Thunder01, Is.EqualTo(0.5f).Within(0.0001f));
            var visualPath = new List<IEnvironmentPortal>(4);
            Assert.That(
                graph.TryBuildStrongestVisualPath(zoneC, visualPath),
                Is.True);
            Assert.That(visualPath.Count, Is.EqualTo(2));
            Assert.That(visualPath[0], Is.SameAs(aC));
            Assert.That(visualPath[1], Is.SameAs(outdoorA));
        }

        [Test]
        public void PortalUnregister_ModelsStreamingUnloadWithoutGlobalReset()
        {
            WeatherPortalSystem graph = Add<WeatherPortalSystem>("Graph");
            TestZone zone = Add<TestZone>("Zone");
            zone.Configure("zone.streamed");
            TestPortal portal = Add<TestPortal>("Portal");
            portal.Configure(
                "portal.streamed",
                null,
                zone,
                PortalTransmission.Open);
            graph.RegisterZone(zone);
            graph.RegisterPortal(portal);
            Assert.That(graph.TryResolveOutdoorTransmission(zone, out _), Is.True);

            graph.UnregisterPortal(portal);

            Assert.That(
                graph.TryResolveOutdoorTransmission(zone, out PortalTransmission result),
                Is.True);
            Assert.That(result, Is.EqualTo(PortalTransmission.Blocked));
        }

        [Test]
        public void LegacyStreamingBridge_IgnoresDestroyedZoneAdapterReference()
        {
            var zoneObject = new GameObject("Streamed Legacy Zone");
            objects.Add(zoneObject);
            LegacyWeatherZoneAdapter zoneAdapter =
                zoneObject.AddComponent<LegacyWeatherZoneAdapter>();
            DoorWeatherPortalAdapter portal =
                Add<DoorWeatherPortalAdapter>("Persistent Door Portal");
            FieldInfo zoneAField = typeof(DoorWeatherPortalAdapter).GetField(
                "zoneAComponent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo resolveZones = typeof(DoorWeatherPortalAdapter).GetMethod(
                "ResolveZones",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(zoneAField, Is.Not.Null);
            Assert.That(resolveZones, Is.Not.Null);
            zoneAField.SetValue(portal, zoneAdapter);
            resolveZones.Invoke(portal, null);
            Assert.That(portal.ZoneA, Is.SameAs(zoneAdapter));

            UnityEngine.Object.DestroyImmediate(zoneAdapter);

            Assert.That(portal.ZoneAComponent, Is.Null);
            Assert.That(portal.ZoneA, Is.Null);
            MethodInfo ensureLegacyPortal =
                typeof(LegacyWeatherStreamingBridge).GetMethod(
                    "EnsureLegacyPortal",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensureLegacyPortal, Is.Not.Null);
            Assert.DoesNotThrow(() => ensureLegacyPortal.Invoke(
                null,
                new object[] { portal, null }));
        }

        [Test]
        public void Router_KeepsOnlySelectedBackendAttachedAndSwitchIsReversible()
        {
            GameWeatherSystem router = Add<GameWeatherSystem>("Router");
            TestWeatherBackend enviro = Add<TestWeatherBackend>("Enviro");
            enviro.Configure(WeatherBackendType.EnviroLegacy);
            TestWeatherBackend native = Add<TestWeatherBackend>("Native");
            native.Configure(WeatherBackendType.NativeHDRP);
            router.ConfigureForAuthoring(
                WeatherBackendType.EnviroLegacy,
                enviro,
                native);

            Assert.That(router.Attach().IsOperational, Is.True);
            Assert.That(enviro.IsAttached, Is.True);
            Assert.That(native.IsAttached, Is.False);
            Assert.That(router.Present(default).IsOperational, Is.True);
            native.SetRejectPresentation(true);
            Assert.That(
                router.SetBackend(WeatherBackendType.NativeHDRP).IsOperational,
                Is.False);
            Assert.That(router.SelectedBackend, Is.EqualTo(
                WeatherBackendType.EnviroLegacy));
            Assert.That(enviro.IsAttached, Is.True);
            Assert.That(native.IsAttached, Is.False);
            native.SetRejectPresentation(false);
            Assert.That(
                router.SetBackend(WeatherBackendType.NativeHDRP).IsOperational,
                Is.True);
            Assert.That(enviro.IsAttached, Is.False);
            Assert.That(native.IsAttached, Is.True);
            Assert.That(
                router.SetBackend(WeatherBackendType.EnviroLegacy).IsOperational,
                Is.True);
            Assert.That(enviro.IsAttached, Is.True);
            Assert.That(native.IsAttached, Is.False);
        }

        [Test]
        public void VehicleContext_SeparatesBodyRainFromCabinListenerRain()
        {
            VehicleLocalWeatherContext vehicle =
                Add<VehicleLocalWeatherContext>("Vehicle Context");
            TestPortal aperture = Add<TestPortal>("Window");
            aperture.Configure(
                "portal.vehicle.window",
                vehicle,
                null,
                PortalTransmission.Blocked);
            vehicle.ConfigureForAuthoring(
                "environment.zone.vehicle.test",
                aperture);

            Assert.That(vehicle.BodyRainExposure01, Is.EqualTo(1f));
            Assert.That(vehicle.ListenerRainExposure01, Is.LessThan(0.2f));

            aperture.Change(PortalTransmission.Open);

            Assert.That(vehicle.ListenerRainExposure01, Is.EqualTo(1f));
            Assert.That(vehicle.Current.AudioLeak01, Is.EqualTo(1f));
        }

        [Test]
        public void NativeBackend_AttachClonesProfileAndDetachRestoresOwners()
        {
            Volume nativeVolume = Add<Volume>("Native Volume");
            VolumeProfile authoredProfile = CreateAsset<VolumeProfile>();
            nativeVolume.sharedProfile = authoredProfile;
            nativeVolume.isGlobal = true;
            nativeVolume.enabled = false;
            nativeVolume.weight = 0f;

            Volume legacyVolume = Add<Volume>("Legacy Volume");
            legacyVolume.sharedProfile = CreateAsset<VolumeProfile>();
            legacyVolume.isGlobal = true;
            legacyVolume.enabled = true;
            legacyVolume.weight = 1f;

            Light sun = Add<Light>("Sun");
            sun.type = LightType.Directional;
            sun.intensity = 100f;
            sun.shadows = LightShadows.Soft;
            Color authoredSunColor = new Color(1f, 0.82f, 0.58f);
            sun.color = authoredSunColor;
            sun.useColorTemperature = false;
            sun.gameObject.SetActive(false);
            Light enviroSun = Add<Light>("Enviro Sun");
            enviroSun.type = LightType.Directional;
            enviroSun.intensity = 100000f;
            enviroSun.enabled = true;
            ParticleSystem rain = Add<ParticleSystem>("Native Rain");
            ParticleSystem.MainModule authoredMain = rain.main;
            authoredMain.cullingMode = ParticleSystemCullingMode.Pause;
            ParticleSystem.EmissionModule authoredEmission = rain.emission;
            authoredEmission.enabled = true;
            authoredEmission.rateOverTimeMultiplier = 100f;
            ParticleSystemRenderer authoredRainRenderer =
                rain.GetComponent<ParticleSystemRenderer>();
            authoredRainRenderer.lengthScale = 5.5f;
            TestLocalWeatherSource localContext =
                Add<TestLocalWeatherSource>("Local Weather Context");
            localContext.Set(new LocalWeatherContext(
                null,
                true,
                true,
                0.05f,
                0f,
                0.05f,
                0.05f,
                0.12f,
                0.12f,
                0.58f,
                1f,
                0f,
                0f,
                1.1f));
            var volumesToSuspend = new List<Volume> { legacyVolume };
            Volume[] existingVolumes = UnityEngine.Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < existingVolumes.Length; index++)
            {
                Volume candidate = existingVolumes[index];
                if (candidate != null && candidate != nativeVolume &&
                    candidate != legacyVolume && candidate.isGlobal &&
                    candidate.enabled && candidate.weight > 0f)
                {
                    volumesToSuspend.Add(candidate);
                }
            }

            NativeHDRPWeatherBackend backend =
                Add<NativeHDRPWeatherBackend>("Native Backend");
            backend.ConfigureForAuthoring(
                nativeVolume,
                sun,
                null,
                null,
                new[] { rain },
                Array.Empty<ParticleSystem>(),
                volumesToSuspend.ToArray(),
                Array.Empty<Behaviour>(),
                localContext);

            EnvironmentPresentationStatus attached = backend.Attach();

            Assert.That(attached.IsOperational, Is.True);
            Assert.That(
                (backend.Capabilities &
                 EnvironmentPresentationCapabilities.SunMoonLighting) != 0,
                Is.False,
                "The full Native fallback must not advertise moon parity that " +
                "it does not implement.");
            Assert.That(backend.RuntimeProfile, Is.Not.Null);
            Assert.That(backend.RuntimeProfile, Is.Not.SameAs(authoredProfile));
            Assert.That(nativeVolume.sharedProfile, Is.SameAs(backend.RuntimeProfile));
            Assert.That(nativeVolume.enabled, Is.True);
            Assert.That(legacyVolume.enabled, Is.False);
            Assert.That(sun.gameObject.activeSelf, Is.True);
            Assert.That(enviroSun.enabled, Is.False);
            Assert.That(rain.collision.enabled, Is.True);
            Assert.That(
                rain.collision.quality,
                Is.EqualTo(ParticleSystemCollisionQuality.Low));
            Assert.That(authoredRainRenderer.lengthScale, Is.LessThan(0.6f));
            Assert.That(rain.main.startSize.constantMax, Is.LessThan(0.02f));
            Assert.That(
                rain.main.cullingMode,
                Is.EqualTo(ParticleSystemCullingMode.AlwaysSimulate));
            Assert.That(backend.SurfaceImpactSystem, Is.Not.Null);
            RainSurfaceImpactPresenter impactPresenter =
                rain.GetComponent<RainSurfaceImpactPresenter>();
            Assert.That(impactPresenter, Is.Not.Null);
            Assert.That(
                impactPresenter.MaximumDropletsPerFrame,
                Is.LessThanOrEqualTo(28));
            Assert.That(backend.LightningBolt, Is.Not.Null);
            Assert.That(
                EnvironmentBindingId.TryParse(
                    "weather.partly_cloudy",
                    out EnvironmentBindingId binding),
                Is.True);
            const float TwilightTime01 =
                (23f * 60f + 22f) / (24f * 60f);
            var frame = new EnvironmentPresentationFrame(
                1UL,
                true,
                1995,
                8,
                1,
                TwilightTime01,
                binding,
                EnvironmentCloudType.Scattered,
                0.45f,
                0.5f,
                EnvironmentPrecipitationType.Rain,
                0.6f,
                0.08f,
                12000f,
                MSC.Weather.Domain.WeatherExposureContext.Exterior,
                Vector2.up,
                3f,
                5f,
                EnvironmentLightningVisualRequest.None,
                EnvironmentRefreshRequest.None,
                EnvironmentQualityTier.Medium,
                1f);
            Assert.That(backend.Present(frame).IsOperational, Is.True);
            Assert.That(rain.emission.enabled, Is.False);
            Assert.That(
                backend.RuntimeProfile.TryGet(out Exposure runtimeExposure),
                Is.True);
            Assert.That(
                runtimeExposure.mode.value,
                Is.EqualTo(ExposureMode.Automatic));
            Assert.That(sun.color, Is.EqualTo(Color.white));
            Assert.That(sun.enabled, Is.True);
            Assert.That(sun.intensity, Is.GreaterThan(0f));
            Assert.That(-sun.transform.forward.y, Is.LessThan(0f));
            Assert.That(sun.shadows, Is.EqualTo(LightShadows.None));

            backend.Detach();

            Assert.That(nativeVolume.sharedProfile, Is.SameAs(authoredProfile));
            Assert.That(nativeVolume.enabled, Is.False);
            Assert.That(nativeVolume.weight, Is.EqualTo(0f));
            Assert.That(legacyVolume.enabled, Is.True);
            Assert.That(sun.gameObject.activeSelf, Is.False);
            Assert.That(sun.color, Is.EqualTo(authoredSunColor));
            Assert.That(sun.useColorTemperature, Is.False);
            Assert.That(sun.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(enviroSun.enabled, Is.True);
            Assert.That(rain.emission.enabled, Is.True);
            Assert.That(
                rain.emission.rateOverTimeMultiplier,
                Is.EqualTo(100f));
            Assert.That(authoredRainRenderer.lengthScale, Is.EqualTo(5.5f));
            Assert.That(rain.collision.enabled, Is.False);
            Assert.That(
                rain.main.cullingMode,
                Is.EqualTo(ParticleSystemCullingMode.Pause));
            Assert.That(backend.IsAttached, Is.False);
        }

        [Test]
        public void SolarPosition_FinnishSummerNoonIsAboveMidnight()
        {
            SolarPosition noon = SolarPositionCalculator.Calculate(
                62.4f,
                25.7f,
                220,
                3f,
                0.5f,
                0f);
            SolarPosition midnight = SolarPositionCalculator.Calculate(
                62.4f,
                25.7f,
                220,
                3f,
                0f,
                0f);

            Assert.That(noon.ElevationDegrees, Is.GreaterThan(30f));
            Assert.That(noon.ElevationDegrees, Is.GreaterThan(midnight.ElevationDegrees));
            Assert.That(noon.AzimuthDegrees, Is.InRange(0f, 360f));
        }

        [Test]
        public void SolarPosition_DirectionalRotationTracksSolarElevationAndAzimuth()
        {
            var solar = new SolarPosition(24f, 90f);

            Vector3 directionToSun =
                -(solar.ToDirectionalLightRotation() * Vector3.forward);
            float elevation = Mathf.Asin(directionToSun.y) * Mathf.Rad2Deg;
            float azimuth = Mathf.Repeat(
                Mathf.Atan2(directionToSun.x, directionToSun.z) *
                Mathf.Rad2Deg,
                360f);

            Assert.That(elevation, Is.EqualTo(24f).Within(0.001f));
            Assert.That(azimuth, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void SolarPosition_FinnishTwilightDoesNotJumpAtTwentyThreeTwentyTwo()
        {
            const int AugustFirstDayOfYear = 213;
            float beforeTime01 = (23f * 60f + 20f) / (24f * 60f);
            float afterTime01 = (23f * 60f + 22f) / (24f * 60f);
            SolarPosition before = SolarPositionCalculator.Calculate(
                62.4f,
                25.7f,
                AugustFirstDayOfYear,
                3f,
                beforeTime01,
                0f);
            SolarPosition after = SolarPositionCalculator.Calculate(
                62.4f,
                25.7f,
                AugustFirstDayOfYear,
                3f,
                afterTime01,
                0f);

            Assert.That(before.ElevationDegrees, Is.EqualTo(-5.876f).Within(0.03f));
            Assert.That(after.ElevationDegrees, Is.EqualTo(-5.989f).Within(0.03f));
            Assert.That(
                before.ElevationDegrees - after.ElevationDegrees,
                Is.InRange(0.08f, 0.15f));
            Assert.That(
                Mathf.DeltaAngle(
                    before.AzimuthDegrees,
                    after.AzimuthDegrees),
                Is.InRange(0.35f, 0.6f));
        }

        private T Add<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            transientAssets.Add(asset);
            return asset;
        }
    }

    public sealed class TestZone : MonoBehaviour, IInteriorZone
    {
        private string stableId;

        public string StableId => stableId;
        public int Priority => 0;
        public LocalWeatherZoneSettings Settings =>
            LocalWeatherZoneSettings.ClosedInterior;
        public bool IsAvailable => true;
        public bool Contains(Vector3 worldPosition) => true;
        public void Configure(string id) => stableId = id;
    }

    public sealed class TestPortal : MonoBehaviour, IEnvironmentPortal
    {
        private string stableId;
        private IInteriorZone zoneA;
        private IInteriorZone zoneB;
        private PortalTransmission transmission;

        public string StableId => stableId;
        public IInteriorZone ZoneA => zoneA;
        public IInteriorZone ZoneB => zoneB;
        public Transform OpeningTransform => transform;
        public Vector2 OpeningSizeMeters => Vector2.one;
        public float Openness01 => 1f;
        public PortalTransmission Transmission => transmission;
        public bool IsAvailable => true;
        public event Action<IEnvironmentPortal> TransmissionChanged;

        public void Configure(
            string id,
            IInteriorZone first,
            IInteriorZone second,
            in PortalTransmission value)
        {
            stableId = id;
            zoneA = first;
            zoneB = second;
            transmission = value;
        }

        public void Change(in PortalTransmission value)
        {
            transmission = value;
            TransmissionChanged?.Invoke(this);
        }
    }

    public sealed class TestWeatherBackend : MonoBehaviour, IWeatherBackend
    {
        private readonly List<EnvironmentPresentationDiagnostic> diagnostics =
            new List<EnvironmentPresentationDiagnostic>();
        private EnvironmentPresentationStatus status =
            EnvironmentPresentationStatus.Detached;
        private bool rejectPresentation;

        public WeatherBackendType BackendType { get; private set; }
        public WeatherPresentationOwnership Ownership =>
            WeatherPresentationOwnership.Sky;
        public bool IsAttached { get; private set; }
        public EnvironmentPresentationCapabilities Capabilities =>
            IsAttached ? EnvironmentPresentationCapabilities.Sky :
            EnvironmentPresentationCapabilities.None;
        public EnvironmentPresentationStatus Status => status;
        public IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics =>
            diagnostics;

        public void Configure(WeatherBackendType type) => BackendType = type;
        public void SetRejectPresentation(bool reject) =>
            rejectPresentation = reject;

        public EnvironmentPresentationStatus Attach()
        {
            IsAttached = true;
            status = Ready();
            return status;
        }

        public EnvironmentPresentationStatus Present(
            in EnvironmentPresentationFrame frame)
        {
            status = rejectPresentation
                ? new EnvironmentPresentationStatus(
                    EnvironmentPresentationState.Faulted,
                    EnvironmentPresentationCapabilities.None,
                    0UL,
                    0,
                    1)
                : Ready();
            return status;
        }

        public void Detach()
        {
            IsAttached = false;
            status = EnvironmentPresentationStatus.Detached;
        }

        public bool TrySetPresentationCamera(Camera camera) => camera != null;
        public EnvironmentPresentationStatus RevalidateSceneOwnership() =>
            status;

        private static EnvironmentPresentationStatus Ready() =>
            new EnvironmentPresentationStatus(
                EnvironmentPresentationState.Ready,
                EnvironmentPresentationCapabilities.Sky,
                0UL,
                0,
                0);
    }

    public sealed class TestLocalWeatherSource : MonoBehaviour,
        ILocalWeatherContextSource
    {
        public LocalWeatherContext Current { get; private set; } =
            LocalWeatherContext.Outdoor;

        public event Action<LocalWeatherContext> ContextChanged
        {
            add { }
            remove { }
        }

        public void Set(in LocalWeatherContext context)
        {
            Current = context;
        }
    }
}
