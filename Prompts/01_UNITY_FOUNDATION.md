/plan

Read `AGENTS.md`, the Milestone 0 report, donor audit, porting matrix, and `Docs/ARCHITECTURE.md`.

Complete **Milestone 1 only: Unity 6 HDRP foundation**.

## Objectives

1. Validate that the destination is a Unity 6 LTS HDRP project.
2. Preserve existing project settings and content unless a documented change is required.
3. Create the target module folder structure and assembly definitions.
4. Create a minimal bootstrap scene and composition root.
5. Add foundational service boundaries.
6. Add stable-entity-ID infrastructure and validation.
7. Add Editor validation entry points.
8. Add initial EditMode tests.
9. Produce the Milestone 1 report.

## Required foundations

Interfaces:

- `IGameTimeService`
- `IWeatherService`
- `ISaveService`
- `IAudioBackend`
- `IInteractionService`
- `IEntityIdProvider`
- `IWorldStreamingService`

Stable identity:

- `StableEntityId`
- authoring component;
- duplicate validation;
- explicit regeneration command;
- no Unity instance ID persistence.

## HDRP baseline

Validate/configure where practical:

- linear color space;
- Input System;
- Windows x64 target documentation;
- HDRP asset and quality association;
- bootstrap scene;
- directional sun;
- HDRP global volume;
- physically based sky or current equivalent;
- basic exposure/tone mapping/fog;
- no ray-tracing requirement.

Do not over-polish visuals in this milestone.

## Tests

Add EditMode tests for:

- stable ID format/uniqueness logic;
- duplicate detection;
- local path config parsing or path normalization;
- assembly-boundary helpers when applicable.

Use Unity batch mode if an Editor path is configured. Otherwise state exact commands and do not claim execution.

## Constraints

- Do not import donor assets.
- Do not install Wwise.
- Do not implement player/vehicle gameplay.
- Do not add a third-party DI framework.
- Do not create giant managers.
- Runtime assemblies must not reference Editor assemblies.

## Deliverables

- compiling module scaffold;
- bootstrap scene/setup;
- foundational interfaces;
- stable-ID system;
- validation menu/window or command;
- tests;
- `Docs/Milestones/MILESTONE_01_REPORT.md`;
- updated architecture/docs.

Stop after Milestone 1.
