using System;
using System.Collections.Generic;
using MSC.Core.Lifecycle;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Player
{
    /// <summary>
    /// Project-owned contextual HUD: a semantic centre reticle, no more than
    /// three lower-left action plaques, and a dynamic lower-centre target /
    /// subtitle stack. Gameplay sources own meaning; this component owns only
    /// compact presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrossdotPresenter : MonoBehaviour,
        IUiVisibilityGate,
        IGameplayLocaleSettingsSink
    {
        public const int ContextTextFontSizePixels = 18;
        public const FontStyle ContextTitleFontStyle = FontStyle.Bold;
        public const FontStyle ContextSubtitleFontStyle = FontStyle.Normal;

        private const float ReferenceWidth = 1672f;
        private const float ReferenceHeight = 941f;

        private const float ActionLeftMargin = 36f;
        private const float ActionBottomMargin = 44f;
        private const float ActionHeight = 40f;
        private const float ActionGap = 7f;
        private const float ActionHorizontalPadding = 11f;
        private const float ActionIconSize = 26f;
        private const float ActionIconGap = 7f;
        private const float KeycapHeight = 24f;
        private const float KeycapMinimumWidth = 28f;
        private const float KeycapHorizontalPadding = 7f;
        private const float BindingActionGap = 8f;
        private const float MinimumActionWidth = 150f;

        private const float ContextBottomMargin = 44f;
        private const float ContextSideMargin = 24f;
        private const float ContextMaximumWidth = 820f;
        private const float ContextMinimumWidth = 190f;
        private const float ContextRowGap = 5f;
        private const float ContextHorizontalPadding = 16f;
        private const float ContextTitleMinimumHeight = 42f;
        private const float ContextSubtitleMinimumHeight = 42f;

        [SerializeField]
        private bool visible = true;

        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField]
        private PlayerInputRouter inputRouter;

        [Header("Licensed interaction icons")]
        [SerializeField]
        private Texture2D pickupIcon;

        [SerializeField]
        private Texture2D installIcon;

        [SerializeField]
        private Texture2D removeIcon;

        [Tooltip("Generic fallback used when a semantic mouse icon is absent.")]
        [SerializeField]
        private Texture2D mouseIcon;

        [SerializeField]
        private Texture2D mouseLeftButtonIcon;

        [SerializeField]
        private Texture2D mouseRightButtonIcon;

        [SerializeField]
        private Texture2D mouseMiddleButtonIcon;

        [SerializeField]
        private Texture2D mouseScrollIcon;

        [SerializeField]
        private Texture2D mouseScrollUpIcon;

        [SerializeField]
        private Texture2D mouseScrollDownIcon;

        [Header("Reticle")]
        [SerializeField, Min(1f)]
        private float dotDiameterPixels = 4f;

        [SerializeField, Min(0f)]
        private float outlineWidthPixels = 1f;

        [SerializeField, Min(8f)]
        private float actionReticleSizePixels = 42f;

        [SerializeField]
        private Color dotColor = Color.white;

        [SerializeField]
        private Color outlineColor = new Color(0f, 0f, 0f, 0.85f);

        [SerializeField]
        private string localeId = InteractionUiTextCatalog.EnglishLocaleId;

        private readonly GUIContent[] bindingContents =
        {
            new GUIContent(),
            new GUIContent(),
            new GUIContent(),
        };

        private readonly GUIContent[] actionContents =
        {
            new GUIContent(),
            new GUIContent(),
            new GUIContent(),
        };

        private readonly float[] actionWidths =
            new float[InteractionActionSnapshot.MaximumActions];
        private readonly float[] keycapWidths =
            new float[InteractionActionSnapshot.MaximumActions];
        private readonly Texture2D[] actionMouseIcons =
            new Texture2D[InteractionActionSnapshot.MaximumActions];
        private readonly Dictionary<string, string> uppercaseLabelCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly GUIContent targetTitleContent = new GUIContent();
        private readonly GUIContent subtitleContent = new GUIContent();

        private Texture2D crossdotTexture;
        private Texture2D actionPillTexture;
        private Texture2D keycapBorderTexture;
        private Texture2D keycapFillTexture;
        private Texture2D contextTitlePillTexture;
        private Texture2D contextSubtitlePillTexture;
        private Font promptFont;
        private GUIStyle actionPillStyle;
        private GUIStyle keycapBorderStyle;
        private GUIStyle keycapFillStyle;
        private GUIStyle actionTextStyle;
        private GUIStyle actionTextShadowStyle;
        private GUIStyle keycapTextStyle;
        private GUIStyle keycapTextShadowStyle;
        private GUIStyle contextTitlePillStyle;
        private GUIStyle contextSubtitlePillStyle;
        private GUIStyle contextTitleStyle;
        private GUIStyle contextTitleShadowStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle subtitleShadowStyle;
        private InteractionActionSnapshot cachedSnapshot;
        private int cachedBindingRevision = int.MinValue;
        private bool actionCacheValid;
        private string cachedTargetTitle = string.Empty;
        private string cachedSubtitle = string.Empty;
        private float contextPanelWidth;
        private float contextTitleHeight;
        private float contextSubtitleHeight;
        private bool contextCacheValid;
        private IPlayerSubtitleSource subtitleSource;
        private bool uiSuppressed;

        public bool Visible => visible;

        public string LocaleId => localeId;

        public bool IsUiSuppressed => uiSuppressed;

        public float DotDiameterPixels => dotDiameterPixels;

        public float OutlineWidthPixels => outlineWidthPixels;

        public void Configure(
            float diameterPixels,
            float outlinePixels,
            Color fillColor,
            Color borderColor)
        {
            dotDiameterPixels = Mathf.Max(1f, diameterPixels);
            outlineWidthPixels = Mathf.Max(0f, outlinePixels);
            dotColor = fillColor;
            outlineColor = borderColor;
            RebuildRuntimeTextures();
        }

        /// <summary>
        /// Compatibility overload for existing prototype builders. A supplied
        /// generic mouse remains a safe fallback until semantic variants are
        /// assigned by the canonical player prefab.
        /// </summary>
        public void ConfigureIcons(
            Texture2D configuredPickupIcon,
            Texture2D configuredInstallIcon,
            Texture2D configuredMouseIcon)
        {
            pickupIcon = configuredPickupIcon;
            installIcon = configuredInstallIcon;
            mouseIcon = configuredMouseIcon;
            mouseLeftButtonIcon = configuredMouseIcon;
            mouseRightButtonIcon = configuredMouseIcon;
            mouseMiddleButtonIcon = configuredMouseIcon;
            mouseScrollIcon = configuredMouseIcon;
            mouseScrollUpIcon = configuredMouseIcon;
            mouseScrollDownIcon = configuredMouseIcon;
            actionCacheValid = false;
        }

        public void ConfigureIcons(
            Texture2D configuredPickupIcon,
            Texture2D configuredInstallIcon,
            Texture2D configuredRemoveIcon,
            Texture2D configuredMouseIcon,
            Texture2D configuredMouseLeftButtonIcon,
            Texture2D configuredMouseRightButtonIcon,
            Texture2D configuredMouseMiddleButtonIcon,
            Texture2D configuredMouseScrollIcon,
            Texture2D configuredMouseScrollUpIcon,
            Texture2D configuredMouseScrollDownIcon)
        {
            pickupIcon = configuredPickupIcon;
            installIcon = configuredInstallIcon;
            removeIcon = configuredRemoveIcon;
            mouseIcon = configuredMouseIcon;
            mouseLeftButtonIcon = configuredMouseLeftButtonIcon;
            mouseRightButtonIcon = configuredMouseRightButtonIcon;
            mouseMiddleButtonIcon = configuredMouseMiddleButtonIcon;
            mouseScrollIcon = configuredMouseScrollIcon;
            mouseScrollUpIcon = configuredMouseScrollUpIcon;
            mouseScrollDownIcon = configuredMouseScrollDownIcon;
            actionCacheValid = false;
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public void ApplyGameplayLocale(string configuredLocaleId)
        {
            string normalized = InteractionUiTextCatalog.NormalizeLocaleId(
                configuredLocaleId);
            if (string.Equals(localeId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            localeId = normalized;
            uppercaseLabelCache.Clear();
            actionCacheValid = false;
            contextCacheValid = false;
        }

        public void BindSubtitleSource(IPlayerSubtitleSource source)
        {
            if (ReferenceEquals(subtitleSource, source))
            {
                return;
            }

            ReleaseSubtitleSource();
            subtitleSource = source;
            if (isActiveAndEnabled)
            {
                subtitleSource?.SetContextHudPresenterActive(true);
            }
            contextCacheValid = false;
        }

        public void UnbindSubtitleSource(IPlayerSubtitleSource source)
        {
            if (ReferenceEquals(subtitleSource, source))
            {
                ReleaseSubtitleSource();
                contextCacheValid = false;
            }
        }

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
        }

        public static Rect CalculateCenteredRect(
            float screenWidth,
            float screenHeight,
            float sizePixels)
        {
            float clampedSize = Mathf.Max(1f, sizePixels);
            return new Rect(
                (screenWidth - clampedSize) * 0.5f,
                (screenHeight - clampedSize) * 0.5f,
                clampedSize,
                clampedSize);
        }

        public static float CalculateReferenceScale(
            float screenWidth,
            float screenHeight)
        {
            if (!float.IsFinite(screenWidth) ||
                !float.IsFinite(screenHeight) ||
                screenWidth <= 0f || screenHeight <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(
                0.25f,
                Mathf.Min(
                    screenWidth / ReferenceWidth,
                    screenHeight / ReferenceHeight));
        }

        public static Rect CalculateActionStackBounds(
            float screenWidth,
            float screenHeight,
            int actionCount,
            float widestActionPixels)
        {
            int count = Mathf.Clamp(
                actionCount,
                0,
                InteractionActionSnapshot.MaximumActions);
            if (count == 0)
            {
                return default;
            }

            float scale = CalculateReferenceScale(screenWidth, screenHeight);
            float logicalWidth = screenWidth / scale;
            float logicalHeight = screenHeight / scale;
            float safeOriginX = Mathf.Max(
                0f,
                (logicalWidth - ReferenceWidth) * 0.5f);
            float safeOriginY = Mathf.Max(
                0f,
                (logicalHeight - ReferenceHeight) * 0.5f);
            float height = count * ActionHeight + (count - 1) * ActionGap;
            return new Rect(
                (safeOriginX + ActionLeftMargin) * scale,
                (logicalHeight - safeOriginY - ActionBottomMargin - height) * scale,
                Mathf.Max(MinimumActionWidth, widestActionPixels) * scale,
                height * scale);
        }

        public static Rect CalculateContextTextBounds(
            float screenWidth,
            float screenHeight,
            float panelWidthPixels,
            float panelHeightPixels)
        {
            if (panelHeightPixels <= 0f)
            {
                return default;
            }

            float scale = CalculateReferenceScale(screenWidth, screenHeight);
            float logicalScreenWidth = screenWidth / scale;
            float logicalScreenHeight = screenHeight / scale;
            float safeOriginY = Mathf.Max(
                0f,
                (logicalScreenHeight - ReferenceHeight) * 0.5f);
            float maximumWidth = Mathf.Max(
                ContextMinimumWidth,
                Mathf.Min(
                    ContextMaximumWidth,
                    logicalScreenWidth - ContextSideMargin * 2f));
            float width = Mathf.Clamp(
                panelWidthPixels,
                ContextMinimumWidth,
                maximumWidth);
            float height = Mathf.Max(1f, panelHeightPixels);
            return new Rect(
                (logicalScreenWidth - width) * 0.5f * scale,
                (logicalScreenHeight - safeOriginY -
                 ContextBottomMargin - height) * scale,
                width * scale,
                height * scale);
        }

        public static InteractionBindingGlyphKind ResolveDefaultBindingGlyph(
            InteractionActionBinding binding) => binding switch
        {
            InteractionActionBinding.Interact =>
                InteractionBindingGlyphKind.MouseLeftButton,
            InteractionActionBinding.Throw =>
                InteractionBindingGlyphKind.MouseRightButton,
            InteractionActionBinding.Scroll =>
                InteractionBindingGlyphKind.MouseWheelScroll,
            _ => InteractionBindingGlyphKind.Keycap,
        };

        private void OnEnable()
        {
            useGUILayout = false;
            ResolveDependencies();
            ResolveSubtitleSource();
            subtitleSource?.SetContextHudPresenterActive(true);
            promptFont = Resources.Load<Font>("Fonts/HelveticaNeueRoman");
            RebuildRuntimeTextures();
            actionCacheValid = false;
            contextCacheValid = false;
        }

        private void OnDisable()
        {
            ReleaseSubtitleSource();
            ReleaseRuntimeTexture(ref crossdotTexture);
            ReleaseRuntimeTexture(ref actionPillTexture);
            ReleaseRuntimeTexture(ref keycapBorderTexture);
            ReleaseRuntimeTexture(ref keycapFillTexture);
            ReleaseRuntimeTexture(ref contextTitlePillTexture);
            ReleaseRuntimeTexture(ref contextSubtitlePillTexture);
            ClearStyles();
            promptFont = null;
            uppercaseLabelCache.Clear();
            actionCacheValid = false;
            contextCacheValid = false;
        }

        private void Reset()
        {
            ResolveDependencies();
        }

        private void OnValidate()
        {
            dotDiameterPixels = Mathf.Max(1f, dotDiameterPixels);
            outlineWidthPixels = Mathf.Max(0f, outlineWidthPixels);
            actionReticleSizePixels = Mathf.Max(8f, actionReticleSizePixels);
            if (isActiveAndEnabled)
            {
                RebuildRuntimeTextures();
                actionCacheValid = false;
                contextCacheValid = false;
            }
        }

        private void OnGUI()
        {
            if (!visible || uiSuppressed ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();
            ResolveSubtitleSource();
            InteractionActionSnapshot snapshot = interactionController != null
                ? interactionController.CurrentActionSnapshot
                : InteractionActionSnapshot.Empty;
            if (inputRouter != null && inputRouter.IsAlternativeActionsHeld)
            {
                snapshot = snapshot.WithAlternativeActions();
            }

            int bindingRevision = inputRouter != null
                ? inputRouter.BindingDisplayRevision
                : -1;
            EnsureActionCache(snapshot, bindingRevision);
            DrawReticle(snapshot.Reticle);
            DrawActionStack(snapshot.ActionCount);

            string targetTitle = interactionController != null
                ? InteractionUiTextCatalog.LocalizeDisplayName(
                    interactionController.CurrentDisplayLocalizationKey,
                    interactionController.CurrentDisplayName,
                    localeId)
                : string.Empty;
            string activeSubtitle = subtitleSource != null
                ? InteractionUiTextCatalog.LocalizeSubtitle(
                    subtitleSource.ActiveSubtitle,
                    localeId)
                : string.Empty;
            EnsureContextTextCache(targetTitle, activeSubtitle);
            DrawContextTextStack();
        }

        private void ResolveDependencies()
        {
            if (interactionController == null)
            {
                interactionController =
                    GetComponent<PlayerInteractionController>();
            }

            if (inputRouter == null)
            {
                inputRouter = GetComponent<PlayerInputRouter>();
            }
        }

        private void ResolveSubtitleSource()
        {
            if (subtitleSource is UnityEngine.Object sourceObject &&
                sourceObject == null)
            {
                subtitleSource = null;
            }

            if (subtitleSource != null)
            {
                return;
            }

            subtitleSource = GetComponent(typeof(IPlayerSubtitleSource)) as
                IPlayerSubtitleSource;
            subtitleSource?.SetContextHudPresenterActive(true);
            contextCacheValid = false;
        }

        private void ReleaseSubtitleSource()
        {
            if (subtitleSource is UnityEngine.Object sourceObject &&
                sourceObject == null)
            {
                subtitleSource = null;
                return;
            }

            subtitleSource?.SetContextHudPresenterActive(false);
            subtitleSource = null;
        }

        private void DrawReticle(InteractionReticleKind reticle)
        {
            float scale = CalculateReferenceScale(Screen.width, Screen.height);
            if (reticle == InteractionReticleKind.Dot)
            {
                if (crossdotTexture == null)
                {
                    return;
                }

                Rect dotRect = CalculateCenteredRect(
                    Screen.width,
                    Screen.height,
                    crossdotTexture.width * scale);
                GUI.DrawTexture(
                    dotRect,
                    crossdotTexture,
                    ScaleMode.StretchToFill,
                    alphaBlend: true);
                return;
            }

            Texture2D icon = reticle switch
            {
                InteractionReticleKind.Install => installIcon,
                InteractionReticleKind.Remove => removeIcon,
                _ => pickupIcon,
            };
            if (icon == null)
            {
                DrawReticle(InteractionReticleKind.Dot);
                return;
            }

            Rect iconRect = CalculateCenteredRect(
                Screen.width,
                Screen.height,
                actionReticleSizePixels * scale);
            DrawIconWithHalo(
                iconRect,
                icon,
                2.35f * scale,
                strong: true);
        }

        private void DrawActionStack(int actionCount)
        {
            if (actionCount <= 0)
            {
                return;
            }

            float widest = 0f;
            for (int index = 0; index < actionCount; index++)
            {
                widest = Mathf.Max(widest, actionWidths[index]);
            }

            Rect physicalBounds = CalculateActionStackBounds(
                Screen.width,
                Screen.height,
                actionCount,
                widest);
            float scale = CalculateReferenceScale(Screen.width, Screen.height);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float x = physicalBounds.x / scale;
            float y = physicalBounds.y / scale;

            for (int index = 0; index < actionCount; index++)
            {
                float width = actionWidths[index];
                var plaqueRect = new Rect(
                    x,
                    y + index * (ActionHeight + ActionGap),
                    width,
                    ActionHeight);
                GUI.Box(plaqueRect, GUIContent.none, actionPillStyle);

                float cursorX = plaqueRect.x + ActionHorizontalPadding;
                Texture2D mouseGlyph = actionMouseIcons[index];
                if (mouseGlyph != null)
                {
                    var iconRect = new Rect(
                        cursorX,
                        plaqueRect.y + (ActionHeight - ActionIconSize) * 0.5f,
                        ActionIconSize,
                        ActionIconSize);
                    DrawIconWithHalo(iconRect, mouseGlyph, 1.1f, strong: false);
                    cursorX += ActionIconSize + ActionIconGap;
                }

                float keycapWidth = keycapWidths[index];
                if (keycapWidth > 0f)
                {
                    var keycapRect = new Rect(
                        cursorX,
                        plaqueRect.y + (ActionHeight - KeycapHeight) * 0.5f,
                        keycapWidth,
                        KeycapHeight);
                    GUI.Box(
                        keycapRect,
                        GUIContent.none,
                        keycapBorderStyle);
                    var keycapInnerRect = new Rect(
                        keycapRect.x + 1f,
                        keycapRect.y + 1f,
                        Mathf.Max(0f, keycapRect.width - 2f),
                        Mathf.Max(0f, keycapRect.height - 2f));
                    GUI.Box(
                        keycapInnerRect,
                        GUIContent.none,
                        keycapFillStyle);
                    DrawLabelWithShadow(
                        keycapRect,
                        bindingContents[index],
                        keycapTextStyle,
                        keycapTextShadowStyle,
                        new Vector2(0.75f, 0.75f));
                    cursorX += keycapWidth + BindingActionGap;
                }

                var textRect = new Rect(
                    cursorX,
                    plaqueRect.y,
                    Mathf.Max(
                        0f,
                        plaqueRect.xMax - ActionHorizontalPadding - cursorX),
                    plaqueRect.height);
                DrawLabelWithShadow(
                    textRect,
                    actionContents[index],
                    actionTextStyle,
                    actionTextShadowStyle,
                    Vector2.one);
            }

            GUI.matrix = previousMatrix;
        }

        private void EnsureActionCache(
            InteractionActionSnapshot snapshot,
            int bindingRevision)
        {
            if (actionCacheValid &&
                cachedSnapshot == snapshot &&
                cachedBindingRevision == bindingRevision)
            {
                return;
            }

            cachedSnapshot = snapshot;
            cachedBindingRevision = bindingRevision;
            actionCacheValid = true;
            for (int index = 0;
                 index < InteractionActionSnapshot.MaximumActions;
                 index++)
            {
                bindingContents[index].text = string.Empty;
                actionContents[index].text = string.Empty;
                actionWidths[index] = MinimumActionWidth;
                keycapWidths[index] = 0f;
                actionMouseIcons[index] = null;
                if (index >= snapshot.ActionCount)
                {
                    continue;
                }

                InteractionActionHint action = snapshot.GetAction(index);
                string binding = inputRouter != null
                    ? inputRouter.GetBindingDisplayLabel(action.InputActionName)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(binding))
                {
                    binding = action.DefaultBindingLabel;
                }
                binding = InteractionUiTextCatalog.LocalizeBindingLabel(
                    binding,
                    localeId);

                InteractionBindingGlyphKind glyphKind = inputRouter != null
                    ? inputRouter.GetBindingGlyphKind(action.InputActionName)
                    : InteractionBindingGlyphKind.None;
                if (glyphKind == InteractionBindingGlyphKind.None)
                {
                    glyphKind = ResolveDefaultBindingGlyph(action.Binding);
                }

                bindingContents[index].text = binding;
                string actionLabel = GetUppercaseLabel(
                    InteractionUiTextCatalog.LocalizeActionLabel(
                        action.Label,
                        action.Binding,
                        localeId));
                actionContents[index].text = string.IsNullOrEmpty(binding)
                    ? actionLabel
                    : "— " + actionLabel;
                actionMouseIcons[index] = ResolveMouseIcon(
                    glyphKind,
                    action.ScrollDirection);

                float keycapWidth = string.IsNullOrEmpty(binding)
                    ? 0f
                    : Mathf.Max(
                        KeycapMinimumWidth,
                        Mathf.Ceil(
                            keycapTextStyle.CalcSize(
                                bindingContents[index]).x) +
                        KeycapHorizontalPadding * 2f);
                keycapWidths[index] = keycapWidth;
                float iconWidth = actionMouseIcons[index] != null
                    ? ActionIconSize + ActionIconGap
                    : 0f;
                float bindingWidth = keycapWidth > 0f
                    ? keycapWidth + BindingActionGap
                    : 0f;
                float labelWidth = Mathf.Ceil(
                    actionTextStyle.CalcSize(actionContents[index]).x);
                actionWidths[index] = Mathf.Max(
                    MinimumActionWidth,
                    ActionHorizontalPadding * 2f + iconWidth +
                    bindingWidth + labelWidth);
            }
        }

        private Texture2D ResolveMouseIcon(
            InteractionBindingGlyphKind glyphKind,
            InteractionScrollDirection? scrollDirection)
        {
            switch (glyphKind)
            {
                case InteractionBindingGlyphKind.MouseLeftButton:
                    return mouseLeftButtonIcon != null
                        ? mouseLeftButtonIcon
                        : mouseIcon;
                case InteractionBindingGlyphKind.MouseRightButton:
                    return mouseRightButtonIcon != null
                        ? mouseRightButtonIcon
                        : mouseIcon;
                case InteractionBindingGlyphKind.MouseMiddleButton:
                    return mouseMiddleButtonIcon != null
                        ? mouseMiddleButtonIcon
                        : mouseIcon;
                case InteractionBindingGlyphKind.MouseWheelScroll:
                    if (scrollDirection == InteractionScrollDirection.Positive)
                    {
                        return mouseScrollUpIcon != null
                            ? mouseScrollUpIcon
                            : mouseScrollIcon != null
                                ? mouseScrollIcon
                                : mouseIcon;
                    }

                    if (scrollDirection == InteractionScrollDirection.Negative)
                    {
                        return mouseScrollDownIcon != null
                            ? mouseScrollDownIcon
                            : mouseScrollIcon != null
                                ? mouseScrollIcon
                                : mouseIcon;
                    }

                    return mouseScrollIcon != null
                        ? mouseScrollIcon
                        : mouseIcon;
                case InteractionBindingGlyphKind.MouseGeneric:
                    return mouseIcon;
                default:
                    return null;
            }
        }

        private void EnsureContextTextCache(
            string targetTitle,
            string activeSubtitle)
        {
            string normalizedTitle = targetTitle?.Trim() ?? string.Empty;
            string normalizedSubtitle = activeSubtitle?.Trim() ?? string.Empty;
            if (contextCacheValid &&
                string.Equals(
                    cachedTargetTitle,
                    normalizedTitle,
                    StringComparison.Ordinal) &&
                string.Equals(
                    cachedSubtitle,
                    normalizedSubtitle,
                    StringComparison.Ordinal))
            {
                return;
            }

            cachedTargetTitle = normalizedTitle;
            cachedSubtitle = normalizedSubtitle;
            targetTitleContent.text = normalizedTitle;
            subtitleContent.text = normalizedSubtitle;
            contextCacheValid = true;
            contextPanelWidth = 0f;
            contextTitleHeight = 0f;
            contextSubtitleHeight = 0f;
            if (string.IsNullOrEmpty(normalizedTitle) &&
                string.IsNullOrEmpty(normalizedSubtitle))
            {
                return;
            }

            float maximumTextWidth =
                ContextMaximumWidth - ContextHorizontalPadding * 2f;
            float widestNaturalText = 0f;
            if (!string.IsNullOrEmpty(normalizedTitle))
            {
                widestNaturalText = Mathf.Max(
                    widestNaturalText,
                    contextTitleStyle.CalcSize(targetTitleContent).x);
            }

            if (!string.IsNullOrEmpty(normalizedSubtitle))
            {
                widestNaturalText = Mathf.Max(
                    widestNaturalText,
                    subtitleStyle.CalcSize(subtitleContent).x);
            }

            contextPanelWidth = Mathf.Clamp(
                Mathf.Ceil(widestNaturalText) +
                ContextHorizontalPadding * 2f,
                ContextMinimumWidth,
                ContextMaximumWidth);
            float textWidth = Mathf.Min(
                maximumTextWidth,
                contextPanelWidth - ContextHorizontalPadding * 2f);
            if (!string.IsNullOrEmpty(normalizedTitle))
            {
                contextTitleHeight = Mathf.Max(
                    ContextTitleMinimumHeight,
                    Mathf.Ceil(contextTitleStyle.CalcHeight(
                        targetTitleContent,
                        textWidth)) + 12f);
            }

            if (!string.IsNullOrEmpty(normalizedSubtitle))
            {
                contextSubtitleHeight = Mathf.Max(
                    ContextSubtitleMinimumHeight,
                    Mathf.Ceil(subtitleStyle.CalcHeight(
                        subtitleContent,
                        textWidth)) + 16f);
            }
        }

        private void DrawContextTextStack()
        {
            bool hasTitle = contextTitleHeight > 0f;
            bool hasSubtitle = contextSubtitleHeight > 0f;
            if (!hasTitle && !hasSubtitle)
            {
                return;
            }

            float totalHeight = contextTitleHeight + contextSubtitleHeight +
                (hasTitle && hasSubtitle ? ContextRowGap : 0f);
            Rect physicalBounds = CalculateContextTextBounds(
                Screen.width,
                Screen.height,
                contextPanelWidth,
                totalHeight);
            float scale = CalculateReferenceScale(Screen.width, Screen.height);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float x = physicalBounds.x / scale;
            float y = physicalBounds.y / scale;

            if (hasTitle)
            {
                var titleRect = new Rect(
                    x,
                    y,
                    contextPanelWidth,
                    contextTitleHeight);
                GUI.Box(
                    titleRect,
                    GUIContent.none,
                    contextTitlePillStyle);
                DrawContextLabel(
                    titleRect,
                    targetTitleContent,
                    contextTitleStyle,
                    contextTitleShadowStyle,
                    6f);
                y += contextTitleHeight +
                    (hasSubtitle ? ContextRowGap : 0f);
            }

            if (hasSubtitle)
            {
                var subtitleRect = new Rect(
                    x,
                    y,
                    contextPanelWidth,
                    contextSubtitleHeight);
                GUI.Box(
                    subtitleRect,
                    GUIContent.none,
                    contextSubtitlePillStyle);
                DrawContextLabel(
                    subtitleRect,
                    subtitleContent,
                    subtitleStyle,
                    subtitleShadowStyle,
                    8f);
            }

            GUI.matrix = previousMatrix;
        }

        private static void DrawContextLabel(
            Rect rowRect,
            GUIContent content,
            GUIStyle style,
            GUIStyle shadowStyle,
            float verticalPadding)
        {
            var textRect = new Rect(
                rowRect.x + ContextHorizontalPadding,
                rowRect.y + verticalPadding,
                Mathf.Max(
                    0f,
                    rowRect.width - ContextHorizontalPadding * 2f),
                Mathf.Max(0f, rowRect.height - verticalPadding * 2f));
            DrawLabelWithShadow(
                textRect,
                content,
                style,
                shadowStyle,
                Vector2.one);
        }

        private string GetUppercaseLabel(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (uppercaseLabelCache.TryGetValue(normalized, out string cached))
            {
                return cached;
            }

            if (uppercaseLabelCache.Count >= 128)
            {
                uppercaseLabelCache.Clear();
            }

            cached = normalized.ToUpperInvariant();
            uppercaseLabelCache.Add(normalized, cached);
            return cached;
        }

        private void EnsureStyles()
        {
            if (actionPillStyle != null &&
                keycapBorderStyle != null &&
                keycapFillStyle != null &&
                actionTextStyle != null &&
                actionTextShadowStyle != null &&
                keycapTextStyle != null &&
                keycapTextShadowStyle != null &&
                contextTitlePillStyle != null &&
                contextSubtitlePillStyle != null &&
                contextTitleStyle != null &&
                contextTitleShadowStyle != null &&
                subtitleStyle != null &&
                subtitleShadowStyle != null)
            {
                return;
            }

            actionPillStyle = CreateBoxStyle(actionPillTexture, 10);
            keycapBorderStyle = CreateBoxStyle(keycapBorderTexture, 6);
            keycapFillStyle = CreateBoxStyle(keycapFillTexture, 5);
            contextTitlePillStyle =
                CreateBoxStyle(contextTitlePillTexture, 10);
            contextSubtitlePillStyle =
                CreateBoxStyle(contextSubtitlePillTexture, 10);
            actionTextStyle = CreateTextStyle(
                Color.white,
                15,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                wordWrap: false);
            actionTextShadowStyle = CreateTextStyle(
                new Color(0f, 0f, 0f, 0.88f),
                15,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                wordWrap: false);
            keycapTextStyle = CreateTextStyle(
                Color.white,
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                wordWrap: false);
            keycapTextShadowStyle = CreateTextStyle(
                new Color(0f, 0f, 0f, 0.9f),
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                wordWrap: false);
            contextTitleStyle = CreateTextStyle(
                new Color(0.94f, 0.94f, 0.94f, 1f),
                ContextTextFontSizePixels,
                ContextTitleFontStyle,
                TextAnchor.MiddleCenter,
                wordWrap: true);
            contextTitleShadowStyle = CreateTextStyle(
                new Color(0f, 0f, 0f, 0.92f),
                ContextTextFontSizePixels,
                ContextTitleFontStyle,
                TextAnchor.MiddleCenter,
                wordWrap: true);
            subtitleStyle = CreateTextStyle(
                Color.white,
                ContextTextFontSizePixels,
                ContextSubtitleFontStyle,
                TextAnchor.MiddleCenter,
                wordWrap: true);
            subtitleShadowStyle = CreateTextStyle(
                new Color(0f, 0f, 0f, 0.92f),
                ContextTextFontSizePixels,
                ContextSubtitleFontStyle,
                TextAnchor.MiddleCenter,
                wordWrap: true);
        }

        private static GUIStyle CreateBoxStyle(
            Texture2D texture,
            int borderPixels)
        {
            return new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(
                    borderPixels,
                    borderPixels,
                    borderPixels,
                    borderPixels),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    background = texture,
                },
            };
        }

        private GUIStyle CreateTextStyle(
            Color color,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            bool wordWrap)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = promptFont != null ? promptFont : GUI.skin.font,
                alignment = alignment,
                fontSize = fontSize,
                fontStyle = fontStyle,
                clipping = TextClipping.Clip,
                wordWrap = wordWrap,
                richText = false,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    textColor = color,
                },
            };
        }

        private void RebuildRuntimeTextures()
        {
            ReleaseRuntimeTexture(ref crossdotTexture);
            ReleaseRuntimeTexture(ref actionPillTexture);
            ReleaseRuntimeTexture(ref keycapBorderTexture);
            ReleaseRuntimeTexture(ref keycapFillTexture);
            ReleaseRuntimeTexture(ref contextTitlePillTexture);
            ReleaseRuntimeTexture(ref contextSubtitlePillTexture);
            crossdotTexture = BuildDotTexture(
                dotDiameterPixels,
                outlineWidthPixels,
                dotColor,
                outlineColor);
            actionPillTexture = BuildRoundedRectangleTexture(
                32,
                10f,
                new Color(0.015f, 0.013f, 0.011f, 0.4f),
                "RuntimeContextActionPlaque");
            keycapBorderTexture = BuildRoundedRectangleTexture(
                24,
                5f,
                new Color(1f, 1f, 1f, 0.62f),
                "RuntimeContextKeycapBorder");
            keycapFillTexture = BuildRoundedRectangleTexture(
                24,
                4f,
                new Color(0.015f, 0.013f, 0.011f, 0.78f),
                "RuntimeContextKeycapFill");
            contextTitlePillTexture = BuildRoundedRectangleTexture(
                32,
                10f,
                new Color(0.015f, 0.013f, 0.011f, 0.4f),
                "RuntimeContextTargetPlaque");
            contextSubtitlePillTexture = BuildRoundedRectangleTexture(
                32,
                10f,
                new Color(0.015f, 0.013f, 0.011f, 0.58f),
                "RuntimeSubtitlePlaque");
            ClearStyles();
            contextCacheValid = false;
        }

        private void ClearStyles()
        {
            actionPillStyle = null;
            keycapBorderStyle = null;
            keycapFillStyle = null;
            actionTextStyle = null;
            actionTextShadowStyle = null;
            keycapTextStyle = null;
            keycapTextShadowStyle = null;
            contextTitlePillStyle = null;
            contextSubtitlePillStyle = null;
            contextTitleStyle = null;
            contextTitleShadowStyle = null;
            subtitleStyle = null;
            subtitleShadowStyle = null;
        }

        private static Texture2D BuildDotTexture(
            float diameterPixels,
            float outlinePixels,
            Color fillColor,
            Color borderColor)
        {
            int dotPixels = Mathf.Max(1, Mathf.RoundToInt(diameterPixels));
            int borderPixels = Mathf.Max(0, Mathf.RoundToInt(outlinePixels));
            int textureSize = dotPixels + borderPixels * 2;
            float center = (textureSize - 1) * 0.5f;
            float innerRadiusSquared = dotPixels * dotPixels * 0.25f;
            float outerRadiusSquared = textureSize * textureSize * 0.25f;
            var pixels = new Color32[textureSize * textureSize];
            Color32 fill = fillColor;
            Color32 border = borderColor;
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < textureSize; y++)
            {
                float offsetY = y - center;
                for (int x = 0; x < textureSize; x++)
                {
                    float offsetX = x - center;
                    float distanceSquared =
                        offsetX * offsetX + offsetY * offsetY;
                    pixels[y * textureSize + x] =
                        distanceSquared <= innerRadiusSquared
                            ? fill
                            : distanceSquared <= outerRadiusSquared
                                ? border
                                : clear;
                }
            }

            var texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = "RuntimeInteractionDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static Texture2D BuildRoundedRectangleTexture(
            int size,
            float radius,
            Color color,
            string textureName)
        {
            var pixels = new Color[size * size];
            float minimum = radius;
            float maximum = size - radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sampleX = x + 0.5f;
                    float sampleY = y + 0.5f;
                    float nearestX = Mathf.Clamp(sampleX, minimum, maximum);
                    float nearestY = Mathf.Clamp(sampleY, minimum, maximum);
                    float distance = Vector2.Distance(
                        new Vector2(sampleX, sampleY),
                        new Vector2(nearestX, nearestY));
                    float coverage = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(
                        color.r,
                        color.g,
                        color.b,
                        color.a * coverage);
                }
            }

            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static void DrawLabelWithShadow(
            Rect rect,
            GUIContent content,
            GUIStyle style,
            GUIStyle shadowStyle,
            Vector2 shadowOffset)
        {
            var shadowRect = rect;
            shadowRect.position += shadowOffset;
            GUI.Label(shadowRect, content, shadowStyle);
            GUI.Label(rect, content, style);
        }

        private static void DrawIconWithHalo(
            Rect rect,
            Texture2D texture,
            float haloOffset,
            bool strong)
        {
            if (texture == null)
            {
                return;
            }

            Color previousColor = GUI.color;
            if (strong)
            {
                DrawHaloRing(
                    rect,
                    texture,
                    haloOffset * 1.65f,
                    new Color(0f, 0f, 0f, 0.62f));
            }

            DrawHaloRing(
                rect,
                texture,
                haloOffset,
                new Color(0f, 0f, 0f, strong ? 0.94f : 0.8f));
            GUI.color = Color.white;
            GUI.DrawTexture(
                rect,
                texture,
                ScaleMode.ScaleToFit,
                alphaBlend: true);
            GUI.color = previousColor;
        }

        private static void DrawHaloRing(
            Rect rect,
            Texture2D texture,
            float haloOffset,
            Color haloColor)
        {
            GUI.color = haloColor;
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    var shadowRect = rect;
                    shadowRect.position +=
                        new Vector2(x * haloOffset, y * haloOffset);
                    GUI.DrawTexture(
                        shadowRect,
                        texture,
                        ScaleMode.ScaleToFit,
                        alphaBlend: true);
                }
            }
        }

        private static void ReleaseRuntimeTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }

            texture = null;
        }
    }
}
