# Testing and Validation

## Honesty rule

Do not state that Unity compiled or tests passed unless the command was actually run and its exit code/output was inspected.

## EditMode tests

Prioritize:

- path normalization;
- manifest serialization;
- hash comparison;
- stable-ID uniqueness;
- import planning idempotency;
- assembly graph rules;
- drivetrain calculations;
- save serialization and migrations;
- weather interpolation;
- unit conversion.

Milestone 1 command-line run (PowerShell; use the Editor path from ignored local configuration):

```powershell
Unity.exe -batchmode -nographics -projectPath <project> -buildTarget Win64 -runTests -testPlatform EditMode -testResults <results.xml> -logFile <tests.log>
```

The current foundation suite covers stable-ID format/uniqueness/duplicate detection, prevention of implicit ID creation, path configuration parsing/normalization, runtime-to-Editor assembly boundaries, Bootstrap build order, scene contents, and serialized HDRP baseline settings.

Milestone 2 adds EditMode coverage for manifest JSON round trips, canonical SHA-256 and file hashing, staging/asset path containment, configured-root separation, plan/execute/plan idempotency, hash mismatch blocking, invalid destinations, missing provenance, production-prefab donor dependencies, and ledger upsert idempotency/CSV escaping.

Milestone 2 final run: **30 total, 30 passed, 0 failed, 0 skipped**. Eighteen expanded test cases belong to the LegacyImport/M2 suite; the remaining twelve are the Milestone 1 foundation suite. Results are stored in ignored `Logs/Milestone02_EditModeResults.xml`.

Milestone 3 adds nine EditMode tests for generated scene/content presence, roof bounds and pivot tolerance, material texture assignments, LOD/collider coverage, production dependency isolation, lighting profiles, build-scene order and static budget capture. Final run: **39 total, 39 passed, 0 failed, 0 skipped**. XML and log outputs are stored under ignored Milestone 3 test/log paths.

Milestone 4 adds six EditMode tests covering pickup/drop physics restoration, destroyed-target loss, invalid collider rejection, mount handoff, carried-object stable-ID JSON snapshot and explicit tool activation. Final full EditMode run: **45 total, 45 passed, 0 failed, 0 skipped**. Results: ignored `TestResults/Milestone04_EditMode_Final.xml` and `Logs/Milestone04_EditMode_Final.log`.

The approved post-M4 review fixes add three EditMode tests for partial composition bindings and explicit pointer/rate look units. A fresh isolated-project run completed **48 total, 48 passed, 0 failed, 0 skipped**. Results were copied to ignored `TestResults/99A_EditMode_Final.xml` and `Logs/99A_EditMode_Final.log`. The isolated copy was required because the main project was already open in Unity; it used the same Unity/package versions and the exact `Assets/Game` fix overlay.

## PlayMode tests

Prioritize:

- bootstrap scene loads;
- required services resolve;
- pickup/drop;
- mount/tighten/unmount;
- engine start/stall;
- basic drive and brake;
- save/load round trip;
- weather transition;
- world-cell load/unload later.

Milestone 4 adds the first PlayMode scene-boot test. It loads `PlayerInteractionPrototype.unity` additively, validates the configured Input System player/camera and at least two stable-ID pickup targets, then unloads the scene. Final run: **1 total, 1 passed, 0 failed, 0 skipped**. Results: ignored `TestResults/Milestone04_PlayMode_Final.xml` and `Logs/Milestone04_PlayMode_Final.log`.

The approved post-M4 review fixes add four PlayMode flows: real ray/controller pickup-to-mount handoff through an occluding held body, carry cleanup on owner destruction, carry cleanup/collision restoration on component disable and motor-intent reset on input-router disable. A fresh run completed **5 total, 5 passed, 0 failed, 0 skipped**. Results: ignored `TestResults/99A_PlayMode_Final.xml` and `Logs/99A_PlayMode_Final.log`.

The same validation copy passed Foundation, M3 Garage, M4 Player/Interaction and Donor Pipeline batch validators. The donor validator reported zero warnings. Validator logs use the ignored `Logs/99A_Validator_*.log` paths.

## Editor validation

Build a validation window or command that checks:

- duplicate or missing stable IDs;
- missing serialized references;
- invalid donor paths;
- missing provenance records;
- donor reference assets used by production prefabs;
- Editor assembly references from runtime;
- missing meshes/materials;
- invalid layers/tags/settings;
- build scenes and bootstrap configuration.

Implemented foundation menu commands:

- `Tools > My Summer Car > Validation > Run Foundation Validation`;
- `Tools > My Summer Car > Foundation > Create Missing Bootstrap Content`;
- batch entry point `MSC.Editor.Foundation.FoundationValidationRunner.RunBatch`.

Implemented Milestone 2 commands:

- `Tools > My Summer Car > Donor Pipeline > Plan Manifest...`;
- `Tools > My Summer Car > Donor Pipeline > Execute Last Plan`;
- `Tools > My Summer Car > Donor Pipeline > Validate Project`;
- `Tools > My Summer Car > Donor Pipeline > Build Comparison Scene`;
- batch entry point `MSC.LegacyImport.Editor.Validation.DonorPipelineValidationRunner.RunBatch`.

Implemented Milestone 3 commands:

- `Tools > My Summer Car > Milestone 3 > Build Garage Art Prototype`;
- `Tools > My Summer Car > Milestone 3 > Validate Garage Art Prototype`;
- `Tools > My Summer Car > Milestone 3 > Build 1080p Performance Player`;
- batch entry points `MSC.Editor.GaragePrototype.GaragePrototypeAssetBuilder.RunBatch`, `MSC.Editor.GaragePrototype.GaragePrototypeValidator.RunBatch` and `MSC.Editor.GaragePrototype.GaragePrototypePerformanceBuild.RunBatch`.

Implemented Milestone 4 commands:

- `Tools > My Summer Car > Milestone 4 > Build Player Interaction Prototype`;
- `Tools > My Summer Car > Milestone 4 > Validate Player Interaction Prototype`;
- batch entry points `MSC.Editor.PlayerInteraction.PlayerInteractionPrototypeBuilder.RunBatch` and `MSC.Editor.PlayerInteraction.PlayerInteractionPrototypeValidator.RunBatch`.

The foundation validator checks stable IDs in every scene/prefab under `Assets/Game` and unsaved loaded scenes, runtime-to-Editor assembly references (including GUID-form references), ignored local path configuration/project-root agreement, and Bootstrap asset presence. The Milestone 2 validator additionally checks donor-root separation, manifest/registry records, staged and imported hashes, missing provenance, controlled replacement proof, production-prefab leakage, and enabled build scenes. The Milestone 3 validator checks scale/pivot tolerance, production/reference dependency separation, required meshes/materials/maps, LOD/collision coverage, lighting presets, build-scene ordering and static content budgets. The Milestone 4 validator checks required actions/bindings, player prefab wiring, prototype scene boot content, stable pickup IDs, explicit capability hosts, required contextual/tool/mount targets and build order. A build guard runs the release-safety subset before a player build. Save application and vehicle assembly references remain work for their owning milestones.

## Comparison fixtures

For donor-derived behavior, store:

- input conditions;
- expected range;
- tolerance;
- donor version/hash;
- capture date;
- known uncertainty.

## Reports

Each milestone ends with `Templates/MILESTONE_REPORT.md` populated under `Docs/Milestones/`.

## Milestone 04A validation update

Milestone 04A adds six EditMode tests for durable JSON round trip, coordinate conversion/round trip, canonical unique stable IDs, external staging-manifest SHA-256 and envelope round trip, explicit blocked terrain transfer, and the complete 04A validator. The fresh main-project run completed **54 total, 54 passed, 0 failed, 0 skipped**. Results: ignored `TestResults/Milestone04A_EditMode.xml` and `Logs/Milestone04A_EditMode.log`.

Milestone 04A did not add a production/build scene. The existing full PlayMode regression suite was nevertheless rerun to protect Player/Interaction integration: **5 total, 5 passed, 0 failed, 0 skipped**. Results: ignored `TestResults/Milestone04A_PlayMode.xml` and `Logs/Milestone04A_PlayMode.log`.

Implemented commands:

- `Tools > My Summer Car > Milestone 04A > Build ReferenceOnly Comparison Scene`;
- `Tools > My Summer Car > Milestone 04A > Validate World Layout Pilot`;
- batch entry points `MSC.Editor.WorldLayoutPilot.WorldLayoutPilotComparisonSceneBuilder.RunBatch` and `MSC.Editor.WorldLayoutPilot.WorldLayoutPilotValidator.RunBatch`.

The 04A validator checks the bounded allow-list, exact sample sequence, provenance, coordinate/measurement tolerance, external manifest hash, configured-root separation, unique stable IDs, comparison-scene exclusion and production/build-scene reference leaks.

## Milestone 04B validation update

Milestone 04B/04B.4 has 14 EditMode tests covering the required categories: database validation/serialization, schema migration, stable IDs, duplicates, unit conversion, coordinate spaces, source/evidence provenance, confidence, derived dependencies, measured-versus-tuned separation, missing P0/P1 reporting, fixture loading and explicit coverage of both rear-drum M05 assembly gates.

The main-project Unity `6000.3.11f1` runs completed on 2026-07-14:

- M04B batch validator: passed, `42` records, `22` non-covered P0 and `6` non-covered P1 requirements;
- full EditMode regression: **80 total, 80 passed, 0 failed, 0 skipped**;
- full PlayMode regression: **9 total, 9 passed, 0 failed, 0 skipped**.

Ignored outputs: `Logs/Milestone04B_Validator.log`, `Logs/Milestone04B_EditMode.log`, `Logs/Milestone04B_PlayMode.log`, `TestResults/Milestone04B_EditMode.xml` and `TestResults/Milestone04B_PlayMode.xml`.

Implemented commands:

- `Tools > MSC Remake > Reference Capture`;
- `Tools > MSC Remake > Reference Capture > Validate Database`;
- batch entry point `MSC.Editor.ReferenceCapture.ReferenceCaptureValidationRunner.RunBatch`.

The validator treats missing P0/P1 as an explicit capture queue rather than a structural failure. It fails on invalid/duplicate IDs, unresolved source/evidence/derived references, missing units/coordinate spaces, invalid tolerance/confidence, tuning leakage, unknown fixture records or missing required documentation.

Dataset `04B.2` baseline supplemental run on 2026-07-14:

- static source SHA-256 recheck: `GAME.unity` remains `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- JSON parse, fixture version consistency and CSV width checks: passed;
- final M04B validator: passed, `47` records, `22` non-covered P0 and `6` non-covered P1; no new reference-schema warnings;
- full EditMode regression: **81 total, 81 passed, 0 failed, 0 skipped**;
- full PlayMode regression: **9 total, 9 passed, 0 failed, 0 skipped**;
- `git diff --check`: passed.

Ignored outputs: `Logs/Milestone04B2_Validator_Final.log`, `Logs/Milestone04B2_EditMode.log`, `Logs/Milestone04B2_PlayMode.log`, `TestResults/Milestone04B2_EditMode.xml` and `TestResults/Milestone04B2_PlayMode.xml`.

Dataset `04B.3` diagnostic-video review run on 2026-07-14:

- external video SHA-256: `adffd52e9dce4171c30b969caf1102bd7ef8af9f0686ce934f2ea0ae675689af`;
- external static `GAME.unity` recheck: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- JSON parse, 14 fixture/database version checks and `git diff --check`: passed;
- M04B batch validator: passed, `49` records, `22` non-covered P0 and `6` non-covered P1;
- filtered ReferenceCapture EditMode suite: **14/14 passed**;
- full PlayMode regression: **9/9 passed**;
- full EditMode regression: **81 total, 80 passed, 1 failed**. The unrelated `WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan` detects that the freshly reinstalled donor `sharedassets3.assets` and `sharedassets3.resource` hashes differ from the frozen 04A1 provenance. The 04A1 database was not rewritten without a new audited extraction.

Ignored outputs: `Logs/Milestone04B3_Validator.log`, `Logs/Milestone04B3_EditMode.log`, `Logs/Milestone04B3_ReferenceCapture_EditMode.log`, `Logs/Milestone04B3_PlayMode.log` and matching `TestResults/Milestone04B3_*.xml`.

Dataset `04B.4` runtime-repetition and blocked-removal evidence run on 2026-07-14:

- external repetition video SHA-256: `86ad948bda1450fb8d2cf32583b51d0c2bc55ccef5aad9f428da3bc38e8e3c84`;
- 14 JSON files parse and report dataset `04B.4`; project evidence hashes and CSV parsing checks pass;
- M04B batch validator: passed, `51` records, `20` non-covered P0 and `6` non-covered P1;
- filtered ReferenceCapture EditMode suite: **14/14 passed**;
- full PlayMode regression: **9/9 passed**;
- full EditMode regression: **81 total, 80 passed, 1 failed**. The same unrelated `WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan` reports the frozen 04A1 versus reinstalled donor hash drift for `sharedassets3.assets` and `sharedassets3.resource`;
- `git diff --check`: passed apart from Git's informational CRLF-to-LF warning for `PORTING_LEDGER.csv`.

Ignored outputs: `Logs/Milestone04B4_Validator.log`, `Logs/Milestone04B4_ReferenceCapture_EditMode.log`, `Logs/Milestone04B4_PlayMode.log`, `Logs/Milestone04B4_EditMode.log` and matching `TestResults/Milestone04B4_*.xml`.

## Milestone 05 validation update

Implemented commands:

- `Tools > MSC Remake > Vehicle Assembly > Build Representative Test Vehicle`;
- `Tools > MSC Remake > Vehicle Assembly > Validate Definitions and Prototype`;
- `Tools > MSC Remake > Vehicle Assembly > Dependency Graph`;
- `Tools > MSC Remake > Vehicle Assembly > Export Validation Report`;
- `Tools > MSC Remake > Vehicle Assembly > Run Performance Audit`;
- batch entry points `VehicleAssemblyPrototypeBuilder.RunBatch`, `VehicleAssemblyPrototypeValidator.RunBatch` and `VehicleAssemblyPerformanceAudit.RunBatch`.

The static validator checks required assets, one assembly root, 12–20 representative parts, mount/fastener ownership, compatible tools, unique mount/stable IDs, dependency cycles, M4 capability wiring, exact rear-drum fixture semantics, Build Settings and absence of `ReferenceOnly`/`DonorGenerated` dependencies.

Focused Unity `6000.3.11f1` results from 2026-07-14:

- M05 builder: passed, 15 parts and 14 mounts generated;
- M05 static validator: passed;
- M05 EditMode: **22/22 passed**;
- M05 PlayMode: **8/8 passed**;
- performance audit: 10 000 deterministic mount queries, 0 managed bytes, 0 graph mutations, about 26.2 microseconds/query in Editor batch mode.

Full regressions: PlayMode **17/17 passed**; EditMode **102/103 passed**. The single failure is the already-known unrelated 04A1 hash drift against the reinstalled donor; M05 does not rewrite the frozen provenance.

## Post-M05 Crossdot baseline update

The shared first-person player now has a presentation-only centered `CrossdotPresenter`. Because the main project was open in Unity, the reproducible builder and tests were executed against an isolated copy containing the exact updated `Assets`, `Packages` and `ProjectSettings` inputs.

Results from 2026-07-14:

- M4 player builder `1.1.0`: passed;
- M4 Player/Interaction validator: passed;
- M05 Vehicle Assembly validator: passed against the updated shared player prefab; its builder, validator and scene-boot test now explicitly require `CrossdotPresenter`;
- focused Player/Interaction EditMode: **8/8 passed**;
- focused Player/Interaction PlayMode: **5/5 passed**.

The working prefab YAML was also copied into the isolated project after builder validation and passed both M4 and M05 validators. A manual Game View check remains required for subjective dot size and contrast.

## Post-M05 installed-part scale correction

The original M05 prototype parented `MountPose` below a `0.13`-scale debug cube. `PartInstance.InstallAt` used local-space reparenting, so an installed part inherited that scale and appeared to disappear. Runtime installation now preserves world scale, while Vehicle Assembly builder `1.1.0` keeps logical mount transforms at unit scale and places marker geometry in a separate child.

Verification on 2026-07-14 used the exact current scene before rebuilding and then a freshly generated scene:

- current `0.13`-scale scene, focused M05 PlayMode: **8/8 passed**;
- builder `1.1.0`: passed, 15 parts / 14 unit-scale logical mounts;
- rebuilt-scene M05 validator: passed;
- rebuilt-scene focused M05 PlayMode: **8/8 passed**;
- regression assertion confirms active renderers and unchanged world scale after carry handoff/install.

## Milestone 05A world-remaster validation

Unity `6000.3.11f1` results from 2026-07-14:

- production builder: passed, version `05A.1`, pilot `cell_0_-3`, 24 direct bindings;
- production validator: passed with 0 errors and 0 warnings;
- focused World Remaster EditMode: **13/13 passed**;
- focused World Remaster PlayMode: **5/5 passed**;
- M4 Player/Interaction validator: passed;
- M05 Vehicle Assembly validator: passed against the integrated pilot;
- full PlayMode regression: **22/22 passed**;
- full EditMode regression: **117 total, 114 passed, 3 failed**.

The three full-EditMode failures are reported rather than hidden:

1. `LightingPreset_UsesPhysicalSkyFogFixedExposureAndAces` and `Validator_ReportsNoMilestoneThreeErrors` observe the retained working-tree edit in `M3_NeutralVolume.asset`: sky type is `1` while the frozen M3 contract expects Physical Sky type `4`. 05A did not overwrite that existing edit. The dark 05A baseline capture is consistent with a pending lighting review.
2. `WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan` reports the already-known frozen 04A1 provenance mismatch for the reinstalled donor `sharedassets3.assets` and `.resource`. No historical provenance was rewritten.

Determinism and data checks:

- two consecutive production-cell builds produced SHA-256 `C0FC69B0C21FE86C1C8435BAE7D47FB3A1A964327DCEFE28DA9AFB773CAFDD52`;
- ledger/backlog/zone SHA-256 values are `558A10FA485C753357CD2A308B3EA5DB11D790DCC10349EBF5F6FBE87CACB500`, `B4E17025222CEFCA691264B6A592318B52CF9151FD9FF7401E6040FE85E5330D`, `5C1CA13C69554FB9290492E1A08B669B95A2C9A873616DC61D79B4B36806947F`;
- all 13,509 ledger rows parse; all 13,485 unassigned rows have a manual-art dependency; 263 backlog tasks and 51 zone rows parse;
- dependency validator found no donor/reference content reachable from production assets;
- `git diff --check` passed (apart from the existing informational CRLF/LF warning for `PORTING_LEDGER.csv`).

Visual capture generated three 1920 × 1080 modes plus a manifest under ignored `PerformanceCaptures/Milestone05A/`. They were inspected for mode separation; final art, lighting and gameplay traversal approval remains manual.

## Milestone 05B strict world-validation update

Unity `6000.3.11f1` results from 2026-07-15:

- 05A production-cell validator: PASS, zero warnings;
- 05C geometry validator: PASS, 3,842 eligible records, 49 cells, 2,784 actual
  meshes and 1,058 bounds fallbacks;
- 05C1 continuous-ground validator: PASS, two pieces, 26 seam pairs and 100/100
  collision probes;
- 05B world validator/export: completed with code 0 and marker
  `WORLD_VALIDATION_05B_COMPLETED achieved=None bindings=33/3842 openIssues=18`;
  four validator runs are `Pass`, world-transfer is `KnownProvenanceDrift`, and
  production-streaming-wiring is `MissingRequiredImplementation`;
- focused WorldRemaster EditMode: **31/31 passed**;
- focused WorldRemaster PlayMode: **9/9 passed**;
- full PlayMode regression: **26/26 passed**;
- full EditMode regression: **137 total, 134 passed, 3 failed**.

The three EditMode failures are pre-existing and explicitly preserved:

1. Two M3 lighting tests observe the user-owned `M3_NeutralVolume.asset` sky type
   `1` instead of the frozen M3 Physical Sky type `4`.
2. The 04A1 donor dry-run observes the documented post-reinstall SHA-256 drift for
   `sharedassets3.assets` and `sharedassets3.resource`.

The corrected production-cell lifecycle fixture starts from `Bootstrap`, performs
two load/unload cycles, rejects duplicate stable IDs and verifies exact runtime
snapshots on every cycle: 7 IDs / marker count 24 for `cell_0_-3`, and 8 IDs /
marker count 9 for `cell_0_-2`. Direct scene lifecycle passes; absence of a wired
production streamer remains a formal `PilotGate` blocker rather than being hidden
by the test.

Ignored evidence: `Logs/M05B_*.log` and `TestResults/M05B_*.xml`. Durable machine
results are under `Docs/WorldValidation/`.
