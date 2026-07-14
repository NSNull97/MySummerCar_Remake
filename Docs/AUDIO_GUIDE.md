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

## Integration milestone

When Wwise is installed:

1. Record integration version.
2. Pin package/project version.
3. Keep generated banks outside normal source unless a later ADR says otherwise.
4. Implement the backend adapter.
5. Preserve Unity fallback for test scenes where practical.
6. Add automated validation for missing event mappings.
