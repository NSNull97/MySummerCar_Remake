using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaDashboardLampFunction { Parking = 0, Headlights = 1, RearTail = 2, Hazard = 3 }

    [Serializable]
    public sealed class SatsumaDashboardLampBinding
    {
        [SerializeField] private string stableLampId;
        [SerializeField] private SatsumaDashboardLampFunction function;
        [SerializeField] private PartInstance owner;
        [SerializeField] private string ownerMountId;
        [SerializeField] private bool requiresBoltedOwner;
        [SerializeField] private string bulbMountId;
        [SerializeField] private SatsumaElectricalConnection[] requiredConnections;
        [SerializeField] private Light light;
        [SerializeField] private Renderer lensRenderer;
        [SerializeField, ColorUsage(true, true)] private Color lensEmissionColor;
        private PartInstance cachedBulb;
        private IAssemblyItemCondition bulbCondition;
        private MaterialPropertyBlock lensProperties;
        private bool lensStateInitialized;
        private bool lensIsOn;
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int AlbedoAffectEmissiveId = Shader.PropertyToID("_AlbedoAffectEmissive");
        public string StableLampId => stableLampId;
        public SatsumaDashboardLampFunction Function => function;
        public PartInstance Owner => owner;
        public string OwnerMountId => ownerMountId;
        public string BulbMountId => bulbMountId;
        public bool RequiresBoltedOwner => requiresBoltedOwner;
        public SatsumaElectricalConnection[] RequiredConnections => requiredConnections;
        public Light Light => light;
        public Renderer LensRenderer => lensRenderer;
        public Color LensEmissionColor => lensEmissionColor;
        public bool IsLensEmitting => lensRenderer != null && lensStateInitialized && lensIsOn;
        public SatsumaDashboardLampBinding(string id, SatsumaDashboardLampFunction purpose,
            PartInstance part, string mount, bool bolted, string bulbMount,
            SatsumaElectricalConnection[] wires, Light output, Renderer lens = null, Color emissionColor = default)
        {
            stableLampId = id; function = purpose; owner = part; ownerMountId = mount;
            requiresBoltedOwner = bolted; bulbMountId = bulbMount ?? string.Empty;
            requiredConnections = (SatsumaElectricalConnection[])wires.Clone(); light = output;
            lensRenderer = lens; lensEmissionColor = emissionColor;
        }
        public bool IsReady(VehicleAssemblyController assembly, SatsumaElectricalSystem electrical)
        {
            if (owner == null || !owner.IsInstalled || assembly?.Graph == null || electrical == null ||
                !electrical.ElectricsOk || !assembly.Graph.TryGetMount(ownerMountId, out MountPointRuntime mount) ||
                mount.InstalledPart != owner || requiresBoltedOwner && !mount.FastenerGroup.IsBolted) return false;
            foreach (var wire in requiredConnections)
                if (!electrical.IsConnectionInstalled(wire)) return false;
            if (string.IsNullOrEmpty(bulbMountId)) return true;
            if (!assembly.Graph.TryGetMount(bulbMountId, out MountPointRuntime bulbMount) || !bulbMount.IsOccupied)
            { cachedBulb = null; bulbCondition = null; return false; }
            if (cachedBulb != bulbMount.InstalledPart)
            {
                cachedBulb = bulbMount.InstalledPart; bulbCondition = null;
                // Only on a changed bulb, not a component scan every frame.
                foreach (MonoBehaviour component in cachedBulb.GetComponents<MonoBehaviour>())
                    if (component is IAssemblyItemCondition condition) { bulbCondition = condition; break; }
            }
            // Fail closed: presence of a mesh is not proof that a purchased
            // bulb is unbroken. Items owns live condition and persistence.
            return bulbCondition != null && !bulbCondition.IsBroken &&
                float.IsFinite(bulbCondition.ConditionPercent) && bulbCondition.ConditionPercent >= 7f;
        }
        public void SetOn(bool on)
        {
            if (light != null && light.enabled != on) light.enabled = on;
            if (lensRenderer == null || lensStateInitialized && lensIsOn == on) return;
            lensProperties ??= new MaterialPropertyBlock();
            // Keep outline/wetness or other existing renderer properties. Do
            // not mutate a shared material or use the light mesh as authority.
            lensRenderer.GetPropertyBlock(lensProperties);
            lensProperties.SetColor(EmissiveColorId, on ? lensEmissionColor : Color.black);
            lensProperties.SetFloat(AlbedoAffectEmissiveId, 1f);
            lensRenderer.SetPropertyBlock(lensProperties);
            lensStateInitialized = true; lensIsOn = on;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class SatsumaDashboardLightingPresenter : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private SatsumaDashboardControlsController controls;
        [SerializeField] private SatsumaDashboardLampBinding[] lamps = Array.Empty<SatsumaDashboardLampBinding>();
        public VehicleAssemblyController Assembly => assembly;
        public SatsumaDashboardControlsController Controls => controls;
        public SatsumaDashboardLampBinding[] Lamps => lamps;
        public void Configure(VehicleAssemblyController vehicle, SatsumaDashboardControlsController dashboard,
            SatsumaDashboardLampBinding[] bindings)
        {
            if (vehicle == null || dashboard == null || bindings == null)
                throw new ArgumentException("Explicit dashboard lighting bindings are required.");
            foreach (var lamp in lamps) lamp?.SetOn(false);
            assembly = vehicle; controls = dashboard; lamps = (SatsumaDashboardLampBinding[])bindings.Clone();
            RefreshOutputs();
        }
        public void RefreshOutputs()
        {
            bool available = isActiveAndEnabled && controls != null && controls.Electrical != null && controls.CanOperate;
            for (int i = 0; i < lamps.Length; i++)
            {
                SatsumaDashboardLampBinding lamp = lamps[i];
                if (lamp == null) continue;
                bool requested = available && (lamp.Function switch
                {
                    SatsumaDashboardLampFunction.Parking => controls.HeadlightsMode == SatsumaHeadlightsMode.Parking,
                    SatsumaDashboardLampFunction.Headlights => controls.HeadlightsMode == SatsumaHeadlightsMode.Headlights,
                    SatsumaDashboardLampFunction.RearTail => controls.HeadlightsMode != SatsumaHeadlightsMode.Off,
                    SatsumaDashboardLampFunction.Hazard => controls.HazardPulseOn &&
                        (controls.Electrical.IsConnectionInstalled(SatsumaElectricalConnection.Dash1) ||
                         controls.Electrical.IsConnectionInstalled(SatsumaElectricalConnection.Dash2)),
                    _ => false,
                });
                lamp.SetOn(requested && lamp.IsReady(assembly, controls.Electrical));
            }
        }
        private void LateUpdate() => RefreshOutputs();
        private void OnDisable() { foreach (var lamp in lamps) lamp?.SetOn(false); }
    }
}
