using System;
using MSC.Core.Lifecycle;
using UnityEngine;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour, IPlayerLookSettingsSink
    {
        [SerializeField]
        private Transform yawRoot;

        [SerializeField]
        private Transform pitchPivot;

        [SerializeField, Min(0f)]
        private float mouseSensitivityDegreesPerPixel = 0.1f;

        [SerializeField, Min(0f)]
        private float gamepadSpeedDegreesPerSecond = 150f;

        [SerializeField, Range(1f, 89f)]
        private float pitchLimitDegrees = 80f;

        [SerializeField]
        private bool lockCursorOnEnable = true;

        private float pitchDegrees;
        private float mouseSensitivityMultiplier = 1f;
        private float gamepadSensitivityMultiplier = 1f;
        private bool invertMouseY;
        private bool invertGamepadY;

        public event Action<Vector2> LookApplied;

        public float PitchDegrees => pitchDegrees;

        public FirstPersonLookSaveDto CaptureSaveState()
        {
            return FirstPersonLookSaveDto.Create(pitchDegrees);
        }

        public bool CanRestoreSaveState(
            FirstPersonLookSaveDto state,
            out string failure)
        {
            if (state == null)
            {
                failure = "Player look state is missing.";
                return false;
            }

            if (!state.TryValidate(out failure))
            {
                return false;
            }

            if (yawRoot == null || pitchPivot == null)
            {
                failure = "Player look authoring references are incomplete.";
                return false;
            }

            if (Mathf.Abs(state.PitchDegrees) > pitchLimitDegrees + 0.001f)
            {
                failure = "Saved player pitch exceeds the authored look limit.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRestoreSaveState(
            FirstPersonLookSaveDto state,
            out string failure)
        {
            if (!CanRestoreSaveState(state, out failure))
            {
                return false;
            }

            pitchDegrees = state.PitchDegrees;
            pitchPivot.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            failure = string.Empty;
            return true;
        }

        public void ApplyLook(Vector2 lookInput, bool isPointerDelta, float deltaTime)
        {
            if (yawRoot == null || pitchPivot == null)
            {
                return;
            }

            ApplyRotationDegrees(CalculateRotationDegrees(
                lookInput,
                isPointerDelta,
                deltaTime));
        }

        public Vector2 CalculateRotationDegrees(
            Vector2 lookInput,
            bool isPointerDelta,
            float deltaTime)
        {
            Vector2 degrees = LookInputScaling.ToRotationDegrees(
                lookInput,
                isPointerDelta,
                mouseSensitivityDegreesPerPixel * mouseSensitivityMultiplier,
                gamepadSpeedDegreesPerSecond * gamepadSensitivityMultiplier,
                deltaTime);

            bool invertY = isPointerDelta ? invertMouseY : invertGamepadY;
            if (invertY)
            {
                degrees.y = -degrees.y;
            }

            return degrees;
        }

        public void ApplyRotationDegrees(Vector2 degrees)
        {
            if (yawRoot == null || pitchPivot == null ||
                !float.IsFinite(degrees.x) || !float.IsFinite(degrees.y))
            {
                return;
            }

            yawRoot.Rotate(0f, degrees.x, 0f, Space.World);
            pitchDegrees = Mathf.Clamp(pitchDegrees - degrees.y, -pitchLimitDegrees, pitchLimitDegrees);
            pitchPivot.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            LookApplied?.Invoke(degrees);
        }

        public void Configure(Transform root, Transform pivot, bool shouldLockCursor)
        {
            yawRoot = root;
            pitchPivot = pivot;
            lockCursorOnEnable = shouldLockCursor;
        }

        public void ApplyLookSettings(
            float configuredMouseSensitivityMultiplier,
            bool configuredInvertMouseY,
            float configuredGamepadSensitivityMultiplier,
            bool configuredInvertGamepadY)
        {
            mouseSensitivityMultiplier = Mathf.Clamp(
                configuredMouseSensitivityMultiplier,
                0.05f,
                10f);
            invertMouseY = configuredInvertMouseY;
            gamepadSensitivityMultiplier = Mathf.Clamp(
                configuredGamepadSensitivityMultiplier,
                0.05f,
                10f);
            invertGamepadY = configuredInvertGamepadY;
        }

        private void OnEnable()
        {
            if (lockCursorOnEnable)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnEnable && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
