using System;
using MSC.Items;
using UnityEngine;

namespace MSC.Presentation.Fluid
{
    /// <summary>Replaceable visible consistency only. Never owns, transfers or restores litres.</summary>
    [DisallowMultipleComponent]
    public sealed class ServiceFluidPourPresenter : MonoBehaviour
    {
        private ServiceFluidPourController controller;
        private ProceduralFluidStreamPresenter stream;
        public bool IsConfigured => controller != null;
        public bool IsFlowing => stream != null && stream.IsFlowing;
        public void Configure(ServiceFluidPourController source, string definitionId)
        {
            if (controller != null) throw new InvalidOperationException("Service stream already configured.");
            if (source == null) throw new ArgumentNullException(nameof(source));
            FluidStreamProfile profile = definitionId switch
            {
                "item.motor-oil" => FluidStreamProfile.MotorOil,
                "item.coolant" => FluidStreamProfile.Coolant,
                "item.brake-fluid" => FluidStreamProfile.BrakeFluid,
                _ => throw new ArgumentException("Unsupported service stream."),
            };
            controller = source;
            var visual = new GameObject("Service fluid gravity stream") { hideFlags = HideFlags.DontSave };
            visual.transform.SetParent(transform, false);
            stream = visual.AddComponent<ProceduralFluidStreamPresenter>();
            stream.Configure(profile, Vector3.zero, Vector3.forward, ~0);
        }
        private void LateUpdate()
        {
            if (stream == null || controller == null) return;
            stream.transform.SetPositionAndRotation(controller.OutletWorldPosition,
                Quaternion.FromToRotation(Vector3.forward, controller.OutletWorldDirection));
            stream.SetIntensity(controller.Flow01);
            stream.SetFlowing(controller.IsPouring && Time.timeScale > 0f, clearExisting: Time.timeScale <= 0f);
        }
        private void OnDisable() { if (stream != null) stream.SetFlowing(false, clearExisting: true); }
        private void OnDestroy() { if (stream != null) Destroy(stream.gameObject); }
    }
}
