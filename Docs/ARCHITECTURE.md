# Target Architecture

## Architectural shape

Use a modular GameObject/MonoBehaviour project with plain C# simulation classes, ScriptableObject definitions, explicit composition roots, stable IDs, and assembly definitions.

Do not begin with DOTS/ECS. Introduce specialized data-oriented systems only after profiling shows a concrete need.

## Layering

### Content definitions

Immutable or mostly immutable authored data:

- part definitions;
- tool definitions;
- vehicle configurations;
- surface definitions;
- weather presets;
- audio event mappings;
- world-cell definitions.

### Runtime state

Mutable state owned by simulation:

- part wear and installation;
- fastener state;
- engine temperature and RPM;
- player-held item;
- weather progression;
- persistent world entity state.

### Presentation

Meshes, materials, particles, animation, camera, UI, and audio consume simulation state. Presentation must not become authoritative for gameplay.

### Persistence

Save DTOs capture stable runtime state. Scene objects are reconstructed or updated from save data through stable IDs.

## Composition root

`Assets/Game/Bootstrap/Bootstrap.unity` owns one `GameCompositionRoot`. The root survives scene loads but has no static `Instance`, global registry, scene-name lookup, or implicit fallback implementation.

`GameServiceBindings` is the explicit immutable constructor boundary for the initial services. Its original constructor still requires the complete seven-service set. The current `CreatePartial(IInteractionService)` overload permits the one implemented milestone service to be owned by the root without fake implementations for unfinished systems; additional partial factories must wait for a concrete use. A partial binding is diagnostic ownership, not service lookup: plain C# consumers still receive narrow dependencies through constructors and MonoBehaviours through an explicit concrete installer. `GameCompositionRoot.Initialize` remains one-shot.

Initial service boundaries:

- `IGameTimeService`
- `IWeatherService`
- `ISaveService`
- `IAudioBackend`
- `IInteractionService`
- `IEntityIdProvider`
- `IWorldStreamingService`

Do not turn the interfaces into a global service locator. Systems should receive dependencies through constructors for plain classes or explicit initialization for MonoBehaviours.

## Assembly graph

Implemented assembly graph under `Assets/Game`:

```text
MSC.Core.Runtime
MSC.Interaction.Runtime -> Core
MSC.Player.Runtime -> Core, Interaction, Input System
MSC.Vehicle.Simulation -> UnityEngine value types only; no project asmdef reference
MSC.Vehicle.Assembly -> Core, Interaction
MSC.Vehicle.Runtime -> Vehicle.Simulation, Vehicle.Assembly, Input System
MSC.World.Runtime -> Core
MSC.World.Streaming -> Core, World.Runtime
MSC.Weather.Runtime -> Core, World
MSC.Audio.Runtime -> Core
MSC.Audio.UnityFallback -> Core, Audio.Runtime
MSC.Save.Runtime -> Core
MSC.Save.Migration -> Core, Save.Runtime
MSC.LegacyImport.Runtime -> Core
MSC.LegacyImport.Editor -> LegacyImport.Runtime, Core [Editor only]
MSC.Bootstrap.Runtime -> initial service-boundary assemblies
MSC.Development.Performance -> RenderPipelines.Core [opt-in capture; full implementation only in Editor/development builds]
MSC.Editor -> runtime modules, LegacyImport.Editor, Development.Performance, HDRP/Core Runtime [Editor only]
MSC.Tests.EditMode -> Core, Bootstrap, Interaction, Player, Vehicle Runtime/Simulation/Assembly, Save, World, LegacyImport, Editor validation [Editor test]
MSC.Tests.PlayMode -> Core, Bootstrap, Interaction, Player, Vehicle Runtime/Simulation/Assembly, World, Input System [PlayMode test]
```

No runtime assembly may reference an Editor assembly. `AssemblyDefinitionValidator` enforces this rule from the real `.asmdef` files and the EditMode suite covers the validator.

## Player and physical interaction ownership

Milestone 4 keeps input, movement, query and object behavior separate:

- `PlayerInputRouter` reads the authored Input System map and emits intent;
- `FirstPersonMotor` owns CharacterController movement, gravity and crouch/headroom;
- `FirstPersonLook` owns yaw/pitch and cursor capture;
- `PlayerInteractionController` chooses which explicit capability receives an intent;
- `RaycastInteractionCandidateSource` owns the bounded physics query;
- `InteractionTargetHost` explicitly lists capability components on a candidate;
- `PhysicalCarryController` owns current held-object physics and its domain snapshot;
- target components own contextual behavior, tool activation or mount acceptance.

While an object is carried, `PlayerInteractionController` explicitly supplies its `Rigidbody` as the one ignored query body. The query still treats every unrelated collider as an occluder; it does not make arbitrary geometry transparent. Carry ownership is lifecycle-safe: disable/destroy restores the captured Rigidbody state and player collision pair through an idempotent cleanup path.

The interaction controller implements the existing `IInteractionService` enablement boundary. A concrete installer may now bind that implemented service through `GameServiceBindings.CreatePartial` without inventing unrelated services; the complete constructor remains the required path once all seven owning subsystems exist.

The mount handoff interface is intentionally one-way. It transfers an already carried `IPickupTarget` to a receiver but does not define vehicle part compatibility, mount constraints, fasteners or assembly persistence; those remain Milestone 5 responsibilities.

## Stable identity

Every persistent entity has a project-owned `StableEntityId`: a canonical lower-case 32-character GUID in `N` format. Donor IDs are provenance metadata only.

`StableEntityIdAuthoring` stores the serialized ID but never creates or changes it in `Awake`, `OnValidate`, or runtime code. IDs are authored only through explicit Editor commands:

- `Tools > My Summer Car > Stable IDs > Assign Missing IDs In Open Scenes` assigns only empty IDs;
- `Tools > My Summer Car > Stable IDs > Regenerate IDs On Selected Objects` replaces selected IDs only after a save-migration warning and confirmation.

Project validation scans every scene and prefab under `Assets/Game`, plus unsaved loaded scenes. Runtime resolution is represented by `IEntityIdProvider`; Unity instance IDs, hierarchy paths, and object names are not persistence keys.

Validation must detect:

- duplicates;
- empty IDs;
- accidental regeneration;
- prefab-instance conflicts;
- save records referencing unknown IDs.

Milestone 1 implements empty, invalid, and duplicate ID detection. Prefab-instance conflict handling and unknown save-record references remain owned by the prefab/save milestones.

## Foundation scene and HDRP baseline

`Assets/Game/Bootstrap/Bootstrap.unity` is the first enabled build scene. It contains only the composition root, a physically measured directional sun, and a global HDRP Volume; it is not a gameplay dumping ground.

`Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset` contains serialized Physical Sky, dynamic sky ambient mode, fixed baseline exposure, ACES tonemapping, and volumetric fog. Ray tracing is not enabled as a baseline. The existing `Assets/OutdoorsScene.unity` remains enabled after Bootstrap and was not rewritten by the foundation generator.

The enabled scene order after Milestone 4 is Bootstrap first, `PlayerInteractionPrototype` second, `GarageArtPrototype` third and the existing Outdoors scene fourth. The garage scene is a static art prototype, not a composition root or streaming owner. Its `GaragePrototypePerformanceProbe` now belongs to `MSC.Development.Performance`; the full capture/file-output implementation compiles only in the Editor or development players and is inert without `-msc-m3-capture`. `MSC.World.Runtime` no longer depends on Render Pipelines Core for this tooling.

## Legacy import boundary

`MSC.LegacyImport.Runtime` owns serializable provenance data only: `DonorAssetManifest`, `DonorAssetRecord`, `DonorAssetRegistry`, and the development-only `LegacyAssetReference` marker. It contains no donor file access, extractor integration, or gameplay behavior.

`MSC.LegacyImport.Editor` owns all machine-path resolution, SHA-256 I/O, import planning/execution, registry/ledger authoring, production-reference validation, controlled-proof comparison tooling, and the pre-build donor-reference guard. The pipeline accepts payload only from configured external staging and can write payload only below the ignored `Assets/Game/LegacyImport/ReferenceOnly` root. Planning is read-only; execution is an explicit confirmed operation and never overwrites conflicts.

Production prefabs and enabled build scenes must have zero dependencies below `ReferenceOnly` or `Imported/DonorGenerated`. Build safety is intentionally independent of reference availability, so deleting all disposable reference assets does not invalidate a clean production prefab or player build. Detailed workflow and failure recovery are in `Docs/Porting/DONOR_PIPELINE.md`.

## Event flow

Prefer direct method calls and narrow domain events. Avoid a universal global event bus.

A domain event should carry immutable data and have an explicit owner. It must not replace clear dependencies.

## Simulation ticks

Use separate update responsibilities:

- frame presentation in `Update`;
- physics interaction in `FixedUpdate` or a controlled substep loop;
- slow world systems on scheduled ticks;
- save snapshots on explicit requests or controlled intervals.

Vehicle simulation should support configurable substeps independent of frame rendering.

## Data ownership examples

- `EngineSimulation` owns RPM, torque state, temperature, and stall state.
- `VehiclePresentation` reads engine state and drives visuals/audio parameters.
- `FastenerState` owns tightness and condition.
- Tool animation visualizes tightening but does not own tightness.
- `WeatherSimulation` owns rain intensity and wetness targets.
- HDRP presentation reads those values and adjusts volumes/material globals.

## Editor tooling

Editor tools should handle:

- donor-manifest inspection;
- path validation;
- reference/provenance validation;
- stable-ID validation;
- controlled import planning;
- pivot/mount-point comparison;
- build-content validation;
- milestone reports.

## Failure policy

Fail loudly during development when required data is missing. Do not silently substitute unrelated defaults.

User-facing builds may recover gracefully, but development tools must produce actionable errors with object paths, stable IDs, and suggested fixes.

## Bounded world-layout pilot ownership

Milestone 04A adds a deliberately narrow data path without introducing a world manager or changing Player/Interaction:

- `MSC.World.Runtime` owns `WorldLayoutPilotData`, source/sample DTOs and pure y-up metre-based coordinate helpers;
- `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json` is the durable project-owned reviewed record;
- `MSC.Editor` owns local path resolution, staging-manifest hashing, provenance/bounds/stable-ID/reference-leak validation and the disposable comparison-scene builder;
- the external staging manifest stays outside Git, and the generated comparison scene stays below ignored `LegacyImport/ReferenceOnly` and outside Build Settings.

The project-local origin is the reviewed donor garage anchor. Runtime source contains no machine-specific path, file I/O, `AssetDatabase` or donor dependency. Donor PathIDs are provenance only and never persistent entity IDs. The M3 road/terrain remain project-authored prototypes; their known disagreement with the measured `204.768 m` garage-to-nearest-route-sample relationship is documented rather than silently corrected in 04A.

## Reference capture ownership

Milestone 04B adds a data boundary, not a gameplay subsystem:

- `MSC.Core.Runtime` owns `ReferenceCaptureDatabase`, records, fixtures, stable IDs, units, deterministic JSON, schema migration and pure validation;
- `Assets/Game/Core/Configuration/ReferenceCapture` owns the versioned dataset, separate tuning overrides and calibration fixtures;
- `MSC.Editor` owns import/manual authoring, local evidence-path resolution, comparison, checklist generation and project validation;
- `Docs/ReferenceCapture` owns capture protocols, source map, indexes and the explicit missing-data queue;
- external donor staging/reference media owns raw donor representations, screenshots, video and audio and never becomes a runtime dependency.

The measured database cannot contain `tunedValue`. A tuning override references a measured record by project-owned stable ID and preserves its own rationale. Missing behavior is represented by a requirement and fixture, not by a fallback constant. Future simulation may consume validated fixtures, but it must not query donor assets or Editor APIs.

Dataset `04B.4` keeps that boundary intact: the rear-left drum state graph, wrench-14 mapping, scroll direction, three runtime repetitions and wheel-installed removal blocker are stored only as project-owned `BehavioralReference` records and a `Ready` fixture. No PlayMaker action, donor assembly or donor object-name dispatcher was added to runtime; future `PartDefinition`/`MountPoint`/fastener systems must reimplement the verified discrete `0..8` contract behind project-owned IDs and interfaces. Remake orientation tolerance remains project-authored tuning because the traced donor rule has no separate angular compare.

## Vehicle assembly ownership — Milestone 05

`MSC.Vehicle.Assembly` now implements the previously reserved assembly boundary. Immutable `PartDefinition`, `MountPointDefinition`, `FastenerDefinition` and `ToolDefinition` assets are separated from `PartRuntimeState`, `MountPointRuntime`, `FastenerInstance` and the versioned save DTOs. `AssemblyGraph` owns explicit install/removal dependencies; `VehicleAssemblyQuery` performs deterministic candidate and completeness queries; `VehicleAssemblyController` is the composition root for one vehicle assembly, not a global manager.

M4 architecture is preserved: `PlayerInteractionController` still produces intent, `PhysicalCarryController` still owns held-body physics, and `IMountHandoffTarget` is the only pickup-to-assembly transfer boundary. Thin assembly capabilities implement mount handoff, fastener tool activation and contextual removal. Object names are presentation only and never drive compatibility or persistence.

The representative scene under `Assets/Game/Vehicle/Content/Assembly/Scenes` contains 15 project-authored prototype parts and 14 mounts. Runtime dependencies are clean: no `ReferenceOnly`, `DonorGenerated`, donor executable, donor Unity assembly or PlayMaker runtime is reachable. Rear-drum `0..8`, wrench `14`, `0.01 m` reference marker and wheel blocker are a clean-room reimplementation of dataset `04B.4`; interaction tolerances are separately documented remake tuning.

Full design, authoring rules and limitations are in `Docs/Vehicle/ASSEMBLY_ARCHITECTURE.md`, `PART_AUTHORING_GUIDE.md`, `MOUNT_AND_FASTENER_GUIDE.md` and `ASSEMBLY_KNOWN_LIMITATIONS.md`.

## World production replacement ownership — Milestone 05A

The frozen 04A1 database remains spatial truth and is not overwritten by scene state. `MSC.World.Remaster.Runtime` owns durable production-registry records, comparison-mode state and thin moving-architecture capability. `MSC.World.Remaster.Editor` owns registry/backlog generation, deterministic prefab/cell construction, the dashboard, comparison scenes, dependency checks and pilot validation.

Three layers are explicit: removable donor/reference metadata, project-authored production assets, and a separate comparison scene. Production prefabs and generated production cells have no dependency on `LegacyImport/ReferenceOnly` or `Imported/DonorGenerated`. The first bounded pass produces only `cell_0_-3`; all other source records stay in the registry with explicit statuses and art-task links.

Player and Vehicle Assembly remain consumers. The pilot reuses M4 player intent and `IContextInteractionTarget`, and is composed into the M05 assembly scene without changing either subsystem's ownership. Generated cell content is updated in place so project-owned scene stable IDs and Unity file IDs remain deterministic across rebuilds.

## Vehicle simulation ownership — Milestone 06

`MSC.Vehicle.Simulation` is the pure fixed-step model boundary. It owns central config/provenance, state/DTOs, input/prerequisite/backend contracts, telemetry, an inspectable single-pair powertrain graph and small simulation nodes. Config indices map generic left/right driven-wheel nodes to two distinct wheels; the authored asset uses FL/FR (`0/1`), so the current prototype is FWD, while AWD remains future work. This FWD selection is a project topology decision for the target vehicle; numeric dynamics remain provisional because the donor dynamic fixture is `Missing`. The assembly has no project reference and cannot access assembly GameObjects, Editor APIs, donor paths or scene content.

`MSC.Vehicle.Runtime` is the Unity adapter layer. `VehicleInputRouter` samples and latches input in `Update`; `VehicleSimulationHost` invokes one root tick from `FixedUpdate`; `PrototypeRaycastWheelPhysicsBackend` implements `IWheelPhysicsBackend` for one dynamic proxy Rigidbody; `VehicleSimulationPresenter` consumes backend visual state in `LateUpdate`. Telemetry overlay/recording is development-only presentation.

Assembly authority remains in `MSC.Vehicle.Assembly`. The M06 adapter converts its separate bounded logical graph into cached prerequisite flags keyed by graph mutation count. The moving proxy is not a hidden replacement for the logical graph, and the logical graph is not assumed to supply validated mass/center of mass. Missing content produces typed prerequisite failures.

The fixed-step relationship is explicit: one backend sample, configurable pure substeps (default four), averaged commands, one backend apply, then finite-state/telemetry update. Numeric tests use tolerances; no cross-platform bitwise PhysX determinism is claimed.

The prototype track's `VehicleSurfaceMetadataAuthoring` is vehicle-owned semantic metadata. It does not alter or validate production collision layers/PhysicMaterials, so `WORLD-COL-003` remains open. All dynamic values remain `RemakeDesignTarget` / `ProvisionalProjectTuning` because reviewed donor dynamic fixtures are `Missing`.

Detailed ownership is recorded in `Docs/Vehicle/SIMULATION_ARCHITECTURE.md` and `Docs/Vehicle/WHEEL_BACKEND.md`.
