# Phase 1 — Legacy Feature Complete

## Цель

Получить полностью рабочую, проходимую и сохраняемую реконструкцию выбранной
версии оригинального My Summer Car на новом Unity 6/HDRP runtime.

Phase 1 должна содержать все подтверждённые donor-версией:

- игровые системы;
- NPC и их поведение;
- транспорт;
- предметы;
- потребности и бытовой цикл;
- магазины, сервисы, работы и экономику;
- сюжетные и условные события;
- инспекцию, полицию, ралли, наказания и восстановление;
- телефон, почту, радио, телевидение, компьютер и мини-активности;
- сохранение и загрузку всего мира.

## Legacy здесь означает

- временные donor-derived meshes, textures, materials, rigs, animation clips,
  audio clips and presentation data могут использоваться только в приватной
  feature-parity сборке;
- весь gameplay/runtime-код принадлежит новому проекту;
- donor executable, assemblies, PlayMaker runtime, old Unity runtime, Steam/DRM
  logic не используются;
- temporary presentation не считается production art;
- каждый temporary asset имеет provenance и replacement key.

## Не входит в Phase 1

Если конкретная вещь не существовала в donor-версии и не нужна для безопасной
работы parity build, она переносится в Phase 2 backlog. Примеры:

- новая система плавания;
- глубокий dynamic bodywork/paint/rust;
- физические руки и full body;
- production SpeedTree forest replacement;
- новая умная память NPC сверх donor поведения;
- новые районы, работы, персонажи или сюжет;
- production reauthoring мешей/текстур/аудио;
- расширенная деформация кузова;
- новые сезоны.

## Bug policy

- исправлять crashes, data loss, softlocks, out-of-bounds и явные runtime defects;
- сохранять подтверждённые gameplay rules и узнаваемые intentional quirks;
- не обязаны переносить случайные exploits, undefined behavior и старые engine
  bugs;
- спорные quirks записываются в parity matrix и требуют решения пользователя.
