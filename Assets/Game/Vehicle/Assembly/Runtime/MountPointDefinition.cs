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

        [SerializeField]
        private FastenerGroupDefinition fastenerGroup;

        [SerializeField]
        private string[] requiredOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] requiredAnyOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] blockedWhileOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Occupied mounts that must be cleared before the installed part can be removed.")]
        private string[] removalBlockedWhileOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] requiredBoltedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] requiredAnyBoltedMountIds = Array.Empty<string>();

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public string SocketType => socketType;
        public string OwnerPartDefinitionId => ownerPartDefinitionId;
        public string[] AcceptedPartDefinitionIds => acceptedPartDefinitionIds;
        public MountConstraint Constraint => constraint;
        public float ReferenceCandidateRadiusMeters => referenceCandidateRadiusMeters;
        public FastenerDefinition[] Fasteners => fasteners;
        public FastenerGroupDefinition FastenerGroup => fastenerGroup;
        public string[] RequiredOccupiedMountIds =>
            requiredOccupiedMountIds ?? Array.Empty<string>();
        public string[] RequiredAnyOccupiedMountIds =>
            requiredAnyOccupiedMountIds ?? Array.Empty<string>();
        public string[] BlockedWhileOccupiedMountIds =>
            blockedWhileOccupiedMountIds ?? Array.Empty<string>();
        public string[] RemovalBlockedWhileOccupiedMountIds =>
            removalBlockedWhileOccupiedMountIds ?? Array.Empty<string>();
        public string[] RequiredBoltedMountIds =>
            requiredBoltedMountIds ?? Array.Empty<string>();
        public string[] RequiredAnyBoltedMountIds =>
            requiredAnyBoltedMountIds ?? Array.Empty<string>();

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

        public void ConfigureFastenerGroup(
            FastenerGroupDefinition configuredGroup)
        {
            fastenerGroup = configuredGroup;
        }

        public void ConfigureSequence(
            string[] requiredMountIds,
            string[] requiredAnyMountIds,
            string[] blockedMountIds)
        {
            requiredOccupiedMountIds = requiredMountIds ?? Array.Empty<string>();
            requiredAnyOccupiedMountIds = requiredAnyMountIds ?? Array.Empty<string>();
            blockedWhileOccupiedMountIds = blockedMountIds ?? Array.Empty<string>();
        }

        public void ConfigureBoltedSequence(
            string[] requiredMountIds,
            string[] requiredAnyMountIds)
        {
            requiredBoltedMountIds = requiredMountIds ?? Array.Empty<string>();
            requiredAnyBoltedMountIds = requiredAnyMountIds ?? Array.Empty<string>();
        }

        public void ConfigureRemovalBlockers(string[] blockedMountIds)
        {
            removalBlockedWhileOccupiedMountIds =
                blockedMountIds ?? Array.Empty<string>();
        }
    }
}
