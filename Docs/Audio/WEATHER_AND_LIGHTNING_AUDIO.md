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

Rain selects exactly one loop for the listener context:

- exterior rain;
- sheltered rain;
- interior/vehicle-interior rain.

Changing listener space stops the old handle before posting the new one. Rain
uses `0.01` start / `0.005` stop hysteresis. Wind intentionally starts only at
`0.18` and stops below `0.12`, so a calm non-zero weather output does not create
an always-audible wind bed. Disable/unsubscribe stops both handles.

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
active, the production weather exposure feeds a bounded fallback context:
Sheltered `0.65/0.20/0.15`, Interior `1.00/0.60/0.35` and VehicleInterior
`0.90/0.50/0.25` for shelter/obstruction/reverb. Wwise authoring applies up to
`-24 dB` wind attenuation from `MSC_Environment_Shelter`; rain selects mutually
exclusive exterior/sheltered/interior loops. Full physical acoustics, aux-send
calibration and portal diffraction remain later work.

## Validation

Focused weather-audio mapping has **7/7 EditMode tests passing**, including the
separate wind threshold, exposure fallback and immediate interior-rain
selection. The current Bootstrap WeatherProduction run has **7/7 functional
tests passing** plus one performance test skipped by explicit environment gate.
The bounded
performance harness measures the weather parameter batch at
`2.930 us/iteration`, `0 B` allocated. Live official Bootstrap smoke loads the
Weather bank without missing-bank diagnostics.

Headless execution cannot prove audible rain-space filtering, delayed-thunder
mix quality or Wwise audio-thread CPU. Those remain manual listening/profiler
limitations, not unverified runtime ownership.

Any donor rain/wind/thunder clip used for private prototype listening is
`TemporaryDirectImport`, hash-ledgered and ignored. Final weather audio is newly
authored.
