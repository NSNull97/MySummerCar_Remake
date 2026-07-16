# Baseline visual completeness — Milestone 06B2 v5.1

Automated status: **PASS**.

06B3 gate: **GO / accepted; milestone not started**.

## Automated closure

- Generator: `1.1.0-06B2-v5.1.5`.
- Renderers: 2605.
- Declared source material slots: 2744.
- Resolved donor materials: 292.
- Built-in reviewed fallback renderers: 2.
- Referenced source images: 265 (0 missing).
- Imported role-specific texture variants: 268.
- Packed detail-normal variants: 8; assigned materials: 22 (detail-only: 20).
- Alpha-cutout materials: 14.
- Transparent/water materials: 15.
- Emissive materials: 5.
- Presentation fingerprint: `e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`.
- Material/texture manifest SHA-256:
  `cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192`.

Water-fix closure:

- `TemporaryWater` alpha uses donor `_BaseColor.a`:
  `Water4Adv_Lake = 0.2901961`, `Water4Simple = 0.5058824`.
- Shore-foam texture is no longer used as the full-surface base map.
- Presentation build: PASS,
  `Logs/M06B2V51_PresentationBuild12_WaterFix.log`.
- Full validator: PASS,
  `Logs/M06B2V51_CellizationValidator09_WaterFix.log`.
- Focused EditMode: `7/7 PASS`, `65.3441271 s`,
  `TestResults/M06B2V51_EditMode05_WaterFix.xml`,
  `Logs/M06B2V51_EditMode05_WaterFix.log`.
- Focused PlayMode: `6/6 PASS`, `7.4557305 s`,
  `TestResults/M06B2V51_PlayMode06_WaterFix.xml`.
- Streaming/material performance PlayMode: `1/1 PASS`, `2.5581146 s`,
  `TestResults/M06B2V51_PerformancePlayMode06_WaterFix.xml`.
- Performance capture SHA-256:
  `60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`.

## Explicit exclusions / fallbacks

- One donor night-gradient reflection cubemap is recorded but intentionally excluded; donor sky/reflection/weather ownership is outside this milestone.
- Built-in material `10302` has no exported definition and uses the obvious orange fallback on two FinishPoles renderers.
- Donor lighting, lightmaps, reflection probes, post-processing, weather, audio and shader code are not imported.
- Severe low-quality, stretched and banded terrain texture remains temporary
  legacy presentation debt.
- Proxy/tree-wall geometry and other donor material/texture artifacts remain
  temporary visual debt. Final production textures and materials will be
  reauthored.

## Required neutral-lighting review

| Area / contract | Status |
|---|---|
| Geometry and layout fidelity | Accepted for the temporary baseline |
| Non-water legacy textures/materials | Accepted as documented temporary visual debt |
| Terrain low quality/stretch/banding | Accepted as documented temporary visual debt |
| Proxy/tree-wall and legacy material artifacts | Accepted as documented temporary visual debt |
| Lake / shoreline visibility after water fix | Accepted / HumanAccepted 2026-07-16 |
| Bridge traversal and cross-cell transitions | Accepted / HumanAccepted 2026-07-16; bridges walked and character relocated between cells without observed issues |

The corrected water, temporary visual baseline and bridge/cell-boundary review
are accepted. The manual method used walking and cross-cell character relocation
rather than a dedicated vehicle drive; automated high-speed preload validation
passed. The 06B3 entry gate is **GO**, but 06B3 has not started.
