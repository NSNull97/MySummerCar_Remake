using UnityEngine;

namespace MSC.Interaction
{
    /// <summary>
    /// Immutable intent context supplied to explicit interaction capabilities.
    /// </summary>
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, Vector3 origin, Vector3 direction)
        {
            Interactor = interactor;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }

        public GameObject Interactor { get; }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }
    }
}
