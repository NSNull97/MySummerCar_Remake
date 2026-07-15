using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Mount Point Definition")]
    public sealed class MountPointDefinition : ScriptableObject
    {
        [SerializeField]
        private string definitionId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private string socketType = string.Empty;

        [SerializeField]
        private string ownerPartDefinitionId = string.Empty;

        [SerializeField]
        private string[] acceptedPartDefinitionIds = Array.Empty<string>();

        [SerializeField]
        private MountConstraint constraint = new MountConstraint(0.35f, 40f, 1.25f, 0.08f);

        [SerializeField, Min(0f)]
        [Tooltip("Observed donor overlap marker radius where known. This is provenance, not necessarily remake UX tolerance.")]
        private float referenceCandidateRadiusMeters;

        [SerializeField]
        private FastenerDefinition[] fasteners = Array.Empty<FastenerDefinition>();

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public string SocketType => socketType;
        public string OwnerPartDefinitionId => ownerPartDefinitionId;
        public string[] AcceptedPartDefinitionIds => acceptedPartDefinitionIds;
        public MountConstraint Constraint => constraint;
        public float ReferenceCandidateRadiusMeters => referenceCandidateRadiusMeters;
        public FastenerDefinition[] Fasteners => fasteners;

        public bool AcceptsPart(string partDefinitionId)
        {
            for (int i = 0; i < acceptedPartDefinitionIds.Length; i++)
            {
                if (string.Equals(
                        acceptedPartDefinitionIds[i],
                        partDefinitionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(
            string id,
            string name,
            string type,
            string ownerDefinitionId,
            string[] acceptedPartIds,
            MountConstraint mountConstraint,
            float referenceRadiusMeters,
            FastenerDefinition[] fastenerDefinitions)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            socketType = type ?? string.Empty;
            ownerPartDefinitionId = ownerDefinitionId ?? string.Empty;
            acceptedPartDefinitionIds = acceptedPartIds ?? Array.Empty<string>();
            constraint = mountConstraint;
            referenceCandidateRadiusMeters = Mathf.Max(0f, referenceRadiusMeters);
            fasteners = fastenerDefinitions ?? Array.Empty<FastenerDefinition>();
        }
    }
}
