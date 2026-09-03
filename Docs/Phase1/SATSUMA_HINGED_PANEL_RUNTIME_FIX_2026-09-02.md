# Satsuma hinged-panel runtime correction — 2026-09-02

## Scope

This correction covers all four donor-hinged Satsuma body panels: both doors,
bootlid and hood. It also covers the reported launch on door installation,
mouse-held movement, retained inertia, closed latching, collision obstruction,
unfastened full-open detachment and the cabin hood release. The 2026-09-03
follow-up additionally covers hinges becoming inactive after the first partial
opening, bootlid bolts piercing the outer skin, and the missing exterior
bootlid handle/garnish. The V58 follow-up restored the donor bootlid's
full-open hold. The V60 follow-up corrects the subsequently disproved static
hinge-arm interpretation and removes mirrored-door latch decisions based on
`HingeJoint.angle`. Save/New Game startup ownership and Player code remain
outside this pass.

## Frozen donor evidence

The read-only locked `GAME.unity` remains SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The generated `Phase1SatsumaV1cHingedAssemblyConfigurationAudit.csv` records:

| Panel | Parent pivot local position | Axis / limits | Open torque | Close torque | Joint break | Fasteners | Separate release |
|---|---:|---:|---:|---:|---:|---:|---:|
| bootlid | `(0, 0.410808861, -1.50000048)` | `X`, `-70..0 deg` | `(-30,0,0)` | `(1,0,0)` | `500 / 500` | 4 | no |
| left door | `(-0.677000344, 0.1808086, 0.6389993)` | `Z`, `0..80 deg` | `(0,0,120)` | `(0,0,-150)` | `1100 / 1100` | 4 | no |
| right door | `(0.676999748, 0.1808086, 0.6389997)` | `Z`, `-80..0 deg` | `(0,0,-120)` | `(0,0,150)` | `1100 / 1100` | 4 | no |
| hood | `(0, 0.240808427, 1.67999983)` | `X`, `-87..0 deg` | `(-30,0,0)` | `(15,0,0)` | `1000 / 1000` | 4 | yes |

The loose-panel `Use` FSMs contain `SetHingeJointLimits`, `AddTorque`, mouse
down/up and rotation checks. They contain no keyboard/F activation. Torque is
applied only while the matching mouse button is held; releasing it does not
zero Rigidbody velocity. The donor creates a `FixedJoint` closed latch with
`LockStrenght=12000`. The Satsuma panel hinges are created during assembly,
rather than existing as four static serialized joints. The frozen vehicle-hinge
inventory uses `EnableCollision=false`, so ignoring only the connected-body
self-collision is an evidence-backed transfer.

Bootlid `Use` component `105480` has a separate full-open `State 1`. Its sole
action is `SetHingeJointLimits` with serialized minimum `-70` and maximum
`-69` degrees, after which it returns to the mouse-off state. The close path
first restores the complete `-70..0` degree range and then applies local
`(1,0,0)` torque. This is a one-degree open hold, distinct from the closed
`FixedJoint` latch.

The donor FSM changes state roughly ten degrees before closed and then sets the
final closed rotation. The user explicitly rejected that visible early jump.
The remake therefore keeps physical motion until a one-degree stop threshold,
then creates the latch. This is a documented project correction, not a claim
that the donor threshold itself was one degree.

The exact door close checks are side-specific Euler tests: left `Use`
component `110762` applies local Z torque `-150` and compares `Rot` with
`10` degrees; right component `112706` applies `+150` and compares the wrapped
rotation with `350` and `2` degrees. They only permit the final donor snap near
the closed endpoint. A jump from roughly 60-70% travel is therefore not donor
behavior.

The donor bootlid has four `BoltPM` parents at transforms `45424`, `49935`,
`55885` and `69737`. Each has local Z scale `0.5`. Its `Screw` FSM moves the
visible bolt child `-0.0025 m` along parent-local Z per stage, so the effective
part-relative travel is `1.25 mm` per stage and `10 mm` across eight stages.
The donor-active direct child `bootlid_emblem` (`Transform51604`) uses mesh
`datsun_bootlid_001` (bounds extents
`0.280406, 0.031166509, 0.108053 m`). Its approximately `0.56 m` width and
placement identify it as the visible exterior handle/garnish assembly, not an
optional badge that may be sanitized away.

The two long black hinge arms use mesh `bootlid_hooks`. The serialized initial
hierarchy alone is misleading: the body-side copy (`GameObject24240`,
`Transform60308`) starts active beneath chassis `pivot_bootlid`, while the copy
beneath the loose bootlid (`GameObject17462`, `Transform53524`) starts inactive.
Bootlid Assembly FSM `104306`, state `End`, explicitly activates the moving
copy and `Handles` while deactivating the body-side copy. Removal FSM `110021`,
state `Remove part`, reverses those values: body copy on, moving copy and
`Handles` off. Thus exactly one pair is visible, and after assembly it must
rotate with the bootlid Rigidbody.

The hood is different from the other three panels: its exterior open action is
blocked until the dashboard `dash_hood_lock` lever releases the closed latch.
The lever does not open the hood; held LMB on the hood does.

## Root causes in the recreation

1. The first version left a solid installed-panel collider overlapping the
   dynamic chassis compound without the donor joint's connected-body collision
   policy. PhysX resolved the overlap by kicking the whole car.
2. Generic installed-part synchronization repeatedly restored the closed mount
   transform and fought any independently moved panel.
3. The panel was exposed as an `IToolActivationTarget`, incorrectly making `F`
   a toggle.
4. Transform interpolation discarded actual angular momentum, could not be
   blocked by a world collider and could not reproduce donor torque asymmetry.
5. Only the doors were initially routed through the special path; bootlid and
   hood still lacked equivalent runtime joints and the hood-release dependency.
6. The audited old-Unity donor break values were fed directly into Unity 6
   `HingeJoint.breakForce/breakTorque`. Normal chassis constraint impulses could
   exceed them, destroy the joint, and leave generic pose synchronization to
   snap the panel closed and remove its hinge interaction.
7. Reparenting the visible bootlid bolts out of their scaled `BoltPM` parents
   preserved the mesh pose but discarded the `0.5` translation scale. The
   presentation consequently travelled `20 mm` instead of the donor `10 mm`
   over eight stages and protruded through the outer skin.
8. The sanitizer treated every object containing `emblem` as optional trim and
   disabled the donor-active `bootlid_emblem`, removing the handle/garnish and
   exposing the empty opening.
9. The project transferred the bootlid's broad `-70..0` travel limits but not
   the endpoint `SetHingeJointLimits(-70,-69)` state. Once opening torque
   stopped, gravity could therefore close the lid immediately.
10. The first hook audit inspected only initial hierarchy/active state and did
    not decode the Assembly/Removal `ActivateGameObject` actions. It therefore
    kept the body-side hinge arms visible after installation and hid the copy
    that actually belongs to the moving lid.
11. Closed-latch detection used `HingeJoint.angle`. PhysX owns that scalar's
    internal reference frame; on mirrored hinges it can wrap or briefly report
    the opposite side. This could snap the left door from broad travel or leave
    the right door refusing to latch despite reaching the physical stop.

## Implemented correction

- `AssemblyInstalledPhysicsLinkMode.OperablePanelHinge` creates a real dynamic
  `HingeJoint` connected to the owning chassis body.
- The joint uses the audited pivot, local axis and limits. The old donor break
  values remain preserved on `AssemblyHingeMountAuthoring` as configuration
  evidence, but the Unity 6 joint itself is unbreakable. Donor-visible
  breakaway remains owned deterministically by the assembly graph: all
  fasteners must be at stage zero and the panel must reach full opening.
- Held LMB/RMB applies the audited local open/close torque.
- Releasing the mouse stops applying force but preserves Rigidbody angular
  velocity; ordinary damping and collision decide the remaining motion.
- Only solid pairs between the panel Rigidbody and its connected chassis
  Rigidbody are ignored. World collision stays active, so an object in a door's
  sweep physically prevents closing and latching.
- Collision ignores are restored on detach/destruction, returning the loose
  panel to normal pickup and world collision.
- A `FixedJoint` with donor latch strength `12000` owns the fully closed state.
  Opening releases it; closing latches only inside the documented one-degree
  physical-stop threshold. The threshold now uses the Rigidbody rotation in
  the authored mount frame and must remain valid for two consecutive fixed
  steps; it no longer trusts `HingeJoint.angle`.
- A fastened bootlid reaching full opening narrows its live hinge limits to the
  donor `-70..-69` degree window. Held RMB restores `-70..0` before applying
  close torque. No velocity is zeroed and no second `FixedJoint` is invented.
- There is no `F` capability. Both directions are continuous mouse actions:
  held LMB opens and held RMB closes.
- A completely unfastened panel remains attached during partial travel and is
  removed only when opening is held through the full-open endpoint.
- Both doors, bootlid and hood use the same physical implementation. The hood
  additionally requires the dashboard release before each opening cycle.
- Installed-pose synchronization yields to the live joint. Save restoration can
  restore the panel's normalized open angle before physics resumes.
- Body-fastener presentation retains the source `BoltPM` Z-scale multiplier.
  The four bootlid bolts now travel `1.25 mm` per stage rather than `2.5 mm`.
- The exact donor-active `datsun_bootlid_001` handle/garnish remains active on
  both the loose bootlid presentation and generated runtime vehicle.
- `bootlid_hooks` now follows the donor active-object swap: body-side arms are
  visible while the lid is loose; installation hides them and activates the
  bootlid-owned pair; removal reverses the swap. There is never a double pair.

No donor MonoBehaviour, PlayMaker FSM or runtime assembly is shipped. The
assembly graph, stable IDs and existing save DTO remain project-owned.

## Automated evidence

- Generated baseline:
  `Logs/codex-satsuma-hinge-arms-build-r1.log`, SHA-256
  `17C5B67FC1AC7F10013DBBF2EEF2A5706305C03C4BFB39F536A0A8237ED82CB9`.
  Result: `11A-V1d.60`, 125 loose parts, 117 mounts, 280 fasteners and four
  hinged mounts.
- `Phase1SatsumaGeneratedContentTests`:
  `Logs/codex-satsuma-hinge-arms-edit-r3.xml`, `39/39` passed, SHA-256
  `68D5F3D385CD164F527751EB97F8D4EE845F85D4F1A66EC827E83480F13CA41A`.
  It validates all four exact profiles, four fasteners per hinge, the physical
  link type, preserved authoring break values, unbreakable Unity 6 joints,
  mouse-only capability, one-degree latch threshold, save angle, four `0.5`
  bootlid bolt travel scales, the exact active handle/garnish mesh, the
  one-degree open-hold profile and the configured body/moving hook swap.
- `SatsumaHingedPanelPlayModeTests`:
  `Logs/codex-satsuma-hinge-arms-play-r1.xml`, `4/4` passed, SHA-256
  `5C4D89DCE72C9CA28E276F9CEA5722804740F1EE86D4A8D394910315BC5CD9F9`.
  It proves no install launch, retained inertia, no `F`, connected-body-only
  collision ignore, an intact unbreakable runtime hinge, obstacle blocking,
  physical close latch on both mirrored doors, bootlid hook swapping and open
  hold/release, full-open unfastened detachment, collision restoration and
  hood-release gating.
- Current 280-fastener migration: `5/5` passed. Front alignment regression:
  `24/24` passed. Focused native-save graph round trip: `1/1` passed.
- Unity batch generation/compilation completed without compiler errors. The
  full Bootstrap/Player suite was not rerun because that area is under a
  parallel task and remains outside this correction.

## Manual acceptance

1. Install left and right doors on a settled car. Neither installation may
   translate, roll or launch the chassis.
2. Hold LMB/RMB on each door. Release at a partial angle: the door must retain
   inertia and damp naturally. `F` must do nothing.
3. Put a solid object in a door's sweep and close it. The door must stop on the
   object and must not latch through it.
4. Close each door fully. The latch may finish only at the physical endpoint,
   without the former visible jump from roughly 60–90% closed.
5. With at least one hinge fastener engaged, full opening must retain the door.
   With all four at stage zero, holding through full opening must detach it.
6. Repeat held motion and endpoint latch checks on the bootlid. A partial open
   retains inertia; once it reaches the full-open stop it must remain inside
   the donor one-degree hold without continued input. Held RMB must release the
   hold and close it.
7. Confirm the donor handle/garnish covers the exterior bootlid opening. Check
   all four bootlid bolts at stages zero and eight: none may pierce the outer
   skin. With the lid loose, the body-side `bootlid_hooks` pair is visible.
   Installing the lid must replace it with exactly one pair that follows the
   lid through the full arc; removal must restore the body-side pair.
8. Verify the hood cannot be opened from outside while latched, pull the cabin
   lever once, open it with held LMB, close it with held RMB, and confirm another
   opening requires another lever pull.

Manual in-game acceptance of all four panels remains required before the issue
is classified as verified.
