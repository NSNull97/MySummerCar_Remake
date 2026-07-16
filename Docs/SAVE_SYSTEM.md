# Save System

## Goals

- stable IDs;
- versioned format;
- atomic writes;
- backup before migration;
- clear errors;
- migration tests;
- optional donor-save importer isolated from the native format.

## Layers

- `ISaveStorage` — files/slots and atomic writing.
- `SaveSnapshotBuilder` — collects state.
- `SaveDocument` — versioned DTO root.
- `ISaveParticipant` — narrow state contribution boundary when useful.
- `SaveMigrationPipeline` — ordered migrations.
- `SaveApplyService` — applies loaded state to registered stable entities.

Do not serialize scene GameObjects directly.

## Stable IDs

Save records use project-owned IDs. Donor identifiers may be stored as metadata for migration or provenance.

## Atomic write strategy

1. Serialize to temporary file.
2. Validate basic structure.
3. Move current save to backup when needed.
4. Replace current save atomically where supported.
5. Keep original donor saves untouched.

## Versioning

The save root contains:

- schema version;
- project build/version;
- timestamp;
- world/time/weather state;
- player state;
- entity records;
- vehicle assembly and simulation records;
- optional diagnostics.

## Original-save importer

Treat donor save import as an optional adapter:

```text
Donor save -> parsed donor DTO -> validated conversion -> native SaveDocument
```

Never make the runtime depend directly on donor save files. Preserve the original file and produce a conversion report.

## Tests

- empty/new save;
- round trip;
- corrupted file handling;
- unknown entity IDs;
- duplicate records;
- migration chain;
- interrupted write simulation;
- carried item state;
- mounted part and fastener state;
- weather/time state;
- backup preservation.

## Milestone 4 carried-object snapshot boundary

`MSC.Interaction.Runtime` owns `CarriedObjectSaveState` schema version `1`. It contains only:

- whether an object is currently carried;
- the project-owned canonical stable entity ID;
- position relative to the carry anchor;
- rotation relative to the carry anchor.

`PhysicalCarryController.CaptureSaveState()` produces the snapshot. It does not write files, scan scenes or resolve IDs. A future `SaveSnapshotBuilder` may embed this DTO in the player record. During load, `SaveApplyService` must resolve the stable ID through `IEntityIdProvider`, validate that the object still exposes `IPickupTarget`, and then restore carrying through an explicit application path.

Failure policy for the future apply step:

- unknown stable ID: report the missing entity and leave the player empty-handed;
- resolved entity without pickup capability: report a schema/content mismatch;
- duplicate ID: reject application before mutating runtime state;
- obsolete prefab ID: resolve through an explicit migration mapping;
- invalid snapshot version: route through the save migration pipeline.

Milestone 4 intentionally does not implement the save root, storage backend, ID registry or load application.

## Milestone 06 vehicle-simulation snapshot boundary

`MSC.Vehicle.Simulation` now owns `VehicleSimulationStateDto` schema version `1`. `VehicleSimulationState.CaptureDto()` copies the current engine, clutch, gearbox, differential, vehicle, electrical, fluid, thermal and four-wheel state. `TryRestoreDto()` rejects a wrong schema, wrong wheel count, invalid enum/gear, negative bounded quantities and non-finite numeric data before mutating the state.

This is a simulation-domain fixture, not a complete vehicle save implementation:

- it does not write a file or save slot;
- it is not yet wrapped by a stable vehicle entity record in `SaveDocument`;
- it does not include the proxy Rigidbody world pose/velocity;
- it does not merge with `VehicleAssemblySaveData`;
- it has no migration beyond schema `1`;
- it does not import donor saves.

A future `SaveSnapshotBuilder` must capture assembly and simulation records together under one project-owned stable vehicle ID. Load order must validate IDs and DTO versions, restore assembly first, rebuild the assembly-to-physics snapshot, restore simulation state, then explicitly restore physical pose. Failure must leave the original save intact and report which boundary failed.

The M06 schema-1 JSON round-trip test passes in the fresh focused EditMode suite (`18/18 PASS`). This validates the bounded domain DTO fixture only; it does not close the save-root, storage, stable-vehicle resolution or migration work listed above. See `Docs/Vehicle/SIMULATION_TEST_MATRIX.md`.
