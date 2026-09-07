using System;
using System.IO;
using System.Linq;
using System.Text;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class UserAudioWavePreparationTests
    {
        [Test]
        public void NoPreparationRetainsExactInputAndExplicitTrimPreservesReviewedSamples()
        {
            byte[] original = Wave(1000);
            byte[] untouched = (byte[])original.Clone();
            Assert.That(UserAudioWavePreparation.Prepare(original, 0, 0), Is.SameAs(original));
            byte[] prepared = UserAudioWavePreparation.Prepare(original, 700, 0);
            Assert.That(prepared.Length, Is.EqualTo(44 + 700 * 4));
            CollectionAssert.AreEqual(original.Skip(44).Take(700 * 4), prepared.Skip(44));
            CollectionAssert.AreEqual(untouched, original);
            CollectionAssert.AreEqual(prepared, UserAudioWavePreparation.Prepare(original, 700, 0));
        }

        [Test]
        public void LoopCrossfadeHasNoSilentPaddingAndContinuesAcrossTheSeam()
        {
            byte[] original = Wave(1000);
            byte[] prepared = UserAudioWavePreparation.Prepare(original, 800, 100);
            Assert.That(prepared.Length, Is.EqualTo(44 + 700 * 4));
            Assert.That(BitConverter.ToSingle(prepared, 44), Is.EqualTo(Sample(100)));
            Assert.That(BitConverter.ToSingle(prepared, prepared.Length - 4), Is.EqualTo(Sample(99)).Within(.000001f));
            float seam = Math.Abs(BitConverter.ToSingle(prepared, 44) - BitConverter.ToSingle(prepared, prepared.Length - 4));
            Assert.That(seam, Is.LessThan(.011f), "The last sample joins the naturally following source sample.");
        }

        [TestCase(-1, 0)]
        [TestCase(0, 10)]
        [TestCase(1000, 0)]
        [TestCase(1100, 0)]
        [TestCase(800, -1)]
        [TestCase(800, 401)]
        public void InvalidReviewRangesFailBeforeAnyOutput(int end, int crossfade)
        {
            Assert.Throws<InvalidDataException>(() => UserAudioWavePreparation.Prepare(Wave(1000), end, crossfade));
        }

        [Test]
        public void ReviewedSteadySegmentSkipsTheRevWithoutChangingSourceOrOldPreparation()
        {
            byte[] original = Wave(2000), untouched = (byte[])original.Clone();
            byte[] prepared = UserAudioWavePreparation.Prepare(original, 1800, 100, 400);
            Assert.That(prepared.Length, Is.EqualTo(44 + 1300 * 4));
            Assert.That(BitConverter.ToSingle(prepared, 44), Is.EqualTo(Sample(500)));
            Assert.That(BitConverter.ToSingle(prepared, prepared.Length - 4), Is.EqualTo(Sample(499)));
            CollectionAssert.AreEqual(untouched, original);
            Assert.Throws<InvalidDataException>(() => UserAudioWavePreparation.Prepare(original, 500, 100, 400));
            Assert.Throws<InvalidDataException>(() => UserAudioWavePreparation.Prepare(original, 500, 0, -1));
            CollectionAssert.AreEqual(UserAudioWavePreparation.Prepare(original, 700, 100),
                UserAudioWavePreparation.Prepare(original, 700, 100, 0));
        }

        [Test]
        public void UnreviewedPcmFormatAndMalformedHeaderFailClosed()
        {
            byte[] original = Wave(1000);
            original[20] = 1; // PCM integer is deliberately not an accepted float source.
            Assert.Throws<InvalidDataException>(() => UserAudioWavePreparation.Prepare(original, 700, 0));
            Assert.Throws<InvalidDataException>(() => UserAudioWavePreparation.Prepare(new byte[12], 5, 0));
        }

        private static float Sample(int index) => .1f + (float)Math.Sin(index * .03d) * .05f;

        private static byte[] Wave(int frames)
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 4);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)3); writer.Write((short)1); writer.Write(44100); writer.Write(176400);
            writer.Write((short)4); writer.Write((short)32);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(frames * 4);
            for (int i = 0; i < frames; i++) writer.Write(Sample(i));
            return output.ToArray();
        }
    }
}
