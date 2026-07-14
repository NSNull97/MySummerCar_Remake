using System;
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
    }
}
