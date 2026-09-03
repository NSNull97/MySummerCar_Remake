# Anti-Aliasing Validation Summary

Validation date: 2026-08-17  
Unity: 6000.3.11f1 / HDRP 17.3.0  
GPU/API: NVIDIA GeForce RTX 4070 SUPER / Direct3D 11

## Automated results

- Semantic project audit: 16 camera definitions, 4 HDRP assets, Global Settings, 11 relevant Volume Profiles, 739 materials and 4 custom shaders; all four assets report mip bias off and DLAA Preset F (1), with zero audit errors and zero warnings.
- EditMode: 13 focused tests passed, covering AA tuning, every HDRP asset's mip-bias/DLAA-preset contract and vegetation temporal behavior.
- Controller PlayMode: 8 focused tests passed on the graphics-enabled RTX 4070 SUPER path, including runtime switching, UI/auxiliary policy, DLSS/fallback, DLAA quality/preset and automatic teleport/FOV/cut/resolution history reset.
- Performance PlayMode: 1 fixture passed; 6 modes × 14 scenarios = 84 records, each with 12 warm-up and 24 measured frames.
- Visual captures: 8 SMAA/TAA frames generated and inspected for foliage, thin geometry, night/emissive motion and contrast detail.
- Final Unity batch compilation completed with exit code 0 and no C# compiler errors or warnings.

## Performance summary

| Mode | Ordinary-scenario CPU (ms) | All-scenario CPU (ms) | Ordinary post CPU marker (ms) | MV CPU marker (ms) |
| --- | ---: | ---: | ---: | ---: |
| Off | 2.00 | 2.03 | 0.09 | 0.05 |
| FXAA | 1.76 | 1.83 | 0.07 | 0.05 |
| SMAA | 1.76 | 1.86 | 0.08 | 0.05 |
| TAA | 2.06 | 2.11 | 0.10 | 0.05 |
| DLSS Balanced | 2.17 | 4.08 | 0.17 | 0.05 |
| DLAA | 2.07 | 3.88 | 0.16 | 0.05 |

DLSS Balanced measured 742×418 internal to 1280×720 output (0.58 scale). DLAA measured native 1280×720. Transition averages include deliberate temporal-history invalidation; the largest was 9.41 ms CPU for DLSS during a resolution change.

## Unavailable/blocked evidence

- `FrameTimingManager` and `ProfilerRecorder("GPU Frame Time")` returned no valid GPU samples in Editor batch mode, including a profiler-enabled retry. Records use `-1` and an explicit unavailable source.
- The installed NVIDIA PresentMon process exited with code 1 before ETW capture because the current non-elevated environment could not start the trace.
- A standalone test-player build was attempted with the required private donor-baseline acknowledgement. It was stopped by the existing mandatory Phase 1 character validation: generated fixture `suski` is missing or retained an Animator. AA runtime execution was not reached.
- The first full-world correction made native-TAA trails smaller and restored ordinary DLSS stability, but TAA remained visibly ghosted and DLAA still jittered. The second correction uses HDRP's 0.60 history floor for High TAA and NVIDIA Preset F only for DLAA.
- Full playable-world acceptance of that second correction remains manual; the automated fixture cannot prove every donor-derived renderer, particle, pool transition or LOD transition.
