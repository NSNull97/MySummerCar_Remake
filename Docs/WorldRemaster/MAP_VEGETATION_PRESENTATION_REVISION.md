# Vegetation presentation correction — active v12 — 2026-09-02

## Active v12 accepted result

Vegetation presentation v12 is the current private Phase 1 baseline. Full
generation run `20260902-001303` passed all 88 cells; its report SHA-256 is
`F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`.
Fresh saved-output validation run `20260902-004112` passed the same 88 cells
with `validationOnly=true`; its report SHA-256 is
`A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`.
Both use settings hash
`7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d`
and source fingerprint
`2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`.
`Artifacts/VegetationRebuild/full-validate-exact-v12.json` additionally passed
an exact comparison of every cell report and 268 non-report artifacts; their
aggregate fingerprint is
`a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169`.

| Active saved category | v12 count |
|---|---:|
| Donor originals | 37,678 |
| Natural infill | 24,490 |
| Original-tree category, including infill | 62,168 |
| Grounded near-boundary trees | 5,811 |
| Total near trees | 67,979 |
| Spruce / pine / birch / aspen | 44,201 / 13,576 / 5,052 / 5,150 |
| Shrubs and forest floor | 18,747 |
| Grass | 5,157,636 from 36,044,800 candidates |
| Grass profile counts | 2,368,747 / 1,651,618 / 1,137,271 |

The natural-infill policy is deterministic and capped at 65% of the 37,678
accepted donor originals. It accepted 24,490 candidates while preserving the
road, yard, field, water, route and OpenSpace exclusions. The tree presentation
keeps the requested 65/20/7.5/7.5 policy and produces the exact species totals
above across original, infill and grounded boundary records.

Grass now uses only the reviewed non-cereal Forest Environment
`Grass02_3`, `Grass01_3` and `Grass03_3` sources, selected at 46/32/22. The
candidate grid is 0.8 m at density 0.96. Each saved record carries progressive
near/middle/far carpet geometry with 16/8/6 authored clumps and LOD distances
18/42/60 m, so the middle and far rings no longer collapse to isolated specks.
No cereal or seed-head source is approved. The corrected binding provenance is
`Artifacts/VegetationRebuild/GrassArt/provenance.json`, SHA-256
`76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`.

Woody and forest-floor presentation is packed rather than serialized as one
prefab hierarchy per placement. Across 88 cells, the accepted output contains
86,726 matrices and matching metadata records, 2,130 prototype references,
47,138 batches and 62,168 collision records. Packed assets total 76,558,979
bytes; the 88 scene files total 1,088,371 bytes. The saved scenes contain zero
prefab-instance GameObjects, direct mesh renderers or serialized collider
components. Collision for accepted original/infill trees is supplied by the
bounded runtime pool. Combined authoring payload is 77,647,350 bytes versus
v7's 482,661,729 scene bytes, a measured 83.91% reduction in serialized
vegetation payload; this comparison is not runtime-memory evidence.

The collisionless distant backdrop contains 16,000 trees in 81 streamed scenes:
307 renderers, 349,465 vertices, zero colliders, zero crown gaps and zero
wrong-side or inside-enclosure placements. It loads/unloads at radii 2/3; normal
near vegetation retains radius 1. The global presentation replaces all 45
canonical rock visuals at their measured anchors with 149 renderers and zero new
colliders. Exact legacy rock collision remains authoritative; there is no free
rock scatter.

The direct packed-scene Editor benchmark passed with maximum main-thread time
66.0798 ms, maximum observed yield 71.6469 ms and p95 yield 20.6325 ms. The
production-service route passed at 63.8666 / 68.1513 / 14.5884 ms and completed
cleanup with no owned scenes, renderers or valid GPU buffers left behind. Core
PlayMode passed 22/22 and the targeted EditMode set passed 77/77. The matching
v12 HDRP pilot capture was accepted.

These results replace the old hundreds-of-milliseconds serialized-hierarchy
baseline and verify the current Editor gate. They do not measure the complete
donor world in a Player build, representative display GPU timing or guaranteed
60 FPS. The selected vendor presentation remains removable private Phase 1
content, `productionReady=false`; the ALP licence/provenance blocker remains.
No save DTO, stable gameplay ID, terrain, road, UI or weather-owner migration is
introduced. The next milestone is one integrated Player traversal benchmark of
real cell crossings and long-session memory using this exact v12 output.

## Active v12.1 packed-woody motion-vector hotfix — 2026-09-02

The generated placement and presentation payload remains v12. The v12.1 change
is a project-owned runtime correction in `PackedWoodyCellRenderer`; it does not
regenerate or rewrite any vegetation cell, packed asset, prototype binding or
placement record.

Packed woody draws submit current `Matrix4x4[]` instance transforms to
`Graphics.RenderMeshInstanced` and do not submit `prevObjectToWorld` data.
Requesting `MotionVectorGenerationMode.Object` therefore gave HDRP no valid
previous transform for each instance and produced the observed radial tree
streaks under TAA and motion blur. Every packed woody LOD now uses
`MotionVectorGenerationMode.Camera`. Resident placements are static, so this
keeps camera-derived motion while avoiding fabricated per-instance motion.

The post-capture focused EditMode suite passed 26/26 in
`Artifacts/VegetationRebuild/Tests/MotionVectorHotfix.PostCapture.20260902.xml`,
SHA-256
`07BCE65CDF8647FCD72D7A41C65678E54C291DEF7C67A6E25146E5F3C60B4373`.
The isolated vegetation compile also passed. The graphics-enabled temporal audit
ran with TAA, object-motion-vector frame settings and gameplay-equivalent motion
blur; `capture-report.json` passed all seven views with zero missing or
unsupported shaders, SHA-256
`7AC14981327EFDA338AE3E6AADF3442058F29F5F2DCB60BAD4E7D5966BEDBCC4`.
Manual inspection of `02_Forest_EyeLevel.png` and `03_Road_EyeLevel.png` found
sharp tree silhouettes and no radial smear. The remaining acceptance gate is a
manual in-game retest at the reported location. This hotfix changes no tree
count, species ratio, LOD geometry, material, collider, stable ID, save data,
streaming radius, terrain or road data. Exact vegetation wind-deformation motion
vectors would require a later custom instance payload carrying previous
transforms; they are not claimed here.

## Active v12.2 grass lighting hotfix — 2026-09-02

The grass placement, meshes, materials, profiles and catalogs remain the
accepted v12 payload. V12.2 updates project-owned shader, compact rendering,
future material-authoring and validation code without rebuilding that payload.
`MSC_VegetationIndirectHDRP.shader` retains GUID
`49f3045c658005748ad888e65476f1ed`; its current SHA-256 is
`FAD25AA707CEA1BBDC24EBB4899CDF880B6C6AE8803C1CA1E96EB612754D81D2`.
No vegetation cell or grass instance was regenerated.

Before the correction, zeroing all audit radiance left grass unchanged:
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-083140-659-p26348`
failed with mean-luminance and bright-pixel ratios of 1.0 / 1.0. After replacing
the independent hemisphere-lit forward colour with the HDRP Lit LightLoop,
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-090040-053-p20132`
passed at 0.0 / 0.0. Both
measure the selected generated cell; the passing run rendered 126,982 saved
grass records in 712 GPU batches.

This result explicitly supersedes the older simplified-unlit/hemisphere shader
limitation as current behavior without deleting its historical record. It proves
zero-radiance suppression for this grass audit. The final extended report
`Artifacts/VegetationRebuild/NightEmissionAudit/20260902-093656-076-p39636-2f2d286bd4d648a0a3b5002e31080da2/night-emission-report.json`
passed with SHA-256
`F6D9748BBC065CF154DB50BF80B6B16C3209159C0A227205BA73202D3F8DDFC8`.
Directional mean luminance was 0.2896735966, punctual mean luminance was
0.02317777649, and zero-radiance mean, maximum and bright-pixel fraction were
exactly zero. Directional and punctual mean/bright ratios were all zero over
126,982 records and 712 batches.

The final focused D3D11 EditMode suite, including
`MapVegetationNightEmissionAuditTests` and
`VegetationIndirectMotionVectorContractTests`, passed 40/40 in
`Artifacts/VegetationRebuild/Tests/GrassLightingHotfix.Final4.20260902.xml`,
SHA-256
`B3D743D7E9479B9510B5BE685C7F3B3A3A235D01F6645C866BC08ED9A5EBBDCA`.
The day capture report
`Artifacts/VegetationRebuild/VisualAuditCells/cell_-4_-3/capture-report.json`
passed 7/7 with no missing or unsupported shaders and zero magenta fraction in
every view; its SHA-256 is
`1417C8CAE6D9179EDFEB720C0A881599AB5FDE61764B51C8085228D22D38F237`.

`VegetationWorldRenderer` now uses camera motion vectors for compact grass. Its
current custom object-motion pass lacks the complete HDRP
normal-buffer/MRT/stencil contract, so exact wind-deformation velocity remains
explicit debt.
`MapVegetationGrassBindings` now authors smoothness 0.04, specular F0 0.018,
black emission and `EmissiveIsBlack` on future grass-material regeneration.
Active material YAML was not reserialized; runtime values resolve from shader
defaults plus existing `m_LightmapFlags: 4`. No current vegetation payload was
regenerated.

Full-map LightLoop performance and subjective gameplay day/night appearance
remain pending. The punctual-light gate is complete. The third-party grass
presentation remains removable private Phase 1 content with
`productionReady=false`.

## Historical v7 package snapshot — 2026-09-01

Everything through the next explicitly historical revision heading records the
superseded v7 state. Words such as “active” and “current” inside that snapshot
refer to 2026-09-01 and do not override the accepted v12 result above.

Status: final presentation revision **v7** passed its saved pilot and matching
five-view graphics-enabled HDRP capture in `cell_-4_-3`. Active full generation
run `20260901-092757` then passed 88/88 cells with zero errors; fresh-process
`ValidateAllBatch` run `20260901-100750` passed 88/88 with
`validationOnly=true` and the same settings, source and per-cell fingerprints
and counts. The old 175/175 EditMode, 8/8 functional PlayMode, 50/35 mix,
Chernobyl grass and Engelmann spruce results below remain historical evidence;
the reviewed Chernobyl pine/birch/aspen bindings remain active.

### Historical v7 accepted scope

The current user-approved private Phase 1 target is:

- Preserve accepted tree anchors and the validated population while assigning
  **65% spruce, 20% pine, 7.5% birch and 7.5% aspen**. The completed active run
  contains 66,910 trees: 43,492 / 13,382 / 5,018 / 5,018 after
  largest-remainder allocation.
- Replace spruce presentation with five reviewed ALP variants:
  `ConiferTreeBig01_Optimized`, `ConiferTreeBig02_Optimized`,
  `ConiferTreeBig04_Optimized`, `ConiferTreeSmall03_Optimized` and
  `ConiferTreeSmall04_Optimized`.
- Retain the reviewed licensed Chernobyl `Tree/Pine_*`, `Tree/Birch_*` and
  `Tree/Aspen_*` bindings for 13,382 pine, 5,018 birch and 5,018 aspen. This
  active non-spruce provenance is separate from the superseded Chernobyl grass.
- Replace the prior Chernobyl grass binding with the reviewed
  NatureManufacture Finnish subset. Final presentation revision v7 uses separate
  detailed, regular and cross-mesh source prefabs as LOD0/LOD1/LOD2, a 0.65 m
  candidate grid and compact indirect GPU instances rather than grass GameObjects.
- Keep the existing full-resolution green terrain texture mask in the placement
  decision after natural-surface, road, building, field and water exclusions.
- Add restrained forest-floor pools: ALP rocks/boulders/stones; Finnish grey
  willow; fern, moss, selected mushrooms and dead grass; plus reviewed branch
  and poplar-leaf litter.
- Keep the existing five Wwise bird ambience layers for morning, day, evening,
  night and swamp. The reviewed packages contain no usable flying-bird mesh,
  rig or animation, so this scope adds audible birds, not simulated bird actors.
- Continue reducing secondary streaming work without claiming the native scene
  completion hitch fixed.

No terrain, road, building, world coordinate, gameplay ID, save DTO, accepted
UI, weather owner or donor installation is changed by this art pass. It does not
approve Phase 2 or certify the earlier southern boundary/void debt.

### Historical v7 package and provenance decisions

The ALP archive SHA-256 is
`39D47D1A7A8424A3C1F39DD55DD64ED6CEE42542A13466CC04FB40280126B827`.
Only the five tree variants above and reviewed rock/stone dependencies are
selected. `Big03` is excluded as an approximately 58-metre accent unsuitable for
the ordinary population; `Small01` is weak/redundant; `Small02` has broken
texture references. Group prefabs, demos, vendor scripts and the package bird
prefab are excluded. The bird prefab is only an AudioSource container and points
to missing clips; it is neither a flying-bird implementation nor an audio source
for this project.

The ALP payload is classified
`ThirdPartyPrivatePhase1Presentation`, with `productionReady=false`. Its archive
contains a redistribution-site marker but no reviewed purchase receipt or
licence proof. This is an explicit provenance/licence blocker: the local private
Phase 1 wrapper must remain removable and cannot be called production-ready or
approved for distribution.

The NatureManufacture Forest and Meadow archives are selectively imported by
dependency closure rather than package-wide. Forest uses 22 seeds and a
79-asset closure; Meadow uses 51 seeds and a 156-asset closure. Both audits have
zero unresolved external GUIDs, demo scenes, scripts or Editor assets. The
Finnish subset supplies the three detailed/regular/cross grass bindings, grey
willow, ferns, moss, selected Armillaria/Russula mushrooms and dead meadow grass.
Binding v3 additionally maps `BranchLitter01` from
`prefab_detail_branches_01` and `PoplarLeafLitter01` from
`prefab_detail_poplar_leaves_01_1`. Project-owned Unity 6 HDRP wrappers adapt
those assets without treating vendor terrains or render-pipeline conversion
content as map source.

The two litter bindings preserve the existing `DeadGrass02` and `DeadGrass03`
output slots and prefab GUIDs. They change presentation without adding or moving
instance records, so generated shrub/forest-floor and map totals remain stable.

#### Historical accepted presentation binding v7

The final v7 grass profile retains this bounded composite geometry per saved
instance:

| Profile | Near / middle / far copies | Near / middle / far triangles |
|---|---:|---:|
| Short | 4 / 2 / 1 | 576 / 28 / 27 |
| Meadow | 3 / 2 / 1 | 330 / 28 / 28 |
| Tall | 2 / 1 / 1 | 348 / 15 / 30 |

Candidate spacing is 0.65 m. The full-resolution texture mask dilates reviewed
green eligibility by three source texels so narrow green areas and immediately
adjacent texels do not break into obvious holes. Every road, building, field,
water and other gameplay exclusion still runs independently. The footprint
calculation requires at least 0.77 m, while the non-decreasing authoring update
preserves the serialized 0.94 m v5 safety floor for ordinary and field
clearances; railway remains 1 m.

Pilot run `20260901-092200` in `cell_-4_-3` passed with 1,967 original trees,
1,727 boundary trees, 1,793 shrubs/forest-floor instances and 223,100 saved
grass instances from 620,500 candidates. Its gate uses settings hash
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569`
and generated fingerprint
`fa868c935f994e13a0a1fb637a45c0184729ae9cf003c3c8e9431983e12b5da6`.
The matching v7 HDRP capture passed all five views on an NVIDIA GeForce RTX 4070
SUPER with zero missing or unsupported shaders. Reviewed views keep roads clear
and show denser grass on green and three-texel-neighbour regions.

The broader colour mask increases saved instance records and their catalog/tile
metadata. It does not increase the composite copy count inside each instance;
that geometry remains fixed by the table above. This one-cell result demonstrates
the selected pilot, not a promise that the whole map is a continuous grass carpet.
Brown/noneligible terrain and explicit exclusions may remain open by design.

The reviewed subset now contains approved branch-cluster litter and poplar-leaf
litter through the two binding-v3 sources above. It still contains no reviewed
standalone Finnish conifer-twig or cone scatter prefab, log, root or stump pool;
the approved litter composites must not be described as those missing classes.

#### Historical v7 full-map generation and validation

`RunAllBatch` run `20260901-092757` passed all 88 cells with zero errors.
`ValidateAllBatch` run `20260901-100750` started in a fresh Unity process and
passed all 88 saved cells with `validationOnly=true`. Both reports have settings
hash `2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569`,
source fingerprint
`55ee507db015d4e9fc68f3034e999487dfa174cb9ab8da3a165046c2114dc407`,
and identical values for every per-cell field, fingerprint and count.

| Active saved category | Count |
|---|---:|
| Original trees | 37,678 |
| Boundary trees | 29,232 |
| Total trees | 66,910 |
| Spruce / pine / birch / aspen | 43,492 / 13,382 / 5,018 / 5,018 |
| Shrubs and forest floor | 28,759 |
| Grass | 8,077,749 from 54,599,726 candidates |

The run wrote 88 vegetation scenes totalling 482,661,729 bytes. The largest is
`cell_-4_-3` at 27,079,109 bytes. Fresh v7 GPU PlayMode passed 6/6 with zero
failures/skips; the green texture-mask filter passed 15/15 with zero
failures/skips. The current v7 WorldRemaster `MapVegetation` filter passed 41/41
with zero failures, superseding the earlier 40/40 result; the separate
forest-floor binding-v7 filter passed 5/5. Evidence is
`Artifacts/Tests/VegetationGpuStreamingPlayModeV7GroundDebris.xml`,
`Artifacts/Tests/MapVegetationGrassTextureMaskEditModeV7.xml`,
`Artifacts/Tests/MapVegetationEditModeV7GroundDebris.xml` and
`Artifacts/Tests/MapVegetationForestFloorBindingV7.xml`. The matching v7 pilot
capture passed. This establishes current data
generation, saved-output determinism and the reviewed pilot view; it does not
describe the entire map as a continuous grass carpet or provide map-wide visual
or runtime-performance acceptance.

Current Play-mode pacing allows at most one automatic presentation-layer load per
refresh, gives it low async priority and prepares to 0.9 before activation on a
separate frame. Grass upload work is limited to 4 operations / 8,192 instances /
a cooperative 1 ms per frame; catalog metadata preparation is incremental at
4 operations / 256 profile records / a cooperative 1 ms per frame. These limits
reduce secondary spikes only. Measurements still place the dominant hitch in
main-thread `LoadSceneOperation.CompleteAwakeSequence` during woody scene
deserialization/integration. The retained fresh-process v6 observation recorded
one heavy scene load at 608.743 ms total: 478.810 ms deserialization and
129.870 ms integration. V7 was not reprofiled by that measurement; it remains
Editor evidence rather than Player timing and permits no "streaming fixed" or
60 FPS claim.

## Historical 50/35 Chernobyl-grass / Engelmann-spruce pass

Everything from this heading through the archived measurements records the
superseded second pass. Its Chernobyl grass, Engelmann spruce and 50/35 species
figures are preserved for audit chronology and are not the current target.

### Historical accepted scope

That pass preserved 66,910 trees and used 50% spruce, 35% pine, 7.5% birch and
7.5% aspen: 33,455 / 23,419 / 5,018 / 5,018 trees. It rebound grass to
Chernobyl Grass_01/02/03, retained the green texture mask, budgeted GPU upload
work and repaired its then-active tree LOD presentation.

### Historical findings and implementation

The saved forest initially contained 17,360 spruce, 17,902 pine, 13,950 birch and
17,698 aspen. Uniform selection also enlarged tiny authored saplings into adult
trees. Species assignment now uses exact rounded quotas over accepted trees;
variant selection compares authored height to the preserved nominal height.

Chernobyl LOD0 thresholds previously ranged up to roughly 0.62 screen-relative
height. Instance overrides now retain detailed geometry closer to the camera
without globally forcing LOD0. All authored renderer sets remain intact.

The five generated Engelmann middle LODs contained literal cylinder trunks.
Project-owned bindings extract the connected authored main stem from the
licensed source bark mesh, preserving its vertices, UVs, normals and tangents.
Whole high-detail bark is not copied into middle LOD. Extraction fails on
ambiguous selection or more than 10,000 stem triangles. An initial Unity attempt
correctly failed because a strict 80%-of-whole-bark height criterion rejected
Bark01_1's real stem (76.177%); selection now compares connected components.
The source art/importers remain unchanged.

Grass profiles use the active `_Base_Color` atlas and original alpha cutoff.
Grass_01 reuses its single authored LOD; Grass_02/03 use their authored two LODs,
with LOD1 reused at far distance. Physical height ranges remain
0.16–0.30 / 0.30–0.58 / 0.55–1.05 metres before category scale.
The existing indirect shader remains the presentation path: this is not a claim
of new HDRP Lit lighting, normal mapping, shadow reception or wetness support.

| New profile | Installed source | Measured source height | Near / middle triangles |
|---|---|---:|---:|
| ChernobylShort | `Assets/Chernobyl/Prefabs/Vegetation/Grass_01.prefab` | 0.576515 m | 32 / 32 |
| ChernobylMeadow | `Assets/Chernobyl/Prefabs/Vegetation/Grass_02.prefab` | 1.116532 m | 136 / 78 |
| ChernobylTall | `Assets/Chernobyl/Prefabs/Vegetation/Grass_03.prefab` | 1.447036 m | 120 / 62 |

These are authored alpha-cut clumps, not individual volumetric blades. Their
active `Grass_01_A.tga` atlas SHA-256 is
`7c39a0cb777861b9b229ff01d14740d9b1db5f2638b1541569e8a20ae3b126b4`;
generated copies match. The source material cutoff is 0.742 and RGB tint white.
Detailed source/output hashes and bounds are in ignored
`Artifacts/VegetationRebuild/GrassArt/provenance.json`.

The authoring mask snapshots approved ground triangles, UV0, material texture
mapping and full-resolution source PNG/JPEG colour before scene unloading.
It accounts for texture scale/offset, wrap and point/bilinear sampling. It does
not treat green colour as permission to plant on roads, roofs, water or fields.
Unknown mapping in configured coverage is rejected with diagnostics; surfaces
outside configured mask coverage are listed explicitly. This is an authoring
colour mask, not a simulation of GPU mip selection or weather lighting.

Current thresholds are `G - max(R, B) >= 1/255`, saturation at least 0.1 and
maximum channel at least 0.03. The measured largest authored footprint radius,
including normal alignment, maximum scale, packing tolerance and wind, is
0.74748373 m. That produces a theoretical 0.77 m minimum. The updater only
raises margins, so the serialized 0.94 m v5 safety floor remains active for the
validated full-map result; the existing 1 m railway margin also remains intact.

Automatic Play-mode GPU preparation rejects distant catalogs before preparing
batches, uploads needed tiles in slices, and shares an aggregate frame budget
across renderers/cameras: 8 operations, 16,384 records, and a cooperative 2 ms
CPU budget. Native driver calls cannot be pre-empted; a single call can exceed
2 ms. Metadata construction, scene activation and resource release need separate
measurement. Explicit synchronous `RebuildGpuResources()` and Editor painter/
capture behavior remain available. Upload copies of backing instance arrays
are removed; LOD command buffers are prepared with their batch.

Optional vegetation layers load nearest first, with a rendered-frame yield
between loads. Empty world-save registration batches no longer call
`Physics.SyncTransforms` twice; batches containing gameplay bodies keep the
existing freeze/restore synchronization sequence.

## Historical bounded migration and evidence

`Tools/Vegetation/Plan-PresentationRevision.py` reads the existing 88 scenes and
records their hashes and exact tree identity-to-species choices. Its first plan
SHA-256 is `0d9604037e8310d03ccfc371db5c91db81ddbe8f35b8ea7c25b3ccc45156ad83`.
The plan is refused if it already exists, preventing accidental replacement of
the pre-migration population snapshot.

`MapVegetationPresentationRevision` prepares art, applies the home-cell pilot,
then requires a matching saved HDRP capture before processing all cells.
Each cell's scene, grass data, catalog and density mask are backed up before
mutation. A failed cell restores only validated backups. Completed checkpoints
appear atomically and include all four output hashes plus settings, source,
plan, relevant implementation and generated-trunk input hashes.

Tree roots preserve exact saved XYZ, rotation relative to the authored prefab,
nominal writer height and existing world-space capsule collision. Reopened
scenes verify species/prefab identity, root transform, LOD thresholds and mesh
bindings. Shrubs remain in place. Grass records are filtered through current
surface/clearance/texture rules; retained XYZ is unchanged, while scale and
normal alignment are rebound to the new profiles. No tree positions are
replanned during this migration.

Generated art, scenes, data, backups, raw screenshots and detailed reports remain
ignored under the existing private-baseline policy. Source layout is
`WorldLayoutReference`; placement/tools are `Reimplemented`; licensed art is
recorded separately and is not donor `ProductionReady` content.

## Historical validation entry points

- `Tools/Vegetation/Validate-VegetationCompile.ps1`: isolated C# compilation,
  explicitly not Unity execution or visual acceptance.
- `MSC.Editor.Vegetation.MapVegetationPresentationRevision.PrepareBatch`
- `MSC.Editor.Vegetation.MapVegetationPresentationRevision.RunPilotBatch`
- `MSC.Editor.Vegetation.MapVegetationVisualAudit.CapturePilotBatch` with
  `-vegetationAuditCell cell_0_-3 -vegetationAuditFocusX 155 -vegetationAuditFocusZ -1035`
- `MSC.Editor.Vegetation.MapVegetationPresentationRevision.RunAllBatch`
- `Tools/Vegetation/Verify-PresentationRevision.py`: independent saved-output
  hashes, tree identities/species quotas and actual serialized grass counts.

The original first-pass verifier remains historical evidence for its original
fingerprints; use the revision verifier for the corrected map.

New regression coverage includes real authored-trunk extraction, LOD renderer
preservation, tree quotas/identity stability, UV/texture rules and footprint
envelopes, shared GPU budgets, exact uploaded data, sliced oversized batches,
distance rejection and resource release. Execution results and actual timings
must be recorded below before declaring this pass validated.

## Historical measured intermediate state (before full-map correction)

The graphics-enabled Unity PlayMode run in
`Artifacts/VegetationRebuild/revision-playmode-results.xml` passed 10/10,
including shared upload-budget/readback/lifetime tests, existing cell-layer
regressions and two explicit measurements. The home cell was corrected at this
point; neighbouring cells still used the first-pass grass. These measurements
are archived in `PresentationRevision/PerformanceBeforeFull` and must not be
represented as the completed map's performance.

With 25 saved home-area catalogs, explicit eager preparation uploaded 8,200,397
records into 17,310 buffers in 501.8152 ms of synchronous CPU work. Automatic
preparation uploaded 161,039 nearby records into 572 buffers over 37 frames:
16.2032 ms total upload CPU, 3.6781 ms worst observed frame, at most 8 operations
and 9,957 records per frame. The eager comparator uses the current code's
explicit synchronous API; it is not an instrumented pre-patch executable.
Catalog loading (8,318.4566 ms) is excluded from these upload timings. Camera
resolution is 256x256; these results do not establish game FPS or GPU cost.

A separate native-scene test caught a 776.7405 ms yield interval loading the
still-old dense `cell_1_-3`, against 114.1164 ms for corrected home `cell_0_-3`.
GPU upload steps in that run peaked at 1.0992 ms, so GPU preparation alone does
not explain the remaining hitch. All three scenes and their captured GPU
buffers were released correctly. The test directly calls SceneManager and
excludes the production streaming service's save-registration notifications.
Repeated first/warm loads with native markers are required after full migration
before drawing a conclusion about the remaining main-thread work.

The expanded EditMode suite exposed both fixture contamination and a real
pre-existing far-LOD defect. The fixture now separates magenta silhouette
coverage at EV14 from colour readback on black at EV12 under a controlled white
HDRP key light. Green pixels count only inside the independently measured
silhouette; the original >500 thresholds remain. Earlier failed runs remain
recorded and are not counted as passing validation.

The shared project-owned `EngelmannSpruce_TieredCrossBillboard` had all 42
triangles wound opposite their authored normals. HDRP back-face normal handling
therefore lit the visible surface from the wrong side. The producer is corrected
and the targeted `MapVegetationBillboardWindingRepair.RepairBatch` executed,
changing only index winding. All 84 vertex/normal/tangent/UV records, bounds,
GUID, prefab/scene references and materials are preserved. Source mesh backup
SHA-256 is `dc2c8892f9eac24969126755aee64863f4e696bb390f5cd082359d8166a62137`;
repaired SHA-256 is `591ce1c5809068ef48599fe3d51382d70bd47be80d3be953ee9e72ed845ce59c`.
The independent verifier decodes the serialized index/vertex buffers, confirms
the exact 42 index swaps and unchanged vertex bytes, and measures face/normal
dot >= 0.9999999. This shared-asset correction follows the population migration;
its new dependency hash does not rewrite that migration's historical journals.

## Historical completed full-map data validation

`RunAllBatch` exited successfully with 88/88 passed cell journals and no errors.
The independent verifier then passed all four saved-output hashes per cell,
exact tree identity/species counts, source-prefab hashes, disabled authored
collider overrides and actual serialized grass records. It also rejects missing
or duplicated zero-tree cells and mismatches between checkpoints and the full
report. Report SHA-256:
`dd2cddb72ebd3765fd06a4a31c70d6aeebfbd03838030f6a83ec354e7d7cf22e`.

| Saved population | Before | After |
|---|---:|---:|
| Trees | 66,910 | 66,910 |
| Spruce | 17,360 | 33,455 |
| Pine | 17,902 | 23,419 |
| Birch | 13,950 | 5,018 |
| Aspen | 17,698 | 5,018 |
| Grass clumps | 22,990,564 | 7,751,195 |

All 26,127 shrubs remain unchanged. Grass rejection accounting is complete:
15,087,021 records failed the colour mask and 152,348 failed renewed clearances.
There are no unsupported-UV/texture or missing-ground rejections. Six positive
cells retained less than 10% of their former grass, including the small outer
`cell_-5_-3` (2,424 to zero); strict colour filtering may need visual tuning in
olive/brown regions. These data checks are not whole-map visual acceptance.

The updated-map PlayMode run `revision-playmode-results-r2.xml` passed 10/10
with zero skips. The 25-catalog upload benchmark now has 3,321,275 total records;
explicit eager preparation took 326.9626 ms synchronous CPU and 15,366 buffers.
Automatic preparation needed 80,120 nearby records / 558 buffers, with 13.8356 ms
total upload CPU over 36 frames and 3.2749 ms maximum aggregate upload CPU in a
frame. Maximum operation count was 8; maximum uploaded records in a frame 4,631.
Catalog-load time (3,296.738 ms) is excluded from this comparison.

The two-pass real-scene benchmark still detects approximately 119 ms and 186 ms
load pauses for home/dense cells even on a warm repeat. The distant dense cell
allocates/uploads no grass GPU buffers in either pass. Native profiler markers
in that first instrumented run were unavailable because resetting the recorder
also stopped collection; wall-time, Main Thread and buffer results remain valid,
but those native counters cannot be used for attribution. The corrected native
run `revision-native-results-r3.xml` passed 1/1 and captured the main-thread
`LoadSceneOperation.CompleteAwakeSequence`: 123.28 ms for home and 190.76 ms for
the dense cell on first load; 125.50 / 231.78 ms on repeat. Main-thread
ReadObject stayed <=1.8 ms and Physics.SyncTransforms <=0.009 ms. This identifies
native scene completion as the dominant residual spike, not grass-array
deserialization or GPU uploads.

The subsequent same-population experiment passed its functional checks (1/1,
`revision-woody-activation-results.xml`) but disproved delayed activation as a
fix. All variants preserved 2,342 woody prefabs / 15,518 GameObjects / 7,605
renderers / 2,721 colliders. Warm native completion took 195.45 ms active,
192.21 ms with categories inactive and 188.89 ms with individual prefabs
inactive; observed load gaps were 199.97 / 196.69 / 193.23 ms. Activating the
individual prefabs later took at most 0.7993 ms per batch over 37 frames, but
did not remove the earlier native completion spike. All copies unloaded and
their temporary Build Settings entries were removed. No delayed-activation
runtime component or new grass serialization format was introduced. Unpacked
same-population and smaller diagnostic scene units were then measured without
changing production scenes (`revision-alternative-results.xml`, 1/1 passed).

| Warm Editor variant | Woody roots / GameObjects | Maximum load gap | Native CompleteAwakeSequence |
|---|---:|---:|---:|
| Original connected prefabs | 2,342 / 15,518 | 197.3543 ms | 193.4156 ms |
| Same population, fully unpacked copy | 2,342 / 15,518 | 56.1004 ms | 44.7277 ms |
| Representative prefab subset | 64 / 447 | 7.3258 ms | 5.9474 ms |
| Representative prefab subset | 128 / 853 | 12.8807 ms | 11.4281 ms |

The full unpacked copy preserves every selected ID, transform, render/collider
asset reference, LOD and grass catalog before save and after reopening. The
64/128 scenes are deliberately smaller populations, stratified by prefab and
category, not a same-population speedup or proposed spatial partition. All eight
prime/warm cases unload successfully, and temporary Build Settings entries are
removed. This demonstrates substantial Editor prefab integration cost but does
not measure player behavior. A bounded standalone diagnostic is required before
choosing a production tree-storage change. The residual hitch remains open,
not a passing 60 FPS result.

After the targeted billboard repair, graphics-enabled EditMode execution
`revision-editmode-results-r6.xml` passed 175/175, zero failed/skipped, in
5.32797 seconds. This includes all existing tree-render checks and the three
new winding/preservation/idempotence regressions. Earlier failures are retained
as diagnostic history; they do not replace this executed result.

The final functional PlayMode rerun `revision-playmode-functional-r3.xml`
passed 8/8, zero failed/skipped, after the repair and diagnostic additions.
It repeats scene ownership/retention/lifetime and exact GPU upload/readback
checks without overwriting the separate performance recordings.

The last expanded EditMode run `revision-editmode-final-r7.xml` additionally
included 13 existing world-lighting tests: 185/188 passed, with zero skips.
All 175 vegetation/streaming/save cases still passed. The three failures are
in `WorldLightingProbeCatalogTests`: expected 45 catalog lights versus 49,
expected 12 night-only lights versus 14, and the point-to-area fixture pose
test measured a 176.39-degree difference from its expected pose. Independent
inspection reproduces that exact angle from the current refrigerator catalog
Euler pose (90, 147.397, 90.001) versus the older test expectation (0, -117.5, 0):
the runtime preserved the current catalog. All 49 catalog entries use realtime
bake mode. The compile guard described below leaves the Editor method body
unchanged; these existing catalog/expectation mismatches were not hidden by
changing assertions during the vegetation task.

Post-repair saved HDRP captures for home `cell_0_-3` (focus 155/-1035) and sparse
`cell_-2_1` (focus -751.6787/736.03436) both exited 0 with `passed=true` and no
missing shaders. Home shows the new grass clumps and clear road/home platform;
the sparse focus exposes concentrated grass near the building and extensive
bare brown ground. These deliberately controlled HDRP views are not the live
Enviro game camera, full-map visual approval, or evidence of 60 FPS. Captures
and per-cell reports remain in `Artifacts/VegetationRebuild/VisualAuditCells`.

The sparse capture is intentionally a density check, not a road-clearance test:
there was no eligible road triangle, so its third view is a forest alternate.
Some far neighbour cells are unavailable in this isolated capture. Within 60 m
of the chosen focus, saved grass falls from 8,956 to 172. Total non-colour
rejections across the entire cell are only 2,341, so at least 6,443 of the local
8,784 removals are attributable to colour. Conversely, the upper green-looking
plane in the image contained only 62 old points in the checked projection;
most of that visible plane was already unplanted. The journal cannot establish
its precise field-versus-unapproved-ground classification. Neither its empty
horizon nor each apparent green pixel is evidence of a new mask failure.

## Historical standalone diagnostic and actual blockers

`MapVegetationPlayerProbeBuild.BuildBatch` builds only an owned diagnostic
bootstrap and the four already verified scene variants. It uses the existing
explicit-scene build scope, a private-process environment flag, the existing
private-baseline manifest permission and `BuildOptions.Development`. All normal
build guards remain enabled; exact scene dependencies are additionally checked
against reference-only/raw donor paths. The Development-only
`VegetationScenePlayerProbe` embeds source hashes and selected IDs at build time;
it does not read project YAML or donor files while running. It records graphics
API, prime/warm native timings, selected population and unload/resource checks,
then exits with a result code. It is not attached to any gameplay scene.

Real Player-reference compilation exposed an existing unsupported API call in
`WorldLightingProbeRuntime`: `Light.lightmapBakeType` is Editor-only. The sole
correction wraps that authoring assignment in `#if UNITY_EDITOR`; Editor
behavior and current realtime catalog settings are unchanged. Runtime-created
lights have no precomputed bake contribution. No fake bake output, lighting
catalog change or weather redesign is introduced. Whole World Editor/Player,
standalone probe and Editor builder isolated compilations passed after this
fix. Five pre-existing TextureInventoryExporter obsolete-API warnings remain.
Unity documents the API boundary in
[Light.lightmapBakeType](https://docs.unity3d.com/cn/6000.0/ScriptReference/Light-lightmapBakeType.html).

Actual Unity build `Logs/vegetation-revision-player-build.log` **failed** during
preprocessing (67.716335 s reported), before a diagnostic player could run:

- `Phase1CharacterPresentationBuildGuard`: generated character fixture `suski`
  fails validation. Its generic error text says "missing or retained an
  Animator", but covers additional validation conditions; this pass does not
  claim which character binding actually failed.
- `Phase1PlayerVoiceBuildGuard`: generated player voices have a stale or invalid
  manifest.

The build report is `Performance/player-probe-build.json`, with `passed=false`
and `sourceAndBuildSettingsUnchanged=true`. Source scenes, fixture journal,
scene dependencies and global Build Settings passed their before/after checks.
The private guards were not disabled, and NPC/voice presentation was not
regenerated merely to unblock this vegetation diagnostic. Repository section
30 and the accepted-baseline preservation rule require stopping this broader
change rather than silently repairing/replacing unrelated presentation.
There is no standalone timing, successful executable or 60 FPS result from
this attempted build. The approximately 200 ms Editor tree hitch is still an
open defect; delayed activation and blind unpacking were not deployed.

## Historical changed project-owned files

Paths below are relative to the repository root; generated licensed meshes,
materials, map scenes/data, screenshots and backups remain ignored.

| Area | Files |
|---|---|
| Existing-map migration and independent audit | `Assets/Game/Editor/Vegetation/MapVegetationPresentationRevision.cs`; `Tools/Vegetation/Plan-PresentationRevision.py`, `Verify-PresentationRevision.py` |
| Species, variant sizing and authored stems | `Assets/Game/Editor/Vegetation/MapVegetationTreeMixture.cs`, `MapVegetationTreePresentation.cs`, `MapVegetationContext.cs`, `MapVegetationRebuild.cs`, `MapVegetationRebuildOptions.cs`, `MapVegetationRebuildWindow.cs` |
| Shared far-LOD correction | `Assets/Game/Editor/WorldBaseline/Phase1VegetationAssetBuilder.cs`; `Assets/Game/Editor/Vegetation/MapVegetationBillboardWindingRepair.cs`; `Assets/Game/Tests/EditMode/WorldRemaster/MapVegetationBillboardWindingTests.cs` |
| Grass assets, geometry envelope and UV mask | `Assets/Game/Editor/Vegetation/MapVegetationGrassBindings.cs`, `MapVegetationGrassTextureMask.cs`, `MapVegetationSurfaceQuery.cs`, `MapVegetationPlacementSettings.cs` |
| Runtime upload budgeting | `Assets/Game/World/Runtime/Vegetation/VegetationWorldRenderer.cs`, `VegetationTypes.cs`, `VegetationUploadFrameStatistics.cs` |
| Existing streaming/save integration | `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs`; `Assets/Game/Save/Integration/WorldEntitySaveParticipant.cs` |
| EditMode regressions | `Assets/Game/Tests/EditMode/WorldBaseline/MapVegetationGrassTextureMaskTests.cs`, `Phase1TreePlacementPolicyEditModeTests.cs`; `Assets/Game/Tests/EditMode/WorldRemaster/MapVegetationTreePresentationTests.cs`, `MapVegetationTreeMixtureTests.cs` |
| PlayMode regressions and measurements | `Assets/Game/Tests/PlayMode/WorldRemaster/VegetationGpuStreamingPlayModeTests.cs`, `VegetationCellActivationBenchmarkPlayModeTests.cs`, `ProductionWorldCellLayerPlayModeTests.cs` |
| Private diagnostic scene copies | `Assets/Game/Editor/Vegetation/MapVegetationActivationExperiment.cs` |
| Private standalone diagnostic | `Assets/Game/Editor/Vegetation/MapVegetationPlayerProbeBuild.cs`; `Assets/Game/World/Runtime/Vegetation/Diagnostics/VegetationScenePlayerProbe.cs` |
| Existing Player compilation blocker | `Assets/Game/World/Runtime/Lighting/WorldLightingProbeRuntime.cs` (Editor-only bake-setting guard) |
| Supporting verification | `Tools/Vegetation/Validate-VegetationCompile.ps1` |

The active package revision additionally uses
`MapVegetationAlpSpruceBindings.cs`, `MapVegetationForestFloorBindings.cs`,
`import_alp_spruce_pack.py` and `import_naturemanufacture_finnish_subset.py`.
The generated vendor payload and provenance reports remain outside Git.

Existing public runtime APIs, serialized grass instance records, gameplay IDs,
save DTOs and scene registrations are preserved. This is a bounded generated
presentation migration, not a save migration; no new playthrough is required.
No donor installation, vendor source importer, UI/weather owner or world
geometry is modified by the correction.

## Historical v7 data acceptance and then-next milestone

The 50/35 mix with Chernobyl grass and Engelmann spruce applies only to the
archived second pass. The active revision uses ALP spruce, retained Chernobyl
pine/birch/aspen and NatureManufacture grass/understory; it now has its own
completed 88-cell generation and fresh-process saved-output validation with
current fingerprints. The original backups and historical journals remain audit evidence;
they are not substituted for the active reports above. Map-wide visual acceptance
and runtime-performance acceptance remain separate.

One recommended next milestone: **run a measured pilot of the compact woody
runtime catalog** in the representative dense cell. Preserve selected IDs,
transforms, LOD/material/collider references, grass-catalog ownership and unload behavior,
then compare cold/warm scene deserialization, integration, main-thread gap and
sliced activation cost before accepting any storage migration. This remains
Phase 1 optimization, not a Phase 2 gate or a claim that the hitch is fixed.

## Historical pre-v12 topology/streaming proposal — 2026-09-01

This section preserves the proposal before execution. Its pending statements
are superseded by the generated, validated and benchmarked v12 evidence above.

The next correction is code-complete only at the static-compile boundary; Unity
generation, saved-scene validation, HDRP screenshots and Player timing are still
pending. Its authoritative planning and risk details are recorded in
`MAP_VEGETATION_REBUILD.md` under “Pending signed infill, backdrop and
canonical-rock revision”.

The correction replaces one-sided empty-space filling with a donor-surrounded
infill rule, preserves hard OpenSpace/road/building/field/water exclusions, and
splits the boundary into a grounded signed near front layer and a collisionless
outer backdrop. The near tree and forest-floor caps are 12,000 and 8,000. The
distant cap is 16,000, with a deterministic three-depth-bin coverage backbone.
The 140.13 m gap in the recovered HI open chain is represented by an audited
synthetic masking/coverage edge only; LOW remains an inner enclosure and receives
no separate distant strip.

The distant forest is cell-streamed as `vegetation-backdrop`, defers initial
manual loading, yields equal-distance priority to normal vegetation, and carries
zero collision/shadow/probe/motion-vector cost. The global scene contains only
45 exact canonical rock visuals. Their child IDs and rendered bottom-centre XYZ
must match source anchors after optional donor-to-world coordinate correction
within 0.03 m; ALP collision is zero while the exact legacy ROCKS collider
remains. RockPale/Rockwall are not replaced. Session teardown also tracks a
presentation load that is still parked before or just after its activation
gate: it forces completion, unloads that exact scene without adopting it, and
coalesces unused-asset cleanup after every unload finishes.

No current paragraph may call the streaming hitch fixed. The retained
approximately 200 ms representative woody-cell Editor activation remains the
known baseline until a new Player probe measures the generated topology,
per-scene renderer/vertex counts, cold/warm transitions and long-session memory.
