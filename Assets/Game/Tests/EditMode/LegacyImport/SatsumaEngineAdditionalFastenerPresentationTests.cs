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
using Rules = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineAdditionalFastenerPresentation;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineAdditionalFastenerPresentationTests
    {
        private const string GeneratedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private DonorUnitySceneModel scene;

        [OneTimeSetUp]
        public void LoadFrozenDonorEvidence()
        {
            const string config = "Config/DonorPaths.local.json";
            if (!File.Exists(config)) return;
            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile(config);
            string path = Path.Combine(paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity");
            Assert.That(File.Exists(path), Is.True, "Configured donor evidence must not silently skip.");
            scene = DonorUnitySceneModel.Parse(path);
        }

        [OneTimeTearDown]
        public void ReleaseFrozenDonorEvidence() => scene = null;

        [Test]
        public void TableContainsSeventyOneBoltsFourSlottedScrewsAndTwentyFiveRealNuts()
        {
            Assert.That(Rules.ReviewedBindings, Has.Count.EqualTo(100));
            Assert.That(Rules.ReviewedBindings.Count(value => value.MeshSourceGuid == Rules.Nut), Is.EqualTo(25));
            Assert.That(Rules.ReviewedBindings.Count(value => value.MeshSourceGuid == Rules.Slotted), Is.EqualTo(4));
            Assert.That(Rules.ReviewedBindings.Count(value => value.MeshSourceGuid == Rules.Short || value.MeshSourceGuid == Rules.Long), Is.EqualTo(71));
            Assert.That(Rules.ReviewedBindings.Select(value => value.FastenerId).Distinct().Count(), Is.EqualTo(100));
            Assert.That(Rules.ReviewedBindings.Select(value => value.MarkerTransformId).Distinct().Count(), Is.EqualTo(100));
            Assert.That(Rules.ReviewedBindings.Where(value => value.MountId == "mount.satsuma.cylinder-head.rocker-shaft")
                .Select(value => value.FastenerId.Split('.').Last()),
                Is.EqualTo(new[] { "boltpm-2", "boltpm-5", "boltpm-6", "boltpm-8", "boltpm-9" }));
            Assert.That(Rules.ReviewedBindings.Count(value => value.MountId == "mount.satsuma.cylinder-head.rocker-cover"), Is.EqualTo(12),
                "Presentation-only work must not silently delete six saved IDs.");
            Assert.That(Rules.ReviewedBindings.Select(value => value.FastenerId).Intersect(
                Phase1SatsumaEngineFastenerPresentation.ReviewedBindings.Select(value => value.FastenerId)), Is.Empty);
        }

        [Test]
        public void EveryReviewedMarkerMatchesFrozenMeshMaterialAndChildFrame()
        {
            if (scene == null) Assert.Ignore("Private donor paths are not configured.");
            foreach (Rules.Binding binding in Rules.ReviewedBindings)
            {
                Assert.DoesNotThrow(() => Rules.ValidateDonorBinding(scene, binding), binding.FastenerId);
            }
        }

        [Test]
        public void SourceGuardRejectsIncorrectShortBoltForWaterPumpNut()
        {
            if (scene == null) Assert.Ignore("Private donor paths are not configured.");
            var wrong = new Rules.Binding("timing-cover", "water-pump", 1, 46855, 7, Rules.Short,
                Vector3.one * 0.7f, Vector3.zero);
            Assert.Throws<InvalidDataException>(() => Rules.ValidateDonorBinding(scene, wrong));
        }

        [Test]
        public void GeneratedTargetsMatchAllReviewedDonorMeshKinds()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                foreach (Rules.Binding binding in Rules.ResolveActiveBindings(root))
                {
                    Assert.That(Presentation(targets[binding.FastenerId]).GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(Mesh(binding.MeshSourceGuid)), binding.FastenerId);
                }
                Assert.That(Rules.ApplyReviewedMeshes(root, GeneratedRoot), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ScopedRefreshChangesOnlySurvivingMeshReferencesAndIsIdempotent()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                IReadOnlyList<Rules.Binding> active = Rules.ResolveActiveBindings(root);
                int expectedChanges = active.Count(value => value.MeshSourceGuid != Rules.Nut);
                Assert.That(active.Count, Is.EqualTo(93));
                Assert.That(expectedChanges, Is.EqualTo(68));
                foreach (Rules.Binding binding in active)
                {
                    Presentation(targets[binding.FastenerId]).GetComponent<MeshFilter>().sharedMesh = Mesh(Rules.Nut);
                }
                Dictionary<UnityEngine.Object, string> protectedState = CaptureProtectedState(root);
                Dictionary<MeshFilter, Mesh> beforeMeshes = root.GetComponentsInChildren<MeshFilter>(true)
                    .ToDictionary(value => value, value => value.sharedMesh);
                Assert.That(Rules.ApplyReviewedMeshes(root, GeneratedRoot), Is.EqualTo(expectedChanges));
                AssertProtectedState(protectedState);
                Dictionary<MeshFilter, string> reviewed = active.ToDictionary(
                    value => Presentation(targets[value.FastenerId]).GetComponent<MeshFilter>(), value => value.MeshSourceGuid);
                Assert.That(beforeMeshes.Count(pair => pair.Key.sharedMesh != pair.Value), Is.EqualTo(expectedChanges));
                foreach (KeyValuePair<MeshFilter, Mesh> pair in beforeMeshes)
                {
                    Assert.That(pair.Key.sharedMesh, Is.SameAs(reviewed.TryGetValue(pair.Key, out string guid) ? Mesh(guid) : pair.Value));
                }
                Assert.That(Rules.ApplyReviewedMeshes(root, GeneratedRoot), Is.Zero);
                AssertProtectedState(protectedState);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase("mesh")]
        [TestCase("owner")]
        [TestCase("material")]
        [TestCase("duplicate")]
        [TestCase("presentation")]
        public void InvalidLastBindingFailsBeforeFirstMeshMutation(string drift)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                AssemblyFastenerInteractionTarget first = targets[Rules.ReviewedBindings[0].FastenerId];
                AssemblyFastenerInteractionTarget last = targets[Rules.ReviewedBindings.Last().FastenerId];
                MeshFilter firstMesh = Presentation(first).GetComponent<MeshFilter>();
                firstMesh.sharedMesh = Mesh(Rules.Nut);
                switch (drift)
                {
                    case "mesh": Presentation(last).GetComponent<MeshFilter>().sharedMesh = Mesh(Rules.Long); break;
                    case "owner": SetReference(last, "controller", null); break;
                    case "material": Presentation(last).GetComponent<MeshRenderer>().sharedMaterial = null; break;
                    case "presentation": SetReference(last, "fastenerPresentation", Presentation(first)); break;
                    case "duplicate":
                        var serialized = new SerializedObject(first);
                        serialized.FindProperty("fastenerDefinitionId").stringValue = last.FastenerDefinitionId;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        break;
                }
                Dictionary<MeshFilter, Mesh> before = root.GetComponentsInChildren<MeshFilter>(true)
                    .ToDictionary(value => value, value => value.sharedMesh);
                Assert.Throws<InvalidDataException>(() => Rules.ApplyReviewedMeshes(root, GeneratedRoot));
                foreach (KeyValuePair<MeshFilter, Mesh> pair in before) Assert.That(pair.Key.sharedMesh, Is.SameAs(pair.Value));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Dictionary<UnityEngine.Object, string> CaptureProtectedState(GameObject root)
        {
            var result = new Dictionary<UnityEngine.Object, string>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (!(component is MeshFilter)) result.Add(component, EditorJsonUtility.ToJson(component));
            }
            foreach (MountPointAuthoring mount in root.GetComponent<VehicleAssemblyController>().MountPoints)
            {
                result.TryAdd(mount.Definition, EditorJsonUtility.ToJson(mount.Definition));
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
                    result.TryAdd(fastener, EditorJsonUtility.ToJson(fastener));
            }
            return result;
        }

        private static void AssertProtectedState(Dictionary<UnityEngine.Object, string> snapshot)
        {
            foreach (KeyValuePair<UnityEngine.Object, string> pair in snapshot)
                Assert.That(EditorJsonUtility.ToJson(pair.Key), Is.EqualTo(pair.Value), pair.Key.name);
        }

        private static Dictionary<string, AssemblyFastenerInteractionTarget> Targets(GameObject root) => root
            .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
            .ToDictionary(value => value.FastenerDefinitionId, StringComparer.Ordinal);

        private static Transform Presentation(AssemblyFastenerInteractionTarget target) =>
            new SerializedObject(target).FindProperty("fastenerPresentation").objectReferenceValue as Transform;

        private static Mesh Mesh(string guid)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedRoot + "/Meshes/" + guid + ".asset");
            Assert.That(mesh, Is.Not.Null, guid);
            return mesh;
        }

        private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
