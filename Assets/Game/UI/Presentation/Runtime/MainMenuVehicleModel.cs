using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.UI.Presentation
{
    [Serializable]
    public sealed class MainMenuVehiclePaintBinding
    {
        [SerializeField] private string surfaceId;
        [SerializeField] private Renderer renderer;
        [SerializeField] private int[] materialSlots = Array.Empty<int>();

        public MainMenuVehiclePaintBinding(string id, Renderer target, int[] slots)
        {
            surfaceId = id;
            renderer = target;
            materialSlots = slots ?? Array.Empty<int>();
        }

        public string SurfaceId => surfaceId;
        public Renderer Renderer => renderer;
        public int[] MaterialSlots => materialSlots;
    }

    [Serializable]
    public sealed class MainMenuVehicleLampBinding
    {
        [SerializeField] private string lampId;
        [SerializeField] private Renderer lensRenderer;
        [SerializeField] private int materialSlot;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localDirection;
        [SerializeField] private bool headlight;

        public MainMenuVehicleLampBinding(string id, Renderer lens, int slot,
            Vector3 position, Vector3 direction, bool isHeadlight)
        {
            lampId = id;
            lensRenderer = lens;
            materialSlot = slot;
            localPosition = position;
            localDirection = direction.normalized;
            headlight = isHeadlight;
        }

        public string LampId => lampId;
        public Renderer LensRenderer => lensRenderer;
        public int MaterialSlot => materialSlot;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalDirection => localDirection;
        public bool IsHeadlight => headlight;
    }

    /// <summary>A mesh-only showroom copy. Owns no vehicle state or gameplay authority.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuVehicleModel : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private MainMenuVehiclePaintBinding[] paintBindings =
            Array.Empty<MainMenuVehiclePaintBinding>();
        [SerializeField] private Bounds localBounds;
        [SerializeField] private MainMenuVehicleLampBinding[] lampBindings =
            Array.Empty<MainMenuVehicleLampBinding>();
        private MaterialPropertyBlock paintBlock;
        private Color lastPaint;
        private bool paintApplied;

        public int RendererCount => renderers.Length;
        public int PaintSurfaceCount => paintBindings.Length;
        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<MainMenuVehiclePaintBinding> PaintBindings => paintBindings;
        public IReadOnlyList<MainMenuVehicleLampBinding> LampBindings => lampBindings;

        public void ConfigureLampsForAuthoring(MainMenuVehicleLampBinding[] bindings)
        {
            if (bindings == null || bindings.Length != 4)
                throw new ArgumentException("The menu car needs two headlights and two rear lamps.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int headlights = 0;
            foreach (MainMenuVehicleLampBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.LampId) || !ids.Add(binding.LampId) ||
                    binding.LensRenderer == null || !binding.LensRenderer.transform.IsChildOf(transform) ||
                    binding.MaterialSlot < 0 || binding.MaterialSlot >= binding.LensRenderer.sharedMaterials.Length ||
                    binding.LocalDirection.sqrMagnitude < 0.9f)
                    throw new ArgumentException("Menu lamps need unique IDs and explicit model lens bindings.");
                if (binding.IsHeadlight) headlights++;
            }
            if (headlights != 2) throw new ArgumentException("Exactly two menu lamps must be headlights.");
            lampBindings = bindings;
        }

        public void ApplyPaint(Color colour)
        {
            if (paintApplied && lastPaint == colour) return;
            paintBlock ??= new MaterialPropertyBlock();
            for (int index = 0; index < paintBindings.Length; index++)
            {
                MainMenuVehiclePaintBinding binding = paintBindings[index];
                if (binding?.Renderer == null) continue;
                int[] slots = binding.MaterialSlots;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    binding.Renderer.GetPropertyBlock(paintBlock, slots[slot]);
                    paintBlock.SetColor(BaseColorId, colour);
                    paintBlock.SetColor(ColorId, colour);
                    binding.Renderer.SetPropertyBlock(paintBlock, slots[slot]);
                    paintBlock.Clear();
                }
            }
            lastPaint = colour;
            paintApplied = true;
        }

        public void ConfigureForAuthoring(Renderer[] modelRenderers,
            MainMenuVehiclePaintBinding[] bindings)
        {
            renderers = modelRenderers ?? throw new ArgumentNullException(nameof(modelRenderers));
            paintBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            if (renderers.Length == 0) throw new ArgumentException("The menu model has no renderers.");
            localBounds = new Bounds();
            bool initialized = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.transform.IsChildOf(transform))
                    throw new ArgumentException("Menu renderer bindings must belong to the model.");
                Bounds bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = transform.InverseTransformPoint(
                        bounds.center + Vector3.Scale(bounds.extents, sign));
                    if (initialized) localBounds.Encapsulate(point);
                    else { localBounds = new Bounds(point, Vector3.zero); initialized = true; }
                }
            }
            for (int index = 0; index < paintBindings.Length; index++)
            {
                MainMenuVehiclePaintBinding binding = paintBindings[index];
                if (binding == null || string.IsNullOrEmpty(binding.SurfaceId) ||
                    binding.Renderer == null || !binding.Renderer.transform.IsChildOf(transform) ||
                    binding.MaterialSlots.Length == 0)
                    throw new ArgumentException("Menu paint bindings must reference model material slots.");
                int count = binding.Renderer.sharedMaterials.Length;
                foreach (int slot in binding.MaterialSlots)
                    if (slot < 0 || slot >= count)
                        throw new ArgumentException("Menu paint material slot is out of range.");
            }
            paintApplied = false;
        }
    }
}
