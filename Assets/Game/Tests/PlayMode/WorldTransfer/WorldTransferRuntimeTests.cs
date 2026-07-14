using MSC.World.Debugging;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.PlayMode.WorldTransfer
{
    public sealed class WorldTransferRuntimeTests
    {
        [Test]
        public void LandmarkRegistry_ResolvesByStableIdAndName()
        {
            var owner = new GameObject("LandmarkRegistryTest");
            try
            {
                WorldLandmarkRegistry registry = owner.AddComponent<WorldLandmarkRegistry>();
                registry.Configure(new[] { new WorldLandmarkEntry("fb0f962be1b325cc19296c66751818c0", "PrimaryHomeGarage", Vector3.zero) });
                Assert.That(registry.TryResolve("PrimaryHomeGarage", out WorldLandmarkEntry byName), Is.True);
                Assert.That(byName.Position, Is.EqualTo(Vector3.zero));
                Assert.That(registry.TryResolve("fb0f962be1b325cc19296c66751818c0", out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DevelopmentFlyCamera_CanBeSpawnedWithoutPlayerDependency()
        {
            var owner = new GameObject("WorldTransferFlyCameraTest");
            try
            {
                owner.AddComponent<Camera>();
                Assert.That(owner.AddComponent<WorldTransferDebugFlyCamera>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CellLoader_ImplementsStreamingBoundaryAndStartsIdle()
        {
            var owner = new GameObject("WorldReferenceCellLoaderTest");
            try
            {
                WorldReferenceCellLoader loader = owner.AddComponent<WorldReferenceCellLoader>();
                loader.Configure(owner.transform, 512f, 1, 2, new[] { new WorldReferenceCellScene(0, 0, "World_cell_0_0") });
                Assert.That(loader, Is.InstanceOf<MSC.World.IWorldStreamingService>());
                Assert.That(loader.IsStreaming, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ReferenceEntity_GarageFixtureAppearsAtExpectedOrigin()
        {
            var owner = new GameObject("garage_shed_roof");
            try
            {
                owner.transform.position = Vector3.zero;
                WorldReferenceEntity entity = owner.AddComponent<WorldReferenceEntity>();
                entity.Configure("fb0f962be1b325cc19296c66751818c0", 1064, "CABIN/Shed/garage_shed_roof", "Roof", "DonorReference", "ExtractedBoundsNeedsReview");
                Assert.That(entity.StableId, Is.EqualTo("fb0f962be1b325cc19296c66751818c0"));
                Assert.That(Vector3.Distance(owner.transform.position, Vector3.zero), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
