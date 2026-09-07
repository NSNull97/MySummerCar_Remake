using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveRestoreTransactionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OwnershipObjectsAreRestoredBeforeReverseParticipantRollback(bool failDuringBegin)
        {
            var calls = new List<string>();
            var plan = new Plan(calls) { FailDuringBegin = failDuringBegin };
            var items = new Participant("items.instances", SaveRestorePhase.ItemInstances, calls);
            var vehicle = new Participant("vehicle.test", SaveRestorePhase.VehicleAssembly, calls) { FailApply = true };
            var registry = new SaveParticipantRegistry(new[] { items, vehicle }, plan);
            var unresolved = new UnresolvedContentReport();
            var deferred = new DeferredStableEntityStore();
            PreparedSaveRestore prepared = registry.PrepareRestore(Document(items, vehicle), unresolved, deferred);
            Assert.That(calls, Is.EqualTo(new[] { "prepare-plan", "prepare:items.instances", "prepare:vehicle.test",
                "quiesce", "checkpoint:items.instances", "checkpoint:vehicle.test" }));
            calls.Clear();
            Assert.Throws<InvalidOperationException>(() => registry.ApplyRestore(prepared, unresolved, deferred));
            Assert.That(calls, Is.EqualTo(failDuringBegin
                ? new[] { "begin", "restore-objects", "rollback-plan" }
                : new[] { "begin", "apply:items.instances", "apply:vehicle.test", "restore-objects",
                    "rollback:vehicle.test", "rollback:items.instances", "rollback-plan" }));
            Assert.That(items.State, Is.EqualTo(7));
            Assert.That(vehicle.State, Is.EqualTo(7));
        }

        [Test]
        public void CleanupRunsOnlyAfterTheLastApplyAndPreflightCannotMutateSource()
        {
            var calls = new List<string>();
            var plan = new Plan(calls);
            var first = new Participant("items.instances", SaveRestorePhase.ItemInstances, calls);
            var last = new Participant("presentation.test", SaveRestorePhase.PresentationSync, calls);
            SaveDocument source = Document(first, last);
            var registry = new SaveParticipantRegistry(new[] { first, last }, plan);
            var unresolved = new UnresolvedContentReport();
            var deferred = new DeferredStableEntityStore();
            PreparedSaveRestore prepared = registry.PrepareRestore(source, unresolved, deferred);
            Assert.That(source.Domains[0].PayloadJson, Is.EqualTo("42"));
            calls.Clear();
            registry.ApplyRestore(prepared, unresolved, deferred);
            Assert.That(calls, Is.EqualTo(new[] { "begin", "apply:items.instances", "apply:presentation.test", "commit" }));
            Assert.That(first.State, Is.EqualTo(99));
        }

        [Test]
        public void RejectedPreflightNeverQuiescesOrBeginsOwnershipTransaction()
        {
            var calls = new List<string>();
            var first = new Participant("items.instances", SaveRestorePhase.ItemInstances, calls);
            var last = new Participant("vehicle.test", SaveRestorePhase.VehicleAssembly, calls) { FailPrepare = true };
            var registry = new SaveParticipantRegistry(new[] { first, last }, new Plan(calls));
            Assert.Throws<InvalidDataException>(() => registry.PrepareRestore(Document(first, last),
                new UnresolvedContentReport(), new DeferredStableEntityStore()));
            Assert.That(calls, Is.EqualTo(new[] { "prepare-plan", "prepare:items.instances", "prepare:vehicle.test" }));
            Assert.That(first.State, Is.EqualTo(7));
        }

        [Test]
        public void CheckpointsCaptureSettledStateOnlyAfterEveryDomainPassesPreparation()
        {
            var calls = new List<string>();
            var first = new Participant("items.instances", SaveRestorePhase.ItemInstances, calls);
            var last = new Participant("vehicle.test", SaveRestorePhase.VehicleAssembly, calls) { FailApply = true };
            var plan = new Plan(calls) { Quiesce = () => { first.State = 8; last.State = 9; } };
            var registry = new SaveParticipantRegistry(new[] { first, last }, plan);
            var unresolved = new UnresolvedContentReport();
            var deferred = new DeferredStableEntityStore();
            PreparedSaveRestore prepared = registry.PrepareRestore(Document(first, last), unresolved, deferred);
            Assert.That(calls, Is.EqualTo(new[] { "prepare-plan", "prepare:items.instances", "prepare:vehicle.test",
                "quiesce", "checkpoint:items.instances", "checkpoint:vehicle.test" }));
            Assert.Throws<InvalidOperationException>(() => registry.ApplyRestore(prepared, unresolved, deferred));
            Assert.That(first.State, Is.EqualTo(8));
            Assert.That(last.State, Is.EqualTo(9));
        }

        private static SaveDocument Document(params Participant[] participants)
        {
            SaveDocument document = SaveTestData.CreateDocument("slot-transaction");
            document.Domains = participants.Select(value => new SaveDomainEnvelope
            { DomainId = value.Descriptor.DomainId, SchemaVersion = 1, Required = true, PayloadJson = "42" }).ToArray();
            return document;
        }

        private sealed class Plan : ISaveRestorePlanFactory, ISaveRestoreTransaction, ISaveRestoreCheckpointBoundary
        {
            private readonly List<string> calls;
            public bool FailDuringBegin;
            public Action Quiesce;
            public Plan(List<string> calls) { this.calls = calls; }
            public SaveRestorePlan Prepare(SaveDocument document, SaveRestorePreparationContext context)
            {
                calls.Add("prepare-plan");
                Assert.That(context.TryGetEnvelope("items.instances", out SaveDomainEnvelope sibling), Is.True);
                sibling.PayloadJson = "bad";
                Assert.That(context.TryGetEnvelope("items.instances", out SaveDomainEnvelope again), Is.True);
                Assert.That(again.PayloadJson, Is.EqualTo("42"), "Sibling reads must be detached copies.");
                document.Domains[0].PayloadJson = "99";
                return new SaveRestorePlan(document, this);
            }
            public void BeforeCaptureCheckpoints() { calls.Add("quiesce"); Quiesce?.Invoke(); }
            public void Begin() { calls.Add("begin"); if (FailDuringBegin) throw new InvalidOperationException("begin failed"); }
            public void BeforeRollback() => calls.Add("restore-objects");
            public void Rollback() => calls.Add("rollback-plan");
            public void Commit() => calls.Add("commit");
        }

        private sealed class Participant : ISaveParticipant
        {
            private readonly List<string> calls;
            public Participant(string id, SaveRestorePhase phase, List<string> calls)
            { Descriptor = new SaveParticipantDescriptor(id, 1, true, phase); this.calls = calls; }
            public SaveParticipantDescriptor Descriptor { get; }
            public int State = 7;
            public bool FailApply, FailPrepare;
            public string CapturePayload() => State.ToString();
            public object PrepareRestore(SaveDomainEnvelope envelope, SaveRestorePreparationContext context)
            {
                calls.Add("prepare:" + Descriptor.DomainId);
                if (FailPrepare) throw new InvalidDataException("prepare failed");
                return int.Parse(envelope.PayloadJson);
            }
            public object CaptureCheckpoint() { calls.Add("checkpoint:" + Descriptor.DomainId); return State; }
            public void ApplyPreparedRestore(object state, SaveRestoreContext context)
            { calls.Add("apply:" + Descriptor.DomainId); State = (int)state; if (FailApply) throw new InvalidOperationException("apply failed"); }
            public void Rollback(object checkpoint) { calls.Add("rollback:" + Descriptor.DomainId); State = (int)checkpoint; }
        }
    }
}
