# Phase 1 — Legacy Feature Complete scope lock

Статус: **UserApproved**

Donor authority: `msc-world-baseline-04a1.1-c3f2f337`

Дата: 2026-07-21

## Цель

Phase 1 заканчивается приватной, полностью рабочей реконструкцией всех
подтверждённых функций зафиксированной donor-ревизии на новом Unity 6/HDRP
runtime. Это не vertical slice и не начало production-remaster.

Авторитетный перечень функций находится в
`LEGACY_FEATURE_PARITY_MATRIX.csv`. Supporting rosters раскрывают конкретных
NPC, транспорт, работы, события, сервисы, категории предметов и presentation.

## Входит в Phase 1

- все donor-evidenced действия игрока, включая ходьбу, бег, два уровня приседа,
  наклон вперёд пешком, боковые наклоны в машине через открытое окно/дверь и
  надёжное прохождение штатных порогов/ступеней;
- needs, быт, дом, сауна, сон, еда, напитки, курение, алкоголь и последствия;
- физические предметы, инструменты, жидкости, упаковки и контейнеры;
- полная Satsuma: сборка, крепёж, электрика, жидкости, настройка, износ,
  повреждения, обслуживание, инспекция и сохранение;
- весь подтверждённый транспорт и транспортные акторы;
- все подтверждённые NPC, расписания, диалоги и relationship state;
- traffic, bus, train, boat и scripted routes;
- магазины, паб, Fleetari, инспекция, заправка, телефон, почта, счета, каталог;
- все jobs, repeatable activities, story/event chains, progression outcomes;
- police, fines, checkpoints, jail, hospital, death/permadeath и rally;
- radio, TV, computer, music import, minigames и local events;
- world triggers, utilities, time/calendar и cross-domain persistence;
- временная `TemporaryDirectImport` presentation, достаточная для понимания
  каждой функции, но отделённая от project-owned gameplay state.

## Защищённый baseline 00–08A

Принятые архитектура, stable IDs, interaction, player baseline, world/streaming,
vehicle prototypes, Enviro 3 integration, audio boundary, save interfaces,
tests/tooling и визуально принятый UI 08A сохраняются. Последующие milestones
расширяют их adapters/bindings и не переигрывают ранее закрытые prompts.

Материалы world-hardening приняты пользователем 2026-07-21. Solid collision и
полная traversal coverage не считаются принятыми: текущая коллизия частична, а
ступени у магазина Теймо воспроизводят gap.

## Player locomotion decision lock

Текущий `CharacterController` не заменяется в 08B. Для первого bounded batch
09C зафиксирован следующий порядок:

1. снять donor evidence скоростей, высот поз, переходов второго приседа,
   величины наклона вперёд и оконно-дверных условий автомобильных наклонов;
2. создать stable-anchor traversal fixtures для дома, паба, Теймо, Fleetari и
   репрезентативных внешних порогов;
3. проверить геометрию и collision proxies порога/рамы/ступеней;
4. проверить совместимое tuning и project-owned step/ground-snap assist поверх
   существующего motor;
5. менять backend только если корректные colliders и bounded solver не проходят
   измеримые критерии на целевых frame rates и после cell reload.

Если миграция понадобится, сохраняются prefab/stable identity, public input и
interaction contracts, camera/ray/carry, footsteps, streaming/OOB recovery и
save compatibility. Physical full body, world-space руки и анимационно
зависимая логика не входят в scope.

## Не входит в Phase 1

- production reauthoring моделей, текстур, материалов, vegetation и audio;
- новые районы, NPC, jobs, story, seasons и remake-only механики;
- multiplayer, mod SDK, console, VR, ECS и ray tracing baseline;
- physical full-body player, generic physical hands, animated vehicle entry;
- расширенная deformation/bodywork и AI сверх locked donor behavior;
- публичная/distributable сборка с donor payload.

Эти пункты находятся в `PHASE2_BACKLOG.md` либо остаются non-goals.

## Правила scope

1. Frozen 04A1 — единственный donor authority; версии не смешиваются.
2. Новая функция входит в Phase 1 только при donor evidence или если нужна для
   безопасности, runtime independence, compatibility либо testability.
3. Сходное имя класса, объекта или prefab не доказывает реализацию.
4. Required feature получает project-owned `FeatureId`, owner milestone,
   presentation, UI/audio feedback, save coverage и acceptance evidence.
5. `Verified` требует выполненного теста/playthrough/comparison; persistent row
   без save/load evidence не может быть `Verified`.
6. Known difference требует явного решения пользователя.
7. Required `Unknown`/`Blocked` запрещает Phase 2 gate.
8. Матрица 08B просмотрена и принята пользователем; реализация 09A разрешена.

## Закрытие scope

`LEGACY_FEATURE_PARITY_MATRIX.csv` просмотрена и принята пользователем
2026-07-21. Scope зафиксирован как `UserApproved`; изменения матрицы далее
проходят через явную корректировку scope с сохранением evidence.
