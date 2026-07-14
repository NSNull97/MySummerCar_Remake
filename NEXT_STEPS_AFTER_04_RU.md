# Как продолжить после завершённого Milestone 04

Текущее подтверждённое состояние:

```text
00 Bootstrap and Donor Audit      DONE
01 Unity Foundation               DONE
02 Donor Reference Pipeline       DONE
03 Garage Art Prototype           DONE
04 Player Interaction             DONE
Git commit after 04               DONE
Post-04 read-only review          DONE
Approved review fixes             DONE
BUILD-001 clean Git baseline      DONE
```

Следующий обязательный этап — только ограниченный world-layout pilot. Полный
перенос мира отложен действующим `AGENTS.md` до стабильного single-player
vertical slice. `PERF-001` остаётся отдельным открытым finding для будущего
интегрированного performance gate и не разрешает объявлять производительность
вертикального среза подтверждённой.

---

## 1. Ревью и исправления — выполнено

Результаты сохранены в `Docs/Reviews/REVIEW_AFTER_04.md`,
`Docs/Reviews/FIX_REPORT_20260714_113701.md` и отдельном отчёте закрытия
`BUILD-001`. Изменения разнесены по тематическим commits; повторять review gate
перед `04A-Pilot` не требуется.

---

## 2. Выполни ограниченный world-layout pilot

Запрос:

```text
Прочитай AGENTS.md и полностью прочитай
Prompts/04A_WORLD_LAYOUT_PILOT.md.

Выполни только Milestone 04A-Pilot для гаража и ближайшего участка дороги.
Сохрани завершённую архитектуру Player/Interaction.
Не переходи к 04B или 05.
В конце создай Docs/Milestones/MILESTONE_04A_PILOT_REPORT.md и остановись.
```

Это большой этап. Если Codex остановился из-за лимита контекста или перезапуска,
не отправляй основной prompt заново вслепую. Используй:

```text
Прочитай AGENTS.md, Prompts/99B_CONTINUE_CURRENT_MILESTONE.md и
Prompts/04A_WORLD_LAYOUT_PILOT.md.
Продолжи только незавершённые пункты Milestone 04A-Pilot.
Не начинай следующий milestone.
```

После завершения запусти при необходимости:

```text
Прочитай AGENTS.md и Prompts/98_MILESTONE_CLOSEOUT.md.
Закрой только Milestone 04A-Pilot: компиляция, тесты, документы, отчёт,
список ручных действий. Новые функции не добавляй.
```

Затем Unity → компиляция → тесты → `git diff` → коммит:

```text
Milestone 04A-Pilot: bounded world layout transfer
```

---

## 3. Собери измерения и эталонные данные

Следующий prompt:

```text
Прочитай AGENTS.md и полностью прочитай
Prompts/04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md.

Выполни только Milestone 04B.
Не меняй донорскую игру и не переходи к Vehicle Assembly.
В конце создай Docs/Milestones/MILESTONE_04B_REPORT.md.
```

После проверки сделай коммит:

```text
Milestone 04B: reference capture and measurements
```

---

## 4. Реализуй сборку автомобиля

```text
Прочитай AGENTS.md и полностью прочитай
Prompts/05_VEHICLE_ASSEMBLY.md.

Выполни только Milestone 05.
Не реализуй подробную физику двигателя и шин.
В конце создай Docs/Milestones/MILESTONE_05_REPORT.md.
```

Проверка и коммит:

```text
Milestone 05: vehicle assembly foundation
```

---

## 5. Запусти production-ремастер мира

Первый запуск:

```text
Прочитай AGENTS.md и полностью прочитай Prompts/05A_WORLD_REMASTER.md.

Выполни первый проход Milestone 05A:
архитектура production replacement, dashboard, registry, pilot zone,
валидация pilot zone и полный art backlog.

Не пытайся объявить всю карту готовой за один проход.
Создай Docs/Milestones/MILESTONE_05A_REPORT.md и остановись.
```

После первого прохода остальные зоны делаются повторяемым prompt:

```text
Прочитай AGENTS.md и полностью прочитай
Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md.

Выбери следующую зону строго по ZONE_REMASTER_STATUS и roadmap.
Обработай только один ограниченный batch, обнови ledger/status/report
и остановись.
```

Повторяй его столько раз, сколько требуется. Каждый успешный batch лучше
коммитить отдельно:

```text
World remaster: <zone name>
```

Для вертикального среза не обязательно ждать 100% всей карты. Достаточно
полностью готовой и проверенной игровой зоны, после чего запускай `05B`.

---

## 6. Проведи валидацию мира

```text
Прочитай AGENTS.md и полностью прочитай
Prompts/05B_WORLD_VALIDATION.md.

Проведи validation gate для текущего фактического покрытия:
pilot, vertical slice или full world.
Не создавай новый арт вместо отсутствующего.
Создай Docs/Milestones/MILESTONE_05B_REPORT.md.
```

Если gate прошёл для vertical slice, можно идти к физике автомобиля, а
ремастер остальных зон продолжать отдельными batches через `05A_CONTINUE`.

---

## 7. Дальнейший линейный порядок

```text
06_VEHICLE_SIMULATION.md
06A_PHYSICS_VALIDATION.md
07_WORLD_WEATHER_HDRP.md
08_WWISE_AUDIO.md
08A_UI_MENU_SETTINGS_AND_HUD.md
09_SAVE_AND_LEGACY_DATA.md
10_OPTIMIZATION_AND_BUILD.md
10A_FINAL_POLISH.md
11_PHASE_2_FULL_GAME_PLANNING.md
```

После каждого основного milestone:

1. Не запускай следующий prompt сразу.
2. При необходимости запусти `98_MILESTONE_CLOSEOUT`.
3. Открой Unity и дождись компиляции.
4. Запусти доступные тесты.
5. Прочитай milestone report.
6. Проверь `git status` и `git diff`.
7. Сделай отдельный коммит.
8. Только потом переходи дальше.

---

## Утилиты

### `97_CONTEXT_REFRESH.md`

Используй при открытии нового Codex-чата, после большого перерыва или крупных
ручных изменений. Он ничего не меняет, только восстанавливает актуальное
понимание репозитория.

### `98_MILESTONE_CLOSEOUT.md`

Используй, когда основной prompt вроде бы закончен, но надо гарантированно
проверить компиляцию, тесты, отчёт, TODO и границы milestone.

### `99_REVIEW.md`

Архитектурное и техническое ревью без исправлений. Рекомендуется после:

- `04`;
- `04A`;
- `05`;
- первого прохода `05A`;
- `06`;
- `08A`;
- `10`.

### `99A_FIX_APPROVED_REVIEW_FINDINGS.md`

Исправляет только те finding IDs, которые ты явно перечислил.

### `99B_CONTINUE_CURRENT_MILESTONE.md`

Используй после обрыва сессии или неполного выполнения. Он запрещает
перескакивать на следующий milestone.

---

## Чего не делать

Не отправляй Codex сразу несколько milestone-файлов с просьбой «сделай всё».
Не запускай `05A` до успешного `04A`.
Не запускай `06` до работоспособной сборки машины.
Не запускай финальную оптимизацию до появления репрезентативного vertical slice.
Не принимай фразу «готово» без milestone report, компиляции и проверки в Unity.

Иначе получится архитектурный холодец: форма есть, а где мясо — никто не знает.
