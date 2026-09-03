using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MSC.Save
{
    public enum UnresolvedContentReason
    {
        UnknownOptionalDomain = 0,
        MissingStableEntity = 1,
        UnsupportedOptionalSchema = 2,
        DeferredUntilCellLoad = 3,
        ParticipantReported = 4,
    }

    [Serializable]
    public sealed class UnresolvedContentEntry
    {
        public string DomainId = string.Empty;
        public string StableEntityId = string.Empty;
        public UnresolvedContentReason Reason;
        public string Detail = string.Empty;

        public UnresolvedContentEntry DeepClone()
        {
            return (UnresolvedContentEntry)MemberwiseClone();
        }
    }

    public sealed class UnresolvedContentReport
    {
        private readonly List<UnresolvedContentEntry> entries = new List<UnresolvedContentEntry>();

        public IReadOnlyList<UnresolvedContentEntry> Entries => entries;
        public bool HasEntries => entries.Count > 0;

        public void Add(string domainId, string stableEntityId, UnresolvedContentReason reason, string detail)
        {
            SaveDomainId.Validate(domainId);
            if (entries.Count >= SaveLimits.MaximumUnresolvedEntries)
            {
                throw new InvalidDataException($"Unresolved content report exceeds {SaveLimits.MaximumUnresolvedEntries} entries.");
            }

            SaveDocumentValidator.RequireBounded(stableEntityId ?? string.Empty, nameof(stableEntityId), SaveLimits.MaximumShortStringLength, true);
            SaveDocumentValidator.RequireBounded(detail ?? string.Empty, nameof(detail), 1024, true);
            entries.Add(new UnresolvedContentEntry
            {
                DomainId = domainId,
                StableEntityId = stableEntityId ?? string.Empty,
                Reason = reason,
                Detail = detail ?? string.Empty,
            });
        }

        public UnresolvedContentEntry[] Snapshot()
        {
            return entries
                .OrderBy(entry => entry.DomainId, StringComparer.Ordinal)
                .ThenBy(entry => entry.StableEntityId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Reason)
                .Select(entry => entry.DeepClone())
                .ToArray();
        }
    }
}
