/plan

# MILESTONE 08B — PHASE 1 SCOPE LOCK AND DONOR FEATURE-PARITY AUDIT

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Also read all milestone reports through 08A, donor audit/ledger/system map,
reference-capture inventories, decompiled-reference classification reports,
current scenes/content registries, save architecture, and current tests.

## Objective

Create the authoritative, evidence-backed Phase 1 scope for a **complete Legacy
Feature Complete build**. This milestone is audit/planning only.

Do not implement gameplay systems, NPCs, vehicles, jobs, story, or production
art in this milestone.

## Donor version lock

Identify and record the exact selected donor version using all available evidence:

- executable and managed assembly hashes;
- build/version strings;
- Steam build evidence when locally available without network assumptions;
- asset/scene hashes;
- save-version evidence;
- capture dates and reference set.

Create `Docs/Phase1/DONOR_VERSION_LOCK.md`.

Do not combine behavior from several donor versions without explicit user
approval. If installed data appears mixed, report it and stop before scope lock.

## Exhaustive feature discovery

Inspect donor evidence and current project implementation across at minimum:

- player actions, needs and domestic life;
- physical items, tools, food, liquids, packages and containers;
- Satsuma assembly, tuning, service, wear, damage and inspection state;
- every vehicle and transportation actor;
- every named/background NPC, schedule, dialogue and relationship state;
- traffic, bus, train, boat and scripted routes;
- every store, pub, workshop, service and authority interaction;
- phone, mail, bills, catalog orders and deliveries;
- every job and repeatable activity;
- story, conditional events, progression paths and outcomes;
- police, fines, jail, hospital, death/permadeath and rally;
- radio, television, computer, music import, minigames and local events;
- world triggers, utilities and time/calendar behavior;
- save fields and persistence dependencies;
- visual/audio/animation presentation required to make each feature visible and
  understandable.

The list above is a discovery checklist, not the final content list.

## Required matrix

Create:

`Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`

Required columns:

- `FeatureId`;
- `Domain`;
- `FeatureName`;
- `Required`;
- `DonorVersionEvidence`;
- `DonorSources`;
- `PlayerVisibleBehavior`;
- `Dependencies`;
- `CurrentImplementation`;
- `Status`;
- `LegacyPresentation`;
- `SaveCoverage`;
- `UITelemetry`;
- `AudioCoverage`;
- `TestCoverage`;
- `KnownDifferences`;
- `OwnerMilestone`;
- `HumanDecision`;
- `Notes`.

Allowed status values:

```text
Unknown
EvidenceCaptured
Specified
PartiallyImplemented
ImplementedUnverified
Verified
KnownDifferenceNeedsApproval
KnownDifferenceApproved
Blocked
DeferredPhase2
RejectedNotInLockedDonorVersion
```

Every row must have a stable `FeatureId` independent of donor object names.

## Supporting inventories

Create:

- `Docs/Phase1/NPC_ROSTER.csv`;
- `Docs/Phase1/VEHICLE_ROSTER.csv`;
- `Docs/Phase1/JOB_AND_ACTIVITY_ROSTER.csv`;
- `Docs/Phase1/STORY_EVENT_ROSTER.csv`;
- `Docs/Phase1/SERVICE_AND_LOCATION_ROSTER.csv`;
- `Docs/Phase1/ITEM_AND_CONSUMABLE_CATEGORY_ROSTER.csv`;
- `Docs/Phase1/LEGACY_PRESENTATION_INVENTORY.csv`;
- `Docs/Phase1/PHASE2_BACKLOG.md`.

Do not copy raw donor payload into these files. Store identifiers, hashes,
classification, evidence locations and behavioral descriptions.

## Current project gap analysis

For every discovered row, inspect whether the new project already has:

- architecture only;
- working implementation;
- temporary placeholder;
- donor-independent tests;
- save coverage;
- presentation baseline;
- nothing.

Do not mark a feature implemented merely because a similarly named type exists.

## Bug/quirk policy proposal

Create `Docs/Phase1/DONOR_QUIRK_AND_BUG_POLICY.md` with each observed quirk
classified as:

- intentional/required parity;
- recognizable but optional compatibility behavior;
- safe difference;
- legacy engine defect to fix;
- unknown, requires user decision.

## Execution plan

Create `Docs/Phase1/PHASE1_EXECUTION_PLAN.md` mapping every required matrix row to
one of the new milestones 09A–14B, with dependencies and acceptance evidence.

If one milestone would contain an unsafe amount of work, split its assigned rows
into named bounded batches while preserving the supplied milestone order.

## Definition of done

1. One donor version is locked.
2. The matrix is exhaustive to the available evidence.
3. Every feature has an owner milestone or explicit decision state.
4. All current implementation claims are evidence-backed.
5. Phase 2 enhancements are separated from donor parity.
6. No gameplay implementation was performed.
7. `Docs/Milestones/MILESTONE_08B_REPORT.md` records methods, unknowns and risks.

Stop after the audit and plan. Wait for user review of the matrix before 09A.
