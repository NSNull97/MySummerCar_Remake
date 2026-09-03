# Milestone 09B — нормализованный donor evidence для предметов

## 1. Назначение и границы

Этот документ фиксирует read-only evidence для Milestone 09B: состав предметов,
их наблюдаемые параметры, состояния и пробелы в доказательствах. Он не является
реализацией, не заменяет `LEGACY_FEATURE_PARITY_MATRIX.csv` и не повышает ни одну
строку до `Verified`.

Зафиксированный источник мира:

- donor revision: `msc-world-baseline-04a1.1-c3f2f337`;
- SHA-256 `sharedassets3.assets`: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- нормализованный staging-файл: `normalized/world/milestone-04a1/WorldObjectPlacements.csv`;
- реестры объёма: `Docs/Phase1/ITEM_AND_CONSUMABLE_CATEGORY_ROSTER.csv` и
  `Docs/Phase1/ITEM_DEFINITION_SUBROSTER.csv`.

Donor hierarchy, имена объектов и PlayMaker FSM используются только как
`BehavioralReference`. Runtime обязан использовать проектные `FeatureId`,
`DefinitionId`, `StableEntityId`, явные registries и project-owned state machines.
Запрещено искать предметы по donor-имени, hierarchy path, `SourceObjectId` или
Unity instance ID.

## 2. Нормализация ссылок `WOP:<n>`

В `ITEM_DEFINITION_SUBROSTER.csv` ссылка `WOP:<n>` означает **физический номер
строки CSV**, а не значение поля `SourceObjectId`.

Для зафиксированного файла с одной строкой заголовка:

```text
physical CSV line n
  -> Import-Csv array index n - 2
  -> SourceObjectId + StableId + HierarchyPath + CellId
```

Перед разрешением ссылок файл нельзя сортировать, фильтровать или пересобирать.
В любой производной записи нужно сохранять исходный `WOP`-номер вместе с
разрешёнными полями. Иначе ссылка перестаёт быть воспроизводимой.

Контрольные примеры:

| WOP | SourceObjectId | StableId | HierarchyPath | CellId |
|---:|---:|---|---|---|
| 32707 | 32720 | `ccf44b25d7c0be3286f276803ea31eb5` | `STORE/LOD/ActivateStore/FoodProducts/Sausages` | `cell_-3_0` |
| 20912 | 20925 | `a4b7184260d8e8fd4505bf1d1ea44c94` | `STORE/LOD/ActivateStore/FoodProducts/MacaronBox` | `cell_-3_0` |
| 6888 | 6901 | `b06590a0f6c1c241776ca728594b119d` | `STORE/LOD/ActivateStore/FoodProducts/Pizza` | `cell_-3_0` |
| 8644 | 8657 | `99e96309024434130d2d39b7d9ac0d62` | `ITEMS/gasoline(itemx)` | `excluded` |
| 13272 | 13285 | `25ac19d76b3e278f8e2f2c1ecdfb0ca7` | `ITEMS/diesel(itemx)` | `excluded` |
| 71 | 84 | `718d196e7c447e0471a18dd6c30c7443` | `ITEMS/car jack(itemx)` | `excluded` |
| 20811 | 20824 | `11aabf50d67993e3b0c3732280f55b0e` | `ITEMS/bucket(itemx)` | `excluded` |
| 33221 | 33234 | `7ec7c143fa3b45e6ddfbf13ca35351f3` | `ITEMS/flashlight(itemx)` | `excluded` |
| 34307 | 34320 | `2c1e6bbcdf4da1eaf8f1af6ce4c2280f` | `ITEMS/radio(itemx)` | `excluded` |

Пример ошибки: поиск `SourceObjectId=32707` возвращает не упаковку сосисок, а
другой объект. Для `WOP:32707` правильный `SourceObjectId` — `32720`.

Объекты под `ITEMS/*` часто имеют `CellId=excluded`. Их размещение и
персистентность нельзя считать автоматически покрытыми legacy streaming:
необходимы project-owned wrapper/placement, стабильный instance ID и сохранение
состояния независимо от загруженности ячейки.

## 3. Зафиксированные параметры и состояния

Числа ниже переписаны в семантике donor-переменных. Единицы, знаки и порядок
применения нельзя «исправлять по смыслу» до отдельного сравнительного прогона.

### 3.1 Еда и скоропортящиеся предметы

| Предмет | Состояние/порча | Зафиксированный эффект |
|---|---|---|
| Sausages package | `Condition=100`, `SpoilRate=.034`, `FridgeRate=.0005` | `Weight +.136`, `Thirst +12`, `Hunger -100` |
| Macaroni box | `Condition=100`, `SpoilRate=.032`, `FridgeRate=.0004` | `Weight +.06`, `Thirst +24`, `Hunger -80` |
| Pizza | `Condition=100`, `SpoilRate=.026`, `FridgeRate=.0003` | `Weight +.068`, `Thirst +22`, `Hunger -50` |
| Potato chips | `Consumed` bool | `Weight +.2`, `Thirst +60`, `Hunger -90` |
| Milk | `Condition=100`, `SpoilRate=.06`, `FridgeRate=.001` | Эффекты питья находятся в shared hand FSM и ещё не нормализованы |
| Loose sausage | `Condition=100`, `.034/.0005`, состояния fresh/grilled/burned, `Grill=30`, `Burn=10` | fresh: `Thirst +3`, `Hunger -33.3`; grilled: `Thirst +4`, `Hunger -42`, `Stress -15`; `Weight +.034` |
| Pike | `Condition=40`, `SpoilRate=.033`, `FridgeRate=.0018`, type state, `Grill=240`, `Burn=20` | `Weight +.165`, `Thirst +16`, `Hunger -130` |
| Moose meat | `Condition=40`, `SpoilRate=.031`, `FridgeRate=.0012`, `Grill=240`, `Burn=20` | `Weight +.41`, `Thirst +22`, `Hunger -150`, `Stress -40` |
| Sausage-potatoes meal | готовое блюдо | `Weight +1.12`, `Thirst +33`, donor action `PlayerHunger SetFloatValue -33` |

Значение `PlayerHunger SetFloatValue -33` у sausage-potatoes выглядит необычно;
его нужно сохранить как сырое evidence и проверить поведением, а не подменять
предположением о `Add`/`Subtract`.

### 3.2 Ингредиенты, упаковки и одноразовые предметы

- Ground coffee: количество `100`.
- Charcoal package: количество `140`.
- Mosquito spray: `Fluid=100`, расход одного применения `10`.
- Beer case: ровно `24` дочерние бутылки; сохраняются число/состояние
  consumed/destroyed бутылок и transform оставшихся.
- Juice concentrate: `Consumed=false`, `ContainsJuice=true`,
  `ContainsKilju=false`, `KiljuAlc=0`, `Sweetness=0`, `Vinegar=0`, `Yeast=0`;
  состояния juice/kilju/empty.
- Sparkplug box: `4` штуки.
- Lightbulb box: `1` штука.
- Fuse package: `5` штук.
- R20 battery box: `4` батарейки.

Runtime catalog содержит четыре project-owned spawned-child definitions и явные
связи package → child для spark plug, lightbulb, fuse и R20 battery (`4/1/5/4`).
Детерминированная выдача проверена representative-тестом spark plug. Ещё не
подтверждены donor-equivalent use/install outcomes, child-specific presentation
и полный end-to-end прогон каждого из четырёх типов.

### 3.3 Жидкости, канистры и ёмкости

- Brake fluid: ёмкость/количество `1`.
- Coolant: `10`.
- Motor oil: `4`.
- Two-stroke oil: `5`.
- Для этих ёмкостей доказаны orientation-driven pouring, зависимость массы от
  остатка и переход в empty/garbage.
- Gasoline can: `Capacity=20`, captured `Fluid=2`, крышка open/closed, pouring,
  `FuelType=Gasoline`, `ExplosionForce=80000`, `ExplosionRadius=8`.
- Diesel can: `Capacity=20`, captured diesel `4`, отдельная ветка fuel oil `0`,
  крышка/pouring и те же explosion values.
- Kilju bucket: `Alcohol=0`, `BrewTime=680`, donor math value `.68`,
  `Sugar=0`, `Sweetness=0`, `Time=0`, `Vinegar=0`, `Water=0`, `Yeast=0`,
  `Finished=false`, `Lid=false`; наблюдаются fill/ingredient/dilute/empty/brew
  states. Точная формула и последовательность donor actions ещё не зафиксированы.
- Coffee pan: `BoilRate=.0005`, `Max=.6`, `Caffeine=0`, `Coffee=0`, `Ground=0`,
  `Water=0`, `GroundMax=26`, `CookingRate=.3`, `TransferRate=.03`; состояния
  cap/fill/empty.
- Coffee cup: persistent coffee/caffeine/transform values.

### 3.4 Запчасти и автомобильные расходники

- Car battery: `Charge=128`, `ChargeMax=145`, `DischargeRate=.0003`;
  installed/consumed/charged states.
- Fire extinguisher: `Fluid=100`; consumed/in-use/installed states.
- Spray paint: `Fluid=100`, `ColorID=0`; in-use/consumed и matte/regular states.
- Oil filter: `Dirt=1`, `Tightness=0`; installed/consumed states.
- Alternator belt: `Wear=0`, `Installed=false`.

### 3.5 Инструменты, переносимые устройства и world items

- Car jack: fold/open, установка относительно terrain, шаг подъёма по Y `.04`.
- Motor hoist: `BoltedYES=10`, `BreakForce=62000`, hook/bolt stage logic.
- Spanner case: open/closed и состояние доступности инструментов.
- Flashlight: charge/batteries/on, совместим с R20, расход `.0013`.
- Radio: charge/batteries/channel/on, `Volume=.5`, `ConsumptionDivider=8000`.
- Fish trap: максимум `12` рыб, open state, fish spawn IDs, индивидуальные
  air/spoil states рыбы.
- Grill: `Charcoal=0`, `CharcoalMax=100`, пакет `140`, `PourRate=12`,
  `BurnTime=120`, подавление огня дождём/водой.
- Wood carrier: две persistent instance; сохраняются массивы/количество дров.

### 3.6 Носители и прочее

- Три CD и три CD cases: open/in-case/in-player states и transforms.
- Одна roster/runtime definition floppy моделирует три data-driven variants:
  `rapula=780 KB`, `massacre=1310 KB`, `joulu=320 KB`; три canonical placements
  получают начальные variant indices `0/1/2`. Точное donor instance → variant
  mapping и computer/media behavior ещё требуют сравнения.
- Sofa имеет отдельное sleep behavior, принадлежащее 09C; в 09B это только
  переносимый предмет/груз.
- Поведение читаемых и проигрываемых media должно связываться с последующими
  системами, но их физическое состояние и сохранение принадлежат предметному
  фундаменту.

## 4. Объём roster на момент аудита

- `20` обязательных item/consumable categories.
- `82` roster definitions: `79` required и `3` optional/unknown; runtime catalog
  содержит `86` definitions (`82` roster + `4` внутренних spawned-child) и `43`
  canonical placements.
- `103` строки parity matrix с `OwnerMilestone=09B`: `100` required и
  `3` optional/unknown.
- Все `100` required-строк находятся в состоянии `PartiallyImplemented`, три
  optional-строки — `Blocked`; `Verified=0`.
- Save coverage: `99` feature-строк `Partial`, `1` presentation-строка
  `NotRequired` и `3` optional-строки `Uncovered`.
- TemporaryDirectImport presentation имеет `46` GeneratedValidated bindings,
  `90` mesh assets и `17` material slots; остальные определения используют
  project-owned proxy fallback. Manual и row-specific proof ещё не выполнены.

## 5. Evidence gaps и variant debt

Перед закрытием 09B необходимо явно разрешить следующие пробелы:

1. Провести donor comparison и child-specific presentation/use/install
   verification для четырёх уже созданных spawned-child definitions; расширить
   representative dispense coverage на все четыре типа.
2. Зафиксировать disposed battery, empty bottles и прочие disposable/garbage
   presentation states.
3. Зафиксировать donor-доказательство соответствия SourceObjectId/StableId
   вариантам rapula/massacre/joulu и добавить прямой mapping/restore test;
   computer integration остаётся downstream.
4. Исправить evidence для `P1.ITEM.114 Beer bottle`: текущая ссылка указывает на
   `beercase0`, а не на отдельный `Beer.prefab`.
5. Зафиксировать mapping `Coffee -> groundcoffee` и
   `Charcoal -> grillcharcoal`.
6. Перечислить все spray-paint color variants и доказать их donor mapping.
7. Категория заявляет screwdrivers/ratchets, но concrete roster rows для них не
   зафиксированы.
8. Отдельно снять точные эффекты milk, beer, booze и cigarettes из shared hand
   FSM/поведенческого прогона.
9. Зафиксировать точную kilju formula, порядок actions и граничные состояния.
10. Для optional `P1.ITEM.180`–`P1.ITEM.182` (trophy/eyewear/hat) доказать
    достижимость в locked donor version либо оставить их вне required scope.
11. Выполнить UI/audio/presentation comparison для player-visible действий.
12. Дополнить выполненные representative automated save/load, deferred-cell и
    duplicate-guard checks ручным production-scene stream-away/back и
    row-specific coverage, особенно для `CellId=excluded`, контейнеров и всех
    mutable variants.

## 6. Запрет преждевременного `Verified`

Наличие prefab, FSM, hierarchy node, extracted value или строки roster означает
только `EvidenceCaptured`. Строка 09B не может стать `Verified`, пока одновременно
не выполнены:

1. Evidence привязано к locked donor revision и нормализовано до воспроизводимой
   ссылки.
2. Есть project-owned stable `FeatureId`, `DefinitionId` и стабильный instance ID
   для каждого persistent экземпляра.
3. Реализован и выполнен полный физический flow: acquire, pickup/carry,
   place/throw, use/consume/install и все применимые empty/broken/spoiled states.
4. Выполнено сравнение min/max/empty/broken и переходов состояний с donor.
5. Выполнен save/load roundtrip всех mutable values, container membership и
   transforms, включая unloaded streaming cells.
6. Выполнен stream-away/back прогон без дублирования, потери или сброса состояния.
7. Есть removable `TemporaryDirectImport` presenter либо project-owned
   presentation, связанный только через стабильные IDs.
8. Есть требуемая UI/audio feedback через существующие project boundaries.
9. Все отличия от donor перечислены и явно приняты.

До выполнения этих условий допустимы только `EvidenceCaptured`,
`PartiallyImplemented`, `Blocked` или другой честный промежуточный статус.

## 7. Внеэтапный food/fridge/placement fix — 2026-08-14

Отдельный совместимый fix pass закрыл ранее зафиксированные пробелы для
time-driven freshness, холодильного коэффициента, четырёх физических сосисок,
raw/cooked/burned переходов и сохранения этих значений. Также выполнен полный
аудит 43 canonical placements с переносом Rigidbody/collision metadata и
проверкой приоритета сохранённой позы над New Game transform.

Точная реализация, таблица всех placements, выполненные тесты и оставшиеся
provisional/manual ограничения записаны в
`Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md`. Статусы Phase 1
остаются `PartiallyImplemented`: ручное donor visual/audio comparison,
row-specific complex collision acceptance и нормализация burned/spoiled
коэффициентов ещё не выполнены.
