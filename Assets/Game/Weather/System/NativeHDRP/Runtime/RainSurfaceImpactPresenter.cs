using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System.NativeHDRP
{
    /// <summary>
    /// Converts project-owned precipitation collision events into short-lived
    /// surface droplets. The weather simulation remains authoritative; this is
    /// a bounded presentation-only effect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainSurfaceImpactPresenter : MonoBehaviour
    {
        private const int CollisionEventCapacity = 256;
        private static readonly ProfilerMarker CollisionMarker =
            new ProfilerMarker("Weather.RainSurfaceImpacts");

        [SerializeField] private ParticleSystem source;
        [SerializeField] private ParticleSystem impactSystem;
        [SerializeField, Min(1)] private int maximumDropletsPerFrame = 48;

        private readonly List<ParticleCollisionEvent> collisionEvents =
            new List<ParticleCollisionEvent>(CollisionEventCapacity);
        private int emissionFrame = -1;
        private int emittedThisFrame;
        private uint emissionSequence;

        public ParticleSystem Source => source;
        public ParticleSystem ImpactSystem => impactSystem;
        public int MaximumDropletsPerFrame => maximumDropletsPerFrame;

        public void ConfigureForRuntime(
            ParticleSystem precipitationSource,
            ParticleSystem authoredImpactSystem,
            int dropletBudgetPerFrame = 48)
        {
            source = precipitationSource;
            impactSystem = authoredImpactSystem;
            maximumDropletsPerFrame = Mathf.Max(1, dropletBudgetPerFrame);
        }

        private void OnParticleCollision(GameObject other)
        {
            using (CollisionMarker.Auto())
            {
                if (source == null || impactSystem == null || other == null)
                {
                    return;
                }

                if (emissionFrame != Time.frameCount)
                {
                    emissionFrame = Time.frameCount;
                    emittedThisFrame = 0;
                }

                int remaining = maximumDropletsPerFrame - emittedThisFrame;
                if (remaining <= 0)
                {
                    return;
                }

                int eventCount = source.GetCollisionEvents(other, collisionEvents);
                if (eventCount <= 0)
                {
                    return;
                }

                int impactCount = Mathf.Min(
                    eventCount,
                    Mathf.Max(1, remaining / 2));
                int stride = Mathf.Max(1, eventCount / impactCount);
                for (int eventIndex = 0;
                     eventIndex < eventCount &&
                     emittedThisFrame < maximumDropletsPerFrame;
                     eventIndex += stride)
                {
                    EmitImpactDroplet(collisionEvents[eventIndex], -1f);
                    if (emittedThisFrame < maximumDropletsPerFrame)
                    {
                        EmitImpactDroplet(collisionEvents[eventIndex], 1f);
                    }
                }
            }
        }

        private void EmitImpactDroplet(
            in ParticleCollisionEvent collision,
            float lateralSign)
        {
            Vector3 normal = collision.normal.sqrMagnitude > 0.0001f
                ? collision.normal.normalized
                : Vector3.up;
            float phase = Hash01(emissionSequence++);
            Vector3 reference = Mathf.Abs(normal.y) < 0.92f
                ? Vector3.up
                : Vector3.right;
            Vector3 tangent = Vector3.Cross(normal, reference).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            float angle = phase * Mathf.PI * 2f;
            Vector3 lateral =
                tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
            float upwardSpeed = Mathf.Lerp(0.45f, 1.15f, phase);
            float lateralSpeed = Mathf.Lerp(0.18f, 0.72f, 1f - phase);

            var emission = new ParticleSystem.EmitParams
            {
                position = collision.intersection + normal * 0.012f,
                velocity = normal * upwardSpeed +
                           lateral * lateralSpeed * lateralSign,
                startLifetime = Mathf.Lerp(0.12f, 0.24f, phase),
                startSize = Mathf.Lerp(0.012f, 0.032f, phase),
                startColor = new Color(0.72f, 0.78f, 0.84f, 0.34f),
            };
            impactSystem.Emit(emission, 1);
            emittedThisFrame++;
        }

        private static float Hash01(uint value)
        {
            uint hash = value + 0x9E3779B9u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }
}
