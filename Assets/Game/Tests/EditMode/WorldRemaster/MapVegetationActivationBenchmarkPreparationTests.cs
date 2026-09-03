using System;
using System.IO;
using System.Linq;
using MSC.Editor.Vegetation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationActivationBenchmarkPreparationTests
    {
        [Test]
        public void BenchmarkPreparation_UsesNonRepresentativeCellMode()
        {
            Assert.That(MapVegetationRebuild.RequiresRepresentativePilot(
                allCells: false,
                validateOnly: false,
                chooseRepresentativePilot: false), Is.False);
            Assert.That(MapVegetationRebuild.RequiresRepresentativePilot(
                allCells: false,
                validateOnly: false,
                chooseRepresentativePilot: true), Is.True);
            Assert.That(MapVegetationRebuild.RequiresRepresentativePilot(
                allCells: true,
                validateOnly: false,
                chooseRepresentativePilot: true), Is.False);
            Assert.That(MapVegetationRebuild.RequiresRepresentativePilot(
                allCells: false,
                validateOnly: true,
                chooseRepresentativePilot: true), Is.False);
        }

        [Test]
        public void ExecutePreservingEvidence_RestoresExactBytesAndRethrowsOriginalFailure()
        {
            string folder = TemporaryFolder();
            string accepted = folder + "/accepted.bin";
            string originallyMissing = folder + "/missing.bin";
            byte[] originalBytes = { 0, 255, 4, 17, 99, 128 };
            DateTime originalWriteUtc = new DateTime(
                2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            Directory.CreateDirectory(ProjectPath(folder));
            File.WriteAllBytes(ProjectPath(accepted), originalBytes);
            File.SetLastWriteTimeUtc(
                ProjectPath(accepted), originalWriteUtc);
            var expected = new InvalidOperationException("benchmark failed");

            try
            {
                InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                    () => MapVegetationActivationBenchmarkPreparation
                        .ExecutePreservingEvidence(
                            () =>
                            {
                                File.WriteAllText(ProjectPath(accepted), "stale");
                                File.WriteAllText(ProjectPath(originallyMissing), "created");
                                throw expected;
                            },
                            new[] { accepted, originallyMissing }));

                Assert.That(actual, Is.SameAs(expected));
                Assert.That(File.ReadAllBytes(ProjectPath(accepted)),
                    Is.EqualTo(originalBytes));
                Assert.That(File.GetLastWriteTimeUtc(ProjectPath(accepted)),
                    Is.EqualTo(originalWriteUtc));
                Assert.That(File.Exists(ProjectPath(originallyMissing)),
                    Is.False);
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        [Test]
        public void ExecutePreservingEvidence_RestoresAfterSuccess()
        {
            string folder = TemporaryFolder();
            string accepted = folder + "/accepted.json";
            string originallyMissing = folder + "/missing.json";
            const string original = "{\"passed\":true,\"runId\":\"accepted\"}";
            Directory.CreateDirectory(ProjectPath(folder));
            File.WriteAllText(ProjectPath(accepted), original);

            try
            {
                MapVegetationActivationBenchmarkPreparation
                    .ExecutePreservingEvidence(
                        () =>
                        {
                            File.WriteAllText(ProjectPath(accepted), "stale");
                            File.WriteAllText(ProjectPath(originallyMissing), "created");
                        },
                        new[] { accepted, originallyMissing });

                Assert.That(File.ReadAllText(ProjectPath(accepted)),
                    Is.EqualTo(original));
                Assert.That(File.Exists(ProjectPath(originallyMissing)),
                    Is.False);
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        [Test]
        public void ExecutePreservingEvidence_RestoresRemainingPathsWhenOneRestoreFails()
        {
            string folder = TemporaryFolder();
            string blocked = folder + "/blocked.bin";
            string accepted = folder + "/accepted.bin";
            byte[] acceptedBytes = { 7, 11, 19, 23 };
            DateTime acceptedWriteUtc = new DateTime(
                2024, 6, 7, 8, 9, 10, DateTimeKind.Utc);
            Directory.CreateDirectory(ProjectPath(folder));
            File.WriteAllBytes(ProjectPath(blocked), new byte[] { 1 });
            File.WriteAllBytes(ProjectPath(accepted), acceptedBytes);
            File.SetLastWriteTimeUtc(
                ProjectPath(accepted), acceptedWriteUtc);
            var expected = new InvalidOperationException("benchmark failed");

            try
            {
                AggregateException actual = Assert.Throws<AggregateException>(
                    () => MapVegetationActivationBenchmarkPreparation
                        .ExecutePreservingEvidence(
                            () =>
                            {
                                File.Delete(ProjectPath(blocked));
                                Directory.CreateDirectory(ProjectPath(blocked));
                                File.WriteAllText(
                                    ProjectPath(accepted), "stale");
                                throw expected;
                            },
                            new[] { blocked, accepted }));

                Assert.That(actual.InnerExceptions.Count, Is.EqualTo(2));
                Assert.That(actual.InnerExceptions[0], Is.SameAs(expected));
                Assert.That(File.ReadAllBytes(ProjectPath(accepted)),
                    Is.EqualTo(acceptedBytes));
                Assert.That(File.GetLastWriteTimeUtc(ProjectPath(accepted)),
                    Is.EqualTo(acceptedWriteUtc));
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        [Test]
        public void CaptureGeneratedDependencySeal_IsOrderedAndChangesWhenReferencedBytesChange()
        {
            string folder = TemporaryFolder();
            string firstPath = folder + "/z.asset";
            string secondPath = folder + "/a.asset";
            Directory.CreateDirectory(ProjectPath(folder));
            File.WriteAllBytes(ProjectPath(firstPath),
                new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(ProjectPath(secondPath),
                new byte[] { 8, 7, 6, 5 });

            try
            {
                MapVegetationActivationBenchmarkPreparation
                    .GeneratedDependencySeal initial =
                        MapVegetationActivationBenchmarkPreparation
                            .CaptureGeneratedDependencySeal(
                                new[]
                                {
                                    firstPath,
                                    secondPath,
                                    firstPath
                                });
                MapVegetationActivationBenchmarkPreparation
                    .GeneratedDependencySeal reordered =
                        MapVegetationActivationBenchmarkPreparation
                            .CaptureGeneratedDependencySeal(
                                new[] { secondPath, firstPath });

                Assert.That(initial.Errors, Is.Empty);
                Assert.That(initial.Dependencies.Select(item => item.path),
                    Is.EqualTo(new[] { secondPath, firstPath }));
                Assert.That(reordered.Fingerprint,
                    Is.EqualTo(initial.Fingerprint));
                Assert.That(reordered.Dependencies.Select(item => item.sha256),
                    Is.EqualTo(initial.Dependencies.Select(item =>
                        item.sha256)));

                File.WriteAllBytes(ProjectPath(secondPath),
                    new byte[] { 8, 7, 6, 4 });
                MapVegetationActivationBenchmarkPreparation
                    .GeneratedDependencySeal mutated =
                        MapVegetationActivationBenchmarkPreparation
                            .CaptureGeneratedDependencySeal(
                                new[] { firstPath, secondPath });

                Assert.That(mutated.Errors, Is.Empty);
                Assert.That(mutated.Dependencies[0].bytes,
                    Is.EqualTo(initial.Dependencies[0].bytes));
                Assert.That(mutated.Dependencies[0].sha256,
                    Is.Not.EqualTo(initial.Dependencies[0].sha256));
                Assert.That(mutated.Fingerprint,
                    Is.Not.EqualTo(initial.Fingerprint));
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        [Test]
        public void MissingRequiredGeneratedDependencyPaths_RequiresGrassDensityCatalogAndEveryPackedCategory()
        {
            const string cellId = "cell_1_-3";
            const string root =
                "Assets/Game/LegacyImport/RuntimeBaseline/" +
                "VegetationRebuild/Data/cell_1_-3/";
            string[] complete =
            {
                root + "GrassCell.asset",
                root + "Catalog.asset",
                root + "Density.asset",
                root + "PackedOriginalTrees.asset",
                root + "PackedBoundaryForest.asset",
                root + "PackedShrubsAndUndergrowth.asset"
            };

            Assert.That(MapVegetationActivationBenchmarkPreparation
                    .MissingRequiredGeneratedDependencyPaths(
                        cellId, complete),
                Is.Empty);

            string[] incomplete = complete.Where(path =>
                    !path.EndsWith("Density.asset", StringComparison.Ordinal) &&
                    !path.EndsWith(
                        "PackedBoundaryForest.asset",
                        StringComparison.Ordinal))
                .ToArray();
            Assert.That(MapVegetationActivationBenchmarkPreparation
                    .MissingRequiredGeneratedDependencyPaths(
                        cellId, incomplete),
                Is.EquivalentTo(new[]
                {
                    root + "Density.asset",
                    root + "PackedBoundaryForest.asset"
                }));
        }

        private static string TemporaryFolder() =>
            "Temp/VegetationEvidenceTests/" + Guid.NewGuid().ToString("N");

        private static string ProjectPath(string relativePath) =>
            Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", relativePath));

        private static void DeleteFolder(string relativePath)
        {
            string fullPath = ProjectPath(relativePath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }
}
