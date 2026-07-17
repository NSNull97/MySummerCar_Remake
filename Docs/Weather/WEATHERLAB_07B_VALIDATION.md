# WeatherLab 07B — журнал валидации

Дата среза: 2026-07-17. Статус engineering gate: `PASS`; automated Editor performance: `CAPTURED`; manual captures: `PENDING`.

## Финальные automated artifacts

| Набор | Артефакт | Результат | Статус для финального 07B |
|---|---|---:|---|
| GameTime EditMode | `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` | 20/20 PASS | Финальный срез, включая finite-rate, reentrancy, transactional callback и scheduler rollback |
| WeatherDomain EditMode | `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` | 29/29 PASS | Финальный срез, включая persistence policy, ambient/gameplay fairness, finite lightning clock и atomic schedule rollback |
| WeatherPresentation EditMode | `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` | 52/52 PASS | Финальный mapper/contracts/boundary срез |
| Объединённый vendor-neutral gate | `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` | 101/101 PASS | Финальный текущий срез |
| Enviro integration + WeatherLab | `TestResults/M07B_Enviro3Integration_Hardening_Final_03.xml` | 13/13 PASS | Финальный lifecycle, mappings, exact transition units, callback rollback, intensity, lightning и material/ripple smoke |
| WeatherLab material/ripple smoke | `TestResults/M07B_WeatherLabRuntimeSmoke_Hardening_02.xml` | 2/2 PASS | Дублирующий targeted proof |
| WeatherLab time/front/callback edge cases | `TestResults/M07B_WeatherLabTime_TransactionalCallbacks_03.xml` | 3/3 PASS | Дублирующий targeted proof |
| WeatherLab automated performance | `TestResults/M07B_WeatherLabPerformance_Final.xml` | 1/1 PASS | 10-state Editor PlayMode JSON; manual claim false |
| Builder + full preflight | `Logs/M07B_WeatherLabBuilder_Final_03.log` | PASS | builder `1.1.0`; `538 / 305967931 / 8e376fa2…`; warnings/errors validator отсутствуют |

`dotnet build` для GameTime, WeatherDomain, WeatherPresentation, WeatherLab Editor и Enviro integration test assemblies завершился без project-owned ошибок. В Enviro-dependent build остаются восемь известных vendor warnings; vendor source не правился.

Исторический baseline полного EditMode до 07B был `235/239 PASS` с четырьмя известными несвязанными Garage/World failures. Он не может скрывать новую regression.

## Обязательный финальный automated gate — PASS

- GameTime progression/pause/scale/day wrap/scheduler/DTO;
- callback failure rollback, pause-retaining DEV advance и запрет reentrant scheduler mutation;
- deterministic weather/RNG/front/override и invalid inputs;
- wetness accumulation/drying/exposure/DTO;
- ambient lightning не влияет на gameplay fairness, protection/cooldown/thunder/restore grace;
- environment output и mapper validation;
- distinct Drizzle/Rain/Heavy/Storm runtime clones;
- dynamic `Rain` emission update без transition restart и без source mutation;
- runtime lightning prefab/material isolation;
- WeatherLab actual scene smoke, low/high, lifecycle и material/ripple proof;
- compile boundaries без Enviro references в core/domain;
- vendor fingerprint до/после runtime tests и fresh preflight.

Fingerprint migration описан в `ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md`. Aggregate до/после runtime tests совпал; `LightningStrike.prefab` остался `10642` bytes / `F946C50A…E33A5`; fresh-process full preflight прошёл без ошибок и предупреждений.

## DEV/manual workflow

1. Открыть `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity`.
2. Войти в Play Mode.
3. Открыть `Tools > MSC Remake > Time and Weather`.
4. Проверить time/date, freeze/seed, logical override/transition, wetness, listener exposure, ambient/gameplay lightning, Low/High.
5. Нажать `Export diagnostics`: `Logs/M07B_TimeWeatherDiagnostics.json`.
6. Нажать `Capture Game View`: `References/Weather/Milestone07B/M07B_WeatherLab_<timestamp>.png`.

Окно вне Play Mode должно показать warning и не разыменовывать controller. Ноль или несколько active controllers, invalid input, missing binding/module и adapter fault должны быть видимы, а не молча игнорироваться.

## Manual/captures

- clear -> cloud -> rain: `PENDING`;
- rain -> drying: `PENDING`;
- morning mist/fog: `PENDING`;
- listener interior/exterior при продолжающейся exterior wetness: `PENDING`;
- ambient/gameplay lightning separation: `PENDING`;
- restore без clear-sky flash: `PENDING`;
- asphalt/gravel/exterior/vehicle/vegetation wetness: `PENDING`;
- bounded puddle/ripple proxy: `PENDING`;
- Low/High comparison: `PENDING`.

Принятый пользователем 07A smoke не выдаётся за новые 07B captures.

Automated performance artifact также не выдаётся за manual visual acceptance.
