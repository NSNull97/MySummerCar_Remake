using System.IO;
using MSC.LegacyImport.Editor.Configuration;
using NUnit.Framework;

namespace MSC.Tests.EditMode.Configuration
{
    public sealed class DonorPathConfigurationTests
    {
        [Test]
        public void ParseJson_NormalizesRequiredPathsAndPreservesEmptyOptionalTools()
        {
            string root = Path.Combine(Path.GetTempPath(), "msc-foundation-path-test");
            string json = CreateJson(root + Path.DirectorySeparatorChar);

            DonorPathConfiguration configuration = DonorPathConfiguration.ParseJson(json);

            Assert.That(configuration.OriginalGameDirectory, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(configuration.UnityProjectDirectory, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(configuration.DonorStagingDirectory, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(configuration.LegacyReferenceDirectory, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(configuration.ReferenceMediaDirectory, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(configuration.AssetRipperExecutable, Is.Empty);
            Assert.That(configuration.ILSpyCmdExecutable, Is.Empty);
            Assert.That(configuration.UnityEditorExecutable, Is.Empty);
        }

        [Test]
        public void ParseJson_RejectsMissingRequiredDirectory()
        {
            const string json =
                "{\"OriginalGameDirectory\":\"\",\"UnityProjectDirectory\":\"\",\"DonorStagingDirectory\":\"\"," +
                "\"LegacyReferenceDirectory\":\"\",\"ReferenceMediaDirectory\":\"\"}";

            Assert.Throws<System.FormatException>(() => DonorPathConfiguration.ParseJson(json));
        }

        private static string CreateJson(string path)
        {
            string escapedPath = path.Replace("\\", "\\\\");
            return
                "{" +
                $"\"OriginalGameDirectory\":\"{escapedPath}\"," +
                $"\"UnityProjectDirectory\":\"{escapedPath}\"," +
                $"\"DonorStagingDirectory\":\"{escapedPath}\"," +
                $"\"LegacyReferenceDirectory\":\"{escapedPath}\"," +
                $"\"ReferenceMediaDirectory\":\"{escapedPath}\"," +
                "\"AssetRipperExecutable\":\"\"," +
                "\"ILSpyCmdExecutable\":\"\"," +
                "\"UnityEditorExecutable\":\"\"" +
                "}";
        }
    }
}
