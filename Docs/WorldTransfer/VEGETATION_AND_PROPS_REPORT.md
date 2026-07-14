# Отчёт vegetation and static props

Reference database содержит:

- `VegetationTree`: 16;
- `VegetationBush`: 7;
- `VegetationGrass`: 3;
- `Rock`: 3;
- `StaticProp`: 265;
- `InteractivePropCandidate`: 395.

В 04A1 vegetation хранится как data records с stable ID, transform, source mesh, cell и provenance. Постоянные GameObject на травинку/дерево не создаются. Generated scenes используют дешёвые editor-only bounds/point proxies с общими category materials; это replaceable reference visualization, не production vegetation renderer.

Species normalization, terrain association и density batches не доказаны source data и остаются `NeedsReview`. Перед production vegetation нужны prototype deduplication, GPU-instanced placement batches и ручное сравнение плотности/силуэта.

Static props разделены на static, interactive candidate и collider-only. Динамические `STORE/Boxes`, rally cars/spectators, ragdoll и mini-game visuals исключены из reference layer, но сохраняются в полном entity inventory.
