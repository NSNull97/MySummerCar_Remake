using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.World.Remaster;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class WorldContinuousGroundBaselineTests
    {
        private static readonly string[] ExpectedPieceIds =
        {
            "piece_cell_-4_0",
            "piece_cell_-3_0"
        };

        [Test]
        public void VoidDefinitions_ParseApprovedTeimoPiecesWithStableProvenance()
        {
            IReadOnlyList<WorldVoidRegionPieceDefinition> definitions =
                WorldContinuousGroundBaselineBuilder.LoadDefinitions();

            Assert.That(definitions.Count, Is.EqualTo(ExpectedPieceIds.Length));
            Assert.That(
                definitions.Select(definition => definition.PieceId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                Is.EqualTo(ExpectedPieceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
            Assert.That(
                definitions.Select(definition => definition.StableEntityId).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(definitions.Count));

            foreach (WorldVoidRegionPieceDefinition definition in definitions)
            {
                Assert.That(definition.RegionId, Is.EqualTo("M05C1-VOID-TEIMO-001"));
                Assert.That(definition.Classification, Is.EqualTo(WorldVoidRegionClassification.IntentionalDonorVoid));
                Assert.That(definition.ImplementationStatus, Is.EqualTo("ApprovedBoundedPilot"));
                Assert.That(definition.CellId, Does.Match(@"^cell_-?[0-9]+_-?[0-9]+$"));
                Assert.That(definition.Stations.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(definition.LongitudinalStepMeters, Is.GreaterThan(0f));
                Assert.That(definition.LongitudinalStepMeters, Is.LessThanOrEqualTo(10f));
                Assert.That(definition.DeclaredAuthoringFingerprintSha256,
                    Is.EqualTo(definition.ComputedAuthoringFingerprintSha256));
                Assert.That(definition.MeshAssetPath,
                    Does.StartWith("Assets/Game/World/Production/Terrain/VoidFill/"));
                Assert.That(definition.SceneAssetPath,
                    Does.StartWith("Assets/Game/World/Generated/VoidFillCells/"));
            }
        }

        [Test]
        public void VoidDefinitions_CreateFiniteIndexedGeometryAndContinuousCellSeam()
        {
            IReadOnlyList<WorldVoidRegionPieceDefinition> definitions =
                WorldContinuousGroundBaselineBuilder.LoadDefinitions()
                    .Where(definition => definition.ImplementationStatus == "ApprovedBoundedPilot")
                    .ToArray();
            Assert.That(definitions.Count, Is.EqualTo(2));

            WorldVoidRegionPieceDefinition leftDefinition = definitions.Single(definition => definition.CellId == "cell_-4_0");
            WorldVoidRegionPieceDefinition rightDefinition = definitions.Single(definition => definition.CellId == "cell_-3_0");
            WorldVoidPieceGeometry[] geometry =
            {
                WorldContinuousGroundBaselineBuilder.CreateGeometry(leftDefinition),
                WorldContinuousGroundBaselineBuilder.CreateGeometry(rightDefinition)
            };

            for (int index = 0; index < geometry.Length; index++)
            {
                WorldVoidPieceGeometry piece = geometry[index];
                Assert.That(piece.Vertices, Is.Not.Empty);
                Assert.That(piece.Vertices.Length % 2, Is.EqualTo(0));
                Assert.That(piece.Uv.Length, Is.EqualTo(piece.Vertices.Length));
                Assert.That(piece.Triangles.Length, Is.EqualTo((piece.Vertices.Length - 2) * 3));
                Assert.That(piece.Triangles, Has.All.GreaterThanOrEqualTo(0));
                Assert.That(piece.Triangles, Has.All.LessThan(piece.Vertices.Length));
                Assert.That(piece.Vertices.All(IsFinite), Is.True);
                Assert.That(piece.Uv.All(IsFinite), Is.True);
            }

            WorldVoidPieceGeometry left = geometry[0];
            WorldVoidPieceGeometry right = geometry[1];
            Assert.That(left.Vertices.Length, Is.EqualTo(right.Vertices.Length));
            for (int row = 0; row < left.Vertices.Length / 2; row++)
            {
                Vector3 leftSeam = left.Vertices[row * 2 + 1] + left.CellOrigin;
                Vector3 rightSeam = right.Vertices[row * 2] + right.CellOrigin;
                Assert.That(Vector3.Distance(leftSeam, rightSeam), Is.LessThanOrEqualTo(0.0001f),
                    $"Void-fill seam diverges at station row {row}.");
            }
        }

        [Test]
        public void VoidBaselineValidator_PassesGeneratedDefinitionsAndScenes()
        {
            WorldContinuousGroundBaselineValidationResult result =
                WorldContinuousGroundBaselineValidator.Validate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Errors, Is.Empty);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}
