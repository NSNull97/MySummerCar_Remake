# Lighting and Weather Compatibility Report — 05A pilot

## Result

The pilot reuses the project-owned M3 neutral HDRP lighting setup and HDRP/Lit production materials. Materials expose plausible metallic/smoothness response and are marked compatible with future wetness/weather control; donor textures are absent.

Static validation confirms HDRP shaders and finds no production dependency on the reference layer. Clear/overcast/rain/fog/dusk/night are capture-plan conditions, not implemented weather states in this milestone.

## Manual gates

Check direct sun, overcast, rain preview, dusk/night, interior practical light and headlights for readability, glass response, light leaks, wetness response and exposure transitions. These captures must not be interpreted as final lighting or weather validation until reviewed in Unity.

The automated neutral comparison capture was visually inspected on 2026-07-14. Geometry is visible, but the yard/building values are too dark for final readability approval. This is recorded as a manual lighting/art gate; no exposure or grading was changed merely to improve the report screenshot.

## Batch 01

Pier/hedge captures 2026-07-15 повторно используют тот же neutral baseline. Пирс, shoreline и изгороди читаются, но тёмная экспозиция остаётся общим manual gate. Партия не меняет `M3_NeutralVolume.asset`: существующий пользовательский lighting experiment сохранён, а weather/time-of-day работа не была присвоена 05A.
