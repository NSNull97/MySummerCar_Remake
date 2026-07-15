using UnityEngine;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    public sealed class InteractionDebugOverlay : MonoBehaviour
    {
        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField]
        private bool showDebugInformation = true;

        public void Configure(PlayerInteractionController controller)
        {
            interactionController = controller;
        }

        private void OnGUI()
        {
            if (!showDebugInformation || interactionController == null)
            {
                return;
            }

            string prompt = interactionController.CurrentPrompt;
            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height * 0.5f + 20f, 280f, 24f), prompt);
            }

            string heldId = interactionController.HeldStableId;
            string debugText = string.IsNullOrEmpty(heldId)
                ? $"Candidate: {interactionController.HasCandidate} | Held: none"
                : $"Candidate: {interactionController.HasCandidate} | Held stable ID: {heldId}";
            GUI.Label(new Rect(12f, 12f, 640f, 24f), debugText);
        }
    }
}
