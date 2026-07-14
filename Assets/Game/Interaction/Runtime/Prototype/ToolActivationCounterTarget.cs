using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Prototype
{
    [DisallowMultipleComponent]
    public sealed class ToolActivationCounterTarget : MonoBehaviour, IToolActivationTarget
    {
        [SerializeField]
        private string toolPrompt = "Использовать инструмент";

        [SerializeField]
        private bool activationEnabled = true;

        private int activationCount;

        public string ToolPrompt => toolPrompt;

        public int ActivationCount => activationCount;

        public bool CanActivateTool(in InteractionContext context)
        {
            return activationEnabled && enabled && gameObject.activeInHierarchy;
        }

        public void ActivateTool(in InteractionContext context)
        {
            if (CanActivateTool(context))
            {
                activationCount++;
            }
        }
    }
}
