# World Remaster Performance Report

Scope: static Editor audit of `WR_HomeYardPilot.prefab`; not a GPU or standalone-player capture.

| Metric | Value |
|---|---:|
| Renderers | 332 |
| Colliders | 134 |
| LOD groups | 64 |
| Mesh triangles (instance-counted) | 158548 |
| Unique mesh memory estimate | 0,18 MiB |

Target remains 60 FPS at 1920×1080 on a mid-range Windows PC. Actual CPU/GPU time, draw calls, VRAM and frame pacing are **not measured yet**; capture is a manual gate after visual acceptance.
