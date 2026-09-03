# Satsuma front wishbone donor-parity correction — 2026-08-29

**Manual result: FAILED / reopened on 2026-08-31.** V1d.32 reproduced only the
airborne target and incorrectly froze it during ground contact. See
`SATSUMA_FRONT_NO_STRUT_GROUND_CONTACT_FIX_2026-08-31.md` for the correction and
ground-contact regression coverage. The earlier automated PASS below is not
manual acceptance and does not establish ground parity.

## Scope

This pass addresses only reported issue 1: excessive/random front-wishbone
droop before a front strut is installed. Front spring force, missing strut
fasteners, rear suspension and road-wheel seating are intentionally unchanged.

## Donor evidence

Authority is the locked `GAME.unity` SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`,
plus the read-only `SimpleIKSolver.cs` and `Wheel.cs` extracted from that same
licensed build.

- An installed donor wishbone has no `Rigidbody` or `Joint`. Its visible child
  is driven each `LateUpdate` by `SimpleIKSolver` toward
  `pivot_wishbone_fl/fr`.
- The first IK joint locks X/Y and rotates around vehicle-local Z. The hub
  target therefore controls the arm angle; gravity does not.
- In the canonical fresh no-strut state, donor suspension rates are `2/2/2`,
  travel remains `0.20 m`, and an airborne `Wheel` sets compression to zero.
  `Wheel.cs` then places its model at `compression - suspensionTravel`.
- The resulting SATSUMA-local airborne hub centres are
  `FL=(-0.6299995,-0.35,1.1669996)` and
  `FR=(0.6300007,-0.35,1.167)`. Together with the frozen wishbone pivots and
  hub target offsets, this points the wishbones about `29.91 degrees` down.

The user's proposed horizontal pose is therefore not donor-correct. Horizontal
is only the bind direction. A raised car with no strut must show bounded droop;
the defect was the remake's uncontrolled extra droop, not all droop.

## Root cause in the remake

Builder `11A-V1d.31` added a dynamic `FrontWishboneHinge` with gravity, no
spring/motor and limits `-38..38 degrees`. The controller also disabled its
collisions against the chassis. The installed arm consequently fell until a
joint limit or world contact happened, independently of the donor hub target.
The spindle was welded to that falling actor, amplifying the visible error.

## Correction

Builder `11A-V1d.32`:

- removes the temporary installed wishbone hinge and spindle weld;
- keeps installed wishbone/spindle bodies as kinematic presentation, matching
  the donor object model;
- adds explicit bilateral canonical no-strut airborne hub poses;
- drives wishbone, spindle and dependent presentation targets from that hub;
- retains the existing NWH authority handoff after a strut is installed;
- restores the same canonical pose after a strut install/remove round-trip.

No stable IDs, mount IDs, assembly dependencies or save DTOs changed.

## Executed validation

- baseline builder: `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.32`;
- generated Satsuma EditMode: `27/27` passed;
- complete installed-part physics PlayMode: `16/16` passed;
- the focused tightened-strut, loosen, remove and no-strut-pose round-trip is
  included in that PlayMode result and also passed alone `1/1`.

## Known donor quirk and chosen behavior

The donor no-strut branch does not explicitly restore travel/root-Y after a
previous stock or rally strut is removed. No runtime fixture currently proves
whether the old `.14/.15 m` value intentionally survives that exact transition.
The remake resets deterministically to the canonical fresh no-strut `.20 m`
pose. This is an explicit, bounded known difference pending a donor removal
capture; it must not be silently changed from screenshots alone.

## Acceptance gate

Before moving to reported issue 2, manually verify in the garage:

1. Install subframe, one wishbone and its spindle without a front strut.
2. Raise the shell so the corner is airborne.
3. Confirm the arm settles once at roughly 30 degrees down, remains connected
   to the hub target and does not continue falling toward 38 degrees.
4. Install the strut, then remove it while its fasteners are below the removal
   threshold; confirm the arm returns to the same bounded no-strut pose.

The next issue is blocked on explicit user acceptance of this result.
