/plan

# MILESTONE 11A — FULL VEHICLE ROSTER AND LEGACY PRESENTATION

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read `VEHICLE_ROSTER.csv`, existing Satsuma/vehicle architecture, input, physics,
audio, world item, NPC and save reports.

## Objective

Implement every donor-evidenced vehicle required by Phase 1, including
player-drivable, service, scripted and AI-only vehicle roles assigned to 11A.

## Required per vehicle

- project-owned stable vehicle ID and definition;
- role classification and ownership/key rules;
- spawn/parking/home state;
- controller or AI presentation boundary;
- donor-faithful powertrain/control behavior to available evidence;
- fuel, ignition, damage/wear and relevant special systems;
- seats/entry/exit using logic-driven transitions;
- cargo/item interaction where required;
- legacy mesh/material/collider/audio presenter;
- save DTO and restore order;
- tests and calibration evidence;
- known differences.

Reuse generic architecture when appropriate, but do not force vehicles with
different donor behavior into one giant controller.

Do not reauthor production vehicle art in Phase 1. Do not add advanced new damage
or bodywork systems unless donor parity requires them.

Batch by roster and close each batch safely if necessary.

Stop when all 11A vehicle rows are verified.
