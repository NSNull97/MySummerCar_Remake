using System;
using MSC.Audio.UnityFallback;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioUnityFallback
{
    public sealed class UnityAudioCalibrationTests
    {
        [Test]
        public void ExplicitSelectedContentCeilingBoundsBaseAndBoostWithoutChangingOtherEventsOrMute()
        {
            var definition = new UnityAudioEventDefinition(); definition.ConfigureMixGainForAuthoring(18f,.82f);
            Assert.That(definition.OutputGainCeiling,Is.Zero);
            Assert.That(definition.ApplyMixGain(1.6f,1f),Is.EqualTo(1.6f));
            definition.ConfigureOutputGainCeilingForAuthoring(.85f);
            Assert.That(definition.ApplyMixGain(1.6f,1f),Is.EqualTo(.85f));
            Assert.That(definition.ApplyMixGain(.8f,.5f),Is.EqualTo(.425f));
            Assert.That(definition.ApplyMixGain(.1f,1f),Is.EqualTo(.7943282f).Within(.00001f));
            Assert.That(definition.ApplyMixGain(0f,1f),Is.Zero);
            Assert.That(definition.ApplyMixGain(1f,0f),Is.Zero);
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-.01f,1.01f})
                Assert.Throws<ArgumentOutOfRangeException>(()=>definition.ConfigureOutputGainCeilingForAuthoring(invalid));
            definition.ConfigureOutputGainCeilingForAuthoring(0f);
            Assert.That(definition.ApplyMixGain(1.6f,1f),Is.EqualTo(1.6f));
        }

        [Test]
        public void EventMixIsIndependentOfClipCorrectionAndOldDefinitionsAreExactBypasses()
        {
            var definition = new UnityAudioEventDefinition();
            Assert.That(definition.MixGainDb, Is.Zero);
            Assert.That(definition.MixLinearGain, Is.EqualTo(1f));
            definition.ConfigureCalibrationForAuthoring(5.3f);
            definition.ConfigureMixGainForAuthoring(12f);
            Assert.That(definition.CalibrationGainDb, Is.EqualTo(5.3f));
            Assert.That(definition.MixLinearGain, Is.EqualTo(3.9810717f).Within(.00001f));
            Assert.That(definition.CalibrationLinearGain * definition.MixLinearGain, Is.LessThan(8f));
            Assert.That(definition.Volume, Is.EqualTo(1f));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -12.01f, 18.01f })
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureMixGainForAuthoring(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureCalibrationForAuthoring(6.01f));
            definition.ConfigureCalibrationForAuthoring(6f);
            Assert.That(definition.CalibrationLinearGain * definition.MixLinearGain, Is.LessThan(8f));
            definition.ConfigureMixGainForAuthoring(0f);
            Assert.That(definition.CalibrationGainDb, Is.EqualTo(6f));
            Assert.That(definition.MixLinearGain, Is.EqualTo(1f));
        }

        [Test]
        public void StrongQuietEndDriveRequiresSafeHeadroomAndDoesNotExpandTheOverflowStage()
        {
            var definition = new UnityAudioEventDefinition();
            definition.ConfigureCalibrationForAuthoring(5.3f);
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureMixGainForAuthoring(18f));
            definition.ConfigureMixGainForAuthoring(18f, .95f);
            Assert.That(definition.ApplyMixGain(.146f, 1f), Is.EqualTo(.95f));
            Assert.That(definition.ApplyMixGain(.073f, .5f), Is.EqualTo(.475f));
            Assert.That(definition.ApplyMixGain(.01f, 1f), Is.EqualTo(.07943282f).Within(.00001f));
            Assert.That(definition.ApplyMixGain(0f, 1f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureMixGainForAuthoring(18f, 0f));
            definition.ConfigureCalibrationForAuthoring(12f);
            for (int step = 0; step <= 100; step++)
            {
                float calibrated = step / 100f * definition.CalibrationLinearGain;
                Assert.That(definition.ApplyMixGain(calibrated, 1f), Is.LessThanOrEqualTo(4f));
            }
        }

        [Test]
        public void AddedBedBoostHasHeadroomAndTracksUserVolumeWithoutReducingTheOriginalLevel()
        {
            var definition = new UnityAudioEventDefinition();
            definition.ConfigureMixGainForAuthoring(12f, .6f);
            Assert.That(definition.ApplyMixGain(.1f, 1f), Is.EqualTo(.39810717f).Within(.00001f));
            Assert.That(definition.ApplyMixGain(.3f, 1f), Is.EqualTo(.6f));
            Assert.That(definition.ApplyMixGain(.15f, .5f), Is.EqualTo(.3f));
            Assert.That(definition.ApplyMixGain(.8f, 1f), Is.EqualTo(.8f));
            Assert.That(definition.ApplyMixGain(0f, 0f), Is.Zero);
            Assert.That(definition.ApplyMixGain(0f, 1f), Is.Zero, "No volume floor may turn a silent RTPC into sound.");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -.01f, 1.01f })
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureMixGainForAuthoring(6f, invalid));
            definition.ConfigureMixGainForAuthoring(0f, .6f);
            Assert.That(definition.ApplyMixGain(.8f, 1f), Is.EqualTo(.8f));
        }

        [Test]
        public void MissingCalibrationIsExactLegacyBypassAndSixDecibelsIsASeparateGain()
        {
            var definition = new UnityAudioEventDefinition();
            Assert.That(definition.CalibrationGainDb, Is.Zero);
            Assert.That(definition.CalibrationLinearGain, Is.EqualTo(1f));
            definition.ConfigureCalibrationForAuthoring(6f);
            Assert.That(definition.CalibrationLinearGain, Is.EqualTo(1.9952623f).Within(.00001f));
            Assert.That(definition.Volume, Is.EqualTo(1f), "No legacy field is repurposed.");
            foreach (float sample in new[] { -2f, -1f, -.1f, 0f, .1f, 1f, 2f })
                Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(sample, 1f), Is.EqualTo(sample));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -12.1f, 12.1f })
                Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConfigureCalibrationForAuthoring(value));
        }

        [Test]
        public void OverflowPreservesSmallSignalsHasContinuousKneeAndFiniteSymmetricCeiling()
        {
            Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(.1f, 2f), Is.EqualTo(.2f));
            float previous = 0f;
            for (int i = 0; i <= 10000; i++)
            {
                float sample = i / 10000f;
                float actual = UnityAudioCalibrationFilter.ApplyOverflow(sample, 4f);
                Assert.That(actual, Is.InRange(previous, UnityAudioCalibrationFilter.PeakCeiling));
                Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(-sample, 4f), Is.EqualTo(-actual));
                previous = actual;
            }
            Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(float.NaN, 4f), Is.Zero);
            Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(.2f, float.NaN), Is.EqualTo(.2f));
            Assert.That(UnityAudioCalibrationFilter.ApplyOverflow(.200001f, 4f) -
                UnityAudioCalibrationFilter.ApplyOverflow(.199999f, 4f), Is.LessThan(.000009f));
        }
    }
}
