# Satsuma operating model, wear and valve-adjustment packet

Status: **In progress. The solver is now explicitly bound to the canonical
playable host and its real assembly/electrical controls. Targeted integration
passes; full fluid geometry, motion and symptom-audio work continue.**

## Implemented ownership and compatibility

- `VehicleSimulationHost.satsumaOperatingSource` is an optional serialized
  capability. An unbound host keeps the existing M06 simulation behavior.
- `SatsumaOperatingModel` is a pure fixed-step consumer of typed tuning,
  electrical and fluid-hardware snapshots. It does not read scene names,
  donor IDs, item filenames, Unity clips or presentation transforms.
- Existing oil/coolant/fuel litres, voltage and temperature stay in
  `VehicleSimulationState`. Its schema-1 DTO gains the explicit
  `hasSatsumaOperatingState` extension, independently versioned at 1.
- The extension contains the two brake reservoirs, clutch reservoir,
  contamination, pressures, cold-start progress and electric-fan state.
  Absent old fields initialize new reservoirs empty and preserve existing
  litres/voltage/temperature, including old oil surplus above the reviewed
  3-litre fill capacity. Filling never deletes that surplus.
- A generic host rejects the new extension before restore mutation rather
  than discarding it. DTO snapshots do not alias live solver state.
- `AssemblyMechanicalConditionState` owns fixed-roster mechanical condition
  on the existing part stable ID. Rebinding/activation does not heal it.
  Its optional part DTO defaults old, previously unmodelled condition to 100%.
  Double-precision accumulation prevents tiny healthy-engine wear from
  rounding away at every substep.
- Purchased consumables retain `WorldItemInstance` condition. The existing
  bridge gains `IAssemblyWearSink`; no additional part-condition DTO is allowed
  on dynamic consumables. Native item/vehicle preflight rejects dual ownership.
  Sub-0.0001 percentage-point item wear is accumulated between ticks; a restore
  may discard only that rounding remainder, not recorded condition.
- No battery-ownership rewrite, physical engine-mount rewrite, stable-ID
  reassignment or donor-runtime dependency is introduced.

## Frozen rules versus solver calibration

Source: read-only frozen `GAME.unity`, revision
`msc-world-baseline-04a1.1-c3f2f337`, SHA-256
`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`.

Transferred scalar/behavioral evidence:

| Source | Rule |
| --- | --- |
| CapTrigger 104542 / 105616 / 105813 / 105908 / 112275 | coolant 5.4 L; oil 3 L; clutch 0.5 L; rear/front brakes 1 L each; coolant 0.2 L/s, others 0.1 L/s |
| Mixture109336 + Choke109201 | density clamp 0.9–1.1; choke factor `1 + ChokeLevel`; mixture multiplier clamp **22**, including full choke; AFR, idle and enabled lean/rich thresholds |
| Mixture109336 | `(100 - temperature) / 20` initial delay; the new solver bounds this to 0.5–8 s and uses actual cranking progress |
| Cylinders104983 | spark <5 gives ×0.3; >15 gives ×0.75 and ping; otherwise angle/15; non-zero cam timing reduces firing contribution |
| Valves108555 | good intake 6–8, exhaust 5–7; bad valve feedback; disabled exhaust-valve backfire action stays disabled |
| RockerShaft Data109466 + Screw104479 etc. | actual intake adjustment bounds **4–10**, exhaust **3–9**, step 0.3; the shared Screw template's 1–12 snapshot is not authoritative |
| Oil112415 | filter `(8-tightness)/800`, pan `(64-tightness)/1000`, cover `(48-tightness)/60000` losses; 1.1 s loop; contamination and wear relationships |
| Cooling106153 | thermostat 80 C; electric fan >97 C; stock pressure `litres * temperature / 44 * 1.4`, relief at 16 PSI |
| Belt Jumping110290 | adjustment reference 7; slack-dependent squeal and adjustment-dependent wear |

The solver is **Reimplemented**, not an exact managed-code port. It uses the
mean of the donor's mixture random interval, normalizes valve output onto the
accepted torque curve, and continuously integrates heat, pressure, hydraulic
loss and electrical charge. Pressure in bar, thermal rates, belt slip scaling,
hydraulic effectiveness/loss and overheat wear are project-owned calibration.
They require live comparison/tuning; passing arithmetic tests does not establish
full donor parity. Empty oil/coolant are no longer hard combustion vetoes only
when this optional model is enabled. Fuel and structural/electrical gates stay
authoritative. The electric radiator fan is not belt-driven.

## Valve retention repair actually authored

`Phase1SatsumaValveAdjustmentsAuthoring.RefreshBatch` ran successfully against
the existing canonical prefab without rebuilding the car or importing the map.
It retained five real mounting IDs, suffixes **2,5,6,8,9**, with the donor
40-stage maximum and ON16/OFF0 latch. Eight false mounting IDs remain reserved
on disk for migration but are removed from the active graph/prefab targets.
They are replaced with typed screwdriver targets bound to eight existing
sanitized donor screw renderers (source mesh `d42cbf361095bad4f9091a43a07fe91a`).

| Previous pseudo-bolt suffix | Valve | Frozen marker / Screw |
| --- | --- | --- |
| 1 | cylinder 3 exhaust | 37384 / 104479 |
| 3 | cylinder 4 exhaust | 42106 / 105791 |
| 4 | cylinder 2 exhaust | 43489 / 106185 |
| 7 | cylinder 1 intake | 47683 / 107429 |
| 10 | cylinder 3 intake | 62067 / 111428 |
| 11 | cylinder 4 intake | 62501 / 111531 |
| 12 | cylinder 2 intake | 66255 / 112762 |
| 13 | cylinder 1 exhaust | 68329 / 113362 |

The optional `hasValveAdjustment` part DTO stores four intake and four exhaust
values. Old pseudo-bolt stages never become valve settings. Missing old values
use the prior healthy operating behavior (7 intake, 6 exhaust). Exact complete
13-marker records migrate to five real bolts; partial, duplicate, invalid-stage
and contradictory-latch records fail before mutation. Source saves are not
rewritten. The historical roster-count normalization includes the eight retired
identities, preserving older front/body/consumable migrations.

Controls: with the rocker cover removed and shaft installed, use the existing
size-0 screwdriver. Wheel up reduces the selected setting by 0.3; wheel down
increases it. F and a 6 mm spanner do not change valves. Settings persist on the
shaft. The live-host binding now consumes these settings.

## Live assembly binding follow-up

`SatsumaEngineOperatingSource` reads the existing explicit assembly registry,
settings, physical choke, current purchased belt/plugs/filter, cap state and
electrical connection identities. `Phase1SatsumaOperatingAuthoring` added that
source and nine condition components (four pistons, crankshaft, head gasket,
water pump, alternator, rocker shaft) to the canonical prefab, without replacing
parts or public IDs. Reauthoring is idempotent and never restores health.

The opt-in host now uses actual simulation litres instead of the old authored
false fuel/oil/coolant flags. That stale second gate was caught by a real-prefab
start test; generic hosts retain their original behavior. The session-only
debug menu offers fuel-only readiness bypass and connected-battery autocharge.
Neither fills fluids, fabricates a battery nor bypasses assembly/wiring.
`SatsumaEngineEnvironmentBridge` supplies the existing vendor-neutral weather
temperature; it does not query Enviro or save a second temperature authority.

The 72-point oilpan fastening group includes the separate 8-stage drain plug.
Oilpan seal tightness therefore excludes `oilpan.boltpm-3` (64 remaining points).
The reviewed travel FSM 108997 opens the drain below stage6. Draining at 0.1 L/s
while stopped is a continuous-flow calibration. Separate pipe-fitting seal
fractions cause brake/clutch fluid loss; clutch leakage cadence remains an
explicit project analogue, not a verified donor equation. Worn/loose plugs
reduce smooth firing. Cam error beyond six degrees applies the donor 5.5-point
rocker damage amount at a documented modern one-second cadence. Alternator
condition is consumed, but its long-term wear progression is not yet modeled.

## Executed checks

Unity `6000.6.0f1`, batch EditMode, no user save writes:

- `Logs/live-engine-operating-initial-20260906.xml`: **48/48 passed**, exit 0.
  New operating model plus prior vehicle simulation tests.
- `Logs/live-engine-wear-initial-20260906.xml`: **85/85 passed**, exit 0.
  Mechanical/item ownership, prior engine save extensions and operating model.
- `Logs/live-engine-valves-author-20260906.log`: exit 0,
  `SATSUMA_VALVE_ADJUSTMENTS_OK changed=18 retainedMountBolts=5 valveSettings=8`.
- `Logs/live-engine-valves-initial-20260906.xml`: **126/126 passed**, exit 0.
  Valve controls and migration, canonical bindings, previous adjustments/save
  extensions, wear and operating rules. No skip or failed test in these runs.
- `Logs/live-engine-source-author-20260906.log`: exit0, changed10; the source
  and nine condition owners were actually authored on the existing prefab.
- `Logs/live-engine-source-initial-20260906.xml`: 34/36 passed. The two failures
  exposed the stale fluid gate and the drain plug being counted as pan bolts.
- `Logs/live-engine-source-fixed-20260906.xml`: **36/36 passed**, exit0, no skips.
  Canonical source/idempotence, actual graph tuning/caps/fittings, running after
  battery removal, stopping after belt loss, part-owned wear/restore and fuel-only
  debug boundaries, plus pure operating and cap regressions. Test assembly and
  consumable fixtures are explicitly synthetic; no user save is rewritten.

The authoring pass made recovery copies of the exact pre-edit prefab and mount
definition under ignored `Logs/codex-valves-before-*` and
`Logs/codex-rocker-mount-before-*`. The earlier scoped source/payload backup is
unchanged. Broad PlayMode/native-save/visual/audio acceptance and import
idempotence remain pending for the complete integration pass.

The paragraph above records the operating batch's original handoff, not the
latest project status. The later native-purchase, real-key, graphics,
full-Bootstrap and combined regression evidence is maintained in
`SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md`. Manual acceptance remains
separate from those executed checks.
