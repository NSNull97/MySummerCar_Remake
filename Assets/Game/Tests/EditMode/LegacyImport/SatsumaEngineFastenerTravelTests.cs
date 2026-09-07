using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rules = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineFastenerTravel;
using Builder = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineFastenerTravelTests
    {
        private IReadOnlyDictionary<long, Rules.SourceEvidence> source;

        [OneTimeSetUp]
        public void ReadOnlyFrozenSourceAudit()
        {
            if (File.Exists("Config/DonorPaths.local.json")) source = Rules.ValidateFrozenSource();
        }

        [Test]
        public void CohortContains115StageBasedMarkersAndExcludesTheMixtureAdjuster()
        {
            Assert.That(Rules.ReviewedBindings, Has.Count.EqualTo(115));
            Assert.That(Rules.ReviewedBindings.Select(value => value.FastenerId).Distinct().Count(), Is.EqualTo(115));
            Assert.That(Rules.ReviewedBindings.Any(value => value.MarkerTransformId == 52523), Is.False);
            Assert.That(Rules.ReviewedBindings.Count(value => value.TravelScale != 1f), Is.EqualTo(113));
            Assert.That(Rules.ReviewedBindings.Single(value => value.MarkerTransformId == 53276).TravelScale, Is.EqualTo(0.8f));
            Assert.That(Rules.ReviewedBindings.Single(value => value.MarkerTransformId == 38244).TravelScale, Is.EqualTo(0.5f));
            Assert.That(Rules.ReviewedBindings.Single(value => value.MarkerTransformId == 53477).TravelScale, Is.EqualTo(0.6f));
            Assert.That(Rules.ReviewedBindings.Single(value => value.MarkerTransformId == 50893).TravelScale, Is.EqualTo(1.1f));
            Assert.That(Rules.ReviewedBindings.Single(value => value.MarkerTransformId == 55496).TravelScale, Is.Zero);
        }

        [Test]
        public void EveryDonorStageUsesTheSameTranslationAxisDespiteMarkerRotationOwnership()
        {
            if (source == null) Assert.Ignore("Private donor paths are not configured.");
            Assert.That(source, Has.Count.EqualTo(115));
            foreach (Rules.Binding binding in Rules.ReviewedBindings)
            {
                Rules.SourceEvidence evidence = source[binding.MarkerTransformId];
                Assert.That(evidence.ScrewComponentId, Is.GreaterThan(0), binding.FastenerId);
                Vector3 restEuler = evidence.MarkerRotation.eulerAngles;
                foreach (int stage in new[] { 0, 1, 8, 7, 0 })
                {
                    Quaternion donorRotation = Quaternion.Euler(restEuler.x, restEuler.y, stage * 45f);
                    Vector3 donorDisplacement = donorRotation *
                        (Vector3.back * (Rules.LocalStageTravelMeters * stage *
                            (binding.MarkerTransformId == 55496 ? 0f : evidence.MarkerScale.z)));
                    Vector3 projectDisplacement = evidence.MarkerRotation *
                        (Vector3.back * (Rules.LocalStageTravelMeters * stage * binding.TravelScale));
                    Assert.That(Vector3.Distance(donorDisplacement, projectDisplacement), Is.LessThan(0.00001f),
                        binding.FastenerId + " stage=" + stage);
                }
            }
        }

        [Test]
        public void RefreshChangesOnlyReviewedScalarsDryRunIsPureAndSecondApplyIsZero()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
            try
            {
                IReadOnlyList<Rules.Binding> active = Rules.ResolveActiveBindings(root);
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                foreach (Rules.Binding binding in active) SetTravel(targets[binding.FastenerId], 1f);
                Dictionary<UnityEngine.Object, string> before = CaptureProtectedState(root);
                Dictionary<string, float> travels = targets.ToDictionary(pair => pair.Key, pair => pair.Value.FastenerPresentationStageTravelScale);
                int expected = active.Count(value => value.TravelScale != 1f);
                Assert.That(expected, Is.EqualTo(active.Count == 115 ? 113 : 107));
                Assert.That(Rules.ApplyReviewedTravel(root, dryRun: true), Is.EqualTo(expected));
                foreach (KeyValuePair<string, float> pair in travels)
                    Assert.That(targets[pair.Key].FastenerPresentationStageTravelScale, Is.EqualTo(pair.Value));
                AssertProtectedState(before);
                Assert.That(Rules.ApplyReviewedTravel(root), Is.EqualTo(expected));
                var byId = active.ToDictionary(value => value.FastenerId);
                foreach (KeyValuePair<string, AssemblyFastenerInteractionTarget> pair in targets)
                    Assert.That(pair.Value.FastenerPresentationStageTravelScale,
                        Is.EqualTo(byId.TryGetValue(pair.Key, out Rules.Binding binding) ? binding.TravelScale : travels[pair.Key]), pair.Key);
                AssertProtectedState(before);
                Assert.That(Rules.ApplyReviewedTravel(root), Is.Zero);
                AssertProtectedState(before);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ActualPresentationFormulaMatchesCorrectedDisplacementAtStagesAndReversals()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
            try
            {
                Rules.ApplyReviewedTravel(root);
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                MethodInfo present = typeof(AssemblyFastenerInteractionTarget).GetMethod("ApplyFastenerPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(present, Is.Not.Null);
                foreach (Rules.Binding binding in Rules.ResolveActiveBindings(root))
                {
                    AssemblyFastenerInteractionTarget target = targets[binding.FastenerId];
                    Transform presentation = Presentation(target);
                    Vector3 authoredScale = presentation.localScale;
                    // Imported hierarchy decomposition has the same 1e-5 scale
                    // tolerance as the reviewed authorer. Stage animation must
                    // preserve that actual authored scale exactly.
                    Assert.That(Vector3.Distance(authoredScale, binding.PresentationScale),
                        Is.LessThan(0.00001f), binding.FastenerId);
                    foreach (int stage in new[] { 0, 1, 8, 7, 0 })
                    {
                        present.Invoke(target, new object[] { (float)stage });
                        Vector3 expected = Vector3.back * (Rules.LocalStageTravelMeters * stage * binding.TravelScale);
                        Assert.That(Vector3.Distance(presentation.localPosition, expected), Is.LessThan(0.000001f), binding.FastenerId);
                        Assert.That(Quaternion.Angle(presentation.localRotation, Quaternion.AngleAxis(stage * 45f, Vector3.forward)), Is.LessThan(0.001f));
                        Assert.That(presentation.localScale, Is.EqualTo(authoredScale), binding.FastenerId);
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase("travel")]
        [TestCase("axis")]
        [TestCase("base")]
        [TestCase("scale")]
        [TestCase("owner")]
        [TestCase("duplicate")]
        public void ChangedLastTargetFailsBeforeAnyEarlierScalarWrite(string drift)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                IReadOnlyList<Rules.Binding> active = Rules.ResolveActiveBindings(root);
                foreach (Rules.Binding binding in active) SetTravel(targets[binding.FastenerId], 1f);
                AssemblyFastenerInteractionTarget last = targets[active.Last().FastenerId];
                var serialized = new SerializedObject(last);
                switch (drift)
                {
                    case "travel": serialized.FindProperty(Rules.TravelProperty).floatValue = 0.123f; break;
                    case "axis": serialized.FindProperty("fastenerPresentationBaseLocalRotation").quaternionValue = Quaternion.Euler(90f, 0f, 0f); break;
                    case "base": serialized.FindProperty("fastenerPresentationBaseLocalPosition").vector3Value = Vector3.back * 0.02f; break;
                    case "scale": Presentation(last).localScale = Vector3.one * 2f; break;
                    case "owner": serialized.FindProperty("controller").objectReferenceValue = null; break;
                    case "duplicate": serialized.FindProperty("fastenerDefinitionId").stringValue = active[0].FastenerId; break;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Dictionary<AssemblyFastenerInteractionTarget, float> before = targets.Values.ToDictionary(value => value, value => value.FastenerPresentationStageTravelScale);
                Assert.Throws<InvalidDataException>(() => Rules.ApplyReviewedTravel(root));
                foreach (KeyValuePair<AssemblyFastenerInteractionTarget, float> pair in before)
                    Assert.That(pair.Key.FastenerPresentationStageTravelScale, Is.EqualTo(pair.Value));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void PartialRockerCoverRetirementIsRejectedWithoutChangingTravel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets = Targets(root);
                const string retained = "fastener.satsuma.cylinder-head-rocker-cover.boltpm-2";
                UnityEngine.Object.DestroyImmediate(targets[retained].gameObject);
                Dictionary<AssemblyFastenerInteractionTarget, float> before = root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .ToDictionary(value => value, value => value.FastenerPresentationStageTravelScale);
                Assert.Throws<InvalidDataException>(() => Rules.ApplyReviewedTravel(root));
                foreach (KeyValuePair<AssemblyFastenerInteractionTarget, float> pair in before)
                    Assert.That(pair.Key.FastenerPresentationStageTravelScale, Is.EqualTo(pair.Value));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void GeneratedActiveTargetsAlreadyUseReviewedTravel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
            try { Assert.That(Rules.ApplyReviewedTravel(root, dryRun: true), Is.Zero); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Dictionary<string, AssemblyFastenerInteractionTarget> Targets(GameObject root) => root
            .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).ToDictionary(value => value.FastenerDefinitionId, StringComparer.Ordinal);
        private static Transform Presentation(AssemblyFastenerInteractionTarget target) =>
            new SerializedObject(target).FindProperty("fastenerPresentation").objectReferenceValue as Transform;
        private static void SetTravel(AssemblyFastenerInteractionTarget target, float scale)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(Rules.TravelProperty).floatValue = scale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Dictionary<UnityEngine.Object, string> CaptureProtectedState(GameObject root)
        {
            var result = new Dictionary<UnityEngine.Object, string>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) result.Add(component, ProtectedJson(component));
            foreach (MountPointAuthoring mount in root.GetComponent<VehicleAssemblyController>().MountPoints)
            {
                result.TryAdd(mount.Definition, ProtectedJson(mount.Definition));
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners) result.TryAdd(fastener, ProtectedJson(fastener));
            }
            return result;
        }
        private static string ProtectedJson(UnityEngine.Object value)
        {
            string json = EditorJsonUtility.ToJson(value);
            return value is AssemblyFastenerInteractionTarget
                ? Regex.Replace(json, "\"" + Rules.TravelProperty + "\":[^,}]+", "\"" + Rules.TravelProperty + "\":0") : json;
        }
        private static void AssertProtectedState(Dictionary<UnityEngine.Object, string> before)
        {
            foreach (KeyValuePair<UnityEngine.Object, string> pair in before)
                Assert.That(ProtectedJson(pair.Key), Is.EqualTo(pair.Value), pair.Key.name);
        }
    }
}
