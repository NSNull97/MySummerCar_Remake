using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Donor piston Assemble/Remove toggles its conrod-cap presentation, not a separate part.</summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaPistonCapPresenter : MonoBehaviour
    {
        [SerializeField] private PartInstance piston;
        [SerializeField] private GameObject capPresentation;

        public PartInstance Piston => piston;
        public GameObject CapPresentation => capPresentation;

        public void Configure(PartInstance configuredPiston, GameObject configuredCap)
        {
            if (configuredPiston == null || configuredCap == null ||
                configuredCap == configuredPiston.gameObject ||
                !configuredCap.transform.IsChildOf(configuredPiston.transform) ||
                !transform.IsChildOf(configuredPiston.transform) ||
                transform.IsChildOf(configuredCap.transform))
            {
                throw new ArgumentException("Piston cap must be a separate descendant; its presenter must remain enabled.");
            }
            piston = configuredPiston;
            capPresentation = configuredCap;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (piston == null || capPresentation == null) return;
            // Installed on a loose floor engine also counts. Tightening and chassis
            // attachment do not govern this donor presentation switch.
            bool visible = piston.IsInstalled && !piston.IsAssemblyRoot;
            if (capPresentation.activeSelf != visible) capPresentation.SetActive(visible);
        }

        private void OnEnable() => RefreshPresentation();
        private void LateUpdate() => RefreshPresentation();
    }
}
