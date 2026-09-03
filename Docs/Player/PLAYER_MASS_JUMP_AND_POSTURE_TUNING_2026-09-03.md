# Player mass, jump, posture and eye-height correction — 2026-09-03

## Scope

This correction extends the accepted M04/08A player rather than replacing it.
It changes only jump/posture tuning, landing presentation, physical support
loading and the existing home-scale presentation binding.

## Donor evidence

Read-only sources from frozen revision
`msc-world-baseline-04a1.1-c3f2f337` were inspected:

- `Assets/Resources/PlayMakerGlobals.asset`, SHA-256
  `0C232081E5DD2D6611D27CCD06274CBB31F5FABC5EF5E5F901B86D39DA25AFEC`,
  stores global `PlayerWeight = 83`;
- `_Scenes/GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`,
  contains `YARD/Building/BEDROOM2/LOD_bedroom2/SCALE/Gauge` FSM
  `Measure` (`MonoBehaviour 111907`);
- the scale FSM multiplies `PlayerWeight` by `-2.78`, applies that value to
  gauge local Z rotation over `2 s`, and returns the gauge to zero over `2 s`;
- its donor distance threshold is `0.2 m`. The recreation uses the extracted
  distance-marker position `(165.967, 1.269, -1029.393)` plus a bounded
  `0.24 m` horizontal / `0.45 m` vertical player-foot volume to accommodate the
  project CharacterController origin;
- the sanitized gauge mesh is resolved by project stable ID
  `066254c4582fa4c1e44162588d101bb7`, never by source name or hierarchy.

No donor FSM or runtime code is executed. The gauge mesh remains removable
`TemporaryDirectImport` presentation.

## Runtime changes

- `FirstPersonMotor` now exposes an authoritative launch velocity of
  `6.65 m/s`; the serialized `1.05 m` jump height remains only as a compatibility
  fallback when the new force is non-positive.
- `PlayerLandingImpact` carries both impact speed and originating jump force.
  The old `Landed(float)` event remains intact for API compatibility.
- Landing camera response now has a `14 cm` / `3 degree` maximum and blends in
  `35%` bounded jump-force influence. Falls without a jump still use impact
  speed only.
- Requesting a valid jump from crouch or deep crouch commits posture to
  `Standing`; it is not restored on landing. A blocked standing headroom check
  rejects the jump.
- Lowering retains `1.7 m/s`; rising uses a separate `6 m/s` transition.
- standing/crouch/deep eye heights are raised by `0.08 m` to
  `1.48 / 0.93 / 0.38 m`. The canonical camera pivot is correspondingly raised
  from `1.70` to `1.78 m` relative to the retained `-0.30 m` body hinge.
- the needs-domain weight is synchronized into `FirstPersonMotor`, defaulting
  to the evidenced `83 kg`. A dedicated vertical feet probe first resolves the
  nearest non-player surface beneath the controller. An upward-facing dynamic
  Rigidbody receives `Physics.gravity * mass` during `FixedUpdate`; a static
  surface blocks anything deeper. A kinematic installed panel can route the load
  only to a dynamic Rigidbody in its own parent chain. This gives vehicle
  chassis and other dynamic supports a real player load without turning the
  CharacterController into a second physics body.
- `HomeWeightScalePresenter` reads the same needs state and animates the loaded
  baseline gauge through its stable-ID binding.

## Same-day dynamic-support stability correction

The first support implementation reused `OnControllerColliderHit` and the
legacy `90 degree` CharacterController slope limit. A vehicle corner with even
`normal.y > 0.01` could therefore replace the real floor as the selected
support while the player remained grounded. Applying the whole `83 kg` load at
that side contact produced a large false torque. The render-frame impulse also
fed suspension movement back into controller grounding and could produce
vertical oscillation on the hood or bootlid.

The corrected boundary is:

- support comes only from a downward feet ray, not an arbitrary capsule-side
  callback;
- a support face requires `normal.y >= 0.55` independently of the retained
  donor-evidenced `90 degree` traversal slope setting;
- the nearest non-player surface owns the support decision: static ground and
  unrelated kinematic geometry stop the probe instead of exposing a deeper
  Rigidbody;
- a kinematic hood, bootlid or player-contact proxy routes load only when it is
  structurally nested under the receiving dynamic chassis;
- the support decision samples the centre plus four points at `0.8` of the
  controller radius and requires `0.2 s` of continuous confirmation before the
  existing `0.15 s` load ramp starts. A narrow rocker caught under only one
  part of the capsule therefore cannot become a temporary balance point;
- a side contact with any collider belonging to the same dynamic aggregate
  immediately rejects or clears feet support for that aggregate;
- load is a continuous `ForceMode.Force` on the fixed physics clock, ramps to
  full strength over `0.15 s`, keeps its point in Rigidbody-local space and
  tolerates a bounded `0.12 s` contact-observation gap;
- generic horizontal Rigidbody nudging now requires a matching, currently
  available `IPickupTarget` capability as well as the existing `35 kg` cap.
  Vehicle bodies and installed parts no longer opt in merely by having a low
  Rigidbody mass; loose pickup items retain the old nudge behavior.

## Same-day rolling-vehicle collision correction

The remaining report was a separate force path. An isolated PlayMode
reproduction put a deep-crouched project `CharacterController` against an
oncoming `600 kg` body. Even with no scripted push, the PhysX contact solver
changed the body's forward velocity from `+0.2 m/s` to approximately
`-1.1157 m/s`. Correcting velocity inside `OnControllerColliderHit` could not
reliably undo the later solver step and was removed instead of becoming another
source of jitter.

The generated Satsuma now preserves the donor's collision-role split:

- all 25 world-facing chassis shapes (including the disabled broad provenance
  hull) exclude project layer 9 `Player`, but keep their world/part collision;
- the four donor `PlayerColl` floor/rocker shapes are children of one separate
  kinematic `Project Player Collision Proxy` and collide only with layer 9;
- the proxy follows the chassis hierarchy and blocks the controller, while its
  kinematic body prevents the controller's solver impulse from reaching the
  dynamic `389 kg` chassis;
- a downward support ray that hits this proxy can still route the authored
  `83 kg` gravitational load to the structurally nested dynamic chassis. The
  isolation removes only uncontrolled horizontal solver momentum, not player
  weight on top of the car.

This is a project-owned reimplementation of the frozen donor layer roles
(`Player`, `Collider2`, `PlayerOnlyColl`); no donor physics code or collision
matrix is executed.

## Validation executed

- Unity batch compilation: passed, return code `0`.
- Focused EditMode: `76/76` passed, `0` failed.
- Focused player locomotion PlayMode: `14/14` passed, `0` failed.
- Production Bootstrap/home streaming smoke: `1/1` passed, including the
  stable-ID gauge binding.
- New coverage verifies mass synchronization and validation, donor scale math
  and footprint, jump-force landing scaling, crouched-jump posture, fast rise,
  landing jump metadata and physical load delivered to a dynamic Rigidbody.
- Stability follow-up compilation in the full project passed with `0` errors.
  The focused Unity `6000.3.11f1` PlayMode run then passed `21/21`, including
  fixed-clock load magnitude, a damped sprung support, a kinematic panel over a
  dynamic chassis, static-ground occlusion, sloped-side rejection, unregistered
  Rigidbody rejection, a rolling `600 kg` body and retained explicit-item
  pushing. The static-ground regression first failed against the pre-fix runtime
  (`20/21`), reproducing the false through-ground load before the correction.
- The compound-rocker regression first failed alone (`21/22`) against the
  centre-only support probe and passed after footprint, confirmation and
  same-aggregate side-contact rejection were added.
- The raw oncoming-body reproduction recorded the `600 kg` reversal above.
  The final isolated locomotion suite passed `23/23` with the kinematic
  player-proxy boundary and without a post-solver velocity hack.
- Deterministic Satsuma builder `11A-V1d.60` passed and promoted the generated
  prefab with `29` donor collider records, `125` parts, `117` mounts and `280`
  runtime fasteners.
- After the `.60` rebuild, the exact generated-prefab Editor contract passed
  `1/1`; the runtime activation/isolation suite passed `2/2`; and the final
  full-project locomotion regression passed `23/23`, with no failures or skips.
- The broader generated-Satsuma EditMode fixture completed `38/39`. Its only
  failure is an unrelated existing NUnit `Has.Count` assertion in the bootlid
  presentation test; the player-collision contract inside that same fixture
  passed both in the broad run and as the exact `1/1` rerun.

## Compatibility and remaining manual checks

No stable ID, input action, save DTO, scene, public legacy landing event or
accepted 08A UI contract was removed or renamed. Weight already persists in
the existing needs DTO, so no save migration is required. The only intentional
behavioral tightening is that arbitrary light Rigidbodies are no longer pushed
without an explicit pickup capability.

Manual Game-view acceptance is still required for subjective landing strength,
the new eye line inside the house/vehicles, visible suspension response on the
active Satsuma backend and alignment/rotation of the streamed legacy scale
gauge. The Satsuma check must include standing still on hood and bootlid,
  walking off their edges, and walking/running/crouch-walking diagonally into all
  four body corners, both before and after the car starts rolling. The physical
  support load is project-owned plausible behavior; exact donor vehicle
  suspension response has not been claimed or verified.
