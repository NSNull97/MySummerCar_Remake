using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaWiperSwitchInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        IInteractionDisplayTarget
    {
        [SerializeField] private SatsumaWiperController wipers;

        public string InteractionDisplayName => "Переключатель дворников";

        public string InteractionPrompt => wipers == null || !wipers.CanOperateSwitch
            ? "ДВОРНИКИ НЕДОСТУПНЫ"
            : wipers.Mode switch
            {
                SatsumaWiperMode.Off => "ДВОРНИКИ: МЕДЛЕННО",
                SatsumaWiperMode.Slow => "ДВОРНИКИ: БЫСТРО",
                _ => "ВЫКЛЮЧИТЬ ДВОРНИКИ",
            };

        public void Configure(SatsumaWiperController configuredWipers)
        {
            wipers = configuredWipers != null
                ? configuredWipers
                : throw new ArgumentNullException(nameof(configuredWipers));
        }

        public bool CanInteract(in InteractionContext context) =>
            isActiveAndEnabled && wipers != null && wipers.CanOperateSwitch;

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(context))
            {
                wipers.CycleMode();
            }
        }
    }
}
