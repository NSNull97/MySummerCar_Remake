# MSC Remake Codex Prompt Set v5 — donor runtime baseline + Enviro 3

Этот набор продолжает проект с активного/закрываемого этапа **Milestone 06A**.

Главное изменение v5: оригинальная карта уже извлечена и просмотрена, а world
streaming уже подключался. Поэтому следующий этап больше не требует вручную
ремонтировать две непохожие production cells.

Вместо этого:

- точная карта оригинала становится временным sanitized runtime baseline;
- существующая streaming-архитектура переиспользуется;
- две неправильные кастомные ячейки сохраняют полезную инфраструктуру, но их
  визуальные roots отключаются;
- production art позже заменяет donor baseline через override layers;
- Enviro 3 подключается только после того, как baseline стабильно загружается и
  стримится.

Сначала прочитай:

1. `START_HERE_RU.md`
2. `CURRENT_STATE.md`
3. `PROJECT_DESIGN_GUARDRAILS.md`
4. `AFTER_06A_SEND_TO_CODEX_RU.md`
5. `PROMPT_PACK_V5_AUDIT.md`

## Текущая последовательность

| Порядок | Prompt | Назначение |
|---:|---|---|
| Active | `06A_PHYSICS_VALIDATION.md` | Текущая калибровка и проверка физики |
| Gate | `98_MILESTONE_CLOSEOUT.md` | Закрытие 06A после реальных тестов |
| 06B1 | `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION.md` | Аудит уже извлечённой карты, канонический source, sanitation |
| 06B2 | `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md` | Подключение baseline к существующему streaming и замена неправильного custom visual |
| 06B3 | `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md` | Full-map validation, debt catalogue, baseline revision freeze |
| Deprecated | `06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md` | Не запускать; старый арт-ремонт двух ячеек отменён |
| Manual | `ENVIRO3_MANUAL_SETUP_RU.md` | Ручной импорт/проверка Enviro 3, если пакет ещё не в проекте |
| 07A | `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md` | Локальный API-аудит Enviro, vendor boundary, WeatherLab |
| 07B | `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md` | Project-owned time/weather/wetness/lightning + Enviro adapter |
| 07C | `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md` | Интеграция в active donor-baseline world и streaming |
| Deprecated | `07_WORLD_WEATHER_HDRP.md` | Только предупреждение; не запускать |
| 08+ | existing prompts | Audio, UI, save, optimization, polish, phase 2 |

## Базовая архитектура мира

```text
World_Global_Legacy
World_Cell_<ID>_Legacy
World_Cell_<ID>_Gameplay
World_Cell_<ID>_ProductionOverride
```

Литеральные имена могут отличаться, если текущая архитектура уже даёт такое же
разделение.

Legacy baseline:

- используется только как `TemporaryDirectImport`;
- может входить в private local feature-parity builds;
- не является final production art;
- не должен содержать donor scripts/FSM/runtime assemblies;
- не коммитится как raw donor payload;
- не может быть основой identity/save через donor hierarchy names.

## Правило работы

```text
один bounded prompt
→ отчёт Codex
→ Unity compilation
→ EditMode/PlayMode tests
→ ручная проверка
→ git diff
→ closeout
→ коммит
→ следующий prompt
```

Никогда не запускай весь архив одним запросом.
