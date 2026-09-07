using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.InteractionIntegration;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.AudioInteractionIntegration
{
    public sealed class InteractionAudioBridgeTests
    {
        private GameObject owner;
        private GameObject item;
        private PhysicalCarryController carry;
        private TestPickupTarget pickupTarget;
        private InteractionContext context;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("InteractionAudioTestOwner");
            BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
            var anchorObject = new GameObject("CarryAnchor");
            anchorObject.transform.SetParent(owner.transform, false);
            anchorObject.transform.localPosition = Vector3.forward;

            carry = owner.AddComponent<PhysicalCarryController>();
            carry.Configure(anchorObject.transform, ownerCollider);
            context = new InteractionContext(owner, owner.transform.position, Vector3.forward);

            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = 2f;
            pickupTarget = item.AddComponent<TestPickupTarget>();
            pickupTarget.Configure(body);
        }

        [TearDown]
        public void TearDown()
        {
            if (item != null)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }

            if (owner != null)
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CarryPublishesOnlyCompletedPlayerActions()
        {
            var completed = new List<InteractionActionCompleted>();
            carry.ActionCompleted += completed.Add;

            Assert.That(carry.Drop(), Is.False);
            Assert.That(carry.Throw(Vector3.forward), Is.False);
            Assert.That(carry.TryPlace(Vector3.zero, Vector3.up, 0), Is.False);
            Assert.That(completed, Is.Empty);

            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carry.Drop(PickupReleaseReason.TargetLost), Is.True);
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(completed[0].Action, Is.EqualTo(InteractionActionKind.Pickup));

            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carry.Drop(), Is.True);
            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carry.TryPlace(Vector3.up * 2f, Vector3.up, 0), Is.True);
            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carry.Throw(Vector3.forward), Is.True);

            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            GameObject mountObject = new GameObject("TestMount");
            TestMountTarget mount = mountObject.AddComponent<TestMountTarget>();
            try
            {
                Assert.That(carry.TryHandoff(mount, context), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mountObject);
            }

            InteractionActionKind[] actual = new InteractionActionKind[completed.Count];
            for (int index = 0; index < completed.Count; index++)
            {
                actual[index] = completed[index].Action;
                Assert.That(
                    completed[index].TargetStableId,
                    Is.EqualTo(pickupTarget.StableId));
            }

            Assert.That(actual, Is.EqualTo(new[]
            {
                InteractionActionKind.Pickup,
                InteractionActionKind.Pickup,
                InteractionActionKind.Drop,
                InteractionActionKind.Pickup,
                InteractionActionKind.Place,
                InteractionActionKind.Pickup,
                InteractionActionKind.Throw,
                InteractionActionKind.Pickup,
                InteractionActionKind.MountHandoff,
            }));
        }

        [Test]
        public void BridgeMapsCompletedActionsToStableProjectAudioIds()
        {
            RecordingAudioBackend backend = owner.AddComponent<RecordingAudioBackend>();
            TestInteractionEmitter emitter = owner.AddComponent<TestInteractionEmitter>();
            emitter.Configure("audio.emitter.interaction_test");
            InteractionAudioBridge bridge = owner.AddComponent<InteractionAudioBridge>();

            Assert.That(bridge.Configure(carry, backend, emitter), Is.True);
            Assert.That(bridge.IsBound, Is.True);

            Assert.That(carry.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carry.Drop(), Is.True);

            Assert.That(backend.PostedEvents.Count, Is.EqualTo(2));
            Assert.That(
                backend.PostedEvents[0].EventId,
                Is.EqualTo(AudioProjectIds.Events.InteractionPickup));
            Assert.That(
                backend.PostedEvents[1].EventId,
                Is.EqualTo(AudioProjectIds.Events.InteractionDrop));
            Assert.That(backend.PostedEvents[0].Emitter, Is.SameAs(emitter));
            Assert.That(backend.ParameterUpdateCount, Is.EqualTo(2));
            Assert.That(
                backend.LastParameterId,
                Is.EqualTo(AudioProjectIds.Parameters.InteractionImpactIntensity));
            Assert.That(backend.LastParameterEmitter, Is.SameAs(emitter));
        }

        [Test]
        public void MappingIncludesDedicatedMountHandoffEvent()
        {
            Assert.That(
                InteractionAudioBridge.MapEvent(InteractionActionKind.Pickup),
                Is.EqualTo(AudioProjectIds.Events.InteractionPickup));
            Assert.That(
                InteractionAudioBridge.MapEvent(InteractionActionKind.Drop),
                Is.EqualTo(AudioProjectIds.Events.InteractionDrop));
            Assert.That(
                InteractionAudioBridge.MapEvent(InteractionActionKind.Place),
                Is.EqualTo(AudioProjectIds.Events.InteractionPlace));
            Assert.That(
                InteractionAudioBridge.MapEvent(InteractionActionKind.Throw),
                Is.EqualTo(AudioProjectIds.Events.InteractionThrow));
            Assert.That(
                InteractionAudioBridge.MapEvent(InteractionActionKind.MountHandoff),
                Is.EqualTo(AudioProjectIds.Events.InteractionMountHandoff));
        }
    }

    public sealed class TestPickupTarget : MonoBehaviour, IPickupTarget
    {
        private static readonly StableEntityId Identity = CreateIdentity();
        private Rigidbody body;
        private bool isCarried;

        public string PickupPrompt => "Pickup";
        public Rigidbody Body => body;
        public StableEntityId StableId => Identity;

        public void Configure(Rigidbody targetBody)
        {
            body = targetBody;
        }

        public bool CanPickup(in InteractionContext interactionContext) =>
            !isCarried && body != null;

        public void NotifyPickedUp(in InteractionContext interactionContext)
        {
            isCarried = true;
        }

        public void NotifyReleased(PickupReleaseReason reason)
        {
            isCarried = false;
        }

        private static StableEntityId CreateIdentity()
        {
            if (!StableEntityId.TryParse(
                    "4b73150f976941e59e436adfa7dc9853",
                    out StableEntityId identity))
            {
                throw new InvalidOperationException("Test stable ID is invalid.");
            }

            return identity;
        }
    }

    public sealed class TestMountTarget : MonoBehaviour, IMountHandoffTarget
    {
        public string HandoffPrompt => "Install";

        public bool CanAccept(IPickupTarget target, in InteractionContext interactionContext) =>
            target != null && target.Body != null;

        public void Accept(IPickupTarget target, in InteractionContext interactionContext)
        {
            target.Body.isKinematic = true;
            target.Body.position = transform.position;
        }
    }

    public sealed class TestInteractionEmitter : MonoBehaviour, IAudioEmitter
    {
        private string stableId;

        public string StableId => stableId;
        public Transform AudioTransform => transform;
        public SceneHandle OwningSceneHandle => gameObject.scene.handle;
        public bool IsAudioEmitterActive => isActiveAndEnabled;
        public AudioSurfaceContext SurfaceContext => AudioSurfaceContext.Unknown;
        public AudioEnvironmentContext EnvironmentContext => AudioEnvironmentContext.Exterior;

        public void Configure(string authoredStableId)
        {
            stableId = authoredStableId;
        }
    }

    public sealed class RecordingAudioBackend : MonoBehaviour, IAudioBackend
    {
        public readonly List<AudioEventRequest> PostedEvents = new List<AudioEventRequest>();

        public string BackendId => "audio.backend.interaction_test";
        public AudioBackendKind Kind => AudioBackendKind.Unity;
        public bool IsReady => true;
        public string FailureReason => string.Empty;
        public int ParameterUpdateCount { get; private set; }
        public AudioParameterId LastParameterId { get; private set; }
        public IAudioEmitter LastParameterEmitter { get; private set; }

        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            failure = string.Empty;
            return emitter != null;
        }

        public bool UnregisterEmitter(IAudioEmitter emitter) => emitter != null;

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            PostedEvents.Add(request);
            return new TestAudioEventHandle(request.EventId);
        }

        public bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter emitter = null)
        {
            ParameterUpdateCount++;
            LastParameterId = parameterId;
            LastParameterEmitter = emitter;
            return true;
        }

        public bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter emitter = null) => true;

        public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId) => true;

        public void SetListenerContext(in AudioListenerContext listenerContext)
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
            1,
            PostedEvents.Count,
            0,
            Array.Empty<string>(),
            default,
            string.Empty);
    }

    public sealed class TestAudioEventHandle : IAudioEventHandle
    {
        public TestAudioEventHandle(AudioEventId eventId)
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
