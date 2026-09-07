using System;
using MSC.Interaction.Capabilities;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>Passive geometric pour receiver, deliberately not an instant held-item F action.</summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaServiceFluidReceiver : MonoBehaviour, ILiquidContainerTarget, ILiquidLevelPresentationSource
    {
        [SerializeField] private VehicleSimulationHost simulation;
        [SerializeField] private AssemblyServiceCapState caps;
        [SerializeField] private int capIndex;
        [SerializeField] private SatsumaServiceFluid fluid;
        public SatsumaServiceFluid Fluid => fluid;
        public AssemblyServiceCapState Caps => caps;
        public int CapIndex => capIndex;
        public float CapacityLiters => SatsumaServiceFluidRules.Capacity(fluid);
        public float ContentLiters => simulation?.State?.GetServiceFluidLiters(fluid) ?? 0f;
        public bool IsOpen => isActiveAndEnabled && caps != null && caps.IsAvailable && caps.IsOpen(capIndex);
        // Oil lives in the sump, not under the rocker-cover filling neck.
        public bool IsLiquidLevelVisible => IsOpen && fluid != SatsumaServiceFluid.MotorOil &&
            simulation?.State?.SatsumaOperating != null && ContentLiters > .0001f;
        public float LiquidLevel01 => Mathf.Clamp01(ContentLiters / CapacityLiters);

        public void Configure(VehicleSimulationHost host, AssemblyServiceCapState state, int index)
        {
            if (host == null || state == null || index < 0 || index >= state.Count) throw new ArgumentException("Explicit fluid host/cap required.");
            simulation = host; caps = state; capIndex = index;
            fluid = state.Kind(index) switch
            {
                SatsumaServiceCapKind.MotorOil => SatsumaServiceFluid.MotorOil,
                SatsumaServiceCapKind.Coolant => SatsumaServiceFluid.Coolant,
                SatsumaServiceCapKind.BrakeFront => SatsumaServiceFluid.BrakeFront,
                SatsumaServiceCapKind.BrakeRear => SatsumaServiceFluid.BrakeRear,
                _ => SatsumaServiceFluid.Clutch,
            };
        }
        public bool CanAcceptLiquid(string liquidId, float requestedLitres) => IsOpen &&
            simulation?.State?.SatsumaOperating != null && float.IsFinite(requestedLitres) && requestedLitres > 0f &&
            liquidId == SatsumaServiceFluidRules.LiquidId(fluid) && ContentLiters < CapacityLiters;
        public bool TryAcceptLiquid(string liquidId, float requestedLitres, out float acceptedLitres)
        {
            acceptedLitres = 0f;
            if (!CanAcceptLiquid(liquidId, requestedLitres)) return false;
            acceptedLitres = simulation.State.AddServiceFluid(fluid, requestedLitres);
            return acceptedLitres > 0f;
        }
    }
}
