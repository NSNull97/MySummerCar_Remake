# Что отправить Codex после завершения 06A

Сначала закрой 06A через `98_MILESTONE_CLOSEOUT.md`, проверь Unity Console,
отчёт, тесты и `git diff`, затем сделай отдельный коммит.

После этого отправь Codex следующий текст:

```text
Этап 06A завершён и закрыт.

Перед началом прочитай AGENTS.md, Prompts/CURRENT_STATE.md,
Prompts/PROJECT_DESIGN_GUARDRAILS.md и отчёт Milestone 06A.

Пользователь подтверждает текущее состояние мира:

- оригинальная карта My Summer Car уже извлечена;
- извлечённая карта уже была визуально просмотрена;
- архитектура world streaming уже подключалась/реализована;
- две созданные кастомные production cells технически существуют, но визуально
  не похожи на соответствующие места оригинала.

Стратегия изменена:

- не реконструировать эти две ячейки вручную сейчас;
- не пытаться улучшать их художественно;
- не извлекать карту повторно без доказанной необходимости;
- использовать точную извлечённую карту оригинала как временный playable runtime
  baseline для feature-parity этапа;
- сохранить полезную streaming-инфраструктуру, registry, cell IDs, tests и
  project-owned metadata;
- кастомное визуальное содержимое двух непохожих ячеек считать
  PrototypeOnly / RejectedForFidelity / Inactive;
- позже заменять donor baseline новым production art через отдельный override
  layer, не ломая gameplay coordinates, stable IDs, saves и streaming.

Выполни только:

Prompts/06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION.md

Не начинай 06B2, 06B3 или Enviro 3 в этом же запуске.

Сначала проведи аудит текущего extraction/import/streaming состояния. Не создавай
второй полный импорт, если каноническая карта уже находится в проекте и пригодна.
Не изменяй donor installation. Не импортируй donor scripts, PlayMaker FSM,
runtime assemblies, old UnityEngine references, cameras, UI, audio, weather,
lighting, Steam/platform logic или gameplay managers.

В финале честно перечисли выполненные проверки, ручные шаги и дай go/no-go только
для 06B2.
```
