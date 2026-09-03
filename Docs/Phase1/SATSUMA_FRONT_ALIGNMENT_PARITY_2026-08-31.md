# Satsuma issue 3 — front steering connection and toe adjustment

Revision `11A-V1d.36`. Scope: issue 3 only. Front ground-contact acceptance
from V1d.33 is protected; rear suspension and wheel-seating issues 4–6 are not
being fixed here. Build and targeted automated validation passed. The later
free-yaw correction was user-accepted on2026-08-31; see the follow-up below.
The documented compatibility limits are not reclassified as exact parity.

## Authority and corrected specification

Read-only frozen authority `msc-world-baseline-04a1.1-c3f2f337`, not a mixture
of the current installed game and older exports. `GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The following is serialized-data/action-source evidence, not an executed
original-game capture. No donor scripts or FSMs are runtime dependencies.

| Mechanism | Original evidence | Required behavior |
|---|---|---|
| Rod Data | FL113199 / FR105610 | Loose-load branch randomizes Alignment in −6…+6 degrees; installed-load retains saved angle |
| Rod Assembly | FL111295 / FR110868 | Activates rod, joint bolt and separate adjuster; no carry/camera rotation transfer or new random angle |
| 12 mm joint | Screw113544 /113958; BoltCheck111004 /104921 | Stage0…8; latch ON at8, OFF at0; live8→7 stays ON |
| Steering connection | FSM107556 /107558 | Rod Bolted AND stock/rally strut Installed; snap to Alignment on connection; otherwise free Y hinge, limits±33 degrees |
| 14 mm adjustment | FSM112113 /105829 | Tighten−0.1°, loosen+0.1°, clamp±6; apply one-shot local-Y yaw even while free; reject ratchet |
| Input cadence | Player RaycastCheck105041 | One action by scroll sign per0.28 scaled seconds; delta magnitude does not multiply steps |
| Nut animation | Same adjustment FSMs | Rotate nut±13° per accepted action, even at adjustment limit; no Stage or axial tightening travel |
| Wishbone Bolted | FL113307 /FR114055 | Aggregate maximum16, ON≥2, OFF≤0 |
| Spindle Bolted | FL110196 /FR110399 | Maximum8, ON≥2, OFF≤0 |
| Installation | Spindle111700 /110678; strut107329 /108451 | Spindle requires wishbone Installed+Bolted; strut requires spindle Installed+Bolted |

Installation activates a prepositioned installed object and destroys the loose
donor object. The remake preserves its existing project-owned part identity and
snaps to the authored moving mount instead. Camera direction can choose a
target, but cannot become the installed part's final quaternion. A free parent
hub can already have rotated; a fixed local mount is not a fixed world angle.

The 14 mm nuts are actual `bolt2` meshes (GUID
`e711c8a15b1135c4089caad19b8f56e8`), not the 12 mm bolt previously confused
with them. Markers FL64267 /FR42271 are children of outer rod bones39613 /66023.
Their visual children70297 /68477 have localZ−0.00438223 and marker scale1.4.
V1d.35's corrected outer endpoints, all30 front bolt meshes and existing IDs
are retained. Two adjusters are not two extra staged graph fasteners.

### Camber is not Alignment

Satsuma Axles112091 (`GAME:6994364`) sets camber−1.4 and caster1.15;
`CarDynamics.Start`/`SetWheelsParams` transfers mirrored camber to the wheels.
Raw Wheel serialized camber0 is overwritten at startup. Suspension108170
does not reset camber when a strut is missing. V1d.33's inference of zero
no-strut camber is superseded. The new unstrung profile preserves front camber;
accepted spring travel, forces, damping and ground-contact ownership stay intact.

### Free-yaw ownership

Original `Wheel.cs:608–620` searches for its force body starting at its parent,
not its own transform. Adding a Rigidbody/HingeJoint on the Wheel root does
not redirect its calculated tire/contact forces away from the chassis.
Installed wheel Removal FSM109302 disables the example wheel's physical mesh
collider; the observed NoTireCollider93773 is serialized disabled. Thus a
separate collisionless yaw body is not an invented second tire-force solver.

The remake adapter must keep NWH attached to the original chassis, receive
steering commands through `NwhWheelPhysicsBackend`, and feed resolved yaw
before NWH's order100 step. It must not move NWH onto a second Rigidbody,
duplicate wheel contacts, invent ground torques, or overwrite the chassis pose.
`SatsumaFrontSteeringController` owns a collisionless Rigidbody/HingeJoint per
free corner, connected to a kinematic anchor. The anchor absorbs constraint
reactions instead of adding the little body's weight to the accepted chassis
simulation. This is an explicit compatibility adaptation, not identical
Unity5/Unity6 trajectories. Joint limits±33 are relative to the creation pose,
not an extra clamp of absolute chassis-local yaw. No centering spring, motor,
random yaw jitter or invented ground torque is used. Connection removes the
free body and uses Alignment plus the normal steering command. A14 mm action
repositions the free body's yaw once without removing its joint.

The original6-second initialization delay is replaced by ordinary configured
startup; dynamic caster1.15 is not implemented in this pass. Base camber−1.4
is preserved. Stock struts are currently authored; rally-strut content is not
silently invented. Exact frozen marker-scaled staged-bolt animation remains
the separate known gap documented in V1d.35.

## Save and compatibility contract

User clarification during implementation: repair of the overall save system
is deferred until the whole suspension is accepted. The small angle field
already added below is groundwork, not a claim that saves are fixed. The
donor latch-reset quirk is not implemented in this suspension pass. Immediate
acceptance concerns mechanics within a running game session.

`PartSaveDto.hasSteeringAlignment` plus `steeringAlignment` is an optional independently versioned extension
on the rod's existing stable identity. No existing part/mount/fastener IDs or
assembly schema versions are replaced. Invalid version, nonfinite/out-of-range
angle or an angle attached to a non-adjustable part must fail before mutation.
The presence bit is required because JsonUtility materializes null inline
objects during a JSON round trip. Relying on nullness caused the first-run
regression; the explicit bit fixed both the new and existing aggregate tests.

- Installed current record: restore saved Alignment.
- Installed old record without extension: neutral0, matching the old remake.
- Loose record, with or without extension: donor loose-load randomization.
- Ordinary remove/reinstall in one session: retain angle.
- V1d.34's old252→260 fastener migration must preserve this optional data.
- Existing native `isBolted` latch history is retained, including intermediate
  values after loosening. Old installed records are not rejected merely because
  new installation-only prerequisites would block assembling them from scratch.

The original BoltCheck starts from OFF on loading its bolt array; source order
suggests a saved8→7 latch loses ON after loading. This has not been captured
in the original runtime. Repeating that quirk is a separate user decision;
silently discarding the native saved latch is not part of the current patch;
the user explicitly deferred this save work.
The V1d.35 handoff's general “no random angle on reload” is corrected above:
it applies to installed rods, not the donor loose-load branch.

InstallRequiresBolted uses existing assembly dependencies. It is deliberately
not implemented using RequiredBoltedMountIds, which governs structural collapse.
No new retention cascade is introduced.

## Verification and handoff

Executed in Unity6000.3.11f1 after the interactive Editor was closed:

- Build exit0, `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.36`:117 mounts,
  260 staged fasteners,125 loose parts/120 active; two separate toe targets.
- Final EditMode **143/143**,0 skipped (`codex-front-alignment-v1d36-edit-r2.xml`).
  Includes32 new angle/target/source/gate cases plus the prior111 regressions.
- Isolated physics PlayMode **26/26**,0 skipped
  (`codex-front-alignment-v1d36-physics.xml`): prior20 front/rear contact and
  fastener cases plus3 steering and3 actual scaled-input cadence cases.
- Isolated production Bootstrap **1/1**,0 skipped
  (`codex-front-alignment-v1d36-bootstrap.xml`).

All artifacts and corresponding logs are in `Logs/`. First EditMode was135/143:
six failures exposed the new optional-field JSON problem; two fixtures still
expected36 dependencies/installation without bolted support. These were fixed,
not skipped. The four new install gates are asserted explicitly, and physical
force/contact tolerances were not relaxed. Airborne pose expectations now
account for live yaw, unchanged NWH rim-pivot displacement and base camber.
The previously intermittent rear-lift test passed again, but no rear fix or
manual acceptance is inferred from that result.

Commands used: Unity `-batchmode -nographics -quit -executeMethod
MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.Build`
for generation; `-batchmode -nographics -runTests -testPlatform EditMode` or
`PlayMode -testFilter ... -testResults ...` for the isolated groups. Test run
command lines with full fixture filters are retained in each log. No `-quit`
was supplied to test runs. Final total:170/170 targeted tests, not a full
project-suite pass or an original-game side-by-side capture.

Changed runtime: `AssemblySteeringAlignmentState` and
`AssemblySteeringAlignmentInteractionTarget` (new); `SatsumaFrontSteeringController`
(new); `NwhWheelPhysicsBackend`; optional camber argument in
`NwhAssemblyWheelSupportController`; optional angle DTO capture/validation/restore
in `VehicleAssemblySaveData`/`VehicleAssemblyController`; clarification comment
in `AssemblyGraph`. Existing public entry points and stable IDs are preserved.
Builder: `Phase1SatsumaBaselineBuilder`; ignored private prefab/assets rebuilt.

Tests: new `SatsumaFrontAlignmentEditModeTests`,
`SatsumaFrontSteeringAlignmentTargetTests` (two fixtures),
`SatsumaFrontSteeringPlayModeTests`, `SatsumaSteeringAdjustmentInputPlayModeTests`;
bounded updates to `Phase1SatsumaGeneratedContentTests` and
`SatsumaInstalledPartPhysicsPlayModeTests`. New C# files have `.meta` files.
This report, V1d.35 follow-up link, donor audit/matrix/system map/ledger and
parity rowsP1.CAR.003/P1.CAR.008 record the work. No original/vendor code changed.

Manual acceptance remains required for issue3: both sides, install from
different camera angles, joint0→7→8→7→0, missing/reinstalled strut,14 mm
adjustment before/after connection and at limits, wrong tool/ratchet rejection,
installed save/load and same-session removal/reinstallation. Recheck ground
support and the previously accepted one-strut no-launch scenario.

The generated assets are already rebuilt: open Bootstrap and start a fresh
Play session. If regeneration is needed later, use
`Tools/MSC Remake/Phase 1/Satsuma/Build V1d Baseline`.

Small next-session specification: stay on issue3, reproduce any remaining
hub/rod mismatch against frozen evidence and the stage sequence above; keep
the accepted vertical solver, existing IDs and the new tool tests. Do not
silently call this exact legacy physics: the anchoring/caster/stage-animation
differences are listed above. Save-system repair waits until the whole
suspension is accepted, per the user's explicit sequencing decision.

The subsequent free-yaw correction is now user-accepted; issue4 is next in the
user's sequence. Preserve the remaining documented compatibility limits.

## Runtime follow-up (QA v1d37) — rotated-spawn free-yaw frame

The user's pre-tightening near-reversal exposed a separate runtime joint-frame
bug that the identity-orientation fixtures above did not cover. See
[Free-yaw joint creation frame fix](SATSUMA_FRONT_FREE_YAW_FRAME_FIX_2026-08-31.md)
for the red reproduction, anchor Rigidbody/Transform mismatch, bounded
creation-time correction and fresh-session acceptance check. The original
strut skin/local frames remain valid. Post-fix177/177 targeted tests pass;
the user accepted the reported pre-tightening correction on2026-08-31
("принимается"). Generated metadata remains V1d.36. This bounded acceptance
does not declare the remaining caster/stage-animation/save gaps resolved.
