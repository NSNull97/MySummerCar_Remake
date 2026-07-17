# Архитектура глобальной влажности

`GlobalWetnessController` — project-owned deterministic state machine. Он не читает Enviro и не сохраняет vendor runtime values как authority.

## Входы и выходы

На явном simulation delta controller получает:

- `WeatherState.WetnessInput01` — отдельный логический вход накопления;
- wind speed, temperature, sun intensity и weather drying modifier;
- `SurfaceExposureProfile`.

Он выдаёт bounded `[0,1]`:

- `GroundWetness`;
- `RoadWetness`;
- `PuddleAmount`;
- `VegetationWetness`;
- stable exposure profile ID и revision.

Визуальная `PrecipitationIntensity01` не подменяет `WetnessInput01`: профиль может калибровать накопление отдельно от количества видимых частиц.

## Exposure model

Domain поддерживает `Exterior`, `Sheltered`, `Interior` и `ShelterVolume`. Локальный профиль масштабирует precipitation/wind/sun/drying. В WeatherLab одна глобальная поверхность мира всегда продвигается с `SurfaceExposureProfile.Exterior`.

`WeatherExposureContext` в DEV окне относится к listener/camera. Переключение на interior camera не должно останавливать дождь и drying на всех внешних дорогах/земле. Будущие локальные interior/sheltered renderers получают отдельный профиль в 07C.

## Persistence и ошибки

`WetnessSaveDto` schema `1` хранит config ID, четыре канала и revision. Restore полностью валидирует schema/config/finite ranges до мутации. Файловая оркестрация остаётся Milestone 09.

Invalid delta, NaN/Infinity, отрицательные коэффициенты или неизвестный config/exposure ID не исправляются silent clamp вне явно документированных `[0,1]` state channels.

## Граница milestone

В 07B домен и WeatherLab bridge реализованы. Production road/terrain/buildings/vehicle/vegetation/water coverage, shelter binding и material-family audit относятся к 07C.
