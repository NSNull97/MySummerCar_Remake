# Phase 1 full-map unified base terrain scene

Date: 2026-08-04

## Outcome and scope

`Assets/Scenes/Generated/FullMap_UnifiedBaseTerrain.unity` is a separate,
generated authoring scene containing the canonical global legacy world, all 49
canonical cell scenes, and the v004 unified ground mesh.

Only the base ground is unified. Canonical `Road`, `Asphalt`, `Pavement`,
`DirtRoad` and `Gravel` meshes remain separate, visible Unity objects with their
original sanitized materials and colliders. This preserves the complete outer
ring highway and avoids replacing donor road presentation with a flat
placeholder submesh. Buildings, vegetation, props, water, airport, railway and
road markings also remain separate.

The output is a private Phase 1 `TemporaryDirectImport`, not Phase 2 production
art.

## Main terrain v004

- one ground mesh, one renderer and one material submesh;
- 3,563,337 imported Unity vertices;
- 7,119,112 triangles;
- 4 m grid spacing;
- bounds centre `(276.00, 28.19, -644.00)` metres;
- bounds size `(8008.00, 105.07, 7112.00)` metres;
- no enclosed holes in the complete heightfield grid;
- outer boundary remains open by design;
- no MeshCollider on the presentation mesh.

The Unity-ready FBX is generated from
`BlenderWork/MapEditing/MSC_UnifiedBaseTerrain_4m_v004.blend`. Export explicitly
compensates for FBX handedness so Unity X is not mirrored.

## Road, shoulder and railway treatment

The heightfield is constrained by asphalt, road, pavement, gravel, dirt road,
airport, railway and railway-tunnel evidence. It sits 0.12 m below accepted
road triangles so ground cannot cover asphalt or eat road markings. The
executed build accepted 73,993 near-ground triangles, rejected 8,426 elevated
bridge/stacked triangles and 158 near-vertical triangles, and measured zero
terrain protrusion at checked accepted vertices and centroids.

The hard donor `Roadside` mesh is disabled. A 12 m ground blend forms the
shoulder. Smoothing is slope-protected and capped at 0.25 m displacement.
Ground may be raised only inside a 3 m road-support margin; donor depressions
beyond it may only be preserved or lowered, preventing broad shoulder blending
from filling the Peräjärvi-exit ravines.

The five canonical road renderers and all five corresponding source colliders
are explicitly re-enabled during every build/refresh. Validation checks every
required path independently:

- `MAP/MESH/TERRAIN_OBJ/Road` — outer ring highway;
- `MAP/MESH/TERRAIN_OBJ/Asphalt`;
- `MAP/MESH/TERRAIN_OBJ/Pavement`;
- `MAP/MESH/TERRAIN_OBJ/DirtRoad`;
- `MAP/MESH/TERRAIN_OBJ/Gravel`;
- `MAP/MESH/AIRPORT`;
- `MAP/MESH/RAILROAD`;
- `MAP/MESH/RAILROAD_TUNNEL`;
- `MAP/MESH/road_lines`.

## Collision and compatibility

The validated 72-tile, 2 m Terrain migration grid remains under
`COLLISION_ONLY_TERRAIN_TILES_2M`. Terrain rendering is disabled and all 72
TerrainColliders remain enabled. The five canonical road colliders are also
enabled for vehicle and NPC contact.

The scene contains 3,846 canonical donor entities, including 2,607 entities
with sanitized renderers. Eleven superseded ground/residual entities are hidden
and have collision disabled; no road surface is included in that replacement
set. Gameplay IDs, route data, anchors, source scenes and hierarchy metadata are
unchanged. The canonical source scene SHA-256 remains
`a4b929ee74b95778f44599e26b081a70e20edcd494f929de7f624c2c632f2322`.

The scene is not added to Bootstrap, the active streaming profile or build
settings. Runtime integration still requires explicit visual acceptance.

## Executed validation

Blender `5.2.0 LTS` generated the v004 FBX with SHA-256
`281ef5aaaaf2565804d26bbf618c9d33035374a56b5c55d6c36beae8a7fb4428`.

Unity `6000.3.11f1` completed a focused refresh and a separate validation pass:

```text
FULL_MAP_UNIFIED_TERRAIN_SCENE_VALIDATION_PASS
donorEntities=3846
cells=49
vertices=3563337
triangles=7119112
submeshes=1
collisionTerrains=72
roadSurfaceRenderers=9
canonicalRoadColliders=5/5 enabled
legacyGroundVisible=0
missingScripts=0
```

Machine-readable report:
`Reports/WorldBaseline/FullMapUnifiedTerrainSceneValidation.json`.

## Remaining manual acceptance

Open the generated scene and inspect the complete ring highway, its markings,
both bridge approaches, dirt-road edges, railway crossings and the ravines near
the Peräjärvi exit. Automated checks establish object activation, topology,
orientation, collider retention and non-covering clearance; final visual
acceptance remains manual. The approximately 740 MB scene is a review/authoring
scene, not final runtime streaming content.
