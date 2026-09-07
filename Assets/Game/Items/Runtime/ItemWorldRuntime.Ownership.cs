using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items
{
    /// <summary>Explicit field ownership boundary; Items never references a vehicle module.</summary>
    public interface IItemExternalPhysicsOwner
    {
        bool OwnsDefinition(string itemDefinitionId);
        void BindMaterialized(WorldItemInstance instance);
        void ReconcilePresentation(WorldItemInstance instance, GameObject presentationRoot);
        bool TryReleaseInstance(WorldItemInstance instance, out string failure);
    }

    public sealed partial class ItemWorldRuntime
    {
        private IItemExternalPhysicsOwner externalPhysicsOwner;
        private HashSet<string> externalOwnershipPlan = new HashSet<string>(StringComparer.Ordinal);
        private ItemDynamicRestoreTransaction dynamicRestoreTransaction;

        public bool IsExternallyOwned(string stableEntityId) =>
            !string.IsNullOrEmpty(stableEntityId) &&
            (externalOwnershipPlan.Contains(stableEntityId) ||
             instances.TryGetValue(stableEntityId, out WorldItemInstance instance) &&
             instance != null && externalPhysicsOwner != null &&
             externalPhysicsOwner.OwnsDefinition(instance.Definition.DefinitionId));

        public string[] CaptureExternalOwnershipPlan() =>
            externalOwnershipPlan.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public void RetainExternalOwnership(WorldItemInstance instance)
        {
            if (instance == null || externalPhysicsOwner == null || instance.State.isConsumed ||
                !externalPhysicsOwner.OwnsDefinition(instance.Definition.DefinitionId) ||
                !instances.TryGetValue(instance.StableId.Value, out WorldItemInstance registered) || registered != instance)
                throw new InvalidOperationException("Only a registered externally owned wrapper may retain ownership.");
            // Remains true while the aggregate and logical item are deferred together.
            externalOwnershipPlan.Add(instance.StableId.Value);
        }

        public void SetExternalOwnershipPlan(IReadOnlyCollection<string> stableEntityIds)
        {
            if (stableEntityIds != null && stableEntityIds.Count > 4096)
                throw new ArgumentException("Ownership plan exceeds the item domain limit.", nameof(stableEntityIds));
            var replacement = new HashSet<string>(StringComparer.Ordinal);
            if (stableEntityIds != null)
                foreach (string id in stableEntityIds)
                {
                    if (!MSC.Core.Identity.StableEntityId.TryParse(id, out _) || !replacement.Add(id))
                        throw new ArgumentException("Ownership plans require unique stable item identities.", nameof(stableEntityIds));
                }
            externalOwnershipPlan = replacement;
        }

        public void RegisterExternalPhysicsOwner(IItemExternalPhysicsOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (externalPhysicsOwner != null && !ReferenceEquals(externalPhysicsOwner, owner))
                throw new InvalidOperationException("An item runtime already has an external physics owner.");
            externalPhysicsOwner = owner;
        }

        public void UnregisterExternalPhysicsOwner(IItemExternalPhysicsOwner owner)
        {
            if (ReferenceEquals(externalPhysicsOwner, owner)) externalPhysicsOwner = null;
        }

        public ItemDynamicRestoreTransaction BeginDynamicRestoreTransaction()
        {
            if (dynamicRestoreTransaction != null)
                throw new InvalidOperationException("Nested item restore transactions are not supported.");
            dynamicRestoreTransaction = new ItemDynamicRestoreTransaction(this,
                instances.Values.Where(item => item != null && !IsCanonicalPlacement(item.StableId.Value)),
                new Dictionary<string, string>(sourceCellIds, StringComparer.Ordinal),
                new Dictionary<string, Pose>(recoveryPoses, StringComparer.Ordinal),
                CaptureExternalOwnershipPlan());
            return dynamicRestoreTransaction;
        }

        internal void StageRestoreRemoval(WorldItemInstance instance)
        {
            // Journal enlistment occurs before notifications, including throwing subscribers.
            NotifyDestroyed(instance);
            sourceCellIds.Remove(instance.StableId.Value);
            recoveryPoses.Remove(instance.StableId.Value);
            instance.gameObject.SetActive(false);
        }

        internal void RestoreRetainedItem(WorldItemInstance instance, bool wasActive)
        {
            string id = instance.StableId.Value;
            if (instances.TryGetValue(id, out WorldItemInstance existing) && existing != null && existing != instance)
                throw new InvalidOperationException("A rollback item identity is occupied by a different wrapper.");
            instances[id] = instance;
            instance.gameObject.SetActive(wasActive);
        }

        internal void RestoreRetainedProvenance(string id, Dictionary<string, string> sources,
            Dictionary<string, Pose> poses)
        {
            if (sources.TryGetValue(id, out string source)) sourceCellIds[id] = source;
            if (poses.TryGetValue(id, out Pose pose)) recoveryPoses[id] = pose;
        }

        internal void CompleteItemRestoreTransaction(ItemDynamicRestoreTransaction transaction,
            Dictionary<string, string> oldSources, Dictionary<string, Pose> oldRecovery,
            string[] oldPlan, bool rollback)
        {
            if (!ReferenceEquals(dynamicRestoreTransaction, transaction))
                throw new InvalidOperationException("The item restore transaction is no longer active.");
            if (rollback)
            {
                sourceCellIds.Clear();
                foreach (var pair in oldSources) sourceCellIds.Add(pair.Key, pair.Value);
                recoveryPoses.Clear();
                foreach (var pair in oldRecovery) recoveryPoses.Add(pair.Key, pair.Value);
                SetExternalOwnershipPlan(oldPlan);
            }
            dynamicRestoreTransaction = null;
        }

        internal void RemoveIntroducedRestoreItem(WorldItemInstance instance)
        {
            if (instance == null) return;
            // The save owner restores controller registrations before rollback reaches here.
            if (externalPhysicsOwner != null &&
                !externalPhysicsOwner.TryReleaseInstance(instance, out string failure))
                throw new InvalidOperationException("Cannot release a restored item: " + failure);
            NotifyDestroyed(instance);
            instance.gameObject.SetActive(false);
        }

        internal static void DestroyRetiredItem(WorldItemInstance instance)
        {
            if (instance == null) return;
            // Already unregistered; destruction does not publish a second InstanceRemoved.
            instance.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(instance.gameObject);
            else DestroyImmediate(instance.gameObject);
        }

        private void AbortDynamicMaterialization(string stableId)
        {
            if (instances.TryGetValue(stableId, out WorldItemInstance failed) && failed != null)
            {
                externalPhysicsOwner?.TryReleaseInstance(failed, out _);
                NotifyDestroyed(failed);
                DestroyRetiredItem(failed);
            }
            sourceCellIds.Remove(stableId);
            recoveryPoses.Remove(stableId);
        }
    }

    /// <summary>
    /// Holds wrapper references until the save coordinator commits. The coordinator
    /// restores external registry membership before replaying vehicle/world checkpoints.
    /// </summary>
    public sealed class ItemDynamicRestoreTransaction
    {
        private readonly ItemWorldRuntime runtime;
        private readonly Dictionary<string, ItemTopology> original = new Dictionary<string, ItemTopology>(StringComparer.Ordinal);
        private readonly HashSet<WorldItemInstance> retired = new HashSet<WorldItemInstance>();
        private readonly HashSet<WorldItemInstance> introduced = new HashSet<WorldItemInstance>();
        private readonly Dictionary<string, string> sourceCells;
        private readonly Dictionary<string, Pose> recovery;
        private readonly string[] ownershipPlan;
        private bool completed;

        internal ItemDynamicRestoreTransaction(ItemWorldRuntime owner, IEnumerable<WorldItemInstance> items,
            Dictionary<string, string> sources, Dictionary<string, Pose> recoveryPoses, string[] plan)
        {
            runtime = owner;
            sourceCells = sources;
            recovery = recoveryPoses;
            ownershipPlan = plan;
            foreach (WorldItemInstance item in items) original.Add(item.StableId.Value, new ItemTopology(item));
        }

        internal void TrackMaterialized(WorldItemInstance instance)
        {
            if (!original.TryGetValue(instance.StableId.Value, out ItemTopology old) || old.Instance != instance)
                introduced.Add(instance);
        }

        internal void StageRemoval(WorldItemInstance instance)
        {
            ThrowIfCompleted();
            retired.Add(instance);
            runtime.StageRestoreRemoval(instance);
        }

        internal bool TryRestoreRetainedInstance(string id, out WorldItemInstance instance)
        {
            instance = null;
            if (!original.TryGetValue(id, out ItemTopology saved) || saved.Instance == null || !retired.Remove(saved.Instance))
                return false;
            instance = saved.Instance;
            runtime.RestoreRetainedItem(instance, saved.Active);
            runtime.RestoreRetainedProvenance(id, sourceCells, recovery);
            return true;
        }

        public void RestoreRetainedInstances()
        {
            ThrowIfCompleted();
            foreach (ItemTopology saved in original.Values)
            {
                if (saved.Instance == null)
                    throw new InvalidOperationException("An original item wrapper was destroyed before save commit.");
                runtime.RestoreRetainedItem(saved.Instance, saved.Active);
                runtime.RestoreRetainedProvenance(saved.Instance.StableId.Value, sourceCells, recovery);
                retired.Remove(saved.Instance);
            }
        }

        public void Rollback()
        {
            ThrowIfCompleted();
            foreach (WorldItemInstance item in introduced)
            {
                runtime.RemoveIntroducedRestoreItem(item);
                ItemWorldRuntime.DestroyRetiredItem(item);
            }
            RestoreRetainedInstances();
            foreach (ItemTopology saved in original.Values) saved.RestoreTopology();
            runtime.CompleteItemRestoreTransaction(this, sourceCells, recovery, ownershipPlan, rollback: true);
            completed = true;
        }

        public void Commit()
        {
            ThrowIfCompleted();
            // Commit is called outside the reversible apply boundary. Cleanup failures
            // are reported separately and must never initiate rollback to destroyed refs.
            runtime.CompleteItemRestoreTransaction(this, sourceCells, recovery, ownershipPlan, rollback: false);
            completed = true;
            foreach (WorldItemInstance item in retired)
            {
                try { ItemWorldRuntime.DestroyRetiredItem(item); }
                catch (Exception exception) { Debug.LogException(exception, runtime); }
            }
        }

        private void ThrowIfCompleted()
        {
            if (completed) throw new InvalidOperationException("The item restore transaction is complete.");
        }

        private sealed class ItemTopology
        {
            public readonly WorldItemInstance Instance;
            public readonly bool Active;
            private readonly Transform parent;
            private readonly Scene scene;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly Rigidbody body;
            private readonly bool isKinematic, gravity, collisions, sleeping;
            private readonly RigidbodyInterpolation interpolation;
            private readonly Vector3 velocity, angularVelocity;

            public ItemTopology(WorldItemInstance item)
            {
                Instance = item;
                Active = item.gameObject.activeSelf;
                parent = item.transform.parent;
                scene = item.gameObject.scene;
                body = item.GetComponent<Rigidbody>();
                // Active PhysX actors may be ahead of their interpolated Transform.
                // Inactive bodies have no actor in Unity 6.6, so their Transform
                // retains the authoritative pose for a later activation/rollback.
                bool hasActiveBody = body != null && body.gameObject.activeInHierarchy;
                position = hasActiveBody ? body.position : item.transform.position;
                rotation = hasActiveBody ? body.rotation : item.transform.rotation;
                scale = item.transform.localScale;
                if (body == null) return;
                isKinematic = body.isKinematic; gravity = body.useGravity;
                collisions = body.detectCollisions; sleeping = body.IsSleeping();
                interpolation = body.interpolation;
                velocity = body.linearVelocity; angularVelocity = body.angularVelocity;
            }

            public void RestoreTopology()
            {
                Transform transform = Instance.transform;
                if (transform.parent != parent) transform.SetParent(parent, true);
                if (parent == null && scene.IsValid() && scene.isLoaded && Instance.gameObject.scene != scene)
                    SceneManager.MoveGameObjectToScene(Instance.gameObject, scene);
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = scale;
                if (body == null) return;
                body.interpolation = RigidbodyInterpolation.None;
                body.detectCollisions = false;
                body.isKinematic = true;
                body.position = position; body.rotation = rotation;
                body.useGravity = gravity; body.isKinematic = isKinematic;
                if (!isKinematic)
                {
                    body.linearVelocity = velocity; body.angularVelocity = angularVelocity;
                    if (sleeping) body.Sleep(); else body.WakeUp();
                }
                body.detectCollisions = collisions; body.interpolation = interpolation;
            }
        }
    }
}
