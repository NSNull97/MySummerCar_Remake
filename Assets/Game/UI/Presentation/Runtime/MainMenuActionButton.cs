using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Presentation-only uGUI button. State changes animate once using unscaled
    /// time; settled controls do not write graphics, transforms, or layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuActionButton : Button, IPointerMoveHandler
    {
        [SerializeField] private RectTransform animatedVisual;
        [SerializeField] private CanvasGroup visualOpacity;
        [SerializeField] private Image stateOverlay;
        [SerializeField] private Image borderGraphic;
        [SerializeField] private Image glowGraphic;
        [SerializeField] private Graphic iconGraphic;
        [SerializeField] private Text titleGraphic;
        [SerializeField] private Text helperGraphic;
        [SerializeField] private bool destructive;
        [SerializeField] private bool persistentSelection;
        [SerializeField] private bool colourSwatch;
        [SerializeField] private bool pageIndicator;
        [SerializeField] private bool reducedMotion;

        private bool pointerOver;
        private bool navigationSelected;
        private bool initialized;
        private bool animating;
        private float animationElapsed;
        private float animationDuration;
        private float submitReleaseAt = -1f;
        private VisualState current;
        private VisualState start;
        private VisualState target;

        public bool PersistentSelection => persistentSelection;

        public bool IsAnimating => animating || submitReleaseAt >= 0f;

        public bool ReducedMotion
        {
            get => reducedMotion;
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                RefreshVisualState(instant: true);
            }
        }

        public void RefreshVisualState(bool instant = false)
        {
            DoStateTransition(currentSelectionState, instant);
        }

        internal void Initialize(
            RectTransform visual,
            Image overlay,
            Image border,
            Image glow,
            Graphic icon,
            Text title,
            Text helper,
            bool isDestructive = false,
            bool isColourSwatch = false,
            bool isPageIndicator = false)
        {
            animatedVisual = visual;
            visualOpacity = visual.GetComponent<CanvasGroup>();
            if (visualOpacity == null) visualOpacity = visual.gameObject.AddComponent<CanvasGroup>();
            stateOverlay = overlay;
            borderGraphic = border;
            glowGraphic = glow;
            iconGraphic = icon;
            titleGraphic = title;
            helperGraphic = helper;
            destructive = isDestructive;
            colourSwatch = isColourSwatch;
            pageIndicator = isPageIndicator;
            transition = Transition.None;
            initialized = true;
            navigationSelected = EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
            DoStateTransition(currentSelectionState, true);
        }

        public void SetPersistentSelection(bool selected)
        {
            if (persistentSelection == selected)
            {
                return;
            }

            persistentSelection = selected;
            DoStateTransition(currentSelectionState, false);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            pointerOver = true;
            TransferPointerSelection(eventData);
            base.OnPointerEnter(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            GameObject hit = eventData?.pointerCurrentRaycast.gameObject;
            if (pointerOver && hit != null &&
                (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                TransferPointerSelection(eventData);
            }
        }

        private void TransferPointerSelection(PointerEventData eventData)
        {
            EventSystem eventSystem = EventSystem.current;
            bool pointerMoved = eventData != null && eventData.delta.sqrMagnitude > 0f;
            if (pointerMoved && IsActive() && IsInteractable() && eventSystem != null &&
                !eventSystem.alreadySelecting)
            {
                // Retain one logical anchor for the next navigation action,
                // without turning transient mouse hover into a sticky focus
                // outline. Stationary route-reveal events never take it over.
                bool wasNavigationSelected = navigationSelected;
                navigationSelected = false;
                if (eventSystem.currentSelectedGameObject != gameObject)
                {
                    eventSystem.SetSelectedGameObject(gameObject, eventData);
                }
                else if (wasNavigationSelected)
                {
                    RefreshVisualState();
                }
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            pointerOver = false;
            if (eventData != null && eventData.delta.sqrMagnitude > 0f)
            {
                navigationSelected = false;
            }
            base.OnPointerExit(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            navigationSelected = !(eventData is PointerEventData);
            base.OnSelect(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            navigationSelected = false;
            base.OnDeselect(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                IsActive() && IsInteractable())
            {
                navigationSelected = false;
            }
            base.OnPointerDown(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            navigationSelected = true;
            RefreshVisualState();
            base.OnMove(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            // Match Button's existing submit semantics without a coroutine
            // allocation. The onClick event retains the existing audio/action.
            navigationSelected = true;
            onClick.Invoke();
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            submitReleaseAt = Time.unscaledTime + MainMenuStyle.PressDurationSeconds;
            DoStateTransition(SelectionState.Pressed, false);
        }

        protected override void OnEnable()
        {
            // Serialized prefabs use the same references as factory-built views.
            initialized = animatedVisual != null;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            pointerOver = false;
            navigationSelected = false;
            submitReleaseAt = -1f;
            base.OnDisable();
            if (initialized)
            {
                DoStateTransition(SelectionState.Normal, true);
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!initialized || animatedVisual == null)
            {
                return;
            }

            bool available = IsInteractable();
            bool pressed = available && state == SelectionState.Pressed;
            bool focused = available && (navigationSelected || persistentSelection);
            bool hovered = available && pointerOver;
            Color accent = destructive ? MainMenuStyle.Destructive : MainMenuStyle.Accent;
            if (colourSwatch && !persistentSelection)
            {
                // White navigation focus is distinct from the orange colour
                // already applied to the vehicle; moving focus never repaints it.
                accent = MainMenuStyle.PrimaryText;
            }
            Color foreground = destructive ? MainMenuStyle.Destructive : MainMenuStyle.PrimaryText;
            Color border = focused ? accent : hovered ? MainMenuStyle.HoverBorder : MainMenuStyle.Border;
            if (colourSwatch && !focused && !hovered)
            {
                border.a = 0f;
            }

            target = new VisualState
            {
                Scale = reducedMotion ? 1f : pressed ? MainMenuStyle.PressedScale : hovered ? MainMenuStyle.HoverScale : 1f,
                Opacity = available ? 1f : 0.64f,
                Border = WithAlpha(border, available ? (pressed ? border.a * 0.7f : border.a) : 0.09f),
                Glow = WithAlpha(accent, focused ? (pressed ? 0.04f : 0.12f) : 0f),
                // Accent belongs to the edge and foreground. The glass keeps
                // its neutral blurred surface even while selected or pressed.
                Overlay = WithAlpha(MainMenuStyle.PrimaryText,
                    hovered ? (pressed ? 0.004f : 0.012f) : 0f),
                Foreground = WithAlpha(focused ? accent : foreground, available ? 1f : 0.43f),
                Helper = WithAlpha(MainMenuStyle.SecondaryText, available ? 1f : 0.59f),
            };
            if (pageIndicator)
            {
                target.Foreground = WithAlpha(
                    persistentSelection ? MainMenuStyle.Accent : MainMenuStyle.SecondaryText,
                    available ? (hovered || focused || persistentSelection ? 1f : 0.68f) : 0.35f);
            }

            start = current;
            instant |= reducedMotion;
            animationElapsed = 0f;
            animationDuration = pressed ? MainMenuStyle.PressDurationSeconds : MainMenuStyle.HoverDurationSeconds;
            animating = !instant;
            if (instant)
            {
                current = target;
                Apply(current);
            }
        }

        private void Update()
        {
            if (submitReleaseAt >= 0f && Time.unscaledTime >= submitReleaseAt)
            {
                submitReleaseAt = -1f;
                DoStateTransition(currentSelectionState, false);
            }

            if (!animating)
            {
                return;
            }

            animationElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(animationElapsed / animationDuration);
            float eased = progress * progress * (3f - 2f * progress);
            current = VisualState.Lerp(start, target, eased);
            Apply(current);
            if (progress >= 1f)
            {
                animating = false;
            }
        }

        private void Apply(VisualState state)
        {
            animatedVisual.localScale = Vector3.one * state.Scale;
            if (visualOpacity != null) visualOpacity.alpha = state.Opacity;
            if (stateOverlay != null) stateOverlay.color = state.Overlay;
            if (borderGraphic != null) borderGraphic.color = state.Border;
            if (glowGraphic != null) glowGraphic.color = state.Glow;
            if (iconGraphic != null) iconGraphic.color = state.Foreground;
            if (titleGraphic != null) titleGraphic.color = state.Foreground;
            if (helperGraphic != null) helperGraphic.color = state.Helper;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private struct VisualState
        {
            public float Scale;
            public float Opacity;
            public Color Border;
            public Color Glow;
            public Color Overlay;
            public Color Foreground;
            public Color Helper;

            public static VisualState Lerp(VisualState from, VisualState to, float amount)
            {
                return new VisualState
                {
                    Scale = Mathf.LerpUnclamped(from.Scale, to.Scale, amount),
                    Opacity = Mathf.LerpUnclamped(from.Opacity, to.Opacity, amount),
                    Border = Color.LerpUnclamped(from.Border, to.Border, amount),
                    Glow = Color.LerpUnclamped(from.Glow, to.Glow, amount),
                    Overlay = Color.LerpUnclamped(from.Overlay, to.Overlay, amount),
                    Foreground = Color.LerpUnclamped(from.Foreground, to.Foreground, amount),
                    Helper = Color.LerpUnclamped(from.Helper, to.Helper, amount),
                };
            }
        }
    }
}
