using System;
using System.Linq;
using System.Reflection;
using MSC.Items.Presentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Items.Tests.EditMode
{
    public sealed partial class ItemRuntimeAndSaveTests
    {
        [Test]
        public void SkinnedItemBridgeRepairsDefaultSolidAndQueryBeforePublishingWithoutReplacingItemState()
        {
            using var presentation = new SkinnedBoundsTestPresentationProvider();
            using var definitions = new SkinnedBoundsTestDefinitions(fixture.Runtime, false);
            SetSkinnedBoundsTestPresentation(presentation);
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.alternator-belt", "mount.test.skinned-belt");
            bool presentationObserved = false;
            fixture.Runtime.PresentationAttached += (observed, _) =>
            {
                AssertSkinnedBridgeBounds(observed, presentation.ExpectedBounds, true);
                presentationObserved = true;
            };

            WorldItemInstance item = SpawnOwnedTestItem("item.alternator-belt", "bridge.skinned.default", "source.skinned");
            Assert.That(presentationObserved, Is.True);
            Assert.That(item.Definition.HasAuthoredColliderShapes, Is.False);
            PartInstance part = item.GetComponent<PartInstance>();
            Rigidbody body = part.Body;
            BoxCollider solid = item.GetComponent<BoxCollider>();
            AssemblyInstalledPartInteractionProxy proxy = item.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
            Collider query = proxy.InteractionCollider;
            GameObject visual = item.PresentationRoot;
            ItemInstanceState condition = item.CaptureState();
            condition.condition = 43f;
            item.ApplyState(condition);
            string stateBefore = JsonUtility.ToJson(item.CaptureState());
            body.position = new Vector3(2f, 3f, 4f);
            body.linearVelocity = new Vector3(.3f, .2f, -.1f);
            Vector3 positionBefore = body.position;
            Vector3 velocityBefore = body.linearVelocity;
            float massBefore = body.mass;
            bool kinematicBefore = body.isKinematic;
            bool collisionsBefore = body.detectCollisions;

            bridgeFixture.Bridge.ReconcilePresentation(item, visual);

            AssertSkinnedBridgeBounds(item, presentation.ExpectedBounds, true);
            Assert.That(item.GetComponent<PartInstance>(), Is.SameAs(part));
            Assert.That(item.GetComponent<Rigidbody>(), Is.SameAs(body));
            Assert.That(item.GetComponent<BoxCollider>(), Is.SameAs(solid));
            Assert.That(proxy.InteractionCollider, Is.SameAs(query));
            Assert.That(item.PresentationRoot, Is.SameAs(visual));
            Assert.That(JsonUtility.ToJson(item.CaptureState()), Is.EqualTo(stateBefore));
            Assert.That(body.position, Is.EqualTo(positionBefore));
            Assert.That(body.linearVelocity, Is.EqualTo(velocityBefore));
            Assert.That(body.mass, Is.EqualTo(massBefore));
            Assert.That(body.isKinematic, Is.EqualTo(kinematicBefore));
            Assert.That(body.detectCollisions, Is.EqualTo(collisionsBefore));
            Assert.That(fixture.Runtime.TryGetInstance(item.StableId.Value, out WorldItemInstance registered), Is.True);
            Assert.That(registered, Is.SameAs(item));
            Assert.That(fixture.Runtime.TryGetSourceCellId(item.StableId.Value, out string cell), Is.True);
            Assert.That(cell, Is.EqualTo("source.skinned"));
            body.position = bridgeFixture.Mount.transform.position;
            Assert.That(bridgeFixture.Assembly.TryInstall(part, bridgeFixture.Mount).Succeeded, Is.True);
            Assert.That(query.enabled, Is.True);
            Assert.That(query.attachedRigidbody, Is.SameAs(body));
        }

        [Test]
        public void SkinnedItemBridgePreservesAuthoredCompoundShapesWhileFittingItsQuery()
        {
            using var presentation = new SkinnedBoundsTestPresentationProvider();
            using var definitions = new SkinnedBoundsTestDefinitions(fixture.Runtime, true);
            SetSkinnedBoundsTestPresentation(presentation);
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.alternator-belt");
            WorldItemInstance item = SpawnOwnedTestItem("item.alternator-belt", "bridge.skinned.authored");
            Assert.That(item.Definition.HasAuthoredColliderShapes, Is.True);
            BoxCollider box = item.GetComponent<BoxCollider>();
            SphereCollider sphere = item.GetComponent<SphereCollider>();
            Assert.That(box, Is.Not.Null);
            Assert.That(sphere, Is.Not.Null);
            Collider[] originalShapes = item.GetComponents<Collider>();
            Rigidbody body = item.GetComponent<Rigidbody>();
            PartInstance part = item.GetComponent<PartInstance>();
            string stateBefore = JsonUtility.ToJson(item.CaptureState());

            bridgeFixture.Bridge.ReconcilePresentation(item, item.PresentationRoot);

            Assert.That(item.GetComponents<Collider>(), Is.EqualTo(originalShapes));
            Assert.That(originalShapes, Has.Length.EqualTo(2));
            Assert.That(box.center, Is.EqualTo(definitions.AuthoredBox.Center));
            Assert.That(box.size, Is.EqualTo(definitions.AuthoredBox.Size));
            Assert.That(sphere.center, Is.EqualTo(definitions.AuthoredSphere.Center));
            Assert.That(sphere.radius, Is.EqualTo(definitions.AuthoredSphere.Radius));
            Assert.That(box.isTrigger || sphere.isTrigger, Is.False);
            Assert.That(box.attachedRigidbody, Is.SameAs(body));
            Assert.That(sphere.attachedRigidbody, Is.SameAs(body));
            Assert.That(item.GetComponent<PartInstance>(), Is.SameAs(part));
            Assert.That(JsonUtility.ToJson(item.CaptureState()), Is.EqualTo(stateBefore));
            AssertSkinnedBridgeBounds(item, presentation.ExpectedBounds, false);
        }

        private void SetSkinnedBoundsTestPresentation(IItemPresentationProvider presentation) =>
            typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fixture.Runtime, presentation);

        private static void AssertSkinnedBridgeBounds(WorldItemInstance item, Bounds expected, bool expectFittedSolid)
        {
            AssemblyInstalledPartInteractionProxy proxy = item.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
            Assert.That(proxy, Is.Not.Null);
            var query = (BoxCollider)proxy.InteractionCollider;
            Assert.That((query.center - expected.center).magnitude, Is.LessThan(.0001f));
            Assert.That((query.size - Vector3.Max(Vector3.one * .075f, expected.size + Vector3.one * .035f)).magnitude,
                Is.LessThan(.0001f));
            Assert.That(query.size.magnitude, Is.LessThan(1f), "The imported skin AABB must not become a room-sized query.");
            if (!expectFittedSolid) return;
            BoxCollider solid = item.GetComponent<BoxCollider>();
            Assert.That((solid.center - expected.center).magnitude, Is.LessThan(.0001f));
            Assert.That((solid.size - Vector3.Max(Vector3.one * .02f, expected.size)).magnitude, Is.LessThan(.0001f));
            Assert.That(solid.size.magnitude, Is.LessThan(1f), "Generic item fitting ran before the bridge callback.");
            Assert.That(solid.isTrigger, Is.False);
        }

        private sealed class SkinnedBoundsTestDefinitions : IDisposable
        {
            private readonly ItemWorldRuntime runtime;
            private readonly FieldInfo field;
            private readonly ItemDefinitionCatalog original;
            private readonly ItemDefinitionCatalog temporary;
            public ItemColliderShapeDefinition AuthoredBox { get; }
            public ItemColliderShapeDefinition AuthoredSphere { get; }

            public SkinnedBoundsTestDefinitions(ItemWorldRuntime runtime, bool authoredShapes)
            {
                this.runtime = runtime;
                field = typeof(ItemWorldRuntime).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic);
                original = (ItemDefinitionCatalog)field.GetValue(runtime);
                ItemDefinitionRecord source = original.Definitions.Single(record => record.DefinitionId == "item.alternator-belt");
                ItemDefinitionRecord local = JsonUtility.FromJson<ItemDefinitionRecord>(JsonUtility.ToJson(source));
                AuthoredBox = new ItemColliderShapeDefinition();
                AuthoredBox.ConfigureForAuthoring(ItemColliderShapeKind.Box,
                    new Vector3(-.05f, .03f, .01f), new Vector3(.07f, .13f, .11f), .05f, .1f, 1);
                AuthoredSphere = new ItemColliderShapeDefinition();
                AuthoredSphere.ConfigureForAuthoring(ItemColliderShapeKind.Sphere,
                    new Vector3(.05f, -.02f, .03f), Vector3.one * .07f, .035f, .1f, 1);
                local.ConfigurePhysicalShapesForAuthoring(authoredShapes
                    ? new[] { AuthoredBox, AuthoredSphere } : Array.Empty<ItemColliderShapeDefinition>());
                temporary = ScriptableObject.CreateInstance<ItemDefinitionCatalog>();
                temporary.ConfigureForAuthoring(original.CatalogId,
                    original.Definitions.Select(record => record == source ? local : record).ToArray());
                field.SetValue(runtime, temporary);
            }

            public void Dispose()
            {
                if (runtime != null) field.SetValue(runtime, original);
                Object.DestroyImmediate(temporary);
            }
        }

        private sealed class SkinnedBoundsTestPresentationProvider : IItemPresentationProvider, IDisposable
        {
            private readonly Mesh mesh;
            private static readonly Vector3 VisualOffset = new Vector3(.11f, .07f, -.08f);
            private static readonly Vector3 BoneOffset = new Vector3(.03f, .01f, -.02f);
            private static readonly Vector3 MeshSize = new Vector3(.24f, .08f, .16f);
            public Bounds ExpectedBounds => new Bounds(VisualOffset + BoneOffset, MeshSize);

            public SkinnedBoundsTestPresentationProvider()
            {
                mesh = new Mesh { name = "Synthetic skinned bridge bounds" };
                var vertices = new Vector3[8];
                var weights = new BoneWeight[8];
                for (int index = 0; index < vertices.Length; index++)
                {
                    vertices[index] = Vector3.Scale(MeshSize, new Vector3(
                        (index & 1) == 0 ? -.5f : .5f,
                        (index & 2) == 0 ? -.5f : .5f,
                        (index & 4) == 0 ? -.5f : .5f));
                    weights[index] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                }
                mesh.vertices = vertices;
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6 };
                mesh.boneWeights = weights;
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.RecalculateBounds();
            }

            public bool TryInstantiate(ItemDefinitionRecord definition, Transform parent, out GameObject visualRoot)
            {
                visualRoot = new GameObject("Synthetic skinned item presentation");
                visualRoot.transform.SetParent(parent, false);
                visualRoot.transform.localPosition = VisualOffset;
                Transform bone = new GameObject("Project-owned bone").transform;
                bone.SetParent(visualRoot.transform, false);
                bone.localPosition = BoneOffset;
                SkinnedMeshRenderer renderer = visualRoot.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.bones = new[] { bone };
                renderer.rootBone = bone;
                renderer.localBounds = new Bounds(new Vector3(4f, .5f, -3f), new Vector3(11.78f, .574f, 11.56f));
                renderer.updateWhenOffscreen = false;
                return true;
            }

            public void Dispose() => Object.DestroyImmediate(mesh);
        }
    }
}
