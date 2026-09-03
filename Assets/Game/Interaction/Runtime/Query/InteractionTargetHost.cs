using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Interaction.Query
{
    /// <summary>
    /// Explicit capability registry for one target. It avoids object-name dispatch and scene-wide lookups.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionTargetHost : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour[] capabilityComponents = Array.Empty<MonoBehaviour>();

        [SerializeField]
        private Renderer[] outlineRenderers = Array.Empty<Renderer>();

        [SerializeField]
        private int selectionPriority;

        public IReadOnlyList<Renderer> OutlineRenderers => outlineRenderers;

        /// <summary>
        /// Resolves overlapping authored targets without allowing arbitrary
        /// world geometry to become transparent. Nested controls such as a
        /// toolbox wrench or an exact vehicle mount may outrank their parent
        /// shell inside the small occlusion tolerance owned by the ray query.
        /// </summary>
        public int SelectionPriority => selectionPriority;

        public bool HasOutlineRendererOverride
        {
            get
            {
                for (int index = 0; index < outlineRenderers.Length; index++)
                {
                    if (outlineRenderers[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasCapabilities
        {
            get
            {
                for (int i = 0; i < capabilityComponents.Length; i++)
                {
                    if (capabilityComponents[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
            for (int i = 0; i < capabilityComponents.Length; i++)
            {
                if (capabilityComponents[i] is TCapability candidate)
                {
                    capability = candidate;
                    return true;
                }
            }

            capability = null;
            return false;
        }

        public void Configure(params MonoBehaviour[] capabilities)
        {
            capabilityComponents = capabilities ?? Array.Empty<MonoBehaviour>();
        }

        public void ConfigureSelectionPriority(int priority)
        {
            selectionPriority = priority;
        }

        public void AddCapability(MonoBehaviour capability)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            for (int index = 0; index < capabilityComponents.Length; index++)
            {
                if (capabilityComponents[index] == capability)
                {
                    return;
                }
            }

            int previousLength = capabilityComponents.Length;
            Array.Resize(ref capabilityComponents, previousLength + 1);
            capabilityComponents[previousLength] = capability;
        }

        /// <summary>
        /// Registers a context-specific capability ahead of generic item
        /// capabilities while preserving every existing binding. This is used
        /// when one physical item also exposes a secondary action, such as
        /// opening a placed catalog without replacing its pickup capability.
        /// </summary>
        public void AddCapabilityFirst(MonoBehaviour capability)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            int existingIndex = Array.IndexOf(
                capabilityComponents,
                capability);
            if (existingIndex == 0)
            {
                return;
            }

            if (existingIndex > 0)
            {
                Array.Copy(
                    capabilityComponents,
                    0,
                    capabilityComponents,
                    1,
                    existingIndex);
                capabilityComponents[0] = capability;
                return;
            }

            int previousLength = capabilityComponents.Length;
            Array.Resize(ref capabilityComponents, previousLength + 1);
            Array.Copy(
                capabilityComponents,
                0,
                capabilityComponents,
                1,
                previousLength);
            capabilityComponents[0] = capability;
        }

        /// <summary>
        /// Restricts hover presentation to authored visual parts such as a
        /// door or vehicle handle. Gameplay capability lookup is unaffected.
        /// </summary>
        public void ConfigureOutlineRenderers(params Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                outlineRenderers = Array.Empty<Renderer>();
                return;
            }

            var unique = new List<Renderer>(renderers.Length);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null && !unique.Contains(renderer))
                {
                    unique.Add(renderer);
                }
            }

            outlineRenderers = unique.ToArray();
        }
    }
}
