using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public sealed class UIReferenceOverlayWindow : EditorWindow
    {
        private const float SafeAreaMarginNormalized = 0.05f;

        private UIReferenceScreen selectedScreen;
        private UIReferenceDisplayMode displayMode = UIReferenceDisplayMode.Blended;
        private float referenceOpacityPercent = 50f;
        private bool showSafeArea = true;
        private bool showNormalizedGuides;
        private UIReferenceValidationResult validation;
        private Texture2D referenceTexture;
        private Texture2D implementationTexture;
        private string implementationPath;
        private string implementationStatus;
        private bool implementationIsCaptureCompatible;
        private Vector2 validationScroll;

        [MenuItem("Tools/My Summer Car/UI/Milestone 08A Reference Review")]
        public static void Open()
        {
            var window = GetWindow<UIReferenceOverlayWindow>();
            window.titleContent = new GUIContent("UI 08A Review");
            window.minSize = new Vector2(900f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            ValidateReferences();
            LoadSelectedScreen(useCanonicalImplementation: true);
        }

        private void OnDisable()
        {
            DestroyLoadedTextures();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawValidationStatus();
            DrawImplementationControls();
            DrawCaptureControls();
            DrawPreview();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var nextScreen = (UIReferenceScreen)EditorGUILayout.EnumPopup(
                    selectedScreen,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(135f));
                if (EditorGUI.EndChangeCheck())
                {
                    selectedScreen = nextScreen;
                    LoadSelectedScreen(useCanonicalImplementation: true);
                }

                displayMode = (UIReferenceDisplayMode)EditorGUILayout.EnumPopup(
                    displayMode,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(155f));
                GUILayout.Label("Reference opacity", GUILayout.Width(112f));
                referenceOpacityPercent = GUILayout.HorizontalSlider(
                    referenceOpacityPercent,
                    0f,
                    100f,
                    GUILayout.Width(150f));
                GUILayout.Label($"{referenceOpacityPercent:0}%", GUILayout.Width(38f));
                showSafeArea = GUILayout.Toggle(
                    showSafeArea,
                    new GUIContent("Safe area"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(75f));
                showNormalizedGuides = GUILayout.Toggle(
                    showNormalizedGuides,
                    new GUIContent("0.1 guides"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(80f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    ValidateReferences();
                    LoadSelectedScreen(useCanonicalImplementation: false);
                }
            }
        }

        private void DrawValidationStatus()
        {
            if (validation == null)
            {
                EditorGUILayout.HelpBox("Reference validation has not run.", MessageType.Warning);
                return;
            }

            if (validation.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "PASS: all 6 locked references match manifest SHA-256, byte size and 1672x941 dimensions.",
                    MessageType.Info);
                return;
            }

            validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.MaxHeight(105f));
            EditorGUILayout.HelpBox(
                "Approved reference validation failed. Capture output is disabled.\n" +
                string.Join("\n", validation.Issues),
                MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void DrawImplementationControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Implementation PNG");
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(implementationPath) ? "Not selected" : implementationPath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Choose...", GUILayout.Width(88f)))
                {
                    ChooseImplementation();
                }

                if (GUILayout.Button("Canonical", GUILayout.Width(88f)))
                {
                    LoadCanonicalImplementation();
                }
            }

            if (!string.IsNullOrWhiteSpace(implementationStatus))
            {
                EditorGUILayout.HelpBox(
                    implementationStatus,
                    implementationTexture == null ? MessageType.Warning : MessageType.None);
            }
        }

        private void DrawCaptureControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           validation == null ||
                           !validation.IsValid ||
                           !implementationIsCaptureCompatible))
                {
                    if (GUILayout.Button("Write selected review set (1672x941)"))
                    {
                        var result = UIReferenceCaptureUtility.GenerateScreenReviewSet(
                            UIReferencePaths.GetProjectRoot(),
                            selectedScreen,
                            implementationPath);
                        ReportCaptureResult(result);
                        LoadCanonicalImplementation();
                    }
                }

                using (new EditorGUI.DisabledScope(validation == null || !validation.IsValid))
                {
                    if (GUILayout.Button("Generate all 6 from canonical implementations"))
                    {
                        var result = UIReferenceCaptureUtility.GenerateAllReviewSets(
                            UIReferencePaths.GetProjectRoot());
                        ReportCaptureResult(result);
                        LoadCanonicalImplementation();
                    }
                }

                if (GUILayout.Button("Reveal review folder", GUILayout.Width(145f)))
                {
                    var reviewDirectory = UIReferencePaths.GetReviewDirectory(
                        UIReferencePaths.GetProjectRoot());
                    Directory.CreateDirectory(reviewDirectory);
                    EditorUtility.RevealInFinder(reviewDirectory);
                }
            }
        }

        private void DrawPreview()
        {
            var previewContainer = GUILayoutUtility.GetRect(
                200f,
                10000f,
                200f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(previewContainer, new Color(0.055f, 0.055f, 0.055f, 1f));

            var imageRect = FitAspect(
                previewContainer,
                (float)UIReferenceCatalog.CanonicalWidth / UIReferenceCatalog.CanonicalHeight);
            EditorGUI.DrawRect(imageRect, Color.black);

            switch (displayMode)
            {
                case UIReferenceDisplayMode.ReferenceOnly:
                    DrawTexture(referenceTexture, imageRect, referenceOpacityPercent / 100f);
                    break;
                case UIReferenceDisplayMode.ImplementationOnly:
                    DrawTexture(implementationTexture, imageRect, 1f);
                    break;
                case UIReferenceDisplayMode.Blended:
                    DrawTexture(implementationTexture, imageRect, 1f);
                    DrawTexture(referenceTexture, imageRect, referenceOpacityPercent / 100f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (showNormalizedGuides)
            {
                DrawNormalizedGuides(imageRect);
            }

            if (showSafeArea)
            {
                DrawSafeArea(imageRect);
            }

            if ((displayMode == UIReferenceDisplayMode.ImplementationOnly ||
                 displayMode == UIReferenceDisplayMode.Blended) &&
                implementationTexture == null)
            {
                var labelRect = new Rect(imageRect.x + 12f, imageRect.y + 12f, 420f, 44f);
                EditorGUI.HelpBox(labelRect, "Implementation PNG is not loaded.", MessageType.Warning);
            }
        }

        private void ValidateReferences()
        {
            validation = UIReferenceManifestValidator.ValidateProject(UIReferencePaths.GetProjectRoot());
            Repaint();
        }

        private void LoadSelectedScreen(bool useCanonicalImplementation)
        {
            DestroyTexture(ref referenceTexture);
            if (validation != null && validation.TryGetRecord(selectedScreen, out var record))
            {
                try
                {
                    referenceTexture = UIReferenceImageUtility.LoadPng(record.FilePath);
                }
                catch (Exception exception)
                {
                    validation = UIReferenceManifestValidator.ValidateProject(
                        UIReferencePaths.GetProjectRoot());
                    Debug.LogError($"Cannot load approved UI reference: {exception.Message}");
                }
            }

            if (useCanonicalImplementation)
            {
                LoadCanonicalImplementation();
            }

            Repaint();
        }

        private void ChooseImplementation()
        {
            var initialDirectory = UIReferencePaths.GetReviewDirectory(
                UIReferencePaths.GetProjectRoot());
            var path = EditorUtility.OpenFilePanel(
                "Choose implementation capture",
                initialDirectory,
                "png");
            if (!string.IsNullOrWhiteSpace(path))
            {
                LoadImplementation(path);
            }
        }

        private void LoadCanonicalImplementation()
        {
            LoadImplementation(
                UIReferencePaths.GetImplementationPath(
                    UIReferencePaths.GetProjectRoot(),
                    selectedScreen));
        }

        private void LoadImplementation(string path)
        {
            DestroyTexture(ref implementationTexture);
            implementationPath = path;
            implementationIsCaptureCompatible = false;

            if (!File.Exists(path))
            {
                implementationStatus =
                    "Canonical implementation capture is not present yet. " +
                    "Create it at 1672x941, then reload it here.";
                Repaint();
                return;
            }

            try
            {
                implementationTexture = UIReferenceImageUtility.LoadPng(path);
                var sourceAspect = (float)implementationTexture.width / implementationTexture.height;
                var canonicalAspect =
                    (float)UIReferenceCatalog.CanonicalWidth / UIReferenceCatalog.CanonicalHeight;
                var aspectMatches = Mathf.Abs(sourceAspect - canonicalAspect) <= 0.002f;
                implementationIsCaptureCompatible = aspectMatches;
                implementationStatus = aspectMatches
                    ? $"Loaded {implementationTexture.width}x{implementationTexture.height}; " +
                      "canonical export will preserve aspect and normalize to 1672x941."
                    : $"Loaded {implementationTexture.width}x{implementationTexture.height}; " +
                      "aspect mismatch. Canonical export is blocked to prevent cropping or stretching.";
            }
            catch (Exception exception)
            {
                implementationStatus = exception.Message;
                Debug.LogError($"Cannot load implementation PNG: {exception.Message}");
            }

            Repaint();
        }

        private static Rect FitAspect(Rect container, float aspect)
        {
            var width = container.width;
            var height = width / aspect;
            if (height > container.height)
            {
                height = container.height;
                width = height * aspect;
            }

            return new Rect(
                container.x + ((container.width - width) * 0.5f),
                container.y + ((container.height - height) * 0.5f),
                width,
                height);
        }

        private static void DrawTexture(Texture2D texture, Rect rect, float opacity)
        {
            if (texture == null || opacity <= 0f)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
            GUI.color = previousColor;
        }

        private static void DrawSafeArea(Rect rect)
        {
            var marginX = rect.width * SafeAreaMarginNormalized;
            var marginY = rect.height * SafeAreaMarginNormalized;
            var safeRect = new Rect(
                rect.x + marginX,
                rect.y + marginY,
                rect.width - (marginX * 2f),
                rect.height - (marginY * 2f));

            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.65f, 0.1f, 0.85f);
            Handles.DrawAAPolyLine(
                2f,
                new Vector3(safeRect.xMin, safeRect.yMin),
                new Vector3(safeRect.xMax, safeRect.yMin),
                new Vector3(safeRect.xMax, safeRect.yMax),
                new Vector3(safeRect.xMin, safeRect.yMax),
                new Vector3(safeRect.xMin, safeRect.yMin));
            Handles.EndGUI();
        }

        private static void DrawNormalizedGuides(Rect rect)
        {
            Handles.BeginGUI();
            for (var index = 1; index < 10; index++)
            {
                var alpha = index == 5 ? 0.65f : 0.25f;
                Handles.color = new Color(0.2f, 0.85f, 1f, alpha);
                var normalized = index / 10f;
                var x = Mathf.Lerp(rect.xMin, rect.xMax, normalized);
                var y = Mathf.Lerp(rect.yMin, rect.yMax, normalized);
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.EndGUI();
        }

        private static void ReportCaptureResult(UIReferenceCaptureResult result)
        {
            if (result.Succeeded)
            {
                Debug.Log(
                    $"Milestone 08A UI review captures written ({result.OutputPaths.Count}):\n" +
                    string.Join("\n", result.OutputPaths));
                return;
            }

            Debug.LogError("Milestone 08A UI review capture failed:\n" + string.Join("\n", result.Errors));
        }

        private void DestroyLoadedTextures()
        {
            DestroyTexture(ref referenceTexture);
            DestroyTexture(ref implementationTexture);
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }
        }
    }
}
