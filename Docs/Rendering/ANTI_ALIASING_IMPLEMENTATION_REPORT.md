# Anti-Aliasing Implementation Report

## Scope and detected conflicts

Audited Unity 6000.3.11f1 with HDRP 17.3.0, the active High Fidelity quality asset, the project-default HDRP asset and Global Settings, three quality levels, all camera-bearing project scenes/prefabs, Volume Profiles, project materials, project shaders, the UI graphics schema and runtime DLSS adapter.

The concrete conflict was fragmented ownership: cameras serialized AA as None, several cameras had no HD additional data, DLSS flags defaulted to allowed on HDRP cameras while dynamic resolution was disabled, and the UI DLSS adapter independently rewrote every Game camera. No camera supplied a project-wide native AA default, history reset or auxiliary/UI policy.

Audit facts:

- current batch quality: High Fidelity, `Assets/Settings/HDRP High Fidelity.asset`;
- project default: `Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset`;
- Global Settings: `Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset`;
- all four HDRP assets: motion vectors on, MSAA None, dynamic-resolution framework enabled at a native 100–100% range, negative mip bias off, DLSS listed as the only advanced upscaler, DLAA-only Preset F selected and ordinary DLSS presets left at Default;
- current quality assets use Catmull-Rom for ordinary dynamic scaling; the project-default asset uses TAAU; neither FSR2 nor XeSS is configured;
- 16 camera definitions were found; 7 have no serialized HD additional data and are now handled non-destructively at runtime;
- 11 relevant Volume Profiles plus the HDRP default profile were inspected. Project gameplay/weather profiles do not add Motion Blur or Depth of Field. The HDRP default profile contains Motion Blur intensity 0.5 and Depth of Field mode Off, so AA comparisons use a local neutral test Volume rather than changing this shared asset;
- 739 materials: 31 alpha clip, 30 transparent, zero alpha-clip/non-mip conflicts, anisotropic filtering forced on;
- the legacy StandardSpecular shader has no motion-vector pass, but no evidence justified wiring it into dynamic production presentation; the two active custom vegetation shaders have explicit temporal support.

## Implemented result

High is the default because native TAA High is the only available project-native option that stabilizes subpixel foliage, wires, fences and distant geometry across frames. Its CAS sharpening remains conservative, while history is deliberately current-frame-biased (0.60 blend, 0.70 jitter, 0.05 anti-flicker and 0.95 motion-vector rejection) so moving detail is rejected instead of retained as a trail. Ultra uses 0.64/0.75/0.10/1.00 respectively. SMAA remains the sharp non-temporal alternative; FXAA is only the cheap fallback.

The central controller configures all Game cameras, catches runtime-created cameras, excludes UI, gives render-texture/auxiliary cameras a cheap SMAA profile, removes conflicting DLSS/FSR2/dynamic-resolution state during native modes, and preserves the camera's original state for reversal. The accepted 08A graphics route was minimally extended in its existing settings panel: AA mode, Low/Medium/High/Ultra preset and sharpening are now live controls, while the existing supported-only DLSS quality row delegates to this controller. No separate settings screen or alternate presentation framework was introduced.

Temporal history is reset for teleport, hard cut, abrupt FOV change and resolution change. DLSS/DLAA activation is hardware- and pipeline-gated. Unsupported requests produce one warning and fall back to native TAA (or SMAA when motion vectors are unavailable).

Full-world user testing after the initial implementation exposed two real
regressions that the synthetic fixture did not reveal: severe native-TAA
ghosting and DLSS/DLAA shimmer on foliage, doors and other fine surfaces. The
correction removes the controller's hard-coded 0.875 history blend, lowers
anti-flicker/history sharpening, raises motion-vector rejection, disables the
new global negative dynamic-resolution mip bias in every HDRP asset and makes
the indirect vegetation LOD mask independent of projection jitter. DLSS optimal
scaling, DLAA native input resolution and motion vectors remain enabled.

The first full-world retest confirmed that ordinary DLSS no longer had the
reported instability after the mip-bias/LOD correction. Native TAA ghosting was
smaller but still strong, while DLAA still jittered on foliage, doors and other
fine surfaces. The second correction therefore moves High to HDRP's minimum
public TAA history blend (0.60), lowers its jitter and anti-flicker further, and
raises motion-vector rejection to 0.95. DLAA alone now uses NVIDIA
`Preset_F` (serialized value 1), which the documentation installed with the
Unity NVIDIA module identifies as its DLAA stability preset. The installed
`nvngx_dlss.dll` is 310.5.3.0. Ordinary DLSS Quality/Balanced/Performance modes
remain at Default (Preset K in this module version), matching the mode the user
already found stable.

## Performance and visual verification

The generated performance matrix contains six requested modes × fourteen scenarios, with 12 warm-up and 24 measured frames per record at 1280×720. The validation machine used Unity 6000.3.11f1, Direct3D 11 and an NVIDIA GeForce RTX 4070 SUPER. These are synthetic Editor PlayMode measurements from the initial implementation, not a full-world hardware benchmark or proof of post-correction visual acceptance. The correction adds no render pass and does not change DLSS/DLAA input-resolution policy.

| Mode | CPU, ordinary scenarios (ms) | CPU, all 14 (ms) | Post CPU marker, ordinary (ms) | MV CPU marker (ms) | Internal resolution |
| --- | ---: | ---: | ---: | ---: | --- |
| Off | 2.00 | 2.03 | 0.09 | 0.05 | 1280×720 |
| FXAA | 1.76 | 1.83 | 0.07 | 0.05 | 1280×720 |
| SMAA High | 1.76 | 1.86 | 0.08 | 0.05 | 1280×720 |
| TAA High | 2.06 | 2.11 | 0.10 | 0.05 | 1280×720 |
| DLSS Balanced | 2.17 | 4.08 | 0.17 | 0.05 | 742×418 → 1280×720 |
| DLAA / Maximum Quality | 2.07 | 3.88 | 0.16 | 0.05 | 1280×720 |

“Ordinary scenarios” are the first eight scene/motion cases. The all-scenario DLSS/DLAA average intentionally includes FOV, resolution, mode, teleport and hard-cut history discontinuities. The most expensive measured average was the DLSS resolution-change case at 9.41 ms CPU, including 7.02 ms in the post-processing marker. Native TAA remained 2.09 ms for the same case. The cost is accepted for a correct history reset and is not hidden by Motion Blur or Depth of Field.

Unity supplied no valid GPU frame-time samples in Editor batch mode, including a profiler-enabled retry. The installed PresentMon capture required unavailable ETW privileges, while the standalone test player was blocked before launch by the unrelated mandatory `suski` Phase 1 character fixture validator. The JSON/CSV therefore records GPU timing as unavailable rather than fabricating a value. A standalone GPU capture remains required after that pre-existing build blocker is fixed.

Final measured summaries are recorded in `Reports/AntiAliasing/PerformanceMatrix.json` and `.csv`; representative SMAA/TAA captures are in `Reports/AntiAliasing/VisualCaptures`.

The fourteen conditions are static outdoor, slow walk, fast camera turn, fast vehicle motion, forest/grass/foliage, thin geometry against sky, night/emissive motion, interior contrast detail, FOV change, resolution change, runtime AA switch, teleport/save-load discontinuity, hard camera cut and multiple active cameras.

The eight captured motion frames cover foliage, thin geometry, moving emissive content and contrast detail in both SMAA and TAA. Inspection found no gross double contour, halo or persistent emissive trail in that generated fixture, but subsequent full-world testing correctly disproved any broader inference from it. A single captured frame cannot prove temporal stability. Final subjective acceptance still requires a human pass in the complete playable world because automated frame timings cannot prove that every donor-derived wheel, hand animation, pooled object, LOD transition, particle or emissive material is ghost-free.

The multiple-camera fixture keeps the auxiliary camera on SMAA and the gameplay camera temporal. Native TAA stays stable; HDRP 17.3 reports DLSS at native scale 1.0 while the non-DRS auxiliary camera is active, so persistent mirrors can temporarily remove DLSS's scaling benefit without stacking AA modes.

## Compatibility

No gameplay logic, physics, map, streaming, weather, lighting, world-save DTO,
stable ID, vehicle assembly API or prefab identity was changed. The existing
Graphics route and shared Apply/Reset/Cancel transaction were reused; the left
panel grew by 14 px to fit the additional AA preset row without overlapping the
accepted action bar. The runtime integration remains behind the existing HDRP
adapter while camera mutation ownership stays in the central controller.

UI settings schema v6 adds mode, preset and sharpening. Migration is automatic:
v5 DLSS maps to `TemporalUpscaler / Custom`, v5 DLAA maps to
`MaximumQuality / Custom`, and a native legacy configuration maps to High/TAA.
The compatibility `DlssEnabled` field remains present and synchronized.

The focused UI settings EditMode run passed 25/25 tests. The new AA Graphics
PlayMode path passed runtime application and JSON persistence, and the existing
capability-row test passed. The broader UI class run passed 22/23; its sole
failure is a reproducible pre-existing Controls-route viewport assertion for
`BindingRow22` (1.72 px below the viewport), outside the Graphics/AA route.

The focused second-correction rerun passed 13/13 EditMode and 8/8 PlayMode tests.
The PlayMode run used Direct3D 11 on an NVIDIA GeForce RTX 4070 SUPER and covered
the supported DLAA path (quality value 4), its Preset F pipeline contract and
the unchanged ordinary-DLSS preset. The project audit found 16 cameras, 739
materials and zero warnings. Unity imported the corrected vegetation shader
without a shader compiler error in the graphics-enabled PlayMode run. Manual
full-world comparison remains required because these tests validate state,
contracts and compilation rather than subjective motion quality.
