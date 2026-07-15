using System;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyMountPreviewBinding
    {
        [SerializeField]
        private MountPointAuthoring mountPoint;

        [SerializeField]
        private Renderer previewRenderer;

        public MountPointAuthoring MountPoint => mountPoint;

        public Renderer PreviewRenderer => previewRenderer;

        public static AssemblyMountPreviewBinding Create(MountPointAuthoring mount, Renderer renderer)
        {
            return new AssemblyMountPreviewBinding
            {
                mountPoint = mount,
                previewRenderer = renderer
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class AssemblyMountPreviewPresenter : MonoBehaviour
    {
        [SerializeField]
        private VehicleAssemblyController controller;

        [SerializeField]
        private PhysicalCarryController carryController;

        [SerializeField]
        private Material validMaterial;

        [SerializeField]
        private Material invalidMaterial;

        [SerializeField]
        private AssemblyMountPreviewBinding[] bindings = Array.Empty<AssemblyMountPreviewBinding>();

        private PartInstance lastPart;
        private string lastMessage = string.Empty;

        public string LastPreviewMessage => lastMessage;

        public void Configure(
            VehicleAssemblyController assemblyController,
            PhysicalCarryController carry,
            Material valid,
            Material invalid,
            AssemblyMountPreviewBinding[] previewBindings)
        {
            controller = assemblyController;
            carryController = carry;
            validMaterial = valid;
            invalidMaterial = invalid;
            bindings = previewBindings ?? Array.Empty<AssemblyMountPreviewBinding>();
            SetAllVisible(false);
        }

        private void Update()
        {
            if (controller == null || carryController == null || !carryController.HasHeldObject)
            {
                lastPart = null;
                lastMessage = string.Empty;
                SetAllVisible(false);
                return;
            }

            PartInstance part = controller.ResolvePart(carryController.HeldTarget);
            if (part == null)
            {
                lastPart = null;
                lastMessage = string.Empty;
                SetAllVisible(false);
                return;
            }

            lastPart = part;
            AssemblyMountCandidate best = controller.FindBestMount(part, includeInvalid: true);
            lastMessage = best.Mount != null ? best.Result.Message : "Совместимая точка не найдена";
            for (int i = 0; i < bindings.Length; i++)
            {
                AssemblyMountPreviewBinding binding = bindings[i];
                Renderer renderer = binding?.PreviewRenderer;
                if (renderer == null)
                {
                    continue;
                }

                bool selected = best.Mount != null && best.Mount.Authoring == binding.MountPoint;
                renderer.enabled = selected;
                if (selected)
                {
                    renderer.sharedMaterial = best.Result.Succeeded ? validMaterial : invalidMaterial;
                }
            }
        }

        private void OnDisable()
        {
            lastPart = null;
            lastMessage = string.Empty;
            SetAllVisible(false);
        }

        private void SetAllVisible(bool visible)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                Renderer renderer = bindings[i]?.PreviewRenderer;
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
