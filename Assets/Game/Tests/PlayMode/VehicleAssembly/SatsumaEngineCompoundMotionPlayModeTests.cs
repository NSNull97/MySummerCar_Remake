using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.LegacyImport;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class SatsumaEngineCompoundMotionPlayModeTests
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

        [UnityTest]
        public IEnumerator CompleteEngineSettlesOnCompoundContactsAtOrigin() =>
            SettleCompleteEngine(Vector3.zero);

        [UnityTest]
        public IEnumerator CompleteEngineSettlesOnCompoundContactsNearHome() =>
            SettleCompleteEngine(new Vector3(1550f, 5f, -1039f));

        [UnityTest]
        public IEnumerator CompleteLooseEngineReportsActualBaySupportAndDockingReach() =>
            SettleCompleteEngine(new Vector3(1550f, 5f, -1039f), inEngineBay: true);

        private static IEnumerator SettleCompleteEngine(Vector3 offset, bool inEngineBay = false)
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null) Assert.Ignore("Private donor-derived Satsuma baseline is unavailable.");
            Scene scene = SceneManager.CreateScene("engine compound motion " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsScene physics = scene.GetPhysicsScene();
            GameObject instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            // Awake intentionally detaches this scope from the car. Moving or
            // destroying only the vehicle would leave all loose actors behind.
            GameObject looseRoot = instance.GetComponent<LegacySatsumaLoosePartsRoot>().LoosePartsRoot.gameObject;
            if (looseRoot.scene != scene) SceneManager.MoveGameObjectToScene(looseRoot, scene);
            var table = new GameObject("engine contact test table");
            SceneManager.MoveGameObjectToScene(table, scene);
            var tools = new List<ToolDefinition>();
            Vector3 previousGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                AssemblyLooseCompoundPhysics compound = instance.GetComponent<AssemblyLooseCompoundPhysics>();
                PartInstance block = assembly.Parts.Single(part => part.Definition.DefinitionId == Prefix + "engine-block");
                // The complete generated engine uses its real 56 convex/primitive
                // source shapes. Only unrelated car/garage inventory contacts
                // are suppressed; no engine shape, friction or gravity is replaced.
                foreach (Rigidbody body in assembly.Parts.Where(part => part.Body != null).Select(part => part.Body).Distinct())
                    if (body != block.Body) { body.isKinematic = true; body.detectCollisions = false; }
                block.transform.SetPositionAndRotation(offset + Vector3.up, Quaternion.identity);
                block.Body.position = block.transform.position;
                block.Body.rotation = block.transform.rotation;
                foreach (string suffix in StockOrder) InstallAndTighten(assembly, suffix, tools);
                AssemblyEngineDockingState docking = block.GetComponent<AssemblyEngineDockingState>();
                if (inEngineBay)
                {
                    instance.transform.position = offset;
                    InstallAndTighten(assembly, "sub-frame", tools);
                    PartInstance chassis = assembly.Parts.Single(part => part.IsAssemblyRoot);
                    PartInstance subframe = assembly.Parts.Single(part => part.Definition.DefinitionId == Prefix + "sub-frame");
                    // Freeze the support, not the engine. Retain the original
                    // enabled body/subframe solids and their real geometry.
                    foreach (PartInstance support in new[] { chassis, subframe })
                    { support.Body.isKinematic = true; support.Body.detectCollisions = true; }
                    block.transform.SetPositionAndRotation(docking.Mount.Pose.position, docking.Mount.Pose.rotation);
                    block.Body.position = block.transform.position; block.Body.rotation = block.transform.rotation;
                }
                compound.Refresh(true);
                Assert.That(block.Body.mass, Is.EqualTo(177.6f).Within(.001f));
                Assert.That(compound.ActiveProxyCount, Is.EqualTo(53));
                Physics.SyncTransforms();
                Collider[] contacts = block.GetComponentsInChildren<Collider>(true).Where(shape =>
                    shape.enabled && shape.gameObject.activeInHierarchy && !shape.isTrigger &&
                    shape.attachedRigidbody == block.Body).ToArray();
                Assert.That(contacts, Has.Length.EqualTo(56));
                Assert.That(block.gameObject.scene, Is.EqualTo(scene));
                Assert.That(block.gameObject.scene.GetPhysicsScene(), Is.EqualTo(physics));
                Assert.That(block.Body.constraints, Is.EqualTo(RigidbodyConstraints.None));
                Assert.That(block.Body.detectCollisions, Is.True);
                Assert.That(block.Body.isKinematic, Is.False);
                Debug.Log($"Engine physics preflight: bay={inEngineBay}, scene={block.gameObject.scene.name}, " +
                    $"constraints={block.Body.constraints}, collisions={block.Body.detectCollisions}, " +
                    $"gravity={Physics.gravity}, priorGravity={previousGravity}, start={block.Body.position}.");
                float bottom = contacts.Min(shape => shape.bounds.min.y);
                if (!inEngineBay)
                {
                    table.transform.position = new Vector3(offset.x, bottom - .27f, offset.z);
                    table.AddComponent<BoxCollider>().size = new Vector3(8f, .5f, 8f);
                }
                else
                {
                    Assert.That(Enumerable.Range(0, 3).All(docking.CanExposePending), Is.True,
                        "The exact installed pose must begin inside all three physical docking ranges.");
                    Collider[] supports = instance.GetComponentsInChildren<Collider>(true).Where(shape =>
                        shape.enabled && shape.gameObject.activeInHierarchy && !shape.isTrigger &&
                        shape.attachedRigidbody != null && shape.attachedRigidbody.detectCollisions &&
                        shape.attachedRigidbody != block.Body).ToArray();
                    var overlaps = new List<string>();
                    foreach (Collider contact in contacts)
                        foreach (Collider support in supports)
                            if (Physics.ComputePenetration(contact, contact.transform.position, contact.transform.rotation,
                                support, support.transform.position, support.transform.rotation, out _, out float depth))
                                overlaps.Add($"{contact.name}/{support.name}:{depth:R}m");
                    Debug.Log($"Initial bay contacts: supportShapes={supports.Length}, penetrations={overlaps.Count}; " +
                        string.Join("; ", overlaps.Take(16)));
                }
                Physics.SyncTransforms();
                AssemblyEngineAdjustmentState[] adjustments = block.GetComponentsInChildren<AssemblyEngineAdjustmentState>();
                Vector3 finalWindowStart = Vector3.zero;
                float maximumFinalSpeed = 0f, maximumFinalTravel = 0f;
                int finalAwakeFrames = 0;
                Vector3 initialPosition = block.Body.position;
                float firstStepsMotion = 0f;
                var sampledDockRanges = new List<string>();
                // Let an unconstrained physical engine land and settle. The
                // explicit calls mirror fixed pose/proxy updates and the engine
                // presenters' late updates without involving another physics scene.
                for (int step = 0; step < 600; step++)
                {
                    foreach (AssemblyEngineAdjustmentState adjustment in adjustments) adjustment.RefreshPresentation();
                    compound.Refresh();
                    Physics.SyncTransforms();
                    physics.Simulate(.02f);
                    if (step == 4)
                    {
                        firstStepsMotion = Vector3.Distance(initialPosition, block.Body.position);
                        Debug.Log($"Engine first5 physics steps: bay={inEngineBay}, moved={firstStepsMotion:R}m, " +
                            $"speed={block.Body.linearVelocity.magnitude:R}, position={block.Body.position}.");
                    }
                    if (inEngineBay && (step == 4 || step == 49 || step == 249 || step == 599))
                        sampledDockRanges.Add($"t={(step + 1) * .02f:F2}: " + string.Join(",",
                            Enumerable.Range(0, 3).Select(index => Vector3.Distance(
                                block.transform.TransformPoint(docking.BlockPoints[index]),
                                docking.ChassisFrame.TransformPoint(docking.ChassisPoints[index])).ToString("R"))));
                    if (step == 500) finalWindowStart = block.Body.position;
                    if (step >= 500)
                    {
                        maximumFinalSpeed = Mathf.Max(maximumFinalSpeed, block.Body.linearVelocity.magnitude);
                        maximumFinalTravel = Mathf.Max(maximumFinalTravel,
                            Vector3.Distance(finalWindowStart, block.Body.position));
                        if (!block.Body.IsSleeping()) finalAwakeFrames++;
                    }
                }
                string diagnostic = $"Generated engine at {offset}, bay={inEngineBay}: last2s speedMax={maximumFinalSpeed:R}, " +
                    $"travelMax={maximumFinalTravel:R}, awakeFrames={finalAwakeFrames}, " +
                    $"position={block.Body.position}, angularSpeed={block.Body.angularVelocity.magnitude:R}.";
                Debug.Log(diagnostic);
                if (inEngineBay) Debug.Log("Unbolted engine docking distances: " + string.Join("; ", sampledDockRanges));
                Assert.That(block.IsInstalled, Is.False);
                Assert.That(block.Body.isKinematic, Is.False);
                Assert.That(block.Body.useGravity, Is.True);
                Assert.That(compound.ActiveProxyCount, Is.EqualTo(53));
                if (inEngineBay)
                {
                    // Measured generated-bay support settles with all three
                    // sockets reachable. Do not require sleep or zero motion:
                    // genuine gravity/contact settling remains unconstrained.
                    Assert.That(float.IsFinite(block.Body.position.y), Is.True, diagnostic);
                    Assert.That(float.IsFinite(block.Body.linearVelocity.sqrMagnitude), Is.True, diagnostic);
                    Assert.That(maximumFinalSpeed, Is.LessThan(.01f), diagnostic);
                    Assert.That(maximumFinalTravel, Is.LessThan(.002f), diagnostic);
                    Assert.That(docking.CaptureSaveData().IsEmpty, Is.True,
                        "Gravity/contact alone must not turn mounting bolts or install the engine.");
                    Assert.That(Enumerable.Range(0, 3).All(docking.CanExposePending), Is.True,
                        "The fully assembled engine must remain bolt-reachable after settling on the real subframe. " + diagnostic);
                    for (int index = 0; index < 3; index++)
                    {
                        float distance = Vector3.Distance(block.transform.TransformPoint(docking.BlockPoints[index]),
                            docking.ChassisFrame.TransformPoint(docking.ChassisPoints[index]));
                        if (distance < .1f) Assert.That(docking.CanExposePending(index), Is.True);
                        else if (distance > .1f) Assert.That(docking.CanExposePending(index), Is.False);
                    }
                }
                else
                {
                    Assert.That(firstStepsMotion, Is.GreaterThan(.001f), "The local physics scene must actually simulate the engine.");
                    Assert.That(maximumFinalSpeed, Is.LessThan(.08f), diagnostic);
                    Assert.That(maximumFinalTravel, Is.LessThan(.015f), diagnostic);
                    Assert.That(finalAwakeFrames, Is.LessThan(20),
                        "A stationary engine must not have its contact shape/inertia rewritten every tick. " + diagnostic);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
                if (looseRoot != null) Object.DestroyImmediate(looseRoot);
                Object.DestroyImmediate(table);
                foreach (ToolDefinition tool in tools) Object.DestroyImmediate(tool);
                Physics.gravity = previousGravity;
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        private static void InstallAndTighten(VehicleAssemblyController assembly, string suffix, List<ToolDefinition> tools)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition.DefinitionId == Prefix + suffix);
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.Definition.AcceptsPart(Prefix + suffix));
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult installed = assembly.TryInstall(part, mount);
            Assert.That(installed.Succeeded, Is.True, suffix + ": " + installed.Message);
            foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
            {
                ToolDefinition tool = tools.FirstOrDefault(value => value.ToolType == fastener.ToolRule.ToolType &&
                    value.Size == fastener.ToolRule.FastenerSize);
                if (tool == null)
                {
                    tool = ScriptableObject.CreateInstance<ToolDefinition>();
                    tool.Configure("test.compound.contact.tool." + tools.Count, "Test tool",
                        fastener.ToolRule.ToolType, fastener.ToolRule.FastenerSize);
                    tools.Add(tool);
                }
                for (int stage = 0; stage < fastener.MaximumStage; stage++)
                    Assert.That(assembly.TryOperateFastener(mount.MountId, fastener.DefinitionId, tool, true).Succeeded, Is.True);
            }
        }
    }
}
