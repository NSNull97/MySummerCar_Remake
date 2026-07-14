# Production Art Guide

## Goal

Create a modern HDRP presentation while preserving the donor game's identity, proportions, gameplay readability, and slightly grimy rural character.

## Models

Use donor meshes for measurement and comparison. New production models should have:

- clean silhouette at gameplay distance;
- physically believable bevels;
- geometry where it affects highlights or interaction;
- baked detail where geometry would be wasteful;
- consistent scale and axes;
- verified pivots and mount points;
- LODs;
- dedicated collision proxies;
- readable naming and prefab structure.

Do not add detail that prevents parts from fitting or changes gameplay dimensions.

## Texture sets

Author new PBR data:

- base color without baked lighting;
- normal;
- metallic;
- smoothness/roughness;
- AO;
- optional height;
- detail normals and masks;
- decals for labels, grime, oil, scratches, and rust.

Use channel packing appropriate for HDRP. Document the packing convention.

## Materials

Create reusable master materials for:

- painted metal;
- bare metal;
- rusted metal;
- rubber;
- plastic;
- glass;
- wood;
- concrete;
- asphalt;
- gravel;
- soil;
- fabric;
- wet variants through global parameters and masks.

Avoid one unique shader graph per object. Build a controlled material library.

## Vehicle parts

Preserve:

- mounting surfaces;
- bolt holes;
- shaft and hinge axes;
- fluid connection points;
- electrical terminals;
- collision volume;
- mass distribution reference.

Add production detail such as seams, stamping, welds, fastener heads, hose shapes, and material transitions without breaking assembly alignment.

## Environment

### Terrain

Use donor height/layout data as a base. Rebuild surface layers, microvariation, drainage, and vegetation masks.

### Roads

Use splines and layered materials. Include shoulders, ditches, edge breakup, puddle masks, gravel scatter, tire marks, and terrain blending.

### Vegetation

Use species and density appropriate to the setting. Plan LOD, billboards/impostors where needed, wind response, and sensible shadow distances.

### Buildings

Rebuild as modular production assets where practical. Preserve dimensions and recognizable layout. Use interior/exterior material separation and baked/real-time lighting decisions appropriate to scene scale.

## Lighting

Target natural low-angle northern summer lighting:

- physically based sky;
- directional sun;
- volumetric fog;
- believable exposure;
- interior adaptation;
- restrained color grading;
- contact shadows only where they materially help.

## Post-processing

Use:

- TAA or current HDRP anti-aliasing appropriate to the target;
- tone mapping;
- subtle bloom;
- restrained vignette if needed;
- sharpening only when justified.

Avoid permanent depth of field, strong chromatic aberration, crushed blacks, and oversaturated orange-teal soup.

## Art validation

Every reconstructed asset should pass:

- scale comparison;
- pivot comparison;
- mount-point comparison;
- material sanity under neutral light;
- LOD transition check;
- collision check;
- reference-dependency check;
- provenance update.

For the first controlled reconstruction, record the reference and production pivot in meters, at least one vehicle mount-point pair, the dimensional tolerance, authored texture assets, and production prefab in a `ReauthoredAssetProvenance` sidecar. Passing this metadata check does not replace visual review or make donor geometry production-ready.

## Milestone 3 material and environment baseline

Milestone 3 creates a reproducible prototype library under `Assets/Game/Presentation/Materials/GaragePrototype`. Each material uses HDRP/Lit and newly generated BaseColor, tangent-space Normal and Mask maps. The Mask map convention is R Metallic, G Ambient Occlusion, B Detail Mask and A Smoothness.

The garage production scene uses only project-authored meshes, materials and textures. The reviewed donor roof remains available only in the ignored comparison scene. The roof bounds and zero pivot are validated within 0.005 m after mapping donor local `(X,Y,Z)` to production y-up `(X,Z,Y)`; the wall openings, furniture, road and terrain remain explicitly reconstructed prototype choices.

These assets are `PrototypeReady`, not final `ProductionReady` art. Required later replacements include authored high-resolution texture/bake sets, production bevel/topology work, vegetation wind/impostors, road splines/ditches/decals and interior-lighting refinement.
