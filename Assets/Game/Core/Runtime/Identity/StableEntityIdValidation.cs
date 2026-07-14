using System;
using System.Collections.Generic;

namespace MSC.Core.Identity
{
    public enum StableEntityIdIssueKind
    {
        Missing,
        Invalid,
        Duplicate
    }

    public readonly struct StableEntityIdCandidate
    {
        public StableEntityIdCandidate(string context, string serializedId)
        {
            Context = context ?? string.Empty;
            SerializedId = serializedId ?? string.Empty;
        }

        public string Context { get; }

        public string SerializedId { get; }
    }

    public readonly struct StableEntityIdIssue
    {
        public StableEntityIdIssue(
            StableEntityIdIssueKind kind,
            string context,
            string serializedId,
            string conflictingContext = "")
        {
            Kind = kind;
            Context = context ?? string.Empty;
            SerializedId = serializedId ?? string.Empty;
            ConflictingContext = conflictingContext ?? string.Empty;
        }

        public StableEntityIdIssueKind Kind { get; }

        public string Context { get; }

        public string SerializedId { get; }

        public string ConflictingContext { get; }
    }

    public static class StableEntityIdValidation
    {
        public static IReadOnlyList<StableEntityIdIssue> Validate(
            IEnumerable<StableEntityIdCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var issues = new List<StableEntityIdIssue>();
            var firstContextById = new Dictionary<StableEntityId, string>();

            foreach (StableEntityIdCandidate candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.SerializedId))
                {
                    issues.Add(new StableEntityIdIssue(
                        StableEntityIdIssueKind.Missing,
                        candidate.Context,
                        candidate.SerializedId));
                    continue;
                }

                if (!StableEntityId.TryParse(candidate.SerializedId, out StableEntityId parsedId))
                {
                    issues.Add(new StableEntityIdIssue(
                        StableEntityIdIssueKind.Invalid,
                        candidate.Context,
                        candidate.SerializedId));
                    continue;
                }

                if (firstContextById.TryGetValue(parsedId, out string firstContext))
                {
                    issues.Add(new StableEntityIdIssue(
                        StableEntityIdIssueKind.Duplicate,
                        candidate.Context,
                        candidate.SerializedId,
                        firstContext));
                    continue;
                }

                firstContextById.Add(parsedId, candidate.Context);
            }

            return issues;
        }
    }
}
