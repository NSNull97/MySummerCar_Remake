using System;
using System.IO;
using System.Linq;
using MSC.Editor.WorldBaseline;
using MSC.Editor.WorldTransfer;
using NUnit.Framework;
using UnityEditor;

namespace MSC.Tests.EditMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class DonorWorldBaselineEditModeTests
    {
        [SetUp]
        public void RequireLocalDonorBaseline()
        {
            string configPath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldTransferPaths.LocalConfigRelativePath);
            string manifestPath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.SourceManifest);
            string scenePath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.CanonicalScene);
            if (!File.Exists(configPath) ||
                !File.Exists(manifestPath) ||
                !File.Exists(scenePath))
            {
                Assert.Ignore(
                    "Local donor staging and generated RuntimeBaseline payload " +
                    "are intentionally absent from a clean clone.");
            }
        }

        [Test]
        public void SanitationPlan_HasLockedCountsAndExplicitReasons()
        {
            var plan = WorldBaselineSanitationPlan.Load();

            Assert.That(
                plan.Count,
                Is.EqualTo(
                    WorldBaselineSanitationPlan.ExpectedSourceEntityCount));
            Assert.That(
                plan.Count(entry => entry.IncludeRenderer),
                Is.EqualTo(
                    WorldBaselineSanitationPlan.ExpectedRendererEntityCount));
            Assert.That(
                plan.Count(entry => !entry.IncludeRenderer),
                Is.EqualTo(
                    WorldBaselineSanitationPlan.ExpectedMetadataOnlyEntityCount));
            Assert.That(
                plan.Where(entry => entry.IncludeRenderer)
                    .All(entry =>
                        entry.Disposition == "RendererAccepted" &&
                        entry.Reason.StartsWith(
                            "WhitelistedStaticMesh",
                            StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                plan.Where(entry => !entry.IncludeRenderer)
                    .All(entry =>
                        entry.Disposition == "MetadataOnly" &&
                        !string.IsNullOrWhiteSpace(entry.Reason)),
                Is.True);
            Assert.That(
                plan.Count(entry => entry.EffectiveActive),
                Is.EqualTo(
                    WorldBaselineSanitationPlan
                        .ExpectedEffectiveActiveEntityCount));
            Assert.That(
                plan.Count(entry =>
                    entry.SourceActiveSelf && !entry.EffectiveActive),
                Is.EqualTo(
                    WorldBaselineSanitationPlan
                        .ExpectedActiveSelfUnderInactiveAncestorCount));
            Assert.That(
                plan.Count(entry =>
                    entry.Reason == "CharacterHierarchyExcluded"),
                Is.EqualTo(
                    WorldBaselineSanitationPlan
                        .ExpectedCharacterHierarchyEntityCount));
        }

        [Test]
        public void SourceManifest_IsPortableAndLocksCanonicalScene()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.SourceManifest);
            string json = File.ReadAllText(path);
            DonorWorldBaselineSourceManifestData manifest =
                DonorWorldBaselineManifest.Read();

            Assert.That(json, Does.Not.Contain("E:\\"));
            Assert.That(json, Does.Not.Contain("D:\\"));
            Assert.That(json, Does.Not.Contain("C:\\Users\\"));
            Assert.That(
                manifest.sourceRevisionId,
                Is.EqualTo(WorldBaselinePaths.SourceRevisionId));
            Assert.That(
                manifest.extractedSceneSha256,
                Is.EqualTo(WorldBaselinePaths.SourceSceneSha256));
            Assert.That(
                manifest.classification,
                Is.EqualTo("TemporaryDirectImport"));
            Assert.That(
                manifest.activationState,
                Is.EqualTo("PreparedNotActiveUntil06B2"));
            Assert.That(
                manifest.sourceFiles.Single(
                    record => record.role == "CanonicalExtractedScene").sha256,
                Is.EqualTo(WorldBaselinePaths.SourceSceneSha256));
        }

        [Test]
        public void CanonicalBaseline_PassesSanitationValidation()
        {
            DonorWorldBaselineValidationResult validation =
                DonorWorldBaselineValidator.Validate(
                    verifySourceHashes: false);

            Assert.That(validation.Errors, Is.Empty);
            Assert.That(
                validation.EntityCount,
                Is.EqualTo(
                    WorldBaselineSanitationPlan.ExpectedSourceEntityCount));
            Assert.That(
                validation.RendererCount,
                Is.EqualTo(
                    WorldBaselineSanitationPlan.ExpectedRendererEntityCount));
            Assert.That(validation.ColliderCount, Is.Zero);
        }

        [Test]
        public void CanonicalSourceScene_IsNotInBuildSettings_While06B2StreamingScenesRemainActive()
        {
            Assert.That(
                EditorBuildSettings.scenes.Any(scene =>
                    string.Equals(
                        scene.path,
                        WorldBaselinePaths.CanonicalScene,
                        StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                EditorBuildSettings.scenes.Count(scene =>
                    scene.enabled &&
                    scene.path.StartsWith(
                        WorldBaseline06B2Paths.StreamingSceneRoot + "/",
                        StringComparison.Ordinal)),
                Is.EqualTo(50));
        }
    }
}
