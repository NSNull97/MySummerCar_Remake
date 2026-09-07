using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    internal sealed partial class UiFactory
    {
        public MainMenuActionButton MainMenuButton(
            string name,
            Transform parent,
            string label,
            UiIconKind icon,
            float x,
            float y,
            float width,
            float height,
            UnityAction onClick,
            bool destructive = false,
            bool interactable = true,
            string helper = "",
            bool compact = false)
        {
            GameObject root = CreateObject(name, parent);
            Place(root, x, y, width, height);
            Image hitGraphic = AddSurface(root, Color.clear, rounded: false);
            MainMenuVisuals visuals = AddMainMenuVisuals(root.transform);
            float iconSize = compact ? 30f : 32f;
            float padding = compact ? 14f : 20f;
            float contentStart = padding + iconSize + (compact ? 10f : 16f);
            Image symbol = Icon(
                name + "Icon", visuals.Body, icon, padding,
                (height - iconSize) * 0.5f, iconSize, MainMenuStyle.PrimaryText);
            if (icon == UiIconKind.Play)
            {
                // The shared procedural glyph points left; orient only this
                // play action without changing icons on accepted screens.
                symbol.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                symbol.rectTransform.anchoredPosition += new Vector2(iconSize * 0.5f, -iconSize * 0.5f);
                symbol.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            }

            bool hasHelper = !string.IsNullOrEmpty(helper);
            float contentWidth = width - contentStart - padding;
            GameObject textColumn = CreateObject(name + "TextColumn", visuals.Body);
            Place(textColumn, contentStart, 0f, contentWidth, height);
            // Center the complete text block. Layout follows real font metrics
            // and updates when the save helper changes between one/two lines.
            var textLayout = textColumn.AddComponent<VerticalLayoutGroup>();
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.spacing = 2f;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            Text title = Text(
                name + "Label", textColumn.transform, label, 0f, 0f, contentWidth, height,
                compact ? MainMenuStyle.CompactTitleSize : MainMenuStyle.TitleSize,
                MainMenuStyle.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Normal);
            Text helperLabel = Text(
                name + "Helper", textColumn.transform, helper, 0f, 0f, contentWidth, height,
                MainMenuStyle.HelperSize, MainMenuStyle.SecondaryText, TextAnchor.MiddleLeft);
            helperLabel.gameObject.SetActive(hasHelper);

            MainMenuActionButton button = ConfigureMainMenuButton(root, hitGraphic, onClick, interactable);
            button.Initialize(visuals.Visual, visuals.Overlay, visuals.Border, visuals.Glow,
                symbol, title, helperLabel, destructive);
            return button;
        }

        public GameObject MainMenuPanel(
            string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject root = CreateObject(name, parent);
            Place(root, x, y, width, height);
            AddSurface(root, Color.clear, rounded: false);
            AddMainMenuVisuals(root.transform, includeGlow: false);
            return root;
        }

        public GameObject MainMenuGreeting(
            string name, Transform parent, string label,
            float x, float y, float width, float height)
        {
            GameObject panel = MainMenuPanel(name, parent, x, y, width, height);
            const float iconSize = 33f;
            Icon("GreetingSmile", panel.transform, UiIconKind.Smile, 17f,
                (height - iconSize) * 0.5f, iconSize, MainMenuStyle.GreetingIcon);
            Text("GreetingText", panel.transform, label, 63f, 0f, width - 79f, height,
                14, MainMenuStyle.PrimaryText);
            return panel;
        }

        public MainMenuActionButton MainMenuSwatch(
            string name, Transform parent, Color colour,
            float x, float y, float size, bool selected, UnityAction onClick)
        {
            GameObject root = CreateObject(name, parent);
            Place(root, x, y, size, size);
            Image hitGraphic = AddSurface(root, Color.clear, rounded: false);
            GameObject visualObject = CreateObject("Visual", root.transform);
            RectTransform visual = Stretch(visualObject);
            Image shadow = MainMenuImage("Shadow", visual, assets.MainMenuSwatchShadowSprite, MainMenuStyle.Shadow);
            Inflate(shadow.rectTransform, 8f, new Vector2(0f, -2f));
            Image glow = MainMenuImage("SelectionGlow", visual, assets.MainMenuSwatchShadowSprite, Color.clear);
            Inflate(glow.rectTransform, 8f, Vector2.zero);
            MainMenuImage("Colour", visual, assets.MainMenuSwatchSprite, colour);
            Image overlay = MainMenuImage("StateOverlay", visual, assets.MainMenuSwatchSprite, Color.clear);
            Image border = MainMenuImage("Border", visual, assets.MainMenuSwatchBorderSprite, Color.clear);
            MainMenuActionButton button = ConfigureMainMenuButton(root, hitGraphic, onClick, true);
            button.Initialize(visual, overlay, border, glow, null, null, null, isColourSwatch: true);
            button.SetPersistentSelection(selected);
            button.RefreshVisualState(instant: true);
            return button;
        }

        public MainMenuActionButton MainMenuPageDot(
            string name, Transform parent, float x, float y, bool selected, UnityAction onClick)
        {
            GameObject root = CreateObject(name, parent);
            Place(root, x, y, 28f, 28f);
            Image hitGraphic = AddSurface(root, Color.clear, rounded: false);
            GameObject visualObject = CreateObject("Visual", root.transform);
            RectTransform visual = Stretch(visualObject);
            GameObject dotObject = CreateObject("Dot", visual);
            Place(dotObject, 9f, 9f, 10f, 10f);
            Image dot = AddSurface(dotObject, MainMenuStyle.SecondaryText);
            dot.sprite = assets.CircleSprite;
            dot.type = Image.Type.Simple;
            dot.raycastTarget = false;
            MainMenuActionButton button = ConfigureMainMenuButton(root, hitGraphic, onClick, true);
            button.Initialize(visual, null, null, null, dot, null, null, isPageIndicator: true);
            button.SetPersistentSelection(selected);
            button.RefreshVisualState(instant: true);
            return button;
        }

        private MainMenuActionButton ConfigureMainMenuButton(
            GameObject root, Image hitGraphic, UnityAction onClick, bool interactable)
        {
            MainMenuActionButton button = root.AddComponent<MainMenuActionButton>();
            button.targetGraphic = hitGraphic;
            button.transition = Selectable.Transition.None;
            button.interactable = interactable;
            if (onClick != null)
            {
                if (confirmAudio != null) button.onClick.AddListener(confirmAudio);
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        private MainMenuVisuals AddMainMenuVisuals(
            Transform parent, bool includeGlow = true, bool includeShadow = true)
        {
            GameObject visualObject = CreateObject("Visual", parent);
            RectTransform visual = Stretch(visualObject);
            if (includeShadow)
            {
                Image shadow = MainMenuImage("Shadow", visual, assets.MainMenuSoftShadowSprite, MainMenuStyle.Shadow);
                Inflate(shadow.rectTransform, 8f, new Vector2(0f, -1f));
            }
            Image glow = null;
            if (includeGlow)
            {
                glow = MainMenuImage("SelectionGlow", visual, assets.MainMenuSoftShadowSprite, Color.clear);
                Inflate(glow.rectTransform, 8f, Vector2.zero);
            }

            // Exterior shadows are siblings of the clipped glass body. The
            // shared blurred source remains one texture for the entire menu.
            GameObject glassBody = CreateObject("GlassBody", visual);
            Stretch(glassBody);
            Image tint = AddGlassSurface(glassBody, UiGlassKind.MainMenuNeutral,
                MainMenuStyle.Surface, raycastTarget: false);
            glassBody.GetComponent<Image>().sprite = assets.MainMenuSurfaceSprite;
            tint.sprite = assets.MainMenuSurfaceSprite;
            // The blurred backdrop supplies all surface variation. A synthetic
            // top highlight would make these flat glass surfaces look bevelled.
            Image overlay = MainMenuImage("StateOverlay", glassBody.transform,
                assets.MainMenuSurfaceSprite, Color.clear);
            Image border = MainMenuImage("Border", glassBody.transform,
                assets.MainMenuBorderSprite, MainMenuStyle.Border);
            return new MainMenuVisuals(visual, glassBody.transform, overlay, border, glow);
        }

        private Image MainMenuImage(string name, Transform parent, Sprite sprite, Color colour)
        {
            GameObject layer = CreateObject(name, parent);
            Stretch(layer);
            Image graphic = AddSurface(layer, colour);
            graphic.sprite = sprite;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void Inflate(RectTransform rect, float amount, Vector2 offset)
        {
            rect.offsetMin = new Vector2(-amount, -amount) + offset;
            rect.offsetMax = new Vector2(amount, amount) + offset;
        }

        private readonly struct MainMenuVisuals
        {
            public MainMenuVisuals(RectTransform visual, Transform body, Image overlay, Image border, Image glow)
            {
                Visual = visual;
                Body = body;
                Overlay = overlay;
                Border = border;
                Glow = glow;
            }

            public RectTransform Visual { get; }
            public Transform Body { get; }
            public Image Overlay { get; }
            public Image Border { get; }
            public Image Glow { get; }
        }
    }
}
