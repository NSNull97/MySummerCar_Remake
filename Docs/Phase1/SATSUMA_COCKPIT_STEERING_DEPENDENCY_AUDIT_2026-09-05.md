# Satsuma: зависимость руля от рулевой колонки

Дата: 2026-09-05. Статус: **ReadOnlyAuditComplete**.
Классификация: `BehavioralReference`.

## Границы прохода

Проверен только узкий граф `steering column -> steering wheel`:

- когда donor предлагает установку руля;
- зависит ли это от установки или затяжки колонки;
- мешает ли установленный руль ослабить или вручную снять колонку;
- есть ли в `Removal`, `BoltCheck` или `Drop part` явный каскад снятия руля;
- чем обычный `RequiredOccupiedMountIds` remake отличается от найденной
  donor-семантики;
- что произойдёт с уже существующим save, где руль установлен без колонки.

Не проверялись поведение рулевого управления, приборка, зажигание, подвеска,
двигатель и физические последствия произвольного collision break. `Assets`,
`ProjectSettings` и `Packages` не изменялись; Unity не запускалась.

## Источник и метод

Authority — frozen donor `msc-world-baseline-04a1.1-c3f2f337`, Steam build
`20171487`:

```text
E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/
  milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity
```

SHA-256 `GAME.unity`:
`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`.

По известным Component/GameObject/Transform ID через `rg -b` находился YAML
header, после чего читался только один bounded YAML document до следующего
`--- !u!`. Условия ниже подтверждены raw `actionNames`, object references,
`fsmStringParams`, variables и state transitions. Название переменной само по
себе доказательством не считалось.

## 1. Короткий вердикт

1. В donor руль можно **установить только при установленной колонке**, потому
   что assembly trigger является ребёнком `steering_column2`; колонке для этого
   не требуется быть затянутой.
2. Установленный руль **не блокирует** ослабление или ручное снятие колонки.
   Колонка проверяет не DB руля, а собственный `Bolted` и вычисляемый
   `SteeringAxle.WheelInstalled`, который на деле равен
   `column.Bolted && rack.Bolted`.
3. Ни `Removal` колонки, ни её `BoltCheck` не содержат явной команды снять,
   сбросить DB или физически выбросить установленный руль.
4. Поэтому добавлять колонку в обычный
   `steering-wheel.requiredOccupiedMountIds` нельзя: текущий remake превратит
   install prerequisite одновременно в обратный manual-removal blocker и в
   forced-collapse edge. Это будет строже оригинала.
5. Нужен отдельный **install-only occupied prerequisite**, который не участвует
   в manual removal, ownership и structural collapse и не валидирует старые
   save задним числом.
6. Отдельно обнаружена ошибка прежнего cockpit-отчёта: у всех четырёх donor
   рулей `BoltCheck` имеет `BoltedYES=2`, `BoltedNO=0`; текущий remake использует
   `ON=1`, `OFF=0`.

## 2. Donor: почему руль предлагается только после установки колонки

### 2.1 Иерархия trigger

| Объект | ID | Byte offset | Подтверждённая связь |
|---|---:|---:|---|
| `steering_column2` | GO `1468` | `505502` | исходно `m_IsActive: 0` |
| `steering_column2` | Transform `37528` | `13131317` | child list содержит `67204` |
| `trigger_steering_wheel` | GO `31145` | `10854143` | активен внутри выключенной ветки |
| trigger | Transform `67204` | `24767215` | `m_Father: 37528` |

Column Assembly component `109461 @ byte 144016964`, FSM `Assembly`, в state
`Assemble`:

- выставляет DB `SteeringColumn` GO `238`, `Data.Installed=true`;
- активирует installed column GO `16469`;
- активирует bolts GO `2588`;
- активирует `steering_column2` GO `1468`.

Затяжка колонки в этом state не проверяется. Следовательно, gate руля — именно
**column installed**, а не `column.Bolted`.

### 2.2 Что делает Wheel Assembly

Wheel Assembly component `113042 @ byte 210110098`, GO `31145`, FSM
`Assembly`, принимает четыре варианта:

| Вариант | Loose GO | DB GO | Assembly state @ byte |
|---|---:|---:|---:|
| GT | `20597` | `6448` | `GT @ 210115517` |
| Rally | `32615` | `31624` | `Rally @ 210135143` |
| Sport | `31914` | `35545` | `Sport @ 210145666` |
| Stock | `6005` | `29660` | `Stock @ 210156189` |

Все четыре state выполняют одинаковый существенный набор действий:

- `DestroyComponent` loose Rigidbody;
- `SetParent` детали в переменную `Parent`, GO `12464`;
- активируют найденный child-крепёж;
- выставляют соответствующий DB `Data.Installed=true`.

`Parent` — GO `12464`, `pivot_steering_wheel`; его Transform `48510 @ byte
17439705` имеет `m_Father: 52534`. Transform `52534 @ byte 18997692` принадлежит
installed column GO `16469`.

Это подтверждает parent relationship donor, но не является разрешением менять
owner текущего mount: ownership в remake дополнительно блокирует снятие владельца
и создаёт каскад. Для C1b достаточно отдельного install-only gate.

## 3. Fastener и Removal руля

### 3.1 Точные FSM

| Вариант | Removal component @ byte | BoltCheck component @ byte | DB | Пороги |
|---|---|---|---:|---|
| Stock | `105774 @ 74578934` | `105776 @ 74620201` | `29660` | `YES=2`, `NO=0` |
| GT | `109890 @ 151913677` | `109891 @ 151948156` | `6448` | `YES=2`, `NO=0` |
| Sport | `113262 @ 214003912` | `113261 @ 213973042` | `35545` | `YES=2`, `NO=0` |
| Rally | `113461 @ 217638563` | `113462 @ 217676468` | `31624` | `YES=2`, `NO=0` |

У каждого `BoltCheck` variables содержат `BoltedYES=2`, `BoltedNO=0` и
`Tightness`. В просмотренных FSM нет ссылки на column DB `238`, installed column
GO `16469` или её `BoltCheck` `108776`.

У каждого Wheel `Removal`, state `Requirements` одинаков:

- `GetFsmBool(db_RemoveReq1, Data.Installed)`, где `db_RemoveReq1={fileID:0}`;
- `GetFsmBool(db_ThisPart, Data.Bolted)`, где `db_ThisPart` — DB варианта из
  таблицы;
- `BoolNoneTrue -> REMOVE`;
- `BoolAnyTrue -> LOOP`.

То есть реальный blocker снятия руля — его собственный `Data.Bolted`. Колонка
ни в установочном, ни в съёмочном predicate руля не читается.

## 4. Removal колонки: `WheelInstalled` назван обманчиво

Installed column:

- GO `16469`, Transform `52534`;
- `BoltCheck` component `108776 @ byte 132464198`, `BoltedYES=10`,
  `BoltedNO=0`, DB `238`;
- `Removal` component `108777 @ byte 132495067`, FSM `Removal`.

В `Removal.Requirements` действительно читаются:

1. GO `12115`, FSM `SteeringAxle`, variable `WheelInstalled`;
2. DB GO `238`, FSM `Data`, variable `Bolted`.

Затем `BoolNoneTrue -> REMOVE`, `BoolAnyTrue -> LOOP`. Однако
`WheelInstalled` — не состояние установленного рулевого колеса.

`SteeringAxle` component `107557 @ byte 109102284`, GO `12115`, FSM
`SteeringAxle`, в states `Not installed` и `Installed` читает:

- DB GO `238` (`SteeringColumn`), `Data.Bolted`;
- DB GO `1135` (`SteeringRack`), `Data.Bolted`.

В `Not installed` действие `BoolAllTrue` выставляет FSM bool
`WheelInstalled`; в `Installed` эти же два значения продолжают отслеживаться.
Ни один DB руля (`29660`, `6448`, `35545`, `31624`) в этом FSM не участвует.

Практический результат:

- установленный руль не мешает крутить крепёж колонки: её `BoltCheck` не читает
  руль;
- после ослабления колонки до `Bolted=false` вычисляемый
  `SteeringAxle.WheelInstalled` также перестаёт быть true;
- оба removal predicate становятся false, поэтому ручное снятие колонки
  разрешается даже при `Data.Installed=true` у реального руля.

### 4.1 Что точно делает `Remove part`

State `Remove part` в component `108777`:

- сбрасывает column DB `238`: `Data.Installed=false` и `Data.Bolted=false`;
- создаёт loose column prefab GUID
  `61172097f73674046ad91f57eb873eb0` на trigger GO `19032`;
- записывает spawned object в `Data.SpawnThis`;
- деактивирует secondary column GO `1468`;
- активирует trigger GO `19032`;
- деактивирует installed column GO `16469`.

В action list нет `SetFsmBool` для DB любого руля, отправки `REMOVE` в Wheel
Removal или отдельного `Detach` для руля. Поэтому доказано только следующее:
column removal **не очищает состояние руля явной командой**.

Сериализованная иерархия помещает assembled wheel под pivot внутри installed
column, поэтому деактивация parent-ветки, вероятно, скрывает его. Но фактическое
поведение в gameplay после remove/reinstall/load этим read-only проходом не
записывалось. Реализовывать hide, reattach или drop на основании одной этой
инференции нельзя; это остаётся `PendingGameplayCapture`.

### 4.2 `Drop part` не является каскадом руля

В Column Assembly component `109461`, state `Check bolts`, проверяется
`SteeringRack` DB GO `1135`, `Data.Bolted`:

- true -> `Assemble`;
- false -> `Drop part`.

`Drop part` пишет `Removal.Detach=true` в installed rack GO `32711`, затем
возвращается к поиску детали. Это обработка неудачной **попытки установки
колонки на незатянутую рейку**. Она не снимает уже установленную колонку и не
содержит ссылок на руль.

Отдельного подтверждённого writer, который при штатном ослаблении колонки
вызывает каскадный `Detach` руля, в bounded scan не найдено. Collision/debug
forced paths и визуальный результат column remove/reinstall остаются unknown.

## 5. Почему текущий `RequiredOccupiedMountIds` семантически неверен

Текущее generated definition:

```text
mount.satsuma.steering-wheel
  ownerPartDefinitionId = vehicle.satsuma.part.body-shell
  requiredOccupiedMountIds = []
  boltedOnThreshold = 1
  boltedOffThreshold = 0
```

Источники: `mount.satsuma.steering-wheel.asset:18,34-35,41`.

Если просто добавить `mount.satsuma.steering-column` в
`RequiredOccupiedMountIds`, текущий runtime применит одно поле сразу в трёх
местах:

1. установка: `AssemblyGraph.AreMountInstallPrerequisitesMet`, строки
   `242-261` — это нужное поведение;
2. manual removal: `AssemblyGraph.HasInstalledRemovalBlocker`, строки
   `470-505` — колонку нельзя будет снять при установленном руле;
3. forced hierarchy collapse: `AssemblyGraph.IsMountDependentOn`, строки
   `374-406`, затем `VehicleAssemblyController.CollapseMountHierarchy`, строки
   `1189-1230` — руль станет structural child колонки.

Пункты 2 и 3 donor FSM не подтверждает. Поле
`RemovalIgnoredDependentMountIds` подавляет только inferred manual blocker
(`AssemblyGraph:481-505`), но намеренно не убирает structural collapse. Поэтому
комбинация существующих полей всё равно не выражает точный контракт.

Менять `ownerPartDefinitionId` с body-shell на steering-column тоже нельзя как
короткий путь: `HasInstalledRemovalBlocker` отдельно блокирует снятие part при
занятом owned mount (`AssemblyGraph:452-468`), а `IsMountDependentOn` включает
owner relationship в collapse (`AssemblyGraph:384-391`).

## 6. Save compatibility

Текущий restore:

- валидирует identity, mount occupancy, part/mount compatibility и owned-mount
  owner (`VehicleAssemblyController:2313-2401`);
- не вызывает `AreMountInstallPrerequisitesMet` для restored installed parts;
- после validation напрямую вызывает `mount.TryOccupy(part)` и `InstallAt`
  (`VehicleAssemblyController:1367-1382`);
- затем восстанавливает fasteners/latch и синхронизирует presentation.

Следовательно, старый save с установленным рулём и отсутствующей колонкой сейчас
валиден, потому что wheel mount принадлежит body-shell. C1b должен сохранить
это поведение:

- новый install-only prerequisite проверяется только при новой player-команде
  установки/preview;
- restore не отклоняет и не разваливает grandfathered topology;
- DTO schema не меняется;
- после ручного снятия такого legacy-руля повторная установка уже требует
  установленную колонку.

Для смены latch руля `ON 1 -> 2` отдельно нужна совместимость hysteresis:
сохранённый ранее валидный `isBolted=true` при `Tightness=1` должен остаться
bolted после restore; новые переходы `false -> true` происходят только при
достижении `2`. Нельзя молча объявлять такой save повреждённым.

## 7. Минимальный контракт C1b

1. Добавить empty-default поле/API вида `installationRequiredOccupiedMountIds`.
2. Проверять его в install query и preview рядом с обычными install predicates.
3. Не учитывать его в:
   - `HasInstalledRemovalBlocker`;
   - `IsMountDependentOn` / collapse;
   - owned-mount availability;
   - restore validation и migration topology rewrite.
4. Для `mount.satsuma.steering-wheel` записать только
   `mount.satsuma.steering-column`.
5. Исправить wheel fastener group на `ON=2`, `OFF=0`, сохранив grandfathered
   `B=true, T=1` при restore.
6. Не добавлять hide/reattach/drop руля при снятии колонки до отдельного
   gameplay capture.

Минимальные проверки:

- без колонки wheel install/preview blocked;
- с установленной, но незатянутой колонкой wheel install разрешён;
- с установленным рулём column fasteners ослабляются и manual column removal не
  блокируется рулём;
- forced collapse колонки не использует новый install-only edge;
- старый save `wheel installed + column absent` восстанавливается без потерь;
- новый wheel latch включается на `T=2`, выключается на `T=0`, а старый
  `B=true,T=1` сохраняет latch через restore.

## 8. Неподтверждённое

- точная видимость/физика руля после ручного remove и последующего reinstall
  колонки;
- есть ли отдельный collision-only writer `Removal.Detach` колонки вне
  просмотренного bounded graph;
- поведение варианта, когда колонка исчезает через debug/save corruption;
- save-порядок donor для логически установленного руля при снятой колонке.

Эти пункты не нужны для install-only C1b и не должны задерживать его. Для них
нужен отдельный donor gameplay capture, а не догадка по hierarchy.
