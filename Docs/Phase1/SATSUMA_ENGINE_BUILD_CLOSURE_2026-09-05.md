# Engine assembly closure — 2026-09-05

Status: bounded repair packet implemented; final EditMode 657/657 and PlayMode
63/63 passed, zero failures/skips. Manual acceptance pending. The complete engine
objective remains open pending approval of the consumable compatibility extension
and the explicitly listed remaining assembly parity work.

## User scope

Build the engine completely outside the car and install the completed assembly
in the car. Preserve accepted suspension/body/physics and concurrent UI/player
work. Full combustion/start/run calibration is a subsequent scope, not implied
by this assembly milestone. Donor installation and user saves remain read-only.

## Reported blockers and plan

1. Review engine fastener mesh identity against the locked donor; keep count,
   stable IDs, pose and tightening state changes separately reviewable.
2. Inspect water-pump mount ownership, access and ray selection. Do not invent a
   block mount if the original requires the timing cover.
3. Audit camshaft-sprocket rotation/timing-mark interaction and implement the
   donor-evidenced assembly adjustment.
4. Restore pickup of loose block/pump/clutch subassemblies from their installed
   child surfaces. Use an explicit capability adapter; do not enable individual
   installed-child pickup or change suspension collision/physics.
5. Correct head-gasket/engine-plate installation and removal access guards.
6. Correct clutch order, complete all three constituent parts, then carry and
   install the complete clutch assembly.
7. Expose screwdriver and spark-plug tool in the existing toolbox and wire
   required engine fasteners. Ruler selection must not claim a missing measuring
   mechanic has been implemented.
8. Exercise the complete stock engine assembly/install flow, fastening and
   removal guards, plus accepted assembly/carry regression tests. Report any
   unverified steps separately; a green data test is not manual gameplay proof.

## Execution boundaries

Independent agents inspect/implement mount rules, bolt presentation and toolbox
bindings. Root owns subassembly pickup, integration, serialized refreshes and
Unity validation. Only one Unity process at a time; never run a full baseline
rebuild or touch unrelated generated assets as a convenience. Capture protected
asset/save hashes and backups before scoped refreshes.

## Initial diagnosis

`PartInstance.InstallAt` disables each installed child's `PhysicsPickupTarget`.
Its own ray host remains selectable through an installed-part proxy, without a
pickup route to its outer loose owner. More attached children therefore hide the
remaining block/pump pickup surface. A new opt-in adapter follows authoritative
`AssemblyOwnedMountAuthoring` ownership; child's RMB removal remains separate.

## Integrated bounded repair packet

All comparisons use the frozen `GAME.unity` extraction, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
No donor FSM/controller/runtime was imported. Classification is
`BehavioralReference`, `ConfigurationTransferred` and project-owned
`Reimplemented`; reused presentation remains `TemporaryDirectImport`.

| Reported problem | Change and evidence boundary |
| --- | --- |
| Nuts everywhere | Reviewed 100 additional authored engine targets: corrected 75 mesh identities (71 bolts, four slotted screws), retained 25 genuine nuts. Earlier 16 oilpan/gearbox mesh corrections remain intact. Counts, IDs, stages and travel were not silently changed. |
| Water-pump target | Retained the original timing-cover-owned mount and its authored pose. An opt-in parent-occlusion adapter now recognizes the authoritative outer loose engine owner; no invented block socket or global ray-distance increase. |
| Camshaft gear timing | After the 10 mm bolt reaches stage eight, another positive turn rotates the mesh by donor local-X +5 degrees while the timing chain is absent. Reverse input loosens the bolt. Gear group ON threshold corrected to five. Optional typed timing save state follows the existing alignment-extension pattern. |
| Block, pump and clutch pickup | Explicit engine-only pickup surfaces delegate to the outer loose assembly body/identity. Installed children retain their own removal action. The captured pickup stays pinned through carry/save/handoff; installed engine surfaces cannot lift the car. |
| Gasket and engine-plate access | Gasket is blocked by the head; plate installation is blocked by gearbox/flywheel and removal by flywheel. Exact donor predicates are in the compound report. |
| Three-part clutch | Pressure plate enters the loose cover first, then disc. Cover removal retains the two owned children. Removed four erroneous generic dependency edges, including the reversed timing-chain/cover pair. |
| Toolbox | Added explicit screwdriver, spark-plug wrench and ruler selection to the existing case. Ruler honestly reports measurement unavailable. Spark-plug tool selection is not a claim that plug installation exists. |
| Actual screwdriver clamps | Alternator, distributor and radiator-hose2 slotted mounting screws use typed Screwdriver rather than a 6 mm wrench. Original tool scale .65 and target comparison support these three bindings; the carburettor tuning screw is deliberately excluded. |
| Engine into/out of car | Installation requires gearbox, oilpan and subframe present. Removing the engine retains 20 explicitly listed internal owned mounts; external clutch-line/halfshaft/gear-linkage/exhaust checks remain separate. |

Detailed source and behavioral notes:

- `SATSUMA_ENGINE_COMPOUND_ASSEMBLY_2026-09-05.md`
- `SATSUMA_CAMSHAFT_TIMING_AND_TOOLBOX_2026-09-05.md`
- `SATSUMA_ENGINE_SCREWDRIVER_BINDINGS_2026-09-05.md`
- `SATSUMA_ENGINE_CONSUMABLE_BRIDGE_PROPOSAL_2026-09-05.md`

## Files and compatibility

Project-owned runtime additions: `AssemblySubassemblyPickupTarget`,
`AssemblyLooseOwnerMountOcclusionTarget`, `AssemblyCamshaftTimingState`,
`IAssemblyFastenerPostTighteningAction`, `SatsumaAuxiliaryAssemblyTools`, and
`SpannerSetAuxiliaryTools`. Existing graph/controller/mount definitions gain
empty-by-default retained-child removal rules and optional timing data; existing
fastener targets gain a null-by-default post-tightening action. Existing IDs,
base part roster, native save version and accepted suspension physics remain.

Editor additions: `Phase1SatsumaEnginePickupAuthoring`,
`Phase1SatsumaEngineCompoundAssemblyRules`,
`Phase1SatsumaEngineAdditionalFastenerPresentation`,
`Phase1SatsumaCamshaftTimingAuthoring`, and
`Phase1SatsumaEngineScrewdriverAuthoring`. The existing full builder calls these
same bounded rules and preserves its active staging paths; it was **not run**.
Existing item presentation authoring/validator and toolbox tests were extended.
The front-rule refresh guard recognizes both complete old engine dependency
shape (40/34 edges) and complete migrated shape (36/30), rejecting partial
four-edge migration rather than accepting arbitrary counts.

New tests cover compound dependencies, retained engine/clutch children,
pickup/carry/save/handoff, pump ray occlusion, authoring repair/idempotence,
additional mesh identities, screwdriver bindings, timing and auxiliary tools.
Existing generated-content fixtures now satisfy the real engine prerequisites
and explicitly assert removal of the four incorrect dependency edges.

## Scoped generated changes and protection

Recoverable pre-refresh copies are under
`Logs/engine-closure-before-20260905-day/`: car prefab, toolbox prefab and all
117 mount definitions with metadata. Refresh helpers also make per-run prefab
backups where applicable. No full car, item catalog, world or streaming rebuild
was performed. Generated donor-derived assets remain ignored outside Git.

- Car prefab: 75 mesh-filter references, 76 engine pickup/occlusion components
  and their host capabilities, timing component/action, three screwdriver target
  references, tool registry and removal of four erroneous dependencies.
- Eight mount definitions changed: timing chain, camshaft gear, clutch disc,
  clutch pressure plate, engine assembly, engine plate, head gasket, clutch cover.
- Exactly three of 560 protected fastener-definition/metadata files changed:
  the three explicit screwdriver tool rules. Other 557 were unchanged.
- Added one screwdriver tool asset; added auxiliary kinds to the existing case.
- Independent existing-prefab-record comparison found no changes to Transform,
  Rigidbody, renderer or collider records and no removed records. No physics,
  pivot or collision rebuild was used to solve interaction access.
- All six pre-existing user-save files were hash-compared and left untouched.

## Executed validation

Hidden Unity batch mode, `-nographics -job-worker-count 2`, one Unity process at
a time. Commands used `-projectPath` for this project and `-runTests
-testPlatform EditMode|PlayMode -testFilter ... -testResults ... -logFile ...`.
The full fixture filter is recorded in each corresponding log.

| Artifact under `Logs/` | Executed result |
| --- | --- |
| `codex-engine-closure-small-01.xml` | 20/20 EditMode passed. |
| `codex-engine-closure-focused-01.xml` | 151/151 EditMode passed. |
| `codex-engine-closure-broad-01.xml` | 647/651 passed; four failures, no skips. Three stale regression assumptions and one real pickup-authoring repair/idempotence defect. Fixed fixture prerequisites, exact dependency migration guard/assertions and explicit binding change detection. |
| `codex-engine-closure-broad-02.xml` | 651/651 passed; zero failures/skips, before the subsequent chassis-mass consumer follow-up. |
| `codex-engine-closure-play-01.xml` | 63/63 PlayMode fixtures passed; zero failures/skips, before that mass follow-up. |
| `codex-engine-mass-ownership-01.xml` | 40/40 passed after the mass correction: six mass/impulse cases, eight pickup cases and 26 compound cases. |
| `codex-engine-closure-final-edit.xml` | Final source/content, including chassis-mass correction: 657/657 passed; zero failures/skips. |
| `codex-engine-closure-final-play.xml` | Final source/content: 63/63 physical fixtures passed; zero failures/skips. |

Scoped `-executeMethod ... -quit` runs, all exit zero:

- `Phase1SatsumaEngineAdditionalFastenerPresentation.RefreshAdditionalEngineFastenerMeshesBatch`:
  `codex-engine-closure-refresh-0-01.log`, 75 changed mesh filters/100 reviewed.
- `Phase1SatsumaEngineCompoundAssemblyRules.RefreshEngineCompoundAssemblyRulesBatch`:
  `codex-engine-closure-refresh-1-01.log`, seven changed mount definitions/four removed edges.
- `MSC.Items.Editor.ItemLegacyPresentationPipeline.RefreshSpannerSetAuxiliaryToolBindingsOnly`:
  `codex-engine-closure-refresh-2-01.log`, one prefab/three auxiliary kinds.
- `Phase1SatsumaEnginePickupAuthoring.RefreshEnginePickupBatch`:
  `codex-engine-pickup-refresh-01.log`, 76 adapters and their capability bindings.
- `Phase1SatsumaCamshaftTimingAuthoring.RefreshCamshaftTimingBatch`:
  `codex-engine-timing-refresh-01.log`, three binding changes/one threshold change.
- `Phase1SatsumaEngineScrewdriverAuthoring.RefreshEngineScrewdriverBatch`:
  `codex-engine-screwdriver-refresh-01.log`, seven changes across definitions,
  targets and tool registry.

Unless fully qualified above, command classes use namespace
`MSC.LegacyImport.Editor.GameplayPresentation`. No manual gameplay or standalone
build test is implied by these headless fixture results.

All six scoped entry points were then executed again in the same order, logs
`codex-engine-closure-repeat-0.log` through `-5.log`. All exited zero and
reported **zero changes** (meshes, mount definitions/dependencies, case kinds,
pickup bindings, timing bindings/threshold and screwdriver bindings). This is
executed canonical-content idempotence, not only a mocked authoring assertion.

Final SHA-256 test records:

- `codex-engine-closure-final-edit.xml`:
  `A3DE50324C3D805C3B99CB4A89D858892CA5C16DAC32F4541F0DA46E9102089B`
- `codex-engine-closure-final-play.xml`:
  `8499103761F6C7321DDDCE8E7A96808DB9D78EB696584B84D8E5CC6BCFBDC7F0`

No `error CS...` or `warning CS...` matches were found in either final log.
Final protected-file recheck confirmed the same two prefab hashes below, exactly
the same eight mount-definition changes and three of 560 fastener files changed.
All six pre-existing user saves remained hash-identical. Unity exited; no editor
or batch process was left running. No standalone build was produced in this task.

Earlier test and final unchanged generated-content SHA-256 records:

- `codex-engine-closure-broad-02.xml`:
  `E5AB141CDD80B2C2A2A0942B04DAC1037EB733841FABC6A58E0B48D1216B81E5`
- `codex-engine-closure-play-01.xml`:
  `953886423B64F2B2E31EDC7A282A7C90A2F9B69AA90294487E21A7B6ABC552BB`
- `codex-engine-mass-ownership-01.xml`:
  `D999F101ECC85B50A04E8BFACE442C7369AFA069ABBE2328513E1C4AC41B939A`
- Generated car prefab:
  `EF4028B10E43A62DED13EFE166D4A1BD5D303DC22B0C25C648824E0FDD23EC24`
- Generated toolbox prefab:
  `E01BBC63E14D463C6714D181D0965AE5AA22C21894C1FD9CEF070EC56A37C359`

## Manual acceptance checklist

Use the current Unity project; a previously built executable does not contain
these changes. No manual gameplay acceptance is claimed by automated tests.

1. Pick up the loose block from several installed child surfaces, including a
   nearly assembled engine. Drop and pick it up again; install the whole body,
   not the child used as a pickup surface.
2. Install timing cover, then water pump and pulley. Locate the pump socket on
   the loose cover and on the cover installed in the block. Pick up the loose
   pump through its pulley; once mounted to the block, carry the whole engine.
3. Assemble cover + pressure plate + disc; carry and install the complete clutch.
   Check gasket/plate and chain/cover access in both assembly directions.
4. Tighten the camshaft bolt with 10 mm, then advance the mark in +5-degree
   steps. Installed chain must prevent further mark adjustment. Reverse input
   must loosen the bolt. Check installed timing after save/load.
5. Select the new toolbox tools. Screwdriver operates the three listed clamps;
   a 6 mm wrench must not. Ruler and unbound spark-plug wrench must not imply
   working measurement or spark-plug assembly.
6. With subframe, gearbox and oilpan present, install the engine on its three
   11 mm car fasteners. After permitted external disconnection and loosening,
   remove the engine with its internal assembly intact.

## Still open: complete engine objective

This packet does **not** complete the user's full engine objective. Four spark
plugs and alternator belt have no assembly mounts; purchased oil filter and the
starting assembly filter use different runtime identities. Existing item
materialization, fixed assembly roster and cross-domain save/streaming ownership
cannot safely be connected by merely adding components. Per AGENTS 3.2 rule 7,
the compatible dynamic-part extension is proposed separately and awaits user
approval before changing that foundation.

Other explicitly unclosed parity debt: cover duplicate fastener ownership,
rocker-shaft mounting versus valve adjusters, remaining group thresholds/stage
travel, filter hand-tightening, and stock exhaust/muffler fasteners. In
particular the new exhaust Bolted removal predicate currently cannot become
true because that mount still has no fasteners; do not disguise it as an
occupancy check. Hoist operation, fluids and engine combustion are not verified.

Exactly one recommended next milestone: approve and implement the bounded
consumable-to-existing-assembly bridge in the linked proposal, with explicit old
save migration and streaming ownership tests; then rerun the complete stock
engine build/install acceptance route.

## Additional review finding: loose engine affected chassis mass

`AssemblyChassisMassController` previously treated every installed kinematic
part as a contribution to the chassis. Installation is a local socket state:
children of an engine on the floor are installed too. Their remote world
positions consequently shifted the car's calculated center of mass. The same
consumer applied moving-part installation impulses to the chassis even when
the installation happened on the floor. Retaining internal children during
engine removal made this pre-existing assumption especially visible.

The bounded correction follows occupied graph sockets and explicit
`AssemblyOwnedMountAuthoring` owners until the actual chassis root is reached.
Legacy directly chassis-owned sockets retain their controller-bound pose owner
resolution. Loose, missing and cyclic paths fail closed; physically jointed
parts keep their existing independent body mass. No graph schema, physics-link
parameters, prefab collision or save migration changed for this correction.

Mass and center are recomputed for the whole chain on block installation and
removal; children's own installed flags need not change. The installation
impulse uses the same chassis-reachability predicate. Six tests in
`AssemblyChassisMassOwnershipTests` cover floor mass/center, complete block
installation and retained removal, floor impulse/torque isolation, ordinary
chassis transfer, jointed-body mass and a broken explicit owner binding.
The focused post-correction run passed 40/40, including all six new consumer
tests; final broad/PlayMode regressions are recorded in the validation section.
