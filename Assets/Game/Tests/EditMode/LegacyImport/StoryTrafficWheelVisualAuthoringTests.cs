using System;
using System.Reflection;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class StoryTrafficWheelVisualAuthoringTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpinPivot_ReparentsVisualsButLeavesMechanicalBranchesStill()
        {
            root = new GameObject("wheelRL");
            Transform visual = CreateChild(root.transform, "Wheel");
            CreateRendererChild(visual, "Tire");
            CreateRendererChild(visual, "Rim");

            Transform spindle = CreateChild(root.transform, "Spindle");
            Transform axle = CreateRendererChild(spindle, "Axle");
            Transform shaft = CreateRendererChild(spindle, "Driveshaft");
            Transform brake = CreateRendererChild(spindle, "brake");
            Transform ikPivot = CreateChild(spindle, "IKpivot");
            Transform arm = CreateChild(ikPivot, "Arm");
            Transform tandemArm = CreateRendererChild(arm, "tandem_arm");

            Transform spinPivot = InvokeCreateWheelSpinPivot(
                root.transform,
                wheelIndex: 2);

            Assert.That(spinPivot.parent, Is.SameAs(root.transform));
            Assert.That(visual.parent, Is.SameAs(spinPivot));
            Assert.That(
                spinPivot.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(2));
            Assert.That(spindle.parent, Is.SameAs(root.transform));
            Assert.That(axle.IsChildOf(spinPivot), Is.False);
            Assert.That(shaft.IsChildOf(spinPivot), Is.False);
            Assert.That(brake.IsChildOf(spinPivot), Is.False);
            Assert.That(ikPivot.IsChildOf(spinPivot), Is.False);
            Assert.That(arm.IsChildOf(spinPivot), Is.False);
            Assert.That(tandemArm.IsChildOf(spinPivot), Is.False);
        }

        [Test]
        public void SpinPivot_FailsClosedWithoutRotatingRendererGeometry()
        {
            root = new GameObject("wheelRR");
            Transform spindle = CreateChild(root.transform, "Spindle");
            CreateRendererChild(spindle, "rearaxle");

            TargetInvocationException exception = Assert.Throws<
                TargetInvocationException>(() =>
                InvokeCreateWheelSpinPivot(root.transform, wheelIndex: 3));

            Assert.That(exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SpinPivot_SplitsVisualChildrenFromMechanicalWheelContainer()
        {
            root = new GameObject("wheelRL");
            Transform wheel = CreateChild(root.transform, "Wheel");
            Transform tire = CreateRendererChild(wheel, "Tire");
            Transform shaft = CreateRendererChild(wheel, "Shaft");
            Transform rim = CreateRendererChild(wheel, "Rim");

            Transform spinPivot = InvokeCreateWheelSpinPivot(
                root.transform,
                wheelIndex: 2);

            Assert.That(wheel.parent, Is.SameAs(root.transform));
            Assert.That(shaft.parent, Is.SameAs(wheel));
            Assert.That(shaft.IsChildOf(spinPivot), Is.False);
            Assert.That(tire.parent, Is.SameAs(spinPivot));
            Assert.That(rim.parent, Is.SameAs(spinPivot));
            Assert.That(
                spinPivot.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(2));
        }

        [Test]
        public void AmbientLaunchClutch_PreservesIdleTorqueMargin()
        {
            float pedal = InvokeFloatPolicy(
                "ResolveLaunchClutchPedal01",
                isAmbientTraffic: true,
                isBus: false);
            float releaseSpeed = InvokeFloatPolicy(
                "ResolveLaunchClutchReleaseSpeedMetersPerSecond",
                isAmbientTraffic: true,
                isBus: false);

            const float ambientMaximumTorqueNewtonMeters = 145f;
            const float idleTorqueFraction = 0.46f;
            const float baseFrictionNewtonMeters = 7f;
            const float idleRpm = 680f;
            const float viscousFrictionNewtonMetersPerRadian = 0.01f;
            const float clutchCapacityMultiplier = 1.35f;
            const float engagementExponent = 1.6f;
            float idleAngularSpeed = idleRpm * Mathf.PI * 2f / 60f;
            float availableIdleTorque =
                ambientMaximumTorqueNewtonMeters * idleTorqueFraction -
                baseFrictionNewtonMeters -
                viscousFrictionNewtonMetersPerRadian * idleAngularSpeed;
            float launchClutchCapacity =
                ambientMaximumTorqueNewtonMeters * clutchCapacityMultiplier *
                Mathf.Pow(1f - pedal, engagementExponent);
            float previousUniversalClutchCapacity =
                ambientMaximumTorqueNewtonMeters * clutchCapacityMultiplier *
                Mathf.Pow(1f - 0.45f, engagementExponent);

            Assert.That(pedal, Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(releaseSpeed, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(
                previousUniversalClutchCapacity,
                Is.GreaterThan(availableIdleTorque),
                "The fixture must continue to reproduce the old ambient " +
                "launch-stall condition before checking its replacement.");
            Assert.That(
                launchClutchCapacity,
                Is.LessThan(availableIdleTorque),
                "A stationary ambient car must not couple more torque than " +
                "its full-throttle idle engine can sustain.");
        }

        [Test]
        public void LaunchClutchPolicy_DoesNotRetuneAcceptedStoryCarsOrBus()
        {
            Assert.That(
                InvokeFloatPolicy(
                    "ResolveLaunchClutchPedal01",
                    isAmbientTraffic: false,
                    isBus: false),
                Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(
                InvokeFloatPolicy(
                    "ResolveLaunchClutchReleaseSpeedMetersPerSecond",
                    isAmbientTraffic: false,
                    isBus: false),
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(
                InvokeFloatPolicy(
                    "ResolveLaunchClutchPedal01",
                    isAmbientTraffic: true,
                    isBus: true),
                Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(
                InvokeFloatPolicy(
                    "ResolveLaunchClutchReleaseSpeedMetersPerSecond",
                    isAmbientTraffic: true,
                    isBus: true),
                Is.EqualTo(6f).Within(0.0001f));
        }

        private static Transform InvokeCreateWheelSpinPivot(
            Transform wheelRoot,
            int wheelIndex)
        {
            MethodInfo method = typeof(
                    Phase1StoryTrafficPresentationImporter)
                .GetMethod(
                    "CreateWheelSpinPivot",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Transform)method.Invoke(
                null,
                new object[] { wheelRoot, wheelIndex });
        }

        private static float InvokeFloatPolicy(
            string methodName,
            bool isAmbientTraffic,
            bool isBus)
        {
            MethodInfo method = typeof(
                    Phase1StoryTrafficPresentationImporter)
                .GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(
                null,
                new object[] { isAmbientTraffic, isBus });
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreateRendererChild(
            Transform parent,
            string name)
        {
            Transform child = CreateChild(parent, name);
            child.gameObject.AddComponent<MeshRenderer>();
            return child;
        }
    }
}
