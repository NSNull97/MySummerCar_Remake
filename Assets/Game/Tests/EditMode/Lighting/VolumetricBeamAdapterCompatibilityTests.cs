using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Lighting.Tests.EditMode
{
    public sealed class VolumetricBeamAdapterCompatibilityTests
    {
        [Test]
        public void VlbConfig_UsesHdrpSrpBatcherShaders()
        {
            UnityEngine.Object config = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Resources/VLBConfigOverride.asset");
            Assert.That(config, Is.Not.Null);

            var serialized = new SerializedObject(config);
            Assert.That(
                serialized.FindProperty("m_RenderPipeline").intValue,
                Is.EqualTo(2));
            Assert.That(
                serialized.FindProperty("m_RenderingMode").intValue,
                Is.EqualTo(3));

            Shader hdShader = serialized.FindProperty("_BeamShaderHD")
                .objectReferenceValue as Shader;
            Assert.That(hdShader, Is.Not.Null);
            Assert.That(hdShader.name, Does.Contain("_HDRP_SRPBatcher"));
        }

        [Test]
        public void LegacyVlb_DoesNotCreateDepthCameraOcclusionInHdrp()
        {
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.Not.Null,
                "This compatibility test requires the project HDRP asset.");

            var beamObject = new GameObject("VLB HDRP Compatibility Test");
            try
            {
                Light light = beamObject.AddComponent<Light>();
                light.type = LightType.Spot;
                var adapter = beamObject.AddComponent<VolumetricBeamAdapter>();

                adapter.Apply(
                    VolumetricBeamQuality.HighDefinition,
                    1f,
                    12f,
                    40f,
                    enableOcclusion: true);

                string[] componentTypes = beamObject
                    .GetComponents<Component>()
                    .Where(component => component != null)
                    .Select(component => component.GetType().FullName)
                    .ToArray();
                Assert.That(
                    componentTypes,
                    Does.Contain("VLB.VolumetricLightBeamHD"));
                Assert.That(
                    componentTypes,
                    Does.Not.Contain("VLB.VolumetricShadowHD"),
                    "VLB 2.2.3 recursively renders a depth camera during " +
                    "Update and corrupts HDRP cached-shadow state.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(beamObject);
            }
        }
    }
}
