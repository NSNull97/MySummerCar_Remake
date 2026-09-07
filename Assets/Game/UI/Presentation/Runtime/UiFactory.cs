using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    internal sealed partial class UiFactory
    {
        private readonly UiVisualAssets assets;
        private readonly UnityAction confirmAudio;
        private RectTransform glassReferenceFrame;
        private Action<UiGlassSurface> glassRegistrar;

        public UiFactory(UiVisualAssets visualAssets, UnityAction configuredConfirmAudio = null)
        {
            assets = visualAssets ?? throw new ArgumentNullException(nameof(visualAssets));
            confirmAudio = configuredConfirmAudio;
        }

        public void ConfigureGlass(
            RectTransform configuredReferenceFrame,
            Action<UiGlassSurface> configuredRegistrar)
        {
            glassReferenceFrame = configuredReferenceFrame ??
                throw new ArgumentNullException(nameof(configuredReferenceFrame));
            glassRegistrar = configuredRegistrar ??
                throw new ArgumentNullException(nameof(configuredRegistrar));
        }

        public GameObject CreateObject(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, worldPositionStays: false);
            return value;
        }

        public RectTransform Stretch(GameObject value)
        {
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        public RectTransform Place(
            GameObject value,
            float x,
            float y,
            float width,
            float height)
        {
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            return rect;
        }

        public Image AddSurface(
            GameObject value,
            Color color,
            bool rounded = true)
        {
            Image image = value.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = assets.RoundedSprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public GameObject Panel(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            Color? color = null)
        {
            GameObject panel = CreateObject(name, parent);
            Place(panel, x, y, width, height);
            AddSurface(panel, color ?? UiThemeTokens.Panel);
            AddBorderLayer(panel, UiThemeTokens.Border);
            return panel;
        }

        public GameObject GlassPanel(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            UiGlassKind kind,
            Color? fallbackTint = null)
        {
            GameObject panel = CreateObject(name, parent);
            Place(panel, x, y, width, height);
            AddGlassSurface(
                panel,
                kind,
                fallbackTint ?? UiThemeTokens.MenuGlassNeutral,
                raycastTarget: false);
            AddBorderLayer(
                panel,
                kind == UiGlassKind.HudDark
                    ? UiThemeTokens.HudBorder
                    : UiThemeTokens.Border);
            return panel;
        }

        public Text Text(
            string name,
            Transform parent,
            string value,
            float x,
            float y,
            float width,
            float height,
            int size = UiThemeTokens.RowSize,
            Color? color = null,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            GameObject textObject = CreateObject(name, parent);
            Place(textObject, x, y, width, height);
            Text text = textObject.AddComponent<Text>();
            text.font = assets.TextFont;
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color ?? UiThemeTokens.TextPrimary;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public Image Icon(
            string name,
            Transform parent,
            UiIconKind kind,
            float x,
            float y,
            float size,
            Color? color = null)
        {
            GameObject iconObject = CreateObject(name, parent);
            Place(iconObject, x, y, size, size);
            Image image = iconObject.AddComponent<Image>();
            image.sprite = assets.GetIcon(kind);
            image.color = color ?? UiThemeTokens.TextPrimary;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public Button Button(
            string name,
            Transform parent,
            string label,
            UiIconKind icon,
            float x,
            float y,
            float width,
            float height,
            UnityAction onClick,
            bool selected = false,
            bool destructive = false,
            bool interactable = true,
            string helper = "",
            bool glass = false)
        {
            GameObject buttonObject = CreateObject(name, parent);
            Place(buttonObject, x, y, width, height);
            Image background = glass
                ? AddGlassSurface(
                    buttonObject,
                    UiGlassKind.MenuTinted,
                    UiThemeTokens.MenuGlassNeutral,
                    raycastTarget: true)
                : AddSurface(
                    buttonObject,
                    selected ? UiThemeTokens.AccentSoft : UiThemeTokens.Card);
            Color buttonOutline = selected
                ? UiThemeTokens.Accent
                : UiThemeTokens.Border;
            buttonOutline.a = selected
                ? UiThemeTokens.EmphasizedBorderAlpha
                : UiThemeTokens.Border.a;
            AddBorderLayer(buttonObject, buttonOutline);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = UiThemeTokens.ControlNormal;
            // The selected navigation entry already owns an amber surface and
            // outline. Multiplying that surface by another saturated amber made
            // both its icon and label disappear in keyboard/controller focus.
            colors.highlightedColor = UiThemeTokens.ActionHighlighted;
            colors.pressedColor = UiThemeTokens.ActionPressed;
            colors.selectedColor = UiThemeTokens.ActionSelected;
            colors.disabledColor = UiThemeTokens.ControlDisabled;
            colors.fadeDuration = UiThemeTokens.TransitionFastSeconds;
            button.colors = colors;
            if (onClick != null)
            {
                if (confirmAudio != null)
                {
                    button.onClick.AddListener(confirmAudio);
                }

                button.onClick.AddListener(onClick);
            }

            Color foreground = !interactable
                ? UiThemeTokens.Disabled
                : destructive
                    ? UiThemeTokens.Destructive
                    : selected
                        ? UiThemeTokens.Accent
                        : UiThemeTokens.TextPrimary;
            bool compactWidth = width < 180f;
            bool narrowWidth = width < 250f;
            float iconX = compactWidth ? 13f : UiThemeTokens.SpacingLarge;
            float iconSize = compactWidth
                ? UiThemeTokens.IconSizeCompact
                : UiThemeTokens.IconSizeStandard;
            float labelX = compactWidth ? 52f : narrowWidth ? 60f : 68f;
            float labelRightPadding = compactWidth
                ? UiThemeTokens.SpacingCompact
                : UiThemeTokens.SpacingStandard;
            int labelSize = narrowWidth ? 18 : UiThemeTokens.ActionSize;
            Icon(
                name + "Icon",
                buttonObject.transform,
                icon,
                iconX,
                UiThemeTokens.SpacingLarge,
                iconSize,
                foreground);
            Text(
                name + "Label",
                buttonObject.transform,
                label,
                labelX,
                string.IsNullOrEmpty(helper) ? 0f : UiThemeTokens.SpacingSmall,
                width - labelX - labelRightPadding,
                string.IsNullOrEmpty(helper) ? height : UiThemeTokens.RowHeightComfortable,
                labelSize,
                foreground,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            if (!string.IsNullOrEmpty(helper))
            {
                Text(
                    name + "Helper",
                    buttonObject.transform,
                    helper,
                    68f,
                    38f,
                    width - 82f,
                    height - 42f,
                    UiThemeTokens.CaptionSize,
                    interactable ? UiThemeTokens.TextMuted : UiThemeTokens.Disabled,
                    TextAnchor.UpperLeft);
            }

            return button;
        }

        private Image AddGlassSurface(
            GameObject value,
            UiGlassKind kind,
            Color fallbackTint,
            bool raycastTarget)
        {
            if (glassReferenceFrame == null || glassRegistrar == null)
            {
                throw new InvalidOperationException(
                    "UiFactory glass rendering must be configured after the reference frame is created.");
            }

            Image maskGraphic = AddSurface(value, Color.white);
            maskGraphic.raycastTarget = raycastTarget;
            var mask = value.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject backdropObject = CreateObject("BackdropSlice", value.transform);
            Stretch(backdropObject);
            var backdrop = backdropObject.AddComponent<RawImage>();
            backdrop.color = Color.white;
            backdrop.raycastTarget = false;
            backdrop.enabled = false;

            GameObject tintObject = CreateObject("GlassTint", value.transform);
            Stretch(tintObject);
            Image tint = AddSurface(tintObject, fallbackTint);
            tint.raycastTarget = raycastTarget;

            UiGlassSurface surface = value.AddComponent<UiGlassSurface>();
            surface.Initialize(kind, glassReferenceFrame, backdrop, tint);
            glassRegistrar(surface);
            return tint;
        }

        public Button CompactButton(
            string name,
            Transform parent,
            string label,
            float x,
            float y,
            float width,
            float height,
            UnityAction onClick,
            bool primary = false,
            bool destructive = false,
            bool interactable = true,
            bool glass = false)
        {
            GameObject buttonObject = CreateObject(name, parent);
            Place(buttonObject, x, y, width, height);
            Image background = glass
                ? AddGlassSurface(
                    buttonObject,
                    UiGlassKind.MenuTinted,
                    UiThemeTokens.MenuGlassNeutral,
                    raycastTarget: true)
                : AddSurface(
                    buttonObject,
                    primary ? UiThemeTokens.Accent : UiThemeTokens.Card);
            Color buttonBorder = primary
                ? UiThemeTokens.Accent
                : UiThemeTokens.Border;
            buttonBorder.a = primary
                ? UiThemeTokens.EmphasizedBorderAlpha
                : UiThemeTokens.Border.a;
            AddBorderLayer(buttonObject, buttonBorder);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = UiThemeTokens.ControlNormal;
            colors.highlightedColor = UiThemeTokens.ActionHighlighted;
            colors.pressedColor = UiThemeTokens.ActionPressed;
            colors.selectedColor = UiThemeTokens.ActionSelected;
            colors.disabledColor = UiThemeTokens.ControlDisabled;
            colors.fadeDuration = UiThemeTokens.TransitionFastSeconds;
            button.colors = colors;
            if (onClick != null)
            {
                if (confirmAudio != null)
                {
                    button.onClick.AddListener(confirmAudio);
                }

                button.onClick.AddListener(onClick);
            }

            Color foreground = !interactable
                ? UiThemeTokens.Disabled
                : destructive
                    ? UiThemeTokens.Destructive
                    : primary
                        ? UiThemeTokens.PrimaryControlForeground
                        : UiThemeTokens.TextPrimary;
            Text(
                name + "Label",
                buttonObject.transform,
                label,
                UiThemeTokens.SpacingCompact,
                0f,
                width - UiThemeTokens.SpacingCompact * 2f,
                height,
                Mathf.RoundToInt(Mathf.Clamp(height * 0.38f, 11f, 18f)),
                foreground,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            return button;
        }

        private Image AddBorderLayer(GameObject value, Color color)
        {
            GameObject borderObject = CreateObject("Border", value.transform);
            Stretch(borderObject);
            Image border = borderObject.AddComponent<Image>();
            border.sprite = assets.RoundedBorderSprite;
            border.type = Image.Type.Sliced;
            border.color = color;
            border.raycastTarget = false;
            return border;
        }

        public Text Heading(
            Transform parent,
            string value,
            float x,
            float y,
            float width,
            int size = UiThemeTokens.HeadingSize)
        {
            return Text(
                value + "Heading",
                parent,
                value,
                x,
                y,
                width,
                size + UiThemeTokens.SpacingStandard,
                size,
                UiThemeTokens.Accent,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
        }

        public GameObject Divider(
            Transform parent,
            float x,
            float y,
            float width,
            Color? color = null)
        {
            GameObject value = CreateObject("Divider", parent);
            Place(value, x, y, width, 1f);
            AddSurface(value, color ?? UiThemeTokens.Border, rounded: false);
            return value;
        }

        public Slider Slider(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float value,
            UnityAction<float> onChanged,
            bool interactable = true)
        {
            GameObject sliderObject = CreateObject(name, parent);
            Place(sliderObject, x, y, width, UiThemeTokens.RowHeightDense);
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);
            slider.interactable = interactable;

            GameObject track = CreateObject("Track", sliderObject.transform);
            RectTransform trackRect = Place(
                track,
                0f,
                UiThemeTokens.SpacingSmall,
                width,
                6f);
            Image trackImage = AddSurface(track, UiThemeTokens.SliderTrack);
            trackImage.sprite = assets.TrackSprite;

            GameObject fillArea = CreateObject("Fill Area", sliderObject.transform);
            Place(fillArea, 0f, UiThemeTokens.SpacingSmall, width, 6f);
            GameObject fill = CreateObject("Fill", fillArea.transform);
            RectTransform fillRect = Stretch(fill);
            fillRect.offsetMax = new Vector2(-6f, 0f);
            Image fillImage = AddSurface(
                fill,
                interactable ? UiThemeTokens.Accent : UiThemeTokens.Disabled);
            fillImage.sprite = assets.TrackSprite;

            GameObject handleArea = CreateObject("Handle Slide Area", sliderObject.transform);
            const float handleDiameter = 16f;
            Place(
                handleArea,
                0f,
                (UiThemeTokens.RowHeightDense - handleDiameter) * 0.5f,
                width,
                handleDiameter);
            GameObject handle = CreateObject("Handle", handleArea.transform);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            // Slider owns the horizontal anchors and stretches the handle over
            // its slide area's height. A 16 px-high area plus zero vertical
            // size delta therefore keeps the rendered knob exactly circular.
            handleRect.anchorMin = new Vector2(0.5f, 0f);
            handleRect.anchorMax = new Vector2(0.5f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(handleDiameter, 0f);
            Image handleImage = AddSurface(
                handle,
                interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled);
            handleImage.sprite = assets.CircleSprite;
            handleImage.type = Image.Type.Simple;
            handleImage.preserveAspect = true;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            if (onChanged != null)
            {
                slider.onValueChanged.AddListener(onChanged);
            }

            return slider;
        }

        public Toggle Toggle(
            string name,
            Transform parent,
            float x,
            float y,
            bool value,
            UnityAction<bool> onChanged,
            bool interactable = true)
        {
            GameObject toggleObject = CreateObject(name, parent);
            Place(toggleObject, x, y, 70f, UiThemeTokens.RowHeightStandard);
            Image background = AddSurface(toggleObject, UiThemeTokens.Row);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.interactable = interactable;

            GameObject mark = CreateObject("Mark", toggleObject.transform);
            Place(
                mark,
                value ? 39f : 3f,
                3f,
                UiThemeTokens.IconSizeCompact,
                UiThemeTokens.RowHeightDense);
            Image markImage = AddSurface(
                mark,
                interactable ? UiThemeTokens.Accent : UiThemeTokens.Disabled);
            toggle.graphic = markImage;
            toggle.isOn = value;
            Text(
                "On",
                toggleObject.transform,
                "ON",
                UiThemeTokens.SpacingXSmall,
                0f,
                31f,
                UiThemeTokens.RowHeightStandard,
                10,
                value ? UiThemeTokens.TextPrimary : UiThemeTokens.TextMuted,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Text(
                "Off",
                toggleObject.transform,
                "OFF",
                36f,
                0f,
                31f,
                UiThemeTokens.RowHeightStandard,
                10,
                value ? UiThemeTokens.TextMuted : UiThemeTokens.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            if (onChanged != null)
            {
                toggle.onValueChanged.AddListener(onChanged);
            }

            toggle.onValueChanged.AddListener(isOn =>
            {
                Place(
                    mark,
                    isOn ? 39f : 3f,
                    3f,
                    UiThemeTokens.IconSizeCompact,
                    UiThemeTokens.RowHeightDense);
            });
            return toggle;
        }

        public void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        public static void SelectFirst(GameObject root)
        {
            if (root == null || EventSystem.current == null)
            {
                return;
            }

            Selectable[] candidates = root.GetComponentsInChildren<Selectable>(false);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(candidates[index].gameObject);
                    return;
                }
            }
        }
    }
}
