# Satsuma: пакет C — точечные правила установки и ручного снятия

Дата: 2026-09-04. Статус: **ImplementedAutomatedPassedManualPending**.
EditMode и PlayMode пройдены; ручная проверка pending. Классификация:
`BehavioralReference;Reimplemented`.

Пакет C исправляет только различие между условиями установки и обычного
снятия. Принятые пользователем A (монтаж, затем развал зависимой сборки) и B
(wheel Chance → Wait 1s realtime) не пересматриваются. Скоростные BREAK из D,
геометрия, подвесочные силы, крепёж, позиции и сохранения не меняются.

## Источник и доказательства

Authority — frozen `GAME.unity`, revision `msc-world-baseline-04a1.1-c3f2f337`,
SHA256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Читались только адресные YAML-документы через byte offset. Donor FSM и код
PlayMaker в runtime не импортируются.

| Правило | Donor FSM RL/FL / RR/FR | Активное доказательство | Авторинг C |
|---|---|---|---|
| Stock-strut снять нельзя при установленной рулевой тяге | Removal `108086/108453` | `GetFsmBool(rod.Data.Installed)` входит в `BoolNoneTrue` вместе с собственным B | `strut-fl/fr.RemovalBlockedWhileOccupied = steering-rod-fl/fr` |
| Полуось снять нельзя при затянутом диске | Removal `107526/110144` | собственный `Bolted` и `disc.Data.Bolted` входят в `BoolNoneTrue` | `halfshaft-fl/fr.RemovalBlockedWhileBolted = discbrake-fl/fr` |
| Диск не блокируется лишь потому, что полуось зависит от него при установке | Removal `104209/107489` | проверяются собственный B и отсутствие колеса через активность wheel-trigger; halfshaft.I отсутствует | `discbrake-fl/fr.RemovalIgnoredDependent = halfshaft-fl/fr` |
| Spindle не блокируется лишь потому, что диск зависит от него при установке | Removal `110197/110400` | проверяются собственный B и stock-strut.I; disc.I отсутствует | `spindle-fl/fr.RemovalIgnoredDependent = discbrake-fl/fr` |
| Пружину нельзя поставить при установленном shock | Assembly stock `105274/104387`, long `106833/112496`; rally evidence `107067/104524` | `Requirements(arm.I) → Not Installed`; два `GetFsmBool(stock/rally shock.Data.Installed)` и `BoolNoneTrue → ASSEMBLE`; затем клик отдельно проверяет arm.B | Текущие stock/long mounts блокируются занятым same-corner stock `shock-*`; rally runtime-варианты ещё не сгенерированы |
| Рычаг нельзя поставить при установленном штатном shock | Assembly `106827/104310` | `Requirements`: stock shock `Data.Installed`; true→LOOP, false→ASSEMBLE | `trail-arm-rl/rr` блокируется занятым same-corner `shock-*` mount, который сейчас принимает только две взаимозаменяемые stock-детали; rally runtime-варианты pending |

Для нового spring gate путь подтверждён именно как условие Assembly, а не
видимость меша:

```text
TriggerEnter
  → Requirements: arm.I
  → Not Installed / Not Installed 2: !stockShock.I && !rallyShock.I
  → Find correct part → Wait for assembly
  → Check bolts: arm.B
  → Assemble либо принятый A-исход монтажа с последующим развалом
```

У шести spring FSM `Not Installed` использует три активных действия
(`actionEnabled=010101`, start indices `0/5/10`). В byteData два bool-входа
начинаются на позициях `24/35`, `ASSEMBLE=415353454d424c45` — на `46`,
`everyFrame=false` — на `56`. `BoolNoneTrue.cs` дополнительно прочитан: событие
отправляется лишь когда все входы false. Проверка происходит при входе в
состояние; на самом клике два shock.I повторно не считываются.

Byte offsets FSM-заголовков: stock spring RL/RR `64868043/48586493`, long
`93475263/201046018`, rally `97596426/51043302`. Rally Removal — отдельные
`112648/104777`; старое объединение их с long Removal `112850/107450` было
неточным. Все три пружины одного угла выключают общий trigger GO `11269/20604`
после установки, а stock/rally shock — общий trigger GO `19046/29426`.

## Совместимый runtime-контракт

`MountPointDefinition` получает два пустых по умолчанию массива:

- `RemovalBlockedWhileBoltedMountIds` — только прямые B-запреты обычного снятия;
- `RemovalIgnoredDependentMountIds` — исключения лишь для автоматически
  выведенного обратного запрета из installation dependency.

Они не разрешают обход собственного крепежа, explicit removal blocker,
установленного дочернего mount и forced/collapse detach. Validator обязан
отклонять неизвестные ID и самоссылки. Поля добавляются в существующий
ScriptableObject совместимо: старые definitions получают пустые массивы.
Save schema/DTO и стабильные ID не меняются; это immutable authoring, поэтому
миграция сохранений не требуется.

Обратные install prerequisites больше не считаются точной таблицей ручного
снятия. Приоритет остаётся у явных removal rules. Это устраняет конкретные
ложные запреты диска и spindle, не выключая проверку зависимостей глобально.

Текущий `shock-rl/rr` mount принимает две взаимозаменяемые **штатные** loose
детали, а rally spring/shock пока отсутствуют в generated definitions и
присутствуют лишь в mail-order mappings. Поэтому текущий arm gate точно относится
к stock shock и не создаёт выдуманного rally-правила. Donor spring gate для
stock+rally shock зафиксирован как evidence; его rally-ветвь остаётся pending до
интеграции соответствующих runtime-деталей.

## Сознательно отложено

Ручной Removal рычага `107387/113637` проверяет собственный B, stock shock.I и
drum.I, но **не пружину**. При оставшейся пружине и снятых shock+drum сырой
predicate разрешает снять рычаг. Однако `Remove part` не посылает spring.Detach;
spring GO — отдельный ребёнок угла, не рычага, и не имеет Rigidbody. Внешние
watchers/skin/joint-последствия в этом bounded проходе не доказаны.

Поэтому C пока не добавляет исключение spring→arm: текущее поведение сохраняется,
пока судьба пружины не подтверждена live в оригинале. Не заявляется ни «она
обязательно падает», ни «она должна висеть в воздухе». Donor quirk Removal рейки,
ground-gate колеса и скоростные адресные каскады D также остаются вне пакета.

## Проверка после свободного окна Unity

- FL/FR: strut при установленной, даже полностью раскрученной тяге не снимается;
  после снятия тяги снимается. Forced detach эту проверку обходит.
- FL/FR: B диска блокирует полуось; I, но B=false — не блокирует. Собственный B
  полуоси сохраняет запрет. При установленной полуоси раскрученный диск снимается.
- FL/FR: при установленном диске раскрученный spindle снимается, если stock-strut
  отсутствует; собственный B и stock-strut.I всё ещё блокируют.
- RL/RR: занятый stock shock запрещает установку stock и long spring; после снятия
  shock установка доступна. Пружину по-прежнему нельзя снять до снятия shock.
- RL/RR: занятый same-corner stock shock запрещает установку arm. Противоположный угол
  не влияет. Обычный корректный порядок и A-collapse на loose arm не ломаются.
- Save roundtrip существующей собранной машины не мутирует occupancy/B. Геометрия,
  spring compression, droop, вращение и принятые wheel offsets не меняются.

Scoped refresh выполнен в Unity `6000.3.11f1`: PID `10052`, exit `0`,
`changed=14`, лог `Logs/codex-suspension-c-refresh-01.log`. Повторная проверка
идемпотентности: PID `14408`, exit `0`, `changed=0`, лог
`Logs/codex-suspension-c-refresh-02.log`. Full rebuild не запускался; scoped
authoring сообщил, что manifest и prefab не менялись. Независимая побайтовая
root-сверка это подтвердила: prefab SHA256
`7F4C06B701116F43428C54411C954085E4FA39DAD4736E4BBE1848E7B2F58CF6`, manifest
SHA256 `3BF4C2535BFF9CE40520FE117220FD0AD1434765A8025AEA0847A14DD524B4DF`,
EditorBuildSettings SHA256
`1849B8BE614E204FB0CF463CBCCC784380598D1BB4302E82AC3840F46CF2E454`.

Offline-компиляция пяти assemblies завершилась без ошибок и с пятью уже
существовавшими `CS0414`. EditMode PID `18696` завершился exit `0`: **278/278**,
failed `0`, skipped `0`, duration `55.8245767 s`, XML
`Logs/codex-suspension-c-edit.xml`. Состав: A `65`, B `17`, новый C `23`, новый
generated C `5`, generated `39`, latch/save `12`, alignment `24`, steering `6`,
P0 bolt `5`, assembly `27`, player `52`, audio `3`.

Первый широкий PlayMode PID `20256` завершился exit `2`: 59/61, failed `2`,
skipped `0`, duration `78.8168371 s`. Один старый steering fixture пытался
снять strut при установленной rod; ожидание исправлено на `RemovalBlocked`, а
для последующих steering-проверок используется forced break. Второй провал был
не runtime-дрейфом: aggregate delta `0.0001220703125 m` (`2^-13 m`) на мировой
координате около `z=-1039` оказался ровно одним ULP `float`. Runtime не менялся;
тест сравнивает компоненты с допуском `max(0.0001 m, one ULP)`.

Focused-повтор PID `16152` прошёл 2/2, failed `0`, skipped `0`, exit `0`,
duration `0.835292 s`. Финальный широкий PlayMode PID `10468` прошёл
**61/61**, failed `0`, skipped `0`, exit `0`, duration `78.6002265 s`:
`Logs/codex-suspension-c-play-final.xml` и
`Logs/codex-suspension-c-play-final.log`. Побайтовые хеши prefab, manifest и
EditorBuildSettings после финального прогона остались теми же. Ручная проверка
pending. Прежние A/B 250/250 EditMode и 52/52 PlayMode остаются историческим
evidence; текущий C подтверждают именно 278/278 EditMode и финальные 61/61
PlayMode.
