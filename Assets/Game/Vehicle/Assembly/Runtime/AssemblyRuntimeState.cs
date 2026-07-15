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
}
