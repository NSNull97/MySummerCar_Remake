# Ревью после Milestone 04

Дата ревью: 2026-07-14  
Ревизия: `fc18f93ccddf567e2a0b67d3b3423b656ac0512f` (`Milestone 04 completed`)  
Режим: read-only; код, сцены, prefabs, assets, настройки и package-файлы не изменялись  
Последний подтверждённый milestone: **04 — Player & Interaction Prototype**

## 1. Executive summary

Milestone 04 присутствует в Git как отдельный commit, а его runtime-код в `Assets/Game` не отличается от проверенной ревизии. Архитектурная основа игрока и взаимодействия в целом модульная: зависимости asmdef направлены корректно, циклов и runtime-ссылок на Editor нет, постоянные сущности используют project-owned stable IDs, а donor-контент не попал в отслеживаемые production assets.

Свежий запуск Unity в рамках этого ревью **не выполнялся**, потому что текущая установка уже оставила в рабочем дереве миграции сцен, HDRP assets и `ProjectSettings`; новый запуск нарушил бы read-only-условие. Исторические результаты Milestone 04 подтверждают 45/45 EditMode и 1/1 PlayMode тестов, но это не является свежей проверкой текущего грязного рабочего дерева.

Критических находок нет. Найдены четыре блокирующие находки уровня High:

- `BUILD-001`: до создания этого отчёта рабочее дерево содержало 20 изменённых и 24 неотслеживаемых файла;
- `DOC-001`: новый prompt полного переноса карты противоречит действующему `AGENTS.md` и прежнему объявленному следующему Milestone 05;
- `INTERACT-001`: переносимый предмет перекрывает единственный interaction ray, поэтому handoff в mount недоступен через реальный player interaction path;
- `INTERACT-002`: carry-контроллер не восстанавливает физическое состояние предмета при disable/destroy/unload.

Итоговый вердикт: **NO-GO для `04A_FULL_WORLD_GEOMETRY_TRANSFER.md` в текущем виде**. Единственный рекомендуемый следующий milestone — пересмотренный **Milestone 04A-Pilot**, ограниченный исходной зоной vertical slice (гараж и ближайший участок дороги), после отдельного устранения блокирующих находок и фиксации чистой базовой ревизии.

## 2. Состояние репозитория и Git

### 2.1 Подтверждённая база

- `HEAD`: `fc18f93ccddf567e2a0b67d3b3423b656ac0512f`.
- Commit message: `Milestone 04 completed`.
- Изменений относительно `HEAD` внутри `Assets/Game` до создания отчёта не было.
- `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt` и `ProjectSettings/EditorBuildSettings.asset` относительно `HEAD` не изменены.
- Unity: `6000.3.11f1` (`3000ef702840`).
- HDRP: `17.3.0`.
- Input System: `1.19.0`.

### 2.2 Рабочее дерево до создания отчёта

Git показал:

- staged: 0;
- modified: 20;
- untracked: 24.

Изменения смешивают несколько независимых категорий:

- автоматически затронутые Unity/HDRP assets и настройки, включая `Assets/OutdoorsScene.unity`, HDRP pipeline assets, `ProjectSettings/ProjectSettings.asset`, `ShaderGraphSettings.asset`, `VFXManager.asset` и `SceneTemplateSettings.json`;
- изменённый `MySummerCar_Remake.slnx`;
- новый/обновлённый prompt pack, включая `Prompts/99_REVIEW.md`, `CURRENT_STATE_AFTER_04.md`, manifest и sequence-документы;
- три бинарных UI-reference изображения в `References/UI/`;
- cloud/project metadata в Unity settings.

Создание этого ревью добавляет только:

- `Docs/Reviews/REVIEW_AFTER_04.md`;
- `Docs/Reviews/LATEST_REVIEW_POINTER.md`.

Остальные изменения существовали до ревью и не редактировались.

## 3. Компиляция, тесты и статическая проверка

### 3.1 Что подтверждено существующими артефактами Milestone 04

| Проверка | Результат | Источник |
|---|---:|---|
| EditMode | 45/45 passed, 0 failed | сохранённый XML, 2026-07-14 04:54:56Z–04:54:57Z |
| PlayMode | 1/1 passed, 0 failed | сохранённый XML, 2026-07-14 04:55:17Z–04:55:18Z |
| Milestone validators | passed | `Docs/Reports/MILESTONE_04_REPORT.md` |
| Финальные test logs | нет `error CS`, `warning CS`, compilation failure, unhandled exception или NullReference | существующие логи Milestone 04 |

В общих Unity logs присутствуют сообщения licensing handshake и Curl certificate, но они не помешали сохранённым тестовым прогонам завершиться успешно.

### 3.2 Что проверено без запуска Unity

- Найдено 19 asmdef; отсутствующие локальные ссылки: 0.
- Циклы между локальными asmdef: 0.
- Runtime → Editor ссылки: 0.
- Runtime absolute paths: не обнаружены.
- Runtime-использование `UnityEditor`/`AssetDatabase`: не обнаружено.
- Runtime-использование `FindObjectOfType`, `GameObject.Find` и `Resources.Load`: не обнаружено.
- Missing-script sentinel `m_Script: {fileID: 0}` в отслеживаемом Game-контенте: не обнаружен.
- Нулевые GUID в отслеживаемом Game-контенте: не обнаружены.
- Два stable ID в Milestone 04 scene непустые и уникальные по статическому просмотру.
- В Git под `LegacyImport/ReferenceOnly` и `Imported/DonorGenerated` отслеживаются только `.gitkeep`; donor payload не обнаружен.
- Локальные reference-only OBJ и comparison scenes игнорируются Git, ссылок на их GUID из отслеживаемых production assets не найдено.
- Prompt manifest внутренне согласован: 37 записей, отсутствующих файлов 0, hash mismatch 0.

### 3.3 Что не выполнялось

Свежая Unity-компиляция, EditMode/PlayMode tests, build и profiler capture не выполнялись. Причина — read-only-режим ревью и уже наблюдаемые автоматические изменения Unity в production/settings-файлах. Поэтому текущий вывод о компиляции опирается на неизменность `Assets/Game` относительно проверенного `HEAD`, статическую проверку и существующие результаты Milestone 04, но не заменяет новый gate после очистки рабочей базы.

## 4. Findings

### Critical

Критических находок не обнаружено.

### High

#### BUILD-001 — Следующий milestone не имеет чистой и однозначной Git-базы

- Severity: **High**
- Confidence: **High**
- Затронуто: рабочее дерево целиком; в частности `Assets/OutdoorsScene.unity`, HDRP assets, `ProjectSettings/*`, `MySummerCar_Remake.slnx`, `Prompts/*`, `References/UI/*`.
- Доказательство: до создания отчёта `git status --short` показал 20 modified, 24 untracked и 0 staged; категории изменений перечислены в разделе 2.2.
- Почему это дефект: крупный следующий milestone нельзя надёжно сравнить с Milestone 04, если prompt pack, Unity migrations/settings и новые бинарные references уже смешаны в одной незавершённой базе.
- Возможный отказ: случайный commit локальных/cloud-настроек или бинарных references; потеря авторства и причин изменений; невозможность определить, какой milestone внёс regression.
- Точная коррекция: отдельно проинвентаризировать существующие пользовательские изменения; принять или отклонить каждую категорию осознанно; вынести prompt pack и намеренные Unity migration/settings changes в отдельные сфокусированные commits; не удалять пользовательские изменения автоматически; получить чистый `git status` перед началом нового milestone.
- Блокирует следующий этап: **да**.
- Проверка коррекции: `git status --short` пуст перед стартом; выбранный baseline commit зафиксирован в milestone plan; donor/local paths и запрещённые payload отсутствуют в `git ls-files`.

#### DOC-001 — План полного Milestone 04A конфликтует с действующим scope authority

- Severity: **High**
- Confidence: **High**
- Затронуто: `AGENTS.md:54-61`, `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md:60-68`, `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md:126`, `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md:1754`, `Docs/Reports/MILESTONE_04_REPORT.md:152`, `Docs/Architecture/CURRENT_REPOSITORY_STATE.md:59`, `Prompts/CURRENT_STATE_AFTER_04.md:19`.
- Доказательство: `AGENTS.md` относит всю оригинальную карту к текущим non-goals до стабильного single-player vertical slice. Prompt 04A требует complete original map, complete world-layout database, complete geometric world и запись для каждой найденной геометрии. Одновременно M04 report и current repository state называют следующим ровно Milestone 05, тогда как новые sequence-документы направляют в 04A.
- Почему это дефект: исполнителю даны взаимоисключающие указания по допустимому объёму и очередности работ; `AGENTS.md` является действующей repository policy.
- Возможный отказ: многонедельный массовый donor transfer вне текущего milestone boundary, загрязнение production assets, отсутствие проверяемого vertical slice и невозможность применить stop condition «не пересекать текущий milestone».
- Точная коррекция: владелец проекта должен явно выбрать authority и переписать sequence согласованно. Без изменения `AGENTS.md` — ограничить 04A одной заранее определённой зоной vertical slice, лимитированным набором records и внешним staging; полный world transfer оставить будущим milestone.
- Блокирует следующий этап: **да, блокирует 04A в текущем виде**.
- Проверка коррекции: `AGENTS.md`, roadmap, current state, prompt sequence, prompt manifest и milestone prompt называют один и тот же bounded scope и один следующий milestone.

#### INTERACT-001 — Held object перекрывает interaction ray и делает mount handoff недоступным через игрока

- Severity: **High**
- Confidence: **High**
- Затронуто: `Assets/Game/Interaction/Runtime/RaycastInteractionCandidateSource.cs:35-47`, `Assets/Game/Editor/PlayerInteractionPrototypeBuilder.cs:112`, `Assets/Game/Editor/PlayerInteractionPrototypeBuilder.cs:125`, `Assets/Game/Editor/PlayerInteractionPrototypeBuilder.cs:184-192`, `Assets/Game/Tests/EditMode/PlayerInteractionRuntimeTests.cs:109-121`.
- Доказательство: candidate source выполняет одиночный `Physics.Raycast` и принимает первый collider без ignore-list. Carry anchor находится в `(0, -0.12, 1.25)` относительно камеры, query mask равен `~0`, а pickup cubes имеют размер 0.55/0.8 и тот же default layer. Луч к mount target сначала пересекает held cube. Имеющийся тест вызывает `carryController.TryHandoff(mount, context)` напрямую, обходя raycast и `PlayerInteractionController`.
- Почему это дефект: реализованный public interaction path не может выбрать mount за переносимым объектом, хотя прямой unit-level handoff проходит.
- Возможный отказ: игрок поднимает предмет, наводится на mount и получает candidate самого предмета; установка не происходит или выполняется неправильное действие.
- Точная коррекция: ввести явную фильтрацию colliders текущего held object либо отдельный interaction/query channel/layer; сохранить выбор следующего валидного capability target; не привязывать логику к имени объекта.
- Блокирует следующий этап: **да для интеграции player interaction с world/vehicle mount points**.
- Проверка коррекции: PlayMode test с настоящей камерой, ray query и controller поднимает cube, целится сквозь него в mount и подтверждает успешный handoff; тест также проверяет отсутствие выбора held collider.

#### INTERACT-002 — Carry-состояние не очищается при disable/destroy/unload контроллера

- Severity: **High**
- Confidence: **High**
- Затронуто: `Assets/Game/Interaction/Runtime/PhysicalCarryController.cs:58-64`, `Assets/Game/Interaction/Runtime/PhysicalCarryController.cs:227-240`, весь lifecycle `PhysicalCarryController`.
- Доказательство: при pickup отключается gravity и игнорируется collision с игроком; восстановление находится только в явном `Release`. В классе нет `OnDisable` или `OnDestroy`, гарантирующих восстановление при выгрузке/отключении владельца.
- Почему это дефект: состояние Rigidbody и collision pair принадлежит миру, но его cleanup зависит от продолжения жизни player controller.
- Возможный отказ: при additive scene unload, respawn или отключении player object переносимый предмет остаётся без gravity, с изменённым damping и/или с не восстановленной collision-ignore парой.
- Точная коррекция: определить явную lifecycle policy и idempotent cleanup, восстанавливающий Rigidbody и collision pairs при disable/destroy/unload; отдельно определить поведение при уничтожении самого held target.
- Блокирует следующий этап: **да для streaming/scene lifecycle интеграции**.
- Проверка коррекции: PlayMode tests отключают и уничтожают carry controller/player во время удержания, затем подтверждают gravity, damping, collision и отсутствие dangling state; повторный cleanup не вызывает ошибок.

### Medium

#### ARCH-001 — Composition root пока не может собрать частичный вертикальный срез

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Assets/Game/Bootstrap/Runtime/GameServiceBindings.cs:17-32`, `Assets/Game/Bootstrap/Runtime/GameCompositionRoot.cs:12-34`, `Docs/ARCHITECTURE.md`, `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`.
- Доказательство: bindings требуют одновременно все семь service references, включая ещё не реализованные subsystem services; composition root только валидирует/хранит private bindings и не предоставляет явного module installation path. Milestone 04 запускается отдельной prototype scene и обходит bootstrap.
- Почему это дефект: следующий интегрированный world/player slice вынужден либо оставаться вне composition root, либо создавать фиктивные services, что противоречит запрету silent/fake architecture.
- Возможный отказ: параллельные composition paths, scene-local wiring без общего ownership или преждевременный service locator.
- Точная коррекция: определить составляемые module installers/phase-specific bindings с явными обязательными зависимостями, не вводя глобальный mutable locator; добавить интеграционный boot path для реализованных модулей.
- Блокирует следующий этап: **условно; блокирует runtime-интеграцию world + player, но не read-only donor discovery**.
- Проверка коррекции: boot integration test запускает Bootstrap + Player + Interaction + ограниченный World module без fake future services и выдаёт явную ошибку при реально отсутствующей обязательной ссылке.

#### PLAYER-001 — Отключение Input Router не обнуляет motor intent

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Assets/Game/Player/Runtime/PlayerInputRouter.cs:69-82`, состояние `FirstPersonMotor`.
- Доказательство: `OnDisable` только отключает action map. Последние move/crouch значения уже записаны в motor и не очищаются до следующего `Update`, которого у отключённого router не будет.
- Почему это дефект: отключение ввода для pause, UI, transition или modal interaction не означает отключение motor component.
- Возможный отказ: игрок продолжает идти или сохраняет неверный crouch intent после отключения input router.
- Точная коррекция: при disable явно сбрасывать movement intent и документированно устанавливать crouch policy; разделить «input disabled» и «player frozen», если это разные состояния.
- Блокирует следующий этап: **нет для donor transfer; да перед полноценной runtime-интеграцией меню/стриминга**.
- Проверка коррекции: PlayMode test задаёт движение, отключает только router при активном motor и подтверждает нулевой intent/скорость в соответствии с policy.

#### TEST-001 — Тесты не покрывают заявленный end-to-end interaction loop

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Assets/Game/Tests/PlayMode/PlayerInteractionBootTests.cs`, `Assets/Game/Tests/EditMode/PlayerInteractionRuntimeTests.cs:109-121`, `Docs/TESTING_AND_VALIDATION.md:52`, `Docs/Reports/MILESTONE_04_REPORT.md`.
- Доказательство: единственный PlayMode test загружает scene и проверяет наличие компонентов/stable IDs. Mount test вызывает carry handoff напрямую. Нет тестов реального raycast pickup/drop/place/throw/rotation, headroom crouch, held-object filtering и lifecycle unload.
- Почему это дефект: component presence подтверждает authoring, но не подтверждает физический игровой путь, ради которого создан milestone.
- Возможный отказ: `INTERACT-001` и `INTERACT-002` проходят текущий gate, хотя ломают основной пользовательский flow.
- Точная коррекция: добавить небольшую end-to-end PlayMode матрицу для camera ray → capability target → controller → physics result, включая отрицательные и lifecycle cases; pure calculations оставить в EditMode.
- Блокирует следующий этап: **условно; блокирует утверждение, что Milestone 04 exit gate полностью закрывает interaction loop**.
- Проверка коррекции: новые tests сначала воспроизводят обе High-находки, после коррекции стабильно проходят вместе со старым набором.

#### DEP-001 — Development performance probe размещён в production World.Runtime

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Assets/Game/World/Runtime/GaragePrototypePerformanceProbe.cs:3`, `Assets/Game/World/Runtime/GaragePrototypePerformanceProbe.cs:86`, `Assets/Game/World/Runtime/GaragePrototypePerformanceProbe.cs:179`, `Assets/Game/World/Runtime/GaragePrototypePerformanceProbe.cs:236`, World runtime asmdef, `Docs/ARCHITECTURE.md:71`, `Docs/ARCHITECTURE.md:132`.
- Доказательство: probe использует `System.IO`, render timing/request и синхронную запись файлов; из-за него World.Runtime ссылается на RenderPipelines.Core. Документация сама помечает зависимость как временную и требует вынести probe до production streaming.
- Почему это дефект: production world module получает profiling/output responsibilities и дополнительную render-pipeline dependency.
- Возможный отказ: развитие streaming кода закрепит временную зависимость, а profiling I/O случайно попадёт в development/release path. Сейчас риск ограничен, потому что probe активируется CLI-флагом.
- Точная коррекция: переместить capture/probe в отдельную development-only assembly или Editor/build-conditional модуль с явной activation policy.
- Блокирует следующий этап: **условно; исправить до расширения production World.Runtime**.
- Проверка коррекции: World.Runtime asmdef больше не зависит от RenderPipelines.Core только ради probe; player build не содержит file-output capture path; development capture по-прежнему воспроизводим.

#### PERF-001 — Существующий performance capture не закрывает установленный бюджет

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Docs/Performance/MILESTONE_03_PERFORMANCE_CAPTURE.md:19`, `Docs/Performance/MILESTONE_03_PERFORMANCE_CAPTURE.md:43`, `Docs/PERFORMANCE_BUDGET.md:42-45`; отсутствует отдельный capture Milestone 04.
- Доказательство: M03 capture сообщает, что Present/GPU timing недоступны. Установленный бюджет также требует managed allocations, physics, draw calls и memory snapshot; эти измерения в capture не представлены. Для M04 отдельного representative capture нет.
- Почему это дефект: CPU-only sample гаража не позволяет оценить стоимость player physics/interaction и не создаёт baseline для увеличения мира.
- Возможный отказ: world content расширяется без GPU, memory, physics и allocation baseline; regression обнаруживается после масштабирования.
- Точная коррекция: после исправления blockers снять development-player capture интегрированной bounded scene с CPU main/render, доступным GPU timing, GC alloc, physics, batches/draw calls и memory snapshot; явно фиксировать hardware/settings.
- Блокирует следующий этап: **нет для bounded reference discovery; да для performance-go полного world expansion**.
- Проверка коррекции: новый capture заполняет обязательные поля бюджета либо документирует воспроизводимую альтернативу недоступному GPU API и сравнивает результат с 60 FPS target.

#### DONOR-001 — Общая запись ledger для `sharedassets3.assets` отстаёт от подтверждённого hash

- Severity: **Medium**
- Confidence: **High**
- Затронуто: `Docs/Porting/PORTING_LEDGER.csv:12`, `Docs/Porting/DONOR_AUDIT.md:73`, `Docs/Porting/PORTING_LEDGER.csv:34`, `Docs/Porting/PORTING_LEDGER.csv:36`, `Docs/Porting/PORTING_LEDGER.csv:42-43`, `Docs/Porting/PORTING_LEDGER.csv:47`.
- Доказательство: общая container row всё ещё имеет пустой hash и статус `InventoriedHashDeferred`, тогда как audit и несколько object rows уже используют конкретный SHA-256 этого файла.
- Почему это дефект: один current ledger одновременно утверждает, что hash отложен, и использует этот hash ниже, что создаёт неоднозначность для автоматического provenance audit.
- Возможный отказ: world extraction records наследуют неверный container status или дублируют несовместимые source identities.
- Точная коррекция: обновить container record подтверждённым hash либо явно пометить старую строку superseded и связать с canonical source record; исторический M00 report не переписывать.
- Блокирует следующий этап: **нет для текущих двух контролируемых records; исправить до массового world-layout ledger expansion**.
- Проверка коррекции: validator находит одну canonical source identity на donor container, все производные records ссылаются на тот же hash, `InventoriedHashDeferred` не остаётся для уже хешированного файла.

### Low

#### PLAYER-002 — Gamepad look потенциально зависит от frame rate

- Severity: **Low**
- Confidence: **High**
- Затронуто: `Assets/Game/Player/Runtime/PlayerInputRouter.cs:91`.
- Доказательство: raw look умножается на degrees-per-pixel без `deltaTime` и без разделения delta-устройства мыши от rate-устройства gamepad stick.
- Почему это дефект: mouse delta и stick rate имеют разные временные семантики.
- Возможный отказ: одинаковое отклонение gamepad stick даёт разную угловую скорость при разном FPS.
- Точная коррекция: применять device/control-specific scaling: mouse delta без frame scaling, stick rate с `deltaTime`, либо Input System processor/отдельные actions с документированными units.
- Блокирует следующий этап: **нет**.
- Проверка коррекции: тест/контролируемый sample сравнивает поворот gamepad за одинаковый реальный интервал при двух frame rates; mouse delta остаётся неизменным.

#### DOC-002 — Архитектурный документ отстаёт от фактических ссылок и build order

- Severity: **Low**
- Confidence: **High**
- Затронуто: `Docs/ARCHITECTURE.md:83`, `Docs/ARCHITECTURE.md:132`, player PlayMode asmdef, `ProjectSettings/EditorBuildSettings.asset`, `Docs/Architecture/CURRENT_REPOSITORY_STATE.md:14`.
- Доказательство: документ описывает PlayMode tests как зависящие только от Core/Bootstrap, хотя текущая assembly также ссылается на Interaction, Player и InputSystem. Документ всё ещё называет garage prototype второй build scene, тогда как Milestone 04 scene теперь вторая, а garage — третья.
- Почему это дефект: dependency/build-order документация больше не является точной картой current repository state.
- Возможный отказ: следующий разработчик делает неверные предположения о test assembly boundary или scene index.
- Точная коррекция: после решения `DOC-001` синхронизировать dependency graph и build scene order с фактическими asmdef/settings.
- Блокирует следующий этап: **нет**.
- Проверка коррекции: небольшой doc validator или review script сравнивает перечисленные asmdef dependencies/build scenes с repository files.

## 5. Блокирующие проблемы

Перед следующим milestone должны быть закрыты:

1. `BUILD-001` — зафиксировать чистую и осознанную baseline-ревизию.
2. `DOC-001` — согласовать scope authority и отказаться от полного переноса карты в текущем vertical-slice этапе.
3. `INTERACT-001` — обеспечить выбор mount через реальный interaction path при удержании предмета.
4. `INTERACT-002` — обеспечить lifecycle-safe cleanup переносимого Rigidbody.

Для runtime-интегрированного world slice также до начала сборки общей сцены необходимо принять решение по `ARCH-001`; для одного лишь bounded donor discovery это не является немедленным blocker.

## 6. Неблокирующие проблемы и долги

- `PLAYER-001`: сброс motor intent при отключении ввода.
- `TEST-001`: расширение PlayMode gate до реального interaction flow.
- `DEP-001`: изоляция development probe от World.Runtime.
- `PERF-001`: полный performance baseline.
- `DONOR-001`: canonical hash/status в ledger.
- `PLAYER-002`: frame-independent gamepad look.
- `DOC-002`: синхронизация architecture/build-order документации.

Эти пункты нельзя считать необязательными навсегда: часть из них становится blocking в момент runtime world integration или масштабирования контента.

## 7. Соответствие `AGENTS.md`

### Нарушения или текущие конфликты

- `DOC-001`: предложенный полный 04A пересекает текущий non-goal «entire original map» и milestone boundary.
- `BUILD-001`: грязная база мешает требованию small/focused commits и честной атрибуции milestone.
- `DEP-001`: временный development probe остаётся внутри World.Runtime, хотя документация уже предписывает отделение перед production streaming.

### Что соответствует правилам

- Донорская установка использовалась read-only; записей в неё не обнаружено.
- Machine-specific paths не захардкожены в runtime C#.
- Raw donor payload не отслеживается Git.
- Reference-only content отделён и не используется production assets.
- Runtime assemblies не ссылаются на Editor assemblies.
- Player/Interaction разделены на модули; interaction основан на capabilities, а не giant switch по именам.
- Persistent scene entities используют project-owned stable IDs.
- Wwise/vehicle/save/world scope не был самовольно реализован в Milestone 04.

## 8. Сводка зависимостей

Фактическая важная часть графа:

```text
Core
  ├─ Interaction.Runtime
  │    └─ Player.Runtime (+ Unity Input System)
  ├─ World.Runtime (+ RenderPipelines.Core только из-за временного probe)
  └─ service-boundary assemblies

Bootstrap.Runtime
  └─ service-boundary assemblies

Editor
  └─ runtime modules + HDRP authoring APIs

Tests.EditMode / Tests.PlayMode
  └─ только необходимые runtime/test dependencies
```

Статический анализ 19 asmdef: missing local refs 0, cycles 0, runtime-to-editor refs 0. Главный dependency risk — не цикл, а временная HDRP/profiling ответственность в World.Runtime (`DEP-001`) и отсутствие пригодного partial composition path (`ARCH-001`).

## 9. Готовность Player/Interaction к переносу в world slice

Положительные стороны:

- движение, камера, interaction intent, targets/capabilities и physical carry разделены;
- параметры authoring сериализованы, нет name-based runtime lookup;
- stable identity встроена в prototype content;
- pure/runtime behaviour имеет EditMode tests;
- prototype scene загружается в существующем PlayMode test.

Необходимые ограничения:

- реальный mount flow сейчас блокируется held collider (`INTERACT-001`);
- additive scene/player lifecycle небезопасен (`INTERACT-002`);
- input disable policy неполна (`PLAYER-001`);
- composition root не собирает текущий частичный slice (`ARCH-001`);
- end-to-end PlayMode coverage недостаточно (`TEST-001`).

Вывод: модули подходят как основа для исправляемого прототипа, но **не готовы к бесконтрольному размножению по streaming cells и world mount points**.

## 10. Готовность donor pipeline к world transfer

Готово и проверяемо:

- local donor config существует и указывает на отдельные original/staging/reference roots;
- donor installation и основные read-only tools доступны;
- guardrails, hashing, manifests и provenance docs существуют;
- текущий controlled pipeline безопасно работает для малого числа явно выбранных records;
- reference-only импорт отделён от production и исключён из Git.

Не готово для полного переноса карты:

- полный transfer противоречит current scope (`DOC-001`);
- current ledger содержит неоднозначный source-container status (`DONOR-001`);
- существующая схема и проверки доказаны на малом контролируемом наборе, а не на exhaustive map database;
- до массовой генерации нет чистого Git baseline (`BUILD-001`);
- отсутствует согласованный bounded acceptance set для initial vertical slice.

Вывод: pipeline **готов к ограниченному read-only discovery/pilot**, но не даёт основания начинать exhaustive world extraction/import.

## 11. Пробелы тестирования

Минимально недостающие будущие проверки:

1. Camera ray pickup → carry → drop/place/throw через `PlayerInteractionController`.
2. Held-object filtering и mount handoff через фактический raycast.
3. Carry cleanup при disable/destroy player и unload scene.
4. Crouch с заблокированным headroom и восстановление высоты.
5. Input router disable при продолжающем работать motor.
6. Gamepad look на разных frame rates.
7. Bootstrap + Player + Interaction + bounded World boot без fake services.
8. Donor manifest/ledger canonical source-hash validation для расширенного набора records.
9. Build validation, что ReferenceOnly и development capture code не попали в player content/path.

Это рекомендации отчёта; тестовые файлы в данном read-only ревью не создавались.

## 12. Производительность

Цель 60 FPS при 1920×1080 документирована, HDRP ray tracing отключён, а development probe не активен без явного CLI-флага. Однако доступный capture относится к Milestone 03 и не содержит полного GPU/memory/physics/allocation набора. Milestone 04 не имеет отдельного representative capture. Поэтому performance status после M04: **не подтверждён**, но и конкретная regression текущими артефактами не доказана.

Перед расширением world content нужен один воспроизводимый integrated baseline, описанный в `PERF-001`.

## 13. Документация

Документация по donor safety, provenance, architecture principles и M04 implementation в целом подробная. Основные проблемы:

- взаимоисключающий следующий milestone и scope (`DOC-001`);
- устаревшие asmdef/build-order детали (`DOC-002`);
- ledger status не синхронизирован с уже известным hash (`DONOR-001`);
- M04 report сообщает успешный исторический gate, но не должен трактоваться как свежий результат после текущих незакоммиченных Unity/settings изменений.

## 14. Рекомендуемые исправления в точном порядке

Исправления здесь только описаны и не применялись.

1. `BUILD-001`: классифицировать и отдельно зафиксировать/исключить существующие prompt, Unity migration/settings и reference changes; получить чистый baseline.
2. `DOC-001`: утвердить bounded 04A-Pilot и синхронизировать `AGENTS.md`-совместимые sequence/roadmap/current-state документы.
3. `INTERACT-001`: добавить held-collider filtering/query policy и воспроизводящий end-to-end PlayMode test.
4. `INTERACT-002`: добавить idempotent lifecycle cleanup и disable/destroy/unload tests.
5. `ARCH-001`: определить partial module composition path до runtime world integration.
6. `PLAYER-001` и `PLAYER-002`: зафиксировать input disable/look time semantics и тесты.
7. `TEST-001`: расширить PlayMode gate основными физическими flows.
8. `DEP-001`: вынести performance probe из production World.Runtime.
9. `DONOR-001` и `DOC-002`: синхронизировать provenance/architecture documentation.
10. `PERF-001`: снять integrated performance baseline перед расширением bounded world slice.

После corrections необходим свежий Unity batch gate с EditMode, PlayMode, validators и проверкой чистого Git status. Это отдельная будущая write-операция, не часть данного ревью.

## 15. Go / No-Go

### Решение

**NO-GO: не начинать `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md` в текущем полном объёме.**

Причины: `BUILD-001`, `DOC-001`, `INTERACT-001`, `INTERACT-002`.

### Ровно один рекомендуемый следующий milestone

**Milestone 04A-Pilot — bounded world-layout/reference transfer для исходной зоны vertical slice: гараж и ближайший участок дороги.**

Он допустим только после закрытия четырёх blocking findings, явной фиксации ограниченного acceptance set и чистого baseline. Exhaustive map discovery/import, вся карта, все buildings/vegetation/water/colliders и массовый production import в этот pilot не входят.

## 16. Что было просмотрено

- Полностью: `AGENTS.md` и `Prompts/99_REVIEW.md`.
- Milestone reports 00–04 и current-state/roadmap/architecture документы.
- Player, Interaction, Core, Bootstrap, World runtime/editor code и соответствующие tests/asmdefs.
- Scenes/prefabs/assets на уровне сериализованных ссылок, GUID, stable IDs и Git provenance без изменения файлов.
- Donor audit, pipeline, porting matrix, system map, ledger, reference checklist и reports.
- Package manifest/lock, Unity version, build settings и HDRP project settings.
- Существующие test XML/logs и performance capture.
- Prompt sequence, manifest и новый 04A prompt в объёме, необходимом для проверки scope и acceptance intent; `99_REVIEW.md` прочитан полностью, как потребовал пользователь.

## 17. Выполненные команды и ограничения ревью

Выполнялись только read-only операции: `git status`, `git diff`, `git show`, `git ls-files`, `rg`, `Get-Content`, `Test-Path`, JSON/CSV/XML parsing и статические dependency/GUID scans.

Unity Editor, batch mode, tests, build, import и donor extraction не запускались. Один read-only helper check получил нефатальную ошибку `Test-Path` на пустом необязательном ILSpy path; основной local config был успешно разобран, Unity Editor и AssetRipper обнаружены.

## 18. Файлы, созданные ревью

- `Docs/Reviews/REVIEW_AFTER_04.md`
- `Docs/Reviews/LATEST_REVIEW_POINTER.md`

Другие файлы не изменялись. Ревью завершено и на этом останавливается.

## 19. Resolution table — approved fix run 2026-07-14

Отдельный fix run выполнен по явному разрешению всех finding IDs. Полный отчёт:
`Docs/Reviews/FIX_REPORT_20260714_113701.md`.

| Finding ID | Статус | Файлы изменения | Проверки | Остаточный риск | Рекомендация по commit |
|---|---|---|---|---|---|
| `BUILD-001` | **Blocked** | Файлы не удалялись, не stash-ились и не коммитились | Повторный `git status --short --untracked-files=all` | До fix run существовали 20 modified и 24 untracked файла разных владельцев/категорий; 99A не разрешает смешанный commit или угадывание судьбы пользовательских изменений | Сначала вручную классифицировать baseline; затем отдельные commits по prompt pack, Unity migration/settings и approved fixes |
| `DOC-001` | **Fixed** | `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md`, новый `Prompts/04A_WORLD_LAYOUT_PILOT.md`, sequence/current-state/manifest docs | Prompt manifest: 38/38 entries, missing 0, mismatch 0 | Pilot всё ещё запрещено начинать до clean baseline | `docs: bound milestone 04A to vertical-slice pilot` |
| `INTERACT-001` | **Fixed** | query, carry/controller integration, PlayMode flow test | PlayMode 5/5; M4 validator passed | Query buffer ограничен 32 hits; unrelated collider намеренно остаётся occluder | `player: fix held-object interaction query` |
| `INTERACT-002` | **Fixed** | `PhysicalCarryController`, PlayMode lifecycle tests | Disable/destroy tests passed | Additive streaming owner ещё не реализован; cleanup проверен на component/owner lifecycle | включить в `player: fix held-object interaction query` |
| `ARCH-001` | **Fixed** | `GameServiceBindings`, `GameCompositionRoot`, EditMode tests, architecture docs | EditMode 48/48 | Partial factory намеренно поддерживает только текущий concrete `IInteractionService`; будущие factories добавляются по use case | `core: allow explicit partial interaction bindings` |
| `PLAYER-001` | **Fixed** | `FirstPersonMotor`, `PlayerInputRouter`, PlayMode test | Input-router disable flow passed | Pause/freeze policy выше input boundary ещё не реализована | `player: reset input intent on router disable` |
| `TEST-001` | **Fixed** | новые EditMode/PlayMode tests, testing docs | EditMode 48/48; PlayMode 5/5 | Manual feel review остаётся отдельным ручным шагом | `test: cover post-m4 interaction regressions` |
| `DEP-001` | **Fixed** | новая `MSC.Development.Performance`, moved probe/meta, World/Editor asmdefs, garage scene assembly identifier | Unity compile passed; M3/M4 validators passed; Development Win64 build succeeded; capture succeeded | Инертная non-development stub branch не проверена отдельным release build | `build: isolate garage performance capture tooling` |
| `PERF-001` | **Blocked (partial validation)** | `Docs/PERFORMANCE_BUDGET.md`; новые capture artifacts остаются ignored | Development build 217,320,758 bytes; M3 capture 600 frames, mean 2.876 ms, p95 3.250 ms, CPU mean 2.748 ms | GPU timing всё ещё 0; нет integrated Player+World scene, allocation/physics/draw/memory capture. Создание такой сцены пересекло бы milestone boundary | Не заявлять closure; закрыть отдельным performance commit после появления bounded integrated slice |
| `DONOR-001` | **Fixed** | `Docs/Porting/PORTING_LEDGER.csv` | Donor validator passed with 0 warnings; canonical hash format checked | Container object inventory остаётся неполным, что явно записано | `import: canonicalize sharedassets3 ledger hash` |
| `PLAYER-002` | **Fixed** | новый `LookInputScaling`, `FirstPersonLook`, `PlayerInputRouter`, EditMode test | Pointer/rate scaling test passed | Device-specific feel tuning остаётся ручным | включить в player commit |
| `DOC-002` | **Fixed** | architecture/current-state/player/testing/performance docs | asmdef scan: 20 assemblies, missing 0, cycles 0, runtime→Editor 0 | Документы надо обновлять при следующем composition/world milestone | включить docs в соответствующие architecture commits |

Итог resolution table: **10 fixed, 2 blocked**. Следующий milestone пока **не безопасен** из-за `BUILD-001`; `PERF-001` дополнительно блокирует performance-go интегрированного среза, но не bounded read-only discovery после появления clean baseline.
