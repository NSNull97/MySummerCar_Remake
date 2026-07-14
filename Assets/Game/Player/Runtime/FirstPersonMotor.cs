using UnityEngine;

namespace MSC.Player
{
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        private readonly Collider[] headroomBuffer = new Collider[12];

        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private Transform cameraPivot;

        [SerializeField, Min(0f)]
        private float walkingSpeedMetersPerSecond = 4.2f;

        [SerializeField, Min(0f)]
        private float crouchingSpeedMetersPerSecond = 2.1f;

        [SerializeField, Min(0.1f)]
        private float standingHeightMeters = 1.8f;

        [SerializeField, Min(0.1f)]
        private float crouchingHeightMeters = 1.15f;

        [SerializeField, Min(0.1f)]
        private float standingEyeHeightMeters = 1.68f;

        [SerializeField, Min(0.1f)]
        private float crouchingEyeHeightMeters = 1.02f;

        [SerializeField, Min(0f)]
        private float crouchTransitionMetersPerSecond = 4.5f;

        [SerializeField, Min(0f)]
        private float gravityMetersPerSecondSquared = 22f;

        [SerializeField]
        private LayerMask headroomMask = ~0;

        private Vector2 moveInput;
        private bool crouchRequested;
        private float verticalSpeed;

        public bool IsCrouching => characterController != null &&
            characterController.height < standingHeightMeters - 0.02f;

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetCrouchRequested(bool requested)
        {
            crouchRequested = requested;
        }

        public void ResetInputIntent()
        {
            moveInput = Vector2.zero;
            crouchRequested = false;
        }

        public void Configure(CharacterController controller, Transform pivot)
        {
            characterController = controller;
            cameraPivot = pivot;
        }

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            UpdateCrouch(Time.deltaTime);

            float speed = IsCrouching ? crouchingSpeedMetersPerSecond : walkingSpeedMetersPerSecond;
            Vector3 horizontal = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (horizontal.sqrMagnitude > 1f)
            {
                horizontal.Normalize();
            }

            if (characterController.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }
            else
            {
                verticalSpeed -= gravityMetersPerSecondSquared * Time.deltaTime;
            }

            Vector3 velocity = horizontal * speed + Vector3.up * verticalSpeed;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateCrouch(float deltaTime)
        {
            bool mayStand = crouchRequested || HasStandingHeadroom();
            float targetHeight = crouchRequested || !mayStand ? crouchingHeightMeters : standingHeightMeters;
            float targetEyeHeight = crouchRequested || !mayStand
                ? crouchingEyeHeightMeters
                : standingEyeHeightMeters;

            float height = Mathf.MoveTowards(
                characterController.height,
                targetHeight,
                crouchTransitionMetersPerSecond * deltaTime);
            characterController.height = height;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraPivot != null)
            {
                Vector3 localPosition = cameraPivot.localPosition;
                localPosition.y = Mathf.MoveTowards(
                    localPosition.y,
                    targetEyeHeight,
                    crouchTransitionMetersPerSecond * deltaTime);
                cameraPivot.localPosition = localPosition;
            }
        }

        private bool HasStandingHeadroom()
        {
            float radius = Mathf.Max(0.01f, characterController.radius * 0.95f);
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * (standingHeightMeters - radius);
            int count = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                headroomBuffer,
                headroomMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider overlap = headroomBuffer[i];
                if (overlap != null && overlap != characterController && !overlap.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
