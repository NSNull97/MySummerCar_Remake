# MSC Remake — Post-08A Legacy Feature Parity Prompt Pack v1

Этот пакет заменяет **все старые промпты после Milestone 08A**.

Проблема старой дорожной карты: она вела от вертикального среза сразу к
оптимизации, локальной полировке и планированию Phase 2. Это не гарантировало,
что до ремастеринга в новом runtime появятся **все** системы оригинального
My Summer Car: полный список NPC, весь транспорт, работы, экономика, события,
сюжетные цепочки, полиция, ралли, телефон, почта, медиа, потребности и прочее.

Новая стратегия:

```text
Phase 1 — Legacy Feature Complete
  Полный оригинальный MSC на новом коде и движке.
  Допустим временный sanitized donor presentation baseline.
  Никакого donor runtime-кода.

Phase 2 — Production Remaster & Polish
  Замена всех temporary donor visuals/audio/animation.
  Новые production-меши, материалы, звук, анимации, расширения и глубокая
  полировка.
```

## Что делать сейчас

1. Закончить только `08A_UI_MENU_SETTINGS_AND_HUD.md`.
2. Выполнить closeout и отдельный коммит.
3. **Не запускать старые**:
   - `09_SAVE_AND_LEGACY_DATA.md`
   - `10_OPTIMIZATION_AND_BUILD.md`
   - `10A_FINAL_POLISH.md`
   - `11_PHASE_2_FULL_GAME_PLANNING.md`
4. Обновить `AGENTS.md` по материалам этого пакета.
5. Запустить только:
   - `Prompts/08B_PHASE1_SCOPE_LOCK_AND_DONOR_FEATURE_PARITY_AUDIT.md`
6. После его отчёта выполнять новые промпты строго по одному.

## Почему сначала аудит

Мы не должны составлять полный список оригинальных механик по памяти. Codex
должен зафиксировать точную donor-версию, исследовать установленную игру,
существующие project reports и текущую реализацию, после чего создать
авторитетную матрицу feature parity. Все последующие этапы работают по этой
матрице и не могут тихо забыть половину Финляндии.

## Содержимое

- обновлённая последовательность Milestone 08B–16;
- Phase 1 Definition of Done;
- шаблон feature-parity matrix;
- addendum и recommended-версия `AGENTS.md`;
- Phase 1 addendum для `PROJECT_DESIGN_GUARDRAILS.md`;
- миграция со старого prompt pack;
- support prompts для closeout/review/continuation;
- готовое сообщение Codex после закрытия 08A.

Не запускай весь архив одним запросом. Один bounded milestone → проверка →
closeout → коммит → следующий milestone.
