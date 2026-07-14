/plan

You are the principal Unity engineer, technical director, reverse-engineering analyst, and build engineer for a private donor-assisted recreation of My Summer Car.

Read `AGENTS.md` completely before doing anything. Then read:

- `START_HERE_RU.md`
- `Docs/PROJECT.md`
- `Docs/PORTING_GUIDE.md`
- `Docs/REVERSE_ENGINEERING.md`
- `Docs/ROADMAP.md`
- `Config/DonorPaths.local.json`

## Fixed paths

```text
ORIGINAL_GAME_DIR = D:\SteamLibrary\steamapps\common\My Summer Car
UNITY_PROJECT_DIR = E:\GAYmDev_Studio\MySummerCar_Remake_Game
DONOR_STAGING_DIR = E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging
LEGACY_REFERENCE_DIR = E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference
REFERENCE_MEDIA_DIR = E:\GAYmDev_Studio\MySummerCar_Remake_Game\References
```

## Current objective

Complete **Milestone 0 only: Bootstrap and donor audit**.

Do not begin the Unity runtime foundation, import production assets, recreate gameplay, or install Wwise during this task.

## Non-negotiable rules

- Treat the original installation as read-only.
- Do not patch, inject into, or modify the donor game.
- Do not bypass DRM, Steam checks, encryption, access controls, or ownership validation.
- Do not copy donor binaries or decompiled source into Git.
- Do not export directly into the production `Assets` directory.
- Do not mass-extract without an inventory and destination plan.
- Do not install external tools or packages without explaining the need and receiving approval.
- If AssetRipper or ilspycmd is absent, document exact setup steps and continue every safe part of the audit.
- Distinguish observed facts from assumptions.
- Do not claim Unity or tests ran unless you actually executed them.

## Tasks

### 1. Inspect the repository

- Verify this is the intended project directory.
- Inventory existing files, Unity folders, package manifests, and project version if present.
- Do not recreate or delete an existing Unity project.
- Identify conflicts with `AGENTS.md`.

### 2. Validate paths

Validate:

- donor game directory exists;
- Unity project directory exists;
- local config parses;
- external staging/reference directories are writable or can be created safely;
- donor directory is not inside the Unity project;
- staging is not inside the donor installation.

Use the provided PowerShell scripts when useful. Do not modify donor files.

### 3. Audit donor filesystem layout

Inspect and document:

- executable and Unity data-directory layout;
- detectable Unity version;
- serialized asset files;
- shared assets;
- resource files;
- scenes/levels when detectable;
- asset bundles;
- managed assemblies;
- native plugins;
- third-party middleware;
- audio containers/clips;
- meshes, textures, animation, terrain/world data indicators;
- configuration files;
- likely save-data location or references when safely discoverable.

Generate a lightweight file inventory and hashes for relevant files. Avoid repeatedly hashing irrelevant multi-gigabyte files.

### 4. Inspect available local tools

Check for locally available:

- AssetRipper;
- ILSpy/ilspycmd;
- Unity Editor path;
- Git and Git LFS.

Do not download or install anything.

If AssetRipper is available and can be invoked safely:

- record version;
- export only an initial inspection set or inventory into `DONOR_STAGING_DIR\raw`;
- preserve logs;
- never export into production Assets.

If ilspycmd is available:

- record version;
- generate a namespace/class inventory into `LEGACY_REFERENCE_DIR`;
- do not compile the exported source;
- identify likely PlayMaker-generated classes and pure-code candidates.

### 5. Create milestone documentation

Create or update:

```text
Docs/Porting/DONOR_AUDIT.md
Docs/Porting/PORTING_MATRIX.md
Docs/Porting/PORTING_LEDGER.csv
Docs/Porting/SYSTEM_MAP.md
Docs/Milestones/MILESTONE_00_REPORT.md
Docs/Architecture/CURRENT_REPOSITORY_STATE.md
```

Use templates under `Templates/`.

The porting matrix must cover:

- player;
- interaction/carrying;
- vehicle assembly;
- fasteners/tools;
- engine;
- clutch;
- gearbox;
- differential;
- wheels/tires;
- suspension/brakes;
- fluids/electrical;
- damage/wear;
- save/load;
- time/needs;
- NPCs/traffic;
- world/terrain/roads;
- weather;
- audio;
- animation;
- UI.

For every system include:

- observed donor sources;
- coupling level;
- direct dependencies;
- recommended transfer strategy;
- technical risk;
- proposed implementation order;
- unknowns.

### 6. Prepare safe directory structure

Create documentation/config/tool directories needed for later work. You may create external staging subdirectories:

```text
raw
normalized
manifests
logs
captures
```

Do not copy donor content yet unless a safe inventory tool produces a small controlled audit output.

### 7. Stop after Milestone 0

Do not continue into Milestone 1.

## Required final report

Provide:

1. Executive summary.
2. Repository state discovered.
3. Original-game structure discovered.
4. Tools found and their versions.
5. Files and systems inventoried.
6. Documents created/updated.
7. Commands executed.
8. Hashing/inventory results.
9. Actions not executed and why.
10. Risks and unknowns.
11. Exact readiness checklist for `Prompts/01_UNITY_FOUNDATION.md`.

Begin now.
