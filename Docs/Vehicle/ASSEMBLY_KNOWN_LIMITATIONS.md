# Известные ограничения сборки M05

- Prototype использует 15 clean graybox/proof деталей; это не production vehicle art.
- Только rear-left brake drum имеет завершённый donor-calibrated behavioral fixture. Остальные массы, крепежи, tolerances и порядок — project-authored prototype configuration.
- Drum position tolerance `0.42 m` и orientation tolerance `35°` — usability tuning. Donor доказал overlap marker `0.01 m`, но не отдельный angular compare.
- Fasteners дискретны. Нет torque, физической резьбы, stripping, wear, damaged threads, анимации руки, IK и звука.
- Prototype tool target содержит назначенный authored tool и автоматически меняет tighten/loosen на endpoints. Inventory, equipped-tool state и ручной выбор направления отсутствуют.
- Connection-point query — placeholder. Fluids, hoses, wiring terminals и electrical topology не реализованы.
- Save schema v1 поддерживает capture/restore DTO, но не disk storage, slots, autosave, migration from future versions или original-save import.
- Нет полноценного vehicle body collision authoring, self-collision matrix и production mount access volumes.
- Candidate scan линейный по 14 mounts. Он allocation-free и достаточен для prototype; spatial partitioning требуется только после профилирования большого автомобиля.
- Assembly completeness считает обязательный fastener полностью закреплённым только на максимальной стадии; частичная затяжка остаётся unsecured.
- Fitted-wheel identity, curb/assembled mass и большинство 12–20 donor part measurements остаются `Partial`/`Missing` в reference dataset.
- Ручная Game View проверка должна подтвердить удобство E/R interaction, preview visibility и отсутствие нежелательного collision jitter на целевом ПК.
