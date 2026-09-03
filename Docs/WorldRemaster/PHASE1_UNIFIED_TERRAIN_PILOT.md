# Phase 1 unified editable terrain pilot

Date: 2026-07-24

## Purpose and classification

This is a reversible private Phase 1 terrain-authoring pilot derived from the
sanitized donor world baseline. It is classified `TemporaryDirectImport` and is
not a Phase 2 production terrain replacement.

## Current active disposition

On 2026-07-25 the pilot was removed from the active global scene after manual
fidelity review. The source donor ground-mesh renderers are active again, while
all generated Phase 1 grass-field renderers remain disabled. Generated trees,
shrubs, their colliders and user-authored tree removals are preserved.

The ignored generated TerrainData assets remain available for further tool
development, but they are not referenced by the active scene. Rebuilding the
pilot is an explicit opt-in action.

Unity Terrain cannot represent the entire donor map at one useful heightmap
resolution as a single object. The pilot therefore uses one shared global
heightfield cut into automatically connected 1,024-metre Terrain tiles. Shared
border samples come from the same global array, so the grid behaves as one
continuous editable surface.

## Generated result

- root: `PHASE1_UNIFIED_EDITABLE_TERRAIN_PILOT`;
- 99 connected tiles in an 11 x 9 grid;
- tile size: 1,024 x 1,024 metres;
- heightmap resolution: 1,025, equivalent to one sample per metre;
- detail resolution: 1,024 with 32-pixel patches;
- covered source samples: 46,709,000;
- enclosed missing samples filled: 1,233,587;
- exterior samples retained as Terrain holes: 55,886,918;
- world domain: `(-5120, -48, -5120)` to `(6144, 112, 4096)`;
- 198,548 near-ground road constraints applied;
- 16,247 elevated or otherwise unsafe road constraints rejected;
- three project-owned matte HDRP TerrainLayers: forest ground, meadow and soil;
- generated local payload: approximately 308 MiB.

The generator samples only reviewed donor ground meshes and the BetterMSC
missing-terrain fill as primary height sources. Near-vertical triangles are
rejected before projection into the heightfield. Road-bed meshes are applied
after the base surface has been reconstructed, and only where their height is
within 2.5 metres of the ground. Elevated bridge, airport and railway geometry
therefore cannot become terrain walls or spikes. Accepted road beds receive
four centimetres of clearance above the generated terrain.

The generator also derives a coarse three-class surface map from the source
ground identity. Its generated textures are deliberately matte: metallic and
smoothness are zero and diffuse alpha is not a smoothness source. Donor road,
bridge, building, water, railway and prop renderers remain separate meshes.

## Safety and compatibility

- original ground renderers are disabled, not deleted;
- the superseded procedural Phase 1 grass fields are disabled for this pilot;
- existing road, building, bridge, water, prop and vegetation presentation
  remains enabled;
- TerrainColliders are deliberately disabled;
- established legacy mesh colliders remain collision authority;
- gameplay anchors, stable IDs, streaming manifests and saves are unchanged;
- the generated TerrainData and modified global scene stay under the ignored
  private runtime baseline.

The pilot cannot represent caves, overhangs or multiple vertical surfaces at
the same X/Z coordinate. Those cases must remain separate meshes.

## Tooling

Project-owned generator:

`Assets/Game/Editor/WorldBaseline/Phase1UnifiedTerrainPilotBuilder.cs`

Unity menu commands:

- `Tools > MSC Remake > World Baseline > Build Phase 1 Unified Editable Terrain Pilot`
- `Tools > MSC Remake > World Baseline > Validate Phase 1 Unified Editable Terrain Pilot`
- `Tools > MSC Remake > World Baseline > Remove Phase 1 Unified Editable Terrain Pilot`
- `Tools > MSC Remake > World Baseline > Return To Legacy Meshes Without Generated Grass`

`Remove` restores the hidden source-ground renderers and the generated
procedural grass fields. It does not delete or alter donor source data.

`Return To Legacy Meshes Without Generated Grass` is the current selected
profile: it removes the pilot root, restores source ground renderers and keeps
all `Phase1GrassFieldRenderer` components disabled.

## Manual grass authoring

Open:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity`

Then select an `Editable Terrain [x,z]` child below the pilot root and use:

`Inspector > Paint Terrain > Paint Details > Edit Details`

Add a reviewed low-grass detail texture or HDRP-compatible detail mesh and paint
the desired areas. Connected tiles share exact height borders. Terrain detail
data is stored in the generated TerrainData assets and remains local.

The current surface colours are authoring placeholders, not a final recreation
of the donor basemap. A later bounded tool may bake the donor ground UV/material
appearance into georeferenced masks, then use those masks to blend project-owned
tiling forest-ground, meadow, field and soil layers. This does not require
changing the generated heightfield.

Do not rerun `Build` after manual sculpting, texture painting or detail painting:
the deterministic build command recreates all TerrainData assets. Use
`Validate` for a non-destructive check.

## Executed validation

Unity `6000.3.11f1` batch generation completed successfully.

The separate validation pass reported:

```text
PHASE1_UNIFIED_TERRAIN_PILOT_VALIDATION_PASS
tiles=99
seams=178
maxNormalizedSeamError=0
legacyGroundVisible=0
proceduralGrassEnabled=0
terrainColliders=disabled
```

The remaining gate is manual Scene/Game view inspection of:

- dirt-road clearance near the home;
- the airport;
- the Loppe bridge;
- the railway;
- the outer-map road;
- water boundaries;
- matte material response and terrain-painting usability.

The current active mesh profile was separately applied and validated as:

```text
PHASE1_LEGACY_MESHES_WITHOUT_GRASS_OK
groundRenderers=7
grassFieldsDisabled=65
terrainPilot=removed
trees=preserved
```
