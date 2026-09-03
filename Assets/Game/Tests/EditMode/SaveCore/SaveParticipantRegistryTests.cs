using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveParticipantRegistryTests
    {
        [Test]
        public void Registry_OrdersByDependenciesAndPhaseDeterministically()
        {
            FakeParticipant player = new FakeParticipant("player.state", SaveRestorePhase.Player, "world.entities");
            FakeParticipant time = new FakeParticipant("core.time", SaveRestorePhase.GlobalState);
            FakeParticipant world = new FakeParticipant("world.entities", SaveRestorePhase.WorldEntities, "core.time");

            SaveParticipantRegistry registry = new SaveParticipantRegistry(new ISaveParticipant[] { player, world, time });

            Assert.That(
                registry.OrderedParticipants.Select(value => value.Descriptor.DomainId),
                Is.EqualTo(new[] { "core.time", "world.entities", "player.state" }));
        }

        [Test]
        public void PrepareFailure_DoesNotMutateAnyParticipant()
        {
            FakeParticipant first = new FakeParticipant("core.time", SaveRestorePhase.GlobalState) { State = 10 };
            FakeParticipant second = new FakeParticipant("world.entities", SaveRestorePhase.WorldEntities, "core.time")
            {
                State = 20,
                ThrowDuringPrepare = true,
            };
            SaveParticipantRegistry registry = new SaveParticipantRegistry(new[] { first, second });
            SaveDocument document = DocumentFor(first, second);

            Assert.That(
                () => registry.PrepareRestore(document, new UnresolvedContentReport(), new DeferredStableEntityStore()),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(first.State, Is.EqualTo(10));
            Assert.That(second.State, Is.EqualTo(20));
            Assert.That(first.ApplyCount, Is.Zero);
        }

        [Test]
        public void ApplyFailure_RollsBackEveryAttemptedParticipantInReverseOrder()
        {
            List<string> calls = new List<string>();
            FakeParticipant first = new FakeParticipant("core.time", SaveRestorePhase.GlobalState, calls: calls) { State = 10 };
            FakeParticipant second = new FakeParticipant("world.entities", SaveRestorePhase.WorldEntities, "core.time", calls)
            {
                State = 20,
                ThrowDuringApply = true,
            };
            SaveParticipantRegistry registry = new SaveParticipantRegistry(new[] { first, second });
            UnresolvedContentReport unresolved = new UnresolvedContentReport();
            DeferredStableEntityStore deferred = new DeferredStableEntityStore();
            PreparedSaveRestore prepared = registry.PrepareRestore(DocumentFor(first, second), unresolved, deferred);

            Assert.That(
                () => registry.ApplyRestore(prepared, unresolved, deferred),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(first.State, Is.EqualTo(10));
            Assert.That(second.State, Is.EqualTo(20));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "apply:core.time",
                "apply:world.entities",
                "rollback:world.entities",
                "rollback:core.time",
            }));
        }

        [Test]
        public void UnknownOptionalDomain_IsReportedWithoutBlockingRestore()
        {
            FakeParticipant time = new FakeParticipant("core.time", SaveRestorePhase.GlobalState);
            SaveParticipantRegistry registry = new SaveParticipantRegistry(new[] { time });
            SaveDocument document = DocumentFor(time);
            document.Domains = document.Domains.Concat(new[]
            {
                SaveTestData.Domain("future.optional", "{}", false),
            }).ToArray();
            UnresolvedContentReport report = new UnresolvedContentReport();

            PreparedSaveRestore prepared = registry.PrepareRestore(document, report, new DeferredStableEntityStore());

            Assert.That(prepared, Is.Not.Null);
            Assert.That(report.Entries, Has.Count.EqualTo(1));
            Assert.That(report.Entries[0].Reason, Is.EqualTo(UnresolvedContentReason.UnknownOptionalDomain));
        }

        [Test]
        public void DeferredStore_SnapshotsInStableIdOrderAndConsumesOnce()
        {
            DeferredStableEntityStore store = new DeferredStableEntityStore();
            store.Enqueue(Entity("entity-z"));
            store.Enqueue(Entity("entity-a"));

            Assert.That(store.Snapshot().Select(value => value.StableEntityId), Is.EqualTo(new[] { "entity-a", "entity-z" }));
            Assert.That(store.TryTake("entity-a", out DeferredStableEntityPayload payload), Is.True);
            Assert.That(payload.StableEntityId, Is.EqualTo("entity-a"));
            Assert.That(store.TryTake("entity-a", out _), Is.False);
        }

        [Test]
        public void DeferredStore_AllowsIndependentDomainStateForOneStableEntity()
        {
            DeferredStableEntityStore store = new DeferredStableEntityStore();
            DeferredStableEntityPayload world = Entity("entity-a");
            DeferredStableEntityPayload carry = Entity("entity-a");
            carry.OwnerDomainId = "interaction.carry";

            store.Enqueue(world);
            store.Enqueue(carry);

            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(
                store.TryTake("world.entities", "entity-a", out DeferredStableEntityPayload restoredWorld),
                Is.True);
            Assert.That(restoredWorld.OwnerDomainId, Is.EqualTo("world.entities"));
            Assert.That(
                store.TryTake("interaction.carry", "entity-a", out DeferredStableEntityPayload restoredCarry),
                Is.True);
            Assert.That(restoredCarry.OwnerDomainId, Is.EqualTo("interaction.carry"));
        }

        private static SaveDocument DocumentFor(params FakeParticipant[] participants)
        {
            SaveDocument document = SaveTestData.CreateDocument("slot-a");
            document.Domains = participants.Select(participant => new SaveDomainEnvelope
            {
                DomainId = participant.Descriptor.DomainId,
                SchemaVersion = participant.Descriptor.SchemaVersion,
                Required = participant.Descriptor.Required,
                PayloadJson = "42",
            }).ToArray();
            return document;
        }

        private static DeferredStableEntityPayload Entity(string id)
        {
            return new DeferredStableEntityPayload
            {
                StableEntityId = id,
                OwnerDomainId = "world.entities",
                SchemaVersion = 1,
                PayloadJson = "{}",
            };
        }

        private sealed class FakeParticipant : ISaveParticipant
        {
            private readonly List<string> calls;

            public FakeParticipant(
                string domainId,
                SaveRestorePhase phase,
                string dependency = null,
                List<string> calls = null)
            {
                this.calls = calls;
                Descriptor = new SaveParticipantDescriptor(
                    domainId,
                    1,
                    true,
                    phase,
                    dependency == null ? Array.Empty<string>() : new[] { dependency });
            }

            public SaveParticipantDescriptor Descriptor { get; }
            public int State { get; set; }
            public int ApplyCount { get; private set; }
            public bool ThrowDuringPrepare { get; set; }
            public bool ThrowDuringApply { get; set; }

            public string CapturePayload() => State.ToString(System.Globalization.CultureInfo.InvariantCulture);

            public object PrepareRestore(SaveDomainEnvelope envelope, SaveRestorePreparationContext context)
            {
                if (ThrowDuringPrepare)
                {
                    throw new InvalidDataException("prepare failed");
                }

                return int.Parse(envelope.PayloadJson, System.Globalization.CultureInfo.InvariantCulture);
            }

            public object CaptureCheckpoint() => State;

            public void ApplyPreparedRestore(object preparedState, SaveRestoreContext context)
            {
                calls?.Add("apply:" + Descriptor.DomainId);
                ApplyCount++;
                State = (int)preparedState;
                if (ThrowDuringApply)
                {
                    throw new InvalidOperationException("apply failed");
                }
            }

            public void Rollback(object checkpoint)
            {
                calls?.Add("rollback:" + Descriptor.DomainId);
                State = (int)checkpoint;
            }
        }
    }
}
