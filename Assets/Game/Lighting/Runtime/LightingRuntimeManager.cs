using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Lighting
{
    public interface ILightingAudioSink
    {
        void PlaySwitchEvent(string switchId, Vector3 worldPosition, bool isOn);
    }

    [DisallowMultipleComponent]
    public sealed class LightingRuntimeManager : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("MSC.LightingRuntimeManager.Update");
        private static readonly ProfilerMarker BudgetMarker =
            new ProfilerMarker("MSC.LightingRuntimeManager.Budget");

        [SerializeField] private LightingCalibrationProfile calibration;
        [SerializeField] private LightingQualityProfile lowQuality;
        [SerializeField] private LightingQualityProfile mediumQuality;
        [SerializeField] private LightingQualityProfile highQuality;
        [SerializeField] private LightingQualityProfile ultraQuality;
        [SerializeField] private LightingQualityTier qualityTier =
            LightingQualityTier.High;
        [SerializeField] private Transform focus;

        private readonly List<GameLightFixture> fixtures =
            new List<GameLightFixture>(128);
        private readonly List<GameLightFixture> transitions =
            new List<GameLightFixture>(32);
        private readonly Dictionary<string, GameLightFixture> fixturesById =
            new Dictionary<string, GameLightFixture>(StringComparer.Ordinal);
        private readonly List<LightingZone> zones = new List<LightingZone>(32);

        private ElectricalGridService grid;
        private ILightingAtmosphereProvider atmosphere;
        private IBusinessPresenceProvider businessPresence;
        private ILightingAudioSink audioSink;
        private Func<float> sunElevationProvider;
        private LightingZone currentZone;
        private float nextBudgetRefresh;
        private bool duskPolicyOn;
        private bool initialized;
        private Comparison<GameLightFixture> budgetComparison;

        public ElectricalGridService Grid => grid;
        public LightingQualityTier QualityTier => qualityTier;
        public int RegisteredFixtureCount => fixtures.Count;
        public int ActiveLogicalLightCount { get; private set; }
        public int ActiveShadowedLightCount { get; private set; }
        public int ActiveEveryFrameShadowLightCount { get; private set; }
        public int ActiveHdBeamCount { get; private set; }
        public int ActiveSdBeamCount { get; private set; }

        private void Awake()
        {
            budgetComparison = CompareBudgetPriority;
        }

        public void Initialize(
            ElectricalGridService configuredGrid,
            Transform configuredFocus,
            Func<float> configuredSunElevationProvider,
            ILightingAtmosphereProvider configuredAtmosphere,
            IBusinessPresenceProvider configuredBusinessPresence,
            ILightingAudioSink configuredAudioSink)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Lighting runtime is already initialized.");
            }

            grid = configuredGrid ??
                throw new ArgumentNullException(nameof(configuredGrid));
            focus = configuredFocus;
            sunElevationProvider = configuredSunElevationProvider;
            atmosphere = configuredAtmosphere;
            businessPresence = configuredBusinessPresence;
            audioSink = configuredAudioSink;
            grid.StateChanged += HandleElectricalStateChanged;
            LightingFixtureLifecycle.Enabled += RegisterFixture;
            LightingFixtureLifecycle.Disabled += UnregisterFixture;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ScanLoadedScenes();
            initialized = true;
            ReevaluateAll();
            RefreshBudgets();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            using (UpdateMarker.Auto())
            {
                UpdateTransitions(Time.unscaledDeltaTime);
                if (Time.unscaledTime >= nextBudgetRefresh)
                {
                    LightingQualityProfile quality = CurrentQualityProfile();
                    nextBudgetRefresh = Time.unscaledTime +
                        (quality != null ? quality.BudgetRefreshSeconds : 0.2f);
                    RefreshPolicies();
                    RefreshBudgets();
                }
            }
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.StateChanged -= HandleElectricalStateChanged;
            }

            LightingFixtureLifecycle.Enabled -= RegisterFixture;
            LightingFixtureLifecycle.Disabled -= UnregisterFixture;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            fixtures.Clear();
            transitions.Clear();
            fixturesById.Clear();
            zones.Clear();
            initialized = false;
        }

        public void SetQualityTier(LightingQualityTier tier)
        {
            qualityTier = tier;
            RefreshBudgets();
        }

        public void SetFocus(Transform configuredFocus)
        {
            focus = configuredFocus;
        }

        public void ToggleSwitch(string switchId, Vector3 worldPosition)
        {
            grid.ToggleSwitch(switchId);
            if (grid.TryGetSwitchState(switchId, out bool isOn))
            {
                audioSink?.PlaySwitchEvent(switchId, worldPosition, isOn);
            }
        }

        public void RegisterZone(LightingZone zone)
        {
            if (zone != null && !zones.Contains(zone))
            {
                zones.Add(zone);
            }
        }

        public void UnregisterZone(LightingZone zone)
        {
            zones.Remove(zone);
            if (currentZone == zone)
            {
                currentZone = null;
            }
        }

        private void RegisterFixture(GameLightFixture fixture)
        {
            if (fixture == null || fixtures.Contains(fixture))
            {
                return;
            }

            fixture.Validate();
            if (fixturesById.TryGetValue(
                    fixture.FixtureId,
                    out GameLightFixture existing))
            {
                if (existing == null)
                {
                    fixturesById.Remove(fixture.FixtureId);
                }
                else if (ReferenceEquals(existing, fixture))
                {
                    return;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Duplicate light fixture ID '{fixture.FixtureId}'.");
                }
            }

            fixturesById.Add(fixture.FixtureId, fixture);
            EnsureElectricalBindings(fixture);
            fixtures.Add(fixture);
            EvaluateFixture(fixture);
        }

        private void UnregisterFixture(GameLightFixture fixture)
        {
            if (fixture == null)
            {
                return;
            }

            fixtures.Remove(fixture);
            transitions.Remove(fixture);
            if (fixturesById.TryGetValue(fixture.FixtureId, out GameLightFixture value) &&
                value == fixture)
            {
                fixturesById.Remove(fixture.FixtureId);
            }
        }

        private void EnsureElectricalBindings(GameLightFixture fixture)
        {
            if (!grid.HasSource(fixture.PowerSourceId))
            {
                grid.RegisterSource(fixture.PowerSourceId, true);
            }

            if (!grid.HasCircuit(fixture.CircuitId))
            {
                grid.RegisterCircuit(
                    fixture.CircuitId,
                    fixture.PowerSourceId,
                    true);
            }

            if (!string.IsNullOrWhiteSpace(fixture.SwitchId) &&
                !grid.HasSwitch(fixture.SwitchId))
            {
                grid.RegisterSwitch(fixture.SwitchId, false);
            }
        }

        private void HandleElectricalStateChanged(ElectricalStateChanged _) =>
            ReevaluateAll();

        private void RefreshPolicies()
        {
            float sunElevation = sunElevationProvider?.Invoke() ?? -10f;
            if (calibration != null)
            {
                if (!duskPolicyOn &&
                    sunElevation <= calibration.DuskOnSunElevationDegrees)
                {
                    duskPolicyOn = true;
                }
                else if (duskPolicyOn &&
                    sunElevation >= calibration.DawnOffSunElevationDegrees)
                {
                    duskPolicyOn = false;
                }
            }
            else
            {
                duskPolicyOn = sunElevation < -1f;
            }

            ReevaluateAll();
            ResolveCurrentZone();
        }

        private void ReevaluateAll()
        {
            ActiveLogicalLightCount = 0;
            for (int index = 0; index < fixtures.Count; index++)
            {
                EvaluateFixture(fixtures[index]);
            }
        }

        private void EvaluateFixture(GameLightFixture fixture)
        {
            bool policyAllows = EvaluatePolicy(fixture);
            bool isOn = grid.IsActuallyOn(
                fixture.CircuitId,
                fixture.SwitchId,
                policyAllows,
                fixture.FixtureAvailable);
            if (isOn)
            {
                ActiveLogicalLightCount++;
            }

            if (fixture.LogicalOn == isOn)
            {
                // Runtime-spawned world lights arrive with their authored Light
                // state already enabled. A newly registered fixture whose switch
                // is off also starts with logicalOn=false, so equality is not proof
                // that the visual state was applied. Normalize it here; otherwise
                // the hierarchy contains lights that the electrical system never
                // actually owns.
                if (!isOn)
                {
                    // VisualFactor defaults to zero, but the underlying authored
                    // Light can still be enabled until this first explicit write.
                    fixture.ApplyVisualFactor(0f);
                }
                return;
            }

            fixture.SetLogicalState(isOn);
            if (!transitions.Contains(fixture))
            {
                transitions.Add(fixture);
            }
        }

        private bool EvaluatePolicy(GameLightFixture fixture)
        {
            switch (fixture.Profile.PowerPolicy)
            {
                case LightPowerPolicyKind.DuskToDawn:
                    return duskPolicyOn;
                case LightPowerPolicyKind.BusinessOpenAndOwnerPresent:
                    return businessPresence != null &&
                           businessPresence.IsBusinessOpenAndOwnerPresent(
                               fixture.BusinessId);
                case LightPowerPolicyKind.VehicleElectrical:
                    return fixture.VehicleElectricalSource != null &&
                           fixture.VehicleElectricalSource.HasUsablePower &&
                           fixture.VehicleElectricalSource.IsLightingChannelOn(
                               fixture.VehicleChannelId);
                case LightPowerPolicyKind.Scripted:
                    return fixture.ScriptedPolicyAllows;
                default:
                    return true;
            }
        }

        private void UpdateTransitions(float deltaTime)
        {
            for (int index = transitions.Count - 1; index >= 0; index--)
            {
                GameLightFixture fixture = transitions[index];
                if (fixture == null)
                {
                    transitions.RemoveAt(index);
                    continue;
                }

                float duration = fixture.WantsVisuals
                    ? fixture.Profile.TurnOnSeconds
                    : fixture.Profile.TurnOffSeconds;
                float target = fixture.WantsVisuals ? 1f : 0f;
                float step = duration <= 0f ? 1f : deltaTime / duration;
                float value = Mathf.MoveTowards(
                    fixture.VisualFactor,
                    target,
                    step);
                fixture.ApplyVisualFactor(value);
                if (Mathf.Approximately(value, target))
                {
                    transitions.RemoveAt(index);
                }
            }
        }

        private void RefreshBudgets()
        {
            using (BudgetMarker.Auto())
            {
                LightingQualityProfile quality = CurrentQualityProfile();
                if (quality == null)
                {
                    return;
                }

                ActiveShadowedLightCount = 0;
                ActiveEveryFrameShadowLightCount = 0;
                ActiveHdBeamCount = 0;
                ActiveSdBeamCount = 0;
                LightingAtmosphereState atmosphereState =
                    atmosphere?.Current ?? LightingAtmosphereState.Clear;
                ResolveCurrentZone();
                budgetComparison ??= CompareBudgetPriority;
                fixtures.Sort(budgetComparison);
#if UNITY_EDITOR
                // Resources.FindObjectsOfTypeAll allocates. Resolve retained
                // Scene View cameras once per 0.2 s budget pass, not once for
                // every fixture (49 fixtures previously meant 49 full scans).
                Camera[] sceneViewCameras =
                    Resources.FindObjectsOfTypeAll<Camera>();
#endif
                for (int index = 0; index < fixtures.Count; index++)
                {
                    GameLightFixture fixture = fixtures[index];
                    float distance = focus != null
                        ? Vector3.Distance(focus.position, fixture.WorldPosition)
                        : 0f;
#if UNITY_EDITOR
                    distance = ResolveSceneViewDistance(
                        fixture.WorldPosition,
                        distance,
                        sceneViewCameras);
#endif
                    LightFixtureQualitySettings fixtureQuality =
                        fixture.Profile.GetQuality(qualityTier);
                    float lightDistance = Mathf.Min(
                        quality.LocalLightDistanceMeters,
                        fixtureQuality.LightDistanceMeters);
                    bool lightSelected = fixture.LogicalOn &&
                        distance <= lightDistance +
                            (fixture.QualityVisible ? 5f : 0f);
                    if (fixture.SetQualityVisible(lightSelected) &&
                        !transitions.Contains(fixture))
                    {
                        transitions.Add(fixture);
                    }
                    bool shadowSelected = fixture.LogicalOn &&
                        fixtureQuality.ShadowsEnabled &&
                        distance <= Mathf.Min(
                            quality.ShadowDistanceMeters,
                            fixtureQuality.ShadowDistanceMeters) &&
                        ActiveShadowedLightCount <
                            quality.MaximumShadowedLights;
                    bool everyFrameSelected = shadowSelected &&
                        ActiveEveryFrameShadowLightCount <
                            quality.MaximumEveryFrameShadowLights;
                    if (shadowSelected)
                    {
                        ActiveShadowedLightCount++;
                    }

                    if (everyFrameSelected)
                    {
                        ActiveEveryFrameShadowLightCount++;
                    }

                    VolumetricBeamQuality beam = VolumetricBeamQuality.Off;
                    if (fixture.LogicalOn && fixture.HasSpotLight &&
                        distance <= Mathf.Min(
                            quality.VolumetricDistanceMeters,
                            fixtureQuality.BeamDistanceMeters))
                    {
                        VolumetricBeamQuality requested =
                            fixtureQuality.BeamQuality;
                        if (requested == VolumetricBeamQuality.HighDefinition &&
                            ActiveHdBeamCount < quality.MaximumHdBeams)
                        {
                            beam = requested;
                            ActiveHdBeamCount++;
                        }
                        else if (requested != VolumetricBeamQuality.Off &&
                            ActiveSdBeamCount < quality.MaximumSdBeams)
                        {
                            beam = VolumetricBeamQuality.StandardDefinition;
                            ActiveSdBeamCount++;
                        }
                    }

                    fixture.ApplyQuality(
                        fixtureQuality,
                        shadowSelected,
                        everyFrameSelected,
                        beam,
                        ResolveBeamIntensity(fixture.Profile, atmosphereState));
                }
            }
        }

#if UNITY_EDITOR
        private static float ResolveSceneViewDistance(
            Vector3 fixturePosition,
            float playerDistance,
            Camera[] cameras)
        {
            // Unity keeps multiple Scene View camera objects alive. Selecting
            // the first one is nondeterministic and often picks a stale hidden
            // view, which made every remote authoring location appear dark.
            // Scene View cameras do not reliably report Camera.enabled while
            // rendering. Take the nearest retained Scene View while preserving
            // player-only distance authority in non-Editor builds.
            float distance = playerDistance;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera != null && camera.cameraType == CameraType.SceneView)
                {
                    distance = Mathf.Min(
                        distance,
                        Vector3.Distance(
                            camera.transform.position,
                            fixturePosition));
                }
            }

            return distance;
        }
#endif

        private void ResolveCurrentZone()
        {
            if (focus == null)
            {
                currentZone = null;
                return;
            }

            if (currentZone != null && currentZone.Contains(focus.position, true))
            {
                return;
            }

            currentZone = null;
            for (int index = 0; index < zones.Count; index++)
            {
                LightingZone zone = zones[index];
                if (zone != null && zone.Contains(focus.position, false))
                {
                    currentZone = zone;
                    return;
                }
            }
        }

        private bool IsRelevantZone(string fixtureZoneId)
        {
            if (currentZone == null)
            {
                return string.Equals(
                    fixtureZoneId,
                    "zone.exterior",
                    StringComparison.Ordinal);
            }

            return string.Equals(
                       fixtureZoneId,
                       currentZone.ZoneId,
                       StringComparison.Ordinal) ||
                   currentZone.IsAdjacent(fixtureZoneId);
        }

        private static float ResolveBeamIntensity(
            LightFixtureProfile profile,
            in LightingAtmosphereState atmosphereState)
        {
            float intensity = Mathf.Lerp(
                profile.BeamClearAirIntensity,
                profile.BeamFogIntensity,
                atmosphereState.Fog);
            intensity = Mathf.Max(
                intensity,
                Mathf.Lerp(
                    profile.BeamClearAirIntensity,
                    profile.BeamRainIntensity,
                    atmosphereState.Precipitation));
            if (atmosphereState.Snow)
            {
                intensity = Mathf.Max(intensity, profile.BeamSnowIntensity);
            }

            return intensity;
        }

        private LightingQualityProfile CurrentQualityProfile()
        {
            return qualityTier switch
            {
                LightingQualityTier.Low => lowQuality,
                LightingQualityTier.Medium => mediumQuality,
                LightingQualityTier.High => highQuality,
                LightingQualityTier.Ultra => ultraQuality,
                _ => highQuality,
            };
        }

        private int CompareBudgetPriority(
            GameLightFixture left,
            GameLightFixture right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int logical = right.LogicalOn.CompareTo(left.LogicalOn);
            if (logical != 0)
            {
                return logical;
            }

            if (focus != null)
            {
                float leftDistance = (left.WorldPosition - focus.position)
                    .sqrMagnitude;
                float rightDistance = (right.WorldPosition - focus.position)
                    .sqrMagnitude;
                int distance = leftDistance.CompareTo(rightDistance);
                if (distance != 0)
                {
                    return distance;
                }
            }

            // Distance is the primary protection against light leaking from a
            // nearby streamed interior. If an exterior zone always won first,
            // its street lamps consumed the bounded shadow atlas and the
            // closer room lights shone straight through donor shell geometry.
            int zone = IsRelevantZone(right.ZoneId).CompareTo(
                IsRelevantZone(left.ZoneId));
            if (zone != 0)
            {
                return zone;
            }

            return string.CompareOrdinal(left.FixtureId, right.FixtureId);
        }

        private void ScanLoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                ScanScene(SceneManager.GetSceneAt(index));
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _) =>
            ScanScene(scene);

        private void ScanScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameLightFixture[] foundFixtures =
                    roots[rootIndex].GetComponentsInChildren<GameLightFixture>(true);
                for (int index = 0; index < foundFixtures.Length; index++)
                {
                    RegisterFixture(foundFixtures[index]);
                }

                LightingZone[] foundZones =
                    roots[rootIndex].GetComponentsInChildren<LightingZone>(true);
                for (int index = 0; index < foundZones.Length; index++)
                {
                    RegisterZone(foundZones[index]);
                }
            }
        }

        public void ConfigureProfiles(
            LightingCalibrationProfile configuredCalibration,
            LightingQualityProfile configuredLow,
            LightingQualityProfile configuredMedium,
            LightingQualityProfile configuredHigh,
            LightingQualityProfile configuredUltra,
            Transform configuredFocus)
        {
            calibration = configuredCalibration;
            lowQuality = configuredLow;
            mediumQuality = configuredMedium;
            highQuality = configuredHigh;
            ultraQuality = configuredUltra;
            focus = configuredFocus;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            LightingCalibrationProfile configuredCalibration,
            LightingQualityProfile configuredLow,
            LightingQualityProfile configuredMedium,
            LightingQualityProfile configuredHigh,
            LightingQualityProfile configuredUltra,
            Transform configuredFocus)
        {
            ConfigureProfiles(
                configuredCalibration,
                configuredLow,
                configuredMedium,
                configuredHigh,
                configuredUltra,
                configuredFocus);
        }
#endif
    }
}
