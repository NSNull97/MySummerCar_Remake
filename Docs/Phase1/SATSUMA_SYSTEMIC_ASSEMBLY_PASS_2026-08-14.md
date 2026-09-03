# Satsuma systemic assembly pass — 2026-08-14

Status: implemented and automatically validated; manual full-garage acceptance
is still required.

## Scope and evidence

This pass repairs the shared physical/interaction foundations behind the
reported assembly failures. It does not claim complete Satsuma parity.

Read-only evidence came from the locked `GAME.unity` at SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`,
the locked `MainMenu.unity`, donor item Rigidbody/FSM records for `Car jack`
and `floor jack`, the donor `CAR_PAINT_RUSTY` / `body_rust.png` material pair,
and the already audited BetterMSC spanner pose/work arc. Donor FSMs,
MonoBehaviours and assemblies remain read-only evidence and are not runtime
authority.

## Implemented corrections

- Interaction selection now resolves a bounded, aim-occluded candidate by
  explicit target priority. The prompt and click evaluate the same currently
  carried `PartInstance`; an earlier held part can no longer leak into UI.
- Root-owned and part-owned mounts are rebound at runtime to the project-owned
  logical `AssemblyGraph` owner while preserving the audited world pose.
  Mount triggers, fasteners and recursively installed parts therefore follow a
  dynamic chassis or loose subassembly as one physical aggregate.
- Installed parts are kinematic assembly children, contribute their authored
  mass to the chassis, can be removed with RMB only while unsecured, and are
  excluded from independent world-item persistence. Restore freezes and places
  the chassis first, restores the graph, then resumes physics.
- Streamed world-item batches are restored while frozen. Shelf items no longer
  wake and fall before all saved poses have been applied.
- Fastener colliders exist only while their matching part is installed. This
  historical pass used white for compatible/incomplete, red for an incompatible
  wrench and green at maximum stage. V1d.26 supersedes that provisional mapping
  with green loose, yellow partial, white complete and red incompatible while
  retaining targetability at both end stops. BetterMSC evidence is reimplemented
  as project-owned wrench motion.
- The Satsuma chassis is dynamic immediately (`389 kg`, the serialized donor
  Rigidbody mass; `557kg` is only part of the donor object's name). The complete
  donor collision contract is retained: 23 convex mesh hulls, four `PlayerColl`
  boxes and two floor capsules. Builder `11A-V1d.60` keeps world-facing shapes
  on the dynamic chassis but makes them ignore project Player; the four
  `PlayerColl` boxes live on one nested kinematic player-only proxy. Automatic
  PhysX center of mass/inertia is rebuilt from world-facing physical shapes
  instead of applying the earlier high prototype center of mass or counting
  player-only proxy volume. NWH Vehicle Physics 2 remains the four-wheel contact
  backend, but each contact is enabled only when the corresponding
  assembly-owned suspension/spring/wheel prerequisites exist. Assembly state,
  not NWH, remains authoritative.
- Car jack and floor jack have project-owned saved `lift-height` state. The car
  jack can be carried only while fully lowered. The floor jack uses the donor
  `9999 / 9999 / 9999` Rigidbody mass/damping contract and donor constraints,
  rolls by held LMB, raises with F and lowers by held RMB. During dragging it
  ignores collision only against a nearby Satsuma, then restores every ignored
  pair. Four explicit chassis lift pads replace donor-sized microscopic aim
  volumes. Both tools animate their reviewed link/arm geometry with the contact
  pad, latch one lift point while loaded and blend support force only after
  physical contact; the chassis is no longer kicked before the head reaches the
  pad.
- The nearby duplicate subframe FSM socket is retired. Only the mesh-derived
  `mount.satsuma.sub-frame` remains authorable; schema-1 saves using retired
  `mount.satsuma.subframe` migrate to it and conflicting occupancy fails closed.
- Every installed part has a separate kinematic trigger proxy for ray selection.
  It follows the installed hierarchy but injects no collision energy, so RMB
  removal can target the part while real installed-part physics remains owned
  by the chassis aggregate.
- Nested wrench targets explicitly bypass only their own toolbox shell during
  bounded ray selection. Selecting a wrench removes it from the case into a
  non-physical camera-local tool mode: it has no Rigidbody, cannot collide and
  cannot launch its neighbours. While selected, the interaction ray accepts
  only fasteners; aiming snaps the real wrench visual immediately to the
  BetterMSC-referenced bolt pose, wheel-up tightens and wheel-down loosens.
  Releasing the mode docks the visual back into its exact case pose. Lid motion
  zeros the loose case velocities so an 8 kg case cannot become a propeller.
- Successful install/remove/fastener mutations use dedicated assembly audio.
  The private override maps donor `assemble`, `disassemble`, `bolt_screw` and
  four body-impact clips to stable project event IDs. A missing override is
  silent: it must never fall back to the generic door-like interaction clip.
- Native save document version 16 adds `lift-height = 0` only to legacy car/floor
  jack item records. Other item records are unchanged.
- The New Game selector now exposes the exact twelve donor menu colours:
  `(50,58,38)`, `(234,217,134)`, `(246,246,246)`, `(202,9,0)`,
  `(202,201,195)`, `(118,75,50)`, `(3,38,69)`, `(206,196,61)`,
  `(158,173,177)`, `(114,104,23)`, `(195,132,35)`, `(41,165,195)`.
- Paint property blocks bind only material slots using donor
  `CAR_PAINT_RUSTY`; `CAR_MASSE` and interior/underbody slots are no longer
  tinted. The selected colour multiplies the existing `body_rust.png`, so a
  fresh car retains starting rust instead of becoming flat clean paint.
- An absent optional paint payload is normalized through palette sentinel `-1`.
  This preserves authored rust for older JsonUtility saves without weakening
  validation of real selected colours.

## Automated validation

- deterministic Satsuma builder `11A-V1d.6`: passed — `8` shell renderers,
  `29` chassis colliders, `125/120` loose/active parts, `115` mounts, `205`
  fasteners, `46` owned mounts and `46` mail-order mappings;
- generated Satsuma EditMode: `21/21` passed, including exact chassis collision,
  body-panel pickup, steering-column order, drum removal and subframe mass;
- item/toolbox/jack EditMode: `45/45` passed;
- generic assembly EditMode: `23/23` passed;
- vehicle assembly PlayMode: `10/10` passed;
- production Bootstrap PlayMode: `1/1` passed, including bound assembly-audio
  override and exact `satsuma_part_install` clip selection;
- Unity fallback audio PlayMode: `9/9` passed, including override precedence
  over the old generic door-like placeholder.

## Compatibility and remaining risk

No established vehicle/part/item identity was renamed. One demonstrably
duplicate mount ID was retired with the explicit alias migration described
above; vehicle assembly save schema remains 1. Native document migration
advances from 15 to 16 only for jack scalar state. Existing renderer-array paint
authoring remains as a legacy fallback; generated Satsuma content uses precise
material-slot bindings.

Manual acceptance still needs one uninterrupted garage pass covering every
assembly branch, both articulated jacks under a settling chassis,
door/bootlid breakaway,
save/load across a streaming-cell boundary, rendered wrench pose and the twelve
paint choices. Exact donor jack link curves remain a visual comparison item;
runtime kinematics and lift authority are project-owned. Fluids, wiring, tuning,
wear, damage and complete drive/start parity remain later 11A work.

## 2026-08-15 toolbox/wrench hot correction

The earlier physical-wrench interaction was superseded after direct user
comparison against the donor toolbox and BetterMSC presentation. Individual
wrenches are now selection targets, not pickup targets. The physical carry/save
pipeline never owns them, all stale per-wrench Rigidbody/pickup components are
neutralized and removed, and the selected visual lives at a direct camera-local
pose until an exact fastener candidate is aimed.

The idle pose is size-aware without changing the wrench mesh scale: only 35%
of the authored `5..15 mm` size difference is compensated by camera distance.
Small keys therefore stay in front of the near plane but remain visibly
smaller than large keys. The 14 mm reference handle exits just below the
viewport and a 22-degree local longitudinal twist exposes the mesh thickness
instead of presenting a flat white silhouette.
Selection state remains immediate, but the real renderer now eases for `0.28 s`
from its exact docked world pose in the open case to that idle pose. Aimed bolt
snap still interrupts the draw presentation immediately, so animation never
owns or delays fastening state.

The bounded ray query switches to fastener-only capability selection in this
mode. Non-fastener actions cannot steal the prompt. A fastener supplies its
authored snap anchor every frame; the wrench is placed there immediately and a
signed wheel step mutates exactly one fastener stage. Positive wheel input
tightens and negative input loosens for every authored thread direction; the
visual work arc still follows the clean BetterMSC-derived 60-degree reference.
Wrong sizes remain snapped but receive red outline feedback and cannot mutate
state; fully tightened fasteners remain green and targetable.

Focused generated-project compilation passed for the changed Interaction,
Items and Player runtime assemblies and for Items EditMode coverage, including
the size-normalized pose assertions. The Unity tests themselves were not
executed during this hot correction because the open Editor was still in Play
Mode; rendered draw path, pose and input acceptance therefore remain manual
before promotion.

## 2026-08-15 installed-part pose, ray and world-contact hot correction

The rear drum mounts no longer mix a chassis-local donor pose with a loose-part
parent. Each drum mount is reconstructed relative to the exact donor-installed
trailing arm, carries explicit arm ownership and moves with that subassembly.
This also removes its remote helper geometry from the arm interaction bounds:
the generated proxy shrank from `7.12 m` to approximately
`0.33 x 0.52 x 0.35 m`.

Installing a part now disables its loose pickup capability and detaching it
restores that capability. Dynamic reviewed parts copy the mount pose into their
Rigidbody before the FixedJoint is created, preventing a stale PhysX pose from
creating an arbitrary joint offset. Their solid colliders remain live, so world
contact transfers through the joint into the chassis.

Builder `11A-V1d.10` completed at
`8/29/125/120/115/205/3/48`. Generated Satsuma EditMode passed `22/22`; focused
installed-part PlayMode passed `2/2`, including an isolated static-world contact
where every solid vehicle collider except the trailing arm was disabled. Manual
hot acceptance remains required for the exact in-game tooltip, arm pose and
ground response. Wrench/fastener behavior was deliberately left untouched.

## 2026-08-16 rear-suspension static pose reset (`11A-V1d.15`)

Live comparison rejected the later V1d.13 and V1d.14 rear-suspension attempts.
The joint/NWH projection layers changed otherwise correct donor transforms and
produced the wrong final arm, spring, shock and drum arrangement. Their rear
runtime projector/deformer components were removed rather than calibrated again.

V1d.15 narrows the accepted boundary to exact static installation of the three
requested stock rear-suspension parts on both corners: trailing arm, coil spring
and shock absorber. Six chassis-local position/rotation pairs are locked from
the frozen donor scene. The builder compares freshly extracted evidence with
that reviewed snapshot and fails when it drifts by more than `2 mm` or `0.25°`.
No rear wheel backend is allowed to rewrite those poses after installation.

The existing four-corner NWH prefab shape is preserved for front-system
compatibility, but both rear support bindings and their rear WheelController
objects are explicitly disabled. Consequently this pass does **not** claim
spring force, spring expansion, damping, ground support or body lift. Those are
a separate later physics pass after the static geometry is accepted in game.

The brake drum remains a control child socket of its matching trailing arm. Its
socket is unavailable while the arm is loose, becomes available only after the
arm is installed and follows the installed owner instead of its original garage
spawn transform. This prevents preassembly from creating a remote drum while
leaving engine-bench owned mounts compatible with their existing behavior.

Builder `11A-V1d.15` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `23/23`; focused static-pose PlayMode coverage
passed `4/4`, including install, chassis translation/rotation and exact retained
local poses. Manual left/right garage comparison remains pending. Audio and
fastener/tool interaction were intentionally not changed.

## Exactly one next milestone

Perform the manual left/right garage comparison of the three locked rear parts;
only after that acceptance, begin a separate rear spring/contact physics pass.
