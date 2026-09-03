using System;
using MSC.Core.Lifecycle;
using MSC.Vehicle.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Vehicle
{
    /// <summary>
    /// Samples the dedicated M06 Input System map during Update and exposes a
    /// cached snapshot to FixedUpdate. Button edges are latched until consumed.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class VehicleInputRouter : MonoBehaviour, IVehicleInputSource,
        IGameplayInputGate
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Vehicle";
        [SerializeField, Min(1)] private int maximumForwardGear = 4;
        [SerializeField] private bool ignitionOnAtStart;

        private InputActionMap vehicleMap;
        private InputAction throttleAction;
        private InputAction brakeAction;
        private InputAction clutchAction;
        private InputAction steeringAction;
        private InputAction ignitionAction;
        private InputAction starterAction;
        private InputAction gearUpAction;
        private InputAction gearDownAction;
        private InputAction resetAction;

        private float throttle01;
        private float brake01;
        private float clutchPedal01;
        private float steering;
        private bool starterRequested;
        private bool ignitionOn;
        private int pendingGearDelta;
        private bool resetRequested;
        private bool actionsResolved;

        public InputActionAsset InputActions => inputActions;

        public string ActionMapName => actionMapName;

        public bool IgnitionOn => ignitionOn;

        public bool IsGameplayInputEnabled =>
            enabled && vehicleMap != null && vehicleMap.enabled;

        public void SetGameplayInputEnabled(bool value)
        {
            enabled = value;
        }

        public void Configure(
            InputActionAsset actions,
            string mapName = "Vehicle",
            int maximumGear = 4,
            bool initialIgnition = false)
        {
            vehicleMap?.Disable();
            inputActions = actions;
            actionMapName = string.IsNullOrWhiteSpace(mapName) ? "Vehicle" : mapName;
            maximumForwardGear = Mathf.Max(1, maximumGear);
            ignitionOnAtStart = initialIgnition;
            ignitionOn = initialIgnition;
            actionsResolved = false;
            ResolveActions();
            if (isActiveAndEnabled)
            {
                vehicleMap?.Enable();
            }
        }

        private void Awake()
        {
            ignitionOn = ignitionOnAtStart;
            ResolveActions();
        }

        private void OnEnable()
        {
            ResolveActions();
            vehicleMap?.Enable();
        }

        private void OnDisable()
        {
            vehicleMap?.Disable();
            ClearTransientState();
        }

        private void Update()
        {
            if (!actionsResolved)
            {
                return;
            }

            throttle01 = Mathf.Clamp01(throttleAction.ReadValue<float>());
            brake01 = Mathf.Clamp01(brakeAction.ReadValue<float>());
            clutchPedal01 = Mathf.Clamp01(clutchAction.ReadValue<float>());
            steering = Mathf.Clamp(steeringAction.ReadValue<float>(), -1f, 1f);
            starterRequested = starterAction.IsPressed();

            if (ignitionAction.WasPerformedThisFrame())
            {
                ignitionOn = !ignitionOn;
            }

            if (gearUpAction.WasPerformedThisFrame())
            {
                pendingGearDelta++;
            }

            if (gearDownAction.WasPerformedThisFrame())
            {
                pendingGearDelta--;
            }

            if (resetAction.WasPerformedThisFrame())
            {
                resetRequested = true;
            }
        }

        public VehicleInputState ConsumeFixedInput(int currentSelectedGear)
        {
            bool gearChangeRequested = pendingGearDelta != 0;
            int requestedGear = gearChangeRequested
                ? Mathf.Clamp(currentSelectedGear + pendingGearDelta, -1, maximumForwardGear)
                : currentSelectedGear;
            pendingGearDelta = 0;

            return new VehicleInputState(
                throttle01,
                clutchPedal01,
                brake01,
                steering,
                ignitionOn,
                starterRequested,
                gearChangeRequested,
                requestedGear);
        }

        public bool ConsumeResetRequest()
        {
            bool result = resetRequested;
            resetRequested = false;
            return result;
        }

        public void SetIgnitionForTesting(bool value)
        {
            ignitionOn = value;
        }

        public void RestorePersistentState(bool restoredIgnitionOn)
        {
            ignitionOn = restoredIgnitionOn;
            ClearTransientState();
        }

        private void ResolveActions()
        {
            if (actionsResolved || inputActions == null)
            {
                return;
            }

            vehicleMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (vehicleMap == null)
            {
                Debug.LogError("M06 vehicle InputAction map is missing: " + actionMapName, this);
                enabled = false;
                return;
            }

            throttleAction = FindRequiredAction("Throttle");
            brakeAction = FindRequiredAction("Brake");
            clutchAction = FindRequiredAction("Clutch");
            steeringAction = FindRequiredAction("Steering");
            ignitionAction = FindRequiredAction("Ignition");
            starterAction = FindRequiredAction("Starter");
            gearUpAction = FindRequiredAction("GearUp");
            gearDownAction = FindRequiredAction("GearDown");
            resetAction = FindRequiredAction("Reset");
            actionsResolved = throttleAction != null && brakeAction != null && clutchAction != null &&
                              steeringAction != null && ignitionAction != null && starterAction != null &&
                              gearUpAction != null && gearDownAction != null && resetAction != null;
            if (!actionsResolved)
            {
                enabled = false;
            }
        }

        private InputAction FindRequiredAction(string actionName)
        {
            InputAction action = vehicleMap.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogError("M06 vehicle InputAction is missing: " + actionName, this);
            }

            return action;
        }

        private void ClearTransientState()
        {
            throttle01 = 0f;
            brake01 = 0f;
            clutchPedal01 = 0f;
            steering = 0f;
            starterRequested = false;
            pendingGearDelta = 0;
            resetRequested = false;
        }
    }
}
