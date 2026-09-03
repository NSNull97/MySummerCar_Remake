# Satsuma issue 3 — free-yaw joint creation frame

Runtime follow-up to `11A-V1d.36` (QA artifact label `v1d37`). Follow-up to
[V1d.36 front alignment parity](SATSUMA_FRONT_ALIGNMENT_PARITY_2026-08-31.md).
Scope remains issue3 only. The accepted front ground-contact/one-strut support
behavior is protected; rear suspension, wheel seating and general save repair
are outside this change. The runtime correction passes all177 targeted
post-fix tests. The user accepted this bounded fix on2026-08-31
("принимается").

This is a runtime-code correction, not an import/configuration change. The
generated prefab retains its V1d.36 metadata and needs no regeneration.

## Reproduction and cause

After V1d.36, installing a spindle/strut could reveal an almost reversed hub
and a strut-side steering attachment facing away from the rod. Tightening the
12 mm rod joint restored the connected pose. This was a remake runtime bug,
not donor evidence that an unconnected hub should turn around by itself.

`SatsumaFrontSteeringController.RefreshCorner` writes the kinematic anchor's
`Rigidbody.position` and `Rigidbody.rotation`, then creates the free body and
its `HingeJoint` in the same call. Before the first simulation step, those
Rigidbody values were correct, but the anchor's Transform still held its
initial identity rotation. Joint creation therefore captured the wrong
connected reference frame. The first physics steps corrected toward that
incorrect constraint frame, turning the free carrier before the player had
even installed the visible spindle.

The red reproduction explicitly distinguishes this from a stale chassis pose:
the chassis Rigidbody, chassis Transform, wheel Transform and free-body pose
all agree; it is the anchor Rigidbody/Transform pair that disagrees.

Executed evidence: `Logs/codex-front-yaw-v1d37-red-r2.xml`,
`SatsumaFrontSteeringSpawnPlayModeTests`, 6 cases: **1 passed, 5 failed**,
0 skipped. Before physics at the exact production spawn pose:

- Chassis Rigidbody and Transform: quaternion `(0, 1, 0, 0)`.
- Anchor Rigidbody: `(0, 1, 0, 0)`; anchor Transform: `(0, 0, 0, 1)`.
- FL initial Alignment `1.73473°`; resolved carrier yaw initially `1.73473°`.
- After 15 pre-install physics steps, FL carrier yaw `−145.26520°`, a wrapped
  change of `−147°` relative to its initial Alignment. FR changes by the same
  `−147°`; the later correct Transform does not repair the captured frame.

The other spawn cases expose the same dependency on global orientation:

| Spawn rotation | Observed pre-install yaw change from Alignment | Red result |
|---|---:|---|
| Yaw0° | 0° | Passed |
| Yaw90° | +57° | Failed |
| Yaw165° | +132° | Failed |
| Yaw180° | −147° | Failed |
| Exact production metadata pose, yaw180° | −147° | Failed |
| Tilted Euler(15°,165°,12°) | About +132° | Failed |

The 57/132/147-degree offsets correspond to the erroneous global reference
offset and the 33-degree limit. They are not a newly sampled toe value or a
rotation transferred from the player's camera. V1d.36's prior isolated
identity-orientation fixtures could not expose this: at yaw0 the correct
anchor rotation is identity already. The earlier170/170 result remains an
executed result for that test scope, not evidence that rotated-spawn creation
was covered.

Connection explains the apparent recovery after tightening: rod Bolted plus
strut Installed releases the free body and resolves the connected carrier from
Alignment. The erroneous joint is no longer driving the pose. This does not
make the preceding free pose valid.

## Original reference and geometry audit

Authority remains the read-only frozen
`msc-world-baseline-04a1.1-c3f2f337`, `GAME.unity` SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
This follow-up uses serialized hierarchy/action evidence, not a new original
runtime capture. No donor file or vendor runtime was modified.

The original steering connection FSMs107556/107558 use a free local-Y hinge
with limits−33…+33 degrees when disconnected. Those are relative joint limits,
not permission to create the joint against a stale world-identity anchor. The
remake's collisionless carrier/kinematic anchor remains a documented adapter;
this correction does not claim identical Unity5/Unity6 trajectories.

The strut skin and mesh-axis transforms were rechecked and are not the fault:

- FL installed skin101770 (`GAME:1649944`) uses bones55934 and64686;
  FR101779 (`GAME:1650395`) uses42342 and44833. The upper bone is below the
  body-fixed strut/Armature/pivot chain; the lower bone is below
  wheel/Spindle/OFFSET. Thus only the lower endpoint inherits hub yaw.
- The upper Armature rotation is approximately−90° about X; pivot quaternion
  is `(0.5, −0.5, 0.5, 0.5)`. Builder upper/lower bone assignment and local
  frames match those source chains. The lower source positions include
  OFFSET+0.05m FL/−0.05m FR, as in the existing reviewed rig.
- Loose and installed struts use the same source mesh on each side:
  FL MeshFilter83176/Skin101770 share GUID
  `ce7e349da23bbd146bbb7a36da1c6235`; FR85690/101779 share
  `25732eb7f9bad1642a642b3d0e9fda59`. Reusing that mesh's two bind poses is
  intentional, not a mismatched loose/installed mesh substitution.
- `SatsumaFrontSuspensionController` takes the live NWH non-rotating hub
  rotation and multiplies each donor-local spindle/lower-strut/rod-end frame
  once. The full-droop quaternion is an alternative fallback, not a second
  camber multiplication. The top remains body-fixed. The original spindle's
  near-180-degree mesh-axis quaternion is constant before and after connection;
  it must not be removed to compensate for the bad joint.
- `SatsumaFrontStrutPresentation` interpolates installation preview bones to
  those same endpoints and restores the same skin bones. It has no branch on
  bolt tightness. No skin, fixed mount or donor-local quaternion change is
  justified by this reproduction.

Relevant implementation: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
(reviewed front poses and `ConfigureFrontSuspensionRuntimeRig`),
`Assets/Game/Vehicle/NWH/Runtime/SatsumaFrontSuspensionController.cs`, and
`Assets/Game/Vehicle/NWH/Runtime/SatsumaFrontStrutPresentation.cs`.

## Bounded correction and compatibility

In `Assets/Game/Vehicle/NWH/Runtime/SatsumaFrontSteeringController.cs`,
`CreateFreeBody` now synchronizes the anchor Transform to the already resolved
position/baseRotation **before** creating the body/joint:

```csharp
state.Anchor.transform.SetPositionAndRotation(position, baseRotation);
```

Keeping this at the creation boundary also covers recreation after a connected
state is released. It does not reset or clamp yaw each frame. Ordinary free
motion, relative±33° limits, the one-shot14 mm adjustment, connection latch
semantics and the existing NWH/chassis ownership remain unchanged. No new
centering force, motor, collider, contact solver, camera offset or special-case
180° compensation is added.

Stable IDs, public assembly entry points, mount definitions, staged fasteners,
save schema and optional alignment data are unchanged. The donor caster1.15
omission and other previously documented parity gaps are not fixed here.
General save-system repair remains deferred until the complete suspension has
been accepted, as requested by the user.

## Validation and manual acceptance

Executed with Unity6000.3.11f1, interactive Editor closed:

- Red reproducer: **1/6 passed**, five rotated-spawn failures
  (`codex-front-yaw-v1d37-red-r2.xml`).
- Identical six cases after the one-line fix: **6/6 passed**
  (`codex-front-yaw-v1d37-green.xml`). Exact production-pose free yaw remains
  at the initial Alignment after15 steps, with matching anchor Transform/RB.
- Final expanded PlayMode: **34/34 passed**, no skips
  (`codex-front-yaw-v1d37-physics.xml`): eight new tests plus all26 existing
  front/rear contact, fastener, connection and actual adjustment-input tests.
- Final EditMode: **143/143 passed**, no skips
  (`codex-front-yaw-v1d37-edit.xml`): existing generated content, donor bolt
  frames, assembly gates, alignment/tool behavior, NWH and DTO regression tests.
  Executing existing save regressions does not implement deferred save repair.

The final count is177 distinct targeted tests, not183 including the earlier
six-case rerun. This is not a full project suite or a new original-game live
comparison. Full Bootstrap was not rerun in this runtime-only follow-up;
the exact production metadata pose is tested directly. No prefab rebuild
was required and no imported payload or builder configuration changed.

The eight new tests retain yaw0 as a control, test yaw90/165/180, tilted spawn
and exact production pose, and delay visible assembly while the free bodies
simulate. They independently assert chassis-relative carrier and connector
orientation, not merely the joint's own reported angle. Two further cases
retain the same bodies/joints while lifting/tilting/turning the chassis, and
recreate the hinge after a connected8→0 release in the same frame as a changed
chassis pose. No global SyncTransforms masks the creation boundary. Both pass.

Commands: Unity `-batchmode -nographics -runTests -testPlatform PlayMode` or
`EditMode -testFilter ... -testResults ... -logFile ...`; complete filters and
logs are retained beside the XML files in `Logs/`. All executed final runners
exit0. The first discovery attempt (`...-red.xml`) ran0 tests because the new
test meta GUID was malformed; the GUID was fixed before the real red run and
that zero-test result is not counted as success. Final logs have no C# compile
errors. Scoped whitespace/GUID checks pass; unrelated existing repository-wide
scene whitespace was left untouched. Frozen GAME SHA256 was rechecked and
matches the authority above.

Changed files: `SatsumaFrontSteeringController.cs`; new
`SatsumaFrontSteeringSpawnPlayModeTests.cs` and `.meta`; this report and its
V1d.36 report follow-up link; `Docs/Porting/DONOR_AUDIT.md`, `PORTING_MATRIX.md`,
`SYSTEM_MAP.md`, and `PORTING_LEDGER.csv`. No API, stable ID, DTO or migration
change. Generated metadata remains V1d.36; QA filenames use v1d37.

User acceptance closes the reported pre-tightening spindle reversal/strut
twist. It is not a claim that every checklist permutation below was separately
observed, that all original-game parity gaps are closed, or that saves work.
Retain the checklist as regression guidance.

Manual regression check in a **fresh Play session** at the normal Bootstrap spawn:

1. Wait before assembling; then install each front spindle/strut in turn.
   Inspect the free pose **before tightening the rod joint**. There must be no
   spontaneous near-reversal or backward-facing strut connector caused by the
   car's world orientation. A small initial toe angle/free motion is allowed.
2. Connect the rod and run the12 mm sequence0→7→8→7→0, then re-tighten.
   Confirm correct one-time connection alignment and a valid recreated free
   pose after disconnecting. Do this on both sides.
3. Recheck14 mm adjustment before/after connection, the accepted contact with
   ground, and the one-strut no-launch scenario. Do not mask the bug by testing
   only an already fully tightened assembly.

## Small next-session specification

The user has accepted the reported front free-yaw correction. Preserve it and
the accepted vertical solver, donor skin frames, stable IDs and177 passing
regressions. The next scoped task is issue4: compare rear-arm droop and shock
separation against the frozen original, then fix only the evidenced mismatch.
Do not proceed from issue4 to wheel issues5–6 before its user acceptance.
General save repair still waits until the entire suspension is accepted.
This acceptance update changes documentation only; no issue4 implementation
was started here.
