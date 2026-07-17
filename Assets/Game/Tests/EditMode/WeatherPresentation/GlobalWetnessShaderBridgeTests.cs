using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class GlobalWetnessShaderBridgeTests
    {
        private GlobalWetnessShaderBridge bridge;

        [SetUp]
        public void SetUp()
        {
            bridge = new GlobalWetnessShaderBridge();
            bridge.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            bridge.Reset();
        }

        [Test]
        public void Apply_NewRevision_WritesAllSharedOutputs()
        {
            var outputs = new WetnessEnvironmentOutputs(
                new WetnessState(0.2f, 0.4f, 0.6f, 0.8f),
                SurfaceExposureProfile.Exterior.StableId,
                3U);

            bridge.Apply(outputs);

            Assert.That(Shader.GetGlobalFloat(GlobalWetnessShaderBridge.GroundWetnessProperty), Is.EqualTo(0.2f));
            Assert.That(Shader.GetGlobalFloat(GlobalWetnessShaderBridge.RoadWetnessProperty), Is.EqualTo(0.4f));
            Assert.That(Shader.GetGlobalFloat(GlobalWetnessShaderBridge.PuddleAmountProperty), Is.EqualTo(0.6f));
            Assert.That(Shader.GetGlobalFloat(GlobalWetnessShaderBridge.VegetationWetnessProperty), Is.EqualTo(0.8f));
            Assert.That(bridge.LastAppliedRevision, Is.EqualTo(3U));
        }

        [Test]
        public void Apply_SameRevision_IsNoOp()
        {
            bridge.Apply(new WetnessEnvironmentOutputs(
                new WetnessState(0.2f, 0.2f, 0.2f, 0.2f),
                SurfaceExposureProfile.Exterior.StableId,
                1U));
            bridge.Apply(new WetnessEnvironmentOutputs(
                new WetnessState(1f, 1f, 1f, 1f),
                SurfaceExposureProfile.Exterior.StableId,
                1U));

            Assert.That(Shader.GetGlobalFloat(GlobalWetnessShaderBridge.GroundWetnessProperty), Is.EqualTo(0.2f));
        }
    }
}
