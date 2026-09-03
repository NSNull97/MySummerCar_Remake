using System;
using MSC.Vehicle.Assembly;
using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Owns the front carrier's assembly-dependent yaw, separately from NWH's
    /// accepted vertical contact solver. The frozen donor also keeps Wheel.cs
    /// tire forces on its cached parent chassis when a carrier HingeJoint is
    /// added; installed rim colliders are disabled by the Removal FSM.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class SatsumaFrontSteeringController : MonoBehaviour
    {
        public const float FreeYawLimitDegrees = 33f;

        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private SatsumaFrontSuspensionCornerBinding[] corners =
            Array.Empty<SatsumaFrontSuspensionCornerBinding>();

        private CornerState[] states;
        private GameObject runtimeRoot;

        public VehicleAssemblyController AssemblyController => assemblyController;
        public Rigidbody Chassis => chassis;
        public SatsumaFrontSuspensionCornerBinding[] Corners => corners;

        public void Configure(VehicleAssemblyController configuredAssembly,
            Rigidbody configuredChassis,
            SatsumaFrontSuspensionCornerBinding[] configuredCorners)
        {
            ReleaseRuntime();
            assemblyController = configuredAssembly;
            chassis = configuredChassis;
            corners = configuredCorners ?? Array.Empty<SatsumaFrontSuspensionCornerBinding>();
        }

        private void Start()
        {
            ApplyNow();
        }

        private void FixedUpdate()
        {
            // VehicleSimulationHost supplies commands at order 0; NWH consumes
            // the combined yaw at 100, before suspension presentation at 125.
            ApplyNow();
        }

        private void OnDisable()
        {
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            ReleaseRuntime();
        }

        public float ResolveSteerAngle(WheelController wheel, float commandDegrees)
        {
            if (!isActiveAndEnabled || !EnsureRuntime())
            {
                return commandDegrees;
            }

            CornerState state = FindState(wheel);
            if (state == null)
            {
                return commandDegrees;
            }

            state.CommandDegrees = commandDegrees;
            return state.YawDegrees + (state.Connected ? commandDegrees : 0f);
        }

        public bool TryGetSteeringState(WheelController wheel,
            out bool connected, out float carrierYawDegrees)
        {
            CornerState state = FindState(wheel);
            connected = state != null && state.Connected;
            carrierYawDegrees = state != null ? state.YawDegrees : 0f;
            return state != null;
        }

        public bool TryGetFreeYawBody(WheelController wheel, out Rigidbody body)
        {
            CornerState state = FindState(wheel);
            body = state?.FreeBody;
            return body != null;
        }

        public void ApplyNow()
        {
            if (!isActiveAndEnabled || !EnsureRuntime())
            {
                return;
            }

            runtimeRoot.SetActive(true);
            foreach (CornerState state in states)
            {
                RefreshCorner(state);
                state.Binding.Wheel.SteerAngle = state.YawDegrees +
                    (state.Connected ? state.CommandDegrees : 0f);
            }
        }

        private bool EnsureRuntime()
        {
            if (states != null && runtimeRoot != null)
            {
                if (runtimeRoot.scene != gameObject.scene)
                {
                    SceneManager.MoveGameObjectToScene(runtimeRoot, gameObject.scene);
                }
                return true;
            }

            // A vehicle may survive a scene transition independently of its
            // previous physics-helper scene. Never keep destroyed body handles.
            states = null;

            if (!Application.isPlaying || assemblyController == null ||
                chassis == null || corners.Length == 0)
            {
                return false;
            }

            foreach (SatsumaFrontSuspensionCornerBinding binding in corners)
            {
                if (binding.Wheel == null || binding.SteeringRodMount == null ||
                    binding.StrutMount == null || binding.SteeringRodPresentation == null ||
                    binding.SteeringRodPresentation.Part == null ||
                    !binding.SteeringRodPresentation.Part.TryGetComponent(
                        out AssemblySteeringAlignmentState _))
                {
                    Debug.LogError("Front steering requires an explicit wheel, rod, " +
                        "strut and per-rod alignment binding.", this);
                    enabled = false;
                    return false;
                }
            }

            runtimeRoot = new GameObject("Satsuma front yaw physics")
            {
                hideFlags = HideFlags.DontSave,
            };
            SceneManager.MoveGameObjectToScene(runtimeRoot, gameObject.scene);
            states = new CornerState[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var anchorObject = new GameObject("Yaw anchor " + corners[i].CornerId);
                anchorObject.transform.SetParent(runtimeRoot.transform, false);
                Rigidbody anchor = anchorObject.AddComponent<Rigidbody>();
                anchor.isKinematic = true;
                anchor.useGravity = false;
                anchor.detectCollisions = false;
                states[i] = new CornerState(corners[i], anchor,
                    Quaternion.Inverse(chassis.rotation) * corners[i].Wheel.transform.rotation);
                RefreshCorner(states[i]);
            }

            return true;
        }

        private void RefreshCorner(CornerState state)
        {
            MountPointRuntime rodMount = assemblyController.ResolveMount(state.Binding.SteeringRodMount);
            MountPointRuntime strutMount = assemblyController.ResolveMount(state.Binding.StrutMount);
            PartInstance rod = rodMount?.InstalledPart ?? state.Binding.SteeringRodPresentation.Part;
            AssemblySteeringAlignmentState alignment = rod.GetComponent<AssemblySteeringAlignmentState>();
            bool sourceChanged = state.Alignment != alignment;
            if (sourceChanged)
            {
                state.Alignment = alignment;
                state.AlignmentRevision = -1;
            }

            float alignmentDegrees = alignment.AlignmentDegrees;
            bool revisionChanged = state.AlignmentRevision != alignment.Revision;
            bool connected = rodMount != null && rodMount.IsOccupied &&
                rodMount.FastenerGroup.IsBolted && strutMount != null && strutMount.IsOccupied;
            Quaternion baseRotation = chassis.rotation * state.BaseLocalRotation;
            Vector3 anchorPosition = state.Binding.Wheel.transform.position;
            state.Anchor.position = anchorPosition;
            state.Anchor.rotation = baseRotation;

            if (connected)
            {
                ReleaseFreeBody(state);
                state.YawDegrees = alignmentDegrees;
            }
            else
            {
                if (state.FreeBody == null)
                {
                    CreateFreeBody(state, anchorPosition, baseRotation, alignmentDegrees);
                }
                else if (revisionChanged)
                {
                    // The 14 mm adjuster writes carrier local Y once, including
                    // while disconnected. It does not keep pulling toward toe.
                    state.FreeBody.rotation = baseRotation * Quaternion.Euler(0f, alignmentDegrees, 0f);
                }

                Vector3 localForward = Quaternion.Inverse(baseRotation) *
                    (state.FreeBody.rotation * Vector3.forward);
                state.YawDegrees = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            }

            state.Connected = connected;
            state.AlignmentRevision = alignment.Revision;
        }

        private void CreateFreeBody(CornerState state, Vector3 position,
            Quaternion baseRotation, float alignmentDegrees)
        {
            // RefreshCorner writes the physics pose, but Rigidbody setters do
            // not update its Transform until the next simulation step. The
            // joint captures that Transform now: at the production 180-degree
            // spawn, an identity anchor baked a reversed constraint frame.
            // Synchronize this anchor before initial creation AND recreation;
            // ordinary free motion and the donor relative limits stay intact.
            state.Anchor.transform.SetPositionAndRotation(position, baseRotation);

            var bodyObject = new GameObject("Free yaw " + state.Binding.CornerId);
            bodyObject.transform.SetParent(runtimeRoot.transform, false);
            bodyObject.transform.SetPositionAndRotation(position,
                baseRotation * Quaternion.Euler(0f, alignmentDegrees, 0f));
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.detectCollisions = false;

            // Compatibility boundary: the kinematic anchor absorbs the small
            // carrier's constraint reactions instead of changing V33 chassis
            // mass/contact. No collider, ground force, centering torque or
            // arbitrary yaw noise is added. NWH remains a chassis child.
            HingeJoint joint = bodyObject.AddComponent<HingeJoint>();
            joint.axis = Vector3.up;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = Vector3.zero;
            joint.connectedBody = state.Anchor;
            joint.useSpring = false;
            joint.useMotor = false;
            JointLimits limits = joint.limits;
            limits.min = -FreeYawLimitDegrees;
            limits.max = FreeYawLimitDegrees;
            joint.limits = limits;
            joint.useLimits = true;
            state.FreeBody = body;
        }

        private static void ReleaseFreeBody(CornerState state)
        {
            if (state.FreeBody == null)
            {
                return;
            }

            state.FreeBody.gameObject.SetActive(false);
            Destroy(state.FreeBody.gameObject);
            state.FreeBody = null;
        }

        private CornerState FindState(WheelController wheel)
        {
            if (states != null)
            {
                foreach (CornerState state in states)
                {
                    if (state.Binding.Wheel == wheel)
                    {
                        return state;
                    }
                }
            }
            return null;
        }

        private void ReleaseRuntime()
        {
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
                Destroy(runtimeRoot);
            }
            runtimeRoot = null;
            states = null;
        }

        private sealed class CornerState
        {
            public readonly SatsumaFrontSuspensionCornerBinding Binding;
            public readonly Rigidbody Anchor;
            public readonly Quaternion BaseLocalRotation;
            public AssemblySteeringAlignmentState Alignment;
            public Rigidbody FreeBody;
            public int AlignmentRevision = -1;
            public bool Connected;
            public float CommandDegrees;
            public float YawDegrees;

            public CornerState(SatsumaFrontSuspensionCornerBinding binding,
                Rigidbody anchor, Quaternion baseLocalRotation)
            {
                Binding = binding;
                Anchor = anchor;
                BaseLocalRotation = baseLocalRotation;
            }
        }
    }
}
