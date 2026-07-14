using System;
using System.IO;
using MSC.LegacyImport.Editor.Validation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorPipelineRootValidatorTests
    {
        [Test]
        public void DistinctSiblingRoots_AreAccepted()
        {
            string root = Path.Combine(Path.GetTempPath(), "msc-roots-" + Guid.NewGuid().ToString("N"));

            var errors = DonorPipelineRootValidator.Validate(
                Path.Combine(root, "donor"),
                Path.Combine(root, "staging"),
                Path.Combine(root, "project"));

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void StagingInsideDonor_IsRejected()
        {
            string donor = Path.Combine(Path.GetTempPath(), "msc-donor-" + Guid.NewGuid().ToString("N"));

            var errors = DonorPipelineRootValidator.Validate(
                donor,
                Path.Combine(donor, "staging"),
                Path.Combine(Path.GetTempPath(), "msc-project-" + Guid.NewGuid().ToString("N")));

            Assert.That(errors, Is.Not.Empty);
        }
    }
}
