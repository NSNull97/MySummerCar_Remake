using MSC.Core.Identity;
using NUnit.Framework;

namespace MSC.Tests.EditMode.Identity
{
    public sealed class StableEntityIdValidationTests
    {
        [Test]
        public void Validate_ReportsMissingInvalidAndDuplicateIds()
        {
            string duplicateId = StableEntityId.New().Value;
            var candidates = new[]
            {
                new StableEntityIdCandidate("first", duplicateId),
                new StableEntityIdCandidate("missing", string.Empty),
                new StableEntityIdCandidate("invalid", "not-an-id"),
                new StableEntityIdCandidate("duplicate", duplicateId)
            };

            var issues = StableEntityIdValidation.Validate(candidates);

            Assert.That(issues, Has.Count.EqualTo(3));
            Assert.That(issues[0].Kind, Is.EqualTo(StableEntityIdIssueKind.Missing));
            Assert.That(issues[1].Kind, Is.EqualTo(StableEntityIdIssueKind.Invalid));
            Assert.That(issues[2].Kind, Is.EqualTo(StableEntityIdIssueKind.Duplicate));
            Assert.That(issues[2].ConflictingContext, Is.EqualTo("first"));
        }

        [Test]
        public void Validate_AcceptsDistinctCanonicalIds()
        {
            var candidates = new[]
            {
                new StableEntityIdCandidate("one", StableEntityId.New().Value),
                new StableEntityIdCandidate("two", StableEntityId.New().Value)
            };

            Assert.That(StableEntityIdValidation.Validate(candidates), Is.Empty);
        }
    }
}
