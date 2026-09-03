# Food, fridge and initial item placement fix — 2026-08-14

## Scope and compatibility

This is a bounded Phase 1 correction outside the milestone sequence. It does
not rename or reorder milestones and does not replace the established
Interaction, physical carry, Home/Electricity, audio or native save systems.
The implementation extends their existing boundaries with project-owned,
data-driven item state.

Locked evidence remains:

- donor revision `msc-world-baseline-04a1.1-c3f2f337`;
- `GAME.unity` / `sharedassets3.assets` SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- normalized `WorldObjectPlacements.csv` and frozen `ITEMS` hierarchy;
- the normalized food values recorded in
  `Docs/Items/M09B_DONOR_ITEM_EVIDENCE.md`;
- fridge stable leaves and collision records in
  `Docs/WorldBaseline/LEGACY_OBJECT_CELL_MANIFEST.csv` and
  `Docs/WorldBaseline/LEGACY_SOLID_COLLIDER_DISPOSITIONS.csv`.

Donor hierarchy names, PlayMaker FSMs and runtime code remain evidence only.
Runtime authority is project-owned and keyed by stable IDs.

## Initial placement and physics audit

All 43 canonical `ITEMS` instances now carry their frozen world position,
normalized quaternion, unit scale, donor parent stable ID, donor source layer,
Rigidbody presence, gravity, kinematic/collision state, collision mode,
interpolation, constraints and damping in
`Phase1ItemPlacementCatalog.asset`. Runtime maps movable items to the explicit
project layer `WorldItem` (8) while retaining the donor layer as provenance.

The audit found:

- 43 canonical instances, all with unit local scale and donor parent stable ID
  `9c88c287a45c52e91f6ac6b345b20e10`;
- donor layers: 40 on layer 19, two on layer 15 and one on layer 0;
- 42 donor Rigidbody instances and one intentionally static parts magazine;
- six donor kinematic starts, including the static magazine; the five physical
  kinematic bodies use `ContinuousSpeculative` in Unity 6 to avoid unsupported
  continuous-dynamic startup combinations;
- exact primitive donor shapes where captured; mesh-only items retain bounded
  project proxy collision until their individual donor/production hull is
  accepted;
- no startup force or torque, zero velocity for new dynamic bodies, sleeping
  canonical bodies, bounded depenetration and no pickup component on the
  non-Rigidbody magazine.

The complete audited placement index is below. `RB`, `Kin` and `Constraints`
are the captured startup values (`1/0` booleans; Unity constraint bit mask).
Exact rotations and the remaining physical fields stay in the canonical asset
and are asserted record-by-record by the EditMode suite.

| Placement | Definition | World position XYZ | Donor layer | RB | Kin | Constraints |
|---|---|---:|---:|---:|---:|---:|
| placement.axe.01 | item.axe | 221.8895, 0.7069999, -1113.0813 | 19 | 1 | 0 | 0 |
| placement.axe.02 | item.axe | -681.6341, -0.8820001, -536.57495 | 19 | 1 | 0 | 0 |
| placement.basketball.01 | item.basketball | 158.83499, 3.361, -1029.275 | 19 | 1 | 0 | 0 |
| placement.beer-case.01 | item.beer-case | 160.75009, 2.0839999, -1032.352 | 19 | 1 | 1 | 0 |
| placement.camera.01 | item.camera | -677.7034, 0.44639993, -535.9292 | 19 | 1 | 0 | 0 |
| placement.car-jack.01 | item.car-jack | 151.2411, 1.083, -1042.0641 | 19 | 1 | 0 | 0 |
| placement.coffee-cup.01 | item.coffee-cup | -677.0814, -0.227, -536.87415 | 19 | 1 | 0 | 0 |
| placement.coffee-pan.01 | item.coffee-pan | -676.8244, -0.18700004, -536.7146 | 19 | 1 | 0 | 0 |
| placement.diesel-can.01 | item.diesel-can | 223.20511, 0.543, -1117.4951 | 19 | 1 | 0 | 0 |
| placement.digging-bar.01 | item.digging-bar | 221.8981, 0.93999994, -1112.7161 | 19 | 1 | 0 | 0 |
| placement.fish-trap.01 | item.fish-trap | -677.2379, -0.57000005, -531.4581 | 19 | 1 | 0 | 0 |
| placement.flashlight.01 | item.flashlight | 155.5421, 2.102, -1039.3451 | 19 | 1 | 0 | 0 |
| placement.floor-jack.01 | item.floor-jack | 151.7381, 1.1659999, -1033.8351 | 15 | 1 | 0 | 84 |
| placement.floppy-disk.01 | item.floppy-disk | -1121.5801, 1.7860001, 43.036865 | 19 | 1 | 0 | 0 |
| placement.floppy-disk.02 | item.floppy-disk | -1121.5801, 1.8500003, 43.036865 | 19 | 1 | 0 | 0 |
| placement.floppy-disk.03 | item.floppy-disk | 168.1091, 2.179, -1033.1292 | 19 | 1 | 0 | 0 |
| placement.garbage-barrel.01 | item.garbage-barrel | 150.1191, 1.5309999, -1036.4111 | 19 | 1 | 0 | 0 |
| placement.gasoline-can.01 | item.gasoline-can | 155.39789, 1.2891536, -1042.4506 | 19 | 1 | 0 | 0 |
| placement.helmet.01 | item.helmet | 158.8941, 1.964, -1026.461 | 19 | 1 | 0 | 0 |
| placement.kilju-bucket.01 | item.kilju-bucket | -679.1619, -0.5860001, -534.6561 | 19 | 1 | 0 | 0 |
| placement.kilju-lid.01 | item.kilju-lid | -679.2349, -0.61099994, -534.3721 | 19 | 1 | 0 | 0 |
| placement.lantern.01 | item.lantern | -677.88873, 0.5730001, -535.5429 | 19 | 1 | 0 | 0 |
| placement.macaroni-box.01 | item.macaroni-box | 157.9261, 2.274, -1032.378 | 19 | 1 | 1 | 0 |
| placement.milk.01 | item.milk | 157.6441, 2.34, -1032.3981 | 19 | 1 | 1 | 0 |
| placement.motor-hoist.01 | item.motor-hoist | 152.4431, 1.092, -1040.6051 | 15 | 1 | 0 | 52 |
| placement.notepad.01 | item.notepad | 161.1921, 1.9699999, -1032.362 | 19 | 1 | 0 | 0 |
| placement.parts-magazine.01 | item.parts-magazine | 155.2289, 2.0370998, -1040.5181 | 0 | 0 | 1 | 0 |
| placement.pizza.01 | item.pizza | 157.8971, 2.567, -1032.3461 | 19 | 1 | 1 | 0 |
| placement.portable-grill.01 | item.portable-grill | 155.3701, 1.369, -1040.659 | 19 | 1 | 0 | 0 |
| placement.portable-radio.01 | item.portable-radio | 160.8921, 1.273, -1029.1531 | 19 | 1 | 0 | 0 |
| placement.radar-detector.01 | item.radar-detector | 1732.69, 6.158, -320.28497 | 19 | 1 | 0 | 0 |
| placement.sauna-bucket.01 | item.sauna-bucket | -679.58594, -0.48599994, -537.0581 | 19 | 1 | 0 | 0 |
| placement.sauna-bucket.02 | item.sauna-bucket | 157.1075, 1.8611784, -1042.4255 | 19 | 1 | 0 | 0 |
| placement.sauna-dipper.01 | item.sauna-dipper | 157.138, 1.921, -1042.42 | 19 | 1 | 0 | 0 |
| placement.sauna-dipper.02 | item.sauna-dipper | -679.58594, -0.42999995, -537.0581 | 19 | 1 | 0 | 0 |
| placement.sausages-package.01 | item.sausages-package | 157.6831, 2.536, -1032.3381 | 19 | 1 | 1 | 0 |
| placement.sledgehammer.01 | item.sledgehammer | 155.6611, 1.5469999, -1038.7871 | 19 | 1 | 0 | 0 |
| placement.sofa.01 | item.sofa | -613.09393, 13.982, -1684.6511 | 19 | 1 | 0 | 0 |
| placement.spanner-set.01 | item.spanner-set | 155.1951, 2.066, -1040.9021 | 19 | 1 | 0 | 0 |
| placement.tv-remote.01 | item.tv-remote | 165.379, 1.6459999, -1033.364 | 19 | 1 | 0 | 0 |
| placement.wiring-mess.01 | item.wiring-mess | 152.6714, 2.5914998, -1042.8752 | 19 | 1 | 0 | 0 |
| placement.wood-carrier.01 | item.wood-carrier | -680.9889, -0.96299994, -533.72107 | 19 | 1 | 0 | 0 |
| placement.wood-carrier.02 | item.wood-carrier | 225.6351, 0.42299998, -1112.3961 | 19 | 1 | 0 | 0 |

New Game materializes the canonical records. Native load continues to apply
`items.instances` logical/materialization state followed by `world.entities`
physical pose. An executed round-trip moves and rotates canonical
`placement.axe.01`, resets it to the New Game pose, restores both domains and
proves that the saved position and rotation win. Dynamic items in unloaded
cells remain archived until their owning cell returns.

## Fridge

`ProductionFoodApplianceInstaller` binds the home fridge only through frozen
stable entity IDs in `cell_0_-3`:

- door mesh `5beeac8c1e16e46351b1224a92b15261`;
- paper `72d34129df6c41f282ca82028a53cfd6`;
- handle `63469f2df1f8e8e8460d03f76327f7e1`;
- obsolete solid-volume LOD collider
  `6a575ded49fa3971746b0a57d50b39db` is disabled;
- three accepted shelf leaves retain explicit solid colliders.

The project hinge uses pivot `(157.5036, 2.493121, -1032.6195)`, the captured
axis orientation, a `92.1` degree limit and `158 deg/s` travel. A kinematic
Rigidbody owns the moving collision leaf; the exact door box is non-trigger,
while the ordinary Interaction target is scoped to a `0.2 m` handle trigger.
Open/close feedback posts the established door-open/door-close project event
IDs through `IAudioBackend`, so official Wwise receives them when available and
the Unity fallback remains valid.

The sanitized baseline had separate static collision copies for the door,
paper and handle plus one coarse `LOD_kitchen/Coll` box filling the entire
fridge volume. Those copies are disabled at binding time. Retaining them left
an invisible closed door in front of the food and depenetrated loose items
stored inside. The project moving door collider and accepted cabinet/shelf
colliders are the only active fridge collision after binding.

The fridge interior is a bounded food environment. Closed shelf/door collision
contains loose bodies; residual contact energy is cleared only for uncarried
items inside a closed fridge. Cooling is active only when all three conditions
hold: fridge power on, project electricity available and door closed. The
lighting/electrical integration forwards the existing `grid.home.kitchen`
availability into Home state.

Home save schema 2 adds fridge door and mangal state additively. Fridge power
remains in the existing Home DTO. Older records default the new booleans closed
and unlit. No established stable ID or save domain was renamed.

## Data-driven food and sausages

`ItemFoodDefinition` and `ItemHeatSourceDefinition` are immutable catalog data;
`ItemInstanceState` stores mutable freshness, food timestamp and accumulated
cooking seconds. `WorldItemInstance` contains no object-name switch for food.

Implemented behavior:

- ordinary configured food consumes whole after one `F` and a `1.35 s`
  logic-owned action; it stays physical until completion and then deactivates;
- food actions emit status feedback only and never start first-person hand/arm
  presentation;
- a sausage package contains exactly four deterministic child stable IDs;
  `F` dispenses one loose physical sausage beside the package and never eats it;
- a loose sausage inherits package freshness/timestamp, is individually
  persistent and is eaten with `F`;
- loose sausage cooking is contact-based on registered heat volumes: raw for
  the first `30 s`, cooked next, burned after another `10 s`;
- home stove heat follows existing stove power plus electricity, portable grill
  heat follows its existing enabled state, and the cottage mangal follows saved
  Home fire state;
- raw/cooked/burned/spoiled states select authored tint and needs effects;
- freshness advances from authoritative game time, using the captured ambient
  and refrigerator rates; restore catches up elapsed game time using the saved
  world position, so unloaded time is not silently lost;
- package count, deterministic child membership, individual freshness,
  timestamps, cooking seconds and consumed state round-trip through the native
  item domain; physical poses remain in the existing item/world domains.

Captured rates and cooked sausage effects match the normalized evidence.
Burned and spoiled adverse coefficients and their green/dark tints are explicit
provisional remake values because the locked donor evidence does not yet expose
normalized values for those branches. They are data, not hard-coded runtime
special cases, and must remain `PartiallyImplemented` until a donor comparison
accepts them.

## Executed validation

Unity `6000.3.11f1` batch runs on 2026-08-14:

- `MSC.Items.Tests.EditMode`: 53/53 passed after the sausage/grill/drink
  presentation addendum;
- `MSC.Home.Tests.EditMode`: 9/9 passed;
- `MSC.Needs.Tests.EditMode`: 16/16 passed, including authored drink-food
  routing while ordinary food remains arm-animation-free;
- `MSC.Save.Integration.Tests.EditMode`: 37/37 passed;
- `MSC.Items.Food.Tests.PlayMode`: 2/2 passed.

The sanitized presentation pipeline now validates 147 bindings. The loose
sausage uses the reviewed generated wrapper rather than a primitive fallback;
the 19 Expanded Shop bindings are documented separately in
`EXPANDED_SHOP_EXTENSION_2026-08-14.md`.

The production PlayMode test loads the real Bootstrap and home streaming cell,
binds the actual fridge leaves, verifies solid door and handle trigger, advances
the hinge open/closed, aims the ordinary interaction ray through the opened
door and resolves a pickable macaroni box, checks door/electricity cooling gates
and finds the production stove/mangal heat volumes. The second PlayMode test
verifies the timed whole-food lifecycle.

## Remaining manual acceptance and risk

- Rendered in-player visual/audio acceptance with official Wwise output is
  still manual; batch mode intentionally does not initialize the Wwise sound
  engine.
- Production grill/mangal contact volumes use evidence-backed centers with
  bounded project sizes; exact donor heat footprint comparison is pending.
- Mesh-only item proxy colliders still need row-specific production hull
  replacement; the automated audit proves stable startup, not final collision
  fidelity for every complex silhouette.
- Burned/spoiled effect magnitudes and tints remain provisional as documented
  above.
- No Phase 1 parity row is promoted to `Verified` by this fix pass.
