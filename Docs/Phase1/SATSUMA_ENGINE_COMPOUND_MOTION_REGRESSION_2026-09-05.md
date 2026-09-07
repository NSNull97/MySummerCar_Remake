# Engine compound motion follow-up — 2026-09-05

Scope: the user reported a loose, complete engine moving in the engine bay
before they could tighten its mounting bolts. Misplaced mounting-bolt markers
are a separate coordinated authoring fix, not part of this runtime patch.

Classification: project-owned numerical-stability correction to the opt-in
loose-engine physics extension. No donor files or generated payload were edited
by this subtask. No new joint, pinning force, kinematic lock, drag/friction
override or gravity exception was added.

## Evidence and cause

The pre-fix `SatsumaEngineCompoundMotionTests` assembled the actual generated
stock engine: 38 parts, 177.6 kg, three original block contacts and 53 child
compound proxies. Forty common root translations/rotations near home
coordinates (approximately x1550, z-1039) changed the supposedly rigid local
contact shape by **0.000222648465 m** (worst: headers) and its local center of
mass by **0.0000572649151 m**, without assembly or adjustment changes.

`AssemblyLooseCompoundPhysics.Refresh` was deriving relative positions by
subtracting large world coordinates every fixed tick. Float cancellation
exceeded its existing 1-micrometre geometry/center change threshold, leading to
unnecessary shape changes, inertia resets and body wakeups. The separate
installed-engine test passed before the fix: the attached block followed its
mount, was kinematic, and had zero active loose-engine proxies. There was no
independent installed-engine oscillation writer to remove.

The patch composes local transform matrices/rotations up to the explicit loose
owner. For registered installed engine parts, it uses the established exact
part-to-occupied-mount position/rotation identity, preserving their scale and
all actual mount/presentation transforms. This also avoids propagating the
kinematic child body's world-pose synchronization roundoff into contact shapes.
The weighted center is calculated in the owner frame, not from world-position
subtraction. A cached Transform-to-Part dictionary avoids per-tick collections.

The same test after the fix measured **zero shape, center, rotation and scale
drift**. Existing thresholds were not widened. Real local presentation changes
still flow into the physical proxies.

## Physical checks on real generated geometry

The coordinating session executed `SatsumaEngineCompoundMotionPlayModeTests`
using isolated PhysX scenes and the complete generated engine. A preliminary
contact run was invalid: `LegacySatsumaLoosePartsRoot.Awake` intentionally
detaches the loose-parts scope, so moving the vehicle root alone had left the
engine in the default physics scene. The fixture now explicitly moves and
destroys both roots, checks the actor's physics scene/flags, and verifies
actual movement in its first five simulation steps. That preliminary run is
not used as contact evidence.

Validated observations (`codex-engine-followup-contact-02.xml`):

| Scenario | Observed result |
| --- | --- |
| Real engine on a table at origin | First five steps moved 0.0408 m; final 2 s speed and displacement zero; 0/100 awake frames |
| Same table test near home | Same physical landing; final 2 s speed and displacement zero; 0/100 awake frames |
| Exact engine mounting pose, real static chassis and installed/tight subframe | Four initial overlaps with `Collider_92134`, depths approximately 18.6/52.3/55.9/76.5 mm; first motion 0.06849 m; after settling final 2 s max speed 0.00109778 m/s and max displacement 0.00012228 m |

The bay test retains all original enabled chassis/subframe solids and the
engine's real contacts, mass and gravity. It suppresses only unrelated loose
inventory contacts. No engine axis is constrained. After 12 seconds, all three
docking distances were still inside the existing 0.1 m threshold:
**0.0901921 / 0.0624503 / 0.0464683 m**. Pending bolt stages stayed zero and the
engine stayed loose. The bay contacts need not sleep; sleeping is required only
by the stationary table regression, not by the supported engine-bay scenario.

Consequently, a small initial movement while the engine settles against the
real subframe is expected. It is distinct from perpetual self-induced movement.
The measured bay regression now requires final-window speed below 0.01 m/s,
travel below 0.002 m, and all three bolts still reachable, rather than inventing
a permanently hovering unbolted engine.

## Donor/compatibility boundary

The already documented frozen-donor contract remains authoritative:
[engine docking design](SATSUMA_ENGINE_DOCKING_DESIGN_2026-09-05.md) and
[compound assembly evidence](SATSUMA_ENGINE_COMPOUND_ASSEMBLY_2026-09-05.md).
Assembly FSM104917 uses physical distance below 0.1 m to expose a mounting bolt;
the first two aggregate turns attach the engine. A loose unbolted engine is not
silently converted into an installed body. This correction changes numerical
evaluation of project-owned contacts, not that donor assembly rule.

No public API, stable ID, graph edge, DTO, save migration, installed-part
collider policy, suspension behavior or pickup mass limit changed. Runtime
changes are confined to `AssemblyLooseCompoundPhysics.cs`.

## Validation and handoff

- Pre-fix measured regression: `Logs/codex-engine-followup-motion-before.xml`.
- Coordinated full EditMode: **807/807 passed**, including both motion cases
  and the existing full-engine assembly/dock/detach tests
  (`Logs/codex-engine-followup-edit-01.xml`).
- Corrected physical contact tests: **3/3 passed** in
  `Logs/codex-engine-followup-contact-02.xml`.
- The subsequent stricter, measured bay assertions await the coordinating
  session's final combined run; no unexecuted run is claimed here.
- `git diff --check` passed for the scoped files. This subtask did not launch
  Unity; the coordinating session owns all executed Unity results.

Manual acceptance remains required: place a complete engine in the bay,
observe the brief contact settling, and tighten its now-correctly positioned
mounting bolts. Next scoped step: that combined in-game placement/fastening
check, without changing accepted suspension physics.
