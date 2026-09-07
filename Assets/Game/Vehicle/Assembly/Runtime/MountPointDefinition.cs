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
        [Tooltip("Occupied mounts required only while installing this part. This does not create removal or structural-collapse dependencies.")]
        private string[] installationRequiredOccupiedMountIds =
            Array.Empty<string>();

        [SerializeField]
        private string[] requiredAnyOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] blockedWhileOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Occupied mounts that must be cleared before the installed part can be removed.")]
        private string[] removalBlockedWhileOccupiedMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Other mount groups whose Bolted latch prevents manual removal. Installation and forced detachment are independent.")]
        private string[] removalBlockedWhileBoltedMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Explicit exceptions to inferred reverse-install removal blockers only. Does not bypass owned children, explicit removal blockers or collapse dependencies.")]
        private string[] removalIgnoredDependentMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Explicit owned child sockets retained inside this part when it is manually removed as a compound assembly. Does not bypass explicit blockers or fastening.")]
        private string[] removalRetainedChildMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] requiredBoltedMountIds = Array.Empty<string>();

        [SerializeField]
        private string[] requiredAnyBoltedMountIds = Array.Empty<string>();

        [SerializeField]
        [Tooltip("Check this support after installation commits. If unbolted, the support and its dependent assembly fall together, including the incoming part.")]
        private string installAttemptBoltedSupportMountId = string.Empty;

        [SerializeField]
        [Tooltip("Installation/preview blockers, independent of structural retention and manual removal.")]
        private string[] installationBlockedWhileBoltedMountIds = Array.Empty<string>();

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
        public string[] InstallationRequiredOccupiedMountIds =>
            installationRequiredOccupiedMountIds ?? Array.Empty<string>();
        public string[] RequiredAnyOccupiedMountIds =>
            requiredAnyOccupiedMountIds ?? Array.Empty<string>();
        public string[] BlockedWhileOccupiedMountIds =>
            blockedWhileOccupiedMountIds ?? Array.Empty<string>();
        public string[] RemovalBlockedWhileOccupiedMountIds =>
            removalBlockedWhileOccupiedMountIds ?? Array.Empty<string>();
        public string[] RemovalBlockedWhileBoltedMountIds =>
            removalBlockedWhileBoltedMountIds ?? Array.Empty<string>();
        public string[] RemovalIgnoredDependentMountIds =>
            removalIgnoredDependentMountIds ?? Array.Empty<string>();
        public string[] RemovalRetainedChildMountIds =>
            removalRetainedChildMountIds ?? Array.Empty<string>();
        public string[] RequiredBoltedMountIds =>
            requiredBoltedMountIds ?? Array.Empty<string>();
        public string[] RequiredAnyBoltedMountIds =>
            requiredAnyBoltedMountIds ?? Array.Empty<string>();
        public string InstallAttemptBoltedSupportMountId =>
            installAttemptBoltedSupportMountId ?? string.Empty;
        public string[] InstallationBlockedWhileBoltedMountIds =>
            installationBlockedWhileBoltedMountIds ?? Array.Empty<string>();

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

        public void ConfigureInstallationOccupancy(string[] requiredMountIds)
        {
            installationRequiredOccupiedMountIds =
                requiredMountIds ?? Array.Empty<string>();
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

        public void ConfigureInstallationChecks(
            string boltedSupportMountId,
            string[] blockedWhileBoltedMountIds)
        {
            installAttemptBoltedSupportMountId = boltedSupportMountId ?? string.Empty;
            installationBlockedWhileBoltedMountIds =
                blockedWhileBoltedMountIds ?? Array.Empty<string>();
        }

        public void ConfigureRemovalChecks(
            string[] blockedWhileBoltedMountIds,
            string[] ignoredDependentMountIds)
        {
            removalBlockedWhileBoltedMountIds =
                blockedWhileBoltedMountIds ?? Array.Empty<string>();
            removalIgnoredDependentMountIds =
                ignoredDependentMountIds ?? Array.Empty<string>();
        }

        public void ConfigureRetainedRemovalChildren(string[] retainedChildMountIds)
        {
            removalRetainedChildMountIds = retainedChildMountIds ?? Array.Empty<string>();
        }
    }
}
