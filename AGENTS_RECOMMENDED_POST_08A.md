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

### 3.1 Mandatory phase model

**Phase 1 — Legacy Feature Complete:** recreate the complete selected donor version on the new runtime, including all donor-evidenced NPCs, vehicles, items, mechanics, jobs, services, story/event chains, media/minigames, progression and save domains. Temporary sanitized donor presentation may be used under sections 6.4 and 6.5. A vertical slice is not the Phase 1 completion target.

**Phase 2 — Production Remaster and Polish:** only after explicit Phase 1 approval, replace temporary donor presentation with newly authored production assets, polish, optimization and remake-only extensions.

## 4. Current non-goals

Until the Phase 1 Legacy Feature Complete build is stable, do not implement:

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
- mass-import raw donor project content directly into ordinary production `Assets`; the only exceptions are the sanitized temporary runtime baselines defined in sections 6.4 and 6.5;
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

### 6.4 Temporary donor world runtime baseline

During the private feature-parity phase, the already extracted donor world may be
used as a temporary playable runtime baseline. This is a deliberate exception to
the normal production-art replacement policy.

Place generated baseline content under:

```text
Assets/Game/LegacyImport/RuntimeBaseline/
```

The baseline may contain sanitized donor-derived:

- terrain or world meshes;
- static buildings and props;
- roads and bridges;
- vegetation and legacy backdrop objects;
- temporary donor materials and textures;
- simple or donor-derived collision used only as an interim runtime baseline;
- transforms and hierarchy needed to preserve the original map exactly.

The baseline must not contain or depend on donor:

- `MonoBehaviour` scripts;
- PlayMaker FSMs or generated state-machine runtime logic;
- runtime assemblies or old `UnityEngine` references;
- cameras, player controllers, UI, audio logic, lighting logic, weather logic,
  Steam/platform code, DRM logic, save logic, or gameplay managers;
- scene-name or hierarchy-name lookups used as project architecture.

Mandatory rules:

1. Classify every baseline item as `TemporaryDirectImport`.
2. Keep raw extraction and donor-generated payloads outside Git. Commit only
   project-owned import tools, manifests, mappings, reports, and replacement
   metadata.
3. The baseline may be included in private local feature-parity development
   builds, but it is not `ProductionReady` and must not be treated as final art.
4. Do not include the baseline in any distributable/public build unless explicit
   rights allow it.
5. Preserve original coordinates, scale, road layout, terrain silhouette,
   landmarks, and gameplay distances. Do not artistically reinterpret the map
   during baseline integration.
6. Gameplay systems must reference project-owned stable IDs, anchors, registries,
   and interfaces, never donor hierarchy paths, object names, or instance IDs.
7. Use separate logical layers or equivalent architecture for:

   ```text
   World_Global_Legacy
   World_Cell_<ID>_Legacy
   World_Cell_<ID>_Gameplay
   World_Cell_<ID>_ProductionOverride
   ```

8. Do not destructively split a single terrain, road, water body, or large mesh
   until a tested tool proves that seams, collision, scale, and transforms remain
   correct. Large cross-cell objects may remain in a global legacy scene.
9. A future production replacement disables the matching legacy renderer and
   collider through an explicit replacement key while keeping gameplay anchors,
   stable IDs, saves, and streaming metadata intact.
10. Visible donor defects such as sprite forests, flat proxy objects, terrain
    voids, and under-map hacks may remain only as documented temporary visual
    debt. Critical traversal, collision, out-of-bounds, and streaming failures
    must be made safe immediately without pretending the art defect is fixed.
11. The import/cellization process must be deterministic and repeatable from the
    canonical extracted source and recorded source hashes.
12. Never duplicate an already imported canonical map merely because a milestone
    prompt mentions import. Audit and reuse the existing extraction/import first.

### 6.5 Temporary donor gameplay presentation baseline

During private Phase 1 development, sanitized donor-derived presentation content may be used as `TemporaryDirectImport` beyond the world baseline.

Allowed generated paths include:

```text
Assets/Game/LegacyImport/RuntimeBaseline/Characters/
Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/
Assets/Game/LegacyImport/RuntimeBaseline/Items/
Assets/Game/LegacyImport/RuntimeBaseline/Animation/
Assets/Game/LegacyImport/RuntimeBaseline/Audio/
Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/
```

This content may include donor-derived meshes, textures, materials, rigs, compatible animation clips, audio clips, icons, transforms, colliders and presentation metadata needed to make the private Phase 1 build visibly and audibly complete.

Mandatory rules:

1. Donor code, `MonoBehaviour`s, PlayMaker FSMs/controllers, runtime assemblies, old `UnityEngine` references, Steam/platform/DRM logic, donor save/gameplay managers and donor state machines remain forbidden.
2. Project-owned definitions, controllers, stable IDs, DTOs, services and state machines are authoritative.
3. Every temporary presentation binding is owned by a project wrapper prefab/presenter. Gameplay never searches donor hierarchy names or asset filenames.
4. Record provenance, source hash when practical, `TemporaryDirectImport` classification and a production replacement key.
5. Raw donor extraction and generated donor payload stay outside Git. Commit only project-owned tools, manifests, mappings, reports and code.
6. This baseline is allowed only in explicitly private local Phase 1 builds and is never `ProductionReady`.
7. Temporary animation clips may drive project-owned Animators/presenters, but donor AnimatorControllers/FSM logic are not runtime authority.
8. Temporary audio is routed only through `IAudioBackend` and project-owned event IDs.
9. Production replacement must preserve stable IDs, save schema, event identity and gameplay coordinates.
10. Missing production polish is acceptable in Phase 1; missing required donor content or mechanics is not.

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
  Characters/
    Runtime/
    Presentation/
    Content/
  NPC/
    Runtime/
    Scheduling/
    Dialogue/
    Content/
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
  Economy/
    Runtime/
    Content/
  Jobs/
    Runtime/
    Content/
  Progression/
    Runtime/
    Events/
    Content/
  Media/
    Runtime/
    Content/
  Save/
    Runtime/
    Migration/
  LegacyImport/
    Runtime/
    Editor/
    Manifests/
    ReferenceOnly/
    RuntimeBaseline/
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
- lightweight first-person viewmodel arms for drinking, smoking, driving, and a
  small approved set of presentation-only gestures.

Do not create a physical full-body first-person player, world-space hand
collision, generic two-hand physics, animated vehicle entry/exit, or gameplay
that depends on an animation frame. Pickup, carrying, assembly, fastening, door
use, and seat transitions remain logic-driven. Viewmodel animations must be
safely interruptible and resettable.

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

Use donor world data to preserve identity, scale, road layout, key locations,
and gameplay distances.

Use a two-phase world strategy.

### Feature-parity phase

- Adopt the already extracted donor map as the sanitized temporary runtime
  baseline described in section 6.4.
- Reuse the existing streaming architecture instead of rebuilding it without
  evidence.
- Preserve the donor world exactly; do not reconstruct locations from memory or
  AI concepts while a direct donor baseline exists.
- Keep large continuous terrain, roads, water, or cross-cell geometry global
  when destructive splitting would create seams.
- Stream cell-owned static objects through additive scenes or the existing world
  cell system.
- Keep gameplay content in project-owned gameplay layers independent from legacy
  visual hierarchy.
- Existing custom cells that do not resemble the donor location are
  `PrototypeOnly / RejectedForFidelity`; disable their visual content in the
  active profile and use donor baseline content instead. Preserve useful
  streaming metadata, registries, tools, and tests.

### Remaster phase

Replace terrain, roads, vegetation, buildings, props, materials, textures, and
collisions cell by cell through production override layers. Preserve gameplay
coordinates, stable IDs, replacement keys, road geometry, and location identity.

Road replacement should normally follow:

```text
Donor road geometry/data
  -> extract centerline, width, elevation, and intersections
  -> author splines
  -> generate new road and shoulder meshes
  -> blend with terrain
  -> add ditches, gravel, wetness, decals, and collision
```

Persistent state must be independent of whether a cell is loaded and independent
of whether the visible object currently comes from the legacy baseline or a
production override.

## 20A. Phase 1 full-game feature parity

Lock one exact donor version and maintain:

```text
Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv
Docs/Phase1/PHASE1_SCOPE_LOCK.md
Docs/Phase1/PHASE2_BACKLOG.md
```

The matrix is authoritative for Phase 1 scope. Every donor-evidenced feature requires source evidence, implementation status, save coverage, temporary presentation status, tests, known differences and an owning milestone. Prompt examples are categories, not an exhaustive content list.

Do not begin Phase 2 while critical rows are unknown, missing or unverified. Do not add remake-only systems during Phase 1 unless required for safety, runtime independence or testability.

## 21. HDRP presentation and weather

Enviro 3 is the approved visual sky and weather backend. Project-owned systems
remain authoritative for game time, calendar, deterministic weather scheduling,
weather transitions, accumulated wetness/drying, gameplay lightning, saves, and
cross-system outputs.

Use a dedicated Enviro integration assembly. Gameplay/core assemblies must not
reference Enviro types directly. Do not modify Enviro vendor files and do not run
Azure Sky or duplicate HDRP sky/cloud/fog owners alongside Enviro.

Plan:

- physically based sky through the validated Enviro/HDRP ownership strategy;
- sun and moon/time-of-day;
- clouds;
- rain and storm presentation;
- one explicit fog owner;
- project-owned wetness and puddle outputs;
- wind and vegetation response;
- project-owned gameplay lightning with Enviro used only for presentation;
- indoor/outdoor exposure transitions;
- restrained post-processing.

Avoid excessive bloom, chromatic aberration, motion blur, or gameplay depth of
field. Readability comes first.

Use global/material-property wetness contracts rather than duplicating every
material. Temporary donor baseline materials may have partial wetness coverage;
record the limitation rather than silently reauthoring the whole world during
the weather milestone.

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
- humanoid retargeting only for suitable NPC character rigs;
- lightweight first-person viewmodel animation for drinking, smoking, driving,
  and approved optional gestures;
- steering-wheel and gear-lever viewmodel presentation where practical;
- explicit interaction targets for logic, not physical player hands;
- animation events only for presentation synchronization, never core state
  authority.

Do not require a physical first-person body, world-space hand IK, or animated
vehicle entry/exit for the current project scope.

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
- forbidden donor scripts/components in the temporary runtime baseline;
- gameplay dependencies on donor hierarchy names or paths;
- donor references in production override prefabs;
- reference-only assets leaking into builds;
- temporary runtime baseline content leaking into distributable/public builds;
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
- reference-only donor assets excluded from builds; temporary runtime baseline assets may appear only in explicitly private local feature-parity builds and must remain streamable and profiled.

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

## 29A. Approved UI reference lock

For Milestone 08A and subsequent work on its locked screens, the files under:

```text
References/UI/Approved/08A/
```

are authoritative visual specifications, not loose mood references.

Rules:

- Preserve their layout, panel proportions, hierarchy, spacing, selection logic,
  and persistent HUD contents as closely as practical.
- In a conflict between generic UI guidance and an approved 08A visual reference,
  the approved reference wins unless doing so would falsely claim unsupported
  functionality or violate project architecture/safety.
- Do not redesign a locked screen merely to make it cleaner or more conventional.
- Do not crop, trace, or bake reference screenshot pixels into runtime UI.
- Rebuild the interface with project-owned widgets, icons, materials, text, and
  live scene content.
- Do not reproduce obvious AI text artifacts, misspellings, or impossible data.
  Preserve the intended semantic control and its visual slot.
- Unsupported features may retain their visual row for fidelity but must be
  disabled and labeled truthfully.
- Reference PNGs are Editor/review inputs only and must be excluded from shipping
  runtime dependencies.
- Use the reference-overlay and comparison-capture workflow required by the 08A
  prompt.
- Codex may mark a locked screen `ImplementationComplete`; only the user may mark
  it `VisuallyApproved`.

## 29B. Phase 1 completion gate

Phase 1 is complete only when the parity matrix is closed, every required donor NPC/vehicle/mechanic/job/progression path is playable, full-game save/load coverage exists, a representative full-game playthrough has no critical blocker, a private Windows build runs independently from donor runtime files, and the user explicitly approves the gate.

## 30. Stop conditions

Stop and report instead of guessing when:

- a requested action might modify the donor installation;
- an external tool/package must be installed;
- a donor format is encrypted or access-controlled;
- a direct import would contaminate normal production assets or violate the dedicated sanitized runtime-baseline rules in section 6.4;
- the exact Unity/Wwise package version is unknown and material to the task;
- a change would cross the current milestone boundary;
- tests cannot actually be executed;
- the repository state conflicts with this document.

Do not use a blocker as an excuse to abandon the whole task. Complete every safe, verifiable part and clearly identify what remains.
