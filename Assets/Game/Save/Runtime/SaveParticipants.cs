using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MSC.Save
{
    public enum SaveRestorePhase
    {
        GlobalState = 0,
        Environment = 100,
        ItemInstances = 150,
        WorldEntities = 200,
        VehicleAssembly = 300,
        VehicleSimulation = 400,
        Player = 500,
        CarryState = 600,
        PresentationSync = 700,
    }

    public sealed class SaveParticipantDescriptor
    {
        public SaveParticipantDescriptor(
            string domainId,
            int schemaVersion,
            bool required,
            SaveRestorePhase restorePhase,
            params string[] dependencies)
        {
            SaveDomainId.Validate(domainId);
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            DomainId = domainId;
            SchemaVersion = schemaVersion;
            Required = required;
            RestorePhase = restorePhase;
            Dependencies = (dependencies ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string dependency in Dependencies)
            {
                SaveDomainId.Validate(dependency);
                if (string.Equals(dependency, domainId, StringComparison.Ordinal))
                {
                    throw new ArgumentException("A save participant cannot depend on itself.", nameof(dependencies));
                }
            }
        }

        public string DomainId { get; }
        public int SchemaVersion { get; }
        public bool Required { get; }
        public SaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }

    public sealed class SaveRestorePreparationContext
    {
        private readonly IReadOnlyDictionary<string, SaveDomainEnvelope> envelopes;

        public SaveRestorePreparationContext(UnresolvedContentReport unresolvedContent, DeferredStableEntityStore deferredEntities)
            : this(unresolvedContent, deferredEntities, null)
        {
        }

        public SaveRestorePreparationContext(UnresolvedContentReport unresolvedContent, DeferredStableEntityStore deferredEntities,
            IReadOnlyDictionary<string, SaveDomainEnvelope> siblingEnvelopes)
        {
            UnresolvedContent = unresolvedContent ?? throw new ArgumentNullException(nameof(unresolvedContent));
            DeferredEntities = deferredEntities ?? throw new ArgumentNullException(nameof(deferredEntities));
            envelopes = siblingEnvelopes;
        }

        public UnresolvedContentReport UnresolvedContent { get; }
        public DeferredStableEntityStore DeferredEntities { get; }

        /// <summary>A detached copy; participants cannot rewrite another domain during preparation.</summary>
        public bool TryGetEnvelope(string domainId, out SaveDomainEnvelope envelope)
        {
            envelope = null;
            if (envelopes == null || !envelopes.TryGetValue(domainId, out SaveDomainEnvelope saved)) return false;
            envelope = saved.DeepClone();
            return true;
        }
    }

    public sealed class SaveRestoreContext
    {
        public SaveRestoreContext(UnresolvedContentReport unresolvedContent, DeferredStableEntityStore deferredEntities)
        {
            UnresolvedContent = unresolvedContent ?? throw new ArgumentNullException(nameof(unresolvedContent));
            DeferredEntities = deferredEntities ?? throw new ArgumentNullException(nameof(deferredEntities));
        }

        public UnresolvedContentReport UnresolvedContent { get; }
        public DeferredStableEntityStore DeferredEntities { get; }
    }

    public interface ISaveParticipant
    {
        SaveParticipantDescriptor Descriptor { get; }
        /// <summary>Returns deterministic canonical JSON for the participant's current state.</summary>
        string CapturePayload();

        /// <summary>Parses and validates without mutating runtime state.</summary>
        object PrepareRestore(SaveDomainEnvelope envelope, SaveRestorePreparationContext context);

        object CaptureCheckpoint();
        void ApplyPreparedRestore(object preparedState, SaveRestoreContext context);
        void Rollback(object checkpoint);
    }

    /// <summary>Optional cross-domain preparation. The supplied document is a private copy; no runtime mutation is allowed.</summary>
    public interface ISaveRestorePlanFactory
    {
        SaveRestorePlan Prepare(SaveDocument document, SaveRestorePreparationContext context);
    }

    public sealed class SaveRestorePlan
    {
        public SaveRestorePlan(SaveDocument document, ISaveRestoreTransaction transaction)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Transaction = transaction;
        }
        public SaveDocument Document { get; }
        public ISaveRestoreTransaction Transaction { get; }
    }

    /// <summary>Retains live cross-domain objects until all participant Apply calls succeed.</summary>
    public interface ISaveRestoreTransaction
    {
        void Begin();
        // Reestablish object identities and memberships before reverse participant rollback.
        void BeforeRollback();
        void Rollback();
        // Final cleanup only; implementations must not throw or start another fallible restore phase.
        void Commit();
    }

    /// <summary>Optional boundary for settling transient operations after validation and before any runtime snapshot.</summary>
    public interface ISaveRestoreCheckpointBoundary
    {
        void BeforeCaptureCheckpoints();
    }

    public sealed class PreparedSaveRestore
    {
        internal PreparedSaveRestore(IReadOnlyList<PreparedParticipant> participants, ISaveRestoreTransaction transaction = null)
        {
            Participants = participants;
            Transaction = transaction;
        }

        internal IReadOnlyList<PreparedParticipant> Participants { get; }
        internal ISaveRestoreTransaction Transaction { get; }
    }

    internal sealed class PreparedParticipant
    {
        public ISaveParticipant Participant;
        public object PreparedState;
        public object Checkpoint;
    }

    public sealed class SaveParticipantRegistry
    {
        private readonly Dictionary<string, ISaveParticipant> byDomainId;
        private readonly ISaveParticipant[] ordered;
        private readonly ISaveRestorePlanFactory restorePlanFactory;

        public SaveParticipantRegistry(IEnumerable<ISaveParticipant> participants) : this(participants, null)
        {
        }

        public SaveParticipantRegistry(IEnumerable<ISaveParticipant> participants, ISaveRestorePlanFactory restorePlanFactory)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            byDomainId = new Dictionary<string, ISaveParticipant>(StringComparer.Ordinal);
            this.restorePlanFactory = restorePlanFactory;
            foreach (ISaveParticipant participant in participants)
            {
                if (participant?.Descriptor == null)
                {
                    throw new ArgumentException("Save participants and descriptors cannot be null.", nameof(participants));
                }

                if (!byDomainId.TryAdd(participant.Descriptor.DomainId, participant))
                {
                    throw new InvalidOperationException($"Duplicate save participant '{participant.Descriptor.DomainId}'.");
                }
            }

            ordered = TopologicallyOrder(byDomainId).ToArray();
        }

        public IReadOnlyList<ISaveParticipant> OrderedParticipants => ordered;

        public SaveDomainEnvelope[] CaptureDomains()
        {
            SaveDomainEnvelope[] result = new SaveDomainEnvelope[ordered.Length];
            for (int index = 0; index < ordered.Length; index++)
            {
                ISaveParticipant participant = ordered[index];
                string payload = participant.CapturePayload();
                if (payload == null || System.Text.Encoding.UTF8.GetByteCount(payload) > SaveLimits.MaximumDomainPayloadBytes)
                {
                    throw new InvalidDataException($"Participant '{participant.Descriptor.DomainId}' returned a null or oversized payload.");
                }

                result[index] = new SaveDomainEnvelope
                {
                    DomainId = participant.Descriptor.DomainId,
                    SchemaVersion = participant.Descriptor.SchemaVersion,
                    Required = participant.Descriptor.Required,
                    PayloadJson = payload,
                };
            }

            return result.OrderBy(envelope => envelope.DomainId, StringComparer.Ordinal).ToArray();
        }

        public PreparedSaveRestore PrepareRestore(
            SaveDocument document,
            UnresolvedContentReport unresolvedContent,
            DeferredStableEntityStore deferredEntities)
        {
            SaveDocumentValidator.Validate(document, true);
            ISaveRestoreTransaction transaction = null;
            if (restorePlanFactory != null)
            {
                SaveDocument privateDocument = document.DeepClone();
                var sourceEnvelopes = privateDocument.Domains.ToDictionary(value => value.DomainId, StringComparer.Ordinal);
                SaveRestorePlan plan = restorePlanFactory.Prepare(privateDocument,
                    new SaveRestorePreparationContext(unresolvedContent, deferredEntities, sourceEnvelopes))
                    ?? throw new InvalidOperationException("Restore plan factory returned no plan.");
                document = plan.Document;
                transaction = plan.Transaction;
                SaveDocumentValidator.Validate(document, true);
            }
            Dictionary<string, SaveDomainEnvelope> envelopes = document.Domains.ToDictionary(
                envelope => envelope.DomainId,
                StringComparer.Ordinal);

            foreach (SaveDomainEnvelope envelope in document.Domains)
            {
                if (!byDomainId.ContainsKey(envelope.DomainId))
                {
                    if (envelope.Required)
                    {
                        throw new InvalidDataException($"Required save domain '{envelope.DomainId}' has no registered participant.");
                    }

                    unresolvedContent.Add(
                        envelope.DomainId,
                        string.Empty,
                        UnresolvedContentReason.UnknownOptionalDomain,
                        "Optional domain was retained but not applied by this build.");
                }
            }

            List<PreparedParticipant> prepared = new List<PreparedParticipant>();
            HashSet<string> preparedDomainIds = new HashSet<string>(StringComparer.Ordinal);
            SaveRestorePreparationContext context = new SaveRestorePreparationContext(unresolvedContent, deferredEntities, envelopes);
            foreach (ISaveParticipant participant in ordered)
            {
                SaveParticipantDescriptor descriptor = participant.Descriptor;
                if (!envelopes.TryGetValue(descriptor.DomainId, out SaveDomainEnvelope envelope))
                {
                    if (descriptor.Required)
                    {
                        throw new InvalidDataException($"Save document is missing required domain '{descriptor.DomainId}'.");
                    }

                    continue;
                }

                if (envelope.SchemaVersion != descriptor.SchemaVersion)
                {
                    if (envelope.Required || descriptor.Required)
                    {
                        throw new NotSupportedException(
                            $"Domain '{descriptor.DomainId}' schema {envelope.SchemaVersion} is not supported; expected {descriptor.SchemaVersion}.");
                    }

                    unresolvedContent.Add(
                        descriptor.DomainId,
                        string.Empty,
                        UnresolvedContentReason.UnsupportedOptionalSchema,
                        $"Schema {envelope.SchemaVersion} is not supported by schema {descriptor.SchemaVersion} participant.");
                    continue;
                }

                foreach (string dependency in descriptor.Dependencies)
                {
                    if (!preparedDomainIds.Contains(dependency))
                    {
                        throw new InvalidDataException(
                            $"Domain '{descriptor.DomainId}' cannot restore because dependency '{dependency}' is missing or unavailable.");
                    }
                }

                prepared.Add(new PreparedParticipant
                {
                    Participant = participant,
                    PreparedState = participant.PrepareRestore(envelope.DeepClone(), context),
                });
                preparedDomainIds.Add(descriptor.DomainId);
            }

            // No runtime state has been mutated before every payload has passed preparation.
            (transaction as ISaveRestoreCheckpointBoundary)?.BeforeCaptureCheckpoints();
            foreach (PreparedParticipant entry in prepared)
            {
                entry.Checkpoint = entry.Participant.CaptureCheckpoint();
            }

            return new PreparedSaveRestore(prepared, transaction);
        }

        public void ApplyRestore(
            PreparedSaveRestore preparedRestore,
            UnresolvedContentReport unresolvedContent,
            DeferredStableEntityStore deferredEntities)
        {
            if (preparedRestore == null)
            {
                throw new ArgumentNullException(nameof(preparedRestore));
            }

            SaveRestoreContext context = new SaveRestoreContext(unresolvedContent, deferredEntities);
            List<PreparedParticipant> attempted = new List<PreparedParticipant>();
            try
            {
                preparedRestore.Transaction?.Begin();
                foreach (PreparedParticipant entry in preparedRestore.Participants)
                {
                    attempted.Add(entry);
                    entry.Participant.ApplyPreparedRestore(entry.PreparedState, context);
                }
            }
            catch (Exception applyFailure)
            {
                List<Exception> rollbackFailures = new List<Exception>();
                try { preparedRestore.Transaction?.BeforeRollback(); }
                catch (Exception rollbackFailure) { rollbackFailures.Add(rollbackFailure); }
                for (int index = attempted.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        attempted[index].Participant.Rollback(attempted[index].Checkpoint);
                    }
                    catch (Exception rollbackFailure)
                    {
                        rollbackFailures.Add(rollbackFailure);
                    }
                }

                try { preparedRestore.Transaction?.Rollback(); }
                catch (Exception rollbackFailure) { rollbackFailures.Add(rollbackFailure); }

                if (rollbackFailures.Count > 0)
                {
                    rollbackFailures.Insert(0, applyFailure);
                    throw new AggregateException("Save restore failed and one or more participants failed to roll back.", rollbackFailures);
                }

                throw;
            }

            // Destruction is outside the rollbackable phase and after every participant succeeded.
            preparedRestore.Transaction?.Commit();
        }

        private static IEnumerable<ISaveParticipant> TopologicallyOrder(
            IReadOnlyDictionary<string, ISaveParticipant> participants)
        {
            Dictionary<string, int> incoming = participants.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
            Dictionary<string, List<string>> outgoing = participants.Keys.ToDictionary(
                id => id,
                _ => new List<string>(),
                StringComparer.Ordinal);

            foreach (ISaveParticipant participant in participants.Values)
            {
                foreach (string dependency in participant.Descriptor.Dependencies)
                {
                    if (!participants.ContainsKey(dependency))
                    {
                        throw new InvalidOperationException(
                            $"Participant '{participant.Descriptor.DomainId}' depends on unregistered domain '{dependency}'.");
                    }

                    incoming[participant.Descriptor.DomainId]++;
                    outgoing[dependency].Add(participant.Descriptor.DomainId);
                }
            }

            List<ISaveParticipant> ready = participants.Values
                .Where(participant => incoming[participant.Descriptor.DomainId] == 0)
                .OrderBy(participant => participant.Descriptor.RestorePhase)
                .ThenBy(participant => participant.Descriptor.DomainId, StringComparer.Ordinal)
                .ToList();
            List<ISaveParticipant> result = new List<ISaveParticipant>(participants.Count);

            while (ready.Count > 0)
            {
                ISaveParticipant next = ready[0];
                ready.RemoveAt(0);
                result.Add(next);
                foreach (string dependentId in outgoing[next.Descriptor.DomainId])
                {
                    incoming[dependentId]--;
                    if (incoming[dependentId] == 0)
                    {
                        ready.Add(participants[dependentId]);
                    }
                }

                ready = ready
                    .OrderBy(participant => participant.Descriptor.RestorePhase)
                    .ThenBy(participant => participant.Descriptor.DomainId, StringComparer.Ordinal)
                    .ToList();
            }

            if (result.Count != participants.Count)
            {
                throw new InvalidOperationException("Save participant dependency graph contains a cycle.");
            }

            return result;
        }
    }
}
