# World Remaster Performance Report

Scope: static Editor audit of the bounded 05A pilot plus Batch 01 `WR_HomeShorelinePier.prefab`; not a GPU or standalone-player capture.

| Metric | Value |
|---|---:|
| Renderers | 370 |
| Colliders | 164 |
| LOD groups | 67 |
| Mesh triangles (instance-counted) | 159548 |
| Unique mesh memory estimate | 0,18 MiB |

Batch 01 contribution:

| Batch 01 metric | Value |
|---|---:|
| Renderers | 38 |
| Colliders | 30 |
| LOD groups | 3 |

Target remains 60 FPS at 1920×1080 on a mid-range Windows PC. Actual CPU/GPU time, draw calls, VRAM and frame pacing are **not measured yet**; capture is a manual gate after visual acceptance.
