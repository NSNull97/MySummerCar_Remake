using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Prototype
{
    [DisallowMultipleComponent]
    public sealed class ContextToggleTarget : MonoBehaviour, IContextInteractionTarget
    {
        [SerializeField]
        private string enablePrompt = "Включить";

        [SerializeField]
        private string disablePrompt = "Выключить";

        private bool isActive;

        public string InteractionPrompt => isActive ? disablePrompt : enablePrompt;

        public bool IsActive => isActive;

        public bool CanInteract(in InteractionContext context)
        {
            return enabled && gameObject.activeInHierarchy;
        }

        public void Interact(in InteractionContext context)
        {
            isActive = !isActive;
        }
    }
}
