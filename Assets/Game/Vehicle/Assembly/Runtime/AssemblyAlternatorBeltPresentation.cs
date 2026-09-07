using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>One bought belt wrapper, two presentation forms. Physics and saved identity never change owners here.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyAlternatorBeltPresentation : MonoBehaviour
    {
        private VehicleAssemblyController assembly;
        private PartInstance part;
        private Transform visualRoot;
        private GameObject template;
        private GameObject installedRig;
        private Renderer[] looseRenderers = Array.Empty<Renderer>();
        private bool[] looseRendererDefaults = Array.Empty<bool>();
        private bool? observedInstalled;
        private SkinnedMeshRenderer operatingRenderer;
        private Transform scaleBone;
        private Vector3 parkedScale;
        private MaterialPropertyBlock operatingProperties;
        private static readonly int BaseMapTransform = Shader.PropertyToID("_BaseColorMap_ST");
        private static readonly int MainMapTransform = Shader.PropertyToID("_MainTex_ST");
        public GameObject InstalledRig => installedRig;

        public void Configure(VehicleAssemblyController configuredAssembly, PartInstance configuredPart,
            Transform configuredVisual, GameObject configuredTemplate)
        {
            if (configuredAssembly == null || configuredPart == null || configuredVisual == null || configuredTemplate == null ||
                configuredPart.Definition == null || configuredPart.Definition.DefinitionId != SatsumaConsumableAssemblyRules.BeltPartId ||
                configuredVisual.parent != configuredPart.transform ||
                configuredVisual.localScale.x <= 0f || configuredVisual.localScale.y <= 0f || configuredVisual.localScale.z <= 0f)
                throw new ArgumentException("Installed-belt presentation requires the existing direct item visual and reviewed template.");
            if (visualRoot == configuredVisual && template == configuredTemplate && part == configuredPart && assembly == configuredAssembly)
            { RefreshPresentation(); return; }
            RestoreLoose();
            if (installedRig != null)
            {
                installedRig.SetActive(false);
                if (Application.isPlaying) Destroy(installedRig); else DestroyImmediate(installedRig);
            }
            assembly = configuredAssembly; part = configuredPart; visualRoot = configuredVisual; template = configuredTemplate;
            looseRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            looseRendererDefaults = new bool[looseRenderers.Length];
            for (int index = 0; index < looseRenderers.Length; index++) looseRendererDefaults[index] = looseRenderers[index].enabled;
            installedRig = Instantiate(template, visualRoot);
            installedRig.name = "Project-owned mounted belt presentation";
            operatingRenderer = installedRig.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (operatingRenderer != null && operatingRenderer.bones.Length == 2)
            { scaleBone = operatingRenderer.bones[1]; parkedScale = scaleBone.localScale; }
            operatingProperties = new MaterialPropertyBlock();
            // Keep the rig inside PresentationRoot so existing item outline/query
            // rebinding sees it, but cancel that root's visual-only fitting pose.
            Matrix4x4 inverse = Matrix4x4.TRS(visualRoot.localPosition, visualRoot.localRotation, visualRoot.localScale).inverse;
            installedRig.transform.localPosition = inverse.MultiplyPoint3x4(Vector3.zero);
            installedRig.transform.localRotation = Quaternion.Inverse(visualRoot.localRotation);
            installedRig.transform.localScale = new Vector3(1f / visualRoot.localScale.x, 1f / visualRoot.localScale.y, 1f / visualRoot.localScale.z);
            observedInstalled = null;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (assembly == null || part == null || installedRig == null) return;
            bool installed = part.IsInstalled && part.RuntimeState.InstalledMountId == SatsumaConsumableAssemblyRules.BeltMountId &&
                assembly.Graph.TryGetMount(SatsumaConsumableAssemblyRules.BeltMountId, out MountPointRuntime mount) && mount.InstalledPart == part;
            if (observedInstalled == installed) return;
            observedInstalled = installed;
            for (int index = 0; index < looseRenderers.Length; index++)
                if (looseRenderers[index] != null) looseRenderers[index].enabled = !installed && looseRendererDefaults[index];
            installedRig.SetActive(installed);
        }

        private void LateUpdate() => RefreshPresentation();
        public void ApplyOperatingMotion(float phase, float alternatorAngle, bool rotating)
        {
            if (operatingRenderer == null || scaleBone == null || !float.IsFinite(phase) || !float.IsFinite(alternatorAngle)) return;
            operatingRenderer.GetPropertyBlock(operatingProperties);
            Vector4 textureTransform = new(1f, 1f, 0f, Mathf.Repeat(phase, 1f));
            operatingProperties.SetVector(BaseMapTransform, textureTransform);
            operatingProperties.SetVector(MainMapTransform, textureTransform);
            operatingRenderer.SetPropertyBlock(operatingProperties);
            // Frozen Jumping scale envelope, deterministic presentation phase
            // instead of a second random simulation or shared-material writes.
            float target = 1f + Mathf.Clamp((7f - alternatorAngle) / 15f, -.9f, .1f);
            float scale = rotating ? Mathf.Lerp(1.1f, target, .5f + .5f * Mathf.Sin(phase * Mathf.PI * 2f)) : 1.1f;
            scaleBone.localScale = new Vector3(parkedScale.x * scale / 1.1f, parkedScale.y, parkedScale.z * scale / 1.1f);
        }
        public void ResetOperatingMotion()
        {
            if (operatingRenderer != null && operatingProperties != null)
            {
                operatingRenderer.GetPropertyBlock(operatingProperties);
                operatingProperties.SetVector(BaseMapTransform, new Vector4(1f, 1f, 0f, 0f));
                operatingProperties.SetVector(MainMapTransform, new Vector4(1f, 1f, 0f, 0f));
                operatingRenderer.SetPropertyBlock(operatingProperties);
            }
            if (scaleBone != null) scaleBone.localScale = parkedScale;
        }
        private void OnDisable() { ResetOperatingMotion(); RestoreLoose(); observedInstalled = null; }
        private void RestoreLoose()
        {
            for (int index = 0; index < looseRenderers.Length; index++)
                if (looseRenderers[index] != null) looseRenderers[index].enabled = looseRendererDefaults[index];
            if (installedRig != null) installedRig.SetActive(false);
        }
    }
}
