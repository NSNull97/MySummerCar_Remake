/plan

# MILESTONE 09 — SAVE SYSTEM HARDENING AND OPTIONAL LEGACY DATA STUDY

Read `AGENTS.md` completely before doing anything.

Read:

- all milestone reports through 08A;
- `Docs/SAVE_SYSTEM.md`;
- stable-ID architecture;
- world-streaming and world-ID documentation;
- vehicle assembly and simulation DTOs;
- weather/time DTOs;
- UI settings schema;
- donor audit and known donor-save findings;
- current Git status and diff.

## Objective

Implement a robust, versioned native save system for the vertical slice and
safely study donor save data when enough evidence exists.

The native save format is authoritative for the remake.

Any donor-save support must be an isolated import/conversion path.

## Save domains

Support, as implemented by the current project:

- world time and weather;
- player transform and state;
- player needs/status;
- inventory/held item;
- stable world-entity state;
- doors/gates/windows;
- vehicle part instances;
- mount relationships;
- fastener states;
- vehicle simulation state;
- fluids/electrical placeholders when available;
- world streaming/cell state needed for reconstruction;
- tasks/progression only when implemented;
- save metadata;
- screenshot/thumbnail hook;
- settings stored separately when appropriate.

Do not serialize raw scene-object graphs.

Do not persist Unity instance IDs.

## Architecture

Create or align:

- `SaveDocument`;
- `SaveHeader`;
- `SaveMetadata`;
- `SaveSchemaVersion`;
- `SaveSlotId`;
- `SaveWriteRequest`;
- `SaveLoadRequest`;
- `SaveResult`;
- `SaveError`;
- `ISaveParticipant`;
- `ISaveStateProvider`;
- `ISaveStateConsumer`;
- `SaveParticipantRegistry`;
- `SaveSerializer`;
- `SaveStorage`;
- `SaveMigrationPipeline`;
- `SaveValidationService`;
- `SaveBackupService`;
- `SaveRecoveryService`;
- `SaveCoordinator`;
- domain DTOs;
- optional `LegacySaveImporter`.

Keep storage, serialization, domain collection, migration, and presentation
separate.

## File behavior

Implement:

- explicit save directory;
- slot naming;
- temporary file;
- flush/write completion;
- atomic replace when supported;
- backup rotation;
- corruption detection;
- schema validation;
- readable error reporting;
- safe handling of missing optional content;
- no silent partial overwrite;
- no donor-save modification.

Document platform-specific limitations.

## Versioning and migration

Use explicit schema versions.

Migrations must:

- be ordered;
- be testable;
- preserve old fixtures;
- fail with a useful error;
- not mutate the source save before success;
- record migration path;
- support unknown-future-version rejection.

Do not rely on runtime type names as permanent schema identity.

## Stable entities and world streaming

Saving/loading must tolerate:

- unloaded cells;
- entities loaded later;
- production prefab replacement;
- missing optional entity;
- renamed scene;
- regenerated cell scenes with stable IDs;
- duplicate-ID detection;
- deferred state application.

Create a clear deferred-state policy.

Do not force the entire map loaded merely to save.

## Vehicle reconstruction

Define order of reconstruction:

1. definitions/config availability;
2. part instances;
3. transforms and held/dropped state;
4. mount relationships;
5. fasteners;
6. assembly graph validation;
7. simulation state;
8. presentation refresh.

Handle invalid or missing content with a report, not a crash.

## Autosave and manual save

Expose policy hooks for:

- manual save;
- autosave;
- safe-save conditions;
- save-on-exit;
- cooldown;
- busy indicator;
- failure notification;
- pause interaction.

Do not implement aggressive autosave that can destroy a usable slot.

## Settings

Keep graphics/audio/control/accessibility settings in a versioned settings
document or clearly separated section.

Settings persistence must not require loading a game slot.

## Legacy donor-save study

Only proceed if the donor save location and format are sufficiently understood.

Rules:

- copy/read donor save from a safe external location;
- hash source;
- never modify source;
- document game version evidence;
- parse into donor DTOs;
- validate;
- convert into native DTOs;
- produce a conversion report;
- preserve unsupported fields in diagnostics where practical;
- reject ambiguity rather than guessing.

Required architecture:

```text
Donor bytes/files
→ Donor DTO
→ Validation
→ Explicit conversion
→ Native SaveDocument
→ Native validation
→ New output slot
```

Do not deserialize donor data directly into runtime domain objects.

If donor format is unclear, stop at research/documentation and do not fabricate
an importer.

## Security and robustness

Validate:

- path traversal;
- invalid slot names;
- absurd sizes;
- malformed JSON/binary data;
- duplicate IDs;
- invalid numbers;
- unsupported schema;
- missing definitions;
- partial writes;
- low disk space reporting where practical;
- concurrent save request behavior.

No arbitrary type activation from save data.

## Debug and Editor tools

Create tools under:

`Tools → MSC Remake → Save`

Required:

- list slots;
- validate slot;
- inspect metadata;
- create test save;
- simulate corrupted save;
- run migration;
- compare round trip;
- show unresolved stable IDs;
- export load report;
- legacy-import dry run when available;
- open save directory.

## Tests

Add tests for:

- domain round trip;
- deterministic metadata where expected;
- atomic write/recovery;
- backup rotation;
- corrupt file;
- unsupported version;
- migration chain;
- migration failure;
- duplicate stable IDs;
- deferred world-entity state;
- vehicle assembly reconstruction;
- weather/time reconstruction;
- settings separation;
- invalid numeric values;
- path validation;
- legacy conversion fixtures when available.

Add PlayMode tests for:

- save during a representative scene;
- load into a clean session;
- cell unload/reload state;
- vehicle assembly restoration;
- failed load UI flow;
- autosave policy.

## Performance

Measure:

- state collection time;
- serialization time;
- file write time;
- load/validation time;
- reconstruction time;
- allocations;
- representative save size.

Do not move expensive synchronous work into gameplay frames without an explicit
safe-state policy.

## Documentation

Create or update:

- `Docs/Save/SAVE_ARCHITECTURE.md`;
- `Docs/Save/SCHEMA.md`;
- `Docs/Save/MIGRATIONS.md`;
- `Docs/Save/STABLE_ENTITY_LOADING.md`;
- `Docs/Save/ERROR_AND_RECOVERY.md`;
- `Docs/Save/LEGACY_SAVE_FINDINGS.md`;
- `Docs/Save/SAVE_TEST_MATRIX.md`;
- `Docs/Save/PERFORMANCE_REPORT.md`;
- `Docs/Milestones/MILESTONE_09_REPORT.md`.

## Definition of done

1. Native saves are versioned.
2. Writes are safe and recoverable.
3. Stable entities reconstruct correctly.
4. Vehicle/world/weather state round-trips.
5. Migrations exist and are tested.
6. Failures are visible and non-destructive.
7. Settings persist separately.
8. Legacy import is implemented only when supported by evidence.
9. Tests and performance measurements exist.

## Final response

Report:

1. Save architecture.
2. Domains covered.
3. Write/recovery behavior.
4. Migration behavior.
5. World/vehicle reconstruction.
6. Legacy study/import status.
7. Tests.
8. Performance.
9. Files changed.
10. Manual checks.
11. Risks and unsupported data.
12. Readiness for vertical-slice optimization.

Stop after Milestone 09.
