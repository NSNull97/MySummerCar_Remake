using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyMechanicalConditionSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public float conditionPercent = 100f;
        public bool broken;
        public bool IsValid => schemaVersion == CurrentSchemaVersion && float.IsFinite(conditionPercent) &&
            conditionPercent >= 0f && conditionPercent <= 100f && (conditionPercent > 0f || broken);
        public AssemblyMechanicalConditionSaveDto Clone() => (AssemblyMechanicalConditionSaveDto)MemberwiseClone();
    }

    /// <summary>
    /// Wear on a fixed-roster part's existing stable identity. No reset on install,
    /// activation or presentation replacement. Purchased parts retain item ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyMechanicalConditionState : MonoBehaviour, IAssemblyItemCondition, IAssemblyWearSink
    {
        [SerializeField] private PartInstance part;
        // Healthy-engine substep losses are smaller than a float ULP near 100.
        // Accumulate in double so low wear does not round away forever.
        private double conditionPercent = 100d;
        private bool broken;
        public PartInstance Part => part;
        public float ConditionPercent => (float)conditionPercent;
        public bool IsBroken => broken;

        public void Configure(PartInstance owner)
        {
            if (owner == null || owner.gameObject != gameObject || owner.Definition == null)
                throw new ArgumentException("Mechanical condition requires its own explicit part wrapper.", nameof(owner));
            foreach (IAssemblyItemCondition existing in GetComponents<IAssemblyItemCondition>())
                if (!ReferenceEquals(existing, this))
                    throw new ArgumentException("An item-owned condition must not acquire a second authority.", nameof(owner));
            part = owner;
        }

        public bool TryApplyWear(float loss)
        {
            if (part == null || !float.IsFinite(loss) || loss <= 0f || broken) return false;
            conditionPercent = Math.Max(0d, conditionPercent - loss);
            if (conditionPercent <= 0f) broken = true;
            return true;
        }

        public AssemblyMechanicalConditionSaveDto CaptureSaveData() => new()
        { conditionPercent = (float)conditionPercent, broken = broken };

        public void RestoreValidated(AssemblyMechanicalConditionSaveDto dto)
        {
            if (dto != null && !dto.IsValid) throw new ArgumentException("Invalid mechanical condition.", nameof(dto));
            // Missing old state preserves the former unworn simulation behavior.
            conditionPercent = dto?.conditionPercent ?? 100f;
            broken = dto?.broken ?? false;
        }
    }
}
