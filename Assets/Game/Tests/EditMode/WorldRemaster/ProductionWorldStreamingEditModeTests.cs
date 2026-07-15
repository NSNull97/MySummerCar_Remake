using MSC.Editor.WorldStreaming;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class ProductionWorldStreamingEditModeTests
    {
        [Test]
        public void AcceptedTwoCellManifestIsStructurallyValid()
        {
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            try
            {
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene("cell_0_-3", 0, -3, 6, "Assets/Pilot.unity"),
                        new ProductionWorldCellScene("cell_0_-2", 0, -2, 8, "Assets/Next.unity")
                    });

                Assert.That(manifest.ValidateConfiguration(), Is.Empty);
                Assert.That(manifest.TryGetCell("cell_0_-3", out ProductionWorldCellScene pilot), Is.True);
                Assert.That(pilot.Index.X, Is.EqualTo(0));
                Assert.That(pilot.Index.Z, Is.EqualTo(-3));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void DuplicateManifestIdentityAndBuildIndexAreRejected()
        {
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            try
            {
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene("cell_0_-3", 0, -3, 6, "Assets/Pilot.unity"),
                        new ProductionWorldCellScene("cell_0_-3", 0, -3, 6, "Assets/Duplicate.unity")
                    });

                Assert.That(manifest.ValidateConfiguration(), Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void CurrentProjectHasExactProductionStreamingBootstrapWiring()
        {
            WorldPilotGateRemediationValidationResult result =
                WorldPilotGateRemediationValidator.Validate(logResult: false);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Evidence, Does.Contain("exactCells=2"));
            Assert.That(result.Evidence, Does.Contain("loadAddress=buildIndex"));
        }
    }
}
