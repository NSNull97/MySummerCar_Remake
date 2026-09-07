using System;
using System.Collections.Generic;
using System.Linq;
using MSC.LegacyImport;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineCompoundMotionTests
    {
        private const string Prefix = "vehicle.satsuma.part.";
        private static readonly string[] StockOrder =
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
        public void MovingCompleteLooseEngineToHomeDoesNotDeformCompoundOrCenter()
        {
            using (var f = new Fixture())
            {
                f.MoveBlock(Vector3.zero, Quaternion.identity);
                foreach (string part in StockOrder) f.InstallAndTighten(part);
                f.Compound.Refresh(true);
                AssemblyCompoundColliderProxy[] proxies = f.Block.GetComponentsInChildren<AssemblyCompoundColliderProxy>()
                    .Where(proxy => proxy.gameObject.activeInHierarchy).ToArray();
                Assert.That(proxies, Has.Length.EqualTo(53));
                Vector3[] positions = proxies.Select(proxy => proxy.transform.localPosition).ToArray();
                Vector3[] scales = proxies.Select(proxy => proxy.transform.localScale).ToArray();
                Quaternion[] rotations = proxies.Select(proxy => proxy.transform.localRotation).ToArray();
                Vector3 center = f.Block.Body.centerOfMass;
                float positionDrift = 0f, centerDrift = 0f, rotationDrift = 0f, scaleDrift = 0f;
                string worstShape = string.Empty;
                for (int step = 0; step < 40; step++)
                {
                    // Real-world coordinates near home expose cancellation which
                    // origin-only synthetic fixtures cannot detect. No part is
                    // installed, removed or adjusted during this common motion.
                    f.MoveBlock(new Vector3(1550f + step * .03125f, 5f, -1039f + step * .015625f),
                        Quaternion.Euler(3f + step * .5f, 27f + step, -2f));
                    f.Compound.Refresh();
                    centerDrift = Mathf.Max(centerDrift, Vector3.Distance(center, f.Block.Body.centerOfMass));
                    for (int index = 0; index < proxies.Length; index++)
                    {
                        float delta = Vector3.Distance(positions[index], proxies[index].transform.localPosition);
                        if (delta > positionDrift)
                        {
                            positionDrift = delta;
                            worstShape = proxies[index].SourcePart.Definition.DefinitionId;
                        }
                        rotationDrift = Mathf.Max(rotationDrift,
                            Quaternion.Angle(rotations[index], proxies[index].transform.localRotation));
                        scaleDrift = Mathf.Max(scaleDrift,
                            Vector3.Distance(scales[index], proxies[index].transform.localScale));
                    }
                }
                string diagnostic = $"Common root motion: max shape {positionDrift:R} m ({worstShape}), " +
                    $"center {centerDrift:R} m, rotation {rotationDrift:R} deg, scale {scaleDrift:R}.";
                Debug.Log(diagnostic);
                Assert.That(positionDrift, Is.LessThan(.00001f), diagnostic);
                Assert.That(centerDrift, Is.LessThan(.00001f), diagnostic);
                Assert.That(scaleDrift, Is.LessThan(.00001f), diagnostic);
                // Quaternion.Angle itself has a float quantization floor. The
                // positional invariants above retain their physical 10 um bound.
                Assert.That(rotationDrift, Is.LessThan(.06f), diagnostic);
            }
        }

        [Test]
        public void InstalledEngineFollowsChassisWithoutIndependentContactsOrMotion()
        {
            using (var f = new Fixture())
            {
                foreach (string part in StockOrder) f.InstallAndTighten(part);
                f.InstallAndTighten("sub-frame");
                AssemblyEngineDockingState docking = f.Block.GetComponent<AssemblyEngineDockingState>();
                f.MoveBlock(docking.Mount.Pose.position, docking.Mount.Pose.rotation);
                ToolDefinition wrench = f.Tool("Wrench", FastenerSize.Millimeter11);
                Assert.That(docking.TryTurnPending(docking.FastenerIds[0], wrench,
                    FastenerRotationDirection.Clockwise).Succeeded, Is.True);
                Assert.That(docking.TryTurnPending(docking.FastenerIds[1], wrench,
                    FastenerRotationDirection.Clockwise).Succeeded, Is.True);
                for (int step = 0; step < 40; step++)
                {
                    f.Instance.transform.SetPositionAndRotation(new Vector3(1550f + step * .1f, 5f, -1039f),
                        Quaternion.Euler(step * .5f, step, 3f));
                    f.Compound.Refresh();
                    Assert.That(f.Block.IsInstalled, Is.True);
                    Assert.That(f.Block.Body.isKinematic, Is.True);
                    Assert.That(f.Compound.ActiveProxyCount, Is.Zero);
                    Assert.That(Vector3.Distance(f.Block.Body.position, docking.Mount.Pose.position),
                        Is.LessThan(.00001f));
                    Assert.That(f.Block.Body.linearVelocity.sqrMagnitude, Is.Zero);
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Instance;
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyLooseCompoundPhysics Compound;
            public readonly PartInstance Block;
            private readonly GameObject looseRoot;
            private readonly List<ToolDefinition> tools = new();

            public Fixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                if (prefab == null) Assert.Ignore("Private donor-derived Satsuma baseline is unavailable.");
                Instance = Object.Instantiate(prefab);
                looseRoot = Instance.GetComponent<LegacySatsumaLoosePartsRoot>().LoosePartsRoot.gameObject;
                Assembly = Instance.GetComponent<VehicleAssemblyController>();
                Compound = Instance.GetComponent<AssemblyLooseCompoundPhysics>();
                Block = Assembly.Parts.Single(part => part.Definition.DefinitionId == Prefix + "engine-block");
                Compound.Refresh(true);
            }

            public void MoveBlock(Vector3 position, Quaternion rotation)
            {
                Block.transform.SetPositionAndRotation(position, rotation);
                Block.Body.position = position;
                Block.Body.rotation = rotation;
            }

            public ToolDefinition Tool(string type, FastenerSize size)
            {
                ToolDefinition tool = tools.FirstOrDefault(value => value.ToolType == type && value.Size == size);
                if (tool != null) return tool;
                tool = ScriptableObject.CreateInstance<ToolDefinition>();
                tool.Configure("test.compound.motion.tool." + tools.Count, "Test tool", type, size);
                tools.Add(tool);
                return tool;
            }

            public void InstallAndTighten(string suffix)
            {
                PartInstance part = Assembly.Parts.Single(value => value.Definition.DefinitionId == Prefix + suffix);
                MountPointAuthoring mount = Assembly.MountPoints.Single(value => value.Definition.AcceptsPart(Prefix + suffix));
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult installed = Assembly.TryInstall(part, mount);
                Assert.That(installed.Succeeded, Is.True, suffix + ": " + installed.Message);
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
                    for (int stage = 0; stage < fastener.MaximumStage; stage++)
                        Assert.That(Assembly.TryOperateFastener(mount.MountId, fastener.DefinitionId,
                            Tool(fastener.ToolRule.ToolType, fastener.ToolRule.FastenerSize), true).Succeeded, Is.True);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Instance);
                if (looseRoot != null) Object.DestroyImmediate(looseRoot);
                foreach (ToolDefinition tool in tools) Object.DestroyImmediate(tool);
            }
        }
    }
}
