# Repository Instructions — My Summer Car Remake Game

## 1. Mission

Build a private, non-public, donor-assisted recreation of My Summer Car using Unity 6 LTS and HDRP.

The locally installed licensed original game is a read-only donor for measurements, object relationships, transforms, configuration values, behavioral reference, selected data, and carefully reviewed isolated managed algorithms.

The new runtime must be an independent Unity project. The original executable, old Unity runtime assemblies, Steam emulation, DRM logic, or original installation must not be required while playing the recreated project.

## 2. Fixed paths

Use these paths unless `Config/DonorPaths.local.json` explicitly overrides them:

```text
Original game:
D:\SteamLibrary\steamapps\common\My Summer Car

Unity project:
E:\GAYmDev_Studio\MySummerCar_Remake_Game

Donor staging:
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging

Decompiled reference:
E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference

Reference media:
E:\GAYmDev_Studio\MySummerCar_Remake_Game\References
```

Never hard-code machine-specific absolute paths in runtime C# source. Read them from the ignored local configuration or use Editor-only settings.

## 3. Project scope

The target is a modern recreation with:

- Unity 6 LTS;
- HDRP;
- physically plausible materials;
- rebuilt production models and textures;
- modern weather, atmosphere, wetness, vegetation, and lighting;
- modular vehicle assembly and simulation;
- first-person interaction;
- versioned save data;
- Wwise-ready audio architecture with a temporary Unity Audio fallback;
- Windows x64 as the first platform;
- stable 60 FPS as the initial performance target.

The target is not a public release. Nevertheless, maintain clean provenance and keep donor content outside Git so the repository can later be audited or shared without accidentally bundling original game files.

## 4. Current non-goals

Until the single-player vertical slice is stable, do not implement:

- multiplayer or networking;
- mod SDK;
- console support;
- VR;
- ray tracing as a baseline requirement;
- DOTS/ECS without a demonstrated profiling need;
- an elaborate custom engine framework unrelated to the current milestone.

## 5. Non-negotiable donor-game rules

The original installation is read-only.

Do not:

- patch, replace, rename, or delete donor files;
- bypass DRM, ownership checks, Steam checks, encryption, access controls, anti-tamper, or platform authentication;
- copy Steam emulation or licensing code;
- use the original executable as a runtime component;
- reference donor `UnityEngine` assemblies from the new project;
- compile the full decompiled `Assembly-CSharp` into the new project;
- mass-import the donor project directly into production `Assets`;
- commit raw extractions, decompiled source, original assets, or generated donor dumps;
- claim that a donor subsystem was ported when it was only approximated.

Safe read-only inspection, hashing, inventory, scene/object analysis, asset export into external staging, and managed-code inspection are allowed for this private project.

## 6. Production art policy

The donor game's visual assets are not production-quality assets for the HDRP recreation.

### 6.1 Original meshes

Original meshes may be extracted and used as:

- dimensional references;
- silhouette and proportion references;
- blockout geometry;
- pivot and axis references;
- mounting-point and bolt-position references;
- collider references;
- retopology references;
- world-layout references;
- temporary comparison geometry in development-only scenes.

Original meshes must normally be replaced by newly authored production models.

For each production replacement:

1. Preserve gameplay-critical dimensions.
2. Preserve or deliberately migrate pivots and axes.
3. Preserve mounting, interaction, and fastener coordinates.
4. Create new high-detail geometry where it contributes visually.
5. Create suitable real-time topology.
6. Create a new UV layout when needed.
7. Bake normal, AO, curvature, and optional height data.
8. Create HDRP-compatible materials.
9. Create appropriate LODs.
10. Create simplified collision geometry.
11. Compare against the donor reference.
12. Record provenance and replacement status.

Do not use automatic subdivision as the primary remastering workflow.

### 6.2 Original textures

Original textures may be inspected only as:

- material identity references;
- color and pattern references;
- signage and label references;
- dirt, wear, and rust placement references;
- historical appearance references;
- evidence for ambiguous model details.

Original textures must not be used as final production textures.

Do not treat AI-upscaled donor textures as finished assets. Upscaling may be used temporarily to read a label or inspect an unclear pattern. Final materials must be reauthored.

Final visual assets should use newly authored:

- albedo without baked lighting;
- normal maps;
- metallic data;
- roughness or HDRP smoothness;
- ambient occlusion;
- optional height;
- detail maps;
- decals;
- procedural or mask-driven dirt, wetness, rust, and damage layers.

### 6.3 Reference-only content

Place temporary donor visual references under:

```text
Assets/Game/LegacyImport/ReferenceOnly/
```

Reference-only content must:

- be clearly labelled;
- never be required by production prefabs;
- be excluded from builds;
- have a provenance record;
- be removable without breaking runtime content.

## 7. Donor transfer classifications

Every donor-derived item must use one classification:

- `ReferenceOnly`
- `DimensionalReference`
- `BlockoutSource`
- `PivotSource`
- `MountPointSource`
- `CollisionReference`
- `WorldLayoutReference`
- `BehavioralReference`
- `ConfigurationTransferred`
- `CodePorted`
- `Reimplemented`
- `ReauthoredGeometry`
- `ReauthoredMaterial`
- `ReauthoredTexture`
- `TemporaryDirectImport`
- `ProductionReady`
- `Rejected`
- `Blocked`

Do not use vague labels such as "done" without specifying what was actually transferred or rebuilt.

## 8. Provenance and audit

Maintain:

- `Docs/Porting/DONOR_AUDIT.md`
- `Docs/Porting/PORTING_MATRIX.md`
- `Docs/Porting/SYSTEM_MAP.md`
- `Docs/Porting/PORTING_LEDGER.csv`
- extraction logs in external donor staging;
- hashes for relevant source files when practical.

A ledger record should contain:

- donor relative path;
- donor object, class, or asset name;
- source hash when available;
- asset or data type;
- transfer classification;
- destination path;
- current status;
- dependencies;
- importer/tool version;
- notes and known differences.

## 9. Repository boundaries

Keep outside Git:

- the original game installation;
- raw extraction dumps;
- normalized donor staging;
- decompiled reference sources;
- temporary converted audio;
- donor-generated bundles;
- Unity `Library`, `Temp`, `Logs`, `Obj`, builds, and user settings;
- machine-specific local path configuration;
- Wwise caches and generated banks unless a later decision explicitly changes this.

The repository may contain:

- newly written runtime and Editor code;
- import tools;
- manifests and hashes without donor binary payloads;
- tests;
- configuration examples;
- technical documentation;
- newly authored production assets;
- build scripts;
- provenance records.

## 10. Fixed technical direction

- Unity 6 LTS. Pin the exact installed patch in `ProjectSettings/ProjectVersion.txt`.
- HDRP.
- Windows x64 first.
- C#.
- Unity Input System.
- Assembly Definitions for all modules.
- ScriptableObject definitions for immutable content data where appropriate.
- Plain serializable save DTOs, not direct scene serialization.
- PhysX Rigidbody for world bodies plus custom testable vehicle simulation layers where needed.
- Wwise behind `IAudioBackend`; temporary Unity Audio backend until official Wwise integration is installed.
- Git and Git LFS for newly authored large binary assets.

Do not silently add external Git packages, paid Asset Store packages, custom native plugins, or large dependencies. Explain and request approval first.

## 11. Architecture principles

Prefer:

- small modules with explicit dependencies;
- composition roots rather than global service locators;
- simulation separated from presentation;
- data-driven definitions separated from mutable instances;
- testable pure calculations;
- stable identifiers for persistent entities;
- Editor tooling for repetitive authoring and import work;
- incremental vertical slices;
- clear temporary implementations with replacement notes.

Avoid:

- giant manager classes;
- arbitrary static mutable state;
- `FindObjectOfType` and name-based scene lookup as architecture;
- silent fallback behavior;
- circular assembly references;
- over-general frameworks before a concrete use case exists;
- magic numbers scattered through MonoBehaviours;
- save code tied directly to scene internals;
- business logic inside visual effects, Animator callbacks, or UI.

## 12. Module layout

Target modules:

```text
Assets/Game/
  Bootstrap/
  Core/
    Runtime/
    Configuration/
  Interaction/
    Runtime/
    Content/
  Player/
    Runtime/
    Animation/
  Vehicle/
    Runtime/
    Simulation/
    Assembly/
    Content/
  World/
    Runtime/
    Streaming/
    Content/
  Weather/
    Runtime/
    Presentation/
  Audio/
    Runtime/
    UnityFallback/
    Wwise/
  Save/
    Runtime/
    Migration/
  LegacyImport/
    Runtime/
    Editor/
    Manifests/
    ReferenceOnly/
  Presentation/
    Materials/
    Shaders/
    VFX/
    Lighting/
  Imported/
    DonorGenerated/
  Editor/
  Tests/
    EditMode/
    PlayMode/
```

Runtime assemblies must not reference Editor assemblies. Legacy import Editor code must not leak into builds.

## 13. Execution workflow

Before changing code or assets:

1. Read this file.
2. Read the relevant milestone prompt and supporting docs.
3. Inspect the current repository state.
4. Inspect existing implementations before adding new abstractions.
5. State assumptions and blockers.
6. Create a concrete scoped plan.
7. Perform only the requested milestone.

During implementation:

- make small verifiable changes;
- run the smallest relevant checks after each meaningful step;
- update documentation and ledger records;
- fix compile errors before continuing;
- do not hide manual Unity Editor steps;
- do not claim a test passed unless it was actually executed.

At completion:

1. Summarize what was inspected.
2. Summarize what changed.
3. List files created or modified.
4. List commands and tests executed.
5. Report results honestly.
6. List manual Unity steps still required.
7. List limitations and risks.
8. Recommend exactly one next milestone.

## 14. Unity operation rules

- Prefer Editor scripts, menu commands, import processors, and batch-mode validation.
- Do not pretend to click through the Unity Editor when you cannot.
- When a manual Editor action is unavoidable, document exact menu paths and expected results.
- Do not mass-move or delete assets without validating references.
- Never write generated files into the donor installation.
- Pin package versions.
- Keep scenes small and purpose-specific during early milestones.
- The bootstrap scene must not become a dumping ground for every system.
- Use Prefab Mode and reusable content prefabs.
- Use serialized references or explicit registries, not object-name lookup.

## 15. Code style

- Use descriptive names.
- Use nullable annotations where practical and supported by the project settings.
- Prefer `readonly` and immutable configuration where possible.
- Avoid public fields; use serialized private fields for authoring.
- Validate required serialized references.
- Keep MonoBehaviours thin when logic can live in plain C# classes.
- Use interfaces at genuine subsystem boundaries, not for every class.
- Use records/structs for small immutable value data when appropriate.
- Do not allocate per-frame in hot paths without a reason.
- Do not use LINQ in confirmed hot loops.
- Document non-obvious units and coordinate conventions.
- Use SI units internally unless the donor data requires a documented conversion.
- Add comments for intent and constraints, not line-by-line narration.
- No compiler errors and no new avoidable warnings.

## 16. Stable entity identity

Every persistent entity must have a project-owned stable ID.

Required concepts:

- `StableEntityId`
- authoring component or Editor tooling;
- duplicate-ID validation;
- explicit regeneration only;
- save records keyed by stable ID;
- migration support when prefabs are replaced;
- donor object IDs stored only as provenance, not as the sole runtime identity.

Never rely on Unity instance IDs, scene hierarchy paths, or names as persistent identity.

## 17. Player and interaction

Separate interaction intent from object implementation.

Plan for:

- first-person movement;
- camera look;
- crouching;
- configurable interaction distance;
- pickup, carry, place, rotate, and throw;
- contextual interaction targets;
- tool use;
- later full-body presentation and procedural IK.

Interaction candidates must expose explicit capabilities. Do not build a giant switch statement keyed by object names.

## 18. Vehicle assembly

Core concepts should include:

- `PartDefinition` — immutable authored data;
- `PartInstance` — mutable runtime/save state;
- `MountPoint`;
- `MountConstraint`;
- `FastenerDefinition`;
- `FastenerState`;
- `ToolDefinition`;
- `AssemblyGraph`;
- `VehicleAssemblyController`.

Each part may need:

- stable ID;
- world transform;
- installed mount;
- compatible mounts;
- fasteners;
- wear;
- damage;
- temperature;
- fluid contents;
- electrical state;
- donor-reference metadata;
- save/load support.

Preserve donor pivots and mount transforms when measurements are reliable, even when the production mesh is rebuilt.

## 19. Vehicle simulation

Separate drivetrain and support systems into testable components:

- engine;
- clutch;
- gearbox;
- differential;
- wheels and tires;
- brakes;
- suspension;
- thermal system;
- fluids;
- electrical system;
- damage and wear.

Use an explicit powertrain graph rather than one giant vehicle script.

Define an `IWheelPhysicsBackend` boundary.

Implementation order:

1. Simple end-to-end backend for a playable prototype.
2. Custom contact/tire-force backend with configurable substeps.
3. Calibration using donor values and captured original-game behavior.

Do not claim deterministic PhysX simulation across machines. Use tolerance-based tests.

## 20. World and streaming

Use donor world data to preserve identity, scale, road layout, key locations, and gameplay distances.

Rebuild production terrain, roads, vegetation, buildings, and props.

Plan world content as streaming cells or additive scenes. Persistent state must be independent of whether a cell is loaded.

Road reconstruction should normally follow:

```text
Donor road geometry/data
  -> extract centerline, width, elevation, and intersections
  -> author splines
  -> generate new road and shoulder meshes
  -> blend with terrain
  -> add ditches, gravel, wetness, decals, and collision
```

## 21. HDRP presentation and weather

Plan:

- physically based sky;
- sun and moon/time-of-day;
- volumetric fog;
- dynamic clouds where practical;
- rain;
- wetness;
- puddles;
- wind;
- vegetation response;
- indoor/outdoor exposure transitions;
- restrained post-processing.

Avoid excessive bloom, chromatic aberration, motion blur, or gameplay depth of field. Readability comes first.

Use material-property or global-parameter systems for wetness rather than duplicating every material.

## 22. Audio and Wwise

Use an `IAudioBackend` boundary.

Provide:

- `UnityAudioBackend` as a temporary development fallback;
- `WwiseAudioBackend` only when the official integration is present;
- compile-time separation so the project compiles without Wwise;
- no fake Wwise types or placeholder namespaces.

Prepare parameters for:

- RPM;
- engine load;
- throttle;
- gear;
- clutch slip;
- speed;
- wheel slip;
- surface;
- damage;
- rain intensity;
- interior/exterior listener state;
- door/window openness.

Original audio may be used as reference or a temporary prototype source if recorded in the ledger. The final target is a newly authored and mixed soundscape.

## 23. Animation

Use donor animation only when compatibility and alignment are verified.

Plan:

- generic rigs where mechanical alignment matters;
- humanoid retargeting only for suitable character rigs;
- procedural IK for steering wheel, gear lever, ignition, tools, fasteners, and held objects;
- explicit interaction targets;
- animation events only for presentation synchronization, not core state authority.

## 24. Save system

Use versioned save DTOs and stable IDs.

Separate:

- current native save format;
- migrations;
- optional original-save importer;
- runtime state application;
- storage backend.

Do not directly couple the new runtime to an undocumented donor save structure. Inspect and document it first.

A failed migration must produce a clear report and preserve the original save file.

## 25. Reverse engineering and code porting

Managed code may be inspected and classified.

Prefer direct adaptation of:

- pure calculations;
- small utility functions;
- configuration tables;
- save-value transformations;
- isolated mechanical formulas;
- simple state transitions with known dependencies.

Prefer behavioral specification and reimplementation for:

- scene lookup code;
- old input;
- old UI;
- rendering;
- audio runtime;
- physics glue;
- coroutine-heavy global controllers;
- generated PlayMaker/state-machine code;
- code tied to obsolete Unity APIs;
- code with a forest of object-name dependencies.

Every ported method must have:

- documented origin;
- mapped dependencies;
- adapted modern API usage;
- a test or comparison fixture;
- a ledger entry;
- known differences.

## 26. Testing and validation

Use EditMode tests for:

- pure simulation;
- manifests;
- path normalization;
- hash comparison;
- stable-ID uniqueness;
- import planning;
- serialization and migration;
- assembly graph rules.

Use PlayMode tests for:

- scene boot;
- interaction flows;
- mounting/fastener flows;
- basic vehicle start/drive/stall;
- save/load round trips;
- weather transitions;
- missing-reference validation.

Use development validation tools for:

- duplicate IDs;
- missing required fields;
- donor references in production prefabs;
- reference-only assets leaking into builds;
- editor assemblies referenced by runtime assemblies;
- missing meshes/materials;
- invalid destination paths;
- broken provenance records.

## 27. Performance

Measure before optimizing.

Initial targets:

- Windows x64;
- 1920x1080 baseline;
- stable 60 FPS on a mid-range gaming PC;
- no per-frame managed allocations in confirmed hot paths;
- physics substeps configurable;
- distant rigidbodies sleeping or simplified;
- world streaming and LOD from the beginning;
- reference-only donor assets excluded from builds.

Maintain a performance capture for each major vertical slice.

## 28. Git workflow

- Keep commits small and focused.
- Do not commit donor binaries or local paths.
- Do not mix generated imports with unrelated runtime code in one commit.
- Use Git LFS for newly authored large binary production assets.
- Update documentation in the same change as architecture decisions.
- Never rewrite history or force-push unless explicitly instructed.
- Do not commit generated Unity project files such as `.csproj`.

Suggested commit prefixes:

- `docs:`
- `build:`
- `core:`
- `import:`
- `player:`
- `vehicle:`
- `world:`
- `weather:`
- `audio:`
- `save:`
- `test:`
- `art:`

## 29. Documentation

Keep these current:

- project scope;
- architecture;
- roadmap;
- donor audit;
- porting matrix;
- system map;
- porting ledger;
- milestone reports;
- ADRs for decisions that are hard to reverse.

A document is not complete if it only describes aspirations. Include concrete paths, interfaces, ownership, inputs, outputs, failure modes, and validation where relevant.

## 30. Stop conditions

Stop and report instead of guessing when:

- a requested action might modify the donor installation;
- an external tool/package must be installed;
- a donor format is encrypted or access-controlled;
- a direct import would contaminate production assets;
- the exact Unity/Wwise package version is unknown and material to the task;
- a change would cross the current milestone boundary;
- tests cannot actually be executed;
- the repository state conflicts with this document.

Do not use a blocker as an excuse to abandon the whole task. Complete every safe, verifiable part and clearly identify what remains.
