using System;
using System.Collections;
using MSC.Audio;
using MSC.Audio.PlayerIntegration;
using MSC.Bootstrap;
using MSC.Economy;
using MSC.Needs;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.AudioPlayerIntegration
{
    public sealed class PlayerVoiceReactionControllerTests
    {
        [Test]
        public void ManualSwear_PostsStableEventAndAppliesOnlyStressRelief()
        {
            GameObject owner = CreateInactiveOwner(
                out PlayerVoiceReactionController controller,
                out FakeNeeds needs,
                out _,
                out RecordingAudioBackend audio,
                out SubtitleRecorder subtitles);
            try
            {
                Assert.That(
                    controller.RequestReaction(
                        PlayerVoiceReactionKind.ManualSwear),
                    Is.True);
                Assert.That(audio.PostCount, Is.EqualTo(1));
                Assert.That(
                    audio.LastEventId.Value,
                    Does.StartWith("audio.event.player.swear."));
                Assert.That(needs.ImmediateEffectCount, Is.EqualTo(1));
                Assert.That(needs.LastImmediateDelta.Stress, Is.EqualTo(-0.5f));
                Assert.That(subtitles.Value, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void InsufficientFundsEvent_PostsVoiceWithoutMutatingNeeds()
        {
            GameObject owner = CreateInactiveOwner(
                out PlayerVoiceReactionController controller,
                out FakeNeeds needs,
                out FakeEconomyFeedback economy,
                out RecordingAudioBackend audio,
                out _);
            try
            {
                economy.Raise(EconomyTransactionFailureReason.InsufficientFunds);

                Assert.That(controller.HasTriggeredReaction, Is.True);
                Assert.That(
                    controller.LastDecision.Kind,
                    Is.EqualTo(PlayerVoiceReactionKind.InsufficientFunds));
                Assert.That(audio.PostCount, Is.EqualTo(1));
                Assert.That(needs.ImmediateEffectCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MiddleFinger_PostsDedicatedFingerEventWithoutMutatingNeeds()
        {
            GameObject owner = CreateInactiveOwner(
                out PlayerVoiceReactionController controller,
                out FakeNeeds needs,
                out _,
                out RecordingAudioBackend audio,
                out SubtitleRecorder subtitles);
            try
            {
                Assert.That(
                    controller.RequestReaction(
                        PlayerVoiceReactionKind.MiddleFinger),
                    Is.True);
                Assert.That(audio.PostCount, Is.EqualTo(1));
                Assert.That(
                    audio.LastEventId.Value,
                    Does.StartWith("audio.event.player.finger."));
                Assert.That(needs.ImmediateEffectCount, Is.Zero);
                Assert.That(subtitles.Value, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ConfiguredGameplayLocale_ControlsVoiceSubtitleLanguage()
        {
            GameObject owner = CreateInactiveOwner(
                out PlayerVoiceReactionController controller,
                out _,
                out _,
                out _,
                out SubtitleRecorder subtitles);
            try
            {
                controller.ApplyGameplayLocale("en-US");
                Assert.That(
                    controller.RequestReaction(
                        PlayerVoiceReactionKind.ManualSwear),
                    Is.True);
                Assert.That(
                    subtitles.Value,
                    Is.EqualTo(PlayerVoiceReactionCatalog.GetSubtitle(
                        controller.LastDecision.Kind,
                        controller.LastDecision.VariantIndex,
                        SystemLanguage.English)));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }

            owner = CreateInactiveOwner(
                out controller,
                out _,
                out _,
                out _,
                out subtitles);
            try
            {
                controller.ApplyGameplayLocale("ru-RU");
                Assert.That(
                    controller.RequestReaction(
                        PlayerVoiceReactionKind.ManualSwear),
                    Is.True);
                Assert.That(
                    subtitles.Value,
                    Is.EqualTo(PlayerVoiceReactionCatalog.GetSubtitle(
                        controller.LastDecision.Kind,
                        controller.LastDecision.VariantIndex,
                        SystemLanguage.Russian)));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [UnityTest]
        public IEnumerator ProductionBootstrap_InstallsControllerAndPrivateFallbackLibrary()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Game/Bootstrap/Bootstrap.unity",
                LoadSceneMode.Single);
            yield return load;
            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);
            yield return null;
            Assert.That(
                installer.TryBeginGameplayPreparation(out string failure),
                Is.True,
                failure);
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!installer.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(installer.IsReady, Is.True);
            PlayerVoiceReactionController controller =
                installer.SpawnedPlayer.GetComponent<
                    PlayerVoiceReactionController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsInitialized, Is.True);

            IAudioBackend fallback = installer.AudioComposition.FallbackBackend;
            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback.IsReady, Is.True, fallback.FailureReason);
            var request = new AudioEventRequest(
                AudioProjectIds.Events.GetPlayerSwearVariant(0),
                allowMultiple: false);
            IAudioEventHandle handle = fallback.PostEvent(in request);
            Assert.That(handle.IsValid, Is.True, fallback.FailureReason);
            handle.Stop();

            request = new AudioEventRequest(
                AudioProjectIds.Events.GetPlayerMiddleFingerVariant(0),
                allowMultiple: false);
            handle = fallback.PostEvent(in request);
            Assert.That(handle.IsValid, Is.True, fallback.FailureReason);
            handle.Stop();
        }

        private static GameObject CreateInactiveOwner(
            out PlayerVoiceReactionController controller,
            out FakeNeeds needs,
            out FakeEconomyFeedback economy,
            out RecordingAudioBackend audio,
            out SubtitleRecorder subtitles)
        {
            var owner = new GameObject("PlayerVoiceReactionControllerTests");
            owner.SetActive(false);
            PlayerInputRouter input = owner.AddComponent<PlayerInputRouter>();
            controller = owner.AddComponent<PlayerVoiceReactionController>();
            needs = new FakeNeeds(75f);
            economy = new FakeEconomyFeedback();
            audio = new RecordingAudioBackend();
            subtitles = new SubtitleRecorder();
            SubtitleRecorder recorder = subtitles;
            controller.Initialize(
                input,
                needs,
                needs,
                economy,
                audio,
                configuredFallbackAudio: null,
                value => recorder.Value = value,
                presentationRandomSeed: 123u);
            return owner;
        }

        private sealed class SubtitleRecorder
        {
            public string Value { get; set; } = string.Empty;
        }

        private sealed class FakeNeeds :
            IPlayerNeedsService,
            IPlayerNeedsEffectSink
        {
            private float stress;

            public FakeNeeds(float initialStress)
            {
                stress = initialStress;
            }

            public event Action<PlayerNeedsSnapshot> StateChanged;

            public PlayerNeedsSnapshot Snapshot => new PlayerNeedsSnapshot(
                0,
                0f,
                0f,
                stress,
                0f,
                0f,
                0f,
                83f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f);

            public int ImmediateEffectCount { get; private set; }
            public PlayerNeedsEffectDelta LastImmediateDelta { get; private set; }

            public bool TryApplyEffects(
                in PlayerNeedsEffectDelta immediateDelta,
                in PlayerNeedsEffectDelta delayedDelta,
                out string failure)
            {
                if (immediateDelta.HasAnyEffect)
                {
                    ImmediateEffectCount++;
                    LastImmediateDelta = immediateDelta;
                    stress = Mathf.Clamp(
                        stress + immediateDelta.Stress,
                        0f,
                        100f);
                    StateChanged?.Invoke(Snapshot);
                }

                failure = string.Empty;
                return true;
            }

            public bool TryApplyEffectImmediately(
                in PlayerNeedsEffectDelta delta,
                out string failure)
            {
                PlayerNeedsEffectDelta delayed = default;
                return TryApplyEffects(in delta, in delayed, out failure);
            }

            public bool TryQueueDelayedEffect(
                in PlayerNeedsEffectDelta delta,
                out string failure)
            {
                PlayerNeedsEffectDelta immediate = default;
                return TryApplyEffects(in immediate, in delta, out failure);
            }
        }

        private sealed class FakeEconomyFeedback :
            IEconomyTransactionFeedbackSource
        {
            public event Action<EconomyTransactionRejected> TransactionRejected;

            public void Raise(EconomyTransactionFailureReason reason)
            {
                var request = new EconomyTransactionRequest(
                    "transaction.tests.voice",
                    EconomyTransactionKind.Purchase,
                    EconomyTransactionDirection.Debit,
                    1,
                    "store.teimo.checkout");
                var receipt = new EconomyTransactionReceipt(
                    false,
                    false,
                    reason,
                    request.TransactionId,
                    0,
                    0,
                    0);
                TransactionRejected?.Invoke(
                    new EconomyTransactionRejected(in request, in receipt));
            }
        }

        private sealed class RecordingAudioBackend : IAudioBackend
        {
            public string BackendId => "tests.player-voice";
            public AudioBackendKind Kind => AudioBackendKind.Silent;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public int PostCount { get; private set; }
            public AudioEventId LastEventId { get; private set; }

            public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
            {
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter) => true;

            public IAudioEventHandle PostEvent(in AudioEventRequest request)
            {
                PostCount++;
                LastEventId = request.EventId;
                return new ValidHandle(request.EventId);
            }

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null) => true;

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null) => true;

            public bool SetState(
                AudioStateId stateGroupId,
                AudioStateId stateValueId) => true;

            public void SetListenerContext(in AudioListenerContext context)
            {
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => default;
        }

        private sealed class ValidHandle : IAudioEventHandle
        {
            public ValidHandle(AudioEventId eventId)
            {
                EventId = eventId;
            }

            public ulong HandleId => 1;
            public AudioEventId EventId { get; }
            public bool IsValid => true;
            public bool IsPlaying => true;

            public void Stop(float fadeSeconds = 0f)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
