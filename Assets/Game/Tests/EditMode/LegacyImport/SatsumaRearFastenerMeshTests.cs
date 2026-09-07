using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaRearFastenerMeshTests
    {
        private const string NutMeshGuid = "e711c8a15b1135c4089caad19b8f56e8";
        private const string BoltMeshGuid = "aec6c756751308a4d830708366ad5cdb";
        private const string LongBoltMeshGuid = "bd64aade39680ac43a380f1c62373e0b";
        private const string GeneratedMeshRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Meshes/";

        private static readonly MeshCase[] Cases =
        {
            new MeshCase("fastener.satsuma.trail-arm-rl.boltpm-1",
                63892L, LongBoltMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.trail-arm-rl.boltpm-2",
                71971L, LongBoltMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.trail-arm-rr.boltpm-1",
                39901L, LongBoltMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.trail-arm-rr.boltpm-2",
                70647L, LongBoltMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.shock-rl.boltpm-1",
                51042L, BoltMeshGuid, FastenerSize.Millimeter6),
            new MeshCase("fastener.satsuma.shock-rl.boltpm-2",
                55634L, BoltMeshGuid, FastenerSize.Millimeter6),
            new MeshCase("fastener.satsuma.shock-rl.boltpm-3",
                58968L, NutMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.shock-rr.boltpm-1",
                51409L, NutMeshGuid, FastenerSize.Millimeter12),
            new MeshCase("fastener.satsuma.shock-rr.boltpm-2",
                53482L, BoltMeshGuid, FastenerSize.Millimeter6),
            new MeshCase("fastener.satsuma.shock-rr.boltpm-3",
                56872L, BoltMeshGuid, FastenerSize.Millimeter6),
            new MeshCase("fastener.satsuma.drum-brake-rl.boltpm",
                48747L, BoltMeshGuid, FastenerSize.Millimeter14),
            new MeshCase("fastener.satsuma.drum-brake-rr.boltpm",
                63595L, BoltMeshGuid, FastenerSize.Millimeter14),
        };

        private DonorUnitySceneModel scene;

        [OneTimeSetUp]
        public void LoadFrozenDonorEvidence()
        {
            const string configurationPath = "Config/DonorPaths.local.json";
            if (!File.Exists(configurationPath))
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }

            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile(
                configurationPath);
            scene = DonorUnitySceneModel.Parse(Path.Combine(
                paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/" +
                "ExportedProject/Assets/_Scenes/GAME.unity"));
        }

        [Test]
        public void FrozenRearFastenersUseTwoNutsSixBoltsAndFourLongBolts()
        {
            Assert.That(Cases, Has.Length.EqualTo(12));
            Assert.That(Cases.Count(value => value.MeshGuid == NutMeshGuid),
                Is.EqualTo(2));
            Assert.That(Cases.Count(value => value.MeshGuid == BoltMeshGuid),
                Is.EqualTo(6));
            Assert.That(Cases.Count(value => value.MeshGuid == LongBoltMeshGuid),
                Is.EqualTo(4));

            foreach (MeshCase value in Cases)
            {
                DonorStaticRendererRecord source = scene
                    .GetStaticRenderersBelowIncludingInactive(
                        value.MarkerTransformId)
                    .Single();
                Assert.That(source.MeshGuid, Is.EqualTo(value.MeshGuid),
                    value.FastenerId);
                Assert.That(
                    Phase1SatsumaBaselineBuilder
                        .ReadReviewedRearSuspensionFastenerMeshGuid(
                            scene,
                            value.MarkerTransformId,
                            value.MeshGuid),
                    Is.EqualTo(source.MeshGuid),
                    value.FastenerId);
            }
        }

        [Test]
        public void GeneratedRearFastenersMatchFrozenMeshAndWrenchKinds()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            Dictionary<string, AssemblyFastenerInteractionTarget> targets = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .ToDictionary(value => value.FastenerDefinitionId,
                    StringComparer.Ordinal);
            SatsumaCanonicalNightTestShape.AssertCanonical(assembly);

            foreach (MeshCase value in Cases)
            {
                Assert.That(targets.ContainsKey(value.FastenerId), Is.True,
                    value.FastenerId);
                Mesh expected = AssetDatabase.LoadAssetAtPath<Mesh>(
                    GeneratedMeshRoot + value.MeshGuid + ".asset");
                Assert.That(expected, Is.Not.Null, value.MeshGuid);
                MeshFilter visual = targets[value.FastenerId]
                    .GetComponentsInChildren<MeshFilter>(true)
                    .Single();
                Assert.That(visual.sharedMesh, Is.SameAs(expected),
                    value.FastenerId);
                Assert.That(visual.transform.localPosition,
                    Is.EqualTo(Vector3.zero), value.FastenerId);
                Assert.That(Quaternion.Angle(
                        visual.transform.localRotation,
                        Quaternion.identity),
                    Is.LessThan(0.001f), value.FastenerId);
                Assert.That(Vector3.Distance(
                        visual.transform.localScale,
                        scene.GetTransform(value.MarkerTransformId).LocalScale),
                    Is.LessThan(0.00001f), value.FastenerId);

                FastenerDefinition definition = assembly.MountPoints
                    .SelectMany(mount => mount.Definition.Fasteners)
                    .Single(fastener => fastener.DefinitionId == value.FastenerId);
                Assert.That(definition.Size, Is.EqualTo(value.Size),
                    value.FastenerId);
            }
        }

        [Test]
        public void RearSpringsHaveNoFastenersAndWheelLugsRemainNuts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();

            string[] springMountIds =
            {
                "mount.satsuma.coilspring-rl",
                "mount.satsuma.coilspring-rr",
                "mount.satsuma.long-coilspring-rl",
                "mount.satsuma.long-coilspring-rr",
            };
            foreach (string mountId in springMountIds)
            {
                Assert.That(assembly.MountPoints.Single(value =>
                        value.MountId == mountId).Definition.Fasteners,
                    Is.Empty, mountId);
            }

            Mesh nut = AssetDatabase.LoadAssetAtPath<Mesh>(
                GeneratedMeshRoot + NutMeshGuid + ".asset");
            foreach (string corner in new[] { "rl", "rr" })
            {
                AssemblyFastenerInteractionTarget[] lugs = prefab
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Where(value => value.MountId ==
                        "mount.satsuma.wheel" + corner + "-new")
                    .ToArray();
                Assert.That(lugs, Has.Length.EqualTo(4), corner);
                Assert.That(lugs.Select(value => value
                        .GetComponentsInChildren<MeshFilter>(true)
                        .Single().sharedMesh),
                    Is.All.SameAs(nut), corner);
            }
        }

        [TestCase(63892L, NutMeshGuid)]
        [TestCase(58968L, BoltMeshGuid)]
        [TestCase(48747L, NutMeshGuid)]
        public void SourceGuardRejectsWrongReviewedRearMeshKind(
            long markerId,
            string wrongMeshGuid)
        {
            Assert.Throws<InvalidDataException>(() =>
                Phase1SatsumaBaselineBuilder
                    .ReadReviewedRearSuspensionFastenerMeshGuid(
                        scene,
                        markerId,
                        wrongMeshGuid));
        }

        private readonly struct MeshCase
        {
            public MeshCase(
                string fastenerId,
                long markerTransformId,
                string meshGuid,
                FastenerSize size)
            {
                FastenerId = fastenerId;
                MarkerTransformId = markerTransformId;
                MeshGuid = meshGuid;
                Size = size;
            }

            public string FastenerId { get; }
            public long MarkerTransformId { get; }
            public string MeshGuid { get; }
            public FastenerSize Size { get; }
        }
    }
}
