using System;
using System.Collections.Generic;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    // The 26 permanent donor Wiring/Triggers connections. Values are append-only:
    // schema-v2 saves store their stable names, not enum ordinals.
    public enum SatsumaElectricalConnection
    {
        Alternator = 0,
        AmplifierPower = 1,
        AmplifierAudio = 2,
        BatteryHarness = 3,
        CoilHarness = 4,
        Dash1 = 5,
        Dash2 = 6,
        GroundBattery = 7,
        MarkerLeft = 8,
        MarkerRight = 9,
        FuelTank = 10,
        GaugeAfr = 11,
        GaugeExtra = 12,
        FrontLightsHarness = 13,
        HeadlightLeft = 14,
        HeadlightRight = 15,
        Ignition = 16,
        RadiatorFan = 17,
        Radio = 18,
        RearlightLeft = 19,
        RearlightRight = 20,
        RegulatorHarness = 21,
        Starter = 22,
        SubwooferLeft = 23,
        SubwooferRight = 24,
        SwitchLights = 25,
    }

    public enum SatsumaElectricalFastener
    {
        BatteryPositiveTerminal = 0,
        BatteryNegativeTerminal = 1,
        StarterCable = 2,
    }

    public enum SatsumaElectricalPresentationRule
    {
        Connection = 0,
        BatteryPositiveShoe = 1,
        BatteryNegativeShoe = 2,
    }

    [Serializable]
    public sealed class SatsumaElectricalConnectionBinding
    {
        [SerializeField] private SatsumaElectricalPresentationRule rule;
        [SerializeField] private SatsumaElectricalConnection connection;
        [SerializeField] private GameObject installedPresentation;

        public SatsumaElectricalConnectionBinding(
            SatsumaElectricalConnection configuredConnection,
            GameObject configuredPresentation)
        {
            rule = SatsumaElectricalPresentationRule.Connection;
            connection = configuredConnection;
            installedPresentation = configuredPresentation;
        }

        public SatsumaElectricalConnectionBinding(
            SatsumaElectricalPresentationRule configuredRule,
            GameObject configuredPresentation)
        {
            if (configuredRule == SatsumaElectricalPresentationRule.Connection)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredRule));
            }

            rule = configuredRule;
            connection = default;
            installedPresentation = configuredPresentation;
        }

        public SatsumaElectricalPresentationRule Rule => rule;
        public SatsumaElectricalConnection Connection => connection;
        public GameObject InstalledPresentation => installedPresentation;
    }

    [Serializable]
    public sealed class SatsumaElectricalSaveDto
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string[] installedConnectionIds = Array.Empty<string>();
        public int batteryPlusStage;
        public int batteryMinusStage;
        public int starterCableStage;

        // Schema-v1 migration fields from the first narrow prototype.
        public bool batteryPlusInstalled;
        public bool batteryMinusInstalled;
        public bool batteryHarnessInstalled;
        public bool ignitionInstalled;
        public bool switchLightsInstalled;

        public bool TryValidate(out string failure)
        {
            if (schemaVersion is < 1 or > CurrentSchemaVersion ||
                !IsStageValid(batteryPlusStage) ||
                !IsStageValid(batteryMinusStage) ||
                !IsStageValid(starterCableStage))
            {
                failure = "Satsuma electrical save payload is invalid.";
                return false;
            }

            if (schemaVersion == 1)
            {
                if ((!batteryPlusInstalled && batteryPlusStage != 0) ||
                    (!batteryMinusInstalled && batteryMinusStage != 0) ||
                    starterCableStage != 0)
                {
                    failure = "Satsuma electrical schema-v1 payload is invalid.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            var parsed = new HashSet<SatsumaElectricalConnection>();
            string[] ids = installedConnectionIds ?? Array.Empty<string>();
            for (int index = 0; index < ids.Length; index++)
            {
                if (!Enum.TryParse(ids[index], false, out SatsumaElectricalConnection value) ||
                    !Enum.IsDefined(typeof(SatsumaElectricalConnection), value) ||
                    !parsed.Add(value))
                {
                    failure = "Satsuma electrical save contains an unknown or duplicate connection.";
                    return false;
                }
            }

            bool hasPositiveShoe = parsed.Contains(SatsumaElectricalConnection.BatteryHarness) ||
                parsed.Contains(SatsumaElectricalConnection.Starter);
            if ((!hasPositiveShoe && batteryPlusStage != 0) ||
                (!parsed.Contains(SatsumaElectricalConnection.GroundBattery) &&
                    batteryMinusStage != 0) ||
                (!parsed.Contains(SatsumaElectricalConnection.Starter) &&
                    starterCableStage != 0))
            {
                failure = "Satsuma electrical fastener exists without its donor cable.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsStageValid(int value) =>
            value >= 0 && value <= SatsumaElectricalSystem.FastenerMaximumStage;
    }

    /// <summary>
    /// Project-owned reimplementation of the donor Wiring database/FSM network.
    /// Use arms the nearby ends. A wire is installed only when its other end
    /// is also selected (donor Sound/CLOSELOOP handshake). Pending selections
    /// are transient and RESETWIRING clears them after a completed connection.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class SatsumaElectricalSystem : MonoBehaviour
    {
        public const int FastenerMaximumStage = 8;
        public const int TerminalMaximumStage = FastenerMaximumStage;
        public const float DonorEndpointToleranceMeters = 0.1f;
        public const float DonorMinimumUsableVoltage = 9.7f;
        public const string BatteryPartDefinitionId = "vehicle.satsuma.part.battery";
        public const string StarterPartDefinitionId = "vehicle.satsuma.part.starter";
        public const string DashboardPartDefinitionId =
            "vehicle.satsuma.part.dashboard";
        public const string DashboardMetersMountId =
            "mount.satsuma.dashboard.meters";

        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private VehicleSimulationHost simulationHost;
        [SerializeField] private AssemblyVehiclePrerequisiteAdapter prerequisites;
        [SerializeField] private SatsumaElectricalConnectionBinding[] bindings =
            Array.Empty<SatsumaElectricalConnectionBinding>();
        [SerializeField] private bool[] installed = new bool[ConnectionCount];
        [SerializeField] private int batteryPlusStage;
        [SerializeField] private int batteryMinusStage;
        [SerializeField] private int starterCableStage;

        private readonly List<SatsumaWiringConnectorInteractionTarget> endpoints =
            new List<SatsumaWiringConnectorInteractionTarget>(52);
        private readonly byte[] pendingEndpoints = new byte[ConnectionCount];
        private readonly byte[] pressedEndpoints = new byte[ConnectionCount];
        private bool previousElectricsOk;

        private static readonly int ConnectionCount =
            Enum.GetValues(typeof(SatsumaElectricalConnection)).Length;

        public event Action StateChanged;

        public bool IsBatteryInstalled => IsPartInstalled(BatteryPartDefinitionId);

        public bool DashboardControlsAvailable =>
            IsPartInstalled(DashboardPartDefinitionId) &&
            assemblyController != null &&
            assemblyController.Graph != null &&
            assemblyController.Graph.TryGetMount(
                DashboardMetersMountId,
                out MountPointRuntime metersMount) &&
            metersMount.IsOccupied &&
            metersMount.FastenerGroup.IsBolted;

        public bool HasBatteryPositiveShoe =>
            IsConnectionInstalled(SatsumaElectricalConnection.BatteryHarness) ||
            IsConnectionInstalled(SatsumaElectricalConnection.Starter);

        public bool HasBatteryNegativeShoe =>
            IsConnectionInstalled(SatsumaElectricalConnection.GroundBattery);

        public float BatteryVoltage =>
            simulationHost != null && simulationHost.State != null
                ? simulationHost.State.BatteryVoltage
                : 0f;

        public bool ElectricsOk =>
            IsBatteryInstalled &&
            HasBatteryPositiveShoe &&
            HasBatteryNegativeShoe &&
            IsConnectionInstalled(SatsumaElectricalConnection.BatteryHarness) &&
            IsConnectionInstalled(SatsumaElectricalConnection.Ignition) &&
            batteryPlusStage >= FastenerMaximumStage &&
            batteryMinusStage >= FastenerMaximumStage &&
            BatteryVoltage > DonorMinimumUsableVoltage;

        /// <summary>
        /// Donor Starter/Wiring adds the installed and tightened positive
        /// starter lead after the ordinary ElectricsOK check. GroundBattery
        /// and the tight negative terminal are already part of ElectricsOk.
        /// </summary>
        public bool StarterCircuitReady =>
            ElectricsOk &&
            IsPartInstalled(StarterPartDefinitionId) &&
            IsConnectionInstalled(SatsumaElectricalConnection.Starter) &&
            starterCableStage >= FastenerMaximumStage;

        public bool WipersPowered => ElectricsOk &&
            IsConnectionInstalled(SatsumaElectricalConnection.SwitchLights);

        public void Configure(
            VehicleAssemblyController configuredAssembly,
            VehicleSimulationHost configuredSimulation,
            AssemblyVehiclePrerequisiteAdapter configuredPrerequisites,
            SatsumaElectricalConnectionBinding[] configuredBindings)
        {
            assemblyController = configuredAssembly != null
                ? configuredAssembly
                : throw new ArgumentNullException(nameof(configuredAssembly));
            simulationHost = configuredSimulation != null
                ? configuredSimulation
                : throw new ArgumentNullException(nameof(configuredSimulation));
            prerequisites = configuredPrerequisites != null
                ? configuredPrerequisites
                : throw new ArgumentNullException(nameof(configuredPrerequisites));
            bindings = configuredBindings != null
                ? (SatsumaElectricalConnectionBinding[])configuredBindings.Clone()
                : throw new ArgumentNullException(nameof(configuredBindings));
            EnsureStateArray();
            ResetState();
        }

        public bool IsPartInstalled(string partDefinitionId) =>
            !string.IsNullOrWhiteSpace(partDefinitionId) &&
            assemblyController != null &&
            assemblyController.Graph != null &&
            assemblyController.Graph.IsPartDefinitionInstalled(partDefinitionId);

        public bool ArePartRequirementsMet(string[] partDefinitionIds, bool matchAny)
        {
            if (partDefinitionIds == null || partDefinitionIds.Length == 0)
            {
                return true;
            }

            for (int index = 0; index < partDefinitionIds.Length; index++)
            {
                bool installedPart = IsPartInstalled(partDefinitionIds[index]);
                if (matchAny && installedPart)
                {
                    return true;
                }

                if (!matchAny && !installedPart)
                {
                    return false;
                }
            }

            return !matchAny;
        }

        public void RegisterEndpoint(SatsumaWiringConnectorInteractionTarget endpoint)
        {
            if (endpoint != null && !endpoints.Contains(endpoint))
            {
                endpoints.Add(endpoint);
            }
        }

        public bool IsConnectionInstalled(SatsumaElectricalConnection connection)
        {
            EnsureStateArray();
            int index = (int)connection;
            return index >= 0 && index < installed.Length && installed[index];
        }

        public bool CanActivateCluster(Vector3 wiringToolPosition)
        {
            EnsureEndpointRegistry();
            PrunePendingEndpoints();
            float toleranceSquared = DonorEndpointToleranceMeters *
                DonorEndpointToleranceMeters;
            for (int index = 0; index < endpoints.Count; index++)
            {
                SatsumaWiringConnectorInteractionTarget endpoint = endpoints[index];
                if (endpoint != null && endpoint.IsEndpointAvailable &&
                    (endpoint.transform.position - wiringToolPosition).sqrMagnitude <=
                        toleranceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public int ActivateCluster(Vector3 wiringToolPosition)
        {
            EnsureEndpointRegistry();
            PrunePendingEndpoints();
            float toleranceSquared = DonorEndpointToleranceMeters *
                DonorEndpointToleranceMeters;
            Array.Clear(pressedEndpoints, 0, pressedEndpoints.Length);
            for (int index = 0; index < endpoints.Count; index++)
            {
                SatsumaWiringConnectorInteractionTarget endpoint = endpoints[index];
                if (endpoint != null && endpoint.IsEndpointAvailable &&
                    (endpoint.transform.position - wiringToolPosition).sqrMagnitude <=
                        toleranceSquared)
                {
                    pressedEndpoints[(int)endpoint.Connection] |=
                        (byte)(1 << endpoint.Endpoint);
                }
            }

            int installedCount = 0;
            // Snapshot every end hit by this press before any completed wire
            // resets the other pending FSMs. Pressing the same end twice never
            // substitutes for visiting the opposite end.
            for (int index = 0; index < pendingEndpoints.Length; index++)
            {
                pendingEndpoints[index] |= pressedEndpoints[index];
            }

            for (int index = 0; index < pendingEndpoints.Length; index++)
            {
                var connection = (SatsumaElectricalConnection)index;
                if (pressedEndpoints[index] != 0 && pendingEndpoints[index] == 3 &&
                    HasBothEligibleEndpoints(connection))
                {
                    installed[index] = true;
                    installedCount++;
                }
            }

            if (installedCount > 0)
            {
                ClearPendingEndpoints();
                RefreshState(forceEvent: true);
            }

            return installedCount;
        }

        public bool IsEndpointArmed(SatsumaElectricalConnection connection, int endpoint)
        {
            int index = (int)connection;
            return index >= 0 && index < pendingEndpoints.Length &&
                endpoint is >= 0 and <= 1 &&
                (pendingEndpoints[index] & (1 << endpoint)) != 0;
        }

        public bool IsMountFastenerBelowStage(
            string mountId, string fastenerDefinitionId, int maximumStageExclusive) =>
            assemblyController != null &&
            assemblyController.Graph.TryGetMount(mountId, out MountPointRuntime mount) &&
            mount.IsOccupied &&
            mount.TryGetFastener(fastenerDefinitionId, out FastenerInstance fastener) &&
            fastener.Stage < maximumStageExclusive;

        public bool TryInstallConnection(SatsumaElectricalConnection connection)
        {
            EnsureStateArray();
            int index = (int)connection;
            if (index < 0 || index >= installed.Length || installed[index])
            {
                return false;
            }

            installed[index] = true;
            ClearPendingEndpoints();
            RefreshState(forceEvent: true);
            return true;
        }

        public int GetFastenerStage(SatsumaElectricalFastener fastener) =>
            fastener switch
            {
                SatsumaElectricalFastener.BatteryPositiveTerminal => batteryPlusStage,
                SatsumaElectricalFastener.BatteryNegativeTerminal => batteryMinusStage,
                SatsumaElectricalFastener.StarterCable => starterCableStage,
                _ => 0,
            };

        public bool IsFastenerAvailable(SatsumaElectricalFastener fastener) =>
            fastener switch
            {
                SatsumaElectricalFastener.BatteryPositiveTerminal =>
                    IsBatteryInstalled && HasBatteryPositiveShoe,
                SatsumaElectricalFastener.BatteryNegativeTerminal =>
                    IsBatteryInstalled && HasBatteryNegativeShoe,
                SatsumaElectricalFastener.StarterCable =>
                    IsConnectionInstalled(SatsumaElectricalConnection.Starter),
                _ => false,
            };

        public bool TryTurnFastener(
            SatsumaElectricalFastener fastener,
            float signedNotches)
        {
            if (!IsFastenerAvailable(fastener) ||
                !float.IsFinite(signedNotches) ||
                Mathf.Abs(signedNotches) < 0.001f)
            {
                return false;
            }

            int current = GetFastenerStage(fastener);
            int next = Mathf.Clamp(current + (signedNotches > 0f ? 1 : -1),
                0, FastenerMaximumStage);
            if (next == current)
            {
                return false;
            }

            SetFastenerStage(fastener, next);
            RefreshState(forceEvent: true);
            return true;
        }

        public SatsumaElectricalSaveDto CaptureSaveData()
        {
            var ids = new List<string>(ConnectionCount);
            foreach (SatsumaElectricalConnection connection in
                     Enum.GetValues(typeof(SatsumaElectricalConnection)))
            {
                if (IsConnectionInstalled(connection))
                {
                    ids.Add(connection.ToString());
                }
            }

            return new SatsumaElectricalSaveDto
            {
                installedConnectionIds = ids.ToArray(),
                batteryPlusStage = batteryPlusStage,
                batteryMinusStage = batteryMinusStage,
                starterCableStage = starterCableStage,
            };
        }

        public bool TryRestore(SatsumaElectricalSaveDto dto, out string failure)
        {
            if (dto == null)
            {
                ResetState();
                failure = string.Empty;
                return true;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            ClearPendingEndpoints();
            installed = new bool[ConnectionCount];
            if (dto.schemaVersion == 1)
            {
                SetInstalled(SatsumaElectricalConnection.BatteryHarness,
                    dto.batteryHarnessInstalled);
                SetInstalled(SatsumaElectricalConnection.GroundBattery,
                    dto.batteryMinusInstalled);
                SetInstalled(SatsumaElectricalConnection.Ignition,
                    dto.ignitionInstalled);
                SetInstalled(SatsumaElectricalConnection.SwitchLights,
                    dto.switchLightsInstalled);
            }
            else
            {
                string[] ids = dto.installedConnectionIds ?? Array.Empty<string>();
                for (int index = 0; index < ids.Length; index++)
                {
                    Enum.TryParse(ids[index], false,
                        out SatsumaElectricalConnection connection);
                    SetInstalled(connection, true);
                }
            }

            batteryPlusStage = dto.batteryPlusStage;
            batteryMinusStage = dto.batteryMinusStage;
            starterCableStage = dto.starterCableStage;
            RefreshState(forceEvent: true);
            failure = string.Empty;
            return true;
        }

        public void ResetState()
        {
            ClearPendingEndpoints();
            installed = new bool[ConnectionCount];
            batteryPlusStage = 0;
            batteryMinusStage = 0;
            starterCableStage = 0;
            RefreshState(forceEvent: true);
        }

        private void Awake()
        {
            EnsureStateArray();
            EnsureEndpointRegistry();
            RefreshState(forceEvent: false);
        }

        private void Update()
        {
            PrunePendingEndpoints();
            RefreshState(forceEvent: false);
        }

        private void OnDisable() => ClearPendingEndpoints();

        private void ClearPendingEndpoints() =>
            Array.Clear(pendingEndpoints, 0, pendingEndpoints.Length);

        private void PrunePendingEndpoints()
        {
            for (int index = 0; index < endpoints.Count; index++)
            {
                SatsumaWiringConnectorInteractionTarget endpoint = endpoints[index];
                if (endpoint != null && IsEndpointArmed(endpoint.Connection, endpoint.Endpoint) &&
                    (!endpoint.isActiveAndEnabled ||
                    !endpoint.ArePartRequirementsMet))
                {
                    pendingEndpoints[(int)endpoint.Connection] &=
                        (byte)~(1 << endpoint.Endpoint);
                }
            }
        }

        private bool HasBothEligibleEndpoints(SatsumaElectricalConnection connection)
        {
            if (IsConnectionInstalled(connection))
            {
                return false;
            }

            bool first = false;
            bool second = false;
            for (int index = 0; index < endpoints.Count; index++)
            {
                SatsumaWiringConnectorInteractionTarget endpoint = endpoints[index];
                if (endpoint == null || endpoint.Connection != connection ||
                    !endpoint.ArePartRequirementsMet)
                {
                    continue;
                }

                first |= endpoint.Endpoint == 0;
                second |= endpoint.Endpoint == 1;
            }

            return first && second;
        }

        private void EnsureEndpointRegistry()
        {
            endpoints.RemoveAll(value => value == null);
            SatsumaWiringConnectorInteractionTarget[] found =
                GetComponentsInChildren<SatsumaWiringConnectorInteractionTarget>(true);
            for (int index = 0; index < found.Length; index++)
            {
                RegisterEndpoint(found[index]);
            }
        }

        private void RefreshState(bool forceEvent)
        {
            EnsureStateArray();
            if (bindings != null)
            {
                for (int index = 0; index < bindings.Length; index++)
                {
                    SatsumaElectricalConnectionBinding binding = bindings[index];
                    if (binding?.InstalledPresentation != null)
                    {
                        binding.InstalledPresentation.SetActive(
                            IsPresentationVisible(binding));
                    }
                }
            }

            bool electricsOk = ElectricsOk;
            prerequisites?.SetBatteryVoltage(electricsOk ? BatteryVoltage : 0f);
            if (forceEvent || electricsOk != previousElectricsOk)
            {
                previousElectricsOk = electricsOk;
                StateChanged?.Invoke();
            }
        }

        private bool IsPresentationVisible(SatsumaElectricalConnectionBinding binding) =>
            binding.Rule switch
            {
                SatsumaElectricalPresentationRule.Connection =>
                    IsConnectionInstalled(binding.Connection),
                SatsumaElectricalPresentationRule.BatteryPositiveShoe =>
                    HasBatteryPositiveShoe,
                SatsumaElectricalPresentationRule.BatteryNegativeShoe =>
                    HasBatteryNegativeShoe,
                _ => false,
            };

        private void EnsureStateArray()
        {
            if (installed == null || installed.Length != ConnectionCount)
            {
                bool[] migrated = new bool[ConnectionCount];
                if (installed != null)
                {
                    Array.Copy(installed, migrated,
                        Mathf.Min(installed.Length, migrated.Length));
                }

                installed = migrated;
            }
        }

        private void SetInstalled(SatsumaElectricalConnection connection, bool value)
        {
            installed[(int)connection] = value;
        }

        private void SetFastenerStage(SatsumaElectricalFastener fastener, int stage)
        {
            switch (fastener)
            {
                case SatsumaElectricalFastener.BatteryPositiveTerminal:
                    batteryPlusStage = stage;
                    break;
                case SatsumaElectricalFastener.BatteryNegativeTerminal:
                    batteryMinusStage = stage;
                    break;
                case SatsumaElectricalFastener.StarterCable:
                    starterCableStage = stage;
                    break;
            }
        }
    }
}
