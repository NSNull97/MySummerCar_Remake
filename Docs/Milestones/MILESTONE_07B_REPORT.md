# Milestone 07B — Project-owned time/weather domain and Enviro 3 adapter

Дата: 2026-07-17. Статус: `ENGINEERING_GATE_PASS / AUTOMATED_PERFORMANCE_CAPTURED / MANUAL_CAPTURES_PENDING`.

## 1. Time architecture

`GameTimeService` является project-owned authority: integer ticks, fractional remainder, Gregorian date/day index, configurable day length, time scale, pause, scheduler и atomic DTO restore. Clock mutation откатывает state/revision/due queue при ошибке observer/scheduled callback; реентрантные state и scheduler mutations отклоняются до изменения данных. Enviro получает только presentation date/time и не free-run. DEV advance ограничен 30 игровыми сутками, предварительно валидируется и сохраняет pause одной транзакционной операцией.

## 2. Weather front и seed

`WeatherDirector` использует versioned PCG32 state, связную front/timeline topology, deterministic duration/successor selection, priority overrides и freeze без уничтожения schedule. Все текущие numerical targets маркированы `RemakeDesignTarget`.

## 3. Stable environment outputs

Добавлен vendor-neutral `WeatherEnvironmentOutputs` с clock, logical weather, accumulated wetness, listener exposure, typed audio/UI outputs и presentation status. Road/vehicle/vegetation/water/audio/UI production consumers остаются контрактами до 07C и не считаются подключёнными.

## 4. Enviro mapping

Enviro 3.x идентифицируется локальными hashes, semantic patch неизвестен. Clear/partly/overcast/fog используют typed assets. Drizzle/Rain/Heavy/Storm получают четыре отдельные adapter-owned runtime clones; exact `Rain` override emission dirty-only следует `PrecipitationIntensity01`. Source Rain/Storm и paid Lightning prefab не должны мутироваться. Transition duration переводится из игровых в simulation seconds по текущей скорости GameTime и отображается в приблизительный 99% exponential blend; coarse DEV jump сохраняет минимум один presentation interval гладкого пути.

Fingerprint migration с 07A baseline на canonical Unity 6 baseline описана в `Docs/Weather/ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md`; runtime stability и fresh-process preflight подтверждены.

## 5. Wetness/material prototype

`GlobalWetnessController` моделирует ground/road/puddle/vegetation accumulation/drying от отдельного `WetnessInput01`. WeatherLab global surface всегда exterior; camera/listener context не замораживает наружную влажность. Shader globals и shared-material-safe MPB samples покрывают asphalt, gravel, exterior, vehicle, vegetation и bounded puddle/ripple proxy максимум 12 Гц. Production material coverage не заявляется.

## 6. Lightning

Ambient presentation и gameplay strike разделены. Ambient request получает следующий общий presentation sequence, но не меняет gameplay fairness counters, cooldowns или выбор кандидата. Gameplay path использует deterministic candidate weighting, protection, cooldown, restore grace, thunder delay и typed effect hooks. Health/death contract отсутствует; режим 07B non-lethal. Enviro visual использует isolated runtime prefab/material.

## 7. Save DTOs

Созданы schema-1 DTO для game time, weather/front/timeline/RNG/overrides, wetness и lightning cooldown/fairness. Enviro objects, indices, instance IDs и VFX frames не сохраняются. Доменный restore валидируется до atomic commit; файловая persistence остаётся Milestone 09.

## 8. DEV tools

Окно: `Tools > MSC Remake > Time and Weather`, только WeatherLab Play Mode. Доступны time/date/scale/pause/advance, logical weather override/transition, freeze/seed, wetness, listener exposure, ambient/gameplay lightning, non-lethal flag, Low/High, validation/present, diagnostics и screenshot.

- diagnostics: `Logs/M07B_TimeWeatherDiagnostics.json`;
- captures: `References/Weather/Milestone07B/M07B_WeatherLab_<timestamp>.png`;
- performance target root: `PerformanceCaptures/Milestone07B/`.

## 9. Tests и captures

Финальные automated results:

- GameTime + WeatherDomain + WeatherPresentation: `101/101 PASS` — `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` (`20 + 29 + 52`);
- Enviro integration + WeatherLab: `13/13 PASS` — `TestResults/M07B_Enviro3Integration_Hardening_Final_03.xml`;
- targeted material/ripple smoke: `2/2 PASS` — `TestResults/M07B_WeatherLabRuntimeSmoke_Hardening_02.xml`;
- targeted time/front/callback edge cases: `3/3 PASS` — `TestResults/M07B_WeatherLabTime_TransactionalCallbacks_03.xml`;
- automated WeatherLab performance harness: `1/1 PASS` — `TestResults/M07B_WeatherLabPerformance_Final.xml`;
- WeatherLab builder `1.1.0` / full preflight: `PASS` — `Logs/M07B_WeatherLabBuilder_Final_03.log`;
- relevant `dotnet build` checks: project-owned errors `0`; Enviro-dependent build содержит только восемь известных vendor warnings.

Все новые 07B manual captures, включая transition, drying, mist, exposure, lightning separation, restore, wetness/ripple и Low/High: `PENDING`. Принятый 07A visual smoke не переименован в 07B evidence.

## 10. Performance

Создан отдельный PlayMode harness и актуальный ignored artifact `PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`: десять состояний, 15 warmup + 40 sampled frames, Editor PlayMode, D3D11, RTX 4070 SUPER, viewport 640x480. Frame/main-thread, GC и memory записаны; Render Thread/GPU markers и domain-only/subsystem decomposition честно отмечены unavailable. Это regression baseline, не Windows x64 1080p production sign-off.

## 11. Основные созданные/изменённые области

- `Assets/Game/Core/Runtime/Time/` и `IGameTimeService.cs`;
- `Assets/Game/Weather/Runtime/Domain/`, `Wetness/`, `Lightning/`, `Persistence/`;
- `Assets/Game/Weather/Presentation/Runtime/`;
- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3EnvironmentAdapter.cs` и bindings;
- `Assets/Game/Development/WeatherLab/Runtime/` и `Editor/TimeWeatherWindow.cs`;
- targeted EditMode/WeatherLab tests и отдельный PlayMode performance harness;
- документы `Docs/Weather/*07B*`, architecture/binding/wetness/lightning/DTO docs и этот отчёт.

Milestone commit включает только перечисленные 07B runtime/editor/test/docs области; пользовательский пакет 08A в scope и staging не входит.

## 12. Vendor files

От 07A aggregate отличается один canonical serialized prefab: `LightningStrike.prefab`, `-39` bytes. Source archive -> clean Unity 6 comparison совпал с current file. После runtime tests prefab и aggregate остались неизменными; fresh preflight подтвердил `538 / 305967931 / 8e376fa2…`. Functional vendor patch и неожиданный vendor drift отсутствуют.

## 13. Известные ограничения и риски

- manual visual captures отсутствуют;
- automated performance существует, но render/GPU/domain-only decomposition и 1080p development-build sign-off отсутствуют;
- production world rollout и consumers отсутствуют по границе milestone;
- Enviro transition и bolt intensity являются bounded approximations;
- Medium quality отсутствует;
- final material/shader coverage, shelter binding, water/underwater, windshield/wipers, wet-road tire calibration и Wwise content не входят в 07B;

## 14. Готовность к 07C

Engineering entry gate закрыт: актуальные tests, automated Editor performance baseline, stable vendor hash и fresh-process preflight прошли. Manual visual captures и production performance sign-off честно не заявляются; они остаются обязательной validation evidence, а не скрытым PASS. Единственный рекомендуемый следующий milestone — `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`.
