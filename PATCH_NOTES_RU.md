# Патч AGENTS.md под полную Phase 1 Legacy Feature Complete

## Основа

Файл создан путём точечного патча предоставленного `AGENTS(2).md`, а не заменой
на более старую reference-версию.

- SHA-256 исходника: `4cde30fc6d4b0a1e6fde00e54ada62a557e96206bce67c91ccc8b1fbc30ecbc0`
- SHA-256 патченного файла: `1ecb18cf7307efae28ac9a86c5a5cfbd30ebe7698e1fb67391d17461ea206879`
- Строк в исходнике: 882
- Строк в результате: 1108

## Что сохранено без отката

- все правила world runtime baseline и streaming;
- Enviro 3 как visual weather backend;
- текущая архитектура viewmodel-рук без физического тела;
- правила vehicle assembly/simulation;
- stable IDs, save DTO, donor boundaries;
- Wwise boundary;
- утверждённый `29A Approved UI reference lock` для Milestone 08A;
- все остальные существующие ограничения и пути проекта.

## Что добавлено

1. Обязательная модель `Phase 1 Legacy Feature Complete → Phase 2 Remaster`.
2. Защита уже сделанного в milestones 00–08A от массовой перезаписи.
3. Temporary donor gameplay presentation baseline для NPC, машин, предметов,
   анимаций и звука в приватной Phase 1 сборке.
4. Модули Characters, NPC, Items, Needs, Traffic, Economy, Services, Jobs,
   Progression и Media.
5. Авторитетная feature-parity matrix и документы `Docs/Phase1/`.
6. Полное save/test/performance покрытие населённой игры, а не только vertical
   slice.
7. Жёсткий Phase 1 completion gate перед production-полировкой.
8. Stop conditions против преждевременного Phase 2 и ломания готового 00–08A.

## Маленькая безопасная чистка

Удалена одна случайно продублированная строка
`Original textures must not be used as final production textures.` — смысл не
изменён.

## Как применить

Самый безопасный вариант — заменить локальный `AGENTS.md` файлом из этого пакета
после просмотра diff.

Альтернатива:

```bash
git apply --check AGENTS_Post08A_Phase1.patch
git apply AGENTS_Post08A_Phase1.patch
```

Если локальный `AGENTS.md` изменился после загрузки исходника, сначала используй
`git apply --check`; при конфликте перенеси изменения вручную по diff, не
перетирая новые локальные правила.
