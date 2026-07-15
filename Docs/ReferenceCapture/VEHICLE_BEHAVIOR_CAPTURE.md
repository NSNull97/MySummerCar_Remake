# Checklist: vehicle behavior и powertrain

Это reference capture, не реализация simulation. Все испытания записывают surface, weather, load, tire, fluids, damage, engine temperature и control inputs.

## Engine P0

- [ ] 5 cold starts и 5 warm starts: prerequisites, starter duration, success/failure and start RPM.
- [ ] Warm idle: 5 windows, RPM median/range, oscillation and accessory/load state.
- [ ] Stall: gear, clutch, throttle/load, RPM/condition and restart behavior; 5 trials.
- [ ] Redline/limiter and engine-braking reference only if safely observable.

## Drivetrain P0/P1

- [ ] Fitted tire circumference established first.
- [ ] 5 steady RPM/speed samples per forward gear and reverse; neutral/indexing checked.
- [ ] Gear ratios and final drive derived with dependencies and uncertainty.
- [ ] Shift delay, clutch bite/slip and engine-braking observations.

## Steering/suspension P0

- [ ] Straight/full-left/full-right wheel angles and steering input positions.
- [ ] Static ride height, compression/rebound travel and response timing at named markers.
- [ ] Load/alignment/tire state held fixed; 5 repetitions.

## Acceleration/braking P0

- [ ] 7 valid 0–40 and 0–80 km/h trials.
- [ ] 7 valid 40–0 and 80–0 km/h trials with distance and time.
- [ ] Median/range reported; invalid trials retained with reason.

## P1/P2

- [ ] Tire grip/slip on asphalt, gravel and wet surface with controlled differences.
- [ ] Surface response, damage/wear thresholds, temperature and fluid/fuel consumption.
- [ ] No claim of deterministic PhysX equivalence or exact formula from behavioral observations.

## Acceptance

- [ ] Engine, gearbox, steering/suspension and braking/acceleration fixtures are no longer `Missing`.
- [ ] Derived ratios reference fitted tire and raw trial records.
- [ ] Subjective handling notes are observations, not exact coefficients.
