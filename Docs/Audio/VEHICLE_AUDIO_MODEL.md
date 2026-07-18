# Vehicle audio model

Status: **PrototypeImplemented / AutomatedValidated / UserAcceptedBoundedBaseline**

## Authority and update path

`VehicleSimulationHost` remains authoritative. `VehicleAudioPresenter` reads
simulation state/telemetry in `FixedUpdate`, sanitizes it into
`VehicleAudioParameters`, and sends it to `IVehicleAudioBackend`. Audio never
writes back to physics, drivetrain, assembly or save state.

The current contract carries engine state, RPM/redline, load, throttle, gear,
clutch slip, absolute and signed speed, aggregate wheel angular speed, maximum
wheel slip, brake, suspension-impact envelope, dominant contacted surface,
battery voltage and engine temperature. Damage, seat/interior blend and
door/window openness use explicit availability flags; unavailable is not
misrepresented as measured zero.

## Layer lifecycle

`VehicleAudioEmitterBackend` starts three non-overlapping engine handles while
the engine is cranking/running:

- intake;
- exhaust;
- mechanical.

A separate tire-roll handle starts from signed speed or aggregate wheel speed.
All continuous handles stop on state transition, reset, disable, emitter
unregister or backend identity change. Starter engage/disengage, engine
start/stop/stall and reset are typed transition events.

This is a layered architecture; it is not a final mix and does not claim that
one pitched clip solves the engine. Transmission/driveline, rattle, skid and
individual suspension one-shots have declared stable event IDs but remain
`DeferredHook`: the current vehicle adapter does not post them.

## Mapped parameters

- RPM and normalized RPM;
- load and throttle;
- selected gear and clutch-slip RPM;
- absolute vehicle speed and aggregate wheel angular speed;
- wheel slip, brake and smoothed suspension impact;
- battery voltage and engine temperature;
- optional damage, interior blend and openings;
- typed surface switch;
- engine state.

The dominant surface is selected from the contacted wheel with greatest normal
load and mapped from the project enum (`Unknown`, `Paved`, `Gravel`, `Dirt`,
`Grass`, `MudWet`). No object/collider name is inspected. Backend maps clamp and
deduplicate RTPC writes.

## Current content boundary

The seven existing Satsuma diagnostic files are hash-pinned
`TemporaryDirectImport` from frozen external staging. They may support private
prototype listening through the Editor fallback or ignored local Wwise
authoring, but are not committed and are not final content. Known gaps include
final authored recordings, load-dependent variations, intake/exhaust separation
quality, interior filtering, transmission/body/tire detail and calibrated mix.

The final vehicle soundscape must be newly authored and mixed. The architecture
must continue to work when every donor clip is absent.

## Validation status

Focused runtime mapping/lifecycle tests are included in the reported Audio
Runtime and Unity fallback totals. Official Wwise Bootstrap smoke loads all six
banks with no missing-bank report. The bounded performance harness measures the
vehicle parameter batch at `17.906 us/iteration`, `0 B` allocated; the complete
audio scenario peaks at 3 emitters/5 active voices and returns to the 2/0
baseline after cleanup. The private Development Player initializes Wwise and
packages matching banks.

For audible verification, the development-only scene
`Assets/Game/Audio/Content/Scenes/M08_VehicleAudioPlaytest.unity` additively
loads the existing `VehicleSimulationPrototype`, disables the old M06 local
diagnostic backend and binds its host/presenter/listener to the production
`AudioBackendRouter`. It is intentionally not added to production build
settings. Live-Wwise PlayMode verifies the authored scene and synthetic rebind
path **2/2 PASS**; the scene must still be driven and judged by ear.

Headless tests cannot prove the final audible engine blend. The user accepted
the bounded Milestone 08 baseline on 2026-07-18; detailed driving mix,
load/transmission/body calibration and Wwise Profiler audio-thread CPU remain
polishing work, not architecture blockers.
