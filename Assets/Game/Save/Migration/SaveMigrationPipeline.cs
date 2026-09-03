using System;
using System.Collections.Generic;
using System.Linq;

namespace MSC.Save.Migration
{
    public sealed class SaveMigrationPipeline : ISaveMigrationPipeline
    {
        private readonly IReadOnlyDictionary<int, ISaveDocumentMigration> bySourceVersion;

        public SaveMigrationPipeline(IEnumerable<ISaveDocumentMigration> migrations)
        {
            Dictionary<int, ISaveDocumentMigration> map = new Dictionary<int, ISaveDocumentMigration>();
            foreach (ISaveDocumentMigration migration in migrations ?? Array.Empty<ISaveDocumentMigration>())
            {
                if (migration == null || migration.FromVersion < 1 || migration.ToVersion <= migration.FromVersion)
                {
                    throw new ArgumentException("Save migrations must be non-null and advance the document version.", nameof(migrations));
                }

                if (!map.TryAdd(migration.FromVersion, migration))
                {
                    throw new InvalidOperationException($"Duplicate migration from version {migration.FromVersion}.");
                }
            }

            bySourceVersion = map;
        }

        public SaveDocument MigrateToCurrent(SaveDocument source, UnresolvedContentReport unresolvedContent)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (unresolvedContent == null)
            {
                throw new ArgumentNullException(nameof(unresolvedContent));
            }

            SaveDocument current = source.DeepClone();
            HashSet<int> visited = new HashSet<int>();
            while (current.Header.DocumentVersion < SaveDocument.CurrentDocumentVersion)
            {
                int version = current.Header.DocumentVersion;
                if (!visited.Add(version))
                {
                    throw new InvalidOperationException("Save migration graph contains a cycle.");
                }

                if (!bySourceVersion.TryGetValue(version, out ISaveDocumentMigration migration))
                {
                    throw new NotSupportedException($"No save migration exists from document version {version}.");
                }

                SaveDocument migrated = migration.Migrate(current.DeepClone(), unresolvedContent);
                if (migrated?.Header == null || migrated.Header.DocumentVersion != migration.ToVersion)
                {
                    throw new InvalidOperationException(
                        $"Migration {migration.FromVersion}->{migration.ToVersion} returned an invalid document version.");
                }

                current = migrated.DeepClone();
            }

            if (current.Header.DocumentVersion != SaveDocument.CurrentDocumentVersion)
            {
                throw new NotSupportedException(
                    $"Save document version {current.Header.DocumentVersion} is newer than supported version {SaveDocument.CurrentDocumentVersion}.");
            }

            return current;
        }
    }
}
