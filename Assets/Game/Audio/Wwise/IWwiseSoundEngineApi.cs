using System;
using UnityEngine;

namespace MSC.Audio.Wwise
{
    /// <summary>
    /// Narrow, project-owned seam around the static official Wwise API. Runtime
    /// uses <see cref="AkUnitySoundEngineApi"/>; tests can exercise adapter
    /// policy without starting the native sound engine or loading SoundBanks.
    /// </summary>
    public interface IWwiseSoundEngineApi
    {
        bool IsInitialized { get; }

        uint InvalidPlayingId { get; }

        bool RegisterGameObject(
            GameObject gameObject,
            string profilerName,
            out string failure);

        bool UnregisterGameObject(GameObject gameObject, out string failure);

        bool SetObjectPosition(GameObject gameObject, Transform source, out string failure);

        uint PostEvent(
            string eventName,
            GameObject gameObject,
            Action<uint> onEnded,
            out string failure);

        bool StopPlayingId(uint playingId, int fadeMilliseconds, out string failure);

        bool SetRtpc(
            string parameterName,
            float value,
            GameObject gameObject,
            out string failure);

        bool SetRtpcByPlayingId(
            string parameterName,
            float value,
            uint playingId,
            out string failure);

        bool SetSwitch(
            string groupName,
            string valueName,
            GameObject gameObject,
            out string failure);

        bool SetState(string groupName, string valueName, out string failure);
    }
}
