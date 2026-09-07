using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MSC.UI.Presentation
{
    /// <summary>Event-driven orbit input owned by the menu background, never by gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuOrbitDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Action<Vector2> orbit;
        private Action resetOrbit;
        private Func<bool> canInteract;
        private int pressedPointerId = int.MinValue;
        private bool pointerPressed;
        private bool dragging;
        private bool hasFocus = true;
        private bool hasClickCandidate;

        public bool IsDragging => dragging;

        public void Initialize(Action<Vector2> onOrbit, Action onResetOrbit, Func<bool> isInteractionAllowed)
        {
            orbit = onOrbit ?? throw new ArgumentNullException(nameof(onOrbit));
            resetOrbit = onResetOrbit ?? throw new ArgumentNullException(nameof(onResetOrbit));
            canInteract = isInteractionAllowed ?? throw new ArgumentNullException(nameof(isInteractionAllowed));
            CancelDrag();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            CancelDrag();
            if (!CanUseBackground(eventData)) return;
            pressedPointerId = eventData.pointerId;
            pointerPressed = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!OwnsPressedPointer(eventData) || !CanUseBackground(eventData) ||
                eventData.pointerPressRaycast.gameObject != gameObject)
            {
                CancelDrag();
                return;
            }
            dragging = true;
            hasClickCandidate = false;
            eventData.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || !OwnsPressedPointer(eventData) || !CanUseBackground(eventData))
            {
                CancelDrag();
                return;
            }
            if (eventData.delta.sqrMagnitude > 0f)
                orbit(eventData.delta / Mathf.Max(1f, Screen.height));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (OwnsPressedPointer(eventData)) CancelDrag();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (OwnsPressedPointer(eventData)) CancelDrag();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Crossing a UI control cancels ownership; returning while still
            // holding LMB cannot resume a drag through the controls.
            if (OwnsPressedPointer(eventData)) CancelDrag();
            hasClickCandidate = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                eventData.dragging || !CanUseBackground(eventData) ||
                eventData.pointerPressRaycast.gameObject != gameObject) return;
            bool doubleClick = eventData.clickCount == 2 && hasClickCandidate;
            hasClickCandidate = !doubleClick;
            if (doubleClick) resetOrbit();
        }

        public void CancelDrag()
        {
            pointerPressed = false;
            dragging = false;
            pressedPointerId = int.MinValue;
        }

        public void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (focused) return;
            CancelDrag();
            hasClickCandidate = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            CancelDrag();
            hasClickCandidate = false;
        }

        private void OnEnable()
        {
            hasFocus = Application.isFocused;
        }

        private void OnDisable()
        {
            CancelDrag();
            hasClickCandidate = false;
        }

        private bool OwnsPressedPointer(PointerEventData eventData) => pointerPressed &&
            eventData != null && eventData.button == PointerEventData.InputButton.Left &&
            eventData.pointerId == pressedPointerId;

        private bool CanUseBackground(PointerEventData eventData) => isActiveAndEnabled && hasFocus &&
            canInteract != null && canInteract() && eventData.pointerCurrentRaycast.gameObject == gameObject;
    }
}
