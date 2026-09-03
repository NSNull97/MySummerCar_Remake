using System;
using MSC.Save.Migration;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveMigrationPipelineTests
    {
        [Test]
        public void CurrentDocument_ReturnsIndependentCopy()
        {
            SaveDocument source = SaveTestData.CreateDocument("slot-a");
            SaveMigrationPipeline pipeline = new SaveMigrationPipeline(Array.Empty<ISaveDocumentMigration>());

            SaveDocument result = pipeline.MigrateToCurrent(source, new UnresolvedContentReport());

            Assert.That(result, Is.Not.SameAs(source));
            Assert.That(result.Header, Is.Not.SameAs(source.Header));
            Assert.That(result.Header.DocumentVersion, Is.EqualTo(SaveDocument.CurrentDocumentVersion));
        }

        [Test]
        public void NewerDocument_IsRejectedWithoutMutation()
        {
            SaveDocument source = SaveTestData.CreateDocument("slot-a");
            source.Header.DocumentVersion = SaveDocument.CurrentDocumentVersion + 1;
            SaveMigrationPipeline pipeline = new SaveMigrationPipeline(Array.Empty<ISaveDocumentMigration>());

            Assert.That(
                () => pipeline.MigrateToCurrent(source, new UnresolvedContentReport()),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(SaveDocument.CurrentDocumentVersion + 1));
        }

        [Test]
        public void DuplicateSourceVersion_IsRejectedAtRegistration()
        {
            Assert.That(
                () => new SaveMigrationPipeline(new ISaveDocumentMigration[]
                {
                    new StubMigration(1, 2),
                    new StubMigration(1, 3),
                }),
                Throws.TypeOf<InvalidOperationException>());
        }

        private sealed class StubMigration : ISaveDocumentMigration
        {
            public StubMigration(int from, int to)
            {
                FromVersion = from;
                ToVersion = to;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }

            public SaveDocument Migrate(SaveDocument source, UnresolvedContentReport unresolvedContent)
            {
                SaveDocument result = source.DeepClone();
                result.Header.DocumentVersion = ToVersion;
                return result;
            }
        }
    }
}
