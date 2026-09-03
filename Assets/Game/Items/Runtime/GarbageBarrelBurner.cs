using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// One-way authoritative burn volume for the donor garbage barrel.
    /// Items keep their stable IDs and are marked consumed instead of destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarbageBarrelBurner : MonoBehaviour
    {
        private const float BurnDelaySeconds = 2.5f;

        private sealed class BurnCandidate
        {
            public float RemainingSeconds;
        }

        private readonly Dictionary<WorldItemInstance, BurnCandidate>
            burnCandidates =
                new Dictionary<WorldItemInstance, BurnCandidate>();
        private readonly HashSet<WorldItemInstance> currentOverlaps =
            new HashSet<WorldItemInstance>();
        private readonly List<WorldItemInstance> removalBuffer =
            new List<WorldItemInstance>();
        private readonly Collider[] overlapBuffer = new Collider[128];

        private WorldItemInstance barrel;
        private BoxCollider burnTrigger;

        public void Configure(
            WorldItemInstance configuredBarrel,
            Vector3 barrelSize)
        {
            barrel = configuredBarrel;
            burnTrigger = gameObject.AddComponent<BoxCollider>();
            burnTrigger.isTrigger = true;
            ConfigureVolume(new Bounds(Vector3.zero, barrelSize));
        }

        public void ConfigureVolume(Bounds localBounds)
        {
            if (burnTrigger == null)
            {
                return;
            }

            Vector3 size = localBounds.size;
            int verticalAxis = LargestAxis(size);
            Vector3 vertical = AxisVector(verticalAxis);
            // Keep almost the entire physical cavity hot. The former 68%
            // radial box missed objects resting naturally against a wall.
            Vector3 triggerSize = size * 0.92f;
            SetAxis(
                ref triggerSize,
                verticalAxis,
                Mathf.Max(0.25f, GetAxis(size, verticalAxis) * 0.88f));
            burnTrigger.size = triggerSize;
            burnTrigger.center = localBounds.center +
                vertical * GetAxis(size, verticalAxis) * 0.015f;

        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float elapsedSeconds)
        {
            if (barrel == null || burnTrigger == null ||
                !barrel.IsIgnited ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                burnCandidates.Clear();
                return;
            }

            currentOverlaps.Clear();
            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                burnTrigger.size * 0.5f,
                new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(
                transform.TransformPoint(burnTrigger.center),
                halfExtents,
                overlapBuffer,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider other = overlapBuffer[index];
                overlapBuffer[index] = null;
                WorldItemInstance item = other != null
                    ? other.GetComponentInParent<WorldItemInstance>()
                    : null;
                if (item == null || item == barrel ||
                    !currentOverlaps.Add(item))
                {
                    continue;
                }

                if (!burnCandidates.TryGetValue(
                        item,
                        out BurnCandidate candidate))
                {
                    candidate = new BurnCandidate
                    {
                        RemainingSeconds = BurnDelaySeconds,
                    };
                    burnCandidates.Add(item, candidate);
                }

                candidate.RemainingSeconds -= elapsedSeconds;
                if (candidate.RemainingSeconds > 0f)
                {
                    continue;
                }

                if (item.TryBurnInGarbageBarrel())
                {
                    burnCandidates.Remove(item);
                }
                else
                {
                    // Protected/rejected items remain physically present but
                    // should not retry every fixed step.
                    candidate.RemainingSeconds = float.PositiveInfinity;
                }
            }

            removalBuffer.Clear();
            foreach (KeyValuePair<WorldItemInstance, BurnCandidate> entry in
                     burnCandidates)
            {
                if (entry.Key == null || !currentOverlaps.Contains(entry.Key))
                {
                    removalBuffer.Add(entry.Key);
                }
            }

            for (int index = 0; index < removalBuffer.Count; index++)
            {
                burnCandidates.Remove(removalBuffer[index]);
            }
        }

        private static int LargestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static Vector3 AxisVector(int axis) =>
            axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward,
            };

        private static float GetAxis(Vector3 value, int axis) =>
            axis switch
            {
                0 => value.x,
                1 => value.y,
                _ => value.z,
            };

        private static void SetAxis(ref Vector3 value, int axis, float axisValue)
        {
            switch (axis)
            {
                case 0: value.x = axisValue; break;
                case 1: value.y = axisValue; break;
                default: value.z = axisValue; break;
            }
        }
    }
}
