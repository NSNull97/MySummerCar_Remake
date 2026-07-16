# Отчёт миграции двух prototype world cells

Дата: 2026-07-16

Milestone: `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE`

Автоматический результат: **PASS**

Ручная visual/traversal приёмка: **PASS / HumanAccepted**

## 1. Исходное состояние

Две project-authored custom cells были полезны как bounded streaming/collision
fixtures, но ранее отклонены по fidelity:

| Cell | Старый visual scene | Статус |
|---|---|---|
| `cell_0_-3` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity` | `PrototypeOnly / RejectedForFidelity` |
| `cell_0_-2` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity` | `PrototypeOnly / RejectedForFidelity` |

Их cell IDs, lifecycle infrastructure, tests, stable IDs и project-owned
gameplay metadata требовалось сохранить, но visual content нельзя было оставлять
активным в feature-parity world profile.

## 2. Выполненная миграция

Active manifest теперь имеет profile ID:

`donor-feature-parity-06b2`

Для `cell_0_-3` и `cell_0_-2` он загружает соответствующие generated donor
baseline scenes:

```text
Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/
  World_Cell_0_-3_Legacy.unity
  World_Cell_0_-2_Legacy.unity
```

Старые custom scenes:

- отсутствуют в active donor manifest;
- не загружаются Bootstrap composition root;
- не удалены и не перемоделированы;
- сохранены в отдельном `prototype-fixture` manifest;
- доступны через
  `Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity`
  для regression/debug.

Это исключает одновременную загрузку donor и rejected prototype visuals в
обычном active profile.

## 3. Что сохранено независимо от visuals

Project-owned gameplay catalog:

`Assets/Game/World/Content/Streaming/WorldGameplayCellCatalog.asset`

Catalog ID:

`project-gameplay-anchors-06b2`

Он содержит 15 стабильных anchors:

- 7 anchors в `cell_0_-3`;
- 8 anchors в `cell_0_-2`.

Anchors сохраняют `StableEntityId`, position и rotation и не ссылаются на donor
GameObject, hierarchy path или Unity scene object reference.

## 4. Проверки миграции

Автоматически подтверждено:

- Bootstrap использует donor feature-parity manifest;
- обе rejected prototype scenes отсутствуют в active profile;
- обе donor cell scenes присутствуют и валидны;
- prototype fixture manifest остаётся отдельным и работоспособным;
- repeated load/unload не создаёт duplicate legacy IDs или replacement keys;
- gameplay anchors доступны независимо от visual replacement mode;
- existing prototype EditMode tests: `3/3 PASS`;
- existing prototype PlayMode tests: `8/8 PASS`;
- bounded 05B.1 prototype player build: `PASS`, 3 scenes.

## 5. Результат ручной приёмки

Пользователь подтвердил 2026-07-16:

- active baseline соответствует оригинальной карте;
- прежняя custom interpretation не отображается вместо donor world;
- пеший маршрут до озера работает;
- район Теймо выглядит корректно;
- перенос персонажа к Теймо и обратно вызывает unload/reload объектов без
  замеченного duplicate world;
- out-of-bounds recovery возвращает персонажа домой.

Плоское озеро и существующие в оригинале terrain voids приняты как временный
legacy/remaster debt. Они не считаются дефектом migration и не заявляются
исправленными.

Migration gate закрыт: **PASS / HumanAccepted**.
