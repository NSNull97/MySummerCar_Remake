using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Home;
using MSC.Interaction;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.LegacyImport;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Bounded project-owned appliance integration for the donor fridge and
    /// cooking surfaces. Stable transfer IDs bind presentation; Home and Items
    /// remain the only gameplay/save authorities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionFoodApplianceInstaller : MonoBehaviour,
        IItemFoodEnvironment
    {
        public const string HomeCellId = "cell_0_-3";
        public const string FridgeDoorMeshStableId =
            "5beeac8c1e16e46351b1224a92b15261";
        public const string FridgePaperStableId =
            "72d34129df6c41f282ca82028a53cfd6";
        public const string FridgeHandleStableId =
            "63469f2df1f8e8e8460d03f76327f7e1";
        public const string FridgeClosedVolumeColliderStableId =
            "6a575ded49fa3971746b0a57d50b39db";
        public const float FridgeDoorSwingDegrees = 92.1f;
        public const float FridgeDoorAngularSpeedDegrees = 158f;

        private static readonly Vector3 FridgePivotPosition =
            new Vector3(157.5036f, 2.493121f, -1032.6195f);
        private static readonly Quaternion FridgePivotRotation =
            new Quaternion(-0.5000001f, -0.5f, -0.5f, 0.5000001f);
        private static readonly Bounds FridgeInteriorBounds =
            new Bounds(
                new Vector3(157.805f, 2.49f, -1032.36f),
                new Vector3(0.58f, 0.90f, 0.49f));
        private static readonly string[] FridgeDisabledBaselineColliderIds =
        {
            FridgeDoorMeshStableId,
            FridgePaperStableId,
            FridgeHandleStableId,
            FridgeClosedVolumeColliderStableId,
        };
        private static readonly string[] FridgeShelfStableIds =
        {
            "4441972214ce0fd6e38c4e1f41c948a3",
            "d7ddd6406cc58a123057294a708ba55f",
            "87fae96ce94102dcf2110edc85b887de",
        };

        private ProductionWorldStreamingService streaming;
        private ItemWorldRuntime items;
        private HomeSystemRuntime home;
        private IAudioBackend audioBackend;
        private Transform fixedTargetRoot;
        private FridgeDoorInteractionTarget fridgeDoor;
        private int fridgeSceneHandle;
        private bool initialized;

        public bool IsInitialized => initialized;
        public Bounds RefrigeratedWorldBounds => FridgeInteriorBounds;
        public FridgeDoorInteractionTarget FridgeDoor => fridgeDoor;

        public void Initialize(
            Transform compositionRoot,
            ProductionWorldStreamingService configuredStreaming,
            ItemWorldRuntime configuredItems,
            HomeSystemRuntime configuredHome,
            IAudioBackend configuredAudio)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Food appliance installer is already initialized.");
            }

            fixedTargetRoot = compositionRoot ??
                throw new ArgumentNullException(nameof(compositionRoot));
            streaming = configuredStreaming ??
                throw new ArgumentNullException(nameof(configuredStreaming));
            items = configuredItems ??
                throw new ArgumentNullException(nameof(configuredItems));
            home = configuredHome ??
                throw new ArgumentNullException(nameof(configuredHome));
            audioBackend = configuredAudio;

            items.SetFoodEnvironment(this);
            CreateFixedCookingSources();
            streaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            streaming.OwnedSceneWillUnload += HandleOwnedSceneWillUnload;
            initialized = true;

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded &&
                    streaming.TryGetCellIdForScene(scene, out string cellId) &&
                    string.Equals(cellId, HomeCellId, StringComparison.Ordinal))
                {
                    BindFridge(scene);
                }
            }
        }

        public bool IsRefrigerated(WorldItemInstance item)
        {
            return item != null &&
                   IsRefrigerated(item.transform.position);
        }

        public bool IsRefrigerated(Vector3 worldPosition)
        {
            return home != null && home.FridgeCoolingActive &&
                   FridgeInteriorBounds.Contains(worldPosition);
        }

        private void FixedUpdate()
        {
            if (!initialized || home.Snapshot.FridgeDoorOpen)
            {
                return;
            }

            foreach (WorldItemInstance item in items.LoadedInstances)
            {
                if (item == null || item.State.isConsumed ||
                    !FridgeInteriorBounds.Contains(item.transform.position))
                {
                    continue;
                }

                PhysicsPickupTarget pickup =
                    item.GetComponent<PhysicsPickupTarget>();
                Rigidbody body = item.GetComponent<Rigidbody>();
                if (body == null || pickup?.IsCarried == true ||
                    body.isKinematic)
                {
                    continue;
                }

                // The closed solid door and donor shelf colliders own
                // containment. Sleeping removes residual spawn/contact energy
                // without replacing the saved world pose or loose-body flags.
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }
        }

        private void CreateFixedCookingSources()
        {
            CreateFixedHeatSource(
                "Home stove cooking volume",
                new Vector3(161.573f, 1.946f, -1034.669f),
                new Vector3(0.56f, 0.18f, 0.56f),
                () => home.StoveHeatingActive);
            CreateFixedHeatSource(
                "Cottage mangal cooking volume",
                new Vector3(-677.6259f, -1.127f, -526.9669f),
                new Vector3(0.68f, 0.28f, 0.58f),
                () => home.MangalHeatingActive);

            var targetObject = new GameObject("Cottage mangal fire target");
            targetObject.transform.SetParent(fixedTargetRoot, false);
            targetObject.transform.position =
                new Vector3(-677.6259f, -1.02f, -526.9669f);
            SphereCollider collider =
                targetObject.AddComponent<SphereCollider>();
            collider.radius = 0.16f;
            HomeInteractionTarget target =
                targetObject.AddComponent<HomeInteractionTarget>();
            target.Configure(
                HomeActionIds.MangalFireToggle,
                home.GetInteractionPrompt(HomeActionKind.MangalFireToggle),
                HomeActionKind.MangalFireToggle,
                home);
            targetObject.AddComponent<InteractionTargetHost>().Configure(target);
        }

        private void CreateFixedHeatSource(
            string objectName,
            Vector3 worldPosition,
            Vector3 worldSize,
            Func<bool> enabledProvider)
        {
            var sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(fixedTargetRoot, false);
            sourceObject.transform.position = worldPosition;
            ItemHeatSourceVolume volume =
                sourceObject.AddComponent<ItemHeatSourceVolume>();
            volume.ConfigureFixed(
                items,
                enabledProvider,
                1f,
                Vector3.zero,
                worldSize);
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            if (streaming.TryGetCellIdForScene(scene, out string cellId) &&
                string.Equals(cellId, HomeCellId, StringComparison.Ordinal))
            {
                BindFridge(scene);
            }
        }

        private void HandleOwnedSceneWillUnload(Scene scene)
        {
            if (scene.handle == fridgeSceneHandle)
            {
                fridgeDoor = null;
                fridgeSceneHandle = 0;
            }
        }

        private void BindFridge(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                fridgeDoor != null && fridgeSceneHandle == scene.handle)
            {
                return;
            }

            Dictionary<string, DonorWorldBaselineEntityMetadata> metadata =
                CollectMetadata(scene);
            if (!metadata.TryGetValue(
                    FridgeDoorMeshStableId,
                    out DonorWorldBaselineEntityMetadata doorMesh) ||
                !metadata.TryGetValue(
                    FridgeHandleStableId,
                    out DonorWorldBaselineEntityMetadata handle))
            {
                Debug.LogError(
                    "Project fridge binding could not find its stable donor leaves.",
                    this);
                return;
            }

            var pivotObject = new GameObject("Fridge Gameplay Hinge");
            SceneManager.MoveGameObjectToScene(pivotObject, scene);
            Transform pivot = pivotObject.transform;
            pivot.SetPositionAndRotation(
                FridgePivotPosition,
                FridgePivotRotation);

            ReparentStableLeaf(metadata, FridgeDoorMeshStableId, pivot);
            ReparentStableLeaf(metadata, FridgePaperStableId, pivot);
            ReparentStableLeaf(metadata, FridgeHandleStableId, pivot);

            // Sanitized baseline colliders are separate static scene roots.
            // Leaving them enabled would keep an invisible closed door in
            // front of the food after the visible leaves start moving.
            // The coarse LOD collider is a solid fridge-sized volume. It is
            // superseded by the audited cabinet/shelf colliders plus the
            // moving door; retaining it would block pickup and depenetrate
            // every loose item stored inside.
            DisableBaselineColliderCopies(
                scene,
                FridgeDisabledBaselineColliderIds);

            Rigidbody hingeBody = pivotObject.AddComponent<Rigidbody>();
            hingeBody.useGravity = false;
            hingeBody.isKinematic = true;
            hingeBody.detectCollisions = true;
            hingeBody.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider doorCollider =
                doorMesh.GetComponent<BoxCollider>();
            if (doorCollider == null)
            {
                doorCollider =
                    doorMesh.gameObject.AddComponent<BoxCollider>();
            }
            doorCollider.center =
                new Vector3(-0.01f, -0.092291504f, 0.40041876f);
            doorCollider.size =
                new Vector3(0.05f, 0.6459472f, 0.93994606f);
            doorCollider.isTrigger = false;
            doorCollider.enabled = true;

            HingedDoorInteractionTarget hinge =
                pivotObject.AddComponent<HingedDoorInteractionTarget>();
            hinge.Configure(
                pivot,
                pivot.localRotation,
                Vector3.forward,
                FridgeDoorSwingDegrees,
                FridgeDoorAngularSpeedDegrees,
                "Открыть холодильник",
                "Закрыть холодильник");

            FridgeDoorInteractionTarget controller =
                pivotObject.AddComponent<FridgeDoorInteractionTarget>();
            controller.Configure(hinge, home, audioBackend);

            var handleZone = new GameObject("Fridge handle interaction zone");
            handleZone.transform.SetParent(pivot, worldPositionStays: false);
            handleZone.transform.position = handle.transform.position;
            SphereCollider handleCollider =
                handleZone.AddComponent<SphereCollider>();
            handleCollider.radius = 0.2f;
            handleCollider.isTrigger = true;
            InteractionTargetHost host =
                handleZone.AddComponent<InteractionTargetHost>();
            host.Configure(controller);
            host.ConfigureOutlineRenderers(
                handle.GetComponentsInChildren<Renderer>(true));

            EnsureBaselineShelfCollidersEnabled(scene);
            controller.RestoreFromHome(immediate: true);
            fridgeDoor = controller;
            fridgeSceneHandle = scene.handle;
        }

        private static Dictionary<string, DonorWorldBaselineEntityMetadata>
            CollectMetadata(Scene scene)
        {
            var result = new Dictionary<
                string,
                DonorWorldBaselineEntityMetadata>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DonorWorldBaselineEntityMetadata[] entries =
                    root.GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true);
                for (int index = 0; index < entries.Length; index++)
                {
                    DonorWorldBaselineEntityMetadata entry = entries[index];
                    if (entry != null &&
                        !string.IsNullOrWhiteSpace(entry.StableId))
                    {
                        result[entry.StableId] = entry;
                    }
                }
            }

            return result;
        }

        private static void ReparentStableLeaf(
            IReadOnlyDictionary<string, DonorWorldBaselineEntityMetadata> metadata,
            string stableId,
            Transform pivot)
        {
            if (metadata.TryGetValue(
                    stableId,
                    out DonorWorldBaselineEntityMetadata entry) &&
                entry != null)
            {
                entry.transform.SetParent(pivot, worldPositionStays: true);
            }
        }

        private static void DisableBaselineColliderCopies(
            Scene scene,
            string[] entityStableIds)
        {
            foreach (DonorWorldBaselineColliderMetadata metadata in
                     CollectColliderMetadata(scene))
            {
                if (metadata == null ||
                    Array.IndexOf(entityStableIds, metadata.EntityStableId) < 0)
                {
                    continue;
                }

                foreach (Collider collider in metadata.GetComponents<Collider>())
                {
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }
            }
        }

        private static void EnsureBaselineShelfCollidersEnabled(Scene scene)
        {
            foreach (DonorWorldBaselineColliderMetadata metadata in
                     CollectColliderMetadata(scene))
            {
                if (metadata == null ||
                    Array.IndexOf(
                        FridgeShelfStableIds,
                        metadata.EntityStableId) < 0)
                {
                    continue;
                }

                foreach (Collider collider in metadata.GetComponents<Collider>())
                {
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                }
            }
        }

        private static IEnumerable<DonorWorldBaselineColliderMetadata>
            CollectColliderMetadata(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DonorWorldBaselineColliderMetadata[] entries =
                    root.GetComponentsInChildren<
                        DonorWorldBaselineColliderMetadata>(true);
                for (int index = 0; index < entries.Length; index++)
                {
                    yield return entries[index];
                }
            }
        }

        private void OnDestroy()
        {
            if (streaming != null)
            {
                streaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
                streaming.OwnedSceneWillUnload -= HandleOwnedSceneWillUnload;
            }

            if (items != null)
            {
                items.SetFoodEnvironment(null);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class FridgeDoorInteractionTarget : MonoBehaviour,
        IContextInteractionTarget
    {
        private HingedDoorInteractionTarget hinge;
        private HomeSystemRuntime home;
        private IAudioBackend audioBackend;

        public string InteractionPrompt =>
            hinge?.InteractionPrompt ?? string.Empty;

        public bool CanInteract(in InteractionContext context) =>
            hinge != null && hinge.CanInteract(context);

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            bool open = !hinge.TargetOpen;
            hinge.SetOpen(open, immediate: false);
            home.SetFridgeDoorOpen(open);
            if (audioBackend?.IsReady == true)
            {
                audioBackend.PostEvent(new AudioEventRequest(
                    open
                        ? AudioProjectIds.Events.InteractionDoorOpen
                        : AudioProjectIds.Events.InteractionDoorClose,
                    worldPosition: transform.position));
            }
        }

        public void Configure(
            HingedDoorInteractionTarget configuredHinge,
            HomeSystemRuntime configuredHome,
            IAudioBackend configuredAudio)
        {
            hinge = configuredHinge ??
                throw new ArgumentNullException(nameof(configuredHinge));
            home = configuredHome ??
                throw new ArgumentNullException(nameof(configuredHome));
            audioBackend = configuredAudio;
            home.StateChanged += HandleHomeStateChanged;
        }

        public void RestoreFromHome(bool immediate)
        {
            if (hinge != null && home != null)
            {
                hinge.SetOpen(home.Snapshot.FridgeDoorOpen, immediate);
            }
        }

        private void HandleHomeStateChanged(HomeStateSnapshot snapshot)
        {
            if (hinge != null && hinge.TargetOpen != snapshot.FridgeDoorOpen)
            {
                hinge.SetOpen(snapshot.FridgeDoorOpen, immediate: true);
            }
        }

        private void OnDestroy()
        {
            if (home != null)
            {
                home.StateChanged -= HandleHomeStateChanged;
            }
        }
    }
}
