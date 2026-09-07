using System;
using System.IO;
using System.Reflection;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DefaultSuskiBuildValidationTests
    {
        private const string BindingId = "presentation.character.suski";
        private const string PrefabPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters/StoryTraffic/" +
            "Generated/Prefabs/suski-better.prefab";

        [Test]
        public void RegisteredOverride_PassesBaseGuardAndRejectsOtherIdentities()
        {
            GameObject prefab = RequirePrivatePrefab();
            Assert.That(Phase1StoryTrafficPresentationImporter
                .TryValidateDefaultSuskiOverrideForBuild(BindingId, prefab), Is.True);
            Assert.DoesNotThrow(Phase1CharacterPresentationImporter.EnsureGeneratedForBuild);
            Assert.That(Phase1StoryTrafficPresentationImporter
                .TryValidateDefaultSuskiOverrideForBuild(
                    "presentation.character.suski-better-rescue", prefab), Is.False);

            GameObject clone = Object.Instantiate(prefab);
            try
            {
                clone.hideFlags = HideFlags.HideAndDontSave;
                Assert.That(Phase1StoryTrafficPresentationImporter
                    .TryValidateDefaultSuskiOverrideForBuild(BindingId, clone), Is.False,
                    "A matching component on an unregistered object is not an override.");
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [TestCase("missing-material")]
        [TestCase("wrong-material-order")]
        [TestCase("missing-mesh")]
        public void GeneratedContent_RejectsTamperedTransientRenderer(string tamper)
        {
            GameObject clone = Object.Instantiate(RequirePrivatePrefab());
            try
            {
                clone.hideFlags = HideFlags.HideAndDontSave;
                Assert.DoesNotThrow(() => ValidateContent(clone));
                SkinnedMeshRenderer renderer = clone
                    .GetComponentInChildren<SkinnedMeshRenderer>(true);
                Material[] materials = renderer.sharedMaterials;
                Assert.That(materials, Has.Length.EqualTo(2));
                switch (tamper)
                {
                    case "missing-material":
                        materials[0] = null;
                        renderer.sharedMaterials = materials;
                        break;
                    case "wrong-material-order":
                        renderer.sharedMaterials = new[] { materials[1], materials[0] };
                        break;
                    case "missing-mesh":
                        renderer.sharedMesh = null;
                        break;
                    default:
                        Assert.Fail("Unknown test mutation.");
                        break;
                }

                TargetInvocationException failure = Assert.Throws<TargetInvocationException>(
                    () => ValidateContent(clone));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [TestCase("wrong-surface")]
        [TestCase("missing-texture")]
        [TestCase("foreign-texture")]
        [TestCase("foreign-shader")]
        public void MaterialContent_RejectsTamperedTransientMaterial(string tamper)
        {
            Material original = RequirePrivatePrefab()
                .GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterials[0];
            Texture expectedTexture = original.GetTexture("_BaseColorMap");
            bool expectedTransparent = original.GetFloat("_SurfaceType") > 0.5f;
            Assert.That(expectedTexture, Is.Not.Null);
            var material = new Material(original) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D replacementTexture = null;
            try
            {
                Assert.That(ValidateSurface(material, expectedTexture, expectedTransparent),
                    Is.True, "The unmodified transient copy must retain valid material properties.");
                switch (tamper)
                {
                    case "wrong-surface":
                        material.SetFloat("_SurfaceType", expectedTransparent ? 0f : 1f);
                        break;
                    case "missing-texture":
                        material.SetTexture("_BaseColorMap", null);
                        break;
                    case "foreign-texture":
                        replacementTexture = new Texture2D(1, 1)
                        {
                            hideFlags = HideFlags.HideAndDontSave,
                        };
                        material.SetTexture("_BaseColorMap", replacementTexture);
                        break;
                    case "foreign-shader":
                        Shader shader = Shader.Find("UI/Default");
                        Assert.That(shader, Is.Not.Null);
                        material.shader = shader;
                        break;
                    default:
                        Assert.Fail("Unknown test mutation.");
                        break;
                }

                Assert.That(ValidateSurface(material, expectedTexture, expectedTransparent),
                    Is.False);
                Assert.That(original.GetTexture("_BaseColorMap"), Is.SameAs(expectedTexture));
                Assert.That(original.GetFloat("_SurfaceType"),
                    Is.EqualTo(expectedTransparent ? 1f : 0f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                if (replacementTexture != null)
                {
                    Object.DestroyImmediate(replacementTexture);
                }
            }
        }

        private static GameObject RequirePrivatePrefab()
        {
            if (!File.Exists("Config/DonorPaths.local.json"))
            {
                Assert.Ignore("Private BetterMSC staging is not configured.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Configured private content must be generated.");
            return prefab;
        }

        private static void ValidateContent(GameObject wrapper) =>
            Method("EnsureDefaultSuskiGeneratedContent").Invoke(null, new object[] { wrapper });

        private static bool ValidateSurface(Material material, Texture texture, bool transparent) =>
            (bool)Method("IsDefaultSuskiMaterialSurfaceValid").Invoke(
                null, new object[] { material, texture, transparent });

        private static MethodInfo Method(string name) =>
            typeof(Phase1StoryTrafficPresentationImporter).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new MissingMethodException(name);
    }
}
