# N2Studio Fire001 provenance

Status: user-provided third-party presentation asset, private project use.

Source package: `Fire001.unitypackage`, supplied locally by the user from
itch.io on 2026-08-02. The machine-local download path is setup evidence only
and is not referenced by runtime code.

Package SHA-256:
`1BF7B7B503BF2DD5B648C306E21230FBDC41D13610961F0A75D65EA30D8A1063`.

Selective import:

- `Fire001.prefab` — six original ParticleSystems; Nova material references
  removed by the deterministic import tool so the prefab cannot render pink in
  HDRP. SHA-256
  `0AB262C80C968A6E2A3A34D2641C661CCB8862606BBB5C4D2932A4D92793C246`.
- `FireSeq1.png` — SHA-256
  `1C48AF2DFAAEF2D2B6A62D62251E6F4BC3F94D349203905254150B57CCDB82D3`.
- `Smoke1.png` — SHA-256
  `A7A3374041DD18968E3E16E6D4BE28D15F88FE51E60BB567B6AEBBB0A26ECD00`.
- `PointGlow.png` — SHA-256
  `BBE84E541DB75804356AD1B3BD63EB3FA4ED2055C4568C0AB3C99E6CB8CD2338`.
- package `Third-Party Notices.txt` retained verbatim — SHA-256
  `EC6B7CD9333D12BA0797029294C01923C665ED8E768DEC7EF08635D7F7FFDC33`.

The package identifies its primary asset under the Asset Store EULA and its
bundled Nova Shader under the MIT License. The Nova shader, renderer features,
runtime scripts, editor scripts, demo scene and source materials are not
imported because they target a different render-pipeline integration. Project
runtime code creates HDRP/Unlit materials for the selected textures and binds
them to the original particle preset. Gameplay ignition and item consumption
remain project-owned and independent of this presentation payload.

The source preset's five burst-driven layers emit for approximately 4.6-5
seconds inside a seven-second loop, which creates a repeated extinguished gap.
The project presenter intentionally clears those finite bursts and assigns
bounded continuous per-layer emission while the authoritative barrel state is
lit. It also scales and positions the general-purpose preset for the barrel;
the selectively imported source prefab remains unchanged.

Reimport with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Presentation/ImportFire001Preset.ps1 -PackagePath <path-to-Fire001.unitypackage>
```
