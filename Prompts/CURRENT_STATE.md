# CURRENT PROJECT STATE — USER CONFIRMED

Updated for Prompt Pack v5.

## Active milestone

- Milestone 06 is treated as implemented.
- Milestone 06A is closed and human-accepted.
- Milestone 06B1 is closed and committed.
- **Milestone 06B2 — donor map streaming cellization and active world profile
  has automated status `PASS` and manual status `HumanAccepted`.**
- 06B2 is fixed in commit `79f02b0` (`world: complete milestone 06B2 v5.1`).
- **Milestone 06B3 — runtime baseline validation, debt catalogue, and freeze is
  closed with status `PASS / Frozen / HumanAccepted`.**
- The user completed the physical vehicle route through Fleetari, Teimo,
  inspection/town, the major road loop, railway crossing and a representative
  bridge. The run covered 24 cells, six consecutive boundaries, reset and OOB
  recovery without critical collision, duplicate or missing-section failures.
- The user confirmed Bootstrap startup, donor-map fidelity, a walking route to
  the lake, Teimo-area streaming unload/reload and out-of-bounds recovery.
- The corrected temporary water presentation and original-game terrain voids
  remain explicit late-remaster debt, not production art.

## User-confirmed world state

The user confirms:

- the original donor map has already been extracted;
- the extracted map has already been visually inspected;
- world streaming has already been connected/prototyped;
- two custom production cells exist but do not resemble their original
  locations.

The two inaccurate cells are now treated as:

```text
PrototypeOnly
RejectedForFidelity
InactiveInFeatureParityProfile
NotProductionReady
```

Do not hand-remodel them during the immediate feature-parity phase.

Preserve useful streaming architecture, cell IDs, registries, tests and
project-owned metadata. Disable the inaccurate custom visual roots and use the
exact donor map as the temporary runtime baseline.

## World strategy — fixed

### Feature-parity phase

Use the already extracted original map as a sanitized, deterministic
`TemporaryDirectImport` runtime baseline.

Allowed baseline content may include donor-derived terrain, world geometry,
roads, buildings, props, vegetation, temporary materials/textures and collision.

Forbidden baseline content includes donor scripts, PlayMaker FSMs, runtime
assemblies, old UnityEngine references, player/vehicle/NPC logic, cameras,
lighting, weather, audio, UI, saves, Steam/platform code and gameplay managers.

The baseline may be used in private local feature-parity development builds. It
is not final art and is not allowed in a distributable/public build without
explicit rights.

### Remaster phase

Replace the baseline cell by cell through production override layers while
preserving coordinates, stable IDs, gameplay anchors, saves and streaming.

## Next allowed sequence

Current allowed sequence:

1. keep frozen `DonorWorldBaseline-v001` unchanged except through the recorded
   regeneration policy;
2. manually import/verify Enviro 3 when absent;
3. `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`;
4. `07B_TIME_WEATHER_DOMAIN_AND_ENVIRO3_ADAPTER.md`;
5. `07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`;
6. continue to Milestone 08 only after Milestone 07 is stable.

Do not run the deprecated
`06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md`.

## Fixed design decisions

Read `PROJECT_DESIGN_GUARDRAILS.md` before every future milestone.

Key decisions:

- donor feature parity first;
- exact donor map baseline before production-art replacement;
- real donor references outrank concept art;
- no physical full-body first-person player in current scope;
- lightweight viewmodel hands only for approved actions;
- Enviro 3 is the visual environment backend;
- project-owned systems own game time, weather, wetness, lightning, saves and
  cross-system outputs;
- Enviro vendor source is read-only;
- Enviro audio is not the production audio authority;
- no GPS, RPG inventory, permanent quest tracker or exact hidden simulation
  percentages in the default experience.

## Repository paths

The currently opened repository and local configuration are authoritative.

Historical possible paths include:

- `E:\GAYmDev_Studio\MySummerCar_Remake`
- `E:\GAYmDev_Studio\MySummerCar_Remake_Game`

Expected donor installation may be:

- `D:\SteamLibrary\steamapps\common\My Summer Car`

Read actual paths from project-local configuration such as
`Config/DonorPaths.local.json`. Never move or rename the repository or donor
installation automatically.

## Enviro package status

The user owns Enviro 3. Ownership does not prove that the package is imported or
which exact version/API is installed. Milestone 07A must inspect local package
evidence and stop cleanly when it is absent.
