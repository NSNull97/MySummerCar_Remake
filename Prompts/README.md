# MSC Remake Codex Prompt Set v2

Этот набор рассчитан на проект после завершённого и закоммиченного
`04_PLAYER_INTERACTION`.

## Основная последовательность

| Порядок | Prompt | Назначение |
|---:|---|---|
| 0–4 | `00`–`04` | Уже завершены. Не запускать повторно без причины |
| Gate | `99_REVIEW.md` | Read-only ревью текущего состояния |
| 4A-Pilot | `04A_WORLD_LAYOUT_PILOT.md` | Ограниченный layout-перенос: гараж и ближайшая дорога |
| Parked | `04A_FULL_WORLD_GEOMETRY_TRANSFER.md` | Будущий полный перенос; не запускать при текущем non-goal |
| 4B | `04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md` | Измерения и эталонные fixtures |
| 5 | `05_VEHICLE_ASSEMBLY.md` | Детали, крепления, болты, инструменты |
| 5A | `05A_WORLD_REMASTER.md` | Production pipeline и pilot zone |
| Repeat | `05A_CONTINUE_NEXT_WORLD_ZONE.md` | Следующая зона ремастера |
| 5B | `05B_WORLD_VALIDATION.md` | Parity/coverage validation gate |
| 6 | `06_VEHICLE_SIMULATION.md` | Двигатель и ходовая симуляция |
| 6A | `06A_PHYSICS_VALIDATION.md` | Калибровка и физическая проверка |
| 7 | `07_WORLD_WEATHER_HDRP.md` | Время, погода, мокрые поверхности |
| 8 | `08_WWISE_AUDIO.md` | Аудио-архитектура и Wwise |
| 8A | `08A_UI_MENU_SETTINGS_AND_HUD.md` | Меню, настройки, HUD |
| 9 | `09_SAVE_AND_LEGACY_DATA.md` | Сохранения и миграции |
| 10 | `10_OPTIMIZATION_AND_BUILD.md` | Профилирование и vertical slice build |
| 10A | `10A_FINAL_POLISH.md` | Финальная полировка среза |
| 11 | `11_PHASE_2_FULL_GAME_PLANNING.md` | План полного игрового контента |

## Правило работы

Один основной prompt → проверка → отчёт → Unity → тесты → `git diff` → коммит.

Никогда не запускай весь набор одновременно.

Полная инструкция находится в корне архива:

`NEXT_STEPS_AFTER_04_RU.md`
