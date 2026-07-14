using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.World.Debugging
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WorldTransferDebugFlyCamera : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeedMetersPerSecond = 30f;
        [SerializeField, Min(1f)] private float fastMultiplier = 5f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;

        private float pitch;

        private void OnEnable()
        {
            pitch = transform.eulerAngles.x;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input += Vector3.forward;
            if (keyboard.sKey.isPressed) input += Vector3.back;
            if (keyboard.aKey.isPressed) input += Vector3.left;
            if (keyboard.dKey.isPressed) input += Vector3.right;
            if (keyboard.eKey.isPressed) input += Vector3.up;
            if (keyboard.qKey.isPressed) input += Vector3.down;

            float speed = moveSpeedMetersPerSecond * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
            transform.position += transform.TransformDirection(input.normalized) * (speed * Time.unscaledDeltaTime);

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
                float yaw = transform.eulerAngles.y + delta.x;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
#endif
        }
    }
}
