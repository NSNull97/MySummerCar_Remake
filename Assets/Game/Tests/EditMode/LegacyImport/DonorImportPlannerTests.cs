using System;
using System.IO;
using MSC.LegacyImport;
using MSC.LegacyImport.Editor.Pipeline;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorImportPlannerTests
    {
        [Test]
        public void PlanExecutePlan_IsIdempotent()
        {
            string root = Path.Combine(Path.GetTempPath(), "msc-import-plan-" + Guid.NewGuid().ToString("N"));
            string staging = Path.Combine(root, "staging");
            string project = Path.Combine(root, "project");
            string stagedRelative = "normalized/proof/environment_fixture.obj";
            string sourceFile = Path.Combine(staging, stagedRelative.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                Directory.CreateDirectory(project);
                File.WriteAllText(sourceFile, "synthetic mesh fixture");
                string hash = Sha256FileHasher.Compute(sourceFile);
                DonorAssetManifest manifest = DonorTestData.CreateManifest(
                    DonorTestData.CreateRecord(hash, stagedPath: stagedRelative));

                DonorImportPlan firstPlan = DonorImportPlanner.CreatePlan(manifest, staging, project);
                Assert.That(firstPlan.CanExecute, Is.True);
                Assert.That(firstPlan.Operations, Has.Count.EqualTo(1));
                Assert.That(firstPlan.Operations[0].Action, Is.EqualTo(DonorImportAction.Copy));

                DonorImportExecutor.Execute(firstPlan);

                DonorImportPlan secondPlan = DonorImportPlanner.CreatePlan(manifest, staging, project);
                Assert.That(secondPlan.CanExecute, Is.True);
                Assert.That(secondPlan.Operations[0].Action, Is.EqualTo(DonorImportAction.UpToDate));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Plan_RejectsProductionDestinationForReferenceImport()
        {
            const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            DonorAssetManifest manifest = DonorTestData.CreateManifest(
                DonorTestData.CreateRecord(
                    hash,
                    referencePath: "Assets/Game/World/Content/unsafe.fbx"));

            DonorImportPlan plan = DonorImportPlanner.CreatePlan(
                manifest,
                Path.GetTempPath(),
                Path.GetTempPath());

            Assert.That(plan.CanExecute, Is.False);
            Assert.That(plan.Errors, Is.Not.Empty);
        }

        [Test]
        public void Plan_BlocksHashMismatchWithoutWritingDestination()
        {
            string root = Path.Combine(Path.GetTempPath(), "msc-import-hash-" + Guid.NewGuid().ToString("N"));
            string staging = Path.Combine(root, "staging");
            string project = Path.Combine(root, "project");
            string sourceFile = Path.Combine(staging, "normalized", "proof", "environment_fixture.obj");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                Directory.CreateDirectory(project);
                File.WriteAllText(sourceFile, "unexpected bytes");
                DonorAssetManifest manifest = DonorTestData.CreateManifest(
                    DonorTestData.CreateRecord(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

                DonorImportPlan plan = DonorImportPlanner.CreatePlan(manifest, staging, project);

                Assert.That(plan.CanExecute, Is.False);
                Assert.That(plan.Operations[0].Action, Is.EqualTo(DonorImportAction.Blocked));
                Assert.That(File.Exists(plan.Operations[0].DestinationFile), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Plan_UsesStagedFileHashInsteadOfContainerHash()
        {
            string root = Path.Combine(Path.GetTempPath(), "msc-import-hash-role-" + Guid.NewGuid().ToString("N"));
            string staging = Path.Combine(root, "staging");
            string project = Path.Combine(root, "project");
            string sourceFile = Path.Combine(staging, "normalized", "proof", "environment_fixture.obj");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                Directory.CreateDirectory(project);
                File.WriteAllText(sourceFile, "synthetic normalized mesh");
                string stagedFileHash = Sha256FileHasher.Compute(sourceFile);
                const string containerHash =
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                DonorAssetManifest manifest = DonorTestData.CreateManifest(
                    DonorTestData.CreateRecord(
                        stagedFileHash,
                        sourceContainerHash: containerHash));

                DonorImportPlan plan = DonorImportPlanner.CreatePlan(manifest, staging, project);

                Assert.That(plan.CanExecute, Is.True);
                Assert.That(plan.Operations[0].Action, Is.EqualTo(DonorImportAction.Copy));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
