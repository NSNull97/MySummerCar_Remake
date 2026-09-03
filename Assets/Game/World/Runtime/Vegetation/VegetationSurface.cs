using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Explicit opt-in marker for mesh ground that can receive vegetation.
    /// Source UVs are intentionally irrelevant; painting uses world XZ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VegetationSurface : MonoBehaviour
    {
        [SerializeField] private bool allowShortGrass = true;
        [SerializeField] private bool allowMeadowGrass = true;
        [SerializeField] private bool allowTallGrass = true;
        [SerializeField] private bool allowDecorativeVegetation = true;
        [SerializeField, Range(0f, 90f)] private float slopeOverrideDegrees = 90f;

        public Collider SurfaceCollider => GetComponent<Collider>();
        public float SlopeOverrideDegrees => slopeOverrideDegrees;

        public bool Allows(VegetationDensityChannel channel)
        {
            return channel switch
            {
                VegetationDensityChannel.ShortGrass => allowShortGrass,
                VegetationDensityChannel.MeadowGrass => allowMeadowGrass,
                VegetationDensityChannel.TallGrass => allowTallGrass,
                VegetationDensityChannel.Decorative => allowDecorativeVegetation,
                _ => false
            };
        }

        public bool IsValid(out string error)
        {
            if (SurfaceCollider == null)
            {
                error = name + " has VegetationSurface but no Collider.";
                return false;
            }

            if (!SurfaceCollider.enabled || !SurfaceCollider.gameObject.activeInHierarchy)
            {
                error = name + " has a disabled vegetation surface collider.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            bool shortGrass,
            bool meadowGrass,
            bool tallGrass,
            bool decorative,
            float maximumSlopeDegrees)
        {
            allowShortGrass = shortGrass;
            allowMeadowGrass = meadowGrass;
            allowTallGrass = tallGrass;
            allowDecorativeVegetation = decorative;
            slopeOverrideDegrees = Mathf.Clamp(maximumSlopeDegrees, 0f, 90f);
        }
#endif
    }
}
