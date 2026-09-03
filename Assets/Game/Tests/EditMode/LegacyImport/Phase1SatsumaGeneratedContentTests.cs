using System;
using System.IO;
using System.Linq;
using MSC.LegacyImport;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Bootstrap;
using MSC.Services;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.Vehicle.NWH;
using MSC.Weather.Production;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Architecture;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using NWH.WheelController3D;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class Phase1SatsumaGeneratedContentTests
    {
        private static readonly string[] Post260AddedFastenerKeys =
        {
            "mount.satsuma.bumper-front/fastener.satsuma.bumper-front.boltpm-1",
            "mount.satsuma.bumper-front/fastener.satsuma.bumper-front.boltpm-2",
            "mount.satsuma.bumper-rear/fastener.satsuma.bumper-rear.boltpm-1",
            "mount.satsuma.bumper-rear/fastener.satsuma.bumper-rear.boltpm-2",
            "mount.satsuma.fender-left/fastener.satsuma.fender-left.boltpm-1",
            "mount.satsuma.fender-left/fastener.satsuma.fender-left.boltpm-2",
            "mount.satsuma.fender-left/fastener.satsuma.fender-left.boltpm-3",
            "mount.satsuma.fender-left/fastener.satsuma.fender-left.boltpm-4",
            "mount.satsuma.fender-left/fastener.satsuma.fender-left.boltpm-5",
            "mount.satsuma.fender-right/fastener.satsuma.fender-right.boltpm-1",
            "mount.satsuma.fender-right/fastener.satsuma.fender-right.boltpm-2",
            "mount.satsuma.fender-right/fastener.satsuma.fender-right.boltpm-3",
            "mount.satsuma.fender-right/fastener.satsuma.fender-right.boltpm-4",
            "mount.satsuma.fender-right/fastener.satsuma.fender-right.boltpm-5",
            "mount.satsuma.grille/fastener.satsuma.grille.boltpm-1",
            "mount.satsuma.grille/fastener.satsuma.grille.boltpm-2",
            "mount.satsuma.hood/fastener.satsuma.hood.boltpm-1",
            "mount.satsuma.hood/fastener.satsuma.hood.boltpm-2",
            "mount.satsuma.hood/fastener.satsuma.hood.boltpm-3",
            "mount.satsuma.hood/fastener.satsuma.hood.boltpm-4",
            "mount.satsuma.steering-wheel/fastener.satsuma.steering-wheel.boltpm-1",
        };

        [Test]
        public void FrontSteeringEndpointsIncludeFrozenDonorOffsetParent()
        {
            const string configurationPath = "Config/DonorPaths.local.json";
            if (!File.Exists(configurationPath))
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }

            var paths = MSC.LegacyImport.Editor.Configuration
                .DonorPathConfiguration.LoadFromFile(configurationPath);
            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(Path.Combine(
                paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/" +
                "ExportedProject/Assets/_Scenes/GAME.unity"));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            SatsumaFrontSuspensionCornerBinding[] corners = prefab
                .GetComponent<SatsumaFrontSuspensionController>().Corners;
            Assert.That(corners, Has.Length.EqualTo(2));
            Assert.That(corners.Select(value => value.CornerId),
                Is.EqualTo(new[] { "fl", "fr" }));
            long[] hubs = { 70301L, 54171L };
            long[] parents = { 58309L, 57398L };
            long[] endpoints = { 39613L, 66023L };
            for (int side = 0; side < corners.Length; side++)
            {
                SatsumaFrontSuspensionCornerBinding corner = corners[side];
                DonorTransformRecord endpoint = scene.GetTransform(endpoints[side]);
                DonorTransformRecord parent = scene.GetTransform(parents[side]);
                Assert.That(endpoint.FatherTransformId, Is.EqualTo(parents[side]));
                Assert.That(parent.FatherTransformId, Is.EqualTo(hubs[side]));
                scene.GetTransformRelativeTo(endpoints[side], hubs[side],
                    out Vector3 hubLocalPosition, out _, out _);
                Assert.That(Vector3.Distance(corner.HubToSteeringOuterLocalOffset,
                    hubLocalPosition), Is.LessThan(0.00002f), corner.CornerId);
                Assert.That(Vector3.Distance(corner.HubToSteeringOuterLocalOffset,
                    endpoint.LocalPosition), Is.EqualTo(0.05f).Within(0.00002f),
                    "The endpoint-local pose alone loses the 50 mm OFFSET parent.");
                // Do not snap a bone origin onto the bolt head: their donor
                // origins are different physical reference points.
                Assert.That(corner.SteeringRodPresentation.InstalledRenderer.bones[1],
                    Is.SameAs(corner.SteeringOuterTarget));
            }
        }

        [Test]
        public void FrontConnectionFastenerPosesMatchFrozenDonorShockPivots()
        {
            const string configurationPath = "Config/DonorPaths.local.json";
            if (!File.Exists(configurationPath))
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }

            var paths = MSC.LegacyImport.Editor.Configuration
                .DonorPathConfiguration.LoadFromFile(configurationPath);
            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(Path.Combine(
                paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/" +
                "ExportedProject/Assets/_Scenes/GAME.unity"));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            AssemblyFastenerInteractionTarget[] targets = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            string[] corners = { "fl", "fr" };
            long[] pivots = { 64686L, 44833L };
            long[][] markers =
            {
                new[] { 39160L, 49488L, 64879L, 65179L, 68946L },
                new[] { 38251L, 51736L, 65041L, 70471L, 70485L },
            };
            for (int side = 0; side < corners.Length; side++)
            {
                for (int index = 0; index < markers[side].Length; index++)
                {
                    string id = index < 4
                        ? "fastener.satsuma.strut-" + corners[side] +
                            ".lower-" + (index + 1)
                        : "fastener.satsuma.steering-rod-" + corners[side] +
                            ".outer-joint";
                    Transform target = targets.Single(value =>
                        value.FastenerDefinitionId == id).transform;
                    long ownerId = index < 4
                        ? (side == 0 ? 13821L : 15127L)
                        : (side == 0 ? 24431L : 2810L);
                    long markerGameObjectId = scene.GetTransform(
                        markers[side][index]).GameObjectId;
                    Assert.That(scene.GetMonoBehaviours(markerGameObjectId).Any(value =>
                        Phase1SatsumaBaselineBuilder.IsStagedFrontConnectionScrew(
                            value.SerializedBody, ownerId)), Is.True, id);
                    scene.GetTransformRelativeTo(markers[side][index], pivots[side],
                        out Vector3 position, out Quaternion rotation, out Vector3 scale);
                    Assert.That(Vector3.Distance(target.localPosition, position),
                        Is.LessThan(0.00001f), id);
                    Assert.That(Quaternion.Angle(target.localRotation, rotation),
                        Is.LessThan(0.03f), id);
                    Assert.That(Vector3.Distance(target.GetChild(0).localScale, scale),
                        Is.LessThan(0.00001f), id);
                    Assert.That(target.parent.name,
                        Is.EqualTo("Front shock bottom " + corners[side].ToUpperInvariant()));
                }
            }
            foreach (long adjusterId in new[] { 112113L, 105829L })
            {
                DonorMonoBehaviourRecord adjuster = scene.MonoBehaviours.Single(
                    value => value.ComponentId == adjusterId);
                Assert.That(Phase1SatsumaBaselineBuilder.IsStagedFrontConnectionScrew(
                    adjuster.SerializedBody, adjusterId == 112113L ? 24431L : 2810L),
                    Is.False, "Numeric adjuster events are not fastening stages.");
            }
        }

        [Test]
        public void GeneratedPrefabLoadsAllProjectOwnedRuntimeBoundaries()
        {
            if (!File.Exists(Phase1SatsumaBaselineBuilder.RuntimePrefabPath))
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleAssemblyController>(), Is.Not.Null);
            CarryCollisionBypassScope carryCollisionScope = prefab
                .GetComponent<CarryCollisionBypassScope>();
            Assert.That(carryCollisionScope, Is.Not.Null);
            Assert.That(carryCollisionScope.ScopeColliders, Has.Length.EqualTo(29));
            Assert.That(
                carryCollisionScope.ScopeColliders.Any(value =>
                    value != null && value.name == "collider_right_92700"),
                Is.True,
                "The carried-part bypass must include the right rear-fender collider reported in the live build.");
            Assert.That(
                carryCollisionScope.ScopeColliders.Any(value =>
                    value != null && value.name == "collider_left_92311"),
                Is.True,
                "The carried-part bypass must include the left rear-fender collider reported in the live build.");
            Assert.That(prefab.GetComponent<VehicleSimulationHost>(), Is.Not.Null);
            Assert.That(
                prefab.GetComponent<AssemblyChassisMassController>(),
                Is.Not.Null);
            VehiclePersistenceBinding persistence =
                prefab.GetComponent<VehiclePersistenceBinding>();
            Assert.That(persistence, Is.Not.Null);
            SatsumaNwhPhysicsRestoreSynchronizer restoreSynchronizer = prefab
                .GetComponent<SatsumaNwhPhysicsRestoreSynchronizer>();
            Assert.That(restoreSynchronizer, Is.Not.Null);
            Assert.That(
                persistence.ChassisMassController,
                Is.SameAs(prefab.GetComponent<AssemblyChassisMassController>()));
            Assert.That(
                persistence.PhysicsRestoreSynchronizerComponent,
                Is.SameAs(restoreSynchronizer));
            Assert.That(
                persistence.PhysicsRestoreSynchronizer,
                Is.SameAs(restoreSynchronizer));
            Assert.That(
                restoreSynchronizer.WheelSupport,
                Is.SameAs(prefab.GetComponent<
                    NwhAssemblyWheelSupportController>()));
            Assert.That(
                restoreSynchronizer.FrontSteering,
                Is.SameAs(prefab.GetComponent<
                    SatsumaFrontSteeringController>()));
            Assert.That(
                restoreSynchronizer.FrontSuspension,
                Is.SameAs(prefab.GetComponent<
                    SatsumaFrontSuspensionController>()));
            Assert.That(
                restoreSynchronizer.RearSuspension,
                Is.SameAs(prefab.GetComponent<
                    SatsumaRearNwhSuspensionController>()));
            Assert.That(prefab.GetComponent<VehiclePaintStateController>(), Is.Not.Null);
            LegacySatsumaBaselineMetadata metadata =
                prefab.GetComponent<LegacySatsumaBaselineMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.LoosePartCount, Is.EqualTo(125));
            Assert.That(metadata.ActiveLoosePartCount, Is.EqualTo(120));
            Assert.That(
                prefab.GetComponentsInChildren<MeshCollider>(true),
                Has.Length.GreaterThanOrEqualTo(23));
            Transform chassisCollision = prefab.transform.Find(
                "Sanitized Donor Chassis Colliders");
            Assert.That(chassisCollision, Is.Not.Null);
            Collider[] chassisColliders =
                chassisCollision.GetComponentsInChildren<Collider>(true);
            Assert.That(
                chassisColliders,
                Has.Length.EqualTo(29),
                "The prefab must retain all locked donor collider records for provenance.");
            Collider broadBodyProxy = chassisColliders.Single(collider =>
                collider.name.EndsWith("_92119"));
            Assert.That(
                broadBodyProxy.enabled,
                Is.False,
                "The donor layer-17 CarCollider is a broad proxy, not a solid cabin collider in the independent runtime.");
            Assert.That(
                chassisColliders.Where(collider => collider != broadBodyProxy),
                Is.All.Matches<Collider>(collider => collider.enabled),
                "The 28 detailed body, floor and rocker colliders must remain physical.");
            Collider[] playerOnlyColliders = chassisColliders
                .Where(collider => collider.name.StartsWith("PlayerColl_"))
                .ToArray();
            Assert.That(playerOnlyColliders, Has.Length.EqualTo(4));
            Assert.That(LayerMask.NameToLayer("Player"), Is.EqualTo(9));
            Transform playerCollisionProxy = chassisCollision.Find(
                "Project Player Collision Proxy");
            Assert.That(playerCollisionProxy, Is.Not.Null);
            Rigidbody playerCollisionProxyBody = playerCollisionProxy
                .GetComponent<Rigidbody>();
            Assert.That(playerCollisionProxyBody, Is.Not.Null);
            Assert.That(playerCollisionProxyBody.isKinematic, Is.True);
            Assert.That(playerCollisionProxyBody.useGravity, Is.False);
            Assert.That(
                playerOnlyColliders,
                Is.All.Matches<Collider>(collider =>
                    collider.enabled &&
                    !collider.isTrigger &&
                    collider.transform.IsChildOf(playerCollisionProxy) &&
                    collider.excludeLayers.value == ~(1 << 9) &&
                    collider.layerOverridePriority == 1),
                "PlayerColl must be isolated on one kinematic proxy that " +
                "collides only with the project Player layer.");
            Collider[] worldFacingChassisColliders = chassisColliders
                .Where(collider => !collider.name.StartsWith("PlayerColl_"))
                .ToArray();
            Assert.That(worldFacingChassisColliders, Has.Length.EqualTo(25));
            Assert.That(
                worldFacingChassisColliders,
                Is.All.Matches<Collider>(collider =>
                    !collider.transform.IsChildOf(playerCollisionProxy) &&
                    (collider.excludeLayers.value & (1 << 9)) != 0 &&
                    collider.layerOverridePriority >= 1),
                "World-facing chassis shapes must ignore Player so the " +
                "CharacterController cannot inject infinite solver momentum " +
                "into the dynamic car body.");
            RestrictedInteriorPostureVolume interiorVolume =
                prefab.GetComponentInChildren<
                    RestrictedInteriorPostureVolume>(true);
            Assert.That(interiorVolume, Is.Not.Null);
            Assert.That(interiorVolume.Volume, Is.Not.Null);
            Assert.That(interiorVolume.Volume.isTrigger, Is.True);
            Assert.That(
                interiorVolume.Volume.size,
                Is.EqualTo(new Vector3(1.28f, 1.2f, 1.67f)));
            AssemblyInstalledPartInteractionProxy[] interactionProxies =
                prefab.GetComponentsInChildren<
                    AssemblyInstalledPartInteractionProxy>(true);
            Assert.That(interactionProxies, Has.Length.EqualTo(125));
            Assert.That(
                interactionProxies,
                Is.All.Matches<AssemblyInstalledPartInteractionProxy>(proxy =>
                    proxy.InteractionCollider != null &&
                    proxy.InteractionCollider.isTrigger &&
                    proxy.GetComponent<Rigidbody>() == null),
                "Installed-part raycast proxies must follow the part actor instead of becoming stale nested rigidbodies.");

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            PartInstance chassis = assembly.Parts.Single(part =>
                part.IsAssemblyRoot);
            Assert.That(chassis.Body.mass, Is.EqualTo(389f).Within(0.001f));
            Assert.That(
                prefab.GetComponent<AssemblyChassisMassController>()
                    .BareChassisMassKilograms,
                Is.EqualTo(389f).Within(0.001f));
            Assert.That(chassis.Body.useGravity, Is.True);
            Assert.That(chassis.Body.isKinematic, Is.False);
            VehicleSpawnPhysicsActivator spawnActivator =
                prefab.GetComponent<VehicleSpawnPhysicsActivator>();
            Assert.That(spawnActivator, Is.Not.Null);
            Assert.That(spawnActivator.Chassis, Is.SameAs(chassis.Body));
            Assert.That(spawnActivator.ActivationFixedSteps, Is.EqualTo(2));
            NwhWheelPhysicsBackend wheelBackend =
                prefab.GetComponent<NwhWheelPhysicsBackend>();
            Assert.That(wheelBackend, Is.Not.Null);
            Assert.That(wheelBackend.Wheels, Has.Length.EqualTo(4));
            NwhAssemblyWheelSupportController wheelSupport = prefab
                .GetComponent<NwhAssemblyWheelSupportController>();
            Assert.That(wheelSupport, Is.Not.Null);
            Assert.That(
                wheelSupport.Bindings,
                Has.Length.EqualTo(4));
            Assert.That(
                wheelSupport.Bindings,
                Has.Exactly(4).Matches<NwhAssemblyWheelSupportBinding>(
                    binding => binding.Enabled));
            Assert.That(
                wheelSupport.Bindings.Skip(2).Select(binding => binding.Wheel),
                Is.All.Matches<WheelController>(wheel =>
                    wheel != null),
                "Both rear donor wheel solvers must be authored even though " +
                "AssemblyGraph keeps them disabled until their structural " +
                "prerequisites are installed.");
            VehicleJackLiftPoint[] liftPoints = prefab
                .GetComponentsInChildren<VehicleJackLiftPoint>(true);
            Assert.That(liftPoints, Has.Length.EqualTo(4));
            Assert.That(
                liftPoints,
                Is.All.Matches<VehicleJackLiftPoint>(point =>
                    point.Chassis == chassis.Body &&
                    point.HorizontalCaptureRadius >= 0.19f));
            Assert.That(
                assembly.Parts,
                Has.Length.EqualTo(126),
                "The body plus all 125 direct donor CARPARTS roots must be registered.");
            Assert.That(
                prefab.GetComponentsInChildren<PhysicsPickupTarget>(true),
                Has.Length.EqualTo(125));
            Assert.That(
                prefab.GetComponent<LegacySatsumaLoosePartsRoot>(),
                Is.Not.Null);
            Assert.That(
                assembly.MountPoints,
                Has.Length.EqualTo(117),
                "The generated graph must retain the current donor-evidenced mount roster.");
            Assert.That(
                assembly.Dependencies,
                Has.Length.EqualTo(40),
                "The donor front installation checks add four bolted-support gates to the existing graph.");
            Assert.That(
                assembly.Dependencies
                    .Where(value => value.Kind == AssemblyDependencyKind.InstallRequiresBolted)
                    .Select(value => value.DependentPartDefinitionId + " -> " +
                        value.RelatedPartDefinitionId),
                Is.EquivalentTo(new[]
                {
                    "vehicle.satsuma.part.spindle-fl -> vehicle.satsuma.part.wishbone-fl",
                    "vehicle.satsuma.part.spindle-fr -> vehicle.satsuma.part.wishbone-fr",
                    "vehicle.satsuma.part.strut-fl -> vehicle.satsuma.part.spindle-fl",
                    "vehicle.satsuma.part.strut-fr -> vehicle.satsuma.part.spindle-fr",
                }));
            Assert.That(assembly.Tools, Has.Length.EqualTo(10));
            Assert.That(
                assembly.Tools.Select(value => value.Size),
                Is.EquivalentTo(new[]
                {
                    FastenerSize.Millimeter5,
                    FastenerSize.Millimeter6,
                    FastenerSize.Millimeter7,
                    FastenerSize.Millimeter8,
                    FastenerSize.Millimeter9,
                    FastenerSize.Millimeter10,
                    FastenerSize.Millimeter11,
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter13,
                    FastenerSize.Millimeter14,
                }));
            AssemblyFastenerInteractionTarget[] generatedFasteners = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            Assert.That(generatedFasteners, Has.Length.EqualTo(280));
            Assert.That(
                generatedFasteners,
                Is.All.Matches<AssemblyFastenerInteractionTarget>(target =>
                    target.GetComponent<Collider>() != null &&
                    !target.GetComponent<Collider>().enabled &&
                    target.GetComponentsInChildren<Renderer>(true)
                        .All(renderer => !renderer.enabled)),
                "A bolt or nut must be physically and visually absent until " +
                "its owning part is installed.");
            MountPointAuthoring subFrameMount = assembly.MountPoints.Single(
                mount => mount.MountId == "mount.satsuma.sub-frame");
            Assert.That(subFrameMount.Definition.Fasteners, Has.Length.EqualTo(4));
            Assert.That(
                subFrameMount.Definition.Fasteners.Select(value => value.Size),
                Is.All.EqualTo(FastenerSize.Millimeter10));
            foreach (string corner in new[] { "fl", "fr" })
            {
                MountPointAuthoring wishboneMount = assembly.MountPoints.Single(
                    mount => mount.MountId ==
                        "mount.satsuma.wishbone-" + corner);
                Assert.That(
                    wishboneMount.Definition.Fasteners,
                    Has.Length.EqualTo(2),
                    wishboneMount.MountId);
                Assert.That(
                    wishboneMount.Definition.Fasteners
                        .Select(value => value.Size),
                    Is.All.EqualTo(FastenerSize.Millimeter10),
                    wishboneMount.MountId);

                MountPointAuthoring steeringRodMount = assembly.MountPoints
                    .Single(mount => mount.MountId ==
                        "mount.satsuma.steering-rod-" + corner);
                Assert.That(
                    steeringRodMount.Definition.Fasteners,
                    Has.Length.EqualTo(1),
                    steeringRodMount.MountId);
                Assert.That(
                    steeringRodMount.Definition.Fasteners[0].Size,
                    Is.EqualTo(FastenerSize.Millimeter12),
                    steeringRodMount.MountId);
                AssemblyFastenerInteractionTarget outerJoint =
                    generatedFasteners.Single(value => string.Equals(
                        value.FastenerDefinitionId,
                        "fastener.satsuma.steering-rod-" + corner +
                        ".outer-joint",
                        StringComparison.Ordinal));
                Assert.That(
                    outerJoint.transform.parent.name,
                    Is.EqualTo("Front shock bottom " +
                        corner.ToUpperInvariant()),
                    "The 12 mm joint bolt follows pivot_shock; the 14 mm " +
                    "steering-pivot marker is a toe adjuster, not a fastener.");
                MountPointAuthoring strutMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.strut-" + corner);
                Assert.That(strutMount.Definition.Fasteners.Select(value => value.Size),
                    Is.EquivalentTo(new[]
                    {
                        FastenerSize.Millimeter10, FastenerSize.Millimeter10,
                        FastenerSize.Millimeter10, FastenerSize.Millimeter9,
                        FastenerSize.Millimeter9, FastenerSize.Millimeter9,
                        FastenerSize.Millimeter9,
                    }));
                Assert.That(strutMount.Definition.FastenerGroup.AggregateMaximumTightness,
                    Is.EqualTo(56));
            }
            foreach (string corner in new[] { "fl", "fr", "rl", "rr" })
            {
                MountPointAuthoring wheelMount = assembly.MountPoints.Single(
                    mount => mount.MountId ==
                        "mount.satsuma.wheel" + corner + "-new");
                Assert.That(
                    wheelMount.Definition.Fasteners,
                    Has.Length.EqualTo(4),
                    wheelMount.MountId);
                Assert.That(
                    wheelMount.Definition.Fasteners.Select(value => value.Size),
                    Is.All.EqualTo(FastenerSize.Millimeter13),
                    wheelMount.MountId);
            }
            int fastenerLayer = LayerMask.NameToLayer(
                FastenerToolRaycastLayer.Name);
            Assert.That(fastenerLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                generatedFasteners.All(value =>
                    value.gameObject.layer == fastenerLayer &&
                    value.GetComponentsInChildren<Renderer>(true).All(renderer =>
                        renderer.gameObject.layer == fastenerLayer)),
                Is.True,
                "All bolt and nut ray targets must share the dedicated bolt-gayka-only layer.");
            MeshRenderer donorFastenerRenderer = generatedFasteners[0]
                .GetComponentInChildren<MeshRenderer>(true);
            Assert.That(
                donorFastenerRenderer.GetComponent<MeshFilter>().sharedMesh.name,
                Is.EqualTo("bolt2"));
            Texture donorFastenerTexture = donorFastenerRenderer
                .sharedMaterial.GetTexture("_BaseColorMap");
            Assert.That(donorFastenerTexture, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(donorFastenerTexture)
                    .Replace('\\', '/'),
                Does.EndWith("/662715305f835b4448f1b4165566e97e.png"),
                "Temporary Phase 1 fasteners must use the donor BOLTS texture rather than the synthetic placeholder.");
            Assert.That(
                prefab.GetComponentsInChildren<AssemblyOwnedMountAuthoring>(true),
                Has.Length.EqualTo(52));
            Assert.That(
                prefab.GetComponentsInChildren<AssemblySurfaceMountHandoffTarget>(true),
                Has.Length.EqualTo(126));
            Assert.That(
                prefab.GetComponentsInChildren<AssemblyHingeMountAuthoring>(true),
                Has.Length.EqualTo(4));
            Assert.That(
                prefab.GetComponentsInChildren<AssemblyHingedPartInteractionTarget>(true),
                Has.Length.EqualTo(4));
            Assert.That(
                prefab.GetComponentsInChildren<AssemblyHoodReleaseInteractionTarget>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<AssemblyInstalledPartInteractionTarget>(true),
                Has.Length.EqualTo(125));
            Assert.That(
                VehicleAssemblyValidator.Validate(
                    assembly.Parts,
                    assembly.MountPoints,
                    assembly.Dependencies,
                    assembly.Tools)
                    .Where(issue =>
                        issue.Severity ==
                        VehicleAssemblyValidationSeverity.Error),
                Is.Empty);
        }

        [Test]
        public void GeneratedAssemblyDependenciesPreserveDonorOrder()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            AssemblyDependency[] dependencies = prefab
                .GetComponent<VehicleAssemblyController>()
                .Dependencies;
            Assert.That(
                dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.InstallRequiresInstalled &&
                    value.DependentPartDefinitionId ==
                    "vehicle.satsuma.part.wishbone-fl" &&
                    value.RelatedPartDefinitionId ==
                    "vehicle.satsuma.part.sub-frame"),
                Is.True);
            Assert.That(
                dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.InstallRequiresInstalled &&
                    value.DependentPartDefinitionId ==
                    "vehicle.satsuma.part.steering-column" &&
                    value.RelatedPartDefinitionId ==
                    "vehicle.satsuma.part.steering-rack"),
                Is.True);
            Assert.That(
                dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.RemovalBlockedWhileInstalled &&
                    value.DependentPartDefinitionId ==
                    "vehicle.satsuma.part.sub-frame" &&
                    value.RelatedPartDefinitionId ==
                    "vehicle.satsuma.part.wishbone-fr"),
                Is.True);
            Assert.That(
                dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.InstallRequiresInstalled &&
                    value.DependentPartDefinitionId ==
                    "vehicle.satsuma.part.steering-rod-fl" &&
                    value.RelatedPartDefinitionId ==
                    "vehicle.satsuma.part.spindle-fl"),
                Is.True);
            Assert.That(
                dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.InstallRequiresInstalled &&
                    value.DependentPartDefinitionId ==
                    "vehicle.satsuma.part.steering-rod-fr" &&
                    value.RelatedPartDefinitionId ==
                    "vehicle.satsuma.part.spindle-fr"),
                Is.True);
        }

        [Test]
        public void GeneratedMountHandoffsAreSmallAndRearSuspensionRequiresBoltedArmOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            AssemblyMountHandoffTarget[] handoffs = prefab
                .GetComponentsInChildren<AssemblyMountHandoffTarget>(true);
            Assert.That(handoffs, Is.Not.Empty);
            foreach (AssemblyMountHandoffTarget handoff in handoffs)
            {
                SphereCollider zone = handoff.GetComponent<SphereCollider>();
                Assert.That(zone, Is.Not.Null, handoff.name);
                Assert.That(zone.isTrigger, Is.True, handoff.name);
                if (handoff.name.StartsWith(
                        "mount.satsuma.wheel",
                        StringComparison.Ordinal))
                {
                    Assert.That(
                        zone.radius,
                        Is.EqualTo(0.24f).Within(0.00001f),
                        handoff.name);
                }
                else if (handoff.name.StartsWith(
                             "mount.satsuma.discbrake-",
                             StringComparison.Ordinal))
                {
                    Assert.That(
                        zone.radius,
                        Is.EqualTo(0.20f).Within(0.00001f),
                        handoff.name);
                }
                else
                {
                    Assert.That(
                        zone.radius,
                        Is.InRange(0.03f, 0.075f),
                        handoff.name);
                }
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                MountPointAuthoring springMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.coilspring-rl");
                MountPointAuthoring longSpringMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.long-coilspring-rl");
                MountPointAuthoring shockMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.shock-rl");
                MountPointAuthoring trailArmMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = assembly.MountPoints.Single(
                    mount => mount.MountId == "mount.satsuma.drum-brake-rl");

                Assert.That(
                    springMount.Definition.RequiredOccupiedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));
                Assert.That(
                    springMount.Definition.RequiredBoltedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));
                Assert.That(
                    springMount.Definition.BlockedWhileOccupiedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.long-coilspring-rl" }));
                foreach (string corner in new[] { "rl", "rr" })
                {
                    string shockMountId = "mount.satsuma.shock-" + corner;
                    foreach (string springMountId in new[]
                             {
                                 "mount.satsuma.coilspring-" + corner,
                                 "mount.satsuma.long-coilspring-" + corner,
                             })
                    {
                        MountPointAuthoring reviewedSpringMount = assembly
                            .MountPoints.Single(mount =>
                                mount.MountId == springMountId);
                        Assert.That(
                            reviewedSpringMount.Definition
                                .RemovalBlockedWhileOccupiedMountIds,
                            Is.EqualTo(new[] { shockMountId }),
                            springMountId);
                    }
                }
                Assert.That(
                    shockMount.Definition.RequiredAnyOccupiedMountIds,
                    Is.Empty,
                    "The donor shock does not require either spring variant.");
                Assert.That(
                    shockMount.Definition.RequiredOccupiedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));
                Assert.That(
                    shockMount.Definition.RequiredBoltedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));
                Assert.That(
                    drumMount.Definition.RequiredOccupiedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));
                Assert.That(
                    drumMount.Definition.RequiredBoltedMountIds,
                    Is.EqualTo(new[] { "mount.satsuma.trail-arm-rl" }));

                PartInstance trailArm = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                trailArm.transform.SetPositionAndRotation(
                    trailArmMount.Pose.position,
                    trailArmMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(trailArm, trailArmMount).Succeeded,
                    Is.True);

                Assert.That(
                    assembly.Graph.TryGetMount(
                        trailArmMount.MountId,
                        out MountPointRuntime trailArmRuntime),
                    Is.True);

                PartInstance spring = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.coil-spring-1");
                spring.transform.SetPositionAndRotation(
                    springMount.Pose.position,
                    springMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(spring, springMount).Succeeded,
                    Is.True,
                    "A donor socket accepts the spring on an installed loose " +
                    "arm; Bolted is a retention predicate, not an install gate.");

                PartInstance drum = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.drum-brake-1");
                drum.transform.SetPositionAndRotation(
                    drumMount.Pose.position,
                    drumMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(drum, drumMount).Succeeded,
                    Is.True,
                    "A loose arm still exposes its drum socket. The structural " +
                    "collapse behavior is covered by the P0 parity test.");

                PartInstance shock = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.shock-absorber-1");
                shock.transform.SetPositionAndRotation(
                    shockMount.Pose.position,
                    shockMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(shock, shockMount).Succeeded,
                    Is.True,
                    "A loose arm still exposes its shock socket; installing on " +
                    "that unsupported branch is allowed and then collapses.");

                ToolDefinition wrench12 = assembly.Tools.Single(value =>
                    value.Size == FastenerSize.Millimeter12);
                int turnsRemaining = 12;
                foreach (FastenerInstance fastener in trailArmRuntime.Fasteners)
                {
                    while (turnsRemaining > 0 &&
                           fastener.Stage < fastener.Definition.MaximumStage)
                    {
                        Assert.That(
                            assembly.TryOperateFastener(
                                trailArmMount.MountId,
                                fastener.Definition.DefinitionId,
                                wrench12,
                                tighten: true).Succeeded,
                            Is.True);
                        turnsRemaining--;
                    }
                }

                Assert.That(turnsRemaining, Is.Zero);
                Assert.That(trailArmRuntime.FastenerGroup.Tightness, Is.EqualTo(12));
                Assert.That(trailArmRuntime.FastenerGroup.IsBolted, Is.True);

                Assert.That(assembly.EvaluateInstall(drum, drumMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(spring, springMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(shock, shockMount).Succeeded, Is.True);

                Assert.That(
                    assembly.TryInstall(shock, shockMount).Succeeded,
                    Is.True);
                Assert.That(spring.IsInstalled, Is.False);
                Assert.That(drum.IsInstalled, Is.False);

                PartInstance alternateSpring = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.extra-long-coil-spring-1");
                alternateSpring.transform.SetPositionAndRotation(
                    longSpringMount.Pose.position,
                    longSpringMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(alternateSpring, longSpringMount)
                        .Succeeded,
                    Is.True,
                    "Stock and long springs are alternatives, but neither depends on the shock or drum.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void InstalledFrontAssemblySurfacesRouteTheNextDonorMountUnderTheCrosshair()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();

                PartInstance InstallAt(string mountId, bool tightenForNextMount = false)
                {
                    MountPointAuthoring mount = assembly.MountPoints.Single(
                        value => value.MountId == mountId);
                    PartInstance part = assembly.Parts.First(value =>
                        !value.IsInstalled && value.Definition != null &&
                        mount.Definition.AcceptsPart(
                            value.Definition.DefinitionId));
                    part.transform.SetPositionAndRotation(
                        mount.Pose.position,
                        mount.Pose.rotation);
                    Assert.That(
                        assembly.TryInstall(part, mount).Succeeded,
                        Is.True,
                        mountId + ": " + assembly.LastOperationResult.Message);
                    if (tightenForNextMount)
                    {
                        MountPointRuntime runtime = assembly.ResolveMount(mount);
                        FastenerDefinition fastener = mount.Definition.Fasteners[0];
                        ToolDefinition tool = assembly.Tools.First(value =>
                            value != null && value.Size == fastener.Size);
                        int attempts = 0;
                        while (!runtime.FastenerGroup.IsBolted)
                        {
                            Assert.That(attempts++, Is.LessThan(fastener.MaximumStage));
                            Assert.That(
                                assembly.TryOperateFastener(mountId, fastener.DefinitionId,
                                    tool, tighten: true).Succeeded,
                                Is.True);
                        }
                    }
                    return part;
                }

                void AssertRoutes(
                    PartInstance surfacePart,
                    string targetMountId)
                {
                    MountPointAuthoring mount = assembly.MountPoints.Single(
                        value => value.MountId == targetMountId);
                    PartInstance heldPart = assembly.Parts.First(value =>
                        !value.IsInstalled && value.Definition != null &&
                        mount.Definition.AcceptsPart(
                            value.Definition.DefinitionId));
                    heldPart.transform.SetPositionAndRotation(
                        mount.Pose.position,
                        mount.Pose.rotation);
                    AssemblySurfaceMountHandoffTarget surface = surfacePart
                        .GetComponent<AssemblySurfaceMountHandoffTarget>();
                    Assert.That(surface, Is.Not.Null, surfacePart.name);
                    var context = new MSC.Interaction.InteractionContext(
                        instance,
                        mount.Pose.position,
                        mount.Pose.forward);
                    Assert.That(
                        surface.CanAccept(heldPart.PickupTarget, context),
                        Is.True,
                        targetMountId + ": " + surface.HandoffPrompt);
                }

                PartInstance subFrame = InstallAt("mount.satsuma.sub-frame");
                AssertRoutes(subFrame, "mount.satsuma.wishbone-fl");
                PartInstance wishbone = InstallAt("mount.satsuma.wishbone-fl", tightenForNextMount: true);
                AssertRoutes(wishbone, "mount.satsuma.spindle-fl");
                PartInstance spindle = InstallAt("mount.satsuma.spindle-fl", tightenForNextMount: true);
                AssertRoutes(spindle, "mount.satsuma.strut-fl");
                InstallAt("mount.satsuma.strut-fl");
                AssertRoutes(spindle, "mount.satsuma.discbrake-fl");
                PartInstance disc = InstallAt("mount.satsuma.discbrake-fl");
                AssertRoutes(disc, "mount.satsuma.wheelfl-new");

                MountPointAuthoring directWheelMount = assembly.MountPoints
                    .Single(value => value.MountId ==
                        "mount.satsuma.wheelfl-new");
                Assert.That(
                    directWheelMount.GetComponent<InteractionTargetHost>()
                        .TryGetCapability(
                            out IParentColliderOcclusionBypass _),
                    Is.True,
                    "A wheel-well mount hidden by its own registered spindle " +
                    "or disc collider must remain selectable.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedMountsUseExactDonorPosesAndCompatiblePartDefinitions()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            Assert.That(
                assembly.MountPoints.Select(mount => mount.MountId).Distinct().Count(),
                Is.EqualTo(assembly.MountPoints.Length));
            foreach (MountPointAuthoring mount in assembly.MountPoints)
            {
                Assert.That(mount.Definition, Is.Not.Null);
                AssemblyOwnedMountAuthoring ownedMount =
                    mount.GetComponent<AssemblyOwnedMountAuthoring>();
                string expectedOwner = ownedMount != null
                    ? ownedMount.OwnerPart.Definition.DefinitionId
                    : "vehicle.satsuma.part.body-shell";
                Assert.That(
                    mount.Definition.OwnerPartDefinitionId,
                    Is.EqualTo(expectedOwner));
                foreach (string acceptedId in
                         mount.Definition.AcceptedPartDefinitionIds)
                {
                    PartInstance part = assembly.Parts.Single(candidate =>
                        candidate.Definition.DefinitionId == acceptedId);
                    Assert.That(
                        part.Definition.IsCompatibleWith(mount.Definition),
                        Is.True,
                        mount.MountId + " -> " + acceptedId);
                }
            }
        }

        [Test]
        public void RearSuspensionUsesLockedDonorPivotsAndNwhRuntimeAuthority()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.trail-arm-rl",
                new Vector3(-0.42300016f, -0.2153f, -0.85700095f),
                new Quaternion(
                    0.000000030908627f,
                    0.7071068f,
                    0.70710677f,
                    -0.000000030908623f));
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.trail-arm-rr",
                new Vector3(0.4230005f, -0.2153f, -0.85700065f),
                new Quaternion(
                    0.00000003090862f,
                    0.7071068f,
                    0.70710677f,
                    -0.000000030908613f));
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.coilspring-rl",
                new Vector3(-0.4300001f, -0.079f, -0.98564017f),
                new Quaternion(
                    0.116225995f,
                    -0.006005722f,
                    0.007918469f,
                    0.99317306f));
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.coilspring-rr",
                new Vector3(0.4300005f, -0.079f, -0.9850009f),
                new Quaternion(
                    0.11622601f,
                    -0.0060060006f,
                    0.007918001f,
                    0.99317306f));
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.shock-rl",
                new Vector3(-0.47f, 0.14f, -1.1500002f),
                new Quaternion(
                    -0.7071069f,
                    0f,
                    0.00000006181726f,
                    0.7071067f));
            AssertStaticRearMount(
                assembly,
                "mount.satsuma.shock-rr",
                new Vector3(0.46999958f, 0.1400002f, -1.1499999f),
                new Quaternion(
                    -0.7071069f,
                    -0.000000030908623f,
                    0.00000003090862f,
                    0.7071067f));

            AssertRearMountPose(
                assembly,
                "mount.satsuma.drum-brake-rl",
                "vehicle.satsuma.part.trail-arm-rl",
                new Vector3(
                    0.18000037f,
                    -0.31490782f,
                    -0.000143628f),
                new Quaternion(
                    -0.76663f,
                    -0.0000022593f,
                    0.00000218644f,
                    0.642089f));
            AssertRearMountPose(
                assembly,
                "mount.satsuma.drum-brake-rr",
                "vehicle.satsuma.part.trail-arm-rr",
                new Vector3(
                    -0.1799964f,
                    -0.31491333f,
                    -0.000137855f),
                new Quaternion(
                    0.00000466596f,
                    -0.766646f,
                    -0.64207f,
                    0.00000411877f));
            AssertRearMountPose(
                assembly,
                "mount.satsuma.wheelrl-new",
                "vehicle.satsuma.part.trail-arm-rl",
                new Vector3(
                    0.21999949f,
                    -0.31319135f,
                    -0.00074551045f),
                new Quaternion(
                    -0.6341037f,
                    0.00000061712484f,
                    -0.00000042188907f,
                    -0.77324796f));
            AssertRearMountPose(
                assembly,
                "mount.satsuma.wheelrr-new",
                "vehicle.satsuma.part.trail-arm-rr",
                new Vector3(
                    -0.22000039f,
                    -0.31318748f,
                    -0.0005988565f),
                new Quaternion(
                    -0.00000050477684f,
                    -0.752298f,
                    -0.65882295f,
                    -0.000001705379f));

            foreach (string rearWheelMountId in new[]
                     {
                         "mount.satsuma.wheelrl-new",
                         "mount.satsuma.wheelrr-new",
                     })
            {
                MountPointAuthoring rearWheelMount = assembly.MountPoints
                    .Single(value => value.MountId == rearWheelMountId);
                Assert.That(
                    rearWheelMount.Definition.RequiredOccupiedMountIds,
                    Does.Contain(rearWheelMountId.EndsWith(
                            "rl-new",
                            StringComparison.Ordinal)
                        ? "mount.satsuma.drum-brake-rl"
                        : "mount.satsuma.drum-brake-rr"));
            }

            foreach (string roadWheelMountId in new[]
                     {
                         "mount.satsuma.wheelfl-new",
                         "mount.satsuma.wheelfr-new",
                         "mount.satsuma.wheelrl-new",
                         "mount.satsuma.wheelrr-new",
                     })
            {
                MountPointAuthoring roadWheelMount = assembly.MountPoints
                    .Single(value => value.MountId == roadWheelMountId);
                Assert.That(
                    Vector3.Distance(
                        roadWheelMount.Pose.localPosition,
                        roadWheelMountId.Contains(
                            "wheelf",
                            StringComparison.Ordinal)
                            ? new Vector3(-0.043f, 0f, 0f)
                            : new Vector3(-0.040f, 0f, 0f)),
                    Is.LessThan(0.00001f),
                    roadWheelMountId +
                    " donor-standard installed-wheel seating");
                Assert.That(
                    Quaternion.Angle(
                        roadWheelMount.Pose.localRotation,
                        Quaternion.identity),
                    Is.LessThan(0.001f),
                    roadWheelMountId +
                    " must inherit the reviewed mount orientation");
            }

            AssemblyInstalledPhysicsLink[] links = prefab
                .GetComponentsInChildren<AssemblyInstalledPhysicsLink>(true);
            Assert.That(
                links.Count(value => value.LinkMode ==
                    AssemblyInstalledPhysicsLinkMode.TrailingArmHinge),
                Is.EqualTo(2),
                "Trailing-arm hinge metadata remains available for assembly " +
                "handoff; it is not the active sprung-force authority while " +
                "rear NWH support is enabled.");
            Assert.That(
                links.Count(value => value.LinkMode ==
                    AssemblyInstalledPhysicsLinkMode.FrontWishboneHinge),
                Is.Zero,
                "Donor installed front wishbones are transform-IK " +
                "presentation, not free gravitational hinges.");
            Assert.That(
                links.Count(value => value.LinkMode ==
                    AssemblyInstalledPhysicsLinkMode.Fixed),
                Is.EqualTo(3));
            Assert.That(
                links.Count(value => value.LinkMode ==
                    AssemblyInstalledPhysicsLinkMode.RoadWheelAxle),
                Is.EqualTo(8));
            foreach (AssemblyInstalledPhysicsLink fixedLink in links.Where(
                         value => value.LinkMode ==
                             AssemblyInstalledPhysicsLinkMode.Fixed))
            {
                Assert.That(
                    fixedLink.WeldedProjectionDistance,
                    Is.EqualTo(0.001f).Within(0.00001f));
                Assert.That(
                    fixedLink.WeldedProjectionAngle,
                    Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(
                    fixedLink.InstalledSolverIterations,
                    Is.GreaterThanOrEqualTo(20));
                Assert.That(
                    fixedLink.InstalledSolverVelocityIterations,
                    Is.GreaterThanOrEqualTo(8));
            }

            string[] acceptedRegularRoadWheelIds =
            {
                "vehicle.satsuma.part.wheel-stock-fl",
                "vehicle.satsuma.part.wheel-stock-fr",
                "vehicle.satsuma.part.wheel-stock-rl",
                "vehicle.satsuma.part.wheel-stock-rr",
                "vehicle.satsuma.part.wheel-gt-fl",
                "vehicle.satsuma.part.wheel-gt-fr",
                "vehicle.satsuma.part.wheel-gt-rl",
                "vehicle.satsuma.part.wheel-gt-rr",
            };
            foreach (string roadWheelMountId in new[]
                     {
                         "mount.satsuma.wheelfl-new",
                         "mount.satsuma.wheelfr-new",
                         "mount.satsuma.wheelrl-new",
                         "mount.satsuma.wheelrr-new",
                     })
            {
                MountPointAuthoring wheelMount = assembly.MountPoints.Single(
                    value => value.MountId == roadWheelMountId);
                Assert.That(
                    wheelMount.Definition.AcceptedPartDefinitionIds,
                    Is.EquivalentTo(acceptedRegularRoadWheelIds),
                    roadWheelMountId +
                    " must accept exactly the eight reviewed regular wheels");
            }

            AssemblyInstalledPhysicsLink[] wheelLinks = assembly.Parts
                .Where(value => value.Definition != null &&
                    (value.Definition.DefinitionId.StartsWith(
                         "vehicle.satsuma.part.wheel-stock-",
                         StringComparison.Ordinal) ||
                     value.Definition.DefinitionId.StartsWith(
                         "vehicle.satsuma.part.wheel-gt-",
                         StringComparison.Ordinal)))
                .Select(value => value.GetComponent<
                    AssemblyInstalledPhysicsLink>())
                .ToArray();
            Assert.That(wheelLinks, Has.Length.EqualTo(8));
            Assert.That(
                wheelLinks,
                Is.All.Matches<AssemblyInstalledPhysicsLink>(value =>
                    value != null &&
                    value.LinkMode ==
                        AssemblyInstalledPhysicsLinkMode.RoadWheelAxle &&
                    value.ConnectedBodyOverride == null));

            SatsumaRearSuspensionController controller = prefab
                .GetComponent<SatsumaRearSuspensionController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Corners.Count, Is.EqualTo(2));
            Assert.That(controller.ExternalWheelAuthority, Is.True);

            NwhAssemblyWheelSupportController support = prefab
                .GetComponent<NwhAssemblyWheelSupportController>();
            Assert.That(support, Is.Not.Null);
            Assert.That(support.Bindings, Has.Length.EqualTo(4));
            for (int index = 2; index < 4; index++)
            {
                string corner = index == 2 ? "rl" : "rr";
                Vector3 wheelAnchor = index == 2
                    ? new Vector3(-0.6029993f, 0f, -1.1670003f)
                    : new Vector3(0.603001f, 0f, -1.1669996f);
                NwhAssemblyWheelSupportBinding binding =
                    support.Bindings[index];
                Assert.That(binding.Enabled, Is.True, corner);
                Assert.That(binding.Wheel, Is.Not.Null, corner);
                Assert.That(binding.StageProfile.Enabled, Is.True, corner);
                Assert.That(
                    binding.StageProfile.RoadWheelMountId,
                    Is.EqualTo("mount.satsuma.wheel" + corner + "-new"));
                Assert.That(
                    binding.StageProfile.DamperMountId,
                    Is.EqualTo("mount.satsuma.shock-" + corner));
                Assert.That(
                    binding.StageProfile.AssemblyContactRadiusMeters,
                    Is.EqualTo(0.0871f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.AssemblyContactWidthMeters,
                    Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.RimContactRadiusMeters,
                    Is.EqualTo(0.163018f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.RimContactWidthMeters,
                    Is.EqualTo(0.148608f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.RoadWheelRadiusMeters,
                    Is.EqualTo(0.272667f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.RoadWheelWidthMeters,
                    Is.EqualTo(0.15057f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.SpringOnlyDamperRate,
                    Is.EqualTo(2f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.DampedBumpRate,
                    Is.EqualTo(1000f).Within(0.00001f));
                Assert.That(
                    binding.StageProfile.DampedReboundRate,
                    Is.EqualTo(1000f).Within(0.00001f));

                NwhAssemblySuspensionStageProfile profile =
                    binding.SuspensionProfile;
                Assert.That(profile.Enabled, Is.True, corner);
                Assert.That(
                    profile.SpringMountId,
                    Is.EqualTo("mount.satsuma.coilspring-" + corner));
                Assert.That(
                    profile.AlternateSpringMountId,
                    Is.EqualTo("mount.satsuma.long-coilspring-" + corner));

                Vector3 noSpringTop = wheelAnchor;
                noSpringTop.y = -0.15f;
                AssertRearNwhStage(
                    profile.Unstrung,
                    noSpringTop,
                    0.14f,
                    0.28f,
                    corner + " no spring");
                Vector3 stockTop = wheelAnchor;
                stockTop.y = -0.165f;
                AssertRearNwhStage(
                    profile.Sprung,
                    stockTop,
                    0.14f,
                    2968f,
                    corner + " stock spring");
                Vector3 longTop = wheelAnchor;
                longTop.y = -0.18f;
                AssertRearNwhStage(
                    profile.AlternateSprung,
                    longTop,
                    0.17f,
                    4930f,
                    corner + " long spring");
            }

            SatsumaRearNwhSuspensionController nwhController = prefab
                .GetComponent<SatsumaRearNwhSuspensionController>();
            Assert.That(nwhController, Is.Not.Null);
            Assert.That(nwhController.AssemblyController, Is.SameAs(assembly));
            Assert.That(
                nwhController.PresentationController,
                Is.SameAs(controller));
            Assert.That(nwhController.Corners, Has.Length.EqualTo(2));
            Assert.That(
                nwhController.Corners.Select(value => value.CornerId),
                Is.EqualTo(new[] { "rl", "rr" }));
            for (int index = 0; index < nwhController.Corners.Length; index++)
            {
                string corner = index == 0 ? "rl" : "rr";
                SatsumaRearNwhCornerBinding nwhCorner =
                    nwhController.Corners[index];
                Assert.That(
                    nwhCorner.Wheel,
                    Is.SameAs(support.Bindings[index + 2].Wheel));
                Assert.That(
                    nwhCorner.TrailingArmMount,
                    Is.SameAs(assembly.MountPoints.Single(value =>
                        value.MountId ==
                        "mount.satsuma.trail-arm-" + corner)));
                Assert.That(
                    nwhCorner.DrumMountId,
                    Is.EqualTo("mount.satsuma.drum-brake-" + corner));
                Assert.That(
                    nwhCorner.RoadWheelMountId,
                    Is.EqualTo("mount.satsuma.wheel" + corner + "-new"));
                Assert.That(
                    nwhCorner.StockSpringMountId,
                    Is.EqualTo("mount.satsuma.coilspring-" + corner));
                Assert.That(
                    nwhCorner.LongSpringMountId,
                    Is.EqualTo("mount.satsuma.long-coilspring-" + corner));
            }
            SatsumaRearSuspensionPartPresentation[] presentations = prefab
                .GetComponentsInChildren<
                    SatsumaRearSuspensionPartPresentation>(true);
            Assert.That(
                presentations.Count(value => value.IsSpring),
                Is.EqualTo(4));
            Assert.That(
                presentations.Count(value => value.IsShock),
                Is.EqualTo(2));
            foreach (SatsumaRearSuspensionPartPresentation presentation in
                     presentations.Where(value => value.IsSpring))
            {
                Assert.That(
                    presentation.SuspensionController,
                    Is.SameAs(controller),
                    "Every loose rear spring must switch to the NWH-driven " +
                    "presentation rig before its install handoff starts.");
            }

            Assert.That(
                VehicleAssemblyController.InstallTransitionDurationSeconds,
                Is.EqualTo(0.17f).Within(0.00001f),
                "The player-requested install flight is twice as fast as " +
                "the former 0.34 second handoff.");
        }

        [Test]
        public void FrontSuspensionUsesDonorHubHierarchyAndNwhTravelAuthority()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            SatsumaFrontSuspensionController controller = prefab
                .GetComponent<SatsumaFrontSuspensionController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.AssemblyController, Is.SameAs(assembly));
            Assert.That(
                controller.ChassisColliders.Count(value => value != null),
                Is.EqualTo(29));
            Assert.That(controller.Corners, Has.Length.EqualTo(2));
            Assert.That(
                controller.Corners.Select(value => value.CornerId),
                Is.EqualTo(new[] { "fl", "fr" }));

            NwhAssemblyWheelSupportController support = prefab
                .GetComponent<NwhAssemblyWheelSupportController>();
            Assert.That(support, Is.Not.Null);
            for (int index = 0; index < 2; index++)
            {
                string corner = index == 0 ? "fl" : "fr";
                SatsumaFrontSuspensionCornerBinding binding =
                    controller.Corners[index];
                NwhAssemblyWheelSupportBinding supportBinding =
                    support.Bindings[index];
                Assert.That(supportBinding.Enabled, Is.True);
                Assert.That(
                    supportBinding.RequiredOccupiedMountIds,
                    Is.EqualTo(new[]
                    {
                        "mount.satsuma.wishbone-" + corner,
                    }),
                    "Donor Wishbone/Data activates wheel contact before the " +
                    "spindle or strut is installed.");
                Assert.That(supportBinding.SuspensionProfile.Enabled, Is.True);
                Assert.That(supportBinding.SuspensionProfile.SpringMountId,
                    Is.EqualTo("mount.satsuma.strut-" + corner));
                Assert.That(supportBinding.SuspensionProfile.Unstrung.TravelMeters,
                    Is.EqualTo(0.20f).Within(0.00001f));
                Assert.That(supportBinding.SuspensionProfile.Unstrung.MaximumForceNewtons,
                    Is.EqualTo(0.4f).Within(0.00001f),
                    "Donor 2 N/m * 0.20 m, not a supporting 2 kN/m spring.");
                Assert.That(supportBinding.SuspensionProfile.Unstrung.TopLocalPosition.y,
                    Is.EqualTo(-0.15f).Within(0.00001f));
                Assert.That(supportBinding.SuspensionProfile.Sprung.TravelMeters,
                    Is.EqualTo(0.18f).Within(0.00001f),
                    "The accepted complete-strut stage is preserved.");
                Assert.That(supportBinding.StageProfile.Enabled, Is.True);
                Assert.That(
                    supportBinding.StageProfile.RoadWheelMountId,
                    Is.EqualTo(index == 0
                        ? "mount.satsuma.wheelfl-new"
                        : "mount.satsuma.wheelfr-new"));
                Assert.That(
                    supportBinding.StageProfile.DamperMountId,
                    Is.EqualTo("mount.satsuma.strut-" + corner));
                Assert.That(
                    supportBinding.StageProfile.AssemblyContactRadiusMeters,
                    Is.EqualTo(0.12f).Within(0.00001f));
                Assert.That(
                    supportBinding.StageProfile.RimContactRadiusMeters,
                    Is.EqualTo(0.163018f).Within(0.00001f));
                Assert.That(
                    supportBinding.StageProfile.RimContactWidthMeters,
                    Is.EqualTo(0.148608f).Within(0.00001f));
                Assert.That(
                    supportBinding.StageProfile.RoadWheelRadiusMeters,
                    Is.EqualTo(0.272667f).Within(0.00001f));
                Assert.That(
                    supportBinding.StageProfile.RoadWheelWidthMeters,
                    Is.EqualTo(0.15057f).Within(0.00001f));

                WheelController wheel = binding.Wheel;
                Assert.That(wheel, Is.SameAs(supportBinding.Wheel));
                Assert.That(supportBinding.SuspensionProfile.Sprung.MaximumForceNewtons,
                    Is.EqualTo(wheel.SpringMaxForce).Within(0.00001f));
                Assert.That(wheel.Camber, Is.EqualTo(-1.4f).Within(0.001f));
                Assert.That(wheel.SpringMaxLength,
                    Is.EqualTo(0.18f).Within(0.0001f));
                Vector3 expectedLoadedHub = index == 0
                    ? new Vector3(
                        -0.6299995f,
                        -0.25873545f,
                        1.1669996f)
                    : new Vector3(
                        0.6300007f,
                        -0.25873807f,
                        1.1669996f);
                Assert.That(
                    Vector3.Distance(
                        wheel.transform.localPosition,
                        expectedLoadedHub + Vector3.up * 0.18f),
                    Is.LessThan(0.00001f),
                    corner + " NWH spring top");
                Assert.That(
                    Vector3.Distance(
                        binding.FullDroopHubLocalPosition,
                        expectedLoadedHub),
                    Is.LessThan(0.00001f));
                Vector3 expectedNoStrutHub = index == 0
                    ? new Vector3(-0.6299995f, -0.35f, 1.1669996f)
                    : new Vector3(0.6300007f, -0.35f, 1.167f);
                Assert.That(
                    Vector3.Distance(
                        binding.CanonicalNoStrutHubLocalPosition,
                        expectedNoStrutHub),
                    Is.LessThan(0.00001f),
                    corner + " canonical donor no-strut airborne hub");
                Assert.That(
                    Quaternion.Angle(
                        binding.CanonicalNoStrutHubLocalRotation,
                        Quaternion.identity),
                    Is.LessThan(0.001f));

                Assert.That(binding.WishboneMount, Is.Not.Null);
                Assert.That(binding.SpindleMount, Is.Not.Null);
                Assert.That(binding.StrutMount, Is.Not.Null);
                Assert.That(binding.ShockBottomTarget, Is.Not.Null);
                Assert.That(binding.StrutPresentation, Is.Not.Null);
                Assert.That(binding.SteeringRodMount, Is.Not.Null);
                Assert.That(binding.DiscBrakeMount, Is.Not.Null);
                Assert.That(binding.RoadWheelMount, Is.Not.Null);
                Assert.That(binding.SteeringOuterTarget, Is.Not.Null);
                Assert.That(binding.SteeringRodPresentation, Is.Not.Null);
                Assert.That(
                    Vector3.Distance(
                        binding.WishboneMount.transform.localPosition,
                        binding.WishboneBodyPivotLocalPosition),
                    Is.LessThan(0.00001f),
                    corner + " wishbone body pivot");
                Vector3 expectedSpindle = expectedNoStrutHub +
                    binding.CanonicalNoStrutHubLocalRotation *
                    binding.HubToSpindleMeshLocalOffset;
                Assert.That(
                    Vector3.Distance(
                        binding.SpindleMount.transform.localPosition,
                        expectedSpindle),
                    Is.LessThan(0.00001f),
                    corner + " visible spindle follows donor no-strut hub");
                Vector3 expectedShockBottom = expectedNoStrutHub +
                    binding.CanonicalNoStrutHubLocalRotation *
                    binding.HubToShockBottomLocalOffset;
                Assert.That(
                    Vector3.Distance(
                        binding.ShockBottomTarget.localPosition,
                        expectedShockBottom),
                    Is.LessThan(0.00001f),
                    corner + " strut lower bone follows physical hub");
                Vector3 expectedSteeringOuter = expectedNoStrutHub +
                    binding.CanonicalNoStrutHubLocalRotation *
                    binding.HubToSteeringOuterLocalOffset;
                Assert.That(
                    Vector3.Distance(
                        binding.SteeringOuterTarget.localPosition,
                        expectedSteeringOuter),
                    Is.LessThan(0.00001f),
                    corner + " steering outer bone follows the non-rotating hub");
                Vector3 expectedDisc = expectedNoStrutHub +
                    binding.CanonicalNoStrutHubLocalRotation *
                    binding.HubToDiscBrakeLocalOffset;
                Assert.That(
                    Vector3.Distance(
                        binding.DiscBrakeMount.transform.localPosition,
                        expectedDisc),
                    Is.LessThan(0.00001f),
                    corner + " disc brake donor offset");
                Vector3 expectedRoadWheel = expectedNoStrutHub +
                    binding.CanonicalNoStrutHubLocalRotation *
                    binding.HubToRoadWheelLocalOffset;
                Assert.That(
                    Vector3.Distance(
                        binding.RoadWheelMount.transform.localPosition,
                        expectedRoadWheel),
                    Is.LessThan(0.00001f),
                    corner + " road wheel donor offset");
                Assert.That(
                    binding.DiscBrakeMount.Definition
                        .RequiredOccupiedMountIds,
                    Is.EqualTo(new[]
                    {
                        "mount.satsuma.spindle-" + corner,
                        "mount.satsuma.strut-" + corner,
                    }));
                Assert.That(
                    binding.RoadWheelMount.Definition
                        .RequiredOccupiedMountIds,
                    Is.EqualTo(new[]
                    {
                        "mount.satsuma.discbrake-" + corner,
                    }));

                SatsumaFrontStrutPresentation presentation =
                    binding.StrutPresentation;
                Assert.That(presentation.Part.IsInstalled, Is.False);
                Assert.That(presentation.LooseRenderer.enabled, Is.True);
                Assert.That(presentation.InstalledRenderer.enabled, Is.False);
                Assert.That(
                    presentation.InstalledRenderer.sharedMesh,
                    Is.SameAs(
                        presentation.LooseRenderer
                            .GetComponent<MeshFilter>().sharedMesh));
                Assert.That(
                    presentation.InstalledRenderer.sharedMesh.bindposes,
                    Has.Length.EqualTo(2));
                Assert.That(
                    presentation.InstalledRenderer.bones,
                    Has.Length.EqualTo(2));
                Assert.That(
                    presentation.InstalledRenderer.rootBone,
                    Is.SameAs(presentation.InstalledRenderer.bones[0]));
                Assert.That(
                    presentation.InstalledRenderer.bones[1],
                    Is.SameAs(binding.ShockBottomTarget));
                Assert.That(
                    presentation.InstalledUpperBone,
                    Is.SameAs(presentation.InstalledRenderer.bones[0]));
                Assert.That(
                    presentation.InstalledLowerTarget,
                    Is.SameAs(binding.ShockBottomTarget));

                SatsumaFrontSteeringRodPresentation steeringPresentation =
                    binding.SteeringRodPresentation;
                Assert.That(steeringPresentation.Part.IsInstalled, Is.False);
                Assert.That(
                    steeringPresentation.LooseRenderer.enabled,
                    Is.True);
                Assert.That(
                    steeringPresentation.InstalledRenderer.enabled,
                    Is.False);
                Assert.That(
                    steeringPresentation.InstalledRenderer.sharedMesh,
                    Is.SameAs(
                        steeringPresentation.LooseRenderer
                            .GetComponent<MeshFilter>().sharedMesh));
                Assert.That(
                    steeringPresentation.InstalledRenderer.sharedMesh
                        .bindposes,
                    Has.Length.EqualTo(2));
                Assert.That(
                    steeringPresentation.InstalledRenderer.bones,
                    Has.Length.EqualTo(2));
                Assert.That(
                    steeringPresentation.InstalledRenderer.rootBone,
                    Is.SameAs(steeringPresentation.InstalledInnerBone));
                Assert.That(
                    steeringPresentation.InstalledRenderer.bones[1],
                    Is.SameAs(binding.SteeringOuterTarget));
                Assert.That(
                    steeringPresentation.InstalledOuterTarget,
                    Is.SameAs(binding.SteeringOuterTarget));

                PartInstance wishbone = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.wishbone-" + corner);
                PartInstance spindle = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.spindle-" + corner);
                Assert.That(
                    wishbone.GetComponent<AssemblyInstalledPhysicsLink>(),
                    Is.Null,
                    corner + " donor IK wishbone must remain kinematic");
                Assert.That(
                    spindle.GetComponent<AssemblyInstalledPhysicsLink>(),
                    Is.Null,
                    corner + " donor hub presentation must remain kinematic");
            }


            PartInstance subframe = assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId ==
                "vehicle.satsuma.part.sub-frame");
            Assert.That(
                subframe.GetComponent<AssemblyInstalledPhysicsLink>().LinkMode,
                Is.EqualTo(AssemblyInstalledPhysicsLinkMode.Fixed));

            MountPointAuthoring steeringRack = assembly.MountPoints.Single(
                value => value.MountId == "mount.satsuma.steering-rack");
            Assert.That(
                Vector3.Distance(
                    steeringRack.transform.localPosition,
                    new Vector3(
                        2.99885869E-07f,
                        -0.192317009f,
                        1.00799644f)),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(
                    steeringRack.transform.localRotation,
                    new Quaternion(
                        -1.49020264E-06f,
                        0.707106769f,
                        0.707106948f,
                        1.58693615E-06f)),
                Is.LessThan(0.001f));
            MountPointAuthoring steeringColumn = assembly.MountPoints.Single(
                value => value.MountId == "mount.satsuma.steering-column");
            Assert.That(
                Vector3.Distance(
                    steeringColumn.transform.localPosition,
                    new Vector3(
                        -0.234629244f,
                        0.07962226f,
                        0.6509674f)),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(
                    steeringColumn.transform.localRotation,
                    new Quaternion(
                        0.00668044249f,
                        0.4470459f,
                        0.894293547f,
                        -0.0185724664f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void StockRoadWheelsAreStackedBesideSatsumaForAssemblyTesting()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            string[] definitionIds =
            {
                "vehicle.satsuma.part.wheel-stock-fl",
                "vehicle.satsuma.part.wheel-stock-fr",
                "vehicle.satsuma.part.wheel-stock-rl",
                "vehicle.satsuma.part.wheel-stock-rr",
            };

            for (int index = 0; index < definitionIds.Length; index++)
            {
                PartInstance wheel = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId == definitionIds[index]);
                Vector3 expectedPosition = new Vector3(
                    -1.05f,
                    -0.4275f + index * 0.19f,
                    -0.35f);

                Assert.That(wheel.gameObject.activeSelf, Is.True);
                Assert.That(wheel.IsInstalled, Is.False);
                Assert.That(
                    Vector3.Distance(
                        wheel.transform.localPosition,
                        expectedPosition),
                    Is.LessThan(0.00001f),
                    definitionIds[index] + " temporary test-stack position");
                Assert.That(
                    Quaternion.Angle(
                        wheel.transform.localRotation,
                        Quaternion.Euler(0f, 0f, 90f)),
                    Is.LessThan(0.001f),
                    definitionIds[index] + " lies flat in the test stack");
            }
        }

        [Test]
        public void GeneratedRoadWheelsDistinguishBareRimFromInstalledTyre()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            PartInstance[] roadWheels = assembly.Parts
                .Where(value => value.Definition != null &&
                    (value.Definition.DefinitionId.StartsWith(
                         "vehicle.satsuma.part.wheel-stock-",
                         StringComparison.Ordinal) ||
                     value.Definition.DefinitionId.StartsWith(
                         "vehicle.satsuma.part.wheel-gt-",
                         StringComparison.Ordinal)))
                .OrderBy(value => value.Definition.DefinitionId,
                    StringComparer.Ordinal)
                .ToArray();
            Assert.That(roadWheels, Has.Length.EqualTo(8));

            foreach (PartInstance wheel in roadWheels)
            {
                string definitionId = wheel.Definition.DefinitionId;
                bool stock = definitionId.StartsWith(
                    "vehicle.satsuma.part.wheel-stock-",
                    StringComparison.Ordinal);
                AssemblyWheelTireState state = wheel.GetComponent<
                    AssemblyWheelTireState>();
                Assert.That(state, Is.Not.Null, definitionId);
                Assert.That(state.HasTire, Is.EqualTo(stock), definitionId);
                Assert.That(
                    state.RimRadiusMeters,
                    Is.EqualTo(0.163018f).Within(0.00001f),
                    definitionId);
                Assert.That(
                    state.TireRadiusMeters,
                    Is.EqualTo(0.272667f).Within(0.00001f),
                    definitionId);
                Assert.That(
                    state.CurrentContactRadiusMeters,
                    Is.EqualTo(stock ? 0.272667f : 0.163018f)
                        .Within(0.00001f),
                    definitionId);
                Assert.That(state.TireRenderers, Has.Length.EqualTo(1));
                Assert.That(state.TireColliders, Has.Length.EqualTo(1));
                Assert.That(
                    state.TireRenderers[0].enabled,
                    Is.EqualTo(stock),
                    definitionId + " tyre renderer");
                Assert.That(
                    state.TireColliders[0].enabled,
                    Is.EqualTo(stock),
                    definitionId + " tyre collider");
            }
        }

        [Test]
        public void GeneratedFrontMudflapMountsFollowTheirFendersAndUseOneDonorBolt()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            AssertFrontMudflapMount(
                assembly,
                "mount.satsuma.fender-left.mudflap-fl",
                "vehicle.satsuma.part.fender-left",
                "vehicle.satsuma.part.mudflap-fl",
                new Vector3(-0.026500687f, -0.35480016f, -0.24299999f));
            AssertFrontMudflapMount(
                assembly,
                "mount.satsuma.fender-right.mudflap-fr",
                "vehicle.satsuma.part.fender-right",
                "vehicle.satsuma.part.mudflap-fr",
                new Vector3(0.026499934f, -0.3548f, -0.24299993f));
        }

        private static void AssertFrontMudflapMount(
            VehicleAssemblyController assembly,
            string mountId,
            string ownerPartId,
            string acceptedPartId,
            Vector3 expectedLocalPosition)
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == mountId);
            AssemblyOwnedMountAuthoring owned =
                mount.GetComponent<AssemblyOwnedMountAuthoring>();

            Assert.That(owned, Is.Not.Null);
            Assert.That(owned.OwnerPart.Definition.DefinitionId, Is.EqualTo(ownerPartId));
            Assert.That(mount.transform.parent, Is.EqualTo(owned.OwnerPart.transform));
            Assert.That(
                mount.Definition.AcceptedPartDefinitionIds,
                Is.EqualTo(new[] { acceptedPartId }));
            Assert.That(
                Vector3.Distance(mount.transform.localPosition, expectedLocalPosition),
                Is.LessThan(0.00001f));
            Assert.That(mount.Definition.Fasteners, Has.Length.EqualTo(1));
        }

        [Test]
        public void GeneratedOwnedEngineMountFollowsLooseOwnerAndAcceptsItsChild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                PartInstance block = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.engine-block");
                PartInstance crankshaft = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.crankshaft");
                MountPointAuthoring crankshaftMount = assembly.MountPoints.Single(mount =>
                    mount.MountId == "mount.satsuma.engine-block.crankshaft");
                AssemblyOwnedMountAuthoring ownedMount =
                    crankshaftMount.GetComponent<AssemblyOwnedMountAuthoring>();

                Assert.That(ownedMount, Is.Not.Null);
                Assert.That(ownedMount.OwnerPart, Is.SameAs(block));
                Assert.That(crankshaftMount.transform.parent, Is.EqualTo(block.transform));
                Assert.That(block.IsInstalled, Is.False);

                Vector3 authoredLocalPosition = crankshaftMount.transform.localPosition;
                Quaternion authoredLocalRotation = crankshaftMount.transform.localRotation;
                block.transform.SetPositionAndRotation(
                    block.transform.position + new Vector3(0.8f, 0.3f, -0.5f),
                    Quaternion.Euler(0f, 73f, 0f));
                Assert.That(
                    crankshaftMount.transform.localPosition,
                    Is.EqualTo(authoredLocalPosition));
                Assert.That(
                    Quaternion.Angle(
                        crankshaftMount.transform.localRotation,
                        authoredLocalRotation),
                    Is.LessThan(0.001f));

                crankshaft.transform.SetPositionAndRotation(
                    crankshaftMount.Pose.position,
                    crankshaftMount.Pose.rotation);
                AssemblyOperationResult result =
                    assembly.TryInstall(crankshaft, crankshaftMount);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(crankshaft.IsInstalled, Is.True);
                Assert.That(
                    crankshaft.RuntimeState.InstalledMountId,
                    Is.EqualTo(crankshaftMount.MountId));

                Vector3 installedLocalPosition = crankshaft.transform.localPosition;
                block.transform.position += Vector3.right;
                Assert.That(crankshaft.transform.localPosition, Is.EqualTo(installedLocalPosition));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedPistonMountRequiresCrankshaftAndBlocksItsRemoval()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                PartInstance crankshaft = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.crankshaft");
                PartInstance piston = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.piston1");
                MountPointAuthoring crankshaftMount = assembly.MountPoints.Single(mount =>
                    mount.MountId == "mount.satsuma.engine-block.crankshaft");
                MountPointAuthoring pistonMount = assembly.MountPoints.Single(mount =>
                    mount.MountId == "mount.satsuma.engine-block.piston1");

                piston.transform.SetPositionAndRotation(
                    pistonMount.Pose.position,
                    pistonMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(piston, pistonMount).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));

                crankshaft.transform.SetPositionAndRotation(
                    crankshaftMount.Pose.position,
                    crankshaftMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(crankshaft, crankshaftMount).Succeeded,
                    Is.True);
                Assert.That(
                    assembly.EvaluateInstall(piston, pistonMount).Succeeded,
                    Is.True);
                Assert.That(
                    assembly.TryInstall(piston, pistonMount).Succeeded,
                    Is.True);
                Assert.That(
                    assembly.EvaluateRemoval(crankshaft).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedEngineAssemblyMountUsesTriangulatedPoseAndThreeElevenMillimeterBolts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                PartInstance block = assembly.Parts.Single(part =>
                    part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.engine-block");
                MountPointAuthoring engineMount = assembly.MountPoints.Single(mount =>
                    mount.MountId == "mount.satsuma.engine-assembly");
                MountPointRuntime runtime = assembly.ResolveMount(engineMount);

                Assert.That(
                    Vector3.Distance(
                        engineMount.transform.localPosition,
                        new Vector3(
                            -0.0015974252f,
                            -0.0347443372f,
                            1.31011021f)),
                    Is.LessThan(0.00001f));
                Assert.That(runtime.Fasteners, Has.Length.EqualTo(3));
                Assert.That(
                    runtime.Fasteners.Select(value => value.Definition.Size),
                    Is.All.EqualTo(FastenerSize.Millimeter11));

                block.transform.SetPositionAndRotation(
                    engineMount.Pose.position + Vector3.right * 0.65f,
                    engineMount.Pose.rotation * Quaternion.Euler(0f, 180f, 0f));
                Assert.That(
                    assembly.EvaluateInstall(block, engineMount).Succeeded,
                    Is.False,
                    "The strict authoring query must still reject a badly aligned part.");
                AssemblySurfaceMountHandoffTarget chassisSurface = assembly.Parts
                    .Single(part => part.IsAssemblyRoot)
                    .GetComponent<AssemblySurfaceMountHandoffTarget>();
                var context = new MSC.Interaction.InteractionContext(
                    instance,
                    engineMount.Pose.position,
                    engineMount.Pose.forward);
                Assert.That(
                    chassisSurface.CanAccept(block.PickupTarget, context),
                    Is.True,
                    chassisSurface.HandoffPrompt);
                chassisSurface.Accept(block.PickupTarget, context);
                Assert.That(block.RuntimeState.InstalledMountId, Is.EqualTo(engineMount.MountId));
                Assert.That(block.transform.parent, Is.EqualTo(engineMount.Pose));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedNewGameRosterExposesStockSuspensionAndCleanPaintedPanels()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            string[] stockSuspensionIds =
            {
                "vehicle.satsuma.part.sub-frame",
                "vehicle.satsuma.part.wishbone-fl",
                "vehicle.satsuma.part.wishbone-fr",
                "vehicle.satsuma.part.spindle-fl",
                "vehicle.satsuma.part.spindle-fr",
                "vehicle.satsuma.part.strut-fl",
                "vehicle.satsuma.part.strut-fr",
                "vehicle.satsuma.part.trail-arm-rl",
                "vehicle.satsuma.part.trail-arm-rr",
                "vehicle.satsuma.part.coil-spring-1",
                "vehicle.satsuma.part.coil-spring-2",
                "vehicle.satsuma.part.shock-absorber-1",
                "vehicle.satsuma.part.shock-absorber-2",
                "vehicle.satsuma.part.disc-brake-1",
                "vehicle.satsuma.part.disc-brake-2",
                "vehicle.satsuma.part.drum-brake-1",
                "vehicle.satsuma.part.drum-brake-2",
                "vehicle.satsuma.part.steering-rack",
                "vehicle.satsuma.part.steering-rod-fl",
                "vehicle.satsuma.part.steering-rod-fr",
                "vehicle.satsuma.part.steering-column",
            };
            foreach (string definitionId in stockSuspensionIds)
            {
                PartInstance part = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == definitionId);
                Assert.That(part.gameObject.activeSelf, Is.True, definitionId);
                Assert.That(
                    assembly.MountPoints.Any(mount =>
                        mount.Definition.AcceptedPartDefinitionIds.Contains(
                            definitionId)),
                    Is.True,
                    definitionId + " has no installable mount.");
            }

            VehiclePaintStateController paint =
                prefab.GetComponent<VehiclePaintStateController>();
            Assert.That(paint.PaintableRendererCount, Is.EqualTo(7));
            Assert.That(paint.PaintType, Is.EqualTo(VehiclePaintType.NoChange));
            Assert.That(paint.PaintMaterialProfiles.Count, Is.EqualTo(7));
            CollectionAssert.AreEquivalent(
                SatsumaPaintSurfaceIds.All,
                paint.PaintSurfaceBindings.Select(value => value.SurfaceId),
                "The donor stores body, both doors, both fenders, hood and " +
                "bootlid as seven independent paint surfaces.");
            Material donorRustPaint = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Materials/be77a835916639d47b5dfdf770515a88.mat");
            Material donorBodyMasse = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Materials/c2f3bd2a8b811844c9abc854f1f1731f.mat");
            Assert.That(donorRustPaint, Is.Not.Null);
            Assert.That(donorBodyMasse, Is.Not.Null);
            Assert.That(
                donorRustPaint.GetFloat("_Metallic"),
                Is.EqualTo(0.5f).Within(0.0001f),
                "The new-game body must retain the donor CAR_PAINT_RUSTY " +
                "metal response instead of the importer fallback.");
            Assert.That(
                donorRustPaint.GetFloat("_Smoothness"),
                Is.EqualTo(0.3f).Within(0.0001f),
                "The new-game body must retain the donor CAR_PAINT_RUSTY " +
                "glossiness instead of looking like the matte fallback.");
            Color donorRustBaseColor = donorRustPaint.GetColor("_BaseColor");
            Assert.That(
                donorRustBaseColor.r,
                Is.EqualTo(0.44705883f).Within(0.0001f));
            Assert.That(
                donorRustBaseColor.g,
                Is.EqualTo(0.40784314f).Within(0.0001f));
            Assert.That(
                donorRustBaseColor.b,
                Is.EqualTo(0.09019608f).Within(0.0001f));
            Material donorRegularPaint = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Materials/a7920469770f2054e95e0192995f5c8e.mat");
            Material donorMetallicPaint = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Materials/45f05d54331fec44fbb1d7e0d8471b04.mat");
            Material donorMattePaint = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Materials/9346388ac807d004eb03249a37391bbf.mat");
            Assert.That(donorRegularPaint, Is.Not.Null);
            Assert.That(donorMetallicPaint, Is.Not.Null);
            Assert.That(donorMattePaint, Is.Not.Null);
            Assert.That(
                donorRegularPaint.GetFloat("_Metallic"),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                donorRegularPaint.GetFloat("_Smoothness"),
                Is.EqualTo(0.587f).Within(0.0001f));
            Assert.That(
                donorMetallicPaint.GetFloat("_Metallic"),
                Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(
                donorMetallicPaint.GetFloat("_Smoothness"),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                donorMattePaint.GetFloat("_Metallic"),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                donorMattePaint.GetFloat("_Smoothness"),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                donorRustPaint.GetTexture("_BaseColorMap"),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                    "Textures/5ef54390c1883c242b295a6b26b94055.png")),
                "The new-game paint must tint the donor starting-rust texture, " +
                "not replace it with a flat colour.");
            Assert.That(
                donorRustPaint.GetTexture("_DetailMap"),
                Is.Not.Null,
                "The donor car_rust detail albedo/normal must survive HDRP conversion.");
            Assert.That(
                donorRustPaint.GetTexture("_MaskMap"),
                Is.Not.Null,
                "The donor rust mask and metallic/smoothness map must survive HDRP conversion.");
            Vector2 rustDetailScale = donorRustPaint.GetTextureScale("_DetailMap");
            Assert.That(rustDetailScale.x, Is.EqualTo(22f).Within(0.0001f));
            Assert.That(rustDetailScale.y, Is.EqualTo(22f).Within(0.0001f));
            foreach (VehiclePaintSurfaceBinding binding in
                     paint.PaintSurfaceBindings)
            {
                Assert.That(binding.Renderer, Is.Not.Null);
                Assert.That(binding.MaterialIndices, Is.Not.Empty);
                foreach (int materialIndex in binding.MaterialIndices)
                {
                    Assert.That(
                        binding.Renderer.sharedMaterials[materialIndex],
                        Is.EqualTo(donorRustPaint));
                    Assert.That(
                        binding.Renderer.sharedMaterials[materialIndex],
                        Is.Not.EqualTo(donorBodyMasse));
                }
            }
            Color selected = new Color32(25, 117, 184, 255);
            Assert.That(
                paint.TryApplyPlayerSelectedPaint(4, selected, out string failure),
                Is.True,
                failure);
            Assert.That(paint.PaintType, Is.EqualTo(VehiclePaintType.NoChange));

            (string definitionId, string rendererPrefix)[] panelRendererPrefixes =
            {
                ("vehicle.satsuma.part.door-left", "door_left_clone"),
                ("vehicle.satsuma.part.door-right", "door_right_clone"),
                ("vehicle.satsuma.part.fender-left", "fender_left_clone"),
                ("vehicle.satsuma.part.fender-right", "fender_right_clone"),
                ("vehicle.satsuma.part.hood", "hood_clone"),
                ("vehicle.satsuma.part.bootlid", "bootlid_clone"),
            };
            foreach ((string definitionId, string rendererPrefix) in
                     panelRendererPrefixes)
            {
                PartInstance panel = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == definitionId);
                Renderer exterior = panel
                    .GetComponentsInChildren<Renderer>(true)
                    .Single(value => value.name.StartsWith(
                        rendererPrefix,
                        System.StringComparison.OrdinalIgnoreCase));
                var block = new MaterialPropertyBlock();
                exterior.GetPropertyBlock(block, 0);
                Assert.That(
                    Vector4.Distance(
                        block.GetColor("_BaseColor"),
                        selected),
                    Is.LessThan(0.001f),
                    definitionId);
            }

            Assert.That(
                paint.TryApplyPaint(
                    4,
                    selected,
                    VehiclePaintType.Metallic,
                    out failure),
                Is.True,
                failure);
            Assert.That(paint.PaintType, Is.EqualTo(VehiclePaintType.Metallic));
            foreach (VehiclePaintSurfaceBinding binding in
                     paint.PaintSurfaceBindings)
            {
                foreach (int materialIndex in binding.MaterialIndices)
                {
                    Assert.That(
                        binding.Renderer.sharedMaterials[materialIndex],
                        Is.EqualTo(donorMetallicPaint));
                }
            }

            string[] forbiddenDecorationNames =
            {
                "rally_sticker",
                "regplaterear",
                "turbo_",
            };
            Transform[] panelTransforms = assembly.Parts
                .Where(part => panelRendererPrefixes.Any(value =>
                    value.definitionId == part.Definition.DefinitionId))
                .SelectMany(part => part.GetComponentsInChildren<Transform>(true))
                .ToArray();
            foreach (string forbidden in forbiddenDecorationNames)
            {
                Transform[] decorations = panelTransforms
                    .Where(value => value.name.Contains(
                        forbidden,
                        System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.That(decorations, Is.Not.Empty, forbidden);
                Assert.That(
                    decorations.All(value => !value.gameObject.activeSelf),
                    Is.True,
                    forbidden + " must be hidden on the stock panels.");
            }

            PartInstance bootlid = assembly.Parts.Single(part =>
                part.Definition.DefinitionId ==
                "vehicle.satsuma.part.bootlid");
            Transform bootlidHandleGarnish = bootlid
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "bootlid_emblem_78487");
            Assert.That(
                bootlidHandleGarnish.gameObject.activeSelf,
                Is.True,
                "The donor-active datsun_bootlid_001 exterior garnish is the " +
                "visible bootlid handle assembly, not optional badge clutter.");
            Mesh bootlidHandleMesh = bootlidHandleGarnish
                .GetComponent<MeshFilter>()
                .sharedMesh;
            Assert.That(bootlidHandleMesh, Is.Not.Null);
            Assert.That(
                bootlidHandleMesh.name,
                Is.EqualTo("datsun_bootlid_001"));
            Assert.That(
                Vector3.Distance(
                    bootlidHandleMesh.bounds.extents,
                    new Vector3(0.280406f, 0.031166509f, 0.108053f)),
                Is.LessThan(0.00001f),
                "The exact donor handle/garnish mesh must remain attached; a " +
                "synthetic placeholder must not cover the bootlid opening.");

            GameObject bodyPresentation = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "Satsuma_BodyPresentation.prefab");
            Transform fixedBootlidHooks = bodyPresentation
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "hooks_78823");
            Transform looseBootlidHooks = bootlid
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "hooks_77027");
            Assert.That(fixedBootlidHooks.gameObject.activeSelf, Is.True);
            Assert.That(fixedBootlidHooks.parent,
                Is.EqualTo(bodyPresentation.transform),
                "The donor body-side copy starts beneath pivot_bootlid while " +
                "the loose bootlid is not assembled.");
            Assert.That(
                fixedBootlidHooks.GetComponent<MeshFilter>().sharedMesh.name,
                Is.EqualTo("bootlid_hooks"));
            Assert.That(looseBootlidHooks.gameObject.activeSelf, Is.False,
                "The bootlid-owned hook copy starts inactive while the part is loose.");
            AssemblyHingedPartInteractionTarget bootlidHinge = bootlid
                .GetComponent<AssemblyHingedPartInteractionTarget>();
            Assert.That(bootlidHinge, Is.Not.Null);
            Assert.That(
                bootlidHinge.InstalledOnlyPresentationObjects.Count,
                Is.EqualTo(1));
            Assert.That(
                bootlidHinge.InstalledOnlyPresentationObjects[0].name,
                Is.EqualTo("hooks_77027"),
                "Assembly FSM &104306 activates the copy parented to the " +
                "moving bootlid.");
            Assert.That(
                bootlidHinge.DetachedOnlyPresentationObjects.Count,
                Is.EqualTo(1));
            Assert.That(
                bootlidHinge.DetachedOnlyPresentationObjects[0].name,
                Is.EqualTo("hooks_78823"),
                "Removal FSM &110021 restores the chassis-side copy only " +
                "after the bootlid is detached.");
        }

        [Test]
        public void GeneratedCabinGlassUsesDonorTransparencyAndShadowPolicy()
        {
            const string root =
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            Material windshield = AssetDatabase.LoadAssetAtPath<Material>(
                root + "Materials/ac664fa7a2ad68d4ca5eca5c8b6c02cf.mat");
            Material cabinGlass = AssetDatabase.LoadAssetAtPath<Material>(
                root + "Materials/423931766c7a0b14ea1fa5c0f98790a4.mat");
            Assert.That(windshield, Is.Not.Null);
            Assert.That(cabinGlass, Is.Not.Null);
            foreach (Material glass in new[] { windshield, cabinGlass })
            {
                Assert.That(glass.shader.name, Is.EqualTo("HDRP/Lit"));
                Assert.That(glass.GetFloat("_SurfaceType"), Is.EqualTo(1f));
                Assert.That(glass.GetFloat("_ZWrite"), Is.Zero);
                Assert.That(glass.renderQueue,
                    Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent));
                Assert.That(
                    glass.GetTag("RenderType", false),
                    Is.EqualTo("Transparent"));
                Assert.That(glass.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                    Is.True);
                Assert.That(glass.GetTexture("_BaseColorMap"), Is.Not.Null,
                    glass.name + " must retain donor window alpha texture.");
            }

            Assert.That(
                windshield.GetColor("_BaseColor").r,
                Is.EqualTo(0.8161765f).Within(0.0001f));
            Assert.That(
                windshield.GetFloat("_Smoothness"),
                Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(
                cabinGlass.GetColor("_BaseColor").r,
                Is.EqualTo(0.9264706f).Within(0.0001f));
            Assert.That(
                cabinGlass.GetFloat("_Smoothness"),
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(
                windshield.GetTexture("_BaseColorMap"),
                Is.EqualTo(cabinGlass.GetTexture("_BaseColorMap")));

            Renderer[] allRenderers = prefab.GetComponentsInChildren<Renderer>(true);
            Renderer[] windshieldRenderers = allRenderers.Where(value =>
                    value.sharedMaterials.Contains(windshield))
                .ToArray();
            Renderer[] cabinRenderers = allRenderers.Where(value =>
                    value.sharedMaterials.Contains(cabinGlass))
                .ToArray();
            Assert.That(windshieldRenderers, Has.Length.EqualTo(1));
            Assert.That(cabinRenderers, Has.Length.EqualTo(4));
            foreach (Renderer renderer in windshieldRenderers.Concat(
                         cabinRenderers))
            {
                Assert.That(renderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off), renderer.name);
                Assert.That(renderer.receiveShadows, Is.True, renderer.name);
            }

            Texture2D neutralRainDetail =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    root + "Textures/glass_rain_neutral_detail.png");
            Assert.That(neutralRainDetail, Is.Not.Null);
            foreach (Material glass in new[] { windshield, cabinGlass })
            {
                Assert.That(
                    glass.GetTexture("_DetailMap"),
                    Is.EqualTo(neutralRainDetail));
                Assert.That(glass.IsKeywordEnabled("_DETAIL_MAP"), Is.True);
            }

            VehicleGlassRainPresenter rain =
                prefab.GetComponent<VehicleGlassRainPresenter>();
            Assert.That(rain, Is.Not.Null);
            Assert.That(
                rain.WindowRenderers.Count,
                Is.EqualTo(5));
            Assert.That(
                rain.WindowRenderers,
                Is.EquivalentTo(
                    windshieldRenderers.Concat(cabinRenderers)));
            Assert.That(
                rain.RainOpacityByRenderer,
                Is.EqualTo(new[] { 0.5f, 0.2f, 0.2f, 0.2f, 0.2f }));
            Assert.That(
                rain.TextureSize,
                Is.EqualTo(VehicleGlassRainPresenter.DonorTextureSize));
            Assert.That(
                rain.GravityMultiplier,
                Is.EqualTo(
                    VehicleGlassRainPresenter.DonorGravityMultiplier));
            Assert.That(rain.RainTypes.Count, Is.EqualTo(3));
            Assert.That(
                rain.RainTypes.Select(value =>
                    value.DropsPerFrameAt60Fps),
                Is.EqualTo(new[] { 0, 450, 900 }));
            Assert.That(
                rain.RainTypes.Select(value => value.DryingSpeed),
                Is.EqualTo(new[] { 2.45f, 2f, 2f }));
            Assert.That(
                rain.RainTypes.Select(value => value.DropSizePixels),
                Is.EqualTo(new[] { 1f, 3f, 4f }));
            Assert.That(
                rain.VehicleBody,
                Is.EqualTo(prefab.GetComponent<Rigidbody>()));
        }

        [Test]
        public void VehicleGlassRainPresenterGeneratesDropsOnlyWhenExposed()
        {
            var root = new GameObject("glass-rain-validation");
            var glass = new GameObject("glass-renderer");
            glass.transform.SetParent(root.transform, false);
            MeshRenderer renderer = glass.AddComponent<MeshRenderer>();
            Rigidbody body = root.AddComponent<Rigidbody>();
            VehicleGlassRainPresenter rain =
                root.AddComponent<VehicleGlassRainPresenter>();
            rain.ConfigureForAuthoring(
                new Renderer[] { renderer },
                new[] { VehicleGlassRainPresenter.DonorFrontRainOpacity },
                body);

            try
            {
                rain.SimulateForValidation(
                    rain01: 0.75f,
                    windMetersPerSecond: 0f,
                    exposure01: 0f,
                    deltaSeconds: 1f / 12f);
                Assert.That(rain.LiveWetPixelCount, Is.Zero);

                rain.SimulateForValidation(
                    rain01: 0.75f,
                    windMetersPerSecond: 0f,
                    exposure01: 1f,
                    deltaSeconds: 1f / 12f);
                Assert.That(rain.LiveWetPixelCount, Is.GreaterThan(0));
                Assert.That(rain.TextureUpdateCount, Is.GreaterThanOrEqualTo(3U));
                Assert.That(rain.RainDetailTexture, Is.Not.Null);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(
                    block.GetTexture(Shader.PropertyToID("_DetailMap")),
                    Is.EqualTo(rain.RainDetailTexture));
                Assert.That(
                    block.GetFloat(Shader.PropertyToID("_DetailNormalScale")),
                    Is.EqualTo(
                        VehicleGlassRainPresenter.DonorFrontRainOpacity)
                        .Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedStockFendersDoNotBakeInstalledMudflapPlaceholders()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            var expectations = new[]
            {
                new
                {
                    FenderId = "vehicle.satsuma.part.fender-left",
                    MudflapId = "vehicle.satsuma.part.mudflap-fl",
                    MountId = "mount.satsuma.fender-left.mudflap-fl",
                },
                new
                {
                    FenderId = "vehicle.satsuma.part.fender-right",
                    MudflapId = "vehicle.satsuma.part.mudflap-fr",
                    MountId = "mount.satsuma.fender-right.mudflap-fr",
                },
            };

            foreach (var expected in expectations)
            {
                PartInstance fender = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == expected.FenderId);
                Renderer[] bakedMudflaps = fender
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(value => value.name.Contains(
                        "mudflap",
                        System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.That(
                    bakedMudflaps,
                    Is.Empty,
                    expected.FenderId +
                    " must not contain the donor ActivateThis mudflap copy.");

                PartInstance separateMudflap = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == expected.MudflapId);
                Assert.That(
                    separateMudflap.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty,
                    expected.MudflapId + " must remain a visible separate part.");

                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == expected.MountId);
                Assert.That(
                    mount.Definition.OwnerPartDefinitionId,
                    Is.EqualTo(expected.FenderId),
                    expected.MountId);
                Assert.That(
                    mount.Definition.AcceptedPartDefinitionIds,
                    Is.EquivalentTo(new[] { expected.MudflapId }),
                    expected.MountId);
                Assert.That(
                    mount.transform.IsChildOf(fender.transform),
                    Is.True,
                    expected.MountId + " must travel with its fender owner.");
                AssemblyOwnedMountAuthoring ownedMount =
                    mount.GetComponent<AssemblyOwnedMountAuthoring>();
                Assert.That(ownedMount, Is.Not.Null, expected.MountId);
                Assert.That(
                    ownedMount.OwnerPart,
                    Is.EqualTo(fender),
                    expected.MountId);
                Assert.That(
                    ownedMount.RequireInstalledOwner,
                    Is.False,
                    expected.MountId +
                    " mirrors the donor active trigger nested under the loose fender.");
            }
        }

        [Test]
        public void GeneratedStockAndGtWheelsPreserveDonorRimMaterials()
        {
            const string root =
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            Material rusty = AssetDatabase.LoadAssetAtPath<Material>(
                root + "Materials/8dfb833f348cbed4bb4a2993a4bc797f.mat");
            Material metallic = AssetDatabase.LoadAssetAtPath<Material>(
                root + "Materials/a1c2918870a9f154c95ff2a5abfae8be.mat");
            Assert.That(rusty, Is.Not.Null);
            Assert.That(metallic, Is.Not.Null);

            Texture2D bakedBase = AssetDatabase.LoadAssetAtPath<Texture2D>(
                root + "Textures/rim_rust_base_color_hdrp.png");
            Texture2D packedDetail = AssetDatabase.LoadAssetAtPath<Texture2D>(
                root + "Textures/rim_rust_detail_hdrp.png");
            Texture2D packedMask = AssetDatabase.LoadAssetAtPath<Texture2D>(
                root + "Textures/rim_rust_mask_hdrp.png");
            Texture2D donorSpecGloss = AssetDatabase.LoadAssetAtPath<Texture2D>(
                root + "Textures/5c58d7ab0f2101b44a2a4baa242b4ec2.png");
            Assert.That(bakedBase, Is.Not.Null);
            Assert.That(packedDetail, Is.Not.Null);
            Assert.That(packedMask, Is.Not.Null);
            Assert.That(donorSpecGloss, Is.Not.Null);
            Assert.That(rusty.GetTexture("_BaseColorMap"), Is.EqualTo(bakedBase));
            Assert.That(rusty.GetTexture("_DetailMap"), Is.EqualTo(packedDetail));
            Assert.That(rusty.GetTexture("_MaskMap"), Is.EqualTo(packedMask));
            Assert.That(
                rusty.GetTexture("_SpecularColorMap"),
                Is.EqualTo(donorSpecGloss));
            Assert.That(
                rusty.GetFloat("_MaterialID"),
                Is.EqualTo((float)MaterialId.LitSpecular));
            Assert.That(rusty.GetFloat("_Metallic"), Is.Zero);
            Assert.That(
                rusty.GetFloat("_Smoothness"),
                Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(
                rusty.GetFloat("_SmoothnessRemapMax"),
                Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(
                rusty.GetFloat("_DetailNormalScale"),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                rusty.GetColor("_BaseColor").r,
                Is.EqualTo(0.9647059f).Within(0.0001f));
            Assert.That(
                rusty.GetColor("_SpecularColor").r,
                Is.EqualTo(0.6764706f).Within(0.0001f));
            Assert.That(rusty.IsKeywordEnabled("_DETAIL_MAP"), Is.True);
            Assert.That(rusty.IsKeywordEnabled("_MASKMAP"), Is.True);
            Assert.That(rusty.IsKeywordEnabled("_SPECULARCOLORMAP"), Is.True);
            Assert.That(
                rusty.IsKeywordEnabled("_MATERIAL_FEATURE_SPECULAR_COLOR"),
                Is.True);

            TextureImporter baseImporter = AssetImporter.GetAtPath(
                root + "Textures/rim_rust_base_color_hdrp.png") as
                TextureImporter;
            TextureImporter detailImporter = AssetImporter.GetAtPath(
                root + "Textures/rim_rust_detail_hdrp.png") as
                TextureImporter;
            TextureImporter maskImporter = AssetImporter.GetAtPath(
                root + "Textures/rim_rust_mask_hdrp.png") as
                TextureImporter;
            Assert.That(baseImporter, Is.Not.Null);
            Assert.That(detailImporter, Is.Not.Null);
            Assert.That(maskImporter, Is.Not.Null);
            Assert.That(baseImporter.sRGBTexture, Is.True);
            Assert.That(detailImporter.sRGBTexture, Is.False);
            Assert.That(maskImporter.sRGBTexture, Is.False);

            Assert.That(
                metallic.GetColor("_BaseColor").r,
                Is.EqualTo(0.97794116f).Within(0.0001f));
            Assert.That(
                metallic.GetFloat("_Metallic"),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                metallic.GetFloat("_Smoothness"),
                Is.EqualTo(0.975f).Within(0.0001f));

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            foreach (string corner in new[] { "fl", "fr", "rl", "rr" })
            {
                PartInstance stock = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.wheel-stock-" + corner);
                Renderer stockRim = stock
                    .GetComponentsInChildren<Renderer>(true)
                    .Single(value => value.name.StartsWith(
                        "wheel_steel",
                        System.StringComparison.OrdinalIgnoreCase));
                Assert.That(
                    stockRim.sharedMaterials,
                    Is.All.EqualTo(rusty),
                    stock.Definition.DefinitionId);

                PartInstance gt = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.wheel-gt-" + corner);
                Renderer gtInner = gt
                    .GetComponentsInChildren<Renderer>(true)
                    .Single(value => value.name.StartsWith(
                        "inner_",
                        System.StringComparison.OrdinalIgnoreCase));
                Renderer gtOuter = gt
                    .GetComponentsInChildren<Renderer>(true)
                    .Single(value => value.name.StartsWith(
                        "wheel_gt",
                        System.StringComparison.OrdinalIgnoreCase));
                Assert.That(gtInner.sharedMaterials, Is.All.EqualTo(rusty));
                Assert.That(gtOuter.sharedMaterials, Is.All.EqualTo(metallic));
            }
        }

        [Test]
        public void LooseBodyPanelsArePhysicalPickupsAndSteeringColumnInstallsAfterRack()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                var context = new MSC.Interaction.InteractionContext(
                    instance,
                    instance.transform.position,
                    instance.transform.forward);
                foreach (string definitionId in new[]
                         {
                             "vehicle.satsuma.part.door-left",
                             "vehicle.satsuma.part.door-right",
                             "vehicle.satsuma.part.hood",
                             "vehicle.satsuma.part.bootlid",
                         })
                {
                    PartInstance panel = assembly.Parts.Single(value =>
                        value.Definition.DefinitionId == definitionId);
                    PhysicsPickupTarget pickup =
                        panel.GetComponent<PhysicsPickupTarget>();
                    Assert.That(pickup, Is.Not.Null, definitionId);
                    Assert.That(
                        pickup.CanPickup(context),
                        Is.True,
                        definitionId + " must be carryable while loose.");
                }

                InstallAtOnlyCompatibleMount(
                    assembly,
                    "vehicle.satsuma.part.sub-frame");
                InstallAtOnlyCompatibleMount(
                    assembly,
                    "vehicle.satsuma.part.steering-rack");
                PartInstance column = InstallAtOnlyCompatibleMount(
                    assembly,
                    "vehicle.satsuma.part.steering-column");
                Assert.That(column.IsInstalled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void StockBodyPanelsUseExactDonorShortBoltsAndNoDeadCopies()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            var expectations = new[]
            {
                (mountId: "mount.satsuma.bumper-rear",
                    partId: "vehicle.satsuma.part.bumper-rear",
                    count: 2, size: FastenerSize.Millimeter8,
                    aggregate: 16, boltedOn: 6),
                (mountId: "mount.satsuma.fender-right",
                    partId: "vehicle.satsuma.part.fender-right",
                    count: 5, size: FastenerSize.Millimeter5,
                    aggregate: 40, boltedOn: 28),
                (mountId: "mount.satsuma.grille",
                    partId: "vehicle.satsuma.part.grille",
                    count: 2, size: FastenerSize.Millimeter6,
                    aggregate: 16, boltedOn: 6),
                (mountId: "mount.satsuma.bootlid",
                    partId: "vehicle.satsuma.part.bootlid",
                    count: 4, size: FastenerSize.Millimeter6,
                    aggregate: 32, boltedOn: 24),
                (mountId: "mount.satsuma.door-left",
                    partId: "vehicle.satsuma.part.door-left",
                    count: 4, size: FastenerSize.Millimeter10,
                    aggregate: 32, boltedOn: 28),
                (mountId: "mount.satsuma.hood",
                    partId: "vehicle.satsuma.part.hood",
                    count: 4, size: FastenerSize.Millimeter6,
                    aggregate: 32, boltedOn: 8),
                (mountId: "mount.satsuma.door-right",
                    partId: "vehicle.satsuma.part.door-right",
                    count: 4, size: FastenerSize.Millimeter10,
                    aggregate: 32, boltedOn: 28),
                (mountId: "mount.satsuma.bumper-front",
                    partId: "vehicle.satsuma.part.bumper-front",
                    count: 2, size: FastenerSize.Millimeter8,
                    aggregate: 16, boltedOn: 6),
                (mountId: "mount.satsuma.fender-left",
                    partId: "vehicle.satsuma.part.fender-left",
                    count: 5, size: FastenerSize.Millimeter5,
                    aggregate: 40, boltedOn: 28),
            };

            Assert.That(expectations.Sum(value => value.count), Is.EqualTo(32));
            foreach (var expected in expectations)
            {
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == expected.mountId);
                Assert.That(
                    mount.Definition.Fasteners,
                    Has.Length.EqualTo(expected.count),
                    expected.mountId);
                Assert.That(
                    mount.Definition.Fasteners.Select(value => value.Size),
                    Is.All.EqualTo(expected.size),
                    expected.mountId);
                Assert.That(
                    mount.Definition.Fasteners.Select(value =>
                        value.MaximumStage),
                    Is.All.EqualTo(8),
                    expected.mountId);
                Assert.That(
                    mount.Definition.Fasteners,
                    Is.All.Matches<FastenerDefinition>(value =>
                        value.InsertedOnInstall && value.RequiredForRemoval),
                    expected.mountId);
                Assert.That(
                    mount.Definition.FastenerGroup.AggregateMaximumTightness,
                    Is.EqualTo(expected.aggregate),
                    expected.mountId);
                Assert.That(
                    mount.Definition.FastenerGroup.BoltedOnThreshold,
                    Is.EqualTo(expected.boltedOn),
                    expected.mountId);
                Assert.That(
                    mount.Definition.FastenerGroup.BoltedOffThreshold,
                    Is.Zero,
                    expected.mountId);

                PartInstance part = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId == expected.partId);
                AssemblyFastenerInteractionTarget[] targets = mount
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(
                        true);
                Assert.That(targets, Has.Length.EqualTo(expected.count));
                Renderer[] fastenerVisuals = targets.Select(value => value
                        .GetComponent<InteractionTargetHost>()
                        .OutlineRenderers.Single())
                    .ToArray();
                for (int targetIndex = 0;
                     targetIndex < targets.Length;
                     targetIndex++)
                {
                    Assert.That(
                        targets[targetIndex].ResolveOutlineRenderer(),
                        Is.SameAs(fastenerVisuals[targetIndex]),
                        expected.mountId +
                        " hover must outline the authored bolt, not its panel.");
                }
                Assert.That(
                    fastenerVisuals.Select(value => value
                        .GetComponent<MeshFilter>().sharedMesh.name),
                    Is.All.EqualTo("bolt"),
                    expected.mountId +
                    " donor body fasteners are short bolts, not nuts.");
                Assert.That(
                    fastenerVisuals.Select(value => value.enabled),
                    Is.All.True,
                    expected.mountId +
                    " bolts must remain visible on the loose donor panel.");
                Assert.That(
                    fastenerVisuals.Select(value => value.transform.parent),
                    Is.All.EqualTo(part.transform),
                    expected.mountId +
                    " bolt presentation must follow the loose/installed part.");
                Assert.That(
                    targets.Select(value => value
                        .GetComponentInChildren<MeshFilter>(true)),
                    Is.All.Null,
                    expected.mountId +
                    " must not keep a second mount-owned bolt renderer.");
                if (expected.partId == "vehicle.satsuma.part.door-left" ||
                    expected.partId == "vehicle.satsuma.part.door-right")
                {
                    Assert.That(
                        fastenerVisuals.Select(value =>
                            value.transform.localScale.x),
                        Is.All.EqualTo(0.9f).Within(0.0001f),
                        expected.mountId +
                        " donor door bolt child scale is exactly 0.9.");
                }

                MeshRenderer[] deadCopies = part
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Where(value =>
                    {
                        string name = value.gameObject.name;
                        return name.StartsWith(
                                   "bolt",
                                   StringComparison.OrdinalIgnoreCase) &&
                               name.Length > 4 &&
                               char.IsDigit(name[4]);
                    })
                    .ToArray();
                Assert.That(
                    deadCopies,
                    Is.Empty,
                    expected.partId +
                    " must not retain always-visible donor bolt copies.");
            }
        }

        [Test]
        public void BodyAuxiliaryPresentationPreservesDonorHandednessAndHiddenOptions()
        {
            GameObject rightDoor = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "LoosePartPresentations/66117_door_right_Clone_.prefab");
            GameObject stockSteeringWheel = AssetDatabase
                .LoadAssetAtPath<GameObject>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/" +
                    "Satsuma/LoosePartPresentations/" +
                    "42060_stock_steering_wheel_Clone_.prefab");
            GameObject frontBumper = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "LoosePartPresentations/66293_bumper_front_Clone_.prefab");
            GameObject bootlid = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/" +
                "LoosePartPresentations/57097_bootlid_Clone_.prefab");
            Assert.That(rightDoor, Is.Not.Null);
            Assert.That(stockSteeringWheel, Is.Not.Null);
            Assert.That(frontBumper, Is.Not.Null);
            Assert.That(bootlid, Is.Not.Null);

            Transform handle = rightDoor.transform.Find("handle_75660");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(
                Quaternion.Angle(handle.localRotation, Quaternion.identity),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(
                    handle.localScale,
                    new Vector3(-1f, 1f, 1f)),
                Is.LessThan(0.00001f),
                "The donor mirrors the complete right handle/keyhole child " +
                "with negative X scale; decomposing its matrix moved both.");

            Transform wheelCover = stockSteeringWheel
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "wheel_cover_xxxxx__79959");
            Assert.That(wheelCover.gameObject.activeSelf, Is.False,
                "The optional steering-wheel cover is a separate installation.");
            Transform frontPlate = frontBumper
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "RegPlateFront_81385");
            Transform rearPlate = bootlid
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "RegPlateRear_75336");
            Assert.That(frontPlate.gameObject.activeSelf, Is.False);
            Assert.That(rearPlate.gameObject.activeSelf, Is.False,
                "Inspection plates are hidden until the separate donor " +
                "registration/installation flow enables them.");
        }

        [Test]
        public void GrilleAndSteeringFastenersMatchReviewedDonorMounts()
        {
            const string configurationPath = "Config/DonorPaths.local.json";
            if (!File.Exists(configurationPath))
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }

            var paths = MSC.LegacyImport.Editor.Configuration
                .DonorPathConfiguration.LoadFromFile(configurationPath);
            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(Path.Combine(
                paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/" +
                "ExportedProject/Assets/_Scenes/GAME.unity"));
            DonorTransformRecord satsuma = scene.GetUniqueTransformByPath(
                "SATSUMA(557kg, 248)");
            scene.GetTransformRelativeTo(
                68091L,
                satsuma.TransformId,
                out Vector3 expectedGrillePosition,
                out Quaternion expectedGrilleRotation,
                out _);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            MountPointAuthoring grille = assembly.MountPoints.Single(value =>
                value.MountId == "mount.satsuma.grille");
            Assert.That(
                Vector3.Distance(
                    grille.transform.localPosition,
                    expectedGrillePosition),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(
                    grille.transform.localRotation,
                    expectedGrilleRotation),
                Is.LessThan(0.001f),
                "The grille mounts at donor Pivot1, not at the sideways " +
                "trigger transform.");

            MountPointAuthoring wheel = assembly.MountPoints.Single(value =>
                value.MountId == "mount.satsuma.steering-wheel");
            Assert.That(wheel.Definition.Fasteners, Has.Length.EqualTo(1));
            Assert.That(
                wheel.Definition.Fasteners.Single().Size,
                Is.EqualTo(FastenerSize.Millimeter10));
            Assert.That(
                wheel.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(
                    true).Single().GetComponent<InteractionTargetHost>()
                    .OutlineRenderers.Single()
                    .GetComponent<MeshFilter>().sharedMesh.name,
                Is.EqualTo("bolt2"),
                "Both donor steering-wheel variants use one central 10 mm nut.");

            MountPointAuthoring column = assembly.MountPoints.Single(value =>
                value.MountId == "mount.satsuma.steering-column");
            Assert.That(column.Definition.Fasteners, Has.Length.EqualTo(2));
            Assert.That(
                column.Definition.Fasteners.Select(value => value.Size),
                Is.All.EqualTo(FastenerSize.Millimeter8));
            Assert.That(
                column.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(
                        true)
                    .Select(value => value.GetComponent<InteractionTargetHost>()
                        .OutlineRenderers.Single()
                        .GetComponent<MeshFilter>().sharedMesh.name),
                Is.All.EqualTo("bolt"),
                "The dashboard tachometer screw must not leak into the two " +
                "steering-column bolts.");
        }

        [Test]
        public void GeneratedRearDrumMountsPreserveDonorBoltPmContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            string[] drumPartIds =
            {
                "vehicle.satsuma.part.drum-brake-1",
                "vehicle.satsuma.part.drum-brake-2",
            };
            foreach (string corner in new[] { "rl", "rr" })
            {
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.drum-brake-" + corner);
                Assert.That(
                    mount.Definition.OwnerPartDefinitionId,
                    Is.EqualTo("vehicle.satsuma.part.trail-arm-" + corner));
                Assert.That(
                    mount.Definition.AcceptedPartDefinitionIds,
                    Is.EquivalentTo(drumPartIds));
                Assert.That(
                    mount.Definition.ReferenceCandidateRadiusMeters,
                    Is.EqualTo(0.01f).Within(0.00001f));
                FastenerDefinition fastener =
                    mount.Definition.Fasteners.Single();
                Assert.That(fastener.Size, Is.EqualTo(FastenerSize.Millimeter14));
                Assert.That(fastener.MaximumStage, Is.EqualTo(8));
                Assert.That(fastener.InsertedOnInstall, Is.True);
                Assert.That(fastener.RequiredForRemoval, Is.True);
                Assert.That(
                    fastener.TighteningDirection,
                    Is.EqualTo(FastenerDirection.ClockwiseToTighten));
            }
        }

        [Test]
        public void GeneratedHingedPanelsPreserveDonorProfilesAndOpenAngleAcrossSaveRestore()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                var expectations = new[]
                {
                    new
                    {
                        Slug = "bootlid",
                        Axis = Vector3.right,
                        Minimum = -70f,
                        Maximum = 0f,
                        BreakForce = 500f,
                        OpenTorque = new Vector3(-30f, 0f, 0f),
                        CloseTorque = new Vector3(1f, 0f, 0f),
                        OpenHoldWindow = 1f,
                        RequiresRelease = false,
                    },
                    new
                    {
                        Slug = "door-left",
                        Axis = Vector3.forward,
                        Minimum = 0f,
                        Maximum = 80f,
                        BreakForce = 1100f,
                        OpenTorque = new Vector3(0f, 0f, 120f),
                        CloseTorque = new Vector3(0f, 0f, -150f),
                        OpenHoldWindow = 0f,
                        RequiresRelease = false,
                    },
                    new
                    {
                        Slug = "door-right",
                        Axis = Vector3.forward,
                        Minimum = -80f,
                        Maximum = 0f,
                        BreakForce = 1100f,
                        OpenTorque = new Vector3(0f, 0f, -120f),
                        CloseTorque = new Vector3(0f, 0f, 150f),
                        OpenHoldWindow = 0f,
                        RequiresRelease = false,
                    },
                    new
                    {
                        Slug = "hood",
                        Axis = Vector3.right,
                        Minimum = -87f,
                        Maximum = 0f,
                        BreakForce = 1000f,
                        OpenTorque = new Vector3(-30f, 0f, 0f),
                        CloseTorque = new Vector3(15f, 0f, 0f),
                        OpenHoldWindow = 0f,
                        RequiresRelease = true,
                    },
                };

                foreach (var expected in expectations)
                {
                    MountPointAuthoring mount = assembly.MountPoints.Single(
                        value => value.MountId ==
                            "mount.satsuma." + expected.Slug);
                    AssemblyHingeMountAuthoring hingeMount = mount
                        .GetComponent<AssemblyHingeMountAuthoring>();
                    Assert.That(hingeMount, Is.Not.Null);
                    Assert.That(hingeMount.LocalAxis, Is.EqualTo(expected.Axis));
                    Assert.That(
                        hingeMount.MinimumAngleDegrees,
                        Is.EqualTo(expected.Minimum));
                    Assert.That(
                        hingeMount.MaximumAngleDegrees,
                        Is.EqualTo(expected.Maximum));
                    Assert.That(
                        hingeMount.DonorBreakForce,
                        Is.EqualTo(expected.BreakForce));
                    Assert.That(
                        hingeMount.DonorBreakTorque,
                        Is.EqualTo(expected.BreakForce));
                    Assert.That(
                        hingeMount.DonorOpenTorqueLocal,
                        Is.EqualTo(expected.OpenTorque));
                    Assert.That(
                        hingeMount.DonorCloseTorqueLocal,
                        Is.EqualTo(expected.CloseTorque));
                    Assert.That(
                        hingeMount.DonorOpenHoldWindowDegrees,
                        Is.EqualTo(expected.OpenHoldWindow));
                    Assert.That(
                        hingeMount.RequiresReleaseBeforeOpening,
                        Is.EqualTo(expected.RequiresRelease));
                    Assert.That(
                        hingeMount.CloseLatchThresholdDegrees,
                        Is.EqualTo(1f).Within(0.001f),
                        "Physical motion must settle at the stop instead of " +
                        "visibly teleporting through the donor FSM's broad " +
                        "ten-degree state threshold.");
                    Assert.That(
                        hingeMount.FastenerTargets,
                        Has.Length.EqualTo(4));
                }

                AssemblyFastenerInteractionTarget[] bootlidFasteners =
                    prefab.GetComponentsInChildren<
                            AssemblyFastenerInteractionTarget>(true)
                        .Where(value => value.MountId ==
                            "mount.satsuma.bootlid")
                        .OrderBy(value => value.FastenerDefinitionId)
                        .ToArray();
                Assert.That(bootlidFasteners, Has.Length.EqualTo(4));
                Assert.That(
                    bootlidFasteners.Select(value =>
                        value.FastenerPresentationStageTravelScale),
                    Is.All.EqualTo(0.5f).Within(0.00001f),
                    "The donor bootlid BoltPM Z scale halves each local " +
                    "Screw-stage translation; omitting it drives tightened " +
                    "bolts through the outer skin.");

                MountPointAuthoring leftDoorMount = assembly.MountPoints.Single(
                    value => value.MountId == "mount.satsuma.door-left");
                PartInstance leftDoor = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.door-left");
                leftDoor.transform.SetPositionAndRotation(
                    leftDoorMount.Pose.position,
                    leftDoorMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(leftDoor, leftDoorMount).Succeeded,
                    Is.True);
                AssemblyHingedPartInteractionTarget hinge = leftDoor
                    .GetComponent<AssemblyHingedPartInteractionTarget>();
                Assert.That(hinge.IsAttachedToHinge, Is.True);
                Assert.That(leftDoor.Body.isKinematic, Is.False);
                Assert.That(
                    leftDoor.GetComponent<AssemblyInstalledPhysicsLink>()
                        .LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.OperablePanelHinge));
                Assert.That(
                    leftDoor.GetComponent<InteractionTargetHost>()
                        .TryGetCapability<IToolActivationTarget>(out _),
                    Is.False,
                    "Donor hinged panels are driven by held mouse torque, not F.");

                hinge.RestoreOpenState(0.625f);
                VehicleAssemblySaveData save = assembly.CaptureSaveData();
                hinge.RestoreOpenState(0f);
                Assert.That(
                    assembly.RestoreSaveData(save).Succeeded,
                    Is.True);
                Assert.That(hinge.OpenNormalized, Is.EqualTo(0.625f).Within(0.002f));
                Assert.That(hinge.TargetOpen, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void InstalledDoorUsesDynamicDonorProfiledHinge()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.door-left");
                PartInstance door = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.door-left");
                AssemblyHingedPartInteractionTarget hinge = door
                    .GetComponent<AssemblyHingedPartInteractionTarget>();
                Assert.That(
                    hinge.UsesDirectionalHold,
                    Is.False,
                    "A loose donor door is an ordinary pickup part, not an active handle.");
                door.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                Assert.That(assembly.TryInstall(door, mount).Succeeded, Is.True);

                Assert.That(
                    door.GetComponent<InteractionTargetHost>()
                        .TryGetCapability(
                            out IContinuousContextInteractionTarget continuous),
                    Is.True);
                Assert.That(hinge.UsesDirectionalHold, Is.True);
                AssemblyInstalledPhysicsLink physicsLink = door
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(physicsLink, Is.Not.Null);
                Assert.That(
                    physicsLink.LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.OperablePanelHinge));
                Assert.That(physicsLink.InstalledHinge, Is.Not.Null);
                Assert.That(
                    physicsLink.InstalledHinge.breakForce,
                    Is.EqualTo(float.PositiveInfinity),
                    "Unity 6 must not turn an ordinary chassis impulse into " +
                    "a detached and permanently inactive door.");
                Assert.That(
                    physicsLink.InstalledHinge.breakTorque,
                    Is.EqualTo(float.PositiveInfinity));
                Assert.That(door.Body.isKinematic, Is.False);
                Assert.That(door.Body.useGravity, Is.True);
                Assert.That(hinge.IsClosedLatched, Is.True);
                var context = new MSC.Interaction.InteractionContext(
                    instance,
                    door.transform.position,
                    door.transform.forward);
                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                Assert.That(continuous.ContinueContinuousInteraction(0.12f),
                    Is.True);
                continuous.EndContinuousInteraction();
                Assert.That(door.IsInstalled, Is.True);
                Assert.That(hinge.IsClosedLatched, Is.False,
                    "Primary hold releases the latch; PhysX advances the " +
                    "actual panel only during play-mode fixed steps.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedRearDrumInstallsTightensAndCannotRemoveWhileSecured()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring trailArmMount =
                    assembly.MountPoints.Single(value =>
                        value.MountId == "mount.satsuma.trail-arm-rl");
                PartInstance drum = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.drum-brake-1");
                MountPointAuthoring drumMount = assembly.MountPoints.Single(
                    value =>
                        value.MountId == "mount.satsuma.drum-brake-rl");
                AssemblyOwnedMountAuthoring ownedDrumMount = drumMount
                    .GetComponent<AssemblyOwnedMountAuthoring>();
                Assert.That(ownedDrumMount, Is.Not.Null);
                Assert.That(ownedDrumMount.OwnerPart, Is.SameAs(trailArm));
                Assert.That(ownedDrumMount.RequireInstalledOwner, Is.True);
                Assert.That(ownedDrumMount.IsAvailable, Is.False);
                Assert.That(drumMount.gameObject.activeSelf, Is.False);
                drum.transform.SetPositionAndRotation(
                    drumMount.Pose.position,
                    drumMount.Pose.rotation);
                Assert.That(
                    assembly.EvaluateInstall(drum, drumMount).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.MissingPrerequisite),
                    "A rear drum socket must not accept a drum while its arm " +
                    "is still loose in the garage.");

                trailArm.transform.SetPositionAndRotation(
                    trailArmMount.Pose.position,
                    trailArmMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(trailArm, trailArmMount).Succeeded,
                    Is.True);
                Assert.That(ownedDrumMount.IsAvailable, Is.True);
                Assert.That(drumMount.gameObject.activeSelf, Is.True);
                Assert.That(drumMount.transform.parent, Is.SameAs(trailArm.transform));
                Assert.That(
                    drumMount.transform.localPosition.magnitude,
                    Is.LessThan(1f),
                    "The rear drum socket must be local to the installed arm, " +
                    "not several metres away in the loose PartsCar coordinate space.");

                MountPointRuntime trailArmRuntime = assembly.ResolveMount(
                    trailArmMount);
                ToolDefinition wrench12 = assembly.Tools.Single(value =>
                    value.Size == FastenerSize.Millimeter12);
                int armTurnsRemaining = 12;
                foreach (FastenerInstance fastener in trailArmRuntime.Fasteners)
                {
                    while (armTurnsRemaining > 0 &&
                           fastener.Stage < fastener.Definition.MaximumStage)
                    {
                        Assert.That(
                            assembly.TryOperateFastener(
                                trailArmMount.MountId,
                                fastener.Definition.DefinitionId,
                                wrench12,
                                tighten: true).Succeeded,
                            Is.True);
                        armTurnsRemaining--;
                    }
                }

                Assert.That(armTurnsRemaining, Is.Zero);
                Assert.That(trailArmRuntime.FastenerGroup.Tightness, Is.EqualTo(12));
                Assert.That(trailArmRuntime.FastenerGroup.IsBolted, Is.True);
                drum.transform.SetPositionAndRotation(
                    drumMount.Pose.position,
                    drumMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(drum, drumMount).Succeeded,
                    Is.True);
                InteractionTargetHost trailArmHost = trailArm
                    .GetComponent<InteractionTargetHost>();
                InteractionTargetHost drumHost = drum
                    .GetComponent<InteractionTargetHost>();
                Assert.That(trailArmHost.SelectionPriority, Is.EqualTo(10));
                Assert.That(drumHost.SelectionPriority, Is.EqualTo(15));
                Assert.That(
                    drumHost.TryGetCapability(
                        out IParentColliderOcclusionBypass _),
                    Is.True);
                Assert.That(
                    drumHost.TryGetCapability(
                        out IRaycastOriginOverlapTarget _),
                    Is.True);
                AssemblyInstalledPartInteractionTarget drumRemoval = drum
                    .GetComponent<AssemblyInstalledPartInteractionTarget>();
                Assert.That(drumRemoval, Is.Not.Null);
                var drumCandidate = new InteractionCandidate(
                    drumHost,
                    drum.transform.position,
                    Vector3.up,
                    0f);
                Assert.That(drumRemoval.CanInteract(default), Is.True);
                Assert.That(
                    drumCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: default),
                    Does.StartWith("Снять:"));

                Assert.That(
                    Vector3.Distance(
                        drum.transform.position,
                        drumMount.Pose.position),
                    Is.LessThan(0.00001f),
                    "The drum root must occupy its arm-owned donor socket. " +
                    "Its car-local position then follows physical arm travel.");

                AssemblyInstalledPartInteractionProxy drumProxy = drum
                    .GetComponentInChildren<
                        AssemblyInstalledPartInteractionProxy>(true);
                BoxCollider drumProxyCollider = drumProxy != null
                    ? drumProxy.GetComponent<BoxCollider>()
                    : null;
                Assert.That(drumProxyCollider, Is.Not.Null);
                Assert.That(drumProxy.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(
                    drumProxyCollider.attachedRigidbody,
                    Is.SameAs(drum.Body));
                GameObject aimObject = new GameObject("Rear drum ray test");
                try
                {
                    Vector3 proxyCenter = drumProxy.transform.TransformPoint(
                        drumProxyCollider.center);
                    aimObject.transform.position = drumProxy.transform
                        .TransformPoint(
                            drumProxyCollider.center +
                            Vector3.right *
                            (drumProxyCollider.size.x * 0.5f + 0.25f));
                    aimObject.transform.forward = (
                        proxyCenter - aimObject.transform.position).normalized;
                    RaycastInteractionCandidateSource source = aimObject
                        .AddComponent<RaycastInteractionCandidateSource>();
                    source.Configure(aimObject.transform, 1f, ~0);
                    Physics.SyncTransforms();

                    InteractionCandidate candidate = source.Query();
                    Assert.That(candidate.IsValid, Is.True);
                    Assert.That(
                        candidate.Host,
                        Is.SameAs(drumHost),
                        "The installed drum must outrank its owning arm under the crosshair.");

                    aimObject.transform.position = drumProxy.transform
                        .TransformPoint(
                            drumProxyCollider.center +
                            Vector3.right *
                            (drumProxyCollider.size.x * 0.45f));
                    aimObject.transform.forward = -drumProxy.transform.right;
                    Physics.SyncTransforms();

                    candidate = source.Query();
                    Assert.That(candidate.IsValid, Is.True);
                    Assert.That(
                        candidate.Host,
                        Is.SameAs(drumHost),
                        "Moving the camera inside the compact proxy must not blind the interaction ray.");
                }
                finally
                {
                    Object.DestroyImmediate(aimObject);
                }

                FastenerDefinition bolt = drumMount.Definition.Fasteners.Single();
                ToolDefinition wrench14 = assembly.Tools.Single(value =>
                    value.Size == FastenerSize.Millimeter14);
                AssemblyFastenerInteractionTarget fastenerTarget = drumMount
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single();
                fastenerTarget.RefreshAvailability();
                Transform fastenerPresentation = fastenerTarget.transform.Find(
                    "Visible inserted bolt or nut");
                Assert.That(fastenerPresentation, Is.Not.Null);
                Assert.That(
                    fastenerPresentation.GetComponent<Renderer>().enabled,
                    Is.True,
                    "Installing the part must reveal its donor fastener.");
                Vector3 fastenerBasePosition =
                    fastenerPresentation.localPosition;
                Quaternion fastenerBaseRotation =
                    fastenerPresentation.localRotation;
                GameObject fastenerRayObject = new GameObject(
                    "Fastener-only ray test");
                try
                {
                    fastenerRayObject.transform.position =
                        fastenerTarget.transform.position + Vector3.up * 0.5f;
                    fastenerRayObject.transform.forward =
                        (fastenerTarget.transform.position -
                         fastenerRayObject.transform.position).normalized;
                    RaycastInteractionCandidateSource fastenerRay =
                        fastenerRayObject.AddComponent<
                            RaycastInteractionCandidateSource>();
                    fastenerRay.Configure(
                        fastenerRayObject.transform,
                        1f,
                        ~0);
                    Physics.SyncTransforms();
                    InteractionTargetHost fastenerHost = fastenerTarget
                        .GetComponent<InteractionTargetHost>();
                    InteractionCandidate ordinaryCandidate =
                        fastenerRay.Query();
                    Assert.That(
                        ordinaryCandidate.Host,
                        Is.Not.SameAs(fastenerHost),
                        "The ordinary interaction ray must look through " +
                        "bolt-gayka-only targets to the part or mount behind them.");

                    fastenerRay.SetScrollToolTargetsOnly(true);
                    InteractionCandidate wrenchCandidate = fastenerRay.Query();
                    Assert.That(wrenchCandidate.IsValid, Is.True);
                    Assert.That(
                        wrenchCandidate.Host,
                        Is.SameAs(fastenerHost),
                        "Wrench mode must see the fastener through all ordinary geometry.");
                }
                finally
                {
                    Object.DestroyImmediate(fastenerRayObject);
                }
                var heldWrench14 = new TestHeldToolIdentity(
                    wrench14.ToolType,
                    ((int)wrench14.Size).ToString());
                Assert.That(
                    fastenerTarget.TryResolveToolSnapAnchor(
                        heldWrench14,
                        default,
                        out Transform snapAnchor),
                    Is.True,
                    "A selected compatible wrench must snap directly onto the installed fastener.");
                Assert.That(snapAnchor, Is.Not.Null);
                Assert.That(snapAnchor.parent, Is.SameAs(fastenerTarget.transform));
                Assert.That(
                    Vector3.Distance(
                        snapAnchor.localPosition,
                        new Vector3(-0.073f, 0.028f, 0.016f)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        snapAnchor.localRotation,
                        Quaternion.Euler(0f, 0f, 160f)),
                    Is.LessThan(0.01f));
                Assert.That(
                    fastenerTarget.GetOutlineFeedback(null),
                    Is.EqualTo(InteractionOutlineFeedback.Loose));
                Assert.That(
                    assembly.TryTurnFastener(
                        drumMount.MountId,
                        bolt.DefinitionId,
                        wrench14,
                        FastenerRotationDirection.Clockwise).Succeeded,
                    Is.True);
                fastenerTarget.RefreshAvailability();
                Assert.That(
                    Vector3.Distance(
                        fastenerPresentation.localPosition,
                        fastenerBasePosition +
                        fastenerBaseRotation *
                        (Vector3.back * 0.0025f)),
                    Is.LessThan(0.00001f),
                    "Donor screw stage 1 must insert the visible bolt by 2.5 mm.");
                Assert.That(
                    Quaternion.Angle(
                        fastenerPresentation.localRotation,
                        fastenerBaseRotation *
                        Quaternion.AngleAxis(45f, Vector3.forward)),
                    Is.LessThan(0.01f),
                    "Donor screw stage 1 must rotate the visible bolt by 45 degrees.");
                Assert.That(
                    fastenerTarget.GetOutlineFeedback(null),
                    Is.EqualTo(InteractionOutlineFeedback.Partial));
                for (int stage = 1; stage < bolt.MaximumStage; stage++)
                {
                    Assert.That(
                        assembly.TryTurnFastener(
                            drumMount.MountId,
                            bolt.DefinitionId,
                            wrench14,
                            FastenerRotationDirection.Clockwise).Succeeded,
                        Is.True);
                }

                fastenerTarget.RefreshAvailability();
                Assert.That(
                    Vector3.Distance(
                        fastenerPresentation.localPosition,
                        fastenerBasePosition +
                        fastenerBaseRotation *
                        (Vector3.back * 0.02f)),
                    Is.LessThan(0.00001f),
                    "Eight donor stages must physically insert the fastener by 20 mm.");

                Assert.That(
                    fastenerTarget.GetOutlineFeedback(null),
                    Is.EqualTo(InteractionOutlineFeedback.Complete));
                Assert.That(
                    fastenerTarget.GetOutlineFeedback(
                        new TestHeldToolIdentity(
                            wrench14.ToolType,
                            ((int)FastenerSize.Millimeter11).ToString())),
                    Is.EqualTo(InteractionOutlineFeedback.Invalid),
                    "A wrong wrench remains red even when the bolt is already tight.");
                Assert.That(
                    assembly.EvaluateRemoval(drum).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.FastenerSecured));
                Assert.That(drumRemoval.CanInteract(default), Is.False);
                Assert.That(
                    drumCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: default),
                    Is.Empty,
                    "A donor-latched installed part must not expose its " +
                    "removal prompt or drive the ordinary part outline.");
                for (int stage = 1; stage < bolt.MaximumStage; stage++)
                {
                    Assert.That(
                        assembly.TryTurnFastener(
                            drumMount.MountId,
                            bolt.DefinitionId,
                            wrench14,
                            FastenerRotationDirection.CounterClockwise).Succeeded,
                        Is.True);
                }

                Assert.That(
                    assembly.ResolveMount(drumMount).FastenerGroup.Tightness,
                    Is.EqualTo(1));
                Assert.That(
                    assembly.ResolveMount(drumMount).FastenerGroup.IsBolted,
                    Is.True,
                    "The removal UI must remain hidden above donor " +
                    "BoltedOffThreshold.");
                Assert.That(drumRemoval.CanInteract(default), Is.False);
                Assert.That(
                    assembly.TryTurnFastener(
                        drumMount.MountId,
                        bolt.DefinitionId,
                        wrench14,
                        FastenerRotationDirection.CounterClockwise).Succeeded,
                    Is.True);
                Assert.That(
                    assembly.ResolveMount(drumMount).FastenerGroup.IsBolted,
                    Is.False);
                Assert.That(drumRemoval.CanInteract(default), Is.True);
                Assert.That(
                    drumCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: default),
                    Does.StartWith("Снять:"));

                fastenerTarget.RefreshAvailability();
                Assert.That(
                    Vector3.Distance(
                        fastenerPresentation.localPosition,
                        fastenerBasePosition),
                    Is.LessThan(0.00001f));
                Assert.That(
                    Quaternion.Angle(
                        fastenerPresentation.localRotation,
                        fastenerBaseRotation),
                    Is.LessThan(0.01f));
                Assert.That(
                    fastenerTarget.GetOutlineFeedback(null),
                    Is.EqualTo(InteractionOutlineFeedback.Loose));
                Assert.That(assembly.TryRemove(drum).Succeeded, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RearTrailArmRetainsFallbackHingeMetadataAndRevealsTwoBolts()
        {
            if (!File.Exists(Phase1SatsumaBaselineBuilder.RuntimePrefabPath))
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.trail-arm-rl");
                AssemblyFastenerInteractionTarget[] bolts = mount
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
                AssemblyInstalledPartInteractionProxy proxy = trailArm
                    .GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
                BoxCollider proxyCollider = proxy != null
                    ? proxy.GetComponent<BoxCollider>()
                    : null;

                Assert.That(bolts, Has.Length.EqualTo(2));
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxyCollider, Is.Not.Null);
                Assert.That(
                    proxyCollider.size.magnitude,
                    Is.LessThan(1.5f),
                    "Owned mount helpers must not inflate the arm interaction proxy.");
                Assert.That(
                    bolts.All(value =>
                        value.GetComponentsInChildren<Renderer>(true).Length == 1),
                    Is.True,
                    "Each donor fastener marker needs one donor-textured temporary presentation.");
                Assert.That(
                    bolts.All(value =>
                        !value.GetComponent<Collider>().enabled &&
                        !value.GetComponentsInChildren<Renderer>(true).Single().enabled),
                    Is.True,
                    "Bolts and nuts must not exist as visible or raycastable targets before their part is installed.");
                Assert.That(
                    trailArm.PickupTarget.CanPickup(default),
                    Is.True);

                trailArm.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                trailArm.Body.position = mount.Pose.position;
                trailArm.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = assembly.TryInstall(
                    trailArm,
                    mount);

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(
                    trailArm.PickupTarget.CanPickup(default),
                    Is.False,
                    "An installed arm must expose removal, never the loose pickup prompt.");
                Assert.That(
                    trailArm.GetComponent<AssemblyInstalledPhysicsLink>(),
                    Is.Not.Null);
                Assert.That(
                    trailArm.GetComponent<AssemblyInstalledPhysicsLink>()
                        .LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.TrailingArmHinge),
                    "The fallback handoff metadata remains authored even when " +
                    "NWH owns the assembled rear suspension at runtime.");
                Assert.That(proxyCollider.enabled, Is.True);
                Assert.That(proxyCollider.isTrigger, Is.True);

                foreach (AssemblyFastenerInteractionTarget bolt in bolts)
                {
                    bolt.RefreshAvailability();
                    Assert.That(
                        bolt.GetComponentsInChildren<Renderer>(true)
                            .Single().enabled,
                        Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedAssemblySaveRoundTripCoversEveryPartAndMount()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                VehicleAssemblySaveData captured = assembly.CaptureSaveData();
                Assert.That(captured.parts, Has.Length.EqualTo(126));
                Assert.That(captured.mounts, Has.Length.EqualTo(117));
                Assert.That(captured.fasteners, Has.Length.EqualTo(280));
                Assert.That(captured.fastenerGroups, Has.Length.EqualTo(117));
                Assert.That(
                    assembly.ValidateSaveDataForRestore(captured).Succeeded,
                    Is.True);
                Assert.That(assembly.RestoreSaveData(captured).Succeeded, Is.True);
                VehicleAssemblySaveData restored = assembly.CaptureSaveData();
                Assert.That(restored.parts, Has.Length.EqualTo(captured.parts.Length));
                Assert.That(restored.mounts, Has.Length.EqualTo(captured.mounts.Length));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedAssemblyMigratesPrevious260FastenerShapeByStableId()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                InstallAtOnlyCompatibleMount(
                    assembly,
                    "vehicle.satsuma.part.bumper-front");
                VehicleAssemblySaveData current = assembly.CaptureSaveData();
                var legacy = new VehicleAssemblySaveData
                {
                    schemaVersion = current.schemaVersion,
                    parts = current.parts,
                    mounts = current.mounts,
                    fasteners = BuildPrevious260FastenerShape(current),
                    fastenerGroups = current.fastenerGroups,
                };
                string originalJson = JsonUtility.ToJson(legacy);
                string[] preservedFasteners = legacy.fasteners
                    .Where(value => !IsRetiredSteeringShapeFastener(value))
                    .Select(FastenerFingerprint)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                Assert.That(legacy.fasteners, Has.Length.EqualTo(260));
                AssemblyOperationResult validation =
                    assembly.ValidateSaveDataForRestore(legacy);
                Assert.That(validation.Succeeded, Is.True, validation.Message);
                AssemblyOperationResult restore = assembly.RestoreSaveData(legacy);
                Assert.That(restore.Succeeded, Is.True, restore.Message);

                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                Assert.That(migrated.fasteners, Has.Length.EqualTo(280));
                Assert.That(
                    migrated.fasteners.Count(IsPost260AddedFastener),
                    Is.EqualTo(21));
                Assert.That(
                    migrated.fasteners
                        .Where(value => value.mountId ==
                            "mount.satsuma.bumper-front")
                        .All(value =>
                            value.inserted && value.seated && value.stage > 0),
                    Is.True,
                    "New fasteners on a previously installed panel must migrate secured.");
                Assert.That(
                    migrated.fasteners
                        .Where(value => !IsPost260AddedFastener(value))
                        .Where(value => !IsRetiredSteeringShapeFastener(value))
                        .Select(FastenerFingerprint)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    Is.EqualTo(preservedFasteners));
                FastenerSaveDto retiredPhysicalColumnBolt = legacy.fasteners
                    .Single(value => value.mountId ==
                        "mount.satsuma.steering-column" &&
                        value.fastenerDefinitionId ==
                        "fastener.satsuma.steering-column.boltpm-3");
                FastenerSaveDto migratedPhysicalColumnBolt = migrated.fasteners
                    .Single(value => value.mountId ==
                        "mount.satsuma.steering-column" &&
                        value.fastenerDefinitionId ==
                        "fastener.satsuma.steering-column.boltpm-2");
                Assert.That(
                    (migratedPhysicalColumnBolt.inserted,
                        migratedPhysicalColumnBolt.seated,
                        migratedPhysicalColumnBolt.stage),
                    Is.EqualTo((retiredPhysicalColumnBolt.inserted,
                        retiredPhysicalColumnBolt.seated,
                        retiredPhysicalColumnBolt.stage)),
                    "The physical second column bolt must survive the ID repair.");
                Assert.That(JsonUtility.ToJson(legacy), Is.EqualTo(originalJson));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Previous260FastenerShapeRejectsADifferentMissingStableIdSet()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                VehicleAssemblySaveData current = assembly.CaptureSaveData();
                FastenerSaveDto[] expectedLegacy =
                    BuildPrevious260FastenerShape(current);
                FastenerSaveDto unexpectedNew = current.fasteners
                    .First(IsPost260AddedFastener);
                var corrupt = new VehicleAssemblySaveData
                {
                    schemaVersion = current.schemaVersion,
                    parts = current.parts,
                    mounts = current.mounts,
                    fasteners = expectedLegacy
                        .Skip(1)
                        .Concat(new[] { unexpectedNew })
                        .ToArray(),
                    fastenerGroups = current.fastenerGroups,
                };

                Assert.That(corrupt.fasteners, Has.Length.EqualTo(260));
                Assert.That(
                    assembly.ValidateSaveDataForRestore(corrupt).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedAssemblyMigratesRetiredDuplicateSubframeMount()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                PartInstance subframe = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.sub-frame");
                MountPointAuthoring canonicalMount = assembly.MountPoints
                    .Single(value => value.Definition != null &&
                        value.Definition.DefinitionId ==
                        "mount.satsuma.sub-frame");
                subframe.transform.SetPositionAndRotation(
                    canonicalMount.Pose.position,
                    canonicalMount.Pose.rotation);
                subframe.Body.position = canonicalMount.Pose.position;
                subframe.Body.rotation = canonicalMount.Pose.rotation;
                Assert.That(
                    assembly.TryInstall(subframe, canonicalMount).Succeeded,
                    Is.True);

                VehicleAssemblySaveData legacy = assembly.CaptureSaveData();
                PartSaveDto partDto = legacy.parts.Single(value =>
                    value.stableEntityId == subframe.StableId.Value);
                MountSaveDto canonicalDto = legacy.mounts.Single(value =>
                    value.mountId == "mount.satsuma.sub-frame");
                string occupant = canonicalDto.installedPartStableEntityId;
                partDto.installedMountId = "mount.satsuma.subframe";
                canonicalDto.installedPartStableEntityId = string.Empty;
                legacy.mounts = legacy.mounts.Concat(new[]
                {
                    new MountSaveDto
                    {
                        mountId = "mount.satsuma.subframe",
                        installedPartStableEntityId = occupant,
                    },
                }).ToArray();

                Assert.That(legacy.mounts, Has.Length.EqualTo(118));
                Assert.That(
                    assembly.ValidateSaveDataForRestore(legacy).Succeeded,
                    Is.True);
                Assert.That(assembly.RestoreSaveData(legacy).Succeeded, Is.True);
                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                Assert.That(migrated.mounts, Has.Length.EqualTo(117));
                Assert.That(
                    migrated.mounts.Any(value =>
                        value.mountId == "mount.satsuma.subframe"),
                    Is.False);
                Assert.That(
                    migrated.parts.Single(value =>
                        value.stableEntityId == subframe.StableId.Value)
                        .installedMountId,
                    Is.EqualTo("mount.satsuma.sub-frame"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedAssemblyMigratesThePreviousFortySixMountSaveShapeAdditively()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                VehicleAssemblySaveData legacy = assembly.CaptureSaveData();
                legacy.fasteners = legacy.fasteners
                    .Take(57)
                    .ToArray();
                string[] fastenerMountIds = legacy.fasteners
                    .Select(dto => dto.mountId)
                    .Distinct()
                    .ToArray();
                legacy.mounts = legacy.mounts
                    .Where(dto => fastenerMountIds.Contains(dto.mountId))
                    .Concat(legacy.mounts.Where(dto =>
                        !fastenerMountIds.Contains(dto.mountId)))
                    .Take(46)
                    .ToArray();

                Assert.That(legacy.mounts, Has.Length.EqualTo(46));
                Assert.That(legacy.fasteners, Has.Length.EqualTo(57));
                Assert.That(
                    assembly.ValidateSaveDataForRestore(legacy).Succeeded,
                    Is.True);
                Assert.That(assembly.RestoreSaveData(legacy).Succeeded, Is.True);
                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                Assert.That(migrated.mounts, Has.Length.EqualTo(117));
                Assert.That(migrated.fasteners, Has.Length.EqualTo(280));
                Assert.That(
                    migrated.mounts.Single(dto =>
                        dto.mountId == "mount.satsuma.engine-assembly")
                        .installedPartStableEntityId,
                    Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedRosterHasUniqueStableIdsAndCoreDefinitions()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            PartInstance[] parts = prefab.GetComponent<VehicleAssemblyController>().Parts;
            Assert.That(
                parts.Select(part => part.StableId.Value).Distinct().Count(),
                Is.EqualTo(parts.Length));
            string[] definitions = parts
                .Select(part => part.Definition.DefinitionId)
                .ToArray();
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.engine-block"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.starter"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.battery"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.fuel-tank"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.clutch"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.gearbox"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.wheel-stock-fl"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.wheel-stock-fr"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.wheel-stock-rl"));
            Assert.That(definitions, Does.Contain("vehicle.satsuma.part.wheel-stock-rr"));
        }

        [Test]
        public void GeneratedCompatibilityCatalogRetainsAllMailOrderMappings()
        {
            VehicleDeliveredPartCompatibilityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    VehicleDeliveredPartCompatibilityCatalog>(
                    Phase1SatsumaBaselineBuilder.CompatibilityCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ValidateConfiguration(), Is.Empty);
            Assert.That(catalog.Entries.Count, Is.EqualTo(46));
        }

        [Test]
        public void ProductionInstallerResolvesResourceAndCreatesOneSatsuma()
        {
            var composition = new GameObject("Satsuma Composition Test");
            var installerOwner = new GameObject("Satsuma Installer Test");
            try
            {
                ProductionSatsumaInstaller installer =
                    installerOwner.AddComponent<ProductionSatsumaInstaller>();
                GameObject spawned = installer.Initialize(composition.transform);

                Assert.That(spawned, Is.Not.Null);
                Assert.That(
                    composition.GetComponentsInChildren<
                        LegacySatsumaBaselineMetadata>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    spawned.GetComponent<VehiclePersistenceBinding>(),
                    Is.Not.Null);

                Color selected = new Color32(34, 174, 191, 255);
                Assert.That(
                    installer.TryApplyNewGamePaint(
                        9,
                        selected,
                        out string paintFailure),
                    Is.True,
                    paintFailure);
                VehiclePaintStateController paint =
                    spawned.GetComponent<VehiclePaintStateController>();
                Assert.That(paint.HasPlayerSelectedPaint, Is.True);
                Assert.That(paint.PaletteIndex, Is.EqualTo(9));
                Assert.That(paint.BodyColor, Is.EqualTo(selected));
                Assert.That(paint.PaintType, Is.EqualTo(VehiclePaintType.NoChange));

                Assert.That(
                    paint.TryApplyPaint(
                        9,
                        selected,
                        VehiclePaintType.Metallic,
                        out paintFailure),
                    Is.True,
                    paintFailure);

                VehiclePersistenceBinding persistence =
                    spawned.GetComponent<VehiclePersistenceBinding>();
                VehicleSimulationHost simulation =
                    spawned.GetComponent<VehicleSimulationHost>();
                Assert.That(
                    simulation.TryInitialize(out string simulationFailure),
                    Is.True,
                    simulationFailure);
                Assert.That(
                    persistence.TryCapture(
                        out VehicleSaveRecordDto save,
                        out string captureFailure),
                    Is.True,
                    captureFailure);
                Assert.That(save.paint, Is.Not.Null);
                Assert.That(save.paint.paletteIndex, Is.EqualTo(9));
                Assert.That(save.paint.bodyColor, Is.EqualTo(selected));
                Assert.That(
                    save.paint.paintType,
                    Is.EqualTo((int)VehiclePaintType.Metallic));
                Assert.That(save.paint.surfaces, Has.Length.EqualTo(7));
                CollectionAssert.AreEquivalent(
                    SatsumaPaintSurfaceIds.All,
                    save.paint.surfaces.Select(value => value.surfaceId));

                Color temporary = new Color32(166, 20, 20, 255);
                Assert.That(
                    paint.TryApplyPaint(
                        3,
                        temporary,
                        VehiclePaintType.Matte,
                        out paintFailure),
                    Is.True,
                    paintFailure);
                Assert.That(
                    persistence.TryRestore(save, out string restoreFailure),
                    Is.True,
                    restoreFailure);
                Assert.That(paint.PaletteIndex, Is.EqualTo(9));
                Assert.That(paint.BodyColor, Is.EqualTo(selected));
                Assert.That(paint.PaintType, Is.EqualTo(VehiclePaintType.Metallic));

                VehiclePaintSurfaceBinding bodyPaintBinding =
                    paint.PaintSurfaceBindings.Single(value =>
                        value.Renderer.name.StartsWith(
                            "car_body_",
                            System.StringComparison.OrdinalIgnoreCase));
                Renderer bodyRenderer = bodyPaintBinding.Renderer;
                int materialIndex = bodyPaintBinding.MaterialIndices.Single();
                Material donorMetallicPaint =
                    paint.PaintMaterialProfiles.Single(value =>
                        value.PaintType == VehiclePaintType.Metallic).Material;
                Assert.That(
                    bodyRenderer.sharedMaterials[materialIndex],
                    Is.EqualTo(donorMetallicPaint));
                var block = new MaterialPropertyBlock();
                bodyRenderer.GetPropertyBlock(block, materialIndex);
                Color applied = block.GetColor("_BaseColor");
                Assert.That(applied.r, Is.EqualTo(selected.r).Within(0.0001f));
                Assert.That(applied.g, Is.EqualTo(selected.g).Within(0.0001f));
                Assert.That(applied.b, Is.EqualTo(selected.b).Within(0.0001f));
                Assert.That(applied.a, Is.EqualTo(selected.a).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(installerOwner);
                Object.DestroyImmediate(composition);
            }
        }

        [Test]
        public void FleetariBodyRepairPaintsOnlyRequestedDonorSurfaces()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject spawned = Object.Instantiate(prefab);
            try
            {
                VehiclePaintStateController paint =
                    spawned.GetComponent<VehiclePaintStateController>();
                VehiclePersistenceBinding persistence =
                    spawned.GetComponent<VehiclePersistenceBinding>();
                var backend = new SatsumaWorkshopOutcomeBackend(
                    spawned,
                    spawned.transform.position);
                var request = new WorkshopOutcomeRequest(
                    "test-fleetari-body-repair-order",
                    persistence.StableVehicleId,
                    new[]
                    {
                        "service.workshop.body-repair",
                        "service.workshop.door-left",
                    },
                    -1,
                    -1,
                    -1,
                    0f);

                Assert.That(
                    backend.TryApply(in request, out string failure),
                    Is.True,
                    failure);
                Assert.That(
                    paint.TryGetSurfaceState(
                        SatsumaPaintSurfaceIds.Body,
                        out _,
                        out Color repairedBodyColor,
                        out VehiclePaintType repairedBodyType),
                    Is.True);
                Assert.That(repairedBodyType, Is.EqualTo(VehiclePaintType.Regular));
                Assert.That(repairedBodyColor.r, Is.EqualTo(0.6544118f).Within(0.0001f));
                Assert.That(repairedBodyColor.g, Is.EqualTo(0.651757f).Within(0.0001f));
                Assert.That(repairedBodyColor.b, Is.EqualTo(0.615917f).Within(0.0001f));

                Assert.That(
                    paint.TryGetSurfaceState(
                        SatsumaPaintSurfaceIds.DoorLeft,
                        out _,
                        out Color repairedDoorColor,
                        out VehiclePaintType repairedDoorType),
                    Is.True);
                Assert.That(repairedDoorType, Is.EqualTo(VehiclePaintType.Regular));
                Assert.That(
                    paint.TryGetSurfaceState(
                        SatsumaPaintSurfaceIds.DoorRight,
                        out _,
                        out _,
                        out VehiclePaintType untouchedDoorType),
                    Is.True);
                Assert.That(
                    untouchedDoorType,
                    Is.EqualTo(VehiclePaintType.NoChange));

                Assert.That(
                    backend.TryApply(in request, out failure),
                    Is.True,
                    failure);
                paint.TryGetSurfaceState(
                    SatsumaPaintSurfaceIds.DoorLeft,
                    out _,
                    out Color replayedDoorColor,
                    out _);
                Assert.That(replayedDoorColor, Is.EqualTo(repairedDoorColor));

                var unsupportedMixedRequest = new WorkshopOutcomeRequest(
                    "test-fleetari-unsupported-mixed-order",
                    persistence.StableVehicleId,
                    new[]
                    {
                        "service.workshop.door-right",
                        "service.workshop.brakes",
                    },
                    -1,
                    -1,
                    -1,
                    0f);
                Assert.That(
                    backend.TryApply(in unsupportedMixedRequest, out _),
                    Is.False);
                paint.TryGetSurfaceState(
                    SatsumaPaintSurfaceIds.DoorRight,
                    out _,
                    out _,
                    out VehiclePaintType afterRejectedBasketType);
                Assert.That(
                    afterRejectedBasketType,
                    Is.EqualTo(VehiclePaintType.NoChange),
                    "An unsupported mixed basket must not partially repaint the car.");
            }
            finally
            {
                Object.DestroyImmediate(spawned);
            }
        }

        [Test]
        public void InstallingSubframeKeepsMassInFixedPhysicalBody()
        {
            if (!File.Exists(Phase1SatsumaBaselineBuilder.RuntimePrefabPath))
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                AssemblyChassisMassController massController = instance
                    .GetComponent<AssemblyChassisMassController>();
                PartInstance chassis = assembly.Parts.Single(value =>
                    value.IsAssemblyRoot);
                PartInstance subframe = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.sub-frame");
                MountPointAuthoring[] compatibleMounts = assembly.MountPoints
                    .Where(value =>
                    value.Definition != null &&
                    value.Definition.AcceptsPart(
                        subframe.Definition.DefinitionId))
                    .ToArray();
                Assert.That(
                    compatibleMounts,
                    Has.Length.EqualTo(1),
                    "The donor sub-frame mesh pose must be the only install target; " +
                    "the nearby legacy FSM target is not a second socket.");
                MountPointAuthoring mount = compatibleMounts[0];
                Assert.That(
                    mount.Definition.DefinitionId,
                    Is.EqualTo("mount.satsuma.sub-frame"));

                float expectedMass = 389f +
                    subframe.Definition.MassKilograms;
                subframe.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                subframe.Body.position = mount.Pose.position;
                subframe.Body.rotation = mount.Pose.rotation;

                AssemblyOperationResult result = assembly.TryInstall(
                    subframe,
                    mount);

                Assert.That(result.Succeeded, Is.True, result.Message);
                massController.RefreshMass(force: true);
                Assert.That(
                    chassis.Body.mass,
                    Is.EqualTo(389f).Within(0.001f));
                Assert.That(subframe.Body.isKinematic, Is.False);
                Assert.That(
                    subframe.Body.mass,
                    Is.EqualTo(subframe.Definition.MassKilograms)
                        .Within(0.001f));
                Assert.That(
                    chassis.Body.mass + subframe.Body.mass,
                    Is.EqualTo(expectedMass).Within(0.001f));
                ConfigurableJoint link = subframe
                    .GetComponent<ConfigurableJoint>();
                Assert.That(link, Is.Not.Null);
                Assert.That(link.connectedBody, Is.SameAs(chassis.Body));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static PartInstance InstallAtOnlyCompatibleMount(
            VehicleAssemblyController assembly,
            string definitionId)
        {
            PartInstance part = assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId == definitionId);
            MountPointAuthoring[] mounts = assembly.MountPoints
                .Where(value => value.Definition != null &&
                    value.Definition.AcceptsPart(definitionId))
                .ToArray();
            Assert.That(
                mounts,
                Has.Length.EqualTo(1),
                definitionId + " must resolve to one exact mount.");
            MountPointAuthoring mount = mounts[0];
            part.transform.SetPositionAndRotation(
                mount.Pose.position,
                mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return part;
        }

        private static bool IsPost260AddedFastener(
            FastenerSaveDto value) =>
            value != null && Post260AddedFastenerKeys.Contains(
                value.mountId + "/" + value.fastenerDefinitionId,
                StringComparer.Ordinal);

        private static FastenerSaveDto[] BuildPrevious260FastenerShape(
            VehicleAssemblySaveData current)
        {
            var legacy = current.fasteners
                .Where(value => !IsPost260AddedFastener(value))
                .Select(CloneFastener)
                .ToList();
            FastenerSaveDto secondPhysicalColumnBolt = legacy.Single(value =>
                value.mountId == "mount.satsuma.steering-column" &&
                value.fastenerDefinitionId ==
                "fastener.satsuma.steering-column.boltpm-2");
            legacy.Add(new FastenerSaveDto
            {
                mountId = secondPhysicalColumnBolt.mountId,
                fastenerDefinitionId =
                    "fastener.satsuma.steering-column.boltpm-3",
                inserted = secondPhysicalColumnBolt.inserted,
                seated = secondPhysicalColumnBolt.seated,
                stage = secondPhysicalColumnBolt.stage,
            });
            return legacy.ToArray();
        }

        private static bool IsRetiredSteeringShapeFastener(
            FastenerSaveDto value) => value != null &&
                value.mountId == "mount.satsuma.steering-column" &&
                (value.fastenerDefinitionId ==
                    "fastener.satsuma.steering-column.boltpm-2" ||
                 value.fastenerDefinitionId ==
                    "fastener.satsuma.steering-column.boltpm-3");

        private static FastenerSaveDto CloneFastener(
            FastenerSaveDto value) => value == null
                ? null
                : new FastenerSaveDto
                {
                    mountId = value.mountId,
                    fastenerDefinitionId = value.fastenerDefinitionId,
                    inserted = value.inserted,
                    seated = value.seated,
                    stage = value.stage,
                };

        private static string FastenerFingerprint(FastenerSaveDto value) =>
            string.Join(
                "|",
                value.mountId,
                value.fastenerDefinitionId,
                value.inserted,
                value.seated,
                value.stage);

        private static void AssertRearNwhStage(
            NwhAssemblySuspensionStage stage,
            Vector3 expectedTopLocalPosition,
            float expectedTravelMeters,
            float expectedMaximumForceNewtons,
            string message)
        {
            Assert.That(
                Vector3.Distance(
                    stage.TopLocalPosition,
                    expectedTopLocalPosition),
                Is.LessThan(0.00001f),
                message + " top");
            Assert.That(
                stage.TravelMeters,
                Is.EqualTo(expectedTravelMeters).Within(0.00001f),
                message + " travel");
            Assert.That(
                stage.MaximumForceNewtons,
                Is.EqualTo(expectedMaximumForceNewtons).Within(0.001f),
                message + " maximum force");
        }

        private static void AssertStaticRearMount(
            VehicleAssemblyController assembly,
            string mountId,
            Vector3 expectedLocalPosition,
            Quaternion expectedLocalRotation)
        {
            AssertRearMountPose(
                assembly,
                mountId,
                "vehicle.satsuma.part.body-shell",
                expectedLocalPosition,
                expectedLocalRotation);
        }

        private static void AssertRearMountPose(
            VehicleAssemblyController assembly,
            string mountId,
            string expectedOwnerPartDefinitionId,
            Vector3 expectedLocalPosition,
            Quaternion expectedLocalRotation)
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == mountId);
            Assert.That(
                mount.Definition.OwnerPartDefinitionId,
                Is.EqualTo(expectedOwnerPartDefinitionId),
                mountId);
            Assert.That(
                Vector3.Distance(
                    mount.transform.localPosition,
                    expectedLocalPosition),
                Is.LessThan(0.00001f),
                mountId + " position");
            Assert.That(
                Quaternion.Angle(
                    mount.transform.localRotation,
                    expectedLocalRotation),
                Is.LessThan(0.001f),
                mountId + " rotation");
        }

        private sealed class TestHeldToolIdentity : IHeldToolIdentity
        {
            public TestHeldToolIdentity(string toolType, string toolVariant)
            {
                ToolType = toolType;
                ToolVariant = toolVariant;
            }

            public string ToolType { get; }

            public string ToolVariant { get; }
        }
    }
}
