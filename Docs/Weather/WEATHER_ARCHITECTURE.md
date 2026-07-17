# Архитектура погоды — Milestone 07B

Дата среза: 2026-07-17.

## Поток владения

```text
GameTimeService
  -> WeatherDirector (seed/front/timeline/override)
  -> WeatherEnvironmentOutputs
     -> GlobalWetnessController
     -> LightningStrikeDirector
     -> WeatherEnvironmentFrameMapper
     -> Enviro3EnvironmentAdapter
     -> Enviro 3 presentation
```

Gameplay/core не знают типов Enviro. Enviro не планирует погоду, не двигает игровые часы, не является save authority и не выдаёт presentation state обратно за gameplay truth.

## Логические состояния

Стабильные project-owned IDs:

- `weather.clear`;
- `weather.partly_cloudy`;
- `weather.overcast`;
- `weather.drizzle`;
- `weather.steady_rain`;
- `weather.heavy_rain`;
- `weather.thunderstorm`;
- `weather.morning_mist`.

Профиль определяет cloud coverage, precipitation type/intensity, fog/visibility, wind/gust, temperature placeholder, readability, lightning risk/intensity, отдельный `WetnessInput01`, drying modifier, диапазоны hold/transition и допустимых соседей. Текущие значения — `RemakeDesignTarget`, поскольку donor captures `P0-WEATHER-TRANSITION` и `P1-WETNESS-DRYING` отсутствуют.

## Overrides

`WeatherOverride` содержит stable ID, owner, reason, priority, lifetime, requested state и serialization policy. Победитель выбирается детерминированно по priority и stable sequence. Удаление override раскрывает продолжающийся underlying schedule; timeline и RNG не сбрасываются.

Ручной WeatherLab override добавляется только через project-owned logical ID. Проверки gameplay по Enviro preset name запрещены.

## Границы 07B

В 07B фактическими потребителями являются WeatherLab material bridge, lightning domain и Enviro adapter. Road/vehicle, production vegetation, production water, audio backend и финальный UI получают типизированные контракты, но ещё не подключены к production-миру. Их rollout и проверка принадлежат 07C; документация не считает наличие DTO/выхода готовой интеграцией.

## Failure modes

- неизвестный logical/profile/binding/config ID отклоняется;
- invalid numeric outputs не доходят до presentation adapter;
- invalid predecessor/successor или исчерпание safety bound прерывает advance;
- отсутствие/дублирование Enviro owner, typed binding или runtime isolation переводит presentation в `Faulted`/`Degraded`, не меняя domain authority;
- removing override не имеет права разрушить schedule state;
- производственные потребители не должны обходить `WeatherEnvironmentOutputs` и читать `EnviroManager` напрямую.
