# С ЧЕГО ПРОДОЛЖАТЬ — PROMPT PACK v5

## Сейчас

Активен или закрывается `06A_PHYSICS_VALIDATION.md`.

1. Закончи только 06A.
2. Проверь фактический отчёт, телеметрию и Unity Console.
3. Запусти `98_MILESTONE_CLOSEOUT.md`, явно указав Milestone 06A.
4. Проверь `git diff` и сделай отдельный коммит после человеческой проверки.

Точный текст для следующего запуска лежит в:

`AFTER_06A_SEND_TO_CODEX_RU.md`

## Потом — не художественный ремонт двух ячеек

Карта оригинала уже вытащена и просмотрена, а streaming уже подключался. Поэтому
не надо сейчас вручную лепить две «production» ячейки заново и пытаться сделать
их похожими.

Правильная последовательность:

1. `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION.md`
   - найти уже существующую каноническую extraction/import версию;
   - не извлекать карту повторно без причины;
   - удалить/не допустить donor scripts и старую runtime-логику;
   - получить точную sanitized full-map baseline.
2. closeout и коммит.
3. `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md`
   - использовать уже существующую streaming-архитектуру;
   - распределить допустимые объекты по cells;
   - большие непрерывные объекты оставить global;
   - отключить неправильный кастомный визуал двух ячеек;
   - активировать там donor baseline.
4. closeout и коммит.
5. `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`
   - проверить весь baseline, traversal, collision, OOB и streaming;
   - каталогизировать старые визуальные костыли, но пока не ремастерить их;
   - заморозить reproducible baseline revision.
6. human review, closeout и коммит.

Старый `06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md` не запускать.

## После закрытия 06B3

Если Enviro 3 ещё не импортирован, выполни:

`ENVIRO3_MANUAL_SETUP_RU.md`

Затем запускай строго по одному:

1. `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`
2. closeout и коммит
3. `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md`
4. closeout и коммит
5. `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`
6. closeout и коммит
7. `08_WWISE_AUDIO.md`
8. `08A_UI_MENU_SETTINGS_AND_HUD.md`
9. `09_SAVE_AND_LEGACY_DATA.md`
10. `10_OPTIMIZATION_AND_BUILD.md`
11. `10A_FINAL_POLISH.md`
12. `11_PHASE_2_FULL_GAME_PLANNING.md`

Старый `07_WORLD_WEATHER_HDRP.md` не запускать.

## Смысл нового подхода

Первая большая цель — рабочий оригинальный MSC на новом коде и движке. Поэтому
сейчас выгоднее получить точную старую карту как временный runtime baseline, чем
полировать два красивых, но чужих места.

Позже каждый legacy-объект заменяется через production override без изменения
координат и без поломки gameplay. Снаружи пока старый сарай, внутри уже нормальный
фундамент — всё честно, без шаманства ради шаманства.
