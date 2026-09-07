using MSC.Presentation.Fluid;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Bootstrap
{
    public static class SatsumaServiceLevelComposition
    {
        public static void Configure(GameObject car)
        {
            if (car == null) return;
            var assembly = car.GetComponent<VehicleAssemblyController>();
            if (assembly == null) return;
            // The real Bootstrap detaches loose parts in Awake, before this
            // composition runs. Their explicit registry, not car ancestry,
            // remains authoritative before installation and after native load.
            foreach (PartInstance part in assembly.Parts)
            foreach (SatsumaServiceFluidReceiver receiver in part.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true))
            {
                if (receiver.Caps.Part != part) continue;
                SatsumaServiceCapKind kind = receiver.Caps.Kind(receiver.CapIndex);
                if (kind == SatsumaServiceCapKind.MotorOil) continue;
                var presenter = receiver.GetComponent<ServiceReservoirLevelPresenter>() ?? receiver.gameObject.AddComponent<ServiceReservoirLevelPresenter>();
                if (presenter.IsConfigured) continue;
                bool coolant = kind == SatsumaServiceCapKind.Coolant;
                // Reviewed mesh cavities: .26855m radiator and .08048m masters.
                // Heights are visual volume calibration, not a second reservoir.
                presenter.Configure(receiver, coolant ? .013f : .015f, coolant ? .22f : .07f,
                    coolant ? new Color(.10f, .45f, .18f, .8f) : new Color(.50f, .36f, .10f, .8f));
            }
        }
    }
}
