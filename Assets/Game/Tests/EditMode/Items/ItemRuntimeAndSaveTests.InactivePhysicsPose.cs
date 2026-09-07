using System.Linq;
using MSC.Save;
using MSC.Save.Integration;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Items.Tests.EditMode
{
    public sealed partial class ItemRuntimeAndSaveTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void PhysicsPose_ItemCapturePreservesActiveAndInactiveMaterializationPose(
            bool disableSelf,
            bool disableParent)
        {
            WorldItemInstance instance = fixture.Spawn(
                "P1.ITEM.138",
                "inactive-pose-spanner",
                new Vector3(41f, 7f, -29f));
            Rigidbody body = instance.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            var parent = new GameObject("Item pose inactive-parent fixture");
            Transform originalParent = instance.transform.parent;
            try
            {
                instance.transform.SetParent(parent.transform, true);
                instance.gameObject.SetActive(!disableSelf);
                parent.SetActive(!disableParent);
                var expected = new Pose(
                    new Vector3(43f, 8f, -31f),
                    Quaternion.Euler(29f, -71f, 13f));
                instance.transform.SetPositionAndRotation(expected.position, expected.rotation);
                if (instance.gameObject.activeInHierarchy)
                {
                    body.position = expected.position;
                    body.rotation = expected.rotation;
                    AssertItemPhysicsPose(body.position, body.rotation, expected);
                }
                bool activeBefore = instance.gameObject.activeInHierarchy;
                string stateBefore = JsonUtility.ToJson(instance.CaptureState());
                var participant = new ItemSaveParticipant(
                    fixture.Runtime,
                    new DeferredStableEntityStore());

                ItemRuntimeSaveRecord saved = JsonUtility
                    .FromJson<ItemDomainSaveDto>(participant.CapturePayload())
                    .instances.Single(value => value.state.stableEntityId == instance.StableId.Value);

                AssertItemPhysicsPose(saved.materializationPosition, saved.materializationRotation, expected);
                AssertItemPhysicsPose(instance.transform.position, instance.transform.rotation, expected);
                Assert.That(JsonUtility.ToJson(saved.state), Is.EqualTo(stateBefore));
                Assert.That(instance.gameObject.activeSelf, Is.EqualTo(!disableSelf));
                Assert.That(instance.gameObject.activeInHierarchy, Is.EqualTo(activeBefore));
                Assert.That(fixture.Runtime.TryGetInstance(instance.StableId.Value, out WorldItemInstance retained), Is.True);
                Assert.That(retained, Is.SameAs(instance));
            }
            finally
            {
                instance.transform.SetParent(originalParent, true);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        private static void AssertItemPhysicsPose(Vector3 position, Quaternion rotation, Pose expected)
        {
            Assert.That(Vector3.Distance(position, expected.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(rotation * Vector3.forward, expected.rotation * Vector3.forward), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(rotation * Vector3.up, expected.rotation * Vector3.up), Is.LessThan(0.0001f));
        }
    }
}
