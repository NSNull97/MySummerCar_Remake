using System;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Process-lifetime owner for explicitly constructed services. This component is not a global service locator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        private GameServiceBindings serviceBindings;

        public bool IsInitialized => serviceBindings != null;

        public void Initialize(GameServiceBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("The game composition root has already been initialized.");
            }

            serviceBindings = bindings;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
