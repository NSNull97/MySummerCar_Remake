# Engine fastener travel — bounded repair

Coordinated final validation: **799/799 EditMode, 74/74 PlayMode passed**;
native slot-01 was restored on a copy without rewriting user saves. Authoring
repeat changed0. Manual acceptance remains pending. The authorship-stage notes
below are supplemented by [the final packet report](SATSUMA_ENGINE_REPAIR_PACKET_2026-09-05.md).

Status: `ConfigurationTransferred / Reimplemented / SourceValidated`.
Unity prefab refresh and Unity tests remain the integration owner's responsibility.
This packet does not claim manual original-game capture or complete engine parity.

## Cause and scope

The generator flattens the donor BoltPM scale onto its visible mesh child.
Unity does not multiply a transform's localPosition by that same transform's
localScale. The former engine default therefore moved every child 20 mm at
stage 8, although most donor markers scale that displacement down.

The old E2a16 and Additional100 cohorts were rechecked, not generalized from
one representative. Exact scope is 115 staged targets: all E2a16 plus 99
Additional targets. Carburettor mixture adjuster marker52523 (Screw108773,
start Idle) has no mounting-stage position contract and remains excluded.
The eight previously identified leaked valve adjusters remain outside this
cohort. No counts, IDs, stages, inserted/seated flags, groups/latches, tooling,
definitions, saves, mounting poses, colliders, materials or meshes are changed.

The compatible rocker-cover retirement is supported explicitly:

- Old shape: 12 cover IDs; 115 active reviewed targets, 113 travel changes.
- Canonical shape: IDs 2,4,8,9,10,12 only; 109 active reviewed targets,
  107 changes. Retired mappings are 5→2,11→4,3→8,7→9,6→10,1→12.
- Partial/unknown target or definition sets fail closed. The source table below
  retains all115 historical donor mappings as evidence; retired aliases are
  not resurrected or mutated.

## Donor proof

Frozen GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

A bounded streaming scan validated all nine states0..8 of every reviewed
Screw FSM: action names, enabled flags, parameter offsets, float values,
None flags, Self spaces, one-shot execution and ThisBolt/marker ownership.
The installed source actions SetPosition.cs and SetRotation.cs were read
from the frozen external export.

For ordinary staged mounting screws:

- SetPosition sets the visible ThisBolt child's local Z to -0.0025 × stage.
- SetRotation sets the BoltPM marker's local Euler Z to 45° × stage.
- Position owner is ThisBolt, rotation owner is the marker. Changing Euler Z
  does not change that marker's forward axis, so the current project's
  marker-oriented scalar travel can reproduce the displacement without
  reparenting anything.
- Raw child Z=-0.02 is an initialized stage pose, not a base offset to add.
  Base position stays zero; otherwise travel would be counted twice.

Three explicit action-layout exceptions were verified:

1. Oilpan drain53276 / Screw108997 has a third ActivateGameObject action,
   on at stages0..5 and off at6..8. This does not change its travel.
2. Alternator clamp65277 / Screw112463 prefixes EnableFSM, enabled at0..7
   and disabled at8. Position and rotation actions remain enabled.
3. Hose clamp55496 / Screw109558 has SetPosition **disabled** in every stage
   (actionEnabled0001). It rotates in place; its travel scale must be **0**,
   not the mesh/marker scale0.65. The donor child scale0.8 affects mesh size,
   not translation; that separate mesh-size debt is preserved.

### Effective full-stage travel

| Target subgroup (historical cohort) | Count | Travel scale | Stage8 displacement |
| --- | ---: | ---: | ---: |
| Hose clamp, rotation only | 1 | 0 | 0 mm |
| Camshaft fixing bolts + clutch cover | 8 | 0.5 | 10 mm |
| Air filter, timing cover, drive gear, piston nuts | 23 | 0.6 | 12 mm |
| Distributor and alternator clamp screws | 2 | 0.65 | 13 mm |
| Ordinary 7 mm markers | 56 | 0.7 | 14 mm |
| 8 mm markers + drain13 + gearbox10 with Z0.8 | 16 | 0.8 | 16 mm |
| Main-bearing caps | 6 | 0.9 | 18 mm |
| Camshaft gear and alternator10 | 2 | 1 | 20 mm, unchanged |
| Crankshaft pulley | 1 | 1.1 | 22 mm |

**Rotation limitation:** this repair preserves the project's current base
rotation and child-spinning presentation. It corrects displacement along the
verified axis, not the donor marker's exact absolute hex-head angular phase.
That fixed phase difference is not the cause of excessive sinking. No
unproven rest-frame rewrite was mixed into this repair.

## Implementation and integration

New helper:
`Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineFastenerTravel.cs`.

- `ApplyReviewedTravel(root, dryRun: true)`: preflight and count only.
- `ApplyReviewedTravel(root)`: changes only each scoped target's serialized
  fastenerPresentationStageTravelScale. Does not Configure/initialize the graph.
- `ResolveActiveBindings(root)`: exact old/canonical cover membership.
- `ValidateDonorBinding(scene,binding)`: source check for an already parsed
  full builder scene.
- `ReadFrozenSource(path)`: SHA-locked bounded scan; stores115 small evidence
  records rather than the entire raw FSM scene.
- `RefreshEngineFastenerTravelBatch()`: source/manifest preflight, prefab
  load, target preflight, timestamped Logs backup, scoped save and idempotence.

Root-owned integration steps:

1. Full builder: validate all ReviewedBindings using the existing scene, then
   call ApplyReviewedTravel after mesh/canonical cover authoring. This is safe
   in either old12 or new6 cover shape; prefer after canonical retirement.
2. E2a mesh preservation guard currently requires travel1. Replace that one
   scalar check with IsLegacyOrReviewedScale(binding.FastenerId, actualScale).
   Preserve every other E2a shape/pose guard. Update E2a tests asserting scale1
   so a later mesh-only refresh accepts, but does not overwrite, reviewed travel.
3. Update Additional mesh cohort ownership separately as required by the cover
   retirement packet; this travel helper does not change that helper.
4. Execute the scoped refresh (not a full asset rebuild), then focused tests.

## Checks executed and still required

Executed:

- Isolated dotnet compilation of the new helper and tests against current local
  Unity/project assemblies: successful, zero C# errors. One local MSBuild
  BaseIntermediateOutputPath warning; this is not a Unity project warning.
- Direct invocation of the compiled ReadFrozenSource(path) outside Unity:
  **SOURCE_STAGE_VALIDATION_OK markers=115**, including all1035 stage states
  and the three explicit layout/enabled exceptions.
- Donor reads were streaming/read-only. No Unity process was launched and no
  generated prefab/definition was written by this agent.

New tests in `SatsumaEngineFastenerTravelTests.cs` cover cohort membership,
source axis equivalence, actual existing presentation formula at0/1/8/7/0,
dry-run purity, only-field changes, idempotence, six guard failures,
partial cover retirement rejection and generated applied state.

**Not run by this agent:** Unity EditMode tests, prefab refresh, runtime/manual
tightening. Root must run these plus accepted front/rear/body fastener and save
regressions. In-game check: ordinary bolts no longer disappear deeply into
parts; clamp does not translate; stages and removal thresholds behave as before.

## Per-target provenance

Scale below is the authored translation multiplier, not inferred wrench size.
The source IDs are editor-only provenance and never runtime lookup keys.

| Existing fastener ID | Donor marker | Donor Screw FSM | Travel scale |
| --- | ---: | ---: | ---: |
| fastener.satsuma.engine-block-oilpan.boltpm-1 | 40758 | 105412 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-2 | 44950 | 106615 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-3 | 53276 | 108997 | 0.80 |
| fastener.satsuma.engine-block-oilpan.boltpm-4 | 54874 | 109405 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-5 | 56771 | 109925 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-6 | 56810 | 109934 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-7 | 58044 | 110308 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-8 | 58670 | 110497 | 0.70 |
| fastener.satsuma.engine-block-oilpan.boltpm-9 | 61811 | 111370 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-1 | 36907 | 104337 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-2 | 38331 | 104763 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-3 | 42660 | 105955 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-4 | 44837 | 106563 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-5 | 51824 | 108590 | 0.80 |
| fastener.satsuma.engine-block-gearbox.boltpm-6 | 67087 | 113005 | 0.70 |
| fastener.satsuma.engine-block-gearbox.boltpm-7 | 68100 | 113315 | 0.70 |
| fastener.satsuma.camshaft-camshaft-gear.boltpm-1 | 37245 | 104426 | 1.00 |
| fastener.satsuma.carburetor-airfilter.boltpm-1 | 42310 | 105846 | 0.60 |
| fastener.satsuma.carburetor-airfilter.boltpm-2 | 66624 | 112875 | 0.60 |
| fastener.satsuma.crankshaft-crankshaft-pulley.boltpm-1 | 50893 | 108347 | 1.10 |
| fastener.satsuma.crankshaft-flywheel.boltpm-1 | 37879 | 104629 | 0.70 |
| fastener.satsuma.crankshaft-flywheel.boltpm-2 | 42749 | 105989 | 0.70 |
| fastener.satsuma.crankshaft-flywheel.boltpm-3 | 44627 | 106500 | 0.70 |
| fastener.satsuma.crankshaft-flywheel.boltpm-4 | 57156 | 110042 | 0.70 |
| fastener.satsuma.crankshaft-flywheel.boltpm-5 | 60650 | 111041 | 0.70 |
| fastener.satsuma.crankshaft-flywheel.boltpm-6 | 64986 | 112363 | 0.70 |
| fastener.satsuma.cylinder-head-carburetor.boltpm-1 | 48741 | 107742 | 0.80 |
| fastener.satsuma.cylinder-head-carburetor.boltpm-2 | 50419 | 108225 | 0.80 |
| fastener.satsuma.cylinder-head-carburetor.boltpm-4 | 60922 | 111110 | 0.80 |
| fastener.satsuma.cylinder-head-carburetor.boltpm-5 | 64079 | 112055 | 0.80 |
| fastener.satsuma.cylinder-head-headers.boltpm-1 | 36705 | 104286 | 0.80 |
| fastener.satsuma.cylinder-head-headers.boltpm-2 | 51075 | 108400 | 0.80 |
| fastener.satsuma.cylinder-head-headers.boltpm-3 | 55793 | 109661 | 0.80 |
| fastener.satsuma.cylinder-head-headers.boltpm-4 | 61870 | 111385 | 0.80 |
| fastener.satsuma.cylinder-head-headers.boltpm-5 | 69218 | 113605 | 0.80 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-1 | 39308 | 105044 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-2 | 41931 | 105751 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-3 | 43047 | 106083 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-4 | 43107 | 106095 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-5 | 51693 | 108552 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-6 | 53156 | 108967 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-7 | 63635 | 111903 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-8 | 65810 | 112610 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-9 | 67202 | 113041 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-10 | 70160 | 113881 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-11 | 70985 | 114087 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-cover.boltpm-12 | 71935 | 114348 | 0.70 |
| fastener.satsuma.cylinder-head-rocker-shaft.boltpm-2 | 40906 | 105458 | 0.80 |
| fastener.satsuma.cylinder-head-rocker-shaft.boltpm-5 | 44469 | 106458 | 0.80 |
| fastener.satsuma.cylinder-head-rocker-shaft.boltpm-6 | 45373 | 106740 | 0.80 |
| fastener.satsuma.cylinder-head-rocker-shaft.boltpm-8 | 56020 | 109708 | 0.80 |
| fastener.satsuma.cylinder-head-rocker-shaft.boltpm-9 | 56364 | 109809 | 0.80 |
| fastener.satsuma.engine-block-alternator.boltpm-1 | 36539 | 104229 | 0.70 |
| fastener.satsuma.engine-block-alternator.boltpm-2 | 62684 | 111589 | 1.00 |
| fastener.satsuma.engine-block-alternator.boltpm-3 | 65277 | 112463 | 0.65 |
| fastener.satsuma.engine-block-camshaft.boltpm-1 | 56110 | 109734 | 0.50 |
| fastener.satsuma.engine-block-camshaft.boltpm-2 | 63989 | 112016 | 0.50 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-1 | 46010 | 106955 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-2 | 48643 | 107708 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-3 | 53741 | 109113 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-4 | 57645 | 110180 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-5 | 58467 | 110428 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-6 | 64525 | 112201 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-7 | 64613 | 112236 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-8 | 64941 | 112330 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-9 | 65656 | 112569 | 0.70 |
| fastener.satsuma.engine-block-cylinder-head.boltpm-10 | 69091 | 113572 | 0.70 |
| fastener.satsuma.engine-block-distributor.boltpm-1 | 45067 | 106655 | 0.65 |
| fastener.satsuma.engine-block-fuel-pump.boltpm-1 | 36969 | 104358 | 0.70 |
| fastener.satsuma.engine-block-fuel-pump.boltpm-2 | 65212 | 112444 | 0.70 |
| fastener.satsuma.engine-block-main-bearing1.boltpm-1 | 37572 | 104541 | 0.90 |
| fastener.satsuma.engine-block-main-bearing1.boltpm-2 | 52423 | 108749 | 0.90 |
| fastener.satsuma.engine-block-main-bearing2.boltpm-1 | 42728 | 105984 | 0.90 |
| fastener.satsuma.engine-block-main-bearing2.boltpm-2 | 55653 | 109617 | 0.90 |
| fastener.satsuma.engine-block-main-bearing3.boltpm-1 | 48781 | 107755 | 0.90 |
| fastener.satsuma.engine-block-main-bearing3.boltpm-2 | 50879 | 108344 | 0.90 |
| fastener.satsuma.engine-block-piston1.boltpm-1 | 53477 | 109047 | 0.60 |
| fastener.satsuma.engine-block-piston1.boltpm-2 | 62925 | 111669 | 0.60 |
| fastener.satsuma.engine-block-piston2.boltpm-1 | 42160 | 105811 | 0.60 |
| fastener.satsuma.engine-block-piston2.boltpm-2 | 50548 | 108250 | 0.60 |
| fastener.satsuma.engine-block-piston3.boltpm-1 | 50929 | 108357 | 0.60 |
| fastener.satsuma.engine-block-piston3.boltpm-2 | 64131 | 112067 | 0.60 |
| fastener.satsuma.engine-block-piston4.boltpm-1 | 54612 | 109341 | 0.60 |
| fastener.satsuma.engine-block-piston4.boltpm-2 | 64515 | 112197 | 0.60 |
| fastener.satsuma.engine-block-radiator-hose2.boltpm-1 | 55496 | 109558 | 0.00 |
| fastener.satsuma.engine-block-timing-cover.boltpm-1 | 42707 | 105975 | 0.60 |
| fastener.satsuma.engine-block-timing-cover.boltpm-2 | 53009 | 108904 | 0.60 |
| fastener.satsuma.engine-block-timing-cover.boltpm-3 | 59658 | 110768 | 0.60 |
| fastener.satsuma.engine-block-timing-cover.boltpm-4 | 60350 | 110953 | 0.60 |
| fastener.satsuma.engine-block-timing-cover.boltpm-5 | 60458 | 110994 | 0.60 |
| fastener.satsuma.engine-block-timing-cover.boltpm-6 | 65489 | 112531 | 0.60 |
| fastener.satsuma.engine-plate-starter.boltpm-1 | 57844 | 110254 | 0.70 |
| fastener.satsuma.engine-plate-starter.boltpm-2 | 64601 | 112234 | 0.70 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-1 | 38244 | 104732 | 0.50 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-2 | 44385 | 106436 | 0.50 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-3 | 62237 | 111471 | 0.50 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-4 | 66988 | 112971 | 0.50 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-5 | 68060 | 113299 | 0.50 |
| fastener.satsuma.flywheel-clutch-cover-plate.boltpm-6 | 71498 | 114227 | 0.50 |
| fastener.satsuma.gearbox-drive-gear.boltpm-1 | 44873 | 106584 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-2 | 46343 | 107024 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-3 | 49676 | 108029 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-4 | 52342 | 108716 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-5 | 55118 | 109465 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-6 | 63334 | 111804 | 0.60 |
| fastener.satsuma.gearbox-drive-gear.boltpm-7 | 64000 | 112023 | 0.60 |
| fastener.satsuma.timing-cover-water-pump.boltpm-1 | 46855 | 107188 | 0.70 |
| fastener.satsuma.timing-cover-water-pump.boltpm-2 | 48447 | 107655 | 0.70 |
| fastener.satsuma.timing-cover-water-pump.boltpm-3 | 57782 | 110230 | 0.70 |
| fastener.satsuma.timing-cover-water-pump.boltpm-4 | 65052 | 112392 | 0.70 |
| fastener.satsuma.timing-cover-water-pump.boltpm-5 | 70154 | 113878 | 0.70 |
| fastener.satsuma.water-pump-water-pump-pulley.boltpm-1 | 41383 | 105591 | 0.70 |
| fastener.satsuma.water-pump-water-pump-pulley.boltpm-2 | 42659 | 105954 | 0.70 |
| fastener.satsuma.water-pump-water-pump-pulley.boltpm-3 | 59744 | 110786 | 0.70 |
| fastener.satsuma.water-pump-water-pump-pulley.boltpm-4 | 70151 | 113877 | 0.70 |
