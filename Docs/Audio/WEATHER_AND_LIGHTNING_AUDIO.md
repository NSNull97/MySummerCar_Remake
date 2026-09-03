# Weather and lightning audio

Status: **ProjectOutputMapped / EnviroAudioDisabled / AutomatedValidated**

## Single source of truth

`WeatherAudioPresenter` consumes only project-owned
`WeatherEnvironmentOutputs`, `LightningStrikeEvent` and
`ThunderAudioRequest` from `ProductionEnvironmentController`. It has no Enviro
reference. Enviro time, weather scheduling, lightning and audio are disabled in
production, preventing duplicate rain, wind, ambience and thunder.

## Continuous weather

Precipitation, wind and thunder-risk parameters are clamped project outputs.
Precipitation type maps to project switch values `None`, `Drizzle` or `Rain`
(`Snow` is declared for backend data but the current mapper does not emit it).

In the production hybrid graph, rain keeps one exterior event handle alive
while the existing configured `MSC_Weather_Precipitation` value is multiplied
by the continuous local weather-audio exposure. This avoids an audible event
restart at an interior boundary. The same resolver writes the existing
`MSC_Environment_Shelter = 1 - exposure`; authored Wwise curves already use it
for wind and thunder. No additional hybrid-weather RTPC or event is required.

The legacy/no-resolver fallback still selects one exterior, sheltered or
interior/vehicle-interior rain loop. Rain uses `0.01` start / `0.005` stop
hysteresis. Wind intentionally starts only at `0.18` and stops below `0.12`, so
a calm non-zero weather output does not create an always-audible wind bed.
Disable/unsubscribe stops both handles.

## Lightning and thunder

`LightningStrikeEvent` updates the presentation intensity once per increasing
sequence. `ThunderAudioRequest` supplies world position, intensity and
project-calculated delay. The presenter derives listener distance once, sets
intensity/distance/delay parameters, and posts a delayed spatial thunder event.
Duplicate/out-of-order sequences are rejected.

The general `audio.event.weather.thunder` is the current runtime event. Distant
and strike-specific event IDs are declared and authored as future selection
hooks but are not selected by the current presenter; the matrix marks them
`DeferredHook`.

## Interior filtering

Shelter is represented by listener state plus a normalized shelter parameter.
An explicit `AudioEnvironmentZone` remains authoritative. When no audio zone is
active, the production weather exposure feeds a continuous bounded context for
shelter, obstruction and reverb. Wwise authoring applies up to `-24 dB` wind
attenuation from `MSC_Environment_Shelter`; rain attenuation reuses the authored
precipitation RTPC instead of switching loops. The Unity fallback applies the
same existing shelter value to active rain, wind and thunder voices.

Door/window openness remains a project-owned portal input. Until a location has
authored portals, its existing zone profile is used as-is. Full physical
acoustics, aux-send calibration and portal diffraction remain later work.

## Validation

The final focused suites have **44/44 EditMode tests passing** across hybrid
exposure, weather-audio mapping, transition mapping and runtime audio, plus
**12/12 PlayMode tests passing** for runtime audio and the Unity fallback. A
targeted Bootstrap lifecycle/audio run also has **5/5 PlayMode tests passing**.
Live official Bootstrap smoke loads the Weather bank without missing-bank
diagnostics.

Headless execution cannot prove audible rain-space filtering, delayed-thunder
mix quality or Wwise audio-thread CPU. Those remain manual listening/profiler
limitations, not unverified runtime ownership.

Any donor rain/wind/thunder clip used for private prototype listening is
`TemporaryDirectImport`, hash-ledgered and ignored. Final weather audio is newly
authored.
