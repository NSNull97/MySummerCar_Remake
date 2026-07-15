# Collision Remaster Report — 05A pilot

## Result

Production collision is newly authored and independent from donor colliders. The pilot prefab contains 134 colliders across terrain/road, floors, walls, roofs, moving architecture, props and tree trunks. Static meshes use generated MeshColliders only where the simple planar shape warrants it; doors/gates use primitive colliders and are not dynamic non-convex mesh colliders.

Automated validation checks collider presence, garage door dimensions, representative vehicle envelope fit and scene integration. Focused PlayMode tests cover pilot loading, mode switching and clearance fixtures.

Manual player navigation, fast vehicle approach, invisible blockers, thin-wall tunnelling, stair comfort and continuous multi-cell road collision still require Unity review. No full-map collision completion is claimed.

## Batch 01 — HomeShorelinePier

Batch 01 добавляет 30 colliders: 14 walkable plank colliders, опоры/пороги/понтонные формы, shore approach/path и три simplified hedge colliders. Автоматический допуск непрерывности пирса — максимум `0,1 м`; измеренный зазор — `0,08 м`. `BoundedLakeSurface` намеренно не имеет collider; пользователь подтвердил, что вода не блокирует игрока.

Первая ручная проверка выявила `SEAM-001`: approach/path не доходили до края принятого home terrain на `3,23 / 4,23 м`. Геометрия продлена с перекрытием стыка более `1 м`; статический валидатор контролирует overlap, а PlayMode-тест трассирует walkable collision с шагом `1 м` от домашнего terrain до порога пирса и ограничивает скачок высоты `0,25 м`. Повторный ручной проход пользователя 2026-07-15 — PASS, дефект не воспроизвёлся.
