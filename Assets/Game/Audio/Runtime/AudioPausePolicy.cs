namespace MSC.Audio
{
    /// <summary>
    /// Shared presentation-only pause policy. Project game time is the primary
    /// signal; Unity time scale is retained as an immediate UI safety signal.
    /// </summary>
    public static class AudioPausePolicy
    {
        public static bool ShouldPausePlayback(
            bool gameTimePaused,
            float unityTimeScale) =>
            gameTimePaused ||
            !float.IsFinite(unityTimeScale) ||
            unityTimeScale <= 0.0001f;
    }
}
