using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class AssemblyFastenerExternalPresentationTests
    {
        [Test]
        public void BoundSiblingRendererTracksInstallRemoveAndInstalledAndLooseSaveRestore()
        {
            using var fixture = new Fixture();
            fixture.Target.ConfigureInstalledOnlyExternalPresentationRenderer(fixture.ExternalRenderer);
            fixture.AssertAvailability(false, false);
            string looseSave = fixture.CaptureJson();

            fixture.Install();
            fixture.AssertAvailability(true, true);
            string installedSave = fixture.CaptureJson();

            fixture.Remove();
            fixture.AssertAvailability(false, false);

            fixture.RestoreJson(installedSave);
            fixture.AssertAvailability(true, true);

            fixture.RestoreJson(looseSave);
            fixture.AssertAvailability(false, false);
            Assert.That(fixture.Target.InstalledOnlyExternalPresentationRenderer,
                Is.SameAs(fixture.ExternalRenderer));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DefaultNullBindingPreservesAuthoredExternalVisibilityAcrossAssemblyChanges(bool enabled)
        {
            using var fixture = new Fixture();
            fixture.Target.Configure(fixture.Assembly, Fixture.MountId, Fixture.FastenerId,
                fixture.Tool, false, fixture.ExternalRenderer.transform);
            fixture.ExternalRenderer.enabled = enabled;
            Assert.That(fixture.Target.InstalledOnlyExternalPresentationRenderer, Is.Null);
            fixture.AssertAvailability(false, enabled);
            string looseSave = fixture.CaptureJson();

            fixture.Install();
            fixture.AssertAvailability(true, enabled);
            string installedSave = fixture.CaptureJson();

            fixture.Remove();
            fixture.AssertAvailability(false, enabled);
            fixture.RestoreJson(installedSave);
            fixture.AssertAvailability(true, enabled);
            fixture.RestoreJson(looseSave);
            fixture.AssertAvailability(false, enabled);
        }

        [Test]
        public void ConfigureOnlyAssignsBindingAndNextNormalQueryRefreshesWithoutGraphMutation()
        {
            using var fixture = new Fixture();
            fixture.AssertAvailability(false, true);
            int graphMutationCount = fixture.Assembly.GraphMutationCount;
            int operationCount = fixture.Assembly.OperationCount;
            string saveBeforeBinding = fixture.CaptureJson();
            var pose = new PresentationSnapshot(fixture.ExternalRenderer);

            fixture.Target.ConfigureInstalledOnlyExternalPresentationRenderer(fixture.ExternalRenderer);

            Assert.That(fixture.Target.InstalledOnlyExternalPresentationRenderer,
                Is.SameAs(fixture.ExternalRenderer));
            Assert.That(fixture.ExternalRenderer.enabled, Is.True,
                "Configuration defers visibility to the normal availability refresh.");
            Assert.That(fixture.CaptureJson(), Is.EqualTo(saveBeforeBinding));
            pose.AssertUnchanged(fixture.ExternalRenderer);

            Assert.That(fixture.Target.CanActivateTool(default), Is.False);
            Assert.That(fixture.ExternalRenderer.enabled, Is.False,
                "A newly bound renderer must refresh even when the graph count has not changed.");
            Assert.That(fixture.Assembly.GraphMutationCount, Is.EqualTo(graphMutationCount));
            Assert.That(fixture.Assembly.OperationCount, Is.EqualTo(operationCount));
            Assert.That(fixture.CaptureJson(), Is.EqualTo(saveBeforeBinding));
            pose.AssertUnchanged(fixture.ExternalRenderer);
        }

        [Test]
        public void BoundVisibilityPreservesFastenerStageAndPoseAndNullBindingStopsDrivingRenderer()
        {
            using var fixture = new Fixture();
            fixture.Install();
            for (int stage = 0; stage < 3; stage++)
            {
                Assert.That(fixture.Assembly.TryTurnFastener(Fixture.MountId, Fixture.FastenerId,
                    fixture.Tool, FastenerRotationDirection.Clockwise).Succeeded, Is.True);
            }

            fixture.Target.ConfigureInstalledOnlyExternalPresentationRenderer(fixture.ExternalRenderer);
            fixture.ExternalRenderer.enabled = false;
            fixture.AssertAvailability(true, true);
            Assert.That(fixture.Fastener.Stage, Is.EqualTo(3));

            fixture.Target.ConfigureInstalledOnlyExternalPresentationRenderer(null);
            fixture.ExternalRenderer.enabled = false;
            fixture.AssertAvailability(true, false);
            Assert.That(fixture.Target.InstalledOnlyExternalPresentationRenderer, Is.Null);
            Assert.That(fixture.Fastener.Stage, Is.EqualTo(3));
        }

        private readonly struct PresentationSnapshot
        {
            private readonly Transform parent;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;
            private readonly Mesh mesh;
            private readonly bool activeSelf;

            public PresentationSnapshot(Renderer renderer)
            {
                Transform transform = renderer.transform;
                parent = transform.parent;
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
                mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                activeSelf = renderer.gameObject.activeSelf;
            }

            public void AssertUnchanged(Renderer renderer)
            {
                Assert.That(renderer.transform.parent, Is.SameAs(parent));
                Assert.That(renderer.transform.localPosition, Is.EqualTo(localPosition));
                Assert.That(Mathf.Abs(Quaternion.Dot(renderer.transform.localRotation, localRotation)),
                    Is.EqualTo(1f).Within(0.000001f));
                Assert.That(renderer.transform.localScale, Is.EqualTo(localScale));
                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(renderer.gameObject.activeSelf, Is.EqualTo(activeSelf));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public const string MountId = "test.external-presentation.mount";
            public const string FastenerId = "test.external-presentation.bolt";
            private const string BodyId = "test.external-presentation.body";
            private const string PartId = "test.external-presentation.part";
            private const string SocketId = "test.external-presentation.socket";
            private readonly GameObject root = new GameObject("External fastener presentation fixture");
            private readonly List<Object> assets = new List<Object>();

            public Fixture()
            {
                PartInstance body = CreatePart(BodyId, true);
                Part = CreatePart(PartId, false);
                var bolt = Asset<FastenerDefinition>();
                bolt.Configure(FastenerId, "Test bolt", FastenerSize.Millimeter10, 8,
                    FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
                var mountDefinition = Asset<MountPointDefinition>();
                mountDefinition.Configure(MountId, "Test mount", SocketId, BodyId,
                    new[] { PartId }, new MountConstraint(0.1f, 30f, 1f, 0f), 0f, new[] { bolt });
                var mountObject = new GameObject("Explicit mount");
                mountObject.transform.SetParent(body.transform, false);
                Mount = mountObject.AddComponent<MountPointAuthoring>();
                Mount.Configure(mountDefinition, MountId, mountObject.transform, 0);
                Tool = Asset<ToolDefinition>();
                Tool.Configure("test.external-presentation.wrench10", "Test wrench", "Wrench",
                    FastenerSize.Millimeter10);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, Part }, new[] { Mount },
                    Array.Empty<AssemblyDependency>(), new[] { Tool }, root.transform);

                var visualObject = new GameObject("Explicit sibling fastener mesh");
                visualObject.transform.SetParent(Part.transform, false);
                visualObject.transform.localPosition = new Vector3(0.03f, 0.07f, 0.11f);
                visualObject.transform.localRotation = Quaternion.Euler(17f, 31f, 43f);
                visualObject.transform.localScale = new Vector3(0.7f, 0.8f, 0.9f);
                var mesh = new Mesh { name = "Project-owned synthetic fastener mesh" };
                assets.Add(mesh);
                visualObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                ExternalRenderer = visualObject.AddComponent<MeshRenderer>();
                var targetObject = new GameObject("Explicit fastener interaction marker");
                targetObject.transform.SetParent(root.transform, false);
                Collider = targetObject.AddComponent<SphereCollider>();
                Collider.isTrigger = true;
                Target = targetObject.AddComponent<AssemblyFastenerInteractionTarget>();
                Target.Configure(Assembly, MountId, FastenerId, Tool, false);
                Assert.That(ExternalRenderer.transform.IsChildOf(Target.transform), Is.False);
            }

            public VehicleAssemblyController Assembly { get; }
            public PartInstance Part { get; }
            public MountPointAuthoring Mount { get; }
            public ToolDefinition Tool { get; }
            public AssemblyFastenerInteractionTarget Target { get; }
            public MeshRenderer ExternalRenderer { get; }
            public SphereCollider Collider { get; }
            public FastenerInstance Fastener => Assembly.ResolveMount(Mount).Fasteners[0];

            public void Install()
            {
                Part.transform.SetPositionAndRotation(Mount.Pose.position, Mount.Pose.rotation);
                Physics.SyncTransforms();
                AssemblyOperationResult result = Assembly.TryInstall(Part, Mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void Remove()
            {
                AssemblyOperationResult result = Assembly.TryRemove(Part);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public string CaptureJson() => JsonUtility.ToJson(Assembly.CaptureSaveData());

            public void RestoreJson(string json)
            {
                AssemblyOperationResult result = Assembly.RestoreSaveData(
                    JsonUtility.FromJson<VehicleAssemblySaveData>(json));
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void AssertAvailability(bool available, bool rendererEnabled)
            {
                var pose = new PresentationSnapshot(ExternalRenderer);
                string saveBeforeRefresh = CaptureJson();
                int operationCount = Assembly.OperationCount;
                int graphMutationCount = Assembly.GraphMutationCount;
                Target.RefreshAvailability();
                Assert.That(Fastener.IsInserted, Is.EqualTo(available));
                Assert.That(Collider.enabled, Is.EqualTo(available));
                Assert.That(ExternalRenderer.enabled, Is.EqualTo(rendererEnabled));
                Assert.That(CaptureJson(), Is.EqualTo(saveBeforeRefresh));
                Assert.That(Assembly.OperationCount, Is.EqualTo(operationCount));
                Assert.That(Assembly.GraphMutationCount, Is.EqualTo(graphMutationCount));
                pose.AssertUnchanged(ExternalRenderer);
            }

            private PartInstance CreatePart(string id, bool isRoot)
            {
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                PartDefinition definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create(SocketId, BodyId) });
                var identity = owner.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = owner.AddComponent<Rigidbody>();
                var pickup = owner.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, id, 35f);
                var part = owner.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, isRoot, string.Empty);
                return part;
            }

            private T Asset<T>() where T : ScriptableObject
            {
                T asset = ScriptableObject.CreateInstance<T>();
                assets.Add(asset);
                return asset;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (Object asset in assets) Object.DestroyImmediate(asset);
            }
        }
    }
}
