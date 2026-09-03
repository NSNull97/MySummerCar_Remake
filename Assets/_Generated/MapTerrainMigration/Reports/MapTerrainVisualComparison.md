# Source mesh / Unity Terrain visual comparison

Panels in every PNG: **source upper surface | generated Terrain + residual ground | absolute difference**.
Red cross: maximum ground-height error. Magenta cross: maximum road-edge gap.

- Overview: `Assets/_Generated/MapTerrainMigration/Reports/MapTerrainComparison_Overview.png` (difference capped at 5 m)
- Height outlier detail: `Assets/_Generated/MapTerrainMigration/Reports/MapTerrainComparison_HeightOutlier.png`
- Road-gap detail: `Assets/_Generated/MapTerrainMigration/Reports/MapTerrainComparison_RoadGap.png`
- Maximum ground error: 3.00199771 m, signed 3.00199771 m, record `57504a4ac4413e6b`, point `(7.80, -6.72, -2002.82)`
- Maximum road gap: 2.20535517 m, record `73b438e204df5b5d`, point `(1281.91, 5.06, -1978.55)`

The generated panel uses Terrain where possible and the preserved residual-ground mesh where it is closer to the source surface. Buildings, vegetation, roads and bridges remain unchanged and are not rasterized into the elevation panels.
