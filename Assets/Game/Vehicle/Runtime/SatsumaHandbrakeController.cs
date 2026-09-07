using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    [Serializable]
    public sealed class SatsumaHandbrakeSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public float positionDegrees = SatsumaHandbrakeController.DonorRestPositionDegrees;

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                !float.IsFinite(positionDegrees) || positionDegrees < 0f ||
                positionDegrees > SatsumaHandbrakeController.MaximumPositionDegrees)
            {
                failure = "Satsuma handbrake save payload is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Project-owned implementation of donor HandBrake Use/Brake/Force behavior.
    /// The held mouse intent moves the ratcheted lever, not a vehicle input key.
    /// The cable brake is independent of the hydraulic service-brake circuit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaHandbrakeController : MonoBehaviour
    {
        public const string PartDefinitionId = "vehicle.satsuma.part.handbrake";
        public const string MountId = "mount.satsuma.handbrake";
        public const string DriveFastenerId = "fastener.satsuma.handbrake.boltpm-5";
        public const float DonorRestPositionDegrees = 0.1f;
        public const float MaximumPositionDegrees = 20f;
        public const float HeldSpeedDegreesPerSecond = 100f;

        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private PartInstance handbrakePart;
        [SerializeField] private Transform leverPivot;
        [SerializeField] private Quaternion leverRestLocalRotation = Quaternion.identity;
        [SerializeField, Range(0f, MaximumPositionDegrees)]
        private float positionDegrees = DonorRestPositionDegrees;

        private int heldDirection;
        private bool applicationChecked;
        private float? nextApplicationSample;

        public event Action<bool> HoldStarted;

        public float PositionDegrees => positionDegrees;
        public Transform LeverPivot => leverPivot;
        public bool IsHeld => heldDirection != 0;
        public bool CanOperate => isActiveAndEnabled && TryGetInstalledMount(
            out MountPointRuntime mount) && mount.FastenerGroup.IsBolted;

        /// <summary>
        /// Effective cable brake demand. The donor fifth, 5 mm fastener is a
        /// binary connection: stage one already transmits full lever demand.
        /// Never infer attachment solely from the part's serialized lifecycle.
        /// </summary>
        public float BrakeInput01
        {
            get
            {
                if (!isActiveAndEnabled ||
                    positionDegrees <= DonorRestPositionDegrees ||
                    !TryGetInstalledMount(out MountPointRuntime mount) ||
                    !mount.FastenerGroup.IsBolted ||
                    !mount.TryGetFastener(DriveFastenerId, out FastenerInstance drive) ||
                    !drive.IsInserted || drive.Stage <= 0)
                {
                    return 0f;
                }

                return Mathf.Clamp01(positionDegrees / MaximumPositionDegrees);
            }
        }

        public void Configure(
            VehicleAssemblyController configuredAssembly,
            PartInstance configuredHandbrakePart,
            Transform configuredLeverPivot)
        {
            assembly = configuredAssembly != null
                ? configuredAssembly
                : throw new ArgumentNullException(nameof(configuredAssembly));
            handbrakePart = configuredHandbrakePart != null
                ? configuredHandbrakePart
                : throw new ArgumentNullException(nameof(configuredHandbrakePart));
            leverPivot = configuredLeverPivot != null
                ? configuredLeverPivot
                : throw new ArgumentNullException(nameof(configuredLeverPivot));
            leverRestLocalRotation = leverPivot.localRotation;
            ResetState();
        }

        public bool TrySetHeldDirection(int signedDirection) =>
            SetHeldDirection(signedDirection, null);

        /// <summary>Supplies one deterministic chance sample for a test/replay application.</summary>
        public bool TrySetHeldDirection(int signedDirection, float sample01)
        {
            if (!float.IsFinite(sample01) || sample01 < 0f || sample01 > 1f)
            {
                ReleaseHold();
                return false;
            }

            return SetHeldDirection(signedDirection, sample01);
        }

        private bool SetHeldDirection(int signedDirection, float? sample01)
        {
            if (signedDirection == 0)
            {
                ReleaseHold();
                return true;
            }

            if ((signedDirection != -1 && signedDirection != 1) || !CanOperate ||
                signedDirection > 0 && positionDegrees >= MaximumPositionDegrees ||
                signedDirection < 0 && positionDegrees <= 0f)
            {
                ReleaseHold();
                return false;
            }

            bool changedDirection = heldDirection != signedDirection;
            heldDirection = signedDirection;
            nextApplicationSample = sample01;
            if (changedDirection)
            {
                HoldStarted?.Invoke(signedDirection > 0);
            }

            return true;
        }

        public void ReleaseHold()
        {
            heldDirection = 0;
            nextApplicationSample = null;
        }

        public void Simulate(float deltaSeconds)
        {
            if (!CanOperate)
            {
                ReleaseHold();
                applicationChecked = false;
            }

            if (float.IsFinite(deltaSeconds) && deltaSeconds > 0f && CanOperate)
            {
                if (heldDirection != 0)
                {
                    positionDegrees = Mathf.Clamp(
                        positionDegrees + heldDirection * HeldSpeedDegreesPerSecond * deltaSeconds,
                        0f,
                        MaximumPositionDegrees);
                }

                if (positionDegrees <= DonorRestPositionDegrees)
                {
                    applicationChecked = false;
                }
                else if (!applicationChecked && TryGetInstalledMount(out MountPointRuntime mount))
                {
                    // Donor Brake OFF -> Chance runs once per application,
                    // including reactivation of an already raised installed lever.
                    applicationChecked = true;
                    float sample = nextApplicationSample ?? UnityEngine.Random.value;
                    nextApplicationSample = null;
                    if (ShouldBreakForTightness(mount.FastenerGroup.Tightness, sample))
                    {
                        assembly.TryBreakInstalledPart(handbrakePart);
                        ReleaseHold();
                    }
                }
            }

            ApplyPresentation();
        }

        public SatsumaHandbrakeSaveDto CaptureSaveData() => new()
        {
            positionDegrees = positionDegrees,
        };

        public bool TryRestore(SatsumaHandbrakeSaveDto dto, out string failure)
        {
            if (dto == null)
            {
                ResetState();
                failure = string.Empty;
                return true;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            ReleaseHold();
            applicationChecked = false;
            positionDegrees = dto.positionDegrees;
            ApplyPresentation();
            return true;
        }

        public void ResetState()
        {
            ReleaseHold();
            applicationChecked = false;
            positionDegrees = DonorRestPositionDegrees;
            ApplyPresentation();
        }

        private bool TryGetInstalledMount(out MountPointRuntime mount)
        {
            mount = null;
            return assembly != null && handbrakePart != null &&
                handbrakePart.IsInstalled &&
                string.Equals(handbrakePart.RuntimeState.InstalledMountId,
                    MountId, StringComparison.Ordinal) &&
                assembly.Graph.TryGetMount(MountId, out mount) &&
                mount.IsOccupied && mount.InstalledPart == handbrakePart;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                Simulate(Time.deltaTime);
            }
        }

        private void OnDisable() => ReleaseHold();

        /// <summary>
        /// Frozen Brake/Chance selects FINISHED with weight 1 and BREAK with
        /// weight (39-Tightness)/40. Non-positive break weights cannot win.
        /// </summary>
        public static bool ShouldBreakForTightness(int tightness, float sample01)
        {
            if (!float.IsFinite(sample01) || sample01 < 0f || sample01 > 1f)
            {
                return false;
            }

            float breakWeight = Mathf.Max(0f, (39f - Mathf.Clamp(tightness, 0, 40)) / 40f);
            return breakWeight > 0f && sample01 < breakWeight / (1f + breakWeight);
        }

        private void ApplyPresentation()
        {
            if (leverPivot != null)
            {
                // Donor Brake writes local X=Position, Y=Z=0. An explicitly
                // authored rest frame keeps the wrapper's import axes intact.
                leverPivot.localRotation = leverRestLocalRotation *
                    Quaternion.AngleAxis(positionDegrees, Vector3.right);
            }
        }
    }
}
