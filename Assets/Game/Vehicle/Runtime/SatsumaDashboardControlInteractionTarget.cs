using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaDashboardControlInteractionTarget : MonoBehaviour,
        IContinuousContextInteractionTarget, IInteractionDisplayTarget,
        IInteractionOutlineRendererSource, IParentColliderOcclusionBypass
    {
        [SerializeField] private SatsumaDashboardControlsController controller;
        [SerializeField] private SatsumaDashboardControlKind kind;
        [SerializeField] private PartInstance owner;
        [SerializeField] private Renderer knobRenderer;
        private bool held;
        private Transform interactor;
        public SatsumaDashboardControlsController Controller => controller;
        public SatsumaDashboardControlKind Kind => kind;
        public bool UsesDirectionalHold => true;
        public string InteractionDisplayName => kind switch
        { SatsumaDashboardControlKind.Choke => "Подсос", SatsumaDashboardControlKind.Hazards => "Аварийная сигнализация", _ => "Свет" };
        public string InteractionPrompt => kind switch
        {
            SatsumaDashboardControlKind.Choke => "Удерживать ЛКМ — вытянуть; ПКМ — утопить",
            SatsumaDashboardControlKind.Lights when controller != null => controller.HeadlightsMode switch
            {
                SatsumaHeadlightsMode.Off => "Выключено · ЛКМ — габариты",
                SatsumaHeadlightsMode.Parking => "Габариты · ЛКМ — фары",
                _ => "Фары · ЛКМ — выключить",
            },
            _ => "ЛКМ — переключить",
        };
        public void Configure(SatsumaDashboardControlsController controls, SatsumaDashboardControlKind control,
            PartInstance part, Renderer renderer)
        {
            if (controls == null || part == null || renderer == null || !Enum.IsDefined(typeof(SatsumaDashboardControlKind), control))
                throw new ArgumentException("Explicit control, installed owner and knob renderer are required.");
            controller = controls; kind = control; owner = part; knobRenderer = renderer;
        }
        public bool CanBeginContinuousInteraction(in InteractionContext context, ContinuousContextInteractionDirection direction) =>
            isActiveAndEnabled && controller != null && controller.CanOperate && owner != null && owner.IsInstalled &&
            (direction == ContinuousContextInteractionDirection.Primary ||
                kind == SatsumaDashboardControlKind.Choke && direction == ContinuousContextInteractionDirection.Secondary);
        public void BeginContinuousInteraction(in InteractionContext context, ContinuousContextInteractionDirection direction)
        {
            if (held || !CanBeginContinuousInteraction(context, direction)) return;
            held = true; interactor = context.Interactor != null ? context.Interactor.transform : null;
            if (kind == SatsumaDashboardControlKind.Choke)
                controller.TrySetChokeHeldDirection(direction == ContinuousContextInteractionDirection.Primary ? 1 : -1);
            else if (kind == SatsumaDashboardControlKind.Hazards) controller.TryToggleHazards();
            else controller.TryCycleLights();
        }
        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!held) return false;
            if (!isActiveAndEnabled || controller == null || !controller.CanOperate || owner == null || !owner.IsInstalled ||
                interactor != null && Vector3.Distance(interactor.position, transform.position) > 2f)
            { EndContinuousInteraction(); return false; }
            return true;
        }
        public void EndContinuousInteraction()
        {
            held = false; interactor = null;
            if (kind == SatsumaDashboardControlKind.Choke) controller?.TrySetChokeHeldDirection(0);
        }
        public Renderer ResolveOutlineRenderer() => controller != null && controller.CanOperate ? knobRenderer : null;
        public bool CanBypassParentCollider(InteractionTargetHost parentHost) => controller != null && controller.CanOperate &&
            owner != null && parentHost != null && parentHost.transform == owner.transform;
        private void OnDisable() => EndContinuousInteraction();
    }
}
