using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MSC.UI.EditorTools;
using MSC.UI.Runtime.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Tests.EditMode.UIEditor
{
    public sealed class UIPresentationSourceContractTests
    {
        private static readonly Regex CatalogKeyPattern = new Regex(
            @"\[\""(?<key>ui\.[^\""\r\n]+)\""\]\s*=\s*new Entry",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex LiteralLookupPattern = new Regex(
            @"textCatalog\.Get\(\""(?<key>ui\.[^\""\r\n]+)\""\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [Test]
        public void PresentationLiteralLocalizationKeys_AllExistAndAreUnique()
        {
            string presentationDirectory = ProjectPath(
                "Assets/Game/UI/Presentation/Runtime");
            string catalogSource = File.ReadAllText(
                Path.Combine(presentationDirectory, "UiTextCatalog.cs"));
            MatchCollection catalogMatches = CatalogKeyPattern.Matches(catalogSource);
            var catalogKeys = new HashSet<string>();
            var duplicates = new List<string>();
            foreach (Match match in catalogMatches)
            {
                string key = match.Groups["key"].Value;
                if (!catalogKeys.Add(key))
                {
                    duplicates.Add(key);
                }
            }

            var usedKeys = new HashSet<string>();
            foreach (string path in Directory.GetFiles(
                         presentationDirectory,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                foreach (Match match in LiteralLookupPattern.Matches(File.ReadAllText(path)))
                {
                    usedKeys.Add(match.Groups["key"].Value);
                }
            }

            string[] missing = usedKeys.Where(key => !catalogKeys.Contains(key)).OrderBy(key => key).ToArray();
            Assert.That(duplicates, Is.Empty, "Duplicate UiTextCatalog keys: " + string.Join(", ", duplicates));
            Assert.That(missing, Is.Empty, "Missing UiTextCatalog entries: " + string.Join(", ", missing));
            Assert.That(catalogKeys.Count, Is.GreaterThanOrEqualTo(100));
        }

        [Test]
        public void StableLocalizationContractKeys_ArePresentInPresentationCatalog()
        {
            string catalogSource = File.ReadAllText(ProjectPath(
                "Assets/Game/UI/Presentation/Runtime/UiTextCatalog.cs"));
            var catalogKeys = CatalogKeyPattern.Matches(catalogSource)
                .Cast<Match>()
                .Select(match => match.Groups["key"].Value)
                .ToHashSet();

            string[] missing = UiLocalizationKeys.All
                .Where(key => !catalogKeys.Contains(key))
                .OrderBy(key => key)
                .ToArray();
            Assert.That(missing, Is.Empty, "Stable localization keys missing from catalog: " + string.Join(", ", missing));
        }

        [Test]
        public void RuntimeUiSources_DoNotReferenceApprovedReferencePixels()
        {
            string[] runtimeDirectories =
            {
                ProjectPath("Assets/Game/UI/Runtime"),
                ProjectPath("Assets/Game/UI/Presentation/Runtime"),
            };
            string[] forbidden =
            {
                "References/UI/Approved",
                "01_MAIN_MENU_APPROVED.png",
                "REFERENCE_MANIFEST.json",
            };

            var violations = new List<string>();
            foreach (string directory in runtimeDirectories)
            {
                foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path);
                    if (extension != ".cs" && extension != ".asmdef")
                    {
                        continue;
                    }

                    string source = File.ReadAllText(path);
                    if (forbidden.Any(source.Contains))
                    {
                        violations.Add(path);
                    }
                }
            }

            Assert.That(violations, Is.Empty, "Approved reference pixels leaked into runtime UI: " + string.Join(", ", violations));
        }

        [Test]
        public void CanonicalPlayerInput_ContainsPauseKeyboardAndGamepadBindings()
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Game/Player/Content/Input/M4_Player.inputactions");
            Assert.That(actions, Is.Not.Null);
            InputAction pause = actions.FindAction("System/Pause", throwIfNotFound: false);
            Assert.That(pause, Is.Not.Null);
            string[] paths = pause.bindings.Select(binding => binding.path).ToArray();
            Assert.That(paths, Does.Contain("<Keyboard>/escape"));
            Assert.That(paths, Does.Contain("<Gamepad>/start"));
        }

        [Test]
        public void CanonicalPlayerInput_ContainsLicensedHandGestures()
        {
            InputActionAsset actions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    "Assets/Game/Player/Content/Input/M4_Player.inputactions");
            Assert.That(actions, Is.Not.Null);

            AssertGestureBindings(
                actions,
                "Player/Wave",
                "<Keyboard>/h",
                "<Gamepad>/dpad/up");
            AssertGestureBindings(
                actions,
                "Player/MiddleFinger",
                "<Keyboard>/m",
                "<Gamepad>/dpad/right");
            AssertGestureBindings(
                actions,
                "Player/Swear",
                "<Keyboard>/n");
            AssertGestureBindings(
                actions,
                "Player/AlternativeActions",
                "<Keyboard>/leftAlt",
                "<Keyboard>/rightAlt");
        }

        [Test]
        public void ProductionMenu_UsesCanonicalMeshOnlyEnvironmentAndVehicleWithoutPhotoBackground()
        {
            // The existing Editor boundary inspects a separate Bootstrap preview
            // scene and checks canonical references plus null legacy photo/shader.
            // It closes that scene without modifying user scenes or build settings.
            Assert.DoesNotThrow(ProductionUiAuthoring.ValidateProductionBootstrap);
            AssertCanonicalMenuMeshPrefab(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MenuPreview/SatsumaMenuPreview.prefab",
                "MSC.UI.Presentation.MainMenuVehicleModel");
            AssertCanonicalMenuMeshPrefab(
                "Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/MainMenu/HomeYardMenuEnvironment.prefab",
                "MSC.UI.Presentation.MainMenuEnvironmentModel");
        }

        [Test]
        public void ProjectOwnedMenuLogo_PreservesTransparentSourceAndCanonicalImport()
        {
            const string logoPath =
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png";
            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
            Assert.That(logo, Is.Not.Null);
            Assert.That(logoPath, Does.Not.Contain("References/UI/Approved"));

            var importer = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            Assert.That(sourceWidth, Is.EqualTo(1536));
            Assert.That(sourceHeight, Is.EqualTo(1024));
            Assert.That((float)logo.width / logo.height,
                Is.EqualTo((float)sourceWidth / sourceHeight).Within(0.002f),
                "The current transparent logo must retain its source aspect after import-size limiting.");
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void GameplayUiFont_IsEmbeddedHelveticaNeueWithCyrillicCoverage()
        {
            const string fontPath =
                "Assets/Game/UI/Presentation/Resources/Fonts/HelveticaNeueRoman.otf";
            Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.name, Does.Contain("HelveticaNeue"));
            Assert.That(font.HasCharacter('A'), Is.True);
            Assert.That(font.HasCharacter('Ж'), Is.True);
            Assert.That(font.HasCharacter('я'), Is.True);

            var importer = AssetImporter.GetAtPath(fontPath) as TrueTypeFontImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.includeFontData, Is.True);
        }

        [Test]
        public void NeedIcons_UseCrispUncompressedNoMipmapImports()
        {
            string[] iconNames =
            {
                "droplet", "utensils", "brain",
                "toilet", "bed-double", "sparkles",
            };

            foreach (string iconName in iconNames)
            {
                string iconPath =
                    "Assets/Game/UI/Presentation/Resources/Icons/Needs/" +
                    iconName + ".png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                Assert.That(icon, Is.Not.Null, iconName);
                Assert.That(icon.width, Is.EqualTo(64), iconName);
                Assert.That(icon.height, Is.EqualTo(64), iconName);

                var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, iconName);
                Assert.That(importer.mipmapEnabled, Is.False, iconName);
                Assert.That(importer.alphaIsTransparency, Is.True, iconName);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), iconName);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), iconName);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    iconName);
            }
        }

        [Test]
        public void ContextActionIcons_UseAuditedCrispRuntimeImports()
        {
            string[] iconNames =
            {
                "check",
                "remove",
                "mouse",
                "mouse-left",
                "mouse-right",
                "mouse-middle",
                "mouse-scroll",
                "mouse-scroll-up",
                "mouse-scroll-down",
            };
            foreach (string iconName in iconNames)
            {
                string iconPath =
                    "Assets/Game/Player/Content/Icons/Interaction/" +
                    iconName + ".png";
                Texture2D icon =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                Assert.That(icon, Is.Not.Null, iconName);
                Assert.That(icon.width, Is.EqualTo(64), iconName);
                Assert.That(icon.height, Is.EqualTo(64), iconName);

                var importer =
                    AssetImporter.GetAtPath(iconPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, iconName);
                Assert.That(importer.mipmapEnabled, Is.False, iconName);
                Assert.That(importer.alphaIsTransparency, Is.True, iconName);
                Assert.That(
                    importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp),
                    iconName);
                Assert.That(
                    importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear),
                    iconName);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    iconName);
            }

            const string donorHandPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/" +
                "GameplayPresentation/UI/Interaction/DonorPickupHand.png";
            Texture2D donorHand =
                AssetDatabase.LoadAssetAtPath<Texture2D>(donorHandPath);
            Assert.That(donorHand, Is.Not.Null);
            Assert.That(donorHand.width, Is.EqualTo(32));
            Assert.That(donorHand.height, Is.EqualTo(32));
            var donorImporter =
                AssetImporter.GetAtPath(donorHandPath) as TextureImporter;
            Assert.That(donorImporter, Is.Not.Null);
            Assert.That(donorImporter.mipmapEnabled, Is.False);
            Assert.That(donorImporter.alphaIsTransparency, Is.True);
            Assert.That(
                donorImporter.wrapMode,
                Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(
                donorImporter.filterMode,
                Is.EqualTo(FilterMode.Bilinear));
            Assert.That(
                donorImporter.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(
                donorImporter.userData,
                Does.Contain("replacementKey=ui.interaction.pickup-hand"));

            const string donorGuid = "f627a294dcce4353b85d5269d8d90116";
            Assert.That(
                File.Exists(ProjectPath(
                    "Assets/Game/LegacyImport/Manifests/" +
                    "Phase1UiInteractionPresentationManifest.json")),
                Is.True);
            Assert.That(
                File.ReadAllText(ProjectPath(
                    "Assets/Game/Player/Content/Prefabs/" +
                    "M4_FirstPersonPlayer.prefab")),
                Does.Contain("guid: " + donorGuid));
        }

        [Test]
        public void ProjectOwnedGaussianBlurShader_IsImportedAndSupported()
        {
            const string shaderPath =
                "Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("Hidden/MSC/UI/SeparableGaussianBlur"));
            Assert.That(shader.isSupported, Is.True);
        }

        private static void AssertCanonicalMenuMeshPrefab(string path, string expectedRootModelType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            MonoBehaviour[] rootModels = prefab.GetComponents<MonoBehaviour>();
            Assert.That(rootModels, Has.Length.EqualTo(1), path);
            Assert.That(rootModels[0], Is.Not.Null, path);
            Assert.That(rootModels[0].GetType().FullName, Is.EqualTo(expectedRootModelType));
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                Assert.That(component != null && (component is Transform || component is MeshFilter ||
                    component is MeshRenderer || component == rootModels[0]), Is.True,
                    "Canonical menu presentation must not contain gameplay, physics, UI or audio components: " + component);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers, Is.Not.Empty, path);
            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null, path);
                Assert.That(filter.sharedMesh, Is.Not.Null, path);
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, path);
                Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0), path);
                Assert.That(renderer.sharedMaterials, Is.Not.Empty, path);
                foreach (Material material in renderer.sharedMaterials)
                    Assert.That(material != null && AssetDatabase.Contains(material), Is.True,
                        "Menu meshes must retain explicit canonical material assets: " + path);
            }
            string[] dependencies = AssetDatabase.GetDependencies(path, recursive: true);
            Assert.That(dependencies, Does.Not.Contain(
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png"));
            Assert.That(dependencies, Does.Not.Contain(
                "Assets/Game/UI/Presentation/Content/Shaders/MainMenuGaragePlate.shader"));
        }

        private static void AssertGestureBindings(
            InputActionAsset actions,
            string actionPath,
            string keyboardPath,
            string gamepadPath = null)
        {
            InputAction action = actions.FindAction(
                actionPath,
                throwIfNotFound: false);
            Assert.That(action, Is.Not.Null, actionPath);
            string[] paths = action.bindings
                .Select(binding => binding.path)
                .ToArray();
            Assert.That(paths, Does.Contain(keyboardPath), actionPath);
            if (!string.IsNullOrWhiteSpace(gamepadPath))
            {
                Assert.That(paths, Does.Contain(gamepadPath), actionPath);
            }
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }
    }
}
