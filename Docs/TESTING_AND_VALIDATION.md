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
