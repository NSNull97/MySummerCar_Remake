using System;
using System.Linq;
using MSC.Items;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Preserves accepted 09A saves by adding the item domain that did not
    /// exist before world items were introduced. No legacy state is guessed.
    /// </summary>
    internal sealed class Milestone09BSaveMigration : ISaveDocumentMigration
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "09B migration requires a version 1 save document.",
                    nameof(source));
            }

            if (source.Domains.Any(domain => string.Equals(
                    domain?.DomainId,
                    ItemSaveParticipant.DomainId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Version 1 save unexpectedly already contains the 09B item domain.");
            }

            SaveDocument migrated = source.DeepClone();
            migrated.Header.DocumentVersion = ToVersion;
            migrated.Domains = migrated.Domains
                .Concat(new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = ItemSaveParticipant.DomainId,
                        SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(
                            ItemDomainSaveDto.Empty()),
                    },
                })
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            return migrated;
        }
    }
}
