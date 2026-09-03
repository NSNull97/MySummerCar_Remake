# Phase 1 vegetation replacement

Date: 2026-07-25  
Tree placement remediation: 2026-08-02  
Supplemental job-location and spruce-wind rebuild: 2026-08-02
Spruce foliage render repair and final rebuild: 2026-08-02
Engelmann spruce model diversification and latest-map rebuild: 2026-08-03
Spruce Lit/night response and invisible-collider remediation: 2026-08-08
Engelmann oversized foliage-card correction revised: 2026-08-09

> **Chronology notice — superseded presentation baseline.** This document
> preserves the July/August 2026 Engelmann/Chernobyl-grass implementation and
> its executed evidence. Words such as "active", "current" and "generated
> result" below describe that historical revision only. Active vegetation v12
> uses reviewed ALP spruce, retained licensed Chernobyl pine/birch/aspen, and
> Forest `Grass02_3` / `Grass01_3` / `Grass03_3` carpet wrappers without cereal
> sources. `RunAllBatch` `20260902-001303` and fresh-process `ValidateAllBatch`
> `20260902-004112` passed 88/88 and matched every cell plus 268 deterministic
> artifacts. The active result contains 67,979 near trees (including 24,490
> natural infill), 16,000 collisionless distant trees and 5,157,636 grass
> records. Chernobyl grass and Engelmann spruce remain superseded presentation
> sources, while the Chernobyl non-spruce bindings remain active. See
> `MAP_VEGETATION_PRESENTATION_REVISION.md` for the current source lock.

## Scope

This is a bounded Phase 1 presentation replacement. It replaces the visible
legacy trees, bushes and sprite tree-wall presentation, then adds clustered
grass coverage without changing donor map coordinates, gameplay anchors, road
layout, terrain ownership or the Phase 2 production gate.

The generated result is local private-build content. It does not classify the
licensed third-party meshes as newly authored project art and does not waive
their license terms.

## Approved source sets

- purchased CGTrader `Realistic Spruce Trees` and five `Picea abies`
  FBX/texture archives;
- user-supplied licensed
  `cl08-picea-engelmannii-glauca-engelmann-spruce` package;
- user-owned `Chernobyl` vegetation from the older Unity project;
- user-owned `NatureManufacture Assets` vegetation from the older Unity
  project.

The source-selection tool copied only the selected vegetation prefabs, their
GUID dependency closure and the NatureManufacture relative shader include that
cannot be discovered through GUIDs. It imported 251 assets from 47 seeds. The local
hash inventory is written to the ignored file
`Artifacts/VegetationImport/Phase1VegetationSourceInventory.json`.

The CGTrader spruce packages are FBX LOD sets, not native `.spm` SpeedTree
sources. The project therefore treats them as licensed LOD vegetation and
builds HDRP-compatible project prefabs around them. The five then-active Norway
spruce variants use their distinct geometry but deliberately share one healthy
texture family and restrained tint.

The Engelmann package supplies one FBX containing six source trees and 28
texture maps. The source archive SHA-256 is
`8204A399B76DC8E814D5D7DFCD683D2A3A1EB5ECF8DA3580F334D72377560A72`;
the imported FBX SHA-256 is
`BB9A11A904E2DB5EA09E9A48539212179FB24C08C261E79C60C1D8B74C1AD22A`.
Runtime uses variants `1`, `2`, `5`, `8` and `9`. Variant `3` remains in the
licensed source copy but is excluded from mass placement because its near mesh
is approximately 798,000 triangles. Full provenance is stored beside the
third-party source.

## Generated result

- 5 then-active Engelmann spruce prefabs with distinct near geometry,
  variant-matched source foliage plus a project cylinder trunk in LOD1, and a
  42-triangle tiered Engelmann cross-billboard in LOD2;
- source variants `2` and `8` use deterministic project-generated foliage
  meshes that scale each disconnected four-vertex branch card to `0.36` and
  `0.44`, respectively, around its own center. This reduces their approximately
  1-1.2 metre gameplay cards to roughly 0.4-0.5 metre after the earlier `0.62`
  pass remained too prominent during close-range play,
  without moving branch centers, narrowing the crown, changing tree height or
  affecting variants `1`, `5` and `9`;
- the source FBX is made readable only while those corrected meshes are being
  generated; the importer and all three generated runtime meshes are returned
  to non-readable state so the correction does not retain avoidable CPU mesh
  copies;
- 5 wind-enabled Engelmann foliage materials, 5 generated colour/opacity
  textures and 1 stationary HDRP bark material;
- the earlier Norway spruce assets remain available but are not referenced by
  any then-active Engelmann LOD or selected as a mass-forest source;
- 84,059 trees after cell-aware road/building/water and supplemental
  job-location clearance:
  - 50,716 Engelmann spruce (60.33% of accepted placements):
    - 10,238 variant `1`;
    - 10,206 variant `2`;
    - 10,158 variant `5`;
    - 10,042 variant `8`;
    - 10,072 variant `9`;
  - 33,343 other trees (39.67%):
    - 11,963 aspens;
    - 9,506 birches;
    - 11,874 pines;
- 5,999 shrub and fern clusters;
- 389,530 deterministic low-grass patch positions stored in 65 logical
  cell-owned data fields and rendered only around the active camera through
  GPU instancing;
- 4,017 tree placements in outward forest bands derived from former
  sprite-wall boundary geometry;
- 12,000 deterministic road-edge planting candidates sampled from paved and
  dirt-road mesh boundaries;
- 84 sparse void-fill candidates placed at least 24 metres from existing
  trees, so open areas receive only isolated trees rather than a new dense
  blanket;
- one enabled project-owned capsule collider on every generated visible tree,
  including the boundary band;
- 112 deterministic logical vegetation groups under the global legacy scene.

Tree positions come from the donor tree aggregates and deterministic local
forest clusters around those evidenced positions. A coarse 40-metre pass may
add isolated trees only where no existing tree is present within 24 metres.
Shrub positions come from the donor bush aggregates, and grass positions come
only from the canonical `Grass1` ground mesh. Grass samples are allocated
proportionally to projected triangle area instead of being capped per
triangle, rejected when the top raycast surface is a road, building, bridge or
water surface, and serialized per 512-metre logical cell. The runtime submits
a shared 36-tuft patch mesh in 1,023-instance batches only inside a 115-metre
game-camera radius (180 metres in Scene View). This replaces the former giant
crossed-card mesh fields and uses 65 lightweight field components instead of
389,530 grass GameObjects. The canonical `Grass1` ground material also receives
a restrained green Phase 1 tint so distant ground does not return to bare
brown after the near-camera blades are culled.

The tree placement preserves each source prefab's authored axis correction and
adds only a deterministic world-Y variation. This prevents the imported FBX
and older-project prefabs from being laid sideways. The former 16–25 metre
target-height range is multiplied by `0.72`, producing an 11.52–18 metre range
and reducing every generated gameplay tree by approximately 1.39 times.

Road-edge plantings are extracted from the exact paved-road and dirt-road mesh
boundaries rather than from a rectangular map approximation. Two staggered
rows are sampled outside each road edge. Every final tree placement checks the
nearest relevant surface first and then performs a nine-point ground-clearance
test around its trunk. The placement index reads the global scene and all 49
legacy streaming cell scenes, including their supplemental job-location
overlays: 91,956 road/bridge triangles, 47,178 exact building triangles and
149 bounded building fallbacks contribute to 139,529 indexed footprints.
The latest `phase1-job-location-presentation-10b-r2-v4` overlays are included.
This covers the newly generated septic-house locations, strawberry-field rows,
farmyard structures and tent. Road
triangles use a 4.75-metre canopy/trunk clearance,
building triangles use 3.5 metres, bounded building fallbacks use 1.5 metres,
and valid water surfaces use 3.5 metres. Donor under-map and presentation
proxies (`LakeWaterUnder*`, `LakeWaterColor`, lake beds and foliage water
helpers) are explicitly ignored because their projected meshes cover dry land.
This geometry-backed check does not depend on complete runtime collider
coverage and rejects roads, dirt roads, structural building geometry,
foundations, bridges and valid water surfaces before prefab instantiation.

## Replacement and safety policy

- renderers on donor vegetation and `TREEWALL_*` sprite presentation are
  disabled;
- the superseded BetterMSC detailed-forest renderer is disabled;
- superseded internal donor `TREES1_COLL`/`TREES2_COLL`/`TREES3_COLL` and
  BetterMSC detailed-forest `MeshCollider` components are disabled with their
  hidden renderers; the pre-fix 2026-08-08 audit found 17 such invisible
  internal multi-kilometre collision meshes;
- `MAP/MESH/FOLIAGE/TREEWALL_LOW/treewallcoll` remains enabled as the approved
  invisible outer map boundary. The collider audit classifies it separately
  and never treats it as an invisible interior tree obstruction;
- source-package tree colliders are disabled and replaced by one project-owned
  capsule per visible trunk, including boundary-band trees, so any remaining
  tree collision has matching visible geometry and follows cell activation;
- generated trees receive shadows, use light/reflection probes and retain
  cross-faded LOD groups; each mass Engelmann spruce uses its distinct source
  model only above the `0.32` screen-height threshold, then its own retained
  foliage with a lightweight trunk below `0.045` and a tiered Engelmann
  cross-billboard below `0.0025`. Only the two mesh levels cast shadows. Spruce foliage and
  billboards use the project `MSC/HDRP/Spruce Wind` vertex shader with object
  motion vectors; its HDRP Lit forward pass samples the real foliage color,
  alpha and normal, receives direct/indirect light and shadows, and has zero
  emission so needles darken correctly at night. A tolerant depth comparison
  keeps animated needles from collapsing into a black depth-only silhouette;
  bark, trunk colliders and tree roots remain stationary;
- Engelmann grounding is stored inside each prefab's `GroundedVisuals` root.
  Variant-specific burial fractions `1=32%`, `2=20%`, `5=19%`, `8=15%` and
  `9=14%` align all five different root collars to the same terrain plane. The
  prefab root and project-owned gameplay trunk collider stay anchored to the
  measured ground height;
- Engelmann foliage-card correction is topology-bounded: generation aborts if
  a foliage component contains more than eight connected vertices, preventing
  an importer/topology change from accidentally shrinking an entire crown;
- one `Phase1SpruceWindController` reads the strongest active directional
  Unity `WindZone` (including the zone owned by Enviro), publishes shared GPU
  wind globals with frame-rate-independent exponential smoothing and does not
  mutate Enviro or add a second weather/wind owner. Wind strength changes only
  wave amplitude, never the absolute-time phase, preventing strong-wind snaps;
- the 112 generated 512 m forest groups remain serialized in the global legacy
  scene for compatibility, but `Phase1ForestRuntimeCuller` activates only cells
  within 620 m of the game camera, with 160 m hysteresis;
- generated grass data is retained but its rejected renderer components are
  disabled by the generator itself, so rebuilding the forest cannot
  accidentally reactivate the rejected carpet;
- the missing NatureManufacture indirect include is restored by the
  deterministic source importer;
- the generator is deterministic and replaces its previous generated root on
  every run.

The generated root is:

`PHASE1_VEGETATION_REPLACEMENT_LICENSED_THIRD_PARTY`

Historical active-presentation note (recorded 2026-07-25): the 65 generated grass-field
renderers are temporarily disabled by user decision while the world uses the
restored donor ground meshes. Generated trees, shrubs and trunk colliders remain
active. The grass data and assets are retained for a later corrected pass.

The source and generator paths are:

- `Tools/Vegetation/Import-Phase1VegetationSources.ps1`
- `Assets/Game/Editor/WorldBaseline/Phase1VegetationAssetBuilder.cs`
- `Assets/Game/Editor/WorldBaseline/Phase1ForestRemediationBuilder.cs`
- `Assets/Game/Editor/WorldBaseline/EngelmannGroundingAuditRenderer.cs`
- `Assets/Game/Editor/WorldBaseline/Phase1TreeColliderAudit.cs`

Unity menu command:

`Tools > MSC Remake > World Baseline > Apply Phase 1 Vegetation Replacement`

## Executed validation

Unity 6000.3.11f1 cell-aware batch execution saved the regenerated scene and
emitted its success marker on 2026-08-08.

Validated by the generator:

- all required source prefabs load;
- every selected tree prefab contains an `LODGroup`;
- generated tree, shrub, grass-cluster, grass-field and trunk-collider counts
  match the plan;
- every grass field has serialized runtime data, remains disabled, and its
  instance total matches the generation plan;
- all five selected mass-spruce prefabs reference alpha-clipped, wind-enabled
  foliage materials plus the shared billboard material; source material names
  are normalized before slot mapping;
- variants `2` and `8` reference only their project-generated card-adjusted
  foliage meshes in LOD0/LOD1, while their LOD envelopes and triangle counts
  remain within the existing bounds;
- source variant `3` and all superseded mass-forest Norway variants are absent
  from the generated world scene;
- every generated tree has one enabled trunk collider, a renderable LOD and
  the minimum physical radius and height;
- every generated tree root is outside the indexed road, building and water
  footprints gathered from the global scene, all 49 streaming cells and their
  supplemental job-location overlays;
- exactly one spruce-wind controller exists on the generated forest root, and
  all selected spruce foliage/billboard materials use the wind shader while
  bark remains on stationary HDRP/Lit;
- representative small, medium and large Engelmann variants, plus the standard
  Norway support prefab, produce visible needle coverage in HDRP render-texture
  regression tests at fixed EV14 exposure;
- no legacy sprite tree-wall renderer remains enabled;
- no enabled internal legacy vegetation collider remains without matching
  visible geometry; the one approved invisible outer map boundary remains
  enabled and is classified separately. The CSV audit is written to
  `Artifacts/VegetationImport/TreeColliderAudit.csv`;
- the project compiles without new C# errors or shader errors during the run.

Result:

`PHASE1_VEGETATION_REPLACEMENT_APPLY_OK`

Focused tree-placement policy tests: 57/57 passed. They cover the requested
1.25–1.5 reduction factor, triangle interior/edge clearance, exact road versus
road-sign classification, structural versus generic location content, all five
supplemental job-location classifications, the 60% spruce policy, the spruce
wind-shader contract, the five-model selection, near/far triangle budgets,
rendered needle visibility for LOD0/LOD1/LOD2, absence of old spruce materials
in Engelmann far LODs, all five grounding profiles, ground-anchored colliders
and the exclusion of donor water proxies from tree-blocking geometry.
The added cases render variants `2` and `8`, verify their `0.36`/`0.44`
local-card policy and require both near foliage LODs to reference the exact
generated adjusted meshes rather than the oversized source cards.

## Manual validation

Open `Assets/Game/Bootstrap/Bootstrap.unity`, enter Play Mode and inspect
at least the home, Teimo, town, airfield, all five septic-house sites, the
farmer's yard, strawberry field, tent and the former enclosed/tree-wall
regions.

Confirm:

1. former flat tree walls are no longer visible;
2. the horizon is filled by upright trees rather than vertical aggregate
   planes;
3. forested areas remain visually coherent without the former blanket density,
   with the intended 60/40 spruce-to-other-tree mix;
4. two irregular tree rows read as forest walls beside long paved and dirt-road
   sections without occupying the road surface;
5. no tree occupies a paved road, dirt road, building, septic installation,
   strawberry row, tent, farm structure, foundation, bridge deck, airport
   surface or water;
6. no large vegetation patch is floating or buried;
7. walking into an accessible tree stops the player at a trunk-sized collider;
8. no player or NPC collision occurs on disabled internal legacy tree or
   BetterMSC forest meshes; the invisible outer map boundary still blocks
   leaving the playable map, while boundary-tree collision remains visible at
   each trunk;
9. LOD transitions are acceptable while walking, teleporting and driving;
10. active-cell transitions do not create missing forests;
11. open voids contain only occasional isolated trees, not a new dense patch;
12. FPS and memory remain acceptable after visiting several regions;
13. spruce crowns and billboard LODs respond coherently to Enviro wind while
    trunks, roots and collision remain visually and physically stable.
14. the five Engelmann shapes are visibly mixed and their near-to-foliage-card
    LOD transitions are acceptable while driving.
15. variants `2` and `8` no longer show metre-scale individual branch tufts at
    close range, while crown width and branch placement still match the other
    Engelmann variants.

## Known limitations

- the 112 vegetation groups are organised spatially but currently live in the
  global legacy scene. Runtime cell activation removes the former all-cells
  rendering/physics cost, but scene deserialization and serialized hierarchy
  memory remain global; true additive per-cell vegetation streaming remains a
  future optimisation;
- the 65 grass data assets total about 17.3 MiB and share one approximately
  32 KiB patch mesh, but their renderers are currently disabled. Manual runtime
  FPS, activation-hitch, memory and traversal profiling is still required
  before the tree density can be accepted;
- the pass does not claim every donor terrain void is reconstructed;
- the 18 unresolved external GUIDs in the copied dependency inventory are
  retained as an audit warning, although all runtime-selected prefabs, LODs,
  materials and shaders loaded successfully in Unity;
- final wind-amplitude tuning, authored biome masks, terrain-normal alignment,
  billboard transition tuning and per-species authored trunk collision belong
  to later production vegetation work;
- the five Engelmann near meshes range from approximately 96,000 to 280,000
  triangles, so their `0.32` near-LOD threshold still requires an in-game GPU
  profile before final acceptance;
- manual visual and performance acceptance is still required.
