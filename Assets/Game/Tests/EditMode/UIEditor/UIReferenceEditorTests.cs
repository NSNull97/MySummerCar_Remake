using System;
using System.IO;
using MSC.UI.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.UIEditor
{
    public sealed class UIReferenceEditorTests
    {
        [Test]
        public void ApprovedManifest_ValidatesAllSixLockedReferences()
        {
            var result = UIReferenceManifestValidator.ValidateProject(
                UIReferencePaths.GetProjectRoot());

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.Records, Has.Count.EqualTo(6));
        }

        [Test]
        public void ApprovedPngHeaderAndHash_MatchValidatedRecord()
        {
            var projectRoot = UIReferencePaths.GetProjectRoot();
            var result = UIReferenceManifestValidator.ValidateProject(projectRoot);
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.TryGetRecord(UIReferenceScreen.MainMenu, out var record), Is.True);

            Assert.That(
                UIReferenceManifestValidator.TryReadPngDimensions(
                    record.FilePath,
                    out var width,
                    out var height,
                    out var error),
                Is.True,
                error);
            Assert.That(width, Is.EqualTo(UIReferenceCatalog.CanonicalWidth));
            Assert.That(height, Is.EqualTo(UIReferenceCatalog.CanonicalHeight));
            Assert.That(
                UIReferenceManifestValidator.ComputeSha256(record.FilePath),
                Is.EqualTo(record.Sha256));
        }

        [Test]
        public void Validator_ReportsMissingApprovedReferenceFiles()
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"MSC_UI08A_MissingReferences_{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                var sourceManifest = UIReferencePaths.GetManifestPath(
                    UIReferencePaths.GetProjectRoot());
                var temporaryManifest = Path.Combine(
                    temporaryDirectory,
                    UIReferencePaths.ManifestFileName);
                File.Copy(sourceManifest, temporaryManifest);

                var result = UIReferenceManifestValidator.Validate(
                    temporaryManifest,
                    temporaryDirectory);

                Assert.That(result.IsValid, Is.False);
                Assert.That(
                    string.Join("\n", result.Issues),
                    Does.Contain("Approved reference is missing"));
            }
            finally
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void Blend_AtHalfOpacity_ProducesMidpointPixels()
        {
            var implementation = CreateSolidTexture(new Color32(0, 20, 40, 255));
            var reference = CreateSolidTexture(new Color32(200, 220, 240, 255));
            Texture2D blended = null;

            try
            {
                blended = UIReferenceImageUtility.Blend(implementation, reference, 0.5f);
                var pixel = blended.GetPixels32()[0];
                Assert.That(pixel.r, Is.EqualTo(100));
                Assert.That(pixel.g, Is.EqualTo(120));
                Assert.That(pixel.b, Is.EqualTo(140));
                Assert.That(pixel.a, Is.EqualTo(255));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blended);
                UnityEngine.Object.DestroyImmediate(reference);
                UnityEngine.Object.DestroyImmediate(implementation);
            }
        }

        [Test]
        public void CanonicalReviewPaths_AreOutsideAssetsAndUseLockedNames()
        {
            var projectRoot = UIReferencePaths.GetProjectRoot();
            var assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets")) +
                             Path.DirectorySeparatorChar;

            foreach (var descriptor in UIReferenceCatalog.All)
            {
                var implementationPath = Path.GetFullPath(
                    UIReferencePaths.GetImplementationPath(projectRoot, descriptor.Screen));
                var referencePath = Path.GetFullPath(
                    UIReferencePaths.GetReferenceCapturePath(projectRoot, descriptor.Screen));
                var blendedPath = Path.GetFullPath(
                    UIReferencePaths.GetBlendedCapturePath(projectRoot, descriptor.Screen));

                Assert.That(
                    implementationPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase),
                    Is.False);
                Assert.That(Path.GetFileName(implementationPath), Is.EqualTo(
                    $"{descriptor.ReviewStem}_Implementation.png"));
                Assert.That(Path.GetFileName(referencePath), Is.EqualTo(
                    $"{descriptor.ReviewStem}_ReferenceOnly.png"));
                Assert.That(Path.GetFileName(blendedPath), Is.EqualTo(
                    $"{descriptor.ReviewStem}_Blended50.png"));
            }
        }

        [Test]
        public void Normalize_RejectsNonCanonicalAspectInsteadOfCroppingOrStretching()
        {
            var texture = new Texture2D(4, 3, TextureFormat.RGBA32, false);
            try
            {
                var exception = Assert.Throws<InvalidDataException>(
                    () => UIReferenceImageUtility.NormalizeToCanonical(texture));
                Assert.That(exception.Message, Does.Contain("does not match canonical"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixels32(new[] { color });
            texture.Apply();
            return texture;
        }
    }
}
