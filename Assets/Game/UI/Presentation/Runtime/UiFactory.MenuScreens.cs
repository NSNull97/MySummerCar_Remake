using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    internal sealed partial class UiFactory
    {
        // Opt-in presentation for the settings screens. Existing factory
        // defaults remain available to HUD, notices and other established UI.
        public MainMenuActionButton MenuScreenButton(
            string name, Transform parent, string label, UiIconKind icon,
            float x, float y, float width, float height, UnityAction onClick,
            bool selected = false, bool destructive = false)
        {
            MainMenuActionButton button = MainMenuButton(
                name, parent, label, icon, x, y, width, height, onClick,
                destructive: destructive, compact: true);
            button.GetComponentInChildren<Text>(includeInactive: true).fontSize = 18;
            button.SetPersistentSelection(selected);
            button.RefreshVisualState(instant: true);
            return button;
        }

        public MainMenuActionButton MenuScreenCompactButton(
            string name, Transform parent, string label,
            float x, float y, float width, float height, UnityAction onClick,
            bool primary = false, bool interactable = true, bool destructive = false)
        {
            GameObject root = CreateObject(name, parent);
            Place(root, x, y, width, height);
            Image hitGraphic = AddSurface(root, Color.clear, rounded: false);
            // Dense settings rows have no exterior halo or shadow between
            // neighbouring controls; selection is the same thin accent edge.
            MainMenuVisuals visuals = AddMainMenuVisuals(
                root.transform, includeGlow: false, includeShadow: false);
            Text title = Text(
                name + "Label", visuals.Body, label,
                UiThemeTokens.SpacingCompact, 0f,
                width - UiThemeTokens.SpacingCompact * 2f, height,
                Mathf.RoundToInt(Mathf.Clamp(height * 0.38f, 11f, 18f)),
                MainMenuStyle.PrimaryText, TextAnchor.MiddleCenter);
            MainMenuActionButton button = ConfigureMainMenuButton(root, hitGraphic, onClick, interactable);
            button.Initialize(visuals.Visual, visuals.Overlay, visuals.Border, visuals.Glow,
                null, title, null, isDestructive: destructive);
            button.SetPersistentSelection(primary);
            button.RefreshVisualState(instant: true);
            return button;
        }

        public GameObject MenuScreenField(
            string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject field = CreateObject(name, parent);
            Place(field, x, y, width, height);
            Image surface = AddSurface(field, MenuScreenStyle.Field);
            surface.sprite = assets.MainMenuSurfaceSprite;
            Image border = AddBorderLayer(field, MainMenuStyle.Border);
            border.sprite = assets.MainMenuBorderSprite;
            return field;
        }

        public Text MenuScreenHeading(
            Transform parent, string value, float x, float y, float width,
            int size = UiThemeTokens.HeadingSize)
        {
            Text heading = Heading(parent, value, x, y, width, size);
            heading.color = MainMenuStyle.PrimaryText;
            heading.fontStyle = FontStyle.Normal;
            return heading;
        }

        public GameObject MenuScreenDivider(Transform parent, float x, float y, float width) =>
            Divider(parent, x, y, width, MainMenuStyle.Border);

        public Slider MenuScreenSlider(
            string name, Transform parent, float x, float y, float width,
            float value, UnityAction<float> onChanged, bool interactable = true)
        {
            // Keep the existing Slider's value mapping, 16 px handle, hit
            // geometry and navigation. Only its graphics change here.
            Slider slider = Slider(name, parent, x, y, width, value, onChanged, interactable);
            foreach (Image image in slider.GetComponentsInChildren<Image>(includeInactive: true))
            {
                image.color = image == slider.targetGraphic
                    ? MainMenuStyle.PrimaryText
                    : image.rectTransform == slider.fillRect
                        ? interactable ? MainMenuStyle.Accent : MenuScreenStyle.Disabled
                        : MenuScreenStyle.Track;
            }

            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = MainMenuStyle.Accent;
            colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            colors.disabledColor = new Color(0.68f, 0.68f, 0.68f, 0.52f);
            colors.fadeDuration = MainMenuStyle.HoverDurationSeconds;
            slider.colors = colors;
            return slider;
        }

        public Toggle MenuScreenToggle(
            string name, Transform parent, float x, float y, bool value,
            UnityAction<bool> onChanged, bool interactable = true)
        {
            Toggle toggle = Toggle(name, parent, x, y, value, onChanged, interactable);
            Image background = toggle.GetComponent<Image>();
            background.color = MenuScreenStyle.Field;
            background.sprite = assets.MainMenuSurfaceSprite;
            toggle.graphic.color = interactable ? MainMenuStyle.Accent : MenuScreenStyle.Disabled;
            foreach (Text label in toggle.GetComponentsInChildren<Text>(includeInactive: true))
            {
                label.color = label.color == UiThemeTokens.TextPrimary
                    ? MainMenuStyle.PrimaryText : MainMenuStyle.SecondaryText;
                label.fontStyle = FontStyle.Normal;
            }

            // Tint the outline instead of filling the whole control on focus.
            Image border = AddBorderLayer(toggle.gameObject, Color.white);
            border.sprite = assets.MainMenuBorderSprite;
            toggle.targetGraphic = border;
            ColorBlock colors = toggle.colors;
            colors.normalColor = MainMenuStyle.Border;
            colors.highlightedColor = MainMenuStyle.HoverBorder;
            colors.selectedColor = MainMenuStyle.Accent;
            Color pressed = MainMenuStyle.Accent;
            pressed.a = 0.65f;
            colors.pressedColor = pressed;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.06f);
            colors.fadeDuration = MainMenuStyle.HoverDurationSeconds;
            toggle.colors = colors;
            return toggle;
        }
    }

    internal static class MenuScreenStyle
    {
        public static readonly Color Row = new Color(0.94f, 0.94f, 0.92f, 0.025f);
        public static readonly Color RowAlternate = new Color(0.94f, 0.94f, 0.92f, 0.04f);
        public static readonly Color Field = new Color(0.015f, 0.017f, 0.019f, 0.34f);
        public static readonly Color Track = new Color(0.24f, 0.25f, 0.26f, 0.70f);
        public static readonly Color Disabled = new Color(0.66f, 0.66f, 0.64f, 0.64f);
    }
}
