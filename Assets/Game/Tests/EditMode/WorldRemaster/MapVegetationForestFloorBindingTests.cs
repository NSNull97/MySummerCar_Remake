using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationForestFloorBindingTests
    {
        [Test]
        public void ApprovedPools_AreDisjointAndContainOnlyTheirExpectedAssetFamilies()
        {
            var rocks = MapVegetationForestFloorBindings
                .ApprovedSourcePaths(MapVegetationForestFloorBindings.Pool.RocksAndBoulders)
                .ToArray();
            var understory = MapVegetationForestFloorBindings
                .ApprovedSourcePaths(MapVegetationForestFloorBindings.Pool.Understory)
                .ToArray();
            var shrubs = MapVegetationForestFloorBindings
                .ApprovedSourcePaths(MapVegetationForestFloorBindings.Pool.Shrubs)
                .ToArray();

            Assert.That(rocks, Has.Length.EqualTo(5));
            Assert.That(understory, Has.Length.EqualTo(12));
            Assert.That(shrubs, Has.Length.EqualTo(4));
            Assert.That(rocks.Intersect(understory), Is.Empty);
            Assert.That(rocks.Intersect(shrubs), Is.Empty);
            Assert.That(understory.Intersect(shrubs), Is.Empty);
            Assert.That(rocks.All(path =>
                path.Contains("ALPSpruceTreesPack") &&
                (path.Contains("Rock") || path.Contains("stone"))), Is.True);
            Assert.That(understory.All(path =>
                path.Contains("fern") || path.Contains("moss") ||
                path.Contains("Mushroom") || path.Contains("dead_grass") ||
                path.Contains("detail_branches") || path.Contains("detail_poplar_leaves")), Is.True);
            string[] organicLitter = understory.Where(path =>
                path.Contains("detail_branches") || path.Contains("detail_poplar_leaves")).ToArray();
            Assert.That(organicLitter, Has.Length.EqualTo(2));
            Assert.That(organicLitter.Count(path => path.Contains("prefab_detail_branches_01.prefab")), Is.EqualTo(1));
            Assert.That(organicLitter.Count(path => path.Contains("prefab_detail_poplar_leaves_01_1.prefab")), Is.EqualTo(1));
            Assert.That(understory.Count(path => path.Contains("dead_grass")), Is.EqualTo(1));
            Assert.That(understory.Any(path =>
                path.Contains("Demo Scenes") || path.Contains("Particles") ||
                path.Contains("detail_leaves") || path.Contains("dead_log") ||
                path.Contains("stump")), Is.False);
            Assert.That(shrubs.All(path =>
                path.Contains("Meadow Environment Dynamic Nature") &&
                path.Contains("prefab_grey_willow_")), Is.True);
            Assert.That(shrubs.Any(path => path.Contains("ALPSpruceTreesPack")), Is.False);
        }

        [Test]
        public void OrganicLitter_ReusesTwoExistingUnderstoryOutputSlots()
        {
            const string branchId =
                "forest-floor:ecology:slot-audit:conifer-litter:0";
            const string leafId =
                "forest-floor:ecology:slot-audit:deciduous-litter:0";
            string branchSource = MapVegetationForestFloorBindings
                .SelectSourcePath(MapVegetationForestFloorBindings.Pool.Understory,
                    branchId, 20260831);
            string leafSource = MapVegetationForestFloorBindings
                .SelectSourcePath(MapVegetationForestFloorBindings.Pool.Understory,
                    leafId, 20260831);
            string branchOutput = MapVegetationForestFloorBindings
                .SelectOutputPath(MapVegetationForestFloorBindings.Pool.Understory,
                    branchId, 20260831);
            string leafOutput = MapVegetationForestFloorBindings
                .SelectOutputPath(MapVegetationForestFloorBindings.Pool.Understory,
                    leafId, 20260831);

            Assert.That(branchSource, Does.Contain(
                "prefab_detail_branches_01.prefab"));
            Assert.That(leafSource, Does.Contain(
                "prefab_detail_poplar_leaves_01_1.prefab"));
            Assert.That(branchOutput, Does.EndWith("/Understory/DeadGrass02.prefab"));
            Assert.That(leafOutput, Does.EndWith("/Understory/DeadGrass03.prefab"));
        }

        [Test]
        public void GenericUnderstory_NeverSelectsCanopyBoundOrganicLitter()
        {
            for (int index = 0; index < 4096; index++)
            {
                string source = MapVegetationForestFloorBindings.SelectSourcePath(
                    MapVegetationForestFloorBindings.Pool.Understory,
                    "undergrowth:boundary:" + index, 20260831);
                Assert.That(source, Does.Not.Contain("detail_branches"));
                Assert.That(source, Does.Not.Contain("detail_poplar_leaves"));
            }
        }

        [Test]
        public void StableSelection_IsRepeatableAndNeverCrossesPools()
        {
            const string id = "forest-floor:cell_1_2:17:9";
            string rockA = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.RocksAndBoulders, id, 20260831);
            string rockB = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.RocksAndBoulders, id, 20260831);
            string plant = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Understory, id, 20260831);
            string shrub = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Shrubs, id, 20260831);

            Assert.That(rockB, Is.EqualTo(rockA));
            Assert.That(rockA, Does.Contain("ALPSpruceTreesPack"));
            Assert.That(plant, Does.Contain("NatureManufacture Assets"));
            Assert.That(shrub, Does.Contain("prefab_grey_willow_"));
            Assert.That(plant, Is.Not.EqualTo(rockA));
            Assert.That(shrub, Is.Not.EqualTo(plant));
        }

        [Test]
        public void PlacementPolicies_PreserveSourceHeightAndDoNotReuseShrubSettings()
        {
            var rocks = MapVegetationForestFloorBindings.Policy(
                MapVegetationForestFloorBindings.Pool.RocksAndBoulders);
            var understory = MapVegetationForestFloorBindings.Policy(
                MapVegetationForestFloorBindings.Pool.Understory);
            var shrubs = MapVegetationForestFloorBindings.Policy(
                MapVegetationForestFloorBindings.Pool.Shrubs);

            Assert.That(rocks.UsesSourceHeight, Is.True);
            Assert.That(understory.UsesSourceHeight, Is.True);
            Assert.That(shrubs.UsesSourceHeight, Is.True);
            Assert.That(understory.MinimumSpacingMeters, Is.EqualTo(5.5f));
            Assert.That(understory.Density, Is.EqualTo(0.28f));
            Assert.That(shrubs.MinimumSpacingMeters, Is.EqualTo(6f));
            Assert.That(shrubs.Density, Is.EqualTo(0.55f));
            Assert.That(rocks.MinimumSpacingMeters, Is.GreaterThan(understory.MinimumSpacingMeters));
            Assert.That(shrubs.MinimumSpacingMeters, Is.GreaterThan(understory.MinimumSpacingMeters));
            Assert.That(shrubs.MinimumSpacingMeters, Is.LessThan(rocks.MinimumSpacingMeters));
            Assert.That(rocks.Density, Is.LessThan(understory.Density));
            Assert.That(shrubs.Density, Is.GreaterThan(understory.Density));
            Assert.That(rocks.NormalAlignment, Is.LessThan(understory.NormalAlignment));
            Assert.That(shrubs.NormalAlignment, Is.LessThan(understory.NormalAlignment));
        }

        [Test]
        public void PlacementFingerprint_IsStableSha256ForCurrentPoliciesAndDependencies()
        {
            string first = MapVegetationForestFloorBindings.PlacementFingerprint;
            string second = MapVegetationForestFloorBindings.PlacementFingerprint;

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Has.Length.EqualTo(64));
            Assert.That(first.All(character =>
                character >= '0' && character <= '9' ||
                character >= 'a' && character <= 'f'), Is.True);
        }

        [Test]
        public void CanonicalRockInstance_MapsWorldAnchorAndKeepsLegacyCollisionAuthority()
        {
            GameObject parent = new GameObject("canonical-rock-test-root");
            GameObject instance = null;
            try
            {
                var anchor = new MapVegetationRockAnchor
                {
                    Id = "canonical-rock:test",
                    Position = new Vector3(10f, 2f, 20f),
                    SourceSize = new Vector3(3f, 2f, 4f),
                    Yaw = 37f
                };
                Matrix4x4 mapping = Matrix4x4.TRS(
                    new Vector3(120f, 7f, -45f),
                    Quaternion.Euler(0f, 25f, 0f),
                    new Vector3(-1.5f, 1.25f, 0.75f));
                MapVegetationGlobalPresentation.CanonicalRockPlacement
                    placement = MapVegetationGlobalPresentation
                        .MapCanonicalRockAnchor(anchor, mapping);
                Vector3 expectedWorldPosition = mapping.MultiplyPoint3x4(
                    anchor.Position);
                Assert.That(Vector3.Distance(placement.Position,
                    expectedWorldPosition), Is.LessThan(0.0001f),
                    "Canonical anchors must use the same optional donor-to-world correction as vegetation placements.");
                Vector3 expectedForward = mapping.MultiplyVector(
                    Quaternion.Euler(0f, anchor.Yaw, 0f) * Vector3.forward);
                expectedForward.y = 0f;
                Vector3 mappedForward = Quaternion.Euler(0f, placement.Yaw,
                    0f) * Vector3.forward;
                Assert.That(Vector3.Dot(expectedForward.normalized,
                    mappedForward), Is.GreaterThan(0.9999f),
                    "A mirrored/translated mapping must preserve the mapped principal direction.");

                instance = MapVegetationForestFloorBindings
                    .InstantiateCanonicalRock(anchor.Id, 20260831,
                        parent.transform, placement.Position,
                        placement.SourceSize, placement.Yaw);
                Assert.That(instance.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty);
                Assert.That(instance.GetComponentsInChildren<Collider>(true),
                    Is.Empty,
                    "ALP visual must not duplicate the exact legacy MAP/MESH/ROCKS collider.");
                Assert.That(instance.name, Is.EqualTo("canonical-rock:test"));
                CanonicalRockReplacementAnchor saved = instance
                    .GetComponent<CanonicalRockReplacementAnchor>();
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved.StableId, Is.EqualTo(anchor.Id));
                Assert.That(Vector3.Distance(saved.WorldBottomCenter,
                    placement.Position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(saved.MappedSourceSize,
                    placement.SourceSize), Is.LessThan(0.0001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                    saved.PrincipalYawDegrees, placement.Yaw)),
                    Is.LessThan(0.0001f));
                Assert.That(instance.transform.localScale,
                    Is.EqualTo(Vector3.one * saved.AuthoredUniformScale));
                Assert.That(MapVegetationGlobalPresentation.TryRendererBounds(
                    instance, out Bounds rendered), Is.True);
                Vector3 bottomCentre = new Vector3(rendered.center.x,
                    rendered.min.y, rendered.center.z);
                Assert.That(Vector3.Distance(bottomCentre,
                    expectedWorldPosition), Is.LessThanOrEqualTo(0.03f),
                    "The renderer bottom-centre must stay on the mapped canonical world anchor after pivot correction.");
            }
            finally
            {
                if (parent != null) Object.DestroyImmediate(parent);
            }
        }
    }
}
