using System;
using System.IO;
using MSC.LegacyImport.Editor.Pipeline;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class Sha256FileHasherTests
    {
        [Test]
        public void Compute_MatchesKnownSha256()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(path, "abc");

                Assert.That(
                    Sha256FileHasher.Compute(path),
                    Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
                Assert.That(
                    Sha256FileHasher.Matches(
                        path,
                        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"),
                    Is.True);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
