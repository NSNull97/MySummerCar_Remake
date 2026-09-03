using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MSC.Audio;
using MSC.Characters;
using MSC.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.NPC.Tests.PlayMode
{
    public sealed class StoryTrafficVehicleAudioPresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator BoundEmitter_FollowsAuthoritativeRigidbodyPose()
        {
            var wrapperObject = new GameObject("TrafficAudio_StaleWrapper");
            var physicsObject = new GameObject("TrafficAudio_PhysicsBody");
            var backendObject = new GameObject("TrafficAudio_Backend");
            var audioOwner = new GameObject("TrafficAudio_Owner");
            try
            {
                wrapperObject.transform.SetPositionAndRotation(
                    new Vector3(10f, 2f, 20f),
                    Quaternion.Euler(0f, 15f, 0f));
                StoryTrafficVehiclePresentationBinding motion =
                    wrapperObject.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();

                Rigidbody physicalBody = physicsObject.AddComponent<Rigidbody>();
                physicalBody.isKinematic = true;
                physicalBody.useGravity = false;
                Vector3 boundPhysicalPosition = new Vector3(42f, 3f, -17f);
                Quaternion boundPhysicalRotation =
                    Quaternion.Euler(4f, 125f, -2f);
                physicalBody.position = boundPhysicalPosition;
                physicalBody.rotation = boundPhysicalRotation;
                SetRouteBodyForTest(motion, physicalBody);

                var backend = backendObject.AddComponent<ReadyAudioBackend>();
                StoryTrafficVehicleAudioPresenter presenter =
                    audioOwner.AddComponent<StoryTrafficVehicleAudioPresenter>();
                presenter.ConfigurePersistent(
                    backend,
                    "character.jani",
                    configuredListener: null);
                presenter.BindMotion(motion);

                AssertPose(
                    audioOwner.transform,
                    boundPhysicalPosition,
                    boundPhysicalRotation,
                    "BindMotion used the interpolated wrapper Transform instead " +
                    "of the authoritative Rigidbody pose.");

                presenter.SetDrivingActive(true);
                Vector3 updatedPhysicalPosition =
                    new Vector3(67f, 4.5f, -31f);
                Quaternion updatedPhysicalRotation =
                    Quaternion.Euler(-3f, 214f, 1f);
                physicalBody.position = updatedPhysicalPosition;
                physicalBody.rotation = updatedPhysicalRotation;
                wrapperObject.transform.SetPositionAndRotation(
                    new Vector3(-100f, 40f, 70f),
                    Quaternion.Euler(0f, 5f, 0f));

                yield return null;

                AssertPose(
                    audioOwner.transform,
                    updatedPhysicalPosition,
                    updatedPhysicalRotation,
                    "Update allowed the persistent emitter to lag behind its " +
                    "bound Rigidbody pose.");
            }
            finally
            {
                UnityEngine.Object.Destroy(wrapperObject);
                UnityEngine.Object.Destroy(physicsObject);
                UnityEngine.Object.Destroy(backendObject);
                UnityEngine.Object.Destroy(audioOwner);
            }

            yield return null;
        }

        private static void SetRouteBodyForTest(
            StoryTrafficVehiclePresentationBinding motion,
            Rigidbody routeBody)
        {
            FieldInfo field = typeof(StoryTrafficVehiclePresentationBinding)
                .GetField(
                    "routeBody",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(motion, routeBody);
        }

        private static void AssertPose(
            Transform actual,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string message)
        {
            Assert.That(
                Vector3.Distance(actual.position, expectedPosition),
                Is.LessThan(0.001f),
                message);
            Assert.That(
                Quaternion.Angle(actual.rotation, expectedRotation),
                Is.LessThan(0.01f),
                message);
        }

        private sealed class ReadyAudioBackend : MonoBehaviour, IAudioBackend
        {
            private readonly HashSet<IAudioEmitter> emitters =
                new HashSet<IAudioEmitter>();

            public string BackendId => "audio.test.story_traffic.pose";
            public AudioBackendKind Kind => AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;

            public bool RegisterEmitter(
                IAudioEmitter emitter,
                out string failure)
            {
                if (emitter == null)
                {
                    failure = "missing emitter";
                    return false;
                }

                emitters.Add(emitter);
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter) =>
                emitters.Remove(emitter);

            public IAudioEventHandle PostEvent(in AudioEventRequest request) =>
                AudioEventHandles.Invalid;

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

            public AudioRuntimeSnapshot CaptureSnapshot() =>
                new AudioRuntimeSnapshot(
                    BackendId,
                    Kind,
                    true,
                    true,
                    emitters.Count,
                    0,
                    0,
                    Array.Empty<string>(),
                    default,
                    string.Empty);
        }
    }
}
