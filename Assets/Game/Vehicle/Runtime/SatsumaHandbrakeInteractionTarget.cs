using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaHandbrakeInteractionTarget : MonoBehaviour,
        IContinuousContextInteractionTarget,
        IInteractionDisplayTarget
    {
        [SerializeField] private SatsumaHandbrakeController handbrake;
        [SerializeField] private Collider interactionCollider;

        private bool held;

        public string InteractionDisplayName => "Ручной тормоз";
        public string InteractionPrompt =>
            "Удерживайте ЛКМ — поднять ручник / ПКМ — опустить ручник";
        public bool UsesDirectionalHold => handbrake != null && handbrake.CanOperate;

        public void Configure(
            SatsumaHandbrakeController configuredHandbrake,
            Collider authoredInteractionCollider = null)
        {
            EndContinuousInteraction();
            handbrake = configuredHandbrake != null
                ? configuredHandbrake
                : throw new ArgumentNullException(nameof(configuredHandbrake));
            interactionCollider = authoredInteractionCollider;
            RefreshCollider();
        }

        public bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!isActiveAndEnabled || handbrake == null || !handbrake.CanOperate)
            {
                return false;
            }

            return direction switch
            {
                ContinuousContextInteractionDirection.Primary =>
                    handbrake.PositionDegrees < SatsumaHandbrakeController.MaximumPositionDegrees,
                ContinuousContextInteractionDirection.Secondary => handbrake.PositionDegrees > 0f,
                _ => false,
            };
        }

        public void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            EndContinuousInteraction();
            if (CanBeginContinuousInteraction(context, direction))
            {
                held = handbrake.TrySetHeldDirection(
                    direction == ContinuousContextInteractionDirection.Primary ? 1 : -1);
            }
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!held || !isActiveAndEnabled || handbrake == null || !handbrake.CanOperate ||
                !handbrake.IsHeld)
            {
                EndContinuousInteraction();
                return false;
            }

            // The controller owns simulation. Continuing the intent must not
            // advance the lever a second time in the same player frame.
            return true;
        }

        public void EndContinuousInteraction()
        {
            held = false;
            handbrake?.ReleaseHold();
        }

        private void Update() => RefreshCollider();

        private void OnEnable() => RefreshCollider();

        private void OnDisable()
        {
            EndContinuousInteraction();
            RefreshCollider();
        }

        private void RefreshCollider()
        {
            if (interactionCollider != null)
            {
                bool available = isActiveAndEnabled && handbrake != null && handbrake.CanOperate;
                if (interactionCollider.enabled != available)
                {
                    // This is a dedicated interaction shape, never the part's
                    // physical collider: loose-part pickup must remain reachable.
                    interactionCollider.enabled = available;
                }
            }
        }
    }
}
