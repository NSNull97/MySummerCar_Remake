using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyValveAdjustmentSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public Vector4 intake = Vector4.one * 7f;
        public Vector4 exhaust = Vector4.one * 6f;
        public bool IsValid
        {
            get
            {
                if (schemaVersion != CurrentSchemaVersion) return false;
                for (int i = 0; i < 4; i++)
                    if (!float.IsFinite(intake[i]) || intake[i] < 4f || intake[i] > 10f ||
                        !float.IsFinite(exhaust[i]) || exhaust[i] < 3f || exhaust[i] > 9f) return false;
                return true;
            }
        }
        public AssemblyValveAdjustmentSaveDto Clone() => (AssemblyValveAdjustmentSaveDto)MemberwiseClone();
    }

    /// <summary>Eight setting values, not mounting stages; frozen RockerShaft Data109466.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyValveAdjustmentState : MonoBehaviour
    {
        public const string PartId = "vehicle.satsuma.part.rocker-shaft";
        public const float Step = 0.3f;
        [SerializeField] private PartInstance part;
        [SerializeField] private VehicleAssemblyController assembly;
        private Vector4 intake = Vector4.one * 7f;
        private Vector4 exhaust = Vector4.one * 6f;
        public PartInstance Part => part;
        public VehicleAssemblyController Assembly => assembly;
        public Vector4 Intake => intake;
        public Vector4 Exhaust => exhaust;
        public int Revision { get; private set; }

        public bool IsAvailable => isActiveAndEnabled && part != null && part.IsInstalled && assembly != null &&
            !assembly.Graph.IsPartDefinitionInstalled("vehicle.satsuma.part.rocker-cover") &&
            !assembly.Graph.IsPartDefinitionInstalled("vehicle.satsuma.part.gt-rocker-cover-gt");

        public void Configure(PartInstance owner, VehicleAssemblyController controller)
        {
            if (owner == null || owner.gameObject != gameObject || owner.Definition == null ||
                owner.Definition.DefinitionId != PartId || controller == null)
                throw new ArgumentException("Valve settings require the explicit stock rocker-shaft part and assembly.");
            part = owner; assembly = controller;
        }

        // Project index is cylinder order: 1-in, 1-ex, 2-in, 2-ex, 3-in, 3-ex, 4-in, 4-ex.
        public float GetSetting(int index)
        {
            ValidateIndex(index); return (index & 1) == 0 ? intake[index / 2] : exhaust[index / 2];
        }
        public bool CanAdjust(int index, float signedNotches) => IsAvailable &&
            TryStep(index, GetSetting(index), signedNotches, out _);
        public bool TryAdjust(int index, float signedNotches)
        {
            if (!CanAdjust(index, signedNotches) || !TryStep(index, GetSetting(index), signedNotches, out float next)) return false;
            if ((index & 1) == 0) intake[index / 2] = next; else exhaust[index / 2] = next;
            Revision++; return true;
        }
        public static bool TryStep(int index, float current, float signedNotches, out float next)
        {
            ValidateIndex(index); next = current;
            if (!float.IsFinite(current) || !float.IsFinite(signedNotches) || Mathf.Abs(signedNotches) < 0.001f) return false;
            float minimum = (index & 1) == 0 ? 4f : 3f;
            next = Mathf.Clamp(current + (signedNotches > 0f ? -Step : Step), minimum, minimum + 6f);
            return !Mathf.Approximately(next, current);
        }
        public AssemblyValveAdjustmentSaveDto CaptureSaveData() => new() { intake = intake, exhaust = exhaust };
        public void RestoreValidated(AssemblyValveAdjustmentSaveDto dto)
        {
            if (dto != null && !dto.IsValid) throw new ArgumentException("Invalid valve settings.", nameof(dto));
            // Previous pseudo-bolt stages have no relationship to a valve setting.
            intake = dto?.intake ?? Vector4.one * 7f;
            exhaust = dto?.exhaust ?? Vector4.one * 6f;
            Revision++;
        }
        private static void ValidateIndex(int index)
        { if (index < 0 || index >= 8) throw new ArgumentOutOfRangeException(nameof(index)); }
    }
}
