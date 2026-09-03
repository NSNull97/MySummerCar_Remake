using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Needs
{
    /// <summary>
    /// Explicit project-owned interaction capability for one domestic action.
    /// Its stable action ID is independent from donor hierarchy and object names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLifeActionInteractionTarget :
        MonoBehaviour,
        IContextInteractionTarget
    {
        [SerializeField] private string stableActionId = "home.bed.player";
        [SerializeField] private string prompt = "Спать";
        [SerializeField] private PlayerLifeActionKind actionKind =
            PlayerLifeActionKind.Sleep;
        [SerializeField] private Vector3 sleepCameraWorldPosition;
        [SerializeField] private Quaternion sleepPlayerWorldRotation =
            Quaternion.identity;

        private PlayerNeedsRuntime needs;
        private FirstPersonLifeActionPresenter presenter;

        public string StableActionId => stableActionId;
        public string InteractionPrompt => prompt;

        public void Configure(
            string configuredStableActionId,
            string configuredPrompt,
            PlayerLifeActionKind configuredActionKind,
            PlayerNeedsRuntime configuredNeeds,
            FirstPersonLifeActionPresenter configuredPresenter,
            Vector3 configuredSleepCameraWorldPosition,
            Quaternion configuredSleepPlayerWorldRotation)
        {
            stableActionId = string.IsNullOrWhiteSpace(configuredStableActionId)
                ? throw new ArgumentException(
                    "Life-action target requires a stable project-owned ID.",
                    nameof(configuredStableActionId))
                : configuredStableActionId;
            prompt = string.IsNullOrWhiteSpace(configuredPrompt)
                ? throw new ArgumentException(
                    "Life-action target requires a prompt.",
                    nameof(configuredPrompt))
                : configuredPrompt;
            actionKind = configuredActionKind;
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            presenter = configuredPresenter ??
                throw new ArgumentNullException(nameof(configuredPresenter));
            sleepCameraWorldPosition = configuredSleepCameraWorldPosition;
            sleepPlayerWorldRotation =
                configuredSleepPlayerWorldRotation.normalized;
        }

        public bool CanInteract(in InteractionContext context) =>
            needs != null &&
            needs.IsInitialized &&
            (actionKind != PlayerLifeActionKind.Sleep || presenter != null);

        public void Interact(in InteractionContext context)
        {
            if (actionKind == PlayerLifeActionKind.Sleep)
            {
                presenter.QueueSleepPose(
                    sleepCameraWorldPosition,
                    sleepPlayerWorldRotation);
                if (!needs.TrySleep(out _))
                {
                    presenter.CancelQueuedSleepPose();
                }
            }
        }
    }
}
