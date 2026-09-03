# Cheap Car Repair player-feel tuning — 2026-08-12

## Scope and evidence

The user-owned Cheap Car Repair installation was inspected read-only as a
secondary behavioral and configuration reference. It is not the locked My
Summer Car Phase 1 donor and does not expand Phase 1 content scope.

| Evidence | Identity | Use |
|---|---|---|
| Steam installation `Cheap Car Repair` | app `2904040`, build `24237531` | Installed-build identity |
| Runtime preferences | game version `1.2.0025` | Active sensitivity `1.0` and horizontal FOV `120` |
| `Fuszerka_Data/Managed/Assembly-CSharp.dll` | SHA-256 `80B305144550642701B6C4747038C9A150BC1B6299A33CCB703E4BD09EC1E11F` | Read-only behavior/configuration inspection |
| Serialized active player resources and scene | Unity `6000.2.15f1` | Movement preset, camera modules and controller values |

No Cheap Car Repair source, assembly, asset, prefab, FSM or runtime dependency
was copied into the project. Temporary inspection tooling and extracted
metadata were removed after the audit. The window capture interface could not
capture the running game, so subjective visual parity is not claimed.

## Project-owned implementation

The existing accepted player architecture remains authoritative. This pass
extends `FirstPersonMotor`, `FirstPersonLook` and `PlayerInputRouter`; it does
not replace them.

- Directional movement targets are `2.9 / 2.5 / 2.6 m/s` for forward,
  backward and strafe walking, `6.4 / 3.1 / 5.1 m/s` for the run preset and
  `1.85 / 1.7 / 1.75 m/s` while crouched.
- Ground acceleration/deceleration use per-frame `Lerp` response rates
  `8 /s` and `10 /s`. Air control remains the existing bounded `4 m/s²` path.
- Run is hold-to-request but cannot engage from rest. It requires forward input,
  horizontal speed greater than `1.7 m/s`, and less than or equal to `0.9`
  absolute strafe input. Backward and pure-strafe run are rejected.
- Jump input reserves a valid ground/coyote jump, plays a short `0.085 second`
  preparation crouch, then launches to `1.05 m` under `21 m/s²` gravity. The
  crouch is a `0.07 m` presentation-rig dip, not a traversal-capsule resize.
  A single-use `0.1 s` coyote window remains available after leaving ground;
  there is deliberately no pre-land jump buffer.
- The accepted My Summer Car traversal capsule, ground projection/snap,
  posture heights, collision-limited forward lean, save DTOs and stable-ID
  boundaries remain unchanged.
- Core mouse look remains unsmoothed and frame-rate independent at `0.1 degrees`
  per pointer delta with an `80 degree` pitch limit. A presentation-only local
  lag is capped at `1.25 degrees` yaw and `0.85 degrees` pitch, returning in
  `0.08 seconds`, so the camera does not feel welded to the player root. The
  gameplay/body yaw still follows immediately and the visual rig adds at most
  `0.018 m` of counter-sway, preventing an unbounded owl-head split.
- `FirstPersonCameraFieldOfView` owns aspect-correct `120 degree` horizontal
  FOV and hold-`C` zoom to `0.5x`; it does not add speed-based FOV pumping.
- `FirstPersonCameraMotion` adds restrained look inertia, body counter-sway,
  roll (`0.4 degrees`) and short jump/landing impulses on a separate
  `MotionPivot`. Jump feedback uses a `0.055 m / 1.15 degree` target with a
  `0.04 second` eased attack and `0.15 second` return. There is no
  cyclic walking/running headbob or noise.

The serialized acceleration field names migrate through
`FormerlySerializedAs`, and the old four-argument `PlayerInputRouter.Configure`
method remains as a compatibility overload. Save schemas, public movement
intent methods, prefab GUID, interaction ray/carry hierarchy and accepted UI
contracts are unchanged.

## Automated validation

- Player/Interaction EditMode: `35/35` passed on D3D11.
- The player runtime and PlayMode test assemblies compiled after the jump
  preparation change. Test execution is currently blocked before discovery by
  unrelated compile errors in the concurrently added
  `LicensedAxisNeutralArmsImporter.cs` (`Vector3` passed where `float` is
  required). The immediately preceding camera revision passed focused
  locomotion/camera PlayMode `11/11`; those results are not claimed for this
  unexecuted revision.
- Coverage includes directional speed selection, per-frame response, run
  entry restrictions, coyote-window boundaries, real run acceleration, jump
  height, double-jump rejection, edge coyote jump, camera impulse presence,
  bounded look/body inertia with recentering and aspect-correct FOV/zoom reset.
- Unity script compilation completed without new compiler errors. Five existing
  obsolete `ShaderUtil` warnings in unrelated texture tooling remain.
- The M4 project validator reached its project-wide stable-ID gate but failed on
  pre-existing duplicate IDs shared by world-remaster prefabs and comparison /
  playtest scenes. No player-prefab validation error was reported.

## Manual acceptance still required

Test the active Bootstrap player at 60, 90 and 144 FPS: start/stop, forward
run threshold, diagonal-to-strafe transition, step/door traversal, standing and
coyote jump preparation, crouch height, `C` zoom, interaction aim at 120-degree
horizontal FOV and motion-sickness comfort. The final feel is intentionally
tunable after that hands-on pass; automated checks prove contracts and bounds,
not taste.
