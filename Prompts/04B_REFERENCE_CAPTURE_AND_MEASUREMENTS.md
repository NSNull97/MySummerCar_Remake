/plan

# MILESTONE 04B — REFERENCE CAPTURE AND MEASUREMENT DATABASE

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE_AFTER_04.md`;
- all reports through Milestone 04A;
- donor audit, system map, porting matrix, and porting ledger;
- world-transfer coordinate/scale reports;
- player and interaction documentation;
- vehicle, save, audio, weather, and testing documentation;
- any existing reference-capture checklists or datasets.

## Objective

Create a project-owned, versioned source of truth for measurements and
observable behavior needed by future vehicle, world, weather, audio, UI, and
save milestones.

The original game is a read-only reference.

This milestone is not a gameplay implementation milestone.

The durable result must be structured reference data and repeatable capture
procedures, not scattered numbers in Markdown or code comments.

## Paths

Treat the currently opened repository as `PROJECT_ROOT`.

Read paths from `Config/DonorPaths.local.json`.

Expected donor installation:

`D:\SteamLibrary\steamapps\common\My Summer Car`

Never modify the donor installation or donor saves.

## Hard constraints

1. Do not patch or inject into the donor executable.
2. Do not bypass DRM, ownership checks, encryption, or access controls.
3. Do not alter donor files or donor saves.
4. Do not guess unknown values and mark them as measured.
5. Do not copy large decompiled classes into the remake.
6. Do not mix source measurements with tuned remake values.
7. Do not scatter constants across runtime code.
8. Do not begin vehicle simulation or final world remastering.
9. Do not install capture tools silently.
10. Do not claim an automated capture occurred when only static inspection was
    performed.
11. Preserve units, coordinate systems, confidence, and provenance for every
    record.
12. Store screenshots/videos outside Git unless repository policy says
    otherwise.

## Measurement categories

Capture or prepare capture procedures for the following categories.

### World and landmarks

- world origin and extents;
- terrain size and elevation range;
- major landmark positions;
- garage/home dimensions;
- floor elevations;
- door and gate pivots;
- road widths;
- junction positions;
- bridge deck heights;
- shoreline and water level;
- player and vehicle clearances;
- representative travel distances and travel times.

Reuse Milestone 04A data rather than duplicating it.

### Player and interaction

- standing and crouching eye height;
- movement speeds;
- acceleration/deceleration;
- jump behavior if present;
- interaction range;
- pickup range;
- held-object distance and orientation behavior;
- object throw/drop behavior;
- maximum or practical carried mass;
- seat-enter/exit anchors;
- tool-use ranges;
- camera field of view;
- head-bob or view-motion behavior when relevant.

### Vehicle geometry and assembly

- body dimensions;
- wheelbase;
- track width;
- ride height;
- wheel radius;
- steering-axis and wheel transforms;
- center-of-mass reference;
- part pivots;
- mount points;
- bolt positions;
- bolt counts;
- fastener sizes;
- installation order dependencies;
- part masses;
- door/hood/trunk pivots and motion range;
- fluid capacities;
- electrical connection relationships.

### Powertrain and vehicle behavior

- idle RPM;
- starter behavior;
- redline;
- torque/power reference where discoverable;
- gear ratios;
- final-drive ratio;
- clutch behavior;
- engine braking;
- stall conditions;
- acceleration fixtures;
- braking fixtures;
- steering lock;
- suspension travel;
- damping behavior;
- tire grip/slip observations;
- surface differences;
- damage and wear thresholds;
- temperature behavior;
- fuel and fluid consumption observations.

These values are reference fixtures only. Do not implement detailed simulation
during this milestone.

### Time, weather, and environment

- day length;
- sunrise/sunset behavior;
- time progression;
- weather states;
- transition durations;
- rain intensity;
- fog behavior;
- wind observations;
- wetness persistence;
- lighting reference conditions;
- ambient color/exposure observations.

### Audio

- donor event/source inventory;
- engine-state audio layers;
- RPM/load relationships;
- interior/exterior differences;
- occlusion observations;
- surface sounds;
- tool and fastener sounds;
- door, gate, and part sounds;
- rain and ambience zones;
- important timing relationships.

Do not treat subjective notes as exact acoustic measurements.

### UI, needs, and game-state presentation

- original status variables;
- value ranges;
- update frequency;
- warning thresholds;
- display priority;
- money/time formatting;
- interaction-prompt behavior;
- vehicle warning indicators;
- settings categories;
- save-slot metadata.

This is reference capture only, not final UI implementation.

## Data architecture

Create or align with project conventions for:

- `ReferenceCaptureDatabase`;
- `ReferenceRecord`;
- `MeasurementRecord`;
- `BehaviorFixture`;
- `ReferenceSourceRecord`;
- `ReferenceEvidenceRecord`;
- `ReferenceUnit`;
- `ReferenceConfidence`;
- `ReferenceCategory`;
- `ReferenceCaptureMethod`;
- `ReferenceTolerance`;
- `ReferenceDatasetVersion`;
- `ReferenceCaptureValidator`.

Every record must contain:

- stable record ID;
- category and subcategory;
- name;
- numeric, vector, curve, enum, string, or observational value;
- unit;
- coordinate space where relevant;
- source;
- source file/class/object when available;
- capture method;
- capture date;
- confidence;
- expected tolerance;
- raw observation;
- normalized value;
- notes;
- evidence references;
- remake tuning override, stored separately;
- validation status.

Do not store measured donor values and remake tuning overrides in the same field.

## Storage

Choose a scalable, versioned project-owned format.

It must support:

- deterministic serialization;
- diffable metadata where practical;
- schema migration;
- unit validation;
- duplicate-ID detection;
- querying by category;
- use by EditMode tests;
- use by simulation calibration later;
- use without loading donor assets.

Document the format in:

`Docs/ReferenceCapture/REFERENCE_DATA_FORMAT.md`

Create:

`Docs/ReferenceCapture/REFERENCE_DATABASE_INDEX.csv`

## Capture methods

Classify evidence as:

- `SerializedDonorData`;
- `DecompiledConstant`;
- `DecompiledFormula`;
- `SceneTransform`;
- `AssetMetadata`;
- `ManualMeasurement`;
- `VideoTiming`;
- `ScreenshotMeasurement`;
- `RuntimeObservation`;
- `DerivedCalculation`;
- `Approximation`;
- `Unknown`.

Derived values must list their input record IDs and calculation.

Approximate values must never be marked as exact.

## Priority levels

Use:

- `P0` — required before Vehicle Assembly or world integration;
- `P1` — required before Vehicle Simulation, Weather, Audio, or UI;
- `P2` — required for later fidelity tuning;
- `P3` — optional historical/reference detail.

At minimum, complete all available P0 records and build procedures for missing
P0 records.

## Tooling

Create an Editor tool under:

`Tools → MSC Remake → Reference Capture`

Capabilities:

- open capture dashboard;
- list records by category/priority/status;
- import structured donor measurements;
- add a manual observation;
- attach evidence paths;
- convert units;
- compare measured and tuned values;
- validate missing units;
- validate duplicate IDs;
- validate confidence;
- export a calibration fixture;
- show missing P0/P1 data;
- generate capture checklist;
- open related donor/reference object when available.

Do not require donor content in normal runtime builds.

## Manual capture pack

Create exact manual procedures where automation is unavailable.

Create:

`Docs/ReferenceCapture/MANUAL_CAPTURE_GUIDE.md`

Include:

- required game version/build evidence;
- graphics and input settings;
- save-state prerequisites;
- camera/setup procedure;
- timing procedure;
- distance and scale procedure;
- repeated-trial procedure;
- how to record uncertainty;
- how to name evidence files;
- how to avoid modifying the donor save;
- how to restore/backup saves where legally and technically appropriate;
- how to report values that cannot be measured reliably.

Create checklists:

- `PLAYER_INTERACTION_CAPTURE.md`
- `VEHICLE_ASSEMBLY_CAPTURE.md`
- `VEHICLE_BEHAVIOR_CAPTURE.md`
- `WORLD_LANDMARK_CAPTURE.md`
- `TIME_WEATHER_CAPTURE.md`
- `AUDIO_REFERENCE_CAPTURE.md`
- `UI_STATE_CAPTURE.md`

## Reference fixtures

Create a small set of machine-readable calibration fixtures for future tests.

Required fixtures:

- player movement/interaction fixture;
- garage dimensions and clearance fixture;
- vehicle body/wheel geometry fixture;
- representative part mount/pivot fixture;
- engine idle/start/stall fixture;
- gearbox/final-drive fixture;
- steering/suspension fixture;
- braking/acceleration fixture;
- time progression fixture;
- weather transition fixture;
- audio-state mapping fixture.

Fixtures may initially contain missing fields with explicit status, but must not
contain fabricated values.

## Tests

Add EditMode tests for:

- database serialization;
- schema versioning;
- stable record IDs;
- duplicate detection;
- unit conversion;
- coordinate-space validation;
- source/provenance requirements;
- confidence rules;
- derived-calculation dependency resolution;
- measured-versus-tuned separation;
- missing P0 reporting;
- fixture loading.

Run tests when Unity batch mode is available.

Otherwise give exact commands and do not claim they ran.

## Documentation

Create or update:

- `Docs/ReferenceCapture/REFERENCE_DATA_FORMAT.md`;
- `Docs/ReferenceCapture/REFERENCE_DATABASE_INDEX.csv`;
- `Docs/ReferenceCapture/REFERENCE_SOURCE_MAP.md`;
- `Docs/ReferenceCapture/MANUAL_CAPTURE_GUIDE.md`;
- all category checklists listed above;
- `Docs/ReferenceCapture/MISSING_REFERENCE_DATA.csv`;
- `Docs/ReferenceCapture/CAPTURE_SESSION_LOG.csv`;
- `Docs/Milestones/MILESTONE_04B_REPORT.md`;
- porting ledger and roadmap where needed.

## Definition of done

1. A versioned reference database exists.
2. Units and coordinate spaces are explicit.
3. Provenance and confidence are explicit.
4. Measured values are separated from remake tuning.
5. Available P0 data is imported.
6. Missing P0 data is listed.
7. Manual capture procedures exist.
8. Calibration fixtures exist.
9. Editor tooling exists.
10. Validation/tests exist.
11. No donor files were changed.
12. No future simulation was implemented.
13. The Milestone 04B report gives a go/no-go for Vehicle Assembly.

## Final report

Report:

1. Sources inspected.
2. Capture methods available.
3. Database/schema created.
4. P0/P1 coverage.
5. Missing critical measurements.
6. Fixtures created.
7. Editor tools created.
8. Tests and results.
9. Manual capture still required.
10. Files changed.
11. Risks and assumptions.
12. Exact readiness for `05_VEHICLE_ASSEMBLY.md`.

Stop after Milestone 04B.
