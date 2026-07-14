# Controlled Donor Reference Pipeline

## Scope and ownership

This pipeline moves a deliberately small, reviewed set of donor-derived reference files from external staging into the ignored development-only `ReferenceOnly` tree. It does not extract assets from the original game, author production art, or approve a transfer automatically.

Owned components:

- `DonorAssetManifest` — versioned JSON session description;
- `DonorAssetRecord` — source hash, object identity, classification, importer version, paths, dependencies, and status;
- `DonorAssetRegistry` — committed Unity metadata registry with no donor binary payload;
- `LegacyAssetReference` — provenance marker allowed only on reference comparison objects;
- `ReauthoredAssetProvenance` — Editor-only sidecar for a production replacement, dimensional comparison, textures, and mount points.

Current pipeline identity is `msc-controlled-donor-import` version `2.0.0`. Manifest schema version is `1`.

## Fixed data flow

```text
Read-only donor installation
  -> approved extractor, minimal export only
  -> external DonorStaging/raw/<session-id>
  -> reviewed normalization
  -> external DonorStaging/normalized/<session-id>
  -> versioned JSON manifest + SHA-256
  -> read-only DonorImportPlan
  -> explicitly confirmed DonorImportExecutor
  -> ignored Assets/Game/LegacyImport/ReferenceOnly/<session-id>
  -> registry + porting-ledger metadata
  -> independent production reauthoring
```

The planner accepts source files only through the configured staging root. Root validation rejects donor, staging, and project directories that are equal or contain one another. Direct donor-to-Assets copying is not supported.

## Manifest record fields

Each record stores:

- stable project-owned record ID;
- donor-relative container path and donor object name;
- optional canonical lower-case SHA-256 of the donor source container;
- required canonical lower-case SHA-256 of the normalized staged file;
- asset kind and transfer classification;
- staging-relative source path;
- destination below `Assets/Game/LegacyImport/ReferenceOnly`;
- optional independent production replacement path;
- explicit status such as `Staged`, `ImportedReference`, `ReplacementReady`, `Rejected`, or `Blocked`;
- extractor/importer ID and pinned version;
- dependencies and known notes.

An empty schema example is `Assets/Game/LegacyImport/Manifests/DonorAssetManifest.example.json`. Machine-specific roots never appear in the manifest or runtime source.

## Planning and execution

Use `Tools > My Summer Car > Legacy Import > Plan Manifest...`.

Planning performs no writes. For every record it checks:

- manifest and pipeline versions;
- unique record ID and destination;
- canonical SHA-256;
- importer ID/version;
- safe staging-relative and ReferenceOnly paths;
- source existence and hash;
- existing destination hash.

Possible actions:

- `Copy` — destination is absent and source hash is valid;
- `UpToDate` — destination already has the expected hash;
- `Conflict` — destination exists with different bytes; overwrite is prohibited;
- `Blocked` — source is missing or its hash does not match.

Execution is a separate command: `Tools > My Summer Car > Legacy Import > Execute Last Plan`. It requires explicit confirmation, copies only `Copy` operations, never overwrites, rehashes every copied file, writes/updates the registry, and previews/upserts the porting ledger. Re-running the same manifest produces only `UpToDate` operations.

If execution is interrupted after copying but before metadata update, the unregistered reference is ignored by Git and the project validator reports `MissingProvenance`. Re-plan the same manifest; the file becomes `UpToDate`, then execute metadata update again.

## Validation and build exclusion

Run `Tools > My Summer Car > Legacy Import > Validate Donor Pipeline` or batch entry point:

```text
MSC.LegacyImport.Editor.Validation.DonorPipelineValidationRunner.RunBatch
```

Validation checks:

- configured root separation;
- manifest/registry schema and pipeline version;
- duplicate IDs, hashes, importer provenance, and destination paths;
- missing reference files and hash mismatches;
- every asset in `ReferenceOnly` has a registry record;
- production prefabs contain no `LegacyAssetReference`;
- production prefabs and enabled build scenes have no dependency below `ReferenceOnly` or `Imported/DonorGenerated`;
- replacement-ready records point to real production assets;
- controlled proof requirements when proof metadata exists.

`DonorReferenceBuildGuard` runs the production-prefab/build-scene dependency checks before every player build and throws `BuildFailedException` on leakage. Missing development references remain a full Editor-validation error but do not block a build when production content is independent; deleting `ReferenceOnly` therefore cannot make clean production prefabs unloadable. `ReferenceOnly` and `DonorGenerated` binary payloads remain ignored by Git.

## Controlled proof requirements

The proof is complete only when registry/provenance data demonstrates:

- one extracted donor environment mesh and an independently authored replacement;
- one extracted donor vehicle-part mesh and an independently authored replacement;
- preserved pivots within documented tolerance;
- at least one measured vehicle mount point within tolerance;
- an HDRP material and at least three newly authored texture assets;
- a collision proxy;
- an `LODGroup` with at least two levels;
- no donor/reference dependency in either production prefab;
- a generated comparison scene at `Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity`.

The comparison scene is created by `Tools > My Summer Car > Legacy Import > Build Controlled Proof Comparison Scene`, is never added to build settings, and is disposable with the entire ReferenceOnly directory.

## Milestone 2 controlled proof result

AssetRipper `1.3.14` is installed outside Git and configured only through ignored `Config/DonorPaths.local.json`. Its official Windows x64 archive SHA-256 was verified before extraction. The donor was opened read-only; all generated files and logs were written to external staging.

The reviewed manifest `milestone-02-controlled-proof` contains exactly two mesh records:

- `garage_shed_roof`, Mesh PathID `2186` from `sharedassets3.assets`;
- `drum_brake_rear`, Mesh PathID `125` from `sharedassets1.assets`.

Raw GLB, mesh JSON, transform JSON, normalized OBJ, conversion metadata and hashes are under external `raw/controlled-proof` and `normalized/controlled-proof`. Only the two normalized OBJ files enter ignored `ReferenceOnly` paths. Original materials and textures were not transferred.

Independent project-authored proof assets provide replacement geometry, preserved local pivots, a `HubAxis` mount point, collision proxies, two LODs, one HDRP Lit material and BaseColor/Normal/Mask textures. Full donor validation passes with zero warnings; repeated planning returns two `UpToDate` operations, and a repeated proof build changes zero ledger rows.
