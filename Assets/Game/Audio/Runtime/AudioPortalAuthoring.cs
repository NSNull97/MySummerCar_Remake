using UnityEngine;

namespace MSC.Audio
{
    /// <summary>
    /// Authored connection between two zones. Door/window gameplay owns
    /// openness; the audio layer only consumes the normalized value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioPortalAuthoring : MonoBehaviour
    {
        [SerializeField] private string stablePortalId = string.Empty;
        [SerializeField] private AudioEnvironmentZone firstZone;
        [SerializeField] private AudioEnvironmentZone secondZone;
        [SerializeField, Range(0f, 1f)] private float openness01 = 1f;

        public string StablePortalId => stablePortalId?.Trim() ?? string.Empty;
        public AudioEnvironmentZone FirstZone => firstZone;
        public AudioEnvironmentZone SecondZone => secondZone;
        public float Openness01 => openness01;

        public void SetOpenness(float value) => openness01 = AudioMath.Clamp01(value);

        public bool TryValidate(out string failure)
        {
            if (!AudioStableId.TryValidate(StablePortalId, out failure))
            {
                failure = "Audio portal stable ID is invalid: " + failure;
                return false;
            }

            if (firstZone == null && secondZone == null)
            {
                failure = "At least one side of an audio portal must reference an authored zone.";
                return false;
            }

            if (firstZone != null && ReferenceEquals(firstZone, secondZone))
            {
                failure = "An audio portal cannot connect a zone to itself.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
