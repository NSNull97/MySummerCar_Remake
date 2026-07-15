# Donor Audit — Milestone 0 baseline through Milestone 05

Audit dates: 2026-07-13 baseline; 2026-07-14 Milestones 3–04A1 updates

Scope: read-only filesystem, binary-header, log, file-hash, reflection-only managed metadata inspection, and audited use of one previously staged garage measurement

Donor root: `D:\SteamLibrary\steamapps\common\My Summer Car`

## Executive finding

The donor is a Unity `5.0.0f4` Mono game with four serialized `level*` files, `mainData`, five `sharedassets*.assets` files, managed assemblies, and three native plugins. The installed directory is not a clean stock baseline: it already contains MSCLoader/doorstop artifacts, a `Mods` directory, dated diagnostic directories, and a pre-existing `mysummercar_Data\Unity_Assets_Files` extraction tree. This task did not create or modify any donor file.

Core player/data files remain useful as read-only reference candidates, but this installation is not claimed to be clean stock. Milestone 2 selected only two objects and bound them to exact installed container hashes, object PathIDs and staged hashes. Steam verification was not run because it would modify the donor installation.

Milestone 3 did not perform a new extraction. It reused only the audited `garage_shed_roof` measurement/reference from Milestone 2 to preserve roof scale and pivot in a newly authored garage prototype. Donor geometry remains ignored and comparison-only; donor textures, materials, terrain, road data and scene hierarchy were not transferred.

## Path validation

| Path | Observed state | Result |
|---|---|---|
| Donor game | `D:\SteamLibrary\steamapps\common\My Summer Car` exists | Pass |
| Configured Unity project | `E:\GAYmDev_Studio\MySummerCar_Remake` exists | Pass; confirmed as the future-game project |
| Open Unity workspace | `E:\GAYmDev_Studio\MySummerCar_Remake` contains `Assets`, `Packages`, and `ProjectSettings` | Pass; matches local configuration |
| Donor staging | `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging` exists with `raw`, `normalized`, `manifests`, `logs`, and `captures` | Pass; write probe succeeded and was removed |
| Legacy reference | `E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference` exists | Pass; write probe succeeded and was removed |
| Configured reference media | `E:\GAYmDev_Studio\MySummerCar_Remake\References` exists | Pass |
| Open-workspace reference media | `E:\GAYmDev_Studio\MySummerCar_Remake\References` exists | Pass; write probe succeeded and was removed |

Resolved-path comparisons show that the Unity project is not inside the donor installation, and staging is not inside the donor installation. The user confirmed `E:\GAYmDev_Studio\MySummerCar_Remake` as the future-game project. Ignored `Config/DonorPaths.local.json` now uses that project and its `References` directory, and records the discovered Unity Editor executable.

## Install identity and Unity version

- Steam App ID: `516750`.
- Installed Steam build ID: `20171487`.
- Steam manifest reported stock depot size: `922,581,588` bytes.
- `mysummercar.exe` file/product version: `5.0.0.6002871`.
- `output_log.txt` line 1 reports `Initialize engine version: 5.0.0f4 (5b98b70ebeb9)`.
- The headers of `mainData`, `resources.assets`, and `level0` through `level3` independently contain `5.0.0f4`.
- No separate `UnityPlayer.dll` is present; this older player layout uses `mysummercar.exe` plus `mysummercar_Data`.

## Filesystem layout

The complete install inventory contains 2,737 files totaling 2,469,864,183 bytes. The difference from the Steam depot size is consistent with the observed derived extraction/mod content; it is not evidence that every extra file came from one tool.

| Audit category | Files | Bytes | Interpretation |
|---|---:|---:|---|
| Unity serialized data | 16 | 1,522,869,540 | Core `mainData`, levels, assets, and resource streams |
| Unity player executable | 1 | 18,653,984 | Inventory/hash only; never a remake runtime dependency |
| Managed assemblies | 31 | 12,288,478 | Mixed donor/runtime and likely mod-loader dependencies |
| Native plugins | 3 | 451,720 | `CSteamworks`, Logitech wheel, force feedback |
| Legacy Unity runtime | 11 | 3,119,895 | Mono runtime/configuration; never reference from the new project |
| Pre-existing derived extraction | 2,631 | 898,709,876 | Not created by this task and not authoritative |
| Mod/diagnostic and mod-loader artifacts | 20 | 2,967,845 | Exclude from base-game inference |
| Platform integration binaries | 2 | 462,400 | Inventory only; never port Steam/licensing code |
| User-customizable media/reference | 14 | 2,913,462 | May contain user-provided content |
| Other/configuration | 8 | 7,426,983 | Logs, helper executable, and uncategorized files |

Observed root-level non-core indicators include `doorstop_config.ini`, `winhttp.dll`, `MSCLoader_Preloader.txt`, `Mods`, `MOP_Logs`, and dated folders. `Resource_Importer.exe` and `mysummercar_Data\Unity_Assets_Files` were already present before this audit. They were neither executed nor changed.

## Serialized files, scenes, and resources

| File | Size bytes | SHA-256 or policy |
|---|---:|---|
| `mysummercar_Data\mainData` | 241,040 | `bdeb2298a71b45bcce81d1d91e6f3fde5c8954b5b76f88535de8fe17bdd66931` |
| `mysummercar_Data\level0` | 514,820 | `0da8800d7ef65dd9a368ab878443e8a84e77e99ac167d7945ed29c22c324d130` |
| `mysummercar_Data\level1` | 122,756 | `aa37a88817372a44137fe8813a82ae43ebf8ada526a6eb2ad1d143147de596b0` |
| `mysummercar_Data\level2` | 104,297,292 | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` |
| `mysummercar_Data\level3` | 164,328 | `334667c90459c78869b18ba32e4d945f532e1c3f644ac3f33b067a3602b73642` |
| `mysummercar_Data\resources.assets` | 14,216 | `292106e4f021b1214ea7d4213763ad25d1073233945ccbd38c5528c24f1d29e1` |
| `mysummercar_Data\sharedassets0.assets` | 8,252,980 | `cb4c806ba1be6f6b9d46579e9907c77d64c60e515da71e5df749eaa159edad97` |
| `mysummercar_Data\sharedassets1.assets` | 141,522,172 | `8f0a0984f4e55229ecaebb57ef931b052f56998b9569a32e013780aa9dc78e02` |
| `mysummercar_Data\sharedassets2.assets` | 37,838,845 | `a90f2ddca72bf25b691f54565a808ceabae34af2392bbbc72b9d8eb5d447608f` |
| `mysummercar_Data\sharedassets3.assets` | 533,373,096 | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` (Milestone 2 controlled proof) |
| `mysummercar_Data\sharedassets3.resource` | 625,805,147 | Not hashed; over the 256 MiB lightweight threshold |
| `mysummercar_Data\sharedassets4.assets` | 6,313,412 | `17132def711445c14ffcc5987aececeee15645c73ef4c2b87f9d58e86f262a49` |

`mainData` contains the scene paths `Assets/_Scenes/SplashScreen.unity`, `MainMenu.unity`, `Intro.unity`, `GAME.unity`, and `Ending.unity`. Only `level0` through `level3` are present as physical files. A scene-name-to-level-number mapping is not proven by this audit and must not be inferred from file size alone.

Two `.unity3d` bundles were observed only under the pre-existing `Mods\Assets` tree. No core asset bundle was identified outside the serialized player data.

## Asset-type indicators

Milestone 2 ran one controlled AssetRipper `1.3.14` read-only load and exported exactly two reviewed mesh objects: `garage_shed_roof` (sharedassets3 Mesh PathID 2186) and `drum_brake_rear` (sharedassets1 Mesh PathID 125). This was not a full inventory, so global mesh, animation, material, terrain and object counts remain unknown.

AssetRipper logged one read-length error for an unrelated `Texture2D` in `sharedassets3.assets`. The two selected Mesh exports completed and were independently hashed, but this warning prevents treating the run as a complete or error-free inventory of all donor assets.

The pre-existing derived tree exposes `sharedassets0/1/2/3` texture directories and `sharedassets1/3` sound directories. Across the whole install it contains 225 `.tex`, 225 `.dds`, 1,096 `.wav`, and 1,081 `.snd` files. These counts prove only that a previous extraction exists; they do not establish clean stock provenance or production suitability.

World and terrain data are expected inside the serialized scene/assets files, but exact terrain objects, road geometry, animation clips and scene hierarchies still require later scoped inventories. Only the two Milestone 2 mesh records changed from unknown to `ReferenceOnly`; their replacements are `ReauthoredGeometry`. The Milestone 3 road, terrain and vegetation are independent prototype context and are not claimed as donor `WorldLayoutReference`.

## Managed assemblies and middleware

Thirty-one files are present under `mysummercar_Data\Managed`. Key observed assemblies are:

- game code: `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, and UnityScript assemblies;
- behavior/state machine: `PlayMaker.dll`;
- input: `cInput.dll`;
- save/storage: `ES2.dll` and `MoodkieSecurity.dll`;
- tweening: `HOTween.dll` plus iTween symbols in `Assembly-CSharp.dll`;
- legacy Unity runtime: `UnityEngine.dll`, `UnityEngine.UI.dll`, `mscorlib.dll`, and `System.*` assemblies;
- likely mod-loader dependencies by filename/install context: `0Harmony.dll`, `MSCLoader*.dll`, `INIFileParser.dll`, `Ionic.Zip.Reduced.dll`, `NAudio*`, `Newtonsoft.Json.dll`, and `NVorbis.dll`.

The stock/mod assessment is filename- and context-based only. It needs comparison with a clean install.

The native plugins are `CSteamworks.dll`, `LogitechSteeringWheel.dll`, and `UnityForceFeedback.dll`. They are audit references only and are not approved remake dependencies.

### Reflection-only metadata inventory

Because `ilspycmd` is absent, PowerShell 5.1 reflection-only loading was used to list metadata without executing code or decompiling method bodies:

- 1,334 types across four non-empty namespaces;
- 903 types directly coupled to PlayMaker namespaces/base types;
- 128 types directly coupled to UnityEngine base types;
- 105 types with direct base `System.Object` (heuristic only);
- 198 enums, nested/compiler-generated, or other types.

Observed plain-object review candidates include `Clutch`, `Axle`, `AxleInfo`, and `TireParameters`. `Clutch` exposes `GetClutchPosition`, `GetDragImpulse`, and `SetClutchPosition`. `Drivetrain` and `Wheel` expose many promising calculation method names, but both inherit `MonoBehaviour`; their formulas are only candidates for isolated review, not `CodePorted` work.

Observed high-coupling symbols include `CarDynamics`, `Drivetrain`, `Wheel`, `CarDamage`, `FuelTank`, `UniqueSaveManager`, `MetalRain`, `RainNearClip`, `SetRainClip`, `SettingsMenu`, `MasterAudio`, and hundreds of `HutongGames.PlayMaker.Actions.*` types. The type inventory strongly supports behavioral specification and reimplementation for scene/FSM systems.

No decompiled source was exported and no donor assembly was compiled.

## Audio, animation, world, and UI indicators

- Audio: `MasterAudio`, `EventSounds`, `SoundController`, playlists, and shared-assets sound directories indicate a custom Unity Audio/Master Audio-style stack. Final audio remains reauthored behind `IAudioBackend`.
- Animation: `HOTween`, iTween types, `SimpleIKSolver`, `IKLimb_BrunoFerreira`, and PlayMaker animation actions are present. Compatibility and authorship remain unknown.
- World/traffic: SWS spline/bezier types are present. Actual road and traffic spline instances are serialized in scene data and not yet inventoried.
- UI/input: `SettingsMenu`, legacy GUI actions, `cInput`, mouse-look components, and many PlayMaker GUI/input actions are present. This is a reimplementation target.

## Save-data location

The likely active save directory was safely confirmed at `%USERPROFILE%\AppData\LocalLow\Amistech\My Summer Car`. Metadata-only inspection found eight root files including `defaultES2File.txt`, `items.txt`, `options.txt`, `graveyard.txt`, and `trophies.txt`, plus mod-related files. Save contents were not parsed, copied, or changed. `ES2.dll`, `MoodkieSecurity.dll`, and `UniqueSaveManager` are format/workflow indicators only; no donor save format is yet documented.

## Local tool audit

| Tool | Observed result |
|---|---|
| Unity Editor | Installed at `C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe`; product version `6000.3.11f1_3000ef702840` |
| Git | `2.53.0.windows.1` |
| Git LFS | `3.7.1` |
| Windows PowerShell | `5.1.19041.6456` |
| .NET SDK | `10.0.301` |
| AssetRipper | `1.3.14` installed outside Git at `E:\GAYmDev_Studio\Tools\AssetRipper\1.3.14`; ignored local configuration points to `AssetRipper.GUI.Free.exe` |
| ILSpy / `ilspycmd` | Not configured, not on `PATH`, and absent from global .NET tools and checked common locations |

### Approved AssetRipper setup

The user explicitly approved external tools. AssetRipper `1.3.14` Windows x64 was downloaded from the [official AssetRipper releases](https://github.com/AssetRipper/AssetRipper/releases), and the archive matched the GitHub-published SHA-256 `808cddf66dd0357ad6b36b97de3a2aef5e3552e63af3ee0610f9a03a0378101c`. It was extracted outside Git, recorded in ignored local configuration, run headless against the donor read-only, and logged under external staging.

The normalized OBJ step uses project tool `msc-glb-to-obj 1.0.0` with pinned `trimesh 4.9.0`; its sidecars record GLB/OBJ hashes, bounds and mesh counts.

### Approval-gated `ilspycmd` setup

No installation was performed. The installed .NET 10 SDK satisfies the current CLI runtime requirement. After explicit approval, the current pinned command verified from the official NuGet listing on the audit date is:

```powershell
dotnet tool install --global ilspycmd --version 10.1.0.8386
ilspycmd --version
```

Then set `ILSpyCmdExecutable` to `%USERPROFILE%\.dotnet\tools\ilspycmd.exe`. Generate list-only output first:

```powershell
New-Item -ItemType Directory -Force "E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference\inventory"
ilspycmd -l c "D:\SteamLibrary\steamapps\common\My Summer Car\mysummercar_Data\Managed\Assembly-CSharp.dll" > "E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference\inventory\Assembly-CSharp.classes.txt"
```

Do not use project export until the output plan is reviewed. Never compile the exported source.

## External audit artifacts

| Artifact | Rows | Artifact SHA-256 |
|---|---:|---|
| `...DonorStaging\manifests\DONOR_FILE_INVENTORY_2026-07-13.csv` | 2,737 | `0556dbc796afce2d672d47ba3164868ea3216b4080472a4ae4f468fd6b2403ce` |
| `...DonorStaging\manifests\DONOR_RELEVANT_HASHES_2026-07-13.csv` | 52 | `d44208b655bc32f98228b7437f053ef400af917746e12daf6668344d805cd7c7` |
| `...DonorStaging\manifests\MANAGED_ASSEMBLY_INVENTORY_2026-07-13.csv` | 31 | `8dc7ac11232478971b6e58b5603cb107bdeaa81c5c79ac35b69a81e410ac048f` |
| `...DonorStaging\logs\MILESTONE_00_AUDIT_2026-07-13.log` | 11 log lines | `9c9a29bc8a7bf73cc4c825588f3c3fbe5992a1e14b4b82a07fb7b436f9da0741` |
| `...LegacyReference\metadata\ASSEMBLY_CSHARP_TYPE_METADATA_2026-07-13.csv` | 1,334 | `be80354d18eb944c944efa5ce9732f2e14b3e28ce779f1f218b253fed776792a` |
| `...LegacyReference\metadata\ASSEMBLY_CSHARP_NAMESPACE_SUMMARY_2026-07-13.csv` | 11 | `a228970e23a0a3adb6128cd893ff5e2048984df21d80c40d6631ad637a496eec` |

These files contain metadata, hashes, and symbol names only. No donor binary payload or decompiled source was placed in Git.

## Milestone 0 actions deliberately not executed

- No donor file was opened for writing, patched, renamed, deleted, or verified through Steam.
- No AssetRipper or resource-importer executable was run.
- No donor asset was copied or exported to staging or production `Assets`.
- No ILSpy package/tool was installed.
- No decompiled source was produced.
- No DRM, Steam, ownership, encryption, or access-control logic was inspected for transfer or bypass.
- No Unity Editor or Unity tests were run; Milestone 0 changed documentation only inside the project.
- No Milestone 1 runtime/asmdef/bootstrap implementation was started.

## Risks and unknowns

1. The donor install is contaminated by pre-existing mods and extraction output; clean provenance is not established.
2. Scene-to-`level*` mapping is unresolved despite observed scene names.
3. Full mesh, animation, terrain, hierarchy, FSM-instance, and material counts remain unknown because Milestone 2 intentionally exported only two objects.
4. Reflection-only type names do not reveal algorithm bodies, constants, object references, or side effects.
5. Save data is identified but its schema, encryption/encoding, and migration safety are unknown.
6. `sharedassets3.assets` was hashed for the controlled proof; `sharedassets3.resource` remains unhashed and was not transferred.

## Milestone 2 pipeline status

The project now contains a versioned, provenance-aware controlled import implementation, documented in `Docs/Porting/DONOR_PIPELINE.md`. It plans before writing, accepts files only from external staging, verifies SHA-256 before and after copy, prohibits overwrite conflicts, upserts registry/ledger metadata, detects production-reference leakage, and blocks builds on validation errors.

The two-mesh controlled proof is complete. External staging contains raw GLB/JSON and normalized OBJ/metadata for `garage_shed_roof` and `drum_brake_rear`. The two OBJ references are copied only into the ignored `ReferenceOnly` tree; the pre-existing in-donor `Unity_Assets_Files` output remains rejected and was not reused.

Independent replacements, HDRP material/textures, collision, LODs, pivot/mount comparison and the dedicated scene are validated by Unity. Donor validation passes with zero warnings, the EditMode suite passes `30/30`, and repeated planning reports two `UpToDate` operations. No donor binary is tracked by Git or used by a production prefab/build scene.

## Milestone 3 donor usage status

The garage prototype records the donor roof source as `sharedassets3.assets`, Mesh PathID `2186`, source-container SHA-256 `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`, and normalized OBJ SHA-256 `ea1c43d48a1b65ccee1bb616626e82cb89b2a79e12ba9ce1e2dbe7f96f1814ed`. Native bounds are `(-2.280219078, -1.648790002, 0.179946005)` to `(2.489785910, 1.861611009, 0.524957001)` with a zero pivot; the documented y-up mapping is `(X,Y,Z)` → `(X,Z,Y)`.

No donor file was written, patched, moved or deleted. No new raw extraction was created. The reference OBJ and comparison scene stay under ignored `LegacyImport/ReferenceOnly`; validation confirms that the production garage scene and enabled build content have no donor/reference-only dependency. New geometry, PBR maps, HDRP materials and scene composition are project-authored and remain `PrototypeReady`, not `ProductionReady`.

## Milestone 4 donor non-use statement

Milestone 4 did not read the donor installation, managed assemblies, decompiled reference, staged extraction or donor save data. No donor movement constants, interaction distance, input bindings, carry forces, tool rules or PlayMaker/FSM transitions were transferred. The first-person and interaction slice is a project-authored `Reimplemented` prototype based only on the milestone requirements and repository architecture.

No donor payload, provenance record or ledger row was added for M4. The donor installation remained read-only and outside the new runtime dependency graph.

## Milestone 04A bounded world-layout pilot

Milestone 04A inspected only one numeric zone around the player garage and adjacent retained road-route samples. Donor-world bounds are `(-269.98, -16.611, 1020.625)` to `(-49.98, 13.389, 1300.625)` metres. The garage anchor is the `CABIN > Shed` hierarchy at `(-169.98, -1.611, 1040.625)` and maps to project-local origin by `projectLocal = donorWorld - garageAnchor`; scene/world coordinates are y-up and use one metre per Unity unit. The older roof-only mesh-local mapping `(X,Y,Z) -> (X,Z,Y)` is not applied to these scene transforms.

The exact inspected `level2` identities were:

- garage: `CABIN` GO `18336` / T `54392`, `Shed` GO `19567` / T `55631`, roof GO `1064` / T `37122`, walls GO `2821` / T `38875`;
- route: `DirtRoad` GO `21869` / T `57925`, retaining only waypoint indices `1655, 1660, 1665, 1670, 1675, 1680, 1685`;
- blocked terrain evidence: `TERRAIN_OBJ` GO `34670` / T `70724`, render `DirtRoad` GO `1616` / T `37675`, candidate combined mesh PathID `3273`.

`level2` was rehashed as `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31`; `sharedassets3.assets` was rehashed as `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`. AssetRipper `1.3.14` was used as a local read-only object inspector. No project export, scene dump, complete route inventory, mesh payload, texture, material, audio, script or terrain extraction was produced.

The seven retained route samples form a `191.353 m` polyline; the nearest retained sample is `204.768 m` from the garage anchor. They are traffic-route positions, not a certified render-mesh centerline, and their Y values are approximate elevation evidence. Bounded terrain-mesh transfer is classified `Blocked`, because the available static object separation exposes combined/full-world content.

External staging stores only `manifests/MILESTONE_04A_WORLD_LAYOUT_INSPECTION.json`, SHA-256 `ae7a512036f8ade31174e9cb4aee018921755eb1cc7fd1bcfd2707fa8881ac54`. Reviewed project-owned data is `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json`; the disposable comparison scene remains under ignored `LegacyImport/ReferenceOnly` and outside Build Settings. The donor installation was not written, patched, renamed or deleted.

## Milestone 04A1 full serialized world-geometry transfer

После подтверждения стабильности vertical slice полный `GAME` world был выгружен AssetRipper 1.3.14 во внешний staging. Donor оставался read-only. К уже известным hashes добавлен `sharedassets3.resource` SHA-256 `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b`; extracted `GAME.unity` имеет SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

Нормализовано 36 045 placements, 13 509 geometry entities, 5 001 collider components и 1 362 unique referenced mesh GUID. Context-filtered reference world содержит 3 842 entities в 49 cells плюс 31 global entity. Остальные 9 667 geometry records не удалены и остаются `ClassifiedNonWorld` в project-owned entity table.

2 007 review records относятся к неоднозначным AssetRipper combined/static mesh bounds, а не к отсутствующим mesh GUID. 37 неподдерживаемых serialized class IDs сохранены metadata-only. Одна непрочитанная Texture2D не влияет на geometry transfer и не используется как production texture.

Generated scenes и category materials находятся только под ignored `Assets/Game/LegacyImport/ReferenceOnly/World/Generated`; production prefabs/build content от них не зависят. Durable database, provenance, tools, tests и отчёты отслеживаются Git. Детали: `Docs/WorldTransfer/`.

## Milestone 04B reference capture audit

Milestone 04B reused the exact-hash 04A/04A1 external manifests and performed read-only static inspection of the external AssetRipper `GAME.unity` representation. Donor build ID is `20171487`, donor Unity is `5.0.0f4`; the local installation remains mod-contaminated and is not claimed as clean stock.

Imported project-owned records cover the garage/world coordinate baseline, player camera/serialized base movement settings, Satsuma body/wheel geometry, root Rigidbody mass and one representative rear-drum pivot/mount marker chain. Each record preserves source ID/hash, locator, method, date, confidence, tolerance, raw observation, normalized unit/coordinate space and evidence references.

Interpretation boundaries are explicit:

- `Rigidbody.mass = 389 kg` is not labelled curb/assembled mass;
- `datsun_body` AABB is not the complete assembled vehicle envelope;
- named `tire_stock` mesh radius is not a proven fitted tire;
- a zero drum transform and `BoltPM` marker do not establish compatibility, install/detach rules, tool size or turn count;
- serialized `CharacterMotor` values do not establish sprint/crouch runtime behavior;
- M4 tuning remains in a separate override dataset and is not donor evidence.

В initial `04B.1` baseline donor runtime capture не заявлялся. В supplements raw video осталось external и в Git не добавлялось; сохраняются только hashes, observations и project-owned review reports. Ни donor file, ни save не открывались для записи, не патчились, не внедрялись и не использовались как runtime dependency.

### Dataset 04B.2 / 04B.3 — bounded rear-drum trace and diagnostic video review

После M05 preflight был выполнен только допустимый read-only разбор representative rear-left drum в уже существующем внешнем `GAME.unity` с повторно подтверждённым SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`. Donor executable и saves не запускались и не изменялись.

Трассированы `Assembly` FSM `112903`, `Removal` FSM `105234`, `BoltCheck` FSM `105233`, `Screw` FSM `107744`, player `Check tool` FSM `105041` и tool pickup FSM `110228`. Статически подтверждены collision trigger radius `0.01 m` at local `(-0.1,0,0)`, candidate identity `PART`/`drum brake(Clone)`, prerequisites `Trailarm_RL.Data.Installed/Bolted`, removal gates `TriggerWheelRL_New active` and `Drumbrake_RL.Data.Bolted=false`, один `BoltPM` control marker, discrete stage range `0..8`, wrench `14` scale mapping и positive-tighten/negative-untighten scroll direction. Local `Screw.BoltSize=0` не участвует в tool check.

Первый предоставленный MP4 добавлен только как external `BehavioralReference`: он диагностически отвергает ключ `11`, но содержит `0` валидных clean trials. Эти записи не являются `CodePorted` или `ProductionReady`; на этапе `04B.3` оба P0 requirement оставались `Partial`.

### Dataset 04B.4 — runtime repetitions and blocked-removal attestation

Внешний `My Summer Car 2026-07-14 18-52-48.mp4`, SHA-256 `86ad948bda1450fb8d2cf32583b51d0c2bc55ccef5aad9f428da3bc38e8e3c84`, просмотрен последовательным frame sampling. Ключ `14` показан явно; три отдельные последовательности дают одинаковый install → forward progression → reverse progression → removal outcome при отсутствующем rear-left wheel. Raw video и временные кадры не добавлялись в Git.

Пользователь отдельно подтвердил runtime blocked case: установленное rear-left wheel блокирует снятие барабана. Это attestation записано как `BehavioralReference` с `Medium` confidence, поскольку отдельного frame-addressable видео blocked case нет. Вместе с exact static `TriggerWheelRL_New` gate, отсутствием отдельного angular compare в donor rule и тремя runtime snap/remove repetitions это переводит `P0-ASSEMBLY-MOUNT-RULE` и `P0-ASSEMBLY-FASTENER-SEMANTICS` в `Covered`; fixture становится `Ready`.

Контракт остаётся дискретным `0..8`; physical torque, continuous turn angle и strip/failure не заявляются. Donor code/runtime не перенесён. Assembly-specific M05 gate открыт, а fitted-wheel identity и assembled/curb mass остаются отдельными `Partial` требованиями.

### Post-reinstall donor hash drift

После переустановки donor game текущий appmanifest по-прежнему сообщает build `20171487`, однако read-only SHA-256 текущих `sharedassets3.assets` и `sharedassets3.resource` равны соответственно `9511802c7fbcc5abcb11cb800d8fbba69edc38fea45cdde3edd2476e855331df` и `53aa0a2198b29ffe24d33a6d5e38a219d5724c99f5a737b537889865f1ea6fb1`. Они отличаются от frozen 04A1 provenance `1e956c...` / `19797f...`.

Исторические 04A1 source records не переписаны: они описывают уже созданный external extraction и должны оставаться привязаны к его исходным hashes. Для привязки нового donor install к world-transfer database требуется отдельная read-only audited extraction/reconciliation; до неё full EditMode dry-run честно сообщает hash mismatch.

## Milestone 05 donor-use audit

Milestone 05 did not read or modify the donor installation. It consumed only project-owned dataset `04B.4`, its ready representative fixture and the already reauthored M2 rear-brake-drum proof prefab.

Transferred behavior is narrowly classified `Reimplemented`: one BoltPM, wrench 14, discrete `0..8` endpoints, full loosen before removal and rear-left-wheel removal blocker. The `0.01 m` donor overlap marker is retained as provenance, while the remake position/orientation tolerances are explicitly project-authored tuning.

The generated M05 scene and all 15 prototype visuals have no dependency on `LegacyImport/ReferenceOnly`, `Imported/DonorGenerated`, donor Unity assemblies, executable, PlayMaker or raw videos. No item is claimed `CodePorted`, `TemporaryDirectImport` or `ProductionReady`.

## Milestone 05A world-remaster audit

05A consumes the frozen project-owned 04A1 world database as `WorldLayoutReference` and does not modify the donor installation or historical source records. The ambiguous 04A1 `CABIN/Shed` landmark label is preserved as an audit finding; the bounded home/garage pilot uses the separately identified `YARD/Building/Garage` anchor in `cell_0_-3`.

All production meshes, materials, prefabs and scenes are project-authored. Dependency validation reports no path from production content to `LegacyImport/ReferenceOnly` or `Imported/DonorGenerated`; donor textures and runtime assemblies are not used. The comparison scene contains only removable metadata proxies and is excluded from normal production ownership.

Registry coverage is 24/13,509 direct bindings. Statuses remain four `ProductionCandidate`, twenty `FirstPass` and 13,485 `Unassigned`; no item is mislabeled `ProductionReady`, `Approved`, `Verified` or `CodePorted`. The 263 grouped backlog tasks explicitly carry unfinished manual art.
