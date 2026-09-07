using System;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.ItemsIntegration
{
    /// <summary>Exposes Items condition and reuses the filter's existing hand-rotation presentation.</summary>
    public sealed class ItemPartPresentationBinding : MonoBehaviour, IAssemblyItemCondition, IAssemblyWearSink
    {
        private WorldItemInstance item;
        private GameObject presentationRoot;
        private AssemblyEngineAdjustmentTarget interaction;
        public GameObject PresentationRoot => presentationRoot;
        public float ConditionPercent => item != null ? item.ConditionPercent : 0f;
        public bool IsBroken => item == null || item.IsBroken;
        public bool TryApplyWear(float conditionLoss) => item != null &&
            (item.DefinitionId == "item.alternator-belt" || item.DefinitionId == "item.spark-plug" ||
             item.DefinitionId == "item.oil-filter") && item.TryApplyMechanicalWear(conditionLoss);

        public void BindCondition(WorldItemInstance configuredItem)
        {
            if (configuredItem == null || configuredItem.gameObject != gameObject)
                throw new ArgumentException("Condition must come from the same physical item wrapper.", nameof(configuredItem));
            // Keep the wrapper, not a DTO snapshot: ApplyState replaces its authoritative state.
            item = configuredItem;
        }

        public void Configure(PartInstance part, VehicleAssemblyController assembly, GameObject visual)
        {
            Renderer renderer = visual.GetComponentInChildren<Renderer>(true);
            if (renderer == null) throw new InvalidOperationException("A purchased filter needs a visible adjustment surface.");
            AssemblyEngineAdjustmentState state = part.GetComponent<AssemblyEngineAdjustmentState>();
            AssemblyEngineAdjustmentSaveDto previous = state != null ? state.CaptureSaveData() : null;
            if (state == null) state = part.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
            AssemblyEngineAdjustmentState template = assembly.Parts
                .Where(candidate => candidate != null && candidate.Definition == part.Definition)
                .Select(candidate => candidate.GetComponent<AssemblyEngineAdjustmentState>())
                .FirstOrDefault(candidate => candidate != null && candidate.Kind == SatsumaEngineAdjustmentKind.OilFilter);
            state.Configure(SatsumaEngineAdjustmentKind.OilFilter, part, assembly,
                template != null ? template.LocalPivot : Vector3.zero,
                template != null ? template.LocalAxis : Vector3.forward,
                template != null ? template.PresentationReferenceAngle : 0f,
                new[] { new AssemblyEngineAdjustmentPresentation(visual.transform,
                    part.transform.InverseTransformPoint(visual.transform.position),
                    Quaternion.Inverse(part.transform.rotation) * visual.transform.rotation) });
            if (interaction == null)
            {
                var control = new GameObject("Project-owned filter adjustment");
                control.layer = gameObject.layer;
                control.transform.SetParent(part.transform, false);
                BoxCollider marker = control.AddComponent<BoxCollider>();
                marker.isTrigger = true;
                interaction = control.AddComponent<AssemblyEngineAdjustmentTarget>();
                InteractionTargetHost host = control.AddComponent<InteractionTargetHost>();
                host.Configure(interaction);
                host.ConfigureSelectionPriority(40);
            }
            BoxCollider hitbox = interaction.GetComponent<BoxCollider>();
            Bounds bounds = renderer.bounds;
            hitbox.center = part.transform.InverseTransformPoint(bounds.center);
            Vector3 size = bounds.size;
            hitbox.size = new Vector3(Mathf.Max(size.x, 0.02f), Mathf.Max(size.y, 0.02f), Mathf.Max(size.z, 0.02f));
            interaction.Configure(state, renderer);
            interaction.GetComponent<InteractionTargetHost>().ConfigureOutlineRenderers(renderer);
            state.RestoreValidated(previous);
            interaction.RefreshAvailability();
            presentationRoot = visual;
        }
    }
}
