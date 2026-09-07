using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaIgnitionInteractionTarget : MonoBehaviour,
        IContinuousContextInteractionTarget,
        IInteractionDisplayTarget
    {
        [SerializeField] private SatsumaIgnitionController ignition;
        [SerializeField] private Collider interactionCollider;

        private bool ownsHold;

        public string InteractionDisplayName => "Замок зажигания";
        public string InteractionPrompt => ignition == null
            ? "ЗАЖИГАНИЕ НЕДОСТУПНО"
            : ignition.State switch
            {
                SatsumaIgnitionState.Off => "ПОВЕРНУТЬ КЛЮЧ",
                SatsumaIgnitionState.Starting => "ОТПУСТИТЬ КЛЮЧ",
                _ when ignition.HasAttemptedStart => "ВЫКЛЮЧИТЬ ЗАЖИГАНИЕ",
                _ => "НАЖАТЬ — ВЫКЛЮЧИТЬ / УДЕРЖИВАТЬ — ЗАПУСК",
            };
        public bool UsesDirectionalHold => true;

        public void Configure(
            SatsumaIgnitionController configuredIgnition,
            Collider authoredInteractionCollider = null)
        {
            EndContinuousInteraction();
            ignition = configuredIgnition != null
                ? configuredIgnition
                : throw new ArgumentNullException(nameof(configuredIgnition));
            interactionCollider = authoredInteractionCollider;
            RefreshCollider();
        }

        public bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction) =>
            isActiveAndEnabled &&
            direction == ContinuousContextInteractionDirection.Primary &&
            ignition != null && ignition.CanOperate;

        public void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (ownsHold && ignition != null && ignition.IsHeld)
            {
                return;
            }

            // Restore or capability loss may cancel the controller before the
            // interaction owner receives its final Continue/End callback.
            ownsHold = false;
            if (CanBeginContinuousInteraction(context, direction))
            {
                ownsHold = ignition.TryBeginPrimaryHold(
                    Time.realtimeSinceStartupAsDouble);
            }
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!ownsHold || !isActiveAndEnabled || ignition == null)
            {
                EndContinuousInteraction();
                return false;
            }

            if (!ignition.IsHeld)
            {
                ownsHold = false;
                return false;
            }

            if (ignition.ContinuePrimaryHold(Time.realtimeSinceStartupAsDouble))
            {
                return true;
            }

            EndContinuousInteraction();
            return false;
        }

        public void EndContinuousInteraction()
        {
            ownsHold = false;
            ignition?.ReleasePrimaryHold(Time.realtimeSinceStartupAsDouble);
        }

        private void Update()
        {
            ignition?.RefreshAvailability();
            RefreshCollider();
        }

        private void OnEnable()
        {
            RefreshCollider();
        }

        private void OnDisable()
        {
            ownsHold = false;
            ignition?.CancelHold();
            RefreshCollider();
        }

        private void RefreshCollider()
        {
            if (interactionCollider != null)
            {
                bool available = isActiveAndEnabled && ignition != null &&
                    ignition.CanOperate;
                if (interactionCollider.enabled != available)
                {
                    interactionCollider.enabled = available;
                }
            }
        }
    }
}
