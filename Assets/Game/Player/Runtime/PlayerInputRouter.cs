using System;
using MSC.Core.Lifecycle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Player
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour, IGameplayInputGate
    {
        private const string PlayerMapName = "Player";

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private FirstPersonMotor motor;

        [SerializeField]
        private FirstPersonLook firstPersonLook;

        [SerializeField]
        private FirstPersonCameraFieldOfView cameraFieldOfView;

        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField, Min(0f)]
        private float heldRotationDegreesPerNotch = 6f;

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction crouchAction;
        private InputAction runAction;
        private InputAction jumpAction;
        private InputAction zoomAction;
        private InputAction forwardLeanAction;
        private InputAction interactAction;
        private InputAction dropAction;
        private InputAction placeAction;
        private InputAction throwAction;
        private InputAction rotateModifierAction;
        private InputAction rotateAxisAction;
        private InputAction toolAction;
        private InputAction urinateAction;
        private InputAction waveAction;
        private InputAction middleFingerAction;
        private InputAction swearAction;
        private InputAction alternativeActionsAction;
        private BindingDisplay interactBindingDisplay;
        private BindingDisplay throwBindingDisplay;
        private BindingDisplay toolBindingDisplay;
        private BindingDisplay scrollBindingDisplay;
        private BindingDisplay waveBindingDisplay;
        private BindingDisplay middleFingerBindingDisplay;
        private BindingDisplay swearBindingDisplay;
        private int bindingDisplayRevision;

        public event Action UrinationRequested;

        public event Action WaveRequested;

        public event Action MiddleFingerRequested;

        public event Action SwearRequested;

        public InputActionAsset InputActions => inputActions;

        public bool IsAlternativeActionsHeld =>
            alternativeActionsAction != null &&
            alternativeActionsAction.IsPressed();

        public int BindingDisplayRevision => bindingDisplayRevision;

        public bool IsReady => playerMap != null;

        public bool IsGameplayInputEnabled =>
            enabled && playerMap != null && playerMap.enabled;

        public void SetGameplayInputEnabled(bool value)
        {
            enabled = value;
        }

        public void Configure(
            InputActionAsset actions,
            FirstPersonMotor movement,
            FirstPersonLook look,
            PlayerInteractionController interaction)
        {
            Configure(actions, movement, look, null, interaction);
        }

        public void Configure(
            InputActionAsset actions,
            FirstPersonMotor movement,
            FirstPersonLook look,
            FirstPersonCameraFieldOfView fieldOfView,
            PlayerInteractionController interaction)
        {
            inputActions = actions;
            motor = movement;
            firstPersonLook = look;
            cameraFieldOfView = fieldOfView;
            interactionController = interaction;
        }

        private void OnEnable()
        {
            try
            {
                BindActions();
                RefreshBindingDisplays();
                InputSystem.onActionChange -= HandleActionChange;
                InputSystem.onActionChange += HandleActionChange;
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
            InputSystem.onActionChange -= HandleActionChange;
            interactionController?.EndPrimaryInteraction();
            interactionController?.EndSecondaryInteraction();
            interactionController?.EndToolActivation();
            cameraFieldOfView?.SetZoomRequested(false);
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
            motor?.SetRunRequested(runAction.IsPressed());
            motor?.SetForwardLeanRequested(forwardLeanAction.IsPressed());
            if (crouchAction.WasPressedThisFrame())
            {
                motor?.CyclePosture();
            }

            if (jumpAction.WasPressedThisFrame())
            {
                motor?.RequestJump();
            }

            cameraFieldOfView?.SetZoomRequested(zoomAction.IsPressed());

            Vector2 lookValue = lookAction.ReadValue<Vector2>();
            bool isPointerDelta = lookAction.activeControl?.device is Pointer;
            if (firstPersonLook != null)
            {
                bool freelyRotatingHeldObject =
                    interactionController != null &&
                    interactionController.HasHeldObject &&
                    rotateAxisAction.IsPressed();
                if (freelyRotatingHeldObject)
                {
                    Vector2 rotationDegrees =
                        firstPersonLook.CalculateRotationDegrees(
                            lookValue,
                            isPointerDelta,
                            Time.unscaledDeltaTime);
                    interactionController.RotateHeldObject(rotationDegrees);
                }
                else
                {
                    firstPersonLook.ApplyLook(
                        lookValue,
                        isPointerDelta,
                        Time.unscaledDeltaTime);
                }
            }

            float scrollRotation = rotateModifierAction.ReadValue<float>();
            if (interactionController != null &&
                Mathf.Abs(scrollRotation) > 0.001f)
            {
                float scrollNotches = Mathf.Abs(scrollRotation) >= 10f
                    ? scrollRotation / 120f
                    : scrollRotation;
                if (interactionController.IsFirstPersonToolModeActive)
                {
                    interactionController.TryOperateCurrentHeldTool(
                        scrollNotches);
                }
                else if (interactionController.HasHeldObject)
                {
                    interactionController.RotateHeldObject(
                        scrollNotches * heldRotationDegreesPerNotch);
                }
                else if (rotateModifierAction.activeControl?.device is Pointer)
                {
                    interactionController.TryAdjustCurrentInteraction(
                        scrollNotches);
                }
            }

            if (interactAction.WasPressedThisFrame())
            {
                interactionController?.TryBeginPrimaryInteraction();
            }

            if (interactAction.IsPressed())
            {
                interactionController?.ContinuePrimaryInteraction(
                    Time.deltaTime);
            }

            if (interactAction.WasReleasedThisFrame())
            {
                interactionController?.EndPrimaryInteraction();
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
                interactionController?.TryBeginSecondaryInteraction();
            }

            if (throwAction.IsPressed())
            {
                interactionController?.ContinueSecondaryInteraction(
                    Time.deltaTime);
            }

            if (throwAction.WasReleasedThisFrame())
            {
                interactionController?.EndSecondaryInteraction();
            }

            if (toolAction.WasPressedThisFrame())
            {
                interactionController?.TryBeginToolActivation();
            }

            if (toolAction.IsPressed())
            {
                interactionController?.ContinueToolActivation(
                    Time.unscaledDeltaTime);
            }

            if (toolAction.WasReleasedThisFrame())
            {
                interactionController?.EndToolActivation();
            }

            if (urinateAction.WasPressedThisFrame())
            {
                UrinationRequested?.Invoke();
            }

            if (waveAction.WasPressedThisFrame())
            {
                WaveRequested?.Invoke();
            }

            if (middleFingerAction.WasPressedThisFrame())
            {
                MiddleFingerRequested?.Invoke();
            }

            if (swearAction.WasPressedThisFrame())
            {
                SwearRequested?.Invoke();
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
            runAction = RequireAction("Run");
            jumpAction = RequireAction("Jump");
            zoomAction = RequireAction("Zoom");
            forwardLeanAction = RequireAction("ForwardLean");
            interactAction = RequireAction("Interact");
            dropAction = RequireAction("Drop");
            placeAction = RequireAction("Place");
            throwAction = RequireAction("Throw");
            rotateModifierAction = RequireAction("RotateModifier");
            rotateAxisAction = RequireAction("RotateAxis");
            toolAction = RequireAction("ToolActivate");
            urinateAction = RequireAction("Urinate");
            waveAction = RequireAction("Wave");
            middleFingerAction = RequireAction("MiddleFinger");
            swearAction = RequireAction("Swear");
            alternativeActionsAction = RequireAction("AlternativeActions");
        }

        public string GetBindingDisplayLabel(string actionName)
        {
            BindingDisplay display = GetBindingDisplay(actionName);
            return display.Label;
        }

        public bool BindingUsesMouse(string actionName)
        {
            BindingDisplay display = GetBindingDisplay(actionName);
            return display.GlyphKind == InteractionBindingGlyphKind.MouseGeneric ||
                   display.GlyphKind == InteractionBindingGlyphKind.MouseLeftButton ||
                   display.GlyphKind == InteractionBindingGlyphKind.MouseRightButton ||
                   display.GlyphKind == InteractionBindingGlyphKind.MouseMiddleButton ||
                   display.GlyphKind == InteractionBindingGlyphKind.MouseWheelScroll;
        }

        public InteractionBindingGlyphKind GetBindingGlyphKind(
            string actionName) => GetBindingDisplay(actionName).GlyphKind;

        public static string FormatBindingDisplayLabel(
            string effectivePath,
            string humanReadable = null)
        {
            if (string.IsNullOrWhiteSpace(effectivePath))
            {
                return humanReadable?.Trim() ?? string.Empty;
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/leftButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "ЛКМ";
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/rightButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "ПКМ";
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/middleButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "СКМ";
            }

            if (effectivePath.StartsWith(
                    "<Mouse>/scroll",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "КОЛЕСО";
            }

            const string keyboardPrefix = "<Keyboard>/";
            if (effectivePath.StartsWith(
                keyboardPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                string control = effectivePath.Substring(keyboardPrefix.Length);
                if (control.Length == 1)
                {
                    return control.ToUpperInvariant();
                }

                return control switch
                {
                    "leftAlt" => "ALT",
                    "rightAlt" => "ALT",
                    "alt" => "ALT",
                    "leftShift" => "SHIFT",
                    "rightShift" => "SHIFT",
                    "leftCtrl" => "CTRL",
                    "rightCtrl" => "CTRL",
                    "space" => "ПРОБЕЛ",
                    "enter" => "ENTER",
                    "escape" => "ESC",
                    _ => string.IsNullOrWhiteSpace(humanReadable)
                        ? control.ToUpperInvariant()
                        : humanReadable.Trim().ToUpperInvariant(),
                };
            }

            return string.IsNullOrWhiteSpace(humanReadable)
                ? effectivePath
                : humanReadable.Trim().ToUpperInvariant();
        }

        public static InteractionBindingGlyphKind ResolveBindingGlyphKind(
            string effectivePath)
        {
            if (string.IsNullOrWhiteSpace(effectivePath))
            {
                return InteractionBindingGlyphKind.None;
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/leftButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return InteractionBindingGlyphKind.MouseLeftButton;
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/rightButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return InteractionBindingGlyphKind.MouseRightButton;
            }

            if (string.Equals(
                    effectivePath,
                    "<Mouse>/middleButton",
                    StringComparison.OrdinalIgnoreCase))
            {
                return InteractionBindingGlyphKind.MouseMiddleButton;
            }

            if (effectivePath.StartsWith(
                    "<Mouse>/scroll",
                    StringComparison.OrdinalIgnoreCase))
            {
                return InteractionBindingGlyphKind.MouseWheelScroll;
            }

            if (effectivePath.StartsWith(
                    "<Mouse>/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return InteractionBindingGlyphKind.MouseGeneric;
            }

            return InteractionBindingGlyphKind.Keycap;
        }

        private void HandleActionChange(
            object changedObject,
            InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged ||
                playerMap == null)
            {
                return;
            }

            if (ReferenceEquals(changedObject, inputActions) ||
                ReferenceEquals(changedObject, playerMap) ||
                changedObject is InputAction changedAction &&
                ReferenceEquals(changedAction.actionMap, playerMap))
            {
                RefreshBindingDisplays();
            }
        }

        private void RefreshBindingDisplays()
        {
            interactBindingDisplay = CreateBindingDisplay(interactAction);
            throwBindingDisplay = CreateBindingDisplay(throwAction);
            toolBindingDisplay = CreateBindingDisplay(toolAction);
            scrollBindingDisplay = CreateBindingDisplay(rotateModifierAction);
            waveBindingDisplay = CreateBindingDisplay(waveAction);
            middleFingerBindingDisplay =
                CreateBindingDisplay(middleFingerAction);
            swearBindingDisplay = CreateBindingDisplay(swearAction);
            bindingDisplayRevision++;
        }

        private BindingDisplay GetBindingDisplay(string actionName)
        {
            return actionName switch
            {
                "Interact" => interactBindingDisplay,
                "Throw" => throwBindingDisplay,
                "ToolActivate" => toolBindingDisplay,
                "RotateModifier" => scrollBindingDisplay,
                "Wave" => waveBindingDisplay,
                "MiddleFinger" => middleFingerBindingDisplay,
                "Swear" => swearBindingDisplay,
                _ => default,
            };
        }

        private static BindingDisplay CreateBindingDisplay(InputAction action)
        {
            if (action == null)
            {
                return default;
            }

            int fallbackIndex = -1;
            for (int index = 0; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (binding.isComposite || binding.isPartOfComposite ||
                    string.IsNullOrWhiteSpace(binding.effectivePath))
                {
                    continue;
                }

                if (fallbackIndex < 0)
                {
                    fallbackIndex = index;
                }

                if (!string.IsNullOrEmpty(binding.groups) &&
                    binding.groups.IndexOf(
                        "Keyboard&Mouse",
                        StringComparison.Ordinal) >= 0)
                {
                    return CreateBindingDisplay(action, index);
                }
            }

            return fallbackIndex >= 0
                ? CreateBindingDisplay(action, fallbackIndex)
                : default;
        }

        private static BindingDisplay CreateBindingDisplay(
            InputAction action,
            int bindingIndex)
        {
            string effectivePath =
                action.bindings[bindingIndex].effectivePath;
            string humanReadable =
                action.GetBindingDisplayString(bindingIndex);
            return new BindingDisplay(
                FormatBindingDisplayLabel(
                    effectivePath,
                    humanReadable),
                ResolveBindingGlyphKind(effectivePath));
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

        private readonly struct BindingDisplay
        {
            public BindingDisplay(
                string label,
                InteractionBindingGlyphKind glyphKind)
            {
                Label = label ?? string.Empty;
                GlyphKind = glyphKind;
            }

            public string Label { get; }

            public InteractionBindingGlyphKind GlyphKind { get; }
        }
    }
}
