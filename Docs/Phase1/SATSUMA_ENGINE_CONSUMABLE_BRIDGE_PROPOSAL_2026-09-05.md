# Stock engine consumables: compatibility-boundary proposal

Status: **architecture proposal only; implementation requires approval** under
AGENTS section 3.2 rule 7. No runtime, graph, catalog or save changes were made
for this report. Scope is assembly on the floor and installation in the car,
not combustion, electrical readiness, fluids simulation or a working hoist.

## Observed gap

The inspected baseline has 117 mounts. It has no spark-plug or alternator-belt
mount. Four plugs can already be dispensed from `item.sparkplug-box` as
`item.spark-plug`; `item.alternator-belt` and `item.oil-filter` also exist as
ordinary items. None has a `PartInstance` assembly binding.

`mount.satsuma.engine-block.oil-filter` exists and accepts only
`vehicle.satsuma.part.oilfilter0`. The starting loose filter is an assembly part;
the purchased `item.oil-filter` is a different runtime representation. The
existing filter mount has no authored fastener/twist interaction.

Direct frozen donor evidence is recorded in
`Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1dLoosePartAssemblyFsmAudit.csv`:

| Surface | Donor Assembly FSM | Trigger transform |
| --- | --- | --- |
| Alternator belt | 110896 | 60106 |
| Oil filter | 110346 | 58168 |
| Spark plug 1 | 110723 | 59443 |
| Spark plug 2 | 106588 | 44884 |
| Spark plug 3 | 113017 | 67143 |
| Spark plug 4 | 105793 | 42118 |

This proves the missing assembly surfaces, not their complete action-level
installation/removal/tightening predicates. Those predicates, installed poses,
colliders and twist-stage ownership still need a bounded donor audit before
authoring. In particular, do not invent belt prerequisites or filter torque.

## Why existing APIs are insufficient

- `VehicleDeliveredPartCompatibilityCatalog.cs`, `TryValidate`: records require
  `item.mail-order.*`. Repository references to this catalog are only defaults,
  generation, validation and tests. It is metadata, not a delivery-to-assembly
  runtime adapter. Adding these item IDs currently fails validation.
- `VehicleAssemblyController.ResolvePart(IPickupTarget)` resolves
  `pickupTarget.Body.GetComponentInParent<PartInstance>()`. An item alone cannot
  enter its candidate/install flow.
- `AssemblyGraph` stores readonly fixed `PartInstance[]` and mount arrays.
  `VehicleAssemblyController` has authoring `Configure(...)`, but no runtime
  register/unregister-part API. Calling Configure when a box dispenses a plug
  would reinitialize the existing graph and is not a safe adapter.
- `VehicleAssemblyController.TryPrepareSaveDataForRestore` explicitly rejects
  `data.parts.Length != parts.Length`. Its known additive-shape migration adds
  selected mounts/fasteners while preserving the part roster. It does **not**
  support arbitrary bought replacement instances or even a changed fixed part
  count. Merely enabling `allowAdditiveSaveMigration` cannot fix this.
- `ItemWorldRuntime` already exposes `InstanceMaterialized`, `InstanceRemoved`,
  `PresentationAttached`, `LoadedInstances`, `SpawnDynamic`,
  `MaterializeForRestore`, and `TryRemoveDynamic`. These are useful adapter
  hooks, not a save/streaming ownership transfer API. Deleting an item and
  spawning a differently identified assembly part would lose its identity and
  state unless explicitly migrated.
- `ItemSaveParticipant` restores `items.instances` at phase 150;
  `WorldEntitySaveParticipant` restores physical entities at 200;
  `VehicleSaveParticipant` restores assembly at 300. The latter currently only
  declares the world-entities dependency. World-entity scene registration
  explicitly excludes any pickup with a `PartInstance` ancestor, since vehicle
  assembly owns loose as well as installed part transforms. Attaching a part
  after world registration therefore needs an explicit ownership reconciliation,
  not just AddComponent. Vehicle preflight also runs before dynamic item
  materialization and must validate descriptors rather than demand live objects.

## Smallest recommended compatible extension

Prefer a **dynamic-part extension to the existing assembly graph, with the
serialized baseline roster preserved**, over a second consumable-only assembly
system. The alternative separate bridge would avoid changing the fixed array
but duplicate mounting, fastening, removal and graph dependencies, and leave
consumables invisible to engine subassembly pickup/removal. It is not recommended.

Proposed work, not existing APIs:

1. Reuse `MSC.Vehicle.ItemsIntegration` as the adapter boundary. Its assembly
   already references Items, Interaction and Vehicle.Assembly; Items must not
   acquire a reverse Vehicle dependency. Introduce a strictly validated mapping
   for only the reviewed consumables. Preserve existing mail-order mappings and
   validation; do not silently turn that catalog into an unrestricted resolver.
2. Keep the item's `StableEntityIdAuthoring`, `WorldItemInstance`, Rigidbody and
   pickup target. Bind a `PartInstance` to the same wrapper and identity through
   the adapter. Keep item identity, package membership and wear in `items.instances`;
   make vehicle assembly authoritative for part transform, mount and fastening.
   This is split field ownership, not two independent physical objects.
3. Add validated dynamic registration/removal in the existing controller/graph.
   Keep the authored base parts untouched; maintain a separate explicit dynamic
   collection used by existing query/install/remove/save operations. Never rebuild
   existing mount runtimes or lose their fastener state during registration.
   Reject identity collisions, unknown definitions and removal of an attached
   part until the defined detach/destruction operation succeeds.
4. Add four plug mounts owned by the cylinder head and one belt mount owned by
   the block: five additive mounts, no pre-spawned fake replacement items. Reuse
   one reviewed plug part definition for all four sockets, a belt definition,
   and the existing filter part definition if its reviewed compatibility permits.
   Extend the existing oil-filter mount instead of making a second competing
   filter socket. Donor-audited fastening/adjustment surfaces remain necessary.
5. Add an explicit versioned dynamic-part save extension and exact previous-base
   roster migration. Old saves initialize five new mounts empty; preserve the
   existing starting filter and every previous stable ID. Save dynamic item IDs
   and definition mappings explicitly, preflight across item/vehicle domains,
   materialize/register before assembly apply, then restore mounts. Reject
   unknown/mismatched/duplicate IDs; do not accept count-only truncated payloads.
6. Define the existing item/world/vehicle streaming ownership transitions for
   attached and detached instances. Installed consumables follow the engine/car
   even when their purchase/source cell unloads. They must not independently
   recover, respawn, archive or regain world-entity physics ownership. Detaching
   must restore ordinary loose-part behavior without duplicating the item.

## Required checks before accepting the extension

- Existing generated base IDs, save migrations and mail-order catalog unchanged.
- Dispense four existing box child IDs; all register exactly once, all fit their
  sockets, and a fifth cannot overwrite an occupied socket.
- Purchased replacement plug/filter/belt works after removal of the previous
  instance; its wear and stable identity survive. No identity conversion/drop.
- Registration while a complex engine is assembled leaves every mount and
  fastener untouched; detachment/complete-engine pickup retains these children.
- Save/load: old baseline, new empty mounts, partially tightened plugs/filter,
  installed belt, spare loose replacements, partial box, and missing/duplicate
  cross-domain references. Failed preflight leaves the original save/runtime
  intact; rollback has no orphan registry entries.
- Unload/reload source shop cell and car cell in both orders; no duplication,
  recovery teleport, physics tear, dangling registration or lost consumable.
- Typed spark-plug tool only works on its intended reviewed targets. Belt and
  hand-tightened filter behavior must follow donor evidence, not a generic wrench.

## Two bounded additional donor findings

Frozen `GAME.unity` SHA-256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

**Exhaust:** direct structural scan finds three BoltPM markers under stock pipe
root 64076 (39814, 40656, 41558; Bolts root 61731), and one under stock muffler
root 46044 (37241; Bolts root 37802). Current mounts have zero fasteners. This
is a concrete missing authored-coverage finding; group ownership, tool sizes,
stages and removal gates were not audited in this packet.

**Three clamp screw tool bindings:** alternator marker 65277 / GO 29232 /
Screw FSM 112463; distributor 45067 / GO 9013 / FSM 106655; hose 55496 /
GO 19432 / FSM 109558. All three use scale 0.65 and slotted `bolt5` presentation.
Donor tool-pickup FSM 110228, state `Screwdriver`, SetFloatValue action #14
(zero-based), writes global ToolWrenchSize = 0.65 (IEEE bytes 66 66 26 3f).
Player Check tool FSM 105041 gets marker scale and compares it with
ToolWrenchSize, tolerance 0.02 (0a d7 a3 3c). This supports the three explicit
Screwdriver tool-rule bindings; their local Screw.BoltSize = 0 is **not** the
tool-selection proof. Carburettor marker 52523 is a separate tuning adjuster,
not included in these three mounting-clamp bindings.

Validation performed: targeted repository API reads and streaming read-only
donor scans. No Unity launch, runtime execution or new tests run for this report.
