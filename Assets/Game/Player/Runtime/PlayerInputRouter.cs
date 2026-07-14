using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Player
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        private const string PlayerMapName = "Player";

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private FirstPersonMotor motor;

        [SerializeField]
        private FirstPersonLook firstPersonLook;

        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField, Min(0f)]
        private float heldRotationDegreesPerPixel = 0.2f;

        [SerializeField, Min(0f)]
        private float heldRotationDegreesPerSecond = 90f;

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction crouchAction;
        private InputAction interactAction;
        private InputAction dropAction;
        private InputAction placeAction;
        private InputAction throwAction;
        private InputAction rotateModifierAction;
        private InputAction toolAction;

        public InputActionAsset InputActions => inputActions;

        public bool IsReady => playerMap != null;

        public void Configure(
            InputActionAsset actions,
            FirstPersonMotor movement,
            FirstPersonLook look,
            PlayerInteractionController interaction)
        {
            inputActions = actions;
            motor = movement;
            firstPersonLook = look;
            interactionController = interaction;
        }

        private void OnEnable()
        {
            try
            {
                BindActions();
                playerMap.Enable();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"Player input configuration is invalid: {exception.Message}", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            motor?.ResetInputIntent();
        }

        private void Update()
        {
            if (playerMap == null)
            {
                return;
            }

            motor?.SetMoveInput(moveAction.ReadValue<Vector2>());
            motor?.SetCrouchRequested(crouchAction.IsPressed());

            Vector2 lookValue = lookAction.ReadValue<Vector2>();
            bool isPointerDelta = lookAction.activeControl?.device is Pointer;
            bool rotatingHeldObject = rotateModifierAction.IsPressed() &&
                interactionController != null &&
                interactionController.HasHeldObject;

            if (rotatingHeldObject)
            {
                Vector2 rotationDegrees = LookInputScaling.ToRotationDegrees(
                    lookValue,
                    isPointerDelta,
                    heldRotationDegreesPerPixel,
                    heldRotationDegreesPerSecond,
                    Time.unscaledDeltaTime);
                interactionController.RotateHeldObject(rotationDegrees);
            }
            else if (firstPersonLook != null)
            {
                firstPersonLook.ApplyLook(lookValue, isPointerDelta, Time.unscaledDeltaTime);
            }

            if (interactAction.WasPressedThisFrame())
            {
                interactionController?.TryPrimaryInteraction();
            }

            if (dropAction.WasPressedThisFrame())
            {
                interactionController?.DropHeldObject();
            }

            if (placeAction.WasPressedThisFrame())
            {
                interactionController?.TryPlaceHeldObject();
            }

            if (throwAction.WasPressedThisFrame())
            {
                interactionController?.ThrowHeldObject();
            }

            if (toolAction.WasPressedThisFrame())
            {
                interactionController?.TryToolActivation();
            }
        }

        private void BindActions()
        {
            if (inputActions == null)
            {
                throw new InvalidOperationException("InputActionAsset reference is missing.");
            }

            playerMap = inputActions.FindActionMap(PlayerMapName, throwIfNotFound: false);
            if (playerMap == null)
            {
                throw new InvalidOperationException($"Action map '{PlayerMapName}' is missing.");
            }

            moveAction = RequireAction("Move");
            lookAction = RequireAction("Look");
            crouchAction = RequireAction("Crouch");
            interactAction = RequireAction("Interact");
            dropAction = RequireAction("Drop");
            placeAction = RequireAction("Place");
            throwAction = RequireAction("Throw");
            rotateModifierAction = RequireAction("RotateModifier");
            toolAction = RequireAction("ToolActivate");
        }

        private InputAction RequireAction(string actionName)
        {
            InputAction action = playerMap.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                throw new InvalidOperationException($"Action '{PlayerMapName}/{actionName}' is missing.");
            }

            return action;
        }
    }
}
