# Water and Shoreline Report — 05A pilot

## Result

The selected zone has a representative drainage ditch rather than a verified major shoreline. A small project-authored ditch-water mesh and HDRP-compatible material exercise the water category, road adjacency and cell ownership.

Direct `Water` registry coverage is `0 / 12`. Water level, shoreline shape, foam, refraction, underwater behavior, rain response and cross-cell seams are not claimed as transferred or production-ready.

The pilot water is `FirstPass` presentation. Future zone work must bind reviewed water bodies to stable layout fixtures and choose a scalable HDRP water solution without a ray-tracing baseline.

## Batch 01 — HomeShorelinePier

`cell_0_-2` теперь содержит bounded project-authored lake surface и bottom presentation, привязанные к stable IDs `b412961b75cb019e74a83b24faac32a4` и `f700b12cf5c75a3906dd079acea3f274`. Вода не имеет blocking collider и не использует donor mesh/texture. Capture review выявил и устранил overlap воды с дворовыми изгородями; ручная проверка пользователя 2026-07-15 подтвердила, что water surface не блокирует игрока и допускает падение в воду. Статус остаётся `ProductionCandidate`: shoreline shape, волны, foam, underwater, плавание, weather response и cross-cell water simulation требуют отдельной реализации и visual reference.
