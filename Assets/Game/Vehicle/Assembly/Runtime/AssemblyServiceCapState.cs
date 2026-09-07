using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum SatsumaServiceCapKind { MotorOil = 0, Coolant = 1, BrakeFront = 2, BrakeRear = 3, Clutch = 4 }

    [Serializable]
    public sealed class AssemblyServiceCapSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public SatsumaServiceCapKind[] kinds = Array.Empty<SatsumaServiceCapKind>();
        public float[] angles = Array.Empty<float>();
        public bool IsValid
        {
            get
            {
                if (schemaVersion != CurrentSchemaVersion || kinds == null || angles == null ||
                    kinds.Length == 0 || kinds.Length > 2 || kinds.Length != angles.Length) return false;
                for (int i = 0; i < kinds.Length; i++)
                {
                    if ((int)kinds[i] < 0 || (int)kinds[i] > 4 || !float.IsFinite(angles[i]) ||
                        angles[i] < 1f || angles[i] > 359f) return false;
                    for (int j = 0; j < i; j++) if (kinds[i] == kinds[j]) return false;
                }
                return true;
            }
        }
        public AssemblyServiceCapSaveDto Clone() => new()
        { schemaVersion = schemaVersion, kinds = kinds == null ? null : (SatsumaServiceCapKind[])kinds.Clone(),
            angles = angles == null ? null : (float[])angles.Clone() };
    }

    /// <summary>Part-owned caps. Frozen controls rotate 33 degrees, 359 closed to 1 open.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyServiceCapState : MonoBehaviour
    {
        [SerializeField] private PartInstance part;
        [SerializeField] private SatsumaServiceCapKind[] kinds = Array.Empty<SatsumaServiceCapKind>();
        private float[] angles;
        public PartInstance Part => part;
        public int Count => kinds.Length;
        public int Revision { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && part != null && part.IsInstalled;

        public void Configure(PartInstance owner, params SatsumaServiceCapKind[] configuredKinds)
        {
            if (owner == null || owner.gameObject != gameObject || owner.Definition == null ||
                configuredKinds == null || configuredKinds.Length == 0 || configuredKinds.Length != ExpectedCount(owner.Definition.DefinitionId))
                throw new ArgumentException("Service caps require their explicit reservoir/cover part.");
            for (int i = 0; i < configuredKinds.Length; i++)
            {
                if (!Supports(owner.Definition.DefinitionId, configuredKinds[i])) throw new ArgumentException("Wrong cap owner.");
                for (int j = 0; j < i; j++) if (configuredKinds[i] == configuredKinds[j]) throw new ArgumentException("Duplicate cap.");
            }
            if (part != null)
            {
                if (part != owner || kinds.Length != configuredKinds.Length) throw new InvalidOperationException("Do not replace cap identity.");
                for (int i = 0; i < kinds.Length; i++)
                    if (kinds[i] != configuredKinds[i]) throw new InvalidOperationException("Do not reorder cap identity.");
                return;
            }
            part = owner; kinds = (SatsumaServiceCapKind[])configuredKinds.Clone(); EnsureState();
        }

        public SatsumaServiceCapKind Kind(int index) => kinds[index];
        public float Angle(int index) { EnsureState(); return angles[index]; }
        public bool IsOpen(int index) => Angle(index) <= 1f;
        public bool CanAdjust(int index, float notches) => IsAvailable && TryStep(Angle(index), notches, out _);
        public bool TryAdjust(int index, float notches)
        {
            if (!IsAvailable || !TryStep(Angle(index), notches, out float next)) return false;
            angles[index] = next; Revision++; return true;
        }
        public static bool TryStep(float current, float notches, out float next)
        {
            next = current;
            if (!float.IsFinite(current) || current < 1f || current > 359f ||
                !float.IsFinite(notches) || Mathf.Abs(notches) < .001f) return false;
            next = Mathf.Clamp(current + (notches > 0f ? 33f : -33f), 1f, 359f);
            return next != current;
        }
        public AssemblyServiceCapSaveDto CaptureSaveData()
        { EnsureState(); return new() { kinds = (SatsumaServiceCapKind[])kinds.Clone(), angles = (float[])angles.Clone() }; }
        public bool CanRestore(AssemblyServiceCapSaveDto dto)
        {
            if (dto == null || !dto.IsValid || dto.kinds.Length != kinds.Length) return false;
            for (int i = 0; i < kinds.Length; i++) if (dto.kinds[i] != kinds[i]) return false;
            return true;
        }
        public void RestoreValidated(AssemblyServiceCapSaveDto dto)
        {
            if (dto != null && !CanRestore(dto)) throw new ArgumentException("Invalid cap save/owner.");
            angles = dto == null ? null : (float[])dto.angles.Clone(); EnsureState(); Revision++;
        }
        private void EnsureState()
        {
            if (angles != null) return;
            angles = new float[kinds.Length];
            for (int i = 0; i < angles.Length; i++) angles[i] = 359f;
        }
        private static int ExpectedCount(string id) => id == "vehicle.satsuma.part.brake-master-cylinder" ? 2 :
            id is "vehicle.satsuma.part.rocker-cover" or "vehicle.satsuma.part.gt-rocker-cover-gt" or
                "vehicle.satsuma.part.radiator" or "vehicle.satsuma.part.clutch-master-cylinder" ? 1 : 0;
        private static bool Supports(string id, SatsumaServiceCapKind kind) => kind switch
        {
            SatsumaServiceCapKind.MotorOil => id is "vehicle.satsuma.part.rocker-cover" or "vehicle.satsuma.part.gt-rocker-cover-gt",
            SatsumaServiceCapKind.Coolant => id == "vehicle.satsuma.part.radiator",
            SatsumaServiceCapKind.BrakeFront or SatsumaServiceCapKind.BrakeRear => id == "vehicle.satsuma.part.brake-master-cylinder",
            SatsumaServiceCapKind.Clutch => id == "vehicle.satsuma.part.clutch-master-cylinder",
            _ => false,
        };
    }
}
