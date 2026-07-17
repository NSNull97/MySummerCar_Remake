# Известные ограничения локального Enviro 3

Дата среза: 2026-07-17.

Актуальный 07B preflight использует canonical Unity 6 baseline
`538 / 305967931 / 8e376fa2…`. Исторический 07A fingerprint ниже сохранён как
аудитный факт; миграция и отсутствие последующего drift доказаны в
`ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md`.

## 1. Exact version неизвестна

`version.txt` одновременно содержит заголовок `3.0.0` и changelog до `3.0.8`; inspector показывает `3.0.7`. Используется hash-identified local payload без уверенного patch label. Подробнее: `ENVIRO3_VERSION_EVIDENCE.md`.

## 2. Нет прямого Unity 6/HDRP 17 metadata

C# compile на Unity `6000.3.11f1` повторно прошёл после integration scaffolding. Actual WeatherLab headless PlayMode smoke также прошёл; пользователь вручную принял общее отображение, fog, lightning и капли Rain/Storm без capture artifacts. Low/High comparison и development-build performance ещё не доказаны. Vendor code содержит obsolete API warnings.

## 3. URP является обязательной compatibility dependency

`Enviro3.Runtime.asmdef` безусловно ссылается на URP Runtime, Core RP Runtime и HDRP Runtime. Vendor URP shaders импортируются в HDRP-проекте. URP `17.3.0` установлен, но не является active pipeline; `ENVIRO_URP` выключен.

## 4. HDRP custom post process настроен, ручной visual smoke подтверждён частично

`EnviroHDRPRenderer` существует, а serialized registration добавлена в:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Settings\HDRPDefaultResources\HDRenderPipelineGlobalSettings.asset`

Effective slot/duplicate validation, single owner и runtime scene lifecycle подтверждены automated preflight/test. Пользователь вручную подтвердил общее отображение, корректную композицию тумана, молнии и капли Rain/Storm после remediation, но capture artifacts отсутствуют и Low/High comparison не выполнен, поэтому полный evidence gate остаётся открытым. Headless test сам по себе не подтверждает пиксельный output renderer.

## 5. Player build не может сам найти default HDRP profile

`EnviroHelper.GetDefaultSkyAndFogProfile` использует `AssetDatabase.FindAssets` только под `UNITY_EDITOR`, а в player возвращает `null`. WeatherLab/production manager должен иметь заранее serialized `volumeHDRP` с project-owned profile.

## 6. Vendor shared profile может быть изменён ExecuteInEditMode code

Sky, Lighting и Fog modules вызывают `sharedProfile.Add<T>()` для `VisualEnvironment`, `EnviroHDRPSky`, `Exposure`, `IndirectLightingController` и `Fog`. Прямое использование vendor profile как mutable lab profile создаёт риск vendor contamination. Нужна project-owned development copy.

## 7. Default time free-runs

`Default Enviro Configuration.asset` содержит `Time.Settings.simulate: true`, cycle length 10 минут. Adapter обязан установить `simulate=false`; иначе Enviro становится конкурирующим clock owner.

## 8. Default audio не является silent

Ambient/weather/thunder master volumes равны `1`. Modifiers равны `0`, а vendor code складывает modifier с master volume. Ambient AudioSources могут начать playback в PlayMode. Silent policy требует выключения Audio module или runtime-clone master volumes `0` плюс validation.

## 9. Default configuration содержит missing Additional Pack references

Additional Weather Pack не импортирован, но default configuration пересекается с ним по 30 GUID files/references, включая 13 weather type assets и effect/audio dependencies. Default target weather указывает на отсутствующий `Fog - Mist Light`.

Integration должна:

- диагностировать null entries;
- использовать явные typed base bindings;
- не менять vendor configuration;
- не импортировать Additional Pack автоматически.

## 10. Additional Pack compatibility не доказана

Pack требует Enviro `3.0.7a+`; exact base patch неизвестен. Pack находится только во внешнем read-only каталоге и исключён из bounded 07A.

## 11. Singleton и lifecycle vendor-кода

`EnviroManager.instance` использует obsolete `FindObjectOfType`. Manager помечен `ExecuteInEditMode`; `OnEnable` распаковывает prefab instance в Editor. Project binding должен быть serialized, а validator — считать managers/volumes/lights после reload.

## 12. Cleanup требует проверки

Actual WeatherLab PlayMode test подтвердил scene-bound manager cleanup, повторные attach/detach, ровно один вызов cleanup live volumetric-cloud module и отсутствие double-destroy уже выгружаемых scene objects. Долгий repeated-reload memory/resource soak и GPU buffer profiling ещё не выполнялись.

## 13. Clouds имеют риск duplicate owner

Vendor HDRP profile сериализует HDRP-native `VolumetricClouds` component, тогда как Enviro рисует собственные volumetric clouds через custom post process. WeatherLab copy должна держать HDRP cloud type выключенным и валидировать одного presentation owner.

## 14. Fog и transparency проверены частично, capture/production criteria ожидаются

Enviro custom fog композитится в `AfterOpaqueAndSky`. Пользователь вручную подтвердил отсутствие перекрытия тумана в проверенных WeatherLab ракурсах, включая прозрачные прокси, но capture artifacts не сохранены. Headlights, production-world coverage и underwater rendering не проверены; shoreline, glass, interior leakage и sorting всё ещё требуют зафиксированных captures. Provisional owner — Enviro custom fog; fallback описан в `FOG_AND_WATER_COMPATIBILITY.md`.

## 15. Текущая вода временная

`WR_HomeShorelineWater.prefab` использует mesh + transparent HDRP/Lit material, не HDRP WaterSurface. Underwater rendering, swimming, buoyancy и final shore response отсутствуют. Нельзя экстраполировать WeatherLab fog proxy на final water system.

## 16. Quality API узкий

Публичного `SetQuality` метода нет; integration назначает `Quality.Settings.defaultQuality`. Low/High assets присутствуют, но реальные стоимость и визуальная разница ещё не измерены.

## 17. Reflection refresh потенциально дорогой

Enviro предоставляет forced и overtime refresh paths. Текущий bounded adapter намеренно не объявляет `EnvironmentRefresh`, а WeatherLab 07A отправляет `EnvironmentRefreshRequest.None`; vendor refresh API не вызывается. Выбор production cadence и performance capture остаются на следующий этап.

## 18. Weather preset semantics не стабильные IDs

Vendor предоставляет typed object, string name и array index overloads. Project runtime использует только serialized typed references, разрешённые через `EnvironmentBindingId`. String/index paths запрещены архитектурой.

## 19. Precipitation не имеет универсального normalized setter

Rain реализуется через weather/effects overrides. Adapter mapping для normalized project frame является bounded approximation и должен честно отражать capabilities/known differences.

## 20. Enviro wetness/snow не authoritative

Vendor Environment module содержит wetness/snow state, но project-owned накопление, drying, saves и material outputs ещё не реализованы. 07A не заявляет мокрые материалы или puddles.

## 21. PDF documentation review завершён для 07A

`Documentation.pdf` присутствует, захеширован, все 28 страниц извлечены и прочитаны bundled PDF runtime без установки внешних tools. Он подтвердил HDRP define/registration path; exact adapter calls дополнительно сверены с source signatures.

## 22. Исторический статус 07A: WeatherLab/preflight созданы

Adapter, binding asset type, builder, preflight, WeatherLab scene и generated content assets присутствуют. Итоговый 07A batch подтвердил 3 cameras, 6 anchors, shipping exclusion, отсутствие donor baseline, canonical vendor fingerprint и single owners; return code 0. Headless runtime smoke циклически применил clear/overcast/rain/storm/night/fog, Low/High и lightning request; post-remediation test также подтвердил работающие Rain particles. Пользователь принял общее отображение, fog composition, lightning и капли Rain/Storm без capture artifacts. Для 07A Low/High comparison и screenshot/performance artifacts оставались `PENDING`.

В 07B добавлен automated Editor PlayMode baseline по десяти состояниям (`M07B_WeatherLab_Performance.json`). Manual screenshots, standalone 1080p и реальные Render Thread/GPU measurements всё ещё не заявляются.

## 23. Встроенный performance probe ограничен CPU frame duration

`WeatherLabPerformanceProbe` собирает только sample count, average и max `unscaledDeltaTime` (по умолчанию 600 samples). Он не измеряет GPU/render thread, memory, GC, draw calls или percentile distribution. Полный development-build profiler capture из `WEATHERLAB_PERFORMANCE.md` остаётся обязательным.

## 24. Историческая transition policy 07A мгновенная

В 07A adapter применял `ChangeWeatherInstant`, а запрос transition duration больше нуля создавал warning. В 07B это ограничение снято: project-owned game duration переводится в simulation seconds, а adapter использует bounded exponential Enviro transition. Production-world подтверждение остаётся 07C.

## 25. Fingerprint зависит от canonical sorting

При одинаковых vendor count/bytes (`538` / `305967970`) ordinal sorting давал `ffc848d3...`, тогда как исходный ru-RU `Sort-Object` exporter даёт `9a4e8bab...`. Validator приведён к canonical сортировке и повторный preflight воспроизвёл baseline с return code 0. Алгоритм/locale остаются частью audit contract: их нельзя менять без миграции fingerprint.

## 26. HDRP Fog component является Enviro output substrate

Builder включает HDRP `Fog`/volumetrics в единственном project-owned profile. `EnviroFogModule.UpdateFogHDRP()` использует этот component как target при `controlHDRPFog/controlHDRPVolumetrics`; это не второй weather authority. Пользовательский WeatherLab smoke не выявил неприемлемых перекрытий, но screenshot evidence отсутствует, а production-world и underwater combination ещё не проверены.

## 27. Vendor Lightning мутирует shared flash material без adapter isolation

`Enviro.Lightning` вызывает `planeMat.SetFloat("_Intensity", ...)` напрямую. При обычном `CastLightningBolt` это может изменить serialized vendor material в Editor PlayMode; первый smoke именно так изменил 9 байт и был остановлен fingerprint gate. Adapter теперь подставляет один переиспользуемый runtime-only material clone до vendor instantiate, немедленно возвращает исходную prefab reference и временно отсоединяет Enviro Audio, чтобы public visual call не запускал thunder. Repeated-lightning test подтвердил bounded material count, а повторный preflight сохранил canonical vendor fingerprint.

## 28. Default configuration Effects несовместим с базовыми Rain/Storm profiles без явного preset

`Rain.asset` и `Storm.asset` управляют exact effect key `Rain`. Effects subasset внутри локального `Default Enviro Configuration.asset` содержит имена `Rain - ...` и prefab/preset GUID из неустановленного Additional Weather Pack, поэтому системы частиц не создавались, а emission оставался нулевым. Vendor content не исправлялся и Additional Pack не импортировался. Project-owned binding теперь явно назначает базовый `Default Effects Preset.asset`; adapter клонирует его, валидирует usable `Rain`, а runtime smoke подтверждает emission и появившиеся частицы. Пользователь принял пиксельную видимость капель после remediation; capture artifact и отдельный Low/High comparison отсутствуют.
