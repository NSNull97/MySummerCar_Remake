using System;
using System.Linq;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode
{
    public sealed class VehiclePaintStateControllerTests
    {
        private GameObject owner;
        private VehiclePaintStateController controller;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Vehicle Paint Migration Test");
            controller = owner.AddComponent<VehiclePaintStateController>();
            controller.Configure(
                SatsumaPaintSurfaceIds.All
                    .Select(surfaceId => new VehiclePaintSurfaceBinding(
                        surfaceId,
                        null,
                        Array.Empty<int>()))
                    .ToArray());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }

        [Test]
        public void Restore_MigratesKnownFiveSurfaceGlobalPaintToStableSurfaces()
        {
            Color savedColor = new Color32(3, 38, 69, 255);
            VehiclePaintSaveDto save = CreateLegacyGlobalPaintSave(
                paletteIndex: 6,
                savedColor,
                VehiclePaintType.NoChange);

            Assert.That(
                controller.TryRestore(save, out string failure),
                Is.True,
                failure);
            Assert.That(controller.PaletteIndex, Is.EqualTo(6));
            Assert.That(controller.BodyColor, Is.EqualTo(savedColor));
            Assert.That(controller.HasPlayerSelectedPaint, Is.True);

            VehiclePaintSaveDto migrated = controller.CaptureSaveData();
            Assert.That(migrated.surfaces, Has.Length.EqualTo(7));
            CollectionAssert.AreEquivalent(
                SatsumaPaintSurfaceIds.All,
                migrated.surfaces.Select(surface => surface.surfaceId));
            Assert.That(
                migrated.surfaces.All(surface =>
                    surface.paletteIndex == 6 &&
                    surface.bodyColor == savedColor &&
                    surface.paintType == (int)VehiclePaintType.NoChange),
                Is.True);
        }

        [Test]
        public void Restore_RejectsIndependentLegacyPaintWithoutPartialMutation()
        {
            Color savedColor = new Color32(3, 38, 69, 255);
            VehiclePaintSaveDto save = CreateLegacyGlobalPaintSave(
                paletteIndex: 6,
                savedColor,
                VehiclePaintType.NoChange);
            save.surfaces[3].bodyColor = Color.red;

            Assert.That(
                controller.TryRestore(save, out string failure),
                Is.False);
            StringAssert.Contains("cannot be migrated safely", failure);
            Assert.That(controller.HasPlayerSelectedPaint, Is.False);
            Assert.That(controller.PaletteIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Restore_RejectsIncompleteLegacySurfaceIdentitySet()
        {
            Color savedColor = new Color32(3, 38, 69, 255);
            VehiclePaintSaveDto save = CreateLegacyGlobalPaintSave(
                paletteIndex: 6,
                savedColor,
                VehiclePaintType.NoChange);
            save.surfaces[4].surfaceId = "legacy-surface-7";

            Assert.That(
                controller.TryRestore(save, out string failure),
                Is.False);
            StringAssert.Contains("identities are incomplete", failure);
            Assert.That(controller.HasPlayerSelectedPaint, Is.False);
        }

        private static VehiclePaintSaveDto CreateLegacyGlobalPaintSave(
            int paletteIndex,
            Color bodyColor,
            VehiclePaintType paintType) =>
            new()
            {
                paletteIndex = paletteIndex,
                bodyColor = bodyColor,
                paintType = (int)paintType,
                surfaces = Enumerable.Range(0, 5)
                    .Select(index => new VehiclePaintSurfaceSaveDto
                    {
                        surfaceId = $"legacy-surface-{index}",
                        paletteIndex = paletteIndex,
                        bodyColor = bodyColor,
                        paintType = (int)paintType,
                    })
                    .ToArray(),
            };
    }
}
