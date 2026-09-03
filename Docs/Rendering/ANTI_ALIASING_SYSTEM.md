# Anti-Aliasing System

## Runtime ownership

`MSC.Presentation.AntiAliasing.Runtime` is the sole runtime owner of post-process anti-aliasing, temporal-upscaler camera flags and the motion-vector camera frame settings required by temporal modes.

The system is created before the first scene, survives additive scene loads and is not tied to a scene name. It scans cameras after a scene load and also configures a camera on its first `beginCameraRendering` callback, so dynamically spawned cameras do not need registration by name.

The controller never mutates an HDRP Asset or Volume Profile at runtime. It captures every managed camera's original state and restores it if the camera is excluded or the controller is removed.

## Presets

| Preset | Native mode | Temporal tuning | Intended use |
| --- | --- | --- | --- |
| Low | FXAA | None | Cheapest fallback |
| Medium | SMAA High | None | Sharp non-temporal alternative |
| High | TAA High + CAS 0.24 | History 0.60, jitter 0.70, anti-flicker 0.05, motion rejection 0.95, anti-ringing | Default gameplay; maximum current-frame bias for deghosting |
| Ultra | TAA High + CAS 0.30 | History 0.64, jitter 0.75, anti-flicker 0.10, motion rejection 1.00, stronger ringing reduction | Native high quality with tightly bounded history |

`MaximumQuality` is separate from Ultra. It uses DLAA quality when HDRP, the NVIDIA module, the active HDRP Asset and the GPU all report DLSS support; otherwise it safely falls back to native TAA. `TemporalUpscaler` uses the explicitly selected DLSS quality and never defaults to Performance. FSR2 and XeSS are not selected because neither is present in the active upscaler priority list.

All four HDRP assets override only `DLSSRenderPresetForDLAA` to serialized value
1 (`Preset_F`). The documentation shipped with Unity's installed NVIDIA module
identifies F as its DLAA stability preset. Quality, Balanced, Performance and
Ultra Performance remain at serialized value 0 (`Default`), which keeps their
normal Preset K path in this module version. The installed `nvngx_dlss.dll` is
version 310.5.3.0.

Sharpening is a separate controller value. Native TAA uses HDRP Contrast Adaptive Sharpening with a capped value of 0.75. DLSS/DLAA forces HDRP TAA sharpening and the deprecated DLSS sharpening value to zero, preventing double sharpening.

The original controller pass hard-coded HDRP's 0.875 history blend and full
1.0 jitter while also using strong anti-flicker. Full-world testing exposed
severe trails in native TAA. The project profiles now own both history blend and
jitter scale: High/Ultra reject unreliable motion history aggressively instead
of accumulating it as a long smear. High now uses HDRP's minimum public history
blend of 0.60 after the first full-world correction reduced but did not remove
the reported trails. HDRP ignores the native `taaJitterScale`
field while DLSS/DLAA owns the temporal resolve, so this correction does not
silently alter NVIDIA's jitter sequence.

## Camera policy

Attach `AntiAliasingCameraPolicy` only when automatic classification is insufficient:

- `Gameplay` and `ViewModel`: selected full-quality mode;
- `Auxiliary`: SMAA High by default, with dynamic resolution and temporal history disabled;
- `UserInterface` and `Excluded`: untouched;
- `Auto`: a render-texture camera is auxiliary, a UI-only culling-mask camera is UI, and another Game camera is gameplay.

Screen Space Overlay UI is not rendered by a camera and therefore receives no jitter. A future dedicated UI camera should use `UserInterface` explicitly if its culling mask is not UI-only.

## Temporal history

The controller detects an instantaneous translation of at least 8 m, a rotation of at least 70 degrees, an FOV jump of at least 5 degrees, and a rendered-resolution change. `NotifyCameraCut` is available to save-load, teleport, cutscene and camera-switch owners that know about a discontinuity before it happens.

HDRP 17.3 has no public per-camera history-clear API. For a discontinuity, the controller renders exactly one frame without temporal AA/upscaling and then restores the requested temporal mode. HDRP observes the public mode transition and invalidates its history. Normal AA/upscaler changes use HDRP's built-in mode-transition invalidation and do not insert a blank frame.

## Motion vectors and local risk decisions

- All four HDRP assets enable motion-vector support and use no MSAA.
- Temporal gameplay cameras explicitly override `MotionVectors`, `ObjectMotionVectors`, `TransparentsWriteMotionVector` and `Antialiasing` frame settings.
- Vehicle assembly and validation renderers serialize Object motion vectors, including wheels, doors and loose/installed parts.
- Ordinary Transform, Rigidbody, skinned NPC and dynamic item renderers use Unity object/skinned motion-vector paths; no global renderer mutation was added.
- `MSC_SpruceWindHDRP.shader` has an HDRP MotionVectors pass using the same current/previous wind deformation contract as its visible passes.
- `MSC_VegetationIndirectHDRP.shader` supplies current and previous clip positions using `_TimeParameters` and `_LastTimeParameters`; the indirect renderer requests Object motion vectors. Its LOD cross-fade mask is seeded from the non-jittered clip position, never jittered fragment `SV_POSITION`, so temporal projection jitter cannot change leaf coverage every frame.
- The simple grass patch renderer is static geometry without vertex wind and intentionally uses `ForceNoMotion`.
- One licensed camera-local viewmodel importer intentionally uses `ForceNoMotion` to avoid translucent multi-finger trails; the separate donor-viewmodel path uses Object vectors. This local decision was preserved rather than globally forcing vectors onto camera-local presentation.
- Static donor-baseline, traffic-light proxy and world-light proxy renderers that deliberately use `ForceNoMotion` were not changed.

No material, mesh, physics, gameplay or weather asset was bulk-modified. The
only content correction is the project-owned indirect vegetation LOD-dither
seed. The semantic audit found 739 materials, 31 alpha-clipped materials, 30
transparent materials, 514 referenced imported textures with mipmaps and one
non-mip texture. No alpha-clipped material referenced the non-mip texture, so
there was no evidence for a local importer change. Anisotropic filtering is
forced on by the active quality configuration.

All four HDRP assets keep dynamic resolution and DLSS enabled but set global
`Use Mip Bias` off. The newly enabled negative bias selected sharper texture,
normal and mask mips at DLSS input resolution and amplified subpixel shimmer on
both foliage and ordinary surfaces such as doors. DLSS optimal resolution and
quality selection remain enabled; this is not a disguised native-resolution
fallback.

## Configuration and developer controls

- Tuning asset: `Assets/Game/Presentation/AntiAliasing/Resources/AntiAliasing/AntiAliasingSettings.asset`
- Runtime API: `AntiAliasingController.Instance.SetPreset`, `SetMode`, `SetSharpening`, `SetExternalDlssRequest`, `NotifyCameraCut` and `ResetAllTemporalHistory`
- Camera override: `AntiAliasingCameraPolicy`
- Editor menu: `MSC > Rendering > Anti-Aliasing > Run Project Audit`
- Development/Editor overlay: click `AA debug`; it shows requested/effective mode, internal/output resolution, dynamic-resolution scale, upscaler and quality, motion vectors, sharpening, preset and managed-camera count.

Player-facing controls are under `Settings > Graphics`: AA mode, Low/Medium/High/
Ultra preset, sharpening and (on supported hardware) DLSS quality. Apply stores
them in UI settings schema v6 and delegates runtime application to the central
controller. Mode or sharpening edits become `Custom`; selecting a preset restores
its authored mode and sharpening atomically. Unsupported hardware cannot select
DLSS or Maximum Quality from the menu.

The controller's direct development API retains versioned PlayerPrefs support
for callers that explicitly request persistence. The in-game settings adapter
calls it with persistence disabled, so the versioned JSON document remains the
single authority for ordinary player settings.

The focused second-correction validation passed 13/13 EditMode tests (AA settings,
HDRP mip-bias/DLAA-preset guards and vegetation temporal contract) and 8/8
graphics-enabled PlayMode controller tests on an RTX 4070 SUPER. Full-world
visual acceptance at native TAA, DLSS Quality/Balanced and DLAA
still requires a human moving-camera pass; a synthetic single-frame capture is
not evidence that every material and LOD transition is temporally stable.

## Validation artifacts

- `Reports/AntiAliasing/ProjectAudit.json`
- `Reports/AntiAliasing/ProjectAudit.md`
- `Reports/AntiAliasing/PerformanceMatrix.json`
- `Reports/AntiAliasing/PerformanceMatrix.csv`
- `Reports/AntiAliasing/VisualCaptures/`

The performance fixture creates a deterministic HDRP scene at 1280×720, disables Motion Blur and Depth of Field only inside that fixture, and samples all six requested modes across the fourteen required motion/camera conditions. Each record uses 12 warm-up and 24 measured frames. Project Volume assets are not altered.

`DynamicResolutionHandler` is sampled at `endCameraRendering` and cached per camera. This is required for truthful multi-camera diagnostics: reading the global handler later can report the last auxiliary camera instead of the requested gameplay camera. In the measured single-gameplay-camera DLSS Balanced path the real internal resolution is 742×418 for a 1280×720 output (scale 0.58); DLAA remains 1280×720.

GPU frame timing is represented as unavailable (`-1`, with an explicit source string) when Unity exposes no valid sample. On the validation machine, `FrameTimingManager`, the `GPU Frame Time` profiler counter and a profiler-enabled Editor run all returned no GPU sample. A standalone test-player attempt was stopped before launch by the existing mandatory Phase 1 character-presentation validator (`suski` fixture), and the installed PresentMon executable could not start an ETW capture without privileges. CPU frame time, post-processing and motion-vector markers remain measured rather than estimated.

When a non-dynamic auxiliary camera is active beside DLSS in HDRP 17.3, the final per-camera telemetry reports a native 1.0 scale for the gameplay camera in the synthetic fixture. The gameplay camera remains DLSS-enabled and the auxiliary camera remains SMAA/non-temporal, but this HDRP multi-camera scaler behavior removes the upscaling performance benefit for that frame. The default High/native-TAA path is unaffected. Treat this as a known engine-level limitation when enabling persistent mirror cameras.
