# Satsuma: camshaft timing adjustment and auxiliary toolbox tools

Date: 2026-09-05. Scope: engine assembly interaction only. Classification:
`BehavioralReference` / `ConfigurationTransferred` / `Reimplemented` with existing
`TemporaryDirectImport` presentation. Source inspection was read-only. No donor
scripts, generated FSMs or old Unity assemblies were added to runtime.

## Evidence and exact differences

Frozen 04A1 `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`:

- Camshaft gear: GameObject 28946, transform 64992. Its mesh is GO 33487,
  transform 69544, source mesh GUID `f846a4e5b79ba9c4ea14694fae7bb75b`.
  Mesh rest rotation is identity; its local X is the adjustment axis.
- Gear fastener marker: GO 1187, transform 37245, `Screw` FSM 104426.
  The existing imported fastener is correctly a 10 mm, eight-stage fastening
  contract. Ordinary tightening advances stages 0..8. A further positive turn
  increments the temporary Stage to 9, branches to `Rotate`, sends `ADJUST` to
  the gear `BoltCheck`, and clamps Stage back to 8. This branch does not add
  aggregate Tightness. Negative turns loosen the bolt; they do not adjust
  timing backwards.
- Gear `BoltCheck` FSM 112367 has Bolted ON=5 and OFF=0. Before this packet the
  project mount used ON=1/OFF=0, and the generic interaction rejected positive
  turns at maximum stage, so timing adjustment was impossible.
- `BoltCheck.Check chain` reads GO 23 (`Timingchain`) Data.Installed. True
  returns without adjustment; false enters `Adjust`. The adjustment is a
  one-shot +5-degree Rotate in Space.Self on mesh GO 33487. It then reads the
  mesh localEulerAngles.x, rounds to nearest integer, converts back to float,
  and writes GO 8700 (`CamshaftGear`) Data.Angle. There is no frame-rate-scaled
  rotation and no extra fastening stage or Tightness.
- Database Data FSM 106546 saves and loads Angle independently of the physical
  world transform. `Basic state` sends a loose gear through `Rand angle`, using
  RandomInt min=0, max=71, inclusiveMax=false, then multiply by 5: 0..350 degrees
  in five-degree increments. Installed loads use saved Angle. The missing-angle
  compatibility branch also samples this range. This was verified against the
  isolated donor RandomInt and IntOperator actions, not inferred from labels.
- The local-Euler X readout is not necessarily an unwrapped 0..360 rotation.
  The original keeps rotating its live quaternion but saves only the rounded
  X readout. The new implementation preserves that distinction; it does not
  reset live rotation from the readout on each click.

Related chain installation blocker, sent to the engine-mount work packet:
Assembly FSM 108011 `Already installed` reads GO 22044 (`Timingcover`)
Data.Installed. BoolTest true=LOOP (back to collision checking), false=ASSEMBLE.
The cover therefore blocks chain installation; it is not a positive prerequisite.

Toolbox evidence is in `M04A1_WorldEntities.csv`: rows 2041 (ruler mesh), 2854
(screwdriver mesh), and 7922 (spark-plug wrench mesh), plus their source parents.
The existing generated toolbox already contains all three meshes. The old
`SpannerSetPresentationController` authored selection targets only for eleven
spanners; auxiliary renderers were visibility-only. This was a missing binding,
not a missing model. The original names are used only by Editor provenance
mapping; runtime uses explicit project-owned typed bindings.

## Implemented source changes

### Camshaft timing

- `AssemblyCamshaftTimingState` owns the logical setting, live adjustment
  quaternion, explicit mesh binding and revision. The mesh gets a local-X
  delta post-multiplied after its imported rest basis. The part, assembly root,
  colliders, chain and bolt do not rotate as a side effect.
- `IAssemblyFastenerPostTighteningAction` is optional and explicitly bound.
  `AssemblyFastenerInteractionTarget` invokes it only on a matching held tool's
  positive turn at maximum stage. Unbound fasteners keep their previous
  behaviour. Normal tightening, negative loosening and work/regrip input gating
  remain in their existing owner. The gear action requires installed gear,
  exact bound mount/fastener, stage 8, Bolted latch, absent chain and clear
  obstruction checks. Missing chain metadata fails closed.
- `AssemblyCamshaftTimingSaveDto` is an independently versioned optional
  per-part extension (schema 1). Root integration adds the same presence-bit,
  capture, restore, clone and pre-validation pattern as steering alignment.
  Invalid/nonfinite angles or an unsupported extension version are rejected.
  Old installed native saves with no extension retain angle zero. Loose loads
  follow the donor random branch, including when a saved value exists. Ordinary
  remove/reinstall and camera/world rotation do not randomize it.
- `Phase1SatsumaCamshaftTimingAuthoring` finds exactly the existing gear part,
  exact source-mesh GUID, and exact 10 mm fastener target. It binds the state and
  changes only this mount's ON threshold 1→5, preserving OFF=0, maximum=8,
  stable IDs and other retention settings. Full-builder integration calls
  `ApplyToInstance(assembly)` after fastener authoring and before prefab save.

Scoped refresh:
`MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaCamshaftTimingAuthoring.RefreshCamshaftTimingBatch`

### Toolbox

- Explicit serialized auxiliary kinds expose Screwdriver, SparkPlugWrench and
  Ruler through the existing non-physical tool-selection/docking contract.
  They have project-owned unsized variant `"0"`, not a fabricated wrench size.
  Opening/closing the box does not hide or reactivate a selected tool.
- Importer and validator require one of each reviewed auxiliary type; old
  generated prefabs retain their eleven wrench behaviour until scoped refresh.
- `SatsumaAuxiliaryAssemblyTools` creates unsized tool definitions and exact
  compatibility rules for reviewed screw and spark-plug consumers. It does not
  make either tool a universal wrench, and is not by itself a consumer binding.
- Ruler selection explicitly says measuring is unavailable. Measuring, exact
  auxiliary viewmodel/work-pose parity and downstream consumers are not claimed.

Scoped refresh, changes only the existing toolbox prefab's typed bindings:
`MSC.Items.Editor.ItemLegacyPresentationPipeline.RefreshSpannerSetAuxiliaryToolBindingsOnly`

## Validation handoff

`git diff --check` executed successfully on the modified owned sources.
This agent did not launch Unity or modify generated payloads. Root owns the
single Unity refresh/test pass and records executed results separately.

Prepared tests:

- Three synthetic auxiliary tool identity/selection/docking cases and updated
  generated toolbox regression expecting fourteen selectable tools.
- One special-tool compatibility test (tools cannot substitute for each other
  or ordinary wrenches).
- Seventeen camshaft cases covering stage gates, post-maximum input, chain
  blocking, negative loosening, wrong tool, unchanged stage/Tightness/root pose,
  discrete loose-load initialization, ordinary install/world-pose independence,
  live-quaternion/Euler-readout behaviour, valid/invalid extension data, native
  assembly capture/restore and generated explicit bindings/thresholds.

Both scoped refreshes must run before generated-asset assertions. No manual
acceptance or engine start/run verification is claimed by source tests.

## Remaining engine integration

Spark-plug objects already exist in the item catalog: `item.sparkplug-box`
dispenses four `item.spark-plug` children. The child is currently a plain
SpawnedUnit with no scalar/flag assembly state, and the inspected Satsuma
generated assembly definitions contain no spark-plug records. The installation
bridge/consumer is separate work; merely selecting the new spark-plug wrench
cannot install or tighten a plug. The generic Satsuma tool builder also only
generates nonzero-size Wrench definitions until explicitly extended.

Timing data is now a real saved setting available for the engine simulation;
this packet does not invent engine damage, startup or power effects. Those
consumers require their own donor-evidenced integration.
