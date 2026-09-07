using System;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>Opt-in assembly/electrical adapter. The solver never discovers scene objects.</summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaEngineOperatingSource : MonoBehaviour, ISatsumaOperatingConditionSource
    {
        private const string PartPrefix = "vehicle.satsuma.part.";
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private SatsumaElectricalSystem electrical;
        [SerializeField] private SatsumaDashboardControlsController dashboard;
        [SerializeField] private AssemblyEngineAdjustmentState alternator;
        [SerializeField] private AssemblyEngineAdjustmentState distributor;
        [SerializeField] private AssemblyEngineAdjustmentState mixture;
        [SerializeField] private AssemblyCamshaftTimingState camshaft;
        [SerializeField] private AssemblyValveAdjustmentState valves;
        [SerializeField] private AssemblyServiceCapState oilCap;
        [SerializeField] private AssemblyServiceCapState radiatorCap;
        [SerializeField] private float unboundAmbientCelsius = 20f;

        private float? ambientCelsius;
        public VehicleAssemblyController Assembly => assembly;
        public bool DebugAutocharge { get; private set; }
        public float AmbientCelsius => ambientCelsius ?? unboundAmbientCelsius;
        public bool HasEnvironmentBinding => ambientCelsius.HasValue;

        public void Configure(VehicleAssemblyController owner, SatsumaElectricalSystem electrics,
            SatsumaDashboardControlsController controls, AssemblyEngineAdjustmentState alternatorSetting,
            AssemblyEngineAdjustmentState distributorSetting, AssemblyEngineAdjustmentState mixtureSetting,
            AssemblyCamshaftTimingState cam, AssemblyValveAdjustmentState valveSettings,
            AssemblyServiceCapState oil, AssemblyServiceCapState radiator)
        {
            assembly = owner; electrical = electrics; dashboard = controls;
            alternator = alternatorSetting; distributor = distributorSetting; mixture = mixtureSetting;
            camshaft = cam; valves = valveSettings; oilCap = oil; radiatorCap = radiator;
            if (!ValidateBindings(out string failure)) throw new ArgumentException(failure);
        }

        public bool ValidateBindings(out string failure)
        {
            if (assembly == null || electrical == null || dashboard == null ||
                alternator == null || alternator.Kind != SatsumaEngineAdjustmentKind.Alternator || alternator.Assembly != assembly ||
                distributor == null || distributor.Kind != SatsumaEngineAdjustmentKind.Distributor || distributor.Assembly != assembly ||
                mixture == null || mixture.Kind != SatsumaEngineAdjustmentKind.CarburetorMixture || mixture.Assembly != assembly ||
                camshaft == null || camshaft.Assembly != assembly || valves == null || valves.Assembly != assembly ||
                oilCap == null || oilCap.Count != 1 || oilCap.Kind(0) != SatsumaServiceCapKind.MotorOil ||
                radiatorCap == null || radiatorCap.Count != 1 || radiatorCap.Kind(0) != SatsumaServiceCapKind.Coolant ||
                electrical.gameObject != assembly.gameObject || dashboard.gameObject != assembly.gameObject ||
                !float.IsFinite(unboundAmbientCelsius))
            { failure = "Satsuma operating source requires the existing assembly, controls, tuning and stock cap bindings."; return false; }
            failure = string.Empty; return true;
        }

        // Explicit composition input, not Enviro access or a second weather owner.
        public void SetAmbientTemperature(float celsius)
        {
            if (!float.IsFinite(celsius)) throw new ArgumentOutOfRangeException(nameof(celsius));
            ambientCelsius = Mathf.Clamp(celsius, -50f, 60f);
        }
        public void ClearEnvironmentBinding() => ambientCelsius = null;
        public void SetDebugAutocharge(bool value) => DebugAutocharge = value;

        public SatsumaOperatingInputs CaptureConditions()
        {
            AssemblyGraph graph = assembly.Graph;
            var tuning = new SatsumaTuningInputs(mixture.Setting, dashboard.Choke01,
                distributor.Setting, camshaft.AngleDegrees, alternator.Setting, valves.Intake, valves.Exhaust);
            bool batteryConnected = electrical.IsBatteryInstalled && electrical.HasBatteryPositiveShoe &&
                electrical.HasBatteryNegativeShoe &&
                electrical.GetFastenerStage(SatsumaElectricalFastener.BatteryPositiveTerminal) == 8 &&
                electrical.GetFastenerStage(SatsumaElectricalFastener.BatteryNegativeTerminal) == 8;
            bool chargeWired = Wired(SatsumaElectricalConnection.Alternator) && Wired(SatsumaElectricalConnection.RegulatorHarness);
            PartInstance belt = InstalledAt(graph, SatsumaConsumableAssemblyRules.BeltMountId);
            bool beltUsable = Usable(belt, 1f) && Installed(graph, "crankshaft-pulley") && Installed(graph, "water-pump-pulley");
            var ancillary = new SatsumaAncillaryInputs(batteryConnected,
                electrical.ElectricsOk && Wired(SatsumaElectricalConnection.CoilHarness), chargeWired,
                chargeWired && Wired(SatsumaElectricalConnection.CoilHarness) && Wired(SatsumaElectricalConnection.Ignition),
                Wired(SatsumaElectricalConnection.RadiatorFan), beltUsable,
                Usable(InstalledPart(graph, "water-pump"), 10f), Usable(InstalledPart(graph, "alternator"), 10f));

            PartInstance filter = InstalledAt(graph, SatsumaEngineAdjustmentRules.MountId(SatsumaEngineAdjustmentKind.OilFilter));
            AssemblyEngineAdjustmentState filterSetting = filter != null ? filter.GetComponent<AssemblyEngineAdjustmentState>() : null;
            bool brakeConnected = graph.IsPartDefinitionBolted("vehicle.satsuma.part.brake-master-cylinder") && Installed(graph, "brake-lining");
            bool clutchConnected = graph.IsPartDefinitionBolted("vehicle.satsuma.part.clutch-master-cylinder") && Installed(graph, "clutch-lining");
            var fluids = new SatsumaFluidHardwareInputs(Installed(graph, "oilpan"), filter != null,
                filterSetting != null ? filterSetting.Setting : 0f,
                Mathf.Max(0f, Tightness(graph, "mount.satsuma.engine-block.oilpan") - OilDrainStage(graph)),
                Tightness(graph, SatsumaRockerCoverFastenerMigration.MountId),
                Usable(InstalledPart(graph, "head-gasket"), 10f), Installed(graph, "radiator"),
                Installed(graph, "radiator-hose1") && Installed(graph, "radiator-hose2") && Installed(graph, "radiator-hose3"),
                Tightness(graph, "mount.satsuma.radiator-hose1") + Tightness(graph, "mount.satsuma.engine-block.radiator-hose2") +
                    Tightness(graph, "mount.satsuma.radiator-hose3"),
                radiatorCap.Part.IsInstalled && !radiatorCap.IsOpen(0), oilCap.Part.IsInstalled && !oilCap.IsOpen(0),
                brakeConnected, brakeConnected, clutchConnected,
                SealFraction(graph, "mount.satsuma.brake-lining"), SealFraction(graph, "mount.satsuma.clutch-lining"),
                Installed(graph, "oilpan") && OilDrainStage(graph) < 6);
            return new SatsumaOperatingInputs(tuning, ancillary, fluids,
                SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(graph),
                Condition(InstalledPart(graph, "crankshaft")), AmbientCelsius, DebugAutocharge,
                PlugRoughness(graph), Condition(InstalledPart(graph, "rocker-shaft")));
        }

        public void ApplyWear(in SatsumaWearDelta wear)
        {
            AssemblyGraph graph = assembly.Graph;
            Wear(InstalledPart(graph, "piston1"), wear.Piston); Wear(InstalledPart(graph, "piston2"), wear.Piston);
            Wear(InstalledPart(graph, "piston3"), wear.Piston); Wear(InstalledPart(graph, "piston4"), wear.Piston);
            Wear(InstalledPart(graph, "crankshaft"), wear.Crankshaft);
            Wear(InstalledPart(graph, "head-gasket"), wear.HeadGasket);
            Wear(InstalledPart(graph, "water-pump"), wear.WaterPump);
            Wear(InstalledPart(graph, "rocker-shaft"), wear.RockerShaft);
            // Purchased belt condition is committed only to its item-owned wear sink.
            Wear(InstalledAt(graph, SatsumaConsumableAssemblyRules.BeltMountId), wear.Belt);
        }

        private bool Wired(SatsumaElectricalConnection connection) => electrical.IsConnectionInstalled(connection);
        private static bool Installed(AssemblyGraph graph, string suffix) => InstalledPart(graph, suffix) != null;
        private static PartInstance InstalledPart(AssemblyGraph graph, string suffix)
        {
            // This is the explicit registry, including current dynamic items, not scene discovery.
            PartInstance[] parts = graph.AllRuntimeParts;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i]?.Definition != null && parts[i].IsInstalled &&
                    parts[i].Definition.DefinitionId.AsSpan().StartsWith(PartPrefix, StringComparison.Ordinal) &&
                    parts[i].Definition.DefinitionId.AsSpan(PartPrefix.Length).SequenceEqual(suffix.AsSpan())) return parts[i];
            return null;
        }
        private static PartInstance InstalledAt(AssemblyGraph graph, string id) =>
            graph.TryGetMount(id, out MountPointRuntime mount) ? mount.InstalledPart : null;
        private static float Tightness(AssemblyGraph graph, string id) =>
            graph.TryGetMount(id, out MountPointRuntime mount) && mount.IsOccupied ? mount.FastenerGroup.Tightness : 0f;
        private static int OilDrainStage(AssemblyGraph graph) => graph.TryGetMount("mount.satsuma.engine-block.oilpan", out MountPointRuntime mount) &&
            mount.IsOccupied && mount.TryGetFastener("fastener.satsuma.engine-block-oilpan.boltpm-3", out FastenerInstance plug) ? plug.Stage : 0;
        private static float SealFraction(AssemblyGraph graph, string id) =>
            graph.TryGetMount(id, out MountPointRuntime mount) && mount.IsOccupied ?
                Mathf.Clamp01((float)mount.FastenerGroup.Tightness / Mathf.Max(1, mount.FastenerGroup.Definition.AggregateMaximumTightness)) : 0f;
        private static bool Usable(PartInstance part, float minimum) => part != null && Condition(part) >= minimum;
        private static float Condition(PartInstance part)
        {
            IAssemblyItemCondition condition = part != null ? part.GetComponent<IAssemblyItemCondition>() : null;
            return condition != null && !condition.IsBroken && float.IsFinite(condition.ConditionPercent) ? condition.ConditionPercent : 0f;
        }
        private static void Wear(PartInstance part, float loss)
        { if (part != null && loss > 0f) part.GetComponent<IAssemblyWearSink>()?.TryApplyWear(loss); }
        private static readonly string[] PlugMounts =
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
        };
        private static float PlugRoughness(AssemblyGraph graph)
        {
            float roughness = 0f;
            foreach (string id in PlugMounts)
                if (graph.TryGetMount(id, out MountPointRuntime mount) && mount.IsOccupied && Condition(mount.InstalledPart) >= 1f)
                    roughness += Mathf.Max(1f - SealFraction(graph, id), Mathf.Clamp01((10f - Condition(mount.InstalledPart)) / 9f)) * .25f;
            return roughness;
        }
    }
}
