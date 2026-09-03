# Weather System Wwise Integration

## Boundary

`WeatherAudioController` calls only the project-owned `IAudioBackend`. It does
not reference Ak types or vendor object names. The existing
`AudioBackendRouter`, Unity fallback, and official Wwise backend continue to own
backend selection, SoundBank readiness, emitter registration, and final value
caching.

The controller samples global and local weather at 8 Hz by default, applies
exponential smoothing, then uses an additional `0.008` normalized deadband.
Rain/wind loop start and stop values use hysteresis. Environment and day-phase
states change only when crossing a state boundary.

## Existing verified project mappings

These names and ranges were read from the current project maps; no replacement
Wwise names were invented.

| Project parameter | Wwise RTPC | Range |
|---|---|---|
| `audio.parameter.weather.precipitation` | `MSC_Weather_Precipitation` | 0..1 |
| `audio.parameter.weather.wind` | `MSC_Weather_Wind` | 0..1 |
| `audio.parameter.weather.thunder_risk` | `MSC_Weather_ThunderRisk` | 0..1 |
| `audio.parameter.environment.shelter` | `MSC_Environment_Shelter` | 0..1 |
| `audio.parameter.environment.time_of_day` | `MSC_Environment_TimeOfDay` | 0..1 |
| `audio.parameter.lightning.intensity` | `MSC_Lightning_Intensity` | 0..1 |
| `audio.parameter.lightning.distance` | `MSC_Lightning_DistanceMeters` | 0..5000 m |
| `audio.parameter.lightning.delay` | `MSC_Lightning_DelaySeconds` | 0..20 s |

Weather switch group `MSC_Weather_PrecipitationType` uses `None_01`, `Drizzle`,
and `Rain`. The Snow value exists in the shared map for future compatibility but
the Finnish summer system does not schedule snow.

Environment state group `MSC_Environment` uses `Exterior`, `Sheltered`,
`Interior`, and `VehicleInterior`. Day group `MSC_Time_DayPhase` uses `Dawn`,
`Day`, `Evening`, and `Night`.

The `MSC_Weather` bank maps:

- `Play_MSC_Weather_Rain_Exterior`
- `Play_MSC_Weather_Rain_Sheltered`
- `Play_MSC_Weather_Rain_Interior`
- `Play_MSC_Weather_Wind`
- `Play_MSC_Weather_Thunder`
- `Play_MSC_Weather_Thunder_Distant`
- `Play_MSC_Weather_Thunder_Strike`

## Local transmission

Global precipitation, wind, and thunder remain authoritative. The listener
result applies separate local channels:

- rain loudness uses the greater of `AudioLeak` and
  `ExteriorAudioExposure`;
- wind uses `WindExposure`;
- thunder uses `ThunderExposure`;
- shelter is `1 - ExteriorAudioExposure`;
- obstruction is derived from `1 - AudioLeak`;
- reverb send increases with `InteriorDepth`.

Door audio transmission deliberately rises faster than precipitation and fog.
A closed exterior door therefore leaves muffled weather and thunder audible;
opening the door continuously restores level/high-frequency presentation rather
than changing one binary `inside/outside` state.

Vehicle acoustics are preserved. If the existing listener context reports
`VehicleInterior`, the new controller retains that state instead of replacing it
with the building zone result. `VehicleLocalWeatherContext` exposes separate
listener-rain and body-rain values; only the listener value uses the existing
weather RTPC. Door/window and body-impact RTPCs remain owned by the vehicle
audio system because the audited Wwise maps do not yet define a dedicated
weather body-rain RTPC.

## Spatial Audio status

No configured `AkRoom`/`AkRoomPortal` authoring was found in the audited runtime
baseline, so the implementation does not fabricate Rooms, Portals, Auxiliary
Buses, or reflection routing. The scene validator reports a Wwise Portal with no
Room if those components are introduced later.

The project-owned weather portal graph is not a fake Ak spatial graph. A future
official Wwise Spatial Audio adapter may consume the same two-sided endpoint and
openness contracts after the Wwise project defines the required Rooms, Portals,
Aux Buses, and diffraction policy.

## Ownership hand-off

During staged migration, the new controller may hold a serialized reference to
the existing weather presenter. Suspension is opt-in, records the previous
enabled state, and restores it on disable. This prevents double rain loops and
keeps rollback deterministic.

Do not enable both weather presenters as active owners. Do not scale the listed
weather RTPCs to 0..100; the authoritative project map is normalized 0..1.
