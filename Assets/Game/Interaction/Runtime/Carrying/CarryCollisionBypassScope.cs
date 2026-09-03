using System;
using UnityEngine;

namespace MSC.Interaction.Carrying
{
    /// <summary>
    /// Defines an authored set of owner colliders that a nested loose object
    /// may pass through only while it is carried. World and ground collision
    /// remain enabled, and every ignored pair is restored on release.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarryCollisionBypassScope : MonoBehaviour
    {
        [SerializeField]
        private Collider[] scopeColliders = Array.Empty<Collider>();

        public Collider[] ScopeColliders => scopeColliders;

        public void Configure(Collider[] colliders)
        {
            scopeColliders = colliders ?? Array.Empty<Collider>();
        }
    }
}
