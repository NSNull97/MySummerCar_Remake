# World Material Standard

Production shader baseline: `HDRP/Lit`; comparison proxy может использовать `HDRP/Unlit`. Materials живут в `Assets/Game/World/Production/Materials`.

Обязательные данные:

- base color без baked lighting;
- tangent-space normal;
- HDRP mask map: metallic, AO, detail mask, smoothness;
- SI-compatible texture scale/tiling;
- global/material-property wetness readiness;
- документированный transparency/alpha clipping только там, где нужно.

Pilot содержит `14` WR material assets. Они используют project-authored M3 procedural texture basis, а не donor textures. Это допустимый production-candidate reuse; уникальные финальные scans/decals остаются manual-art задачами.

Валидатор запрещает зависимости production prefabs/materials от `LegacyImport/ReferenceOnly` и `Imported/DonorGenerated` и требует HDRP shader family.
