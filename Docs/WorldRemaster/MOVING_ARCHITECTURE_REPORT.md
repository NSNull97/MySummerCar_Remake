# Moving Architecture Report — 05A pilot

## Result

The pilot keeps moving elements separate from static shells and implements six interactable hinges: two garage doors, one garage side door, one house door and two gate leaves. `WorldHingedArchitecture` implements the existing `IContextInteractionTarget` capability without name-based dispatch.

Each generated element has a deliberate hinge-side pivot, Y-axis swing range, closed/open target angle, collider and interaction target. Stable persistence IDs are scene-owned where required; donor IDs remain provenance only.

## Validation status

Automated checks verify component/collider presence and garage fit. Manual swing-arc, obstruction, handle placement, save/load state and sound-hook review remain pending. The single direct `Door` binding is only `FirstPass`; other source doors/windows/gates remain in the backlog.
