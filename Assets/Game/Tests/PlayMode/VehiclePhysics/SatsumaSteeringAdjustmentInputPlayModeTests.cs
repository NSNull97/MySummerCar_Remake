using System.Collections;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaSteeringAdjustmentInputPlayModeTests
    {
        private GameObject root;
        private PartInstance rod;
        private AssemblySteeringAlignmentState alignment;
        private AssemblySteeringAlignmentInteractionTarget target;
        private Transform nut;
        private float previousTimeScale;
        private readonly InteractionContext context = default;
        private readonly IHeldToolIdentity wrench = new WrenchIdentity();

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            root = new GameObject("Steering input cadence fixture");
            rod = root.AddComponent<PartInstance>();
            rod.RuntimeState.SetInstalled("test.steering-rod", false);
            alignment = root.AddComponent<AssemblySteeringAlignmentState>();
            alignment.Configure(rod);
            alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto());
            var marker = new GameObject("Adjustment marker");
            marker.transform.SetParent(root.transform, false);
            marker.AddComponent<SphereCollider>().isTrigger = true;
            nut = new GameObject("Adjustment nut").transform;
            nut.SetParent(marker.transform, false);
            Renderer renderer = nut.gameObject.AddComponent<MeshRenderer>();
            target = marker.AddComponent<AssemblySteeringAlignmentInteractionTarget>();
            target.Configure(alignment, nut, renderer, "fl");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (root != null)
            {
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CooldownConsumesOneStepAtNormalAndHalfSpeed()
        {
            yield return null;
            foreach (float timeScale in new[] { 1f, 0.5f })
            {
                Time.timeScale = timeScale;
                // Let the previous operation expire before beginning the next
                // scale case; these are actual player-loop seconds, not a mock.
                yield return WaitScaledSeconds(0.30f);
                alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto());
                nut.localRotation = Quaternion.identity;
                float start = Time.time;
                Assert.That(target.TryActivateHeldTool(wrench, context, 120f), Is.True);
                int revision = alignment.Revision;
                Assert.That(alignment.AlignmentDegrees, Is.EqualTo(-0.1f).Within(0.00001f));
                AssertNutAngle(-13f);
                Assert.That(target.TryActivateHeldTool(wrench, context, -120f), Is.True);
                Assert.That(alignment.Revision, Is.EqualTo(revision));

                float deadline = Time.realtimeSinceStartup + 5f;
                bool accepted = false;
                while (!accepted)
                {
                    yield return null;
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                        "Scaled cooldown did not expire within the real-time watchdog.");
                    float elapsed = Time.time - start;
                    Assert.That(target.TryActivateHeldTool(wrench, context, -500f), Is.True);
                    if (elapsed < AssemblySteeringAlignmentInteractionTarget.OperationCooldownSeconds)
                    {
                        Assert.That(alignment.Revision, Is.EqualTo(revision),
                            $"timeScale={timeScale}: accepted early at {elapsed} scaled seconds");
                        Assert.That(alignment.AlignmentDegrees, Is.EqualTo(-0.1f).Within(0.00001f));
                        AssertNutAngle(-13f);
                    }
                    else
                    {
                        accepted = true;
                        Assert.That(alignment.Revision, Is.EqualTo(revision + 1));
                        Assert.That(alignment.AlignmentDegrees, Is.Zero.Within(0.00001f));
                        AssertNutAngle(0f);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesInputCooldownAndSpannerWorkPose()
        {
            yield return null;
            float operationTime = Time.time;
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.True);
            yield return WaitScaledSeconds(0.06f);
            Assert.That(target.TryResolveToolSnapAnchor(wrench, context, out Transform anchor), Is.True);
            Assert.That(Quaternion.Angle(anchor.localRotation, Quaternion.Euler(0f, 0f, 160f)),
                Is.GreaterThan(0.01f), "The spanner should be partway through its work stroke.");
            Time.timeScale = 0f;
            yield return null;
            target.TryResolveToolSnapAnchor(wrench, context, out anchor);
            float pausedTime = Time.time;
            Quaternion pausedRotation = anchor.localRotation;
            Vector3 pausedPosition = anchor.localPosition;
            int revision = alignment.Revision;
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(Time.time, Is.EqualTo(pausedTime).Within(0.00001f));
            Assert.That(target.TryActivateHeldTool(wrench, context, -1f), Is.False);
            Assert.That(alignment.Revision, Is.EqualTo(revision));
            Assert.That(anchor.localPosition, Is.EqualTo(pausedPosition));
            Assert.That(Quaternion.Angle(anchor.localRotation, pausedRotation), Is.LessThan(0.001f));
            AssertNutAngle(-13f);

            Time.timeScale = 0.5f;
            Assert.That(Time.time - operationTime,
                Is.LessThan(AssemblySteeringAlignmentInteractionTarget.OperationCooldownSeconds));
            Assert.That(target.TryActivateHeldTool(wrench, context, -1f), Is.True);
            Assert.That(alignment.Revision, Is.EqualTo(revision), "Real time during pause must not expire cooldown.");
            yield return WaitScaledSeconds(0.30f);
            Assert.That(target.TryActivateHeldTool(wrench, context, -1f), Is.True);
            Assert.That(alignment.Revision, Is.EqualTo(revision + 1));
            Assert.That(alignment.AlignmentDegrees, Is.Zero.Within(0.00001f));
            AssertNutAngle(0f);
        }

        [UnityTest]
        public IEnumerator RemovalCancelsWorkPoseAndReinstallDoesNotReplayInput()
        {
            yield return null;
            Assert.That(target.TryActivateHeldTool(wrench, context, -99f), Is.True);
            yield return WaitScaledSeconds(0.06f);
            Assert.That(target.TryResolveToolSnapAnchor(wrench, context, out Transform anchor), Is.True);
            int revision = alignment.Revision;
            rod.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
            yield return null;
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(nut.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.False);
            Assert.That(Quaternion.Angle(anchor.localRotation, Quaternion.Euler(0f, 0f, 160f)),
                Is.LessThan(0.001f));
            AssertNutAngle(13f);

            rod.RuntimeState.SetInstalled("test.steering-rod", false);
            yield return WaitScaledSeconds(0.32f);
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(nut.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(alignment.Revision, Is.EqualTo(revision));
            Assert.That(alignment.AlignmentDegrees, Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(Quaternion.Angle(anchor.localRotation, Quaternion.Euler(0f, 0f, 160f)),
                Is.LessThan(0.001f));
            AssertNutAngle(13f);
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.True);
            Assert.That(alignment.Revision, Is.EqualTo(revision + 1));
            Assert.That(alignment.AlignmentDegrees, Is.Zero.Within(0.00001f));
            AssertNutAngle(0f);
        }

        private void AssertNutAngle(float angle) => Assert.That(
            Quaternion.Angle(nut.localRotation, Quaternion.Euler(0f, 0f, angle)),
            Is.LessThan(0.001f));

        private static IEnumerator WaitScaledSeconds(float duration)
        {
            float until = Time.time + duration;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.time < until)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Scaled fixture wait exceeded real-time watchdog.");
                yield return null;
            }
        }

        private sealed class WrenchIdentity : IHeldToolIdentity
        {
            public string ToolType => "Wrench";
            public string ToolVariant => "14";
        }
    }
}
