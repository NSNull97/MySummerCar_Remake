# Cheap Car Repair item interaction tuning — 2026-08-13

## Scope and evidence

The user-owned Cheap Car Repair installation was inspected read-only as a
secondary feel reference. It is not the locked My Summer Car Phase 1 donor and
does not change Phase 1 scope.

- Steam build: `24237531`;
- game version: `1.2.0025`;
- Unity version: `6000.2.15f1`;
- `Assembly-CSharp.dll` SHA-256:
  `80B305144550642701B6C4747038C9A150BC1B6299A33CCB703E4BD09EC1E11F`.

The running Windows game was found as `Fuszerka.exe`, but two attempts to read
its window state failed with Windows UI Automation error `0x80004002`
(`SetIsBorderRequired failed`). Per the computer-control recovery boundary,
interactive capture stopped there. Subjective visual parity is therefore not
claimed. The behavioral notes below come from read-only managed assembly
inspection; no donor binary, source, prefab, scene or asset was retained.

## Reference behavior

Cheap Car Repair has two distinct held-object paths:

1. `HoldingModule` makes tools/presentation items kinematic, parents them to a
   hold root and settles local position/rotation toward zero. Its default
   interpolation multiplier is `3`, roughly a one-third-second settle.
2. `GrabbingHandler` keeps ordinary grabbed objects physical. It disables
   gravity, enables interpolation and continuous dynamic collision, preserves
   the selected surface point as a local grab anchor and drives the Rigidbody
   toward a camera-space target in `FixedUpdate`.

The inspected `GrabbingHandler` defaults are:

- ray distance `2.5 m`;
- default hold offset `(0, 0, 1.5)` before any serialized scene override;
- spring force `90`;
- per-fixed-step velocity retention `0.8`;
- wheel rotation factor `0.05` (`120` raw mouse-wheel units become `6°`);
- mouse free-rotation factor `1`;
- temporary carried mass `1`.

The physical path adds `offset * springForce * fixedDeltaTime` to linear
velocity and then applies the retention factor. It also damps angular velocity,
keeps world collision active, restores captured physics on release and blocks
immediate re-grab for `0.25 s`. Holding the rotation input redirects mouse
motion from the camera to camera-relative object rotation.

## Project adaptation

The project keeps its established `PhysicalCarryController`, stable IDs,
capability registry, save DTO and release/handoff contracts. The adaptation is
clean-room and bounded:

- a pickup now passes `InteractionCandidate.Point`; the selected surface point,
  not the Rigidbody pivot, follows the carry anchor;
- the project carry anchor moves from `(0, -0.12, 1.25)` to
  `(0, -0.10, 0.82)` camera-local metres after the user's direct report that
  items were too far away;
- the physical spring uses force `110`, retention `0.82` and an `18 m/s` speed
  cap; carried damping drops from the old forced minimum of `8/8` to authored
  linear/angular values `1/2.5`;
- target rotation uses response `14 s^-1` with a `24 rad/s` cap;
- the wheel rotates the held object by `6°` per notch;
- holding middle mouse redirects normal sensitivity/inversion-aware mouse look
  into free held-object rotation and freezes camera/body look for that input;
- collision with the world remains active; only player-versus-held colliders
  are ignored, exactly as before;
- full-weight first-person action poses deliberately blend out the physical grab
  offset so the existing drink/smoke viewmodel grip remains authoritative.

### 2026-08-14 running inertia and near-grab correction

Direct user review found two remaining project-side feel problems: a carried
body lagged behind player locomotion and every selected point was pulled to the
same `0.82 m` distance.

The follow solver now separates player translation from motion relative to the
player. It inherits `96%` of sampled owner velocity, capped at `12 m/s`, and
applies the physical spring only to the remaining relative velocity. This
strongly suppresses the bag-on-a-string swing caused by starting or stopping a
run while preserving non-kinematic Rigidbody collision and spring response to
walls or other world bodies.

The selected point's initial camera-forward distance is now preserved between
`0.32 m` and the authored `0.82 m` maximum. A close pickup therefore stays near
the camera; a distant ray hit still comes back to the safe authored maximum.
Full-weight action poses continue to blend toward their authored grip. Save
restore reconstructs its transient carry target from the existing saved
anchor-local body pose, and the existing teleport handoff resets the owner
motion sample. No save schema or stable ID changed.

The reference's `1.5 m` constructor offset was not copied because its active
serialized scene value could not be proven and the user's current-build
feedback explicitly identified excessive distance. `0.82 m` is a project-owned
feel value requiring manual in-game acceptance.

## Validation boundary

Automated coverage asserts the authored anchor/tuning, deterministic spring
calculation and convergence of the originally selected surface point while the
body remains non-kinematic with continuous collision. Final acceptance still
requires an in-game pass with small, long, light and heavy objects near walls,
floor and doorways.

Executed Unity 6.3 validation for the preceding selected-point revision:

- focused Player Interaction EditMode: `26/26` passed;
- complete `PlayerInteractionFlowTests` PlayMode: `6/6` passed, including the
  new physical selected-point convergence test.

The 2026-08-14 revision adds deterministic owner-motion/adaptive-distance tests
and a PlayMode run/stop fixture. Interaction runtime plus both affected test
assemblies compile directly through Unity's generated Roslyn response files.
Test execution is pending because the user-controlled Unity Editor currently
first owned the project lock and, after it was released, the project-wide Unity
compile was blocked by unrelated concurrent errors in
`GarbageBarrelFirePresenter.cs` and `FirstPersonLifeActionPresenter.cs`.
