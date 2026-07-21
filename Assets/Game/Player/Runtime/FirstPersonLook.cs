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
        private float mouseSensitivityDegreesPerPixel = 0.08f;

        [SerializeField, Min(0f)]
        private float gamepadSpeedDegreesPerSecond = 150f;

        [SerializeField, Range(1f, 89f)]
        private float pitchLimitDegrees = 85f;

        [SerializeField]
        private bool lockCursorOnEnable = true;

        private float pitchDegrees;
        private float mouseSensitivityMultiplier = 1f;
        private float gamepadSensitivityMultiplier = 1f;
        private bool invertMouseY;
        private bool invertGamepadY;

        public void ApplyLook(Vector2 lookInput, bool isPointerDelta, float deltaTime)
        {
            if (yawRoot == null || pitchPivot == null)
            {
                return;
            }

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

            yawRoot.Rotate(0f, degrees.x, 0f, Space.World);
            pitchDegrees = Mathf.Clamp(pitchDegrees - degrees.y, -pitchLimitDegrees, pitchLimitDegrees);
            pitchPivot.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
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
