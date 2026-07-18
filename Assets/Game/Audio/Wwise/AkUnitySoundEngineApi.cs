using System;
using UnityEngine;

namespace MSC.Audio.Wwise
{
    /// <summary>
    /// Production bridge to the official Wwise Unity Integration 2025.1 API.
    /// No vendor type crosses the project-owned interface boundary.
    /// </summary>
    public sealed class AkUnitySoundEngineApi : IWwiseSoundEngineApi
    {
        public bool IsInitialized
        {
            get
            {
                try
                {
                    return AkUnitySoundEngine.IsInitialized();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public uint InvalidPlayingId => AkUnitySoundEngine.AK_INVALID_PLAYING_ID;

        public bool RegisterGameObject(
            GameObject gameObject,
            string profilerName,
            out string failure)
        {
            if (gameObject == null)
            {
                failure = "A Unity GameObject is required for Wwise registration.";
                return false;
            }

            try
            {
                AKRESULT result = AkUnitySoundEngine.RegisterGameObj(
                    gameObject,
                    string.IsNullOrWhiteSpace(profilerName)
                        ? gameObject.name
                        : profilerName.Trim());
                return ResolveResult("RegisterGameObj", result, out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("RegisterGameObj", exception, out failure);
            }
        }

        public bool UnregisterGameObject(GameObject gameObject, out string failure)
        {
            if (gameObject == null)
            {
                failure = string.Empty;
                return true;
            }

            try
            {
                return ResolveResult(
                    "UnregisterGameObj",
                    AkUnitySoundEngine.UnregisterGameObj(gameObject),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("UnregisterGameObj", exception, out failure);
            }
        }

        public bool SetObjectPosition(
            GameObject gameObject,
            Transform source,
            out string failure)
        {
            if (gameObject == null || source == null)
            {
                failure = "A registered GameObject and Transform are required for Wwise positioning.";
                return false;
            }

            try
            {
                return ResolveResult(
                    "SetObjectPosition",
                    AkUnitySoundEngine.SetObjectPosition(gameObject, source),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("SetObjectPosition", exception, out failure);
            }
        }

        public uint PostEvent(
            string eventName,
            GameObject gameObject,
            Action<uint> onEnded,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(eventName) || gameObject == null)
            {
                failure = "A Wwise event name and registered GameObject are required.";
                return InvalidPlayingId;
            }

            try
            {
                uint playingId = AkUnitySoundEngine.PostEvent(
                    eventName.Trim(),
                    gameObject,
                    (uint)AkCallbackType.AK_EndOfEvent,
                    ForwardEndOfEvent,
                    onEnded);
                if (playingId == InvalidPlayingId)
                {
                    failure = $"PostEvent rejected Wwise event '{eventName.Trim()}'.";
                    return playingId;
                }

                failure = string.Empty;
                return playingId;
            }
            catch (Exception exception)
            {
                failure = $"PostEvent failed: {exception.Message}";
                return InvalidPlayingId;
            }
        }

        public bool StopPlayingId(
            uint playingId,
            int fadeMilliseconds,
            out string failure)
        {
            if (playingId == InvalidPlayingId)
            {
                failure = "A valid Wwise PlayingID is required.";
                return false;
            }

            try
            {
                AkUnitySoundEngine.ExecuteActionOnPlayingID(
                    AkActionOnEventType.AkActionOnEventType_Stop,
                    playingId,
                    Math.Max(0, fadeMilliseconds),
                    AkCurveInterpolation.AkCurveInterpolation_Linear);
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return ResolveException(
                    "ExecuteActionOnPlayingID",
                    exception,
                    out failure);
            }
        }

        public bool SetRtpc(
            string parameterName,
            float value,
            GameObject gameObject,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                failure = "A Wwise RTPC name is required.";
                return false;
            }

            try
            {
                return ResolveResult(
                    "SetRTPCValue",
                    AkUnitySoundEngine.SetRTPCValue(
                        parameterName.Trim(),
                        value,
                        gameObject),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("SetRTPCValue", exception, out failure);
            }
        }

        public bool SetRtpcByPlayingId(
            string parameterName,
            float value,
            uint playingId,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(parameterName) ||
                playingId == InvalidPlayingId)
            {
                failure = "A Wwise RTPC name and valid PlayingID are required.";
                return false;
            }

            try
            {
                return ResolveResult(
                    "SetRTPCValueByPlayingID",
                    AkUnitySoundEngine.SetRTPCValueByPlayingID(
                        parameterName.Trim(),
                        value,
                        playingId),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException(
                    "SetRTPCValueByPlayingID",
                    exception,
                    out failure);
            }
        }

        public bool SetSwitch(
            string groupName,
            string valueName,
            GameObject gameObject,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(groupName) ||
                string.IsNullOrWhiteSpace(valueName) ||
                gameObject == null)
            {
                failure = "Wwise switch group/value names and a registered GameObject are required.";
                return false;
            }

            try
            {
                return ResolveResult(
                    "SetSwitch",
                    AkUnitySoundEngine.SetSwitch(
                        groupName.Trim(),
                        valueName.Trim(),
                        gameObject),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("SetSwitch", exception, out failure);
            }
        }

        public bool SetState(string groupName, string valueName, out string failure)
        {
            if (string.IsNullOrWhiteSpace(groupName) ||
                string.IsNullOrWhiteSpace(valueName))
            {
                failure = "Wwise state group and value names are required.";
                return false;
            }

            try
            {
                return ResolveResult(
                    "SetState",
                    AkUnitySoundEngine.SetState(groupName.Trim(), valueName.Trim()),
                    out failure);
            }
            catch (Exception exception)
            {
                return ResolveException("SetState", exception, out failure);
            }
        }

        private static void ForwardEndOfEvent(
            object cookie,
            AkCallbackType callbackType,
            AkCallbackInfo callbackInfo)
        {
            if (callbackType != AkCallbackType.AK_EndOfEvent ||
                !(cookie is Action<uint> callback))
            {
                return;
            }

            uint playingId = callbackInfo is AkEventCallbackInfo eventInfo
                ? eventInfo.playingID
                : AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
            callback(playingId);
        }

        private static bool ResolveResult(
            string operation,
            AKRESULT result,
            out string failure)
        {
            if (result == AKRESULT.AK_Success)
            {
                failure = string.Empty;
                return true;
            }

            failure = $"{operation} returned {result}.";
            return false;
        }

        private static bool ResolveException(
            string operation,
            Exception exception,
            out string failure)
        {
            failure = $"{operation} failed: {exception.Message}";
            return false;
        }
    }
}
