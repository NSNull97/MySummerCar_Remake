# Stock engine manual controls — frozen donor audit and bounded repair

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Date: 2026-09-05. Classification: `BehavioralReference`, `ConfigurationTransferred`,
`Reimplemented`; presentation remains `TemporaryDirectImport`.

## Reference and scope

Read-only canonical source: donor staging
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
Frozen SHA-256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Specific serialized actions, enabled flags, parameter bytes, object references,
and hierarchy/pivot records were inspected; no PlayMaker runtime is transferred.

The task concerns existing assembly parts. It does not implement the separately
proposed purchased-item/assembly consumable bridge or overhaul engine simulation.

## Different controls, not one generic wheel-rotation system

| Part/control | Frozen evidence | Input and gates | Value/step | Persistence |
| --- | --- | --- | --- | --- |
| Alternator position | HandRotate106146 on pivot43308; clamp Screw112463; BoltCheck113993 | Bare hand, mouse wheel. Clamp stages0..7 enable rotation, stage8 disables it. HandRotate is also enabled on the loose donor part. | Pivot localX, 0..8 degrees, +/-0.5; positive scroll decreases angle. 0.1s real-time wait. | BoltCheck saves `Rotation`, missing-save default2. Raw mesh pivot is authored at7 degrees. |
| Distributor timing | HandRotate110808; BoltCheck110809; Data109294 on18381 | Installed only; bare hand, wheel, clamp tightness<8. Earlier BoltedYES2 does not lock adjustment. | Pivot51182 localZ, 0..20 degrees, +/-0.2; positive decreases. 0.01s wait. | Data `SparkAngle`, default15, maximum20, explicit Save/Load. |
| Existing oilfilter0 | Screw106430, Use106429, Removal106431; Assembly110346 | Installed, bare hand, wheel. Stage0 permits removal; any positive tightness blocks it. | Stages0..8, +/-1. Positive tightens. Each stage sets localZ=-stage*0.0025m. The inspected Screw has **no bank Rotate/SetRotation action**. | Use saves Tightness, Dirt, Installed, Consumed and transform. New packet persists tightness only; existing item-state integration remains separate. |
| Stock carb mixture screw | Screw108773 on marker52523; Data110735 on23454 | Installed carb, screwdriver. ToolPickup110228 selects ToolWrenchSize0.65; player Check105041 matches the marker scale0.65. It is not a6mm mounting fastener. | Positive TIGHTEN rotates the screw-3 degrees and changes IdleAdjust-0.2; negative does the inverse. Clamp10..22. Visual screw turns even at the logical limit. | Data `IdleAdjust`, default15. It feeds donor Mixture.AirRatio: mixture setting, not an invented idle-RPM knob. Screw visual turns are not separately saved. |
| Stock carb throttle | Misleadingly named Screw108301 on50748 | Hold LMB. Loose carb: mechanical linkage only. Installed carb: mechanical linkage plus actual full-throttle intent; button release or installed-player distance>2m releases it. | Linkage pivot60542 localX0/40 degrees; closed/open butterfly meshes alternate. No wheel-input adjustment. | Transient held input, not persistent. |
| Flywheel / clutch cover | Flywheel30646: BoltCheck112895, Removal112894, child hierarchy; clutch cover17812: BoltCheck109149, Removal109148, child hierarchy | No manual HandRotate or equivalent adjustment control found in the complete inspected own hierarchy/FSM set. Child bolt rotation is normal fastening. | Do **not** give these parts the alternator/distributor behaviour without new evidence. This finding does not mean a running engine cannot animate them. | Existing assembly/fastener state. |

All values refer to the frozen selected game, not memory of another version or a mod.

## Exact imported pivot bindings

The sanitized importer flattens mechanical presentation. The new wrapper restores
measured part-local rotation with explicit mesh references; it does not rotate
physical part roots, move mounts, or reparent imported nested-prefab children.

- Alternator pivot: `(-0.0030999184,0.029600155,-0.062200014)`, localX,
  authored reference7 degrees. Moving alternator mesh source
  `5b55f637bcd4e5d4db3d8326a5c733e4` and pulley
  `f8771cd5897dcc94abbc279a97615b05`; static bracket remains fixed. The exact
  clamp marker follows the same presentation pivot. Hand sphere radius0.055m,
  pivot-local center `(0.05,-0.019,0.063)`.
- Distributor pivot: part origin, localZ. Mesh
  `ed8c93264302de144b73f50955ac0872` has the original child-rest rotationZ-10
  degrees; the binding retains that basis. Hand sphere radius0.04m,
  center `(0,0,0.05)`.
- Oil filter bank mesh `5833635d309ec3248b5c7ba65bddad85` moves axially0..-0.020m.
  The interaction box preserves donor size `(0.06,0.06,0.122568026)`.
- Mixture uses the existing exact source52523 marker, part-local position
  `(-0.004,-0.0082,0.0081)` with measured marker axis (about localX-49.95 degrees).
  Only its explicitly bound screw presentation rotates.
- Throttle pivot: `(-0.05525875,-0.021597866,0.012377917)`, localX. Linkage source
  `47923c19a9934cf4096251afb7c03d86`; closed butterfly
  `75b83bb00e0a6664b9d079523a9a5392`, open butterfly
  `2de4325ee6ab3ef429a10e8554147537`. Trigger radius0.03m,
  center `(-0.06,-0.02,0.012)`.

Defaults are initialized logically rather than dependent on camera/world rotation.
The remade wrapper applies persisted/default distributor and alternator settings
to loose presentation immediately; this avoids donor FSM enable/load ordering
determining the first visible pose. The raw alternator pivot7 and persisted
default2 are deliberately not confused.

## Root causes and changes

1. Imported geometry did not imply imported gameplay. No explicit project-owned
   incremental targets or tuning states existed for these meshes.
2. The starting oil filter mount had `fasteners: []` and no separate hand-tightness
   state. Generic assembly therefore treated it as freely removable.
3. Carb source52523 was wrongly authored as
   `fastener.satsuma.cylinder-head-carburetor.boltpm-3`, a fifth6mm Wrench
   fastener with eight stages and removal authority. The other four are actual
   8mm mounts with stable suffixes1,2,4,5.
4. Carb BoltCheck104785 explicitly clamps aggregate0..32 and uses ON8/OFF0.
   Generated fallback was MAX40/ON1/OFF0. The bounded authoring retires only
   the false fifth fastener, preserves the four real definitions/IDs, and sets
   MAX32/ON8/OFF0. The old definition asset remains; only its obsolete target
   component is removed from the marker, whose host becomes the tuning target.
   Save migration must discard only the allowlisted obsolete saved fastener and
   recompute aggregate/latch without losing real bolt stages.

New source files:

- `Assets/Game/Vehicle/Assembly/Runtime/SatsumaEngineAdjustmentRules.cs` — pure
  typed values, directions, limits and steps.
- `AssemblyEngineAdjustmentState.cs` — one optional saved state per explicit
  part, availability gates, render-only pivot bindings and filter removal flag.
- `AssemblyEngineAdjustmentTarget.cs` — existing directional hand/tool
  capabilities; typed screwdriver only for mixture, no F activation.
- `AssemblyCarburetorThrottleTarget.cs` — existing continuous LMB capability,
  mechanical pose plus `RequestedThrottle01`; release on lifecycle loss,
  disable or installed-distance loss. No synthesized RPM or ignition.
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineAdjustmentsAuthoring.cs`
  — explicit source-mesh authoring, exact false-fastener retirement and existing
  `SatsumaIgnitionInputAdapter.ConfigureCarburetorThrottle` binding.
- `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineAdjustmentTests.cs`.

## Core integration and compatibility

Coordinated separately by root/engine-fastener agent:

- Optional PartSaveDto `hasEngineAdjustment` + version1 `engineAdjustment`.
  Payload fields: `schemaVersion`, typed `kind`, finite bounded `value`.
  Validate type and bound part before mutation, clone explicitly, restore after
  part lifecycle/graph. Missing old fields use2/15/15/0 defaults; no schema reset.
- `EvaluateRemoval` checks the filter's `BlocksRemoval`; structural forced
  detachment remains possible and clears filter tightening. Same-frame capture
  refreshes this state, so a loose filter cannot save stale positive tightness.
- Existing `SatsumaIgnitionInputAdapter.ConsumeFixedInput` combines router
  throttle with `RequestedThrottle01` using max. Existing
  `VehicleSimulationHost.FixedUpdate -> VehicleSimulationRoot.Tick` remains the
  actual consumer; engine prerequisites and ignition are untouched.
- Full builder calls `ApplyToInstance` after generic fastener and ignition
  authoring. Scoped refresh: `RefreshEngineAdjustmentsBatch`, menu
  `Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Adjustments Only`.

No stable part/mount IDs, accepted input presentation, physical roots, suspension,
or player controller are replaced. Source controls contain no donor scene-name
or object-ID lookup at runtime.

## Validation and remaining differences

Authored EditMode cases cover directions, bounds, exact clamp stage8, loose-part
differences, typed screwdriver versus hand/F, filter eight stages and no fabricated
bank spin, forced detach, optional versioned save/defaults, invalid DTO rejection,
physical-root stability, throttle hold/real input/release, actual removal/save
integration, and generated bindings with four carb mounting bolts.

This agent did not launch Unity or execute these tests. `git diff --check` was
run for the scoped packet; root must execute compilation, scoped refresh, and the
tests in the shared single-Editor validation pass, then record results. Manual
in-game aim/accessibility and visual-axis validation remain required.

Explicit remaining engine-simulation work: the existing simulation has no donor
`SparkAngle`, `IdleAdjust/AirRatio`, alternator belt-position or filter
leak/pressure consumer. Settings now have real typed, persistent logical values,
and carb throttle feeds real input, but this packet does not claim complete
combustion/belt/oil effect parity. Do not mark that broader feature Verified.
The starting filter works in assembly; purchased filter items remain outside this
packet. The oil bank uses safe presentation-only axial movement while keeping
the accepted part physics root fixed. Existing pickup/outline policies and tool
feedback remain the accepted architecture, not copied donor UI.

Next bounded milestone: connect these proven persisted adjustments to the real
engine/belt/oil simulation when those consumers are implemented, with original
running-engine comparisons rather than artificial RPM feedback.

## Manual regression: throttle highlighted but ignored mouse hold

The subsequent manual check found a real input integration defect that direct
component tests did not cover. `AssemblyCarburetorThrottleTarget` returned
`UsesDirectionalHold=false`, while the existing player controller requires this
flag in `TryBeginContinuousContextInteraction` before dispatching mouse-down.
Hover availability still used `CanBeginContinuousInteraction`, so the lever
could highlight without receiving its press. World-action HUD hints use the
same opt-in flag and were also missing.

The bounded correction changes only this target's flag to true. It does not
add RMB operation: `CanBeginContinuousInteraction` still accepts Primary only,
as frozen Screw108301 does (`GetMouseButtonDown/Up` button0, local-X 0/40).
Existing authored pivot, flattened linkage mesh/rest pose, closed/open butterfly
bindings and real throttle-input adapter were checked and remain unchanged.
No player/controller, mixture setting, save schema or generated asset migration
is required.

New dedicated `SatsumaCarburetorThrottleInteractionPlayModeTests` drives actual
Input System mouse input through `PlayerInputRouter` and the ray-selected player
interaction target. It covers installed/loose press-hold-release, HUD action,
40-degree part-space movement, butterfly swap, real throttle intent without
starter fabrication, RMB/F/scroll rejection, tool-only ray exclusion, distance
release and same-frame lifecycle release. This regression test packet awaits
the coordinated Unity run; the earlier 799/74 results do not verify this fix.
