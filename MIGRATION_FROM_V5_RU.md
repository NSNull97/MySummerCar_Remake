# Миграция со старого prompt pack v5

После успешного завершения 08A следующие старые файлы больше не являются
актуальной дорожной картой:

| Старый prompt | Статус | Замена |
|---|---|---|
| `09_SAVE_AND_LEGACY_DATA.md` | Deprecated | `09A` + `14D` |
| `10_OPTIMIZATION_AND_BUILD.md` | Deprecated | `15B` |
| `10A_FINAL_POLISH.md` | Deprecated | `15C`; production polish перенесён в Phase 2 |
| `11_PHASE_2_FULL_GAME_PLANNING.md` | Deprecated | `16` |

Не удаляй старые milestone reports. Они остаются историей проекта.

Рекомендуется переместить старые prompt-файлы в:

```text
Prompts/Deprecated/PrePhase1FullParity/
```

или добавить в их начало жёсткое предупреждение `DEPRECATED — DO NOT RUN`.

Новая Phase 1 не является художественной полировкой. Она закрывает весь
оригинальный gameplay/content scope на новом runtime, используя допустимый
Legacy presentation baseline.
