using UnityEngine;

namespace MSC.Audio
{
    /// <summary>Explicit listener-space metadata for authored interior zones.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioEnvironmentZone : MonoBehaviour
    {
        [SerializeField] private string stableZoneId = string.Empty;
        [SerializeField] private Collider zoneCollider;
        [SerializeField] private int priority;
        [SerializeField] private AudioListenerSpace listenerSpace = AudioListenerSpace.Interior;
        [SerializeField, Range(0f, 1f)] private float shelter01 = 1f;
        [SerializeField, Range(0f, 1f)] private float obstruction01 = 0.25f;
        [SerializeField, Range(0f, 1f)] private float reverbSend01 = 0.35f;

        public string StableZoneId => stableZoneId?.Trim() ?? string.Empty;
        public Collider ZoneCollider => zoneCollider;
        public int Priority => priority;
        public bool IsUsable =>
            isActiveAndEnabled &&
            zoneCollider != null &&
            zoneCollider.isTrigger &&
            AudioStableId.TryValidate(StableZoneId, out _);

        public AudioEnvironmentContext CreateContext(
            float precipitation01,
            float wind01,
            float normalizedDayTime01) => new AudioEnvironmentContext(
                listenerSpace,
                shelter01,
                obstruction01,
                reverbSend01,
                precipitation01,
                wind01,
                normalizedDayTime01);

        public void Configure(
            string zoneId,
            Collider trigger,
            int authoredPriority,
            AudioListenerSpace space,
            float shelter,
            float obstruction,
            float reverbSend)
        {
            stableZoneId = zoneId?.Trim() ?? string.Empty;
            zoneCollider = trigger;
            priority = authoredPriority;
            listenerSpace = space;
            shelter01 = AudioMath.Clamp01(shelter);
            obstruction01 = AudioMath.Clamp01(obstruction);
            reverbSend01 = AudioMath.Clamp01(reverbSend);
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }

        public bool TryValidate(out string failure)
        {
            if (!AudioStableId.TryValidate(StableZoneId, out failure))
            {
                failure = "Audio zone stable ID is invalid: " + failure;
                return false;
            }

            if (zoneCollider == null)
            {
                failure = "Audio zone collider is missing.";
                return false;
            }

            if (!zoneCollider.isTrigger)
            {
                failure = "Audio zone collider must be a trigger.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void Reset()
        {
            zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }
    }
}
