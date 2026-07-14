# Project Definition

## Working title

My Summer Car Remake Game.

## Product statement

A private modern recreation of the original My Summer Car experience, preserving its mechanical identity, scale, progression logic, awkward physical comedy, and Finnish rural atmosphere while replacing the technical foundation with Unity 6 HDRP, modern rendering, modular simulation, improved animation, and a Wwise-ready audio pipeline.

## Core pillars

### Mechanical intimacy

The player should understand the car through physical assembly, fasteners, fluids, electricity, tuning, wear, failure, and sound. The vehicle is not an inventory icon. It is a graph of parts and relationships.

### Physical interaction

Items are carried, placed, dropped, mounted, tightened, spilled, damaged, and lost in the grass because life is beautiful and slightly cursed.

### Recognizable world

Preserve the identity, routes, important distances, key locations, and functional geography of the donor world. Rebuild terrain, roads, buildings, vegetation, and materials for HDRP.

### Atmospheric realism

Use warm low-angle sunlight, long summer evenings, mist, rain, wet roads, changing clouds, wind, insects, room tone, distant vehicles, and restrained post-processing.

### Modern maintainability

The codebase must support incremental development, testable simulation, stable saves, later additional vehicles, and eventually optional multiplayer research without requiring a rewrite of every system.

## Private-project boundary

No public release is planned. The repository still keeps donor content and decompiled material outside Git. This prevents accidental redistribution and keeps the project technically clean.

## Initial vertical slice

The first serious playable target contains:

- the garage and roughly 500 meters of nearby road;
- first-person movement and interaction;
- carrying and placing items;
- a vehicle shell;
- 12–20 representative installable parts;
- fasteners and at least two tool types;
- simplified engine, clutch, gearbox, differential, wheels, and brakes;
- engine start, stall, and a short drive;
- day/night, clear weather, and rain;
- wet-road presentation;
- versioned save/load;
- basic engine and environment audio;
- performance and validation reports.

## Visual target

- realistic, but recognizably My Summer Car;
- new production geometry and texture sets;
- correct material response rather than maximum shininess;
- dense but optimized vegetation;
- believable gravel, dirt, wood, rust, paint, rubber, glass, and fabric;
- volumetric atmosphere;
- natural exposure and tone mapping;
- no permanent cinematic depth of field;
- no radioactive bloom festival.

## Explicit non-goals for the first vertical slice

- full map;
- all quests and NPCs;
- all vehicles;
- multiplayer;
- public mod support;
- final production audio;
- photoreal character faces;
- exhaustive destructibility;
- ray tracing as a requirement.

## Success criteria

The project is successful at the vertical-slice stage when:

1. A new player can enter the garage, pick up a part, install it, tighten it, save, reload, and find the state preserved.
2. The prototype vehicle can start, stall, move, brake, and react to basic damage or incorrect assembly.
3. Donor reference assets can be deleted from the Unity project without breaking production prefabs.
4. The garage/road scene looks and sounds materially more modern than the donor while remaining recognizable.
5. The build holds the baseline performance target and has no compiler errors.
