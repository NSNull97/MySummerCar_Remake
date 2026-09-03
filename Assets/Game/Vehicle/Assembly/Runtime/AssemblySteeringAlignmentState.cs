using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblySteeringAlignmentSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public float alignmentDegrees;

        public bool IsValid => schemaVersion == CurrentSchemaVersion &&
            float.IsFinite(alignmentDegrees) &&
            alignmentDegrees >= -AssemblySteeringAlignmentState.LimitDegrees &&
            alignmentDegrees <= AssemblySteeringAlignmentState.LimitDegrees;
    }

    /// <summary>
    /// Per-part toe setting. The 14 mm adjuster is not a staged fastener.
    /// Frozen rod Data randomizes loose rods on load, not on ordinary removal.
    /// Physics consumes Revision to apply each adjustment as a one-shot yaw snap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySteeringAlignmentState : MonoBehaviour
    {
        public const float LimitDegrees = 6f;
        public const float StepDegrees = 0.1f;

        [SerializeField] private PartInstance part;
        private float alignmentDegrees;
        private bool initialized;
        private int revision;

        public PartInstance Part => part;
        public int Revision
        {
            get
            {
                EnsureInitialized();
                return revision;
            }
        }
        public float AlignmentDegrees
        {
            get
            {
                EnsureInitialized();
                return alignmentDegrees;
            }
        }

        public void Configure(PartInstance configuredPart)
        {
            if (configuredPart == null)
            {
                throw new ArgumentNullException(nameof(configuredPart));
            }

            part = configuredPart;
        }

        public bool TryAdjust(bool tighten)
        {
            if (part == null || !part.IsInstalled)
            {
                return false;
            }

            EnsureInitialized();
            alignmentDegrees = Mathf.Clamp(
                alignmentDegrees + (tighten ? -StepDegrees : StepDegrees),
                -LimitDegrees, LimitDegrees);
            // At a limit the donor still turns the nut and reapplies hub yaw.
            revision++;
            return true;
        }

        public AssemblySteeringAlignmentSaveDto CaptureSaveData() =>
            new AssemblySteeringAlignmentSaveDto
            {
                alignmentDegrees = AlignmentDegrees,
            };

        public void RestoreValidated(AssemblySteeringAlignmentSaveDto data)
        {
            if (data != null && !data.IsValid)
            {
                throw new ArgumentException("Invalid steering alignment.", nameof(data));
            }

            // Old installed saves had neutral toe and no field: preserve that
            // pose. Loose load follows the donor's RandomFloat branch, including
            // when a saved value exists. Remove/reinstall never calls this.
            alignmentDegrees = part != null && part.IsInstalled
                ? data?.alignmentDegrees ?? 0f
                : UnityEngine.Random.Range(-LimitDegrees, LimitDegrees);
            initialized = true;
            revision++;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            alignmentDegrees = UnityEngine.Random.Range(-LimitDegrees, LimitDegrees);
            initialized = true;
            revision++;
        }

        private void Start()
        {
            EnsureInitialized();
        }
    }
}
