using System;
using MSC.Player;
using UnityEngine;

namespace MSC.Needs
{
    /// <summary>
    /// Converts explicit player life-action input into project-owned needs
    /// operations. Presentation observes the results and never owns state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLifeActionController : MonoBehaviour
    {
        private PlayerNeedsRuntime needs;
        private PlayerInputRouter input;
        private bool initialized;

        public void Initialize(
            PlayerNeedsRuntime configuredNeeds,
            PlayerInputRouter configuredInput)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Player life-action controller is already initialized.");
            }

            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            input = configuredInput ??
                throw new ArgumentNullException(nameof(configuredInput));
            input.UrinationRequested += HandleUrinationRequested;
            initialized = true;
        }

        private void HandleUrinationRequested()
        {
            needs.TryUrinate(out _);
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.UrinationRequested -= HandleUrinationRequested;
            }
        }
    }
}
