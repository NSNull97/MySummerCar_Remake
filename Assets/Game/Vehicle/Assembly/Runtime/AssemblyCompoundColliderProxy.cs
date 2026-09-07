using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Runtime-only compound contact, never an original part collider or saved entity.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyCompoundColliderProxy : MonoBehaviour
    {
        public PartInstance SourcePart { get; private set; }
        public Collider SourceShape { get; private set; }
        public PartInstance LooseOwner { get; private set; }

        internal void Configure(PartInstance sourcePart, Collider sourceShape)
        {
            SourcePart = sourcePart;
            SourceShape = sourceShape;
        }

        internal void SetLooseOwner(PartInstance owner) => LooseOwner = owner;
    }
}
