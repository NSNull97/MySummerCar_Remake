# Purchased battery boundary — 2026-09-06

## Verdict

Read-only source / frozen-donor audit. No runtime or generated content changed,
no Unity run, no native save read/write, no new tests claimed. Classification:
`BehavioralReference`; purchased-battery integration remains incomplete.

The current night bridge covers **four item categories**, not everything sold
for the car at Teimo: spark plugs, belt, oil filter and headlight bulbs.
`service.store.car-battery` creates `item.car-battery`, but that item cannot
currently become an assembly part. No second battery adapter was found in the
project-owned C# sources. Adding a catalog mapping would make the existing
socket mechanically eligible, but **would not implement interchangeable battery
charge or donor removal rules**.

This is not an obstacle to the first-start test using the existing authored
battery. It is an explicit limit on the broader purchased-parts claim.

## Current ownership and exact integration points

| Evidence | Consequence |
| --- | --- |
| `Assets/Game/Services/Content/Phase1/Phase1ServiceCatalog.asset:328`: `service.store.car-battery` → `item.car-battery`; `Services/Presentation/ServiceItemHandoffBackend.cs` creates ordinary stable-ID world items | Checkout already has a physical battery product. The handoff itself does not install it. |
| `Assets/Game/Vehicle/ItemsIntegration/Runtime/VehicleItemPartCatalog.cs:63` only allows four exact item/part mappings; `Validate` also rejects more than four entries | Battery is intentionally outside the approved consumable bridge. |
| `VehicleItemAssemblyBridge.BindMaterialized` returns when the catalog cannot resolve the item | The purchased wrapper gets no `PartInstance`, assembly registration, installed removal target or surface handoff. Ordinary pickup is not assembly compatibility. |
| Generated `Vehicles/Satsuma/MountDefinitions/mount.satsuma.battery.asset` accepts `vehicle.satsuma.part.battery`, socket `satsuma.socket.battery`, owner body shell | A new mount or graph rebuild is unnecessary. The existing fixed mount can accept a replacement of this definition once properly registered. |
| Generated `LoosePartDefinitions/51854_battery0.asset` and `Phase1SatsumaV1aManifest.json` preserve authored battery ID `e3041a4b559d954e3350946811a1e3cc`, mass 12 kg | Do not replace or re-ID this existing part to implement purchases. |
| `AssemblyGraph.IsPartDefinitionInstalled` iterates `allRuntimeParts`; `SatsumaElectricalSystem.IsBatteryInstalled` uses that query | No inherent authored-roster-only battery-presence lookup blocker. A correctly registered dynamic battery can satisfy presence. |
| `SatsumaElectricalSystem.BatteryVoltage` reads `simulationHost.State.BatteryVoltage` | Voltage belongs to the car, not to the installed battery identity. |
| `VehicleSimulationNodes.cs`, `ElectricalSimulation.Step`, writes global voltage / normalized charge; `VehicleSimulationContracts.cs` captures/restores both in the vehicle simulation DTO | Merely swapping batteries would retain the previous car-global voltage. A discharged replacement could act charged, or a new battery could inherit a dead predecessor's voltage. |
| `Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset:3301`; `ItemInstanceState.cs` | Each purchased battery already has persistent `content=128`, maximum 145, `discharge-rate=.0003`, generic `installed`/`consumed`/`charged` flags and separate condition. None is bound to vehicle electrical state. |
| `ItemPartPresentationBinding` exposes generic condition, not battery charge | Condition cannot be substituted for charge. No charge-to-voltage conversion is implemented here. |

`item.car-battery` is currently provisionally authored at **8 kg**, while the
reviewed assembly battery is **12 kg**. The decorative donor `battery.prefab`
has 10 kg, but the actual behavioral `battery_0.prefab` has 12 kg. A future
mapping must deliberately resolve mass and visual pivot/collider alignment;
it must not silently take the decorative prefab as the gameplay authority.

### Terminal and removal boundary

Terminal shoes, bolt stages and wiring flags are car-owned in
`SatsumaElectricalSystem`, not children/state of the replaceable battery.
`Phase1SatsumaBaselineBuilder.BuildSatsumaElectricalPresentation` places their
presentation under the vehicle electrical root. Keeping existing car-owned
wire IDs is appropriate; replacement must not auto-wire or reset those wires.

However, `mount.satsuma.battery` has **no generic fasteners**. The current
`VehicleAssemblyController.EvaluateRemoval` checks consumable rules, oil-filter
adjustment, generic fastener group, dependency blockers and obstruction; it has
no electrical-terminal guard. `SatsumaConsumableAssemblyRules.EvaluateRemoval`
only specializes the belt. Thus source logic presently permits battery removal
despite tightened terminal shoes when no ordinary obstruction prevents it.
This conclusion is code inspection, not an executed in-game reproduction.

## Frozen original evidence

Read-only source root:
`E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/`.

The distinction between similarly named files matters:

- `GameObject/battery.prefab` is presentation-only (no FSM), SHA-256
  `11475069210bf843a90a6b80a2b56be13cb847e65f1cb354155459961a3303ce`.
- **`GameObject/battery_0.prefab`** contains the purchased battery behavior,
  SHA-256 `81a87af78167d764b6cf8ec444965cffd6ab5d0fd5690da3a2464c96153335f2`.
  Root GO 147589; Rigidbody 5461526 mass 12 kg; `Use` FSM 11484216;
  `Removal` FSM 11454010.
- `Use` float variables (lines 3128–3144) are `Charge=128`, `ChargeMax=145`,
  `DischargeRate=.0003`. `Set data` (line 2275) sends `RESET` to the referenced
  car battery Data FSM, sets its `Charge`, `MaxCharge` and `Installed` from this
  battery. `Installed` (line 2015) reads Data `Charge` back into the battery's
  local `Charge`, subtracts its discharge rate from Data and checks its own
  `Installed` state. `Save`/`Load` use this battery's own charge state.
- `Removal.Shoes` (line 649) reads `Data.Bolted` from `ShoePlus` and `ShoeMinus`,
  then `BoolNoneTrue` sends `REMOVE` only when neither is bolted. It is not a
  generic mounting bolt in the battery socket.
- Authored old `battery0` in `GAME.unity`: GO 11402, Transform 51854, Use
  107358 / Removal 107359. Its captured values differ: Charge 110, maximum
  131, discharge .0008. This is more evidence that charge/wear belongs to an
  individual battery, not a permanent car-global value.

This audit establishes ownership and transfer direction. It **does not derive
or approve** a `128 → 12.8 V` (or any other) conversion, charging curve,
dead-battery behavior or full charger implementation. Those must be measured
from the donor electrical Data / charging logic before changing calibration.

## Other Teimo car-fitting products outside the four-type bridge

| Product | Current boundary |
| --- | --- |
| Car battery | Physical item exists; no assembly bridge; state ownership / terminal guard as above. |
| Fire extinguisher (`service.store.extinguisher` → `item.fire-extinguisher`) | Physical item exists, but no vehicle mount/adapter in the examined `Vehicle` sources or Satsuma manifest. Frozen `fireextinguisher0.prefab` explicitly has `Use.Install` / `Installed` and `Removal`; GAME also has `trigger_extinguisher`, holder and holder trigger. Holder availability is a separate prerequisite to establish, not a reason to make the bottle attach anywhere. |
| SUOMI dashboard, seat and steering-wheel covers | All three current store offers have empty `itemDefinitionId` and explicit `.deferred` effect IDs (`Phase1ServiceCatalog.asset:958`, `973`, `988`). Existing authored parts are `extra-dash-cover-suomi`, `extra-seat-cover-suomi`, `extra-wheel-cover-suomi`; their existence in the garage/assembly does not implement purchase fulfillment. The installed Teimo effect backend accepts only the pub vodka effect, not these cover effects. |

Fire-extinguisher behavioral prefab SHA-256:
`2a131ea0be4ac38112a124552c000d1f3ee0f8c5fab9e0b80acf6322d0150998`.

Store fuse packages and R20 battery packages are not established here as Satsuma
assembly parts. Generic fluids, sprays and expanded household products are not
part of this fitting audit. Mail-order catalog parts are explicitly separate
from Teimo shelf consumables; this report makes no completeness claim for them.

## Smallest compatible proposal — not implemented

1. Keep the original battery, mount, car wires, terminal stable IDs and assembly
   roster. Extend the reviewed catalog/allowlist to a fifth category mapping
   `item.car-battery` → existing `vehicle.satsuma.part.battery`, reusing the
   same item wrapper, stable ID and native dynamic-part save bridge. Validate
   battery mesh pivot, collider and 12 kg behavioral mass explicitly.
2. Add a narrow installed-battery state binding resolved through the exact
   occupied `mount.satsuma.battery`. Do not use the first matching loose spare
   or an authored-only array. Items remains authoritative for purchased item
   state; a compatible state component/optional DTO covers the existing
   authored battery without fabricating an Items identity.
3. Preserve the current simulation voltage API as a projection of the installed
   battery, with explicit install/readback/remove/save boundaries. Establish
   donor conversion and charge ownership first. No replenishment on every
   bind/load and no swapping a global voltage between unrelated batteries.
4. Add a narrow assembly removal rule backed by both existing terminal stages,
   matching the donor bolted/unbolted semantics. Keep wire connections when
   replacing the battery; do not auto-tighten new installation.
5. Plan backward-compatible save initialization deliberately: old native
   vehicle voltage must seed only the historically installed authored battery
   when per-instance data is absent, never overwrite existing purchased charge.
   Reuse existing prepare/apply/rollback and stable-ID validation. Do not edit
   the user's source slot to implement migration.

Required regressions: real purchased battery pickup/handoff to the canonical
socket; two different batteries retain different charge through A→B→A swaps;
loose spare is never selected; both terminal states block/allow removal as
specified; empty socket has no electrical power; save/load while loose and
installed, old snapshot migration, absent source-cell restore and failed-apply
rollback preserve identity/charge; existing four consumables remain unchanged.

**Recommended next bounded milestone:** approve and implement this battery
state/installation adapter as one compatible task, then execute those tests.
The extinguisher and cover fulfillment are separate explicit backlog rows, not
implicit additions to a battery mapping patch.

## Checks performed

Targeted `rg` over project-owned C#, catalog/manifest inspection, direct reads
of the listed generated mount/part definitions and frozen donor prefab/FSM
blocks, and SHA-256 of the two behavioral prefabs. No mass donor extraction,
no full-scene in-memory parse, no executable/DRM changes, no Unity or test run.
Only this report was added; existing 00–08A interfaces/assets/save schemas are
unchanged by the audit.
