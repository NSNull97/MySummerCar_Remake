# My Summer Car Remake Game

Private experimental donor-assisted recreation of My Summer Car in Unity 6 HDRP.

## Fixed local paths

- Donor game: `D:\SteamLibrary\steamapps\common\My Summer Car`
- Unity project: `E:\GAYmDev_Studio\MySummerCar_Remake_Game`
- Donor staging: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging`
- Decompiled reference: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference`

## Start

1. Read `START_HERE_RU.md`.
2. Run `Tools/Validate-Environment.ps1`.
3. Run `Tools/Create-Donor-Staging.ps1`.
4. Open the project root in Codex.
5. Execute `Prompts/00_BOOTSTRAP_AND_DONOR_AUDIT.md` only.
6. Review the generated audit before running the next milestone.

## Core rule

The original installation is a read-only donor and behavioral reference. The new Unity runtime must be independent. Production graphics are reauthored for HDRP; donor meshes are normally used only for dimensions, pivots, mount points, blockout, and comparison.

## Important documents

- `AGENTS.md` — mandatory repository instructions for Codex.
- `Docs/PROJECT.md` — product definition and scope.
- `Docs/ARCHITECTURE.md` — target technical architecture.
- `Docs/PORTING_GUIDE.md` — transfer and reconstruction rules.
- `Docs/REVERSE_ENGINEERING.md` — safe inspection workflow.
- `Docs/ROADMAP.md` — milestone order and exit gates.
- `Prompts/README.md` — how to run the task prompts.
