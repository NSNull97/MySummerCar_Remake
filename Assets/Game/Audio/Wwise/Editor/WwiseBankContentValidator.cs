using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MSC.Audio.Wwise.Editor
{
    public enum WwiseBankContentFailure
    {
        InvalidRoute,
        MissingBank,
        MissingMetadata,
        InvalidBank,
        InvalidMetadata,
        MissingEvent,
        EmptyPlaybackEvent,
    }

    public sealed class WwiseBankContentIssue
    {
        public WwiseBankContentIssue(
            string stableId, string bankName, WwiseBankContentFailure kind, string message)
        {
            StableId = stableId;
            BankName = bankName;
            Kind = kind;
            Message = message;
        }

        public string StableId { get; }
        public string BankName { get; }
        public WwiseBankContentFailure Kind { get; }
        public string Message { get; }
        public override string ToString() => $"{Kind}: {StableId} [{BankName}]: {Message}";
    }

    public sealed class WwiseBankContentReport
    {
        private readonly List<WwiseBankContentIssue> issues = new List<WwiseBankContentIssue>();
        public IReadOnlyList<WwiseBankContentIssue> Issues => issues;
        public int InspectedRouteCount { get; internal set; }
        public bool Succeeded => issues.Count == 0;
        internal void Add(WwiseBankContentIssue issue) => issues.Add(issue);

        public void ThrowIfFailed()
        {
            if (!Succeeded)
            {
                throw new InvalidDataException(
                    "Wwise bank content validation failed:\n" +
                    string.Join("\n", issues));
            }
        }
    }

    /// <summary>
    /// Editor/build evidence, not a runtime bank lease or proof of audible output.
    /// Callers supply only their explicit Wwise routes; Unity/deferred routes are
    /// never inferred or silently excluded here. Checks metadata against actual
    /// binary event objects so a stale JSON cannot certify a missing Event.
    /// </summary>
    public static class WwiseBankContentValidator
    {
        public static WwiseBankContentReport Inspect(
            string bankRoot, IReadOnlyList<AudioEventMapEntry> expectedWwiseRoutes)
        {
            if (string.IsNullOrWhiteSpace(bankRoot))
                throw new ArgumentException("A bank root is required.", nameof(bankRoot));
            if (expectedWwiseRoutes == null)
                throw new ArgumentNullException(nameof(expectedWwiseRoutes));

            var report = new WwiseBankContentReport();
            var banks = new Dictionary<string, BankEvidence>(StringComparer.Ordinal);
            for (int index = 0; index < expectedWwiseRoutes.Count; index++)
            {
                AudioEventMapEntry route = expectedWwiseRoutes[index];
                report.InspectedRouteCount++;
                string bankName = route?.RequiredBankName ?? string.Empty;
                if (route == null || !route.TryGetId(out _, out _) ||
                    string.IsNullOrWhiteSpace(route.BackendEventName) || !IsSafeBankName(bankName))
                {
                    report.Add(new WwiseBankContentIssue(route?.StableId ?? string.Empty,
                        bankName, WwiseBankContentFailure.InvalidRoute,
                        $"Route {index} needs a valid stable ID, backend event and plain bank name."));
                    continue;
                }

                if (!banks.TryGetValue(bankName, out BankEvidence bank))
                {
                    bank = ReadBank(bankRoot, bankName);
                    banks.Add(bankName, bank);
                }

                if (bank.Failure.HasValue)
                {
                    report.Add(new WwiseBankContentIssue(route.StableId, bankName,
                        bank.Failure.Value, bank.Message));
                    continue;
                }

                if (!bank.Events.TryGetValue(route.BackendEventName, out EventMetadata eventData) ||
                    !uint.TryParse(eventData.Id, NumberStyles.None, CultureInfo.InvariantCulture,
                        out uint eventId) || !bank.EventIds.Contains(eventId))
                {
                    report.Add(new WwiseBankContentIssue(route.StableId, bankName,
                        WwiseBankContentFailure.MissingEvent,
                        $"'{route.BackendEventName}' is not present in both bank metadata and binary HIRC events."));
                    continue;
                }

                // Play_/Stop_ are the explicit project authoring-name contract.
                // Stops intentionally have no media. Switch-container branches
                // hold media below the event, not in its top-level MediaRefs.
                if (route.BackendEventName.StartsWith("Play_", StringComparison.Ordinal) &&
                    !HasMedia(eventData.MediaRefs, eventData.SwitchContainers))
                {
                    report.Add(new WwiseBankContentIssue(route.StableId, bankName,
                        WwiseBankContentFailure.EmptyPlaybackEvent,
                        $"'{route.BackendEventName}' has no generated media references; a valid event ID alone does not prove playback."));
                }
            }

            return report;
        }

        private static bool IsSafeBankName(string bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName)) return false;
            for (int index = 0; index < bankName.Length; index++)
            {
                char c = bankName[index];
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' ||
                      c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            }

            return true;
        }

        private static BankEvidence ReadBank(string bankRoot, string bankName)
        {
            var result = new BankEvidence();
            string binaryPath = Path.Combine(bankRoot, bankName + ".bnk");
            string metadataPath = Path.Combine(bankRoot, bankName + ".json");
            if (!File.Exists(binaryPath)) return result.Fail(
                WwiseBankContentFailure.MissingBank, $"Missing '{binaryPath}'.");
            if (!File.Exists(metadataPath)) return result.Fail(
                WwiseBankContentFailure.MissingMetadata, $"Missing '{metadataPath}'.");

            uint binaryBankId;
            try
            {
                binaryBankId = ReadEventIds(binaryPath, result.EventIds);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException)
            {
                return result.Fail(WwiseBankContentFailure.InvalidBank, exception.Message);
            }

            try
            {
                MetadataDocument document = JsonUtility.FromJson<MetadataDocument>(
                    File.ReadAllText(metadataPath));
                BankMetadata[] metadataBanks = document?.SoundBanksInfo?.SoundBanks;
                if (metadataBanks == null || metadataBanks.Length != 1 ||
                    !string.Equals(metadataBanks[0]?.ShortName, bankName, StringComparison.Ordinal) ||
                    !uint.TryParse(metadataBanks[0].Id, NumberStyles.None,
                        CultureInfo.InvariantCulture, out uint metadataBankId) ||
                    metadataBankId != binaryBankId)
                {
                    return result.Fail(WwiseBankContentFailure.InvalidMetadata,
                        $"Metadata '{metadataPath}' does not identify the corresponding binary bank.");
                }

                foreach (EventMetadata eventData in metadataBanks[0].Events ?? Array.Empty<EventMetadata>())
                {
                    if (eventData == null || string.IsNullOrWhiteSpace(eventData.Name) ||
                        result.Events.ContainsKey(eventData.Name))
                        return result.Fail(WwiseBankContentFailure.InvalidMetadata,
                            $"Null, unnamed or duplicate Event in '{metadataPath}'.");
                    result.Events.Add(eventData.Name, eventData);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException)
            {
                return result.Fail(WwiseBankContentFailure.InvalidMetadata, exception.Message);
            }

            return result;
        }

        private static uint ReadEventIds(string path, HashSet<uint> eventIds)
        {
            bool hasHeader = false;
            bool hasHierarchy = false;
            uint bankId = 0;
            using (FileStream stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    if (stream.Length - stream.Position < 8)
                        throw new InvalidDataException($"Truncated bank chunk header: '{path}'.");
                    string tag = new string(reader.ReadChars(4));
                    uint size = reader.ReadUInt32();
                    long end = stream.Position + size;
                    if (end > stream.Length)
                        throw new InvalidDataException($"Truncated {tag} chunk: '{path}'.");

                    if (tag == "BKHD")
                    {
                        if (hasHeader || size < 8)
                            throw new InvalidDataException($"Invalid bank header: '{path}'.");
                        // Fail explicitly on a future format rather than certify
                        // an unreviewed hierarchy layout after an SDK upgrade.
                        if (reader.ReadUInt32() != 172)
                            throw new InvalidDataException($"Unreviewed Wwise bank format: '{path}'.");
                        bankId = reader.ReadUInt32();
                        hasHeader = true;
                    }
                    else if (tag == "HIRC")
                    {
                        if (hasHierarchy || size < 4)
                            throw new InvalidDataException($"Invalid hierarchy chunk: '{path}'.");
                        hasHierarchy = true;
                        uint count = reader.ReadUInt32();
                        for (uint index = 0; index < count; index++)
                        {
                            if (end - stream.Position < 9)
                                throw new InvalidDataException($"Truncated hierarchy object: '{path}'.");
                            byte type = reader.ReadByte();
                            uint objectSize = reader.ReadUInt32();
                            long objectEnd = stream.Position + objectSize;
                            if (objectSize < 4 || objectEnd > end)
                                throw new InvalidDataException($"Invalid hierarchy object length: '{path}'.");
                            uint id = reader.ReadUInt32();
                            // Verify actual included Event identities only. The
                            // version-172 event payload has additional fields;
                            // interpreting a guessed action-count offset would
                            // falsely mark valid banks silent. Media evidence
                            // below comes from metadata, not binary graph proof.
                            if (type == 4) eventIds.Add(id);
                            stream.Position = objectEnd;
                        }

                        if (stream.Position != end)
                            throw new InvalidDataException($"Hierarchy object count mismatch: '{path}'.");
                    }

                    stream.Position = end;
                }
            }

            if (!hasHeader || !hasHierarchy)
                throw new InvalidDataException($"Bank has no BKHD/HIRC evidence: '{path}'.");
            return bankId;
        }

        private static bool HasMedia(MediaReference[] media, SwitchContainerMetadata[] containers)
        {
            foreach (MediaReference reference in media ?? Array.Empty<MediaReference>())
            {
                if (reference != null && uint.TryParse(reference.Id, out uint id) && id != 0)
                    return true;
            }

            foreach (SwitchContainerMetadata container in containers ?? Array.Empty<SwitchContainerMetadata>())
            {
                if (container != null && HasMedia(container.MediaRefs, container.SwitchContainers))
                    return true;
            }

            return false;
        }

        private sealed class BankEvidence
        {
            public readonly Dictionary<string, EventMetadata> Events =
                new Dictionary<string, EventMetadata>(StringComparer.Ordinal);
            public readonly HashSet<uint> EventIds = new HashSet<uint>();
            public WwiseBankContentFailure? Failure;
            public string Message;
            public BankEvidence Fail(WwiseBankContentFailure failure, string message)
            {
                Failure = failure;
                Message = message;
                return this;
            }
        }

        // Generated JSON schema is project tooling data; never loaded as gameplay state.
#pragma warning disable 0649
        [Serializable] private sealed class MetadataDocument { public SoundBanksMetadata SoundBanksInfo; }
        [Serializable] private sealed class SoundBanksMetadata { public BankMetadata[] SoundBanks; }
        [Serializable] private sealed class BankMetadata
        {
            public string Id;
            public string ShortName;
            public EventMetadata[] Events;
        }
        [Serializable] private sealed class EventMetadata
        {
            public string Id;
            public string Name;
            public MediaReference[] MediaRefs;
            public SwitchContainerMetadata[] SwitchContainers;
        }
        [Serializable] private sealed class MediaReference { public string Id; }
        [Serializable] private sealed class SwitchContainerMetadata
        {
            public MediaReference[] MediaRefs;
            public SwitchContainerMetadata[] SwitchContainers;
        }
#pragma warning restore 0649
    }
}
