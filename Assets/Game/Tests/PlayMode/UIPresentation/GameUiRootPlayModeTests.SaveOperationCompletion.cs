using System;
using System.Collections;
using System.IO;
using MSC.Save;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainMenu_RealSaveCoordinator_ReleasesButtonsAfterCompletionAndFailure()
        {
            var saves = new SaveCoordinator(
                new FileSystemSaveStorage(Path.Combine(temporaryDirectory, "empty-save-operation-store")),
                new SaveParticipantRegistry(Array.Empty<ISaveParticipant>()),
                new CurrentVersionSaveMigrationPipeline(),
                new DeferredStableEntityStore());
            UiFixture fixture = CreateFixture(startInMainMenu: true, saveService: saves);
            yield return null;

            var main = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Button load = FindRequired(main, "LoadGame").GetComponent<Button>();
            Button resume = FindRequired(main, "Continue").GetComponent<Button>();
            Assert.That(load.interactable, Is.True);
            Assert.That(resume.interactable, Is.False);
            int startedOperations = 0;
            EventHandler<SaveOperationEventArgs> countOperation = (_, __) => startedOperations++;
            saves.OperationStarted += countOperation;
            try
            {
                // The same external post-bind enumeration exposed this defect
                // in the actual standalone UI fixture. The coordinator emits
                // completion while busy and releases its guard in finally.
                Assert.That(saves.EnumerateSlots(), Is.Empty);
                Assert.That(saves.IsOperationInProgress, Is.False);
                yield return null;
                Assert.That(load.interactable, Is.True,
                    "Load must recover after the real completion event releases the busy guard.");
                Assert.That(resume.interactable, Is.False);
                Assert.That(startedOperations, Is.EqualTo(1),
                    "A presentation refresh must not start another storage enumeration.");

                Assert.Throws<FileNotFoundException>(() => saves.Load("slot-01"));
                Assert.That(saves.IsOperationInProgress, Is.False);
                yield return null;
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                Assert.That(load.interactable, Is.True,
                    "The failed-load event also precedes release of the busy guard.");
                Assert.That(resume.interactable, Is.False);
                yield return null;
                Assert.That(startedOperations, Is.EqualTo(2),
                    "Settled idle presentation must not repeat save-service operations.");
                yield return DestroyFixture(fixture);
            }
            finally
            {
                saves.OperationStarted -= countOperation;
                // The shared fixture TearDown destroys owned objects and the
                // isolated temporary directory even if an assertion fails.
            }
        }
    }
}
