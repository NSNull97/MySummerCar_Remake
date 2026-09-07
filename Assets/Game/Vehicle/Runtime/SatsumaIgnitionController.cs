using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaIgnitionState
    {
        Off = 0,
        Accessory = 1,
        Starting = 2,
    }

    /// <summary>
    /// Project-owned reproduction of the donor Satsuma ignition-lock gesture.
    /// The local attempted-start latch deliberately is not engine-running
    /// feedback: the donor sets it as soon as START is reached, even if the
    /// downstream starter circuit rejects the attempt.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class SatsumaIgnitionController : MonoBehaviour
    {
        public const string ColumnPartDefinitionId =
            "vehicle.satsuma.part.steering-column";
        public const string ColumnMountId = "mount.satsuma.steering-column";
        public const double StartHoldSeconds = 0.4d;
        public const float AccessoryAngleDegrees = -30f;
        public const float StartingAngleDegrees = -60f;

        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private SatsumaElectricalSystem electrical;
        [SerializeField] private VehicleSimulationHost simulationHost;
        [SerializeField] private Transform keyPivot;
        [SerializeField] private GameObject visibleKey;
        [SerializeField] private GameObject installedPresentation;
        [SerializeField] private Quaternion keyRestLocalRotation = Quaternion.identity;
        [SerializeField] private SatsumaIgnitionState state;
        [SerializeField] private bool attemptedStart;

        private ISatsumaKeyAccess keyAccess;
        private bool held;
        private bool turnOffOnShortRelease;
        private double holdStartedAtRealtime;

        // Presentation-only gesture events; restore and availability refreshes
        // do not replay physical key insertion/removal feedback.
        public event Action<bool> KeySoundRequested;

        public VehicleAssemblyController Assembly => assembly;
        public SatsumaElectricalSystem Electrical => electrical;
        public VehicleSimulationHost SimulationHost => simulationHost;
        public Transform KeyPivot => keyPivot;
        public GameObject VisibleKey => visibleKey;
        public GameObject InstalledPresentation => installedPresentation;
        public SatsumaIgnitionState State => state;
        public bool IgnitionOn => state != SatsumaIgnitionState.Off;
        public bool Starting => state == SatsumaIgnitionState.Starting;
        public bool StarterRequested =>
            Starting && CanOperate && electrical != null && electrical.StarterCircuitReady;
        public bool IsHeld => held;
        public bool HasAttemptedStart => attemptedStart;
        public bool ColumnInstalled => TryGetInstalledColumn(out _);
        public bool CanOperate =>
            isActiveAndEnabled && ColumnInstalled && keyAccess != null && keyAccess.HasAccess;

        public void Configure(
            VehicleAssemblyController configuredAssembly,
            SatsumaElectricalSystem configuredElectrical,
            VehicleSimulationHost configuredSimulationHost,
            Transform configuredKeyPivot = null,
            GameObject configuredVisibleKey = null,
            GameObject configuredInstalledPresentation = null)
        {
            assembly = configuredAssembly != null
                ? configuredAssembly
                : throw new ArgumentNullException(nameof(configuredAssembly));
            electrical = configuredElectrical != null
                ? configuredElectrical
                : throw new ArgumentNullException(nameof(configuredElectrical));
            simulationHost = configuredSimulationHost != null
                ? configuredSimulationHost
                : throw new ArgumentNullException(nameof(configuredSimulationHost));
            keyPivot = configuredKeyPivot;
            visibleKey = configuredVisibleKey;
            installedPresentation = configuredInstalledPresentation;
            keyRestLocalRotation = keyPivot != null
                ? keyPivot.localRotation
                : Quaternion.identity;
            RestorePersistentState(false);
        }

        public void BindKeyAccess(ISatsumaKeyAccess configuredKeyAccess)
        {
            keyAccess = configuredKeyAccess ??
                throw new ArgumentNullException(nameof(configuredKeyAccess));
            RefreshAvailability();
        }

        public bool TryBeginPrimaryHold(double realtimeSeconds)
        {
            RefreshAvailability();
            if (!IsFiniteRealtime(realtimeSeconds) || !CanOperate || held || Starting)
            {
                return false;
            }

            KeySoundRequested?.Invoke(true);

            // A donor attempted START sets MotorOn locally even if the starter
            // circuit cannot turn the engine. The next down edge therefore
            // turns the ignition fully off immediately.
            if (state == SatsumaIgnitionState.Accessory && attemptedStart)
            {
                TurnOff();
                return true;
            }

            turnOffOnShortRelease = state == SatsumaIgnitionState.Accessory;
            if (state == SatsumaIgnitionState.Off)
            {
                state = SatsumaIgnitionState.Accessory;
                attemptedStart = false;
            }

            held = true;
            holdStartedAtRealtime = realtimeSeconds;
            ApplyPresentation();
            return true;
        }

        public bool ContinuePrimaryHold(double realtimeSeconds)
        {
            RefreshAvailability();
            if (!held || !CanOperate || !IsFiniteRealtime(realtimeSeconds))
            {
                CancelHold();
                return false;
            }

            double elapsed = realtimeSeconds - holdStartedAtRealtime;
            if (elapsed < 0d)
            {
                CancelHold();
                return false;
            }

            if (state == SatsumaIgnitionState.Accessory &&
                realtimeSeconds >= holdStartedAtRealtime + StartHoldSeconds)
            {
                state = SatsumaIgnitionState.Starting;
                attemptedStart = true;
                ApplyPresentation();
            }

            return true;
        }

        public void ReleasePrimaryHold() =>
            ReleasePrimaryHold(Time.realtimeSinceStartupAsDouble);

        public void ReleasePrimaryHold(double realtimeSeconds)
        {
            if (!held)
            {
                return;
            }

            // A low frame rate can deliver the release edge without a final
            // held callback. Sample the absolute donor timer before deciding
            // between the short-click and START branches.
            if (!ContinuePrimaryHold(realtimeSeconds))
            {
                return;
            }

            bool wasStarting = state == SatsumaIgnitionState.Starting;
            bool shouldTurnOff = !wasStarting && turnOffOnShortRelease;
            ClearHeldIntent();
            if (wasStarting)
            {
                state = SatsumaIgnitionState.Accessory;
            }
            else if (shouldTurnOff)
            {
                TurnOff();
                return;
            }

            ApplyPresentation();
        }

        /// <summary>
        /// Cancels only the transient gesture. Losing the column/key capability
        /// does not silently rewrite a persisted Accessory state.
        /// </summary>
        public void CancelHold()
        {
            if (state == SatsumaIgnitionState.Starting)
            {
                state = SatsumaIgnitionState.Accessory;
            }

            ClearHeldIntent();
            ApplyPresentation();
        }

        public void RestorePersistentState(bool restoredIgnitionOn)
        {
            ClearHeldIntent();
            attemptedStart = false;
            state = restoredIgnitionOn
                ? SatsumaIgnitionState.Accessory
                : SatsumaIgnitionState.Off;
            ApplyPresentation();
        }

        public void RefreshAvailability()
        {
            if (held && !CanOperate)
            {
                CancelHold();
                return;
            }

            ApplyPresentation();
        }

        private void TurnOff()
        {
            bool wasOn = state != SatsumaIgnitionState.Off;
            ClearHeldIntent();
            state = SatsumaIgnitionState.Off;
            attemptedStart = false;
            ApplyPresentation();
            if (wasOn) KeySoundRequested?.Invoke(false);
        }

        private void ClearHeldIntent()
        {
            held = false;
            turnOffOnShortRelease = false;
            holdStartedAtRealtime = 0d;
        }

        private bool TryGetInstalledColumn(out PartInstance column)
        {
            column = null;
            if (assembly == null ||
                !assembly.Graph.TryGetMount(ColumnMountId, out MountPointRuntime mount) ||
                !mount.IsOccupied || mount.InstalledPart == null ||
                mount.InstalledPart.Definition == null ||
                !string.Equals(
                    mount.InstalledPart.Definition.DefinitionId,
                    ColumnPartDefinitionId,
                    StringComparison.Ordinal) ||
                !mount.InstalledPart.IsInstalled ||
                !string.Equals(
                    mount.InstalledPart.RuntimeState.InstalledMountId,
                    ColumnMountId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            column = mount.InstalledPart;
            return true;
        }

        private void Update()
        {
            RefreshAvailability();
        }

        private void OnEnable()
        {
            RefreshAvailability();
        }

        private void OnDisable()
        {
            CancelHold();
        }

        private void ApplyPresentation()
        {
            bool columnInstalled = ColumnInstalled;
            if (installedPresentation != null &&
                installedPresentation.activeSelf != columnInstalled)
            {
                installedPresentation.SetActive(columnInstalled);
            }

            if (keyPivot != null)
            {
                float angle = state switch
                {
                    SatsumaIgnitionState.Accessory => AccessoryAngleDegrees,
                    SatsumaIgnitionState.Starting => StartingAngleDegrees,
                    _ => 0f,
                };
                keyPivot.localRotation = keyRestLocalRotation *
                    Quaternion.AngleAxis(angle, Vector3.up);
            }

            bool showKey = columnInstalled && state != SatsumaIgnitionState.Off;
            if (visibleKey != null && visibleKey.activeSelf != showKey)
            {
                visibleKey.SetActive(showKey);
            }
        }

        private static bool IsFiniteRealtime(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }
}
