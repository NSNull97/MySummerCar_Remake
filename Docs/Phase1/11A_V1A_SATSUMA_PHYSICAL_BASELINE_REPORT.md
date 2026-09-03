# 11A-V1a — Satsuma physical baseline

Date: 2026-08-13

Status: implemented and automatically validated; manual in-game visual acceptance pending

> Historical baseline note (2026-08-15): V1d.6 supersedes this initial
> collider policy. A complete donor component audit found 29 active solid
> shapes, including the usable `CarCollider`, four `PlayerColl` boxes and two
> floor capsules. The current runtime transfers all 29 and is dynamic at the
> serialized 389 kg mass. Counts and the kinematic state below describe V1a
> only, not the current generated Satsuma.

> Collision-role note (2026-09-03): builder `11A-V1d.60` still preserves all
> 29 records, but the four donor `PlayerColl` boxes now live on one nested
> kinematic player-only proxy. The 25 world-facing records ignore project layer
> 9 and remain on the dynamic chassis (the broad `CarCollider` provenance hull
> stays disabled). This prevents CharacterController solver momentum from
> reversing the rolling car while retaining controlled player-weight routing.
> Root center of mass/inertia is derived only from world-facing physical shapes;
> player-only proxy volume contributes no phantom chassis inertia.

## Outcome

The production Bootstrap now materializes one project-owned `vehicle.satsuma`
aggregate at the locked home spawn. It reuses the established M05 assembly,
M06 simulation and native vehicle-save boundaries instead of introducing a
parallel vehicle framework.

The private generated prefab contains:

- stable vehicle ID `323d9fece916469ea30c705ebfcf68df`;
- donor body-shell presentation sanitized to 8 enabled renderers;
- 22 donor-evidenced convex component colliders;
- donor Rigidbody reference mass `389 kg`, damping `0.02 / 0.205`;
- project-owned assembly root, M06 simulation host and save aggregate;
- 46 deterministic mail-order item-to-assembly compatibility records;
- `TemporaryDirectImport` provenance and replacement key
  `legacy.vehicle.satsuma.presentation`.

The monolithic donor `CarCollider` is deliberately excluded because Unity 6
cannot create its full convex hull within the 256-polygon PhysX limit. The 22
component colliders remain and avoid the silently truncated hull.

## Deliberate fail-closed state

V1a is the physical shell slice, not complete Satsuma parity. The chassis stays
kinematic; fuel, oil, coolant and battery availability are false; input is
disabled. It cannot start or drive until the donor loose-part roster, complete
mount/fastener graph, fluid/wiring state and seat authority are implemented.

The compatibility catalog resolves every delivered package now, including
multi-part wheel, seat, N2O, flare and suspension kits. Delivery-to-world-item
materialization remains unchanged; expansion into real `PartInstance` objects
belongs to V1b with the full part roster.

## Generated content and provenance

Generated donor payload remains ignored under:

`Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/`

Tracked manifest:

`Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1aManifest.json`

Locked source: `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`,
root transform `64200`, hierarchy `SATSUMA(557kg, 248)`.

## Validation

- deterministic Unity batch builder: passed (`8` renderers, `22` colliders,
  `46` mail-order mappings);
- focused EditMode: `7/7` passed;
- production resource installer test: passed, exactly one Satsuma and valid
  persistence binding;
- focused production Bootstrap PlayMode: `1/1` passed (`9.46 s`), exactly one
  fail-closed Satsuma is present before gameplay activation;
- manual rendered home/garage position, material, collision and new-save/load
  acceptance: pending.

## Compatibility impact

No established stable ID, public save DTO, assembly controller API, simulation
configuration or catalog item ID was renamed. `VehiclePersistenceBinding` now
has a filename-matched Unity script wrapper over the unchanged implementation
so Unity can serialize it correctly; schema remains version 1 and no migration
is required.

## Next bounded slice

11A-V1b: deterministically inventory and build the complete donor `CARPARTS`
loose-part roster with project-owned definitions, stable IDs, starting poses,
mount points, fasteners, compatibility alternatives and save round-trip.

## V1b follow-up вЂ” complete direct CARPARTS roster

V1b now deterministically registers every logical direct part root below the
four donor groups, excluding only the `PartsGT/WoodSheet` garage fixture:

- `PartsCar`: 75;
- `PartsMotor`: 39;
- `PartsGT`: 6;
- `PartsExtra`: 5;
- total: 125 parts, 78 donor-active at the frozen scene state.

Each root owns a project `PartDefinition`, deterministic stable instance ID,
donor-relative presentation, Rigidbody, sanitized collider (or renderer-bounds
fallback), pickup capability and schema-1 assembly save record. The loose-part
root becomes a composition sibling during `Awake`, so moving the chassis cannot
drag garage parts with it.

V1b validation: deterministic builder `8/22/125/78/46`; focused EditMode
`8/8`; production Bootstrap PlayMode `1/1` (`6.91 s`). The exact new-game
meaning of 47 donor-inactive variants, manual garage physics/save acceptance,
mount points and fasteners remain open. The next bounded slice is 11A-V1c:
mount/fastener graph and verified starting activation.

## V1c follow-up — donor-authored mounts and Assembly FSM evidence

V1c no longer guesses every installation point from a part name. The builder
now records three deterministic audits from the locked donor scene:

- `Phase1SatsumaV1cMountCandidateAudit.csv`: all 125 loose roots against the
  installed hierarchy (`41` unique name candidates, `13` ambiguous, `71`
  unresolved);
- `Phase1SatsumaV1cMountPoseAudit.csv`: mesh-identity/pivot reconstruction
  evidence (`22` unique, `29` ambiguous, `74` unresolved after rejecting common
  primitive/shared meshes);
- `Phase1SatsumaV1cAssemblyFsmAudit.csv`: all `102` Satsuma Assembly FSM records,
  including trigger, held-part identity, activated installed object, prerequisite
  database record, detach object and bolt/bolted references. Donor FSMs remain
  read-only evidence and are never instantiated in the remake runtime.

The runtime prefab now exposes `26` exact-name, unique donor mount poses. Their
`PartDefinition` compatibility is project-owned, each mount has one accepted
stable part definition, and ambiguous matches remain audit-only. The first `20`
directed suspension/steering dependencies preserve donor-evidenced ordering and
reciprocal removal blockers for subframe → wishbones → spindles → front struts
and subframe → steering rack → steering rods/column.

Assembly schema 1 round-trip now executes over `126` parts and `26` mounts.
Focused V1c EditMode is `8/8` green and production Bootstrap PlayMode is `1/1`
green (`6.86 s`). Fastener counts, tool sizes and the remaining engine/body/
interior mount graph are still open; no value is invented where serialized
evidence is ambiguous. The chassis therefore remains fail-closed and kinematic.

## V1c.1 follow-up — exact BoltPM subset and New Game paint

The frozen scene contributes `85` `BoltPM` markers below Satsuma Assembly
targets. The builder records every marker in
`Phase1SatsumaV1cFastenerAudit.csv`, including exact local pose, donor sphere
radius and wrench size inferred from the serialized marker scale. Runtime
authoring is deliberately limited to the `45` markers whose installed part is
already one of the `28` verified mounts (the earlier 26 plus interchangeable
rear drums). The other `40` remain audit-only until their mount identity is
unambiguous.

The working subset uses eight physical wrench identities: 5, 6, 7, 8, 9, 10,
12 and 14 mm. All fasteners preserve the donor `Screw` FSM's discrete stages
`0..8`, clockwise tightening and exact marker pose, but runtime authority is
the project-owned `VehicleAssemblyController`; no PlayMaker code is shipped.
Assembly save round-trip now covers `126` parts, `28` mounts and `45`
fasteners.

The existing ten-colour main-menu selector now configures the fresh Satsuma
before gameplay activation. Body colour is applied per vehicle through
`MaterialPropertyBlock`, captured by the vehicle save aggregate and restored on
load. The paint payload is optional, so existing saves without it retain their
authored appearance and are never repainted by the New Game menu.

Validation: deterministic builder passed (`8/22/125/78/28/45/46`); focused
Satsuma EditMode `9/9`; spanner/catalog EditMode `8/8`; selected-colour New Game
PlayMode `1/1`. Manual rendered paint/material and garage assembly acceptance
remain pending. The chassis stays fail-closed and kinematic while fluids,
wiring, tuning, wear and the unresolved mount graph are incomplete.

## V1c.3 follow-up — extended mount graph and hinged panels

The bounded parent-pivot pass adds 18 more donor-evidenced mounts: 15 fixed
Assembly-parent pivots plus the bootlid and both doors. The runtime aggregate now
contains 46 mounts and 57 live fasteners. The remaining 28 of the 85 inventoried
`BoltPM` markers remain audit-only because their owning assembly identity is not
yet safe to infer.

The bootlid and doors use exact donor parent pivots and project-owned hinge
profiles reconstructed from the locked Assembly and `Use` FSM data:

- bootlid: local X, `-70..0` degrees, break force/torque `500`, donor opening
  torque `(-30, 0, 0)`;
- left door: local Z, `0..80` degrees, break force/torque `1100`, donor opening
  torque `(0, 0, 120)`;
- right door: local Z, `-80..0` degrees, break force/torque `1100`, donor opening
  torque `(0, 0, -120)`.

Installed hinged panels are operated only by held mouse input, matching the
donor `Use` FSM; `F` has no hinge action. Releasing the mouse preserves damped
angular inertia. A held close command snaps and latches in the donor final
approximately `10 deg`, while a completely unfastened panel detaches only after
held opening reaches full travel. Each hinged panel carries its four exact
fastener targets with it while rotating. Open angle is restored from the
existing schema-1 installed-part world rotation, so no schema bump or save
migration is required.

Tracked evidence now includes static `HingeJoint` inventory, all relevant loose
part FSM/action lists, and a compact hinged-assembly configuration audit with
source component IDs, pivots, angles, torques and break values. Donor joints,
PlayMaker actions and state machines remain read-only evidence and are never
runtime authority.

Validation: deterministic builder passed
`8/22/125/78/46/57/3/46`; focused Satsuma EditMode `10/10`; production
Bootstrap PlayMode `1/1`. Manual garage interaction and rendered New Game paint
acceptance remain pending. The chassis deliberately remains kinematic until the
remaining engine assembly, fluids, wiring and seat/driver authority are complete.

## V1d.0 follow-up — loose-part assembly graph and complete engine handoff

The locked donor `CARPARTS` hierarchy contains `76` serialized `Assembly` FSM
records below loose parts. They are now reduced to inert build-time evidence in
`Phase1SatsumaV1dLoosePartAssemblyFsmAudit.csv`; their descendant bolt markers
are recorded separately in `Phase1SatsumaV1dLoosePartFastenerAudit.csv`. Donor
PlayMaker code is not loaded or copied into runtime.

The generated aggregate now contains `84` mounts and `170` live fasteners:

- the existing `46` chassis/body mounts;
- `37` mounts owned by loose parts, so the engine can be assembled on the
  garage floor before it is attached to the car;
- one complete engine-to-body mount solved from the three donor motor-mount
  trigger pairs. The maximum triangulation residual is `3.92 mm`, recorded in
  `Phase1SatsumaV1dEngineMountTriangulationAudit.csv`;
- three exact 11 mm engine-mount bolts and ten runtime wrench sizes, 5 through
  14 mm.

Every registered part exposes a surface handoff target. A carried compatible
part may therefore be handed to the visible owner part or body surface even
when the small mount trigger is occluded by its collider. Owned mounts follow
their loose owner and removal is blocked while a child part remains installed.

The dependency graph now contains `34` directed edges. In addition to the
existing suspension/steering order, donor `db_PartRequired1` and `DetachPart`
evidence enforces crankshaft-before-pistons, timing-cover-before-chain,
flywheel/clutch and dashboard/meter removal order. The three engine mount points
solve a single rigid engine pose rather than allowing three incompatible
partial installs.

Schema 1 remains unchanged. A bounded additive migration accepts only the exact
previous Satsuma save shapes (`46/57` and the intermediate `83/167`) and appends
empty state for newly introduced mounts/fasteners. Unknown, duplicate or
arbitrarily truncated records still fail closed. New Game paint remains an
optional backward-compatible payload.

Validation: deterministic builder passed
`8/22/125/78/84/170/3/37/46`; focused generated-content EditMode `14/14`;
assembly regression EditMode `22/22`; complete vehicle-assembly PlayMode `9/9`;
selected-colour New Game PlayMode `1/1`. Manual garage assembly, rendered paint,
save migration and engine placement acceptance remain pending. Fluids, wiring,
tuning, wear, start/drive authority and donor startup variants remain open, so
the Satsuma is still `PartiallyImplemented` and intentionally kinematic.

## V1d.2 follow-up — smooth handoff, front mudflaps and physical spanner case

Two donor `Assembly` records for `mudflap fl(Clone)` and
`mudflap fr(Clone)` use `ActivateThis` without a `Parent`. The builder now
accepts that exact shape, places each mount under its corresponding fender at
the donor local pose, and selects only the nearest sibling `BoltPM`. The
generated aggregate therefore contains `116` mounts, `205` live fasteners and
`46` loose-part-owned mounts. Dependency edges remain `34`.

Player handoff keeps the donor mount pose and assembly graph authoritative, but
no longer teleports a released part. After a valid relaxed handoff the part is
reserved, made temporarily kinematic and moved with a smooth-step position and
rotation interpolation for `0.34 s`; only then does the ordinary strict install
operation commit it. Invalid order, occupied mounts and incompatible parts are
still rejected before the transition. EditMode/direct API assembly remains
immediate and deterministic.

The donor `spanner set(itemx)` presentation is no longer a magic variant-cycling
wrench. `F` toggles the case, using the evidenced `0.45 s` open and `0.25 s`
close durations and the donor lid pivot/open rotation. The case exposes eleven
separate physical pickup targets for wrench sizes `5..15`; a released wrench
returns to its exact donor slot. Case open state remains in the existing item
save domain. Donor animations and PlayMaker state machines are evidence only;
project-owned controllers own runtime state.

Validation: deterministic builder passed
`8/22/125/120/116/205/3/46`; generated Satsuma EditMode `16/16`;
vehicle-assembly PlayMode `8/8`; production Satsuma bootstrap PlayMode `1/1`;
targeted spanner definition/open/save/stream EditMode `5/5`. Manual garage
acceptance remains required for feel, wrench ray access and rendered alignment.
The complete item EditMode regression subsequently passed `45/45`.
Fluids, wiring, tuning, wear, startup variants and drive authority remain open;
this follow-up does not claim complete Satsuma parity.

## V1d.5 follow-up — systemic physical ownership, jacks and donor paint

The physical chassis is now dynamic from New Game while NWH wheel contacts are
strictly gated by assembly-owned suspension/spring/wheel prerequisites. Runtime
mounts rebind to their logical AssemblyGraph owner, so triggers, fasteners and
recursively installed parts follow the chassis/subassembly across physics,
streaming and restore. Loose/unsecured parts retain ordinary Rigidbody pickup
and RMB removal semantics.

Both donor jacks now have saved project-owned lift state and explicit Satsuma
lift pads. The floor jack can be rolled under the car with bounded temporary
vehicle-only collision ignoring, then raised with F and lowered with held RMB.
The exact twelve donor MainMenu colours replace the former ten approximations;
paint affects only `CAR_PAINT_RUSTY` material slots and retains `body_rust.png`.

Builder `11A-V1d.5` passed. Focused automated coverage passed `62/62` Satsuma,
item/jack and migration EditMode tests, `54/54` assembly/simulation/NWH EditMode
tests and `12/12` assembly/Bootstrap/New Game PlayMode tests. Full UI PlayMode
passed `20/21`; the sole failure is the unrelated pre-existing settings-scroll
viewport tolerance. Exact scope, compatibility and remaining manual work are
recorded in `SATSUMA_SYSTEMIC_ASSEMBLY_PASS_2026-08-14.md`.

## V1d.28 follow-up — bolt/nut tool mode, dense install routing and save discovery

All `235` runtime fasteners now share the exact `bolt-gayka-only` project layer
for bolts and nuts. Their reviewed donor `bolt2`/`BOLTS` presentation exists
only while the owning part is installed. Wrench mode queries only that layer,
snaps the viewmodel wrench to the aimed target and maps opposite mouse-wheel
directions to tightening and loosening. Outline feedback is green at stage zero,
yellow while partial, white at maximum and red for an incompatible wrench.
Each accepted notch owns one bounded BetterMSC-referenced work/regrip cycle;
extra wheel events cannot outrun the animation and mutate several stages at once.

V1d.34 correction to the historical claim below: the 14 mm marker was in fact
the toe adjuster, not an outer-joint fastener. Frozen Screw/Assembly evidence
requires a separate 12 mm joint bolt and four previously omitted 9 mm lower
strut bolts per side. See `SATSUMA_FRONT_CONNECTION_FASTENERS_FIX_2026-08-31.md`.

The subframe now exposes exactly four donor `10 mm` bolts and each front
wishbone exactly two donor `10 mm` bolts. The final assembled-front donor dump
adds four `13 mm` lug nuts to every wheel and one moving `14 mm` outer
steering-rod-to-hub fastener per side; the separate tie-rod adjuster remains the
toe-control evidence and is not conflated with this joint. Installed prerequisite
surfaces route the next valid mount through subframe, wishbone, spindle,
strut/disc and wheel. The interaction query now retains up to `256` ordered hits,
bypasses only the current assembly owner's enclosing collider and uses bounded
`0.20 m` disc / `0.24 m` wheel handoff volumes, so dense chassis geometry cannot
silently truncate the real socket.

The real local slot exposed a separate save-discovery defect: its
`vehicle.satsuma` payload contained `vehicles=0`. Because the production vehicle
lives below the persistent `GameCompositionRoot`, it is moved into Unity's
hidden `DontDestroyOnLoad` scene and was absent from ordinary scene enumeration.
Native save initialization now explicitly registers that persistent hierarchy
through the existing idempotent vehicle participant. Schema 1 and stable IDs do
not change. Newly created saves retain installed mounts, all fastener stages and
loose-part world transforms; the pre-fix empty slot cannot reconstruct assembly
data it never stored. A clean-prefab reload regression destroys the original
aggregate before restoring an installed part, a stage-three fastener and a
loose-wheel pose. The complete save integration suite also guards streamed
canonical-clone disposal so a stale Rigidbody plan cannot abort loading.

Builder `11A-V1d.28` passed at `8/29/125/120/115/235/3/50`. Generated/install
EditMode passed `27/27`, interaction EditMode `41/41`, assembly EditMode `23/23`,
assembly/production-bootstrap PlayMode `12/12`, and save integration `13/13`.
Manual in-game install-zone, wrench-feel and native-slot acceptance remain the
final gate for this bounded correction.
