using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        private const string SatsumaKeyAccessDomainId =
            "vehicle.satsuma.key-access";

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

        [UnityTest]
        public IEnumerator SatsumaKeyAccess_RoundTripsFalseAndMigratesVersionSixteenToTrue()
        {
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return null;

            ProductionWorldStreamingInstaller installer = RequireInstaller();
            Assert.That(
                installer.TryBeginGameplayPreparation(
                    out string preparationFailure),
                Is.True,
                preparationFailure);
            yield return WaitUntilPrepared(installer);
            Assert.That(
                installer.TryActivateGameplay(out string activationFailure),
                Is.True,
                activationFailure);

            NativeSaveSessionController session = installer.NativeSaveSession;
            Assert.That(session, Is.Not.Null);
            object keyState = RequireSatsumaKeyState(session);
            Assert.That(ReadSatsumaKeyAccess(keyState), Is.True);

            createdSlotId = "test-key-" + Guid.NewGuid().ToString("N");
            string nativeSaveRoot = Path.Combine(
                Application.persistentDataPath,
                "Saves",
                "Native");
            createdSlotDirectory = Path.Combine(
                nativeSaveRoot,
                createdSlotId);

            SetSatsumaKeyAccess(keyState, false);
            SaveWriteResult current = session.SaveService.Save(
                session.CreateSaveRequest(createdSlotId));

            Assert.That(current.Document.Header.DocumentVersion,
                Is.EqualTo(SaveDocument.CurrentDocumentVersion));
            Assert.That(current.Document.Domains, Has.Length.EqualTo(15));
            SaveDomainEnvelope currentKey = current.Document.Domains.Single(
                domain => domain.DomainId == SatsumaKeyAccessDomainId);
            Assert.That(currentKey.Required, Is.True);
            Assert.That(currentKey.PayloadJson,
                Does.Contain("\"hasAccess\":false"));

            SetSatsumaKeyAccess(keyState, true);
            GameCompositionRoot initialRoot = GameCompositionRoot.ActiveRoot;
            Assert.That(initialRoot, Is.Not.Null);
            Assert.That(
                session.RequestLoad(createdSlotId, out string currentFailure),
                Is.True,
                currentFailure);
            ProductionWorldStreamingInstaller currentInstaller = null;
            yield return WaitForRestoredInstaller(
                initialRoot,
                createdSlotId,
                value => currentInstaller = value);
            NativeSaveSessionController currentSession =
                currentInstaller.NativeSaveSession;
            object currentKeyState = RequireSatsumaKeyState(currentSession);

            Assert.That(
                currentSession.LastLoadResult.Document.Header.DocumentVersion,
                Is.EqualTo(SaveDocument.CurrentDocumentVersion));
            Assert.That(ReadSatsumaKeyAccess(currentKeyState), Is.False);

            SaveDocument legacy = current.Document.DeepClone();
            legacy.Header.DocumentVersion = 16;
            legacy.Domains = legacy.Domains
                .Where(domain => domain.DomainId != SatsumaKeyAccessDomainId)
                .ToArray();
            Assert.That(legacy.Domains, Has.Length.EqualTo(14));
            new FileSystemSaveStorage(nativeSaveRoot).Write(
                createdSlotId,
                legacy);

            SetSatsumaKeyAccess(currentKeyState, false);
            GameCompositionRoot currentRoot = GameCompositionRoot.ActiveRoot;
            Assert.That(currentRoot, Is.Not.Null);
            Assert.That(
                currentSession.RequestLoad(
                    createdSlotId,
                    out string migrationFailure),
                Is.True,
                migrationFailure);
            ProductionWorldStreamingInstaller migratedInstaller = null;
            yield return WaitForRestoredInstaller(
                currentRoot,
                createdSlotId,
                value => migratedInstaller = value);
            NativeSaveSessionController migratedSession =
                migratedInstaller.NativeSaveSession;
            object migratedKeyState = RequireSatsumaKeyState(migratedSession);
            SaveLoadResult migrated = migratedSession.LastLoadResult;

            Assert.That(migrated.Document.Header.DocumentVersion,
                Is.EqualTo(SaveDocument.CurrentDocumentVersion));
            Assert.That(migrated.Document.Domains, Has.Length.EqualTo(15));
            SaveDomainEnvelope migratedKey = migrated.Document.Domains.Single(
                domain => domain.DomainId == SatsumaKeyAccessDomainId);
            Assert.That(migratedKey.Required, Is.True);
            Assert.That(migratedKey.PayloadJson,
                Does.Contain("\"hasAccess\":true"));
            Assert.That(ReadSatsumaKeyAccess(migratedKeyState), Is.True);
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

        private static IEnumerator WaitForRestoredInstaller(
            GameCompositionRoot previousRoot,
            string expectedSlotId,
            Action<ProductionWorldStreamingInstaller> receive)
        {
            ProductionWorldStreamingInstaller restoredInstaller = null;
            float deadline =
                Time.realtimeSinceStartup + BootstrapTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameCompositionRoot activeRoot =
                    GameCompositionRoot.ActiveRoot;
                if (activeRoot != null && activeRoot != previousRoot)
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
                restoredInstaller.Environment
                    .WasRestoreAppliedBeforeReveal,
                Is.True);
            NativeSaveSessionController session =
                restoredInstaller.NativeSaveSession;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.HasPendingRestore, Is.False);
            Assert.That(session.HasRestoredSave, Is.True,
                session.LastLoadFailure);
            Assert.That(session.ActiveSlotId, Is.EqualTo(expectedSlotId));
            Assert.That(session.LastLoadResult, Is.Not.Null);
            receive(restoredInstaller);
        }

        private static object RequireSatsumaKeyState(
            NativeSaveSessionController session)
        {
            object state = session.GetType()
                .GetProperty("SatsumaKeyAccess")
                ?.GetValue(session);
            Assert.That(state, Is.Not.Null,
                "Native save session must own one logical Satsuma key state.");
            Assert.That(state.GetType().GetMethod("SetAccess"), Is.Not.Null);
            Assert.That(state.GetType().GetProperty("HasAccess"), Is.Not.Null);
            return state;
        }

        private static bool ReadSatsumaKeyAccess(object state) =>
            (bool)state.GetType().GetProperty("HasAccess").GetValue(state);

        private static void SetSatsumaKeyAccess(object state, bool value)
        {
            state.GetType().GetMethod("SetAccess").Invoke(
                state,
                new object[] { value });
        }
    }
}
