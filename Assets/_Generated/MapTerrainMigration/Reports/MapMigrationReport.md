# Map terrain migration validation

Status: **PASS**

- Source: `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity`
- Output: `Assets/Scenes/Generated/3Buildings_TerrainMigration.unity`
- Source choice: 3Buildings/2BasicMap are absent. The frozen, sanitized World_Global_Legacy scene is the accepted canonical donor-map baseline; sibling cells are included for full-instance export. Later project-generated vegetation/grass/lighting overlays without TemporaryDirectImport provenance are intentionally outside the source root.
- Source SHA-256 unchanged: `True`
- Inventory/export instances: 2606/2606
- Ground candidates: 7
- Road meshes: 15
- Residual/Ambiguous: 123/63
- Terrain: 72 tiles (9 x 8), tile 1024 m, resolution 513, spacing 2 m
- Source coverage: 99,498%
- Height error mean/max: 0,136557817 / 3,00199771 m
- Maximum height-error sample: signed 3,00199771 m at (7.80069733, -6.71643066, -2002.82178) (record 57504a4ac4413e6b)
- Maximum normalized seam error: 0
- Road protrusion/edge gap: 0 / 2,20535517 m
- Maximum road-gap sample: (1281.90918, 5.05951929, -1978.54968) (record 73b438e204df5b5d)
- Elevated road triangles retained as structure: 30
- Ground triangles preserved as residual meshes: 2528
- Road preservation failures: 0
- Missing scripts: 0
- Committed: True; ground renderers replaced: 7
- Material transfer: Exact transfer requires one shared base-map texture; found 3. A valid matte HDRP-compatible TerrainLayer is used; source meshes remain available as reference.
- Smoothing: mean 0.00111758 m; median 0 m; p95 0.00432539 m; max 0.4187202 m; limit hits 0

## Errors

- None.

## Warnings and manual review

- Official Unity FBX Exporter is not installed; OBJ/MTL is the actual export format.
- 30 vertically overlapping road triangles are retained as elevated RoadStructure and excluded from road-bed clearance measurements.
- Maximum measured near-ground road gap is 2,20535517 m at (1281.90918, 5.05951929, -1978.54968) (record 73b438e204df5b5d); manual shoulder review is required.
- Bridge decks and other RoadStructure meshes remain meshes and are excluded from road height constraints.
- Visual review remains required for road shoulders, bridge clearance, water boundaries and material alignment.
