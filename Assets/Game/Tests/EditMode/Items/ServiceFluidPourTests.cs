using MSC.Interaction.Capabilities;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Items.Tests.EditMode
{
    public sealed partial class ItemRuntimeAndSaveTests
    {
        [TestCase("P1.ITEM.120", .1f)] [TestCase("P1.ITEM.121", .2f)] [TestCase("P1.ITEM.122", .1f)]
        public void ServicePour_UsesFiniteRealCan_AndMatchingPassiveReceiver(string featureId, float rate)
        {
            WorldItemInstance can = fixture.Spawn(featureId, "service-pour-" + featureId, new Vector3(100f, 100f, 100f));
            var pour = can.GetComponent<ServiceFluidPourController>(); Assert.That(pour, Is.Not.Null);
            Assert.That(can.TryPerformPrimaryAction(fixture.Context), Is.True);
            can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            var receiverObject = new GameObject("Typed receiver");
            try
            {
                receiverObject.transform.position = pour.OutletWorldPosition + Vector3.down * .3f;
                receiverObject.AddComponent<SphereCollider>().radius = .025f;
                var receiver = receiverObject.AddComponent<ServicePourTestReceiver>(); receiver.Liquid = can.LiquidId;
                Physics.SyncTransforms(); float before = can.LiquidAmountLitres;
                Assert.That(pour.TraceStream(out _, out bool hit), Is.SameAs(receiver)); Assert.That(hit, Is.True);
                pour.Tick(.1f);
                Assert.That(pour.IsPouring, Is.True);
                Assert.That(receiver.Content, Is.EqualTo(rate * .1f).Within(.00001f));
                Assert.That(can.LiquidAmountLitres + receiver.Content, Is.EqualTo(before).Within(.00001f));
            }
            finally { Object.DestroyImmediate(receiverObject); }
        }

        [Test]
        public void ServicePour_ClosedUprightPausedAndRestoredCansNeverMagicallyRefillOrDischarge()
        {
            WorldItemInstance can = fixture.Spawn("P1.ITEM.120", "service-pour-closed", new Vector3(100f, 100f, 100f));
            var pour = can.GetComponent<ServiceFluidPourController>();
            can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            pour.Tick(.1f); Assert.That(can.LiquidAmountLitres, Is.EqualTo(1f)); Assert.That(pour.IsPouring, Is.False);
            can.TryPerformPrimaryAction(fixture.Context);
            can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
            pour.Tick(.1f); Assert.That(can.LiquidAmountLitres, Is.EqualTo(1f));
            can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            pour.Tick(0f); Assert.That(pour.IsPouring, Is.False);
            Physics.SyncTransforms(); pour.Tick(.1f);
            Assert.That(can.LiquidAmountLitres, Is.EqualTo(.99f).Within(.00001f));
            ItemInstanceState saved = can.CaptureState();
            can.ApplyState(saved); pour.Tick(0f);
            Assert.That(can.LiquidAmountLitres, Is.EqualTo(saved.content));
            Assert.That(can.IsOpen, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void ServicePour_WrongFluidOrInterveningWallSpillsInsteadOfFillingThroughIt(bool wall)
        {
            WorldItemInstance can = fixture.Spawn("P1.ITEM.120", "service-pour-blocked", new Vector3(100f, 100f, 100f));
            can.TryPerformPrimaryAction(fixture.Context); can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            var pour = can.GetComponent<ServiceFluidPourController>();
            var receiverObject = new GameObject("Blocked receiver"); GameObject obstacle = null;
            try
            {
                receiverObject.transform.position = pour.OutletWorldPosition + Vector3.down * .3f;
                receiverObject.AddComponent<SphereCollider>().radius = .025f;
                var receiver = receiverObject.AddComponent<ServicePourTestReceiver>(); receiver.Liquid = wall ? can.LiquidId : "liquid.coolant";
                if (wall)
                {
                    obstacle = new GameObject("Solid obstacle"); obstacle.transform.position = pour.OutletWorldPosition + Vector3.down * .15f;
                    obstacle.AddComponent<BoxCollider>().size = new Vector3(.2f, .04f, .2f);
                }
                float spilled = 0f;
                fixture.Runtime.ActionCompleted += value => { if (value.Action == ItemActionKind.LiquidSpilled) spilled += value.AffectedAmount; };
                Physics.SyncTransforms(); pour.Tick(.1f);
                Assert.That(receiver.Content, Is.Zero); Assert.That(spilled, Is.EqualTo(.01f).Within(.00001f));
                Assert.That(can.LiquidAmountLitres + spilled, Is.EqualTo(1f).Within(.00001f));
            }
            finally { Object.DestroyImmediate(receiverObject); if (obstacle != null) Object.DestroyImmediate(obstacle); }
        }

        [Test]
        public void ServicePour_OverflowConservesAcceptedPlusSpilled_AndPetrolStaysOutOfScope()
        {
            WorldItemInstance can = fixture.Spawn("P1.ITEM.120", "service-pour-overflow", new Vector3(100f, 100f, 100f));
            can.TryPerformPrimaryAction(fixture.Context); can.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            var pour = can.GetComponent<ServiceFluidPourController>(); var receiverObject = new GameObject("Nearly full receiver");
            try
            {
                receiverObject.transform.position = pour.OutletWorldPosition + Vector3.down * .3f;
                receiverObject.AddComponent<SphereCollider>().radius = .025f;
                var receiver = receiverObject.AddComponent<ServicePourTestReceiver>(); receiver.Liquid = can.LiquidId; receiver.Capacity = .003f;
                float spilled = 0f;
                fixture.Runtime.ActionCompleted += value => { if (value.Action == ItemActionKind.LiquidSpilled) spilled += value.AffectedAmount; };
                Physics.SyncTransforms(); pour.Tick(.1f);
                Assert.That(receiver.Content, Is.EqualTo(.003f).Within(.000001f));
                Assert.That(spilled, Is.EqualTo(.007f).Within(.000001f));
                Assert.That(can.LiquidAmountLitres + receiver.Content + spilled, Is.EqualTo(1f).Within(.00001f));
                Assert.That(ServiceFluidPourController.TryGetProfile("item.jerrycan", out _), Is.False);
                Assert.That(ServiceFluidPourController.TryGetProfile("item.two-stroke-oil", out _), Is.False);
            }
            finally { Object.DestroyImmediate(receiverObject); }
        }

        [Test]
        public void ServicePour_EmptyCanCannotDuplicateFluid_AndBadFlowInputIsRejected()
        {
            WorldItemInstance can = fixture.Spawn("P1.ITEM.120", "service-pour-empty", Vector3.zero);
            can.TryPerformPrimaryAction(fixture.Context); can.TrySpillLiquid(1f, out _);
            var receiverObject = new GameObject("Empty source receiver");
            try
            {
                var receiver = receiverObject.AddComponent<ServicePourTestReceiver>(); receiver.Liquid = "liquid.brake-fluid";
                Assert.That(can.TryPourLiquidTo(receiver, .1f, out _), Is.False); Assert.That(receiver.Content, Is.Zero);
                Assert.That(ServiceFluidPourController.EvaluateFlow(float.NaN, 1f), Is.Zero);
                Assert.That(ServiceFluidPourController.EvaluateFlow(-1f, 0f), Is.Zero);
                Assert.That(ServiceFluidPourController.EvaluateFlow(1f, 1f), Is.Zero);
            }
            finally { Object.DestroyImmediate(receiverObject); }
        }
    }

    public sealed class ServicePourTestReceiver : MonoBehaviour, ILiquidContainerTarget
    {
        public string Liquid;
        public float Capacity = 1f;
        public float Content;
        public bool CanAcceptLiquid(string liquidId, float requested) => liquidId == Liquid && requested > 0f && Content < Capacity;
        public bool TryAcceptLiquid(string liquidId, float requested, out float accepted)
        {
            accepted = CanAcceptLiquid(liquidId, requested) ? Mathf.Min(requested, Capacity - Content) : 0f;
            Content += accepted; return accepted > 0f;
        }
    }
}
