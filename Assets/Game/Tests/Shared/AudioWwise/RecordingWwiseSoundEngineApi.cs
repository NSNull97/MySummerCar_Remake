using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.Wwise;
using UnityEngine;

namespace MSC.Tests.AudioWwise
{
    public sealed class RecordingWwiseSoundEngineApi : IWwiseSoundEngineApi
    {
        private readonly Dictionary<uint, Action<uint>> endCallbacks =
            new Dictionary<uint, Action<uint>>();
        private uint nextPlayingId = 100U;

        public bool IsInitializedValue { get; set; } = true;
        public bool IsInitialized => IsInitializedValue;
        public uint InvalidPlayingId => 0U;
        public List<GameObject> RegisteredGameObjects { get; } = new List<GameObject>();
        public List<GameObject> UnregisteredGameObjects { get; } = new List<GameObject>();
        public List<PositionCall> PositionCalls { get; } = new List<PositionCall>();
        public List<EventCall> EventCalls { get; } = new List<EventCall>();
        public List<StopCall> StopCalls { get; } = new List<StopCall>();
        public List<RtpcCall> RtpcCalls { get; } = new List<RtpcCall>();
        public List<RtpcPlayingIdCall> RtpcPlayingIdCalls { get; } =
            new List<RtpcPlayingIdCall>();
        public List<SwitchCall> SwitchCalls { get; } = new List<SwitchCall>();
        public List<StateCall> StateCalls { get; } = new List<StateCall>();

        public bool RegisterGameObject(
            GameObject gameObject,
            string profilerName,
            out string failure)
        {
            if (!IsInitializedValue || gameObject == null)
            {
                failure = "Recording Wwise API is unavailable.";
                return false;
            }

            RegisteredGameObjects.Add(gameObject);
            failure = string.Empty;
            return true;
        }

        public bool UnregisterGameObject(GameObject gameObject, out string failure)
        {
            if (gameObject != null)
            {
                UnregisteredGameObjects.Add(gameObject);
            }

            failure = string.Empty;
            return true;
        }

        public bool SetObjectPosition(
            GameObject gameObject,
            Transform source,
            out string failure)
        {
            PositionCalls.Add(new PositionCall(gameObject, source));
            failure = string.Empty;
            return true;
        }

        public uint PostEvent(
            string eventName,
            GameObject gameObject,
            Action<uint> onEnded,
            out string failure)
        {
            uint playingId = nextPlayingId++;
            EventCalls.Add(new EventCall(eventName, gameObject, playingId));
            endCallbacks[playingId] = onEnded;
            failure = string.Empty;
            return playingId;
        }

        public bool StopPlayingId(
            uint playingId,
            int fadeMilliseconds,
            out string failure)
        {
            StopCalls.Add(new StopCall(playingId, fadeMilliseconds));
            endCallbacks.Remove(playingId);
            failure = string.Empty;
            return true;
        }

        public bool SetRtpc(
            string parameterName,
            float value,
            GameObject gameObject,
            out string failure)
        {
            RtpcCalls.Add(new RtpcCall(parameterName, value, gameObject));
            failure = string.Empty;
            return true;
        }

        public bool SetRtpcByPlayingId(
            string parameterName,
            float value,
            uint playingId,
            out string failure)
        {
            RtpcPlayingIdCalls.Add(
                new RtpcPlayingIdCall(parameterName, value, playingId));
            failure = string.Empty;
            return true;
        }

        public bool SetSwitch(
            string groupName,
            string valueName,
            GameObject gameObject,
            out string failure)
        {
            SwitchCalls.Add(new SwitchCall(groupName, valueName, gameObject));
            failure = string.Empty;
            return true;
        }

        public bool SetState(string groupName, string valueName, out string failure)
        {
            StateCalls.Add(new StateCall(groupName, valueName));
            failure = string.Empty;
            return true;
        }

        public void Complete(uint playingId)
        {
            if (endCallbacks.TryGetValue(playingId, out Action<uint> callback))
            {
                endCallbacks.Remove(playingId);
                callback?.Invoke(playingId);
            }
        }

        public readonly struct PositionCall
        {
            public PositionCall(GameObject gameObject, Transform source)
            {
                GameObject = gameObject;
                Source = source;
            }

            public GameObject GameObject { get; }
            public Transform Source { get; }
        }

        public readonly struct EventCall
        {
            public EventCall(string eventName, GameObject gameObject, uint playingId)
            {
                EventName = eventName;
                GameObject = gameObject;
                PlayingId = playingId;
            }

            public string EventName { get; }
            public GameObject GameObject { get; }
            public uint PlayingId { get; }
        }

        public readonly struct StopCall
        {
            public StopCall(uint playingId, int fadeMilliseconds)
            {
                PlayingId = playingId;
                FadeMilliseconds = fadeMilliseconds;
            }

            public uint PlayingId { get; }
            public int FadeMilliseconds { get; }
        }

        public readonly struct RtpcCall
        {
            public RtpcCall(string name, float value, GameObject gameObject)
            {
                Name = name;
                Value = value;
                GameObject = gameObject;
            }

            public string Name { get; }
            public float Value { get; }
            public GameObject GameObject { get; }
        }

        public readonly struct RtpcPlayingIdCall
        {
            public RtpcPlayingIdCall(string name, float value, uint playingId)
            {
                Name = name;
                Value = value;
                PlayingId = playingId;
            }

            public string Name { get; }
            public float Value { get; }
            public uint PlayingId { get; }
        }

        public readonly struct SwitchCall
        {
            public SwitchCall(string groupName, string valueName, GameObject gameObject)
            {
                GroupName = groupName;
                ValueName = valueName;
                GameObject = gameObject;
            }

            public string GroupName { get; }
            public string ValueName { get; }
            public GameObject GameObject { get; }
        }

        public readonly struct StateCall
        {
            public StateCall(string groupName, string valueName)
            {
                GroupName = groupName;
                ValueName = valueName;
            }

            public string GroupName { get; }
            public string ValueName { get; }
        }
    }

    public sealed class RecordingAudioEmitter : MonoBehaviour, IAudioEmitter
    {
        private string stableId = "audio.emitter.test";

        public string StableId => stableId;
        public Transform AudioTransform => transform;
        public int OwningSceneHandle => gameObject.scene.handle;
        public bool IsAudioEmitterActive => isActiveAndEnabled;
        public AudioSurfaceContext SurfaceContext => default;
        public AudioEnvironmentContext EnvironmentContext => default;

        public void Configure(string configuredStableId)
        {
            stableId = configuredStableId?.Trim() ?? string.Empty;
        }
    }
}
