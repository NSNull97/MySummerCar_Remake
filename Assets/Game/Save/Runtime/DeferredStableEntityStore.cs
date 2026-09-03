using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MSC.Save
{
    [Serializable]
    public sealed class DeferredStableEntityPayload
    {
        public string StableEntityId = string.Empty;
        public string OwnerDomainId = string.Empty;
        public int SchemaVersion;
        public string PayloadJson = string.Empty;

        public DeferredStableEntityPayload DeepClone()
        {
            return (DeferredStableEntityPayload)MemberwiseClone();
        }
    }

    public sealed class DeferredStableEntityStore
    {
        private readonly Dictionary<string, DeferredStableEntityPayload> payloads =
            new Dictionary<string, DeferredStableEntityPayload>(StringComparer.Ordinal);

        public int Count => payloads.Count;

        public void Enqueue(DeferredStableEntityPayload payload, bool replaceExisting = false)
        {
            Validate(payload);
            string key = BuildKey(payload.OwnerDomainId, payload.StableEntityId);
            bool exists = payloads.ContainsKey(key);
            if (!exists && payloads.Count >= SaveLimits.MaximumDeferredEntities)
            {
                throw new InvalidDataException($"Deferred entity store exceeds {SaveLimits.MaximumDeferredEntities} entries.");
            }

            if (!replaceExisting && exists)
            {
                throw new InvalidOperationException($"Deferred state for '{payload.StableEntityId}' already exists.");
            }

            payloads[key] = payload.DeepClone();
        }

        public bool TryPeek(string stableEntityId, out DeferredStableEntityPayload payload)
        {
            DeferredStableEntityPayload[] matches = payloads.Values
                .Where(candidate => string.Equals(
                    candidate.StableEntityId,
                    stableEntityId ?? string.Empty,
                    StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length == 1)
            {
                payload = matches[0].DeepClone();
                return true;
            }

            payload = null;
            return false;
        }

        public bool TryTake(string stableEntityId, out DeferredStableEntityPayload payload)
        {
            if (TryPeek(stableEntityId, out payload))
            {
                payloads.Remove(BuildKey(payload.OwnerDomainId, payload.StableEntityId));
                return true;
            }

            return false;
        }

        public bool TryPeek(
            string ownerDomainId,
            string stableEntityId,
            out DeferredStableEntityPayload payload)
        {
            if (payloads.TryGetValue(
                    BuildKey(ownerDomainId, stableEntityId),
                    out DeferredStableEntityPayload stored))
            {
                payload = stored.DeepClone();
                return true;
            }

            payload = null;
            return false;
        }

        public bool TryTake(
            string ownerDomainId,
            string stableEntityId,
            out DeferredStableEntityPayload payload)
        {
            string key = BuildKey(ownerDomainId, stableEntityId);
            if (payloads.TryGetValue(key, out DeferredStableEntityPayload stored))
            {
                payloads.Remove(key);
                payload = stored.DeepClone();
                return true;
            }

            payload = null;
            return false;
        }

        public DeferredStableEntityPayload[] Snapshot()
        {
            return payloads.Values
                .OrderBy(payload => payload.StableEntityId, StringComparer.Ordinal)
                .ThenBy(payload => payload.OwnerDomainId, StringComparer.Ordinal)
                .Select(payload => payload.DeepClone())
                .ToArray();
        }

        public void Restore(IEnumerable<DeferredStableEntityPayload> snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Dictionary<string, DeferredStableEntityPayload> replacement =
                new Dictionary<string, DeferredStableEntityPayload>(StringComparer.Ordinal);
            foreach (DeferredStableEntityPayload payload in snapshot)
            {
                if (replacement.Count >= SaveLimits.MaximumDeferredEntities)
                {
                    throw new InvalidDataException($"Deferred entity snapshot exceeds {SaveLimits.MaximumDeferredEntities} entries.");
                }

                Validate(payload);
                string key = BuildKey(payload.OwnerDomainId, payload.StableEntityId);
                if (!replacement.TryAdd(key, payload.DeepClone()))
                {
                    throw new InvalidDataException(
                        $"Duplicate deferred state for '{payload.OwnerDomainId}/{payload.StableEntityId}'.");
                }
            }

            payloads.Clear();
            foreach (KeyValuePair<string, DeferredStableEntityPayload> pair in replacement)
            {
                payloads.Add(pair.Key, pair.Value);
            }
        }

        private static void Validate(DeferredStableEntityPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            SaveDocumentValidator.RequireBounded(payload.StableEntityId, nameof(payload.StableEntityId), SaveLimits.MaximumShortStringLength, false);
            SaveDomainId.Validate(payload.OwnerDomainId);
            if (payload.SchemaVersion < 1)
            {
                throw new InvalidDataException("Deferred entity schema version must be positive.");
            }

            if (payload.PayloadJson == null || Encoding.UTF8.GetByteCount(payload.PayloadJson) > SaveLimits.MaximumDomainPayloadBytes)
            {
                throw new InvalidDataException("Deferred entity payload is null or exceeds the payload limit.");
            }
        }

        private static string BuildKey(string ownerDomainId, string stableEntityId)
        {
            SaveDomainId.Validate(ownerDomainId);
            SaveDocumentValidator.RequireBounded(
                stableEntityId ?? string.Empty,
                nameof(stableEntityId),
                SaveLimits.MaximumShortStringLength,
                false);
            return ownerDomainId + "\u001f" + stableEntityId;
        }
    }
}
