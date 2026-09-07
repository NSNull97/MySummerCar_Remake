using System.Collections.Generic;
using MSC.Vehicle.ItemsIntegration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Items.Tests.EditMode
{
    public sealed class ItemPartInteractionBoundsTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null) Object.DestroyImmediate(owned[index]);
            owned.Clear();
        }

        [TestCase(1f)]
        [TestCase(1.7f)]
        public void SkinnedGeometryIgnoresImportedAabbAndAppliesRendererScaleOnce(float parentScale)
        {
            GameObject part = CreateObject("Part");
            part.transform.SetPositionAndRotation(new Vector3(12f, 3f, -8f), Quaternion.Euler(12f, 37f, 5f));
            part.transform.localScale = Vector3.one * parentScale;
            SkinnedMeshRenderer renderer = CreateSkin(part.transform, out Mesh source, out Transform bone);
            renderer.transform.localPosition = new Vector3(.1f, .2f, -.3f);
            renderer.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            renderer.transform.localScale = new Vector3(2f, 3f, .5f);
            bone.localPosition = new Vector3(.04f, -.02f, .01f);
            Bounds imported = renderer.localBounds;
            Bounds expected = MeasureSkinnedVertices(part.transform, source, bone);
            // In part space the 90-degree Y rotation swaps X/Z; the outer
            // parent scale must cancel, including for the bone's displaced center.
            AssertBounds(expected, new Bounds(new Vector3(.105f, .14f, -.38f), new Vector3(.06f, .18f, .48f)));

            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, renderer.gameObject, out Bounds actual), Is.True);

            AssertBounds(actual, expected);
            Assert.That(actual.size.magnitude, Is.LessThan(1f));
            Assert.That(renderer.sharedMesh, Is.SameAs(source));
            AssertBounds(renderer.localBounds, imported);
        }

        [Test]
        public void RebuildMeasuresCurrentBonePoseWithoutRetainingTemporaryMeshes()
        {
            GameObject part = CreateObject("Part");
            SkinnedMeshRenderer renderer = CreateSkin(part.transform, out Mesh source, out Transform bone);
            HashSet<EntityId> meshesBefore = CaptureMeshIds();
            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, renderer.gameObject, out Bounds initial), Is.True);

            bone.localPosition = new Vector3(.3f, .1f, -.2f);
            bone.localScale = new Vector3(1.5f, 1f, 2f);
            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, renderer.gameObject, out Bounds moved), Is.True);

            AssertBounds(moved, MeasureSkinnedVertices(part.transform, source, bone));
            Assert.That((moved.center - initial.center).magnitude, Is.GreaterThan(.3f));
            Assert.That(CaptureMeshIds(), Is.EquivalentTo(meshesBefore));
        }

        [Test]
        public void OrdinaryRendererKeepsItsAuthoredLocalBoundsAndMixedGeometryIsCombined()
        {
            GameObject part = CreateObject("Part");
            CreateSkin(part.transform, out Mesh source, out Transform bone);
            GameObject rigid = CreateObject("Ordinary visual", part.transform);
            rigid.transform.localPosition = new Vector3(.5f, .3f, -.4f);
            rigid.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            rigid.transform.localScale = new Vector3(2f, .5f, 3f);
            rigid.AddComponent<MeshFilter>().sharedMesh = source;
            MeshRenderer renderer = rigid.AddComponent<MeshRenderer>();
            renderer.localBounds = new Bounds(new Vector3(.1f, .2f, 0f), new Vector3(.4f, .6f, .8f));
            Bounds expected = MeasureBox(part.transform, rigid.transform, renderer.localBounds);

            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, rigid, out Bounds ordinary), Is.True);
            AssertBounds(ordinary, expected);

            expected.Encapsulate(MeasureSkinnedVertices(part.transform, source, bone));
            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, part, out Bounds mixed), Is.True);
            AssertBounds(mixed, expected);
        }

        [Test]
        public void MissingSkinnedGeometryRejectsEvenAnOtherwiseValidAggregate()
        {
            GameObject part = CreateObject("Part");
            GameObject rigid = CreateObject("Ordinary visual", part.transform);
            rigid.AddComponent<MeshRenderer>().localBounds = new Bounds(Vector3.zero, Vector3.one);
            GameObject invalid = CreateObject("Missing skin", part.transform);
            invalid.AddComponent<SkinnedMeshRenderer>().localBounds =
                new Bounds(Vector3.zero, Vector3.one * 12f);

            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, part, out Bounds bounds), Is.False);
            AssertBounds(bounds, default);
        }

        [Test]
        public void EmptyPresentationRequestsTheAuthoredFallback()
        {
            GameObject part = CreateObject("Part");
            Assert.That(ItemPartInteractionBounds.TryGetPartLocalBounds(
                part.transform, part, out Bounds bounds), Is.False);
            AssertBounds(bounds, default);
        }

        private GameObject CreateObject(string name, Transform parent = null)
        {
            var result = new GameObject(name);
            owned.Add(result);
            result.transform.SetParent(parent, false);
            return result;
        }

        private SkinnedMeshRenderer CreateSkin(Transform parent, out Mesh mesh, out Transform bone)
        {
            GameObject visual = CreateObject("Skinned visual", parent);
            bone = CreateObject("Bone", visual.transform).transform;
            mesh = new Mesh { name = "Project-owned quarter-metre skin fixture" };
            owned.Add(mesh);
            var vertices = new Vector3[8];
            var weights = new BoneWeight[8];
            for (int corner = 0; corner < 8; corner++)
            {
                vertices[corner] = new Vector3(
                    (corner & 1) == 0 ? -.12f : .12f,
                    (corner & 2) == 0 ? -.03f : .03f,
                    (corner & 4) == 0 ? -.06f : .06f);
                weights[corner] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6 };
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { bone.worldToLocalMatrix * visual.transform.localToWorldMatrix };
            mesh.RecalculateBounds();
            SkinnedMeshRenderer renderer = visual.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { bone };
            renderer.rootBone = bone;
            renderer.localBounds = new Bounds(new Vector3(4f, .5f, -3f), new Vector3(11.78f, .574f, 11.56f));
            renderer.updateWhenOffscreen = false;
            return renderer;
        }

        private static Bounds MeasureSkinnedVertices(Transform part, Mesh mesh, Transform bone)
        {
            Matrix4x4 toPart = part.worldToLocalMatrix * bone.localToWorldMatrix * mesh.bindposes[0];
            Vector3[] vertices = mesh.vertices;
            var result = new Bounds(toPart.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int index = 1; index < vertices.Length; index++)
                result.Encapsulate(toPart.MultiplyPoint3x4(vertices[index]));
            return result;
        }

        private static Bounds MeasureBox(Transform part, Transform visual, Bounds box)
        {
            Matrix4x4 toPart = part.worldToLocalMatrix * visual.localToWorldMatrix;
            var result = new Bounds(toPart.MultiplyPoint3x4(box.min), Vector3.zero);
            for (int corner = 1; corner < 8; corner++)
                result.Encapsulate(toPart.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y,
                    (corner & 4) == 0 ? box.min.z : box.max.z)));
            return result;
        }

        private static HashSet<EntityId> CaptureMeshIds()
        {
            var result = new HashSet<EntityId>();
            foreach (Mesh mesh in Resources.FindObjectsOfTypeAll<Mesh>()) result.Add(mesh.GetEntityId());
            return result;
        }

        private static void AssertBounds(Bounds actual, Bounds expected)
        {
            Assert.That((actual.center - expected.center).magnitude, Is.LessThan(.0001f), "center");
            Assert.That((actual.size - expected.size).magnitude, Is.LessThan(.0001f), "size");
        }
    }
}
