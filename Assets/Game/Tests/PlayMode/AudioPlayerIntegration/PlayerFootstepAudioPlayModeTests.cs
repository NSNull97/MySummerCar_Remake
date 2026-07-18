using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.PlayerIntegration;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioPlayerIntegration
{
    public sealed class PlayerFootstepAudioPlayModeTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    UnityEngine.Object.Destroy(cleanup[index]);
                }
            }

            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualGroundedMovementPostsTypedFootstepOnSharedEmitter()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleanup.Add(ground);
            ground.name = "WoodGround";
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(20f, 0.2f, 20f);
            AudioSurfaceAuthoring surface = ground.AddComponent<AudioSurfaceAuthoring>();
            surface.Configure(AudioSurfaceKind.Wood);

            GameObject player = new GameObject("FootstepPlayer");
            cleanup.Add(player);
            player.SetActive(false);
            player.transform.position = Vector3.up * 0.05f;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.skinWidth = 0.02f;

            FirstPersonMotor motor = player.AddComponent<FirstPersonMotor>();
            motor.Configure(controller, null);
            motor.enabled = false;

            RecordingAudioBackend backend = player.AddComponent<RecordingAudioBackend>();
            AudioEmitterAuthoring sharedEmitter = player.AddComponent<AudioEmitterAuthoring>();
            sharedEmitter.Configure("audio.emitter.player.shared_test", backend, player.transform);

            PlayerFootstepAudioPresenter presenter =
                player.AddComponent<PlayerFootstepAudioPresenter>();
            Assert.That(
                presenter.Configure(player, backend, sharedEmitter),
                Is.True,
                presenter.LastFailure);

            player.SetActive(true);
            Physics.SyncTransforms();
            controller.Move(Vector3.down * 0.2f);
            yield return null;
            Assert.That(controller.isGrounded, Is.True);

            for (int index = 0; index < 4; index++)
            {
                controller.Move(Vector3.right * 0.5f + Vector3.down * 0.2f);
                yield return null;
            }

            Assert.That(backend.RegisterCount, Is.EqualTo(1),
                "Footsteps must reuse the existing player emitter registration.");
            Assert.That(backend.PostedEvents.Count, Is.EqualTo(1));
            Assert.That(
                backend.PostedEvents[0].EventId,
                Is.EqualTo(AudioProjectIds.Events.PlayerFootstep));
            Assert.That(backend.PostedEvents[0].Emitter, Is.SameAs(sharedEmitter));
            Assert.That(
                backend.LastSwitchGroup,
                Is.EqualTo(AudioProjectIds.Switches.FootstepSurfaceGroup));
            Assert.That(
                backend.LastSwitchValue,
                Is.EqualTo(AudioProjectIds.Switches.FootstepSurfaceWood));
            Assert.That(presenter.LastSurfaceKind, Is.EqualTo(AudioSurfaceKind.Wood));
            Assert.That(presenter.PostedStepCount, Is.EqualTo(1));
        }

        private sealed class RecordingAudioBackend : MonoBehaviour, IAudioBackend
        {
            private readonly HashSet<IAudioEmitter> emitters = new HashSet<IAudioEmitter>();

            public readonly List<AudioEventRequest> PostedEvents = new List<AudioEventRequest>();

            public string BackendId => "audio.backend.footstep_test";
            public AudioBackendKind Kind => AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public int RegisterCount { get; private set; }
            public AudioSwitchId LastSwitchGroup { get; private set; }
            public AudioSwitchId LastSwitchValue { get; private set; }

            public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
            {
                if (emitter == null || !emitters.Add(emitter))
                {
                    failure = "duplicate";
                    return false;
                }

                RegisterCount++;
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter) => emitters.Remove(emitter);

            public IAudioEventHandle PostEvent(in AudioEventRequest request)
            {
                PostedEvents.Add(request);
                return new ValidAudioEventHandle(request.EventId);
            }

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null) => true;

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null)
            {
                LastSwitchGroup = switchGroupId;
                LastSwitchValue = switchValueId;
                return emitter != null;
            }

            public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId) => true;

            public void SetListenerContext(in AudioListenerContext context)
            {
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                true,
                false,
                emitters.Count,
                PostedEvents.Count,
                0,
                Array.Empty<string>(),
                default,
                string.Empty);
        }

        private sealed class ValidAudioEventHandle : IAudioEventHandle
        {
            public ValidAudioEventHandle(AudioEventId eventId)
            {
                EventId = eventId;
            }

            public ulong HandleId => 1UL;
            public AudioEventId EventId { get; }
            public bool IsValid => true;
            public bool IsPlaying { get; private set; } = true;

            public void Stop(float fadeSeconds = 0f)
            {
                IsPlaying = false;
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
