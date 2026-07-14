/plan

# MILESTONE 04A-PILOT — BOUNDED WORLD LAYOUT REFERENCE TRANSFER

Read `AGENTS.md` completely before doing anything.

## Authority and boundary

This is the only executable Milestone 04A prompt during the current
single-player vertical-slice phase. It replaces the parked full-world prompt in
the active sequence; it does not authorize the entire original map.

Work on exactly one bounded acceptance zone:

- the existing garage prototype footprint;
- the immediately adjacent road segment already represented by the M3 prototype;
- only the minimum surrounding terrain/layout context required to validate scale,
  orientation, elevation and connection between those two elements.

Do not expand the zone because additional donor objects are easy to discover.

## Objective

Create a small, provenance-aware world-layout pilot that proves the safe path:

```text
read-only donor inspection
  -> external staging records
  -> reviewed coordinate/layout data
  -> reference-only comparison
  -> project-owned durable pilot layout data
  -> validation report
```

The result is reference/prototype data for later reauthoring, not a claim that
the map or production world is complete.

## Required inputs

Read:

- `Docs/PROJECT.md`;
- `Docs/ARCHITECTURE.md`;
- `Docs/ROADMAP.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/DONOR_PIPELINE.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/PORTING_LEDGER.csv`;
- `Docs/Reviews/LATEST_REVIEW_POINTER.md`;
- the latest approved fix report.

Use `Config/DonorPaths.local.json` for machine paths. The donor installation is
read-only. Raw extraction and normalized staging remain outside Git.

## Required pre-flight

Before any extraction or Unity authoring:

1. Confirm a clean, intentional Git baseline.
2. Confirm the four High findings from the review are resolved or explicitly
   accepted in the latest fix report.
3. Define the pilot zone with numeric bounds and a short landmark allow-list.
4. List the exact donor containers/objects to inspect.
5. Stop if the plan requires a full scene dump, DRM/access-control work, a new
   package, or writes to the donor installation.

## Allowed work

- read-only scene/object inventory for the bounded zone;
- hashes and provenance metadata;
- transforms, pivots, bounds, road centerline samples and terrain elevations;
- a small external staging manifest;
- reference-only comparison content under the existing ignored boundary;
- project-owned serializable layout records and Editor validation tooling;
- one purpose-specific pilot validation scene if required;
- tests for coordinate conversion, bounds, manifest integrity and reference leaks.

## Prohibited work

- exhaustive map discovery or a complete-world database;
- mass import of donor scenes/assets;
- production terrain, buildings, vegetation, water or final materials;
- importing donor textures as final content;
- changing Player/Interaction except for a measured integration regression;
- vehicle assembly, vehicle simulation, weather, audio or save implementation;
- adding packages or native tools without separate approval;
- changing the donor installation.

## Data and provenance requirements

Every donor-derived record must include:

- donor relative container path and exact object identity/PathID where available;
- canonical source SHA-256;
- transfer classification;
- coordinate-system and unit conversion;
- destination/reference path;
- tool/importer version;
- dependencies, limitations and known differences.

Use only project-owned stable IDs for persistent pilot records. Donor IDs remain
provenance metadata.

## Validation

Run the smallest available checks for:

- source/staging path separation and donor read-only guardrails;
- canonical hashes and manifest round trip;
- coordinate conversion and scale tolerance;
- unique stable IDs;
- no ReferenceOnly dependency from production prefabs/build scenes;
- runtime-to-Editor assembly boundaries;
- missing references;
- EditMode tests and a focused PlayMode/scene boot test if a scene is added.

Document any manual Unity comparison with exact scene path, camera/overlay setup,
expected landmark alignment and numeric tolerance. Do not claim it was performed
unless it was actually performed.

## Definition of done

The pilot is complete only when:

1. The bounded zone and allow-list are recorded.
2. Every transferred pilot record has canonical provenance.
3. Coordinate/scale assumptions are tested.
4. Reference-only content can be removed without breaking production content.
5. No full-world completeness claim appears in code or documentation.
6. Available tests and validators pass, or failures are reported honestly.
7. `Docs/Milestones/MILESTONE_04A_PILOT_REPORT.md` records inspected sources,
   changed files, commands, tests, manual steps, limitations and risks.

## Stop condition

Stop after the bounded pilot report. Do not continue to 04B, Milestone 05 or a
second world zone.
