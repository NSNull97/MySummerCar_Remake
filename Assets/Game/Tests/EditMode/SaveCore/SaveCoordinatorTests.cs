using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveCoordinatorTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MSC_Save_Coordinator_Tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveThenLoad_RoundTripsParticipantAndRaisesLifecycleEvents()
        {
            StatefulParticipant participant = new StatefulParticipant { State = 7 };
            SaveCoordinator coordinator = CreateCoordinator(participant);
            List<string> events = new List<string>();
            coordinator.OperationStarted += (_, args) => events.Add("start:" + args.Operation);
            coordinator.OperationCompleted += (_, args) => events.Add("complete:" + args.Operation);

            SaveWriteResult write = coordinator.Save(new SaveRequest
            {
                SlotId = "slot-a",
                BuildId = "test",
                Metadata = new SaveMetadata { DisplayName = "A" },
            });
            participant.State = 99;
            SaveLoadResult load = coordinator.Load("slot-a");

            Assert.That(write.Document.Header.SlotId, Is.EqualTo("slot-a"));
            Assert.That(load.ReadStatus, Is.EqualTo(SaveReadStatus.Loaded));
            Assert.That(participant.State, Is.EqualTo(7));
            Assert.That(coordinator.IsOperationInProgress, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "start:Save", "complete:Save", "start:Load", "complete:Load" }));
        }

        [Test]
        public void LoadApplyFailure_RaisesFailureAndLeavesServiceIdle()
        {
            StatefulParticipant participant = new StatefulParticipant { State = 7 };
            SaveCoordinator coordinator = CreateCoordinator(participant);
            coordinator.Save(new SaveRequest { SlotId = "slot-a", Metadata = new SaveMetadata() });
            participant.State = 99;
            participant.ThrowDuringApply = true;
            int failures = 0;
            coordinator.OperationFailed += (_, _) => failures++;

            Assert.That(() => coordinator.Load("slot-a"), Throws.TypeOf<InvalidOperationException>());
            Assert.That(participant.State, Is.EqualTo(99));
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(coordinator.IsOperationInProgress, Is.False);
        }

        private SaveCoordinator CreateCoordinator(StatefulParticipant participant)
        {
            return new SaveCoordinator(
                new FileSystemSaveStorage(directory),
                new SaveParticipantRegistry(new[] { participant }),
                new CurrentVersionSaveMigrationPipeline(),
                new DeferredStableEntityStore(),
                () => new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero));
        }

        private sealed class StatefulParticipant : ISaveParticipant
        {
            public SaveParticipantDescriptor Descriptor { get; } =
                new SaveParticipantDescriptor("test.state", 1, true, SaveRestorePhase.GlobalState);

            public int State { get; set; }
            public bool ThrowDuringApply { get; set; }

            public string CapturePayload() => State.ToString(System.Globalization.CultureInfo.InvariantCulture);

            public object PrepareRestore(SaveDomainEnvelope envelope, SaveRestorePreparationContext context)
            {
                return int.Parse(envelope.PayloadJson, System.Globalization.CultureInfo.InvariantCulture);
            }

            public object CaptureCheckpoint() => State;

            public void ApplyPreparedRestore(object preparedState, SaveRestoreContext context)
            {
                State = (int)preparedState;
                if (ThrowDuringApply)
                {
                    throw new InvalidOperationException("failure");
                }
            }

            public void Rollback(object checkpoint) => State = (int)checkpoint;
        }
    }
}
