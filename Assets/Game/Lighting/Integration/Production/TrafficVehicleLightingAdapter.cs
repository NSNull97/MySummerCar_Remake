using System;
using System.Collections.Generic;
using MSC.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Lighting.Production
{
    /// <summary>
    /// Supplies streamed story-traffic wrappers with project-owned low beams.
    /// The scan is deliberately bounded to the small set of active wrappers;
    /// it is the compatibility edge for presenters that predate lifecycle
    /// events and does not make hierarchy names runtime authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficVehicleLightingAdapter : MonoBehaviour,
        IVehicleElectricalLightingSource
    {
        private const float ScanIntervalSeconds = 0.75f;
        private const string LowBeamChannel = "low-beam";

        private readonly Dictionary<EntityId, GameObject> fixtureRoots =
            new Dictionary<EntityId, GameObject>();

        private LightingProfileCatalog profiles;
        private EnviroLightingBridge environment;
        private LightingCalibrationProfile calibration;
        private Material runtimeEmissionMaterial;
        private float nextScanTime;
        private bool nightLightingOn;
        private bool initialized;

        public int BoundVehicleCount => fixtureRoots.Count;
        public int GeneratedLowBeamLightCount
        {
            get
            {
                int count = 0;
                foreach (GameObject root in fixtureRoots.Values)
                {
                    if (root != null)
                    {
                        count += root.GetComponentsInChildren<Light>(true).Length;
                    }
                }

                return count;
            }
        }
        public bool NightLightingOn => nightLightingOn;
        public bool HasUsablePower => initialized && isActiveAndEnabled;

        public void Initialize(
            LightingProfileCatalog configuredProfiles,
            EnviroLightingBridge configuredEnvironment)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Traffic vehicle lighting is already initialized.");
            }

            profiles = configuredProfiles ??
                throw new ArgumentNullException(nameof(configuredProfiles));
            environment = configuredEnvironment ??
                throw new ArgumentNullException(nameof(configuredEnvironment));
            calibration = profiles.Calibration ??
                throw new InvalidOperationException(
                    "Traffic lighting requires the shared lighting calibration.");
            initialized = true;
            RefreshNightPolicy();
            ScanActiveVehicles();
        }

        public bool IsLightingChannelOn(string channelId) =>
            string.Equals(
                channelId,
                LowBeamChannel,
                StringComparison.Ordinal) && nightLightingOn;

        public bool AreGeneratedFixturesElectricallyBound()
        {
            foreach (GameObject root in fixtureRoots.Values)
            {
                GameLightFixture fixture =
                    root != null ? root.GetComponent<GameLightFixture>() : null;
                if (fixture == null || !ReferenceEquals(
                        fixture.VehicleElectricalSource,
                        this))
                {
                    return false;
                }
            }

            return fixtureRoots.Count > 0;
        }

        public bool AreGeneratedFixturesAlignedToHeadlamps()
        {
            foreach (GameObject root in fixtureRoots.Values)
            {
                StoryTrafficVehiclePresentationBinding vehicle = root != null
                    ? root.GetComponentInParent<
                        StoryTrafficVehiclePresentationBinding>()
                    : null;
                Light[] lights = root != null
                    ? root.GetComponentsInChildren<Light>(true)
                    : Array.Empty<Light>();
                RuntimeEmissionLens[] lenses = root != null
                    ? root.GetComponentsInChildren<RuntimeEmissionLens>(true)
                    : Array.Empty<RuntimeEmissionLens>();
                if (vehicle == null || lights.Length != 2 ||
                    lenses.Length != 2 ||
                    !vehicle.TryGetLowBeamMounts(
                        out Vector3 left,
                        out Vector3 right,
                        out Vector3 direction))
                {
                    return false;
                }

                bool direct = Vector3.Distance(
                        lights[0].transform.position,
                        left) < 0.02f &&
                    Vector3.Distance(
                        lights[1].transform.position,
                        right) < 0.02f;
                bool swapped = Vector3.Distance(
                        lights[0].transform.position,
                        right) < 0.02f &&
                    Vector3.Distance(
                        lights[1].transform.position,
                        left) < 0.02f;
                Vector3 leftLens = left + direction * 0.006f;
                Vector3 rightLens = right + direction * 0.006f;
                bool lensesDirect = Vector3.Distance(
                        lenses[0].transform.position,
                        leftLens) < 0.02f &&
                    Vector3.Distance(
                        lenses[1].transform.position,
                        rightLens) < 0.02f;
                bool lensesSwapped = Vector3.Distance(
                        lenses[0].transform.position,
                        rightLens) < 0.02f &&
                    Vector3.Distance(
                        lenses[1].transform.position,
                        leftLens) < 0.02f;
                if ((!direct && !swapped) ||
                    (!lensesDirect && !lensesSwapped) ||
                    Vector3.Angle(lights[0].transform.forward, direction) > 0.1f ||
                    Vector3.Angle(lights[1].transform.forward, direction) > 0.1f ||
                    Vector3.Angle(lenses[0].transform.forward, direction) > 0.1f ||
                    Vector3.Angle(lenses[1].transform.forward, direction) > 0.1f)
                {
                    return false;
                }
            }

            return fixtureRoots.Count > 0;
        }

        public void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            foreach (GameObject root in fixtureRoots.Values)
            {
                if (root != null)
                {
                    root.SetActive(false);
                    Destroy(root);
                }
            }

            fixtureRoots.Clear();
            profiles = null;
            environment = null;
            calibration = null;
            initialized = false;
        }

        private void Update()
        {
            if (!initialized || Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            RefreshNightPolicy();
            ScanActiveVehicles();
        }

        private void OnDestroy()
        {
            Shutdown();
            if (runtimeEmissionMaterial != null)
            {
                Destroy(runtimeEmissionMaterial);
                runtimeEmissionMaterial = null;
            }
        }

        private void RefreshNightPolicy()
        {
            float elevation = environment.GetSunElevationDegrees();
            if (!nightLightingOn &&
                elevation <= calibration.DuskOnSunElevationDegrees)
            {
                nightLightingOn = true;
            }
            else if (nightLightingOn &&
                     elevation >= calibration.DawnOffSunElevationDegrees)
            {
                nightLightingOn = false;
            }
        }

        private void ScanActiveVehicles()
        {
            RemoveDestroyedVehicles();
            StoryTrafficVehiclePresentationBinding[] vehicles =
                FindObjectsByType<StoryTrafficVehiclePresentationBinding>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < vehicles.Length; index++)
            {
                StoryTrafficVehiclePresentationBinding vehicle = vehicles[index];
                if (vehicle == null ||
                    string.IsNullOrWhiteSpace(vehicle.DriverFeatureId) ||
                    fixtureRoots.ContainsKey(vehicle.GetEntityId()) ||
                    HasAuthoredLowBeam(vehicle))
                {
                    continue;
                }

                fixtureRoots.Add(
                    vehicle.GetEntityId(),
                    CreateLowBeams(vehicle));
            }
        }

        private static bool HasAuthoredLowBeam(
            StoryTrafficVehiclePresentationBinding vehicle)
        {
            GameLightFixture[] fixtures =
                vehicle.GetComponentsInChildren<GameLightFixture>(true);
            for (int index = 0; index < fixtures.Length; index++)
            {
                if (fixtures[index].Profile != null &&
                    fixtures[index].Profile.Category ==
                    LightFixtureCategory.VehicleLowBeam)
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject CreateLowBeams(
            StoryTrafficVehiclePresentationBinding vehicle)
        {
            if (!vehicle.TryGetLowBeamMounts(
                    out Vector3 leftPosition,
                    out Vector3 rightPosition,
                    out Vector3 forward))
            {
                throw new InvalidOperationException(
                    $"Story-traffic vehicle '{vehicle.DriverFeatureId}' has " +
                    "no usable donor-wheel basis for low-beam placement.");
            }

            var root = new GameObject("Traffic Low Beams (Runtime)");
            root.hideFlags = HideFlags.DontSave;
            root.SetActive(false);
            root.transform.SetParent(vehicle.transform, false);

            Light left = CreateBeam(
                root.transform,
                leftPosition,
                forward,
                "Low Beam Left");
            Light rightLight = CreateBeam(
                root.transform,
                rightPosition,
                forward,
                "Low Beam Right");
            Renderer leftLens = CreateLens(
                root.transform,
                left.transform.position + forward * 0.006f,
                forward,
                "Headlamp Lens Left");
            Renderer rightLens = CreateLens(
                root.transform,
                rightLight.transform.position + forward * 0.006f,
                forward,
                "Headlamp Lens Right");

            GameLightFixture fixture = root.AddComponent<GameLightFixture>();
            string stableSuffix = vehicle.DriverFeatureId.ToLowerInvariant();
            // Bind before InitializeRuntime publishes the fixture lifecycle.
            // Otherwise the manager's first evaluation sees a vehicle policy
            // with no electrical source and leaves a freshly streamed car dark
            // until the next policy sweep.
            fixture.BindVehicleElectricalSource(this);
            fixture.InitializeRuntime(
                "traffic.light." + stableSuffix + ".low-beam",
                profiles.GetRequiredProfile(LightFixtureCategory.VehicleLowBeam),
                new[] { left, rightLight },
                new[] { leftLens, rightLens },
                "source.traffic.vehicle",
                "grid.traffic.vehicle." + stableSuffix,
                string.Empty,
                "zone.traffic.vehicle",
                string.Empty,
                LowBeamChannel,
                true);
            root.SetActive(true);
            return root;
        }

        private static Light CreateBeam(
            Transform parent,
            Vector3 position,
            Vector3 forward,
            string objectName)
        {
            var beamObject = new GameObject(objectName);
            beamObject.hideFlags = HideFlags.DontSave;
            beamObject.transform.SetParent(parent, false);
            beamObject.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(forward, Vector3.up));
            return beamObject.AddComponent<Light>();
        }

        private Renderer CreateLens(
            Transform parent,
            Vector3 position,
            Vector3 forward,
            string objectName)
        {
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lens.name = objectName;
            lens.hideFlags = HideFlags.DontSave;
            lens.transform.SetParent(parent, false);
            lens.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(forward, Vector3.up));
            lens.transform.localScale = new Vector3(0.2f, 0.085f, 1f);
            Collider collider = lens.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer renderer = lens.GetComponent<Renderer>();
            renderer.sharedMaterial = GetRuntimeEmissionMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            lens.AddComponent<RuntimeEmissionLens>();
            return renderer;
        }

        private Material GetRuntimeEmissionMaterial()
        {
            if (runtimeEmissionMaterial != null)
            {
                return runtimeEmissionMaterial;
            }

            Shader shader = Shader.Find("HDRP/Unlit") ??
                Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Traffic headlamp emission shader is unavailable.");
            }

            runtimeEmissionMaterial = new Material(shader)
            {
                name = "Runtime Traffic Headlamp Emission",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true,
            };
            return runtimeEmissionMaterial;
        }

        private void RemoveDestroyedVehicles()
        {
            if (fixtureRoots.Count == 0)
            {
                return;
            }

            var missing = new List<EntityId>();
            foreach (KeyValuePair<EntityId, GameObject> pair in fixtureRoots)
            {
                if (pair.Value == null)
                {
                    missing.Add(pair.Key);
                }
            }

            for (int index = 0; index < missing.Count; index++)
            {
                fixtureRoots.Remove(missing[index]);
            }
        }
    }
}
