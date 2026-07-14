# BUILD-001 closure — intentional post-Milestone 04 baseline

Дата: 2026-07-14

Исходная ревизия: `fc18f93` (`Milestone 04 completed`)

Finding: `BUILD-001`

## Результат

`BUILD-001` закрыт. Ранее смешанное рабочее дерево классифицировано и разнесено
по самостоятельным commits без удаления пользовательских исходников и без
добавления donor payloads.

Сгенерированный `MySummerCar_Remake.slnx` удалён только из Git-индекса и добавлен
в `.gitignore`; локальный IDE-файл сохранён на диске.

## Созданные commits

| Commit | Назначение |
|---|---|
| `63272a9` | generated Unity solution больше не отслеживается |
| `d37fc0c` | Unity 6/HDRP serialization migration и ProjectSettings |
| `ba1f54e` | UI concept references |
| `4817ff4` | explicit partial interaction bindings |
| `5cf780c` | carry lifecycle, ray query и input scaling fixes |
| `e7d7af1` | post-M4 regression tests |
| `13d5577` | development performance probe вынесен из World runtime |
| `0a0bbe0` | canonical `sharedassets3.assets` provenance hash |
| `f579a40` | post-M4 prompt set, sequence и manifest |
| `062110d` | review/fix reports и синхронизация архитектурных документов |

Commit, содержащий этот отчёт, фиксирует финальные current-state/sequence
изменения и завершает intentional baseline.

## Проверки границ

- donor binaries, decompiled source и raw extraction в commits не добавлялись;
- machine-specific local configuration не добавлялась;
- generated `.csproj`, `.sln` и `.slnx` не отслеживаются;
- Unity source assets, runtime fixes, tests, provenance, docs и reference media
  находятся в раздельных commits;
- полный `04A_FULL_WORLD_GEOMETRY_TRANSFER.md` остаётся parked.

## Оставшийся finding

`PERF-001` не закрыт: существующий M3 capture не является интегрированным
Player + World performance proof и не содержит полного набора требуемых GPU,
allocation, physics, draw и memory метрик. Это не блокирует bounded
`04A_WORLD_LAYOUT_PILOT.md`, но блокирует performance-go вертикального среза.

## Следующий milestone

Разрешён ровно один следующий milestone:
`Prompts/04A_WORLD_LAYOUT_PILOT.md`.
