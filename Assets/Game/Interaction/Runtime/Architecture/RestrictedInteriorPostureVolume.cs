using System.Collections.Generic;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Architecture
{
    /// <summary>
    /// Non-physical interior volume that asks capable actors to use their
    /// confined-space posture policy. It deliberately does not know about the
    /// player module or vehicle implementation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RestrictedInteriorPostureVolume : MonoBehaviour
    {
        private readonly Dictionary<MonoBehaviour, int> overlapCounts =
            new Dictionary<MonoBehaviour, int>();

        [SerializeField]
        private BoxCollider volume;

        public BoxCollider Volume => volume;

        public void Configure(BoxCollider authoredVolume)
        {
            volume = authoredVolume != null
                ? authoredVolume
                : GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        public bool TryEnter(Collider other)
        {
            MonoBehaviour participant = FindParticipant(other);
            if (participant == null)
            {
                return false;
            }

            if (overlapCounts.TryGetValue(participant, out int count))
            {
                overlapCounts[participant] = count + 1;
                return true;
            }

            overlapCounts.Add(participant, 1);
            ((IRestrictedInteriorPostureParticipant)participant)
                .EnterRestrictedInteriorPosture();
            return true;
        }

        public bool TryExit(Collider other)
        {
            MonoBehaviour participant = FindParticipant(other);
            if (participant == null ||
                !overlapCounts.TryGetValue(participant, out int count))
            {
                return false;
            }

            if (count > 1)
            {
                overlapCounts[participant] = count - 1;
                return true;
            }

            overlapCounts.Remove(participant);
            ((IRestrictedInteriorPostureParticipant)participant)
                .ExitRestrictedInteriorPosture();
            return true;
        }

        private void Reset()
        {
            Configure(GetComponent<BoxCollider>());
        }

        private void Awake()
        {
            Configure(volume);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            TryExit(other);
        }

        private void OnDisable()
        {
            foreach (MonoBehaviour participant in overlapCounts.Keys)
            {
                if (participant != null &&
                    participant is IRestrictedInteriorPostureParticipant capable)
                {
                    capable.ExitRestrictedInteriorPosture();
                }
            }

            overlapCounts.Clear();
        }

        private static MonoBehaviour FindParticipant(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            MonoBehaviour[] candidates =
                other.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] is IRestrictedInteriorPostureParticipant)
                {
                    return candidates[index];
                }
            }

            return null;
        }
    }
}
