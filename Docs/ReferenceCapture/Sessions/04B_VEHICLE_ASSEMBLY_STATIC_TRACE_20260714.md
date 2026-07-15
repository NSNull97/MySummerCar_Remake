# 04B vehicle assembly static trace — rear-left brake drum

Дата: 2026-07-14
Session ID: `04B-STATIC-DRUM-FSM-20260714`
Метод: `SerializedDonorData + SceneTransform`
Результат: `CompletedStaticInspection`, runtime trials: `0`

## Границы

Проведена только read-only статическая трассировка representative rear-left brake drum в уже существующем внешнем AssetRipper export. Donor executable не запускался, donor save не открывался и не изменялся. Milestone 05, runtime assembly code, сцены, prefabs и production assets не создавались и не менялись.

Источник: `DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`, SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`. Это производное статическое представление локальной mod-contaminated установки build `20171487`, а не clean-stock универсальная истина.

## Идентичность объектов

| Роль | Donor object |
|---|---|
| Installed drum | `SATSUMA(557kg, 248)/RL/wheelRL/TireRL/drumbrake rl(xxxxx)`, GO `4017`, T `40078` |
| Assembly trigger | `.../TireRL/trigger_drumbrake_rl`, GO `30683`, T `66735`, SphereCollider `100240` |
| Part database | `Database/DatabaseBody/Drumbrake_RL`, GO `29640` |
| Required part database | `Database/DatabaseBody/Trailarm_RL`, GO `20467` |
| Removal gate object | `.../RL/wheelRL/TriggerWheelRL_New`, GO `20992` |
| Fastener control marker | `.../drumbrake rl(xxxxx)/BoltPM`, GO `12701`, T `48747` |
| Fastener visual child | `.../BoltPM/bolt0`, GO `19555` |

## Доказанные статические факты

### Candidate detection и установка

- `trigger_drumbrake_rl` расположен локально в `(-0.1, 0, 0)`, имеет trigger-sphere radius `0.01`, center `(0, 0, 0)`.
- `Assembly` FSM `112903` принимает trigger collision с tag `PART` и ищет child identity `drum brake(Clone)`.
- До установки требуется `Trailarm_RL.Data.Installed == true`.
- Текущий `Drumbrake_RL.Data.Installed` должен быть `false`.
- После input `ASSEMBLE` повторно проверяется `Trailarm_RL.Data.Bolted`; `true` ведёт в install, `false` — в ветку detach для trail arm.
- Успешная ветка выставляет `Drumbrake_RL.Data.Installed = true`, активирует installed GO `4017` и уничтожает loose candidate.

Статическая sphere geometry не является полной snap tolerance: эффективное пересечение зависит от collider loose part, а отдельного angular threshold в этом FSM не найдено.

### Снятие

- `Removal` FSM `105234` не проходит дальше, если `TriggerWheelRL_New` GO `20992` неактивен.
- При активном gate object FSM читает `Drumbrake_RL.Data.Bolted`; снятие разрешается только при `false`.
- Ветка remove выставляет `Installed=false` и `Bolted=false`, создаёт loose prefab `drum brake` GUID `7ea6e4a1b669a4343807ab188c22efb5` в позиции trigger, записывает созданный объект в `Data.SpawnThis`, активирует assembly trigger и деактивирует installed owner.

Активность `TriggerWheelRL_New` является доказанным условием. Её интерпретация как состояния «колесо снято» правдоподобна по имени и месту в иерархии, но остаётся inference до runtime-наблюдения.

### Fastener state

- Под installed drum найден ровно один `BoltPM` control marker и один его visual child `bolt0`; дополнительных `BoltPM` descendants нет.
- `Screw` FSM `107744` хранит `Stage`, ограниченный диапазоном `0..8`.
- `TIGHTEN` добавляет `+1` к `Stage`, затем `+1` к `BoltCheck.Tightness`.
- `UNTIGHTEN` добавляет `-1` к `Stage`, затем `-1` к `BoltCheck.Tightness`.
- `BoltCheck` FSM `105233` использует `BoltedYES=8` и `BoltedNO=0`: при `Tightness >= 8` пишет `Data.Bolted=true` и отключает interaction collider; при `Tightness <= 0` пишет `false` и включает collider.
- local variable `Screw.BoltSize` сериализован как `0`, однако action payload его не использует для выбора инструмента; это unused/default value, а не номер ключа.
- `PLAYER/Raycast` FSM `Check` читает `localScale.x` целевого `Bolt` и сравнивает с global `ToolWrenchSize` с tolerance `0.02`.
- target `BoltPM` (`T 48747`) имеет `localScale=(1.4,1.4,1.4)`; child mesh ключа `14` (`T 68967`) также имеет `localScale=(1.4,1.4,1.4)`; pickup FSM записывает scale выбранного ключа в `ToolWrenchSize`.
- в state `Get Mouse scroll` `Mouse ScrollWheel < 0` отправляет `UNTIGHTEN`, а `> 0` — `TIGHTEN`.

Значение `8` классифицировано как discrete completion stage, а не восемь физических оборотов и не torque. Tool identity и направление scroll/input доказаны на уровне serialized state graph; torque, physical turn/angle одного stage, strip/failure и промежуточное влияние на физику не доказаны.

## Coverage result

- `P0-ASSEMBLY-MOUNT-RULE`: `Missing -> Partial`.
- `P0-ASSEMBLY-FASTENER-SEMANTICS`: остаётся `Partial`, но tool mapping и scroll direction теперь статически разрешены.
- Representative fixture остаётся `Partial`; required runtime repetitions: `3`, выполнено: `0`.
- Полный `Prompts/05_VEHICLE_ASSEMBLY.md`: **NO-GO**.

Для `Covered` всё ещё требуются три чистых install/remove trial с одинаковыми gates/outcome, runtime-подтверждение snap/blocked presentation, clean stage-progression trial ключом `14` и определение physical turn/angle/torque semantics одного stage. Диагностическое видео `04B_VEHICLE_ASSEMBLY_VIDEO_REVIEW_20260714.md` отвергает ключ `11`, но не является валидным repetition.
