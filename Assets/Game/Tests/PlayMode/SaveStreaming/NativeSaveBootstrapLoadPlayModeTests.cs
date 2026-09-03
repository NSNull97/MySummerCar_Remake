using System;
using System.Collections;
using System.IO;
using MSC.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.PlayMode
{
    public sealed class NativeSaveBootstrapLoadPlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const float BootstrapTimeoutSeconds = 120f;

        private string createdSlotId = string.Empty;
        private string createdSlotDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            NativeSaveLoadHandoff.Clear(string.Empty);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NativeSaveLoadHandoff.Clear(string.Empty);
            if (!string.IsNullOrEmpty(createdSlotDirectory) &&
                Directory.Exists(createdSlotDirectory))
            {
                Directory.Delete(createdSlotDirectory, recursive: true);
            }

            GameCompositionRoot[] roots = Object.FindObjectsByType<
                GameCompositionRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < roots.Length; index++)
            {
                Object.Destroy(roots[index].gameObject);
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingNativeSave_ReloadsBootstrapAndRestoresWithoutNewGameClick()
        {
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return null;

            ProductionWorldStreamingInstaller initialInstaller =
                RequireInstaller();
            Assert.That(
                initialInstaller.TryBeginGameplayPreparation(
                    out string preparationFailure),
                Is.True,
                preparationFailure);
            yield return WaitUntilPrepared(initialInstaller);
            Assert.That(
                initialInstaller.TryActivateGameplay(out string activationFailure),
                Is.True,
                activationFailure);
            int initialShelterVolumeCount =
                initialInstaller.Environment.ActiveShelterVolumeCount;

            NativeSaveSessionController initialSession =
                initialInstaller.NativeSaveSession;
            Assert.That(initialSession, Is.Not.Null);
            createdSlotId = "test-load-" + Guid.NewGuid().ToString("N");
            createdSlotDirectory = Path.Combine(
                Application.persistentDataPath,
                "Saves",
                "Native",
                createdSlotId);
            initialSession.SaveService.Save(
                initialSession.CreateSaveRequest(createdSlotId));

            GameCompositionRoot initialRoot = GameCompositionRoot.ActiveRoot;
            Assert.That(initialRoot, Is.Not.Null);
            Assert.That(
                initialSession.RequestLoad(createdSlotId, out string loadFailure),
                Is.True,
                loadFailure);

            ProductionWorldStreamingInstaller restoredInstaller = null;
            float deadline = Time.realtimeSinceStartup + BootstrapTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameCompositionRoot activeRoot = GameCompositionRoot.ActiveRoot;
                if (activeRoot != null && activeRoot != initialRoot)
                {
                    restoredInstaller = activeRoot.GetComponent<
                        ProductionWorldStreamingInstaller>();
                    NativeSaveSessionController restoredSession =
                        restoredInstaller?.NativeSaveSession;
                    if (restoredInstaller != null &&
                        restoredInstaller.IsReady &&
                        restoredInstaller.IsGameplayActive &&
                        restoredSession != null &&
                        restoredSession.HasRestoredSave)
                    {
                        break;
                    }
                }

                yield return null;
            }

            Assert.That(restoredInstaller, Is.Not.Null);
            Assert.That(restoredInstaller.IsReady, Is.True,
                restoredInstaller.LastGameplayPreparationFailure);
            Assert.That(restoredInstaller.IsGameplayActive, Is.True);
            Assert.That(
                restoredInstaller.Environment.WasRestoreAppliedBeforeReveal,
                Is.True);
            Assert.That(
                restoredInstaller.Environment.ActiveShelterVolumeCount,
                Is.EqualTo(initialShelterVolumeCount),
                "Native reload must not duplicate registered shelter volumes.");
            Assert.That(
                restoredInstaller.NativeSaveSession.HasPendingRestore,
                Is.False);
            Assert.That(
                restoredInstaller.NativeSaveSession.HasRestoredSave,
                Is.True,
                restoredInstaller.NativeSaveSession.LastLoadFailure);
            Assert.That(
                restoredInstaller.NativeSaveSession.ActiveSlotId,
                Is.EqualTo(createdSlotId));
            Assert.That(
                restoredInstaller.NativeSaveSession.LastLoadResult,
                Is.Not.Null);
        }

        private static ProductionWorldStreamingInstaller RequireInstaller()
        {
            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);
            return installer;
        }

        private static IEnumerator WaitUntilPrepared(
            ProductionWorldStreamingInstaller installer)
        {
            float deadline = Time.realtimeSinceStartup + BootstrapTimeoutSeconds;
            while (!installer.IsGameplayPrepared &&
                   installer.IsGameplayPreparationRunning &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                installer.IsGameplayPrepared,
                Is.True,
                installer.LastGameplayPreparationFailure);
        }
    }
}
