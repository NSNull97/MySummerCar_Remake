# Map vegetation rebuild

Date: 2026-08-31. Tool identity: `msc.map-vegetation-rebuild.v1`.

## Active v12 rebuild — accepted 2026-09-02

Run `20260902-001303` generated and validated all 88 cells with report SHA-256
`F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`.
Fresh validation run `20260902-004112` passed 88/88 with
`validationOnly=true`; report SHA-256 is
`A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`.
The runs share settings hash
`7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d`
and source fingerprint
`2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`.
The exact comparer in
`Artifacts/VegetationRebuild/full-validate-exact-v12.json` passed every cell
report plus 268 non-report artifacts, aggregate fingerprint
`a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169`.

### Active placement totals

| Saved or planned category | Count |
|---|---:|
| Accepted donor originals | 37,678 |
| Deterministic natural infill | 24,490 |
| Original-tree category, including infill | 62,168 |
| Grounded near-boundary trees | 5,811 |
| Total near trees | 67,979 |
| Spruce | 44,201 |
| Pine | 13,576 |
| Birch | 5,052 |
| Aspen | 5,150 |
| Shrubs and forest floor | 18,747 |
| Grass | 5,157,636 |
| Grass candidates | 36,044,800 |
| Grass profiles, 46/32/22 | 2,368,747 / 1,651,618 / 1,137,271 |

Natural infill uses the signed, donor-surrounded policy with a hard cap of 65%
of accepted originals. It accepted 24,490 placements. Roads, yards, fields,
water, routes and OpenSpace remain hard exclusions. The grounded near-boundary
layer accepted 3,189 inward and 2,622 outward trees. The collisionless distant
plan accepted 16,000 trees separately from the 67,979 near-tree total.

### Active grass and packed-cell representation

Grass profiles bind only Forest Environment `Grass02_3`, `Grass01_3` and
`Grass03_3`; selection is 46/32/22 and no cereal or seed-head family is used.
The candidate grid is 0.8 m at density 0.96. Each record uses 16/8/6 authored
clumps for near/middle/far presentation with LOD distances 18/42/60 m. The
green-texture/local-carpet evidence may connect noisy green regions, but it
never bypasses geometry or gameplay exclusions. Corrected provenance SHA-256 is
`76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`.

The three non-grass categories are saved as category-local packed assets. The
88 cells contain 86,726 matrices and the same number of placement metadata
records, 2,130 prototype references, 47,138 batches and 62,168 donor/infill
collision records. Packed assets total 76,558,979 bytes and the 88 thin scene
shells total 1,088,371 bytes. There are zero prefab-instance GameObjects, direct
mesh renderers and serialized collider components. Combined authoring payload
is 77,647,350 bytes versus v7's 482,661,729 scene bytes, a measured 83.91%
reduction in serialized vegetation payload; this is not a runtime-memory claim.

The independently streamed backdrop has 16,000 trees in 81 scenes, 307
renderers, 349,465 vertices and zero colliders. Exact skyline validation found
zero gaps and zero wrong-side/inside-enclosure placements. Near vegetation uses
loading radius 1; backdrop uses load/unload radii 2/3. The global presentation
contains 45 canonical rock replacements, 149 renderers and zero new colliders;
legacy rock collision remains in place and random rock scatter is absent.

### Active v12.2 grass lighting hotfix

The project-owned `MSC/HDRP/Vegetation Indirect` forward pass now evaluates the
HDRP Lit LightLoop instead of returning an independent hemisphere-lit colour.
The shader keeps GUID `49f3045c658005748ad888e65476f1ed` and has SHA-256
`FAD25AA707CEA1BBDC24EBB4899CDF880B6C6AE8803C1CA1E96EB612754D81D2`.
Existing material, profile and catalog references therefore remain valid; no
vegetation cell, packed catalog or grass instance was regenerated.

The fail-before night-emission audit
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-083140-659-p26348`
rendered the zero-radiance frame
identically to its lit control: mean-luminance and bright-pixel ratios were
1.0 / 1.0. The post-fix audit
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-090040-053-p20132`
passed at 0.0 / 0.0 using the
same selected cell population of 126,982 saved grass records and 712 GPU
batches. This supersedes the simplified-unlit/hemisphere-lighting limitation
described in the older implementation sections below; those paragraphs remain
only as chronological pre-v12.2 evidence.

The final extended `MapVegetationNightEmissionAudit`
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-093656-076-p39636-2f2d286bd4d648a0a3b5002e31080da2`
passed with report SHA-256
`F6D9748BBC065CF154DB50BF80B6B16C3209159C0A227205BA73202D3F8DDFC8`.
Directional-control mean luminance was 0.2896735966,
punctual-control mean luminance was 0.02317777649, and zero-radiance mean,
maximum and bright-pixel fraction were exactly zero. Directional and punctual
mean/bright ratios were all zero over the same 126,982 records and 712 batches.
The final focused D3D11 EditMode suite, including
`MapVegetationNightEmissionAuditTests` and
`VegetationIndirectMotionVectorContractTests`, passed 40/40 in
`Artifacts/VegetationRebuild/Tests/GrassLightingHotfix.Final4.20260902.xml`,
SHA-256
`B3D743D7E9479B9510B5BE685C7F3B3A3A235D01F6645C866BC08ED9A5EBBDCA`.
The day visual audit report
`Artifacts/VegetationRebuild/VisualAuditCells/cell_-4_-3/capture-report.json`
passed all seven views with no missing or unsupported shader and zero magenta
fraction in every view; report SHA-256 is
`1417C8CAE6D9179EDFEB720C0A881599AB5FDE61764B51C8085228D22D38F237`.

Compact grass draws now use camera motion vectors in
`VegetationWorldRenderer`. The current custom object-motion pass does not yet
implement the complete HDRP normal-buffer/MRT/stencil contract, so exact
wind-deformation velocity remains explicit debt. `MapVegetationGrassBindings`
now authors smoothness 0.04, specular F0 0.018, black emission and
`MaterialGlobalIlluminationFlags.EmissiveIsBlack` whenever grass materials are
regenerated in the future. Active material YAML was not reserialized; current
runtime values resolve from shader defaults plus existing `m_LightmapFlags: 4`.
This hotfix did not regenerate the current payload.

The bounded evidence does not measure full-map LightLoop performance or replace
a manual day/night gameplay pass under Enviro. Those two gates remain pending.
The imported grass presentation remains removable private Phase 1 content with
`productionReady=false`.

### Active validation and performance boundary

The direct packed-scene benchmark passed its Editor gate at maximum main thread
66.0798 ms, maximum yield interval 71.6469 ms and p95 yield 20.6325 ms. The
production streaming-service benchmark passed at 63.8666 / 68.1513 / 14.5884 ms
and completed cleanup with no remaining owned scenes, renderers or valid GPU
buffers. Core PlayMode passed 22/22; targeted EditMode passed 77/77; the v12
pilot HDRP capture was accepted.

This verifies deterministic v12 authoring, the saved representation and the
bounded Editor benchmark. It is not a Player-build, complete-world GPU, 60 FPS
or runtime-memory acceptance claim. ALP/vendor art remains removable private
Phase 1 presentation with `productionReady=false`; the ALP licence/provenance
blocker remains. No stable ID, save DTO, terrain, road, gameplay or completed
00–08A API migration is introduced. The next milestone is one integrated Player
traversal and long-session memory benchmark using this exact v12 payload.

## Historical v7 package revision — 2026-09-01

This section and the following v7 full-map result preserve the superseded
2026-09-01 snapshot. “Active” inside that snapshot means active at that date.

The active rebuild contains **65% spruce, 20% pine, 7.5% birch and 7.5%
aspen**. Its validated total of 66,910 trees is 43,492 / 13,382 / 5,018 / 5,018
after largest-remainder allocation. The former 50/35 mix, Chernobyl grass,
Engelmann spruce and older run IDs below remain historical evidence, not
acceptance of this revision; reviewed Chernobyl pine/birch/aspen bindings remain
active.

Current art bindings are deliberately selective:

- Spruce uses reviewed ALP `Big01`, `Big02`, `Big04`, `Small03` and `Small04`
  optimized prefabs through project-owned HDRP wrappers and corrected 4/5-stage
  LOD thresholds. `Big03` is excluded as an approximately 58-metre accent,
  `Small01` as weak/redundant and `Small02` for broken texture references. Group
  prefabs, demos and vendor scripts are excluded.
- Pine, birch and aspen retain the reviewed licensed Chernobyl
  `Tree/Pine_*`, `Tree/Birch_*` and `Tree/Aspen_*` prefab GUID bindings at
  13,382 / 5,018 / 5,018. NatureManufacture does not supply these active trees.
- Grass uses final presentation revision v7 from the Finnish NatureManufacture
  subset. Short, Meadow and Tall wrappers each bind a detailed prefab for LOD0, a regular
  prefab for LOD1 and an authored cross-mesh prefab for LOD2. The candidate grid
  is 0.65 m; grass remains compact indirect instance data.
- The existing full-resolution green terrain texture mask remains authoritative
  after natural-ground and gameplay exclusions. V7 dilates green eligibility by
  three source texels to fill narrow green/adjacent regions; density still does
  not authorize grass on roads, buildings, water or fields.
- Forest-floor accents use separate pools for ALP rocks/boulders/stones, Finnish
  grey willow shrubs, and fern/moss/selected mushrooms/dead grass understory.
  Binding v3 adds `BranchLitter01` from `prefab_detail_branches_01` and
  `PoplarLeafLitter01` from `prefab_detail_poplar_leaves_01_1` while preserving
  the existing `DeadGrass02`/`DeadGrass03` output slots and prefab GUIDs.
  This is a presentation replacement; instance records and category totals stay
  unchanged.
  Rocks retain authored size; sparse accent GameObjects do not replace the dense
  indirect grass carpet.
- The reviewed subset now provides branch-cluster and poplar-leaf litter. It does
  not provide a standalone Finnish conifer-twig or cone scatter prefab, log, root
  or stump pool.
- Bird ambience already comes from five project-owned Wwise layers: morning,
  day, evening, night and swamp. The package bird prefab is an AudioSource-only
  object with missing clip references; there are no flying-bird meshes, rigs or
  animations to place.

The ALP archive SHA-256 is
`39D47D1A7A8424A3C1F39DD55DD64ED6CEE42542A13466CC04FB40280126B827`.
It is classified `ThirdPartyPrivatePhase1Presentation` with
`productionReady=false`. No reviewed purchase receipt or licence proof is
present, so the ALP payload is a removable private Phase 1 dependency and a
licence/provenance blocker for distribution or production-ready status.
NatureManufacture content is likewise imported as a reviewed dependency closure,
not by importing either complete demo package. Forest has 22 seeds / 79 closure
assets and Meadow 51 / 156; both have zero unresolved dependencies, demo scenes,
scripts and Editor assets.

Streaming changes pace at most one automatic presentation-layer load per refresh,
prepare it asynchronously to 0.9 at priority -1 and separate final activation by
a frame. Grass upload work is limited to 4 operations / 8,192 instances /
cooperative 1 ms per frame; metadata preparation is incremental at 4 operations /
256 profile records / cooperative 1 ms. This reduces secondary work only. Dense
cell measurements still attribute the dominant main-thread pause to native scene
deserialization/integration in `LoadSceneOperation.CompleteAwakeSequence`; the
hitch is not fixed.

Final presentation revision v7 retains the following grass composites:

| Profile | Near / middle / far copies | Near / middle / far triangles |
|---|---:|---:|
| Short | 4 / 2 / 1 | 576 / 28 / 27 |
| Meadow | 3 / 2 / 1 | 330 / 28 / 28 |
| Tall | 2 / 1 / 1 | 348 / 15 / 30 |

Its footprint calculation produces a 0.77 m minimum, but the non-decreasing
authoring update retains the serialized 0.94 m v5 safety floor. Both validated
full-map runs use 0.94 m for ordinary and agricultural-field grass clearances;
railway remains 1 m. Broader texture eligibility can increase saved instance
records and catalog/tile metadata; it does not increase the fixed mesh-composite
copy count in the table.

Pilot `20260901-092200` for `cell_-4_-3` passed with 1,967 original trees, 1,727
boundary trees, 1,793 shrubs/forest-floor instances and 223,100 grass instances
from 620,500 candidates. Its settings hash is
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569` and
fingerprint is
`fa868c935f994e13a0a1fb637a45c0184729ae9cf003c3c8e9431983e12b5da6`.
The matching graphics-enabled v7 capture passed five views on an NVIDIA GeForce
RTX 4070 SUPER with zero missing or unsupported shaders. This evidence approves
that pilot, not a claim that every map surface forms an unbroken grass carpet.
The subsequent full generation and fresh-process saved-output verification are
recorded below.

## Historical v7 full-map result — completed

`RunAllBatch` run `20260901-092757` passed all 88 cells with zero errors.
Fresh-process `ValidateAllBatch` run `20260901-100750` passed all 88 cells with
`validationOnly=true`. Both reports use settings hash
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569` and
source fingerprint
`55ee507db015d4e9fc68f3034e999487dfa174cb9ab8da3a165046c2114dc407`;
every per-cell field, fingerprint and count is identical.
The source reports are
`Artifacts/VegetationRebuild/20260901-092757/report.json` and
`Artifacts/VegetationRebuild/20260901-100750/report.json`.

| Active saved category | Count |
|---|---:|
| Original trees | 37,678 |
| Boundary trees | 29,232 |
| Total trees | 66,910 |
| Spruce | 43,492 |
| Pine | 13,382 |
| Birch | 5,018 |
| Aspen | 5,018 |
| Shrubs and forest floor | 28,759 |
| Grass | 8,077,749 |
| Grass candidates | 54,599,726 |

The active output has 88 scenes totalling 482,661,729 bytes; largest
`cell_-4_-3` is 27,079,109 bytes. Fresh v7 GPU PlayMode passed 6/6 and the green
texture-mask filter 15/15, both with zero failures/skips. Current v7
`MapVegetation` passed 41/41 with zero failures, superseding the old 40/40
result; the separate forest-floor binding-v7 filter passed 5/5. Evidence:
`Artifacts/Tests/VegetationGpuStreamingPlayModeV7GroundDebris.xml`,
`Artifacts/Tests/MapVegetationGrassTextureMaskEditModeV7.xml`,
`Artifacts/Tests/MapVegetationEditModeV7GroundDebris.xml` and
`Artifacts/Tests/MapVegetationForestFloorBindingV7.xml`. The matching v7 pilot
capture passed. These results establish full-map generation and deterministic
saved-data validation, while the capture remains representative rather than
map-wide visual acceptance.

The retained fresh-process v6 measurement observed one heavy scene load at
608.743 ms total, split between 478.810 ms deserialization and 129.870 ms
integration. V7 was not reprofiled by that observation. It remains Editor
evidence rather than a Player benchmark and leaves the streaming hitch unresolved.
The measurement is recorded in
`Artifacts/VegetationRebuild/ValidateFullMapV6.log`.

## Historical first-pass result

**Full-map generation and saved-cell validation passed** in run
`20260831-163439`: all 88 cells completed, every cell has zero validation errors,
the final report has `passed: true` with an empty failure, and Unity exited 0.

| Saved category | Instances |
|---|---:|
| Original trees | 37,678 |
| Boundary forest | 29,232 across 29 cells |
| Shrubs and undergrowth | 26,127 |
| Grass | 22,990,564 |

The saved original-tree total exactly matches the accepted source plan. All
65,879 recovered originals are accounted for: 37,678 accepted and 28,201 rejected
with reasons; the grass pass evaluated 92,274,688 candidates. Evidence:
`Artifacts/VegetationRebuild/20260831-163439/report.json` and
`Logs/vegetation-rebuild-full.log`. The report SHA-256 is
`8c6ca7d46f0d026cbf8e09daa2ccaf94262d75f3cc79692cbb7960362ebaee96`.

This is a **data-generation pass, not complete boundary or performance acceptance**.
The executed height audit found 710 boundary roots (2.43%) more than 10 m below
their paired wall elevation, including 461 below 20 m and 255 below 30 m. Root
height difference is not a canopy-occlusion or grounding-error measurement, but
low technical ground patches remain explicit visual debt. The executed southern
cell's camera 04 shows an under-terrain void from that low patch; this is not an
accepted view of a closed boundary from normal high ground. Deep views also
intersect foreground trunks/branches. Southern, home and eastern rail-area
captures passed their technical checks, and independent serialized-output
verification passed. The southern camera-only recapture shows ground and forest
depth with reduced near-trunk obstruction. The final open-rail view resolves
unplanted rails, ballast and portal, with the adjacent field remaining open.
Those are specific reviewed sightlines, not map-wide boundary or rail acceptance.
Gameplay/frame-time checks remain pending.

## Historical status and scope

The user explicitly authorized removing and recreating the current generated
vegetation on 2026-08-31. This is a bounded replacement of Phase 1 presentation,
not approval of the Phase 2 gate. Roads, terrain, water, buildings, gameplay
anchors, saves and the accepted 08A UI are not redesigned.

| Check | Recorded result |
|---|---|
| Isolated compilation of `MSC.World.Runtime` and `MSC.Editor` | **Passed**, including focus, category-scope and context-camera-clearance updates, using the installed Unity compiler and existing project references |
| Compiler diagnostics | No new errors; five pre-existing obsolete-shader warnings in `TextureInventoryExporter` |
| Unity EditMode suites | **Post-capture rerun passed 75/75**, `editmode-results-post-capture.xml`; zero failures/skips, including 12 capture-scope tests, after all camera edits |
| Unity PlayMode cell-layer suite | **Latest rerun passed 3/3**, `playmode-results-r4.xml`, including shared native asset release/reload; earlier fixture failures remain recorded |
| Approved grass/material preparation | **Passed**, three licensed grass profiles and five Chernobyl HDRP bindings; process exit 0 |
| Pilot generation and saved-scene validation | **Current r6b pilot passed**, `20260831-162325`, one saved cell; matching technical capture **passed** |
| Full-map generation / counts / processed cells | **Passed**, `20260831-163439`, 88 saved cells, zero cell errors |
| HDRP visual audit | Current r6 **passed five technical views**, matching hashes and zero unsupported materials; road/front reviewed, deep view has foreground occlusion |
| Additional cell/focus captures | South/home **passed five technical views each**, final open-rail capture **passed six**, zero unsupported shaders; southern depth/ground, home access and the specific open-rail sector reviewed; southern low-patch void remains |
| Independent serialized-output verifier | **Passed**, all 88 cells and all serialized category counts, zero errors/warnings; 49 existing legacy registrations and build prefix preserved |
| Complete boundary occlusion and runtime profiling | **Pending** |

Latest test evidence is `Artifacts/VegetationRebuild/editmode-results-post-capture.xml`
and `Logs/vegetation-rebuild-editmode-post-capture.log`: 75 passed, zero failed/skipped,
ending 17:38:58Z in 2.2062821 seconds, after all camera edits. Breakdown: donor source 8, surface query 38,
clear/regeneration 4, planning 3, cell-layer configuration 3, existing vegetation
7 and capture scope 12. PlayMode remains the executed 3/3 r4 suite. Neither
compilation nor these tests establishes complete visual coverage or 60 FPS.

`Artifacts/VegetationRebuild/full-verification.json` independently verifies the
full run with `--scan-grass`: all four serialized totals match the table above,
including all 22,990,564 grass position records; 37,678 saved originals equal
the accepted source plan. The manifest has 88 vegetation layers, while all 49
pre-existing legacy registrations and the existing build-scene prefix remain
unchanged. The old global forest retains one root and 112 empty cell containers,
with no old vegetation category children or legacy grass renderer components.
Exactly one explicit serialized wind component is active there; generated scenes
contain zero explicit wind components. This check does not expand prefab
components or profile live weather/runtime ownership. The verifier returned
`PASS` again after the post-capture tests, with empty errors and warnings; the
latest report SHA-256 is
`ac3047fe3cba6360484d97d4498734113bdc8d6baf09f903f6f415f40461c100`.
An earlier verifier-only quoted-YAML-name parsing failure was corrected before
this successful rerun; it was not a planting failure.

## Historical validation and pilot runs

The chronology below preserves failed and superseded attempts. Its earlier
pending states are historical; the current full-map result above is authoritative.

The earlier 63-test EditMode breakdown was donor source 8, surface query 38,
clear/regeneration 4, planning 3, cell-layer configuration 3 and existing
vegetation system 7. Evidence:
`Artifacts/VegetationRebuild/editmode-results-r5.xml` and
`Logs/vegetation-rebuild-editmode-r5.log`. That run ended at 16:16:14Z;
the grass-margin fixture now derives its boundary from the configured margin.
The earlier 42/42 run at 14:59:44Z
remains historical evidence in `editmode-results.xml`; it does not substitute
for the expanded rerun.
The first PlayMode run could not load its test fixture scene from the active
build profile/shared scene list. This failure is recorded in
`Artifacts/VegetationRebuild/playmode-results.xml` and
`Logs/vegetation-rebuild-playmode.log`. The fixture was corrected; the executed
rerun in `Artifacts/VegetationRebuild/playmode-results-r2.xml` passed both tests
with zero failures at 15:05:18Z. This verifies the tested cell-layer flows, not
populated-map streaming, runtime performance or visual acceptance.
The expanded final PlayMode suite additionally exercises shared generated
catalog/cell/mask lifetime, retaining referenced assets until the last owner
unloads and loading the same persistent assets again. All three tests passed
in `playmode-results-r4.xml`; one unused-asset sweep runs after a batch of owned
presentation scenes is unloaded, not once per cell.

Pilot source reads recovered **65,879 tree inputs, 51,132 shrub inputs and 823
boundary segments from 50 source scenes**. These are input evidence counts,
not successfully placed vegetation. Attempt `20260831-150210` stopped at the
representative-cell guard; `20260831-150714` stopped on an invalidated settings
reference while choosing the pilot. Their reports record `passed: false` and
empty saved-cell result lists. Source diagnostics, planning reports and backups
may exist, but neither attempt establishes a generated pilot or full-map pass.

The third attempt, `20260831-152639`, completed with `passed: true`, an empty
failure string and zero saved-cell validation errors. Its saved pilot contains
**1,581 original trees, 1,368 boundary trees, 1,833 shrubs and 280,251 grass
instances** from 409,600 grass candidates; 776 original source positions were
rejected by the configured rules. The log records `MAP_VEGETATION_CELL_OK` and
`MAP_VEGETATION_PILOT_OK`; `pilot-gate.json` and `latest-report.json` matched the
run at that stage. All 50 source scenes reported zero active legacy card
renderers. That result covered one data pilot, not the full map.

The subsequent real capture ran on an NVIDIA GeForce RTX 4070 SUPER and wrote
four PNGs, but inspection failed: the foliage was black, the inspection lighting
was too dark and the grass showed coarse triangular silhouettes. The capture
report listed unsupported `AE/Leaves` for Pine_Needle, Aspen_Leaves,
Birch_Leaves, Grass_01 and Bushes_01. Nonzero image contrast and frustum counts
did not establish visual acceptance. The material adapter and explicit HDRP
inspection lighting were corrected afterward; another prepared-art pilot and
real capture were then required and executed before the full-map run.
The first failed capture is archived under
`Artifacts/VegetationRebuild/VisualAuditPilotR3/`.

`PrepareApprovedArtBatch` subsequently completed with
`MAP_VEGETATION_ART_BINDINGS_OK grassProfiles=3 materialOverrides=5` in
`Logs/vegetation-rebuild-prepare-art.log`. It selected existing licensed
NatureManufacture grass meshes/alpha atlases and prepared the five explicit
Chernobyl material overrides; vendor assets were not rewritten. The configured
ground list now also includes the audited `BetterMSC/MissingTerrain` patch.
Preparation and compilation passing do not establish a new visual pass.

The prepared-art r4 pilot, `20260831-154913`, also passed saved-data validation:
1,581 original trees, 1,368 boundary trees, 1,833 shrubs and 270,024 grass
instances. Its actual HDRP report matched the pilot fingerprint and listed no
missing/unsupported shaders. Inspection nevertheless led to a denser grass
setting, safer grass clearances and a fifth view of the deep boundary buffer.
Those changes invalidate using r4 as final acceptance for the current settings.
The denser r5 pilot, `20260831-161043`, subsequently completed with
`passed: true`, no failure and no saved-cell validation errors. It contains
**1,581 original trees, 1,368 boundary trees, 1,833 shrubs and 689,981 grass
instances** from 1,048,576 grass candidates in `cell_-4_-3`. Its 776 original
source rejections are retained in the cell report. Evidence is the run's
`report.json`, with settings SHA-256
`9677c54df38c62f022e783f6ef0ec76bd4ec97393d6c75e94bd55acdf0b7008c`, source
fingerprint `d767c829b0fe8af818e5ef0d91ffcba1b3e6e1e2f64522ed14b3cb28c51168ff`
and saved generated fingerprint
`a8907bd7590cad618a84b531171c2f2062c1c389ad72ba2bb7215b83d97dc1f0`.
Its matching HDRP capture subsequently passed all five views with no unsupported
shaders or magenta pixels. The road and deep-boundary images were visually
reviewed: the actual road is readable and the deep buffer shows a forest layer.
The PNGs and report are archived under `VisualAuditPilotR5/`. This is evidence
for those viewpoints and settings, not every map edge or runtime performance.

The same source evaluation planned 29,008 accepted and 36,871 rejected original
trees across all source cells. Those values sum to all 65,879 recovered inputs;
they are **planning counts, not full-map saved placements**. The largest rejection
groups were minimum trunk spacing (15,363), no allowed ground (10,179) and water
(6,290). Every rejected original source is written with its ID, cell, coordinates,
reason and source path, including records outside the natural-ground cells.

The final pre-full r6b pilot, `20260831-162325`, passed after reducing minimum tree
spacing to 1.5 m. It saves **1,967 original trees, 1,727 boundary trees, 1,827
shrubs and 688,405 grass instances** from 1,048,576 grass candidates, with 390
original source rejections and no saved-cell errors. Its settings SHA-256 is
`d78731262fa7d8b9e852cdc65f151732f8e28d384643d44a35f4816cb44066fc`, and its
generated fingerprint is
`5f76422d3f27f5c827c408cbcbc3cad072d384be91136e42a340db16e655f487`.
The source fingerprint was unchanged from r5: no source X/Z was shifted to make
the denser plan fit. The whole-source original-tree plan then accepted 37,678
and rejected 28,201 inputs; spacing rejections fell from 15,363 to 6,693. At that
stage these were planning counts outside the saved pilot; the subsequent full
run saved all 37,678 accepted originals. The matching r6 HDRP capture passed
five technical views with zero unsupported materials and the same settings and
generated fingerprints. The road view is clear and the front transition shows
healthy growth with a deeper forest layer. The fifth camera is too close to fir
branches: its dark foreground obscures the sightline, so it does **not** prove
complete boundary occlusion. Neighbouring cells were unavailable in that
pilot-only capture. Full-map generation then completed as recorded above.

## Existing architecture and integration

The active project is resolved from `Config/DonorPaths.local.json`; its local
override selects the current repository, rather than the older `_Game` default.
Unity is pinned to `6000.3.11f1`, HDRP. No package was added.

The active map is `World_Global_Legacy` plus the existing 49 legacy cell scenes,
selected by `WorldBaseline06B2Paths` and `ProductionWorldStreamingManifest`.
The separate unified-terrain authoring experiments are not additional source
maps. The former `Phase1ForestRemediationBuilder` generated a large global
forest hierarchy and `Phase1GrassFieldRenderer` fields. The accepted mesh
vegetation painter already supplies `VegetationCellAsset`, `VegetationProfile`,
`VegetationWorldRenderer`, marker components and compact indirect GPU instances.

The new tool reuses those grass assets and renderer contracts. It adds optional
`ProductionWorldCellLayerScene` entries with layer ID `vegetation` to the same
streaming manifest/service. Each generated scene owns one spatial cell,
including natural-ground cells without a legacy-object scene. Existing legacy
cells, gameplay catalogs and build indices remain intact; missing enabled scene
entries are appended rather than inserting ahead of existing indices. No second
streaming service or world-global replacement forest is introduced.

```text
explicit global + cell source scenes
  -> MapVegetationDonorSource: copied source roots / cards / boundary segments
  -> MapVegetationSurfaceQuery: ground triangles + exclusion footprints
  -> MapVegetationContext: deterministic plans and rejections
  -> preview / one-cell pilot
  -> owned category roots + per-cell grass data
  -> saved-scene validation
  -> existing manifest CellLayers / existing streaming service
  -> existing VegetationWorldRenderer / prefab LODGroups
```

Input cells are opened sequentially. Mesh triangles, footprint data and marker
settings are copied before a cell closes; Terrain heights, holes, size and
transform are copied without retaining native TerrainData dependencies.
The old generated forest is classified before CPU vertex
access, avoiding repeated reads of tens of thousands of high-detail tree meshes.
Settings and manifest assets are held through a temporary native asset lease
while Unity replaces scenes. A unique owned empty scene under
`VegetationRebuild/EditorScratch/Context_<guid>.unity` provides a saved context
for additive cell generation; disposal restores the original scene setup and
removes only that exact scratch asset. User scenes are never saved as scratch.
During an all-cell run, completed cell arrays and unused native assets are
released after every four validated cells and at the last cell, retaining
managed Editor references and the native asset lease. This bounds Editor
generation memory; it is not runtime streaming performance evidence.

`MapVegetationContext.SourceFingerprint` hashes copied source point identity,
position, height and provenance plus boundary geometry and the copied
ground/exclusion geometry fingerprint. It streams binary records into SHA-256
once per context instead of materializing another full source JSON document.
This gives the pilot gate evidence for source/ground changes as well as settings.

## Source evidence and coordinate mapping

The original installation remains read-only. The tool reads the already
sanitized canonical extraction, not the original executable or donor scripts.
Authoritative source manifest:
`Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json`.

- Source revision: `msc-world-baseline-04a1.1-c3f2f337`.
- Frozen donor scene SHA-256:
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Canonical axes and scale are unchanged: Unity Y-up, one source unit per metre.
- The canonical importer already applied translation
  `(169.97999572753907, 1.6109999418258668, -1040.625)` and identity rotation/scale.

**Do not enter that translation into the vegetation settings again.** The
tool's coordinate matrix is an optional correction *after canonical import*,
identity by default. It applies consistently to source points and boundary
segments; ground geometry remains in the current scene's coordinates. Corrected
source X/Z is preserved exactly. Ground height is resampled; no lateral jitter
or unbounded automatic relocation is applied to rejected original positions.

Individual donor tree transforms are used where available. Batched tree/shrub
meshes require connected-component analysis and paired-card root recovery;
these are recovered positions, not a claim that an individual donor transform
survived. Ambiguous/unpaired cards retain diagnostic evidence and lower
confidence. Source records include stable provenance identity, method, source
mesh GUID and hash. Donor hierarchy paths are used only by Editor inspection;
runtime systems do not search them.

Existing modern tree variants are selected by compatible species where possible.
Spruce now comes only from the five reviewed ALP wrappers listed in the active
package revision; pine, birch and aspen retain their reviewed Chernobyl
non-spruce bindings until separately replaced. Grey willow, fern, moss, selected
mushrooms and dead grass come from the Finnish NatureManufacture subset. Prefab axis corrections,
root presentation and LODGroups are retained. Source assets are not rewritten.
Arbitrary extra species and donor near-tree meshes are not imported.

The earlier r4 pilot's young growth follows source height evidence: 739 accepted
small-atlas trees span 1.29–4.26 m (median 2.82 m), 555 medium-atlas trees
2.58–8.53 m (median 5.61 m), and 287 large-atlas trees 5.06–24.67 m (median
12.86 m). These are pilot source-card heights, not global species statistics.
Do not inflate all original trees because a distant overhead image looks sparse.
The existing prefab root orientation and authored visual burial remain intact;
normalization measures prefab height at unit scale and applies the target height
with the configured 0.92–1.08 scale variation. No pivot-to-renderer-bottom lift
is applied.

`MapVegetationAlpSpruceBindings` builds removable project-owned Unity 6 HDRP
wrappers around the five selected ALP prefabs. It preserves each reviewed 4/5
stage renderer set, assigns bounded transition thresholds and separates bark
from foliage/billboard material adaptation. Vendor source assets are untouched.
`MapVegetationForestFloorBindings` applies the same wrapper boundary to selected
rock/stone and Finnish plant assets. Material adaptation does not establish
production provenance, physical equivalence or distribution rights.

## Surface and exclusion rules

Ground must have an existing `VegetationSurface`, an explicit allowed layer,
tag/material rule, or an audited canonical natural-ground path:

| Path | Evidence / role |
|---|---|
| `MAP/MESH/TERRAIN_OBJ/Grass1` | Existing canonical natural ground |
| `MAP/MESH/TERRAIN_OBJ/Grass2` | `VegetationGrass`, stable ID `2dee048c54a6fce0a349dcae54f43e8b` |
| `MAP/SkijumpHill/grass` | `VegetationGrass`, stable ID `38f547477bf6c6e69ea47c4ea93a479a` |
| `BetterMSC/MissingTerrain` | Existing remediation terrain, stable ID `49a40657708f9739cbba81a119d77cbe`; explicit prepared-settings opt-in |

Texture colour and current GameObject names alone never authorize ground.
Terrain requires explicit review/opt-in or the same marker/rule route; holes are
respected. Mesh and box-collider ground are supported. Unsupported explicit
collider types and unreadable surfaces are reported rather than guessed.
Highest allowed ground is sampled from above with its material, layer, normal
and provenance. Excessive slope rejects that point; it does not fall through to
a flatter surface below it.

Deny rules override ground opt-in. The 32-metre spatial index uses transformed
triangle footprints and distance to their edges, rather than treating every
road/building's whole AABB as occupied. Conservative bounds are a reported
fallback for unreadable exclusion meshes. Existing `VegetationBlocker`, copied
`VegetationExclusionVolume` channels, explicit layer/tag/material rules,
canonical semantics, interactive interfaces and the project's
`TrafficRoadNetworkCatalog` contribute independently.

Configurable tree / shrub / grass margins cover asphalt, dirt and forest roads,
railways, buildings, doors, gates, garage openings, driveways, bridges,
shorelines, agricultural fields, interactive objects, vehicle routes, open
spaces and other artificial structures. Defaults are initial safety settings,
not donor measurements. Grass can approach natural verges more closely than
trees. The traffic catalog uses swept route segments with an authored half-width
plus the category margin.

Final validated v7 grass clearances for asphalt, dirt roads, buildings,
driveways, artificial structures and agricultural fields are 0.94 m. The
selected scaled composite meshes, permitted slope/alignment, packing margin and
shader wind displacement only require a computed 0.77 m minimum; the preparation
step deliberately does not reduce the pre-existing serialized 0.94 m v5 margin.
Railway remains 1 m. These values apply to v7 and must be reviewed again if
geometry, scale, composite copy layout or wind amplitude changes. Other category
margins remain separately configurable.

The historical r4 roadside probe independently resolved all twelve nearby saved grass
samples to `Grass2`, preserving their exact X/Y/Z. Their nearest distances to
the actual DirtRoad footprint were 0.526–2.175 m. The camera sat on DirtRoad
triangle 4850 with a 1.700 m eye offset; automatic grass at the camera X/Z was
rejected as `NoAllowedGround`. Grey-looking ground beside that camera was thus
not sufficient evidence of planting inside the road. Final v7 is stricter still
at 0.94 m. This is a bounded location probe, not an exhaustive road-clearance
pass.

- `MAP/MESH/TERRAIN_OBJ/Fields` and `MAP/MESH/TRACKFIELD` are field exclusions;
  their source stable IDs are `2987bdc3576114fda4b4b319bd0d4681` and
  `3126994543fb175171b95b18b546d349` respectively.
- Actual lake tiles can be classified `StaticProp`: both
  `MAP/LakeSimple/Tile*` and `MAP/LakeNice/Lake/Tile*` explicitly identify water
  geometry. They can extend beneath raised dry land, so their whole projected
  rectangle is not treated as a shoreline. A point at or below the water plane
  (with the query's 0.02-metre tolerance) is excluded; higher approved dry ground
  remains eligible subject to every other rule.
- Shoreline margins use exposed water and the wet portion of nearby ground
  triangles, including Terrain samples. Ground/water intersections and bank
  edges without an approved lake-bed mesh supply shoreline evidence. Covered
  submerged lower meshes and ordinary internal dry triangle edges do not create
  artificial shoreline exclusions over higher dry land.
- `WaterUnder`, `WaterColor`, lake-bed and lake-vegetation proxy meshes are
  never ground and do not define projected water exclusion: some span dry land.
  Real water-surface geometry supplies that exclusion instead.
- Open-space exclusions protect trees/shrubs; grass still requires permitted
  ground. Agricultural fields exclude all automatic categories.
- Automatic placement excludes the entire bridge column, including below a
  high deck. This intentionally follows this request's stricter rule without
  changing the older manual painter's configurable under-bridge behavior.
- Grass profile selection honors each profile's density channel, surface marker,
  matching exclusion-volume channels, slope and height limits. A meadow-only
  exclusion can still allow a short-grass alternative.

## Categories, density and performance

The owned categories are `OriginalTrees`, `BoundaryForest`,
`ShrubsAndUndergrowth` and `GrassCoverage`. They may be regenerated individually
after the initial legacy migration. Old forest cells mix original and boundary
trees under one `Trees` container; the first replacement must select both tree
categories to avoid leaving duplicates or removing the wrong old trees.

Boundary candidates use the recovered current billboard-wall segments, a staggered
world-space grid and stable jitter, increasing density toward the technical
boundary. They resolve only on existing permitted ground, with tree spacing and
open-space exclusions applied first. The tool does not add terrain, plant over
voids, move fields or claim that mathematical density guarantees visual occlusion.
The existing BetterMSC remediation replaced the baseline low/high wall meshes;
these current geometry assets are hashed as source evidence rather than claimed
to be unchanged frozen donor wall meshes. Its `BetterMSC/MissingTerrain` void-fill
mesh was absent from the initial three-path whitelist. Executed art preparation
now adds that exact audited metadata path, while retaining every deny rule.

Grass uses the existing Short/Meadow/Tall profiles, compact instance records and
32-metre tiles; there is no GameObject per blade. Generated RGBA density channels
match the profiles' actual channels. Noise modulates dense coverage without
randomly changing the source area. Profile and category scale ranges combine.
Profile meshes/materials, normal alignment, shadow rules, LOD and culling remain
the renderer's existing responsibility.

The active Short/Broad/DryFine bindings use the reviewed Forest Environment
`Grass02_3`, `Grass01_3` and `Grass03_3` prefabs. Their deterministic selection
weights are 46/32/22. All three LODs retain progressive carpet geometry with
16/8/6 authored clumps and 18/42/60 m distances instead of collapsing to a
single far speck. No cereal or seed-head source is accepted. The project-owned
`GrassArt/provenance.json` records prefab, mesh, texture, geometry and spatial
coverage evidence; its corrected v12 SHA-256 is
`76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`.

The v12 colour mask keeps three-source-texel green dilation and adds bounded
local-carpet evidence around noisy green regions. This broadens eligible metadata
without authorizing grass on brown unsupported ground. Natural-ground evidence
and every road, yard, route, field, water and OpenSpace exclusion remain
mandatory. Full generation accepted 5,157,636 grass records from 36,044,800
candidates; the profile counts are 2,368,747 / 1,651,618 / 1,137,271.

The existing `MSC/HDRP/Vegetation Indirect` shader remains a simplified unlit
hemisphere approximation with alpha clipping, procedural wind, depth, shadow
casting and motion-vector passes. Its forward colour does not evaluate HDRP
lights, received shadows, normal maps or weather wetness. A profile's
`receiveShadows` flag does not add missing lighting code. This is an explicit
remaining presentation limitation; using licensed grass meshes does not turn
that shader into a physically based foliage material.

Active settings in `MapVegetationRebuildOptions` use 0.8-metre grass candidate
spacing, 0.96 density, a signed grounded near-boundary layer and separate sparse
forest-floor policies. Tree and floor presentation is packed into matrices,
prototype/batch tables and placement metadata. Source prefab colliders are not
serialized into cell scenes: the runtime collision pool consumes the 62,168
accepted donor/infill collision records, while boundary and distant trees carry
no generated collision.

The placement settings' tree minimum spacing is now 1.5 m, preserving more
original anchors while still applying ground and clearance rules. It does not
move either rejected or accepted source trees. Generated boundary candidates
share the trunk-spacing check; the rear/front tree count therefore also changes.

`MapVegetationRebuildOptions` owns the active boundary, grass, preview and
performance controls. Generated output is deliberately fixed under the private
runtime-baseline root; it is not an arbitrary production-asset destination.

## Editor workflow and safe regeneration

Open `Tools > MSC Remake > Vegetation > Map Vegetation Rebuild`.

For actual runtime inspection, open `Assets/Game/Bootstrap/Bootstrap.unity` and
enter Play Mode. The existing streamer automatically loads vegetation cell layers
around the player. Opening only `World_Global_Legacy` in Scene View does not show
the full streamed vegetation and is not a valid runtime density check.

1. Exit Play Mode through the owner of the current session and save or discard
   unsaved scenes. The tool/bridge will not do either automatically.
2. Select a representative `cell_X_Z`, categories, source settings, natural-ground
   rules, margins and existing prefab/profile references. Initial pilot selection
   must contain the selected categories; use both tree categories for migration.
3. Click **Preview selected cell — no generated content is saved**. Source and
   settings assets may load, but no generated vegetation scene is written.
4. Inspect source rings, accepted trees, rejected crosses, grass points and
   exclusions. Issue buttons frame their coordinates in Scene View. Green means
   grass, dark green trees, cyan boundary candidates, orange roads, blue water,
   purple fields/open spaces, red other exclusions, yellow safety margins.
   Exclusion preview bounds are summaries; placement uses exact geometry.
5. Click **Generate + validate pilot cell**. The plan is evaluated twice for
   determinism, written to owned content, reopened and compared with the plan.
6. Inspect the real saved pilot in HDRP, including road and water edges, slopes,
   doorways, interiors, tree roots, transitions and boundary sightlines.
7. Only after the pilot and its matching graphics-enabled HDRP capture pass,
   click **Generate + validate all cells (requires matching pilot)**, then
   **Validate all saved cells**. The capture gate checks settings and saved
   generated fingerprints and rejects unsupported materials. Inspect its PNGs
   too: those technical checks do not prove visual quality. Changed settings,
   art or source geometry require another pilot and capture.

Settings are created at `Assets/Game/Editor/Vegetation/MapVegetationPlacementSettings.asset`
and `MapVegetationRebuildOptions.asset`. Output is:

```text
Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/
  Scenes/World_Cell_<X>_<Z>_Vegetation.unity
  Data/cell_<X>_<Z>/GrassCell.asset
  Data/cell_<X>_<Z>/Catalog.asset
  Data/cell_<X>_<Z>/Density.asset
```

`GeneratedVegetationGroup` records generator, cell, category and fingerprint.
Only matching selected roots are replaced. Foreign roots, manual painter masks
and source prefabs are retained. Wrong ownership, duplicate category roots,
missing scripts/meshes/materials, invalid saved positions/profiles or foreign
cell data fail validation. Old generated category contents are retired only
through the known historical generated container; the established wind owner
is preserved.

To clear, select the category/cell and click **Clear selected generated categories
in selected cell**. This does not restore retired old vegetation. It retains
unselected/manual roots, does not recursively delete project directories, and
keeps reusable generated data assets. Empty owned layers are unregistered;
build-list indices remain stable. The Editor API also exposes
`MapVegetationRebuild.Clear(options, true)` for all owned cells.

Before mutation, backups of existing generated scenes/data, the global scene,
streaming manifest and build settings are written under
`Artifacts/VegetationRebuild/Backups/<runId>/`, retaining project-relative paths
and available `.meta` files. Backups are not an automatic transactional rollback
of an entire map run: a failed later cell can leave earlier generated files
updated. Review the failure report, then regenerate or restore the matching
backup files with the Editor idle, preserving their `.meta` files.

For manual correction, adjust an exclusion rule, a settings-authored world
bound, an existing marker/channel or a profile, then regenerate the affected
cell/category. Keep hand-placed vegetation outside generated category roots;
editing children inside those roots is not persistent authoring and the next
generation replaces them. Do not move ground or donor source trees to make a
sample pass.

## Commands and evidence artifacts

The isolated compile command, from the active project root:

```powershell
& .\Tools\Vegetation\Validate-VegetationCompile.ps1
```

It reads the local Unity executable configuration and existing Bee response
files, producing only ignored compile artifacts. It requires a previously
imported project and is not a clean Unity import or a test run.

When no other Unity process owns this project, synchronous batch generation can
use the installed Editor selected by local configuration:

```powershell
$vegetationPaths = Get-Content -LiteralPath 'Config/DonorPaths.local.json' -Raw | ConvertFrom-Json
& $vegetationPaths.UnityEditorExecutable -batchmode -quit -projectPath $vegetationPaths.UnityProjectDirectory -executeMethod MSC.Editor.Vegetation.MapVegetationRebuild.RunPilotBatch -logFile Artifacts/VegetationRebuild/pilot.log
```

Replace only the execute method with `RunAllBatch` or `ValidateAllBatch` for
those operations. The pilot gate still applies. For real HDRP capture use
`MSC.Editor.Vegetation.MapVegetationVisualAudit.CapturePilotBatch`, without
`-nographics`, after saving a pilot.

To inspect another already-generated cell without changing settings, add
`-vegetationAuditCell cell_X_Z` to that capture command. PNGs, capture report and
road diagnostics then go under `Artifacts/VegetationRebuild/VisualAuditCells/cell_X_Z/`.
Omitting the argument preserves the default `VisualAudit/` pilot/full-gate path.
The override never saves the options asset or substitutes evidence for its pilot.

An explicit landmark focus requires both invariant finite coordinates in that
same overridden cell. For the home, append
`-vegetationAuditFocusX 155 -vegetationAuditFocusZ -1035` to
`-vegetationAuditCell cell_0_-3`. The executed open-rail focus for
`cell_4_-2` is `-vegetationAuditFocusX 2316.162 -vegetationAuditFocusZ -998.896`.
The earlier axis point `(2415.584, -930.716)` lay inside the railroad tunnel;
it was a correct measured axis point but unsuitable for viewing exposed rails.
Focus changes the closest tree/road/boundary selection; the overhead still covers
the whole cell. It also adds `06_Focus_Oblique`, looking at the exact focus X/Z
from +25 m height and -40 m Z. Target Y comes from the nearest saved grass point,
falling back to a saved tree/zero; it is not a raycast at the focus. The report
records requested/effective focus and the explicit look target. This view does
not force LODs, remove occluders or modify ground, assets or settings.

To recreate the audited licensed art bindings and exact MissingTerrain opt-in,
use `MSC.Editor.Vegetation.MapVegetationRebuild.PrepareApprovedArtBatch` before
the pilot. This updates generated grass assets, material overrides and settings;
it invalidates previous pilot/capture evidence when their fingerprints change.

For an already open Editor, the Editor-only vegetation bridge reads explicit
requests from `Artifacts/VegetationRebuild/editor-request.json`:

```json
{"id":"vegetation-pilot-001","action":"pilot"}
```

Actions are restricted to `status`, `edit-tests`, `play-tests`, `pilot`, `all`,
`validate`, `capture`. The bridge accepts no arbitrary method, code or output
path. Each completed ID is idempotent. Submit a new ID after resolving a blocker;
it will not stop Play Mode, discard scenes, collide with another test run or
silently retry a mutation after a domain reload. Inspect `editor-status.json`,
`result-<id>.json` and `tests-<id>.xml`. A status request alone runs no tests.

Run reports are under `Artifacts/VegetationRebuild/<runId>/`, including
`source-diagnostics.json`, `report.json`, cell JSON, placement CSV and rejection
reason CSV. `original-tree-source-rejections.csv` contains every rejected
original source, including cells outside the generated natural-ground set;
`globalOriginalTreePlanAccepted`, `globalOriginalTreePlanRejected` and grouped
reason counts describe the full source plan separately from saved-cell counts.
`latest-report.json` identifies the latest successful report;
`pilot-gate.json` records the matching settings/fingerprint. Detailed grass
rejection samples are capped, but reason totals remain available.

`VisualAudit/` holds actual PNGs and `capture-report.json`: overhead, eye-level
forest, road view where a road exists, the front boundary transition, and a fifth
deep-buffer view when boundary trees exist. That fifth camera uses a recovered
current wall segment, a generated tree within the inner 15 m / 20% of the band,
and a nearby saved grass point for eye height. Existing LODs remain active.
`MapVegetationCaptureScope` validates only the selected generation categories:
their roots must exist, belong to the requested cell, share the new fingerprint
and contain representative saved instances for a pilot. Preserved unselected
categories may have different fingerprints and remain visible as context; their
fingerprints are reported separately and do not substitute for selected content.
An explicit extra-cell capture may contain legitimately empty categories without
being labelled a representative pilot. Missing/duplicate/foreign selected roots
still fail. Five base views are required when a selected boundary category has
instances, otherwise four; explicit focus adds `06_Focus_Oblique` to
that required count. A category-only boundary capture can resolve current ground
for the camera without requiring saved grass. None of these ownership/count
checks proves pixel-visible coverage.

The original front-transition view remains deliberately visible. In r4 its
selected tree was 74.31 m from the wall in a 75 m band, so that image looked
through the sparsest young front layer. The deep view supplements it rather
than hiding it. The overhead camera spans 563.2 m vertically: many small trees
are LOD-culled, and vertical far billboards are seen edge-on. Its frustum count
does not measure visible canopy coverage or prove a scaling failure.

Available registered generated neighbour layers load within the configured
`ForestLoadingRadiusCells` (1–4 cells; currently 2, a 5×5 neighbourhood). The
report records `vegetationLoadingRadiusCells`; canonical base scenes remain a
3×3 source-visual context with `sourceContextRadiusCells = 1`. Their paths and
missing neighbour cells are reported; counters still describe the audited cell,
not all visible neighbours. Missing layers limit any
claim about the distant forest. `road-surface-diagnostics.json` records exact
ground/road samples. No selection of five viewpoints proves every map void
hidden, and non-magenta images do not establish frame-time performance. The r6
deep view also demonstrates a camera limitation: grounding on grass avoids an
underground camera but does not guarantee clearance from tree branches.

Explicit extra-cell deep views now use a bounded camera-only clearance search:
a saved grass point within 8 m horizontally and 2 m vertically, farther than
2.25 m from every audited-cell tree root. The report records the attempted search,
actual move and nearest trunk. Crowns, neighbouring-cell trees and other occluders
can remain visible. This adjustment does not move vegetation or change the
default pilot camera, saved fingerprints, generation settings or acceptance gates.

The read-only `BoundaryHeightAudit/summary.json` scan at 17:17:50Z covers all
29,232 saved boundary roots in 88 completed cells. It interpolates the paired
wall elevation from actual source endpoints: 710 roots are more than 10 m below
that wall, 461 more than 20 m and 255 more than 30 m. It does not re-query terrain
or measure canopy visibility. The pre-clearance replay in `cell_3_-6` placed
camera 04's eye at Y = -22.675 m above an existing patch at Y = -24.375 m, and
camera 05's eye at Y = 9.741 m above ground at Y = 8.041 m. The helper
now warns when camera ground and the selected tree differ by more than 10 m;
the view remains visible as a diagnostic. No plants or patches were moved to
conceal this limitation.

Three additional saved-cell audits have executed after full generation. Their
technical capture passes and bounded visual findings are recorded below.

| Cell | Saved woody count | Evidence and inspection purpose |
|---|---:|---|
| `cell_3_-6` | 2,427 | Distant southern edge; nine current wall-segment midpoints and nine original tree inputs, all accepted. Inspect the generated boundary/undergrowth and supporting ground. |
| `cell_0_-3` | 1,546 | Home/garage and shoreline: 421 eligible `YARD/Building/` entities in the frozen mapped table; 312 original-tree water rejections. Inspect yard access, roads and the land/water transition. |
| `cell_4_-2` | 3,385 | Eastern road/rail/boundary zone: 47 original-tree railway rejections and 20 wall-segment midpoints. Fleetari's building at `(1734.16, 6.191, -307.205)` is in source-context neighbour `cell_3_-1`. |

Saved woody counts above come from the full run's cell reports; selection used
r6b's source/rejection evidence. The CLI camera chooses actual local vegetation
and road geometry; loading a landmark as context does not guarantee it appears
in the automatic frame. A field or railway not visible in those PNGs requires
an additional positioned inspection and must not be marked visually verified.

`VisualAuditCells/cell_3_-6/capture-report.json` passed five technical views with
zero unsupported shaders and zero magenta pixels. It reports 2,187 trees
(2,178 boundary), 425,191 grass instances and 415 grass batches. Reviewed forest
views show volumetric dense vegetation, but view 03 is an alternate forest view,
not a road test. The final camera-clearance recapture also passed five views and
exited 0 (`Logs/vegetation-rebuild-capture-south-clearance.log`). View 05 now shows
ground and forest depth with reduced near-trunk obstruction; trunks and branches
remain visible. Its eye is `(1797.255127, 9.737438, -2797.157959)`, moved only
1.580974 m horizontally and -0.003270 m vertically; the nearest audited-cell trunk
is 2.337460 m away. Earlier images remain in
`Artifacts/VegetationRebuild/VisualAuditCellsSouthBeforeClearance/`.
View 04 still reveals the actual under-terrain/low-patch void. High-ground or
map-wide boundary closure is not accepted from these images; no plants changed.

`VisualAuditCells/cell_0_-3/capture-report.json` also passed five technical views
with zero unsupported shaders/magenta pixels. It reports 1,057 trees, no boundary
trees and 490,383 grass instances. The category-scope gate correctly allows the
empty boundary category for this explicit cell inspection. Reviewed
`06_Focus_Oblique` visibly frames the actual home: roof/foundation are unplanted
and yard/road access is readable; `03_Road_EyeLevel` shows a clear road. These
are bounded location findings, not evidence for every building or shoreline.

`VisualAuditCells/cell_4_-2/capture-report.json` passed six technical views with
zero unsupported shaders/magenta pixels. It reports 2,535 trees (1,359 boundary),
621,812 grass instances and 624 grass batches. The final explicit open-rail focus
is `(2316.162, -998.896)`, with no tunnel triangle above it; capture exited 0
(`Logs/vegetation-rebuild-capture-open-rail.log`). Reviewed `06_Focus_Oblique`
clearly shows the actual rails, ballast and portal without planting, the adjacent
field remains open, and grass surrounds the clear track bed. Its eye is
`(2316.162109, 24.892353, -1038.895996)` and target Y is -0.107647 m.
The road view also shows clear asphalt/dirt roads. This is visual verification
of that particular open sector, not every railway segment.

The earlier focus `(2415.584, -930.716)` was inside `RailroadTunnel`: source bed
Y was approximately 0.144 m and roof Y approximately 5.151 m. That explains why
the overhead oblique frame did not resolve the rails; it was not evidence of
planted track. The earlier images remain in
`Artifacts/VegetationRebuild/VisualAuditCellsRailTunnelFocus/`. Reframing changed
no generated instances or placement rules. Railway exclusion tests, independent
serialized-data checks and these bounded visual findings remain separate evidence.

## File inventory

| Area | Files |
|---|---|
| Editor source/planning | `Assets/Game/Editor/Vegetation/MapVegetationDonorSource.cs`, `MapVegetationPlan.cs`, `MapVegetationContext.cs` |
| Settings/query | `Assets/Game/Editor/Vegetation/MapVegetationPlacementSettings.cs`, `MapVegetationSurfaceQuery.cs`, `MapVegetationRebuildOptions.cs` |
| Active Editor settings | `Assets/Game/Editor/Vegetation/MapVegetationPlacementSettings.asset`, `MapVegetationRebuildOptions.asset` |
| Active third-party art bindings | `Assets/Game/Editor/Vegetation/MapVegetationAlpSpruceBindings.cs`, `MapVegetationGrassBindings.cs`, `MapVegetationForestFloorBindings.cs` |
| Selective package importers | `Tools/Vegetation/import_alp_spruce_pack.py`, `Tools/Vegetation/import_naturemanufacture_finnish_subset.py` |
| Writer/UI/capture | `Assets/Game/Editor/Vegetation/MapVegetationRebuild.cs`, `MapVegetationRebuildWindow.cs`, `MapVegetationVisualAudit.cs`, `MapVegetationRoadVisualDiagnostics.cs` |
| Capture ownership gate | `Assets/Game/Editor/Vegetation/MapVegetationCaptureScope.cs` |
| Editor assembly | `Assets/Game/Editor/MSC.Editor.asmdef` |
| Open-Editor bridge | `Assets/Game/Editor/VegetationAutomation/MapVegetationEditorBridge.cs`, `MSC.Vegetation.Automation.Editor.asmdef` |
| Runtime ownership | `Assets/Game/World/Runtime/Vegetation/GeneratedVegetationGroup.cs` |
| Reused grass runtime/data | `Assets/Game/World/Runtime/Vegetation/VegetationWorldRenderer.cs`, `VegetationCellAsset.cs`, `VegetationCellCatalog.cs`, `VegetationProfile.cs` |
| Streaming extension | `Assets/Game/World/Runtime/Streaming/ProductionWorldCellLayerScene.cs`, `ProductionWorldStreamingManifest.cs`, `ProductionWorldStreamingService.cs`; `Assets/Game/Editor/WorldStreaming/ProductionWorldCellLayerBuilder.cs` |
| EditMode source/query tests | `Assets/Game/Tests/EditMode/WorldBaseline/MapVegetationDonorSourceEditModeTests.cs`, `MapVegetationSurfaceQueryTests.cs` |
| EditMode generation/layer tests | `Assets/Game/Tests/EditMode/WorldRemaster/MapVegetationPlanningRegressionTests.cs`, `MapVegetationClearRegenerationTests.cs`, `ProductionWorldCellLayerTests.cs`; existing `VegetationSystemTests.cs` regression coverage |
| EditMode capture-scope tests | `Assets/Game/Tests/EditMode/WorldRemaster/MapVegetationCaptureScopeTests.cs` |
| PlayMode tests | `Assets/Game/Tests/PlayMode/WorldRemaster/ProductionWorldCellLayerPlayModeTests.cs` |
| Compile helper | `Tools/Vegetation/Validate-VegetationCompile.ps1` |
| Ignored read-only evidence helpers | `Artifacts/VegetationRebuild/verify_full_run.py`; `BoundaryHeightAudit/analyze.py`, `replay_camera.py` |

The existing painter and grass runtime are reused. Selected vendor payload,
generated wrappers, reports, backups and donor data remain outside Git; only
project-owned import/binding code, tests, settings/manifests where permitted and
this audit documentation belong in the repository.

## Compatibility and remaining limits

No save DTO or stable gameplay identity changes. Cell-layer serialization is
additive; an old manifest with no layers behaves as before. Generated stateless
vegetation is presentation and requires no new save domain or new playthrough.
If a later feature makes a tree mutable/persistent, it must use project-owned
identity/state rather than treating this generated hierarchy as save authority.
Runtime assemblies do not depend on Editor tools, donor assemblies, PlayMaker,
the original installation, Steam/DRM code or the optional Editor bridge.

The baseline through 08A, existing weather/audio boundaries and world geometry
remain preserved. Source positions are `WorldLayoutReference`; the placement
algorithms are `Reimplemented`; generated private source-derived presentation
is separately `TemporaryDirectImport`, never `ProductionReady`. ALP art also
retains its stricter `ThirdPartyPrivatePhase1Presentation`,
`productionReady=false` licence/provenance blocker.

Remaining work is broader boundary sightline and void inspection, gameplay
traversal/streaming checks and CPU/GPU profiling.
Recovered batch-card roots have less confidence than individual transforms.
Unclassified natural surfaces are not silently planted, arbitrary collider
types are not guessed, and boundary trees cannot hide a void when no permitted
supporting ground exists. These limitations must remain explicit in results.

### Historical v7 then-next milestone, completed by v12

The v7 report recommended a **measured pilot of the compact woody
runtime catalog** in the representative dense cell. Preserve source positions, selected
IDs, LOD/material/collider references, grass-catalog ownership and unload
behavior, and compare cold/warm deserialization, integration, main-thread gap
and sliced activation cost before accepting a storage migration. This remains
Phase 1 optimization, not a Phase 2 gate or a claim that streaming is fixed.

## Historical presentation revisions — 2026-09-01

The verified second pass is recorded historically in
[MAP_VEGETATION_PRESENTATION_REVISION.md](MAP_VEGETATION_PRESENTATION_REVISION.md):
it preserved 66,910 trees at 50/35/7.5/7.5, used Chernobyl Grass_01/02/03 and
retained 7,751,195 green-mask grass records. Its independent saved-data verifier
and 175/175 EditMode run do not validate later art changes.

The active third pass supersedes those current-art choices with
65/20/7.5/7.5, five reviewed ALP spruce variants, retained Chernobyl
pine/birch/aspen bindings, three-stage NatureManufacture grass wrappers and the
restrained forest-floor pools described above. Final presentation v7 adds branch and poplar-leaf litter through binding
v3 while preserving the `DeadGrass02`/`DeadGrass03` output slots and all instance
records. It retains the 0.65 m grid, three-texel green dilation and bounded grass
composites recorded above. Pilot `20260901-092200` and its matching five-view
HDRP capture passed. Active run `20260901-092757` and fresh validation run
`20260901-100750` then passed 88/88 with every per-cell field, fingerprint and
count identical. Runtime pacing and metadata budgets reduce secondary spikes,
while the retained 608.743 ms v6 scene-load observation leaves native
deserialization/integration as an explicitly unresolved hitch.

## Historical pre-v12 signed-infill, backdrop and rock proposal — 2026-09-01

This proposal is retained for design chronology. Its pending counts and
unexecuted-status statements are superseded by the accepted v12 results at the
top of this document.

This project-owned revision is implemented and statically compiled, but its
Unity generation, HDRP capture and Player profiling have not run yet. Historical
v7 counts above therefore remain historical evidence and must not be quoted as
acceptance for this revision.

The aggregate donor-tree audit found 65,878 candidates and 37,678 accepted
original placements from nine provenance paths. Candidate/accepted counts are:
`TREES2` 8,216/5,045, `SMALL3` 8,197/4,647, `MEDIUM2` 8,196/5,680,
`SMALL2` 8,195/5,185, `MEDIUM1` 8,194/4,178, `SMALL1` 8,192/3,344,
`TREES1` 6,819/4,272, `MEDIUM3` 5,075/3,631 and `SMALL4` 4,794/1,696.
There are zero duplicate stable IDs, zero duplicate positions at 0.01 m and
only 18 close pairs at 0.1 m. The earlier 2,149-original count belonged to one
pilot cell and was never a map-wide source count.

Natural infill is a globally ranked deterministic independent set with a hard
cap of 20% of accepted originals. A candidate now needs at least four accepted
donor originals within 55 m, occupancy in at least three of eight angular
sectors, and a largest empty angular arc below 180 degrees. This admits an
internal forest hole and rejects a one-sided forest edge. `OpenSpace` remains a
hard tree exclusion; roads, railways, buildings, fields and water still pass
through the normal geometry/exclusion query. Brown natural forest soil is
allowed because the colour mask belongs only to grass. Reports include every
infill rejection family, the stable cap rejection count, per-cell
minimum/median/maximum and an explicit zero accepted-on-OpenSpace assertion.

The recovered boundary consists of two nearly concentric enclosures. Static
analysis of the copied source records gives LOW: 568 closed edges, 21,548.60 m
perimeter and 16,173,837.75 m² area; HI: 255 open-chain edges, 21,890.51 m
perimeter, a 140.13 m endpoint gap and 17,765,527.84 m² area. HI is the outer
envelope. Its strict small-gap closure is a separately labelled synthetic edge
used only for distant masking and coverage; it never changes donor geometry or
collision. Polygon winding, adjacent wall-triangle plane evidence and donor-tree
containment establish the signed side. Mirrored/translated coordinate
corrections are inverted before source-space containment tests.

The near band has separate signed policies. Its inward 0–75 m front layer
requires the same surrounding-donor test and real opted-in ground; its outward
side also requires real ground. Both keep `OpenSpace` and the normal exclusions.
Candidates are processed in global stable-hash order. Accepted near trees are
hard-capped at 12,000 and near boundary forest-floor instances at 8,000; reports
record accepted counts, budgets and candidates skipped after each cap. The
generator fails if topology provides an inward band but no inward front tree is
accepted, if any accepted boundary tree enters `OpenSpace`, or if a budget is
exceeded.

The collisionless distant extension uses only HI plus its labelled closure and
must remain outside the union of both enclosures. It covers signed 95–200,
200–400 and 400–650 m depth bins. A deterministic coverage backbone samples
each outer segment and bin before stable-score filler selection; the current
source has a conservative 3,345-target upper bound, below the 16,000-tree hard
cap. The outer length including closure is 22,030.64 m and the raw strip estimate
is about 63,644 grid candidates before jitter, exclusions and density. Actual
candidate, rejection, scene, renderer and vertex counts remain pending the Unity
run. Coverage validation requires every sampled segment/bin within a
spacing/crown-derived along-edge gap, zero wrong-side placements and zero trees
inside any enclosure.

Distant geometry is written to independent `vegetation-backdrop` cell-layer
scenes, never one always-loaded world root. Each scene contains bounded batched
far-LOD parts with project-approved tree material policy, tangents and optional
UV/color streams preserved, zero colliders, shadows, probes and motion vectors.
The global presentation scene owns only the 45 canonical `MAP/MESH/ROCKS`
renderer replacements. Validation maps each child ID one-to-one to its source
anchor after the same optional donor-to-world mirror/rotation/translation used
by vegetation, then checks rendered bottom-centre XYZ within 0.03 m,
missing/duplicate children and zero ALP colliders. The exact legacy ROCKS
collider remains physics authority; its renderer is disabled through the
renderer-only key. RockPale and Rockwall stay untouched.

Near vegetation uses loading radius 1 (at most a 3×3 neighbourhood); sparse
backdrop layers request radius 2 (at most 5×5 registered cells). Equal-distance
loads explicitly prioritize `vegetation` before `vegetation-backdrop`, and
backdrop registrations defer the initial manual refresh so startup does not wait
for the whole ring. Automatic refresh admits one new layer per pass. Ordinary
distance unloads coalesce unused-asset cleanup until explicit owned-scene/session
teardown; synchronous session teardown keeps a detached async-operation
countdown and starts one cleanup only after requested unloads complete. A load
already preparing at teardown is tracked before its coroutine is stopped,
forced through a disabled activation gate, never published as owned, unloaded
again by its exact scene path, and joined to that same detached cleanup.

These controls reduce residency, draw submissions and avoid known avoidable
startup/cleanup work. They do not prove the hitch fixed. Existing Editor evidence
still shows roughly 200 ms `CompleteAwakeSequence` for a representative woody
cell with about 15,500 GameObjects, and radius 1 does not divide the activation
spike of one scene. A fresh Player probe must report cold/warm load,
deserialization/integration, main-thread gaps, renderer/vertex budgets and
long-session memory before any performance acceptance claim.
