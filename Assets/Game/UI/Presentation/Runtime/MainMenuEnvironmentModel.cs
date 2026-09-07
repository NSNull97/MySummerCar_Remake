using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.UI.Presentation
{
    /// <summary>Static, isolated menu presentation; owns no world or persistent state.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuEnvironmentModel : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private Transform vehicleAnchor;
        [SerializeField] private VolumeProfile menuLightingProfile;
        [SerializeField] private Vector3 cameraFromVehicleDirection;
        [SerializeField] private Vector3 sourceOrigin;
        [SerializeField] private float groundHeightWorld;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Renderer garageLampRenderer;
        [SerializeField] private int garageLampMaterialIndex;
        [SerializeField] private Transform garageLampAnchor;
        [SerializeField] private Bounds homeExteriorBoundsLocal;

        public IReadOnlyList<Renderer> Renderers => renderers;
        public int RendererCount => renderers.Length;
        public Transform VehicleAnchor => vehicleAnchor;
        public VolumeProfile MenuLightingProfile => menuLightingProfile;
        /// <summary>Direction from the parked vehicle toward the camera, in environment-local axes.</summary>
        public Vector3 CameraFromVehicleDirection => cameraFromVehicleDirection;
        public Vector3 SourceOrigin => sourceOrigin;
        public float GroundHeightWorld => groundHeightWorld;
        public Bounds LocalBounds => localBounds;
        public Renderer GarageLampRenderer => garageLampRenderer;
        public int GarageLampMaterialIndex => garageLampMaterialIndex;
        /// <summary>Centre of the existing lamp's lower housing; forward points toward the yard.</summary>
        public Transform GarageLampAnchor => garageLampAnchor;
        /// <summary>House and garage exterior only, excluding the intact cross-cell terrain.</summary>
        public Bounds HomeExteriorBoundsLocal => homeExteriorBoundsLocal;

        public void ConfigureAtmosphereForAuthoring(Renderer lampRenderer, int materialIndex,
            Transform lampAnchor, Bounds houseBoundsLocal)
        {
            if (lampRenderer == null || Array.IndexOf(renderers, lampRenderer) < 0)
                throw new ArgumentException("The garage lamp must be an explicit renderer of this environment.");
            if (materialIndex < 0 || materialIndex >= lampRenderer.sharedMaterials.Length)
                throw new ArgumentOutOfRangeException(nameof(materialIndex));
            if (lampAnchor == null || lampAnchor == transform || !lampAnchor.IsChildOf(transform))
                throw new ArgumentException("The garage lamp anchor must belong to the menu environment.");
            Vector3 size = houseBoundsLocal.size;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f ||
                !IsFinite(size) || !IsFinite(houseBoundsLocal.center))
                throw new ArgumentException("The measured house exterior bounds are invalid.");
            garageLampRenderer = lampRenderer;
            garageLampMaterialIndex = materialIndex;
            garageLampAnchor = lampAnchor;
            homeExteriorBoundsLocal = houseBoundsLocal;
        }

        public void ConfigureForAuthoring(Renderer[] environmentRenderers, Transform anchor,
            Vector3 cameraDirection, Vector3 worldOrigin, float measuredGroundHeight, VolumeProfile lightingProfile)
        {
            if (environmentRenderers == null || environmentRenderers.Length == 0)
                throw new ArgumentException("The menu environment has no renderers.");
            if (anchor == null || anchor == transform || !anchor.IsChildOf(transform))
                throw new ArgumentException("The vehicle anchor must belong to the menu environment.");
            if (!float.IsFinite(measuredGroundHeight) || cameraDirection.sqrMagnitude < 0.1f)
                throw new ArgumentException("The menu environment placement is invalid.");
            if (lightingProfile == null) throw new ArgumentNullException(nameof(lightingProfile));
            menuLightingProfile = lightingProfile;
            renderers = environmentRenderers;
            vehicleAnchor = anchor;
            cameraFromVehicleDirection = cameraDirection.normalized;
            sourceOrigin = worldOrigin;
            groundHeightWorld = measuredGroundHeight;
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.transform.IsChildOf(transform))
                    throw new ArgumentException("Environment renderers must belong to the model.");
                Bounds bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    if (initialized) localBounds.Encapsulate(point);
                    else { localBounds = new Bounds(point, Vector3.zero); initialized = true; }
                }
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
