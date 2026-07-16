# Что изменено в AGENTS.md

В архиве лежит полный обновлённый `AGENTS.md`. Его не надо слепо копировать:
сначала сравни с текущим root-файлом проекта.

Главные изменения:

1. Добавлено исключение для sanitized donor world runtime baseline.
2. Baseline хранится под `LegacyImport/RuntimeBaseline` и классифицируется
   `TemporaryDirectImport`.
3. Raw donor payload по-прежнему не коммитится.
4. Baseline разрешён только для private local feature-parity builds и не
   считается final production art.
5. Donor scripts/FSM/runtime assemblies/UI/audio/weather/gameplay logic
   запрещены.
6. Gameplay не может зависеть от donor hierarchy names/paths.
7. Добавлены Legacy / Gameplay / ProductionOverride layers.
8. Запрещено деструктивно резать единый terrain/road/water без seam-safe tool.
9. Две непохожие кастомные ячейки переводятся в inactive prototypes, а не
   ремонтируются вручную сейчас.
10. Full-body player и физические руки убраны из направления проекта.
11. Enviro 3 закреплён как visual backend, а game weather остаётся project-owned.
