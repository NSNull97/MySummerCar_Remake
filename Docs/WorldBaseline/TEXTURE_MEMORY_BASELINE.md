# Texture memory baseline — Milestone 06B2 v5.1

Scope: deterministic imported-asset baseline in Unity Editor. Resident/streamed counts during traversal are captured by the PlayMode performance evidence.

| Metric | Value |
|---|---:|
| Unique source images | 265 |
| Role-specific conversion records | 269 |
| Imported role variants | 268 |
| Declared material-slot references | 2744 |
| Shared generated materials (fallback included) | 293 |
| Material copies avoided by sharing | 2451 |
| Imported texture-property references | 384 |
| Texture copies avoided by source+role sharing | 116 |
| Excluded cubemap variants | 1 |
| Unique source PNG bytes | 301818115 |
| Generated encoded bytes (role splits included) | 304277161 |
| Unity imported texture runtime-size estimate | 682544163 |
| Unity generated material runtime-size estimate | 2147112 |

## Import contract

- Source dimensions are preserved (maximum source size 8192).
- World-space variants use mipmaps and texture streaming.
- Read/Write is disabled.
- Color variants use sRGB; normal/linear/packed-detail variants do not.
- Donor detail normals are channel-packed as `R=0.5, G=Y, B=0.5, A=X` for HDRP Detail Map semantics; source dimensions are unchanged.
- Standalone import uses Unity `CompressedHQ`; signage/alpha remains a manual readability check.
- Source wrap/filter/aniso intent is carried into the project importer.

## Limitations

- `Profiler.GetRuntimeMemorySizeLong` in the Editor is an estimate, not a standalone GPU residency measurement.
- Texture streaming residency and plateau after repeated travel must be read from `M06B2_STREAMING_PERFORMANCE.json`.
- No source payload hash duplicates exist among the 265 images; deduplication savings are `2744 -> 293` shared materials (`2451` copies avoided) and `384 -> 268` source+role texture variants (`116` copies avoided).
