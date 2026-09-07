# Cockpit C1a — dashboard fastening and switch availability

Status: `ImplementedAutomatedPassedManualPending`.

## Scope and donor evidence

This packet is deliberately limited to dashboard-meter fastening and
availability of the existing wiper switch. It does not implement ignition, keys, seat input ownership,
gauges or warning lamps. Detailed evidence is in
`SATSUMA_COCKPIT_CONTROLS_AUDIT_2026-09-05.md` sections 6 and 10.

- Locked donor `BoltCheck108742 @132003213`: dashboard meters Bolted ON12,
  OFF0.
- Locked donor `ButtonsDash105853 @75974544`: dashboard controls are enabled
  by `Dashboard.Installed && DashboardMeters.Bolted`.
- This predicate does not require full meter tightness, battery installation,
  wiring, voltage or the dashboard mount's own Bolted latch.

`Phase1SatsumaCockpitRules.ApplyDashboardMeterLatch` corrects ON1/OFF0 to12/0.
Its guarded refresh validates the frozen manifest, the unique117-mount prefab,
the exact target asset/owner/accepted part, two6mm fasteners with unchanged IDs
and stages, and old/current group shape before changing one definition. The
same helper is hooked into the existing full builder; no full rebuild was run.
Prefab, manifest and build settings are not saved by this helper. The runtime
change consumes the resulting `FastenerGroup.IsBolted` through existing bindings.

## Runtime change

`SatsumaElectricalSystem.DashboardControlsAvailable` now evaluates the current
assembly graph through stable IDs:

1. `vehicle.satsuma.part.dashboard` is installed;
2. `mount.satsuma.dashboard.meters` is occupied;
3. that mount's `FastenerGroup.IsBolted` latch is true.

`SatsumaWiperController.CanOperateSwitch` delegates to that structural
predicate. `SatsumaWiperSwitchInteractionTarget.CanInteract` and its unavailable
prompt use the same value. The target therefore cannot mutate wiper mode while
the controls are absent or structurally unavailable.

The existing public `CycleMode()` API intentionally remains unchanged for save,
test and system callers. Structural availability selects the switch position;
actual sweep still uses the existing `WipersPowered` battery/wiring/voltage
predicate. Sweep, park, delays, presentation, wiring, physics and save schemas
are untouched.

## Compatibility decisions

Saved Bolted history remains compatible: a pre-existing true latch at aggregate
tightness 1..11 stays valid above OFF0. A fresh false latch must reach12 before
becoming true; after latching, loosening one or more stages keeps it true until
aggregate0. No DTO field, stable ID, prefab binding or schema changed.

The donor polls dashboard availability periodically (4s). This bounded remake
evaluates current assembly state when interaction availability is queried, so a
removed part cannot leave an actionable stale switch. This is an intentional
timing difference, not a power-model change.

## Automated coverage authored

`SatsumaDashboardControlsTests.cs` adds exactly eight EditMode tests:

1. fresh aggregate stages0..11 remain unavailable and stage12 enables controls;
2. the ON12/OFF0 latch survives one-stage loosening and disables at0;
3. a missing dashboard blocks a still-latched meter mount;
4. an installed dashboard does not need its own Bolted latch;
5. missing dashboard meters block controls;
6. a structurally available, electrically powerless switch changes mode but
   cannot start a sweep;
7. blocked target interaction leaves mode unchanged and reports unavailable;
8. direct `CycleMode()` remains independent of interaction availability.

The separate `SatsumaCockpitDashboardRulesTests` suite adds41 cases covering
the canonical authoring, helper idempotence/only-field mutation,15 rejected
drift shapes, hysteresis and actual assembly DTO restore. Historical true
latches at every stage1..11 are retained, fresh false latches at1..11 stay
false, and invalid true-at0 restores are rejected without state mutation.

## Changed files in this bounded packet

- `Assets/Game/Vehicle/Runtime/SatsumaElectricalSystem.cs`
- `Assets/Game/Vehicle/Runtime/SatsumaWiperController.cs`
- `Assets/Game/Vehicle/Runtime/SatsumaWiperSwitchInteractionTarget.cs`
- `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaDashboardControlsTests.cs`
- `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaDashboardControlsTests.cs.meta`
- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaCockpitRules.cs` and `.meta`
- one hook in `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaCockpitDashboardRulesTests.cs` and `.meta`
- ignored generated `MountDefinitions/mount.satsuma.dashboard.meters.asset`
- this report

## Verification

Root executed the guarded refresh: PID9592, exit0, changed1. Repeat PID24500,
exit0, changed0. Logs: `Logs/codex-cockpit-c1a-refresh-01.log` and `-02.log`.
Of120 protected hashes, only the intended meters definition changed; the other
116 mount definitions, prefab, manifest and build settings stayed byte-identical.
Unity serialized the existing newly-added empty assembly fields on that one
asset as well; no additional gameplay rule was populated. New target SHA256:
`4FEB48689970B3E3C6AF1099614FD8FFBC6708F2E6E8C916F00550916702C25B`.

Combined focused/regression EditMode: PID17164, exit0, **190/190 passed**,
failed0/skipped0,59.6200327s. New C1a41+8; existing generated39, electrical16,
assembly27, front latch-save12, removal23+5 and E1 engine19.
Result: `Logs/codex-cockpit-c1a-edit.xml`, SHA256
`D82BC6BC26E0B5E1FE5A21025FB63077D78FE5280753CEC14DC049D67D4AA3ED`.

PlayMode regression: PID10404, exit0, **31/31 passed**, failed0/skipped0,
33.7720603s (assembly11 and installed-part physics20).
Result: `Logs/codex-cockpit-c1a-play.xml`, SHA256
`DC89BD984F7A02298F21BA29982310E2FB6C19AF22674F0CE493119E756B4CE7`.
Both test runs used Unity6000.3.11f1 batchmode/nographics and the existing
fixture scenes, not a menu/world visual test. No C1a compile errors/warnings;
existing external warnings include TextureInventoryExporter CS0618 and
PhysicalServiceCatalogController CS0108.

Final120 hashes exactly match the post-refresh snapshot. Compared with the
pre-C1a snapshot, only the intended single definition differs. Protected SHA256:

- prefab: `7F4C06B701116F43428C54411C954085E4FA39DAD4736E4BBE1848E7B2F58CF6`;
- manifest: `3BF4C2535BFF9CE40520FE117220FD0AD1434765A8025AEA0847A14DD524B4DF`;
- build settings: `1849B8BE614E204FB0CF463CBCCC784380598D1BB4302E82AC3840F46CF2E454`.

Manual cockpit acceptance remains pending. The already delivered build
`024640` predates C1a and therefore does not contain this change; this packet is
currently in the Unity project only. A later player rebuild is required to
include it. No existing user save file was opened or written by these tests.

## Manual checklist and next packet

1. With dashboard missing, or meters installed but never latched, the wiper
   switch must not be actionable. Tighten its two6mm fasteners: aggregate11
   stays unavailable; aggregate12 (for example8+4) enables selection.
2. Loosen after latching: selection remains available at11 and1, disables at0.
   Dashboard presence matters, not the dashboard's own fastener latch.
3. Without electrical power the available switch may change position, but
   wipers must not move. With the existing complete electrical prerequisites,
   the already accepted sweep and parking behavior must remain unchanged.
4. Load an existing valid save with a historical partly-loosened/partly-tight
   Bolted latch: keep that history, with no spontaneous panel removal.

Next cockpit packet: finish the steering dependency audit before modifying its
prerequisite. Physical ignition still needs explicit key persistence and input
ownership design; do not enable the entire vehicle map from a physical click.
