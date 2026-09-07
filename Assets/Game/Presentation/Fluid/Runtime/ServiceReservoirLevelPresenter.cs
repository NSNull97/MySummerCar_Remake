using System;
using MSC.Interaction.Capabilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.Fluid
{
    /// <summary>A small liquid surface inside the real opening; contains no simulation state.</summary>
    [DisallowMultipleComponent]
    public sealed class ServiceReservoirLevelPresenter : MonoBehaviour
    {
        private ILiquidLevelPresentationSource source;
        private MonoBehaviour sourceComponent;
        private GameObject surface;
        private Mesh mesh;
        private Material material;
        private float depth;
        public bool IsConfigured => source != null;
        public bool IsVisible => surface != null && surface.activeSelf;
        public Transform SurfaceTransform => surface != null ? surface.transform : null;

        public void Configure(MonoBehaviour owner, float radiusMeters, float depthMeters, Color tint)
        {
            if (source != null) throw new InvalidOperationException("Reservoir level already configured.");
            if (owner is not ILiquidLevelPresentationSource level || owner.transform != transform ||
                !float.IsFinite(radiusMeters) || radiusMeters <= 0f || !float.IsFinite(depthMeters) || depthMeters <= .01f)
                throw new ArgumentException("Explicit source/opening dimensions required.");
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null) throw new InvalidOperationException("HDRP/Lit is required for reservoir levels.");
            source = level; sourceComponent = owner; depth = depthMeters;
            material = new Material(shader) { name = "MSC Service Reservoir Liquid", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", .96f);
            material.SetFloat("_SurfaceType", 1f); material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_TransparentZWrite", 0f); material.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetSurfaceType(material, true);
            HDMaterial.SetRenderingPass(material, HDMaterial.RenderingPass.Default);
            if (!HDMaterial.ValidateMaterial(material)) throw new InvalidOperationException("Reservoir material validation failed.");
            const int segments = 32;
            var vertices = new Vector3[segments + 1]; var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radiusMeters;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            mesh = new Mesh { name = "MSC Service Opening Surface", hideFlags = HideFlags.HideAndDontSave, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            surface = new GameObject("Physical reservoir liquid level") { hideFlags = HideFlags.DontSave };
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (surface == null) return;
            bool visible = isActiveAndEnabled && sourceComponent != null && sourceComponent.isActiveAndEnabled &&
                source.IsLiquidLevelVisible && float.IsFinite(source.LiquidLevel01) && Vector3.Dot(transform.forward, Vector3.up) > .85f;
            surface.SetActive(visible);
            if (!visible) return;
            surface.transform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-depth, -.008f, Mathf.Clamp01(source.LiquidLevel01)));
            surface.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
        }
        private void LateUpdate() => RefreshPresentation();
        private void OnDisable() { if (surface != null) surface.SetActive(false); }
        private void OnDestroy()
        { Release(surface); Release(mesh); Release(material); }
        private static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
