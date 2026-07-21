# Material shader mapping — 08A.1 candidate v002 remediation

Status: deterministic project-owned HDRP compatibility mapping.
Compatibility policy: `08A1-temporary-hdrp-compatibility-v3`.

Donor `.shader` files are read only for identity/classification. They are never copied, compiled, or referenced by runtime content.

| Source shader identity | Source materials | HDRP mapping | Notes |
|---|---:|---|---|
| BuiltIn/10752 | 6 | Unlit (5), TransparentUnlit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| BuiltIn/10755 | 2 | Unlit (2) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| BuiltIn/7 | 129 | OpaqueLit (128), EmissiveLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Car/LightsEmmissive | 1 | EmissiveLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| FX/SimpleWater4 | 1 | TemporaryWater (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| FX/Water4 | 1 | TemporaryWater (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Legacy Shaders/Bumped Diffuse | 4 | OpaqueLit (4) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Legacy Shaders/Diffuse Detail | 35 | OpaqueLit (35) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Legacy Shaders/Reflective/Bumped Specular | 1 | OpaqueLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Legacy Shaders/Transparent/Diffuse | 1 | TransparentLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Nature/Tree Creator Leaves | 2 | AlphaClipLit (2) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Nature/Tree Soft Occlusion Leaves | 1 | AlphaClipLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Standard | 99 | OpaqueLit (74), AlphaClipLit (11), TransparentLit (11), EmissiveLit (3) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |
| Standard (Specular setup) | 9 | OpaqueLit (9) | Source tint/UV are retained except for the documented project-owned water RGB override; temporary PBR channels follow the bounded shader-aware compatibility policy. |

## Mapping policy

- Opaque surfaces -> `HDRP/Lit`.
- Alpha-test/tree-wall/foliage -> `HDRP/Lit`, alpha clipping, reviewed double-sided intent.
- The temporary Phase 1 world baseline is dielectric: donor `_Metallic` is not inherited until a material identity is explicitly reviewed as metal.
- Smoothness is conservatively capped by source semantics: detail ground and disabled emissive `0`, foliage `0.04`, legacy diffuse `0.08`, generic `0.12`, water/transparent/specular/Standard `0.18`.
- Transparent/glass -> bounded non-metallic `HDRP/Lit` transparent mode.
- Screen/unlit source families -> `HDRP/Unlit`.
- Static donor emission is disabled. Stateful lights/screens must later be enabled by project-owned gameplay presenters instead of frozen donor material variants.
- Frozen road/ground `Legacy Shaders/Diffuse Detail` GUIDs -> deterministic HDRP Detail Map packing (`R=luminance`, neutral normal/smoothness channels), original detail UV transform, and SSR disabled.
- HDRP double-sided mode uses Flip normals (`0`, constants `-1,-1,-1`) for reviewed foliage/tree-wall sources and None (`2`, constants `1,1,1`) otherwise; every generated material is finalized through `HDMaterial.ValidateMaterial`.
- Water -> temporary project-owned transparent HDRP material with RGB `(0.08, 0.22, 0.28)` and preserved/clamped donor `_BaseColor.a`; donor shore foam is not misused as the full-surface base map, and donor water runtime is excluded.
- Built-in material `10302` on two FinishPoles objects -> explicit orange diagnostic fallback.

## Known differences and review disposition

- AssetRipper shader text does not prove original Cull/Tags/blend implementation; double-sided/culling intent is a bounded shader/material-name heuristic. The representative v5.1 visual review was accepted on 2026-07-16 as temporary legacy visual debt, not production parity.
- Donor detail normals and allowlisted legacy road/ground detail albedo are deterministically channel-packed into separate HDRP Detail Map variants. Specular/metallic legacy maps remain audit-only where semantics are ambiguous.
- Alpha cutout, glass, tree walls, water, emission and UV slot order were included in the manual baseline review. Corrected water is `PASS / HumanAccepted`; remaining legacy artifacts are accepted temporary visual debt and still require production replacement.
