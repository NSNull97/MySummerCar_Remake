using System;
using System.Collections.Generic;
using MSC.Interaction;
using MSC.Items;
using UnityEngine;

namespace MSC.Lighting.Production
{
    /// <summary>
    /// Gives the stable Phase 1 flashlight item a real HDRP spot light. Item
    /// state remains authoritative, so pickup, streaming and save restore do
    /// not create a second flashlight state machine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortableFlashlightLightingAdapter : MonoBehaviour
    {
        private const string FlashlightDefinitionId = "item.flashlight";

        private sealed class RuntimeBinding
        {
            public WorldItemInstance Item;
            public GameObject FixtureRoot;
            public GameLightFixture Fixture;
            public string SwitchId;
            public Action<IItemStatusSource> StatusHandler;
        }

        private readonly Dictionary<EntityId, RuntimeBinding> bindings =
            new Dictionary<EntityId, RuntimeBinding>();

        private ItemWorldRuntime items;
        private LightingProfileCatalog profiles;
        private ElectricalGridService grid;
        private bool initialized;

        public int BoundFlashlightCount => bindings.Count;

        public bool TryGetFixture(
            string stableEntityId,
            out GameLightFixture fixture)
        {
            foreach (RuntimeBinding binding in bindings.Values)
            {
                if (binding.Item != null &&
                    binding.Item.StableId.IsValid &&
                    string.Equals(
                        binding.Item.StableId.Value,
                        stableEntityId,
                        StringComparison.Ordinal) &&
                    binding.Fixture != null)
                {
                    fixture = binding.Fixture;
                    return true;
                }
            }

            fixture = null;
            return false;
        }

        public void Initialize(
            ItemWorldRuntime configuredItems,
            LightingProfileCatalog configuredProfiles,
            ElectricalGridService configuredGrid)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Portable flashlight lighting is already initialized.");
            }

            items = configuredItems ??
                throw new ArgumentNullException(nameof(configuredItems));
            profiles = configuredProfiles ??
                throw new ArgumentNullException(nameof(configuredProfiles));
            grid = configuredGrid ??
                throw new ArgumentNullException(nameof(configuredGrid));

            items.InstanceMaterialized += HandleInstanceMaterialized;
            items.PresentationAttached += HandlePresentationAttached;
            items.InstanceRemoved += HandleInstanceRemoved;
            initialized = true;

            foreach (WorldItemInstance instance in items.LoadedInstances)
            {
                Attach(instance);
            }
        }

        public void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            items.InstanceMaterialized -= HandleInstanceMaterialized;
            items.PresentationAttached -= HandlePresentationAttached;
            items.InstanceRemoved -= HandleInstanceRemoved;
            foreach (RuntimeBinding binding in bindings.Values)
            {
                Release(binding);
            }

            bindings.Clear();
            items = null;
            profiles = null;
            grid = null;
            initialized = false;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void HandleInstanceMaterialized(WorldItemInstance instance)
        {
            Attach(instance);
        }

        private void HandlePresentationAttached(
            WorldItemInstance instance,
            GameObject _)
        {
            Attach(instance);
        }

        private void HandleInstanceRemoved(WorldItemInstance instance)
        {
            if (instance == null ||
                !bindings.Remove(instance.GetEntityId(), out RuntimeBinding binding))
            {
                return;
            }

            Release(binding);
        }

        private void Attach(WorldItemInstance instance)
        {
            if (!initialized || instance == null ||
                !string.Equals(
                    instance.DefinitionId,
                    FlashlightDefinitionId,
                    StringComparison.Ordinal) ||
                !instance.StableId.IsValid)
            {
                return;
            }

            EntityId instanceId = instance.GetEntityId();
            if (!bindings.TryGetValue(instanceId, out RuntimeBinding binding))
            {
                binding = CreateBinding(instance);
                bindings.Add(instanceId, binding);
            }
            else
            {
                RebindPresentation(binding);
            }

            RefreshState(binding);
        }

        private RuntimeBinding CreateBinding(WorldItemInstance instance)
        {
            string stableId = instance.StableId.Value;
            string switchId = "switch.item.flashlight." + stableId;
            var fixtureRoot = new GameObject("Flashlight Light (Runtime)");
            fixtureRoot.hideFlags = HideFlags.DontSave;
            fixtureRoot.SetActive(false);
            fixtureRoot.transform.SetParent(instance.transform, false);

            var beamObject = new GameObject("Flashlight Beam");
            beamObject.hideFlags = HideFlags.DontSave;
            beamObject.transform.SetParent(fixtureRoot.transform, false);
            Light beam = beamObject.AddComponent<Light>();

            GameLightFixture fixture =
                fixtureRoot.AddComponent<GameLightFixture>();
            var binding = new RuntimeBinding
            {
                Item = instance,
                FixtureRoot = fixtureRoot,
                Fixture = fixture,
                SwitchId = switchId,
            };
            binding.StatusHandler = _ => RefreshState(binding);
            instance.StatusChanged += binding.StatusHandler;

            PositionBeam(instance, beam.transform, out Renderer lens);
            fixture.InitializeRuntime(
                "item.light.flashlight." + stableId,
                profiles.GetRequiredProfile(
                    LightFixtureCategory.PlayerFlashlight),
                new[] { beam },
                lens != null ? new[] { lens } : Array.Empty<Renderer>(),
                "source.item.flashlight",
                "grid.item.flashlight." + stableId,
                switchId,
                "zone.item.portable",
                string.Empty,
                string.Empty,
                true);
            fixtureRoot.SetActive(true);
            return binding;
        }

        private static void PositionBeam(
            WorldItemInstance instance,
            Transform beam,
            out Renderer lens)
        {
            lens = FindEmitterLens(instance.PresentationRoot);
            // Donor GAME.unity: the real FlashLight Spot is a direct child of
            // the item at local rotation X +90, so Unity's +Z light axis maps
            // to the item's -Y axis. The old adapter used +Y from
            // prop_flashlight_cover (the battery cover), putting the beam on
            // the physically wrong end of the torch.
            Vector3 direction = -instance.transform.up;
            if (direction.sqrMagnitude < 0.5f)
            {
                direction = instance.transform.forward;
            }

            direction.Normalize();
            Vector3 origin = lens != null
                ? lens.bounds.center + direction * 0.012f
                : instance.transform.position + direction * 0.09f;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.92f
                ? instance.transform.forward
                : Vector3.up;
            beam.SetPositionAndRotation(
                origin,
                Quaternion.LookRotation(direction, up));
        }

        private static Renderer FindEmitterLens(GameObject presentationRoot)
        {
            if (presentationRoot == null)
            {
                return null;
            }

            MeshFilter[] filters = presentationRoot.GetComponentsInChildren<
                MeshFilter>(true);
            Renderer fallback = null;
            for (int index = 0; index < filters.Length; index++)
            {
                Mesh mesh = filters[index].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (mesh.name.IndexOf(
                        "headlight_glass",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return filters[index].GetComponent<Renderer>();
                }

                if (fallback == null && mesh.name.IndexOf(
                        "prop_flashlight",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    mesh.name.IndexOf(
                        "cover",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    fallback = filters[index].GetComponent<Renderer>();
                }
            }

            return fallback;
        }

        private static void RebindPresentation(RuntimeBinding binding)
        {
            if (binding?.Fixture == null || binding.Item == null)
            {
                return;
            }

            Light beam = binding.FixtureRoot.GetComponentInChildren<Light>(true);
            PositionBeam(binding.Item, beam.transform, out Renderer lens);
            binding.Fixture.InitializeRuntime(
                binding.Fixture.FixtureId,
                binding.Fixture.Profile,
                new[] { beam },
                lens != null ? new[] { lens } : Array.Empty<Renderer>(),
                binding.Fixture.PowerSourceId,
                binding.Fixture.CircuitId,
                binding.Fixture.SwitchId,
                binding.Fixture.ZoneId,
                binding.Fixture.BusinessId,
                binding.Fixture.VehicleChannelId,
                true,
                binding.Fixture.IntensityMultiplier);
        }

        private void RefreshState(RuntimeBinding binding)
        {
            if (binding?.Item == null || grid == null ||
                !grid.HasSwitch(binding.SwitchId))
            {
                return;
            }

            ItemInstanceState state = binding.Item.State;
            // The current Phase 1 item schema already owns charge and the on/off
            // bit. The provisional battery-installed flag is not yet connected
            // to the R20 insertion mechanic, so charge is the usable-power gate
            // until that feature lands.
            bool isOn = state != null && state.isEnabled &&
                        state.content > 0.0001f && !state.isBroken &&
                        !state.isConsumed;
            if (!grid.TryGetSwitchState(binding.SwitchId, out bool current) ||
                current != isOn)
            {
                grid.SetSwitchState(binding.SwitchId, isOn);
            }
        }

        private static void Release(RuntimeBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            if (binding.Item != null && binding.StatusHandler != null)
            {
                binding.Item.StatusChanged -= binding.StatusHandler;
            }

            if (binding.FixtureRoot != null)
            {
                binding.FixtureRoot.SetActive(false);
                Destroy(binding.FixtureRoot);
            }
        }
    }
}
