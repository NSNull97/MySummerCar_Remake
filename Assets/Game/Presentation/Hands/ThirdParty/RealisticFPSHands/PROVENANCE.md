# Realistic FPS Hands v1.0 provenance

Status: `RejectedForFidelity`, superseded by the user-purchased AXIS Neutral
Arms source on 2026-08-12. Kept as provenance/comparison history only; it is no
longer generated runtime presentation authority. The original package licence
continues to apply.

Source package: `Realistic FPS Hands v1.0.unitypackage`, supplied locally by
the user on 2026-08-10. Runtime code contains no machine-specific download
path. Local setup is read from ignored `Config/PresentationAssets.local.json`.

Package size: `510873673` bytes.

Package SHA-256:
`BD4B657AD2C2817C0ED83DECA2EC4A00FA16802E428FF2E79F4AFEB6844F8A5B`.

Selective source import:

| Source | SHA-256 |
| --- | --- |
| `RealisticFPSHands.fbx` | `128B8ED8CBE188BBB995041C00E6DACF4E24383AD17AA7822BDBB1DEAEA8746E` |
| `RealisticFPSHands_Albedo.png` | `9C49E9461DDC7F1650A3AD79BF861EB7D7E49899458C190D202D3BBB497D304A` |
| `RealisticFPSHands_AO.png` | `70140A3509B22ECF21BCFA36DA41EA8488B2C5E68EAE311BD258EFCACA4CD3AC` |
| `RealisticFPSHands_Normal.png` | `0F5C676F03E21067B6426D056E2A31650E8E83B3D14872CF2DEB28DD1E3C69E1` |
| `RealisticFPSHands_SpecularSmoothness.png` | `EE3211234C7E764ABAEF7857362A0FB81856CE5B6C9C0B7AC6D2D463A86306E2` |

The deterministic installer preserves the supplied full two-arm Generic rig
and 4K maps. It excludes the demo scene/prefab, legacy shaders/materials,
bloody variants, accessories and source archive. A project-owned importer
generates the HDRP mask/material, a hand-and-forearm-only right viewmodel mesh
and authored drink, wave and middle-finger clips. Upper-arm/shoulder-dominated
triangles and viewmodel motion vectors are excluded. Vendor scripts,
controllers and animation events are not runtime dependencies.

The rejected first pass attempted to transfer donor elbow, palm, finger and
grip positions onto this different skeleton. Those tracks are no longer used.
Only the donor-evidenced clip durations remain behavioural reference. Arm IK,
screen placement, palm orientation, finger poses and the palm-local beer grip
are project-authored. The arm solver preserves the licensed bone lengths,
distributes roll across the forearm/twist chain, and bakes 30 Hz
quaternion-continuous keys. The sip lifts the bottle base toward the viewer,
the wave moves the full bent-arm chain and the palm-facing middle-finger pose
uses individual joint curls rather than uniform finger arcs. See
`Docs/Items/ORIGINAL_VIEWMODEL_HAND_RETARGET_AUDIT.md`.

Historical generated binaries stay inside the ignored private RuntimeBaseline
and may be deleted without affecting the current AXIS integration. Gameplay
uses the existing project-owned input, item, needs, carry and save systems.
