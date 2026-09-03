# Satsuma front no-strut ground contact — 2026-08-31

Status: front-contact validation passed; user manual smoke result provisionally
positive on 2026-08-31; one rear regression test remains unresolved.
Issues 2–6 are not closed or
worked as separate fixes in this pass.

## Reopened defect

The user rejected V1d.32: installed front suspension parts did not react to
ground, while the subframe did. The previous test froze the chassis in empty
space; it proved only the chosen airborne pose, not contact correctness.

V1d.32 disabled the front wheel solver until wishbone + spindle + strut were
installed, disabled the solid colliders of the kinematic presentation parts,
and forced the canonical airborne hub pose every FixedUpdate/LateUpdate.
There was no remaining ground-to-hub response. The subframe retained its
physical fixed attachment, explaining the observed difference.

## Frozen donor evidence

Authority: frozen 04A1 `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Relative source: `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets`.
The donor installation is unchanged. This pass inspects the frozen export;
it does not claim a new original-game runtime capture.

- Scene component `108089` is FR `Wheel` (GO `13829`), enabled in the frozen
  scene. Its initial travel is `0.20 m`, root local Y is `-0.15 m`, and camber
  is zero. FL uses component `112524`.
- `Wishbone_FR` (GO `23156`) `Data` FSM component `110636`, state
  `Activate wheel`, sets `Wheel.enabled = true`. The contact stage is not
  conditional on installing a spindle and strut first.
- `TireStatus/WheelFR` FSM component `108809`, state `Rim off`, sets radius
  `0.12 m`, rim radius `0.10 m`, width `0.10 m`. Wheel removal `Assembly`
  component `110620`, state `State 2`, restores the same radius/rim values.
- `Suspension` FSM component `108170`, states `Not installed` / `Not installed 2`,
  sets front spring/bump/rebound rates to `2 / 2 / 2`. It does not disable the
  wheel contact solver.
- `Wheel.cs:796` raycasts from the suspension top along its negative up axis;
  range is travel + loaded radius. Lines 839–846 derive and clamp compression
  from the surface distance. Line 988 sets compression to zero in air.
- `Wheel.cs:759` and `763` apply `compression - suspensionTravel` to the wheel
  model and caliper/spindle model. `SimpleIKSolver` then rotates the wishbone
  toward the moving target; it is not a free gravitational hinge.
- `Wheel.cs:1074` computes spring force as rate * compression. Thus `2 N/m`
  across `0.20 m` is only `0.4 N`, not a `2 kN/m` spring. Fast damping factors
  are `0.3`; the donor damper transition velocity is `0.3 m/s`.

Consequently, the previously derived approximately 29.9-degree downward arm
direction describes the **airborne** fresh/no-strut case only. Ground contact
must move the hub upward and change that direction. A ground-loaded screenshot
must not be compared with the airborne angle as if it were a fixed requirement.

## V1d.33 correction

- Keep the existing front NWH contact solver active from the installed
  wishbone stage. Do not reintroduce free PhysX arm/spindle actors.
- Add an optional, explicitly authored suspension-stage profile to the existing
  assembly/NWH binding. No strut uses the donor `0.20 m` travel, zero camber,
  `2 N/m`, and `2 Ns/m` damping. NWH's normalized force curve is linear and
  max force is `k * travel = 0.4 N`.
- The complete-strut stage is captured from the existing approved NWH setup:
  top, travel, force curve, spring force, camber and damper shaping restore
  without retuning the assembled suspension.
- The front presentation adapter consumes the live NWH hub in both stages.
  The canonical air pose is only a fallback before solver initialization or
  when the wishbone is absent.
- Preserve the previous complete-only axle-stability gate: running a weak
  contact solver must not accidentally activate full axle-stability force.
- Refresh the support state before NWH's physics step. Toggle NWH's live
  generated collider along with support; its vendor OnDisable only deregisters
  the wheel and does not disable that collider.
- Keep both spring-length history samples consistent when changing the
  spring top/travel, so the configuration change is not a fake damper impact.

No vendor or donor runtime code is modified. No stable IDs, mount IDs, save
DTOs, assembly dependency definitions, rear solver, wheel seating, or fastener
definitions change. Only the project-owned front contact prerequisites change.

## Validation

Executed:

- Baseline build: `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.33`, exit 0.
- Combined generated-content and NWH backend EditMode: `57/57` passed.
- Installed-part physics plus floor-jack PlayMode: `23/24` passed.
  Floor-jack tests passed `6/6`; Satsuma physics passed `17/18`. All four front
  contact/airborne/transition tests listed below passed.
- `RearSpringsLiftTheChassisThroughGroundedDrums` failed: chassis height
  increased from `0.292074 m` to `0.302656 m` (10.58 mm), while its assertion
  requires more than 20 mm. No rear tuning or assertion was changed to hide
  this result. It is not yet classified as a regression or fixture variability.
- A focused rear recheck could not start (Unity exit 1): the user had opened
  this project in the interactive Editor. That Editor was left running.

Artifacts: `Logs/codex-front-contact-v1d33-build.log`,
`Logs/codex-front-contact-v1d33-editmode.xml`,
`Logs/codex-front-contact-v1d33-playmode.xml`.

New regression cases include:

1. Both front corners, wishbone-only then with spindle: a Default-layer ground
   patch enters 0.10 m of travel; correct HitCollider, upward hub displacement,
   changed arm angle and sub-newton spring load are required.
2. Removing/lowering the patches restores airborne droop without drift.
3. A vertically free chassis drops onto two corner-only patches, compresses to
   the bare-hub hard stop and does not acquire phantom strut lift. Body/subframe
   contacts are excluded so they cannot conceal a failed contact solver.
4. Strut installation/removal changes weak/sprung/weak stages; removing the
   final wishbone disables both solver and its generated solid collider.

## Limits and manual gate

User follow-up after trying V1d.33: "проверил - вроде бы ок щас стало".
Record this as a positive manual smoke check of issue 1, not a repeated
whole-vehicle regression pass. The separate rear-lift recheck still needs the
interactive Unity Editor to be closed before batch validation can resume.

This is behavioral reimplementation on NWH, not a port of the original Wheel
solver. NWH uses its own cast/contact geometry and a PhysX hard-stop collider;
the original uses a ray plus its own bump-stop force calculation. Exact impact
and slope calibration is not implied by matching a static pose.

The documented deterministic fresh/no-strut reset after strut removal remains:
the donor may retain previous strut travel/root Y. No new removal capture
resolves that quirk in this pass.

Manual issue-1 acceptance, using a normal reload with the regenerated prefab:

1. Install subframe and both wishbones, leave struts off; lower/raise the car.
2. Ground approach must raise the arms; lifting clear of ground must restore
   bounded droop. Do not require an always-horizontal arm.
3. Install the spindles and repeat. The spindle and arm presentation must follow
   the same hub, with no frozen underground pose.
4. Verify subframe/jack contact still works. Check a strut install/remove
   round-trip if practical.

Do not proceed to reported issue 2 until the user explicitly accepts issue 1.
