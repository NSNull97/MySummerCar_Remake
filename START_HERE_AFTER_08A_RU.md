# С чего продолжать после 08A

## 1. Закрыть текущий UI milestone

- закончить `08A_UI_MENU_SETTINGS_AND_HUD.md`;
- получить implementation/reference/overlay captures;
- получить пользовательское визуальное одобрение утверждённых экранов;
- выполнить `98_MILESTONE_CLOSEOUT.md` для 08A;
- проверить Unity Console, тесты, `git diff`;
- сделать отдельный коммит.

## 2. Обновить правила проекта

Не перетирай локальный `AGENTS.md` вслепую.

Сначала прочитай:

- `AGENTS_PHASE1_LEGACY_PARITY_ADDENDUM.md`;
- `AGENTS_PHASE1_LEGACY_PARITY_MERGE_GUIDE_RU.md`;
- `AGENTS_RECOMMENDED_POST_08A.md` как reference-версию;
- `Patches/AGENTS_PHASE1_LEGACY_PARITY.patch`.

## 3. Запустить только 08B

Передай Codex текст:

`Prompts/AFTER_08A_SEND_TO_CODEX_RU.md`

Он должен выполнить только аудит и планирование. Никакой массовой реализации
NPC, машин и сюжетов в рамках 08B.

## 4. Новая последовательность

| Order | Prompt |
|---:|---|
| 08B | `08B_PHASE1_SCOPE_LOCK_AND_DONOR_FEATURE_PARITY_AUDIT.md` |
| 09A | `09A_FULL_GAME_NATIVE_SAVE_FOUNDATION.md` |
| 09B | `09B_WORLD_ITEMS_CONSUMABLES_AND_CONTAINERS_PARITY.md` |
| 09C | `09C_PLAYER_NEEDS_HOME_SAUNA_AND_LIFE_LOOP_PARITY.md` |
| 10A | `10A_NPC_CHARACTER_RUNTIME_FOUNDATION.md` |
| 10B | `10B_FULL_NPC_ROSTER_DIALOGUE_AND_SCHEDULE_PARITY.md` |
| 11A | `11A_FULL_VEHICLE_ROSTER_AND_LEGACY_PRESENTATION.md` |
| 11B | `11B_TRAFFIC_PUBLIC_TRANSPORT_TRAIN_AND_ROUTE_AI.md` |
| 12A | `12A_COMMERCE_PHONE_MAIL_SERVICES_AND_ECONOMY_PARITY.md` |
| 12B | `12B_JOBS_REPEATABLE_ACTIVITIES_AND_WORLD_TASKS_PARITY.md` |
| 13A | `13A_STORY_RELATIONSHIPS_PROGRESSION_AND_EVENT_GRAPH_PARITY.md` |
| 13B | `13B_INSPECTION_POLICE_RALLY_JAIL_HOSPITAL_AND_DEATH_PARITY.md` |
| 13C | `13C_MEDIA_MINIGAMES_AND_REMAINING_DONOR_MECHANICS.md` |
| 14A | `14A_PHASE1_PARITY_GAP_AUDIT_AND_WAVE_PLAN.md` |
| repeat | `14B_PHASE1_PARITY_GAP_IMPLEMENTATION_WAVE.md` |
| 14C | `14C_LEGACY_PRESENTATION_COMPLETION.md` |
| 14D | `14D_FULL_GAME_SAVE_COVERAGE_AND_RECOVERY_HARDENING.md` |
| 15A | `15A_FULL_GAME_INTEGRATION_AND_PARITY_PLAYTHROUGH.md` |
| 15B | `15B_FULL_GAME_OPTIMIZATION_CONTENT_AUDIT_AND_PRIVATE_BUILD.md` |
| 15C | `15C_PHASE1_STABILIZATION_REGRESSION_AND_RC_GATE.md` |
| 16 | `16_PHASE2_PRODUCTION_REMASTER_AND_POLISH_PLANNING.md` |

После каждого этапа: closeout, ручная проверка, отдельный коммит.
