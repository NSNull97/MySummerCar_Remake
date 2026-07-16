/plan

# MILESTONE 05 — VEHICLE ASSEMBLY FOUNDATION

Read `AGENTS.md` completely before doing anything.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md` when present.

Read:

- `Prompts/CURRENT_STATE.md`;
- all milestone reports through 04B;
- `Docs/VEHICLE_SYSTEM.md`;
- player/interaction architecture;
- save-system architecture;
- donor porting records;
- reference-capture database and missing-data report;
- world/garage clearance fixtures;
- relevant tests and assembly definitions.

## Player-representation boundary

Do not introduce a physical full-body player, world-space hand IK, or animation-
dependent interaction. Assembly, controls and vehicle entry remain gameplay-
driven; optional first-person viewmodel arms are presentation only.

## Objective

Implement a robust, data-driven prototype of the vehicle assembly system.

The prototype must allow the player to pick up, position, install, fasten,
unfasten, and remove representative vehicle parts while preserving stable
identity and save-ready state.

This milestone does not implement detailed engine, tire, or drivetrain physics.

## Preserve existing work

- Reuse the completed player/interaction system.
- Do not replace it with a vehicle-specific interaction framework.
- Preserve stable entity IDs.
- Preserve validated donor pivots and mount coordinates.
- Preserve garage and world integration.
- Reuse existing service boundaries where sound.
- Do not create a second save identity system.

## Required architecture

Create or align the following concepts:

- `PartDefinition`;
- `PartInstance`;
- `PartRuntimeState`;
- `PartCategory`;
- `PartCompatibilityRule`;
- `MountPointDefinition`;
- `MountPointAuthoring`;
- `MountPointRuntime`;
- `MountConstraint`;
- `MountPose`;
- `FastenerDefinition`;
- `FastenerInstance`;
- `FastenerState`;
- `FastenerSize`;
- `ToolDefinition`;
- `ToolCompatibilityRule`;
- `AssemblyDependency`;
- `AssemblyGraph`;
- `AssemblyOperation`;
- `AssemblyOperationResult`;
- `VehicleAssemblyController`;
- `VehicleAssemblyQuery`;
- `VehicleAssemblyValidator`;
- save DTOs for parts, mounts, and fasteners.

Prefer immutable ScriptableObject definitions plus explicit runtime state.

Do not make ScriptableObject assets hold mutable per-save state.

## Part identity and state

Every part instance must support:

- stable instance ID;
- definition ID;
- current world transform;
- held state;
- installed/uninstalled state;
- current mount ID;
- installation pose;
- fastener states;
- wear/damage placeholders;
- temperature/fluid/electrical placeholders when relevant;
- collision mode;
- Rigidbody ownership;
- save/load;
- donor/reference provenance where applicable.

Definitions must support:

- category;
- compatible mounts;
- expected pivot;
- installation tolerances;
- mass;
- collision profile;
- required tools/fasteners;
- dependency rules;
- production prefab;
- reference fixture IDs;
- future simulation connection points.

## Mounting workflow

Implement a clear interaction flow:

1. Player holds a compatible part.
2. Nearby compatible mount candidates are queried.
3. The best valid candidate is selected deterministically.
4. A preview/ghost pose is shown.
5. Position and orientation tolerances are evaluated.
6. Dependencies and obstructions are evaluated.
7. The part is installed in an unfastened or partially fastened state.
8. Fasteners are tightened with compatible tools.
9. The assembly graph updates.
10. Physics and collision ownership update safely.

Do not teleport parts into mounts from arbitrary distances.

Do not use object names as identity.

Do not use scene hierarchy order as an assembly rule.

## Preview and feedback

Provide restrained prototype feedback:

- valid/invalid mount indication;
- reason for invalid placement;
- snap pose;
- required fastener/tool;
- fastener progress;
- dependency warning;
- obstruction warning;
- install/uninstall result.

Use existing interaction UI hooks. Do not build the final HUD here.

## Fasteners

Fasteners must have explicit state, such as:

- absent;
- inserted;
- loose;
- partially tightened;
- tightened;
- over-tightened placeholder if the design requires it;
- damaged placeholder.

Fastener operations must validate:

- correct fastener;
- compatible tool;
- tool size;
- access/visibility policy;
- part installed state;
- dependency state;
- tightening direction;
- minimum/maximum state.

Do not model every thread physically.

Use a deterministic state model suitable for saves and tests.

## Assembly graph

The graph must represent:

- installed parts;
- mount relationships;
- dependency relationships;
- fastener ownership;
- prerequisites;
- blockers;
- removal constraints;
- simulation connection points.

Provide queries for:

- can install;
- can remove;
- missing prerequisites;
- unsecured parts;
- connected drivetrain path placeholder;
- connected electrical/fluid path placeholders;
- assembly completeness by subsystem.

Avoid a single giant `VehicleManager`.

## Prototype content

Use 12–20 representative parts that exercise different rules, for example:

- wheel;
- wheel fasteners;
- door;
- hood;
- battery;
- seat;
- dashboard component;
- radiator;
- starter;
- alternator;
- engine-block representative;
- cylinder-head representative;
- intake/exhaust representative;
- fuel tank or fluid container;
- one electrical connection;
- one dependency chain.

Use production or clean prototype meshes.

Donor meshes may appear only in a reference/comparison view.

## Physical behavior

Define safe state transitions for:

- held Rigidbody;
- dropped Rigidbody;
- installed kinematic/static state;
- unfastened installed state;
- detached state;
- collision layers;
- parent changes;
- scene/cell ownership.

Prevent:

- duplicated parts after save/load;
- exploding physics on installation;
- stale joints;
- collider overlap storms;
- part state existing in two mounts;
- a fastener belonging to two parts;
- removed prerequisites leaving impossible graph state without a reported rule.

## Save readiness

Create versioned DTOs for:

- part instances;
- mount relationships;
- fastener states;
- current transforms;
- assembly graph version;
- unresolved/missing content.

Do not implement the entire save system here.

Provide round-trip fixtures that the save milestone can consume later.

## Editor tooling

Create tools under:

`Tools → MSC Remake → Vehicle Assembly`

Required capabilities:

- validate part definitions;
- validate duplicate IDs;
- validate mount IDs;
- visualize mount poses and tolerances;
- visualize dependency graph;
- validate fastener ownership;
- detect missing production prefabs;
- detect donor dependencies;
- create a representative test vehicle;
- run assembly validation;
- export assembly report.

## Tests

Add EditMode tests for:

- compatible/incompatible mounting;
- deterministic candidate selection;
- position tolerance;
- orientation tolerance;
- dependency rules;
- obstruction result;
- duplicate mount rejection;
- install/remove graph consistency;
- fastener state transitions;
- tool mismatch;
- missing fastener;
- unsecured-part query;
- stable-ID serialization;
- save DTO round trip;
- invalid content handling.

Add PlayMode tests where practical for:

- player pickup → install;
- install → fasten;
- unfasten → remove;
- drop and repick;
- load representative assembly state;
- no physics explosion during transition;
- no duplicate part after reconstruction.

## Performance

Measure or inspect:

- candidate-query allocations;
- physics overlap frequency;
- graph-update cost;
- per-frame work;
- gizmo/editor cost;
- object count in the prototype.

Do not prematurely introduce DOTS/ECS.

## Non-goals

Do not implement:

- detailed combustion;
- torque curves as final behavior;
- clutch/gearbox/differential simulation;
- custom tire forces;
- final damage/wear;
- final fluids/electrics;
- Wwise;
- final UI;
- networking;
- complete vehicle content.

## Documentation

Create or update:

- `Docs/Vehicle/ASSEMBLY_ARCHITECTURE.md`;
- `Docs/Vehicle/PART_AUTHORING_GUIDE.md`;
- `Docs/Vehicle/MOUNT_AND_FASTENER_GUIDE.md`;
- `Docs/Vehicle/ASSEMBLY_TEST_MATRIX.md`;
- `Docs/Vehicle/ASSEMBLY_KNOWN_LIMITATIONS.md`;
- `Docs/Milestones/MILESTONE_05_REPORT.md`;
- architecture, roadmap, porting ledger, and reference links.

## Definition of done

1. The system compiles.
2. The existing interaction system drives assembly actions.
3. Representative parts can be installed and removed.
4. Fasteners and tools work.
5. Dependency rules work.
6. Stable IDs and DTOs exist.
7. Editor validation exists.
8. Tests exist and are run when possible.
9. No detailed simulation has been smuggled into this milestone.
10. The report states readiness for the first `05A` world-remaster pass and
    `06` vehicle simulation.

## Final response

Report:

1. Existing architecture reused.
2. New assembly architecture.
3. Prototype parts.
4. Mount/fastener behavior.
5. Save-ready state.
6. Editor tooling.
7. Tests and results.
8. Performance observations.
9. Files changed.
10. Manual Unity steps.
11. Known limitations.
12. Exact recommended next prompt.

Create `Docs/Milestones/MILESTONE_05_REPORT.md` and stop.
