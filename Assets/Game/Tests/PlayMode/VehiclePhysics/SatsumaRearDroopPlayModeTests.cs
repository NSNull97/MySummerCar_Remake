using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NWH.WheelController3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaRearDroopPlayModeTests
    {
        private const int AirbornePhysicsSteps = 120;
        private const float CompressionToleranceMeters = 0.0005f;
        private const float ArmAngleToleranceDegrees = 0.02f;
        private const float ShockSeparationToleranceMeters = 0.001f;

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
        public IEnumerator NoSpringAirborneNwhDroopHasZeroCompressionAndDonorMinimum() =>
            RunAirborneDroop(SpringConfiguration.None);

        [UnityTest]
        public IEnumerator StockSpringAndShockAirborneNwhDroopHasZeroCompressionAndDonorMinimum() =>
            RunAirborneDroop(SpringConfiguration.Stock);

        [UnityTest]
        public IEnumerator LongSpringAndStockShockAirborneNwhDroopHasZeroCompressionAndDonorMinimum() =>
            RunAirborneDroop(SpringConfiguration.Long);

        [UnityTest]
        public IEnumerator LiveSpringChangesUpdateSameNwhWheelTravelTopAndKinematicArm()
        {
            RearFixture context = CreateFixture();
            InstallRearStructure(context, SpringConfiguration.None);
            RefreshAuthority(context);
            CornerProbe[] probes = CreateProbes(context);

            SpringConfiguration[] sequence =
            {
                SpringConfiguration.None,
                SpringConfiguration.Stock,
                SpringConfiguration.Long,
            };
            foreach (SpringConfiguration configuration in sequence)
            {
                foreach (CornerProbe probe in probes)
                {
                    RemoveShockIfInstalled(
                        context.Assembly,
                        probe.PresentationCorner.ShockMountId);
                    RemoveSpringIfInstalled(
                        context.Assembly,
                        probe.Binding.StockSpringMountId);
                    RemoveSpringIfInstalled(
                        context.Assembly,
                        probe.Binding.LongSpringMountId);
                    InstallSpring(context.Assembly, probe.Binding, configuration);
                    Install(
                        context.Assembly,
                        "shock-absorber-" +
                        (probe.Binding.CornerId == "rl" ? "1" : "2"),
                        probe.PresentationCorner.ShockMountId);
                }

                RefreshAuthority(context);
                foreach (CornerProbe probe in probes)
                {
                    AssertLiveIdentity(context, probe, configuration);
                }

                yield return ObserveAirborne(context, probes, configuration);
                foreach (CornerProbe probe in probes)
                {
                    AssertLiveIdentity(context, probe, configuration);
                    AssertAirborneProfile(context, probe, configuration);
                }
            }
        }

        private IEnumerator RunAirborneDroop(SpringConfiguration configuration)
        {
            RearFixture context = CreateFixture();
            InstallRearStructure(context, configuration);
            RefreshAuthority(context);
            CornerProbe[] probes = CreateProbes(context);

            yield return ObserveAirborne(context, probes, configuration);
            foreach (CornerProbe probe in probes)
            {
                AssertAirborneProfile(context, probe, configuration);
            }
        }

        private static IEnumerator ObserveAirborne(
            RearFixture context,
            CornerProbe[] probes,
            SpringConfiguration configuration)
        {
            foreach (CornerProbe probe in probes)
            {
                probe.ResetStageMeasurements();
                probe.MeasureAndLog(
                    context.Root.transform,
                    "before physics",
                    configuration);
            }

            for (int step = 1; step <= AirbornePhysicsSteps; step++)
            {
                yield return new WaitForFixedUpdate();
                if (step % 30 != 0)
                {
                    continue;
                }

                context.Authority.ApplyNow();
                Physics.SyncTransforms();
                foreach (CornerProbe probe in probes)
                {
                    probe.MeasureAndLog(
                        context.Root.transform,
                        "after " + step + " physics steps",
                        configuration);
                }
            }

            context.Authority.ApplyNow();
            Physics.SyncTransforms();
        }

        private RearFixture CreateFixture()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            LegacySatsumaBaselineMetadata metadata = prefab
                .GetComponent<LegacySatsumaBaselineMetadata>();
            Assert.That(metadata, Is.Not.Null);
            fixture = UnityEngine.Object.Instantiate(
                prefab,
                new Vector3(0f, 50f, 0f),
                metadata.DefaultWorldRotation);

            Rigidbody chassis = fixture.GetComponent<Rigidbody>();
            Assert.That(chassis, Is.Not.Null);
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleSimulationHost simulation = fixture
                .GetComponent<VehicleSimulationHost>();
            if (simulation != null)
            {
                simulation.enabled = false;
            }

            VehicleAssemblyController assembly = fixture
                .GetComponent<VehicleAssemblyController>();
            NwhAssemblyWheelSupportController support = fixture
                .GetComponent<NwhAssemblyWheelSupportController>();
            SatsumaRearNwhSuspensionController authority = fixture
                .GetComponent<SatsumaRearNwhSuspensionController>();
            SatsumaRearSuspensionController presentation = fixture
                .GetComponent<SatsumaRearSuspensionController>();
            Assert.That(assembly, Is.Not.Null);
            Assert.That(support, Is.Not.Null);
            Assert.That(authority, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(authority.Corners, Has.Length.EqualTo(2));
            Assert.That(presentation.Corners.Count, Is.EqualTo(2));
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
            return new RearFixture(
                fixture,
                assembly,
                support,
                authority,
                presentation);
        }

        private static void InstallRearStructure(
            RearFixture context,
            SpringConfiguration configuration)
        {
            foreach (SatsumaRearNwhCornerBinding binding in
                     context.Authority.Corners)
            {
                SatsumaRearSuspensionCornerBinding presentationCorner =
                    FindPresentationCorner(context.Presentation, binding.CornerId);
                string suffix = binding.CornerId == "rl" ? "1" : "2";
                Install(
                    context.Assembly,
                    "trail-arm-" + binding.CornerId,
                    binding.TrailingArmMount.MountId);
                Install(
                    context.Assembly,
                    "drum-brake-" + suffix,
                    binding.DrumMountId);
                InstallSpring(context.Assembly, binding, configuration);
                Install(
                    context.Assembly,
                    "shock-absorber-" + suffix,
                    presentationCorner.ShockMountId);
            }
        }

        private static void InstallSpring(
            VehicleAssemblyController assembly,
            SatsumaRearNwhCornerBinding binding,
            SpringConfiguration configuration)
        {
            if (configuration == SpringConfiguration.None)
            {
                return;
            }

            string suffix = binding.CornerId == "rl" ? "1" : "2";
            bool useLongSpring = configuration == SpringConfiguration.Long;
            Install(
                assembly,
                (useLongSpring
                    ? "extra-long-coil-spring-"
                    : "coil-spring-") + suffix,
                useLongSpring
                    ? binding.LongSpringMountId
                    : binding.StockSpringMountId);
        }

        private static CornerProbe[] CreateProbes(RearFixture context)
        {
            return context.Authority.Corners
                .Select(binding => new CornerProbe(
                    context,
                    binding,
                    FindPresentationCorner(
                        context.Presentation,
                        binding.CornerId)))
                .ToArray();
        }

        private static void RefreshAuthority(RearFixture context)
        {
            context.Support.RefreshSupport(force: true);
            context.Authority.ApplyNow();
            Physics.SyncTransforms();
        }

        private static void AssertAirborneProfile(
            RearFixture context,
            CornerProbe probe,
            SpringConfiguration configuration)
        {
            StageExpectation expected = GetExpectation(
                probe.Binding.CornerId,
                configuration);
            string label = probe.Label + "; configuration=" + configuration;
            float compression = probe.Wheel.SpringMaxLength -
                probe.Wheel.SpringLength;

            Assert.That(context.Support.IsSupported(probe.SupportIndex), Is.True,
                label + ": arm plus drum must retain NWH authority.");
            Assert.That(probe.Wheel.enabled, Is.True, label);
            Assert.That(probe.Wheel.IsGrounded, Is.False,
                label + ": the fixture is 50 metres above all ground.");
            Assert.That(compression,
                Is.EqualTo(0f).Within(CompressionToleranceMeters),
                label + ": airborne donor compression c must be zero.");
            Assert.That(probe.Wheel.SpringLength,
                Is.EqualTo(expected.TravelMeters).Within(
                    CompressionToleranceMeters),
                label + ": airborne spring length must be full travel.");
            Assert.That(probe.Wheel.SpringMaxLength,
                Is.EqualTo(expected.TravelMeters).Within(0.0001f),
                label + ": donor travel.");
            Assert.That(probe.Wheel.SpringMaxForce,
                Is.EqualTo(expected.MaximumForceNewtons).Within(0.001f),
                label + ": donor linear spring maximum force.");
            Assert.That(
                Vector3.Distance(
                    probe.Wheel.transform.localPosition,
                    expected.TopLocalPosition),
                Is.LessThan(0.00001f),
                label + ": donor wheel top.");
            Assert.That(probe.Wheel.DamperBumpRate,
                Is.EqualTo(1000f).Within(0.001f),
                label + ": fitted donor shock bump rate.");
            Assert.That(probe.Wheel.DamperReboundRate,
                Is.EqualTo(1000f).Within(0.001f),
                label + ": fitted donor shock rebound rate.");

            Assert.That(probe.Arm.IsInstalled, Is.True, label);
            Assert.That(probe.Drum.IsInstalled, Is.True, label);
            Assert.That(probe.Shock.IsInstalled, Is.True, label);
            Assert.That(
                ResolveInstalledPart(
                    context.Assembly,
                    probe.Binding.StockSpringMountId) != null,
                Is.EqualTo(configuration == SpringConfiguration.Stock),
                label + ": stock spring occupancy.");
            Assert.That(
                ResolveInstalledPart(
                    context.Assembly,
                    probe.Binding.LongSpringMountId) != null,
                Is.EqualTo(configuration == SpringConfiguration.Long),
                label + ": long spring occupancy.");
            AssertNwhKinematic(probe.Arm, label + " arm");
            AssertNwhKinematic(probe.Drum, label + " drum");

            float resolvedAngle = SatsumaRearSuspensionTravel.ResolveArmAngle(
                probe.Binding.NeutralArmPivotLocalPosition,
                0f,
                expected.HasStockSpring,
                expected.HasLongSpring);
            Assert.That(resolvedAngle,
                Is.EqualTo(expected.MinimumArmAngleDegrees).Within(0.0001f),
                label + ": frozen donor minimum angle.");
            float appliedAngle = ResolveAppliedArmAngle(
                context.Root.transform,
                probe.Binding);
            Assert.That(appliedAngle,
                Is.EqualTo(expected.MinimumArmAngleDegrees).Within(
                    ArmAngleToleranceDegrees),
                label + ": applied kinematic arm angle at c=0.");

            Quaternion expectedRotation = context.Root.transform.rotation *
                Quaternion.AngleAxis(
                    expected.MinimumArmAngleDegrees,
                    Vector3.right) *
                probe.Binding.NeutralArmLocalRotation;
            Assert.That(
                Quaternion.Angle(
                    probe.Binding.TrailingArmMount.Pose.rotation,
                    expectedRotation),
                Is.LessThan(ArmAngleToleranceDegrees),
                label + ": trailing-arm mount rotation.");
            Assert.That(
                Quaternion.Angle(probe.Arm.transform.rotation, expectedRotation),
                Is.LessThan(ArmAngleToleranceDegrees),
                label + ": installed arm must follow the NWH IK mount.");

            Assert.That(probe.MaximumAxialGap,
                Is.LessThanOrEqualTo(ShockSeparationToleranceMeters),
                label + ": the fitted shock halves separated; maximum gap=" +
                probe.MaximumAxialGap);
            Assert.That(probe.LastAxialGap, Is.LessThan(0f),
                label + ": the settled shock meshes must retain real overlap.");
        }

        private static void AssertLiveIdentity(
            RearFixture context,
            CornerProbe probe,
            SpringConfiguration configuration)
        {
            string label = probe.Label + "; configuration=" + configuration;
            SatsumaRearNwhCornerBinding currentBinding = context.Authority.Corners
                .Single(value => value.CornerId == probe.Binding.CornerId);
            Assert.That(currentBinding.Wheel, Is.SameAs(probe.Wheel),
                label + ": changing the spring must not replace WheelController.");
            Assert.That(currentBinding.TrailingArmMount,
                Is.SameAs(probe.Binding.TrailingArmMount),
                label + ": changing the spring must not replace the arm mount.");
            Assert.That(
                context.Support.Bindings[probe.SupportIndex].Wheel,
                Is.SameAs(probe.Wheel),
                label + ": support binding changed WheelController.");
            Assert.That(
                ResolveInstalledPart(
                    context.Assembly,
                    probe.Binding.TrailingArmMount.MountId),
                Is.SameAs(probe.Arm),
                label + ": changing the spring replaced the installed arm.");
        }

        private static void AssertNwhKinematic(PartInstance part, string label)
        {
            AssemblyInstalledPhysicsLink link = part
                .GetComponent<AssemblyInstalledPhysicsLink>();
            Assert.That(link, Is.Not.Null, label);
            Assert.That(part.UsesDynamicInstalledPhysics, Is.False, label);
            Assert.That(part.Body, Is.Not.Null, label);
            Assert.That(part.Body.isKinematic, Is.True, label);
            Assert.That(link.InstalledJoint, Is.Null, label);
            Assert.That(link.InstalledHinge, Is.Null, label);
            Assert.That(part.GetComponents<Joint>(), Is.Empty,
                label + ": NWH must remain the only rear suspension solver.");
        }

        private static float ResolveAppliedArmAngle(
            Transform vehicleRoot,
            SatsumaRearNwhCornerBinding binding)
        {
            Quaternion isolatedAngle = Quaternion.Inverse(vehicleRoot.rotation) *
                binding.TrailingArmMount.Pose.rotation *
                Quaternion.Inverse(binding.NeutralArmLocalRotation);
            return Mathf.DeltaAngle(0f, isolatedAngle.eulerAngles.x);
        }

        private static StageExpectation GetExpectation(
            string cornerId,
            SpringConfiguration configuration)
        {
            bool isRight = cornerId == "rr";
            float topX = isRight ? 0.603001f : -0.6029993f;
            float topZ = isRight ? -1.1669996f : -1.1670003f;
            switch (configuration)
            {
                case SpringConfiguration.Stock:
                    return new StageExpectation(
                        new Vector3(topX, -0.165f, topZ),
                        0.14f,
                        2968f,
                        isRight ? -16.1380705f : -16.1380754f,
                        hasStockSpring: true,
                        hasLongSpring: false);
                case SpringConfiguration.Long:
                    return new StageExpectation(
                        new Vector3(topX, -0.18f, topZ),
                        0.17f,
                        4930f,
                        isRight ? -23.4857439f : -23.4857507f,
                        hasStockSpring: false,
                        hasLongSpring: true);
                default:
                    return new StageExpectation(
                        new Vector3(topX, -0.15f, topZ),
                        0.14f,
                        0.28f,
                        isRight ? -13.5481621f : -13.5481663f,
                        hasStockSpring: false,
                        hasLongSpring: false);
            }
        }

        private static void Install(
            VehicleAssemblyController assembly,
            string slug,
            string mountId)
        {
            PartInstance part = FindPart(assembly, slug);
            MountPointAuthoring mount = FindMount(assembly, mountId);
            part.transform.SetPositionAndRotation(
                mount.Pose.position,
                mount.Pose.rotation);
            if (part.Body != null)
            {
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
            }

            AssemblyOperationResult installed = assembly.TryInstall(part, mount);
            Assert.That(installed.Succeeded, Is.True, installed.Message);
            TightenFully(assembly, assembly.ResolveMount(mount));
        }

        private static void TightenFully(
            VehicleAssemblyController assembly,
            MountPointRuntime mount)
        {
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null && value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null,
                    "No droop fixture tool for " +
                    fastener.Definition.DefinitionId);
                while (fastener.Stage < fastener.Definition.MaximumStage)
                {
                    AssemblyOperationResult tightened =
                        assembly.TryOperateFastener(
                            mount.MountId,
                            fastener.Definition.DefinitionId,
                            tool,
                            tighten: true);
                    Assert.That(
                        tightened.Succeeded,
                        Is.True,
                        tightened.Message);
                }
            }
        }

        private static void RemoveSpringIfInstalled(
            VehicleAssemblyController assembly,
            string mountId)
        {
            PartInstance spring = ResolveInstalledPart(assembly, mountId);
            if (spring == null)
            {
                return;
            }

            MountPointRuntime mount = assembly.Graph.FindMountForPart(spring);
            Assert.That(mount, Is.Not.Null, mountId);
            Assert.That(mount.Fasteners, Is.Empty,
                "This fixture must not bypass spring fasteners.");
            AssemblyOperationResult removed = assembly.TryRemove(spring);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            if (spring.Body != null)
            {
                spring.Body.isKinematic = true;
                spring.Body.detectCollisions = false;
            }
        }

        private static void RemoveShockIfInstalled(
            VehicleAssemblyController assembly,
            string mountId)
        {
            PartInstance shock = ResolveInstalledPart(assembly, mountId);
            if (shock == null)
            {
                return;
            }

            MountPointRuntime mount = assembly.Graph.FindMountForPart(shock);
            Assert.That(mount, Is.Not.Null, mountId);
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null && value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null,
                    "No droop fixture tool for " +
                    fastener.Definition.DefinitionId);
                while (fastener.Stage > 0)
                {
                    AssemblyOperationResult loosened =
                        assembly.TryOperateFastener(
                            mount.MountId,
                            fastener.Definition.DefinitionId,
                            tool,
                            tighten: false);
                    Assert.That(loosened.Succeeded, Is.True, loosened.Message);
                }
            }

            Assert.That(mount.FastenerGroup.IsBolted, Is.False, mountId);
            AssemblyOperationResult removed = assembly.TryRemove(shock);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
        }

        private static PartInstance ResolveInstalledPart(
            VehicleAssemblyController assembly,
            string mountId)
        {
            MountPointRuntime mount = assembly.ResolveMount(
                FindMount(assembly, mountId));
            return mount.IsOccupied ? mount.InstalledPart : null;
        }

        private static PartInstance FindPart(
            VehicleAssemblyController assembly,
            string slug) =>
            assembly.Parts.Single(part =>
                part.Definition != null &&
                part.Definition.DefinitionId ==
                    "vehicle.satsuma.part." + slug);

        private static MountPointAuthoring FindMount(
            VehicleAssemblyController assembly,
            string mountId) =>
            assembly.MountPoints.Single(mount => mount.MountId == mountId);

        private static SatsumaRearSuspensionCornerBinding FindPresentationCorner(
            SatsumaRearSuspensionController presentation,
            string cornerId) =>
            presentation.Corners.Single(corner => corner.CornerId == cornerId);

        private enum SpringConfiguration
        {
            None,
            Stock,
            Long,
        }

        private readonly struct StageExpectation
        {
            public Vector3 TopLocalPosition { get; }
            public float TravelMeters { get; }
            public float MaximumForceNewtons { get; }
            public float MinimumArmAngleDegrees { get; }
            public bool HasStockSpring { get; }
            public bool HasLongSpring { get; }

            public StageExpectation(
                Vector3 topLocalPosition,
                float travelMeters,
                float maximumForceNewtons,
                float minimumArmAngleDegrees,
                bool hasStockSpring,
                bool hasLongSpring)
            {
                TopLocalPosition = topLocalPosition;
                TravelMeters = travelMeters;
                MaximumForceNewtons = maximumForceNewtons;
                MinimumArmAngleDegrees = minimumArmAngleDegrees;
                HasStockSpring = hasStockSpring;
                HasLongSpring = hasLongSpring;
            }
        }

        private sealed class RearFixture
        {
            public GameObject Root { get; }
            public VehicleAssemblyController Assembly { get; }
            public NwhAssemblyWheelSupportController Support { get; }
            public SatsumaRearNwhSuspensionController Authority { get; }
            public SatsumaRearSuspensionController Presentation { get; }

            public RearFixture(
                GameObject root,
                VehicleAssemblyController assembly,
                NwhAssemblyWheelSupportController support,
                SatsumaRearNwhSuspensionController authority,
                SatsumaRearSuspensionController presentation)
            {
                Root = root;
                Assembly = assembly;
                Support = support;
                Authority = authority;
                Presentation = presentation;
            }
        }

        private sealed class CornerProbe
        {
            private readonly Transform topMesh;
            private readonly Transform bottomMesh;
            private readonly Vector3[] topVertices;
            private readonly Vector3[] bottomVertices;

            public SatsumaRearNwhCornerBinding Binding { get; }
            public SatsumaRearSuspensionCornerBinding PresentationCorner { get; }
            public WheelController Wheel { get; }
            public int SupportIndex { get; }
            public PartInstance Arm { get; }
            public PartInstance Drum { get; }
            public PartInstance Shock { get; }
            public string Label => "rear " + Binding.CornerId;
            public float MaximumAxialGap { get; private set; }
            public float LastAxialGap { get; private set; }

            public CornerProbe(
                RearFixture context,
                SatsumaRearNwhCornerBinding binding,
                SatsumaRearSuspensionCornerBinding presentationCorner)
            {
                Binding = binding;
                PresentationCorner = presentationCorner;
                Wheel = binding.Wheel;
                Assert.That(Wheel, Is.Not.Null, Label);
                SupportIndex = Array.FindIndex(
                    context.Support.Bindings,
                    candidate => candidate.Wheel == Wheel);
                Assert.That(SupportIndex, Is.GreaterThanOrEqualTo(0),
                    Label + ": rear NWH support binding is absent.");
                Assert.That(context.Support.Bindings[SupportIndex].Enabled,
                    Is.True,
                    Label + ": rear NWH support binding is disabled.");

                Arm = ResolveInstalledPart(
                    context.Assembly,
                    binding.TrailingArmMount.MountId);
                Drum = ResolveInstalledPart(
                    context.Assembly,
                    binding.DrumMountId);
                Shock = ResolveInstalledPart(
                    context.Assembly,
                    presentationCorner.ShockMountId);
                Assert.That(Arm, Is.Not.Null, Label + " arm");
                Assert.That(Drum, Is.Not.Null, Label + " drum");
                Assert.That(Shock, Is.Not.Null, Label + " shock");

                SatsumaRearSuspensionPartPresentation presentation = Shock
                    .GetComponent<SatsumaRearSuspensionPartPresentation>();
                Assert.That(presentation, Is.Not.Null, Label);
                Assert.That(presentation.IsShock, Is.True, Label);
                topMesh = ReadMeshTransform(presentation, "shockTopMesh");
                bottomMesh = ReadMeshTransform(presentation, "shockBottomMesh");
                topVertices = ReadVertices(topMesh);
                bottomVertices = ReadVertices(bottomMesh);
            }

            public void ResetStageMeasurements()
            {
                MaximumAxialGap = float.NegativeInfinity;
                LastAxialGap = float.NaN;
            }

            public void MeasureAndLog(
                Transform vehicleRoot,
                string phase,
                SpringConfiguration configuration)
            {
                Vector3 origin = PresentationCorner.ShockBottomTarget.position;
                Vector3 axis = (PresentationCorner.ShockTopTarget.position -
                    origin).normalized;
                Assert.That(
                    Mathf.Abs(Vector3.Dot(topMesh.forward, axis)),
                    Is.GreaterThan(0.999f),
                    "Top shock mesh is not coaxial: " + Label);
                Assert.That(
                    Mathf.Abs(Vector3.Dot(bottomMesh.forward, axis)),
                    Is.GreaterThan(0.999f),
                    "Bottom shock mesh is not coaxial: " + Label);
                ProjectVertices(
                    topMesh,
                    topVertices,
                    origin,
                    axis,
                    out float topMinimum,
                    out float topMaximum);
                ProjectVertices(
                    bottomMesh,
                    bottomVertices,
                    origin,
                    axis,
                    out float bottomMinimum,
                    out float bottomMaximum);

                // Positive is an empty interval between the mesh halves;
                // negative is real axial overlap. Mesh vertices avoid the
                // false overlap produced by world-axis renderer bounds.
                float axialGap = Mathf.Max(topMinimum, bottomMinimum) -
                    Mathf.Min(topMaximum, bottomMaximum);
                MaximumAxialGap = Mathf.Max(MaximumAxialGap, axialGap);
                LastAxialGap = axialGap;

                float compression = Wheel.SpringMaxLength - Wheel.SpringLength;
                float appliedArmAngle = ResolveAppliedArmAngle(
                    vehicleRoot,
                    Binding);
                float springLength = Vector3.Distance(
                    PresentationCorner.SpringTopBone.position,
                    PresentationCorner.SpringBottomAnchor.position);
                float shockLength = Vector3.Distance(
                    PresentationCorner.ShockTopTarget.position,
                    origin);
                TestContext.WriteLine(
                    $"REAR_NWH_DROOP {Binding.CornerId}; {phase}; " +
                    $"configuration={configuration}; grounded={Wheel.IsGrounded}; " +
                    $"compression={compression:F6}; travel={Wheel.SpringMaxLength:F6}; " +
                    $"armAngle={appliedArmAngle:F5}; springLength={springLength:F6}; " +
                    $"expansion={PresentationCorner.SpringExpansion01:F4}; " +
                    $"shockLength={shockLength:F6}; " +
                    $"topInterval=[{topMinimum:F6},{topMaximum:F6}]; " +
                    $"bottomInterval=[{bottomMinimum:F6},{bottomMaximum:F6}]; " +
                    $"axialMeshGap={axialGap:F6}");
            }

            private static Transform ReadMeshTransform(
                SatsumaRearSuspensionPartPresentation presentation,
                string fieldName)
            {
                FieldInfo field = typeof(
                        SatsumaRearSuspensionPartPresentation)
                    .GetField(
                        fieldName,
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null,
                    "The presentation binding changed: " + fieldName);
                Transform value = field.GetValue(presentation) as Transform;
                Assert.That(value, Is.Not.Null, fieldName);
                return value;
            }

            private static Vector3[] ReadVertices(Transform meshTransform)
            {
                MeshFilter filter = meshTransform.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null);
                Assert.That(filter.sharedMesh, Is.Not.Null);
                Assert.That(filter.sharedMesh.isReadable, Is.True,
                    "The geometry probe requires readable generated baseline meshes.");
                Vector3[] vertices = filter.sharedMesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThan(0));
                return vertices;
            }

            private static void ProjectVertices(
                Transform meshTransform,
                Vector3[] vertices,
                Vector3 origin,
                Vector3 axis,
                out float minimum,
                out float maximum)
            {
                minimum = float.PositiveInfinity;
                maximum = float.NegativeInfinity;
                foreach (Vector3 vertex in vertices)
                {
                    float projection = Vector3.Dot(
                        meshTransform.TransformPoint(vertex) - origin,
                        axis);
                    minimum = Mathf.Min(minimum, projection);
                    maximum = Mathf.Max(maximum, projection);
                }
            }
        }
    }
}
