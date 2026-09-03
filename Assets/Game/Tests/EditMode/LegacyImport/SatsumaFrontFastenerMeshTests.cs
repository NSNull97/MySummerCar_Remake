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
    public sealed class SatsumaFrontFastenerMeshTests
    {
        private const string NutMeshGuid = "e711c8a15b1135c4089caad19b8f56e8";
        private const string BoltMeshGuid = "aec6c756751308a4d830708366ad5cdb";
        private const string LongBoltMeshGuid = "bd64aade39680ac43a380f1c62373e0b";
        private const string GeneratedMeshRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Meshes/";

        private static readonly MeshCase[] Cases = CreateCases();
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
        public void FrozenFrontFastenersUseSixNutsTwentyBoltsAndFourLongBolts()
        {
            Assert.That(Cases.Length, Is.EqualTo(30));
            Assert.That(Cases.Count(value => value.MeshGuid == NutMeshGuid), Is.EqualTo(6));
            Assert.That(Cases.Count(value => value.MeshGuid == BoltMeshGuid), Is.EqualTo(20));
            Assert.That(Cases.Count(value => value.MeshGuid == LongBoltMeshGuid), Is.EqualTo(4));
            foreach (MeshCase value in Cases)
            {
                DonorStaticRendererRecord source = scene
                    .GetStaticRenderersBelowIncludingInactive(value.MarkerTransformId)
                    .Single();
                Assert.That(source.MeshGuid, Is.EqualTo(value.MeshGuid), value.FastenerId);
                Assert.That(Phase1SatsumaBaselineBuilder.ReadReviewedFrontFastenerMeshGuid(
                    scene, value.MarkerTransformId, value.MeshGuid),
                    Is.EqualTo(source.MeshGuid), value.FastenerId);
            }
        }

        [Test]
        public void GeneratedFrontFastenerMeshesMatchEachFrozenMarker()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Dictionary<string, AssemblyFastenerInteractionTarget> targets = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .ToDictionary(value => value.FastenerDefinitionId, StringComparer.Ordinal);
            Assert.That(targets.Count, Is.EqualTo(280), "Presentation must not add or remove fasteners.");
            foreach (MeshCase value in Cases)
            {
                Assert.That(targets.ContainsKey(value.FastenerId), Is.True, value.FastenerId);
                Mesh expected = AssetDatabase.LoadAssetAtPath<Mesh>(
                    GeneratedMeshRoot + value.MeshGuid + ".asset");
                Assert.That(expected, Is.Not.Null, value.MeshGuid);
                MeshFilter visual = targets[value.FastenerId]
                    .GetComponentsInChildren<MeshFilter>(true).Single();
                Assert.That(visual.sharedMesh, Is.SameAs(expected), value.FastenerId);
                Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero), value.FastenerId);
                Assert.That(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f), value.FastenerId);
                Assert.That(Vector3.Distance(visual.transform.localScale,
                    scene.GetTransform(value.MarkerTransformId).LocalScale),
                    Is.LessThan(0.00001f), value.FastenerId);
            }
        }

        [Test]
        public void FastenersOutsideReviewedFrontAndRearRetainTheirExistingMesh()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var reviewedIds = new HashSet<string>(Cases.Select(value => value.FastenerId),
                StringComparer.Ordinal);
            reviewedIds.UnionWith(new[]
            {
                "fastener.satsuma.trail-arm-rl.boltpm-1",
                "fastener.satsuma.trail-arm-rl.boltpm-2",
                "fastener.satsuma.trail-arm-rr.boltpm-1",
                "fastener.satsuma.trail-arm-rr.boltpm-2",
                "fastener.satsuma.shock-rl.boltpm-1",
                "fastener.satsuma.shock-rl.boltpm-2",
                "fastener.satsuma.shock-rl.boltpm-3",
                "fastener.satsuma.shock-rr.boltpm-1",
                "fastener.satsuma.shock-rr.boltpm-2",
                "fastener.satsuma.shock-rr.boltpm-3",
                "fastener.satsuma.drum-brake-rl.boltpm",
                "fastener.satsuma.drum-brake-rr.boltpm",
                "fastener.satsuma.steering-column.boltpm-1",
                "fastener.satsuma.steering-column.boltpm-2",
            });
            foreach ((string mountSlug, int count) in new[]
                     {
                         ("bumper-front", 2),
                         ("bumper-rear", 2),
                         ("fender-left", 5),
                         ("fender-right", 5),
                         ("grille", 2),
                         ("hood", 4),
                         ("bootlid", 4),
                         ("door-left", 4),
                         ("door-right", 4),
                     })
            {
                for (int index = 1; index <= count; index++)
                {
                    reviewedIds.Add("fastener.satsuma." + mountSlug +
                        ".boltpm-" + index);
                }
            }
            AssemblyFastenerInteractionTarget[] unreviewed = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(value => !reviewedIds.Contains(value.FastenerDefinitionId)).ToArray();
            Assert.That(unreviewed.Length, Is.EqualTo(204));
            Mesh legacyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                GeneratedMeshRoot + NutMeshGuid + ".asset");
            Assert.That(legacyMesh, Is.Not.Null);
            foreach (AssemblyFastenerInteractionTarget target in unreviewed)
            {
                MeshFilter[] visuals = target.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(visuals, Has.Length.EqualTo(1), target.FastenerDefinitionId);
                Assert.That(visuals[0].sharedMesh,
                    Is.SameAs(legacyMesh), target.FastenerDefinitionId);
            }
        }

        [TestCase(68946L, NutMeshGuid)]
        [TestCase(47815L, BoltMeshGuid)]
        [TestCase(59140L, BoltMeshGuid)]
        public void SourceGuardRejectsWrongReviewedMeshKind(long markerId, string wrongMeshGuid)
        {
            Assert.Throws<InvalidDataException>(() =>
                Phase1SatsumaBaselineBuilder.ReadReviewedFrontFastenerMeshGuid(
                    scene, markerId, wrongMeshGuid));
        }

        private static MeshCase[] CreateCases()
        {
            var result = new List<MeshCase>();
            AddGroup(result, "sub-frame", "boltpm-", BoltMeshGuid,
                48657L, 67681L, 68239L, 69913L);
            AddGroup(result, "steering-rack", "boltpm-", BoltMeshGuid,
                52063L, 58500L, 62435L, 62595L);
            AddGroup(result, "wishbone-fl", "boltpm-", LongBoltMeshGuid, 59140L, 65219L);
            AddGroup(result, "wishbone-fr", "boltpm-", LongBoltMeshGuid, 56492L, 64309L);
            AddGroup(result, "spindle-fl", "boltpm-", BoltMeshGuid, 68709L);
            AddGroup(result, "spindle-fr", "boltpm-", BoltMeshGuid, 61842L);
            AddGroup(result, "strut-fl", "boltpm-", NutMeshGuid, 47815L, 53776L, 61168L);
            AddGroup(result, "strut-fr", "boltpm-", NutMeshGuid, 41902L, 42496L, 50660L);
            AddGroup(result, "strut-fl", "lower-", BoltMeshGuid, 39160L, 49488L, 64879L, 65179L);
            AddGroup(result, "strut-fr", "lower-", BoltMeshGuid, 38251L, 51736L, 65041L, 70471L);
            result.Add(new MeshCase("fastener.satsuma.steering-rod-fl.outer-joint", 68946L, BoltMeshGuid));
            result.Add(new MeshCase("fastener.satsuma.steering-rod-fr.outer-joint", 70485L, BoltMeshGuid));
            return result.ToArray();
        }

        private static void AddGroup(List<MeshCase> cases, string partId,
            string suffix, string meshGuid, params long[] markerIds)
        {
            for (int index = 0; index < markerIds.Length; index++)
            {
                cases.Add(new MeshCase("fastener.satsuma." + partId + "." + suffix +
                    (index + 1), markerIds[index], meshGuid));
            }
        }

        private readonly struct MeshCase
        {
            public MeshCase(string fastenerId, long markerTransformId, string meshGuid)
            {
                FastenerId = fastenerId;
                MarkerTransformId = markerTransformId;
                MeshGuid = meshGuid;
            }

            public string FastenerId { get; }
            public long MarkerTransformId { get; }
            public string MeshGuid { get; }
        }
    }
}
