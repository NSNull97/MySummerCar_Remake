using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;
using MSC.World.Partition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace MSC.Tests.EditMode.WorldTransfer
{
    public sealed class WorldTransferDataTests
    {
        private static readonly Vector3 GarageAnchor = new Vector3(-169.98f, -1.611f, 1040.625f);

        [Test]
        public void CoordinateConversion_KnownGarageAnchorBecomesOrigin()
        {
            WorldCoordinateConversionConfig config = WorldCoordinateConversionConfig.CreateGarageAnchored(GarageAnchor);
            Assert.That(config.ConvertPoint(GarageAnchor), Is.EqualTo(Vector3.zero).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CoordinateConversion_DirectionRotationScaleAndRoundTripArePreserved()
        {
            WorldCoordinateConversionConfig config = WorldCoordinateConversionConfig.CreateGarageAnchored(GarageAnchor);
            Vector3 point = new Vector3(1234.5f, 8.25f, -777.75f);
            Assert.That(Vector3.Distance(point, config.InverseConvertPoint(config.ConvertPoint(point))), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(Vector3.forward, config.ConvertDirection(Vector3.forward)), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 37f, 0f), config.ConvertRotation(Quaternion.Euler(0f, 37f, 0f))), Is.LessThan(0.001f));
            Assert.That(config.ConvertScale(new Vector3(-1f, 2f, 3f)), Is.EqualTo(new Vector3(-1f, 2f, 3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void ParentChildComposition_PreservesWorldPosition()
        {
            Matrix4x4 parent = Matrix4x4.TRS(new Vector3(10f, 2f, -5f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 1f, 2f));
            Matrix4x4 world = WorldTransformHierarchyUtility.ComposeWorldMatrix(parent, new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Assert.That(Vector3.Distance(new Vector3(10f, 2f, -7f), WorldTransformHierarchyUtility.ExtractPosition(world)), Is.LessThan(0.001f));
        }

        [Test]
        public void StableId_IsDeterministicValidAndRoleSensitive()
        {
            WorldStableId first = WorldStableIdUtility.Create("source", "GAME", 10, 20, "MAP/Road", "entity");
            WorldStableId second = WorldStableIdUtility.Create("source", "GAME", 10, 20, "MAP/Road", "entity");
            WorldStableId collider = WorldStableIdUtility.Create("source", "GAME", 10, 20, "MAP/Road", "collider:30");
            Assert.That(first.IsValid, Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(collider));
        }

        [Test]
        public void StableId_KnownExtractorFixtureMatches()
        {
            WorldStableId id = WorldStableIdUtility.Create(
                "msc-donor-level2-39e5c8f3", "GAME", 1064, 37122, "CABIN/Shed/garage_shed_roof", "entity");
            Assert.That(id.Value, Is.EqualTo("fb0f962be1b325cc19296c66751818c0"));
        }

        [Test]
        public void CellAssignment_HandlesNegativeCoordinatesAndLargeObjects()
        {
            Assert.That(WorldCellMembershipUtility.FromPosition(new Vector3(-0.1f, 0f, -512.1f), 512f).Id, Is.EqualTo("cell_-1_-2"));
            Assert.That(WorldCellMembershipUtility.Assign(new Bounds(Vector3.zero, new Vector3(500f, 10f, 20f)), "Road", 512f), Is.EqualTo("global"));
            Assert.That(WorldCellMembershipUtility.Assign(new Bounds(Vector3.zero, Vector3.one), "Terrain", 512f), Is.EqualTo("global"));
        }

        [Test]
        public void CellRadius_IsDeterministicAndUnique()
        {
            IReadOnlyList<WorldCellIndex> cells = WorldCellMembershipUtility.EnumerateRadius(new WorldCellIndex(0, 0), 1);
            Assert.That(cells.Count, Is.EqualTo(9));
            Assert.That(cells.Distinct().Count(), Is.EqualTo(9));
            Assert.That(cells[0].Id, Is.EqualTo("cell_-1_-1"));
        }

        [Test]
        public void EntityCsv_ParsesQuotedHierarchyAndRequiredFields()
        {
            string header = "StableId,SourceObjectId,HierarchyPath,OriginalName,SemanticCategory,ConvertedPositionX,ConvertedPositionY,ConvertedPositionZ,ConvertedBoundsMinX,ConvertedBoundsMinY,ConvertedBoundsMinZ,ConvertedBoundsMaxX,ConvertedBoundsMaxY,ConvertedBoundsMaxZ,MeshGuid,Active,CellId,InteriorExterior,LandmarkTag,ReplacementStatus,TransferStatus,ReferenceWorldEligible";
            string row = "fb0f962be1b325cc19296c66751818c0,1064,\"CABIN/Shed, Main/roof\",roof,Roof,0,0,0,-1,-1,-1,1,1,1,abc,1,cell_0_0,Exterior,PrimaryHomeGarage,DonorReference,Extracted,1";
            IReadOnlyList<WorldEntityPlacement> records = WorldEntityTable.Parse(header + "\n" + row);
            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].HierarchyPath, Is.EqualTo("CABIN/Shed, Main/roof"));
            Assert.That(records[0].Bounds.size, Is.EqualTo(Vector3.one * 2f).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void DatabaseMigration_LeavesCurrentSchemaUnchangedAndMigratesZero()
        {
            const string current = "{\"schemaVersion\": 1, \"databaseVersion\": \"04A1.1\"}";
            Assert.That(WorldGeometryDatabaseMigration.TryMigrate(current, out string same, out _), Is.True);
            Assert.That(same, Is.EqualTo(current));
            const string old = "{\"schemaVersion\": 0, \"databaseVersion\": \"04A1.0\"}";
            Assert.That(WorldGeometryDatabaseMigration.TryMigrate(old, out string migrated, out _), Is.True);
            Assert.That(migrated, Does.Contain("\"schemaVersion\": 1"));
        }

        [Test]
        public void ProjectDatabase_HasUniqueIdsResolvedCellsAndExpectedCounts()
        {
            string path = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath);
            IReadOnlyList<WorldEntityPlacement> records = WorldEntityTable.Parse(File.ReadAllText(path));
            Assert.That(records.Count, Is.EqualTo(13509));
            Assert.That(records.Select(record => record.StableId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(records.Count));
            Assert.That(records.Count(record => record.ReferenceWorldEligible), Is.EqualTo(3842));
            Assert.That(records.Where(record => record.ReferenceWorldEligible).All(record => record.CellId != "excluded"), Is.True);
            Assert.That(records.Where(record => record.ReferenceWorldEligible).All(record => !record.HierarchyPath.Contains("/COMPUTER/SYSTEM/", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(records.Where(record => record.ReferenceWorldEligible && record.Category == "RoadShoulder").All(record => record.HierarchyPath.StartsWith("MAP/", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(records.Where(record => record.ReferenceWorldEligible && record.Category == "Water").All(record => record.HierarchyPath.StartsWith("MAP/", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void ClassificationRules_AreVersionedAndContainRequiredWorldRoots()
        {
            string json = File.ReadAllText(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.RulesRelativePath));
            Assert.That(json, Does.Contain("\"schemaVersion\": 2"));
            Assert.That(json, Does.Contain("\"worldRootAllowList\""));
            Assert.That(json, Does.Contain("\"excludedHierarchyContains\""));
            Assert.That(json, Does.Contain("\"hierarchyStartsWith\""));
            Assert.That(json, Does.Contain("\"Terrain\""));
            Assert.That(json, Does.Contain("\"Road\""));
            Assert.That(json, Does.Contain("\"Water\""));
        }

        [Test]
        public void Validation_DryRunMatchesDatabasePlan()
        {
            WorldTransferValidationResult validation = WorldTransferValidator.Validate(requireGeneratedScenes: false);
            Assert.That(validation.Errors, Is.Empty);
            Assert.That(validation.EntityCount, Is.EqualTo(13509));
            Assert.That(validation.EligibleEntityCount, Is.EqualTo(3842));
            Assert.That(validation.CellCount, Is.EqualTo(49));
        }
    }
}
