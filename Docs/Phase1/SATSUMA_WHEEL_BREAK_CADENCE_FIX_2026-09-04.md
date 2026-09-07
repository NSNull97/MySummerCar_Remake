# Satsuma: пакет B — частота попыток срыва колёс

Дата: 2026-09-04. Статус: `ImplementedAutomatedPassedManualPending`. Разделы 1–5 — `BehavioralReference`, read-only сверка оригинала; реализация и фактически выполненные проверки записаны отдельно в разделе 6. Полная ручная проверка всех типов колёс не заявляется.

## 1. Источник и покрытие

Frozen `GAME.unity`: внешний DonorStaging, `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`, revision `msc-world-baseline-04a1.1-c3f2f337`. Зафиксированный SHA256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

Читались только отдельные объекты по `rg -b` → `FileStream.Seek` → следующий YAML-заголовок; полной загрузки GAME в RAM не было. Индекс: `Phase1SatsumaV1dBoltCheckAudit.csv`. Семантика проверялась по активным действиям, переходам, именам параметров и byteData, а не по эвристическим CSV-полям.

Проверены одинаковые `Bolts OFF`, `Bolts ON 2`, `Chance`, `Wait`, `Check wear`, четыре ветки выбора угла и `RestartOnEnable=1` у 12 колесных **Use FSM**:

| Семейство | Use component IDs, колёса 1–4 | Ветка установки |
|---|---|---|
| Stock steel | 113900, 111136, 108243, 111898 | `wheel_regula` |
| GT | 114016, 109103, 112738, 107110 | `wheel_regula` |
| Steelwide | 110710, 106371, 108155, 112484 | `wheel_offset` |

Колёсные имена 1–4 не означают жёстко закреплённые углы: каждый проверенный Use имеет `Installed? 2 -> FL 2 / FR 2 / RL 2 / RR 2 -> Check wear`. В этих четырёх ветках меняется `WheelCurrent`, но не Chance/Wait.

Все четыре Assembly FSM проверены отдельно. `Find correct part 2` выбирает `PART1` для `wheel_regula`, `PART2` для `wheel_offset`; `Assemble`/`Assemble 2` различаются целевым pivot и сходятся в `Set data -> End`. Отдельной политики таймера у pivot нет.

| Угол | Assembly FSM | Pivot1, regular GO | Pivot2, offset GO | ThisWheel GO |
|---|---:|---:|---:|---:|
| FL | 111654 | 30664 | 21889 | 29428 |
| FR | 110620 | 12959 | 30472 | 13829 |
| RL | 110008 | 17673 | 20387 | 33899 |
| RR | 105625 | 14380 | 29364 | 28534 |

Pivot ID взяты из финальной таблицы FSM-переменных; старые cached-значения inline-параметров в разных FSM могут совпадать и не являются текущей привязкой. Геометрия и вылет этим аудитом не пересматриваются. Другие покупные семейства из CSV отдельно не декодировались.

## 2. Точная цепочка времени

Обозначения: T — суммарный `Tightness`, V — исходная переменная `SpeedKMH`. Источник/знак V здесь не изменяются и не уточняются.

1. `Check wear` направляет в `Bolts OFF` (или через `Flat` туда же).
2. В `Bolts OFF` активны проверка T относительно `BoltedYES=1` и проверка V. T>=1 переводит в `Bolts ON 2`; V>5 вызывает `BREAK -> Break off`. На этом прямом пути **нет начального Wait**.
3. В `Bolts ON 2` T<=`BoltedNO=0` возвращает OFF; V>33 переводит в `Chance`. Оба сравнения работают каждый кадр. При первой подходящей проверке переход сразу в Chance: секунду перед первой попыткой FSM не ждёт.
4. `Chance`: два FloatOperator вычисляют `(32-T)/100`, затем `SendRandomEvent` получает BREAK с этим весом и FINISHED с весом 1. Задержка события равна нулю. BREAK идёт в `Break off`; FINISHED — в `Wait`.
5. `Wait` содержит **только одно активное действие Wait**, time=1.0, realTime=true, finishEvent=FINISHED. После него `FINISHED -> Bolts ON 2`, где актуальные T/V проверяются снова.

Контрольный одинаковый byteData `Wait` всех 12 FSM: `0000803f0046494e495348454401` (1.0, FINISHED, realTime=true). Ветки скорости содержат 5.0=`0000a040`, 33.0=`00000442`. Формулу вероятности в пакете B не менять: доказаны сырые веса; внутренний `ActionHelpers.GetRandomWeightedIndex` по-прежнему отдельно pending, и этот аудит не выдаёт текущую нормализацию проекта за новое доказательство donor helper.

Read-only прочитан исходный `Assets/Scripts/Assembly-CSharp/HutongGames/PlayMaker/Actions/Wait.cs` того же экспорта, SHA256 `512763417503687d9709e368a479bc6b2c6f996d2bdb657eba22a2c8fb8bc15b`:

- OnEnter записывает `startTime=FsmTime.RealtimeSinceStartup`, сбрасывает локальный timer.
- При realTime OnUpdate вычисляет разницу `FsmTime.RealtimeSinceStartup-startTime`.
- При timer>=time завершает действие и отправляет finishEvent.

Это не накопление FixedUpdate-ticks и не `Time.deltaTime`. Пропущенные интервалы не описаны как очередь нескольких Chance: после пробуждения происходит один возврат в ON и очередной переход по текущим значениям.

## 3. Что происходит во время уже начатого Wait

| Изменение | Что подтверждает FSM |
|---|---|
| V падает ниже 33 и поднимается до истечения секунды | В Wait нет сравнения скорости: дедлайн не отменяется и не запускается заново |
| Секунда истекла, а V<=33 | Возврат в ON2, но нового Chance нет. При последующем превышении 33 первая новая попытка без дополнительной начальной секунды |
| T становится 0 внутри Wait | В Wait нет сравнения T: остаток ожидания сохраняется; при возврате ON2 обнаруживается T<=0, затем действует OFF с отдельным >5 |
| T становится 32 внутри Wait | Ожидание также не прерывается |
| T уже 32, V>33 и FSM в ON2 | В ON2 нет условия T<32: всё равно входит в Chance, где исходный вес BREAK становится нулём; дальнейшее FINISHED снова даёт Wait |

Таким образом, фразу «полностью свободное колесо отрывается немедленно» нельзя распространять на **переход T→0 посреди уже активного Wait**. Немедленная ветка относится к OFF без текущего ожидания. Полностью затянутое колесо не должно срываться, но его нулевой по риску проход может поддерживать cadence, из-за чего последующее ослабление не обязано дать мгновенную новую попытку.

Для согласованного пакета B это означает: сохранять начатый дедлайн независимо от изменения T, полной затяжки и скорости; вне ожидания не вводить общий начальный cooldown для loose OFF >5. Проверку статистической вероятности и геометрию не трогать. Утверждение, будто любой T0 обязан обходить уже активный Wait, было бы отличием от прочитанного оригинала.

## 4. Жизненный цикл: доказанное и границы вывода

**Снятие и повторная установка.** У steel1 Removal113901 `Remove part` сбрасывает свой Use.Tightness и Use.Corner, посылает `RESETWHEEL` в wheel Assembly и в свой Use. В Use113900 global transition `RESETWHEEL -> Installed?`; при пустом Corner ветка уходит в `Idle`, а не возвращается в старый Wait. При повторной установке включённый Removal начинает с `State 2`, отправляет `INSTALL` в Use; далее `Idle -> Installed? 2 -> выбранный угол 2 -> Check wear -> Bolts OFF`. Следовательно, таймер предыдущей установки не следует переносить в новую occupancy. Этот подробный Removal-маршрут прочитан на steel1; другие Removal FSM в данном проходе полностью не сравнивались.

**Деактивация.** У всех 12 Use сериализован `RestartOnEnable=1`, startState=`State 2`. Это явное намерение рестартовать FSM при включении. Wait.OnEnter заново задаёт локальный startTime. Следовательно, сброс project-owned cadence при disable/новом enable соответствует этим настройкам; низкоуровневое выполнение PlayMaker.OnEnable и точный порядок при глобальной остановке приложения здесь не исследованы, live-захват не выполнялся.

**Загрузка сохранения.** Для steel1/GT1/steelwide1 проверены `State 1`/`Load`/`Save`: сохраняются Transform, Tightness, TireWear, TireType, Corner и массив Bolts; остаток Wait/startTime не сохраняется. Повторная инициализация идёт через `State 2 -> State 1 -> Load -> Tire type -> Installed?`, затем выбранный угол и Check wear. Новый remake cadence не должен попадать в DTO или переноситься из предыдущего runtime при restore.

**Отдельная задержка старта.** В `State 2` steel1/GT1/steelwide1 стоит Wait **0.2s realTime** (`cdcc4c3e0046494e495348454401`). Это задержка загрузочной инициализации, не пауза между Chance. В пакете B она сознательно не добавляется: scope — cadence существующего независимого runtime, а не воспроизведение donor startup glue.

**Пауза.** Исходное действие использует realtime-часы, но для перехода нужен следующий OnUpdate. Если обновления не выполнялись, по возобновлению истёкший дедлайн не означает догоняющую серию попыток. Точные отношения FsmTime с меню паузы, остановкой приложения и временными масштабами требуют отдельного исследования, если возникнет специфический баг; не следует выдавать их за выполненную live-проверку.

## 5. Проверки для реализации B

- Первая подходящая попытка без начального интервала; повторная не ранее 1.0 realtime-секунды после предыдущего FINISHED.
- 50 физических тиков за одну секунду не превращаются в 50 Chance; задержка нескольких секунд не вызывает догоняющую очередь.
- V вниз/вверх внутри Wait сохраняет исходный дедлайн; после его истечения при низкой скорости новая попытка ждёт только возвращения скорости.
- T→0 и T→32 внутри Wait не сбрасывают/не обходят его; уже свободное колесо вне Wait сохраняет отдельную прямую >5 ветку.
- Два колеса имеют независимые дедлайны; снятие/замена на том же mount, disable и restore не наследуют старый таймер.
- Подтверждать число обращений к выборке управляемыми часами, не flaky-статистикой. Пороговые скорости, разовая формула вероятности, позы/вращение/вылет не меняются.

## 6. Реализация и состояние проверки

Project-owned `FastenerGroupState` в `Assets/Game/Vehicle/Assembly/Runtime/AssemblyRuntimeState.cs`
теперь хранит отдельный runtime-only дедлайн `nextSpeedRetentionCheckRealtime`.
`ShouldEvaluateSpeedRetention(float speedKph, double realtimeSeconds)` разрешает
первую подходящую оценку сразу, повторную — не раньше чем через 1.0 realtime-секунды.
Дедлайн устанавливается от текущего времени, не накапливает пропущенные попытки,
не сбрасывается изменением T/скорости и распространяется на уже начатый Wait
при последующем T=0/32. Вне Wait свободное колесо сохраняет прямой путь V>5.

`VehicleAssemblyController.EvaluateFastenerRetentionPolicies` передаёт скорость
из прежнего Rigidbody и `Time.realtimeSinceStartupAsDouble` в ограниченную
`EvaluateFastenerRetentionPoliciesAt` проверку. Существующий детерминированный
sampler вызывается только после допуска группы; `retentionPolicyTick` теперь
считает реальные оценки, не физические кадры. Каждый занятый wheel mount
имеет независимый дедлайн, включая regular/offset точки всех четырёх углов.

`FastenerGroupState.Reset` сбрасывает дедлайн при освобождении/повторной установке
mount и успешном restore. `VehicleAssemblyController.OnDisable` сбрасывает
расписания до раннего выхода, даже без незавершённых install transitions.
Configure создаёт новый runtime graph. Таймер не добавлялся в DTO: schema,
stable IDs, стадии и сохранённый `Bolted` остаются прежними. Неудачная валидация
сохранения не запускает restore/reset. Прямой публичный
`TryApplyFastenerBreakPolicy` сохраняет прежнюю семантику явной команды;
секундное расписание относится к автоматическому наблюдению из FixedUpdate.

Формула разовой вероятности, пороги 5/33 км/ч, break action, радиус, вылет,
вращение, геометрия, силы и NWH не менялись. Новые serialized-поля и пересборка
generated baseline не нужны. Пакеты C/D не реализовывались. Поведение развала
передка изменено отдельно по уточнению пользователя, см.
[актуальную редакцию A](SATSUMA_FRONT_INSTALLATION_RULES_FIX_2026-09-04.md).

Новые файлы вместе с `.meta`:

- `Assets/Game/Tests/EditMode/VehicleAssembly/WheelFastenerRetentionCadenceTests.cs`: 17 cases;
- `Assets/Game/Tests/PlayMode/VehiclePhysics/WheelFastenerRetentionCadencePlayModeTests.cs`: 4 UnityTests.

Edit tests используют управляемое realtime-время и считают допуски, а не
статистическую частоту удачного BREAK. Play fixtures вызывают реальный controller
с фиксированным временем на независимом графе четырёх колёс: счётчик попыток,
отрыв T0 после дедлайна, снятие/повторная установка, restore, disable и отдельная
явная команда. Синтетические fixtures не загружают GAME, baseline или NWH.

Выполнено: `& 'Artifacts/FrontInstallRules/Compile.ps1' -IncludeTests` — 5 assemblies
(`MSC.Interaction.Runtime`, `MSC.Vehicle.Assembly`, `MSC.LegacyImport.Editor`,
`MSC.Tests.EditMode`, `MSC.Tests.PlayMode`), 0 ошибок; 5 прежних CS0414 неиспользуемых
rear-полей. Игнорируемый helper расширен двумя новыми test sources. Это проверка
компиляции, **не исполнение NUnit/Unity тестов**. `git diff --check` без ошибок;
имеются предупреждения Git о нормализации CRLF в других изменённых файлах.

**EditMode/PlayMode выполнены.** Пользователь явно разрешил закрыть редактор;
PID17052 подтвердил штатный `CloseMainWindow` и завершился. На момент закрытия
Editor status показывал активный Play, compilationFailed=false, import/test job
не выполнялся. Сцену не удаляли/не перезаписывали, принудительное уничтожение
процесса не потребовалось. После тестов окно передано параллельной UI-задаче.

| Gate | Фактический результат | Артефакты |
|---|---|---|
| EditMode | 250/250 PASS, failed0/skipped0, PID5728 exit0 | `Logs/codex-suspension-ab-edit.xml` / `.log` |
| PlayMode | 52/52 PASS, failed0/skipped0, PID33372 exit0 | `Logs/codex-suspension-ab-play.xml` / `.log` |

SHA256 Edit XML: `65723691df0d508dab27db2652eded1b78311eb64a67a86af1e3084972426d29`.
SHA256 Play XML: `34c739ce7240d950940bbefea90e8f307eb93e4157c79d329b60dc36abd3e5da`.
Оба XML лично прочитаны после завершения процессов. В этой редакции оба gate
прошли с первого раза. Предыдущие233/233 и47/47 остаются только историей A.

Разбивка Edit: A-policy65; B-cadence17; save-latch12; generated39; alignment24;
steering target6; P0fastener5; assembly27; player interaction52; interaction audio3.
Разбивка Play: A-policy4; B-cadence4; installed physics20; steering3; spawn8;
handbrake2; assembly11. Все указанные fixtures Passed, пропущенных нет.

Параметры Unity6000.3.11f1: `-batchmode -nographics -projectPath <repo> -runTests
-testPlatform EditMode|PlayMode -testFilter <filter> -testResults <xml> -logFile <log>`.
Для Test Runner `-quit` не передавался. Процессы запускались последовательно,
через PowerShell `Start-Process -WindowStyle Hidden -PassThru` и `WaitForExit`.
Пересборка baseline/переимпорт GAME для этого шага не запускались.

Точный Edit filter:
`MSC.Tests.EditMode.VehicleAssembly.WheelFastenerRetentionCadenceTests;MSC.Tests.EditMode.VehicleAssembly.SatsumaFrontInstallPolicyTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontLatchSaveCompatibilityTests;MSC.Tests.EditMode.LegacyImport.Phase1SatsumaGeneratedContentTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontAlignmentEditModeTests;MSC.Tests.EditMode.LegacyImport.SatsumaFrontSteeringAlignmentTargetTests;MSC.Tests.EditMode.LegacyImport.P0SatsumaBoltCheckParityTests;MSC.Tests.EditMode.VehicleAssembly.VehicleAssemblyEditModeTests;MSC.Tests.EditMode.PlayerInteraction.PlayerInteractionRuntimeTests;MSC.Tests.EditMode.AudioInteractionIntegration.InteractionAudioBridgeTests`.

Точный Play filter:
`MSC.Tests.PlayMode.VehiclePhysics.WheelFastenerRetentionCadencePlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontInstallPolicyPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaInstalledPartPhysicsPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaHandbrakePlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringSpawnPlayModeTests;MSC.Tests.PlayMode.VehicleAssembly.VehicleAssemblyPlayModeTests`.

## 7. Ручная совместная проверка A/B

После автоматического gate пользователь проверяет оба пакета вместе на тестовой
копии состояния. Сначала новая установка на незакреплённой опоре должна завершить
монтаж и обрушить зависимую сборку, не кузов/независимые ветки. Затем сравнить
полностью свободные, частично и полностью затянутые колёса на скорости:
строгие >5 и >33, отсутствие отрыва полностью затянутого колеса, отсутствие
изменений посадки и вращения. Частичный BREAK случаен — отсутствие отрыва за
одну секунду не означает неисправность. Проверка точного числа попыток уже
выполнена в детерминированных тестах; ручная проверка оценивает игровой сценарий.

Следующий отдельный пакет после совместной приёмки A/B — C, ручное снятие и
задние условия неудачной установки; он не входит в настоящую реализацию.
