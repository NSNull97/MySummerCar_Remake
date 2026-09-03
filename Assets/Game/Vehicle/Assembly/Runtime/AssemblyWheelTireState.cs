using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Owns the physical/presentation distinction between a bare wheel rim and
    /// the same wheel with a tyre installed. A mounted rim must contact the
    /// world at the metal rim radius; only an explicitly installed tyre may
    /// expose the larger road-contact radius.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyWheelTireState : MonoBehaviour
    {
        [SerializeField] private bool tireInstalled;
        [SerializeField, Min(0.01f)] private float rimRadiusMeters = 0.16f;
        [SerializeField, Min(0.01f)] private float rimWidthMeters = 0.15f;
        [SerializeField, Min(0.01f)] private float tireRadiusMeters = 0.27f;
        [SerializeField, Min(0.01f)] private float tireWidthMeters = 0.15f;
        [SerializeField] private Renderer[] tireRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Collider[] tireColliders =
            Array.Empty<Collider>();

        [NonSerialized] private int revision;

        public bool HasTire => tireInstalled;

        public float RimRadiusMeters => rimRadiusMeters;

        public float RimWidthMeters => rimWidthMeters;

        public float TireRadiusMeters => tireRadiusMeters;

        public float TireWidthMeters => tireWidthMeters;

        public float CurrentContactRadiusMeters => tireInstalled
            ? tireRadiusMeters
            : rimRadiusMeters;

        public float CurrentContactWidthMeters => tireInstalled
            ? tireWidthMeters
            : rimWidthMeters;

        public int Revision => revision;

        public Renderer[] TireRenderers => tireRenderers;

        public Collider[] TireColliders => tireColliders;

        public void Configure(
            bool initiallyInstalled,
            float configuredRimRadiusMeters,
            float configuredRimWidthMeters,
            float configuredTireRadiusMeters,
            float configuredTireWidthMeters,
            Renderer[] configuredRenderers,
            Collider[] configuredColliders)
        {
            tireInstalled = initiallyInstalled;
            rimRadiusMeters = Mathf.Max(0.01f, configuredRimRadiusMeters);
            rimWidthMeters = Mathf.Max(0.01f, configuredRimWidthMeters);
            tireRadiusMeters = Mathf.Max(
                rimRadiusMeters,
                configuredTireRadiusMeters);
            tireWidthMeters = Mathf.Max(0.01f, configuredTireWidthMeters);
            tireRenderers = configuredRenderers ?? Array.Empty<Renderer>();
            tireColliders = configuredColliders ?? Array.Empty<Collider>();
            revision++;
            ApplyAuthoredState();
        }

        public void BindTireColliders(Collider[] configuredColliders)
        {
            tireColliders = configuredColliders ?? Array.Empty<Collider>();
            revision++;
            ApplyAuthoredState();
        }

        public bool SetTireInstalled(bool installed)
        {
            if (tireInstalled == installed)
            {
                ApplyAuthoredState();
                return false;
            }

            tireInstalled = installed;
            revision++;
            ApplyAuthoredState();
            return true;
        }

        public void ApplyAuthoredState()
        {
            for (int index = 0; index < tireRenderers.Length; index++)
            {
                Renderer renderer = tireRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = tireInstalled;
                }
            }

            for (int index = 0; index < tireColliders.Length; index++)
            {
                Collider collider = tireColliders[index];
                if (collider != null)
                {
                    collider.enabled = tireInstalled;
                }
            }
        }

        private void Awake()
        {
            ApplyAuthoredState();
        }

        private void OnEnable()
        {
            ApplyAuthoredState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            rimRadiusMeters = Mathf.Max(0.01f, rimRadiusMeters);
            rimWidthMeters = Mathf.Max(0.01f, rimWidthMeters);
            tireRadiusMeters = Mathf.Max(rimRadiusMeters, tireRadiusMeters);
            tireWidthMeters = Mathf.Max(0.01f, tireWidthMeters);
            ApplyAuthoredState();
        }
#endif
    }
}
