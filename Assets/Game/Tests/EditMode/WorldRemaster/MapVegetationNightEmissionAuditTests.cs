using MSC.Editor.Vegetation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationNightEmissionAuditTests
    {
        [Test]
        public void GateRejectsUnchangedGrassRadiance()
        {
            var directionalControl = Frame(0.02f, 0.08f);
            var punctualControl = Frame(0.012f, 0.04f);
            var zeroRadiance = Frame(0.019f, 0.078f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    directionalControl, punctualControl, zeroRadiance);

            Assert.That(result.ControlVisible, Is.True);
            Assert.That(result.DirectionalControlVisible, Is.True);
            Assert.That(result.PunctualControlVisible, Is.True);
            Assert.That(result.AllControlsVisible, Is.True);
            Assert.That(result.ZeroRadianceSuppressed, Is.False);
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void GateAcceptsStrongZeroRadianceSuppression()
        {
            var directionalControl = Frame(0.02f, 0.08f);
            var punctualControl = Frame(0.012f, 0.04f);
            var zeroRadiance = Frame(0.0004f, 0.00005f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    directionalControl, punctualControl, zeroRadiance);

            Assert.That(result.ControlVisible, Is.True);
            Assert.That(result.PunctualControlVisible, Is.True);
            Assert.That(result.AllControlsVisible, Is.True);
            Assert.That(result.DirectionalZeroRadianceSuppressed, Is.True);
            Assert.That(result.PunctualZeroRadianceSuppressed, Is.True);
            Assert.That(result.ZeroRadianceSuppressed, Is.True);
            Assert.That(result.MeanLuminanceRatio,
                Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(result.PunctualMeanLuminanceRatio,
                Is.EqualTo(0.033333f).Within(0.0001f));
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void GateRejectsBlackDirectionalControlAsMissingRenderEvidence()
        {
            var directionalControl = Frame(0.0001f, 0.00001f);
            var punctualControl = Frame(0.012f, 0.04f);
            var zeroRadiance = Frame(0f, 0f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    directionalControl, punctualControl, zeroRadiance);

            Assert.That(result.ControlVisible, Is.False);
            Assert.That(result.PunctualControlVisible, Is.True);
            Assert.That(result.AllControlsVisible, Is.False);
            Assert.That(result.ZeroRadianceSuppressed, Is.True);
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void GateRejectsBlackPunctualControlAsMissingRenderEvidence()
        {
            var directionalControl = Frame(0.02f, 0.08f);
            var punctualControl = Frame(0.0001f, 0.00001f);
            var zeroRadiance = Frame(0f, 0f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    directionalControl, punctualControl, zeroRadiance);

            Assert.That(result.DirectionalControlVisible, Is.True);
            Assert.That(result.PunctualControlVisible, Is.False);
            Assert.That(result.AllControlsVisible, Is.False);
            Assert.That(result.ZeroRadianceSuppressed, Is.True);
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void GateRequiresSuppressionAgainstDimmerPunctualControl()
        {
            var directionalControl = Frame(0.02f, 0.08f);
            var punctualControl = Frame(0.002f, 0.01f);
            var zeroRadiance = Frame(0.0006f, 0.0016f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    directionalControl, punctualControl, zeroRadiance);

            Assert.That(result.DirectionalZeroRadianceSuppressed, Is.True);
            Assert.That(result.PunctualZeroRadianceSuppressed, Is.False);
            Assert.That(result.ZeroRadianceSuppressed, Is.False);
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void LegacyDirectionalGateOverloadRemainsAvailable()
        {
            var control = Frame(0.02f, 0.08f);
            var zeroRadiance = Frame(0.0004f, 0.00005f);

            MapVegetationNightEmissionAudit.GateEvaluation result =
                MapVegetationNightEmissionAudit.EvaluateGate(
                    control, zeroRadiance);

            Assert.That(result.HasPunctualControl, Is.False);
            Assert.That(result.ControlVisible, Is.True);
            Assert.That(result.AllControlsVisible, Is.True);
            Assert.That(result.ZeroRadianceSuppressed, Is.True);
            Assert.That(result.Passed, Is.True);
        }

        private static MapVegetationNightEmissionAudit.FrameMetrics Frame(
            float meanLuminance, float brightPixelFraction)
        {
            return new MapVegetationNightEmissionAudit.FrameMetrics
            {
                meanLuminance = meanLuminance,
                brightPixelFraction = brightPixelFraction
            };
        }
    }
}
