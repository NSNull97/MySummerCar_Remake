using System;

namespace MSC.Save.Integration
{
    /// <summary>Forward writer gate for schema-3 dynamic assembly ownership. Domain migration is definition-aware during preflight.</summary>
    internal sealed class SatsumaDynamicAssemblySaveMigration : ISaveDocumentMigration
    {
        public int FromVersion => 17;
        public int ToVersion => 18;

        public SaveDocument Migrate(SaveDocument source, UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null || source.Header.DocumentVersion != FromVersion)
                throw new ArgumentException("Dynamic assembly migration requires document version 17.", nameof(source));
            SaveDocument migrated = source.DeepClone();
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }
}
