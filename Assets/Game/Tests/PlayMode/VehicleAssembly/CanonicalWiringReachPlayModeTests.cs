#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    /// <summary>
    /// Opt-in private snapshot coverage. The copied record supplies the actual
    /// assembled car, not a test-built collection of connector spheres. F is
    /// dispatched through PlayerInteractionController after its ordinary query.
    /// </summary>
    public sealed class CanonicalWiringReachPlayModeTests
    {
        private const string VehicleRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        private const string ItemsRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string SpoolId = "item.wiring-mess";
        private static readonly SatsumaElectricalConnection[] Connections =
        {
            SatsumaElectricalConnection.FuelTank,
            SatsumaElectricalConnection.RearlightLeft,
            SatsumaElectricalConnection.RearlightRight,
            SatsumaElectricalConnection.Alternator,
            SatsumaElectricalConnection.HeadlightLeft,
            SatsumaElectricalConnection.HeadlightRight,
        };

        private readonly List<ScriptableObject> ownedAssets = new List<ScriptableObject>();
        private readonly List<string> approaches = new List<string>();
        private Scene scene;
        private float previousTimeScale;
        private IItemPresentationProvider previousHub;
        private bool fixtureStarted;
        private string sourcePath, sourceHash, sourceRecordJson, unconnectedElectricalJson;
        private VehicleSaveRecordDto sourceRecord;
        private GameObject vehicle, player;
        private VehicleAssemblyController assembly;
        private SatsumaElectricalSystem electrical;
        private ItemWorldRuntime runtime;
        private WorldItemInstance spool;
        private Rigidbody spoolBody;
        private PhysicalCarryController carry;
        private RaycastInteractionCandidateSource query;
        private PlayerInteractionController interaction;
        private SatsumaWiringConnectorInteractionTarget[] endpoints;
        private ApproachResult lastApproach;
        private float serviceGroundTop;

        [UnityTest]
        public IEnumerator RealSpoolCanWireSixStockCircuitsOnRestoredCanonicalCar()
        {
            CreateFixture();
            yield return OpenServicePanels();
            foreach (SatsumaElectricalConnection connection in Connections)
            {
                ResetTestElectricalState();
                Assert.That(electrical.IsConnectionInstalled(connection), Is.False);
                for (int end = 0; end < 2 && !electrical.IsConnectionInstalled(connection); end++)
                {
                    SatsumaWiringConnectorInteractionTarget endpoint = Endpoint(connection, end);
                    yield return FindPhysicalApproach(endpoint);
                    Assert.That(lastApproach.Reached, Is.True, Diagnostics(connection, end));
                    // Do not invoke the electrical system, cluster, or selected
                    // target directly. This is the production F intent path.
                    Assert.That(interaction.TryToolActivation(), Is.True, Diagnostics(connection, end));
                    Assert.That(electrical.IsEndpointArmed(connection, end) ||
                        electrical.IsConnectionInstalled(connection), Is.True, Diagnostics(connection, end));
                    Debug.Log("SATSUMA_CANONICAL_WIRING_F " + Diagnostics(connection, end));
                }
                Assert.That(electrical.IsConnectionInstalled(connection), Is.True, connection.ToString());
            }
            Assert.That(JsonUtility.ToJson(sourceRecord), Is.EqualTo(sourceRecordJson));
        }

        [UnityTest]
        public IEnumerator ForeignSolidBlocksFAtOtherwiseReachableCanonicalRearHarness()
        {
            CreateFixture();
            yield return OpenServicePanels();
            ResetTestElectricalState();
            SatsumaWiringConnectorInteractionTarget endpoint = Endpoint(SatsumaElectricalConnection.RearlightLeft, 0);
            yield return FindPhysicalApproach(endpoint);
            Assert.That(lastApproach.Reached, Is.True, Diagnostics(endpoint.Connection, endpoint.Endpoint));
            Assert.That(interaction.CurrentCandidate.TryGetCapability(out IHeldToolActivationTarget _), Is.True);
            byte[] before = PendingSnapshot();
            string electricalBefore = JsonUtility.ToJson(electrical.CaptureSaveData());

            // Behind the camera, there is no new player/body collision exception.
            // The wall is ahead of the camera but behind the physically held spool.
            var wall = SceneRoot("Unregistered foreign solid wiring occluder");
            wall.transform.SetPositionAndRotation(player.transform.position + player.transform.forward * .18f,
                player.transform.rotation);
            BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(.5f, .5f, .04f);
            Assert.That(wallCollider.isTrigger, Is.False);
            Physics.SyncTransforms();
            interaction.RefreshCandidate();
            Assert.That(interaction.CurrentCandidate.TryGetCapability(out IHeldToolActivationTarget _), Is.False,
                "A foreign solid must not inherit the Satsuma ancestor-occlusion bypass.");
            Assert.That(interaction.TryToolActivation(), Is.False);
            Assert.That(PendingSnapshot(), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(electrical.CaptureSaveData()), Is.EqualTo(electricalBefore));

            wall.SetActive(false);
            Physics.SyncTransforms();
            interaction.RefreshCandidate();
            Assert.That(interaction.TryToolActivation(), Is.True,
                "Removing only the foreign wall must restore the same normal F action.");
            Assert.That(electrical.IsEndpointArmed(endpoint.Connection, endpoint.Endpoint) ||
                electrical.IsConnectionInstalled(endpoint.Connection), Is.True);
        }

        private void CreateFixture()
        {
            string[] args = Environment.GetCommandLineArgs();
            int argIndex = Array.IndexOf(args, "-engineSavePath");
            if (argIndex < 0 || argIndex + 1 >= args.Length)
                Assert.Ignore("Private real-geometry wiring check requires -engineSavePath; no native file is written.");
            sourcePath = Path.GetFullPath(args[argIndex + 1]);
            byte[] bytes = File.ReadAllBytes(sourcePath);
            sourceHash = Hash(bytes);
            NativeProjection native = JsonUtility.FromJson<NativeProjection>(Encoding.UTF8.GetString(bytes));
            DomainProjection domain = native.Domains.Single(value => value.DomainId == "vehicle.satsuma");
            VehicleDomainSaveDto vehicles = JsonUtility.FromJson<VehicleDomainSaveDto>(domain.PayloadJson);
            sourceRecord = vehicles.vehicles.Single();
            sourceRecordJson = JsonUtility.ToJson(sourceRecord);
            Assert.That(sourceRecord.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>(), Is.Empty,
                "This fixture restores the reviewed pre-bridge snapshot; dynamic purchases require their Items domain too.");
            VehicleSaveRecordDto copy = JsonUtility.FromJson<VehicleSaveRecordDto>(sourceRecordJson);
            var testElectrical = JsonUtility.FromJson<SatsumaElectricalSaveDto>(JsonUtility.ToJson(copy.electrical));
            Assert.That(testElectrical, Is.Not.Null);
            testElectrical.installedConnectionIds = testElectrical.installedConnectionIds
                .Where(id => !Connections.Any(connection => connection.ToString() == id)).ToArray();
            unconnectedElectricalJson = JsonUtility.ToJson(testElectrical);

            previousTimeScale = Time.timeScale;
            previousHub = ItemPresentationProviderHub.Current;
            fixtureStarted = true;
            Time.timeScale = 1f;
            scene = SceneManager.CreateScene("Isolated canonical wiring " + Guid.NewGuid().ToString("N"));
            var holder = SceneRoot("Inactive canonical wiring car holder");
            holder.SetActive(false);
            vehicle = Object.Instantiate(RequireAsset<GameObject>(VehicleRoot +
                "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab"), holder.transform, false);
            assembly = vehicle.GetComponent<VehicleAssemblyController>();
            assembly.Initialize();
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            Assert.That(assembly.MountPoints, Has.Length.EqualTo(124));
            VehicleSimulationHost simulation = vehicle.GetComponent<VehicleSimulationHost>();
            simulation.enabled = false;
            Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
            vehicle.GetComponent<VehicleInputRouter>().enabled = false;
            foreach (MonoBehaviour component in vehicle.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().FullName == "MSC.Weather.Production.VehicleGlassRainPresenter")
                    component.enabled = false;
            // A stationary service fixture: preserve actual installed colliders,
            // joint bodies and panel dynamics, but keep the chassis on its stand.
            // NWH still requires a dynamic body during its normal Awake path.
            Rigidbody chassis = vehicle.GetComponent<Rigidbody>();
            chassis.isKinematic = false;
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            // Native restoration runs on an active aggregate. In particular,
            // physical links resolve their chassis via active parent components;
            // restoring below an inactive holder bypasses that normal lifecycle
            // and leaves otherwise installed panels without their HingeJoint.
            holder.SetActive(true);
            Assert.That(assembly.isActiveAndEnabled, Is.True);
            LogServicePanelStates("active-before-restore");
            Assert.That(vehicle.GetComponent<VehiclePersistenceBinding>().TryRestore(copy, out failure), Is.True, failure);
            LogServicePanelStates("restored-after-awake");
            Assert.That(chassis.isKinematic, Is.False);
            Assert.That(chassis.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            foreach (PartInstance part in assembly.Parts)
                if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);
            electrical = vehicle.GetComponent<SatsumaElectricalSystem>();
            endpoints = vehicle.GetComponentsInChildren<SatsumaWiringConnectorInteractionTarget>(true);
            Assert.That(endpoints, Has.Length.EqualTo(52));
            foreach (SatsumaElectricalConnection connection in Connections)
            {
                Assert.That(endpoints.Count(value => value.Connection == connection), Is.EqualTo(2));
                Assert.That(Endpoint(connection, 0).ArePartRequirementsMet &&
                    Endpoint(connection, 1).ArePartRequirementsMet, Is.True, "Snapshot prerequisites: " + connection);
            }
            CreateServiceStand(chassis);

            ItemDefinitionCatalog definitions = RequireAsset<ItemDefinitionCatalog>(
                "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            Assert.That(definitions.TryGet(SpoolId, out ItemDefinitionRecord definition), Is.True);
            GameObject providerRoot = SceneRoot("Inactive instance-only real spool provider");
            providerRoot.SetActive(false);
            var provider = providerRoot.AddComponent<ItemPresentationProvider>();
            var binding = new ItemPresentationBinding();
            binding.Configure(SpoolId, definition.ReplacementKey,
                RequireAsset<GameObject>(ItemsRoot + "/Prefabs/LegacyItemVisual_item_wiring-mess.prefab"));
            provider.Configure("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", new[] { binding });
            var placements = NewAsset<ItemPlacementCatalog>();
            placements.ConfigureForAuthoring("test.playmode.wiring.placements", "msc-world-baseline-04a1.1-c3f2f337",
                Array.Empty<ItemPlacementRecord>());
            var manifest = NewAsset<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, Array.Empty<ProductionWorldCellScene>());
            var streaming = SceneRoot("Disabled isolated spool streaming").AddComponent<ProductionWorldStreamingService>();
            streaming.ConfigureForAuthoring(manifest);
            streaming.enabled = false;
            runtime = SceneRoot("Real wiring item runtime").AddComponent<ItemWorldRuntime>();
            runtime.Initialize(definitions, placements, streaming, scene, -10000f);
            FieldInfo providerField = typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(runtime, provider); // Same instance-only seam as the real bulb fixture; no hub replacement.
            player = SceneRoot("Canonical wiring F and physical carry");
            Transform anchor = new GameObject("Ordinary held spool anchor").transform;
            anchor.SetParent(player.transform, false);
            anchor.localPosition = Vector3.forward * .65f;
            carry = player.AddComponent<PhysicalCarryController>();
            carry.Configure(anchor, null);
            query = player.AddComponent<RaycastInteractionCandidateSource>();
            query.Configure(player.transform, 2.25f, ~0);
            interaction = player.AddComponent<PlayerInteractionController>();
            interaction.Configure(query, carry, player.transform, ~0);
            spool = runtime.SpawnDynamic(SpoolId, ItemStableIdUtility.CreateDeterministic("test.playmode.real-spool.wiring"),
                vehicle.transform.position + Vector3.up * 3f, Quaternion.identity, scene);
            spoolBody = spool.GetComponent<Rigidbody>();
            spoolBody.useGravity = false;
            Assert.That(spool.PresentationRoot, Is.Not.Null);
            Assert.That(spool.PresentationRoot.GetComponentsInChildren<MeshFilter>(true)
                .Any(value => value.sharedMesh != null && value.sharedMesh.vertexCount > 0), Is.True);
            Assert.That(spool.PresentationRoot.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
            Assert.That(spool.ToolType, Is.EqualTo("Wiring"));
            Assert.That(spool.ToolVariant, Is.EqualTo("mess"));
            Assert.That(spoolBody.detectCollisions, Is.True);
            Assert.That(spool.gameObject.scene, Is.EqualTo(scene));
            Debug.Log("SATSUMA_CANONICAL_WIRING_SPOOL spawned " + SpoolGeometry(spoolBody.position));
            Debug.Log("SATSUMA_CANONICAL_WIRING_SOURCE sha=" + sourceHash + " bytes=" + bytes.Length);
        }

        private void CreateServiceStand(Rigidbody chassis)
        {
            // Do not move the saved car or invent an underground camera. This
            // isolated service stand has an explicit ground 1.5m below the tank
            // connector and four solid supports under actual chassis surfaces.
            serviceGroundTop = Endpoint(SatsumaElectricalConnection.FuelTank, 0).transform.position.y - 1.5f;
            var ground = SceneRoot("Canonical wiring service stand ground");
            ground.transform.position = new Vector3(vehicle.transform.position.x, serviceGroundTop - .1f, vehicle.transform.position.z);
            ground.AddComponent<BoxCollider>().size = new Vector3(8f, .2f, 8f);
            Collider[] shellShapes = vehicle.GetComponentsInChildren<Collider>(true)
                .Where(value => value.enabled && value.gameObject.activeInHierarchy && !value.isTrigger &&
                    value.attachedRigidbody == chassis && value.GetComponentInParent<PartInstance>(true)?.IsAssemblyRoot == true)
                .ToArray();
            foreach (float side in new[] { -.55f, .55f })
            foreach (float longitudinal in new[] { -.55f, .55f })
            {
                Vector3 below = vehicle.transform.TransformPoint(new Vector3(side, 0f, longitudinal));
                below.y = serviceGroundTop + .01f;
                var ray = new Ray(below, Vector3.up);
                float height = float.PositiveInfinity;
                foreach (Collider shape in shellShapes)
                    if (shape.Raycast(ray, out RaycastHit hit, 3f)) height = Mathf.Min(height, hit.point.y - serviceGroundTop);
                Assert.That(float.IsPositiveInfinity(height), Is.False, "Service support must meet an actual shell surface.");
                var support = SceneRoot("Canonical wiring solid chassis service support");
                support.transform.position = new Vector3(below.x, serviceGroundTop + height * .5f, below.z);
                support.AddComponent<BoxCollider>().size = new Vector3(.08f, height, .08f);
            }
            Physics.SyncTransforms();
        }

        private IEnumerator OpenServicePanels()
        {
            foreach (string partId in new[] { "vehicle.satsuma.part.hood", "vehicle.satsuma.part.bootlid" })
                yield return OpenServicePanelFully(partId, "initial-service");
        }

        private IEnumerator OpenServicePanelFully(string partId, string phase)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition.DefinitionId == partId);
            Assert.That(part.IsInstalled, Is.True, partId);
            var hinge = part.GetComponent<AssemblyHingedPartInteractionTarget>();
            Assert.That(hinge, Is.Not.Null);
            Assert.That(hinge.IsAttachedToHinge, Is.True, ServicePanelState(part, phase));
            bool bootlid = partId == "vehicle.satsuma.part.bootlid";
            bool FullyOpen() => bootlid ? hinge.IsOpenHeld : hinge.OpenNormalized >= .99f;
            if (FullyOpen()) yield break;

            // Opening is a separate one-hand action, never continuous torque
            // applied while the same player carries the wiring spool.
            if (carry.HasHeldObject) carry.Drop();
            Debug.Log("SATSUMA_CANONICAL_WIRING_PANEL " + ServicePanelState(part, phase + ":before-open"));
            // A restored bootlid already at its stop can acquire its authored
            // one-degree hold on the next ordinary hinge tick.
            if (bootlid && hinge.OpenNormalized >= .985f)
            {
                yield return new WaitForFixedUpdate();
                if (FullyOpen()) yield break;
            }
            if (hinge.RequiresReleaseBeforeOpening && hinge.IsClosedLatched)
            {
                AssemblyHoodReleaseInteractionTarget release = vehicle.GetComponentsInChildren<AssemblyHoodReleaseInteractionTarget>(true).Single();
                var releaseContext = new InteractionContext(player, release.transform.position, release.transform.forward);
                Assert.That(release.CanInteract(releaseContext), Is.True);
                release.Interact(releaseContext);
            }
            var context = new InteractionContext(player, part.transform.position, part.transform.forward);
            Assert.That(hinge.CanBeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary), Is.True,
                ServicePanelState(part, phase + ":normal-open-hold"));
            hinge.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            try
            {
                for (int step = 0; step < 250 && !FullyOpen(); step++)
                {
                    hinge.ContinueContinuousInteraction(Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }
            }
            finally
            {
                hinge.EndContinuousInteraction();
            }
            // The donor boot hold is -70..-69 degrees: its valid lower end
            // normalizes to .985714, not .99. Its actual held state is decisive.
            Assert.That(FullyOpen(), Is.True, ServicePanelState(part, phase + ":failed-full-open"));
            Assert.That(part.IsInstalled, Is.True, "Opening must not silently remove the panel.");
            Debug.Log("SATSUMA_CANONICAL_WIRING_PANEL " + ServicePanelState(part, phase + ":after-open-release"));
        }

        private static string ServicePanelFor(SatsumaWiringConnectorInteractionTarget endpoint) => endpoint.Connection switch
        {
            SatsumaElectricalConnection.Alternator or SatsumaElectricalConnection.HeadlightLeft or
                SatsumaElectricalConnection.HeadlightRight => "vehicle.satsuma.part.hood",
            SatsumaElectricalConnection.RearlightLeft or SatsumaElectricalConnection.RearlightRight =>
                "vehicle.satsuma.part.bootlid",
            SatsumaElectricalConnection.FuelTank when endpoint.Endpoint == 1 => "vehicle.satsuma.part.bootlid",
            _ => null,
        };

        private string EndpointPanelState(SatsumaWiringConnectorInteractionTarget endpoint, string phase)
        {
            string partId = ServicePanelFor(endpoint);
            return partId == null ? "no-service-panel" : ServicePanelState(
                assembly.Parts.Single(value => value.Definition.DefinitionId == partId), phase);
        }

        private void LogServicePanelStates(string phase)
        {
            foreach (string partId in new[] { "vehicle.satsuma.part.hood", "vehicle.satsuma.part.bootlid" })
                Debug.Log("SATSUMA_CANONICAL_WIRING_PANEL " + ServicePanelState(
                    assembly.Parts.Single(value => value.Definition.DefinitionId == partId), phase));
        }

        private string ServicePanelState(PartInstance part, string phase)
        {
            var hinge = part.GetComponent<AssemblyHingedPartInteractionTarget>();
            MountPointRuntime mount = assembly.Graph.FindMountForPart(part);
            PartSaveDto saved = sourceRecord.assembly.parts.Single(value => value.partDefinitionId == part.Definition.DefinitionId);
            return phase + " part=" + part.Definition.DefinitionId + " active=" + part.gameObject.activeInHierarchy +
                " installed=" + part.IsInstalled + " attached=" + hinge.IsAttachedToHinge +
                " open=" + hinge.OpenNormalized.ToString("F6") + " latched=" + hinge.IsClosedLatched +
                " openHeld=" + hinge.IsOpenHeld + " angularVelocity=" + part.Body.angularVelocity.ToString("F5") +
                " requiresRelease=" + hinge.RequiresReleaseBeforeOpening + " released=" + hinge.IsReleasedForOpening +
                " savedRotation=" + saved.worldRotation.ToString("F6") +
                " bodyRotation=" + part.Body.rotation.ToString("F6") +
                " mountRotation=" + (mount?.Authoring != null ? mount.Authoring.Pose.rotation.ToString("F6") : "missing");
        }

        private IEnumerator FindPhysicalApproach(SatsumaWiringConnectorInteractionTarget endpoint)
        {
            lastApproach = default;
            approaches.Clear();
            string phase = endpoint.Connection + "/" + endpoint.Endpoint;
            LogServicePanelStates("before-endpoint:" + phase);
            string servicePanelId = ServicePanelFor(endpoint);
            if (servicePanelId != null) yield return OpenServicePanelFully(servicePanelId, phase);
            Debug.Log("SATSUMA_CANONICAL_WIRING_PANEL " + EndpointPanelState(endpoint, "prepared-endpoint:" + phase));
            Assert.That(endpoint.IsEndpointAvailable, Is.True, endpoint.Connection + "/" + endpoint.Endpoint);
            Vector3 local = vehicle.transform.InverseTransformPoint(endpoint.transform.position);
            float side = local.x < 0f ? -1f : 1f;
            float longitudinal = local.z < 0f ? -1f : 1f;
            // Frozen mesh floor3 + transform put this one connector ~4mm above
            // the floor's underside and 52mm below its top. Its donor-evidenced
            // service side is below the car, not through the boot floor.
            bool underbodyTank = endpoint.Connection == SatsumaElectricalConnection.FuelTank && endpoint.Endpoint == 0;
            bool sharedFrontLampConnector =
                endpoint.Connection == SatsumaElectricalConnection.HeadlightLeft && endpoint.Endpoint == 0 ||
                endpoint.Connection == SatsumaElectricalConnection.HeadlightRight && endpoint.Endpoint == 1;
            bool individualLampConnector =
                endpoint.Connection == SatsumaElectricalConnection.HeadlightLeft && endpoint.Endpoint == 1 ||
                endpoint.Connection == SatsumaElectricalConnection.HeadlightRight && endpoint.Endpoint == 0;
            var directions = underbodyTank ? new List<Vector3> { Vector3.down } : new List<Vector3>
            {
                new Vector3(0f, .65f, longitudinal),
                new Vector3(side, .65f, 0f),
                Vector3.up,
                new Vector3(side, .35f, longitudinal),
                new Vector3(-side, .65f, 0f),
            };
            if (endpoint.Connection == SatsumaElectricalConnection.HeadlightLeft ||
                endpoint.Connection == SatsumaElectricalConnection.HeadlightRight)
            {
                // Donor front-panel mesh spans carZ[1.603675,1.722306].
                // Shared lamp connectorZ1.633 is 29mm from its engine-bay
                // side, but 89mm from the nose. Approach behind the installed
                // lamps through the normally opened hood; never through the
                // front panel. Two bounded lean heights, not a sphere scan.
                directions.Insert(0, new Vector3(0f, .65f, -1f));
                directions.Insert(1, new Vector3(0f, 1.5f, -1f));
                if (sharedFrontLampConnector)
                {
                    // The front-harness corner is beside the +X inner fender.
                    // One lower lean toward the engine-bay centre is the only
                    // fallback added to its measured rearward approach.
                    directions.Insert(1, new Vector3(-.5f, .15f, -1f));
                }
                else if (individualLampConnector)
                {
                    // These donor points lie on the outer/lower face of the
                    // inner fender, not the engine-bay side used by the shared
                    // harness. The measured two approaches come from outside,
                    // slightly behind the lamp: below it first, then level.
                    directions.Insert(0, new Vector3(side, -.35f, -.5f));
                    directions.Insert(1, new Vector3(side, 0f, -.5f));
                }
            }
            for (int attempt = 0; attempt < directions.Count; attempt++)
            {
                if (carry.HasHeldObject) carry.Drop();
                Vector3 aim = endpoint.transform.position;
                Vector3 outward = vehicle.transform.TransformDirection(directions[attempt].normalized);
                Vector3 cameraPosition = aim + outward * .7f;
                Assert.That(cameraPosition.y - .065f, Is.GreaterThan(serviceGroundTop), "Camera must remain above the service stand ground.");
                Collider[] cameraObstacles = Physics.OverlapSphere(cameraPosition, .065f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(value => value.attachedRigidbody != spoolBody && value.enabled && value.gameObject.activeInHierarchy).ToArray();
                if (cameraObstacles.Length > 0)
                {
                    approaches.Add("approach=" + attempt + " camera blocked at=" + cameraPosition.ToString("F5") +
                        " by=" + string.Join(",", cameraObstacles.Select(ColliderIdentity)));
                    continue;
                }
                Vector3 cameraUp = Mathf.Abs(Vector3.Dot(outward, vehicle.transform.up)) > .95f
                    ? vehicle.transform.forward : vehicle.transform.up;
                player.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(-outward, cameraUp));
                Vector3 start = player.transform.position + player.transform.forward * .3f;
                spoolBody.transform.SetPositionAndRotation(start, player.transform.rotation);
                spoolBody.position = start;
                spoolBody.rotation = player.transform.rotation;
                spoolBody.linearVelocity = Vector3.zero;
                spoolBody.angularVelocity = Vector3.zero;
                spoolBody.useGravity = false;
                Physics.SyncTransforms();
                if (SpoolStartsPenetrating(out string startObstacle))
                {
                    approaches.Add("approach=" + attempt + " loose spool start blocked by=" + startObstacle);
                    continue;
                }
                var context = new InteractionContext(player, player.transform.position, player.transform.forward);
                Assert.That(carry.TryPickup(spool.GetComponent<PhysicsPickupTarget>(), context), Is.True);
                Assert.That(carry.TryGetHeldCapability(out IHeldToolIdentity tool), Is.True);
                float commandedYaw = attempt <= 1
                    ? sharedFrontLampConnector ? -90f : individualLampConnector ? 180f : 0f
                    : 0f;
                if (commandedYaw != 0f)
                {
                    // Use the player's real rotation command, not a Rigidbody
                    // pose write. The shared corner uses -90; an individual
                    // lamp uses 180 to point the box's +13mm centre offset out
                    // of the fender, along the measured exterior approach.
                    Assert.That(interaction.CanRotateHeldObject, Is.True,
                        "The ordinary held-tool rotation action must be available.");
                    interaction.RotateHeldObject(new Vector2(commandedYaw, 0f));
                }
                float closest = float.PositiveInfinity;
                string lastHit = "none";
                string closestGeometry = "none";
                for (int step = 0; step < 65; step++)
                {
                    yield return new WaitForFixedUpdate();
                    if (!carry.HasHeldObject) break;
                    Physics.SyncTransforms();
                    interaction.RefreshCandidate();
                    float distance = Vector3.Distance(spoolBody.position, endpoint.transform.position);
                    if (distance < closest)
                    {
                        closest = distance;
                        closestGeometry = "step=" + step + " " + SpoolGeometry(endpoint.transform.position) +
                            " servicePanel=[" + EndpointPanelState(endpoint, "closest-contact") + "]";
                    }
                    lastHit = query.HasLastHit ? query.LastHit.collider.name : "none";
                    if (distance <= .095f && interaction.CurrentCandidate.TryGetCapability(
                        out SatsumaWiringConnectorInteractionTarget selected) && selected.CanActivateHeldTool(tool, context))
                    {
                        lastApproach = new ApproachResult { Reached = true, Index = attempt, Distance = distance,
                            Hit = lastHit, Camera = player.transform.position, Spool = spoolBody.position,
                            CommandedYaw = commandedYaw };
                        Debug.Log("SATSUMA_CANONICAL_WIRING_PANEL " + EndpointPanelState(endpoint, "reached-endpoint:" + phase));
                        yield break;
                    }
                }
                approaches.Add("approach=" + attempt + " closest=" + closest.ToString("F5") + " hit=" + lastHit +
                    " held=" + carry.HasHeldObject + " rotateYaw=" + commandedYaw.ToString("F1") + " aim=" + aim.ToString("F5") +
                    " intendedAnchor=" + (aim + outward * .05f).ToString("F5") + " " + closestGeometry);
            }
        }

        private string SpoolGeometry(Vector3 endpointPosition)
        {
            var details = new List<string>();
            foreach (Collider own in spool.GetComponentsInChildren<Collider>(true))
            {
                string shape = own is BoxCollider box
                    ? " boxCenter=" + box.center.ToString("F5") + " boxSize=" + box.size.ToString("F5")
                    : " shape=" + own.GetType().Name;
                details.Add("own=" + ColliderIdentity(own) + shape + " enabled=" + own.enabled +
                    " active=" + own.gameObject.activeInHierarchy + " trigger=" + own.isTrigger +
                    " boundsCenter=" + own.bounds.center.ToString("F5") + " boundsSize=" + own.bounds.size.ToString("F5"));
                if (!own.enabled || !own.gameObject.activeInHierarchy || own.isTrigger) continue;
                foreach (Collider other in Physics.OverlapBox(own.bounds.center, own.bounds.extents + Vector3.one * .025f,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(value => value != own && value.attachedRigidbody != spoolBody)
                    .OrderBy(value => (value.ClosestPoint(own.bounds.center) - own.bounds.center).sqrMagnitude).Take(6))
                {
                    bool penetrating = Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out Vector3 normal, out float depth);
                    if (!penetrating) { normal = Vector3.zero; depth = 0f; } // Output values are undefined for a non-overlap.
                    Vector3 otherSurface = other.ClosestPoint(own.bounds.center);
                    float surfaceGap = Vector3.Distance(own.ClosestPoint(otherSurface), otherSurface);
                    details.Add("near=" + ColliderIdentity(other) + " surfaceGap=" + surfaceGap.ToString("F6") +
                        " penetration=" + penetrating + "/" + depth.ToString("F6") + " normal=" + normal.ToString("F5") +
                        " point=" + otherSurface.ToString("F5") + " endpointToSurface=" +
                        Vector3.Distance(endpointPosition, other.ClosestPoint(endpointPosition)).ToString("F6"));
                }
            }
            return "pivot=" + spoolBody.position.ToString("F5") + " rotation=" + spoolBody.rotation.ToString("F5") +
                " mass=" + spoolBody.mass + " geometry=[" + string.Join(" | ", details) + "]";
        }

        private static string ColliderIdentity(Collider collider)
        {
            PartInstance owner = collider.GetComponentInParent<PartInstance>(true);
            return collider.name + "(part=" + (owner != null ? owner.Definition.DefinitionId : "none") +
                ",body=" + (collider.attachedRigidbody != null ? collider.attachedRigidbody.name : "static") + ")";
        }

        private bool SpoolStartsPenetrating(out string obstacle)
        {
            obstacle = "none";
            foreach (Collider own in spool.GetComponents<Collider>())
            {
                if (!own.enabled || own.isTrigger) continue;
                foreach (Collider other in Physics.OverlapBox(own.bounds.center, own.bounds.extents,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other == own || other.attachedRigidbody == spoolBody) continue;
                    if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out Vector3 normal, out float distance) && distance > .001f)
                    {
                        obstacle = ColliderIdentity(other) + " depth=" + distance.ToString("F6") +
                            " normal=" + normal.ToString("F5") + " spool=" + spoolBody.position.ToString("F5") +
                            " rotation=" + spoolBody.rotation.ToString("F5");
                        return true;
                    }
                }
            }
            return false;
        }

        private SatsumaWiringConnectorInteractionTarget Endpoint(SatsumaElectricalConnection connection, int end) =>
            endpoints.Single(value => value.Connection == connection && value.Endpoint == end);

        private void ResetTestElectricalState()
        {
            if (carry.HasHeldObject) carry.Drop();
            Assert.That(electrical.TryRestore(JsonUtility.FromJson<SatsumaElectricalSaveDto>(unconnectedElectricalJson),
                out string failure), Is.True, failure);
        }

        private byte[] PendingSnapshot() => Enum.GetValues(typeof(SatsumaElectricalConnection))
            .Cast<SatsumaElectricalConnection>().Select(connection => (byte)(
            (electrical.IsEndpointArmed(connection, 0) ? 1 : 0) |
            (electrical.IsEndpointArmed(connection, 1) ? 2 : 0))).ToArray();

        private string Diagnostics(SatsumaElectricalConnection connection, int end) =>
            connection + "/" + end + " approach=" + lastApproach.Index + " distance=" + lastApproach.Distance.ToString("F5") +
            " hit=" + lastApproach.Hit + " camera=" + lastApproach.Camera.ToString("F4") +
            " spool=" + lastApproach.Spool.ToString("F4") + " rotateYaw=" + lastApproach.CommandedYaw.ToString("F1") +
            " attempts=[" + string.Join("; ", approaches) + "]";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var failures = new List<Exception>();
            void Run(Action action) { try { action(); } catch (Exception exception) { failures.Add(exception); } }
            AsyncOperation unload = null;
            try
            {
                if (carry != null && carry.HasHeldObject) Run(() => carry.Drop());
                if (runtime != null)
                    foreach (WorldItemInstance item in runtime.LoadedInstances.ToArray())
                        Run(() => Assert.That(runtime.TryRemoveDynamic(item), Is.True));
            }
            finally
            {
                try
                {
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        Run(() =>
                        {
                            foreach (GameObject root in scene.GetRootGameObjects()) Run(() => root.SetActive(false));
                        });
                        Run(() => unload = SceneManager.UnloadSceneAsync(scene));
                    }
                }
                finally
                {
                    if (fixtureStarted) Time.timeScale = previousTimeScale;
                }
            }

            try
            {
                if (unload != null) yield return unload;
                yield return null;
            }
            finally
            {
                foreach (ScriptableObject asset in ownedAssets) Run(() => { if (asset != null) Object.Destroy(asset); });
                ownedAssets.Clear();
                if (fixtureStarted)
                {
                    Time.timeScale = previousTimeScale;
                    Run(() => Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub)));
                }
                if (!string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(sourceHash))
                    Run(() => Assert.That(Hash(File.ReadAllBytes(sourcePath)), Is.EqualTo(sourceHash),
                        "Native source changed during read-only wiring coverage; this fixture never writes it."));
                if (sourceRecord != null)
                    Run(() => Assert.That(JsonUtility.ToJson(sourceRecord), Is.EqualTo(sourceRecordJson)));
                if (failures.Count > 0) throw new AggregateException("Canonical wiring fixture cleanup failed.", failures);
            }
        }

        private GameObject SceneRoot(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            ownedAssets.Add(asset);
            return asset;
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private struct ApproachResult
        {
            public bool Reached;
            public int Index;
            public float Distance;
            public float CommandedYaw;
            public string Hit;
            public Vector3 Camera, Spool;
        }

        [Serializable] private sealed class NativeProjection { public DomainProjection[] Domains; }
        [Serializable] private sealed class DomainProjection { public string DomainId; public string PayloadJson; }
    }
}
#endif
