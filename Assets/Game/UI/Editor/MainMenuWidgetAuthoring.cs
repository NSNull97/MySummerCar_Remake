using System;
using System.Collections.Generic;
using MSC.UI.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MSC.UI.EditorTools
{
    /// <summary>
    /// Exports authorable widgets from the runtime's canonical constructors.
    /// The prefabs contain project-owned art and presentation state only;
    /// their host connects Button.onClick and its shared glass source.
    /// </summary>
    public static class MainMenuWidgetAuthoring
    {
        public const string OutputDirectory = "Assets/Game/UI/Presentation/Content/MainMenu/Widgets";
        private const string SpriteBankPath = OutputDirectory + "/MainMenuWidgetSprites.asset";

        [MenuItem("Tools/My Summer Car/UI/Main Menu/Rebuild Reusable Widgets")]
        public static void BuildAll()
        {
            EnsureFolder(OutputDirectory);
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject scratch = null;
            UiVisualAssets visualAssets = null;
            try
            {
                scratch = new GameObject("MainMenuWidgetAuthoring", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(scratch, preview);
                var frame = scratch.GetComponent<RectTransform>();
                frame.sizeDelta = new Vector2(UiThemeTokens.ReferenceWidth, UiThemeTokens.ReferenceHeight);
                visualAssets = new UiVisualAssets();
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(visualAssets.TextFont)))
                {
                    throw new InvalidOperationException(
                        "Widget prefabs require the existing imported project font. An OS font fallback cannot be serialized.");
                }

                var factory = new UiFactory(visualAssets);
                factory.ConfigureGlass(frame, surface => surface.Apply(null, MainMenuStyle.Surface));
                GameObject[] widgets =
                {
                    factory.MainMenuButton("MainMenuActionButton", frame, "НОВАЯ ИГРА", UiIconKind.Plus,
                        0f, 0f, 300f, 72f, null).gameObject,
                    factory.MainMenuButton("MainMenuCompactButton", frame, "НАСТРОЙКИ", UiIconKind.Gear,
                        0f, 0f, 180f, 54f, null, compact: true).gameObject,
                    factory.MainMenuPanel("MainMenuGlassPanel", frame, 0f, 0f, 354f, 220f),
                    factory.MainMenuSwatch("MainMenuColourSwatch", frame, new Color32(234, 217, 134, 255),
                        0f, 0f, 50f, true, null).gameObject,
                    factory.MainMenuGreeting("MainMenuGreetingPanel", frame, "Хорошего дня!",
                        0f, 0f, 238f, 62f),
                };

                var savedSprites = new Dictionary<Sprite, Sprite>();
                for (int index = 0; index < widgets.Length; index++)
                {
                    GameObject widget = widgets[index];
                    Image[] images = widget.GetComponentsInChildren<Image>(includeInactive: true);
                    for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
                    {
                        Image image = images[imageIndex];
                        if (image.sprite != null)
                        {
                            image.sprite = PersistSprite(image.sprite, savedSprites);
                        }
                    }

                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)widget.transform);
                    PrefabUtility.SaveAsPrefabAsset(widget, OutputDirectory + "/" + widget.name + ".prefab");
                }

                AssetDatabase.SaveAssets();
                Debug.Log("Main menu: exported five reusable widget prefabs with project-owned sprite assets.");
            }
            finally
            {
                if (scratch != null) UnityEngine.Object.DestroyImmediate(scratch);
                visualAssets?.Dispose();
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Sprite PersistSprite(Sprite source, Dictionary<Sprite, Sprite> savedSprites)
        {
            if (savedSprites.TryGetValue(source, out Sprite saved)) return saved;
            string key = source.name + "_" + source.texture.width + "x" + source.texture.height +
                "_r" + Mathf.RoundToInt(source.border.x);
            Sprite existing = null;
            Texture2D texture = null;
            if (AssetDatabase.LoadMainAssetAtPath(SpriteBankPath) == null)
            {
                var bank = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "MainMenuWidgetSprites",
                    filterMode = FilterMode.Bilinear,
                };
                bank.SetPixel(0, 0, Color.clear);
                bank.Apply();
                AssetDatabase.CreateAsset(bank, SpriteBankPath);
            }

            UnityEngine.Object[] bankAssets = AssetDatabase.LoadAllAssetsAtPath(SpriteBankPath);
            for (int index = 0; index < bankAssets.Length; index++)
            {
                if (bankAssets[index] is Sprite candidate && candidate.name == key) existing = candidate;
                if (bankAssets[index] is Texture2D candidateTexture && candidateTexture.name == key + "_Texture") texture = candidateTexture;
            }

            Texture2D copy = UnityEngine.Object.Instantiate(source.texture);
            copy.name = key + "_Texture";
            copy.hideFlags = HideFlags.None;
            if (texture == null)
            {
                texture = copy;
                AssetDatabase.AddObjectToAsset(texture, SpriteBankPath);
            }
            else
            {
                EditorUtility.CopySerialized(copy, texture);
                UnityEngine.Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(texture);
            }

            if (existing == null)
            {
                existing = Sprite.Create(texture, source.rect,
                    new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                existing.name = key;
                AssetDatabase.AddObjectToAsset(existing, SpriteBankPath);
            }

            savedSprites.Add(source, existing);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string parent = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string folder = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, segments[index]);
                parent = folder;
            }
        }
    }
}
