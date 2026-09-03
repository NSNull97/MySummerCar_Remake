# Base-map mesh to Unity Terrain migration

Status: implemented and batch-validated on 2026-08-03. This is a private Phase 1
`TemporaryDirectImport` migration aid, not production-remaster art and not a
Phase 2 gate.

## Audited source

The requested `Assets/Scenes/3Buildings.unity` and `Assets/Scenes/2BasicMap.unity`
do not exist in the current project. The repository's canonical, already
sanitized donor map is:

```text
Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity
```

Its migration root is `TEMPORARY_DIRECT_IMPORT_ENTITIES`. The scanner also opens
the 50 canonical sibling legacy cell scenes and accepts only renderers carrying
`DonorWorldBaselineEntityMetadata` or `DonorWorldSupplementalEntityMetadata`.
Project-owned overlays without donor provenance are therefore not pulled into
the source set.

The source scan found 2,606 renderer instances:

| Category | Count |
|---|---:|
| GroundCandidate | 7 |
| RoadAsphalt | 4 |
| RoadDirtOrGravel | 2 |
| RoadStructure | 9 |
| Water | 75 |
| Building | 1,601 |
| Vegetation | 18 |
| Utility | 16 |
| Prop | 688 |
| Technical | 0 |
| ResidualUnsupported | 123 |
| Ambiguous | 63 |

The source-scene SHA-256 before and after the executed pipeline is identical:

```text
a4b929ee74b95778f44599e26b081a70e20edcd494f929de7f624c2c632f2322
```

## Tool entry points

Editor window:

```text
Tools/MSC Remake/Map Migration/Open Migration Window
```

Batch entry point:

```text
MSCMapMigration.MapMigrationBatch.RunFullPipeline
```

The window exposes scan, Blender export, preview, validation, commit, full
pipeline, export-folder, generated-scene and side-by-side QA actions. Settings
are stored in:

```text
Assets/_Generated/MapTerrainMigration/Settings/MapTerrainMigrationSettings.asset
```

Generated binary assets, the generated scene and the external Blender payload
are ignored by Git. Project-owned code, settings, reports and documentation
remain auditable.

## Blender interchange package

The Unity FBX Exporter is not installed, so the actual interchange format is
OBJ/MTL. No dependency was silently added. The completed package is:

```text
BlenderWork/BaseMap_SourceExport/20260803_162611_222Z/
```

It contains 4,587 files (929,535,345 bytes), including 2,168 OBJ files. The
2,606 instances reference 2,165 deduplicated per-object geometry files and keep
world matrices in the manifest. Aggregate scene exports are:

```text
Scene/BaseMap_All.obj   294,323,391 bytes
Scene/Ground_All.obj     17,764,523 bytes
Scene/Roads_All.obj      11,612,077 bytes
```

`Metadata/manifest.json`, inventory, material metadata, copied readable
textures, MTL files, a Blender import script and README make the export
repeatable. Exporter version 1.2 reuses an existing package only when its source
fingerprint and schema match.

## Terrain conversion

The generated result is written to a separate scene:

```text
Assets/Scenes/Generated/3Buildings_TerrainMigration.unity
```

The conversion creates a 9 x 8 grid (72 tiles), each 1,024 m square with a 513
heightmap resolution and 2 m source sampling. Coverage and fill statistics from
the executed run are:

```text
covered samples:       11,672,921
interior filled:           58,870
exterior holes:         6,901,769
source coverage:          0.994982
```

Height is sampled from transformed source triangles with barycentric
interpolation. Interior holes receive a bounded deterministic fill; exterior
holes remain Terrain holes. Two smoothing iterations at strength 0.32 operate
on the combined global grid, are capped to 0.75 m displacement, and therefore
cannot introduce tile-border discontinuities. Reciprocal neighbors are stored
and reapplied by `MapMigrationTerrainNeighborConnector` after scene load.

Executed smoothing displacement was 0.001118 m mean, 0 m median, 0.004325 m at
p95 and 0.418720 m maximum, with zero samples reaching the 0.75 m cap.

Unity Terrain cannot reproduce vertical, folded or multiply-valued ground.
The builder therefore preserves 2,528 such source triangles in five generated
residual mesh assets. The original seven base-ground renderers are disabled
only in the generated scene and recorded by source markers; buildings, roads,
bridges, water, vegetation, props, transforms, materials and colliders remain
unchanged.

## Roads

Road meshes are not baked into Terrain and are not replaced. Asphalt and
dirt/gravel triangles constrain the heightfield below the road with 0.075 m
clearance and a 4 m shoulder blend. Four tile corners around constrained samples
are clamped so bilinear Terrain interpolation cannot protrude through a road.

Thirty vertically overlapping triangles are classified as elevated road
structure and excluded from road-bed clearance constraints. This prevents
bridges and stacked road geometry from incorrectly pulling Terrain upward.
Executed validation reports:

```text
maximum Terrain protrusion above road: 0.000000 m
road preservation failures:           0
bridge burial failures:                0
maximum near-ground road-edge gap:     2.205355 m
```

The remaining 2.205 m edge gap is at record `73b438e204df5b5d`, world point
`(1281.909, 5.060, -1978.550)`. It is a manual shoulder-review warning, not a
claim of final road-surface polish.

## Terrain material

Exact UV/material transfer is not possible because the seven source ground
renderers use three different base textures. The tool deliberately creates a
valid matte HDRP-compatible TerrainLayer per tile instead of pretending to have
preserved the source UV mapping. Source materials and textures remain present
on unchanged objects and in the export package. Production Terrain materials,
splat maps, wetness and biome dressing remain Phase 2 work.

## Executed validation

The last full run completed with `MAP_TERRAIN_MIGRATION_PIPELINE_PASS`; an
independent validation rerun completed with `MAP_MIGRATION_VALIDATION_PASS`.

```text
inventory/exported instances: 2,606 / 2,606
Terrain tiles:                72
committed source ground:       7
mean composite height error:   0.136558 m
maximum composite error:       3.001998 m
maximum tile seam error:       0.000000 m
missing scripts:               0
```

“Composite” means Terrain plus preserved residual ground. The 21 focused
EditMode tests pass and cover classification, transforms, barycentric sampling,
negative winding, seam equality, bounded smoothing, road constraints,
multi-level roads, deterministic tile names, neighbor serialization and runtime
marker serialization.

Machine-readable and human-readable evidence is under:

```text
Assets/_Generated/MapTerrainMigration/Reports/
```

The three QA PNGs show source surface, generated Terrain plus residual surface,
and absolute difference. Red marks the maximum ground error; magenta marks the
maximum road-edge gap.

## Manual Unity review

Open `Assets/Scenes/Generated/3Buildings_TerrainMigration.unity` and check:

1. Terrain holes/water boundaries at the large lakes and map perimeter.
2. Road shoulders around `(1281.909, 5.060, -1978.550)`.
3. Residual ground at `(7.801, -6.716, -2002.822)`.
4. Bridge clearance and road collision in representative drive-throughs.
5. TerrainLayer appearance under the accepted Enviro 3/HDRP lighting profile.
6. Cell streaming and origin/coordinate consistency in the active Phase 1 map
   profile before promoting this generated scene into gameplay.

Do not remove the residual meshes or treat the placeholder TerrainLayer as
production art. Do not overwrite the canonical legacy scene from this output.
