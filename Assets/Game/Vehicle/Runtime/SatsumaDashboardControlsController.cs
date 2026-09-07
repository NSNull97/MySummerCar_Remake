using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    // Donor Selection 1 activates Markers/RearLights, NOT dipped beams.
    public enum SatsumaHeadlightsMode { Off = 0, Parking = 1, Headlights = 2 }
    public enum SatsumaDashboardControlKind { Choke = 0, Hazards = 1, Lights = 2 }

    [Serializable]
    public sealed class SatsumaDashboardControlsSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public float choke01;
        public int headlightsMode;
        public bool hazardsOn;
        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion || !float.IsFinite(choke01) ||
                choke01 < 0f || choke01 > 1f || headlightsMode < 0 || headlightsMode > 2)
            { failure = "Satsuma dashboard control payload is invalid."; return false; }
            failure = string.Empty; return true;
        }
    }

    /// <summary>Physical controls and persistent intent; no invented carburetor/RPM model.</summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaDashboardControlsController : MonoBehaviour
    {
        // Source is frame-based .0303 per rendered frame. Explicit 60 Hz
        // reference conversion removes modern frame-rate-dependent operation.
        public const float ChokeTravelMeters = .03f;
        public const float ChokeUnitsPerSecond = .0303f * 60f;
        public const float HazardHalfCycleSeconds = .4f;
        [SerializeField] private SatsumaElectricalSystem electrical;
        [SerializeField] private Transform chokeKnob;
        [SerializeField] private Transform hazardKnob;
        [SerializeField] private Transform lightsKnob;
        [SerializeField] private Vector3 chokeRestPosition;
        [SerializeField] private Quaternion chokeRestRotation = Quaternion.identity;
        [SerializeField] private Quaternion hazardRestRotation = Quaternion.identity;
        [SerializeField] private Quaternion lightsRestRotation = Quaternion.identity;
        [SerializeField] private float choke01;
        [SerializeField] private SatsumaHeadlightsMode headlightsMode;
        [SerializeField] private bool hazardsOn;
        private int chokeDirection;
        private float hazardPhase;
        public event Action<SatsumaDashboardControlKind> ActionRequested;
        public SatsumaElectricalSystem Electrical => electrical;
        public Transform ChokeKnob => chokeKnob;
        public Transform HazardKnob => hazardKnob;
        public Transform LightsKnob => lightsKnob;
        public float Choke01 => choke01;
        public SatsumaHeadlightsMode HeadlightsMode => headlightsMode;
        public bool HazardsOn => hazardsOn;
        public bool HazardPulseOn => hazardsOn && hazardPhase < HazardHalfCycleSeconds;
        public bool CanOperate => isActiveAndEnabled && electrical != null && electrical.DashboardControlsAvailable;
        public int ChokeHeldDirection => chokeDirection;

        public void Configure(SatsumaElectricalSystem power, Transform choke, Transform hazards, Transform lights)
        {
            if (power == null || choke == null || hazards == null || lights == null)
                throw new ArgumentException("Explicit dashboard controls and electrical bindings are required.");
            electrical = power; chokeKnob = choke; hazardKnob = hazards; lightsKnob = lights;
            chokeRestPosition = choke.localPosition; chokeRestRotation = choke.localRotation;
            hazardRestRotation = hazards.localRotation; lightsRestRotation = lights.localRotation;
            TryRestore(null, out _);
        }
        public bool TrySetChokeHeldDirection(int direction)
        {
            if (direction == 0) { chokeDirection = 0; return true; }
            if (!CanOperate || direction is < -1 or > 1) return false;
            if (chokeDirection != direction) ActionRequested?.Invoke(SatsumaDashboardControlKind.Choke);
            chokeDirection = direction; return true;
        }
        public bool TryToggleHazards()
        {
            if (!CanOperate) return false;
            hazardsOn = !hazardsOn; hazardPhase = 0f; ApplyPresentation();
            ActionRequested?.Invoke(SatsumaDashboardControlKind.Hazards); return true;
        }
        public bool TryCycleLights()
        {
            if (!CanOperate) return false;
            headlightsMode = (SatsumaHeadlightsMode)(((int)headlightsMode + 1) % 3);
            ApplyPresentation(); ActionRequested?.Invoke(SatsumaDashboardControlKind.Lights); return true;
        }
        public void Simulate(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f) return;
            if (!CanOperate) chokeDirection = 0;
            if (chokeDirection != 0) choke01 = Mathf.Clamp01(choke01 + chokeDirection * ChokeUnitsPerSecond * deltaSeconds);
            if (hazardsOn) hazardPhase = Mathf.Repeat(hazardPhase + deltaSeconds, HazardHalfCycleSeconds * 2f);
            ApplyPresentation();
        }
        public SatsumaDashboardControlsSaveDto CaptureSaveData() => new()
        { choke01 = choke01, headlightsMode = (int)headlightsMode, hazardsOn = hazardsOn };
        public bool TryRestore(SatsumaDashboardControlsSaveDto dto, out string failure)
        {
            if (dto != null && !dto.TryValidate(out failure)) return false;
            choke01 = dto?.choke01 ?? 0f; headlightsMode = (SatsumaHeadlightsMode)(dto?.headlightsMode ?? 0);
            hazardsOn = dto?.hazardsOn ?? false; chokeDirection = 0; hazardPhase = 0f;
            ApplyPresentation(); failure = string.Empty; return true;
        }
        private void ApplyPresentation()
        {
            if (chokeKnob != null) chokeKnob.localPosition = chokeRestPosition +
                chokeRestRotation * new Vector3(0f, -ChokeTravelMeters * choke01, 0f);
            if (hazardKnob != null) hazardKnob.localRotation = hazardRestRotation *
                Quaternion.AngleAxis(hazardsOn ? -45f : 0f, Vector3.up);
            if (lightsKnob != null) lightsKnob.localRotation = lightsRestRotation *
                Quaternion.AngleAxis(-45f * (int)headlightsMode, Vector3.up);
        }
        private void Update() => Simulate(Time.unscaledDeltaTime);
        private void OnDisable() => chokeDirection = 0;
    }
}
