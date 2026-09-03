using System;
using MSC.Characters;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.NPC
{
    public delegate void NpcDialogueFeedbackHandler(
        NpcDialogueLine line,
        Vector3 worldPosition);

    /// <summary>
    /// Thin project-owned interaction adapter. Dialogue selection and state
    /// remain in the NPC domain; the callback is presentation/audio feedback
    /// supplied explicitly by the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcDialogueInteractionTarget : MonoBehaviour,
        IContextInteractionTarget
    {
        private CharacterInstance character;
        private NpcDialogueDefinition definition;
        private NpcDialogueRuntime dialogueRuntime;
        private Func<double> elapsedGameSeconds;
        private NpcDialogueFeedbackHandler feedback;

        public string InteractionPrompt =>
            "\u041f\u043e\u0433\u043e\u0432\u043e\u0440\u0438\u0442\u044c";

        public void Configure(
            CharacterInstance configuredCharacter,
            NpcDialogueDefinition configuredDefinition,
            NpcDialogueRuntime configuredRuntime,
            Func<double> configuredElapsedGameSeconds,
            NpcDialogueFeedbackHandler configuredFeedback)
        {
            character = configuredCharacter ??
                throw new ArgumentNullException(nameof(configuredCharacter));
            definition = configuredDefinition ??
                throw new ArgumentNullException(nameof(configuredDefinition));
            dialogueRuntime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            elapsedGameSeconds = configuredElapsedGameSeconds ??
                throw new ArgumentNullException(
                    nameof(configuredElapsedGameSeconds));
            feedback = configuredFeedback;
        }

        public bool CanInteract(in InteractionContext context) =>
            character != null &&
            definition != null &&
            dialogueRuntime != null &&
            elapsedGameSeconds != null &&
            character.ActivityState != CharacterActivityState.Hidden &&
            character.ActivityState != CharacterActivityState.Disabled;

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            double now = elapsedGameSeconds();
            if (!dialogueRuntime.TrySelectLine(
                    definition,
                    character,
                    now,
                    out NpcDialogueLine line))
            {
                return;
            }

            dialogueRuntime.CommitLine(line, character, now);
            feedback?.Invoke(line, transform.position);
        }
    }
}
