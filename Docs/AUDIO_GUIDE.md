# Audio Architecture and Wwise Guide

## Strategy

Build gameplay against an `IAudioBackend` abstraction.

Early milestones use `UnityAudioBackend` so the project compiles and can be tested without Wwise. Add `WwiseAudioBackend` only after the official integration is installed and its version is pinned.

Do not create fake Wwise namespaces or stub official types.

## Main domains

- vehicle mechanical audio;
- vehicle body and suspension;
- surface interaction;
- player foley;
- tools and fasteners;
- weather;
- ambience;
- interiors and occlusion;
- UI;
- music/radio later.

## Vehicle parameters

Prepare normalized or physical parameters for:

| Parameter | Meaning |
|---|---|
| RPM | Engine rotational speed |
| EngineLoad | Requested/actual load |
| Throttle | Driver throttle input |
| Gear | Current gear index |
| ClutchSlip | Relative clutch slip |
| VehicleSpeed | Linear speed |
| WheelSlip | Per-wheel or aggregated slip |
| SurfaceType | Gravel, asphalt, dirt, grass, etc. |
| Damage | Mechanical damage intensity |
| InteriorBlend | Listener inside/outside blend |
| DoorOpenness | Acoustic opening state |
| WindowOpenness | Acoustic opening state |
| RainIntensity | Weather parameter |

## Engine layering

Plan layers for:

- intake;
- exhaust;
- mechanical valvetrain;
- transmission whine;
- starter;
- belt/squeal;
- body resonance;
- misfire or damage;
- interior filtering.

Do not rely on one loop with pitch shifted from idle to redline.

## Spatial audio

Plan:

- room/interior volumes;
- portals/openings;
- distance attenuation;
- occlusion via raycasts or geometry;
- reverb sends;
- interior/exterior listener states;
- rain-on-roof and rain-outside separation.

## Donor audio

Donor clips may be inventoried and used as reference or temporary prototype content with ledger entries. Final sound design should use newly authored or properly licensed sources.

### M06 local Satsuma diagnostic set

The user authorized a narrowly scoped auditory aid for the vehicle-simulation prototype. It is an Editor-only local diagnostic, not final content and not evidence that donor mixing or behavior has been reproduced.

The source is the frozen external AssetRipper export under donor staging, relative path `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/AudioClip`. Do not source these files from the current donor installation: that installation is mod-contaminated and its audited containers have hash drift. Do not copy the clips into tracked `Assets`, Git/LFS, a player build, package, or generated bank.

The Editor backend resolves the staging root only through ignored `Config/DonorPaths.local.json` field `DonorStagingDirectory`; no absolute machine path is serialized into code or scene content.

| Diagnostic role | Frozen clip | SHA-256 |
|---|---|---|
| Low/idle RPM layer | `850_idle5.ogg` | `41296478D8828AF8E7840FE66F9FE2C9A0F05D78127B2BFB20ED35C403894CF9` |
| Mid RPM layer | `850_mid3.ogg` | `EF0F7E94F7FB08B1D8F1EA16AEE7BDDE5A27F54FB12FB6357A26DB11493F1FA5` |
| High RPM layer | `850_mid13.ogg` | `464DA9DBCE2219040522A18B481B4E5C1C285303152F56975D167E5F54044480` |
| Starter event variant 1 | `motor_start_1.ogg` | `5F9EEB889C660E7AE1F08F9474951ECB3938878AE2A5C13038952A6DDD5892AE` |
| Starter event variant 2 | `motor_start_2.ogg` | `854EA1EECC2E9DBFC37674CA0968F3FFACE2F75AE8B85A73F06C40FD1198F5AB` |
| Starter event variant 3 | `motor_start_3.ogg` | `9D9ABE53A8C253E8C571FE1E99B8B34509B2944D6428FBFCA8412413FE551CD2` |
| Starter-whine layer | `starter_whine.ogg` | `215329D05D5C5C1BE5F0AE1B831AA20E01A02DD0418856C56808DE52CD310CE3` |

AssetRipper `1.3.14`, `Default` importer settings produced the frozen files. Static `GAME.unity` `AudioEngineSatsuma` references support the three RPM roles; `MasterAudio/Starting` supports the starter roles. This routing evidence is `ReferenceOnly`; the clips used by the local prototype are `TemporaryDirectImport`. Exact rows and hashes are in `Docs/Porting/PORTING_LEDGER.csv`.

No Satsuma-specific shutdown or stall clip is proven in the inspected mapping. The local diagnostic must fade running loops on engine stop/stall and must not invent a donor-parity event. Audio consumes telemetry only; it never drives start, stall, RPM, shift or physics state. Missing files, hash mismatch or non-Editor execution must fail silent and leave the simulation usable.

Concrete ownership:

- `Assets/Game/Audio/Runtime/VehicleAudioContracts.cs` defines `IVehicleAudioBackend` plus vehicle parameters/events without depending on donor types;
- `Assets/Game/Vehicle/Runtime/VehicleAudioPresenter.cs` maps typed simulation telemetry/state to the audio contract;
- `Assets/Game/Audio/UnityFallback/UnityAudioBackend.cs` owns Editor-only SHA verification, asynchronous external loading and diagnostic mixing;
- the prototype builder composes `M06_LocalDiagnosticVehicleAudio` as a child plus one presenter;
- the strict validator requires no serialized `AudioSource` or `AudioClip` dependency, keeping the scene and builds donor-payload-free.

Fresh automated evidence: strict validator PASS; focused EditMode `18/18` in `0.1090665 s`; focused PlayMode `4/4` in `10.8416789 s`; full PlayMode `31/31` in `49.4868179 s`. All four M06 cases reached `M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips=7 source=ExternalDonorStaging hashes=Verified` when run with the configured staging, proving that the external files were present, matched the pinned SHA-256 values and loaded locally. Missing/invalid local staging is an intentional silent fallback and does not by itself fail the simulation suite. On 2026-07-16 the user accepted the drive/audio result for the bounded basic prototype. This is prototype acceptance, not a donor-parity or production-audio claim.

## Integration milestone

When Wwise is installed:

1. Record integration version.
2. Pin package/project version.
3. Keep generated banks outside normal source unless a later ADR says otherwise.
4. Implement the backend adapter.
5. Preserve Unity fallback for test scenes where practical.
6. Add automated validation for missing event mappings.

## Milestone 06 telemetry source boundary

Milestone 06 does not implement final vehicle audio. It provides typed simulation telemetry that `VehicleAudioPresenter` samples in `FixedUpdate` and sends through `IVehicleAudioBackend` without querying the Rigidbody, wheel backend or scene object names. PlayMode asserts `StarterEngaged -> StarterDisengaged -> EngineStarted` ordering. Reset has an explicit one-way path: `VehicleSimulationHost.SimulationReset -> VehicleAudioPresenter -> VehicleAudioEvent.Reset`; reset during `Cranking` is tested, and `UnityAudioBackend` immediately executes `StopImmediately`. Production gameplay remains designed against backend interfaces; the Editor-only donor diagnostic must not become an implicit production dependency.

Available source mapping:

| Audio parameter | M06 source |
|---|---|
| RPM | `VehicleTelemetry.EngineRpm` |
| EngineLoad | `VehicleTelemetry.EngineLoad01` |
| Throttle | `VehicleTelemetry.Throttle01` |
| Gear | `VehicleTelemetry.SelectedGear` |
| ClutchSlip | `VehicleTelemetry.ClutchSlipRpm` |
| VehicleSpeed | `VehicleTelemetry.VehicleSpeedMetersPerSecond` |
| WheelSlip | `VehicleTelemetry.GetWheel(i).LongitudinalSlip/LateralSlip` |
| SurfaceType | `VehicleTelemetry.GetWheel(i).Surface` |
| Starter/running state | `VehicleSimulationState.EngineStatus` |
| Battery/temperature diagnostics | `VehicleTelemetry.BatteryVoltage` / `EngineTemperatureCelsius` |

`Damage`, `InteriorBlend`, openings and weather remain unavailable. `MudWet` is a config placeholder only. Any production `VehicleAudioPresenter` should normalize/rate-limit these values and send them to the existing backend boundary; it must not make audio callbacks authoritative for engine, shift or wheel state.

All current dynamic behavior and surface responses are `RemakeDesignTarget` / `ProvisionalProjectTuning`. The seven frozen clips provide only local perceptual feedback; there is no complete reviewed donor audio fixture, mixer/event graph, interior/exterior behavior or proven stop/stall mapping. Telemetry is therefore a stable project API, not proof of donor sound behavior.
