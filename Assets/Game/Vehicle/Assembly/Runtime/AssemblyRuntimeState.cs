using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum PartLifecycleState
    {
        Loose = 0,
        Installed = 1,
        AssemblyRoot = 2
    }

    public enum FastenerState
    {
        Absent = 0,
        Inserted = 1,
        Loose = 2,
        PartiallyTightened = 3,
        Tightened = 4
    }

    [Serializable]
    public sealed class PartRuntimeState
    {
        [SerializeField]
        private string stableEntityId = string.Empty;

        [SerializeField]
        private string partDefinitionId = string.Empty;

        [SerializeField]
        private PartLifecycleState lifecycleState;

        [SerializeField]
        private string installedMountId = string.Empty;

        [SerializeField]
        private Vector3 looseWorldPosition;

        [SerializeField]
        private Quaternion looseWorldRotation = Quaternion.identity;

        public string StableEntityId => stableEntityId;

        public string PartDefinitionId => partDefinitionId;

        public PartLifecycleState LifecycleState => lifecycleState;

        public string InstalledMountId => installedMountId;

        public Vector3 LooseWorldPosition => looseWorldPosition;

        public Quaternion LooseWorldRotation => looseWorldRotation;

        public bool IsInstalled => lifecycleState != PartLifecycleState.Loose;

        public void SetIdentity(string entityId, string definitionId)
        {
            stableEntityId = entityId ?? string.Empty;
            partDefinitionId = definitionId ?? string.Empty;
        }

        public void SetLoose(Vector3 position, Quaternion rotation)
        {
            lifecycleState = PartLifecycleState.Loose;
            installedMountId = string.Empty;
            looseWorldPosition = position;
            looseWorldRotation = rotation;
        }

        public void SetInstalled(string mountId, bool isAssemblyRoot)
        {
            lifecycleState = isAssemblyRoot
                ? PartLifecycleState.AssemblyRoot
                : PartLifecycleState.Installed;
            installedMountId = mountId ?? string.Empty;
        }
    }

    public sealed class FastenerInstance
    {
        private bool inserted;
        private bool seated;
        private int stage;

        public FastenerInstance(FastenerDefinition definition)
        {
            Definition = definition;
            ResetForEmptyMount();
        }

        public FastenerDefinition Definition { get; }

        public int Stage => stage;

        public bool IsInserted => inserted;

        public bool IsSeated => seated;

        public FastenerState State
        {
            get
            {
                if (!inserted)
                {
                    return FastenerState.Absent;
                }

                if (!seated)
                {
                    return FastenerState.Inserted;
                }

                if (stage <= 0)
                {
                    return FastenerState.Loose;
                }

                return stage >= Definition.MaximumStage
                    ? FastenerState.Tightened
                    : FastenerState.PartiallyTightened;
            }
        }

        public bool IsRemovalSecured =>
            Definition != null && Definition.RequiredForRemoval && stage > 0;

        public bool TryAdvance(bool tighten)
        {
            if (!inserted || Definition == null)
            {
                return false;
            }

            if (!seated)
            {
                if (!tighten)
                {
                    return false;
                }

                seated = true;
                stage = 0;
                return true;
            }

            int next = Mathf.Clamp(stage + (tighten ? 1 : -1), 0, Definition.MaximumStage);
            if (next == stage)
            {
                return false;
            }

            stage = next;
            return true;
        }

        public void ResetForInstalledPart()
        {
            inserted = Definition != null && Definition.InsertedOnInstall;
            seated = inserted;
            stage = 0;
        }

        public void ResetForEmptyMount()
        {
            inserted = false;
            seated = false;
            stage = 0;
        }

        public bool TryInsert()
        {
            if (inserted || Definition == null)
            {
                return false;
            }

            inserted = true;
            seated = false;
            stage = 0;
            return true;
        }

        public bool TryRemove()
        {
            if (!inserted || stage != 0)
            {
                return false;
            }

            ResetForEmptyMount();
            return true;
        }

        public bool TryRestore(bool restoredInserted, bool restoredSeated, int restoredStage)
        {
            if (Definition == null || restoredStage < 0 || restoredStage > Definition.MaximumStage ||
                (!restoredInserted && (restoredSeated || restoredStage != 0)) ||
                (restoredInserted && !restoredSeated && restoredStage != 0))
            {
                return false;
            }

            inserted = restoredInserted;
            seated = restoredSeated;
            stage = restoredStage;
            return true;
        }
    }

    public sealed class FastenerGroupState
    {
        private readonly FastenerInstance[] fasteners;
        private bool isBolted;

        public FastenerGroupState(
            FastenerGroupDefinition definition,
            FastenerInstance[] mountFasteners)
        {
            fasteners = mountFasteners ?? Array.Empty<FastenerInstance>();
            FastenerGroupDefinition compatibilityDefinition =
                FastenerGroupDefinition.CreateCompatibility(
                    GetDefinitions(fasteners));

            // Inline serializable classes added to an existing ScriptableObject
            // can deserialize as a non-null, default-valued instance. Treat that
            // shape as legacy missing authoring when the old mount still contains
            // removal-blocking fasteners; otherwise the part becomes FullySafe
            // immediately and its bolts never latch IsBolted.
            Definition = definition == null ||
                !definition.HasFasteners && compatibilityDefinition.HasFasteners
                    ? compatibilityDefinition
                    : definition;
        }

        public FastenerGroupDefinition Definition { get; }

        public int Tightness
        {
            get
            {
                int result = 0;
                string[] ids = Definition.FastenerDefinitionIds;
                for (int index = 0; index < fasteners.Length; index++)
                {
                    FastenerInstance fastener = fasteners[index];
                    if (fastener?.Definition != null &&
                        Contains(ids, fastener.Definition.DefinitionId))
                    {
                        result += fastener.Stage;
                    }
                }

                return Mathf.Clamp(
                    result,
                    0,
                    Definition.AggregateMaximumTightness);
            }
        }

        public bool IsBolted => isBolted;

        public bool IsFullySafe =>
            !Definition.HasFasteners ||
            Tightness >= Definition.AggregateMaximumTightness;

        public void Reevaluate(bool mountOccupied)
        {
            if (!mountOccupied || !Definition.HasFasteners)
            {
                isBolted = false;
                return;
            }

            int tightness = Tightness;
            if (isBolted)
            {
                if (tightness <= Definition.BoltedOffThreshold)
                {
                    isBolted = false;
                }
            }
            else if (tightness >= Definition.BoltedOnThreshold)
            {
                isBolted = true;
            }
        }

        public bool TryRestoreLatch(bool restoredBolted, bool mountOccupied)
        {
            int tightness = Tightness;
            if (!Definition.IsLatchConsistent(
                    tightness,
                    restoredBolted,
                    mountOccupied))
            {
                return false;
            }

            isBolted = restoredBolted;
            return true;
        }

        public void Reset()
        {
            isBolted = false;
        }

        public bool ShouldBreak(float speedKph, float sample01) =>
            Definition.ShouldBreak(Tightness, speedKph, sample01);

        private static FastenerDefinition[] GetDefinitions(
            FastenerInstance[] instances)
        {
            var definitions = new FastenerDefinition[instances.Length];
            for (int index = 0; index < instances.Length; index++)
            {
                definitions[index] = instances[index]?.Definition;
            }

            return definitions;
        }

        private static bool Contains(string[] values, string value)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
