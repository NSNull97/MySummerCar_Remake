using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaFuelLineConnectionAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaFuelLineConnectionTests
    {
        [Test]
        public void FixedNutPreservesSevenMountingBoltsAndRepeatIsZero()
        {
            using var f = new Fixture();
            string definitionBefore = EditorJsonUtility.ToJson(f.Mount.Definition);
            PartInstance[] parts = f.Assembly.Parts;
            MountPointAuthoring[] mounts = f.Assembly.MountPoints;
            Assert.That(f.Apply(), Is.EqualTo(3));
            Assert.That(f.Apply(), Is.Zero);
            Assert.That(f.Assembly.Parts, Is.SameAs(parts));
            Assert.That(f.Assembly.MountPoints, Is.SameAs(mounts));
            Assert.That(EditorJsonUtility.ToJson(f.Mount.Definition), Is.EqualTo(definitionBefore));
            Assert.That(f.Mount.Definition.Fasteners.Length, Is.EqualTo(7));
            Assert.That(f.Root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true), Is.Empty);
            Assert.That(f.Original.gameObject.activeSelf, Is.False);
            Assert.That(f.State.Stage, Is.Zero);
            Assert.That(f.State.IsBolted, Is.False);
            Assert.That(f.Binding.FuelLineConnection, Is.SameAs(f.State));
            Assert.That(f.Target.GetComponent<InteractionTargetHost>().OutlineRenderers.Single(), Is.SameAs(f.Target.FastenerRenderer));
        }

        [Test]
        public void RealTurnsUseEightZeroStickyLatchAndOnlyTwelveMillimeterWrench()
        {
            using var f = new Fixture(); f.Apply(); f.Install();
            var context = new InteractionContext(f.Root, f.Target.transform.position, Vector3.forward);
            var wrench = new HeldTool("Wrench", "12");
            Assert.That(f.Target.CanActivateTool(context), Is.False);
            Assert.That(f.Target.CanActivateHeldTool(wrench, context), Is.False);
            foreach (var wrong in new[] { new HeldTool("Wrench", "11"), new HeldTool("Wrench", "13"), new HeldTool("Screwdriver", "12") })
                Assert.That(f.Target.TryActivateHeldTool(wrong, context, 1), Is.False);
            Assert.That(f.State.TryTurn(float.NaN), Is.False);
            Assert.That(f.State.TryTurn(float.PositiveInfinity), Is.False);
            Assert.That(f.State.TryTurn(0), Is.False);
            for (int stage = 1; stage <= 8; stage++)
            {
                Assert.That(f.Target.TryActivateHeldTool(wrench, context, 1), Is.True);
                Assert.That(f.State.Stage, Is.EqualTo(stage));
                Assert.That(f.State.IsBolted, Is.EqualTo(stage == 8));
            }
            Assert.That(f.Target.TryActivateHeldTool(wrench, context, 1), Is.False);
            for (int stage = 7; stage >= 0; stage--)
            {
                Assert.That(f.Target.TryActivateHeldTool(wrench, context, -1), Is.True);
                Assert.That(f.State.IsBolted, Is.EqualTo(stage != 0));
            }
            Assert.That(f.Target.TryActivateHeldTool(wrench, context, -1), Is.False);
            Time.timeScale = 0;
            Assert.That(f.Target.TryActivateHeldTool(wrench, context, 1), Is.False);
        }

        [Test]
        public void NineStageWorldPosesMatchOriginalNonuniformSourceFrameAndReenable()
        {
            using var f = new Fixture(); f.Apply(); f.Install();
            for (int stage = 0; stage <= 8; stage++)
            {
                Assert.That(f.State.TryRestore(new SatsumaFuelLineConnectionSaveDto
                    { stage = stage, isBolted = stage == 8 }, out string failure), Is.True, failure);
                f.Target.RefreshPresentation();
                Matrix4x4 original = f.Tank.transform.localToWorldMatrix *
                    Matrix4x4.TRS(Authoring.TankLocalPose.position, Authoring.TankLocalPose.rotation, Authoring.PhysicalScale) *
                    Matrix4x4.TRS(new Vector3(0, 0, -.0025f * stage), Quaternion.AngleAxis(45f * stage, Vector3.forward), Vector3.one);
                AssertMatrix(f.Target.Presentation.localToWorldMatrix, original);
                Assert.That(f.Target.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(f.Target.Presentation.localScale, Is.EqualTo(Authoring.PhysicalScale));
                var trigger = f.Target.GetComponent<SphereCollider>();
                Assert.That(trigger.transform, Is.SameAs(f.Target.transform));
                Assert.That(trigger.center, Is.EqualTo(Vector3.zero));
                Assert.That(trigger.radius, Is.EqualTo(.0144f).Within(.000001f));
                Assert.That(f.Target.Presentation.GetComponent<Collider>(), Is.Null,
                    "Donor99370 belongs to fixed marker9234/45288, not moving visible child11892/47938.");
                Vector3 fixedCenter = f.Tank.transform.TransformPoint(Authoring.TankLocalPose.position);
                Assert.That(Vector3.Distance(trigger.transform.TransformPoint(trigger.center), fixedCenter), Is.LessThan(.0002f));
                f.Target.gameObject.SetActive(false); f.Target.gameObject.SetActive(true);
                f.Target.RefreshPresentation();
                AssertMatrix(f.Target.Presentation.localToWorldMatrix, original);
                Assert.That(f.Apply(), Is.Zero, "A live intermediate stage must not be mistaken for a changed rest frame.");
            }
            Assert.That(f.Target.Presentation.localPosition.z, Is.EqualTo(-.016f).Within(.000001f));
        }

        [Test]
        public void TankRemovalHidesFittingWithoutBlockingOrResettingItsLatch()
        {
            using var f = new Fixture(); f.Apply();
            f.Target.RefreshPresentation();
            Assert.That(f.Target.FastenerRenderer.enabled, Is.False);
            f.Install();
            Assert.That(f.State.TryRestore(new SatsumaFuelLineConnectionSaveDto { stage = 7, isBolted = true }, out _), Is.True);
            f.Target.RefreshPresentation();
            Assert.That(f.Target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(f.Target.FastenerRenderer.enabled, Is.True);
            Assert.That(f.Assembly.TryRemove(f.Tank).Succeeded, Is.True, "The fitting is not a tank removal bolt.");
            f.Target.RefreshPresentation();
            Assert.That(f.Target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(f.Target.ResolveOutlineRenderer(), Is.Null);
            Assert.That(f.State.Stage, Is.EqualTo(7));
            Assert.That(f.State.IsBolted, Is.True);
            Assert.That(f.State.TryTurn(1), Is.False, "No off-car invisible fitting operation.");
            f.Install();
            Assert.That(f.State.Stage, Is.EqualTo(7));
            Assert.That(f.State.IsBolted, Is.True);
            Assert.That(f.Target.ResolveOutlineRenderer(), Is.Not.Null);
            Assert.That(f.Assembly.Graph.TryGetMount(SatsumaFuelLineConnection.TankMountId, out var mount), Is.True);
            Assert.That(mount.Fasteners.All(b => b.Stage == 0), Is.True, "Tank remount resets only its own seven bolts.");
        }

        [TestCase("pose")]
        [TestCase("missing-source")]
        [TestCase("duplicate-source")]
        [TestCase("missing-binding")]
        [TestCase("partial-target")]
        [TestCase("eighth-tank-bolt")]
        public void UnknownSourceOrOwnerRejectsBeforeCreatingOrHidingAnything(string corruption)
        {
            using var f = new Fixture();
            if (corruption == "pose") f.Original.transform.localPosition += Vector3.up * .01f;
            if (corruption == "missing-source") f.Original.sharedMesh = null;
            if (corruption == "duplicate-source") Object.Instantiate(f.Original.gameObject, f.Tank.transform);
            if (corruption == "missing-binding") Object.DestroyImmediate(f.Binding);
            if (corruption == "partial-target") f.Root.AddComponent<SatsumaFuelLineFastenerInteractionTarget>();
            if (corruption == "eighth-tank-bolt")
            {
                var data = new SerializedObject(f.Mount.Definition); var array = data.FindProperty("fasteners");
                array.arraySize = 8; array.GetArrayElementAtIndex(7).objectReferenceValue = f.Mount.Definition.Fasteners[0];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<InvalidDataException>(() => f.Apply());
            Assert.That(f.Root.GetComponent<SatsumaFuelLineConnection>(), Is.Null);
            Assert.That(f.Original.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CanonicalAuthoredFittingIsIdempotentAndLeaves294GraphUntouched()
        {
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            if (!File.Exists(path)) Assert.Ignore("Private canonical Satsuma not generated.");
            string before = File.ReadAllText(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Assert.That(root.GetComponent<SatsumaFuelLineConnection>(), Is.Not.Null, "Run the isolated fitting refresh before this integration gate.");
                string[] ids = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Select(t => t.FastenerDefinitionId).ToArray();
                Assert.That(ids.Length, Is.EqualTo(294));
                Assert.That(Authoring.ApplyToInstance(assembly), Is.Zero);
                Assert.That(assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Select(t => t.FastenerDefinitionId), Is.EqualTo(ids));
                Assert.That(assembly.Parts.Length, Is.EqualTo(126));
                Assert.That(assembly.MountPoints.Length, Is.EqualTo(124));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test]
        public void SerializedPrefabReloadPreservesExplicitStateOwnerRestPoseAndZeroRepeat()
        {
            string folder = "Assets/__FuelLineSerialization_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                using var f = new Fixture();
                f.PersistSources(folder);
                f.Apply();
                Assert.That(f.State.TryRestore(new SatsumaFuelLineConnectionSaveDto { stage = 7, isBolted = true }, out _), Is.True);
                f.Target.RefreshPresentation();
                string path = folder + "/FuelLineFixture.prefab";
                Assert.That(PrefabUtility.SaveAsPrefabAsset(f.Root, path), Is.Not.Null);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var assembly = loaded.GetComponent<VehicleAssemblyController>();
                    var connection = loaded.GetComponent<SatsumaFuelLineConnection>();
                    var target = loaded.GetComponentInChildren<SatsumaFuelLineFastenerInteractionTarget>(true);
                    Assert.That(connection.Assembly, Is.SameAs(assembly));
                    Assert.That(connection.Stage, Is.EqualTo(7));
                    Assert.That(connection.IsBolted, Is.True);
                    Assert.That(target.Connection, Is.SameAs(connection));
                    Assert.That(loaded.GetComponent<VehiclePersistenceBinding>().FuelLineConnection, Is.SameAs(connection));
                    Assert.That(Authoring.Configure(assembly,
                        AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Meshes/" + Authoring.NutMeshSourceGuid + ".asset"),
                        AssetDatabase.LoadAssetAtPath<Material>(folder + "/Materials/" + Authoring.MaterialSourceGuid + ".mat"), 8), Is.Zero);
                    target.gameObject.SetActive(false); target.gameObject.SetActive(true); target.RefreshPresentation();
                    Assert.That(target.Presentation.localPosition.z, Is.EqualTo(-.014f).Within(.000001f));
                    Assert.That(Quaternion.Angle(target.transform.localRotation, Authoring.TankLocalPose.rotation), Is.LessThan(.01f));
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        private static void AssertMatrix(Matrix4x4 actual, Matrix4x4 expected)
        {
            for (int i = 0; i < 16; i++) Assert.That(actual[i], Is.EqualTo(expected[i]).Within(.0002f), "matrix element " + i);
        }

        private sealed class HeldTool : IHeldToolIdentity
        {
            public HeldTool(string type, string variant) { ToolType = type; ToolVariant = variant; }
            public string ToolType { get; }
            public string ToolVariant { get; }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new("fuel-line test vehicle");
            public readonly VehicleAssemblyController Assembly;
            public readonly VehiclePersistenceBinding Binding;
            public readonly PartInstance Tank;
            public readonly MountPointAuthoring Mount;
            public readonly MeshFilter Original;
            private readonly List<ScriptableObject> assets = new();
            private readonly Mesh mesh = new();
            private readonly Material material = new(Shader.Find("Hidden/InternalErrorShader"));
            private readonly float oldTimeScale = Time.timeScale;
            private readonly ToolDefinition[] tools;
            private bool initialized;
            public SatsumaFuelLineConnection State => Root.GetComponent<SatsumaFuelLineConnection>();
            public SatsumaFuelLineFastenerInteractionTarget Target => Root.GetComponentInChildren<SatsumaFuelLineFastenerInteractionTarget>(true);

            public Fixture()
            {
                Time.timeScale = 1;
                Root.SetActive(false);
                Root.transform.SetPositionAndRotation(new Vector3(153.5f, 3.2f, -1026.1f), Quaternion.Euler(11, 73, -9));
                var wrench11 = Asset<ToolDefinition>(); wrench11.Configure("test.wrench11", "Wrench11", "Wrench", FastenerSize.Millimeter11);
                var wrench12 = Asset<ToolDefinition>(); wrench12.Configure("test.wrench12", "Wrench12", "Wrench", FastenerSize.Millimeter12);
                tools = new[] { wrench11, wrench12 };
                PartInstance body = Part("test.body", true);
                Tank = Part(SatsumaFuelLineConnection.TankPartId, false);
                Tank.transform.SetLocalPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(31, 56, 73));
                FastenerDefinition[] bolts = Phase1SatsumaStockMountFastenerAuthoring.GetBindings()
                    .Where(b => b.MountId == SatsumaFuelLineConnection.TankMountId).Select(b =>
                    { var definition = Phase1SatsumaStockMountFastenerAuthoring.CreateDefinition(b); assets.Add(definition); return definition; }).ToArray();
                var mountDefinition = Asset<MountPointDefinition>();
                mountDefinition.Configure(SatsumaFuelLineConnection.TankMountId, "tank", "test.socket", "test.body",
                    new[] { SatsumaFuelLineConnection.TankPartId }, new MountConstraint(.2f, 180f, 1f, 0f), .01f, bolts);
                var group = new FastenerGroupDefinition();
                group.Configure(bolts.Select(b => b.DefinitionId).ToArray(), 56, 12, 0);
                mountDefinition.ConfigureFastenerGroup(group);
                var mountGo = new GameObject("tank mount"); mountGo.transform.SetParent(body.transform, false);
                mountGo.transform.SetLocalPositionAndRotation(new Vector3(.15f, .2f, -.5f), Quaternion.Euler(88, 13, 17));
                Mount = mountGo.AddComponent<MountPointAuthoring>(); Mount.Configure(mountDefinition, SatsumaFuelLineConnection.TankMountId, mountGo.transform, 0);
                var original = new GameObject("reviewed source nut"); original.transform.SetParent(Tank.transform, false);
                original.transform.SetLocalPositionAndRotation(Authoring.TankLocalPose.position, Authoring.TankLocalPose.rotation);
                original.transform.localScale = Authoring.PhysicalScale;
                Original = original.AddComponent<MeshFilter>(); Original.sharedMesh = mesh;
                original.AddComponent<MeshRenderer>().sharedMaterial = material;
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                Binding = Root.AddComponent<VehiclePersistenceBinding>();
                var data = new SerializedObject(Assembly);
                Assign(data, "parts", new Object[] { body, Tank }); Assign(data, "mountPoints", new Object[] { Mount });
                Assign(data, "tools", tools.Cast<Object>().ToArray()); data.FindProperty("loosePartsRoot").objectReferenceValue = Root.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            public int Apply() => Authoring.Configure(Assembly, mesh, material, 8);
            public void PersistSources(string folder)
            {
                AssetDatabase.CreateFolder(folder, "Meshes"); AssetDatabase.CreateFolder(folder, "Materials");
                AssetDatabase.CreateAsset(mesh, folder + "/Meshes/" + Authoring.NutMeshSourceGuid + ".asset");
                AssetDatabase.CreateAsset(material, folder + "/Materials/" + Authoring.MaterialSourceGuid + ".mat");
                for (int i = 0; i < assets.Count; i++) AssetDatabase.CreateAsset(assets[i], folder + "/Definition" + i + ".asset");
            }
            public void Install()
            {
                if (!initialized)
                {
                    Assembly.Configure(Assembly.Parts, Assembly.MountPoints, Array.Empty<AssemblyDependency>(), tools, Root.transform);
                    initialized = true; Root.SetActive(true);
                }
                Tank.transform.SetPositionAndRotation(Mount.Pose.position, Mount.Pose.rotation);
                Assert.That(Assembly.TryInstall(Tank, Mount).Succeeded, Is.True);
                Target.RefreshPresentation();
            }
            private PartInstance Part(string id, bool root)
            {
                var go = new GameObject(id); go.transform.SetParent(Root.transform, false);
                var definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Body, 1, null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, go.AddComponent<Rigidbody>(), null, root, "");
                return part;
            }
            private T Asset<T>() where T : ScriptableObject
            { T value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
            private static void Assign(SerializedObject data, string name, Object[] values)
            {
                var array = data.FindProperty(name); array.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            public void Dispose()
            {
                Time.timeScale = oldTimeScale;
                Object.DestroyImmediate(Root);
                foreach (var asset in assets) if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
                if (mesh != null && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
                if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
            }
        }
    }
}
