using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.UIEditor
{
    public sealed class MainMenuStandaloneBuildSourceTests
    {
        [Test]
        public void StandaloneBuild_UsesExplicitScenesWithoutReplacingProjectBuildSettings()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/UI/Validation/StandaloneEditor/MainMenuStandaloneBuild.cs"));

            Assert.That(Regex.IsMatch(source,
                @"\bEditorBuildSettings\s*\.\s*scenes\s*="), Is.False,
                "A UI fixture must not replace the production streaming scene list, even temporarily.");
            Assert.That(source, Does.Contain("BeginExplicitSceneBuild(scenePaths)"));
            Assert.That(source, Does.Contain("BuildPipeline.BuildPlayer(new BuildPlayerOptions"));
            Assert.That(source, Does.Contain("scenes = scenePaths,"));
        }
    }
}
