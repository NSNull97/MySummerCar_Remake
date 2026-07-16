# Material shader mapping — Milestone 06B2 v5.1

Status: deterministic project-owned HDRP compatibility mapping.

Donor `.shader` files are read only for identity/classification. They are never copied, compiled, or referenced by runtime content.

| Source shader identity | Source materials | HDRP mapping | Notes |
|---|---:|---|---|
| BuiltIn/10752 | 6 | Unlit (5), TransparentUnlit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| BuiltIn/10755 | 2 | Unlit (2) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| BuiltIn/7 | 129 | OpaqueLit (128), EmissiveLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Car/LightsEmmissive | 1 | EmissiveLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| FX/SimpleWater4 | 1 | TemporaryWater (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| FX/Water4 | 1 | TemporaryWater (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Legacy Shaders/Bumped Diffuse | 4 | OpaqueLit (4) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Legacy Shaders/Diffuse Detail | 35 | OpaqueLit (35) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Legacy Shaders/Reflective/Bumped Specular | 1 | OpaqueLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Legacy Shaders/Transparent/Diffuse | 1 | TransparentLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Nature/Tree Creator Leaves | 2 | AlphaClipLit (2) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Nature/Tree Soft Occlusion Leaves | 1 | AlphaClipLit (1) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Standard | 99 | OpaqueLit (74), AlphaClipLit (11), TransparentLit (11), EmissiveLit (3) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |
| Standard (Specular setup) | 9 | OpaqueLit (9) | Source tint/UV are retained except for the documented project-owned water RGB override; modern PBR channels use explicit neutral defaults. |

## Mapping policy

- Opaque surfaces -> `HDRP/Lit`.
- Alpha-test/tree-wall/foliage -> `HDRP/Lit`, alpha clipping, reviewed double-sided intent.
- Transparent/glass -> bounded `HDRP/Lit` transparent mode.
- Screen/unlit source families -> `HDRP/Unlit`.
- Emission -> HDRP emissive color/map when source data exists.
- Water -> temporary project-owned transparent HDRP material with RGB `(0.08, 0.22, 0.28)` and preserved/clamped donor `_BaseColor.a`; donor shore foam is not misused as the full-surface base map, and donor water runtime is excluded.
- Built-in material `10302` on two FinishPoles objects -> explicit orange diagnostic fallback.

## Known differences and review disposition

- AssetRipper shader text does not prove original Cull/Tags/blend implementation; double-sided/culling intent is a bounded shader/material-name heuristic. The representative v5.1 visual review was accepted on 2026-07-16 as temporary legacy visual debt, not production parity.
- Donor detail normals are deterministically channel-packed into HDRP Detail Maps; donor detail UV selection/transform and strength are preserved while detail albedo/smoothness influence remains neutral. Specular/metallic legacy maps remain audit-only where semantics are ambiguous.
- Alpha cutout, glass, tree walls, water, emission and UV slot order were included in the manual baseline review. Corrected water is `PASS / HumanAccepted`; remaining legacy artifacts are accepted temporary visual debt and still require production replacement.
