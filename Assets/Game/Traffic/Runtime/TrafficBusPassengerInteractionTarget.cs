using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Player;
using UnityEngine;

namespace MSC.Traffic
{
    [DisallowMultipleComponent]
    public sealed class TrafficBusPassengerInteractionTarget : MonoBehaviour,
        IContextInteractionTarget
    {
        [SerializeField] private TrafficTransportPresentationBinding transport;

        private GameObject passenger;
        private Transform previousParent;
        private FirstPersonMotor motor;
        private CharacterController characterController;

        public string InteractionPrompt => passenger == null
            ? "Сесть в автобус"
            : "Выйти из автобуса";

        public void ConfigureForAuthoring(
            TrafficTransportPresentationBinding configuredTransport)
        {
            transport = configuredTransport;
            GetComponent<InteractionTargetHost>()?.Configure(this);
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (!enabled || !gameObject.activeInHierarchy ||
                transport == null || context.Interactor == null)
            {
                return false;
            }

            return passenger == null ||
                   context.Interactor == passenger ||
                   context.Interactor.transform.IsChildOf(passenger.transform);
        }

        public void Interact(in InteractionContext context)
        {
            if (passenger == null)
            {
                Board(context.Interactor);
            }
            else
            {
                Exit();
            }
        }

        private void Board(GameObject interactor)
        {
            FirstPersonMotor foundMotor = interactor.GetComponentInParent<
                FirstPersonMotor>() ?? interactor.GetComponentInChildren<
                FirstPersonMotor>(true);
            GameObject root = foundMotor != null
                ? foundMotor.gameObject
                : interactor.transform.root.gameObject;
            passenger = root;
            previousParent = root.transform.parent;
            motor = foundMotor;
            characterController = root.GetComponent<CharacterController>();
            motor?.ResetInputIntent();
            if (motor != null)
            {
                motor.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            root.transform.SetParent(transport.PassengerSeat, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            transport.PassengerAboard = true;
        }

        private void Exit()
        {
            if (passenger == null)
            {
                return;
            }

            Transform root = passenger.transform;
            root.SetParent(previousParent, true);
            root.SetPositionAndRotation(
                transport.PassengerExit.position,
                transport.PassengerExit.rotation);
            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (motor != null)
            {
                motor.enabled = true;
            }

            transport.PassengerAboard = false;
            passenger = null;
            previousParent = null;
            motor = null;
            characterController = null;
        }

        private void OnDisable()
        {
            if (passenger != null)
            {
                Exit();
            }
        }
    }
}
