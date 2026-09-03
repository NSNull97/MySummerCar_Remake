# Map vegetation source audit

Date: 2026-08-31. Scope: source evidence and the Editor-only source adapter.
This is not a claim that the final generated map has passed visual or performance acceptance.

## Active v12 source and output lock — 2026-09-02

The current source fingerprint is
`2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`;
the accepted settings hash is
`7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d`.
The source adapter recovered 65,878 donor-tree candidates across nine provenance
paths, accepted 37,678 and rejected 28,200. Stable source validation reports no
duplicate IDs or duplicate millimetre positions.

Spruce presentation remains the five reviewed ALP optimized variants. Reviewed
licensed Chernobyl `Tree/Pine_*`, `Tree/Birch_*` and `Tree/Aspen_*` bindings
remain the non-spruce sources. Across accepted donor, natural-infill and grounded
near-boundary records, exact v12 totals are 44,201 spruce, 13,576 pine, 5,052
birch and 5,150 aspen. The natural-infill policy caps additions at 65% of the
37,678 accepted donor originals and accepted 24,490; the original-tree category
therefore contains 62,168 records. A separate grounded boundary layer contributes
5,811, for 67,979 near trees. All normal surface and road, yard, field, water,
route and OpenSpace exclusions remain authoritative.

Active grass provenance no longer uses the v7 Meadow detailed/regular/cross
triplets. The only approved v12 source prefabs are:

| Owned profile | Reviewed source | Selection weight | Near / middle / far clumps | LOD distances |
|---|---|---:|---:|---:|
| `NatureManufactureShort` | Forest `prefab_grass_02_3` | 46% | 16 / 8 / 6 | 18 / 42 / 60 m |
| `NatureManufactureBroad` | Forest `prefab_grass_01_3` | 32% | 16 / 8 / 6 | 18 / 42 / 60 m |
| `NatureManufactureDryFine` | Forest `prefab_grass_03_3` | 22% | 16 / 8 / 6 | 18 / 42 / 60 m |

These are reviewed low blade forms, not cereal or seed-head sources. Candidate
spacing is 0.8 m and density is 0.96. The full-resolution green mask, bounded
three-texel dilation and local-carpet evidence operate only after natural-ground
and gameplay exclusions. Full v12 output contains 5,157,636 grass records from
36,044,800 candidates, split 2,368,747 / 1,651,618 / 1,137,271. Corrected
`Artifacts/VegetationRebuild/GrassArt/provenance.json` SHA-256 is
`76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`.

Active runtime presentation v12.2 changes only project-owned shader, rendering,
future material-authoring and validation code; the placement, mesh, profile and
catalog payload remains unchanged. `MSC_VegetationIndirectHDRP.shader` retains
GUID
`49f3045c658005748ad888e65476f1ed`; its current SHA-256 is
`FAD25AA707CEA1BBDC24EBB4899CDF880B6C6AE8803C1CA1E96EB612754D81D2`.
The same source prefabs, generated meshes, materials, profiles, catalog assets
and 5,157,636 placement records remain authoritative; no generated vegetation
payload was rebuilt.

The fail-before zero-radiance audit
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-083140-659-p26348`
produced mean-luminance and bright-pixel ratios of 1.0 / 1.0. The post-fix audit
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-090040-053-p20132`
passed at 0.0 / 0.0 over 126,982 saved records and 712 GPU batches. The shader
now evaluates the HDRP Lit LightLoop, so the older simplified-unlit/hemisphere
description below is superseded as an active limitation and is retained only
for chronology.

The final extended audit
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-093656-076-p39636-2f2d286bd4d648a0a3b5002e31080da2`
passed directional, punctual and zero-radiance controls with report SHA-256
`F6D9748BBC065CF154DB50BF80B6B16C3209159C0A227205BA73202D3F8DDFC8`.
It used the same 126,982 records and 712 batches. Directional mean luminance
was 0.2896735966, punctual mean was 0.02317777649, and zero-radiance mean,
maximum and bright-pixel fraction were exactly zero; all recorded directional
and punctual mean/bright ratios were zero. Focused D3D11 EditMode, including
`MapVegetationNightEmissionAuditTests` and
`VegetationIndirectMotionVectorContractTests`, passed 40/40 in
`Artifacts/VegetationRebuild/Tests/GrassLightingHotfix.Final4.20260902.xml`;
its SHA-256 is
`B3D743D7E9479B9510B5BE685C7F3B3A3A235D01F6645C866BC08ED9A5EBBDCA`.
The matching day capture report
`Artifacts/VegetationRebuild/VisualAuditCells/cell_-4_-3/capture-report.json`
passed 7/7 with no missing or unsupported shaders and zero magenta fraction in
every view; report SHA-256 is
`1417C8CAE6D9179EDFEB720C0A881599AB5FDE61764B51C8085228D22D38F237`.

The compact indirect renderer now requests camera motion vectors because its
current object-motion shader pass lacks the complete HDRP
normal-buffer/MRT/stencil contract; exact wind velocity remains explicit debt.
Future grass-material regeneration now authors smoothness 0.04, specular F0
0.018, black emission and `EmissiveIsBlack`. Active material YAML was not
reserialized; runtime values resolve from shader defaults plus existing
`m_LightmapFlags: 4`. No generated asset was regenerated for this hotfix.
Full-map LightLoop performance and manual gameplay lighting remain pending. The
selected vendor presentation remains removable private Phase 1 content with
`productionReady=false`.

`RunAllBatch` run `20260902-001303` passed 88/88; report SHA-256 is
`F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`.
Fresh validation `20260902-004112` also passed 88/88 with
`validationOnly=true`; report SHA-256 is
`A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`.
`Artifacts/VegetationRebuild/full-validate-exact-v12.json` passed exact cell
reports and 268 non-report artifacts, aggregate fingerprint
`a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169`.
Targeted EditMode passed 77/77, core PlayMode passed 22/22 and the matching v12
HDRP pilot capture was accepted.

The generated output keeps 18,747 shrub/forest-floor records. Non-grass data is
packed into 86,726 matrices/metadata records, 2,130 prototypes and 47,138
batches; its 62,168 collision records cover donor and natural-infill trees. The
88 thin scenes total 1,088,371 bytes and packed assets 76,558,979 bytes, with
zero prefab-instance GameObjects, direct renderers or serialized colliders. The
combined 77,647,350-byte authoring payload is 83.91% below v7's 482,661,729
scene bytes; this comparison is serialized authoring payload, not runtime memory.

The distant source plan produces 16,000 collisionless trees in 81 streamed
backdrop scenes, 307 renderers and 349,465 vertices with zero skyline gaps or
wrong-side placements; its load/unload radii are 2/3. Global presentation replaces 45 canonical rock visuals
at audited anchors with 149 renderers and zero new colliders; exact legacy rock
collision remains authoritative and random rock scatter is not a source class.

The direct Editor benchmark passed at maximum main thread 66.0798 ms, maximum
yield 71.6469 ms and p95 yield 20.6325 ms. The production-service route passed
at 63.8666 / 68.1513 / 14.5884 ms with clean teardown. These bounded gates
passed, but they are not Player, full-world GPU, runtime-memory or 60 FPS evidence. The
ALP archive remains `ThirdPartyPrivatePhase1Presentation`,
`productionReady=false`, removable and blocked for distribution by missing
reviewed licence evidence. No vendor asset is reclassified as newly authored
production art.

The historical first full run `20260831-163439` passed generation and saved-data
validation for 88 cells: 37,678 original trees, 29,232 boundary trees, 26,127
shrubs and 22,990,564 grass instances, with zero cell errors. All 65,879 recovered
original inputs are accounted for by 37,678 accepted and 28,201 rejected records.
Post-capture EditMode is 75/75 passed after all camera edits
(`editmode-results-post-capture.xml`, ending 17:38:58Z); PlayMode remains 3/3 passed. Southern, home and
eastern rail-area captures passed technical checks, with bounded visual findings
recorded in `MAP_VEGETATION_REBUILD.md`. Independent serialized-output verification
passed all 88 cells, including every saved grass position, with zero errors or
warnings. The 49 legacy registrations/build prefix are unchanged; 88 vegetation
layers are registered. No old global vegetation category or legacy grass
renderer remains, and one explicit serialized wind component is active. These
counts alone do not establish visual clearance or runtime performance. The final
open-rail focus separately shows clear rails, ballast and portal; the southern
camera-clearance recapture shows ground and forest depth. Neither establishes
map-wide boundary closure or clearance of every railway segment.

Boundary visual acceptance remains limited near low technical ground: the
executed all-cell height audit found 710 boundary roots more than 10 m below
their paired wall elevation, including 461 below 20 m and 255 below 30 m.
That comparison is not a ground-error or canopy-visibility test. Neither those
counts nor a camera obscured by fir branches establishes that all map gaps are
hidden. The executed southern camera 04 reveals an under-terrain void from an
existing low patch, so normal high-ground boundary closure remains unaccepted.

## Historical v7 third-party presentation source lock — 2026-09-01

This section preserves the superseded v7 binding snapshot. Its “active” wording
is historical and does not override the v12 source lock above.

This section supersedes **Engelmann spruce and Chernobyl grass** as current
presentation sources; it does not supersede the reviewed Chernobyl pine, birch
and aspen bindings or alter the frozen donor map evidence documented below.

The active species target is **65% spruce, 20% pine, 7.5% birch and 7.5%
aspen**. The spruce whitelist contains exactly five ALP optimized prefabs:
`ConiferTreeBig01_Optimized`, `ConiferTreeBig02_Optimized`,
`ConiferTreeBig04_Optimized`, `ConiferTreeSmall03_Optimized` and
`ConiferTreeSmall04_Optimized`. `Big03` is excluded as an approximately
58-metre accent, `Small01` as weak/redundant and `Small02` because its texture
references are broken. Groups, demos and vendor scripts are not map sources.

Active tree provenance is therefore explicit: ALP supplies 43,492 spruce;
the existing licensed Chernobyl `Tree/Pine_*`, `Tree/Birch_*` and
`Tree/Aspen_*` prefab GUID bindings remain active for 13,382 pine, 5,018 birch
and 5,018 aspen. NatureManufacture supplies grass, grey willow and the reviewed
understory/debris subset; it is not the source of those three tree species.

The ALP package SHA-256 is
`39D47D1A7A8424A3C1F39DD55DD64ED6CEE42542A13466CC04FB40280126B827`.
Its classification is `ThirdPartyPrivatePhase1Presentation`,
`productionReady=false`. The reviewed archive has a
`unityassetcollection.com` redistribution-site marker and no purchase receipt or
licence proof, so it remains a removable private
Phase 1 source with an unresolved licence/provenance blocker. Nothing from it
may be described as production-ready or approved for distribution.

The Forest Environment and Meadow Environment archives are imported only as a
reviewed Finnish dependency closure. Forest has 22 seeds and 79 closure assets;
Meadow has 51 seeds and 156. Both imports have zero unresolved external GUIDs,
demo scenes, scripts or Editor assets. Active grass profiles use separate
detailed, regular and cross-mesh NatureManufacture prefabs for near/middle/far
geometry. The same subset supplies four grey willow variants, four ferns, two
moss variants, selected Armillaria/Russula mushrooms, one dead-grass variant,
branch-cluster litter and poplar-leaf litter. ALP supplies three boulders and two
stone variants. Project-owned Unity 6 HDRP wrappers own the runtime bindings;
vendor terrains and old render-pipeline tooling are excluded.

| Owned profile | Detailed LOD0 source | Regular LOD1 source | Cross-mesh LOD2 source |
|---|---|---|---|
| `NatureManufactureShort` | `prefab_grass_meadow_01_detailed_1` | `prefab_grass_meadow_01_1` | `prefab_grass_meadow_01_cross_1` |
| `NatureManufactureMeadow` | `prefab_grass_meadow_02_detailed_1` | `prefab_grass_meadow_02_2` | `prefab_grass_meadow_02_cross_1` |
| `NatureManufactureTall` | `prefab_grass_meadow_02_detailed_2` | `prefab_grass_meadow_02_6` | `prefab_grass_meadow_02_cross_3` |

Final presentation revision v7 retains these grass composites:

| Profile | Near / middle / far copies | Near / middle / far triangles |
|---|---:|---:|
| Short | 4 / 2 / 1 | 576 / 28 / 27 |
| Meadow | 3 / 2 / 1 | 330 / 28 / 28 |
| Tall | 2 / 1 / 1 | 348 / 15 / 30 |

V7 samples a 0.65 m candidate grid and dilates green texture eligibility by
three source texels. The dilation broadens instance metadata around narrow green
features; it does not add copies to the fixed per-instance composites above.
The v7 footprint calculation requires at least 0.77 m, but the non-decreasing
authoring update preserves the serialized 0.94 m safety floor from v5. The two
validated full-map runs therefore use 0.94 m for ordinary and agricultural-field
grass clearances; railway remains 1 m.

The existing full-resolution terrain colour mask remains part of authoring. A
candidate needs approved natural ground and must pass road/building/field/water
rules before its mapped texture colour can authorize grass. The new meshes do not
turn every green-looking rendered pixel into plantable ground.

Forest-floor binding v3 maps `BranchLitter01` from
`prefab_detail_branches_01` and `PoplarLeafLitter01` from
`prefab_detail_poplar_leaves_01_1`. Their explicit `DeadGrass02` and
`DeadGrass03` legacy-output aliases preserve existing output prefab GUIDs and
instance slots, so they add reviewed branch and leaf litter without changing
record counts. No standalone Finnish conifer-twig or cone scatter prefab, log,
root or stump pool is approved.

The five bird layers already present in the project are Wwise ambience events for
morning, day, evening, night and swamp. The supplied packages contain no usable
flying-bird mesh, rig or animation. The ALP bird prefab is an AudioSource-only
container with missing clip references and is excluded, so birds remain audible
ambience rather than simulated visual actors.

All historical generated counts, captures and tests below predate this source
lock. They remain donor/source-algorithm evidence. Active v7 pilot
`20260901-092200` in `cell_-4_-3` passed with 1,967 original trees, 1,727
boundary trees, 1,793 shrubs/forest-floor instances and 223,100 saved grass
instances from 620,500 candidates. Settings hash is
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569` and
fingerprint is
`fa868c935f994e13a0a1fb637a45c0184729ae9cf003c3c8e9431983e12b5da6`.
Its five-view HDRP capture passed on an NVIDIA GeForce RTX 4070 SUPER with zero
missing or unsupported shaders. This validates the representative view, not a
continuous grass carpet across the whole map.

The active source lock then completed full generation in `RunAllBatch` run
`20260901-092757`: 88/88 cells passed with zero errors. Fresh-process
`ValidateAllBatch` run `20260901-100750` passed the same 88/88 cells with
`validationOnly=true`. Both reports use settings hash
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569` and
source fingerprint
`55ee507db015d4e9fc68f3034e999487dfa174cb9ab8da3a165046c2114dc407`;
every per-cell field, fingerprint and count is identical.

| Active saved category | Count |
|---|---:|
| Original / boundary / total trees | 37,678 / 29,232 / 66,910 |
| Spruce / pine / birch / aspen | 43,492 / 13,382 / 5,018 / 5,018 |
| Shrubs and forest floor | 28,759 |
| Grass / candidates | 8,077,749 / 54,599,726 |

The 88 scenes total 482,661,729 bytes; largest `cell_-4_-3` is 27,079,109 bytes.
Fresh v7 GPU PlayMode passed 6/6 and the green texture-mask filter 15/15, both
with zero failures/skips. Current v7 `MapVegetation` passed 41/41 with zero
failures, superseding 40/40; the separate forest-floor binding-v7 filter passed
5/5. Evidence is
`Artifacts/Tests/VegetationGpuStreamingPlayModeV7GroundDebris.xml`,
`Artifacts/Tests/MapVegetationGrassTextureMaskEditModeV7.xml`,
`Artifacts/Tests/MapVegetationEditModeV7GroundDebris.xml` and
`Artifacts/Tests/MapVegetationForestFloorBindingV7.xml`. The full runs establish
active source, settings and serialized-output consistency. They do not extend
the pilot capture to map-wide visual acceptance or establish runtime performance.

The retained v6 fresh-process observation measured one heavy scene load at
608.743 ms total: 478.810 ms deserialization and 129.870 ms integration. It is
recorded in `Artifacts/VegetationRebuild/ValidateFullMapV6.log`; v7 was not
reprofiled by that measurement, and no streaming-hitch fix is claimed.

## Project and immutable source

`Config/DonorPaths.local.json` explicitly selects
`E:\GAYmDev_Studio\MySummerCar_Remake` as `UnityProjectDirectory`.
That checkout contains the current world, streaming, vegetation tools and licensed
plant assets. The similarly named `E:\GAYmDev_Studio\MySummerCar_Remake_Game`
contains only the isolated `Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/LicensedAxisNeutralArms`
content under `Assets/Game`; it is not the current map project. No files in either
the donor installation or the old project directory were changed by this audit.

The frozen source revision remains `msc-world-baseline-04a1.1-c3f2f337`.
Do not re-extract the current installed game and mix its meshes with this revision:
the existing canonical-source audit records shared-asset container drift.

The following SHA-256 values were checked again read-only on this date:

| Input | SHA-256 |
|---|---|
| External frozen `ExportedProject/Assets/_Scenes/GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| External normalized `WorldVegetationManifest.csv` | `279ebac82b1c9a2c27a0f1aae96936e7875ce71fff986f923b4753e8e876bdb1` |
| Project `Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv` | `a0f45c9eb50b2f7dff90caa9d848a0a20be463ce333eae19da576f91dd31a408` |

External root resolution from local configuration:

- Frozen export: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\raw\world\milestone-04a1\assetripper-unity-project\ExportedProject`.
- Normalized metadata: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\normalized\world\milestone-04a1`.
- `manifests/world/milestone-04a1` also contains the historical manifest copies.

See `Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md` for the complete source lock.
Donor hierarchy names used below are Editor-only evidence classification, never
runtime lookup or persistent entity identity.

## Coordinate contract

Raw donor world coordinates become project coordinates through:

```text
project = donor + (169.98, 1.611, -1040.625)
axes = identity; rotation = identity; scale = 1 metre per source unit
cell = (floor(project.x / 512), floor(project.z / 512))
```

The sanitized runtime baseline has already applied this conversion. The source
adapter reads `MeshFilter.transform.localToWorldMatrix` and returns project-space
points, with `PositionsAreProjectSpace = true`. Its caller must default to identity
mapping. Applying the garage translation a second time moves the whole forest.
Negative cell coordinates require floor, not truncation toward zero.

## What the tree source actually contains

The complete normalized placement table has 36,045 GameObject/Transform records;
the project geometry table has 13,509 records, including 3,842 reference-world
eligible records. `WorldVegetationManifest.csv` has only 26 rows:

- 16 `VegetationTree`: nine aggregate render meshes, two tree-wall render
  meshes, three aggregate tree colliders and two tree-wall colliders;
- seven `VegetationBush` aggregate meshes;
- three `VegetationGrass` ground-surface records.

These are not 16 individual trees and there is no verified table of one transform
per visible donor tree. Render trees are baked crossed cards. Read-only decoding
of the original aggregate meshes found 131,634 triangle-connected components
before seam welding, almost all four-vertex cards. Crossing cards have independent
vertices and slightly different base midpoints, so neither vertex-grid averaging
nor treating every quad as a tree preserves the source composition correctly.
Some cards have sloped base edges: selecting only the globally lowest vertex
would place a trunk at a card corner instead of its root.

| Source family | Project metadata paths | Frozen export meshes |
|---|---|---|
| Large tree atlas | `MAP/MESH/FOLIAGE/TREES1`, `TREES2` | `Mesh/TREES1_MeshPart1.asset`, `Mesh/TREES1_MeshPart0.asset` |
| Medium tree atlas | `MAP/MESH/FOLIAGE/TREES_MEDIUM1..3` | `Mesh/TREES_MEDIUM_MeshPart0..2.asset` |
| Small tree atlas | `MAP/MESH/FOLIAGE/TREES_SMALL1..4` | `Mesh/TREES_SMALL_MeshPart0..3.asset` |
| Shrub atlas | `MAP/MESH/FOLIAGE/BUSHES1..7` | `Mesh/BUSHES1_MeshPart0..6.asset` |
| Low wall | `MAP/MESH/FOLIAGE/TREEWALL_LOW` | `Mesh/TREEWALL_LOW.asset` |
| High wall | `MAP/MESH/FOLIAGE/TREEWALL_HI` | `Mesh/TREEWALL_HIGH.asset` |

Atlas family and source height are known. Exact botanical species are not proven
by those names, so the adapter records `Species = Unknown`; it does not invent
species evidence from prefab availability.

Read-only analysis of accepted r4 pilot inputs (`cell_-4_-3`) found:

| Source atlas | Accepted pilot trees | Height range, m | Median height, m |
|---|---:|---:|---:|
| Small | 739 | 1.286–4.261 | 2.816 |
| Medium | 555 | 2.585–8.526 | 5.612 |
| Large | 287 | 5.061–24.667 | 12.865 |

These values are recovered card heights and only describe that pilot. Its saved
`LODGroup.size × rootScale` diagnostic spans 1.21–26.08 m for original trees
(median 3.96 m) and 4.93–23.90 m for boundary trees (median 14.01 m). That proxy
is not a rendered silhouette measurement. It supports the implemented height
normalization rather than arbitrary enlargement of all source trees.

For the superseded Chernobyl-grass/Engelmann-spruce pass, the overhead camera's 563.2 m
vertical span and then-active final-LOD thresholds around 0.022–0.028 could cull
trees below roughly 12–16 m; Engelmann vertical billboards were also thin from
directly above. Those measurements explain historical captures only and are not
the current ALP LOD contract. No original height or root pivot was changed to
make an overhead screenshot look fuller.

## Implemented reader

`Assets/Game/Editor/Vegetation/MapVegetationDonorSource.cs` exposes
`MapVegetationDonorSource.Read(IEnumerable<GameObject> explicitRoots)`.

It reads only supplied objects carrying project-owned
`DonorWorldBaselineEntityMetadata`; it excludes the prior generated forest and
BetterMSC's unrelated detail geometry. Disabled donor renderers remain readable
as source evidence. It neither enables them nor imports their visual assets.

The returned snapshot contains trees, shrubs, lower boundary segments, billboard
source paths, source-method counts and actionable diagnostics. Each source point
has stable source identity, source path/GUID, inferred height, project position,
cell and derivation method. `SourceHash` is the actual sanitized mesh asset file's
SHA-256, labelled `SanitizedMeshAssetSHA256`; cross-mesh paired cards retain both
source identities and hashes. The snapshot also records the canonical scene hash.

Root recovery:

1. Build triangle components and weld only coincident seam vertices at 0.1 mm
   identification precision. Output positions are never snapped to that precision.
2. Identify vertical cards by their two lower and two upper vertices, including
   sloped base edges.
3. Match perpendicular cards with compatible height/base elevation and central
   edge intersection; pairing works across source mesh-part splits.
4. Use the intersection of their original bottom edges for X/Z. No random
   position offset, forest cluster or road-edge planting enters donor evidence.
5. Label this `CrossedCardRootReconstructed`, not an original serialized transform.
6. Retain a lone card as `SingleCardRootReconstructed` at its exact base midpoint,
   with a per-location lower-confidence diagnostic. Multiple compatible partners
   receive a review diagnostic. Placement/exclusion rules remain authoritative.
7. Bounded non-card components use `GeometryComponentRootReconstructed` and their
   root-collar footprint. Invalid/unbounded components are skipped with reasons.
8. `IndividualDonorTransform` is reserved for independently classified standalone
   tree metadata with bounded tree geometry, never aggregate pivots.

Boundary segments come from the lower open edges of the actual low/high wall
meshes. Missing readable lower edges are reported; no rectangular map boundary
is silently substituted. The reader does not claim all original geometry holes
can be discovered from tree walls alone.

The current baseline is already remediated: `BetterMscMapRemediationImporter`
replaces the meshes beneath the canonical `TREEWALL_LOW` and `TREEWALL_HI`
metadata with selected BetterMSC wall geometry. Accordingly, boundary segments
describe the current sanitized baseline walls, not necessarily the unchanged
frozen donor meshes listed above. Their actual mesh hashes preserve this
distinction; the canonical scene revision alone does not prove wall identity.

Runtime gameplay state, IDs, existing assets, scene transforms and donor payloads
are not changed by the reader. It lives in the existing Editor-only `MSC.Editor`
assembly and has no runtime dependency impact or save migration.

## Ground and exclusion evidence

Natural ground records:

| Exact path | Category | Stable ID |
|---|---|---|
| `MAP/MESH/TERRAIN_OBJ/Grass1` | `VegetationGrass` | Read from frozen entity table |
| `MAP/MESH/TERRAIN_OBJ/Grass2` | `VegetationGrass` | `2dee048c54a6fce0a349dcae54f43e8b` |
| `MAP/SkijumpHill/grass` | `VegetationGrass` | `38f547477bf6c6e69ea47c4ea93a479a` |

The existing remediation also adds `BetterMSC/MissingTerrain`, category
`Terrain`, stable ID `49a40657708f9739cbba81a119d77cbe`, aligned to Grass1
(`bettermsc-remediation:missing-terrain` is the importer hash input, not the
serialized stable ID).
It is documented as continuous ground filling original map voids in
`Docs/WorldBaseline/BETTERMSC_MAP_REMEDIATION_REPORT.md`. The vegetation defaults
initially authorized only the three paths above. Executed
`PrepareApprovedArtBatch` now adds this exact path to the configured ground
list. The r4/r5/r6b saved-data pilots include this reviewed ground configuration;
their visual acceptance is tracked separately in the integration report.
Omitting the patch initially was a coverage limitation, not evidence that no
ground existed there.

Read-only mesh inspection supports an exact-path opt-in: the current
`BetterMscMapRemediation/Source/Mesh/default_20.asset` has SHA-256
`7538de76493615569c4d2c3bb9c27214020483b7cfa73915e84fcdef6fa3232d`, 1,069 finite
vertices, 1,087 nondegenerate triangles and 23.19 metres of local height relief.
Its triangles cover approximately 1,089,811.9 square metres in projection,
over 99.99% at slopes below 52 degrees. The saved scene binds the same mesh to
an enabled non-trigger MeshCollider on `WorldSolid`, and uses the existing
Grass1 compatibility material
`M06B2_da5bc03c62a0f174197555e90357aac9.mat` (GUID
`bed942f7f1789fa449a58b096052c871`). This is existing terrain-patch geometry,
not an inferred surface from a name or colour. An exact metadata-path opt-in
must retain water, field, road and slope exclusions; it does not authorize every
`Terrain` category object or the whole `WorldSolid` layer.

Mandatory open-area exclusions include:

- `MAP/MESH/TERRAIN_OBJ/Fields`, `Field`, stable ID
  `2987bdc3576114fda4b4b319bd0d4681`;
- `MAP/MESH/TRACKFIELD`, `Field`, stable ID
  `3126994543fb175171b95b18b546d349`;
- `MAP/MESH/TERRAIN_OBJ/DirtRoad`, road/driveway variants and paved road;
- railroad base, rails, sleepers and tunnel, not merely paved-road materials;
- all current project gameplay/interaction reservations and supplemental job-site
  overlays, including strawberry rows and farm/septic-yard access.

The legacy reference table reports 29 Road, two Bridge, two Field, one Terrain
and 12 Water records. These category counts do not prove exhaustive geometry:
some rail signs are misclassified Road, while visible lake tiles are StaticProp
(`MAP/LakeSimple/Tile`, `MAP/LakeNice/Lake/Tile`). WaterUnder, WaterColor, LAKEBED,
LakeSmallBottom and foliage-water helpers are presentation/under-map proxies whose
XZ footprints can cover dry land; their entire bounds must not exclude all plants.
The actual lake tiles can also extend beneath raised dry ground. The current
query compares approved ground height with the real water plane (0.02-metre
tolerance), then derives category-specific margins from exposed water, wet
ground/water intersections and bank edges. It does not turn an entire lake-tile
rectangle or a covered submerged lower ground mesh into a shoreline. Use actual
surface geometry, elevation and project metadata together.

The r4 road probe at camera `(-1849.343, 80.764, -1281.790)` inspected twelve
saved grass points. Every point re-resolved to the exact original X/Y/Z on
`Grass2`; the current query rejected automatic grass at the camera's own X/Z.
The camera was 1.700 m above actual DirtRoad triangle 4850, while nearby grass
anchors were 0.526–2.175 m outside its nearest footprint. This confirms the
inspected verge, not every road on the map. Final validated v7 grass road,
building, driveway, artificial and agricultural-field clearances are 0.94 m;
railway remains 1 m.
The 0.65 m candidate grid and three-texel green dilation do not bypass those
clearances.

The existing `Phase1TreePlacementExclusionIndex` is reusable geometry evidence,
but it has no Field category branch and uses hard-coded tree-only clearances.
It cannot satisfy the new category-specific rules unchanged.

## Existing tools and pitfalls

- `Phase1ForestRemediationBuilder` currently obtains approximate grid-averaged
  anchors, mixes in BetterMSC geometry and generates additional clusters/roadside
  trees. Its historical 84,059 tree count is not an exact donor-tree count.
- Its former grass pass samples only Grass1 and stores 389,530 instanced positions;
  its renderers were disabled after user rejection. Do not reactivate that old
  presentation merely to satisfy a grass count.
- `WorldTransferExtractor` preserves original and converted transforms and
  mesh/semantic/provenance columns. Reuse frozen tables rather than re-extracting.
- `M05C_StaticBatchSubsets.csv` reconstructs precise subsets of combined meshes.
  Whole static-batch bounds are unsuitable building/road exclusion volumes.
- `MAP/MESH/FOLIAGE/TREEWALL_LOW/treewallcoll` is the approved outer safety
  collider. Hiding its visual wall does not authorize deleting that safety fence.

## Validation status

Current data-generation/test results are recorded at the top of this document.
The guarded attempts and pilot chronology below are historical evidence.

Executed for this audit: PowerShell file/manifest inspection and SHA-256 checks;
read-only Python decoding of frozen Unity mesh vertex/index buffers; geometry
component and crossed-card compatibility counts. Those scripts did not generate
assets or claim a playable-map result.

Executed Unity source reads in the first guarded pilot attempts produced 65,879
tree inputs, 51,132 shrub inputs and 823 boundary segments from 50 source scenes.
These are recovered source records before placement/exclusions, not successful
placement counts. Both attempts recorded `passed: false`; no pilot or map-wide
acceptance is inferred from those counts.

The third pilot, `20260831-152639`, subsequently passed saved-data validation for
`cell_-4_-3`; its counts and scope are recorded in `MAP_VEGETATION_REBUILD.md`.
Its first real HDRP capture failed visual/material inspection. Later art or
ground-setting changes invalidate using that historical data pilot for full
generation until the pilot and capture are rerun.

The denser r5 pilot `20260831-161043` passed saved-data validation and its five
HDRP views subsequently passed; the road and deep-buffer frames were visually
reviewed. That evidence is archived under `VisualAuditPilotR5/` and does not
establish every road or boundary sector. The final pre-full r6b pilot
`20260831-162325` then passed with tree minimum spacing reduced to 1.5 m:
1,967 original trees, 1,727 boundary trees, 1,827 shrubs and 688,405 grass
instances in the same cell. Its source fingerprint is unchanged; reducing
spacing did not shift source coordinates. Current settings/generated hashes are
recorded in `MAP_VEGETATION_REBUILD.md`. Matching r6 capture passed five
technical views with no unsupported materials; the road and front transition
were reviewed. The fifth view is partly obstructed by nearby fir branches and
does not establish complete boundary occlusion. The subsequent full run passed
all 88 saved cells as recorded above. The earlier 63/63 EditMode r5 evidence was
superseded by `editmode-results-final.xml`, which passed 75/75 including 12
capture-scope cases. The subsequent post-capture rerun passed the same 75/75
after all camera edits in 2.2062821 seconds; the unchanged PlayMode suite passed
3/3 in r4. The final independent `--scan-grass` rerun also passed all 88 cells
with no errors or warnings and references that latest test XML.

The r6b run also evaluates the entire original-tree source plan: 37,678 accepted
and 28,201 rejected records, accounting for all 65,879 tree inputs. The spacing
change reduces `MinimumTrunkSpacing` rejections from 15,363 in r5 to 6,693.
`NoAllowedGround` (10,179) and actual water (6,290) remain separate reasons.
Those were planning results in r6b; the full saved original-tree sum subsequently
matched all 37,678 accepted records.
`original-tree-source-rejections.csv` records every original rejection with
source ID/path, cell and exact coordinates, including source cells outside the
generated natural-ground set. Per-cell rejection reports alone would omit those
outlying inputs. Review the reasons rather than silently moving rejected trees
or treating recovered-input totals as the number of surviving trees.

Eight meaningful Unity EditMode cases were added in
`Assets/Game/Tests/EditMode/WorldBaseline/MapVegetationDonorSourceEditModeTests.cs`:
offset card centers, cross-mesh pairing and negative cells, parallel independent
cards, unequal heights, sloped base edges, wall lower-edge extraction, ignoring
unmarked generated vegetation and source/ground fingerprint invalidation.
Unity execution results belong in the integration
report; adding tests alone is not a passing result.

`MapVegetationVisualAudit.CapturePilotBatch` renders the saved pilot through the
existing HDRP pipeline, with the global baseline and neighbouring canonical
cells loaded. It now writes overhead and four eye-level PNGs for a boundary
pilot, including both the front transition and a separate deep-buffer view,
plus a JSON capture
report under `Artifacts/VegetationRebuild/VisualAudit`. Temporary inspection
lighting is fixed EV14 with a 100,000-lux sun and fog disabled; it is explicitly
not an Enviro/gameplay lighting acceptance test. Captures require a graphics
device and never save or modify input scene files. Shader/reference errors,
magenta pixels, nearly uniform images and exact camera coordinates are reported.
Frustum/cull eligibility counts are not represented as pixel-visible coverage.
A pilot without boundary trees is labelled cell-edge context, not proof of
world-boundary coverage. Actual execution/PNG inspection results must be added
to the integration report after running the helper.

Read-only r4 inspection found the front camera's target at
`(-1889.010, 79.441, -1271.324)`, 74.31 m from the nearest recovered low wall
segment in a 75 m band. The added deep view selects an actual generated tree
within 15 m / 20% of the wall and grounds the camera on a nearby saved grass
point; no invented trees or forced LODs are introduced. Available registered
generated neighbour layers load within the configured forest loading radius
(currently two cells / 5×5), while canonical base scenes remain 3×3 source
context. Both radii and missing neighbour cells are reported. Therefore a
pilot-only horizon is not claimed to demonstrate complete map vegetation or
universal boundary occlusion. The optional CLI argument
`-vegetationAuditCell cell_X_Z` captures another existing generated cell into
`VisualAuditCells/cell_X_Z/`, without modifying settings or default pilot-gate
evidence under `VisualAudit/`.

Optional paired `-vegetationAuditFocusX` / `-vegetationAuditFocusZ` arguments use
finite invariant coordinates inside that overridden cell. Home focus is
`(155, -1035)`; the executed open-rail focus is `(2316.162, -998.896)` in
`cell_4_-2`. The earlier measured axis point `(2415.584, -930.716)` was inside
`RailroadTunnel`, beneath its roof, and did not frame exposed rails. The whole-cell
overhead is unchanged, while local camera selection
uses the focus and `06_Focus_Oblique` records an explicit look target. Its target
height reuses nearby saved grass/tree height rather than pretending to raycast
at the focus. Category-only capture now compares selected category fingerprints;
unselected preserved categories remain context with separately reported hashes.
These tooling changes do not modify generated vegetation. Executed capture
results are recorded below separately from the tooling's intended behavior.

Post-generation inspection targets are `cell_3_-6` (distant southern boundary),
`cell_0_-3` (mapped home and shoreline) and `cell_4_-2` (eastern railway/road
and boundary, with repair workshop in neighbour `cell_3_-1`). Their selection
uses the r6b woody plan, source wall segments, rejection coordinates and frozen
mapped entity rows. Southern/home captures passed five technical views each;
the eastern rail-area capture passed six. All have zero unsupported shaders.
The home focus visibly shows clear roof/foundation and readable access. The
southern recapture moved only its deep camera by 1.580974 m horizontally and
-0.003270 m vertically; reviewed ground and forest depth are now visible with
less near-trunk obstruction. Other trunks/branches and the camera-04 low-patch
void remain. The context-only search uses saved grass within 8 m horizontally,
2 m vertically and farther than 2.25 m from audited-cell tree roots; it does not
modify instances or pilot gates.

The final eastern open-rail `06_Focus_Oblique` visibly resolves unplanted rails,
ballast and portal, with the adjacent field open and grass around the clear bed.
This validates that sector, not the whole railway. The earlier tunnel focus
(source bed Y approximately 0.144 m, roof Y approximately 5.151 m) is archived in
`VisualAuditCellsRailTunnelFocus`; pre-clearance southern images are archived in
`VisualAuditCellsSouthBeforeClearance`. Both final recaptures exited 0 with no
unsupported shaders. See the integration report for counts and full limitations.

After the first failed capture, the helper was corrected to use an actual
`HDAdditionalLightData` directional light with `LightUnit.Lux`, plus a neutral
gradient sky for ambient illumination. The report now records the settings
hash, saved generated fingerprint and technical capture pass/fail; unsupported
materials cause failure only after actual PNGs and diagnostics are saved.

## Historical v7 package binding boundary

The following v7 binding description is preserved for provenance chronology;
the current Forest Grass02/01/03 binding is recorded in the v12 section above.

`MapVegetationAlpSpruceBindings`, `MapVegetationGrassBindings` and
`MapVegetationForestFloorBindings` are the current project-owned adaptation
boundary. The selective importers preserve source GUID/dependency evidence while
keeping vendor payload outside Git. Wrapper reports record source/output hashes,
LOD source identity and `productionReady=false`; generated art is removable
without changing world coordinates, gameplay anchors or save identity.

The three grass profiles use reviewed detailed/regular/cross source prefabs, not
the older two-LOD binding described below. V7 fixes near/middle/far copy counts at
4/2/1 for Short, 3/2/1 for Meadow and 2/1/1 for Tall. The resulting triangle
budgets are 576/28/27, 330/28/28 and 348/15/30 respectively. Broader three-texel
green eligibility grows instance/catalog metadata, not these composites.

The existing indirect grass shader is still a simplified hemisphere-lit path
and does not by itself prove HDRP shadow, normal-map or wetness parity. The v7
capture passed and its roads were visually reviewed as clear, but one pilot does
not establish a continuous carpet or full-map visual acceptance.

## Historical grass/spruce adaptation with retained non-spruce dependencies

The following Chernobyl-grass, then-active spruce and older NatureManufacture
two-LOD results are retained as evidence for the superseded 50/35 pass. Any
Chernobyl material binding still referenced by the active pine/birch/aspen
prefabs remains a licensed runtime dependency and is not reclassified as
historical merely because that earlier grass/spruce pass was superseded.

`MapVegetationMaterialBindings.BuildMaterials()` creates or refreshes five
project-owned HDRP/Lit compatibility materials for existing Chernobyl package
foliage; `Apply(instance)` binds them only on newly generated renderers.
Vendor prefabs, materials, texture bytes and texture import settings remain
unchanged. The five source material GUIDs are explicit in the Editor helper;
mapping does not depend on `shader.isSupported` in a graphics-disabled process.
Compatible existing spruce materials remain in use.

The vendor's active `AE/Leaves` shader reads `_Base_Color` and `_Normal`.
Several stored `_MainTex`, `_Albedo` and `_NormalMap` properties are obsolete,
contain different or missing texture GUIDs, and are not valid conversion input.
The adapter keeps active albedo/normal UV transforms, tint RGB and alpha cutoff,
forces tint alpha to one because the source shader ignores that component, and
uses double-sided alpha clipping with a nonmetallic HDRP surface. Vendor MRO
mask packing, wind deformation and transmission are not claimed equivalent.

Preparation writes `Artifacts/VegetationRebuild/material-bindings-provenance.json`
with source material GUID/path/SHA-256, generated material GUID/path/SHA-256,
active albedo/normal paths and hashes, cutoff, conversion limits and the existing
licensed Chernobyl package identity (product ID 221608). Generated materials
live under `VegetationRebuild/Materials`; no original My Summer Car textures are
used by this adapter. Report hashes are executed evidence only after preparation
runs, not values inferred from the presence of this helper.

Preparation has now executed successfully (`grassProfiles=3`,
`materialOverrides=5`, exit 0). Twenty recorded hashes were independently checked
against the current source materials, generated materials and active textures;
all matched. The material provenance report's SHA-256 is
`247fb6464a436c9868f1a57304639ca3a719c172b43796e1bc542ef1c2b6827b` for this run.

`MapVegetationGrassBindings` separately reuses existing licensed NatureManufacture
grass prefabs, keeping their authored UVs, pivot, normalized height and two LODs.
The preparation report `Artifacts/VegetationRebuild/GrassArt/provenance.json`
records source/output atlas hash matches and triangle counts: Short 86/41,
Meadow 14/3, Tall 14/4. Its generated copies do not modify vendor meshes or atlas
importers. The reused GPU shader still has simplified hemisphere colour, not
HDRP light/shadow reception or a physically based normal/wetness response;
new mesh/texture bindings do not prove that remaining shader limitation fixed.

Active v7 generation and independent fresh-process saved-data validation have
passed 88/88; the representative pilot and matching HDRP capture have also
passed. Wider wall coverage, low-patch voids and real in-game memory, CPU/GPU and
streaming performance remain outside that data acceptance.

One recommended next milestone is a **measured pilot of the compact woody
runtime catalog** described in the presentation and streaming reports. It must
preserve source positions, selected IDs, LOD/material/collider references, catalog
ownership and unload behavior while measuring cold/warm scene load and sliced
activation. No donor-code or production-art claim is introduced; selected
third-party assets remain private, removable and subject to the provenance
limits above.
