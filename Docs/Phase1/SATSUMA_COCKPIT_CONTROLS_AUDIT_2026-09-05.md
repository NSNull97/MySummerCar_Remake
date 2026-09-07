# Satsuma: руль, приборка и замок зажигания

Дата: 2026-09-05. Статус: **ReadOnlyAuditComplete**.
Классификация: `BehavioralReference`; новых donor-настроек в runtime этим
проходом не переносилось.

Последующее исправление штатных приборов, часов, одометра и подсветки выполнено
пакетом 2026-09-07: `SATSUMA_VISUAL_AUDIO_PACKET_2026-09-07.md`. Ниже сохранён
исторический срез аудита 2026-09-05, а не актуальное утверждение об отсутствии
этих контроллеров. High-beam input и неподдержанные дополнительные приборы
по-прежнему не объявлены закрытыми.

## Границы аудита

Проверены:

- физический замок зажигания, положения ключа, ввод и условие наличия ключа;
- связь замка с `Starter`, `Electrics` и проводкой;
- визуальное вращение салонного руля;
- установка руля и рулевой колонки;
- установка и крепёж приборной панели;
- включение органов управления на приборке;
- базовые FSM спидометра, температуры, топлива и одометра;
- соответствующие контроллеры, определения, prefab и тесты remake.

Не объявлены полностью проверенными: весь граф контрольных ламп, все звуковые
вариации, donor-сохранение состояния `ACC`, тахометр и дополнительные
mail-order приборы. Для них ниже явно оставлен `Pending`, а не придуманная
семантика.

Подвеска, NWH, колёса, двигатель, кузов, ручник, сохранения вне cockpit и
принятая геометрия не изменялись. Unity не запускалась. `Assets`,
`ProjectSettings` и `Packages` не изменялись.

## Источник и метод

Authority — зафиксированная версия `msc-world-baseline-04a1.1-c3f2f337`,
Steam build `20171487`:

```text
E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/
  milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity
```

SHA-256 `GAME.unity`:
`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`.

Дополнительные frozen-источники:

- `Assets/Resources/PlayMakerGlobals.asset`, SHA-256
  `0C232081E5DD2D6611D27CCD06274CBB31F5FABC5EF5E5F901B86D39DA25AFEC`;
- managed reference `SteeringWheel.cs`, SHA-256
  `58A3340C51C9D1DE0A1E1A24D21D460F8AD6DB4EC29FEBEE60B03FA9A18E043B`;
- managed reference `CarDynamics.cs`, SHA-256
  `64BC8F1A54E0CFCCBEEE9740B453659B45D2553652A9A43CF6784D310E9F328F`;
- `WorldObjectPlacements.csv`, recorded source SHA-256
  `39E5C8F38EDD83652325B403E06449EB6FE4E5C588E4DAD2BF04B5C9FF406C31`.

По известному Component ID заголовок YAML находился через `rg -b`, затем
читался bounded-диапазон от его byte offset до следующего YAML-документа.
Весь 234 MB `GAME.unity` в память не загружался. Состояния и условия ниже
подтверждены raw `actionNames`, `paramData`, object references и `byteData`,
а не выведены из названий объектов.

## 1. Короткий вердикт

Нижний слой remake уже не пустой: есть рабочая симуляция двигателя,
электрическая сеть, проводка, сборка колонки/руля и геометрия приборов.
Проблема выше по цепочке — **нет физического cockpit-моста от игрока к этим
системам**.

Точные отличия:

| Узел | Оригинал | Текущий remake | Итог |
|---|---|---|---|
| Зажигание | физический LMB-замок, ключ, `OFF / ACC / START`, удержание `0.4 s` | бинарный `Ignition` на `I` и отдельный `Starter` на `Enter` | механика обходится клавишами и теряет три положения |
| Салонный руль | `CarSteeringPivot`, до `+/-450 deg`, следует raw steering input | передние колёса управляются, cockpit presenter отсутствует | руль визуально не связан с рулением |
| Приборы | отдельные FSM стрелок и одометра | donor-меши есть, контроллеров стрелок/одометра нет | приборка остаётся статической |
| Крепёж приборки | `Bolted ON=12`, `OFF=0` | `ON=1`, `OFF=0` | приборка считается прикрученной почти сразу |
| Доступ к кнопкам | dashboard installed **и** meters bolted | wiper-target не проверяет эти условия | дворники доступны раньше времени |
| Установка руля | trigger существует внутри установленной колонки; 4 варианта | mount принадлежит body-shell, без зависимости от колонки; 2 варианта | руль можно предложить без колонки, Sport/Rally пока отсутствуют |
| Питание | батарея, две затянутые клеммы, battery harness, ignition wiring, заряд `>97`, затем `/10` | тот же набор и порог `>9.7 V` | базовая топология в этой части соответствует оригиналу |

## 2. Замок зажигания в оригинале

### 2.1 Объекты

- `IgnitionSatsuma`: GO `11158`, Transform `47206`, путь
  `.../steering_column2/IgnitionSatsuma`;
- collider component `99431`;
- PlayMaker component `107294 @ byte 104412228`, FSM `UseNew`,
  `RestartOnEnable=1`;
- родитель `Ignition`: GO `13877`, Transform `49936`;
- `Keys`: GO `11898`, Transform `47944`;
- видимый ключ `CarKey`: GO `15481`, Transform `51537`;
- `keyhole`: GO `15768`, Transform `51832`.

Сам interactive-объект находится под `steering_column2`. Поэтому в donor он
физически появляется вместе с установленной колонкой; это не независимый
замок на голом кузове.

### 2.2 Подтверждённый переход состояний `UseNew`

| State @ byte | Активные действия | Переход |
|---|---|---|
| `Wait player @ 104412638` | `SetBoolValue`, `SetStringValue`, `MousePickEvent` | `FINISHED -> Wait button` при наведении |
| `Wait button @ 104415384` | `MousePickEvent`, `GetMouseButtonDown(button=0)`; label `IGNITION` | клик `ACC -> Check key`; уход курсора `FINISHED -> Wait player` |
| `Check key @ 104444235` | `IntCompare(PlayerKeySatsuma, 1)` | `KEY -> Sound`, `NOKEY -> Wait button` |
| `Sound @ 104446029` | включает GO `15481`; `MasterAudioPlaySound(CarFoley/carkeys_in)` | `FINISHED -> Test` |
| `Test @ 104429570` | проверяет локальные `ACC`, `MotorOn` | выбирает `OFF`, `ON`, `ACC` либо первый `ACC on` |
| `ACC on @ 104436604` | ключ local X `-30 deg`; `Starter.ACC=true`; `DashVolume=.5`; `Wait(0.4, realTime=true)` | отпускание LMB -> `Wait button`; выдержка -> `Motor starting` |
| `ACC on2 @ 104440552` | ключ local X `-30 deg`, ACC остаётся включён | отпускание -> `Motor OFF`; выдержка `0.4 s` -> `Motor starting` |
| `Motor starting @ 104418561` | ключ local X `-60 deg`; `Starter.Starting=true`; `MotorOn=true` | отпускание -> `Shut off` |
| `Shut off @ 104433827` | ключ local X `-30 deg`; `Starter.Starting=false` | `FINISHED -> Wait button` |
| `Motor OFF @ 104421825` | ключ X `0`; выключает key, `Electrics`, warnings; `ACC=false`, `MotorOn=false`, `Starter.ACC=false`, `Starter.ShutOff=true`; `DashVolume=0`; `CarFoley/carkeys_out` | `FINISHED -> Wait button` |

Следовательно, подтверждённое управление такое:

1. Используется только физический **LMB**, не `F`, не `I` и не отдельный
   `Enter` для стартера.
2. Первый короткий клик вставляет ключ и оставляет его в `ACC` (`-30 deg`).
3. Удержание LMB не менее `0.4 s` переводит подпружиненный ключ в `START`
   (`-60 deg`).
4. Отпускание из `START` возвращает ключ в `ACC` и снимает сигнал стартера.
5. Следующий короткий цикл из `ACC` выключает зажигание, возвращает угол в
   `0 deg` и вынимает ключ.

Глобальная переменная `PlayerKeySatsuma` — `int`, serialized default `0`; FSM
пропускает к состоянию `Sound` только значение `1`. Этот default **не является
fresh-game значением**: `Database/Keys/PlayerKeys` ниже подтверждённо задаёт
Satsuma key в `1` при создании нового donor-сохранения. В remake отдельной
подтверждённой механики владения ключом Satsuma в просмотренном скоупе нет.

`UseNew` не содержит прямых Save/Load action. Сохранение `ACC` или угла ключа
оригиналом в этом проходе **не доказано**.

## 3. Питание и проводка зажигания

`Electrics`: GO `22680`, Transform `58734`, component
`110521 @ byte 163709337`, изначально inactive.

State `Wiring @ 163711426` читает пять donor database-значений:

- Battery `26601 / Data.Battery`;
- BatteryPlus `21525 / Data.Bolted`;
- BatteryMinus `3446 / Data.Bolted`;
- WiringIgnition `23629 / Data.Installed`;
- WiringBatteryHarness `30568 / Data.Installed`.

`BoolAllTrue` отправляет FSM в `Battery`, иначе в `Not Ok`.
`Battery @ 163756639` сравнивает `Battery/Data.Charge` с `97`. В состоянии
`OK @ 163717543` charge делится на `10`, результат становится напряжением,
`ElectricsOK=true`; проверка повторяется через `1 s realTime`. `Not Ok @
163720682` гасит warnings и снимает `ElectricsOK`. `Engine off @ 163762655`
дополнительно задаёт `Starter.ShutOff=true`.

`Wiring status`, component `107955 @ byte 117314548`:

- state `Steering` требует установленную steering column и только затем
  активирует ignition endpoint Transform `63749`;
- state `Dash` требует установленный dashboard и dashboard meters, затем
  активирует dashboard endpoints Transform `70777` и `60508`.

Пара ignition-проводки:

- `FuseboxIgnition`: component `105688`, GO `5625`, Transform `41681`;
- `IgnitionFusebox`: component `111940`, GO `27694`, Transform `63749`;
- общая DB `WiringIgnition 23629`, tolerance `0.1`, labels `FUSEBOX` и
  `IGNITION SWITCH`.

### Состояние remake

`SatsumaElectricalSystem.ElectricsOk` уже требует батарею, обе клеммы,
`BatteryHarness`, `Ignition`, обе клеммы на stage `8` и напряжение
`>9.7 V`. Это соответствует donor-проверке `Charge > 97` после деления на
`10`; переписывать эту часть оснований нет.

Builder уже авторит три необходимые cockpit-линии:

| Connection | Donor wire root GUID | Endpoints | Условие endpoint |
|---|---|---|---|
| `Dash1` | Transform `47855`, `3d3dd037494c743459863835417239a1` | `56657 FUSEBOX`, `70777 INSTRUMENT PANEL 1` | dashboard + meters для panel |
| `Dash2` | Transform `51049`, `7c5e9d95b59b9034aa42181561235837` | `60508 INSTRUMENT PANEL 2`, `68787 FUSEBOX` | dashboard + meters для panel |
| `Ignition` | Transform `59161`, `cfe641defee778e40a43c7ed120df1f1` | `41681 FUSEBOX`, `63749 IGNITION SWITCH` | steering column для switch |

Пробел здесь не в самой проводке, а в том, что физический замок не управляет
авторитетным ignition/starter состоянием.

## 4. Зажигание в текущем remake

`VehicleInputRouter` предоставляет только:

- бинарный `IgnitionOn`;
- toggle-action `Ignition` (`I` / gamepad North);
- held-action `Starter` (`Enter` / gamepad South).

Состояние сохраняется как один `bool ignitionOn` в `VehiclePersistence`.
`VehicleSimulationNodes` глушит двигатель при `IgnitionOn=false`, а
`AssemblyVehiclePrerequisiteAdapter` выдаёт `IgnitionOff` и
`BatteryVoltageLow`. То есть абстрактный двигатель реагирует правильно, но
ему неизвестны `OFF / ACC / START`, физический ключ и `0.4 s` удержания.

В canonical Satsuma prefab `VehicleInputRouter` сериализован с `m_Enabled: 0`.
В просмотренных cockpit/driver-seat файлах владелец, который включает его при
посадке, не найден. Это ограниченный вывод аудита, а не утверждение обо всём
проекте: параллельные изменения после frozen-среза могут добавить такого
владельца.

## 5. Салонный руль

### 5.1 Donor presentation

- `CarSteeringPivot`: GO `31155`, Transform `67214`, путь
  `SATSUMA(557kg, 248)/Dashboard/Steering/CarSteeringPivot`;
- custom component `113045 @ byte 210238491`;
- `maxSteeringAngle=450`, `rotateAroundY=0`, `invertRotation=0`.

`SteeringWheel.cs` сохраняет исходный local Z и каждый кадр задаёт:

```text
localZ = initialLocalZ + carController.steering * 450
```

`CarDynamics.cs` передаёт выбранный Axis/Mouse/Mobile controller в этот
component. Установленная колонка GO `16469`, Transform `52534`, является
потомком `CarSteeringPivot`; pivot руля GO `12464`, Transform `48510`, —
потомок установленной колонки. Поэтому колонка и руль следуют именно raw
normalized steering input, а не уже ограниченному скоростью углу дорожных
колёс.

В remake передняя кинематика работает через
`SatsumaFrontSteeringController`, но отдельного presenter, который вращает
`CarSteeringPivot` на `+/-450 deg`, в просмотренном runtime/prefab нет.

### 5.2 Donor assembly руля

Assembly component `113042 @ byte 210110098`, GO `31145`, Transform `67204`,
FSM `Assembly`. Trigger расположен по пути
`.../steering_column2/trigger_steering_wheel`; сам `steering_column2`
активируется Assembly component `109461` после установки колонки.

FSM принимает четыре donor-варианта:

| Ветка | GO | DB | State @ byte |
|---|---:|---:|---:|
| GT | `20597` | `6448` | `GT @ 210115517` |
| Rally | `32615` | `31624` | `Rally @ 210135143` |
| Sport | `31914` | `35545` | `Sport @ 210145666` |
| Stock | `6005` | `29660` | `Stock @ 210156189` |

Dispatch states: `Check collision @ 210110523`, `Find correct part2 @
210126354`, `Find correct part3 @ 210164259`.

Текущий `mount.satsuma.steering-wheel`:

- owner — `body-shell`, а не установленная steering column;
- не требует occupied `mount.satsuma.steering-column`;
- принимает только stock и GT;
- имеет правильную центральную гайку `10 mm`, max/stage `8`, но неверный
  исходный ON `1` при OFF `0`: отдельная raw-FSM сверка ниже уточнила ON `2`.

Sport/Rally IDs пока существуют только в mail-order compatibility defaults;
генерировать фиктивные loose parts ради списка нельзя. Уточнение C1b:
`SATSUMA_COCKPIT_STEERING_DEPENDENCY_AUDIT_2026-09-05.md` подтвердил у всех
четырёх вариантов ON2/OFF0 и необходимость отдельного install-only gate.
Обычный `RequiredOccupiedMountIds` использовать нельзя: он добавляет
неподтверждённые обратные removal/collapse зависимости. Предыдущие утверждения
про правильный ON1 и достаточность прежнего контракта были ошибочными.

Рулевая колонка в remake в проверенной части совпадает: два donor-болта
`8 mm`, aggregate max `16`, ON `10`, OFF `0`, установка требует steering
rack. Donor: Assembly component `109461`; installed GO/Transform
`16469/52534`, secondary `steering_column2` `1468/37528`, BoltCheck
component `108776`, ON `10`, OFF `0`.

## 6. Приборка

### 6.1 Установка и доступ к кнопкам

Dashboard meters:

- loose GO `16325`, Transform `52392`;
- Assembly component `107433 @ byte 106979987`;
- trigger GO `11661`, Transform `47710`;
- pivot Transform `17688`;
- DB `DashboardMeters 16587`;
- viewer `108741`, BoltCheck `108742`, Removal `108743`.

`BoltCheck 108742 @ byte 132003213`:

- `Bolts OFF @ 132003626`: `Data.Bolted=false`, ждёт
  `Tightness >= 12`;
- `Bolts ON @ 132009156`: `Data.Bolted=true`, возвращается в OFF при
  `Tightness <= 0`.

Следовательно, точный donor latch: **ON `12`, OFF `0`**. В текущем
`mount.satsuma.dashboard.meters.asset` стоит **ON `1`, OFF `0`** — это
подтверждённое authoring-расхождение.

Dashboard control activation — component `105853 @ byte 75974544`, state
`Status @ 75974948`. Он читает `Dashboard 24983 / Data.Installed` и
`DashboardMeters 16587 / Data.Bolted`, затем `BoolAllTrue` включает/выключает
GO `ButtonsDash 30221`; проверка повторяется через `4 s realTime`.

В remake builder добавляет физический
`SatsumaWiperSwitchInteractionTarget` на donor knob Transform `75512`, но его
`CanInteract` проверяет только active target и наличие controller. Условия
dashboard installed + meters bolted отсутствуют. Поэтому кнопка доступна
раньше, чем в оригинале.

### 6.2 Подтверждённые стрелки

| Прибор / component | Вход | Donor-преобразование | Gate |
|---|---|---|---|
| Speed `104548 @ 51563582` | `Drivetrain 112082.differentialSpeed` | clamp `0..250`, multiply `-1.6875`, needle Transform `58688` local Y, every frame | отдельного wiring/ACC gate в FSM нет |
| Coolant `107749 @ 113632196` | `Cooling 7280 / CoolantTemp` | multiply `-0.461`, clamp `[-75,0]`, needle Transform `71635` local Y | WiringDash1 installed + `ElectricsOK`; recheck через Wait |
| Fuel `109205 @ 139945333` | `FuelTank / FuelLevel` | multiply `-1.66`, clamp `[-70,0]`, needle Transform `60352` local Y | WiringDash1 + ElectricsOK + WiringFueltank + `Starter.ACC`; иначе Reset в `0` |

У спидометра отсутствие электрического gate подтверждено просмотренной FSM;
не следует добавлять ему выдуманное условие только потому, что оно кажется
физически логичным.

### 6.3 Одометр

Component `105464 @ byte 68919445`, FSM `Data`:

- donor save tags `SatsumaOdo` (`int OdometerReading`) и `SatsumaOdoF`
  (`float Odo`);
- default `OdometerReading=10000`;
- state `Calculate distance @ 68928145` использует `SpeedKMH`, делит на
  `3.6`, накапливает дистанцию и вращает digit wheels;
- `Wait @ 68962785` содержит период `8 s` и ограничение показаний
  `10000..99999`.

В remake-меше `52392_dashboard_meters_Clone_.prefab` уже есть 21 renderer,
включая needles, knobs и digit wheels, но donor MonoBehaviour/PlayMaker
отсутствуют по правилам проекта. Project-owned контроллеров стрелок,
одометра и предупреждающих ламп в просмотренном runtime нет. Это именно
отсутствующая gameplay/presentation-логика, а не повод импортировать donor FSM.

## 7. Что уже покрыто тестами, а что нет

Существующие тесты покрывают:

- 26 electrical bindings / 52 endpoints, порядок пар, restore и питание;
- wiper mode, потерю питания и возврат щёток в park;
- один центральный крепёж руля `10 mm`;
- два болта рулевой колонки `8 mm`, ON `10`;
- install/latch/save policy рулевой колонки;
- абстрактный `IgnitionOn`, запуск/глушение двигателя и bool в vehicle DTO.

Не найдено focused-покрытие:

- физического замка, key possession, `0/-30/-60 deg` и `0.4 s`;
- связи физического замка с authoritative ignition/starter/electrics;
- запрета interaction без установленной колонки;
- салонного руля `+/-450 deg`;
- wheel mount prerequisite на колонку и четырёх donor-вариантов;
- dashboard meters ON `12` / OFF `0`;
- общего gate `dashboard installed && meters bolted` для dashboard controls;
- калибровок speed/temp/fuel и их точных power gates;
- одометра и его persistence.

В рамках этого read-only аудита тесты не запускались. Их текущий зелёный или
красный статус здесь не заявляется.

## 8. Минимальный следующий фикс-пакет

Рекомендуемый следующий пакет один: **Cockpit P1 — физическое зажигание и
структурные gate**. Он не должен трогать принятую физику автомобиля.

1. Добавить project-owned `SatsumaIgnitionController` с состояниями
   `Off`, `Accessory`, `Starting`; transient `Starting` не сохранять.
2. Добавить physical continuous interaction target на reviewed
   `IgnitionSatsuma/Keys` refs: LMB, порог `0.4 s`, углы local X
   `0/-30/-60 deg`, проверка project-owned possession ключа.
3. Через явный интерфейс подавать authoritative ignition/starter intent в
   существующий simulation input; никаких donor name/path lookups в runtime.
   Это не должно преждевременно включать весь `Vehicle` action map: владельца
   режима посадки в текущем проекте ещё нет.
4. Звуки `carkeys_in`, `ignition_keys`, `carkeys_out` маршрутизировать через
   `IAudioBackend` и project-owned event IDs, не по donor clip name.
5. Исправить `dashboard meters` latch с `1/0` на `12/0` и добавить единый
   gate dashboard installed + meters bolted для dashboard targets.
6. Добавить steering-wheel mount prerequisite на установленную колонку.
   Sport/Rally добавлять только после появления реальных PartDefinition и
   presentation, а не пустыми ID.

Совместимость save: старый `bool ignitionOn` нельзя молча удалить. При
введении enum нужен versioned migration policy, например `false -> Off`,
`true -> Accessory`; это **проектное решение совместимости**, не доказанная
donor-семантика. `Starting` никогда не должен восстанавливаться как постоянное
состояние.

Следом, отдельными пакетами, а не вперемешку:

- Cockpit P2: `SatsumaCockpitSteeringPresenter`, serialized pivot и raw input
  `+/-450 deg` по local Z;
- Cockpit P3: project-owned speed/temp/fuel gauges, odometer persistence и
  только после отдельного raw-аудита warning lamps/tachometer.

## 9. Pending и ограничения

- Полная семантика каждой warning lamp не декодировалась; подтверждено лишь
  их групповое выключение в `Electrics`/`UseNew`.
- Persistence donor `ACC` и положения ключа не доказан: в `UseNew` прямых
  Save/Load action нет.
- Спортивный и rally руль подтверждены donor Assembly, но их полноценные
  generated loose definitions/presentation в текущем remake отсутствуют.
- Результаты аудита относятся к просмотренному frozen-срезу; параллельный
  builder может добавить новые компоненты после этого документа.
- Никакой full Satsuma rebuild, prefab refresh или Unity validation в этом
  проходе не выполнялись.

## 10. Уточнение: input owner и владение ключом

### 10.1 Кто сейчас включает `VehicleInputRouter`

Полный поиск ссылок на тип дал только runtime самого router, persistence,
simulation host, prototype tooling и Satsuma builder. Cockpit/seat controller,
который включал бы его при посадке и выключал при выходе, отсутствует в
просмотренном срезе.

Точная цепочка canonical Satsuma:

1. `Phase1SatsumaBaselineBuilder.cs:8049-8056` добавляет
   `VehicleInputRouter`, конфигурирует `Vehicle` map и сразу выполняет
   `inputRouter.enabled = false`.
2. В generated `Satsuma_Phase1_V1a.prefab:109827-109842` component
   `9045668569564978319` действительно имеет `m_Enabled: 0`.
3. `VehicleSimulationHost` ссылается на него как `inputSourceComponent`
   (`prefab:109843-109859`) и вызывает `ConsumeFixedInput`, но выключенный
   router не выполняет `Update` и его action map выключен.
4. `GameUiRoot.SetGameplaySuspended` — не seat owner. Он временно выключает
   все `IGameplayInputGate`, запоминает прежнее состояние и восстанавливает
   именно его. Для Satsuma прежнее состояние `false`, поэтому выход из меню
   не превращает router во включённый.
5. `VehiclePersistence` может восстановить сохранённый `ignitionOn` прямо в
   router, но это не включает sampling газа, руля, сцепления или стартера.

В кодовой базе нет Satsuma driver-seat interaction/controller. Найденный
`vehicle_driver_seat` — сборочная PartDefinition/prefab, а не владелец режима
управления. Следовательно, сейчас нет симметричной пары `enter -> enable
vehicle/disable player` и `exit -> disable vehicle/enable player`.

Это важно для ignition P1: просто вызвать
`VehicleInputRouter.SetGameplayInputEnabled(true)` из замка нельзя. Пока игрок
стоит рядом с машиной, одновременно активируются `Player` и `Vehicle` action
maps, и одни кнопки начнут двигать сразу два мира — классическая ебанина с
двумя хозяевами ввода.

### 10.2 Готовый путь physical press/hold/release уже есть

Новый общий input framework для ключа не нужен:

- `IContinuousContextInteractionTarget` уже задаёт
  `CanBegin / Begin / Continue(deltaTime) / End` и направление Primary/LMB;
- `M4_Player.inputactions:47` привязывает `Interact` к
  `<Mouse>/leftButton`;
- `PlayerInputRouter:210-223` передаёт press, каждый held-frame и release в
  `PlayerInteractionController`;
- `PlayerInteractionController:693-705,1195-1264` выбирает continuous
  capability текущего raycast-candidate и гарантированно вызывает `End`;
- `SatsumaHandbrakeInteractionTarget` — уже работающий локальный пример того
  же контракта без чтения Input System внутри детали.

Для замка достаточно Primary/LMB. Secondary/RMB должен возвращать false и не
менять состояние. Порог `0.4 s` должен считать время внутри project-owned
ignition controller; target лишь передаёт начало, продолжение и отпускание.

### 10.3 Кто выдаёт Satsuma key в оригинале

Authority ключей находится не в `UseNew`, а в root-объекте donor database:

- GO `8194`, Transform `44248`, `Database/Keys`;
- parent `Database`: GO `22046`, Transform `58100`;
- PlayMaker component `106395 @ byte 85492614`;
- FSM `PlayerKeys`, `RestartOnEnable=1`, start state `State 1`.

Точная fresh/load/save цепочка:

| State @ byte | Действия | Результат |
|---|---|---|
| `State 1 @ 85493023` | `Exists(BBSConline, defaultES2File.txt)` | `DOESNOTEXIST -> Reset`, `EXISTS -> Load` |
| `Load @ 85499914` | семь `LoadInt`, включая global `PlayerKeySatsuma` из tag `KeySatsuma` | существующее значение загружается из save |
| `Reset @ 85505382` | `RandomInt` + шесть active `SetIntValue` | Ferndale `0`, Gifu `0`, Hayosiko `0`, Home `1`, Ruscko `0`, **Satsuma `1`** |
| `State 3 @ 85514514` | шесть `IntClamp` | каждый key global ограничивается диапазоном `0..1` |
| `State 2 @ 85494998` | семь `SaveInt`; global transition `SAVEGAME` | `PlayerKeySatsuma` сохраняется в `KeySatsuma` |

Raw `Reset.byteData` содержит последовательность
`PlayerKeyHome 01 ... PlayerKeyRuscko 00 ... PlayerKeySatsuma 01`; все шесть
`SetIntValue` имеют `actionEnabled=01`. Значит, для locked donor версии
Satsuma key и Home key выдаются **с новой игры**, а serialized default `0` в
`PlayMakerGlobals.asset` — лишь состояние до выполнения database FSM.

В later-game FSM `Use`, component `107250 @ byte 103549121`, есть отдельные
ссылки/изменения `PlayerKeyRuscko` и `PlayerKeySatsuma` (например `Win house @
103634813`). Это подтверждает, что ключи являются persistent logical access,
но полный Pig/bet progression намеренно не разворачивался в этом bounded
уточнении.

### 10.4 Состояние remake и совместимый bridge-plan

Поиск в item catalog, save runtime, NPC runtime, Bootstrap и generated Satsuma
не нашёл:

- `SatsumaKey`/`PlayerKeySatsuma` state;
- key PartDefinition/ItemDefinition;
- new-game callback, выдающий Satsuma key;
- persistent keyring/access DTO;
- possession capability, которую мог бы спросить ignition target.

`NPC` metadata про Uncle как `vehicle-key contact` — только будущая роль для
других машин; она не реализует и не должна подменять стартовый Satsuma key.

Минимальный совместимый мост:

1. Завести project-owned persistent access state с отдельным stable key ID
   `vehicle.satsuma.key`, а не donor global/name lookup.
2. На new-game boundary инициализировать `HasSatsumaKey=true`, как доказано
   `PlayerKeys.Reset`; при load читать сохранённое значение. Для существующих
   remake saves migration должна дать `true`, чтобы не запереть уже начатые
   прохождения после обновления.
3. Передать ignition target только read-only possession capability и
   отдельный `IIgnitionIntentSink`. Sink меняет `Off/Accessory/Starting` в
   simulation input даже при выключенном device-sampling component.
4. Не включать целиком `VehicleInputRouter` от клика по замку. Позже отдельный
   project-owned `VehicleControlSessionController` должен единолично владеть
   enter/exit и симметрично переключать player/vehicle maps.
5. Сохранить старый `ignitionOn` через versioned migration
   `false -> Off`, `true -> Accessory`; `Starting` и held-duration не сохранять.

Это добавляет физический ignition без преждевременного изобретения посадки и
не ломает существующий `VehicleSimulationHost`, NWH или accepted suspension.
