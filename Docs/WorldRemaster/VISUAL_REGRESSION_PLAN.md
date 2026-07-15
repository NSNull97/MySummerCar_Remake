# Visual Regression Plan — World Remaster

## Repeatable baseline

Use `Assets/Game/World/Debug/Comparison/WR_HomeYardComparison.unity` and the menu `Tools > MSC Remake > World Remaster` to switch `ReferenceOnly`, `ProductionOnly` and `OverlayComparison`. Keep the comparison camera transform, FOV, 1920 × 1080 resolution and HDRP quality fixed.

Local generated captures belong in `PerformanceCaptures/Milestone05A/` and are excluded from Git. Record Unity version, registry version, cell hash, mode, camera and lighting preset in the capture manifest.

## Required pilot matrix

For home/garage, interior and road approach capture:

- donor/reference metadata view;
- production view;
- overlay view;
- clear midday, golden evening, overcast, rain preview, dusk/night;
- interior, garage-door and representative vehicle-clearance views.

The available reference layer is metadata/bounds comparison, not a visual donor-mesh render. A missing visual donor capture must remain explicitly marked missing.

## Acceptance

Compare anchor, bounds, footprint, pivots, door arcs, terrain contact, road approach, collision envelope and material readability. Any systematic scale/pivot/alignment defect stops batch replacement. Image review is manual; automated capture existence alone is not visual approval.

## 05A baseline captured

`ReferenceOnly.png`, `ProductionOnly.png`, `OverlayComparison.png` and `capture_manifest.csv` were generated at 1920 × 1080 under `PerformanceCaptures/Milestone05A/`. Review confirms that all three modes are distinct and the overlay proxies render over the production yard. The available reference image is sparse metadata geometry, not donor visual fidelity. The neutral production capture is very dark and the tree assets are obvious first-pass capsules; both observations remain acceptance blockers for art approval rather than being hidden.

## Batch 01 baseline captured

Для `cell_0_-2 / HomeShorelinePier` созданы три фиксированные камеры (`Pier`, `Hedge`, `Seam`) и по три режима, всего девять PNG 1920 × 1080 под ignored-путём `PerformanceCaptures/Milestone05A/Batch01_cell_0_-2/`. Первый capture выявил воду поверх hedge boundary; после исправления повторный capture подтвердил отсутствие overlap. Пользовательский Scene View review 2026-07-15 дополнительно выявил тёмный геометрический разрыв `SEAM-001` между home terrain и подходом к пирсу; для него добавлена отдельная камера, а повторный ручной проход после исправления завершён с PASS. Proxy layer показывает точные stable-position anchors, но не donor silhouette/материалы, поэтому кадры подтверждают структуру и не дают visual approval без отдельного shoreline capture оригинальной игры.
