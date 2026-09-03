# Vegetation cell streaming compatibility — 2026-08-31

## Active v12 packed-cell and backdrop contract — 2026-09-02

V12 keeps the established `ProductionWorldStreamingManifest`, optional
presentation `cellLayers`, 512 m `WorldCellIndex`, scene lifecycle and gameplay
cell identity APIs. It changes only the generated vegetation presentation
payload. No save DTO, stable entity ID, authoritative gameplay scene mapping or
schema migration is introduced.

Each near vegetation cell is now a thin scene shell with three category-local
packed roots rather than one GameObject/prefab hierarchy per woody placement.
The saved `PackedWoodyCellAsset` data owns prototype references, draw batches,
matrices, stable placement metadata and tiled collision records. Rendering uses
the automatic packed woody path; accepted donor/natural-infill tree collision is
materialized by the bounded runtime pool near the player. Boundary forest,
shrubs/floor and distant backdrop do not serialize generated colliders.

Full generation `20260902-001303` and fresh validation `20260902-004112` each
passed 88/88. Report SHA-256 values are respectively
`F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`
and `A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`.
Both share settings hash
`7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d`
and source fingerprint
`2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`.
`Artifacts/VegetationRebuild/full-validate-exact-v12.json` passed exact
comparison of every cell report and 268 non-report artifacts, aggregate
fingerprint
`a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169`.

The saved near population is 62,168 original-category trees, including 37,678
donor originals and 24,490 natural infill, plus 5,811 grounded boundary trees.
Its 67,979 species records split into 44,201 spruce, 13,576 pine, 5,052 birch
and 5,150 aspen. There are 18,747 shrub/floor records. Grass remains in the
existing indirect path: 5,157,636 records from 36,044,800 candidates, using
Forest Grass02/01/03 at 46/32/22, a 0.8 m grid, density 0.96 and 16/8/6
near/middle/far clumps. No cereal or seed-head source is used.

Across the 88 near-cell scenes, packed assets contain 86,726 matrices and
matching metadata records, 2,130 prototype references, 47,138 batches and
62,168 collision records. Packed assets total 76,558,979 bytes; scene shells
total 1,088,371 bytes. Saved scenes contain zero prefab-instance GameObjects,
direct mesh renderers and serialized collider components. Combined serialized
authoring payload is 77,647,350 bytes versus v7's 482,661,729 scene bytes, an
83.91% reduction. This is a file-payload comparison, not runtime-memory evidence.

The near layer retains loading radius 1, so at most a 3×3 neighbourhood is
resident by distance policy. The independent `vegetation-backdrop` layer uses
load/unload radii 2/3. Its 16,000 distant trees are split over 81 scenes with
307 renderers, 349,465 vertices and zero colliders. Coverage validation reports
zero crown gaps and zero wrong-side or inside-enclosure placements. Equal-distance
priority still favors near vegetation, and normal automatic refresh admits one
new presentation layer per pass.

The global presentation scene owns no distant batch. It contains renderer-only
replacements for all 45 canonical rock anchors: 149 renderers and zero generated
colliders. The exact legacy rock collider stays authoritative; there is no free
or random rock scatter.

### Executed v12 benchmark boundary

`Artifacts/VegetationRebuild/Performance/cell-activation-benchmark.post-packed.json`
passed the direct three-scene Editor gate with maximum main-thread time 66.0798
ms, maximum observed yield 71.6469 ms and p95 yield 20.6325 ms.
`cell-activation-benchmark.production-service.json` exercised the real automatic
streaming-service route and passed at 63.8666 / 68.1513 / 14.5884 ms. Cleanup
passed with one completed coalesced unused-asset cleanup, no pending cleanup, no
owned/remaining loaded scenes, no renderers and no valid GPU buffers remaining.

Core PlayMode passed 22/22 and targeted EditMode passed 77/77. The matching v12
HDRP pilot capture was accepted. These measurements supersede the old serialized
hierarchy's roughly 200–608 ms Editor observations for the tested v12 fixtures.
They do not include the complete donor world, gameplay/save subscribers,
representative display GPU timing, a Player build or a 60 FPS guarantee. Runtime
memory and long-session crossing behavior still require an integrated Player
traversal benchmark. Vendor presentation remains private, removable and
`productionReady=false`; the ALP licence/provenance blocker is unchanged.

## Historical pre-packed implementation and bounded change

The following pre-v12 sections preserve the former scene-hierarchy contract and
v7 evidence. Their “current” wording is historical.

`ProductionWorldStreamingManifest` already owns one legacy scene per cell,
global legacy geometry, the gameplay catalog and vehicle preload radii. The
previous `Phase1ForestRuntimeCuller` only disabled children of the global forest:
it did not unload their GameObjects, meshes or colliders. The existing
`VegetationWorldRenderer` allocates one GPU buffer per nonempty tile/profile in
its assigned catalog and releases those buffers on disable/destroy.

The optional `cellLayers` array adds presentation-only additive scenes to the
same streaming service. Its entries use the existing 512 m `WorldCellIndex`, a
project-owned layer ID, an exact build index and a project-relative scene path.
Layer cells may exist beyond the 49 cells with legacy objects. They do not
change the original map, gameplay-cell catalog or persistent entity identity.

All existing serialized fields and public APIs remain. Schema 2 is retained:
old manifests deserialize the optional array as empty. No native save DTO,
stable entity ID or save migration changes are required. In particular,
`TryGetCellIdForScene` continues to identify only authoritative gameplay cells;
layer loads must not materialize the same NPCs/items/doors a second time.
Subscribers to the existing scene lifecycle events either filter through that
mapping or scan the loaded scene for their own components. Generated vegetation
scenes must contain presentation/static collision only.

## Historical pre-packed authoring contract

Save a separate generated scene per vegetation cell, containing the independent
tree/boundary/shrub category roots and a `VegetationWorldRenderer` referencing a
**one-cell catalog**. The all-cell catalog remains an Editor authoring index;
do not bind it to a global runtime renderer. Loading/unloading a scene then
allocates/releases only that cell's grass buffers and destroys its tree
colliders. No eager map-wide renderer or runtime GameObject instantiation loop
is introduced.

Register saved scenes through
`MSC.Editor.WorldStreaming.ProductionWorldCellLayerBuilder.ReplaceLayerScenes`.
Provide the exact affected cell set to replace only those registrations.
Omitting a replacement for an affected cell unregisters that cell. Passing null
for the affected set deliberately replaces the whole named layer. Other layers,
legacy/global scenes, gameplay metadata and scene content remain untouched.
The helper does not delete scene files or manual objects.

New enabled Build Settings entries are appended. A previously disabled entry is
moved to the end before enabling so that earlier enabled build indices remain
valid, including those held by other manifests. Existing enabled entries retain
their order. Removed layer registrations leave unused build entries intact;
their eventual removal is a separate project-wide build-index audit.

The optional minimum loading/unloading radii extend the manifest radii; zero
inherits existing behavior. Boundary forest can use 2/3 while ordinary content
inherits the existing 1/2 (vehicle 2/2) configuration. Unload radius is clamped to
at least load radius. The service respects retained gameplay cells and never
adopts/unloads a layer scene already loaded by another owner. Session cleanup
unloads owned layers before global scenes. Temporary baseline scene registration
requires the manifest's explicit private-local flag.

Scene unloading alone does not release Unity asset payloads. After a refresh or
explicit `UnloadOwnedScenes` batch that actually unloads an owned cell-layer
scene, the service awaits `Resources.UnloadUnusedAssets` once. It never scans for
legacy-only unloads or once per individual cell, and never calls `UnloadAsset`
on a catalog, mask or material that another scene could share. Unity's
reachability check preserves assets referenced by loaded/retained/external
scenes. This asynchronous scan can still cause a frame-time spike and must be
profiled at cell transitions; it is not a 60 FPS guarantee. The existing
non-awaitable `EndGameSession` path is unchanged.

## Historical pre-v12 verification coverage and limits

`ProductionWorldCellLayerTests` checks schema compatibility, presentation-only
cells, duplicate identity/address rejection and radius behavior.
`ProductionWorldCellLayerPlayModeTests` uses two small project-owned fixtures,
appends their Build Settings entries in `IPrebuildSetup`, before Unity snapshots
the runtime scene table. A SessionState snapshot survives domain reload and
`IPostBuildCleanup` restores the previous settings after the run. Runtime NUnit
setup never changes Build Settings and asserts the authoritative SceneUtility
addresses. The tests execute repeated asynchronous load/unload,
hysteresis, destroyed collider checks, scene lifecycle notifications and
protection of externally loaded layers. These tests do not require donor assets.
The asset-lifetime test creates two temporary saved scenes sharing a tiny
catalog, instance-data cell and density texture, all removed after the run. It
checks sharing across an owned-scene unload, native asset release after the last
owner unloads, and intact data on reload. Its renderer is disabled to isolate
asset lifetime from HDRP/GPU availability; GPU-memory and frame-time profiling
remain separate validation.

The test source being present is not a passing result; the integration report
must record actual Unity results. Full-map frame rate, visual horizon coverage,
grass density and camera transition quality still require generated-content
inspection and profiling. This extension does not certify those separately.

## Historical v7 presentation correction — 2026-09-01

Automatic Play-mode rendering defers GPU allocation until a catalog is near a
camera. Catalog metadata is no longer built synchronously in the camera callback:
all vegetation renderers share an incremental budget of 4 operations / 256
profile records / a cooperative 1 ms per frame. Visible and prefetch tiles share
a separate upload budget of 4 operations / 8,192 instances / a cooperative 1 ms
per frame. A partial tile cannot draw until all records and indirect commands for
that tile are ready. Explicit `RebuildGpuResources()` and Edit-mode authoring or
capture still rebuild eagerly. Partial and complete buffers are released on
disable/destroy.

For automatic refreshes, `ProductionWorldStreamingService` starts at most one
presentation-layer load, selects nearest first, assigns async priority -1,
holds activation while progress is below 0.9 and yields a rendered frame before
allowing final activation. Focus is recomputed after each activation. Explicit
`RefreshNow` retains its load-all compatibility behavior. Public scene ownership,
retention, gameplay-cell mapping, serialized radii and lifecycle notifications
remain unchanged. The save participant still skips empty registration batches
before global physics synchronization; nonempty gameplay restore is preserved.

V7 forest-floor binding v3 maps `BranchLitter01` from
`prefab_detail_branches_01` and `PoplarLeafLitter01` from
`prefab_detail_poplar_leaves_01_1` into the existing
`DeadGrass02`/`DeadGrass03` prefab slots. Preserving those output GUIDs changes
presentation without adding instance records or runtime scene roots. Forest
import is 22 seeds / 79 closure assets and Meadow 51 / 156, with zero unresolved
external GUIDs, demo scenes, scripts or Editor assets. The v7 dependency update
does not change the ALP `ThirdPartyPrivatePhase1Presentation`,
`productionReady=false` licence/provenance blocker.

Tree provenance is unchanged by this streaming revision: ALP supplies the active
43,492 spruce, while reviewed licensed Chernobyl `Tree/Pine_*`, `Tree/Birch_*`
and `Tree/Aspen_*` bindings remain active for 13,382 / 5,018 / 5,018.
NatureManufacture supplies grass, grey willow and understory/debris rather than
those non-spruce trees. The superseded Chernobyl scope is its old grass binding;
Engelmann is the superseded spruce source.

Active v7 pilot `20260901-092200` passed in `cell_-4_-3` with 1,967 original
trees, 1,727 boundary trees, 1,793 shrubs/forest-floor instances and 223,100 grass
instances from 620,500 candidates. Settings hash is
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569` and
fingerprint is
`fa868c935f994e13a0a1fb637a45c0184729ae9cf003c3c8e9431983e12b5da6`.
Its five-view HDRP capture passed on an NVIDIA GeForce RTX 4070 SUPER with zero
missing or unsupported shaders.

The computed v7 grass-footprint minimum remains 0.77 m, but active serialized
ordinary and field clearance remains 0.94 m; railway clearance remains 1 m.

Active v7 `RunAllBatch` run `20260901-092757` passed 88/88 scenes with zero
errors. Fresh-process `ValidateAllBatch` run `20260901-100750` passed the same
88/88 with `validationOnly=true`; every per-cell field, fingerprint and count
matches. The output contains 37,678 original and 29,232 boundary trees,
66,910 total: 43,492 spruce, 13,382 pine, 5,018 birch and 5,018 aspen. It also
contains 28,759 shrub/forest-floor instances and 8,077,749 grass instances from
54,599,726 candidates. Its 88 scene files total 482,661,729 bytes; dense
`cell_-4_-3` is the largest at 27,079,109 bytes. Fresh v7 GPU PlayMode passed
6/6 and the green texture-mask filter 15/15, both with zero failures/skips.
Current v7 `MapVegetation` passed 41/41 with zero failures, superseding 40/40;
the separate forest-floor binding-v7 filter passed 5/5. Evidence is
`Artifacts/Tests/VegetationGpuStreamingPlayModeV7GroundDebris.xml`,
`Artifacts/Tests/MapVegetationGrassTextureMaskEditModeV7.xml`,
`Artifacts/Tests/MapVegetationEditModeV7GroundDebris.xml` and
`Artifacts/Tests/MapVegetationForestFloorBindingV7.xml`. These suites and the
deterministic saved-output result do not bound Unity scene loading.

These changes reduce secondary GPU-upload, metadata and burst-loading work. They
do **not** bound Unity scene deserialization/integration, resource cleanup or a
single non-preemptible native/graphics call. In the representative dense Editor
cell, native `LoadSceneOperation.CompleteAwakeSequence` remains approximately
191–232 ms and dominates the observed main-thread pause; making roots inactive
did not remove it. A same-population unpacked diagnostic copy was much faster,
but it was not a Player measurement and was not deployed to production scenes.

The retained fresh-process v6 observation measured one heavier scene load at
608.743 ms total: 478.810 ms deserialization and 129.870 ms integration. V7 was
not reprofiled by that observation. It remains Editor evidence rather than a
normal Player transition benchmark and confirms that matching saved fingerprints
do not mean the hitch is fixed. The measurement is recorded in
`Artifacts/VegetationRebuild/ValidateFullMapV6.log`.

The one recommended next milestone is a measured pilot of the compact woody
runtime catalog for the same dense cell. It must preserve selected IDs,
transforms, LOD/material/collider references, grass-catalog ownership and unload
behavior, then compare cold/warm
`CompleteAwakeSequence`, total main-thread gap and sliced activation cost before
any storage migration is accepted. The current pacing work is not a claim that
streaming is fixed, that the live game holds 60 FPS, or that current Player
performance has been measured. See
[the correction report](MAP_VEGETATION_PRESENTATION_REVISION.md) for historical
benchmarks and active acceptance work.
