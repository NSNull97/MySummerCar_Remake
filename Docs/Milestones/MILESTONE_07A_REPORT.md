# Milestone 07A — Enviro 3 preflight и WeatherLab

Дата: 2026-07-17
Статус: **реализация и автоматизированная проверка завершены; пользователь принял ручной visual smoke капель Rain/Storm после исправления. Screenshot captures, Low/High comparison и performance capture остаются `PENDING`**.

Milestone выполнен в границах `Prompts/07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`. Автономное игровое время, планировщик погоды, накопленная влажность, gameplay-урон молнией, production rollout и другие задачи 07B/07C не реализовывались.

## 1. Наличие и версия Enviro

- Базовый Enviro установлен локально в `Assets/Enviro 3 - Sky and Weather/`.
- Дополнительный Weather Pack не импортирован и для 07A не требуется.
- Установка содержит 538 файлов общим размером 305 967 970 байт.
- Канонический fingerprint установки: `9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3`.
- Точный patch-level пакета нельзя доказать одним непротиворечивым локальным источником: заголовок сообщает `3.0.0`, changelog содержит записи до `3.0.8`, а inspector показывает `3.0.7`. Поэтому версия честно классифицирована как Enviro 3 с точным patch-level `UNKNOWN`.
- Полностью прочитан локальный `Enviro Documentation.pdf` из 28 страниц и проверены доступные C# API/asmdef/profile assets.
- Unity: `6000.3.11f1`; активный render pipeline проекта — HDRP `17.3.0`.

Для компиляции находящихся в vendor-пакете URP shader variants добавлена официальная зависимость URP `17.3.0`. Она не назначена активным pipeline asset: Graphics/Quality продолжают использовать HDRP, `ENVIRO_URP` не включён, а Enviro зарегистрирован в HDRP custom post-process list.

Подробности: `Docs/Weather/ENVIRO3_LOCAL_INSTALLATION_AUDIT.md`, `Docs/Weather/ENVIRO3_VERSION_EVIDENCE.md` и `Docs/Weather/ENVIRO3_COMPATIBILITY_BLOCKER.md`.

## 2. Обнаруженные API и модули

Проверены публичные типы Enviro manager/configuration и модули Time, Sky, Lighting, Volumetric Clouds, Flat Clouds, Fog, Weather, Effects, Environment, Lightning, Audio, Quality и HDRP renderer. Интеграция использует только подтверждённые типизированные API:

- явную передачу даты/времени при `Time.Settings.simulate = false`;
- `ChangeWeatherInstant(...)` для стабильных project-owned weather binding IDs;
- явный project-owned binding на базовый Effects preset с ключом `Rain`;
- типизированное переключение quality preset;
- явный visual-only lightning request;
- HDRP custom post-process `Enviro.EnviroHDRPRenderer`;
- явное назначение камеры без поиска по имени.

Полная карта API и сборок: `Docs/Weather/ENVIRO3_PUBLIC_API_EVIDENCE.md` и `Docs/Weather/ENVIRO3_FILE_AND_ASSEMBLY_MAP.md`.

## 3. Изменения vendor-файлов

Итоговое число изменений исходников и assets Enviro: **0**. Финальный preflight повторно получил исходные 538 файлов, 305 967 970 байт и fingerprint `9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3`.

Во время реального lightning smoke test был обнаружен нежелательный побочный эффект vendor API: `Lightning.cs` изменял `_Intensity` общего `planeMat`. Preflight остановил проверку по drift. Материал был восстановлен из идентичной неизменённой локальной копии того же приобретённого пакета, после чего adapter получил собственный runtime-clone материала и временно подавляет vendor thunder audio. Повторный тест подтвердил, что vendor material и общий fingerprint больше не меняются. Vendor-код не патчился.

Предупреждения `CS0618` внутри Enviro оставлены как vendor debt: они не являются ошибками компиляции и не исправлялись в соответствии с запретом vendor patches.

## 4. Владение окружением и конфликты

WeatherLab запускается независимо от Bootstrap и содержит по одному владельцу каждой presentation-области:

- Enviro владеет sky, sun/moon, clouds, fog presentation, exposure, ambient lighting, rain effects, wind и visual lightning;
- project-owned frame остаётся источником логического времени и команд;
- Enviro audio отключено;
- gameplay lightning, wetness, saves и scheduling остаются за project-owned системами будущих milestones;
- HDRP native sky/cloud owners не запускаются параллельно с Enviro;
- reflections остаются отдельным неразрешённым пунктом для измерения cadence/performance, без ложного refresh contract.

Матрица: `Docs/Weather/ENVIRONMENT_OWNER_MATRIX.csv`.

## 5. Граница adapter/assembly

Создан vendor-neutral контракт `MSC.Weather.Presentation.Runtime`, который не ссылается на Enviro. Прямая runtime-зависимость от `Enviro3.Runtime` сосредоточена в одной узкой сборке `MSC.Weather.Enviro3Integration`; отдельные Editor tooling и Editor-only tests также явно изолированы и проверяются preflight.

Adapter принимает immutable presentation frame с project-owned stable binding IDs, валидирует его, применяет типизированные Enviro assets, не запускает автономные clock/weather/gameplay systems и корректно освобождает runtime clones. Capabilities объявляются явно; неподдерживаемый reflection refresh не симулируется.

Архитектура: `Docs/Weather/ENVIRO3_ADAPTER_BOUNDARY.md`.

## 6. Содержимое WeatherLab

Создана non-shipping сцена `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity`, не зависящая от donor map. В ней есть:

- площадки ground/asphalt/gravel/dirt и небольшая depression;
- water proxy, opaque/transparent material samples;
- простое exterior/interior строение с дверным и оконным проёмами;
- разрешённый vegetation sample и vehicle proxy;
- три камеры и шесть именованных capture anchors;
- exterior/interior fixtures, headlights и performance marker;
- ровно один Enviro manager, один adapter и один HDRP Volume с WeatherLab profile;
- controller для явного выбора smoke state/quality/camera/lightning;
- hard guard, исключающий сцену из shipping Build Settings.

Сцену и profiles детерминированно строит Editor tool `WeatherLabBuilder`. Инструкция: `Docs/Weather/WEATHERLAB_SETUP.md`.

## 7. Проверенные smoke states

Автоматизированный smoke test открыл настоящую WeatherLab scene в Play Mode и проверил:

- clear;
- overcast;
- rain;
- storm visual;
- night;
- mist/fog;
- low и high quality mapping;
- visual lightning request;
- остановленное автономное время;
- беззвучный Enviro audio policy;
- одного Enviro manager, один активный directional environment light и один global Volume.
- реальное создание базового `Rain` ParticleSystem, ненулевой emission, запуск системы и появление частиц после нескольких кадров.

Результат headless smoke после rain remediation: **PASS**. Ручная проверка 2026-07-17 подтвердила корректное общее отображение, отсутствие неприемлемого перекрытия fog, видимую visual lightning и капли Rain/Storm после исправления. До remediation капли дождя отсутствовали. Причина: `Rain.asset` и `Storm.asset` управляют точным effect key `Rain`, а Effects subasset внутри default configuration содержал только отсутствующие ссылки optional Additional Weather Pack и другие имена `Rain - ...`. Adapter теперь клонирует явно назначенный базовый `Default Effects Preset.asset`, не изменяя vendor content. Screenshot captures и отдельный Low/High comparison остаются `PENDING`; ручное подтверждение без артефакта не выдаётся за сохранённый capture.

## 8. Fog/water strategy

В WeatherLab единственным presentation-владельцем fog является Enviro adapter/module. HDRP `Fog` используется как управляемый output substrate, а не как второй самостоятельный погодный контроллер. Native HDRP sky/cloud presentation параллельно не включается.

Water proxy остаётся тестовой HDRP-прозрачной поверхностью для проверки fog/transparent ordering, depth и ночной читаемости. Vendor patch, production water, swimming, buoyancy и финальные puddles не вводились. Пользователь вручную подтвердил, что fog нигде неприемлемо не перекрывается; screenshot artifact при этом отсутствует. Поэтому WeatherLab fog/transparent composition имеет статус `MANUAL_PASS_BY_USER_CAPTURE_MISSING`, а production-world и underwater compatibility остаются неподтверждёнными.

Решение: `Docs/Weather/FOG_AND_WATER_COMPATIBILITY.md`.

## 9. Политика audio и lightning

- Enviro в 07A полностью беззвучен: runtime audio masters/modifiers/clips не используются, а LateUpdate повторно закрепляет silent state.
- Временное Unity audio backend и будущий Wwise-контракт остаются project-owned.
- Enviro lightning используется только как визуальная презентация по явному sequence-keyed запросу.
- Место удара, gameplay damage/fire/electrical effects и детерминированный scheduling не делегируются Enviro.
- Общий vendor lightning material никогда не передаётся на запись: adapter создаёт один переиспользуемый runtime clone, освобождает его при teardown и подавляет vendor thunder на время запроса.

## 10. Тесты и captures

Фактически выполнено:

- Initial pre-remediation baseline `TestResults/M07A_Weather_EditMode_Final.xml`: **56/56 PASS** — vendor-neutral contracts, assembly boundary, persisted VolumeProfile, bindings, repeated-lightning material bound, cloud-module teardown, build exclusion и прежний actual WeatherLab runtime smoke; этот smoke ещё не проверял появление частиц.
- `TestResults/M07A_EnviroIntegration_EditMode_4.xml`: **7/7 PASS** — отдельный финальный integration run.
- Pre-remediation baseline `Logs/M07A_WeatherLab_Final_4.log` и `Logs/M07A_Final_Preflight_5.log`: прежний builder/preflight **PASS**; `cameras=3`, `anchors=6`, `buildSettings=excluded`, `donorBaseline=absent`, `owners=single`, `integrationAssemblies=3`. Повторный builder сохранил SHA-256 VolumeProfile без изменений (`d69c1bbd3aa5eea733e7ee89f6bdf2a478b9ea02f8507d7dd96a113b134c4cd6`).
- `Logs/M07A_Enviro3PreflightDiagnostics.json`: `passed=true`, без errors и warnings.
- `TestResults/M07A_EnviroIntegration_RainFix_Final.xml`: **9/9 PASS** — включая explicit Effects source, runtime isolation и фактический запуск Rain ParticleSystem с появившимися частицами при `Rain` (`0.5`) и `Storm` (`1.0`).
- `TestResults/M07A_WeatherPresentation_RainFix.xml`: **48/48 PASS**; суммарный targeted результат после remediation — **57/57 PASS**.
- `Logs/M07A_WeatherLab_RainFix.log` и финальный `Logs/M07A_Final_Preflight_RainFix_2.log`: builder `1.0.1` и fresh-process preflight после Rain/Storm runtime smoke **PASS**; vendor snapshot остался `538 / 305967970 / 9a4e8bab...`, errors/warnings отсутствуют.
- Полный post-remediation EditMode run `TestResults/M07A_EditMode_RainFix.xml`: **235/239 PASS**; ровно те же четыре несвязанных Garage/World failure, новых regression нет.
- Initial pre-remediation полный EditMode run `TestResults/M07A_EditMode.xml`: **234/238 PASS**, четыре уже существовавших несвязанных failure:
  1. `GaragePrototypeContentTests.LightingPreset_UsesPhysicalSkyFogFixedExposureAndAces`;
  2. `GaragePrototypeContentTests.Validator_ReportsNoMilestoneThreeErrors`;
  3. `WorldValidationEditModeTests.CurrentWorld_ReportsExactCoverageAndAchievesOnlyPilotGate`;
  4. `WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan`.

Garage/world assets не изменялись в рамках 07A, поэтому эти failures не маскировались и не исправлялись вне milestone scope.

Ручной visual smoke: **PASS_BY_USER_CAPTURE_MISSING** для общего отображения, WeatherLab fog composition, lightning и капель Rain/Storm после исправления. Отдельный Low/High comparison не подтверждён. Screenshots/video captures: **PENDING**. Индекс: `Docs/Weather/WEATHERLAB_CAPTURE_INDEX.csv`.

## 11. Performance

Performance capture на целевом Windows PC пока не выполнялся, поэтому FPS, CPU/GPU frame time, allocations и memory здесь не выдумываются. WeatherLab содержит probe/marker и подготовленную таблицу для будущего измерения. Статус и процедура: `Docs/Weather/WEATHERLAB_PERFORMANCE.md`.

## 12. Ручные шаги и ограничения

Остаётся выполнить в Unity Editor:

1. Открыть `Tools > MSC Remake > Enviro 3 Preflight > Open Dashboard` и убедиться, что dashboard зелёный.
2. Открыть WeatherLab через `Tools > MSC Remake > Enviro 3 Preflight > Open WeatherLab`.
3. При подготовке evidence повторить `Rain` и `Storm` на фиксированных High/Low anchors и сохранить exterior/interior/vehicle captures; ручной High visual retest уже принят пользователем.
4. Отдельно зафиксировать screenshot evidence для уже принятого fog/lightning и проверить night headlights, storm cleanup, exposure и wind sample.
5. Заполнить `Docs/Weather/WEATHERLAB_CAPTURE_INDEX.csv` реальными capture paths/status.
6. Выполнить performance capture по `Docs/Weather/WEATHERLAB_PERFORMANCE.md`.

Известные ограничения перечислены в `Docs/Weather/KNOWN_ENVIRO_LIMITATIONS.md`. Ключевые из них: неоднозначный patch-level Enviro, vendor `CS0618` warnings, отсутствующие capture/performance artifacts, неподтверждённый Low/High comparison, ещё не измеренный reflection cadence и отсутствие production rollout.

## 13. Готовность к 07B

Проект компилируется, Enviro изолирован от core contracts, WeatherLab не попадает в shipping build, automated precipitation smoke/preflight проходят, а ручной High retest капель принят пользователем. Архитектурный вход в `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md` готов: project-owned logical domain сможет подавать кадры в существующий presentation contract без прямой зависимости от Enviro. Полный evidence gate 07A остаётся открытым до сохранения captures, Low/High comparison и performance measurements.

Рекомендуемый следующий milestone: **только `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md`**.

## Созданные и изменённые области

- `Assets/Game/Weather/Presentation/Runtime/` — vendor-neutral contracts и asmdef;
- `Assets/Game/Weather/Enviro3Integration/Runtime/` — Enviro adapter/bindings и asmdef;
- `Assets/Game/Weather/Enviro3Integration/Editor/` — builder, preflight, dashboard и build guard;
- `Assets/Game/Development/WeatherLab/` — сцена, profiles, материалы, controller, capture/performance helpers;
- `Assets/Game/Tests/EditMode/WeatherPresentation/` — contract tests;
- `Assets/Game/Tests/EditMode/Enviro3Integration/` — integration/build/runtime scene tests;
- `Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset` — регистрация Enviro HDRP renderer;
- `Packages/manifest.json`, `Packages/packages-lock.json` — official URP compatibility-only dependency;
- `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/ProjectSettings.asset` — inactive URP global settings mapping и Enviro/HDRP defines при сохранённом активном HDRP;
- `.gitignore` — локальные Enviro package roots;
- `Docs/Weather/` — installation/API/ownership/architecture/setup/capture/performance/limitations evidence;
- этот milestone report.

## Выполненные команды и проверки

- Unity batch-mode package resolve/compile;
- `WeatherLabBuilder.RunBatch` с build validation и diagnostics export;
- Unity Test Framework: полный EditMode run, targeted Weather/Enviro EditMode runs и actual-scene Play Mode lifecycle внутри EditMode fixture;
- vendor fingerprint/file count/byte count до и после runtime lightning;
- проверки asmdef dependency boundary, build exclusion, owner uniqueness, stable IDs и frame validation;
- JSON/CSV/asmdef parse validation и Git diff/status audit.
