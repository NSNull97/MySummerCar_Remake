# Vehicle Assembly and Simulation

## Separation

Treat the vehicle as three connected layers:

1. Authored definitions.
2. Mutable assembly/simulation state.
3. Presentation and physical GameObjects.

## Assembly graph

Each installed part is a node. Mount relationships and required fasteners form edges and constraints.

Core types:

- `PartDefinition`
- `PartInstance`
- `MountPoint`
- `MountConstraint`
- `FastenerDefinition`
- `FastenerState`
- `ToolDefinition`
- `AssemblyGraph`
- `VehicleAssemblyController`

## Mounting rules

A part can mount only when:

- the mount type is compatible;
- orientation and position tolerances are met;
- blocking parts are absent or requirements are satisfied;
- required tools or preparatory states are available;
- installation would not create an invalid graph.

Mounting must preserve stable identity. Do not destroy and recreate a different logical part merely because the visual prefab changes parent.

## Fasteners

Fasteners have:

- stable association with part and mount;
- type and tool compatibility;
- tightness;
- condition/damage;
- required torque range later;
- visual state;
- save state.

Tool animation is presentation. Fastener state is simulation authority.

## Drivetrain

Use explicit components:

```text
Engine -> Clutch -> Gearbox -> Differential -> Wheels
```

Each component exposes inputs, outputs, and state. Pure calculations should be testable without a scene.

## Engine

Initial model:

- RPM;
- torque curve;
- throttle;
- inertia;
- friction;
- starter torque;
- idle behavior;
- stall threshold;
- temperature;
- simplified fueling/ignition prerequisites.

Later extensions:

- mixture;
- carburetion;
- oil pressure;
- cooling;
- damage and wear;
- electrical dependencies.

## Wheels and tires

Define `IWheelPhysicsBackend`.

Prototype backend may be simple. Production direction should support:

- suspension travel;
- spring/damper;
- contact normal;
- longitudinal slip;
- lateral slip;
- tire load;
- surface friction;
- braking torque;
- wheel inertia;
- configurable substeps.

## Damage and wear

Do not use one generic health bar. Damage should affect meaningful parameters:

- leakage;
- friction;
- alignment;
- electrical continuity;
- thermal behavior;
- torque transfer;
- visual presentation;
- sound.

## Calibration

Store donor-derived measurements and constants in documented reference data. Never bury them across MonoBehaviours.

Build comparison tests for:

- idle and redline behavior;
- acceleration windows;
- gear ratios;
- clutch engagement;
- braking distance;
- suspension response;
- part masses;
- stall conditions.

## Milestone 05 implemented assembly baseline

The assembly half of this document is now implemented under `Assets/Game/Vehicle/Assembly/Runtime`; drivetrain, engine, wheels, damage and wear remain future simulation work.

Implemented concrete types include `PartDefinition`, `PartInstance`, `PartRuntimeState`, `PartCompatibilityRule`, `MountPointDefinition`, `MountPointAuthoring`, `MountPointRuntime`, `MountConstraint`, `MountPose`, `FastenerDefinition`, `FastenerInstance`, `FastenerState`, `FastenerSize`, `ToolDefinition`, `ToolCompatibilityRule`, `AssemblyDependency`, `AssemblyGraph`, `AssemblyOperation`, `AssemblyOperationResult`, `VehicleAssemblyController`, `VehicleAssemblyQuery`, `VehicleAssemblyValidator` and schema-v1 DTOs.

The current representative asset set contains 15 clean prototype parts and 14 mounts. It supports pickup, deterministic preview, handoff/install, discrete tighten/loosen, blocked removal, detach and save DTO round trip. The detailed contract is maintained in `Docs/Vehicle/ASSEMBLY_ARCHITECTURE.md`.
