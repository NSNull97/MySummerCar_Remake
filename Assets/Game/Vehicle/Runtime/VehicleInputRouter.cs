using System;
using MSC.Core.Lifecycle;
using MSC.Vehicle.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
        private bool driverSessionManaged;
        private bool driverSessionActive;
        private bool gameplayInputAllowed = true;
        private bool waitingForNeutralControls;
        private bool observedInputUpdateAfterEnable;
        private bool inputUpdateSubscribed;

        public bool IsDriverSessionManaged => driverSessionManaged;
        public bool IsDriverSessionActive => driverSessionActive;
        public bool IsWaitingForNeutralControls => waitingForNeutralControls;
        public float SteeringInputMinusOneToOne => steering;

        public InputActionAsset InputActions => inputActions;

        public string ActionMapName => actionMapName;

        public bool IgnitionOn => ignitionOn;

        public bool IsGameplayInputEnabled =>
            enabled && (driverSessionManaged ? gameplayInputAllowed :
                vehicleMap != null && vehicleMap.enabled);

        public void SetGameplayInputEnabled(bool value)
        {
            if (!driverSessionManaged) { enabled = value; return; }
            if (value && !gameplayInputAllowed) waitingForNeutralControls = true;
            gameplayInputAllowed = value;
            RefreshActionMapPermission();
        }

        /// <summary>
        /// Opt-in for an explicitly composed driver station. Pause permission
        /// survives independently from occupancy, so a load behind the menu
        /// cannot re-enable pedals or restore an obsolete pre-load occupancy.
        /// </summary>
        public void ConfigureDriverSessionManagement()
        {
            driverSessionManaged = true;
            driverSessionActive = false;
            waitingForNeutralControls = true;
            enabled = true;
            SubscribeInputUpdate();
            RefreshActionMapPermission();
        }

        public void SetDriverSessionActive(bool active)
        {
            if (!driverSessionManaged)
                throw new InvalidOperationException("Driver session ownership was not configured.");
            driverSessionActive = active;
            waitingForNeutralControls = true;
            ClearTransientState();
            RefreshActionMapPermission();
        }

        private void RefreshActionMapPermission()
        {
            bool allow = isActiveAndEnabled && (!driverSessionManaged ||
                driverSessionActive && gameplayInputAllowed);
            if (allow)
            {
                if (vehicleMap == null || vehicleMap.enabled) return;
                vehicleMap.Enable();
                // Enabling from the player's Update defers Input System's
                // initial-state check until the next input update. The zero
                // values in this frame are not evidence of released pedals.
                observedInputUpdateAfterEnable = false;
            }
            else { vehicleMap?.Disable(); ClearTransientState(); }
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
                RefreshActionMapPermission();
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
            SubscribeInputUpdate();
            RefreshActionMapPermission();
        }

        private void OnDisable()
        {
            if (inputUpdateSubscribed) InputSystem.onAfterUpdate -= ObserveInputUpdate;
            inputUpdateSubscribed = false;
            vehicleMap?.Disable();
            ClearTransientState();
        }

        private void Update()
        {
            if (!actionsResolved || vehicleMap == null || !vehicleMap.enabled)
            {
                return;
            }
            if (driverSessionManaged && Time.timeScale <= 0f)
            {
                waitingForNeutralControls = true;
                ClearTransientState();
                return;
            }
            if (driverSessionManaged && !observedInputUpdateAfterEnable)
            {
                ClearTransientState();
                return;
            }

            throttle01 = Mathf.Clamp01(throttleAction.ReadValue<float>());
            brake01 = Mathf.Clamp01(brakeAction.ReadValue<float>());
            clutchPedal01 = Mathf.Clamp01(clutchAction.ReadValue<float>());
            steering = Mathf.Clamp(steeringAction.ReadValue<float>(), -1f, 1f);
            if (driverSessionManaged && waitingForNeutralControls)
            {
                bool neutral = throttle01 <= .001f && brake01 <= .001f &&
                    clutchPedal01 <= .001f && Mathf.Abs(steering) <= .001f &&
                    !gearUpAction.IsPressed() && !gearDownAction.IsPressed();
                ClearTransientState();
                if (neutral) waitingForNeutralControls = false;
                return;
            }

            starterRequested = !driverSessionManaged && starterAction.IsPressed();

            if (!driverSessionManaged && ignitionAction.WasPerformedThisFrame())
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

            if (!driverSessionManaged && resetAction.WasPerformedThisFrame())
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

        private void SubscribeInputUpdate()
        {
            if (!driverSessionManaged || inputUpdateSubscribed || !isActiveAndEnabled) return;
            InputSystem.onAfterUpdate += ObserveInputUpdate;
            inputUpdateSubscribed = true;
        }

        private void ObserveInputUpdate()
        {
            // An Editor/BeforeRender callback is not the action update that
            // performs the newly-enabled pedal map's initial-state check.
            InputUpdateType type = InputState.currentUpdateType;
            if (vehicleMap != null && vehicleMap.enabled &&
                (type == InputUpdateType.Dynamic || type == InputUpdateType.Fixed || type == InputUpdateType.Manual))
                observedInputUpdateAfterEnable = true;
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
            if (driverSessionManaged) waitingForNeutralControls = true;
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
