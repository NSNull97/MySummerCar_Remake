# World, Streaming, Roads, and Weather

## World identity

Preserve:

- important location relationships;
- recognizable route topology;
- travel times and broad scale;
- terrain silhouette;
- shoreline and major elevation changes;
- gameplay-critical spawn and interaction zones.

Rebuild production content.

## Streaming

Plan additive scenes or world cells:

- static geometry;
- dynamic persistent entities;
- traffic/NPC simulation later;
- loading boundaries hidden by terrain and road layout;
- stable state independent of loaded scene.

A cell unload must not destroy persistent logical state.

## Roads

Preferred workflow:

1. Extract donor road centerline/width/elevation.
2. Rebuild with splines.
3. Generate road, shoulder, and ditch geometry.
4. Blend into terrain.
5. Add surface definitions and collision.
6. Add decals, wetness, puddles, gravel scatter, and vegetation exclusion.
7. Verify route scale and driveability.

## Terrain

Use layered materials and masks for:

- soil;
- grass;
- gravel;
- mud;
- rock;
- wetness;
- path wear.

Avoid painting one enormous unique texture when reusable layers and masks are more maintainable.

## Time and weather simulation

Separate weather state from HDRP presentation.

Simulation values:

- time of day;
- cloud coverage;
- precipitation;
- fog density;
- wind;
- wetness target;
- puddle accumulation;
- temperature later.

Presentation applies these values to:

- sky;
- sun;
- volumes;
- VFX;
- materials;
- vegetation;
- audio parameters.

## Wetness

Use global/material parameters and local masks. Wetness should:

- increase smoothness appropriately;
- darken porous surfaces carefully;
- create puddles only where plausible;
- affect tire audio and friction only through explicit simulation rules;
- dry over time.

## Weather prototype

First weather milestone needs:

- clear preset;
- overcast transition;
- rain transition;
- fog response;
- wet-road material response;
- simple wind;
- save/load of weather state;
- performance capture.

## Milestone 3 world-art baseline

The first production-only environment slice is `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`. It contains the rebuilt garage, a 180 m project-authored gravel road/shoulder, a mesh terrain patch, basic LOD vegetation, one reflection probe and neutral/late-day HDRP presentation presets.

This is not yet a streaming cell and contains no weather simulation. The road curve and terrain elevation are not claimed as donor-exact because donor centerline/height data were not extracted for this milestone. Milestone 7 must replace the procedural road with spline-owned data, add ditches/terrain blend masks/wetness, and separate weather state from these presentation profiles.
