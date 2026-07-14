# Safe Donor Inspection Workflow

## Goals

- identify the donor Unity version and file layout;
- inventory assets, scenes, managed assemblies, plugins, and configuration;
- build a system map;
- extract only what is needed into external staging;
- derive behavioral specifications and measurements;
- avoid modifying the original installation.

## Read-only source

Source directory:

```text
D:\SteamLibrary\steamapps\common\My Summer Car
```

All tools must write to:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging
```

or:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference
```

## Phase A — filesystem audit

Record:

- executable names and timestamps;
- Unity data directory;
- `globalgamemanagers`/serialized files;
- shared assets;
- resource files;
- asset bundles;
- managed assemblies;
- native plugins;
- configuration files;
- likely save paths;
- file sizes and relevant hashes.

Do not hash every multi-gigabyte file repeatedly. Cache inventory results.

## Phase B — Unity asset inventory

When AssetRipper is locally available:

- run it non-destructively;
- export into `DonorStaging\raw`;
- preserve an extraction log;
- record tool version;
- do not export directly into the production `Assets` directory;
- do not assume every reconstructed scene or material is trustworthy.

If it is unavailable, document exact manual setup steps. Do not download random binaries without approval.

## Phase C — managed-code inventory

When ILSpy/ilspycmd is locally available:

- inspect managed assemblies;
- generate namespace/class/method inventories;
- export readable reference code only into the external legacy-reference directory;
- do not compile that dump in the new project;
- identify PlayMaker-generated/state-machine-heavy classes;
- locate pure formulas, constants, save structures, and object-name dependencies.

## Phase D — system map

Map donor systems:

- player;
- interaction and carrying;
- vehicle assembly;
- fasteners and tools;
- drivetrain;
- wheels, suspension, brakes;
- fluids and electrical;
- damage and wear;
- time and needs;
- NPCs and traffic;
- world and weather;
- audio and animation;
- UI;
- save/load.

For each system record coupling, source symbols/assets, risk, recommended strategy, and implementation order.

## Phase E — behavioral capture

Capture measurable behavior in the donor game:

- player height, speed, and interaction distance;
- vehicle mass and part masses;
- gear ratios and engine RPM ranges;
- steering lock;
- acceleration and braking distance;
- suspension travel;
- time-of-day duration;
- rain and visibility behavior;
- sound transitions;
- key world distances.

Use `Templates/BEHAVIOR_CAPTURE.md` for each capture session.

## Stop conditions

Stop and report if a tool requires:

- modifying donor files;
- bypassing access controls;
- injecting into the donor executable;
- disabling ownership validation;
- writing output into the donor directory.

Complete all safe audit/documentation work even when one tool is unavailable.
