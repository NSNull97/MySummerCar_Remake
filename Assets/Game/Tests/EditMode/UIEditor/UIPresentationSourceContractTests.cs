using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public void ProjectOwnedMenuBackdrop_IsCanonicalAndNotAReferenceScreenshot()
        {
            const string backdropPath =
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png";
            Texture2D backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(backdropPath);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.width, Is.EqualTo(1672));
            Assert.That(backdrop.height, Is.EqualTo(941));
            Assert.That(backdropPath, Does.Not.Contain("References/UI/Approved"));

            var importer = AssetImporter.GetAtPath(backdropPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
        }

        [Test]
        public void ProjectOwnedMenuLogo_PreservesTransparentSourceAndCanonicalImport()
        {
            const string logoPath =
                "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png";
            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
            Assert.That(logo, Is.Not.Null);
            Assert.That(logo.width, Is.EqualTo(600));
            Assert.That(logo.height, Is.EqualTo(337));
            Assert.That(logoPath, Does.Not.Contain("References/UI/Approved"));

            var importer = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
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

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }
    }
}
