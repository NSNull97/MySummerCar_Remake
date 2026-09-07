using System;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class SatsumaEngineFastenerPresentationTests
    {
        private const string ConfigurationPath = "Config/DonorPaths.local.json";
        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string DonorMaterialSourceGuid =
            "98697bae08a8c114ba9774c487f2658d";

        private static readonly FastenerCase[] Cases = CreateCases();
        private DonorUnitySceneModel donorScene;

        [OneTimeSetUp]
        public void LoadFrozenDonorEvidenceOnceWhenConfigured()
        {
            if (!File.Exists(ConfigurationPath))
            {
                return;
            }

            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile(
                ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/" +
                "ExportedProject/Assets/_Scenes/GAME.unity");
            Assert.That(File.Exists(scenePath), Is.True,
                "Configured frozen GAME.unity is missing; E2a donor evidence " +
                "must fail rather than silently skip a configured source.");
            donorScene = DonorUnitySceneModel.Parse(scenePath);
        }

        [OneTimeTearDown]
        public void ReleaseFrozenDonorEvidence()
        {
            donorScene = null;
        }

        [Test]
        public void ReviewedTableMatchesTheExactSixteenOwnedDonorMarkers()
        {
            Phase1SatsumaEngineFastenerPresentation.ReviewedFastenerBinding[]
                actual = Phase1SatsumaEngineFastenerPresentation
                    .ReviewedBindings.ToArray();

            Assert.That(actual, Has.Length.EqualTo(16));
            for (int index = 0; index < Cases.Length; index++)
            {
                FastenerCase expected = Cases[index];
                Phase1SatsumaEngineFastenerPresentation.ReviewedFastenerBinding
                    binding = actual[index];
                Assert.That(binding.MountId, Is.EqualTo(expected.MountId),
                    expected.FastenerId);
                Assert.That(binding.FastenerId, Is.EqualTo(expected.FastenerId),
                    expected.FastenerId);
                Assert.That(binding.DonorMarkerTransformId,
                    Is.EqualTo(expected.MarkerTransformId), expected.FastenerId);
                Assert.That(binding.ExpectedMeshSourceGuid,
                    Is.EqualTo(expected.MeshSourceGuid), expected.FastenerId);
                Assert.That(binding.ExpectedSize, Is.EqualTo(expected.Size),
                    expected.FastenerId);
                Assert.That(Vector3.Distance(
                        binding.ExpectedPresentationScale,
                        expected.PresentationScale),
                    Is.LessThan(0.00001f), expected.FastenerId);
                Assert.That(binding.ExpectedColliderRadius,
                    Is.EqualTo(expected.ColliderRadius).Within(0.000001f),
                    expected.FastenerId);
                Assert.That(Vector3.Distance(
                        binding.ExpectedDonorRendererChildLocalPosition,
                        expected.RendererLocalPosition),
                    Is.LessThan(0.000001f), expected.FastenerId);
            }

            Assert.That(actual.Select(value => value.FastenerId).Distinct(
                    StringComparer.Ordinal).Count(),
                Is.EqualTo(16));
            Assert.That(actual.Select(value => value.DonorMarkerTransformId)
                    .Distinct().Count(),
                Is.EqualTo(16));
        }

        [Test]
        public void FrozenMarkersOwnTheExpectedMeshMaterialAndRendererFrame()
        {
            if (donorScene == null)
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }

            foreach (FastenerCase value in Cases)
            {
                DonorStaticRendererRecord source = donorScene
                    .GetStaticRenderersBelowIncludingInactive(
                        value.MarkerTransformId)
                    .Single();
                Assert.That(source.MeshGuid, Is.EqualTo(value.MeshSourceGuid),
                    value.FastenerId);
                Assert.That(source.MaterialGuids,
                    Is.EqualTo(new[] { DonorMaterialSourceGuid }),
                    value.FastenerId);
                long rendererTransformId = donorScene
                    .GetTransformIdForGameObject(source.GameObjectId);
                DonorTransformRecord rendererFrame = donorScene.GetTransform(
                    rendererTransformId);
                Assert.That(rendererFrame.FatherTransformId,
                    Is.EqualTo(value.MarkerTransformId), value.FastenerId);
                Assert.That(Vector3.Distance(
                        rendererFrame.LocalPosition,
                        value.RendererLocalPosition),
                    Is.LessThan(0.000001f), value.FastenerId);
                Assert.That(rendererFrame.LocalRotation,
                    Is.EqualTo(Quaternion.identity), value.FastenerId);
                Assert.That(rendererFrame.LocalScale,
                    Is.EqualTo(Vector3.one),
                    value.FastenerId +
                    " must use the reviewed identity child renderer frame.");
                Assert.That(Vector3.Distance(
                        donorScene.GetTransform(value.MarkerTransformId).LocalScale,
                        value.PresentationScale),
                    Is.LessThan(0.00001f), value.FastenerId);
            }
        }

        [Test]
        public void GeneratedPrefabUsesReviewedMeshesAndRetainsFastenerContracts()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController assembly = contents
                    .GetComponents<VehicleAssemblyController>().Single();
                Dictionary<string, MountPointAuthoring> mounts = assembly
                    .MountPoints.ToDictionary(value => value.MountId,
                        StringComparer.Ordinal);
                Dictionary<string, AssemblyFastenerInteractionTarget> targets =
                    contents.GetComponentsInChildren<
                            AssemblyFastenerInteractionTarget>(true)
                        .ToDictionary(value => value.FastenerDefinitionId,
                            StringComparer.Ordinal);
                Material expectedMaterial = RequireAsset<Material>(
                    GeneratedRoot + "/Materials/" + DonorMaterialSourceGuid +
                    ".mat");

                SatsumaCanonicalNightTestShape.AssertCanonical(assembly);
                foreach (FastenerCase value in Cases)
                {
                    MountPointAuthoring mount = mounts[value.MountId];
                    AssemblyFastenerInteractionTarget target =
                        targets[value.FastenerId];
                    Assert.That(target.Controller, Is.SameAs(assembly),
                        value.FastenerId);
                    Assert.That(target.MountId, Is.EqualTo(value.MountId),
                        value.FastenerId);
                    Assert.That(target.transform.name,
                        Is.EqualTo(value.FastenerId), value.FastenerId);
                    Assert.That(target.transform.parent,
                        Is.SameAs(mount.transform), value.FastenerId);

                    Transform presentation = ReadPresentation(target);
                    Assert.That(presentation.parent,
                        Is.SameAs(target.transform), value.FastenerId);
                    Assert.That(presentation.localPosition,
                        Is.EqualTo(Vector3.zero), value.FastenerId);
                    Assert.That(presentation.localRotation,
                        Is.EqualTo(Quaternion.identity), value.FastenerId);
                    Assert.That(Vector3.Distance(
                            presentation.localScale,
                            value.PresentationScale),
                        Is.LessThan(0.00001f), value.FastenerId);

                    MeshFilter filter = presentation.GetComponent<MeshFilter>();
                    MeshRenderer renderer = presentation
                        .GetComponent<MeshRenderer>();
                    Assert.That(filter, Is.Not.Null, value.FastenerId);
                    Assert.That(renderer, Is.Not.Null, value.FastenerId);
                    Assert.That(filter.sharedMesh, Is.SameAs(RequireAsset<Mesh>(
                        MeshPath(value.MeshSourceGuid))), value.FastenerId);
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1),
                        value.FastenerId);
                    Assert.That(renderer.sharedMaterial,
                        Is.SameAs(expectedMaterial), value.FastenerId);

                    SphereCollider collider = target.GetComponent<SphereCollider>();
                    Assert.That(collider, Is.Not.Null, value.FastenerId);
                    Assert.That(collider.isTrigger, Is.True, value.FastenerId);
                    Assert.That(collider.enabled, Is.False, value.FastenerId);
                    Assert.That(collider.center, Is.EqualTo(Vector3.zero),
                        value.FastenerId);
                    Assert.That(collider.radius,
                        Is.EqualTo(value.ColliderRadius).Within(0.000001f),
                        value.FastenerId);
                    AssertTargetBasePose(target, value.FastenerId);
                }

                AssertMountContract(
                    mounts[Phase1SatsumaEngineFastenerPresentation.OilpanMountId],
                    Cases.Where(value => value.MountId ==
                        Phase1SatsumaEngineFastenerPresentation.OilpanMountId)
                        .ToArray(),
                    expectedAggregateTightness: 72);
                AssertMountContract(
                    mounts[Phase1SatsumaEngineFastenerPresentation.GearboxMountId],
                    Cases.Where(value => value.MountId ==
                        Phase1SatsumaEngineFastenerPresentation.GearboxMountId)
                        .ToArray(),
                    expectedAggregateTightness: 56);

                Assert.That(mounts[
                        Phase1SatsumaEngineFastenerPresentation.OilpanMountId]
                        .Definition.Fasteners.Count(value =>
                            value.Size == FastenerSize.Millimeter7),
                    Is.EqualTo(8));
                Assert.That(mounts[
                        Phase1SatsumaEngineFastenerPresentation.OilpanMountId]
                        .Definition.Fasteners.Count(value =>
                            value.Size == FastenerSize.Millimeter13),
                    Is.EqualTo(1));
                Assert.That(mounts[
                        Phase1SatsumaEngineFastenerPresentation.GearboxMountId]
                        .Definition.Fasteners.Count(value =>
                            value.Size == FastenerSize.Millimeter7),
                    Is.EqualTo(6));
                Assert.That(mounts[
                        Phase1SatsumaEngineFastenerPresentation.GearboxMountId]
                        .Definition.Fasteners.Count(value =>
                            value.Size == FastenerSize.Millimeter10),
                    Is.EqualTo(1));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void ScopedApplyChangesOnlySixteenMeshReferencesAndIsIdempotent()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                Mesh oldNut = RequireAsset<Mesh>(MeshPath(
                    Phase1SatsumaEngineFastenerPresentation
                        .DefaultNutMeshSourceGuid));
                Dictionary<string, AssemblyFastenerInteractionTarget> targets =
                    ReviewedTargets(contents);
                foreach (FastenerCase value in Cases)
                {
                    ReadPresentation(targets[value.FastenerId])
                        .GetComponent<MeshFilter>().sharedMesh = oldNut;
                }

                PrefabPayloadSnapshot before = CapturePrefabPayload(contents);
                int changed = Phase1SatsumaEngineFastenerPresentation
                    .ApplyReviewedMeshes(contents, GeneratedRoot);
                PrefabPayloadSnapshot after = CapturePrefabPayload(contents);

                Assert.That(changed, Is.EqualTo(16));
                AssertNonMeshPayloadEqual(before, after);
                var expectedPathsByKey = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                foreach (FastenerCase value in Cases)
                {
                    MeshFilter filter = ReadPresentation(
                            targets[value.FastenerId])
                        .GetComponent<MeshFilter>();
                    expectedPathsByKey.Add(
                        ComponentKey(contents.transform, filter),
                        MeshPath(value.MeshSourceGuid));
                }

                int changedReferences = 0;
                foreach (KeyValuePair<string, string> entry in before.MeshPaths)
                {
                    Assert.That(after.MeshPaths.ContainsKey(entry.Key), Is.True,
                        entry.Key);
                    if (expectedPathsByKey.TryGetValue(
                            entry.Key,
                            out string expectedPath))
                    {
                        Assert.That(entry.Value,
                            Is.EqualTo(MeshPath(
                                Phase1SatsumaEngineFastenerPresentation
                                    .DefaultNutMeshSourceGuid)),
                            entry.Key);
                        Assert.That(after.MeshPaths[entry.Key],
                            Is.EqualTo(expectedPath), entry.Key);
                        changedReferences++;
                    }
                    else
                    {
                        Assert.That(after.MeshPaths[entry.Key],
                            Is.EqualTo(entry.Value), entry.Key);
                    }
                }

                Assert.That(changedReferences, Is.EqualTo(16));
                Assert.That(Phase1SatsumaEngineFastenerPresentation
                        .ApplyReviewedMeshes(contents, GeneratedRoot),
                    Is.Zero);
                AssertPayloadEqual(after, CapturePrefabPayload(contents));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [TestCase(PreflightDrift.WrongMesh)]
        [TestCase(PreflightDrift.WrongMaterial)]
        [TestCase(PreflightDrift.DuplicateTargetId)]
        [TestCase(PreflightDrift.MissingTarget)]
        [TestCase(PreflightDrift.WrongOwner)]
        [TestCase(PreflightDrift.WrongPresentationReference)]
        [TestCase(PreflightDrift.WrongPresentationPose)]
        [TestCase(PreflightDrift.NonFiniteTravel)]
        public void InvalidCanonicalShapeFailsBeforeAnyMeshMutation(
            PreflightDrift drift)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                Dictionary<string, AssemblyFastenerInteractionTarget> targets =
                    ReviewedTargets(contents);
                AssemblyFastenerInteractionTarget first =
                    targets[Cases[0].FastenerId];
                AssemblyFastenerInteractionTarget last =
                    targets[Cases[Cases.Length - 1].FastenerId];
                ReadPresentation(first).GetComponent<MeshFilter>().sharedMesh =
                    RequireAsset<Mesh>(MeshPath(
                        Phase1SatsumaEngineFastenerPresentation
                            .DefaultNutMeshSourceGuid));
                ApplyDrift(contents, targets, first, last, drift);
                IReadOnlyDictionary<string, string> before =
                    CaptureMutationSurface(contents, targets);

                InvalidDataException error = Assert.Throws<InvalidDataException>(
                    () => Phase1SatsumaEngineFastenerPresentation
                        .ApplyReviewedMeshes(contents, GeneratedRoot));
                Assert.That(error.Message, Does.StartWith("E2a"));
                AssertStringDictionariesEqual(
                    before,
                    CaptureMutationSurface(contents, targets),
                    drift.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ApplyDrift(
            GameObject root,
            IReadOnlyDictionary<string, AssemblyFastenerInteractionTarget> targets,
            AssemblyFastenerInteractionTarget first,
            AssemblyFastenerInteractionTarget last,
            PreflightDrift drift)
        {
            switch (drift)
            {
                case PreflightDrift.WrongMesh:
                    ReadPresentation(last).GetComponent<MeshFilter>().sharedMesh =
                        RequireAsset<Mesh>(MeshPath(
                            Phase1SatsumaEngineFastenerPresentation
                                .ShortBoltMeshSourceGuid));
                    break;
                case PreflightDrift.WrongMaterial:
                    ReadPresentation(last).GetComponent<MeshRenderer>()
                        .sharedMaterial = null;
                    break;
                case PreflightDrift.DuplicateTargetId:
                    AssemblyFastenerInteractionTarget unreviewed = root
                        .GetComponentsInChildren<
                            AssemblyFastenerInteractionTarget>(true)
                        .First(value => !targets.ContainsKey(
                            value.FastenerDefinitionId));
                    SetString(unreviewed, "fastenerDefinitionId",
                        last.FastenerDefinitionId);
                    break;
                case PreflightDrift.MissingTarget:
                    SetString(last, "fastenerDefinitionId",
                        "fastener.satsuma.e2a.missing");
                    break;
                case PreflightDrift.WrongOwner:
                    SetObjectReference(last, "controller", null);
                    break;
                case PreflightDrift.WrongPresentationReference:
                    SetObjectReference(last, "fastenerPresentation",
                        ReadPresentation(first));
                    break;
                case PreflightDrift.WrongPresentationPose:
                    ReadPresentation(last).localPosition =
                        new Vector3(0.001f, 0f, 0f);
                    break;
                case PreflightDrift.NonFiniteTravel:
                    SetFloat(last, "fastenerPresentationStageTravelScale",
                        float.NaN);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(drift), drift,
                        null);
            }
        }

        private static void AssertMountContract(
            MountPointAuthoring mount,
            IReadOnlyList<FastenerCase> cases,
            int expectedAggregateTightness)
        {
            Assert.That(mount.Definition.Fasteners, Has.Length.EqualTo(cases.Count),
                mount.MountId);
            Assert.That(mount.Definition.Fasteners.Select(value =>
                    value.DefinitionId),
                Is.EqualTo(cases.Select(value => value.FastenerId)),
                mount.MountId);
            Assert.That(mount.Definition.Fasteners.Select(value => value.Size),
                Is.EqualTo(cases.Select(value => value.Size)), mount.MountId);
            Assert.That(mount.Definition.Fasteners.Select(value =>
                    value.MaximumStage),
                Is.All.EqualTo(8), mount.MountId);

            FastenerGroupDefinition group = mount.Definition.FastenerGroup;
            Assert.That(group, Is.Not.Null, mount.MountId);
            Assert.That(group.FastenerDefinitionIds,
                Is.EqualTo(cases.Select(value => value.FastenerId)),
                mount.MountId);
            Assert.That(group.AggregateMaximumTightness,
                Is.EqualTo(expectedAggregateTightness), mount.MountId);
            Assert.That(group.BoltedOnThreshold, Is.EqualTo(1), mount.MountId);
            Assert.That(group.BoltedOffThreshold, Is.Zero, mount.MountId);
        }

        private static void AssertTargetBasePose(
            AssemblyFastenerInteractionTarget target,
            string fastenerId)
        {
            var serialized = new SerializedObject(target);
            serialized.Update();
            Assert.That(serialized.FindProperty(
                    "fastenerPresentationBaseLocalPosition").vector3Value,
                Is.EqualTo(Vector3.zero), fastenerId);
            Assert.That(serialized.FindProperty(
                    "fastenerPresentationBaseLocalRotation").quaternionValue,
                Is.EqualTo(Quaternion.identity), fastenerId);
            Assert.That(Phase1SatsumaEngineFastenerTravel.IsLegacyOrReviewedScale(fastenerId,
                serialized.FindProperty("fastenerPresentationStageTravelScale").floatValue), Is.True, fastenerId);
        }

        private static Transform ReadPresentation(
            AssemblyFastenerInteractionTarget target)
        {
            var serialized = new SerializedObject(target);
            serialized.Update();
            Transform result = serialized.FindProperty("fastenerPresentation")
                .objectReferenceValue as Transform;
            Assert.That(result, Is.Not.Null, target.FastenerDefinitionId);
            return result;
        }

        private static void SetString(
            UnityEngine.Object owner,
            string propertyName,
            string value)
        {
            var serialized = new SerializedObject(owner);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(
            UnityEngine.Object owner,
            string propertyName,
            float value)
        {
            var serialized = new SerializedObject(owner);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, AssemblyFastenerInteractionTarget>
            ReviewedTargets(GameObject root)
        {
            HashSet<string> reviewedIds = Cases.Select(value => value.FastenerId)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, AssemblyFastenerInteractionTarget> result = root
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(value => reviewedIds.Contains(value.FastenerDefinitionId))
                .ToDictionary(value => value.FastenerDefinitionId,
                    StringComparer.Ordinal);
            Assert.That(result, Has.Count.EqualTo(16));
            return result;
        }

        private static PrefabPayloadSnapshot CapturePrefabPayload(GameObject root)
        {
            VehicleAssemblyController assembly = root
                .GetComponents<VehicleAssemblyController>().Single();
            return new PrefabPayloadSnapshot(
                CaptureHierarchyFrames(root),
                CaptureComponentJson(root),
                CaptureMaterialPaths(root),
                CaptureColliderJson(root),
                CaptureTargetJson(root),
                CaptureMountDefinitionJson(assembly),
                CaptureFastenerDefinitionJson(assembly),
                CaptureMeshPaths(root));
        }

        private static IReadOnlyDictionary<string, string> CaptureMutationSurface(
            GameObject root,
            IReadOnlyDictionary<string, AssemblyFastenerInteractionTarget> targets)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in CaptureMeshPaths(root))
            {
                result.Add("mesh|" + entry.Key, entry.Value);
            }

            foreach (FastenerCase value in Cases)
            {
                AssemblyFastenerInteractionTarget target =
                    targets[value.FastenerId];
                result.Add("target|" + value.FastenerId,
                    EditorJsonUtility.ToJson(target));
                result.Add("collider|" + value.FastenerId,
                    EditorJsonUtility.ToJson(
                        target.GetComponent<SphereCollider>()));
                Transform presentation = target.transform.GetChild(0);
                result.Add("presentation|" + value.FastenerId,
                    EditorJsonUtility.ToJson(presentation));
                result.Add("renderer|" + value.FastenerId,
                    EditorJsonUtility.ToJson(
                        presentation.GetComponent<MeshRenderer>()));
            }

            return result;
        }

        private static Dictionary<string, string> CaptureHierarchyFrames(
            GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(
                value => HierarchyKey(root.transform, value),
                value =>
                    $"active={value.gameObject.activeSelf};" +
                    $"layer={value.gameObject.layer};" +
                    $"static={value.gameObject.isStatic};" +
                    $"pos={Format(value.localPosition)};" +
                    $"rot={Format(value.localRotation)};" +
                    $"scale={Format(value.localScale)}",
                StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureComponentJson(
            GameObject root)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(
                         true))
            {
                foreach (Component component in transform.GetComponents<Component>())
                {
                    Assert.That(component, Is.Not.Null,
                        HierarchyKey(root.transform, transform));
                    if (component is MeshFilter)
                    {
                        continue;
                    }

                    result.Add(ComponentKey(root.transform, component),
                        EditorJsonUtility.ToJson(component));
                }
            }

            return result;
        }

        private static Dictionary<string, string> CaptureMaterialPaths(
            GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(true).ToDictionary(
                value => ComponentKey(root.transform, value),
                value => string.Join("|", value.sharedMaterials.Select(material =>
                    material != null ? AssetDatabase.GetAssetPath(material) :
                    "<null>")),
                StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureColliderJson(
            GameObject root)
        {
            return root.GetComponentsInChildren<Collider>(true).ToDictionary(
                value => ComponentKey(root.transform, value),
                value => EditorJsonUtility.ToJson(value),
                StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureTargetJson(
            GameObject root)
        {
            return root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(
                    true)
                .ToDictionary(
                    value => ComponentKey(root.transform, value),
                    value => EditorJsonUtility.ToJson(value),
                    StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureMountDefinitionJson(
            VehicleAssemblyController assembly)
        {
            return assembly.MountPoints.ToDictionary(
                value => value.MountId,
                value => EditorJsonUtility.ToJson(value.Definition),
                StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureFastenerDefinitionJson(
            VehicleAssemblyController assembly)
        {
            return assembly.MountPoints.SelectMany(value =>
                    value.Definition.Fasteners)
                .ToDictionary(
                    value => value.DefinitionId,
                    value => EditorJsonUtility.ToJson(value),
                    StringComparer.Ordinal);
        }

        private static Dictionary<string, string> CaptureMeshPaths(GameObject root)
        {
            return root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(
                value => ComponentKey(root.transform, value),
                value => value.sharedMesh != null
                    ? AssetDatabase.GetAssetPath(value.sharedMesh)
                    : "<null>",
                StringComparer.Ordinal);
        }

        private static void AssertNonMeshPayloadEqual(
            PrefabPayloadSnapshot expected,
            PrefabPayloadSnapshot actual)
        {
            AssertStringDictionariesEqual(expected.HierarchyFrames,
                actual.HierarchyFrames, "hierarchy/TRS");
            AssertStringDictionariesEqual(expected.ComponentJson,
                actual.ComponentJson, "serialized components");
            AssertStringDictionariesEqual(expected.MaterialPaths,
                actual.MaterialPaths, "materials");
            AssertStringDictionariesEqual(expected.ColliderJson,
                actual.ColliderJson, "colliders");
            AssertStringDictionariesEqual(expected.TargetJson,
                actual.TargetJson, "target base poses");
            AssertStringDictionariesEqual(expected.MountDefinitionJson,
                actual.MountDefinitionJson, "mount definitions");
            AssertStringDictionariesEqual(expected.FastenerDefinitionJson,
                actual.FastenerDefinitionJson, "fastener definitions");
        }

        private static void AssertPayloadEqual(
            PrefabPayloadSnapshot expected,
            PrefabPayloadSnapshot actual)
        {
            AssertNonMeshPayloadEqual(expected, actual);
            AssertStringDictionariesEqual(expected.MeshPaths,
                actual.MeshPaths, "mesh references");
        }

        private static void AssertStringDictionariesEqual(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual,
            string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), label);
            foreach (KeyValuePair<string, string> entry in expected)
            {
                Assert.That(actual.ContainsKey(entry.Key), Is.True,
                    label + ": " + entry.Key);
                Assert.That(actual[entry.Key], Is.EqualTo(entry.Value),
                    label + ": " + entry.Key);
            }
        }

        private static string ComponentKey(Transform root, Component component)
        {
            Component[] sameType = component.gameObject.GetComponents(
                component.GetType());
            int ordinal = Array.IndexOf(sameType, component);
            return HierarchyKey(root, component.transform) + "|" +
                component.GetType().AssemblyQualifiedName + "#" + ordinal;
        }

        private static string HierarchyKey(Transform root, Transform value)
        {
            var segments = new Stack<string>();
            Transform current = value;
            while (current != null)
            {
                segments.Push(current.GetSiblingIndex() + ":" + current.name);
                if (current == root)
                {
                    return string.Join("/", segments);
                }

                current = current.parent;
            }

            throw new InvalidOperationException(
                "Transform is outside the captured prefab root.");
        }

        private static string Format(Vector3 value) =>
            Format(value.x) + "," + Format(value.y) + "," + Format(value.z);

        private static string Format(Quaternion value) =>
            Format(value.x) + "," + Format(value.y) + "," + Format(value.z) +
            "," + Format(value.w);

        private static string Format(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T result = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(result, Is.Not.Null, path);
            return result;
        }

        private static string MeshPath(string sourceGuid) =>
            GeneratedRoot + "/Meshes/" + sourceGuid + ".asset";

        private static FastenerCase[] CreateCases()
        {
            var result = new List<FastenerCase>();
            long[] oilpanMarkers =
            {
                40758L, 44950L, 53276L, 54874L, 56771L,
                56810L, 58044L, 58670L, 61811L,
            };
            for (int index = 0; index < oilpanMarkers.Length; index++)
            {
                bool drainPlug = index == 2;
                result.Add(new FastenerCase(
                    Phase1SatsumaEngineFastenerPresentation.OilpanMountId,
                    "fastener.satsuma.engine-block-oilpan.boltpm-" +
                    (index + 1),
                    oilpanMarkers[index],
                    Phase1SatsumaEngineFastenerPresentation
                        .ShortBoltMeshSourceGuid,
                    drainPlug ? FastenerSize.Millimeter13 :
                        FastenerSize.Millimeter7,
                    drainPlug ? new Vector3(1.3f, 1.3f, 0.8f) :
                        new Vector3(0.7f, 0.7f, 0.7f),
                    drainPlug ? 0.03375f : 0.028f,
                    new Vector3(0f, 0f, -0.02f)));
            }

            long[] gearboxMarkers =
            {
                36907L, 38331L, 42660L, 44837L, 51824L, 67087L, 68100L,
            };
            for (int index = 0; index < gearboxMarkers.Length; index++)
            {
                bool shortTenMillimeterBolt = index == 4;
                result.Add(new FastenerCase(
                    Phase1SatsumaEngineFastenerPresentation.GearboxMountId,
                    "fastener.satsuma.engine-block-gearbox.boltpm-" +
                    (index + 1),
                    gearboxMarkers[index],
                    shortTenMillimeterBolt
                        ? Phase1SatsumaEngineFastenerPresentation
                            .ShortBoltMeshSourceGuid
                        : Phase1SatsumaEngineFastenerPresentation
                            .LongBoltMeshSourceGuid,
                    shortTenMillimeterBolt ? FastenerSize.Millimeter10 :
                        FastenerSize.Millimeter7,
                    shortTenMillimeterBolt
                        ? new Vector3(1f, 1f, 0.8f)
                        : new Vector3(0.7f, 0.7f, 0.7f),
                    0.028f,
                    Vector3.zero));
            }

            return result.ToArray();
        }

        public enum PreflightDrift
        {
            WrongMesh,
            WrongMaterial,
            DuplicateTargetId,
            MissingTarget,
            WrongOwner,
            WrongPresentationReference,
            WrongPresentationPose,
            NonFiniteTravel,
        }

        private readonly struct FastenerCase
        {
            public FastenerCase(
                string mountId,
                string fastenerId,
                long markerTransformId,
                string meshSourceGuid,
                FastenerSize size,
                Vector3 presentationScale,
                float colliderRadius,
                Vector3 rendererLocalPosition)
            {
                MountId = mountId;
                FastenerId = fastenerId;
                MarkerTransformId = markerTransformId;
                MeshSourceGuid = meshSourceGuid;
                Size = size;
                PresentationScale = presentationScale;
                ColliderRadius = colliderRadius;
                RendererLocalPosition = rendererLocalPosition;
            }

            public string MountId { get; }
            public string FastenerId { get; }
            public long MarkerTransformId { get; }
            public string MeshSourceGuid { get; }
            public FastenerSize Size { get; }
            public Vector3 PresentationScale { get; }
            public float ColliderRadius { get; }
            public Vector3 RendererLocalPosition { get; }
        }

        private sealed class PrefabPayloadSnapshot
        {
            public PrefabPayloadSnapshot(
                IReadOnlyDictionary<string, string> hierarchyFrames,
                IReadOnlyDictionary<string, string> componentJson,
                IReadOnlyDictionary<string, string> materialPaths,
                IReadOnlyDictionary<string, string> colliderJson,
                IReadOnlyDictionary<string, string> targetJson,
                IReadOnlyDictionary<string, string> mountDefinitionJson,
                IReadOnlyDictionary<string, string> fastenerDefinitionJson,
                IReadOnlyDictionary<string, string> meshPaths)
            {
                HierarchyFrames = hierarchyFrames;
                ComponentJson = componentJson;
                MaterialPaths = materialPaths;
                ColliderJson = colliderJson;
                TargetJson = targetJson;
                MountDefinitionJson = mountDefinitionJson;
                FastenerDefinitionJson = fastenerDefinitionJson;
                MeshPaths = meshPaths;
            }

            public IReadOnlyDictionary<string, string> HierarchyFrames { get; }
            public IReadOnlyDictionary<string, string> ComponentJson { get; }
            public IReadOnlyDictionary<string, string> MaterialPaths { get; }
            public IReadOnlyDictionary<string, string> ColliderJson { get; }
            public IReadOnlyDictionary<string, string> TargetJson { get; }
            public IReadOnlyDictionary<string, string> MountDefinitionJson { get; }
            public IReadOnlyDictionary<string, string> FastenerDefinitionJson { get; }
            public IReadOnlyDictionary<string, string> MeshPaths { get; }
        }
    }
}
