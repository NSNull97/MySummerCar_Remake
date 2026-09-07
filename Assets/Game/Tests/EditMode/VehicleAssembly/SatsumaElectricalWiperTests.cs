using System;
using System.Linq;
using System.Reflection;
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

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaElectricalWiperTests
    {
        [Test]
        public void GeneratedSatsumaContainsReviewedElectricalAndFixedWiperRig()
        {
            GameObject prefab = LoadPrefab();
            Assert.That(prefab.GetComponent<SatsumaElectricalSystem>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SatsumaWiperController>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<
                SatsumaWiringConnectorInteractionTarget>(true),
                Has.Length.EqualTo(52));
            SatsumaWiringConnectorInteractionTarget[] ends = prefab
                .GetComponentsInChildren<SatsumaWiringConnectorInteractionTarget>(true);
            Assert.That(ends.Select(value => (value.Connection, value.Endpoint)).Distinct().Count(),
                Is.EqualTo(52), "Every donor pair needs both distinct endpoints.");
            Assert.That(ends.All(value => Mathf.Approximately(
                value.GetComponent<SphereCollider>().radius,
                SatsumaElectricalSystem.DonorEndpointToleranceMeters)), Is.True);
            Assert.That(prefab.GetComponentsInChildren<
                SatsumaElectricalTerminalFastenerInteractionTarget>(true),
                Has.Length.EqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<
                SatsumaWiperSwitchInteractionTarget>(true),
                Has.Length.EqualTo(1));

            Transform fixedWipers = prefab.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "Fixed donor wipers");
            Assert.That(
                fixedWipers.GetComponentInParent<PartInstance>(),
                Is.Null,
                "Donor Satsuma wipers are fixed vehicle equipment, not a removable part.");
            Assert.That(fixedWipers.GetComponentsInChildren<MeshRenderer>(true),
                Has.Length.EqualTo(4));
        }

        [Test]
        public void CompleteDonorWiringAndTightTerminalsExposeElectricalPower()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance
                    .GetComponent<SatsumaElectricalSystem>();
                VehicleSimulationHost simulation = instance
                    .GetComponent<VehicleSimulationHost>();
                Assert.That(simulation.TryInitialize(out string initializeFailure),
                    Is.True, initializeFailure);
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                PartInstance battery = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    SatsumaElectricalSystem.BatteryPartDefinitionId);

                battery.RuntimeState.SetInstalled("test.battery", false);
                Assert.That(electrical.TryInstallConnection(
                    SatsumaElectricalConnection.BatteryHarness), Is.True);
                Assert.That(electrical.TryInstallConnection(
                    SatsumaElectricalConnection.GroundBattery), Is.True);
                Assert.That(electrical.TryInstallConnection(
                    SatsumaElectricalConnection.Ignition), Is.True);
                Assert.That(electrical.TryInstallConnection(
                    SatsumaElectricalConnection.SwitchLights), Is.True);
                for (int stage = 0;
                     stage < SatsumaElectricalSystem.TerminalMaximumStage;
                     stage++)
                {
                    Assert.That(electrical.TryTurnFastener(
                        SatsumaElectricalFastener.BatteryPositiveTerminal, 1f),
                        Is.True);
                    Assert.That(electrical.TryTurnFastener(
                        SatsumaElectricalFastener.BatteryNegativeTerminal, 1f),
                        Is.True);
                }

                Assert.That(electrical.BatteryVoltage,
                    Is.GreaterThan(SatsumaElectricalSystem.DonorMinimumUsableVoltage));
                Assert.That(electrical.ElectricsOk, Is.True);
                Assert.That(electrical.WipersPowered, Is.True);

                SatsumaElectricalSaveDto saved = electrical.CaptureSaveData();
                electrical.ResetState();
                Assert.That(electrical.ElectricsOk, Is.False);
                Assert.That(electrical.TryRestore(saved, out string failure),
                    Is.True, failure);
                Assert.That(electrical.ElectricsOk, Is.True);
                Assert.That(electrical.GetFastenerStage(
                    SatsumaElectricalFastener.BatteryPositiveTerminal),
                    Is.EqualTo(SatsumaElectricalSystem.TerminalMaximumStage));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void WireRequiresBothDistinctEndsInEitherOrder(int firstEnd)
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance
                    .GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget endpoint = instance
                    .GetComponentsInChildren<
                        SatsumaWiringConnectorInteractionTarget>(true)
                    .Single(value => value.Connection ==
                        SatsumaElectricalConnection.FrontLightsHarness &&
                        value.Endpoint == firstEnd);
                SatsumaWiringConnectorInteractionTarget other = instance
                    .GetComponentsInChildren<SatsumaWiringConnectorInteractionTarget>(true)
                    .Single(value => value.Connection == endpoint.Connection &&
                        value.Endpoint != firstEnd);

                Assert.That(endpoint.IsEndpointAvailable, Is.True);
                Assert.That(electrical.ActivateCluster(endpoint.transform.position),
                    Is.Zero, "First use only arms this end; no wire is visible or saved.");
                Assert.That(electrical.IsConnectionInstalled(endpoint.Connection), Is.False);
                Assert.That(endpoint.IsEndpointAvailable, Is.False);
                Assert.That(electrical.IsEndpointArmed(endpoint.Connection, firstEnd), Is.True);
                Assert.That(other.ToolPrompt, Is.EqualTo("СОЕДИНИТЬ ПРОВОД"));
                Assert.That(electrical.ActivateCluster(endpoint.transform.position), Is.Zero,
                    "Repeated use at the same end must not complete its own wire.");
                Assert.That(electrical.ActivateCluster(other.transform.position),
                    Is.EqualTo(1));
                Assert.That(electrical.IsConnectionInstalled(
                    SatsumaElectricalConnection.FrontLightsHarness), Is.True);
                Assert.That(other.IsEndpointAvailable, Is.False);
                Assert.That(electrical.CaptureSaveData().installedConnectionIds,
                    Is.EquivalentTo(new[] { endpoint.Connection.ToString() }));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HarnessCanBeSelectedBeforeTheOppositePartIsInstalled()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget fuse = FindEndpoint(
                    instance, SatsumaElectricalConnection.Dash1, 0);
                SatsumaWiringConnectorInteractionTarget panel = FindEndpoint(
                    instance, SatsumaElectricalConnection.Dash1, 1);
                Assert.That(panel.ArePartRequirementsMet, Is.False);
                Assert.That(electrical.CanActivateCluster(fuse.transform.position), Is.True);
                Assert.That(electrical.ActivateCluster(fuse.transform.position), Is.Zero);
                Assert.That(electrical.IsEndpointArmed(fuse.Connection, fuse.Endpoint), Is.True);
                Assert.That(electrical.CaptureSaveData().installedConnectionIds, Is.Empty);

                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                InstallForTest(assembly, "vehicle.satsuma.part.dashboard");
                InstallForTest(assembly, "vehicle.satsuma.part.dashboard-meters");
                Assert.That(electrical.ActivateCluster(panel.transform.position), Is.EqualTo(1));
                Assert.That(electrical.IsConnectionInstalled(fuse.Connection), Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void CompletionResetsOtherPendingEndsAndCannotConnectAnUnrelatedPair()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget main = FindEndpoint(
                    instance, SatsumaElectricalConnection.FrontLightsHarness, 0);
                SatsumaWiringConnectorInteractionTarget front = FindEndpoint(
                    instance, main.Connection, 1);
                SatsumaWiringConnectorInteractionTarget regulator = FindEndpoint(
                    instance, SatsumaElectricalConnection.RegulatorHarness, 1);
                Assert.That(electrical.ActivateCluster(main.transform.position), Is.Zero);
                Assert.That(electrical.IsEndpointArmed(regulator.Connection, 0), Is.True,
                    "One use can arm multiple close harness ends, not complete them.");
                Assert.That(electrical.ActivateCluster(front.transform.position), Is.EqualTo(1));
                Assert.That(electrical.IsEndpointArmed(regulator.Connection, 0), Is.False,
                    "Donor RESETWIRING clears the other selections after a completed wire.");
                Assert.That(electrical.ActivateCluster(regulator.transform.position), Is.Zero);
                Assert.That(electrical.IsConnectionInstalled(regulator.Connection), Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void PendingEndsAreNotSavedAndRestoreDoesNotCompleteThem()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget main = FindEndpoint(
                    instance, SatsumaElectricalConnection.FrontLightsHarness, 0);
                SatsumaWiringConnectorInteractionTarget front = FindEndpoint(instance, main.Connection, 1);
                electrical.ActivateCluster(main.transform.position);
                SatsumaElectricalSaveDto save = electrical.CaptureSaveData();
                Assert.That(save.installedConnectionIds, Is.Empty);
                Assert.That(electrical.TryRestore(save, out string failure), Is.True, failure);
                Assert.That(electrical.IsEndpointArmed(main.Connection, 0), Is.False);
                Assert.That(electrical.ActivateCluster(front.transform.position), Is.Zero);
                Assert.That(electrical.IsConnectionInstalled(main.Connection), Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void RemovedPartCannotLeaveAnArmedEndThatCompletesLater()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                InstallForTest(assembly, "vehicle.satsuma.part.dashboard");
                InstallForTest(assembly, "vehicle.satsuma.part.dashboard-meters");
                SatsumaWiringConnectorInteractionTarget panel = FindEndpoint(
                    instance, SatsumaElectricalConnection.Dash1, 1);
                SatsumaWiringConnectorInteractionTarget fuse = FindEndpoint(instance, panel.Connection, 0);
                Assert.That(electrical.ActivateCluster(panel.transform.position), Is.Zero);
                PartInstance dashboard = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == "vehicle.satsuma.part.dashboard");
                dashboard.RuntimeState.SetLoose(dashboard.transform.position, dashboard.transform.rotation);
                // Observe removal on a game tick, without another wiring click.
                typeof(SatsumaElectricalSystem).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(electrical, null);
                InstallForTest(assembly, "vehicle.satsuma.part.dashboard");
                Assert.That(electrical.ActivateCluster(fuse.transform.position), Is.Zero);
                Assert.That(electrical.IsEndpointArmed(panel.Connection, panel.Endpoint), Is.False);
                Assert.That(electrical.IsConnectionInstalled(panel.Connection), Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void EndpointRayAcceptsWiringMessOnlyAndUsesReviewedTolerance()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            GameObject rayOwner = new GameObject("Wiring test ray");
            GameObject toolOwner = new GameObject("Wiring test tool");
            SatsumaWiringConnectorInteractionTarget endpoint = null;
            try
            {
                endpoint = FindEndpoint(
                    instance, SatsumaElectricalConnection.FrontLightsHarness, 0);
                // Isolate the ray/filter geometry from the interpolated car
                // Rigidbody's cached physics pose in an EditMode fixture.
                endpoint.transform.SetParent(null, true);
                endpoint.transform.position = new Vector3(2000f, 100f, 2000f);
                endpoint.GetComponent<Collider>().enabled = true;
                rayOwner.transform.position = endpoint.transform.position + new Vector3(0.075f, 0f, -1f);
                RaycastInteractionCandidateSource query = rayOwner.AddComponent<RaycastInteractionCandidateSource>();
                query.Configure(rayOwner.transform, 2f, ~0);
                toolOwner.AddComponent<Rigidbody>().isKinematic = true;
                WiringTestPickup tool = toolOwner.AddComponent<WiringTestPickup>();
                toolOwner.AddComponent<InteractionTargetHost>().Configure(tool);
                toolOwner.transform.position = endpoint.transform.position;
                Physics.SyncTransforms();
                Assert.That(query.Query().IsValid, Is.False, "An empty hand must not select wire triggers.");
                query.SetCarriedObjectTarget(tool);
                tool.Type = "Spanner";
                Assert.That(query.Query().IsValid, Is.False);
                tool.Type = "Wiring";
                var context = new InteractionContext(rayOwner, rayOwner.transform.position, rayOwner.transform.forward);
                Assert.That(endpoint.CanSelectForCarriedObject(tool, context), Is.True,
                    "A real wiring identity within the endpoint's reach must pass the carried-object filter.");
                InteractionCandidate candidate = query.Query();
                Assert.That(candidate.TryGetCapability(out SatsumaWiringConnectorInteractionTarget selected), Is.True,
                    $"available={endpoint.IsEndpointAvailable}, bounds={endpoint.GetComponent<Collider>().bounds}, " +
                    $"origin={rayOwner.transform.position}, hit={query.HasLastHit}, " +
                    $"hitCollider={query.LastHit.collider}, hitDistance={query.LastHit.distance}");
                Assert.That(selected, Is.SameAs(endpoint), "75 mm off-axis was outside the old 28 mm sphere.");
            }
            finally
            {
                Object.DestroyImmediate(toolOwner);
                Object.DestroyImmediate(rayOwner);
                if (endpoint != null) Object.DestroyImmediate(endpoint.gameObject);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void DisabledEndpointCannotBeArmedByClusterScan()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget front = FindEndpoint(
                    instance, SatsumaElectricalConnection.FrontLightsHarness, 1);
                front.enabled = false;
                electrical.ActivateCluster(front.transform.position);
                Assert.That(electrical.IsEndpointArmed(front.Connection, front.Endpoint), Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void OneUseCanReachBothDistinctEndsWhenDonorEndpointsPhysicallyOverlap()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaElectricalSystem electrical = instance.GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget first = FindEndpoint(
                    instance, SatsumaElectricalConnection.MarkerRight, 0);
                SatsumaWiringConnectorInteractionTarget second = FindEndpoint(instance, first.Connection, 1);
                // This optional part is not generated yet. Isolate the reviewed
                // overlapping endpoint geometry without pretending it is playable.
                first.Configure(electrical, first.Connection, 0, first.InteractionDisplayName);
                second.Configure(electrical, second.Connection, 1, second.InteractionDisplayName);
                Assert.That(Vector3.Distance(first.transform.position, second.transform.position),
                    Is.LessThan(SatsumaElectricalSystem.DonorEndpointToleranceMeters));
                Assert.That(electrical.ActivateCluster(first.transform.position), Is.EqualTo(1));
                Assert.That(electrical.IsConnectionInstalled(first.Connection), Is.True,
                    "The handshake requires both ends, not an arbitrary minimum number of key presses.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void MotorEndpointRequiresEngineBlockAndItsComponent()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                SatsumaWiringConnectorInteractionTarget starterEndpoint = instance
                    .GetComponentsInChildren<
                        SatsumaWiringConnectorInteractionTarget>(true)
                    .Single(value => value.Connection ==
                        SatsumaElectricalConnection.Starter && value.Endpoint == 0);

                Assert.That(starterEndpoint.ArePartRequirementsMet, Is.False);
                InstallForTest(assembly, "vehicle.satsuma.part.engine-block");
                InstallForTest(assembly, "vehicle.satsuma.part.starter");
                Assert.That(starterEndpoint.ArePartRequirementsMet, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void DonorGroundLeadRequiresLoosenedStarterMountingBoltNotCableBolt()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                SatsumaElectricalSystem electrical = instance
                    .GetComponent<SatsumaElectricalSystem>();
                SatsumaWiringConnectorInteractionTarget groundStarterEndpoint =
                    instance.GetComponentsInChildren<
                            SatsumaWiringConnectorInteractionTarget>(true)
                        .Single(value => value.Connection ==
                            SatsumaElectricalConnection.GroundBattery &&
                            value.Endpoint == 1);

                const string starterMountId = "mount.satsuma.engine-plate.starter";
                const string mountingBoltId = "fastener.satsuma.engine-plate-starter.boltpm-1";
                VehicleAssemblySaveData assemblySave = assembly.CaptureSaveData();
                PartSaveDto starter = assemblySave.parts.Single(value =>
                    value.partDefinitionId == "vehicle.satsuma.part.starter");
                starter.lifecycleState = PartLifecycleState.Installed;
                starter.installedMountId = starterMountId;
                assemblySave.mounts.Single(value => value.mountId == starterMountId)
                    .installedPartStableEntityId = starter.stableEntityId;
                AssemblyOperationResult restored = assembly.RestoreSaveData(assemblySave);
                Assert.That(restored.Succeeded, Is.True, restored.Message);
                InstallForTest(assembly, "vehicle.satsuma.part.engine-block");
                InstallForTest(assembly, SatsumaElectricalSystem.BatteryPartDefinitionId);
                Assert.That(assembly.Graph.TryGetMount(starterMountId, out MountPointRuntime mount), Is.True);
                Assert.That(mount.TryGetFastener(mountingBoltId, out FastenerInstance mountingBolt), Is.True);
                Assert.That(groundStarterEndpoint.ArePartRequirementsMet, Is.True,
                    "An untightened mounting screw accepts the ground lead even without a positive cable.");

                Assert.That(mountingBolt.TryRestore(true, true, 8), Is.True);
                Assert.That(groundStarterEndpoint.ArePartRequirementsMet, Is.False);

                Assert.That(electrical.TryInstallConnection(SatsumaElectricalConnection.Starter), Is.True);
                for (int stage = 0;
                     stage < SatsumaElectricalSystem.FastenerMaximumStage;
                     stage++)
                {
                    Assert.That(electrical.TryTurnFastener(
                        SatsumaElectricalFastener.StarterCable, 1f), Is.True);
                }

                Assert.That(groundStarterEndpoint.ArePartRequirementsMet, Is.False,
                    "Tightening the separate positive cable cannot unlock the ground connector.");
                Assert.That(mountingBolt.TryRestore(true, true, 7), Is.True);
                Assert.That(groundStarterEndpoint.ArePartRequirementsMet, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void UnpoweredSwitchChangesModeButDoesNotStartSweep()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaWiperController wipers = instance
                    .GetComponent<SatsumaWiperController>();
                Assert.That(wipers.Mode, Is.EqualTo(SatsumaWiperMode.Off));

                wipers.CycleMode();
                wipers.Simulate(0.25f);

                Assert.That(wipers.Mode, Is.EqualTo(SatsumaWiperMode.Slow));
                Assert.That(wipers.IsCycleActive, Is.False);
                Assert.That(wipers.Sweep01, Is.Zero.Within(0.0001f));
                SatsumaWiperSwitchInteractionTarget switchTarget = instance
                    .GetComponentInChildren<SatsumaWiperSwitchInteractionTarget>(true);
                Assert.That(
                    Quaternion.Angle(
                        switchTarget.transform.localRotation,
                        wipers.SwitchKnobBaseLocalRotation * Quaternion.Euler(0f, -45f, 0f)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SweepAlreadyInProgressReturnsToParkAfterPowerLoss()
        {
            GameObject instance = Object.Instantiate(LoadPrefab());
            try
            {
                SatsumaWiperController wipers = instance
                    .GetComponent<SatsumaWiperController>();
                var inProgress = new SatsumaWiperSaveDto
                {
                    mode = (int)SatsumaWiperMode.Off,
                    cycleActive = true,
                    cycleTimeSeconds = 0.25f,
                };
                Assert.That(wipers.TryRestore(inProgress, out string failure),
                    Is.True, failure);
                Assert.That(wipers.Sweep01, Is.EqualTo(0.5f).Within(0.0001f));

                wipers.Simulate(0.75f);

                Assert.That(wipers.IsCycleActive, Is.False);
                Assert.That(wipers.CycleTimeSeconds, Is.Zero.Within(0.0001f));
                Assert.That(wipers.Sweep01, Is.Zero.Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VehicleSaveRecordAcceptsOptionalElectricalExtensions()
        {
            var electrical = new SatsumaElectricalSaveDto
            {
                installedConnectionIds = new[]
                {
                    SatsumaElectricalConnection.BatteryHarness.ToString(),
                },
                batteryPlusStage = SatsumaElectricalSystem.TerminalMaximumStage,
            };
            var wipers = new SatsumaWiperSaveDto
            {
                mode = (int)SatsumaWiperMode.Fast,
                cycleActive = true,
                cycleTimeSeconds = 0.4f,
            };

            Assert.That(electrical.TryValidate(out string electricalFailure),
                Is.True, electricalFailure);
            Assert.That(wipers.TryValidate(out string wiperFailure),
                Is.True, wiperFailure);

            electrical.installedConnectionIds = Array.Empty<string>();
            Assert.That(electrical.TryValidate(out _), Is.False);
            wipers.cycleTimeSeconds = float.NaN;
            Assert.That(wipers.TryValidate(out _), Is.False);
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }

        private static SatsumaWiringConnectorInteractionTarget FindEndpoint(
            GameObject instance, SatsumaElectricalConnection connection, int endpoint) =>
            instance.GetComponentsInChildren<SatsumaWiringConnectorInteractionTarget>(true)
                .Single(value => value.Connection == connection && value.Endpoint == endpoint);

        private static void InstallForTest(
            VehicleAssemblyController assembly,
            string definitionId)
        {
            PartInstance part = assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId == definitionId);
            part.RuntimeState.SetInstalled("test." + definitionId, false);
        }
    }

    public sealed class WiringTestPickup : MonoBehaviour, IPickupTarget, IHeldToolIdentity
    {
        public string Type = "Wiring";
        public string ToolType => Type;
        public string ToolVariant => "mess";
        public string PickupPrompt => "Test wiring";
        public Rigidbody Body => GetComponent<Rigidbody>();
        public StableEntityId StableId => default;
        public bool CanPickup(in InteractionContext context) => true;
        public void NotifyPickedUp(in InteractionContext context) { }
        public void NotifyReleased(PickupReleaseReason reason) { }
    }
}
