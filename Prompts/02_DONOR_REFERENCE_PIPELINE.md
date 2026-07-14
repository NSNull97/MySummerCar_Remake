/plan

Read `AGENTS.md`, Milestone 0–1 reports, `Docs/PORTING_GUIDE.md`, and `Docs/ART_GUIDE.md`.

Complete **Milestone 2 only: controlled donor reference pipeline**.

## Objectives

Create a repeatable, idempotent, provenance-aware pipeline from external donor staging to development-only reference assets and newly authored production replacements.

## Required code/data

- `DonorAssetManifest`
- `DonorAssetRecord`
- `DonorAssetRegistry`
- `LegacyAssetReference`
- path validation;
- SHA-256 comparison;
- import planning separated from execution;
- importer versioning;
- Editor validation UI or command;
- ledger update support.

## Controlled proof

Use only a few representative assets:

- one donor environment mesh as reference;
- one rebuilt/replacement environment asset;
- one donor vehicle part as reference;
- one rebuilt/replacement vehicle part;
- preserved pivot and at least one mount point;
- one new HDRP material and texture set;
- one collision proxy;
- one LOD group;
- a dedicated comparison scene.

Original textures are reference only. Do not upscale one and call it final.

The production prefab must not reference donor geometry or donor textures. Deleting the reference-only directory must not break production content.

## Tests

- manifest serialization;
- path normalization;
- hash comparison;
- import-plan idempotency;
- invalid destination rejection;
- missing provenance detection;
- production prefab donor-reference detection.

## Constraints

- No mass import.
- No full world scene.
- No gameplay implementation.
- No donor asset in a release build.
- Do not commit donor binaries.

Create `Docs/Milestones/MILESTONE_02_REPORT.md` and stop.
