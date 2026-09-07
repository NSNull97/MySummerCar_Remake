using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Audio;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaHandbrakeAudioTests
    {
        [Test]
        public void HandbrakeAudioManifestRetainsHashLockedDonorClipsAndAttenuation()
        {
            var manifest = JsonUtility.FromJson<AudioManifest>(File.ReadAllText(
                "Assets/Game/LegacyImport/Manifests/Phase1SatsumaAssemblyAudioManifest.json"));
            Assert.That(manifest.classification, Is.EqualTo("TemporaryDirectImport"));
            AssertClip(manifest, "handbrake-raise", "AudioClip/handbrake_on.ogg",
                "33539a94161602a487eb5682eeb726291a20f38cf5f905662e1af42e2560bbf1");
            AssertClip(manifest, "handbrake-lower", "AudioClip/handbrake_off.ogg",
                "a7cf8eb023f72b6d6f907c8e1e409ae808c977bb27073dc5348042713f981e5e");
            foreach (string direction in new[] { "raise", "lower" })
            {
                EventSpec entry = manifest.events.Single(value =>
                    value.eventId == "audio.event.vehicle.handbrake." + direction);
                Assert.That(entry.clipId, Is.EqualTo("handbrake-" + direction));
                Assert.That(entry.volume, Is.EqualTo(1f));
                Assert.That(entry.minimumDistanceMeters, Is.EqualTo(1f));
                Assert.That(entry.maximumDistanceMeters, Is.EqualTo(8f));
            }
        }

        [Test]
        public void HandbrakeDirectionEntryPostsOnceAndRestoreAndDisableStaySilent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                assembly.Initialize();
                SatsumaHandbrakeController handbrake = instance.GetComponent<SatsumaHandbrakeController>();
                Assert.That(handbrake, Is.Not.Null);
                PartInstance part = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == SatsumaHandbrakeController.PartDefinitionId);
                MountPointAuthoring authoring = assembly.MountPoints.Single(value =>
                    value.MountId == SatsumaHandbrakeController.MountId);
                part.transform.SetPositionAndRotation(authoring.Pose.position, authoring.Pose.rotation);
                Assert.That(assembly.TryInstall(part, authoring).Succeeded, Is.True);
                Assert.That(assembly.Graph.TryGetMount(authoring.MountId, out MountPointRuntime mount), Is.True);
                foreach (FastenerInstance fastener in mount.Fasteners)
                {
                    Assert.That(fastener.TryRestore(true, true, 8), Is.True);
                }

                mount.FastenerGroup.Reevaluate(true);
                var backend = instance.AddComponent<HandbrakeRecordingAudioBackend>();
                var presenter = instance.AddComponent<VehicleAssemblyAudioPresenter>();
                Assert.That(presenter.Configure(assembly, backend), Is.True);
                Assert.That(handbrake.TrySetHeldDirection(1), Is.True);
                Assert.That(handbrake.TrySetHeldDirection(1), Is.True);
                handbrake.Simulate(0.02f);
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(1));
                Assert.That(backend.PostedEvents[0].EventId,
                    Is.EqualTo(AudioProjectIds.Events.VehicleHandbrakeRaise));
                Assert.That(backend.PostedEvents[0].WorldPosition,
                    Is.EqualTo(handbrake.LeverPivot.position));
                Assert.That(backend.PostedEvents[0].Emitter, Is.Null,
                    "Donor handbrake playback uses the lever position without following the body emitter.");
                Assert.That(backend.PostedEvents[0].ResolveWorldPosition(),
                    Is.EqualTo(handbrake.LeverPivot.position));

                Assert.That(handbrake.TrySetHeldDirection(-1), Is.True);
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(2));
                Assert.That(backend.PostedEvents[1].EventId,
                    Is.EqualTo(AudioProjectIds.Events.VehicleHandbrakeLower));
                handbrake.ReleaseHold();
                Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto
                    { positionDegrees = 10f }, out string failure), Is.True, failure);
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(2));

                // EditMode does not dispatch normal MonoBehaviour OnDisable;
                // posting itself must still reject an inactive presenter.
                presenter.enabled = false;
                Assert.That(handbrake.TrySetHeldDirection(1), Is.True);
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(2));
                handbrake.ReleaseHold();
                presenter.enabled = true;
                // Reconfiguration also must not accumulate duplicate subscriptions.
                Assert.That(presenter.Configure(assembly, backend), Is.True);
                Assert.That(handbrake.TrySetHeldDirection(-1), Is.True);
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AssertClip(AudioManifest manifest, string id, string path, string hash)
        {
            ClipSpec clip = manifest.clips.Single(value => value.clipId == id);
            Assert.That(clip.sourceRelativePath, Is.EqualTo(path));
            Assert.That(clip.sourceSha256, Is.EqualTo(hash));
        }

        [Serializable]
        private sealed class AudioManifest
        {
            public string classification;
            public ClipSpec[] clips;
            public EventSpec[] events;
        }

        [Serializable]
        private sealed class ClipSpec
        {
            public string clipId;
            public string sourceRelativePath;
            public string sourceSha256;
        }

        [Serializable]
        private sealed class EventSpec
        {
            public string eventId;
            public string clipId;
            public float volume;
            public float minimumDistanceMeters;
            public float maximumDistanceMeters;
        }
    }

    public sealed class HandbrakeRecordingAudioBackend : MonoBehaviour, IAudioBackend
    {
        public readonly List<AudioEventRequest> PostedEvents = new();
        public string BackendId => "audio.backend.handbrake_test";
        public AudioBackendKind Kind => AudioBackendKind.Unity;
        public bool IsReady => true;
        public string FailureReason => string.Empty;
        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            failure = string.Empty;
            return emitter != null;
        }
        public bool UnregisterEmitter(IAudioEmitter emitter) => emitter != null;
        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            PostedEvents.Add(request);
            return AudioEventHandles.Invalid;
        }
        public bool SetParameter(AudioParameterId id, float value, IAudioEmitter emitter = null) => true;
        public bool SetSwitch(AudioSwitchId group, AudioSwitchId value, IAudioEmitter emitter = null) => true;
        public bool SetState(AudioStateId group, AudioStateId value) => true;
        public void SetListenerContext(in AudioListenerContext context) { }
        public void ApplySettings(in AudioSettingsState settings) { }
        public void StopAll(float fadeSeconds = 0f) { }
        public AudioRuntimeSnapshot CaptureSnapshot() => default;
    }
}
