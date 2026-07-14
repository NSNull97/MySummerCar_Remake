/plan

# MILESTONE 06A — VEHICLE PHYSICS CALIBRATION AND VALIDATION

Read `AGENTS.md` completely.

Read:

- all reports through Milestone 06;
- reference-capture fixtures;
- vehicle simulation architecture and telemetry guide;
- vehicle assembly report;
- road, collision, surface, and world-validation reports;
- current simulation configs;
- current Git status and diff.

## Objective

Validate and calibrate the vehicle simulation against available donor
measurements, observational fixtures, and physically plausible constraints.

This milestone is a validation and tuning gate.

Do not add unrelated vehicle features.

Do not hide incorrect physics by changing the camera, time scale, road, or
telemetry.

## Validation levels

Classify each metric as:

- `MeasuredDonorReference`;
- `ObservedDonorReference`;
- `DerivedReference`;
- `PlausibilityTarget`;
- `RemakeDesignTarget`;
- `Unknown`.

Never present a plausibility target as an exact donor value.

## Test configuration

Create a repeatable validation environment.

It must record:

- vehicle configuration;
- installed parts;
- masses;
- center of mass;
- tire/surface configuration;
- weather/wetness state;
- fuel/load;
- input device or scripted input;
- fixed timestep;
- substeps;
- Unity/PhysX version;
- build/editor mode;
- hardware;
- seed where relevant.

Create a stable test route or test track that includes:

- flat acceleration section;
- braking section;
- steering/slalom section;
- bump/suspension section;
- gravel/dirt section;
- slope if available;
- garage/driveway clearance;
- representative collision transition.

Do not damage production world layout merely to create the track.

## Required metrics

### Static geometry and mass

- total mass;
- part contribution;
- center of mass;
- wheelbase;
- track width;
- ride height;
- wheel radius;
- suspension rest position;
- steering lock;
- ground clearance;
- garage and ramp clearance.

### Engine and powertrain

- starter time;
- idle RPM;
- idle stability;
- throttle response;
- free-rev response;
- stall behavior;
- gear ratios;
- final drive;
- clutch engagement;
- clutch slip;
- engine braking;
- RPM versus vehicle speed by gear.

### Longitudinal dynamics

- launch behavior;
- acceleration over defined intervals;
- top-speed trend on suitable route;
- coast-down;
- braking distance;
- brake balance;
- wheel lock/slip;
- hill-start behavior when practical.

### Lateral dynamics

- steering response;
- turning radius;
- low-speed maneuvering;
- understeer/oversteer tendency;
- lateral slip;
- recovery;
- surface differences.

### Suspension and contact

- static compression;
- travel;
- damping;
- rebound;
- response to bumps;
- wheel contact continuity;
- body roll;
- pitch under braking/acceleration;
- no unstable oscillation;
- no NaN/explosive forces.

### World integration

- road-surface metadata;
- bridge/driveway continuity;
- curb/ditch interaction;
- terrain transitions;
- garage entry;
- collision layers;
- vehicle reset/recovery;
- streaming boundaries while driving.

## Calibration framework

Create or align:

- `VehicleCalibrationProfile`;
- `VehicleCalibrationFixture`;
- `VehicleMetricDefinition`;
- `VehicleMetricResult`;
- `VehicleReferenceTarget`;
- `VehicleCalibrationRun`;
- `VehicleCalibrationComparator`;
- `VehiclePhysicsValidationDashboard`.

Every tuning change must record:

- parameter;
- old value;
- new value;
- reason;
- target fixture;
- result before;
- result after;
- side effects;
- confidence.

Do not create hidden tuning values in scene components.

## Automated runs

Where practical, support scripted input for repeatable tests.

Provide:

- start/idle run;
- acceleration run;
- braking run;
- coast-down run;
- steering step or slalom run;
- suspension bump run;
- surface comparison run;
- garage-clearance run.

Export telemetry and summary data.

Do not claim perfect repeatability if PhysX introduces variance.

Use repeated trials and statistical summaries where needed.

## Allowed fixes

You may tune:

- simulation configs;
- force curves;
- damping;
- masses when validated;
- center of mass;
- timestep/substeps within performance budget;
- wheel-backend bugs;
- surface metadata;
- collision defects directly blocking validation.

Do not broadly rewrite the world, assembly system, player, UI, or audio.

If a validation failure proves an architectural defect, document it and make
the smallest justified change.

## Tests

Add tests for:

- no invalid numeric state;
- expected RPM/speed relation;
- repeated-run tolerance;
- braking monotonicity;
- suspension stability;
- surface-friction ordering;
- center-of-mass validation;
- world-surface lookup;
- streaming-boundary continuity;
- garage-clearance fixture;
- calibration-profile serialization.

## Performance

Measure the cost of:

- baseline simulation;
- selected substep count;
- telemetry off;
- telemetry on;
- scripted validation;
- custom versus simple wheel backend if both exist.

A tuning result that cannot meet the performance budget must be reported.

## Output

Create:

- `Docs/VehicleValidation/VALIDATION_PLAN.md`;
- `Docs/VehicleValidation/CALIBRATION_TARGETS.csv`;
- `Docs/VehicleValidation/CALIBRATION_RUNS.csv`;
- `Docs/VehicleValidation/METRIC_RESULTS.csv`;
- `Docs/VehicleValidation/TUNING_CHANGE_LOG.csv`;
- `Docs/VehicleValidation/WORLD_INTEGRATION_REPORT.md`;
- `Docs/VehicleValidation/PERFORMANCE_REPORT.md`;
- `Docs/VehicleValidation/KNOWN_DEVIATIONS.md`;
- `Docs/Milestones/MILESTONE_06A_REPORT.md`.

## Definition of done

1. A repeatable validation setup exists.
2. Metrics and target types are explicit.
3. Available reference fixtures are evaluated.
4. Tuning changes are logged.
5. The vehicle is stable on the required vertical-slice route.
6. No invalid numeric behavior remains in tested scenarios.
7. Performance is measured.
8. Known deviations are honest.
9. The report gives a go/no-go for weather/audio/UI integration.

## Final response

Report:

1. Validation setup.
2. Reference targets.
3. Metrics executed.
4. Before/after results.
5. Tuning changes.
6. World integration.
7. Tests.
8. Performance.
9. Known deviations.
10. Files changed.
11. Manual tests required.
12. Go/no-go for Milestone 07.

Stop after Milestone 06A.
