# Satsuma: передняя сборка, пакет A — реализация и проверка

Дата: 2026-09-04. Старый контракт отказа: `SupersededByUserRevision`.
Актуальная редакция A: `ImplementedUserRevisionAutomatedPassedManualPending`.

Это итог автоматизированного этапа только по пакету A из
[спецификации исправлений](SATSUMA_SUSPENSION_ASSEMBLY_RULES_FIX_SPEC_2026-09-04.md):
условия установки передних деталей и пороги крепежа. Первоначальный адресный
отказ установки заменён уточнением пользователя ниже. EditMode 233/233 и
PlayMode 47/47 — подтверждённые **исторические результаты старого контракта**,
не проверка новой редакции. Новая редакция вместе с B прошла 250/250 EditMode
и 52/52 PlayMode; ручная приёмка pending.
`P1.CAR.002` / `P1.CAR.003` остаются `PartiallyImplemented`, не `Verified`;
пакеты B–D сюда не включены.

## 0. Актуальная редакция: сначала монтаж, затем развал зависимой сборки

Уточнение пользователя от 2026-09-04: входящая деталь должна **установиться и
спровоцировать развал конструкции**, а не оставаться в руках при падении одной
опоры. Это явно согласованный `UserRevision` project-owned поведения; полная
цепочка такого каскада не объявляется побайтно подтверждённой donor-механикой.

Текущий прочитанный код `VehicleAssemblyController` реализует:

1. `TryValidateInstallationSupport` до передачи/монтажа проверяет только наличие
   опоры и корректность ссылки; запрещены отсутствующая опора, самоссылка и
   assembly-root как такая опора. `TryPrepareHandoffInstall` не роняет детали.
2. При существующей незатянутой опоре передача из рук разрешена. Входящая деталь
   проходит обычную установку, включая принятую анимацию, `TryOccupy`, `InstallAt`
   и событие `PartInstalled`.
3. Затем `CollapseAfterInstallationSupportCheck` читает актуальный `Bolted` опоры.
   Если он false, существующий `CollapseMountHierarchy` освобождает опору и
   транзитивно зависимые mounts, включая только что установленную деталь.
   Зависимость определяется существующим `AssemblyGraph.IsMountDependentOn`:
   owner и authored RequiredOccupied/RequiredAnyOccupied/RequiredBolted/
   RequiredAnyBolted. `visited` исключает повторное отсоединение в одном обходе.
4. Для каждого реально освобождённого объекта публикуется `PartBrokenLoose`;
   результат попытки — успешный `Install`, за которым произошёл развал, а не
   старый `InstallationSupportDetached`. Независимые от опоры ветки не затрагиваются.
   Общая опора может связывать обе передние стороны — «не трогать соседнее» не
   означает сохранять зависимую противоположную сторону на падающем подрамнике.
5. Если опора закреплена, монтаж завершается нормально. Если опора пропала до
   фактического монтажа, остаётся штатный отказ/отмена перехода. Текущее B читается
   после монтажа, поэтому раскручивание за время handoff приводит к тому же
   post-commit развалу, а не к старому раннему отказу.

Это действие **по попытке сборки**, не постоянное правило «любой T=0 немедленно
роняет конструкцию». Обратное условие полуоси, 12 support mappings, пороги,
геометрия, физические настройки и схема save этой редакцией не заменяются.

### Узкая повторная сверка косвенных donor-эффектов

Повторно прочитаны активные `Remove part` у Removal107346 (subframe), 113484
(rack), 113308 (wishbone FL), 110197 (spindle FL). `Remove other` в этих четырёх
FSM отсутствует; в прочитанных действиях нет явной рассылки дочерних Detach.
Есть собственные Data.Installed/Bolted reset, создание loose representation и
деактивация установленного представления. Однако отсутствие явной рассылки
**не доказывает отсутствие косвенного каскада**:

- Wishbone113308 отдельно отключает `Wheel` на GO29428 и деактивирует
  `IK_wishbone_fl` GO22141 / Transform58195. Уже это — побочный эффект вне
  собственной loose-детали.
- Subframe GO11354 / Transform47402 и rack GO32711 / Transform68773 — отдельные
  дети Chassis Transform38351. У подрамника прямые дети Bolts, `_Motor`,
  CarMotorPivot; у рейки — четыре BoltPM. Поэтому общий каскад рычагов/реек
  нельзя вывести лишь из деактивации transform-детей подрамника.
- У subframe/rack/wishbone прочитан `SAVEGAME` с сериализованными target=2,
  fsmName=BoltCheck, sendToChildren=false. Внутренняя семантика enum target=2
  отдельно не декодировалась; это не основание утверждать абсолютную локальность
  всех его побочных эффектов. Другие FSM-watchers и физические joint-реакции
  в этом узком проходе не исследованы; live-сверка оригинала не выполнялась.

Итого: первоначальная трактовка «только одна опора падает, остальное точно
остаётся» была сильнее доказательств. Текущий зависимый каскад — явное уточнение
пользователя, а не выдуманная полная расшифровка неизвестных donor-последствий.

### Приёмка актуальной редакции — pending

- FL и FR: монтаж рычага на незатянутый подрамник действительно завершается,
  затем рычаг и подрамник падают; предмет не остаётся в руках.
- С заранее установленными зависимыми деталями проверяется вся их транзитивная
  ветка; независимая кузовная/задняя ветка не падает. На общем подрамнике это
  может включать обе передние стороны.
- Повторить для ступицы/рычага, стойки или диска/ступицы, рейки/подрамника,
  тяги или колонки/рейки; закреплённая опора удерживает сборку.
- При анимированной передаче последовательность — монтаж и `PartInstalled`,
  затем срыв; раскручивание опоры во время анимации использует её итоговое B.
  Событие срыва не дублируется на одном объекте.
- Обратное условие полуоси, старые допустимые save-latch состояния и принятые
  положения/сжатие/вращение подвески должны сохранить прежнее поведение.

### Выполненная автоматическая проверка актуальной редакции

После прямого разрешения пользователя Editor PID17052 закрыт штатным
`CloseMainWindow`; завершение подтверждено без принудительного уничтожения
процесса или сохранения текущей runtime-сцены. Затем последовательно выполнены
два scoped batch gate Unity6000.3.11f1:

- `Logs/codex-suspension-ab-edit.xml` / `.log`: **250/250 PASS**, failed0/skipped0,
  PID5728 exit0. Включены 65 актуальных A-policy, 17 B-cadence, 12 save-latch,
  39 generated, 24 alignment, 6 steering target, 5 P0 fastener, 27 assembly,
  52 player interaction и 3 interaction audio tests.
- `Logs/codex-suspension-ab-play.xml` / `.log`: **52/52 PASS**, failed0/skipped0,
  PID33372 exit0. Включены A-policy4, B-cadence4, installed physics20,
  steering3, steering spawn8, handbrake2 и assembly11.

Новый A проверяет реальный commit перед cascade, один сигнал на каждый срыв,
двухуровневые зависимости, сохранение независимых веток, выход предмета из рук,
анимированную передачу FL/FR, изменение B во время передачи и отсутствие
постоянного развала от последующего T0. Новые тесты не потребовали ослабления
численных физических допусков. Для текущей редакции оба прогона прошли с первого
раза; исторические отказы старой редакции ниже не относятся к этим запускам.
Полные фильтры, хеши и параметры запуска — в
[отчёте совместного gate A/B](SATSUMA_WHEEL_BREAK_CADENCE_FIX_2026-09-04.md#6-реализация-и-состояние-проверки).

Ручная комплексная проверка пользователем всё ещё требуется. Исторические
233/233 и 47/47 ниже сохранены отдельно и не подменяют новый gate.

## 1. Основание и границы

- Поведенческий источник: замороженная версия `msc-world-baseline-04a1.1-c3f2f337`.
- Исходный файл: `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
- SHA-256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Точные donor FSM/ветви и различия до исправления:
  [передний аудит](SATSUMA_FRONT_ASSEMBLY_RULES_AUDIT_2026-09-04.md),
  [задний аудит](SATSUMA_REAR_ASSEMBLY_RULES_AUDIT_2026-09-04.md).
- Классификация: `BehavioralReference` → project-owned `Reimplemented` и
  `ConfigurationTransferred`. Donor FSM, код и runtime-зависимости не импортируются.
- Оригинальная установка read-only. Пакет не меняет геометрию, массы, коллизию,
  позы/оси, сход, пружины, демпферы, ход, радиусы, вращение колёс или NWH vendor.
  Ранее принятые изменения ручника и численных rotation/contact fixtures сохраняются.

## 2. История: наличие, попытка и удержание (`SupersededByUserRevision`)

Ниже сохранён первоначальный pre-commit отказ для аудита. Его timing, direct-drop
и поведение входящей детали заменены разделом 0; таблица mappings и обратные
условия полуоси остаются актуальными.

В `MountPointDefinition` добавлены совместимые пустые по умолчанию поля:

- `InstallAttemptBoltedSupportMountId`: опора, чья защёлка `Bolted` проверяется
  при настоящей попытке установки, а не при наведении.
- `InstallationBlockedWhileBoltedMountIds`: отдельные обратные запреты установки,
  используемые также read-only preview. Они не являются правилами постоянного
  удержания или ручного снятия.

Авторинг: `ConfigureInstallationChecks(string, string[])`.
`VehicleAssemblyValidator` проверяет ссылки обоих полей: неизвестные ID и
самоссылки — ошибки. Обратный gate в `AssemblyGraph` при неизвестной ссылке
или самоссылке запрещает установку, а не молча разрешает её.

Для следующих 12 mount-пар требуется наличие опоры (`RequiredOccupiedMountIds`),
но её незатянутость **не скрывает** допустимый preview:

| Входящая деталь / mount suffix | Адресная опора / mount suffix |
|---|---|
| `wishbone-fl`, `wishbone-fr` | `sub-frame` |
| `steering-rack` | `sub-frame` |
| `spindle-fl`, `spindle-fr` | `wishbone-fl`, `wishbone-fr` соответственно |
| `strut-fl`, `strut-fr` (stock) | `spindle-fl`, `spindle-fr` соответственно |
| `discbrake-fl`, `discbrake-fr` | `spindle-fl`, `spindle-fr` соответственно |
| `steering-rod-fl`, `steering-rod-fr` | `steering-rack` |
| `steering-column` | `steering-rack` |

Полный ID каждого mount — `mount.satsuma.` + suffix. Канонический подрамник —
`mount.satsuma.sub-frame`, не старый alias `mount.satsuma.subframe`.

`VehicleAssemblyController` проверяет опору до `TryOccupy`, `InstallAt`, запуска
анимации передачи и события `PartInstalled`:

1. Опоры нет / ссылка некорректна — `MissingPrerequisite`, без отсоединения.
2. Опора установлена, `Bolted=true` — обычная установка.
3. Опора установлена, `Bolted=false` — освобождается только её mount; опора
   отсоединяется в прежней мировой позе; публикуется один `PartBrokenLoose`.
   Входящая деталь не устанавливается. Результат —
   `Install / InstallationSupportDetached` (новое значение enum добавлено в конец,
   номер `17`; прежние значения не перенумерованы).

Это адресный donor `Drop`, **не** `CollapseMountHierarchy`. Уже установленные
зависимые/соседние детали не удаляются этим кодом каскадно. Постоянное слежение
«затяжка стала нулевой → развалить всё» не добавлено. Повторная попытка без
возвращённой опоры не создаёт второе событие отсоединения.

Проверка есть в прямой установке, немедленном handoff и анимированном handoff.
На завершении анимации состояние проверяется повторно: если опору раскрутили
за время перехода, успешная установка не фиксируется; существующая отмена
перехода возвращает входящую деталь в исходную позу/физическое состояние и
освобождает резерв mount.

### Полуоси и убранные лишние требования

- Для каждой `halfshaft-fl/fr` требуется установленный `discbrake` той же стороны
  **с `Bolted=false`**. При затянутом диске установка/preview запрещены без `Drop`.
  У полуоси поле адресной опоры пустое. Обе взаимозаменяемые полуоси используют
  правило выбранного mount, а не ошибочную глобальную привязку `part1/part2`.
- Рулевая тяга больше не требует заранее установленной ступицы.
- Тормозной диск больше не требует заранее установленной стойки.
- Четыре прежних глобальных `InstallRequiresBolted` и две зависимости тяга→ступица
  убраны: всего уникальных dependencies `40 → 34`. Нужные существующие зависимости
  наличия рейки/колонки/тяги сохранены; явное наличие на mount не создаёт нового
  постоянного structural-retention gate.

## 3. История: передача из рук (`SupersededByUserRevision`)

Ниже — прежний контракт отказа при loose support. В актуальной редакции такая
опора не вызывает pre-release отказ; capability остаётся чистой проверкой
возможности передачи, а монтаж с последующим развалом описан в разделе 0.

Добавлена необязательная capability `IMountHandoffPreReleaseTarget` с методом
`TryPrepareHandoff(IPickupTarget, in InteractionContext)`. Её реализуют оба
assembly target: marker и surface.

`PhysicalCarryController.TryHandoff` вызывает capability **до** `Release`.
Отказ потребляет попытку, но сохраняет `HasHeldObject`, held target и Rigidbody:
предмет не выпадает из рук, не получает ложные `MountHandoff`/`Drop` notifications
и соответствующий ложный звук передачи. Обработчик не проваливается в обычное
ЛКМ-бросание. Targets без новой capability и mounts без настроенной политики
сохраняют прежний поток. Отказ после уже начавшейся принятой анимации — отдельная
race-проверка, а не обещание автоматически вернуть предмет в руки.

## 4. Пороги группы крепежа и совместимость save

`T` — сумма стадий крепежа группы, не момент затяжки в Н·м. `B` — историческая
защёлка `Bolted`: включается на ON и сохраняется при ослаблении ниже ON до OFF.

| Группа (FL/FR, где применимо) | Максимум T | Прежний ON → новый ON | OFF |
|---|---:|---:|---:|
| Подрамник | 32 | 1 → 26 | 0 |
| Рулевая рейка | 32 | 1 → 24 | 0 |
| Рулевая колонка | 16 | 1 → 10 | 0 |
| Stock-стойка | 56 | 1 → 3 | 0 |
| Тормозной диск | 8 | 1 → 2 | 0 |
| Полуось | 24 | 1 → 2 | 0 |
| Рычаг | 16 | 2, без изменения | 0 |
| Ступица | 8 | 2, без изменения | 0 |
| Рулевая тяга | 8 | 8, без изменения | 0 |
| Переднее колесо | 32 | 1, без изменения | 0 |

Размеры, количество и индивидуальные максимумы крепежа сохранены. Refresh
проверяет исходную/новую форму групп до изменения реальных assets и отказывается
продолжать при неожиданной конфигурации.

Схема `VehicleAssemblySaveData` **не изменена**: текущая `2`, поддерживаемая
историческая `1`. Стабильные ID и DTO-поля не заменены. Для schema 2 сохранённый
`B=true,T=1` после старого ON=1 валиден при новом ON>1: это история защёлки,
а не причина насильно раскрутить сохранённую машину. При T=0 защёлка снимается.
`B=false,T=ON-1` также валиден; невозможные `B=true,T=0` и `B=false,T>=ON`
отвергаются до изменения живой сборки. Новые click-time правила не применяются
как повторные попытки монтажа к уже сохранённой установке.

В существующей миграции старой формы крепежа колонки уточнён только пересчёт B:
после remap сохранённая защёлка удерживается, если `IsLatchConsistent` её допускает.
Старый `boltpm-3` переносится в физический `boltpm-2` по существующему remap;
если удержание обеспечивал только удалённый ошибочный крепёж и после remap T=0,
защёлка снимается. Исходный DTO не изменяется. Schema 1 не содержала истории B:
для неё сохранена существующая реконструкция по актуальному ON, без выдумывания
утраченной истории. Это ограниченная совместимость, не переработка всей системы save.

## 5. Scoped refresh вместо повторной генерации машины

Builder имеет версию `11A-V1d.65`. Entry point:
`MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.RefreshFrontInstallationRulesBatch`.
Меню: `Tools/MSC Remake/Phase 1/Satsuma/Refresh Front Installation Rules`.

Refresh использует уже созданный hash-locked V64/V65 baseline, проверяет
канонические 117 mounts, пути 15 передних definitions и 40 старых / 34 новых
уникальных dependencies. Сначала проверяются одноразовые копии definitions;
затем сохраняются только изменённые definitions и scoped dependency edit prefab.
`GAME.unity` не парсится заново, presentation не перестраивается, полный builder
не запускается. Уже имеющийся `Phase1SatsumaV1aManifest.json` остаётся с
`builderVersion: 11A-V1d.64`: он описывает предыдущую полную генерацию, а не этот
узкий refresh. Его source hash совпадает с указанным выше.

Выполненные логи:

- `Artifacts/FrontInstallRules/FrontRules.Refresh.01.log`:
  `PHASE1_SATSUMA_FRONT_RULES_REFRESH_OK`, version `.65`,
  `changedMountDefinitions=15`, `removedLegacyDependencies=6`.
- `Artifacts/FrontInstallRules/FrontRules.Refresh.02.log`:
  тот же success marker, `changedMountDefinitions=0`, `removedLegacyDependencies=0`.
  Повторный запуск подтвердил идемпотентность scoped изменения.
- В обоих: `fullRebuild=false`, `manifestUnchanged=true`, замороженный source hash.

Это подтверждение refresh, **не результат выполнения gameplay-тестов**.
Generated prefab/definitions и временные build/compiler outputs остаются вне Git.

## 6. Исторические проверки до UserRevision

Все результаты этого раздела относятся к первоначальному контракту разделов
2/3/8. Они сохранены как факты выполненных прогонов, но не являются gate новой
post-commit редакции; её повторный gate pending.

| Проверка | Состояние на момент этого отчёта |
|---|---|
| Offline Roslyn compile | По выполненному root-прогону: 5 assemblies, 0 errors; 5 ранее существовавших warnings неиспользуемых rear-полей |
| Scoped refresh + повторный refresh | Success markers 15/6 и 0/0, логи прочитаны |
| Новый EditMode policy suite | 65/65 PASSED в обоих прогонах |
| Новый EditMode save suite | 12/12 PASSED в обоих прогонах |
| Новый PlayMode transition suite | 3/3 PASSED, каждый тест проверяет FL и FR |
| Общий выбранный EditMode gate | Финальный 233/233 PASSED, 0 skipped, process exit 0 |
| Общий выбранный PlayMode gate | Финальный 47/47 PASSED, 0 skipped, process exit 0 |
| Ручная приёмка в игре | PENDING |

Число policy cases выросло с первоначальных 61 до 65 после добавления четырёх
проверок неизвестной ссылки/самоссылки. Их 65/65 теперь подтверждены XML, а не
только инвентаризацией исходника.
Offline compile не заменяет импорт Unity, сериализацию или выполнение тестов.
Его вывод был сохранён в tool transcript; отдельного постоянного compiler log
на момент отчёта нет.

Первый общий EditMode прогон `Logs/codex-front-install-v65-edit.xml` дал 232/233.
Единственный отказ — старый
`Phase1SatsumaGeneratedContentTests.GeneratedAssemblyDependenciesPreserveDonorOrder`:
две прежние проверки требовали зависимости тяга→ступица, намеренно удалённой
пакетом A. Их ожидания изменены на отсутствие зависимости для FL/FR; runtime
ради теста не откатывался. Новые policy 65/65 и save 12/12 прошли уже этот прогон.

Финальный `Logs/codex-front-install-v65-edit-final.xml` / `.log`: 233/233,
0 failures, 0 skips, process exit 0. SHA-256 XML:
`97b60e26d1c6730314c07a494a0721656a9e24629694c2ff59150d10e0829dd1`.
Разбивка TestFixture: policy 65; save 12; generated 39; front alignment 24;
front steering target 6; P0 bolt parity 5; vehicle assembly 27; player interaction
52; interaction audio bridge 3 — все Passed. Результаты и hash прочитаны из
фактического XML; это не полная ручная donor-приёмка автомобиля.

Первый PlayMode `Logs/codex-front-install-v65-play.xml`: 39/47, 8 failures,
0 skips. Все восемь — `SatsumaFrontSteeringSpawnPlayModeTests`; прежняя подготовка
пыталась установить зависимую деталь при незатянутом подрамнике и закономерно
получала `InstallationSupportDetached`. Исправлена только подготовка этого
стенда: в обеих setup-ветках закрепляется подрамник, при `includeRods` — рейка;
helper проходит крепёж группы до `IsBolted`, а не пытается набрать ON одним
болтом. Проверяемые свободные стойка/тяга, численные assertions и допуски
не менялись. Первые новые transition-тесты уже прошли 3/3.

Финальный `Logs/codex-front-install-v65-play-final.xml` / `.log`: 47/47,
0 failures, 0 skips, process exit 0. SHA-256 XML:
`2a6e44f8b46d21da5d935cdbcd3db64ee8a7aee70ae4f8f41320e372411583f9`.
Разбивка TestFixture: assembly 11; новый install policy 3; front steering 3;
front steering spawn 8; handbrake 2; installed-part physics 20 — все Passed.
Обе итоговые XML прочитаны и хешированы после выполнения; первоначальные отказы
сохранены как история исправления fixtures, не скрыты новым зелёным итогом.

Новые EditMode проверки покрывают обе стороны: чистый preview, отсутствие опоры,
все три install entry points, один адресный Drop, сохранение соседнего/установленного
зависимого объекта, неизменность входящей детали, реальные marker/surface carry
targets и отсутствие ложных событий, прежний no-policy путь, обратный gate полуоси,
12 generated mappings, удалённые лишние зависимости, 17 threshold/hysteresis
контрактов и ошибочные ссылки. Save suite проверяет старые B/latch состояния,
schema 1, retired column remap, повторную запись/загрузку и отсутствие мутаций
при валидации/отказе.

Новые PlayMode проверки:

- `UnboltedSupportRejectsBeforeRealAnimationBeginsOnBothCorners`;
- `BoltedSupportCompletesRealAnimationAndInstallsOnceOnBothCorners`;
- `SupportLoosenedDuringAnimationIsRecheckedBeforeCommitOnBothCorners`.

Их fixtures синтетические, с явным владением объектами и двухкадровой очисткой;
это проверки перехода установки, не калибровка физики полной машины.

### Команды и ожидаемые артефакты

Unity: `6000.3.11f1 (3000ef702840)`. Запуски выполняются последовательно в
согласованном свободном окне проекта; не запускать второй Editor параллельно.

Offline, выполнено root (локальный ignored helper):

```powershell
& ./Artifacts/FrontInstallRules/Compile.ps1 -IncludeTests
```

Этот helper компилирует `MSC.Interaction.Runtime`, `MSC.Vehicle.Assembly`,
`MSC.LegacyImport.Editor`, `MSC.Tests.EditMode`, `MSC.Tests.PlayMode` через
существующие Bee response files с выводом в `Artifacts/FrontInstallRules/Compile`.

Scoped refresh, выполнен дважды (во втором вызове logFile `.02.log`):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe' -batchmode -quit -projectPath 'E:/GAYmDev_Studio/MySummerCar_Remake' -executeMethod MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder.RefreshFrontInstallationRulesBatch -logFile 'E:/GAYmDev_Studio/MySummerCar_Remake/Artifacts/FrontInstallRules/FrontRules.Refresh.01.log'
```

Unity test gates:

- EditMode: первоначальный `Logs/codex-front-install-v65-edit.xml` / `.log`,
  финальный `Logs/codex-front-install-v65-edit-final.xml` / `.log` (результат выше).
- PlayMode: первоначальный `Logs/codex-front-install-v65-play.xml` / `.log`,
  финальный `Logs/codex-front-install-v65-play-final.xml` / `.log` (результаты выше).
  Команды прочитаны из фактических логов, не предполагаются по именам файлов.
- Фактический финальный Edit filter:
  `MSC.Tests.EditMode.VehicleAssembly.SatsumaFrontInstallPolicyTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontLatchSaveCompatibilityTests;MSC.Tests.EditMode.LegacyImport.Phase1SatsumaGeneratedContentTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontAlignmentEditModeTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontSteeringAlignmentTargetTests;MSC.Tests.EditMode.LegacyImport.P0SatsumaBoltCheckParityTests;MSC.Tests.EditMode.VehicleAssembly.VehicleAssemblyEditModeTests;MSC.Tests.EditMode.PlayerInteraction.PlayerInteractionRuntimeTests;MSC.Tests.EditMode.AudioInteractionIntegration.InteractionAudioBridgeTests`.
- Фактический финальный Play filter:
  `MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontInstallPolicyPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaInstalledPartPhysicsPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaHandbrakePlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringSpawnPlayModeTests;MSC.Tests.PlayMode.VehicleAssembly.VehicleAssemblyPlayModeTests`.

Для запусков использованы `-batchmode -nographics -runTests -testPlatform EditMode`
или `PlayMode`, `-testFilter`, `-testResults` и `-logFile`; `-quit` к test-runner
команде не добавлялся. Прежние зелёные прогоны не подменяют проверку пакета A;
текущие результаты подтверждены завершёнными процессами и финальными XML.

## 7. Изменённые файлы именно пакета A

Пути относительно корня репозитория. Общий dirty worktree содержит предыдущие
этапы и параллельную UI-работу; они не входят в этот changelist.

Runtime / интеграция:

- `Assets/Game/Interaction/Runtime/Capabilities/IMountHandoffPreReleaseTarget.cs` **new**, вместе с `.meta`;
- `Assets/Game/Interaction/Runtime/Carrying/PhysicalCarryController.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/MountPointDefinition.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyGraph.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyOperation.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyValidator.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblyMountHandoffTarget.cs`;
- `Assets/Game/Vehicle/Assembly/Runtime/AssemblySurfaceMountHandoffTarget.cs`.

Авторинг и тесты:

- `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`;
- `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaFrontInstallPolicyTests.cs` **new**, вместе с `.meta`;
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontLatchSaveCompatibilityTests.cs` **new**, вместе с `.meta`;
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaFrontInstallPolicyPlayModeTests.cs` **new**, вместе с `.meta`;
- `Assets/Game/Tests/EditMode/LegacyImport/Phase1SatsumaGeneratedContentTests.cs`;
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontAlignmentEditModeTests.cs`;
- `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontSteeringAlignmentTargetTests.cs`;
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaFrontSteeringSpawnPlayModeTests.cs` — подготовка закреплённых опор;
- этот отчёт: `Docs/Phase1/SATSUMA_FRONT_INSTALLATION_RULES_FIX_2026-09-04.md` **new**;
- `Docs/Phase1/SATSUMA_SUSPENSION_ASSEMBLY_RULES_FIX_SPEC_2026-09-04.md` — статус A и ссылка на итог; B/C/D остаются открытыми.

Учёт после EditMode / PlayMode gates: `Docs/Porting/DONOR_AUDIT.md`,
`Docs/Porting/PORTING_MATRIX.md`, `Docs/Porting/SYSTEM_MAP.md`,
`Docs/Porting/PORTING_LEDGER.csv` и две scoped строки
`Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv` (`P1.CAR.002` / `.003`).

В существующих fixtures изменены прежние preview-gate ожидания и подготовка
реально закреплённых опор. Helper затягивает всю группу до её ON, а не пытается
добрать 26 стадий одним восьмистадийным болтом. Физические tolerances не ослаблены.

Локальные generated изменения, **не для Git**:

- `Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MountDefinitions/` —
  15 assets `mount.satsuma.{sub-frame,steering-rack,steering-column,wishbone-fl,wishbone-fr,spindle-fl,spindle-fr,strut-fl,strut-fr,steering-rod-fl,steering-rod-fr,discbrake-fl,discbrake-fr,halfshaft-fl,halfshaft-fr}.asset`;
- `Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab` — шесть удалённых dependency entries;
- `Artifacts/FrontInstallRules/Compile.ps1`, временные compiler outputs и refresh logs.

После действительных EditMode / PlayMode результатов добавлены scoped записи в четыре
Porting-документа и coverage строк `P1.CAR.002` / `P1.CAR.003`, обе остаются
`PartiallyImplemented`. Ручная приёмка там также отмечена PENDING.

## 8. Историческая ручная приёмка (`SupersededByUserRevision`)

Этот первоначальный чек-лист больше не является текущим ожиданием поведения.
В частности, «рычаг остаётся в руках» и сохранение зависимых деталей после
падения их опоры заменены актуальными критериями раздела 0.

Проверить на тестовой копии состояния, не перезаписывая единственный рабочий save.
Стороны указаны по автомобилю, а не по стоящему перед ним наблюдателю.

1. Установить подрамник без крепежа. Поднести рычаг к FL, затем повторить опыт
   отдельно для FR: preview есть; попытка установки роняет подрамник, рычаг
   остаётся в руках, ложного успешного монтажа/звука нет. Вернуть и закрепить
   подрамник; повторить установку — рычаг устанавливается.
2. На каждой стороне повторить адресный отказ и успешный вариант для
   ступица→рычаг, стойка→ступица, диск→ступица. После отказа не должны исчезать
   входящая деталь, противоположная сторона или неадресные установленные детали.
   Аналогично проверить рейка→подрамник, колонка/тяга→рейка.
3. На закреплённой рейке установить тягу до ступицы; на закреплённой ступице
   установить диск до стойки. Оба прежних лишних запрета должны отсутствовать.
4. Для каждой стороны поставить диск и оставить его `B=false`: полуось ставится.
   После затяжки диска до B установка полуоси запрещена без падения диска.
   Ослабить диск до OFF=0 — установка снова доступна. Проверить обе
   взаимозаменяемые полуоси в обоих передних mounts.
5. На новой, ещё не защёлкнутой группе проверить ON−1 и ON из таблицы.
   После достижения ON ослабление до положительного T ниже ON сохраняет B;
   OFF=0 снимает B. Достаточно нужной суммы T, не обязательно максимальной
   затяжки всех болтов. Не принимать доступность preview за доказательство B.
6. Проверить цепочку обычной сборки обеих сторон целиком, снятие/подбор после
   отказа, существующее выравнивание тяги/ступицы и отсутствие возврата прежних
   визуальных/физических дефектов. Для исторического save проверить сохранение
   допустимого B при T=1, отсутствие самопроизвольного развала после загрузки.

Ручное подтверждение пока отсутствует. Автоматическая race-проверка дополнительно
ослабляет опору между началом и концом handoff; она прошла в новом suite 3/3.

## 9. Что сознательно не исправлялось

**Историческая граница пакета A:** список ниже фиксирует то, что не входило
именно в реализацию A на момент её отчёта. Он не является текущим статусом всей
подвески: B впоследствии реализован отдельно, а C прошёл автоматические проверки
278/278 EditMode и 61/61 PlayMode. Актуальные результаты находятся в
`SATSUMA_WHEEL_BREAK_CADENCE_FIX_2026-09-04.md` и
`SATSUMA_MANUAL_REMOVAL_RULES_FIX_2026-09-04.md`; D остаётся открытым.

- **B:** секундный realtime cadence скоростных проверок колеса и разграничение
  частично закреплённого/полностью свободного состояния.
- **C:** donor manual-removal исключения, задние install/Drop условия и их отличия
  от постоянных structural-retention ограничений.
- **D:** недостающие скоростные BREAK/chance ветви и отдельные каскады.
- Полная электрика/двигатель, визуальное производство Phase 2, физический
  retuning и широкая перепись сохранений.

Автоматический gate редакции A/B 250/250+52/52 завершён. Исторические 233/233 и
47/47 не подтверждают новую редакцию, полную донорскую сборку/ощущения или
скоростной BREAK. Текущий C подтверждается только собственным gate
278/278+61/61; его ручная приёмка pending.
