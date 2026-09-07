using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Carrying;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaGeneratedFullEngineCompoundTests
    {
        private const string Prefix = "vehicle.satsuma.part.";
        // Explicit successful stock path, not an opportunistic dependency sort:
        // close the crankcase/head/timing covers only after their internals and
        // assemble both clutch children before attaching their shared cover.
        private static readonly string[] InstallationOrder =
        {
            "crankshaft", "main-bearing1", "main-bearing2", "main-bearing3",
            "piston1", "piston2", "piston3", "piston4", "camshaft", "camshaft-gear",
            "timing-chain", "timing-cover", "crankshaft-pulley", "water-pump", "water-pump-pulley",
            "engine-plate", "flywheel", "clutch-pressure-plate", "clutch", "clutch-cover-plate",
            "gearbox", "drive-gear", "inspection-cover", "starter", "oilpan",
            "head-gasket", "cylinder-head", "rocker-shaft", "rocker-cover", "headers",
            "carburetor", "airfilter", "alternator", "distributor", "fuel-pump",
            "radiator-hose2", "oilfilter0",
        };

        [Test]
        public void GeneratedStockEngineAssembles38PartsHas177Point6KgAndRestoresWithoutProxyIdentityLeakage()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null) Assert.Ignore("Private donor-derived Satsuma baseline is unavailable on this machine.");
            GameObject instance = Object.Instantiate(prefab);
            var player = new GameObject("full engine carry test player");
            var tools = new List<ToolDefinition>();
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                AssemblyLooseCompoundPhysics compound = instance.GetComponent<AssemblyLooseCompoundPhysics>();
                Assert.That(compound, Is.Not.Null);
                Assert.That(compound.Bindings, Has.Length.EqualTo(39));
                PartInstance block = assembly.Parts.Single(part => part.Definition.DefinitionId == Prefix + "engine-block");
                PartInstance gtCover = assembly.Parts.Single(part => part.Definition.DefinitionId == Prefix + "gt-rocker-cover-gt");
                var stock = compound.Bindings.Select(binding => binding.Part).Where(part => part != gtCover).ToArray();
                Assert.That(stock, Has.Length.EqualTo(38));
                CollectionAssert.AreEquivalent(InstallationOrder.Select(suffix => Prefix + suffix)
                    .Append(Prefix + "engine-block"), stock.Select(part => part.Definition.DefinitionId));

                var debug = player.AddComponent<AssemblyCarryDebugOverride>();
                var context = new InteractionContext(player, block.transform.position, Vector3.forward);
                compound.Refresh(true);
                Assert.That(block.Body.mass, Is.EqualTo(95f).Within(.0001f));
                Assert.That(block.PickupTarget.CanPickup(context), Is.True);
                foreach (string suffix in InstallationOrder) InstallAndTighten(assembly, suffix, tools);

                AssertCompleteCompound(assembly, compound, block, gtCover, stock);
                Assert.That(block.PickupTarget.MaximumCarryMassKilograms, Is.EqualTo(120f));
                Assert.That(block.PickupTarget.CanPickup(context), Is.False);
                // Picking through a child must enforce the same aggregate mass,
                // not the child's own mass or the old bare 95 kg block value.
                AssemblySubassemblyPickupTarget childSurface = stock.Single(part =>
                    part.Definition.DefinitionId == Prefix + "water-pump-pulley")
                    .GetComponent<AssemblySubassemblyPickupTarget>();
                Assert.That(childSurface.Body, Is.SameAs(block.Body));
                Assert.That(childSurface.CanPickup(context), Is.False);
                bool gravity = block.Body.useGravity;
                Vector3 center = block.Body.centerOfMass;
                debug.SetIgnoreAssemblyMassLimit(true);
                Assert.That(block.PickupTarget.CanPickup(context), Is.True);
                Assert.That(childSurface.CanPickup(context), Is.True);
                Assert.That(block.Body.mass, Is.EqualTo(177.6f).Within(.001f));
                Assert.That(block.Body.useGravity, Is.EqualTo(gravity));
                Assert.That(block.Body.centerOfMass, Is.EqualTo(center));
                debug.SetIgnoreAssemblyMassLimit(false);

                VehicleAssemblySaveData saved = assembly.CaptureSaveData();
                Assert.That(saved.parts.Length, Is.EqualTo(assembly.Parts.Length));
                CollectionAssert.AreEquivalent(assembly.Parts.Select(part => part.StableId.Value),
                    saved.parts.Select(part => part.stableEntityId));
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    AssemblyOperationResult restore = assembly.RestoreSaveData(saved);
                    Assert.That(restore.Succeeded, Is.True, restore.Message);
                    // Restore itself must have refreshed contacts/mass before return.
                    AssertCompleteCompound(assembly, compound, block, gtCover, stock);
                    Assert.That(Vector3.Distance(block.Body.centerOfMass, center), Is.LessThan(.00001f));
                    Assert.That(block.PickupTarget.CanPickup(context), Is.False);
                    saved = assembly.CaptureSaveData();
                }

                // A support/hoist may position an engine that is too heavy for
                // ordinary carrying. The physical docking bolts, not the pickup
                // mass gate, authorize attachment of the complete assembly.
                InstallAndTighten(assembly, "sub-frame", tools);
                AssemblyEngineDockingState docking = block.GetComponent<AssemblyEngineDockingState>();
                Assert.That(docking, Is.Not.Null);
                MountPointAuthoring engineMount = docking.Mount;
                block.transform.SetPositionAndRotation(engineMount.Pose.position, engineMount.Pose.rotation);
                block.Body.position = engineMount.Pose.position;
                block.Body.rotation = engineMount.Pose.rotation;
                compound.Refresh(true);
                Assert.That(debug.IgnoreAssemblyMassLimit, Is.False);
                Assert.That(block.PickupTarget.CanPickup(context), Is.False);
                ToolDefinition dockingTool = ScriptableObject.CreateInstance<ToolDefinition>();
                dockingTool.Configure("test.full-engine.docking-tool", "Engine mounting wrench", "Wrench",
                    FastenerSize.Millimeter11);
                tools.Add(dockingTool);
                for (int turn = 0; turn < 2; turn++)
                {
                    AssemblyOperationResult docked = docking.TryTurnPending(docking.FastenerIds[turn], dockingTool,
                        FastenerRotationDirection.Clockwise);
                    Assert.That(docked.Succeeded, Is.True, docked.Message);
                    Assert.That(block.IsInstalled, Is.EqualTo(turn == 1));
                }
                MountPointRuntime installedEngine = assembly.ResolveMount(engineMount);
                Assert.That(installedEngine.InstalledPart, Is.SameAs(block));
                Assert.That(installedEngine.FastenerGroup.IsBolted, Is.True);
                Assert.That(installedEngine.Fasteners.Sum(fastener => fastener.Stage), Is.EqualTo(2));
                Assert.That(compound.ActiveProxyCount, Is.Zero);
                Assert.That(block.Body.mass, Is.EqualTo(95f).Within(.0001f));
                Assert.That(stock.Where(part => part != block).All(part => part.IsInstalled &&
                    part.transform.IsChildOf(block.transform)), Is.True);
                Assert.That(docking.ReleaseIfUnfastened(), Is.False);

                Vector3 installedPosition = block.transform.position;
                Quaternion installedRotation = block.transform.rotation;
                foreach (FastenerInstance fastener in installedEngine.Fasteners)
                {
                    while (fastener.Stage > 0)
                    {
                        AssemblyOperationResult loosened = assembly.TryOperateFastener(engineMount.MountId,
                            fastener.Definition.DefinitionId, dockingTool, tighten: false);
                        Assert.That(loosened.Succeeded, Is.True, loosened.Message);
                    }
                }
                Assert.That(installedEngine.FastenerGroup.IsBolted, Is.False);
                Assert.That(docking.ReleaseIfUnfastened(), Is.True);
                Assert.That(Vector3.Distance(block.transform.position, installedPosition), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(block.transform.rotation, installedRotation), Is.LessThan(.001f));
                AssertCompleteCompound(assembly, compound, block, gtCover, stock);
                Assert.That(block.PickupTarget.CanPickup(context), Is.False);
                foreach (PartInstance retained in stock.Where(part => part != block))
                    Assert.That(assembly.Graph.FindMountForPart(retained).Fasteners.All(fastener =>
                        fastener.Stage == fastener.Definition.MaximumStage), Is.True,
                        retained.Definition.DefinitionId + " lost its internal fastening during engine removal.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(player);
                foreach (ToolDefinition tool in tools) Object.DestroyImmediate(tool);
            }
        }

        private static void InstallAndTighten(VehicleAssemblyController assembly, string suffix, List<ToolDefinition> tools)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition.DefinitionId == Prefix + suffix);
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.Definition.AcceptsPart(Prefix + suffix));
            Assert.That(part.IsInstalled, Is.False, suffix + " should begin loose.");
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, suffix + ": " + result.Message);
            Assert.That(part.IsInstalled, Is.True, suffix + " installation must not collapse its support.");
            foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
            {
                ToolDefinition tool = tools.FirstOrDefault(value => value.ToolType == fastener.ToolRule.ToolType &&
                    value.Size == fastener.ToolRule.FastenerSize);
                if (tool == null)
                {
                    tool = ScriptableObject.CreateInstance<ToolDefinition>();
                    tool.Configure("test.full-engine.tool." + tools.Count, "Test assembly tool",
                        fastener.ToolRule.ToolType, fastener.ToolRule.FastenerSize);
                    tools.Add(tool);
                }
                for (int stage = 0; stage < fastener.MaximumStage; stage++)
                {
                    result = assembly.TryOperateFastener(mount.MountId, fastener.DefinitionId, tool, tighten: true);
                    Assert.That(result.Succeeded, Is.True, fastener.DefinitionId + ": " + result.Message);
                }
            }
        }

        private static void AssertCompleteCompound(VehicleAssemblyController assembly, AssemblyLooseCompoundPhysics compound,
            PartInstance block, PartInstance gtCover, PartInstance[] stock)
        {
            Assert.That(block.IsInstalled, Is.False);
            Assert.That(gtCover.IsInstalled, Is.False, "Never count both mutually exclusive rocker covers.");
            Assert.That(stock.Where(part => part != block).All(part => part.IsInstalled && part.transform.IsChildOf(block.transform)),
                Is.True);
            Assert.That(stock.Sum(part => part.Definition.MassKilograms), Is.EqualTo(177.6f).Within(.001f));
            Assert.That(block.Body.mass, Is.EqualTo(177.6f).Within(.001f));
            Assert.That(block.Body.isKinematic, Is.False);
            Assert.That(compound.ActiveProxyCount, Is.EqualTo(53));
            AssemblyCompoundColliderProxy[] active = assembly.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true)
                .Where(proxy => proxy.gameObject.activeInHierarchy).ToArray();
            Assert.That(active, Has.Length.EqualTo(53));
            Assert.That(active.All(proxy => proxy.LooseOwner == block &&
                proxy.GetComponent<Collider>().attachedRigidbody == block.Body), Is.True);
            Assert.That(active.All(proxy => proxy.GetComponent<StableEntityIdAuthoring>() == null &&
                proxy.GetComponent<PartInstance>() == null &&
                (proxy.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave), Is.True);
        }
    }
}
