# Как обновить AGENTS.md

Локальный `AGENTS.md` мог уже получить изменения 06B, Enviro 3 и 08A UI lock.
Поэтому не заменяй его вслепую.

## Обязательные изменения

1. В `Mission` или сразу после `Project scope` добавить двухфазную модель:
   Phase 1 Legacy Feature Complete → Phase 2 Production Remaster.
2. В `Current non-goals` заменить условие «до стабильного vertical slice» на
   «до стабильного Phase 1 full-game parity build».
3. В donor-import исключениях разрешить не только world baseline, но и
   sanitized gameplay presentation baseline из addendum.
4. После `Temporary donor world runtime baseline` добавить раздел
   `Temporary donor gameplay presentation baseline`.
5. Добавить правила `Feature parity authority and tracking`.
6. Добавить `Phase 1 scope control` и `Phase 1 completion gate`.
7. Сохранить существующий `Approved UI reference lock` 08A.
8. Не ослаблять запреты на donor code, assemblies, PlayMaker runtime, Steam/DRM.

`AGENTS_RECOMMENDED_POST_08A.md` — готовая reference-версия на основе prompt pack
v5 + 08A UI lock + новых Phase 1 правил. Сначала сравни её с локальным файлом.
