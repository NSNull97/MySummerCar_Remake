# Отчёт Milestone 01 — Unity 6 HDRP Foundation

Дата: 2026-07-13
Результат: **пройден** — foundation-код компилируется, автоматическая валидация проходит, все 12 EditMode-тестов проходят.

## 1. Что было проверено до изменений

- Открыт существующий Unity-проект `E:\GAYmDev_Studio\MySummerCar_Remake`; новый проект не создавался.
- Версия проекта и установленного Editor совпадает: `6000.3.11f1 (3000ef702840)`.
- В проекте уже были HDRP `17.3.0`, Input System `1.19.0`, Linear color space и HDRP assets для Graphics/Quality.
- До milestone существовала и была включена `Assets/OutdoorsScene.unity`.
- До milestone в `Assets/Game` не было игровых C#-реализаций и `.asmdef`.
- Рабочее дерево было загрязнено пользовательскими изменениями. Существующие сцена, HDRP assets и ProjectSettings были сохранены.
- `Config/DonorPaths.local.json` игнорируется Git и указывает на фактически открытый проект и соответствующий Unity Editor.
- Донорская установка, staging и legacy-reference находятся вне проекта; в этом milestone донор не читался и не изменялся.

## 2. Реализованный foundation

### Модули и границы сборок

Созданы 19 assembly definitions для Core, Bootstrap, Interaction, Player, Vehicle Runtime/Simulation/Assembly, World Runtime/Streaming, Weather, Audio Runtime/UnityFallback, Save Runtime/Migration, LegacyImport Runtime/Editor, общего Editor-кода и двух test assemblies.

Runtime assemblies не ссылаются на Editor assemblies. `AssemblyDefinitionValidator` проверяет как ссылки по имени, так и ссылки вида `GUID:...`.

### Composition root и сервисные контракты

Добавлены узкие первоначальные границы:

- `IGameTimeService`;
- `IWeatherService`;
- `ISaveService`;
- `IAudioBackend`;
- `IInteractionService`;
- `IEntityIdProvider`;
- `IWorldStreamingService`.

`GameCompositionRoot` не имеет статического singleton-доступа или глобального service locator. `GameServiceBindings` принимает все зависимости явно и запрещает `null`; повторная инициализация root запрещена. Конкретные/fake-сервисы намеренно не создавались: ими будут владеть профильные milestones.

### Стабильная идентичность

`StableEntityId` использует канонический 32-символьный lower-case GUID в формате `N`. `StableEntityIdAuthoring` не создаёт и не меняет ID в runtime, `Awake` или `OnValidate`.

Editor-команды разделены:

- выдача только отсутствующих ID в открытых сценах;
- явная регенерация ID выбранных объектов с предупреждением о миграции save data и подтверждением.

Validator проверяет отсутствующие, некорректные и повторяющиеся ID во всех сценах и prefab assets под `Assets/Game`, а также в несохранённых открытых сценах.

### Bootstrap и HDRP

Созданы:

- `Assets/Game/Bootstrap/Bootstrap.unity`;
- `Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset`.

Bootstrap является первой включённой build scene; существующая `Assets/OutdoorsScene.unity` сохранена второй включённой сценой. Bootstrap содержит только composition root, Directional Sun и Global Volume.

HDRP baseline содержит сериализованные sub-assets:

- Visual Environment с Physically Based Sky;
- Physical Sky с земным preset;
- фиксированную baseline exposure;
- ACES tonemapping;
- fog с включённой volumetric составляющей.

Солнце настроено как Directional Light с физической единицей `Lux` и интенсивностью `100000`. Ray tracing не включался.

### Локальная конфигурация и валидация

Добавлен Editor-only parser `DonorPathConfiguration`, который читает JSON, раскрывает environment variables, приводит пути через `Path.GetFullPath` и убирает лишний завершающий разделитель каталога. Абсолютные машинные пути не попали в runtime C#.

Foundation validator проверяет:

- стабильные ID;
- runtime-to-Editor assembly boundaries;
- наличие и корректность ignored local path configuration;
- совпадение `UnityProjectDirectory` с открытым проектом;
- наличие Bootstrap scene.

Доступны меню `Tools > My Summer Car > ...` и batch entry point `MSC.Editor.Foundation.FoundationValidationRunner.RunBatch`.

## 3. Созданные и изменённые файлы

Основной runtime:

- `Assets/Game/Core/Runtime/IGameTimeService.cs`;
- `Assets/Game/Core/Runtime/Identity/StableEntityId.cs`;
- `Assets/Game/Core/Runtime/Identity/StableEntityIdAuthoring.cs`;
- `Assets/Game/Core/Runtime/Identity/StableEntityIdValidation.cs`;
- `Assets/Game/Core/Runtime/Identity/IEntityIdProvider.cs`;
- `Assets/Game/Bootstrap/GameCompositionRoot.cs`;
- `Assets/Game/Bootstrap/GameServiceBindings.cs`;
- интерфейсы сервисов в `Assets/Game/{Interaction,World,Weather,Audio,Save}/Runtime`.

Assembly definitions и структура:

- 19 `.asmdef` под `Assets/Game`;
- добавлены отсутствовавшие целевые папки `Content`, `Animation`, `Streaming`, `Presentation`, `UnityFallback`, `Wwise`, `Migration`, `Manifests` и `Lighting`;
- Unity сгенерировал соответствующие `.meta`.

Editor tooling:

- `Assets/Game/Editor/Identity/StableEntityIdAuthoringTools.cs`;
- `Assets/Game/Editor/Validation/StableEntityIdProjectValidator.cs`;
- `Assets/Game/Editor/Validation/AssemblyDefinitionValidator.cs`;
- `Assets/Game/Editor/Foundation/FoundationSceneBuilder.cs`;
- `Assets/Game/Editor/Foundation/FoundationValidationRunner.cs`;
- `Assets/Game/LegacyImport/Editor/Configuration/DonorPathConfiguration.cs`.

Тесты:

- `Assets/Game/Tests/EditMode/Identity/StableEntityIdTests.cs`;
- `Assets/Game/Tests/EditMode/Identity/StableEntityIdValidationTests.cs`;
- `Assets/Game/Tests/EditMode/Configuration/DonorPathConfigurationTests.cs`;
- `Assets/Game/Tests/EditMode/Validation/AssemblyBoundaryTests.cs`;
- `Assets/Game/Tests/EditMode/Foundation/FoundationContentTests.cs`;
- EditMode и PlayMode `.asmdef`.

Unity content/settings:

- создана `Assets/Game/Bootstrap/Bootstrap.unity`;
- создан `Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset`;
- изменён только список сцен в `ProjectSettings/EditorBuildSettings.asset`;
- `Config/DonorPaths.example.json` обновлён на подтверждённый путь будущей игры.

Документация:

- обновлены `Docs/ARCHITECTURE.md`;
- обновлены `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`;
- обновлены `Docs/TESTING_AND_VALIDATION.md`;
- создан этот отчёт.

## 4. Команды и проверки

1. Проверены `ProjectVersion.txt`, `Packages/manifest.json`, Graphics/Quality/ProjectSettings, локальная конфигурация, состояние Git и установленный Unity Editor.
2. Через `rg` проверены фактические HDRP 17.3 API для `VisualEnvironment`, `PhysicallyBasedSky`, `Exposure`, `Tonemapping`, `Fog` и HDRP light data.
3. Все 19 `.asmdef` разобраны через `ConvertFrom-Json`; синтаксических ошибок нет.
4. Unity batch mode с `-buildTarget Win64` и `FoundationValidationRunner.RunBatch`: компиляция прошла, Bootstrap/HDRP content создан, foundation validation прошла.
5. Первый EditMode run: 12 тестов, 11 passed, 1 failed. Тест обнаружил, что HDRP components не были сохранены как sub-assets после перезапуска Editor.
6. Генератор исправлен через `AssetDatabase.AddObjectToAsset`; профиль повторно создан/сохранён, foundation validation повторно прошла.
7. Повторный EditMode run: **12 total, 12 passed, 0 failed, 0 skipped**, exit code `0`.
8. Лог повторного теста проверен на `error CS`, `warning CS`, compilation failure и unhandled exception — совпадений нет.
9. Проверены контрольные SHA-256 существующих файлов: `OutdoorsScene.unity`, `GraphicsSettings.asset`, `QualitySettings.asset` и `ProjectSettings.asset` совпадают с pre-change hashes.
10. Проверено `Assets/Game/LegacyImport/ReferenceOnly`: донорских payload-файлов нет.

Игнорируемые test artifacts:

- `Logs/Milestone01_FoundationValidation.log`;
- `Logs/Milestone01_FoundationValidation_Rerun.log`;
- `Logs/Milestone01_EditModeResults.xml` — первый, намеренно зафиксированный failed run;
- `Logs/Milestone01_EditModeResults_Rerun.xml` — итоговый passed run;
- `Logs/Milestone01_FoundationValidation_Final.log` — финальная idempotency/validation проверка с корректным batch shutdown;
- `Logs/Milestone01_EditModeResults_Final.xml` — финальный passed run после усиления project-wide validator;
- соответствующие Unity log files.

## 5. Результаты и сохранность существующего проекта

| Проверка | Результат |
|---|---|
| Unity 6 patch / installed Editor | Pass: `6000.3.11f1` |
| HDRP / Input System / Linear | Pass: `17.3.0` / `1.19.0` / enabled |
| Foundation batch validation | Pass |
| EditMode tests | Pass: `12/12` |
| Runtime → Editor references | Pass: 0 violations |
| Bootstrap first build scene | Pass |
| HDRP profile persists across Editor restart | Pass |
| Donor payload imported | No |
| Existing `OutdoorsScene` changed by milestone | No; SHA-256 remains `666748D3C80A1BAB668C3C69ECB0A3A4431BF2F14BAE7C843F9E03A896D701E8` |
| Existing Graphics/Quality/ProjectSettings changed by milestone | No; baseline hashes preserved |

## 6. Ручные действия Unity

Обязательных ручных действий для завершения Milestone 1 нет. Bootstrap scene, Volume profile и build order созданы и проверены в batch mode.

Необязательная визуальная проверка: открыть `Assets/Game/Bootstrap/Bootstrap.unity`, убедиться, что Global Volume ссылается на `BootstrapGlobalVolume`, затем запустить Scene View с включёнными Effects/Fog. Это не заменяет и не блокирует уже выполненные автоматические проверки.

## 7. Ограничения и риски

1. Composition root пока намеренно не инициализирован конкретными сервисами; это foundation-контракт, а не playable bootstrap.
2. PlayMode test assembly создана, но PlayMode-тесты не запускались: gameplay/service implementations ещё отсутствуют, и prompt требовал EditMode coverage.
3. Validator ещё не проверяет unknown stable IDs из save records — save schema появится в отдельном milestone.
4. HDRP baseline структурно проверен в `-nographics` режиме; художественная калибровка экспозиции, тумана и солнца требует будущей реальной сцены и performance capture.
5. Донорская установка из Milestone 0 остаётся загрязнённой модами/старыми extraction artifacts. Это не повлияло на Milestone 1, потому что донор не использовался, но должно быть разрешено перед контролируемым donor-reference pipeline.
6. Wwise, gameplay, vehicle, world streaming, save storage, donor import и сторонний DI не добавлялись.

## 8. Provenance

Новых donor-derived элементов нет. `DONOR_AUDIT.md`, `PORTING_MATRIX.md`, `SYSTEM_MAP.md` и `PORTING_LEDGER.csv` не требовали новых записей: ни один объект, класс, алгоритм, asset или configuration value из донора не переносился.

## 9. Exit gate

- [x] Unity foundation компилируется без новых C# errors/warnings.
- [x] Bootstrap scene существует и стоит первой в build settings.
- [x] HDRP baseline сериализован и переживает перезапуск Editor.
- [x] Module/Editor/test assembly boundaries созданы и валидируются.
- [x] Initial service interfaces и explicit composition root существуют без global service locator.
- [x] Stable ID format, authoring, explicit regeneration и duplicate validation реализованы.
- [x] EditMode suite проходит `12/12`.
- [x] Донор, Wwise, gameplay и внешние зависимости не добавлены.

## 10. Рекомендуемый следующий milestone

Выполнить ровно **Milestone 2 — `Prompts/02_DONOR_REFERENCE_PIPELINE.md`**, начав с решения о чистом donor baseline и не импортируя raw donor content в production `Assets`.
