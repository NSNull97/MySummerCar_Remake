# Satsuma dashboard save extension — 2026-09-05

## Bounded integration

The approved cockpit follow-up adds persistence for choke position, headlight
mode and hazards through the existing vehicle aggregate. Controller behavior,
donor measurements, interaction/audio and Editor authoring belong to the
parallel cockpit author. This task owns the persistence adapter and regression
tests. It does not launch Unity or modify a user save.

`VehicleSaveRecordDto` now has `hasDashboardControls` and nullable
`dashboardControls`. The payload is the author's
`SatsumaDashboardControlsSaveDto`: schema 1, `choke01`, `headlightsMode` (0/1/2),
and `hazardsOn`. The explicit presence flag handles JsonUtility's inline-object
behavior, following the existing optional assembly-state pattern.

- Legacy / absent presence calls `TryRestore(null)` for quiet safe defaults.
- Present data requires a non-null, valid DTO and a bound runtime controller.
- Absent presence ignores an otherwise materialized stale/default nested DTO.
- Capture uses the configured controller; its presence is stored explicitly.
- Restore calls the agreed quiet controller API inside existing vehicle apply.
- Existing binding and participant rollback/deferred records include the entire
  vehicle record, so no additional save participant or schema migration is added.

`ConfigureDashboardControls(SatsumaDashboardControlsController controls)` is a
separate additive authoring seam. The existing `Configure` signature is not
changed by this follow-up. The authoring task must call this seam on the vehicle
persistence binding after creating/configuring the cockpit controller.

Held control input and blink phase are transient. The controller's agreed
`TryRestore` contract resets them without sound or user-action events. This
integration calls that API directly, never the player-facing interaction APIs.

## Files

- `Assets/Game/Vehicle/Runtime/VehiclePersistence.cs`: the optional DTO fields,
  validation, serialized reference, configuration, capture and restore.
- `Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.cs`:
  only adds `partial` to reuse the established synthetic fixture.
- `Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.Dashboard.cs`
  and `.meta`: focused integration regressions.

Pre-existing dirty changes in these files are retained. The separately authored
dashboard controller and its DTO are dependencies, not replacements created by
this persistence task.

## Validation status

The integration has been independently reviewed and `git diff --check` is clean.
Sixteen written cases cover real legacy JSON omitting both fields, explicit
absence with a stale object, eight malformed present payloads, missing runtime
binding, native JSON roundtrip, internal binding rollback, rollback after a
later participant fails, unloaded/deferred roundtrip and invalid deferred data.

The published controller API is integrated. Five explicit `ActionRequested == 0`
assertions cover native/legacy restore, local/participant rollback and deferred
binding; the roundtrip also verifies transient blink-phase reset. Private C#
compilation passed for nine runtime assemblies (including fresh Vehicle
Simulation) and the existing CurrentDomain + Dashboard test sources. Only the
five existing RearSuspension CS0414 warnings remain. No NUnit or Unity execution
has been claimed. Logs: `%TEMP%/msc-dashboard-save-validation`.

The parent task also requested a private C# check of its current Items.Editor
changes. All seven `MSC.Items.Editor` sources compiled with zero warnings/errors,
using fresh private Items/ItemsIntegration references. Outputs are confined to
`C:/Users/NSNull/AppData/Local/Temp/msc-dynamic-consumer-validation`; the shared
`Library` was not modified by that check.

Exactly one next gate: the main task runs the integrated Unity cockpit
save/restore/rollback tests and
checks its authoring binding. The actual slot-01 remains read-only throughout
this delegated work.
