using UnityEngine;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
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

        public void ApplyLook(Vector2 lookInput, bool isPointerDelta, float deltaTime)
        {
            if (yawRoot == null || pitchPivot == null)
            {
                return;
            }

            float scale = isPointerDelta
                ? mouseSensitivityDegreesPerPixel
                : gamepadSpeedDegreesPerSecond * deltaTime;
            Vector2 degrees = lookInput * scale;

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
