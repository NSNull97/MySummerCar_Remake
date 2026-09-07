using UnityEngine;

namespace MSC.Interaction.Carrying
{
    /// <summary>Per-player, session-only test switch. Never changes physical mass or gravity.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyCarryDebugOverride : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool ignoreAssemblyMassLimit;
#endif
        public bool IgnoreAssemblyMassLimit
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ignoreAssemblyMassLimit;
#else
                return false;
#endif
            }
        }

        public void SetIgnoreAssemblyMassLimit(bool enabled)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ignoreAssemblyMassLimit = enabled;
#endif
        }
    }
}
