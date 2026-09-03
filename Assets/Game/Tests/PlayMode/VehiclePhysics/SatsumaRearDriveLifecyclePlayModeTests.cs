using System;
using System.Collections;
using System.Linq;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaRearDriveLifecyclePlayModeTests
    {
        private GameObject fixture;
        private float originalTimeScale;

        [SetUp]
        public void UseRunningPhysicsClock()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator DestroyFixtureAndRestoreClock()
        {
            try
            {
                if (fixture != null)
                {
                    UnityEngine.Object.Destroy(fixture);
                    fixture = null;
                }

                yield return null;
                yield return null;
                Physics.SyncTransforms();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator ArmOnlyIsPhysical_DrumHandsAuthorityToNwh_AndRemovalRestoresHinge()
        {
            RearFixture context = CreateFixture();
            CornerProbe[] probes = InstallArms(context);
            RefreshAuthority(context);

            foreach (CornerProbe probe in probes)
            {
                Assert.That(context.Support.IsSupported(probe.SupportIndex), Is.False,
                    probe.Label + ": a bare arm must not create an invisible NWH contact.");
                Assert.That(probe.Binding.Wheel.enabled, Is.False, probe.Label);
                AssertPhysicalArm(probe, false, false);
            }

            foreach (CornerProbe probe in probes)
            {
                string suffix = probe.Binding.CornerId == "rl" ? "1" : "2";
                probe.Drum = Install(context.Assembly, "drum-brake-" + suffix,
                    probe.Binding.DrumMountId);
            }

            RefreshAuthority(context);
            yield return null;
            RefreshAuthority(context);
            foreach (CornerProbe probe in probes)
            {
                AssertNwhOwnsCorner(context, probe);
            }

            foreach (CornerProbe probe in probes)
            {
                LoosenAndRemove(context.Assembly, probe.Drum);
            }

            RefreshAuthority(context);
            foreach (CornerProbe probe in probes)
            {
                Quaternion neutral = context.Root.transform.rotation *
                    probe.Binding.NeutralArmLocalRotation;
                Assert.That(Quaternion.Angle(probe.Arm.transform.rotation, neutral),
                    Is.LessThan(0.01f),
                    probe.Label + ": neutral pose must be restored before hinge recreation.");
            }
            yield return null;
            RefreshAuthority(context);
            foreach (CornerProbe probe in probes)
            {
                Assert.That(context.Support.IsSupported(probe.SupportIndex), Is.False,
                    probe.Label + ": removing the drum must release NWH support.");
                Assert.That(probe.Binding.Wheel.enabled, Is.False, probe.Label);
                AssertPhysicalArm(probe, false, false);
            }
        }

        [UnityTest]
        public IEnumerator NoneStockAndLongSpringStagesStayNwhOwnedWithoutNativeMotor()
        {
            RearFixture context = CreateFixture();
            CornerProbe[] probes = InstallArms(context);
            foreach (CornerProbe probe in probes)
            {
                string suffix = probe.Binding.CornerId == "rl" ? "1" : "2";
                probe.Drum = Install(context.Assembly, "drum-brake-" + suffix,
                    probe.Binding.DrumMountId);
            }

            RefreshAuthority(context);
            yield return null;
            RefreshAuthority(context);
            AssertStage(context, probes, SpringStage.None);

            foreach (CornerProbe probe in probes)
            {
                string suffix = probe.Binding.CornerId == "rl" ? "1" : "2";
                probe.Spring = Install(context.Assembly, "coil-spring-" + suffix,
                    probe.Binding.StockSpringMountId);
            }

            RefreshAuthority(context);
            yield return null;
            RefreshAuthority(context);
            AssertStage(context, probes, SpringStage.Stock);

            foreach (CornerProbe probe in probes)
            {
                LoosenAndRemove(context.Assembly, probe.Spring);
                string suffix = probe.Binding.CornerId == "rl" ? "1" : "2";
                probe.Spring = Install(context.Assembly,
                    "extra-long-coil-spring-" + suffix,
                    probe.Binding.LongSpringMountId);
            }

            RefreshAuthority(context);
            yield return null;
            RefreshAuthority(context);
            AssertStage(context, probes, SpringStage.Long);

            foreach (CornerProbe probe in probes)
            {
                LoosenAndRemove(context.Assembly, probe.Spring);
            }

            RefreshAuthority(context);
            yield return null;
            RefreshAuthority(context);
            AssertStage(context, probes, SpringStage.None);
        }

        private RearFixture CreateFixture()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private donor-derived Satsuma baseline is unavailable.");
            }

            LegacySatsumaBaselineMetadata metadata =
                prefab.GetComponent<LegacySatsumaBaselineMetadata>();
            Assert.That(metadata, Is.Not.Null);
            fixture = UnityEngine.Object.Instantiate(prefab,
                new Vector3(0f, 80f, 0f), metadata.DefaultWorldRotation);

            Rigidbody chassis = fixture.GetComponent<Rigidbody>();
            Assert.That(chassis, Is.Not.Null);
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleSimulationHost simulation =
                fixture.GetComponent<VehicleSimulationHost>();
            if (simulation != null)
            {
                simulation.enabled = false;
            }

            VehicleAssemblyController assembly =
                fixture.GetComponent<VehicleAssemblyController>();
            NwhAssemblyWheelSupportController support =
                fixture.GetComponent<NwhAssemblyWheelSupportController>();
            SatsumaRearNwhSuspensionController authority =
                fixture.GetComponent<SatsumaRearNwhSuspensionController>();
            SatsumaRearSuspensionController presentation =
                fixture.GetComponent<SatsumaRearSuspensionController>();
            Assert.That(assembly, Is.Not.Null);
            Assert.That(support, Is.Not.Null);
            Assert.That(authority, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(authority.Corners, Has.Length.EqualTo(2));
            Assert.That(presentation.ExternalWheelAuthority, Is.True);

            foreach (PartInstance part in assembly.Parts)
            {
                if (part != null && !part.IsAssemblyRoot && part.Body != null)
                {
                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }
            }

            Physics.SyncTransforms();
            return new RearFixture(fixture, assembly, support, authority);
        }

        private static CornerProbe[] InstallArms(RearFixture context)
        {
            var probes = new CornerProbe[context.Authority.Corners.Length];
            for (int index = 0; index < probes.Length; index++)
            {
                SatsumaRearNwhCornerBinding binding =
                    context.Authority.Corners[index];
                int supportIndex = Array.FindIndex(context.Support.Bindings,
                    candidate => candidate.Wheel == binding.Wheel);
                Assert.That(supportIndex, Is.GreaterThanOrEqualTo(0),
                    "Rear NWH binding is absent for " + binding.CornerId + ".");
                Assert.That(context.Support.Bindings[supportIndex].Enabled, Is.True,
                    "Rear NWH binding is disabled for " + binding.CornerId + ".");
                PartInstance arm = Install(context.Assembly,
                    "trail-arm-" + binding.CornerId,
                    binding.TrailingArmMount.MountId);
                probes[index] = new CornerProbe(binding, supportIndex, arm);
            }

            return probes;
        }

        private static void RefreshAuthority(RearFixture context)
        {
            context.Support.RefreshSupport(force: true);
            context.Authority.ApplyNow();
            Physics.SyncTransforms();
        }

        private static PartInstance Install(
            VehicleAssemblyController assembly,
            string slug,
            string mountId)
        {
            PartInstance part = assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == mountId);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            if (part.Body != null)
            {
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
            }

            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            foreach (FastenerInstance fastener in assembly.ResolveMount(mount).Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null && value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null, "Missing lifecycle fixture fastener tool.");
                while (fastener.Stage < fastener.Definition.MaximumStage)
                {
                    result = assembly.TryOperateFastener(mountId,
                        fastener.Definition.DefinitionId, tool, tighten: true);
                    Assert.That(result.Succeeded, Is.True, result.Message);
                }
            }

            return part;
        }

        private static void LoosenAndRemove(
            VehicleAssemblyController assembly,
            PartInstance part)
        {
            MountPointRuntime mount = assembly.Graph.FindMountForPart(part);
            Assert.That(mount, Is.Not.Null);
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null && value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null, "Missing lifecycle fixture fastener tool.");
                while (fastener.Stage > 0)
                {
                    AssemblyOperationResult loosened = assembly.TryOperateFastener(
                        mount.MountId, fastener.Definition.DefinitionId, tool,
                        tighten: false);
                    Assert.That(loosened.Succeeded, Is.True, loosened.Message);
                }
            }

            AssemblyOperationResult removed = assembly.TryRemove(part);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            if (part.Body != null)
            {
                part.Body.isKinematic = true;
                part.Body.detectCollisions = false;
            }
        }

        private static void AssertPhysicalArm(
            CornerProbe probe,
            bool hasStockSpring,
            bool hasLongSpring)
        {
            AssemblyInstalledPhysicsLink link =
                probe.Arm.GetComponent<AssemblyInstalledPhysicsLink>();
            Assert.That(link, Is.Not.Null, probe.Label);
            Assert.That(probe.Arm.UsesDynamicInstalledPhysics, Is.True, probe.Label);
            Assert.That(probe.Arm.Body.isKinematic, Is.False, probe.Label);
            HingeJoint hinge = link.InstalledHinge;
            Assert.That(hinge, Is.Not.Null, probe.Label);
            Assert.That(link.InstalledJoint, Is.SameAs(hinge), probe.Label);
            Assert.That(probe.Arm.GetComponents<HingeJoint>(), Has.Length.EqualTo(1),
                probe.Label);
            AssertNoNativeMotor(hinge, probe.Label);

            Vector2 limits = SatsumaRearSuspensionTravel.ResolveArmLimits(
                probe.Binding.NeutralArmPivotLocalPosition,
                hasStockSpring,
                hasLongSpring);
            Assert.That(hinge.useLimits, Is.True, probe.Label);
            Assert.That(hinge.limits.min, Is.EqualTo(limits.x).Within(0.002f),
                probe.Label);
            Assert.That(hinge.limits.max, Is.EqualTo(limits.y).Within(0.002f),
                probe.Label);
        }

        private static void AssertNwhOwnsCorner(
            RearFixture context,
            CornerProbe probe)
        {
            Assert.That(context.Support.IsSupported(probe.SupportIndex), Is.True,
                probe.Label + ": arm plus drum must enable the donor Wheel contact.");
            Assert.That(probe.Binding.Wheel.enabled, Is.True, probe.Label);
            AssertKinematic(probe.Arm, probe.Label + " arm");
            AssertKinematic(probe.Drum, probe.Label + " drum");
        }

        private static void AssertKinematic(PartInstance part, string label)
        {
            AssemblyInstalledPhysicsLink link =
                part.GetComponent<AssemblyInstalledPhysicsLink>();
            Assert.That(link, Is.Not.Null, label);
            Assert.That(part.UsesDynamicInstalledPhysics, Is.False, label);
            Assert.That(part.Body.isKinematic, Is.True, label);
            Assert.That(link.InstalledJoint, Is.Null, label);
            Assert.That(part.GetComponents<Joint>(), Is.Empty,
                label + ": NWH must be the sole authority.");
        }

        private static void AssertStage(
            RearFixture context,
            CornerProbe[] probes,
            SpringStage stage)
        {
            float topY;
            float travel;
            float maximumForce;
            switch (stage)
            {
                case SpringStage.Stock:
                    topY = SatsumaRearSuspensionTravel.StockWheelRootY;
                    travel = SatsumaRearSuspensionTravel.StockSuspensionTravel;
                    maximumForce = SatsumaRearSuspensionForce.StockWheelRate * travel;
                    break;
                case SpringStage.Long:
                    topY = SatsumaRearSuspensionTravel.LongWheelRootY;
                    travel = SatsumaRearSuspensionTravel.LongSuspensionTravel;
                    maximumForce = SatsumaRearSuspensionForce.LongWheelRate * travel;
                    break;
                default:
                    topY = SatsumaRearSuspensionTravel.NoSpringWheelRootY;
                    travel = SatsumaRearSuspensionTravel.StockSuspensionTravel;
                    maximumForce = SatsumaRearSuspensionForce.NoSpringWheelRate * travel;
                    break;
            }

            foreach (CornerProbe probe in probes)
            {
                string label = probe.Label + "; stage=" + stage;
                AssertNwhOwnsCorner(context, probe);
                Assert.That(probe.Binding.Wheel.transform.localPosition.y,
                    Is.EqualTo(topY).Within(0.0001f), label);
                Assert.That(probe.Binding.Wheel.SpringMaxLength,
                    Is.EqualTo(travel).Within(0.0001f), label);
                Assert.That(probe.Binding.Wheel.SpringMaxForce,
                    Is.EqualTo(maximumForce).Within(0.001f), label);
                Assert.That(probe.Binding.Wheel.DamperBumpRate,
                    Is.EqualTo(SatsumaRearSuspensionForce.NoShockDamper).Within(0.001f),
                    label);
                Assert.That(probe.Binding.Wheel.DamperReboundRate,
                    Is.EqualTo(SatsumaRearSuspensionForce.NoShockDamper).Within(0.001f),
                    label);
            }
        }

        private static void AssertNoNativeMotor(HingeJoint hinge, string label)
        {
            Assert.That(hinge.useSpring, Is.False, label);
            Assert.That(hinge.spring.spring, Is.Zero, label);
            Assert.That(hinge.spring.damper, Is.Zero, label);
            Assert.That(hinge.useMotor, Is.False, label);
            Assert.That(hinge.motor.force, Is.Zero, label);
            Assert.That(hinge.motor.targetVelocity, Is.Zero, label);
        }

        private enum SpringStage
        {
            None,
            Stock,
            Long,
        }

        private sealed class RearFixture
        {
            public GameObject Root { get; }
            public VehicleAssemblyController Assembly { get; }
            public NwhAssemblyWheelSupportController Support { get; }
            public SatsumaRearNwhSuspensionController Authority { get; }

            public RearFixture(
                GameObject root,
                VehicleAssemblyController assembly,
                NwhAssemblyWheelSupportController support,
                SatsumaRearNwhSuspensionController authority)
            {
                Root = root;
                Assembly = assembly;
                Support = support;
                Authority = authority;
            }
        }

        private sealed class CornerProbe
        {
            public SatsumaRearNwhCornerBinding Binding { get; }
            public int SupportIndex { get; }
            public PartInstance Arm { get; }
            public PartInstance Drum { get; set; }
            public PartInstance Spring { get; set; }
            public string Label => "rear " + Binding.CornerId;

            public CornerProbe(
                SatsumaRearNwhCornerBinding binding,
                int supportIndex,
                PartInstance arm)
            {
                Binding = binding;
                SupportIndex = supportIndex;
                Arm = arm;
            }
        }
    }
}
