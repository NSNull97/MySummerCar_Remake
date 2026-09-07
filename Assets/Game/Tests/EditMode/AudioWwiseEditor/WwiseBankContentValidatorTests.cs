using System;
using System.IO;
using System.Text;
using MSC.Audio;
using MSC.Audio.Wwise.Editor;
using NUnit.Framework;

namespace MSC.Tests.EditMode.AudioWwiseEditor
{
    public sealed class WwiseBankContentValidatorTests
    {
        private string fixtureRoot;

        [SetUp]
        public void SetUp()
        {
            fixtureRoot = Path.Combine(Path.GetTempPath(),
                "msc-wwise-content-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
        }

        [Test]
        public void ExistingMappingWithoutBank_IsNotCoverage()
        {
            AssertFailure(Inspect(), WwiseBankContentFailure.MissingBank);
        }

        [Test]
        public void BinaryWithoutMetadata_IsNotCoverage()
        {
            WriteBank();
            AssertFailure(Inspect(), WwiseBankContentFailure.MissingMetadata);
        }

        [Test]
        public void IncludedEventWithDirectMedia_PassesStaticCoverageOnly()
        {
            WriteBank();
            WriteMetadata();
            Assert.That(Inspect().Succeeded, Is.True);
            Assert.That(Inspect().InspectedRouteCount, Is.EqualTo(1));
        }

        [Test]
        public void MediaInNestedSwitchContainer_IsRecognized()
        {
            WriteBank();
            WriteMetadata(media: "\"SwitchContainers\":[{\"SwitchContainers\":[{\"MediaRefs\":[{\"Id\":\"77\"}]}]}]");
            Assert.That(Inspect().Succeeded, Is.True);
        }

        [Test]
        public void MappedEventMissingFromGeneratedMetadata_Fails()
        {
            WriteBank();
            WriteMetadata(eventName: "Play_Other");
            AssertFailure(Inspect(), WwiseBankContentFailure.MissingEvent);
        }

        [Test]
        public void StaleMetadataWithEventAbsentFromBinary_Fails()
        {
            WriteBank(eventId: 999);
            WriteMetadata();
            AssertFailure(Inspect(), WwiseBankContentFailure.MissingEvent);
        }

        [Test]
        public void MetadataForDifferentBank_Fails()
        {
            WriteBank();
            WriteMetadata(bankId: 999);
            AssertFailure(Inspect(), WwiseBankContentFailure.InvalidMetadata);
        }

        [Test]
        public void ExistingPlayEventWithoutMedia_IsReportedAsEmpty()
        {
            WriteBank();
            WriteMetadata(media: "\"MediaRefs\":[]");
            AssertFailure(Inspect(), WwiseBankContentFailure.EmptyPlaybackEvent);
        }

        [Test]
        public void EmptyMediaReferencesAreNotAcceptedAsContent()
        {
            WriteBank();
            WriteMetadata(media: "\"MediaRefs\":[{\"Id\":\"0\"}]");
            AssertFailure(Inspect(), WwiseBankContentFailure.EmptyPlaybackEvent);
        }

        [Test]
        public void StopEventDoesNotRequirePlaybackMedia()
        {
            WriteBank();
            WriteMetadata(eventName: "Stop_Test", media: "\"MediaRefs\":[]");
            Assert.That(Inspect("Stop_Test").Succeeded, Is.True);
        }

        [Test]
        public void TruncatedBinaryDoesNotFallBackToTrustingJson()
        {
            File.WriteAllBytes(Path.Combine(fixtureRoot, "MSC_Test.bnk"),
                new byte[] { 66, 75, 72, 68, 32, 0, 0, 0 });
            WriteMetadata();
            AssertFailure(Inspect(), WwiseBankContentFailure.InvalidBank);
        }

        [Test]
        public void MalformedJsonIsStructuredFailure()
        {
            WriteBank();
            File.WriteAllText(Path.Combine(fixtureRoot, "MSC_Test.json"), "not json");
            AssertFailure(Inspect(), WwiseBankContentFailure.InvalidMetadata);
        }

        [Test]
        public void RouteMustDeclarePlainBankName()
        {
            AssertFailure(Inspect(bank: "../MSC_Test"), WwiseBankContentFailure.InvalidRoute);
        }

        [Test]
        public void CallerExplicitlyExcludesUnityRoutes_NoImplicitFallbackIsInvented()
        {
            WwiseBankContentReport report = WwiseBankContentValidator.Inspect(
                fixtureRoot, Array.Empty<AudioEventMapEntry>());
            Assert.That(report.Succeeded, Is.True);
            Assert.That(report.InspectedRouteCount, Is.Zero);
        }

        private WwiseBankContentReport Inspect(string eventName = "Play_Test", string bank = "MSC_Test") =>
            WwiseBankContentValidator.Inspect(fixtureRoot, new[]
            {
                new AudioEventMapEntry("audio.event.test", eventName, bank),
            });

        private static void AssertFailure(WwiseBankContentReport report, WwiseBankContentFailure kind)
        {
            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Kind, Is.EqualTo(kind));
            Assert.That(report.Issues[0].StableId, Is.EqualTo("audio.event.test"));
            Assert.Throws<InvalidDataException>(() => report.ThrowIfFailed());
        }

        private void WriteMetadata(uint bankId = 42, string eventName = "Play_Test",
            string media = "\"MediaRefs\":[{\"Id\":\"77\"}]")
        {
            File.WriteAllText(Path.Combine(fixtureRoot, "MSC_Test.json"),
                "{\"SoundBanksInfo\":{\"SoundBanks\":[{\"Id\":\"" + bankId +
                "\",\"ShortName\":\"MSC_Test\",\"Events\":[{\"Id\":\"123\",\"Name\":\"" +
                eventName + "\"," + media + "}]}]}}");
        }

        private void WriteBank(uint eventId = 123, bool hasAction = true)
        {
            // Minimal version-172 chunk fixture for evidence parsing, not a
            // playable SoundBank and never submitted to the real SoundEngine.
            using (FileStream stream = File.Create(Path.Combine(fixtureRoot, "MSC_Test.bnk")))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("BKHD"));
                writer.Write((uint)8);
                writer.Write((uint)172);
                writer.Write((uint)42);
                writer.Write(Encoding.ASCII.GetBytes("HIRC"));
                writer.Write((uint)(hasAction ? 25 : 21));
                writer.Write((uint)1);
                writer.Write((byte)4);
                writer.Write((uint)(hasAction ? 16 : 12));
                writer.Write(eventId);
                writer.Write(new byte[7]); // Additional version-172 Event fields.
                writer.Write((byte)(hasAction ? 1 : 0));
                if (hasAction) writer.Write((uint)88);
            }
        }
    }
}
