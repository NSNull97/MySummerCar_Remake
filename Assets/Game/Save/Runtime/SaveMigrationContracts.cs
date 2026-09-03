using System;

namespace MSC.Save
{
    public interface ISaveDocumentMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        SaveDocument Migrate(SaveDocument source, UnresolvedContentReport unresolvedContent);
    }

    public interface ISaveMigrationPipeline
    {
        SaveDocument MigrateToCurrent(SaveDocument source, UnresolvedContentReport unresolvedContent);
    }

    public sealed class CurrentVersionSaveMigrationPipeline : ISaveMigrationPipeline
    {
        public SaveDocument MigrateToCurrent(SaveDocument source, UnresolvedContentReport unresolvedContent)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Header == null || source.Header.DocumentVersion != SaveDocument.CurrentDocumentVersion)
            {
                throw new NotSupportedException($"No migration path exists for save document version {source.Header?.DocumentVersion ?? 0}.");
            }

            return source.DeepClone();
        }
    }
}
