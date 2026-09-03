# Engelmann spruce — provenance

Source: user-supplied licensed package
`cl08-picea-engelmannii-glauca-engelmann-spruce`.

Classification: licensed third-party Phase 1 presentation content. This is not
donor-game content and is not newly authored project art. The original package
license and source terms continue to apply.

## Source payload

| File | SHA-256 |
| --- | --- |
| `source/primary uploads.zip` | `8204A399B76DC8E814D5D7DFCD683D2A3A1EB5ECF8DA3580F334D72377560A72` |
| `picea-engelmanni-glauca.fbx` | `BB9A11A904E2DB5EA09E9A48539212179FB24C08C261E79C60C1D8B74C1AD22A` |

The original archive remains unchanged in the user's download directory. The
Unity source copy contains one FBX and 28 supplied texture maps. Project-owned
wrapper prefabs, HDRP materials and combined colour/opacity textures are
generated under
`Assets/Game/Presentation/Vegetation/Generated/Phase1`.

## Runtime selection

The FBX contains six distinct source trees: `1`, `2`, `3`, `5`, `8` and `9`.
Runtime uses `1`, `2`, `5`, `8` and `9`. Variant `3` is retained as source but
excluded from mass placement because its near mesh is approximately 798,000
triangles.

Each active wrapper uses its distinct source geometry in the near LOD. LOD1
retains only that variant's supplied foliage and adds a lightweight
project-generated trunk. LOD2 uses a project-generated 42-triangle tiered
cross-billboard with an Engelmann foliage material. No active Engelmann far LOD
references the older Norway spruce assets. Foliage uses the project
`MSC/HDRP/Spruce Wind` shader; trunks remain stationary.

Because the five supplied models have materially different root-to-height
ratios, each wrapper stores a project-owned `GroundedVisuals` offset. The
reviewed burial fractions are `1=32%`, `2=20%`, `5=19%`, `8=15%` and `9=14%`.
The wrapper pivot and gameplay collider remain at terrain height while the
radial roots are hidden below it. The ignored generated comparison render is
`Artifacts/VegetationImport/EngelmannGroundingAudit.png`.
