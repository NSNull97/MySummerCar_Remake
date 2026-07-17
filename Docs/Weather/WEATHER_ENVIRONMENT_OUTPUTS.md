# Стабильные environment outputs

`WeatherEnvironmentOutputs` — immutable vendor-neutral read model. Он не содержит Enviro assets, scene objects, vendor indices или display names.

## Состав

- `Weather`: logical state/profile/front binding, cloud, precipitation, fog, visibility, wind/gust, temperature, readability, lightning, wetness input и drying modifier;
- `LogicalRevision`;
- `Clock`: calendar/day index и normalized day time `[0,1)`;
- `Wetness`: ground/road/puddle/vegetation и exposure profile ID;
- `ExposureContext`: listener/capture context `Exterior`, `Sheltered` или `Interior`;
- `Audio`: precipitation type/intensity, normalized wind и thunder risk;
- `Ui`: state ID, normalized time, temperature и precipitation intensity;
- `PresentationStatus`: stable quality tier ID, health и presented revision.

Дата/время поступают из `GameTimeSnapshot`; накопленная влажность — из `GlobalWetnessController`. Presentation health является read-only диагностикой и не записывается обратно в authoritative weather state.

## Exposure semantics

`ExposureContext` описывает listener/camera и будущие локальные precipitation/audio/UI реакции. В WeatherLab переключение камеры на интерьер не переводит общую поверхность мира в `Interior`: глобальные ground/road/puddle/vegetation продолжают симулироваться с `SurfaceExposureProfile.Exterior`. Локальные sheltered/interior surfaces должны получать собственный профиль, а не замораживать global wetness.

## Состояние потребителей

| Потребитель | 07B |
|---|---|
| Enviro presentation adapter | Реально подключён в WeatherLab |
| WeatherLab wetness/material bridge | Реально подключён в WeatherLab |
| Gameplay lightning | Реально подключён к domain hooks |
| Road/vehicle | Контракт; production consumer до 07C отсутствует |
| Vegetation/wind | Контракт; production consumer до 07C отсутствует |
| Water | Контракт; production consumer до 07C отсутствует |
| Audio backend | Типизированный output; production routing до 07C отсутствует |
| UI/HUD | Типизированный summary; финальный UI отсутствует |

Любой production consumer обязан читать project-owned output/событие. Прямой запрос `EnviroManager` запрещён.

## Валидация

Compose отклоняет пустые IDs, нулевую revision, invalid calendar/enum, NaN/Infinity и значения вне диапазонов. Ошибка presentation не отменяет корректное domain state; она отражается в status/diagnostics.
