using System;
using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Serializable project-owned descriptor. Vendor asset references belong to the
    /// integration assembly and must not be added to this DTO.
    /// </summary>
    [Serializable]
    public sealed class EnvironmentPresentationBindingDefinition
    {
        [SerializeField] private string bindingId = string.Empty;
        [SerializeField] private EnvironmentPresentationPresetKind presetKind;
        [SerializeField] private EnvironmentPresentationCapabilities requiredCapabilities;

        public EnvironmentPresentationBindingDefinition()
        {
        }

        public EnvironmentPresentationBindingDefinition(
            string bindingId,
            EnvironmentPresentationPresetKind presetKind,
            EnvironmentPresentationCapabilities requiredCapabilities)
        {
            this.bindingId = bindingId ?? string.Empty;
            this.presetKind = presetKind;
            this.requiredCapabilities = requiredCapabilities;
        }

        public string BindingId => bindingId ?? string.Empty;

        public EnvironmentPresentationPresetKind PresetKind => presetKind;

        public EnvironmentPresentationCapabilities RequiredCapabilities => requiredCapabilities;

        public bool TryGetBindingId(out EnvironmentBindingId parsedBindingId)
        {
            return EnvironmentBindingId.TryParse(BindingId, out parsedBindingId);
        }
    }
}
