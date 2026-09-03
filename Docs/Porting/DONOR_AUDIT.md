# Donor Audit — Phase 1 evidence and implementation snapshot

Audit dates: 2026-07-13 baseline; 2026-07-14 Milestones 3–05A updates; 2026-07-15 Milestones 05B–06 updates; 2026-07-16 M06 diagnostic-audio, M06B1 sanitation and M06B2 cellization addenda; 2026-07-18 Milestone 07C provenance/night-dawn follow-up and Milestone 08 audio addenda; 2026-09-02 full-game parity/status synchronization; 2026-09-03 Satsuma panel-lifetime/bootlid-presentation/open-hold follow-up

Scope: read-only filesystem, binary-header, log, file-hash, reflection-only managed metadata inspection, audited use of previously staged references, and user-authorized local-only hash-ledgered donor gameplay-audio prototypes from frozen external staging

Donor root: `D:\SteamLibrary\steamapps\common\My Summer Car`

## Current implementation interpretation — 2026-09-02

This document records donor evidence and transfer provenance. Evidence capture
does not mean the corresponding remake feature is implemented. The current
formal result is 517 required rows: 4 `Verified`, 1
`KnownDifferenceApproved`, 6 `ImplementedUnverified`, 202
`PartiallyImplemented`, 303 `EvidenceCaptured` and 1 `Specified`.

The complete current implementation audit is
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md`. Row-level status remains
authoritative in `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`.

Current transfer boundary summary:

- world layout and 49-cell streaming are Verified;
- the donor world and gameplay presentation remain removable private
  `TemporaryDirectImport`, never `ProductionReady`;
- player/items/needs/home/NPC/traffic/economy/services/Satsuma are bounded
  Partial implementations, not complete domain ports;
- native save v16 is project-owned and independent from the donor save format;
- Jobs, Story, Authority, Rally and Media donor evidence has not been converted
  into complete runtime gameplay domains;
- no donor executable, old Unity runtime assembly, PlayMaker FSM/controller,
  Steam/DRM code or donor save manager is part of the new runtime.

## Executive finding

Satsuma hinged-panel correction V1d.60: frozen Assembly/Use FSM evidence
identifies four runtime-created hinges—bootlid, both doors and hood—with exact
pivots, axes, limits, asymmetric mouse-held torque, break values and four
fasteners each. The remake now uses dynamic `HingeJoint` physics rather than a
transform animation: release retains inertia, world objects block the panel,
only connected-chassis collision is ignored, and a `FixedJoint` with transferred
strength `12000` owns the closed latch. `F` has no action. A fully unfastened
panel detaches only at full opening; hood opening additionally requires the
dashboard release. The remake's one-degree latch threshold deliberately removes
the user-reported visible jump from the donor FSM's roughly-ten-degree final
state. Old donor break values remain configuration evidence, but the actual
Unity 6 joint is unbreakable so an ordinary chassis constraint impulse cannot
destroy it and deactivate the panel. Closed detection now uses the panel
Rigidbody's mount-relative rotation for two consecutive physics steps rather
than mirrored `HingeJoint.angle`. Generated content passes 39/39 and the
physical door/bootlid/hood PlayMode gate passes 4/4. The user accepted the V60
four-panel in-game check on 2026-09-03. See
`Docs/Phase1/SATSUMA_HINGED_PANEL_RUNTIME_FIX_2026-09-02.md`.

Registration-plate audit V1d.55: the two physical donor plate clones are
inactive inspection-station rewards, not starting `CARPARTS`. Front-bumper FSM
`109990` and bootlid FSM `109898` each consume a carried plate, enable an
embedded presentation and use no bolt group. Both embedded renderers are now
inactive on the fresh remake car. The actual reward/install flow remains
pending inspection-service scope instead of being incorrectly spawned in the
garage or inserted into the current Satsuma save graph.

Body-fitment and steering-hardware audit V1d.57: the right-door combined
handle/keyhole child is serialized with identity rotation and negative X scale;
preserving its direct-child local TRS removes the erroneous extra 180-degree
decomposition. The grille's actual Assembly destination is `pivot_grille`
Transform `68091`, not its sideways trigger. Stock/GT steering wheels each have
one central 10 mm nut at markers `62790`/`52786`; the steering column has only
the two 8 mm short bolts `57828`/`60175`, while `59336` is a referenced
tachometer screw. Explicit fastener presentation ownership also prevents a bolt
hover from outlining its entire panel. Current legacy migration passes 5/5 and
generated-content validation passes 39/39. The four bootlid `BoltPM` parents
have local Z scale `0.5`; retaining it corrects staged visible travel from the
remake's erroneous `20 mm` to the donor `10 mm`. The donor-active
`bootlid_emblem` uses the approximately `0.56 m` wide `datsun_bootlid_001`
exterior handle/garnish mesh and must not be removed by emblem sanitation.
Bootlid `Use` component `105480` ends opening with
`SetHingeJointLimits(-70,-69)` and restores `-70..0` before closing. Initial
hierarchy alone is insufficient for `bootlid_hooks`: Assembly FSM `104306`
turns the chassis copy off and the lid-owned copy on, while Removal FSM `110021`
reverses them. Exactly one black pair is visible; once installed it rotates with
the cover.

Streamed persistent-physics correction (2026-09-02): read-only native-slot
inspection proves that the reported missing engine parts remain present with
unique stable IDs but many saved `Loose` poses have enormous negative Y. The
signature is free fall while base-cell collision was absent, not donor content
loss, duplicate IDs, V1d.43/V1d.44 or NWH. The correction is project-owned
`Reimplemented` lifecycle: spatial/body quarantine across base-cell unload,
static-before-dynamic native restore, transactional topology rollback and
bounded home-front recovery for the vehicle aggregate, `Loose` parts and
explicitly critical items. No donor file, FSM, runtime assembly or raw payload
is transferred. Evidence:
`Docs/Phase1/ITEM_STREAMING_PHYSICS_AND_SATSUMA_SAVE_AUDIT_2026-09-02.md`.

Rear fastener presentation V1d.42: frozen MeshFilter children prove that the
rear suspension/drums use four long `bolt3`, six short `bolt` and only two
`bolt2` nuts. Springs have no fasteners; rear wheel lugs remain `bolt2` nuts.
The previous all-nut result came from `FastenerBuild`'s default mesh, not from
the fastening mechanics. A 12-marker serialized-YAML guard now assigns only
the reviewed mesh GUIDs while preserving poses, sizes, stages, graph, NWH and
saves. Build, EditMode 46/46, physics 18/18 and Bootstrap 1/1 pass; manual
acceptance remains pending. See
`Docs/Phase1/SATSUMA_REAR_FASTENER_MESH_PARITY_2026-09-01.md`.

Road-wheel seating V1d.41: donor corner Assembly FSMs select `Pivot1` for
`wheel_regula` and `Pivot2` for `wheel_offset`; the seats differ by 43 mm front
and 40 mm rear. All eight currently accepted stock/GT wheels are
`wheel_regula`, so their generated MountPose now uses the donor-standard
`-0.043 m` front / `-0.040 m` rear local-X correction. Build, generated 29/29,
installed physics 18/18, rear regression 9/9 and Bootstrap 1/1 pass. Offset
families are not currently imported; their per-part selector is deferred until
they exist. Manual seating acceptance remains pending. See
`Docs/Phase1/SATSUMA_ROAD_WHEEL_SEATING_PARITY_2026-09-01.md`.

Rear issue 4A (V1d.39, user accepted 2026-09-01): frozen Wheel.cs raycasts and
applies wheel-space K*c plus signed damping directly to the chassis; its rear
arm is IK/presentation, not the remake's former load-bearing spring-seat hinge.
The remake now enables exact none/stock/long NWH stages only with arm+drum and
drives kinematic arm/drum presentation from measured compression. Rear anti-roll
is zero. Known-load, lifecycle, generated-content and pure formula gates pass;
rear droop is 4/4, front regression is 13/13 and production Bootstrap is 1/1.
The prior hinge-motor candidate is rejected. See the scoped correction report.
The user reported normal rear loading/compression and accepted bounded 4A on
2026-09-01. The separate wheel test was updated in V1d.41 and the broad
installed-part suite now passes 18/18. Centre-of-mass changes and general save
repair remain deferred.

Rear issue 4 follow-up (V1d.38): the arbitrary −32-degree physical arm stop
exceeded the frozen donor Wheel/IK travel envelope and separated the correctly
positioned rigid shock meshes by 27.54 mm. The remake now derives arm limits
from the donor's no-spring, stock and long-spring carrier/travel profiles.
Build and 38/38 physics regressions pass; the user accepted the reported
extension-without-separation fix on 2026-08-31. Deferred EditMode/Bootstrap
results remain outstanding; this is not whole-suspension parity approval.
This is an IK-derived envelope adapted to the existing PhysX rig, not copied
donor joint limits or full force/trajectory parity. Evidence and validation:
`Docs/Phase1/SATSUMA_REAR_DROOP_AND_SHOCK_FIX_2026-08-31.md`.
No front, wheel-fitting or save-repair scope was added.

V1d.36 free-yaw regression follow-up: the original strut bone frames are
correct. The independent Unity6 adapter captured a stale identity anchor
Transform at the production 180-degree spawn; the rotated-spawn reproducer
measured 147 degrees of unintended yaw. Anchor pose synchronization before
joint creation fixes this without changing donor limits or presentation.
See `Docs/Phase1/SATSUMA_FRONT_FREE_YAW_FRAME_FIX_2026-08-31.md` for red/green
evidence and the user's2026-08-31 acceptance of the bounded free-yaw fix.

### Scoped front-steering follow-up, 2026-08-31 (V1d.36)

Read-only frozen GAME evidence confirms rod Data random loose Alignment±6,
the distinct14 mm adjustment nut (−/+0.1 degree; scaled0.28s; no ratchet),
joint12 mm latch8/0 and strutInstalled connection, free Y hinge±33, wishbone
and spindle BoltCheck2/0, and installation-only Bolted prerequisites.
Axles/CarDynamics startup overrides raw Wheelcamber0 with frontcamber−1.4;
the no-strut FSM does not reset it. Actual installed rim Removal disables its
mesh collider while Wheel.cs continues applying calculated contact forces
to the parent chassis. These findings support the bounded independent yaw
adapter without a second wheel-contact solver. Original runtime captures
were not executed; kinematic anchoring, startup timing, caster and save quirks
remain explicit compatibility limits, not hidden claims of exact physics.
Build passed. Current verification and exact source references are maintained
in `Docs/Phase1/SATSUMA_FRONT_ALIGNMENT_PARITY_2026-08-31.md`. The user deferred
general save repair until the entire suspension is accepted.

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

- Audio: `MasterAudio`, `EventSounds`, `SoundController`, playlists, and shared-assets sound directories indicate a custom Unity Audio/Master Audio-style stack. A bounded frozen-staging inspection later identified seven Satsuma prototype clips and static RPM/starter routing evidence; it did not establish a complete event/mixer graph. Final audio remains reauthored behind `IAudioBackend`.
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

After bounded Batch 01, registry coverage is 33/13,509 direct bindings. Statuses are thirteen `ProductionCandidate`, twenty `FirstPass` and 13,476 `Unassigned`; no item is mislabeled `ProductionReady`, `Approved`, `Verified` or `CodePorted`. The 261 grouped backlog tasks explicitly carry unfinished manual art.

Batch 01 consumes only nine frozen `cell_0_-2` placement records for home pier, lake/bottom anchors and yard hedges. Production geometry/materials are newly authored; comparison uses removable position proxies. The six garbage/milk gameplay records in the same cell remain unassigned rather than being guessed or pulled across the milestone boundary.

## Milestone 05C1 donor-use audit

05C1 did not read from or modify the current donor installation. It used only
the frozen, hash-bound 04A1/05C reference database and stable IDs for
`TREEWALL_HI`, `TERRAINOUT`, the store foundation and lakebed. Those records are
classified as `WorldLayoutReference`/`DimensionalReference`.

The two generated ground meshes, colliders, stable IDs, runtime marker and
scenes are project-authored `BlockoutSource`/`ReauthoredGeometry`. They contain
no donor mesh, texture, runtime assembly or executable dependency. The surface
is explicitly not `ProductionReady`: it is a bounded safety-topology baseline
whose scoped manual review passed on 2026-07-15.

## Milestone 05B production-independence audit

05B did not transfer new donor content. It validated the frozen 04A1/05C durable
metadata and project-authored 05A/05C1 outputs. The complete static production and
enabled-build dependency graph contains 49 seed assets, 230 visited assets and 482
edges, with zero references to `ReferenceOnly`, `DonorGenerated` or project Editor
content.

The 13,509-row donor replacement ledger was normalized to contain donor records
only. Two 05C1 project-authored safety pieces remain in their dedicated provenance
table and porting ledger, so they cannot inflate donor replacement coverage.

The current donor install was read only to recheck hashes. Its reinstalled
`sharedassets3.assets` and `.resource` still differ from frozen 04A1 provenance;
historical records were not rewritten. No donor file was modified, deleted or used
as a runtime dependency.

## Milestone 06 vehicle-simulation donor-use audit

The base M06 implementation did not read from, write to or modify the donor installation. It consumed only project-owned 04B reference records and existing audit documentation. No donor executable, Unity assembly, PlayMaker graph/runtime, mesh, texture or decompiled method is reachable from the simulation prototype or build.

The only donor-linked geometry uses transfer classification `DimensionalReference`; the calculated dimensions use M06 validation level `DerivedReference`:

- four reviewed wheel-root anchors;
- derived wheelbase `2.334 m`;
- derived front/rear tracks `1.2600002 / 1.2060003 m`.

Interpretation remains constrained:

- serialized root `Rigidbody.mass = 389 kg` is not curb/assembled mass and is not used as the M06 proxy mass;
- `datsun_body` AABB is body-mesh evidence only;
- candidate `tire_stock` radius `0.272667 m` remains `NeedsReview`/plausibility evidence because fitted-wheel identity is unproven.

All reviewed donor dynamic fixtures remain `Missing`: torque curve, ratios/final drive, clutch, steering, suspension, brakes, tire/surface forces, total mass/center of mass, battery/fluids/thermal behavior and acceleration/handling. M06 therefore labels every dynamic value `RemakeDesignTarget` / `ProvisionalProjectTuning`; none is claimed as `MeasuredDonorReference` or `ObservedDonorReference`.

The logical prerequisite fixture, pure simulation, raycast backend, surface metadata, 100 m track, telemetry and Editor tools are clean-room project implementations. The route is a `BlockoutSource`, not world-layout transfer or parity. `WORLD-COL-003` remains open. No simulation implementation is classified `CodePorted` or `ProductionReady`.

The strict validator passed on 2026-07-15 with marker `M06_VEHICLE_SIMULATION_VALIDATION_OK` after checking the config and scene dependency graphs for `LegacyImport/ReferenceOnly`, `Imported/DonorGenerated`, donor assemblies and PlayMaker. Builder, focused suites and calibration also passed; calibration still reports `referenceDynamicFixture=Missing` and `provisionalOnly=true`. After the first manual run exposed startup creep and camera shake, bounded stability remediation passed focused PlayMode `4/4`, including level rest, six-degree incline freedom and a later external wake/impulse. The user accepted the post-remediation basic-prototype drive/audio recheck on 2026-07-16; this does not promote any donor-derived item or donor fixture.

### M06 user-authorized local diagnostic audio

The user authorized original Satsuma sounds only to make the prototype state easier to assess by ear. The inspected source is the frozen external export at `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/AudioClip` under donor staging, produced by AssetRipper `1.3.14` with `Default` importer settings. The current installed donor remains mod-contaminated and has audited container hash drift; it is not the runtime/file source for this diagnostic and was not modified.

The bounded mapping is:

| Role | File | SHA-256 |
|---|---|---|
| Low/idle RPM | `850_idle5.ogg` | `41296478D8828AF8E7840FE66F9FE2C9A0F05D78127B2BFB20ED35C403894CF9` |
| Mid RPM | `850_mid3.ogg` | `EF0F7E94F7FB08B1D8F1EA16AEE7BDDE5A27F54FB12FB6357A26DB11493F1FA5` |
| High RPM | `850_mid13.ogg` | `464DA9DBCE2219040522A18B481B4E5C1C285303152F56975D167E5F54044480` |
| Starter event 1 | `motor_start_1.ogg` | `5F9EEB889C660E7AE1F08F9474951ECB3938878AE2A5C13038952A6DDD5892AE` |
| Starter event 2 | `motor_start_2.ogg` | `854EA1EECC2E9DBFC37674CA0968F3FFACE2F75AE8B85A73F06C40FD1198F5AB` |
| Starter event 3 | `motor_start_3.ogg` | `9D9ABE53A8C253E8C571FE1E99B8B34509B2944D6428FBFCA8412413FE551CD2` |
| Starter whine | `starter_whine.ogg` | `215329D05D5C5C1BE5F0AE1B831AA20E01A02DD0418856C56808DE52CD310CE3` |

Static `GAME.unity` (frozen export SHA-256 `C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`) `AudioEngineSatsuma` references confirm the three RPM-layer roles, and `MasterAudio/Starting` confirms the starter roles. That inspected routing metadata is `ReferenceOnly`; the seven external clips used by the local Editor prototype are `TemporaryDirectImport`. They stay outside Git, tracked `Assets`, player builds and production dependencies. No Satsuma-specific shutdown/stall clip has been proven, so loop fade-out is an explicit diagnostic approximation. No complete mixer, load response, interior/exterior behavior or donor audio parity is claimed; final clips and mix remain reauthored or properly licensed.

The project-owned bridge is clean-room `Reimplemented` code: `VehicleAudioContracts.cs` defines `IVehicleAudioBackend`, `VehicleAudioPresenter.cs` consumes simulation telemetry at `FixedUpdate`, and `UnityAudioBackend.cs` performs Editor-only hash/load/mix work. Builder `1.1.0` creates `M06_LocalDiagnosticVehicleAudio`; the strict validator requires the scene to have no serialized `AudioSource`/`AudioClip` dependency. Fresh focused EditMode passes `18/18`, focused PlayMode passes `4/4`, full EditMode is `157/160` in `44.4875523 s` with exactly the same three unrelated baselines, and full PlayMode passes `31/31`; each M06 PlayMode case emits `M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips=7 source=ExternalDonorStaging hashes=Verified` with configured staging. Missing staging remains a silent fallback rather than a simulation-test failure, and starter event ordering is asserted. Thus the local diagnostic can be removed or unavailable without changing simulation authority or build viability.

## Milestone 06B1 canonical donor-world baseline audit

06B1 did not repeat extraction and did not use the current reinstalled donor
containers as an implicit fallback. The canonical source is the frozen
AssetRipper export:

`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`

Its SHA-256 is
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The accompanying `path_id_map.json`, twelve normalized manifests and six
project-owned transfer inputs are independently hash-pinned before generation.
The historical source revision remains separate from the current donor
`sharedassets3` hash drift.

The deterministic sanitation policy `06B1.4` creates a local ignored runtime
payload only below:

`Assets/Game/LegacyImport/RuntimeBaseline/`

The canonical scene contains `3,842` project-owned metadata entities and
`2,605` static renderers. `1,237` records remain metadata-only: `1,058` have no
usable mesh, `62` are skinned renderers and `117` are static accessories below
character `/skeleton/` hierarchies. Runtime collision is intentionally `0` until
06B2. Donor MonoBehaviours, PlayMaker FSMs, assemblies, cameras, audio, lighting,
weather, UI, NPC logic, physics bodies and gameplay managers are absent.

The scene and generated mesh/material payload are classified
`TemporaryDirectImport`, allowed only for private local feature-parity work and
not `ProductionReady`. Generated payload remains ignored by Git and excluded
from Build Settings. The committed source manifest contains portable relative
paths, source hashes, counts and semantic/source/payload fingerprints.

Two consecutive builds produced the same manifest SHA-256
`e385298c0b6ef344ade8c3a5f1f7c5fd684b690a8114808d658aea5f7b15ec6a`,
semantic fingerprint
`32438aca354e85af6bb8356a0546fc4ac6a0009fb58b5276a97a843191476635`
and payload fingerprint
`ac500e0b81db904840b7a6d742db33699548553bf234fb1dfc393bbb56fc7e21`.
Cold validation, focused EditMode `4/4` and PlayMode boot `1/1` passed.

The existing `cell_0_-3` and `cell_0_-2` custom visuals remain retained
technical fixtures but are classified
`PrototypeOnly / RejectedForFidelity / InactiveInFeatureParityProfile`.
At the 06B1 close Bootstrap/profile activation, cellization and safe collision
transfer remained deferred. Their bounded 06B2 disposition is recorded below.

## Milestone 06B2 donor-world cellization and activation audit

06B2 reused the frozen 06B1 canonical source and the existing project-owned
512 m streaming architecture. It did not extract the donor game again and did
not use the current installed donor files as a fallback.

The active private-local profile is `donor-feature-parity-06b2`. Its generated
ignored payload contains:

- one `World_Global_Legacy` scene;
- 49 `World_Cell_<X>_<Z>_Legacy` scenes;
- 3,842 project-owned legacy metadata entities;
- 2,605 static renderers;
- 32 explicitly allowed static colliders;
- no donor MonoBehaviour, PlayMaker FSM, assembly, camera, audio, lighting,
  weather, UI, NPC logic, Rigidbody, joint or trigger.

Ownership is deterministic. 3,754 normal static entities retain their frozen
source cell. 88 entities remain global: 31 already-classified large/continuous
objects, 41 `MAP/MESH` static-batch aggregates, one explicit cross-cell
traversal object and 15 bootstrap/traversal safety-collider owners. Geometry was
not artistically changed or destructively split. The ownership fingerprint is
`1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b`.

The collision subset is a project-owned allowlist of 20 `MeshCollider` and 12
`BoxCollider` records. It is `TemporaryDirectImport`, not a transfer of donor
physics behavior. Doors, windows, dynamic props, NPC collision and trigger
volumes remain excluded.

All legacy visuals receive a project-owned replacement key
`legacy-world:<stable-id>`. Donor names and hierarchy paths remain provenance
only. Fifteen project-owned gameplay anchors are stored in a separate
`WorldGameplayCellCatalog` and retain `StableEntityId` independent of visual
scene load/unload or future production replacement.

The rejected custom visuals for `cell_0_-3` and `cell_0_-2` are absent from the
active donor profile. They are retained only in the separate
`prototype-fixture` manifest and debug scene. No deletion or remodelling was
performed.

Generated RuntimeBaseline content remains ignored by Git. A pre-build guard
blocks any public/distributable build and permits donor content only in an
explicitly acknowledged private local Development build.

The v5.1 presentation closure, generated by converter `06B2-v5.1.5`, resolves 292 donor material definitions plus one
explicit reviewed fallback for built-in material `10302`. It preserves 2,744
ordered slots on 2,605 renderers. The source image closure contains 265 images;
269 conversion records resolve to 268 imported role-specific texture variants
and one donor cubemap intentionally excluded because sky/reflection/weather
ownership is out of scope. The sharing closure is `384 -> 268`, avoiding 116
duplicate variants. Eight detail-normal variants use deterministic HDRP Detail
Map packing `R=.5, G=Y, B=.5, A=X`, while donor detail UV and strength are
preserved. Detail Map is assigned to 22 materials: 20 detail-only and two
together with a primary normal. Source donor shader files are read only for
classification and are never copied, compiled or referenced by runtime assets.
The two lake-water materials now preserve donor `_BaseColor.a`: `Water4Adv_Lake`
uses `0.2901961` and `Water4Simple` uses `0.5058824`. The shore-foam texture is
explicitly excluded as a full-surface base map because that donor shader input
does not represent the lake's full-surface colour.

Generated materials use project-owned HDRP Lit/Unlit shaders and are shared
across cells. Runtime mode switching uses only `Renderer.sharedMaterials`.
`LegacyTextured` is the active default, `LegacyDiagnostic` is the comparison
mode and rejected prototype visuals remain hidden. Presentation fingerprint:
`e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`.

Artifact SHA-256 values are
`9f83de8770a966ef7cc28ae9be71f00e6668883b9c6e5e2498adf92b8fc93546`
for the ownership matrix,
`794b68c9d86a083343e08452d38ef621bc2c989c433810e7de17429f354cfcb4`
for the object-to-cell manifest and
`cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192`
for the material/texture manifest.

Presentation build `M06B2V51_PresentationBuild12_WaterFix.log`, full validator
`M06B2V51_CellizationValidator09_WaterFix.log` and focused EditMode
`M06B2V51_EditMode05_WaterFix.xml` `7/7` in `65.344 s` passed. Focused PlayMode
`M06B2V51_PlayMode06_WaterFix.xml` `6/6` in `7.4557305 s`, prototype
regressions `3/3` and `8/8`, and performance PlayMode
`M06B2V51_PerformancePlayMode06_WaterFix.xml` `1/1` in `2.5581146 s` passed. The final
performance capture SHA-256 is
`60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`;
initial/plateau texture residency is `396.25 / 502.88 MiB`, peak generated
material/texture counts are `263 / 217`, initial/peak used memory is
`602.30 / 732.64 MiB`, and peak reserved memory is `1,177.36 MiB`. Startup,
global and first-focus timings are `1068.26 / 818.39 / 1060.62 ms`; vehicle
preload/worst refresh are `95.46 / 404.40 ms`; sampled/main-thread maxima are
`11.259 / 11.201 ms`. The user then accepted Bootstrap startup, donor-map
fidelity, walking to the lake, Teimo-area unload/reload and out-of-bounds
recovery on 2026-07-16.

The lake surface remains a temporary flat compatibility plane and original-game
terrain voids remain present. The user accepted unusual legacy textures,
low-resolution/striped terrain presentation and tree-wall artifacts as
documented temporary visual debt; none is promoted to production art.
The user accepted the alpha-corrected water on 2026-07-16 as
`PASS / HumanAccepted`. On the same date the user walked the bridges and moved
the character across cell boundaries without observing traversal, collision,
seam, duplicate, popping or load/unload issues. Dedicated vehicle driving was
not repeated; automated high-speed preload validation passed. The v5.1 manual
completion gate is `PASS / HumanAccepted`, and the 06B3 entry gate is `GO`;
the subsequent 06B3 audit is now closed as
`PASS / Frozen / HumanAccepted` without promoting the donor baseline to
production art.

## Milestone 07C production-weather provenance

Milestone 07C does not inspect, transfer or execute donor weather code,
PlayMaker state, lighting logic, audio logic or save data. Project time,
calendar, weather fronts, wetness, lightning, exposure and save DTOs remain
clean-room `Reimplemented` systems. Enviro 3 is a separately licensed,
read-only third-party presentation dependency and is not donor content. Its
accepted installation is now identified by `538` files, `305968075` bytes and
SHA-256 fingerprint
`a22883eaea25d7dca37c50429c59cff7e8cb6a61cd0686463801e10123d9f040`.
The audited migration from the 07C fingerprint is limited to Unity metadata
reserialization of one unused URP sample material; shader, textures, values,
runtime bindings and Build Settings references are unchanged. The vendor file
itself was not edited by the project remediation.

The only donor-derived 07C measurements are the bounded home-house and
home-garage static renderer AABBs from the already frozen world baseline. They
are classified `DimensionalReference`; the generated runtime volumes use the
project-owned stable IDs
`weather.shelter.home.house.interior.v1` and
`weather.shelter.home.garage.interior.v1`. The measurement evidence pins the
selected-geometry fingerprint
`508b155f7df7c04622952b2b1e63dadcce1a043030c022c271930a54e5dea400`.
The normalized full-scene SHA-256 is retained as audit metadata only so a
bounded renderer/material/collision regeneration cannot invalidate unchanged
shelter geometry.
No donor hierarchy path or instance ID becomes runtime identity.

The frozen `DonorWorldBaseline-v001`, active profile
`donor-feature-parity-06b2`, ownership/presentation fingerprints, source
transforms and generated world payload were not modified by the weather
rollout. Production wetness uses a reviewed, bounded compatibility allowlist
over shared materials; it does not reauthor or promote donor materials to
`ProductionReady`.

The bounded visual follow-ups also remain project-owned presentation
configuration rather than donor transfer. The user accepted rain and the
current sunset, and accepted metallic-looking temporary surfaces as donor
material/shader debt for later replacement. At runtime the adapter now applies
the solar calibration `60 N / 27.3 E / UTC+3`; on the reference date the
installed Enviro algorithm crosses the horizon at approximately `04:59` and
`21:35`. A smooth `7.5 EV` minimum night exposure applies from full-night
`solarTime <= 0.43` toward unchanged daylight at `0.5`. Aurora remains
suppressed across quality tiers, temporary donor-baseline reflection intensity
is capped at `0.6`, and reviewed wet-surface smoothness remains limited to
`0.45` for opaque surfaces and `0.25` for alpha-clipped vegetation. Shelter
removal ellipsoids keep their measured horizontal tiling while enforcing
vertical stretch `>= 1`. None of these values is claimed as donor
`ConfigurationTransferred` or final production-art tuning.

Fresh night/dawn follow-up evidence is:

- Enviro integration `17/17`, combined production EditMode `32/32` and
  production PlayMode `6/6` pass in the matching
  `Logs/M07C_VisualRemediation3_*.xml` artifacts;
- full EditMode is `328/334`: the same four historical Garage/World failures
  plus two current WorldBaseline material-contract failures
  (`Logs/M07C_VisualRemediation3_FullEditMode.xml`);
- a fresh focused WorldBaseline rerun is `8/10` with the same two failures
  (`Logs/M07C_VisualRemediation3_WorldBaseline.xml`), confirming persistent
  generated-payload drift rather than full-suite ordering pollution;
- the four affected ignored generated compatibility materials (`06cd8242...`,
  `2b837893...`, `5cc44389...`, `69ad9b54...`) were already rewritten before
  this follow-up; the night/dawn change did not edit frozen payload;
- the previous second-remediation freeze result remains the last strict `PASS`,
  with SHA-256
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`
  (`Logs/M07C_VisualRemediation2_WorldFreeze.log`). Current generated-material
  validation is not clean and is not a fresh freeze pass.

The user accepted the corrected night brightness and approximately `05:00` dawn
as `USER PASS` on 2026-07-18; no capture artifact was supplied for that manual
retest. This does not accept matched world-fidelity captures, additional
interior coverage, Development Player performance or the whole 07C milestone;
those gates remain `PENDING`. Neither the Enviro vendor payload nor frozen donor
world was modified by this follow-up, and the material-contract failure above
remains open.

## Milestone 08 audio provenance addendum

Milestone 08 does not port donor `MasterAudio`, `EventSounds`, `SoundController`,
PlayMaker actions, mixer graphs or runtime assemblies. The backend contracts,
router, emitter/listener/zone metadata, vehicle/weather/interaction adapters,
Unity fallback and official-Wwise boundary are clean-room `Reimplemented` code.
Audiokinetic Wwise Authoring/SDK `2025.1.9.9197` and Unity Integration bundle
`2025.1.9.4241` are third-party tooling/dependencies, not donor transfers.

The seven previous vehicle diagnostics and twenty-three selected M08 prototype
sources each have an individual source hash/ledger row and keep
`TemporaryDirectImport` classification. They stay in external staging or the
ignored local `Originals/**/TempDonorPrototype` subtree, outside Git and
public/distributable builds. Putting a clip in a local Wwise project does not
promote it to `ProductionReady`. Final gameplay and ambience audio is newly
authored/mixed; later menu/UI sounds are explicitly a different newly authored
set.

The Wwise project and generated banks do not contain donor code or create a
donor runtime dependency. Six ignored Windows banks total `26,115,951 B`; exact
per-bank hashes are recorded in `Docs/Audio/WWISE_SETUP.md`. Authoring validates
52 events, 32 RTPCs, 4 switch groups, 3 state groups, 6 mixer buses/Volume
curves, 7 routed roots and exact child routing `52/52`. Footsteps validate 10
sounds, 5 random pairs and `9/9` switch assignments; generated output contains
30 embedded media objects and no loose WEM. Source/runtime bank copies
hash-match, and the user confirms Wwise backend / 6 banks / 0 missing at
runtime. The post-remediation private Windows Development build passes at
`844,040,197 B` (`844,257,747 B` complete folder), packages matching banks and
initializes Wwise in native boot. Headless output suspension leaves audibility
and real-device Wwise Profiler audio-thread CPU as manual evidence. The user
accepted the bounded Milestone 08 baseline on 2026-07-18; fine-grained audio
zone and mix tuning is explicitly deferred to polishing without promoting any
temporary donor media to production status.
The latest full EditMode baseline is 350/356 with exactly six unrelated
Garage/World failures and is not claimed as a full-suite pass. Full PlayMode is
68/70: one unrelated VehiclePhysicsValidation GarageExit failure and one
environment-gated performance skip; the explicit performance run passes 1/1,
and the exact vehicle-route failure passes 1/1 in 8.696 s in isolation. This is
an order-dependent/flaky non-audio baseline; full PlayMode is not a pass.

## Milestone 08A1 donor-world baseline hardening addendum

The 08A1 work reuses the frozen canonical donor-world extraction and does not
read from, write to or regenerate the installed donor game. It produces local
candidate `DonorWorldBaseline-v002` from the same recorded source revision.
The candidate is `NotPromoted / HumanAcceptancePending`; it is active in the
main local workspace only for the next manual playtest. The previously accepted
`DonorWorldBaseline-v001` remains the accepted record and has an external
rollback copy labelled
`DonorWorldBaseline-v001_before_08A1_20260720`.

The generated material path remains temporary private Phase 1 presentation.
Generator `06B2-v5.2-08A1` and project-owned compatibility policy
`08A1-temporary-hdrp-compatibility-v2` validate HDRP material ownership, cap
metallic/smoothness/emission response, correct two-sided normals and apply
explicit renderer shadow compatibility without importing donor shader code.
The resulting 2,605 renderer bindings remain `TemporaryDirectImport`; the
selection, conversion and renderer policy are clean-room `Reimplemented`
tooling. No material is promoted to `ProductionReady`.

The collision review covers all 1,488 frozen source collider records. Policy
`08A1.6` admits 586 traversal solids: 32 existing safety-critical global
colliders plus 554 ordinary cell-owned static solids. The runtime shape split is
276 non-convex static mesh, 279 box and 31 capsule colliders. Actors, triggers,
inactive/disabled objects, dynamic Rigidbody ancestry and unresolved dynamic
vehicles remain excluded. All 79 door records are deliberately excluded and
remain pass-through until a project-owned door binding/mechanic is implemented.
The selected donor-derived collision shapes are `TemporaryDirectImport`; the
disposition manifest, stable ownership and generation policy are
`Reimplemented`. Gameplay does not address donor hierarchy names or instance
IDs.

The bounded daylight/indirect-lighting adjustment is project-authored
presentation configuration through the existing Enviro boundary and HDRP
`IndirectLightingController`; it is not donor `ConfigurationTransferred`.
Automated candidate evidence passes focused EditMode `41/41` and PlayMode
`14/14`. After local activation, the protected 08A UI regression suites also
pass EditMode `33/33` and PlayMode `11/11`. Human traversal, material, shadow
and collision acceptance remains pending and is not inferred from automation.

## Milestone 10A character-presentation addendum

The locked `GAME.unity` scene and five selected assets were inspected read-only
from the external AssetRipper staging tree. The bounded transfer contains two
body meshes and three compatible clips for stationary-service, scheduled-
roaming and vehicle-linked framework fixtures. Source scene, asset hashes and
donor GUIDs are fixed in
`Phase1CharacterPresentationManifest.json`; local generated copies receive
separate deterministic project GUIDs to avoid depending on the existing
`ReferenceOnly` mesh library.

The generated payload remains ignored under
`Assets/Game/LegacyImport/RuntimeBaseline/Characters` and is classified
`TemporaryDirectImport`. The importer reconstructs only required transforms,
bones and skinned renderers, assigns a neutral project HDRP material, converts
clips to legacy presentation clips, removes animation events and creates a
project-owned wrapper/catalog. Donor materials, textures, audio, scripts,
MonoBehaviours, PlayMaker FSMs, AnimatorControllers and runtime assemblies are
excluded. A build guard compares the local report to the current manifest and
fails closed when the payload is absent, invalid or stale.

The corresponding Characters/NPC simulation, scheduling, dialogue hooks,
streaming reconciliation and save code are clean-room `Reimplemented`. The
three fixture schedules/routes are explicit framework test data, not donor
configuration transfer and not evidence that any 10B roster row is Verified.

The first manual 10A pass exposed two project-runtime defects rather than donor
evidence gaps: one-shot all-day route progress was already complete at the
default noon start, and repeated state application restarted clips every
GameTime tick. The corrected roaming fixture uses project-owned ping-pong
traversal and unchanged activity is animation-idempotent. Focused validation is
`8/8`. A second manual pass confirmed route movement/streaming but exposed
renderer-based culling because sanitized skeleton and renderer branches are
siblings. The corrected importer enforces `AlwaysAnimate`, and the focused test
now samples actual bone changes for all three clips. Manual visual retest remains
pending; generated real-frame PlayMode smoke passes `1/1`.

Final manual retest on 2026-08-01 confirmed Teimo register motion, Alpo gait and
ping-pong traversal, Latanen receipt/register motion and stable streaming
transitions. The user explicitly accepted the bounded 10A foundation. This is
not donor-behavior verification for the complete 10B roster.

## Milestone 10B-R1 character-presentation addendum

The 10A neutral-material limitation is superseded for the private Phase 1
character baseline. Read-only audit of the locked scene renderer records added
Teimo, Alpo, Latanen, Fleetari, Farmer and Berryman ordered material identities,
18 hash-pinned donor textures, `bodymesh_1`, `bodymesh_2`, three compatible
clips, four reviewed headwear meshes and two exact glasses meshes to
`Phase1CharacterPresentationManifest.json` schema 5. The importer now builds
project HDRP/Lit wrapper materials with the correct `_BaseColorMap` in all body
submesh slots and reconstructs only the explicitly selected static accessories.
Donor shaders remain excluded.

For R1, exact scene evidence covers Fleetari REPAIRSHOP opening/work metadata,
Farmer farm root and six move targets, Berryman StrawberryField opening/babble
metadata and Teimo STORE opening/speak/bicycle metadata. The resulting anchors,
schedules and route are `ConfigurationTransferred` only where the serialized
evidence was explicit; project controllers, stable IDs, dialogue/event hooks,
streaming and saves are `Reimplemented`. Details and known gaps are in
`Docs/NPC/M10B_R1_DONOR_EVIDENCE.md`.

All six local wrappers remain `TemporaryDirectImport`, private and ignored.
Geometry, materials/textures and animation have separate production replacement
keys. Phase 2 must replace each presentation layer; this addendum does not
classify any donor character asset as `ProductionReady`.

Manual inspection then identified presentation calibration defects. Schema 5
records a Teimo visual-root lift, Alpo zeroed visual offset,
Teimo/Berryman/Latanen/Farmer headwear bindings and Teimo/Fleetari/Farmer
glasses bindings.
Fleetari's source parent carries a non-upright world rotation even though the
project interaction host intentionally retains only its yaw. The manifest now
stores the exact inverse-host compensation (`position -0.0202864, 0.47984046,
-0.1293867`; `rotation -0.18221372, -0.7523851, -0.17260018, -0.6090358`),
which reconstructs the serialized `bodymesh` world transform beside renderer
`80242` (`chair_pub 6`) without duplicating or owning that world prop. Berryman
retains the donor `hand_left` target and seated left-hand clip; his apparent pose defect
was caused by missing StrawberryField tent context, not by the character clip.
The schema 5 rebuild completed with 6 wrappers, 19 materials, 18 textures and
seven accessory renderers. Current focused validation passes NPC EditMode
`14/14` and NPC PlayMode `2/2`. On 2026-08-01 the user accepted the bounded character/accessory
placement and Fleetari's contact with the existing chair/desk context. A later
observation exposed that Teimo's daytime anchor was still the scene's saved pub
endpoint. The exact `teimo_move_store`/`teimo_move_bar` root curves prove the
shop/pub local-X endpoints `0/-6.5`; the corrected anchors pass NPC EditMode
`11/11` and PlayMode `1/1`. The user accepted the corrected daytime/evening
placement on 2026-08-01. Dependent behavior remains open.

The Farmer route was subsequently corrected from a synthetic six-waypoint
closing loop to a distance-weighted ping-pong traversal. The return from the
mailbox now walks the donor-ordered intermediate anchors in reverse at uniform
path speed instead of taking a long direct segment across terrain. Automated
route validation is included in the NPC EditMode `14/14` result; manual in-game
comparison remains pending.

The subsequent user capture showed that interpolated anchor Y remained above
the local road profile. The project navigation presentation now performs a
non-allocating nearest-surface probe against loaded `WorldSurface` and
`WorldSolid` collision only for `Walking` poses. Logical anchors, route progress
and save data remain unchanged. Focused validation passes NPC EditMode `14/14`,
NPC PlayMode `2/2` and the Farm Bootstrap streaming test `1/1`; manual visual
confirmation remains pending.

### Milestone 10B-R1 bounded voice addendum

Read-only review of the frozen `GAME.unity` FSMs selected 17 exact R1 voice
variations: Teimo `paivaa1..7` and `pubi1..4`, Fleetari `hello01`, Tohvakka
`hello`, and Berryman `hitaus1..3` plus `halla`. The committed schema-1 manifest
pins each FSM/component association, AudioSource, AudioClip GUID, serialized
asset hash, PCM resource hash, subtitle, serialized donor duration, source PCM
duration and spatial distance. AssetRipper's serialized `AudioClip.m_Length`
does not consistently match the RIFF PCM data duration, so these two values are
retained and validated independently rather than conflated.

The importer verifies the frozen scene hash and both hashes for every entry,
checks the donor resource is RIFF/WAVE, and copies only the PCM bytes into the
ignored private `RuntimeBaseline/Audio/NpcR1` boundary. It does not copy donor
AudioClip assets, MasterAudio objects, PlayMaker FSMs/controllers, scripts or
assemblies. Runtime dialogue, schedule gating, deterministic rotation, stable
event IDs and preferred-to-fallback routing are `Reimplemented`; the clips are
`TemporaryDirectImport` and retain one Phase-2 production replacement key. The
fresh builder produced 17/17 clips and event definitions; current NPC EditMode
passes `14/14`, NPC PlayMode `2/2`, and Unity fallback PlayMode `7/7`. Conditional
service/job lines, autonomous donor babble timing and audible manual comparison
remain open.

## Phase 1 missing job-location presentation addendum

The frozen entity table proves that `JOBS/StrawberryField/`,
`JOBS/HouseShit1/` through `JOBS/HouseShit5/` and `JOBS/Farm/` had zero
`ReferenceWorldEligible` objects in the original baseline selection. A
hash-locked supplemental manifest now recovers only reviewed active static
renderers and non-trigger static collision from those roots. It excludes donor
NPC, skeleton, function/FSM, wasp, Farmer and combine hierarchies.

The generated private overlay contains 252 renderers and 80 effective
colliders across seven existing streaming cells. It includes the strawberry
field proxy, both tent renderers, five septic-job house sites and their
waste-well geometry, plus 49 static Farm renderers. Two enabled donor
`MeshCollider` records with empty mesh references are explicitly recorded and
omitted because they have no collision effect. Stable
supplemental metadata and `legacy-world:<stable-id>` replacement keys are
project-owned; the Phase-2 registry now treats base and supplemental entities
uniformly. Details are in
`Docs/WorldBaseline/PHASE1_JOB_LOCATION_PRESENTATION_REMEDIATION.md`.

## Phase 1 item physics and surface correction addendum

Read-only inspection of frozen `GAME.unity` confirmed the basketball renderer
material GUID `1603a543741ec1a4f863a810e1509c21` and the helmet shell/lining
material GUID `bc4c3320b6b204d4a8e798a5a76de0e4`. A hash-locked schema-1 manifest
selects only `basketball2.png`, `basketball_n.png` and
`racing_accessories.png`. The ignored private RuntimeBaseline receives copied
textures with generated GUIDs and project-owned HDRP/Lit materials; donor
shaders and material assets are not runtime dependencies. These assets remain
`TemporaryDirectImport`, not `ProductionReady`, and retain the existing
`legacy.item.basketball` / `legacy.item.helmet` replacement keys.

The same scene shows the helmet `Paint` FSM storing `Color` and `PaintType`,
then selecting regular or matte material. This is `BehavioralReference` only:
no PlayMaker state, action or controller was transferred. Newly written item
state stores three normalized color channels plus applied/matte flags, while a
project-owned presenter applies an HDRP material-property override to the
reviewed shell binding. Save migration `10 -> 11` initializes old helmet
records without changing stable identity.

The gravity failure was project-authored regression, not donor behavior or a
PhysX global setting. `useGravity` was added to world-entity JSON without a
migration; absent members in document v9 became `false` and carry/throw restored
that stale value. Migration `9 -> 10` repairs only authoritative item stable
IDs, and loose-item policy now enforces dynamic gravity/collision after release
or throw. Focused Item/Save EditMode passes `50/50`, the new throw PlayMode test
passes `1/1`, and lighting calibration EditMode passes `9/9`.

### 2026-08-02 — ambience, liquids, garbage barrel and puddles

Read-only review of the frozen `GAME.unity` located the daytime chainsaw source
under `MAP/SoundAmbience/Day/Chainsaw`, the garbage-barrel fire/garbage trigger
objects and the water bucket/dipper trigger evidence. The inspected evidence was
used only as `BehavioralReference`; no donor FSM, script, controller or object
name became runtime authority.

The private `chainsaw.ogg` source is individually hash-locked as
`2828D6977693DF60DA88634C6C6EAD030E2B165CEDDF15F404327B47762398AB` and
classified `TemporaryDirectImport`. It is routed through project event ID
`audio.event.world.chainsaw` and `IAudioBackend`. Project-owned implementations
now provide explicit liquid source/receiver capabilities, persisted container
liquid state, one-way barrel ignition/consumption and rain/spill puddle
presentation. Focused EditMode validation passes `44/44`; manual audible and
in-world fidelity acceptance remains pending.

The subsequent screenshot-driven acceptance review found that the donor sauna
bucket wrapper still exposed its flat `Water` mesh because its former donor FSM
was intentionally absent. Read-only scene evidence records that child under
`ITEMS/water bucket(itemx)` at local position
`(-0.008300255, 0.0006999555, 0.119899996)` and scale `0.9`. The mesh is retained
as `TemporaryDirectImport` evidence but disabled at runtime by a project-owned
presentation adapter; no donor child name or FSM is runtime authority. The
replacement water level, physical stream fill volumes, HDRP-safe fire material
and world-anchored neutral puddles are `Reimplemented`. Focused validation after
these corrections passes `47/47`; manual visual acceptance remains pending.

### 2026-08-02 — Teimo bicycle route and private presentation

Read-only inspection of the locked scene identified `TeimoInBike`, Move and
SplineMove components `105851`/`105852`, path managers `106459`/`112267`, two
37-point waypoint chains and source speed `4 m/s`. The project records these as
`ConfigurationTransferred` plus `BehavioralReference`; donor PlayMaker states
and spline runtime code are not copied or executed.

The private Phase 1 wrapper selects only the reviewed rider, hat, clear glasses,
bicycle frame, pedals, tires, rims, motor-parts texture and greeting clip. Each
source payload has a locked GUID and SHA-256 in the character manifest. They are
`TemporaryDirectImport`, never `ProductionReady`, and remain replaceable without
changing Teimo's stable identity, route IDs or `npc.state` save schema.

The project-owned schedule and route simulation pass focused EditMode `15/15`
and PlayMode `2/2`, including mid-route save and streaming reconciliation. The
donor collision-to-ragdoll result is still absent and explicitly unverified.
Remounting or continuing on foot is a Phase 2 candidate, not a silent Phase 1
difference.

The first manual search found no bicycle rider. Audit showed that the initial
route build had treated donor-world spline points as active project positions,
placing the route roughly one kilometre away and assigning nonexistent
`cell_-2_2` through `cell_-4_2` owners. The reviewed manifest now declares the
M04A1 translation `(169.98, 1.611, -1040.625)` explicitly; the builder applies
it and validates all 74 resulting anchors against the real streaming manifest.
The corrected route occupies `cell_-2_0` and `cell_-3_0`, ending beside the
active store. The repeat manual comparison found Teimo on the bicycle and then
exposed three bounded presentation defects: static wheels/pedals, a uniform
vertical clearance and the absent greeting.

Further read-only inspection identified the project-safe evidence rather than
copying the donor controllers. `Speed/Rotate` FSM `106628` rotates pedals GO
`11102` and tires GO `10266`/`18450`; source speed is multiplied by `170` for
the tires and divided by `3.5` for the pedals. `Functions/Waving` FSM `106828`
waits for `Move.SeesPlayer`, compares against donor distance `10 m`, plays the
one-second `teimo_bicycle_waving_hello` clip and then idles. The locked tire
radius `0.347645 m` and hierarchy centre height `0.532 m` prove the reported
`0.184355 m` clearance.

Project importer revision 8 now generates a typed, serialized bicycle
presentation binding with direct transform references, exact distance-driven
rotation ratios, automatic tire-contact calibration and a build correction cap.
The same binding now resolves the locked left/right hip, knee and ankle chains
against crank targets derived from the actual rotating pedal transform. This is
a project-owned two-bone presentation solve because the locked donor payload has
no separate pedalling clip; no animation event or donor controller owns motion.
The project uses the user-calibrated `5 m` unobstructed greeting range and stores
next-day eligibility in the existing saved Teimo cooldown state. No donor FSM,
animation event, hierarchy lookup or source component ID is runtime authority.
Focused validation passes NPC EditMode `16/16` and PlayMode `3/3`; the revised
height, moving parts, leg solve and greeting await repeat in-world acceptance.

The later arrival audit identified the donor SWS curved-path flag and the exact
uniform Catmull-Rom expression in the read-only `WaypointManager` reference.
Only that isolated calculation is classified `CodePorted`; project routes,
schedules, transforms, stable IDs and streaming remain authoritative. Both
37-point bicycle routes now use arc-length traversal and analytic spline
tangents, eliminating waypoint snap-turns while retaining the source `4 m/s`
timing. The hash-locked `teimo_move_in` root curve contributes 17 timed arrival
anchors over `26.1` real seconds: Teimo dismounts at the shop, walks around the
building, pauses at the service door and reaches the existing counter anchor.

The six parked-bicycle renderers and service-door mesh are bound only through
their generated stable world IDs. The bicycle is hidden during home/travel
states and shown for arrival, shop wait, shop and pub blocks. Door OPEN/CLOSE
timing (`15.0`/`16.0` seconds), pivot and `85` degree travel are project-owned
presentation driven from authoritative route progress. Donor PlayMaker, HOTween,
Animation components and hierarchy names are not used at runtime. Arrival,
door motion, parked-bicycle visibility and obstacle fidelity remain manual
in-world comparison items.

The user conditionally accepted this bounded bicycle/arrival iteration on
2026-08-02 and authorized progression to the next roster package. This is a
progression receipt, not a final donor-fidelity verdict: greeting repetition,
collision-to-ragdoll and the final presentation comparison remain open, so the
rows stay `PartiallyImplemented`.

### 2026-08-02 — tilt spill, barrel-cavity burn and conditional ambience

Screenshot-driven acceptance established that the project-owned liquid surface
needed physical depth and tilt behavior, and that the initial HDRP flame origin
was occluded by the temporary donor barrel shell. The reviewed flat water insert
continues to serve only as mouth-plane calibration evidence. Runtime authority
is project-owned: saved litres drive a closed water volume, bucket/dipper tilt
drains those litres through a typed spill event, and the garbage barrel scans its
bounded hot cavity before marking eligible stable items consumed.

The reviewed daytime chainsaw identity remains a hash-locked
`TemporaryDirectImport` clip behind `audio.event.world.chainsaw`. Its new time,
weather, rarity and fixed near-home spatial policy is `Reimplemented`, not
claimed as transferred donor timing. NPC voice media remains replaceable behind
stable `audio.npc.*` events; the fallback loudness correction does not copy or
port donor code. Focused validation passes `51/51`; audible and visual donor
comparison remains pending.

### 2026-08-02 — complete `MAP/SoundAmbience` roster

The prior daytime-birds approximation was incomplete. Read-only inspection of
the frozen `GAME.unity` (SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`)
resolved the complete main ambience hierarchy and its donor phase boundaries:
Night 00:00-06:00, Morning 06:00-12:00, Day 12:00-18:00 and Evening
18:00-24:00. The roster is morning/day/evening/night birds, separate
morning/evening swamp birds, daytime meadow, distant dog and three lake-water
sources. The familiar owl-like "hu-hu", whistle and other calls are phrases
inside the complete `birds_*` recordings; the scene does not expose them as
separate ambience objects.

The frozen source positions, distances, hashes and the approved M04A1
translation `(169.98, 1.611, -1040.625)` are locked in
`Assets/Game/LegacyImport/Manifests/Phase1WorldAmbientManifest.json`. Eleven
project-owned spatial emitters now reproduce that roster behind stable event
IDs. Time/weather authority remains project-owned; donor PlayMaker FSMs,
mixers, hierarchy lookups and scripts were not transferred.

The same audit classified `wind_chime.ogg` under the inactive
`DINGONBIISI/Mover` source and mosquito/fly/wasp clips as local gameplay
sources. They are hash-locked and Wwise-mapped but deliberately not auto-started
as world ambience. The user-requested chainsaw policy remains an explicit
project correction rather than donor configuration: near-home, fair-weather,
08:00-18:30 and rare 15-35-minute attempts after a 5-15-minute initial delay.

Wwise validation passes with 65 events, exact `65/65` child routing and 43
embedded media; focused Audio Runtime EditMode passes `13/13`. Audible spatial
and mix acceptance still requires a real Editor/player session.

### 2026-08-02 — audio pause boundary and user-supplied Fire001 preset

This correction does not add donor behavior or donor presentation. World and
weather loop handles now follow the project-owned pause boundary immediately,
using `IGameTimeService` plus Unity time scale as the UI safety signal. The
existing ambient roster, weather mapping, stable audio event IDs and Wwise/
Unity backend boundary are unchanged.

`Fire001.unitypackage` is a user-supplied third-party presentation source, not
donor content. Package SHA-256 is
`1BF7B7B503BF2DD5B648C306E21230FBDC41D13610961F0A75D65EA30D8A1063`.
Only the six-layer particle prefab, three textures and retained third-party
notice were selected. Nova code, demo content and incompatible source materials
were rejected from the runtime import. Project HDRP materials wrap the selected
textures; item ignition, hot-cavity scanning, consumption and save identity
remain authoritative in project-owned code. Focused EditMode validation passes
`27/27`; manual visual/audio acceptance remains pending.

Follow-up visual acceptance found the third-party preset's finite burst window,
not donor or project item state, caused the repeated ignite/burn/extinguish
cycle. The project presentation adapter now converts the six selected layers to
continuous bounded emission and places a smaller effect inside the barrel's
upper cavity. The imported prefab remains unchanged, and no donor behavior,
stable identity or save field changed. Unity Editor and independent runtime/test
assembly compilation pass; after the shared Editor closed, the updated Fluid
EditMode visual regression passed `6/6`.

### 2026-08-03 — rain-puddle placement and neutral absorption

Low-angle traversal feedback showed a project presentation defect rather than
new donor evidence: the small player-relative seed ring recycled rain slots too
close to a running listener, and the cool absorption base read as painted blue.
`ProceduralPuddlePresenter` now prepares rain slots at 30-55 m, anchors them to
the world until 85 m, fades newly placed slots over four seconds and uses equal
neutral RGB channels. Typed local spill placement and authoritative persisted
`PuddleAmount01` are unchanged. Unity/Tundra and independent runtime/test
assembly compilation pass; after the shared Editor closed, the focused Fluid
EditMode regression passed `6/6`.
## 10B-R2 NPC donor audit — 2026-08-02

The locked `GAME.unity` scene and selected character source assets were inspected
read-only for 14 R2 identities. Exact converted actor/state anchors are now owned
by project IDs; donor hierarchy names remain provenance only.

| Feature | Donor evidence summary | Project result | Open evidence |
|---|---|---|---|
| P1.NPC.003 | Uncle Drinking/Home/Walking variants | One stable hidden identity; one review wrapper | Event gates and remaining variants |
| P1.NPC.004 | Active home actor plus inactive church variant | Exact home anchor and wrapper | Weekly church transition |
| P1.NPC.005 | KiljuBuyer, Hiker1 and Hiker2/Suicidal | One stable Jokke identity; buyer wrapper | Trade/story variant gates |
| P1.NPC.006 | Store-hiker Suski actor | Hidden stable identity and wrapper | Story activation/passenger flow |
| P1.NPC.009–013 | Five distinct ShitMan actors with distinct material combinations | Five stable customers and five exact anchors/wrappers | Septic job activation/payment |
| P1.NPC.014 | WoodsMan actor | Stable exact-anchor wrapper | Firewood call/delivery gate |
| P1.NPC.015 | InspectionMan actor | Stable exact-anchor wrapper | Opening hours and inspection service |
| P1.NPC.016 | WaterFacilityMan actor with clear glasses | Stable exact-anchor wrapper | Facility hours/transaction |
| P1.NPC.017 | VenttiPig actor with latsa and dark glasses | Stable exact-anchor wrapper | Ventti schedule/minigame/house state |
| P1.NPC.101 | `HouseDrunk/LOD/Wife/Point`, Lamore and wife1..6 audio; no physical body in the locked scene | Explicit saved `StateOnly` identity; no fabricated wrapper | Story-event and audio mapping |

The thirteen physical wrappers are `TemporaryDirectImport`. Their project-owned
controllers, stable IDs, schedules and save state never execute donor scripts,
FSMs, controllers or legacy runtime assemblies. The R2 review-availability
blocks are deliberately provisional and are not donor schedule evidence.

## 10B-R3 story-traffic audit — 2026-08-02

Read-only inspection of the locked `GAME.unity` source identified the complete
KYLAJANI and AMIS2 presentation roots, their driver skinned renderers, four
wheel transforms per car and KYLAJANI's passenger anchor. User comparison
identifies the two drivers as Jani and Petteri and the initial KYLAJANI
passenger as Suski; the latter therefore reuses `P1.NPC.006` rather than
creating a duplicate persistent identity for `P1.NPC.103`.

The BetterMSC AssetRipper export was inspected for the user-required Suski
source. It contains the `Suski.prefab`, mesh/material/texture closure,
`SuskiAvatar.asset` and `Suski_car_sit.anim`, but no managed BetterMSC assembly
or source code was present in the staged export or donor installation. The
model and car-sit presentation are `TemporaryDirectImport`; the passenger to
post-rescue state switch is consequently a project-owned `Reimplemented`
boundary, not claimed as code-ported BetterMSC logic.

The imported BetterMSC avatar cannot safely remain a runtime humanoid Avatar in
Unity 6. Direct Editor sampling through that imported Avatar crashes inside
native Mecanim while applying the old hand-IK goals, despite Unity reporting the
AssetRipper-reconstructed asset as valid and humanoid. The Editor importer keeps
the source hash as provenance, reads the locked Avatar's serialized 43-node
human T-pose, restores the Biped root and all 42 mapped body/finger transforms,
then builds a temporary compatibility Avatar. It transfers only the ordinary
muscle/root curves from the locked car-sit clip, bakes a legacy transform-pose
clip and removes the temporary Animator/Avatar.
No donor or BetterMSC controller, script, animation event, FSM, shader or
vehicle-physics component enters runtime.

Jani and Petteri currently use separate, smooth project-owned looping review
routes so their complete car/driver presentation can be inspected through the
existing streaming runtime. These are not donor-exact traffic/story schedules.
Crash detection, damage, rivalry, rescue trigger conditions, recovery and audio
remain owned by later vehicle/story work; only the stable identity,
presentation, route/save foundation and persisted Suski state transition are
implemented here.

### 2026-08-03 NPC context and story-traffic correction

Read-only component/transform evidence from the locked `GAME.unity` adds the
missing seated context: Grandmother's garden chair/table, coffee cup/plate and
conditional product tray; Jokke's plastic chair/table and `terrace_shade`;
one held beer, chair and original right-hand drinking clip for each of the five
septic customers; and Livaloinen's held vodka bottle. A project-owned looping
presentation layer staggers and restarts the five hand clips across streaming
disable/enable cycles. Donor scripts and job/drinking state machines remain
excluded. The product tray is controlled by
the project flag `flag.grandmother.products-ordered`; actual order-flow
ownership and a persistent drinkable coffee instance remain open.

The same correction restores the Mummola neighboring shed and remediates the
reviewed Jokke house facade plus adjacent HouseDrunk shed walls, windows and
wooden doors/trim as local double-sided renderers. Vehicle wheel bounds provide
deterministic vertical corrections for both story cars. The v5 Suski pose was
baked from the BetterMSC Avatar's serialized human T-pose and locked sit clip
without its incompatible IK goals, then vertically aligned to what was believed
to be a donor head-height reference. The 2026-08-04 audit below proves that
reference was only the tail/detail-renderer bounds and supersedes the v5 seat
calibration. The original `RedHot`, tail and two cigarette renderers remain
explicitly excluded from runtime.

### 2026-08-04 BetterMSC Suski seat/neck correction

Read-only comparison of `SuskiAvatar.asset`, `Suski.prefab` and the locked
KYLAJANI passenger skeleton identified two incorrect assumptions in the v5
importer. BetterMSC's humanoid Avatar maps its human head to the intermediate
`HeadPivot`; `Bip01 Head` is a fixed mesh bone below that pivot. Applying the
serialized human-head transform to `Bip01 Head` left the original pivot active
and doubled the head-chain transform. Also, the former `-0.59 m` seat correction
was derived from the only imported passenger MeshRenderers, which are the tail
and cigarette details rather than the donor body.

Manifest v7 restores the serialized human pose to `HeadPivot`, uses the same
source-compatible Avatar mapping for muscle transfer, and retains `Bip01 Head`'s
source zero local offset. Before removing the original passenger hierarchy, the
Editor importer now includes and records the locked `pelvis`, `ankle_left` and
`ankle_right` transform IDs `69311`, `62678` and `53657`. The replacement is
yaw-aligned to the donor pelvis-to-feet direction and then translated to the
donor pelvis, so direction and seat height no longer depend on a visual-detail
mesh. The audited passenger-local pelvis is
`(-0.024782, -0.021795, 0.000021)` and the projected pelvis-to-ankles direction
is `(0.999943, 0, 0.010716)`. New import/test constraints cover the two-stage
head chain, pelvis height and forward direction. The importer now transactionally
restores the previous generated directory and catalog file after a failed build;
this rollback was exercised by the initial v7 validation failure. The corrected
v7 Unity batch rebuild passes, produces both non-null traffic wrapper references,
and the complete NPC EditMode and PlayMode suites pass 16/16 and 4/4. No donor
files were written. The user manually accepted Suski's corrected seat height,
facing direction, neck and arm pose in Play Mode on 2026-08-04.

### 2026-08-03 base-map Terrain migration audit

The requested `3Buildings.unity` / `2BasicMap.unity` sources are absent. The
audited source is the already sanitized canonical `World_Global_Legacy.unity`
plus its 50 sibling legacy cells, filtered by donor baseline/supplemental
metadata under `TEMPORARY_DIRECT_IMPORT_ENTITIES`. A deterministic inventory
found 2,606 renderer instances, including seven base-ground candidates and 15
road/road-structure instances. The source scene SHA-256 remained
`a4b929ee74b95778f44599e26b081a70e20edcd494f929de7f624c2c632f2322`
before and after execution.

The isolated project-owned Editor pipeline exports provenance-rich OBJ/MTL
because the optional Unity FBX Exporter is absent, then writes a separate 72-tile
Terrain scene. It does not transfer donor scripts, FSMs or runtime authority.
Seven base-ground renderers are disabled only in the output scene; 2,528 folded
or multi-height ground triangles remain as replaceable residual meshes.
Classification is `Reimplemented` for tooling and `TemporaryDirectImport` for
the private Phase 1 presentation source. Full details and executed evidence are
in `Docs/WorldRemaster/MAP_TERRAIN_MIGRATION_TOOLING.md`.

### 2026-08-04 locked traffic-route audit

The canonical locked `GAME.unity` scene (SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`)
contains distinct route owners for Highway (1,889 transforms), BusRoute
(1,084), DirtRoad (3,719), Village (295), HomeRoad (462), Dancehall (269),
Dragrace (166), RoadRace (624), Trackfield (291), two eight-point boat routes
and two train endpoint pairs. The Editor-only route reader requires the locked
hash, unique hierarchy paths, exact counts and contiguous numeric waypoint
names. It transfers only transforms/configuration through the established
legacy-world translation; donor traffic components and PlayMaker graphs are
not instantiated.

Jani and Petteri now use their exact positions under locked formation transform
61904 in Perajarvi from 16:00. Jani/KYLAJANI transform 43020 resolves to
`(-1173.1444, 3.3897, 123.3123)` and Petteri/AMIS2 transform 48255 resolves to
`(-1176.1509, 3.4643, 128.1752)` in project coordinates. Their heading matches
decreasing Village indices. A once-only Village -> RoadRace departure follows
the measured Village 22 / RoadRace 622 (0.339 m) and RoadRace 248 / Highway 464
(0.035 m) joins before both cars enter the reverse-direction Highway loop;
Petteri's source offset remains behind/lateral to Jani.
Jani's inspected donor FSM/configuration evidence includes `SpeedMin=115`,
`SpeedMax=185`, `ThrottleAccel=1`, `ThrottleCruise=0.1`, shifting RPM values,
burnout/handbrake states, and NavigationAI raycast/passing/braking states with
lane and pass-speed variables. The donor therefore uses physics-controlled
high-speed road behavior rather than a simple transform follower. R1 transfers
the route and provides bounded project-owned obstacle/audio presentation only;
speed choice, passing, lane selection, drift and recovery remain 11B-R2 work.
Detailed validation and limitations are recorded in
`Docs/Milestones/MILESTONE_11B_R1_REPORT.md`.

### 2026-08-04 story-traffic maneuver transfer

R2A reimplements the inspected passing-state vocabulary without transferring
the donor PlayMaker graphs. Materialized Jani/Petteri cars choose a stable speed
inside the donor 115–185 km/h interval and use project-owned cruise, brake,
pass-out, pass, return and reverse states. Obstacle probing includes stopping
distance and adjacent-lane clearance. The logical NPC route remains off-screen
authority, while a loaded physical vehicle cell now prevents presentation
removal caused solely by logical-route lead. Automated evidence is recorded in
`Docs/Milestones/MILESTONE_11B_R2_REPORT.md`; drift, crash/recovery and story
consequences are not claimed.

### 2026-08-04 story-traffic incident and spatial-audio transfer

R2B additionally locks the actual Jani `CollisionEvent -> CRASH` transition,
`DeathSpeedMPS=5` value and separate handbrake evidence. Project-owned drift,
crash and recovery states now extend the R2A cruise/pass/return/reverse driver.
Obstacle rays are planning evidence only; a typed physical-contact incident is
required to cross the 5 m/s crash threshold. Recovery returns at bounded speed
toward the last safe road pose without a catch-up teleport.

The previously substituted mid-Highway anchors 1511/1510 are superseded by the
exact formation transforms above. Project-owned schedule blocks make the
Perajarvi departure once-only, then hand off without a positional discontinuity
to the existing Highway loop. Existing driver/car/home stable IDs and the
`npc.state` schema remain unchanged.

The donor Jani/Petteri engine clips, one `Amistekno` tracker track, skid and
crash clips are selected through five exact hashes and classified
`TemporaryDirectImport`. Donor source radii of 400/700 m were audited but not
copied: project-owned 3D fallback limits engine/music/skid/crash to
65/55/45/80 m. Persistent per-car owners retain loop/music handles and
emitter-scoped RPM across wrapper streaming. The same stable IDs and RTPCs are
reserved for Wwise, but the seven R2B Wwise events/bank payload remain an
explicit authoring task. Durable damage and story consequences are not claimed.

### 2026-08-05 physical story-traffic donor audit

The locked donor scene confirms that Jani/Petteri's time/day logic gates the
encounter; it is not a position timeline. Reviewed Jani FSM values include
`SpeedMin=115`, `SpeedMax=185`, `ThrottleAccel=1`, `ThrottleCruise=0.1` and
`DeathSpeedMPS=5`. GlobalDay values 3/5/6/7 and the `16:00-02:00` window map to
the project's Wednesday/Friday/Saturday/Sunday schedule mask. Loaded-car route
progress is therefore now derived from physical chassis projection, while the
schedule only selects visible/active state.

The project uses the exact locked Perajarvi formation and measured
Village/RoadRace/Highway joins for a full same-direction return loop. Donor
mass, driven axle, ratios, final drive, suspension, steering, brake and
handbrake configuration are transferred into project-owned configs. The NWH
Vehicle Physics 2 v13.5 package is a user-authorized licensed third-party wheel
contact backend (SHA-256
`171FB37CD3BC0D62C7F6C05762AB362B4BF02549FE048C810485AE513354D817`),
not donor content or gameplay authority. Donor controllers, FSMs, scripts,
assemblies and physics components remain excluded.

The 16:04 regression proved why schedule time cannot initialize physical route
distance: the previous first evaluation converted four minutes since activation
into Highway progress. Physical story drivers are now registered before that
evaluation. A new encounter begins at the locked Perajarvi formation regardless
of how far inside the active window the session starts; loaded progress is
chassis-derived, unloaded progress advances by elapsed delta, and saved progress
remains authoritative on restore.

A production-Bootstrap reproduction then isolated the stationary rear-wheel-spin
failure to project integration rather than NWH or donor configuration. Dialogue
authoring was attaching a humanoid `CapsuleCollider` to the car wrapper; the
capsule supported the Rigidbody above the road while the rear wheels spun. Story
cars now retain only their authored chassis interaction collider. Continuous
clock reconciliation also no longer overwrites a materialized physical driver's
look-ahead with a logical route pose; logical placement is restricted to
materialization, route transitions and explicit restore discontinuities.

The same Bootstrap run isolated traffic-audio silence to two project-owned
routing errors. The Phase 1 clips exist only in the removable Unity supplemental
library, so they are now sent to that spatial fallback until matching Wwise bank
media is authored. `VehicleAudioEmitterBackend` is the sole owner of emitter
registration, eliminating the previous duplicate registration that left both
presenters uninitialized. This does not claim a final Wwise mix.

Automated evidence passes NWH EditMode `4/4`, physical generated-car PlayMode
`5/5`, NPC EditMode `19/19`, NPC PlayMode `8/8` and shared vehicle-simulation
EditMode `29/29`. The new paired fixture proves both cars launch from their exact
relative Perajarvi formation without mutual deadlock. The obstacle fixture proves
a 3 m cube is cleared without a pose teleport (`0.310 m` maximum observed
Rigidbody step). Full-road behavior and final audio feel remain manual acceptance
items; exact social-stop triggering, durable damage and story consequences are
not claimed.

An additional full production-Bootstrap PlayMode regression passes `1/1`: from
Friday 16:04 Jani moved `4.19 m` and Petteri `3.03 m` in the bounded observation
window, both reported wheel contact, and both persistent audio owners initialized.
The Unity fallback audio suite passes `8/8`, including local RPM-driven
story-traffic engine playback.

### 2026-08-05 exact Amis route and durable rescue correction

The previous Village/RoadRace/Highway composition was re-audited against the
serialized Navigation state rather than inferred from nearest route joins.
`Highway` is not selected by the Jani/Petteri Navigation owner. The locked
sequence is TrackField `228..290`, Village `0..294`, RoadRace `0..623`,
TrackField `0..201`, followed by seven TrackField `73..201` circuits (eight
requested circuits total). The separate Saturday branch is Dancehall
`0..268` plus its reverse return. Generated driver-specific race and dancehall
routes now preserve these ordered transforms exactly; the earlier Highway
claim is superseded.

The locked crash evidence also includes separate Jani/Petteri CrashEvent
owners and Jani `DeathSpeedMPS=5`. The project now reimplements a terminal
physical hold and a dedicated, optional `traffic.state` domain rather than
treating every crash as transient presentation recovery. The domain stores
stable driver identities, incident count/speed/time and wreck transforms.
Jani's first terminal crash moves the existing stable Suski identity through
project-owned `Passenger -> AwaitingPickup -> Transporting ->
RestingAtParentsBed -> Rescued` stages. The rescue destination is the stable
`anchor.story.suski-rescue-bed`, measured from donor bed visual
`legacy-world:598d32f0a2b76eb89a6ab8cf648745d0`. Carry interaction and sleep
completion are project-owned reimplementations; the donor PlayMaker graphs are
not transferred.

The current 16:04/35 m encounter start remains an explicit temporary trigger.
Donor evidence requires the nearby current Satsuma/rev challenge and that
binding is deferred only until the player car domain exposes a stable rev
event. It must not be promoted to parity evidence.

### 2026-08-06 ambient Highway traffic audit

Hash-locked inspection of `GAME.unity` confirms exactly ten direct children
under donor `VehiclesHighway`: Truck, Svoboda, two Lamore, three Victro,
Menace, Fittan and Polsa. Their source transforms and multiplicities are
configuration evidence; their donor controllers and physics are excluded.
Menace records a 10 percent weekday and 40 percent weekend appearance chance.

Eight selected road owners contribute 7,715 exact waypoint transforms to the
project-owned T1 catalog. Known serialized joins Village 22/RoadRace 622 and
RoadRace 248/Highway 464 are retained; other graph connections are bounded
nearest donor points below three metres and remain subject to manual route
intent comparison. Runtime evaluates the exact polyline rather than inventing
a smoothing spline that could cut a corner.

The seven sanitized presentation archetypes are `TemporaryDirectImport` and
contain only selected renderers, materials, wheel transforms and project-owned
Rigidbody/NWH configuration. Logical traffic, probability, residency, save and
control authority are all reimplemented. The generated build report and tests
prove catalog/multiplicity/save composition, but do not yet prove subjective
original-game density, every collision response or final audio mix.

### 2026-08-06 bus, train and AI-boat audit

The locked scene contains `BusRoute` with 1,084 contiguous transforms, exact
stop distances at Loppe (`61.392963 m`), Kesseli (`2038.7765 m`) and
Rykipohja (`3598.2095 m`), and a two-hour departure cycle starting at route
indices 3/890/482. The cycle repeats across all 24 hours. The actor root used
for sanitized presentation is `BUS` transform 65181.

Train evidence consists of separate east-to-west and west-to-east endpoint
records, `30 m/s` travel speed and a `250` simulation-second endpoint wait.
Presentation is sourced from wrapper transform 55611. The lake traffic evidence
is two independent eight-point loops owned by `AIboat1` transform 57586 and
`AIboat2` transform 48730.

These values and transforms are `ConfigurationTransferred` or
`WorldLayoutReference`. Runtime scheduling, logical/physical streaming handoff,
Rigidbody/NWH controls, save state and passenger interaction are
`Reimplemented`; four sanitized renderer wrappers are `TemporaryDirectImport`.
No donor traffic component, FSM, controller or runtime assembly is transferred.

Automated evidence validates catalog counts, exact schedule/route constants,
stable save composition and wrapper closure. It does not yet establish exact
journey timing, passenger/ticket/door behavior, train hazard consequences,
boat force calibration, dedicated audio or representative multi-hour behavior.

### 2026-08-06 economy foundation audit

The locked globals asset contains `PlayerMoney` with value `3000`. The locked
`GAME.unity` Teimo store `Prices` reference contains 38 matching key/value
entries. Representative item price FSM data exposes `Inflationrate = 0.054`,
`PriceMultiplier = 1` and `RestockDay = 4`; comparison with the global weekday
state establishes the Thursday additive update.

Both source files are read only through an Editor evidence reader and rejected
if their SHA-256 differs from the Phase 1 lock. Values and the price rule are
`ConfigurationTransferred` / `BehavioralReference`; the catalog, fixed-point
wallet, transaction ledger, clock subscription, save participant/migration and
HUD provider are `Reimplemented`. Donor PlayMaker actions, FSMs, runtime
assemblies and variable-name lookup do not enter the player build.

### 2026-08-06 Jani/Petteri Perajarvi handbrake and streaming correction

Read-only reinspection of locked `GAME.unity` confirms four explicit Jani
navigation objects named `HandbrakeZone0` through `HandbrakeZone3`. Each uses a
12 m trigger distance. Their donor positions are `(-1578.8, 2.9, 1169.7)`,
`(-1519.93, 3.35, 1183.96)`, `(-1567.7, 4.0, 1282.5)` and
`(-1529.82, 2.9, 1233.17)`. Applying the established source-to-project
translation `(169.98, 1.611, -1040.625)` produces project positions
`(-1408.82, 4.511, 129.075)`, `(-1349.95, 4.961, 143.335)`,
`(-1397.72, 5.611, 241.875)` and `(-1359.84, 4.511, 192.545)`.

This evidence supersedes the provisional assumption that drifting is disabled
throughout Perajarvi. Town travel is calmer than the highway, but the donor
requests brief handbrake behavior at those four authored corners. The separate
normal-brake intersection is not classified as a drift trigger. The donor
throttle state retains `SpeedMin=115` and `SpeedMax=185` for the high-speed
RoadRace behavior; these values are not applied wholesale to Perajarvi or the
later TrackField circuits.

The project route retains the exact segment boundaries: indices `0..358` are
the formation/TrackField tail plus Village (Perajarvi), `359..982` are
RoadRace, and `983..2087` are the repeated TrackField section. Cumulative
waypoint progress is serialized so physical route projection, streaming and
save/load select the same profile at those boundaries. Physical wrappers are
created at retained logical poses and a separation above 80 m is treated as an
invalid bootstrap/handoff pose instead of new route authority. This is a
project-owned runtime correction; no donor controller or FSM was transferred.

### 2026-08-08 traffic residency and physical-control correction

The donor root evidence above remains the behavioral reference: inactive roots
freeze rather than analytically catching up. The project now uses an explicit
performance/story divergence approved by the user. Ordinary actors in the
selected root advance project-owned route progress by real runtime seconds when
not physically resident. They materialize at `440 m`, are retained through
`520 m`, and share a retained-first cap of six wrappers. Logical offscreen
traffic cannot collide because no physical contact simulation exists there;
game-clock jumps still do not move it.

Pena's ordinary context and the separately owned Saturday context do not enter
that ordinary cap. Jani, Petteri, the bus and Pena remain physical regardless
of player distance so streaming cannot erase a race accident or other
story-relevant outcome. This residency choice is `Reimplemented`, not donor
configuration, and does not merge the mutually exclusive Pena contexts.

The exact 2,088-point Jani/Petteri race route now uses linear evaluation of its
already dense donor samples. Project control tuning shortens look-ahead to
`7..10 m` inside the four locked Teimo handbrake zones and begins an
anticipatory speed cap approximately `245 m` before RoadRace exit. Bus control
uses the locked `60 km/h` lower band and `82.5 km/h` cruise ceiling while
generic wander and drift are disabled. These changes are implemented with
automated validation passed (`79/79` combined EditMode, `4/4` focused physical
story PlayMode and `1/1` long traffic/bus PlayMode); manual full-route and
service acceptance remains open.

### 2026-08-08 hybrid Enviro / native HDRP environment audit

Read-only inspection of the locked `GAME.unity`, the normalized
`WorldInteriorManifest.csv`, current Bootstrap composition, Enviro 3 package,
HDRP 17.3, and active Wwise project identified a double-writer risk for native
HDRP Fog and Exposure. The safe migration preserves the accepted project-owned
time, calendar, weather schedule, wetness, gameplay lightning, and save domain.
Enviro remains a presentation backend for sky, clouds, sun/moon visuals and
precipitation effects. A single project bridge now owns the active native HDRP
Fog, Exposure, indirect-light multiplier and light volumetric dimmers.

Donor interior paths are classified `WorldLayoutReference` and are recorded in
`Assets/Documentation/Environment/Weather_Zone_Coverage.md`. They are not used
as runtime lookups. Migration v1 safely converted the existing measured
player-house and garage bounds. Migration v2 additionally transferred reviewed
local mesh AABBs and root transforms for Teimo shop/pub and Fleetari workshop.
Migration v3 expands the project-owned additive-cell catalog to 15 streamed
volumes across 12 logical locations using reviewed mesh, collider, placement,
and donor `NoRain` evidence. Together with the static house and garage this is
17 volumes across 14 logical locations, classified
`DimensionalReference;WorldLayoutReference;ConfigurationTransferred` and
reimplemented without donor hierarchy lookup. The generated donor scenes remain
unchanged. Buildings without reliable accessibility/bounds evidence, most real
door/window portals and vehicle cabins remain explicitly pending rather than
being inferred from noisy `interior` names. No donor, Enviro vendor, KWS or
water file was modified.

Teimo's existing project-owned service-door schedule/pivot now supplies one
continuous `WeatherPortal`; weather code does not own or animate that door.
Fleetari's donor doors/garage door have no project-owned state provider after
donor FSM removal, so they remain closed-zone presentation rather than being
falsely exposed as dynamic portals.

The migration is `Reimplemented`. V1 focused automated validation passed: 16/16
EditMode, 1/1 hybrid lifecycle PlayMode, 1/1 existing production lifecycle
PlayMode. V3 passes 17/17 focused EditMode, both expanded streamed-building/hybrid
PlayMode tests, and both static validators with zero errors or warnings. A wider
order-dependent lifecycle invocation still exposes six pre-existing Bootstrap
startup-race failures and is documented rather than hidden. Wwise RTPC,
Room/Portal and SoundBank authoring plus manual visual/audio acceptance remain
open, so this work is not classified `Verified` or `ProductionReady`.

### 2026-08-09 exposure-range and storm-particle correction

The reported 16:17 white clipping and 03:27 near-black frame were traced to the
hybrid native bridge's constant `10 EV` exposure. Enviro's accepted lighting
changes by orders of magnitude over the day, so one constant could not cover
both ends. Migration v8 replaces it with a project-clock curve: `12.5 EV` full
daylight, `7.25 EV` from 23:00 through 03:00, and smooth dawn/dusk ramps.
Weather compensation and ambient-darkness correction remain bounded inside
`-1..14 EV`. The owner remains camera-independent Fixed Exposure; no histogram,
view luminance or donor camera logic becomes authoritative.

The reported thunderstorm drop to 15-20 FPS correlated with a runtime-clone
rain configuration of `12,000 particles/s`, High-quality 3D collision against
all layers, dynamic colliders and `512` collision shapes, with a splash emitted
for every collision. The correction retains sixfold scaling below the ceiling
but caps emission and live drops at `8,000`; splash particles are capped at
`512` and sampled at `20%`. Collision is Low quality, static-only, uses `128`
shapes, and targets only project world layers. Drop/splash size scaling remains
the accepted smaller proportional range. Enviro vendor files are unchanged.

This correction is `Reimplemented`, not donor configuration. Focused EditMode
tests pass `28/28`; focused Bootstrap ownership/presentation PlayMode tests pass
`2/2`. Structural budgets are proven, but exact restored storm FPS and final
day/night appearance require a matched real-GPU manual capture.

### 2026-08-09 bus terminal-stall and driver-abandonment audit

Read-only inspection of the locked `GAME.unity` (`SHA-256
c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`)
identified the donor `DrivingIssue` owner on the bus (`GameObject 29134`), its
route owner (`GameObject 19479`), seated driver (`GameObject 29782`, Transform
`65832`), inactive Latanen walker (`GameObject 3698`, Transform `39756`) and
`CarLightsAI 28133`. The donor arms the failure detector only after speed
exceeds `5 m/s`; a continuous `25` real seconds at or below `2 m/s` triggers
the terminal sequence. It opens the service door over one real second to
absolute local position `(0,-0.606,0)` and quaternion
`(0,0,-0.66568637,0.7462316)`. Four real seconds after shutdown, the seated
driver is hidden, dead-light presentation replaces the driving lights, the
route owner is disabled, and the walker leaves the bus hierarchy. Donor media
evidence is `fat_walk.anim` SHA-256
`20945aced2971b6d993eb6894316dd9fad5a8cf58d8d0ff78519d7ec0df5297a`,
`fat_standing.anim` SHA-256
`10ba3db5ca038cc2cf55f5dbf48cc1c96ebdaff8a51f3d3e13173e6aac59aabd`,
and `vitunvittu.audioclip` / `.resS` SHA-256
`298c4bf2fafba4c91a942a817fe40e5733377ea79b7d0091d18e72422baab1d7` /
`07bbad6518a00074cb92d56326480589e0a47d1b42007b9909b3a6744c0951ad`.

The project reimplementation keeps the donor thresholds and real-time delays,
but adapts them to the existing bounded reverse-recovery controller. Reverse
motion alone cannot clear the detector: five metres of meaningful forward
route progress resets it, while three recovery attempts without such progress
exhaust recovery and allow terminal confirmation. Scheduled dwell and explicit
story/social/service holds are excluded. The resulting bus, phase, driver pose
and bark cooldown are stored in `traffic.state`; this is an intentional
project persistence extension because the donor sequence itself did not expose
a corresponding save contract.

All runtime references are serialized by project importers. No donor hierarchy
or filename lookup is used in play mode. `character.fixture.vehicle-linked` is
state-only and has no NPC schedule, preventing a second fixed Latanen instance
from competing with the bus-owned walker. A compatibility bit allows the
pre-existing generated bus wrapper to boot and continue its route before the
deterministic presentation importer is rerun; terminal abandonment remains
disabled on that stale wrapper rather than partially materializing. Full
walking uses the donor-compatible generic clip at `1.2 m/s`; path selection is
a bounded project presenter, not donor navigation/FSM code. Source assemblies
and focused test assemblies compile with zero errors; Unity importer execution,
generated-wrapper validation, audio playback and end-to-end manual acceptance
remain pending.

### 2026-08-10 story-traffic implementation and importer closure

The Jani/Petteri donor route evidence above remains authoritative. The project
reimplementation now resolves the directed H2 inspection corridor and the
TrackField approach, oval, closure and terminal portions by route progress.
Teimo dwell is a saved one-shot (`6 s` Jani at story `96`; `8 s` Petteri at
story `94`), passing is bounded to `6` real seconds or `55 m`, the physical
handbrake pulse lasts `0.88 s`, and the terminal donor FULLSTOP formation is
preserved. These controls are `Reimplemented`; no donor navigation component,
FSM, controller or transform animation was transferred.

The audited bus terminal-stall sequence is now present in the generated private
wrapper. It retains the donor arm/stall thresholds, `1 s` service-door motion,
`+4 s` driver transition, bus-owned Latanen walker and curse presentation.
Intermediate and terminal phases plus bus/walker poses are persisted as a
project extension. The vehicle-linked Latanen definition remains state-only and
its materializing fixture schedule was removed, preventing a duplicate NPC.

The character manifest now targets donor skeleton root `37455` for
`fat_standing`/`fat_walk`. Transport import converts service-door position and
rotation only for the bus; non-bus transports no longer require bus-only data.
The full deterministic importer chain completed with exit code `0`. The red
Fittan town excursion separately uses project-owned nearest directed joins for
TownLoop and GasPump; measured entry/return gaps are all below `1.7 m`.

Executed automated evidence is NPC EditMode `33/33`, Traffic EditMode `48/48`,
NPC PlayMode `12/12`, NWH story-traffic PlayMode `3/3`, long Traffic PlayMode
`1/1` in `158.56 s`, in-car ragdoll `1/1` and authoritative audio-pose `1/1`.
The implementation remains `PartiallyImplemented` pending manual populated-world
route, abandonment, handling and audio acceptance; temporary donor presentation
remains `TemporaryDirectImport`, not `ProductionReady`.

### 2026-08-10 conditional acceptance decision

The user conditionally accepted this NPC/story-traffic implementation as the
Phase 1 compatibility baseline. Existing project-owned behavior, identities,
save state and generated bindings are therefore preserved through subsequent
work. The acceptance is deliberately conditional: it does not upgrade donor
presentation to `ProductionReady`, does not mark every parity row `Verified`
and does not independently approve the Phase 2 gate. Further handling, route,
audio, animation and presentation refinement is recorded as Phase 2 remaster
work.

### 2026-08-10 player traversal and BetterMSC lean correction

Read-only comparison of the locked `GAME.unity` (SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`),
staged `CharacterMotor.cs` (SHA-256
`fbe413dd246120ecd65b88d440e057e49ae3123d72524f4b36ad1aa6fc63644a`)
and installed `BetterMSC.dll` (SHA-256
`20410adbb59cf7a3252103c36301a91ca8f9c83ae3cc9d1e4f8de0bf0d17e6f8`)
identified two concrete remake defects. The remake capsule was `0.64 m` wide
versus the donor's exact `0.24 m`, and deep crouch reduced its step offset to
zero. Forward lean also used a hinge at local `Y = 1.1 m` with a `0.3 m`
camera arm. Exact `GAME.unity` hierarchy inspection shows donor `PLAYER/Pivot`
at `Y = -0.3 m` with the camera `1.7 m` above it. The remake therefore tilted
the head/neck instead of bending the body and did not match the donor or
BetterMSC behavior. Its release branch also assigned a lower target angle
immediately, which made key release snap to zero.

The project-owned correction transfers the exact donor capsule configuration
(`0.5/0.12/0.4/0.03 m`, `90 degrees`, zero minimum move distance), keeps it
constant across posture, projects motion onto the ground normal and applies a
bounded ground snap. A separated view rig keeps `LeanPivot` at donor local
`Y = -0.3 m`, moves posture through `CameraPivot`, and gives the standing lean
a `1.7 m` body arc. The `40 degree` entry is project-tuned to `150 degree/s`;
release remains the BetterMSC-evidenced smooth `120 degree/s`. A `0.11 m`
sphere limits the arc. The wall impact gate preserves the `>3 m/s` forward
speed and `<30 degree` facing-normal conditions, then emits a project event,
camera kick and a short project-owned eyelid response. A clean presentation
adapter posts the already-authored `audio.event.interaction.impact` through
`IAudioBackend` and the shared player emitter. No donor or mod runtime code,
DLL, audio file, intoxication mutation, `SleepEyes` asset or NPC reaction logic
was transferred.

Focused Player/Interaction validation passes EditMode `29/29` and PlayMode
`12/12`. The updated
locomotion fixture passes `6/6`, including a `0.36 m` opening, `0.28 m`
threshold, wall-clamped head sphere, fixed body hinge, smooth return and running
lean impact/eyelid feedback. Audio player-integration PlayMode passes `2/2`,
including exact typed impact-event delivery. The real M4 garage/house route was
regenerated against the corrected prefab and passes `1/1` in `27.447 s`.
Active donor-world pit and irregular-wall acceptance remain manual. Full
evidence and compatibility notes are in
`Docs/Player/PLAYER_TRAVERSAL_AND_LEAN_AUDIT_2026-08-10.md`.

### 2026-08-11 Teimo commerce, fuel canister and catalog correction

Read-only inspection of the locked `GAME.unity` recovered 41 individual store
offer coordinates, five distinct pub order buttons, three fuel-pump anchors,
Fleetari's brochure anchor, the 46-entry home parts catalog pivot, Teimo's
fridge/microwave entity IDs and the store/pub movement endpoints. Selected
Teimo animation clips were hash-checked and recorded individually in
`Phase1CharacterPresentationManifest.json`.

The project-owned runtime now materializes those interaction surfaces without
donor hierarchy lookup. Store products enter the basket at their shelves; all
five pub choices are reachable; physical stable nozzles fill only compatible
open `ILiquidContainerTarget` receivers; Fleetari's full selection is
save-backed; and the home form exposes all captured entries without pretending
mail delivery exists. Gasoline and diesel now use the exact liquid identities
already authored by the item domain.

Teimo's idle/working/talking state uses the hash-locked
`teimo_lean_table_in` clip and clamps its final counter pose. Register, drink,
anger, kitchen and food-handoff clips are explicit presentation actions. A
middle-finger aim check or an actual urine particle collision latches the angry
pose until a service action or authoritative schedule/state change replaces it.
The store-to-pub change follows the four root-position keys in
`teimo_move_bar`: two reviewed intermediate anchors and an explicit
`AuthoredHeight` route mode keep the actor on the floor and route him around the
counter/wall during the 57 game-second transition
under the locked 12x clock. A paid meal remains in the service
handoff journal while presentation walks Teimo through the audited fridge and
microwave sequence. Project navigation follows the exact
`teimo_move_kitchen_in` positions while walking/hand-out clips animate only the
skeleton. The complete meal presentation is reparented between donor
`hand_right` and the exact microwave marker; ordinary pub goods use donor
`hand_left`. The final authoritative handoff uses the donor `FoodSpawnPoint` on
the counter rather than the cash register;
`ServiceRuntime` and `ItemWorldRuntime` remain authoritative.

The 41 store offer fixtures now bind reviewed shelf-group stable IDs. Their
interaction boxes are fitted to full rendered product bounds after streaming,
and visible product units follow authoritative remaining stock. The sanitized
item presentation plan grew from 46 to 80 bindings: every shop food/automotive
product, the pub meal and 13 indexed paint cans now produce reviewed item
wrappers instead of cubes. `P1.ITEM.132` gained the corresponding 13 valid
runtime variants so checkout state and presentation agree.

The service builder and full NPC builder both completed with exit code `0`.
The latest combined Services/NPC EditMode run passes `83/83`, and focused Teimo
root-motion PlayMode passes `1/1` after recompilation. Earlier focused validation
passes Services `49/49`, NPC `34/34`, Items `32/32`
and Fluid Presentation `6/6`; Save Integration passes `37/37`. The item
presentation builder validates `80/80` bindings and `102` unique meshes; the
focused rebuilt-character PlayMode passes `1/1`. Manual
in-world acceptance is still required for nozzle alignment, kitchen hand/prop
alignment, action interruption, streaming unload and audio. Fleetari outcomes,
home mail-order fulfillment, Suomi vehicle effects and vehicle/inspection
receivers remain fail-closed and are not classified as complete.

### 2026-08-12 physical catalog book interaction

Read-only inspection of the locked `Sheets/Magazine` hierarchy recovered eight
home product-page groups, 46 product hit regions and the donor camera/page
layout. The reviewed Fleetari textures resolve to six service pages containing
32 choices plus a separate order-confirmation page. Their source PNGs and hashes
remain in external donor staging; no donor controller, FSM or scene hierarchy is
used by the new runtime.

`PhysicalServiceCatalogController` reimplements the interaction as a world book:
primary interaction opens it, suspends player input, pins the existing camera
above the catalog, turns physical page geometry with wheel/arrow input and
selects items through normalized page hit regions. Escape or right click closes
the book and restores the exact camera, cursor, input and UI-gate states. The
home spread keeps the order form visible on the right; Fleetari preserves the
save-backed workshop selection, exclusive groups and final-drive variants.
Temporary page art is imported through the existing hash-gated item surface
pipeline and remains `TemporaryDirectImport`. Home form persistence/payment/post
delivery and Fleetari vehicle outcomes remain explicitly fail-closed.

The 2026-08-13 fidelity pass additionally inspected
`Sheets/Magazine/Products/*`, the individual donor selection `x` renderers and
their `x.png` material closure, plus the serialized order `TextMesh` settings.
Forward physical motion is right-to-left without changing logical page order.
All 46 form rows now preserve their independently transferred two-column
positions, bitmap font `RAGE`, `0.0025` character size and blue colour
`57/64/208/255`; selected page entries use the transparent donor X mark.
Only presentation data/assets were transferred. Donor FSMs and controller code
remain outside runtime authority.

### 2026-08-11 local lighting system and VLB 2.2.3

The full project audit opened 70 game/streaming scenes and 168 prefabs. After
excluding verified non-fixture semantics such as vegetation `LightTrunk`,
billboards, light maps, lightning targets, zones, switches and global
directional/environment lights, it recorded 297 fixture candidates. Three
existing project-owned WeatherLab Lights were safely bound; 294 ambiguous
donor/generated or geometry-only candidates remain in explicit
ManualReview rather than receiving guessed origins or aim.

The first audit revision briefly treated 17 global directional/environment
lights as local fixture candidates. The final builder rejects them by Light type
and semantic context, removes only its own `lighting.fixture.auto.*` components,
and restores directional photometry. Enviro/HDRP ownership is unchanged.

The accepted Phase 1 world-light catalog remains the integration baseline. Its
deterministic builder now emits 46 donor-evidenced sources plus three
project-owned refrigerator fills. All 49 stable `world.light.*` records have
project-owned electrical/profile/zone bindings. The shared Teimo bank follows
the user-confirmed five-shop/one-pub split; three cool rectangle fills remain
shop-owned behind the refrigerator doors.

The user-supplied VLB package hash is
`6129490984551E1C64BDEDE337636F05C78291A81E36E425BD285D1B625F6C8B`.
Its authoritative code constant is `20203` (2.2.3). Vendor source is unchanged;
project code uses a reflection-cached public API adapter. The latest Lighting
EditMode run passes 33/33 and the D3D11 Bootstrap lifecycle passes 1/1 while
streaming Teimo, Fleetari and home. Cached-shadow, renderer-after-culling and
null-reference signatures are zero. GPU/GC evidence and manual visual
acceptance remain open.

The follow-up local-light correction uses the locked `GAME.unity` rather than
inventing a remake-only sensor. Donor `switch_garage` owns the `Garage` group
and persists as `HouseLightSwitchGarage`; donor `outdoor_lamp` has no Light,
FSM, motion or presence-sensor component. The recreated outdoor fixture is
therefore a project-owned `HomeExterior` load on `grid.home.garage` and
`switch.home.garage`, preserving the existing home fuse, metering and save
authority. Its light and generated diffuser use a neutral white presentation;
the static donor body remains white when the circuit is off.

The donor player flashlight contains a real hard-shadow spot (range 25.78,
legacy intensity 1.2, 128-degree cone) and the item FSM owns charge, batteries
and on/off state. The project adapter now creates a bounded 300 lm / 20 m HDRP
spot on the
stable `item.flashlight` instance and reads its existing charge/on bit across
pickup, save restore and cell reload. Story-traffic wrappers receive two
project-owned low beams behind the existing dusk and quality budgets; no donor
vehicle controller or hierarchy name becomes authority. Home switch targets
retain donor visual motion but use a uniform 0.28 x 0.34 x 0.16 m ray envelope.

The final visual correction raises the garage-only no-bake multiplier to 3.0x,
authors Teimo's five shop tubes at 5200 lm and the single pub tube at 3500 lm,
and keeps both Teimo zones at neutral-white 4200 K room light with pure-white
fluorescent emitter presentation. Street and other spot fixtures now generate a
small dedicated emissive lens at the lamp head; the complete donor pole/body is
never used as an emitter. Focused Lighting EditMode passes `33/33` and the
streamed Bootstrap lifecycle passes `1/1` after the correction.
The corrected focused runs pass Lighting EditMode 33/33 and the full streamed
lighting PlayMode lifecycle 1/1 (38.49 s); its log contains zero cached-shadow,
renderer-after-culling, duplicate fixture, compile or null-reference matches.

The fourth correction resolves the remaining mobile/streamed presentation
regressions. Locked donor `GAME.unity` places the flashlight Light as a direct
child with local X +90 degrees, so the project beam now follows item-local `-Y`
from the actual `headlight_glass` lens instead of using the battery-cover mesh.
Story-traffic low beams are reduced to 6500 cd / 35 m with a 0.015 native HDRP
volumetric dimmer and a maximum VLB weather density of 0.025. The home exterior
diffuser is generated on the outward donor-renderer surface rather than inside
the opaque housing. Streamed fixtures also republish their idempotent lifecycle
registration once per second, repairing a missed/replaced composition-root
subscription. In Editor only, the local-light budget measures the nearer of the
real player and an enabled Scene View camera so remote loaded cells remain
inspectable; player position remains the sole shipping-build focus. The final
focused runs pass Lighting EditMode `33/33` and the streamed lifecycle `1/1`
(`42.67 s`). Manual night-scene and GPU acceptance remain open.

The fifth correction proves that the missing remote-location presentation was
not a streamed-fixture loss: Unity retains Scene View cameras whose
`Camera.enabled` flag is false even while that view renders. Editor-only
budgeting now measures every retained `CameraType.SceneView` and uses the
nearest player/view distance; shipping builds remain strictly player-centred.
The locked `GAME.unity` also provides exact story-car low-beam transforms:
Jani uses local `(+/-0.579979, 0.21898432, 1.8)` and Petteri uses
`(+/-0.5300047, 0.16999996, 1.92)`, both pitched down three degrees. Generated
Lights and their dedicated emissive lenses now use those anchors; other traffic
falls back to a four-wheel fascia basis instead of renderer bounds. Home
domestic/enclosed profiles move from 3000/3150 K to a restrained 3300/3500 K.
The rebuilt 49-binding catalog passes Lighting EditMode `33/33` and the full
streamed lifecycle `1/1` (`57.14 s`) with zero cached-shadow,
renderer-after-culling, compile or null-reference signatures. Manual visual and
GPU acceptance remain open.

## 2026-08-12 — AXIS first-person single-arm skinning correction

The licensed AXIS source contains driver-authored corrective shape keys for its
complete two-arm control rig. The Phase 1 wrapper renders a distal, indexed
single-arm extraction instead. Importing those full-rig morph targets and their
64 animated `blendShape.*` curves into each cut action root allowed unrelated
correctives to displace the retained vertices, producing detached fingers and
long torn surfaces in the live viewmodel.

`LicensedAxisNeutralArmsImporter` now imports the sanitized FBX without blend
shapes, strips any defensive residual morph curves from all six generated
clips, zeros renderer morph weights, and keeps the reviewed bone animation and
eight-weight skinning. The generated prefab was rebuilt. Unity camera renders
show contiguous wave, middle-finger and drink meshes; focused authoring tests
pass `6/6`, binding tests pass `4/4`, and the real generated binding activates
exactly one root for each of Drink, Smoke, Hello, MiddleFinger, Push and Fist.
All active renderers bake finite bounded geometry. The camera-local prefab is
intentionally parented at the gameplay camera (therefore it appears at the
player head in Scene View); Game Camera output is the presentation authority.
Manual in-game visual acceptance after this rebuild remains open.

## 2026-08-13 — AXIS runtime evaluator and prop-axis correction

An executed HDRP PlayMode comparison reproduced the reported giant fingers,
camera-filling arm and head-local offset only when Unity 6 drove the purchased
Generic hierarchy through legacy `Animation.Play`. Sampling the same imported
clips through `AnimationClip.SampleAnimation` produced the valid bounded meshes
already seen in Editor audits. `FirstPersonLifeActionViewmodelBinding` now owns
an unscaled, interruptible presentation clock and directly samples all fourteen
AXIS clips. All six actions use AXIS roots; donor hand clips remain evidence,
not active runtime presentation.

The smoke prop follows an index-middle-phalanx anchor between the index and
middle fingers. The beer grip also accounts for the imported mesh convention:
the bottle neck is local `-Z`, so the grip maps `-Z` toward the mouth rather
than aiming the recessed base at the camera. The private wrapper rebuild passed
with exit 0. After the bottle-axis correction, the focused six-action realtime
HDRP skinning audit passed `1/1`, the complete ready/mouth/four-sip/return drink
timeline passed `1/1`, and the binding EditMode fixture passed `4/4`. The saved
frames show bounded limbs, an upright ready bottle, its neck below the camera at
the mouth, progressive base raises, and an upright return. Manual in-game visual
acceptance remains open. The expanded AXIS authoring fixture subsequently passed
`6/6`, covering all fourteen event-free clips, anatomical side ownership,
eight-weight skinning, camera-local envelopes and torn-triangle rejection.

The external-cell lifecycle correction makes the world-light factory reconcile
actual loaded manifest scenes rather than treating an existing scene root as
proof that every fixture was created. Generic and owned scene load/unload paths
now share cleanup; missing Lights are recreated, incomplete adapter hand-offs
are retried, and one presentation subscriber cannot abort the remaining lights
in a cell. Focused PlayMode physically unloads and reloads Fleetari
`cell_3_-1` and passes `1/1` (`40.83 s`), restoring exactly two workshop Lights
and two emission lenses without duplicate stable fixture IDs. The broader world
catalog run compiles and passes `10/12`; its two count assertions still expect
the older `45 / 12` catalog while the current concurrent baseline contains
`49 / 14`, so those unrelated stale assertions were not rewritten here.

## 2026-08-12 — Teimo store player-only counter boundary

The invisible blocker behind Teimo's shop counter is donor object
`STORE/LOD/ActivateStore/PlayerColl`, an enabled non-trigger box on donor layer
23 `PlayerOnlyColl`. Its name, dedicated player layer, broad volume and store
activation parent show that it enforced staff-area access rather than providing
structural collision for the counter or sausage refrigerator. Stable collider
`e4474f2a451172f949123c2f37727a69` is classified as `Rejected` for the remake's
active runtime, removed from `cell_-3_0`, and explicitly excluded by collision
policy `08A1.7` so regeneration cannot restore it.

## 2026-08-12 — secondary Cheap Car Repair player-feel reference

The user-owned Cheap Car Repair installation was inspected read-only as an
out-of-scope secondary behavioral/configuration reference: Steam build
`24237531`, game version `1.2.0025`, Unity `6000.2.15f1`, managed gameplay
assembly SHA-256
`80B305144550642701B6C4747038C9A150BC1B6299A33CCB703E4BD09EC1E11F`.
No assembly, decompiled source, asset, prefab, FSM or extraction was retained in
the repository. Transferred numeric configuration and observed boundaries are
recorded as `BehavioralReference;ConfigurationTransferred`; movement and camera
runtime are clean-room `Reimplemented`. This reference does not alter the
locked My Summer Car Phase 1 donor or its authoritative scope.

## 2026-08-13 — secondary Cheap Car Repair item-interaction reference

The same user-owned installation was inspected read-only for physical item
grabbing. `GrabbingHandler` preserves the selected surface point, keeps ordinary
objects non-kinematic with continuous collision, applies a spring/retention
velocity response and redirects held rotation input away from camera look.
Constructor defaults include spring `90`, retention `0.8` and `6°` wheel steps;
the active serialized scene offset was not proven and was not claimed.

The running window could not be captured after two Windows UI Automation
failures (`0x80004002`), so subjective visual parity remains unverified. Project
runtime is classified `BehavioralReference;ConfigurationTransferred;Reimplemented`.
No donor code, binary or asset was retained, and the locked My Summer Car donor
and Phase 1 scope remain unchanged. Full evidence and project differences are in
`Docs/Player/CHEAP_CAR_REPAIR_ITEM_INTERACTION_TUNING_2026-08-13.md`.
Focused Player Interaction EditMode passes `26/26`; the complete interaction
flow PlayMode fixture passes `6/6`. Manual varied-object feel acceptance remains
open.

The 2026-08-14 follow-up is project tuning from direct user feedback, not new
donor evidence: carried bodies inherit `96%` of bounded player translation and
retain the selected point's initial forward distance between `0.32 m` and the
authored `0.82 m` maximum. Save schemas, stable IDs and donor classification are
unchanged. Runtime and both affected test assemblies compile; execution of the
new fixtures is blocked by unrelated concurrent compile errors in the fire and
life-action presentation assemblies.

## 2026-08-13 — locked player Swearing/Fuck evidence and private presentation

Read-only inspection of locked `GAME.unity` (SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`)
confirmed Setup component `&108669` (`Swear=N`, `Finger=M`), Speech FSM
`&110839` (random 16-way Swearing selection, `PlayerStress -= 0.5`, one-second
real-time wait), Simulation FSM `&112251` (`SWEARING` at stress `100`) and
MasterAudio group `&105622` (16 variations). Insufficient-funds branches also
reference the Swearing group. A correction pass separately confirmed the
PlayerFunctions FSM `&111301`: its `Finger` state selects random `[0,11)`, plays
MasterAudio group `Fuck`, drives animation `middlefinger` and emits `FINGER` to
nearby NPC logic. The `Fuck` group `&105307` has 11 variations backed by
`fuck01..fuck11`; component `&107451` stores their 11 English subtitle entries.

The behavior/input values are classified
`BehavioralReference;ConfigurationTransferred;Reimplemented`. The 16 Swearing
and 11 Fuck PCM resources are `TemporaryDirectImport` solely for the private Phase 1
fallback. Every metadata/resource hash, AudioClip GUID and variation/
GameObject/AudioSource file ID is locked in
`Assets/Game/LegacyImport/Manifests/Phase1PlayerVoiceManifest.json`.

The importer is schema-locked to copy only verified mono 22050 Hz PCM WAV
resources into the ignored RuntimeBaseline and build 27 project-ID mappings:
`player.swear.01..16` and `player.finger.01..11`. It does not copy donor
AudioClip assets, AudioSources, MasterAudio objects, FSMs, scripts or runtime
assemblies. Gameplay authority remains the project Input/Needs/Economy policy;
the removable audio is loaded through `IAudioBackend`. Full evidence and manual
acceptance are in `Docs/Player/PLAYER_VOICE_REACTIONS_2026-08-13.md`.

## 2026-08-13 — Satsuma V1a sanitized physical shell

Read-only inspection of locked `GAME.unity` identified Satsuma root transform
`&64200`, the enabled Body renderer subset, donor Rigidbody reference values and
the component collider roster. The generated private baseline copies only the
8 required shell renderer meshes/material inputs and 22 usable component hulls.
It copies no donor MonoBehaviour, PlayMaker FSM, controller, camera, input,
save, audio or runtime assembly.

The source scene hash and every mesh/material/texture GUID are recorded in
`Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1aManifest.json`. The runtime
wrapper is project-owned and classified `TemporaryDirectImport`; assembly,
simulation, stable identity and persistence remain project authority. The
donor monolithic `CarCollider` was rejected because Unity 6 reported a convex
hull over the 256-polygon limit and would silently substitute a partial hull.
Detailed scope and known gaps are in
`Docs/Phase1/11A_V1A_SATSUMA_PHYSICAL_BASELINE_REPORT.md`.

## 2026-08-13 вЂ” Satsuma V1b direct CARPARTS roster

The same hash-locked scene was inspected script-free below the four direct
`CARPARTS` groups. The bounded logical inventory contains 125 part roots after
excluding the non-part `PartsGT/WoodSheet` fixture: 75 `PartsCar`, 39
`PartsMotor`, 6 `PartsGT`, and 5 `PartsExtra`. Frozen activation marks 78 roots
active and 47 inactive; the inactive set is retained in the project registry,
but its final new-game activation semantics are not claimed until the donor
startup FSM behavior is compared.

For each root the builder records its transform ID, group/name, generated
project part ID, deterministic stable entity ID, activation, renderer/collider
counts and mass in `Phase1SatsumaV1aManifest.json`. Runtime content contains
only imported presentation inputs and sanitized physics values behind
project-owned `PartInstance`, `PhysicsPickupTarget` and save boundaries. No
donor MonoBehaviour, FSM, controller, assembly or hierarchy lookup is shipped.

Automated evidence: builder `8/22/125/78/46`, EditMode `8/8`, production
Bootstrap PlayMode `1/1` (`6.91 s`). Manual placement, settling, pickup and
new-save/load acceptance remains pending.

## 2026-08-13 — Satsuma V1c Assembly FSM and mount evidence

The locked scene contains 102 `Assembly` PlayMaker components under the Satsuma
root. They were read as inert serialized YAML and reduced to a tracked evidence
table; no donor actions, types, controllers or state machines were loaded or
copied into runtime. The table captures trigger path, held-part identity,
installed presentation object, database prerequisite, detach object and whether
the FSM references `Bolts`/`Bolted`.

Independent name and mesh/pivot audits prevent false certainty: the name pass
finds 41 unique candidates while the filtered mesh pass finds 22 unique root
poses. Only 26 exact-name, unique and manually bounded part identities currently
author runtime mounts; two additional interchangeable rear-drum mounts are
resolved directly from their Assembly triggers. The remaining ambiguous/
unresolved records stay audit-only.
Twenty suspension/steering dependency edges are transferred from direct
serialized prerequisite evidence.

The V1c.1 read-only pass inventories 85 descendant `BoltPM` markers. Exact
local pose, sphere radius and scale-derived wrench size are tracked in
`Phase1SatsumaV1cFastenerAudit.csv`. Forty-five markers belong to the 28
currently verified runtime mounts and are reimplemented with the donor `Screw`
FSM's common `0..8` stage contract; the remaining 40 stay audit-only rather
than being attached to guessed mounts. The runtime spanner set exposes sizes
5 through 17, with the current verified fasteners using 5, 6, 7, 8, 9, 10, 12
and 14 mm.

The project-owned ten-colour main-menu selector now applies the chosen colour
to a fresh Satsuma before gameplay activation. Per-vehicle material property
blocks preserve shared imported materials. An optional paint payload participates
in vehicle save/load; an older record without it retains its authored colour
and is not repainted.

V1c.3 adds 15 fixed Assembly-parent pivots plus exact hinged mounts for the
bootlid and both doors. `Phase1SatsumaV1cHingedAssemblyConfigurationAudit.csv`
records their source Assembly/Use component IDs, local pivots, axes, limits,
opening torque and break values. All loose-part FSM action inventories and all
static donor HingeJoint records are tracked separately; none is instantiated as
runtime code. The three project-owned hinges carry their four exact fastener
targets and restore open angle through the existing installed-part rotation.

V1c.3 automated evidence: deterministic builder passed
`8/22/125/78/46/57/3/46`; focused Satsuma EditMode `10/10`; spanner/catalog
EditMode `8/8`; production Bootstrap PlayMode `1/1`; selected-colour New Game
PlayMode `1/1`. Manual rendered paint, hinged-panel interaction, garage assembly
and new-save/load acceptance remains pending. Twenty-eight inventoried fasteners,
fluids, wiring, tuning, wear and unresolved mount authority remain open, so the
vehicle stays fail-closed and kinematic.

## 2026-08-14 — Satsuma V1d loose-part assembly and engine mount

Read-only parsing below the locked `CARPARTS` root found 76 additional
serialized `Assembly` FSM records. The builder records their trigger, held
child, owner, `db_PartRequired1`, `DetachPart`, bolt references and hierarchy
paths in `Phase1SatsumaV1dLoosePartAssemblyFsmAudit.csv`; descendant fasteners
are recorded in `Phase1SatsumaV1dLoosePartFastenerAudit.csv`. These records are
`BehavioralReference`/`PivotSource` only. Runtime authority remains the
project-owned assembly graph.

Thirty-seven unambiguous mounts now follow their loose owner part. The chassis
motor mount is reconstructed from three donor chassis/block trigger pairs as a
single rigid transform; its maximum point residual is 3.92 mm and all source
IDs/coordinates are retained in
`Phase1SatsumaV1dEngineMountTriangulationAudit.csv`. This produces 84 mounts,
170 supported fasteners, three hinges, ten wrench sizes and 34 directed
dependencies over the existing 126-part aggregate.

The assembly controller accepts only two known older Satsuma save shapes for
additive migration: V1c `46 mounts / 57 fasteners` and the intermediate V1d
`83 / 167`. New state is appended empty; arbitrary truncation, duplicate IDs or
unknown records still fail closed. Schema 1 and stable IDs are unchanged.

Automated evidence: builder passed; generated Satsuma EditMode `14/14`; generic
assembly regression EditMode `22/22`; vehicle-assembly PlayMode `9/9`; New Game
paint PlayMode `1/1`. Manual garage, colour, save-migration and full build-order
comparison remains pending. Fluids, wiring, tuning, wear, startup activation and
drive authority remain incomplete, so no full-parity claim is made.

## 2026-08-14 — Satsuma V1d.2 front mudflaps, handoff presentation and spanner set

Two previously omitted front-mudflap Assembly records have
`ParentGameObjectId=0` but valid `ActivateThisGameObjectId` values. Read-only
scene evidence binds left/right mudflaps to the corresponding fender at local
positions `(-0.026500687,-0.35480016,-0.24299999)` and
`(0.026499934,-0.3548,-0.24299993)`. The nearest sibling `BoltPM` is retained
for each; other fender bolts are not guessed onto these mounts. The generated
graph is now 116 mounts, 205 fasteners, 46 owned mounts and 34 dependencies.

The valid carried-part handoff gained a project-owned `0.34 s` smooth-step
translation/rotation into the exact mount pose. This timing/easing is a remake
interaction presentation choice inspired by the user's licensed Cheap Car
Repair reference, not transferred donor configuration. Compatibility, donor
build order, mount ownership, exact final pose and save authority remain the
existing project-owned assembly graph.

The frozen donor spanner-set hierarchy and animation clips establish an 8 kg
case, lid pivot `(0.156,0,0.0257)`, open quaternion
`(0,0.91509414,0,0.40324026)`, durations `0.45/0.25 s`, hidden tools while
closed and wrench meshes sized 5 through 15. Runtime uses a project-owned case
controller and eleven physical pickup/tool identities. Donor clips/FSMs are not
loaded as runtime authority. Builder/validator passed; generated Satsuma
EditMode 16/16, assembly PlayMode 8/8, bootstrap PlayMode 1/1 and targeted
spanner EditMode 5/5 passed. Manual in-world acceptance remains pending.
The complete item EditMode regression subsequently passed 45/45.

## 2026-08-14 — outside-milestone item placement, fridge and food correction

Read-only `ITEMS` evidence was normalized into all 43 canonical placement
records: exact world transform, unit scale, parent provenance, donor layer,
Rigidbody presence/state, collision mode, constraints and damping. The runtime
uses a project-owned collision layer and exact captured primitive shapes where
available. There are 42 donor Rigidbody instances and one intentionally static
parts magazine. New Game materializes the canonical pose; an executed combined
`items.instances` plus `world.entities` round-trip proves that the saved pose
and rotation override it on load.

The home fridge binds the frozen door/paper/handle stable IDs
`5beeac8c1e16e46351b1224a92b15261`,
`72d34129df6c41f282ca82028a53cfd6` and
`63469f2df1f8e8e8460d03f76327f7e1`. Its project hinge preserves the audited
pivot, axis, 92.1-degree travel and 158 deg/s speed. Door and shelf collision,
ordinary handle interaction, `IAudioBackend` door events, Home electricity and
native persistence remain project-owned.
The sanitized static copies of the moving leaves and coarse solid-volume
`LOD_kitchen/Coll` entity `6a575ded49fa3971746b0a57d50b39db` are disabled:
they otherwise leave an invisible closed door and overlap every stored item.
The real-Bootstrap test resolves a normal pickup target through the opened
door while retaining cabinet, shelf and moving-door collision.

Food definitions now own consume timing, freshness rates, cooking thresholds,
state tints and effects as catalog data. The sausage package has four
deterministic physical loose children. Loose sausages inherit package
freshness, cook for 30 seconds and burn after 10 more on registered stove,
portable-grill or mangal heat volumes. Captured spoil/refrigerator rates and
cooked effects are `ConfigurationTransferred`; state machines and save/runtime
integration are `Reimplemented`. Burned/spoiled adverse coefficients remain
explicit provisional values because normalized donor evidence is incomplete.

The reviewed loose-sausage prefab/mesh/texture closure now generates a
sanitized `TemporaryDirectImport` wrapper instead of the primitive fallback.
Milk, juice concentrate and the Expanded Shop buttermilk/orange juice/mustard/
ketchup definitions opt into the existing drink viewmodel through the authored
`ConsumptionPresentation` field. The real carried item is attached to the
existing Drink grip until the logic-owned consumption action completes;
ordinary food remains arm-animation-free. The juice-concentrate donor `Use`
FSM explicitly dispatches `Drink` to the hand FSM and is retained only as
`BehavioralReference`.

Automated evidence: Items EditMode 53/53, Home 9/9, Needs 16/16, Save
Integration 37/37 and real-Bootstrap food/fridge PlayMode 2/2 passed. Manual
official-Wwise output, rendered donor comparison, exact heat-footprint and
complex mesh-collider acceptance remain pending, so no parity row is promoted
to `Verified`. Full details are in
`Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md`.

## 2026-08-14 — outside-milestone Expanded Shop purchase extension

The user-supplied `ExpandedShop.dll` was inspected read-only as a third-party
behavioral/configuration reference. The assembly hash is
`2E51250495D97E628AE98A8892B733FE76C775A7278D104DD73E2BE719789A7E`;
its embedded `drinks` AssetBundle hash is
`997E3F2D227AAED732B91DAAF2EB40EE180D75A9051319813E28B534B3AB0773`.
Neither the DLL nor complete AssetBundle is present in project Assets or
required at runtime. A selected presentation-only asset closure is regenerated
from the locked staging export into the ignored private Phase-1 baseline.

IL and prefab evidence yielded nineteen unique Teimo products, exact prices,
effective stock capacities and shelf trigger transforms. `ShopBuying.Inventory`
is a zero-based last-child index, so capacity is `Inventory + 1`; the serialized
can-opener value zero therefore represents one item. Duplicate mustard and
ketchup shelf sections are merged into one stable offer per product with summed
capacity.

Project-owned catalogs add nineteen prices/offers and nineteen item definitions
(eighteen purchase roots plus the spawned fishstick). The sausage offer reuses
the established loose-sausage definition. Normal store interaction, basket,
checkout, physical handoff, Rigidbody pickup and native save paths remain
authoritative. Nineteen sanitized presentation wrappers contain only reviewed
render meshes and project HDRP materials; mod scripts, old physics, tags, layers
and FSMs are excluded. Purchased items use that provider; the later exact-layout
correction below replaces the initial provider-grid shelf approximation with
sanitized group prefabs and exact `Col` geometry. The loose-sausage offer uses
the reviewed original-game sausage wrapper rather than the duplicate mod
prefab.
Additive service-state restore initializes newly introduced stock lines while
retaining strict rejection of unknown, duplicated and out-of-range records.

The presentation pipeline validates `147` bindings: `128` reviewed base-item
bindings plus `19` Expanded Shop bindings. Items EditMode `53/53`, Needs
EditMode `16/16`, Economy EditMode `5/5`, Services EditMode `57/57`, Save
Integration EditMode `37/37` and food PlayMode `2/2` passed. Full
configuration, known boundaries and remaining manual acceptance are recorded in
`Docs/Items/EXPANDED_SHOP_EXTENSION_2026-08-14.md`.

### Exact physical shelf-layout correction

A second evidence pass over `ShopBuying`, `ShopRaycast` and the complete
hash-locked `TeimoDrinksMod.prefab` corrected the earlier approximation. The
mod owns `21` independently transformed physical buying groups for `19` unique
offers. Each group has its own `Col`; its `Inventory` children are manually
positioned and ordered. The earlier project implementation retained only one
root point per offer, discarded group rotation, rescaled product wrappers into
guessed dimensions and generated a rectangular grid with one renderer-bounds
collider. That implementation was therefore not faithful and could leave
visible products outside the interactive volume.

The replacement importer parses the locked prefab YAML read-only because the
AssetRipper export contains duplicate renderer component IDs and cannot safely
enter Unity's AssetDatabase. It emits `20` sanitized mesh-only group prefabs
plus one collider-only sausage group, preserving every group/unit transform and
BoxCollider field. Mustard and ketchup remain two physical groups bound to one
project offer through non-overlapping stock-index ranges. Runtime Interaction,
ServiceRuntime, checkout, ItemWorldRuntime and save authority are unchanged.
Focused service-fixture EditMode coverage passed `5/5`, including child-collider
capability resolution, outline ownership and duplicate-group stock depletion.
Classification remains `WorldLayoutReference; TemporaryDirectImport;
Reimplemented`; rendered in-game comparison is still pending.

## 2026-08-14 — Satsuma V1d.3 physical assembly, hinges and wrench feedback

The generated chassis now preserves the locked donor root mass label at
`557 kg` and starts as a gravity-driven, non-kinematic Rigidbody. A
project-owned mass adapter adds installed loose-part masses without changing
stable IDs or the schema-1 assembly state. The four raycast-wheel contacts are
enabled only when their required suspension, spring alternative and wheel
mounts are occupied, so a bare shell falls and an incomplete corner supplies
no invented support.

Surface handoff is restricted to mounts owned by the renderer actually aimed
at, with a `0.16 m` aim-ray limit. Direct mount spheres are clamped to
`0.03..0.075 m`; accepting a handoff keeps the install prompt until the
project-owned `0.34 s` transition commits. The graph adds both spindle
prerequisites for the steering rods. Rear springs require the matching trailing
arm, normal/long springs exclude each other, and each rear shock requires one
of those springs. The aggregate remains `126 parts / 116 mounts / 205
fasteners`, with `36` dependency edges.

Installed doors and bootlid now use held LMB/RMB force-like motion on their
existing exact pivots and donor limits. A partly secured panel remains attached;
an entirely unfastened panel can detach under a sufficiently hard opening pull
and returns to normal dynamic pickup physics. This is a deterministic
project-owned transform/assembly implementation, not donor HingeJoint or FSM
runtime.

The locally installed BetterMSC assembly was inspected read-only at SHA-256
`20410ADBB59CF7A3252103C36301A91CA8F9C83AE3CC9D1E4F8DE0BF0D17E6F8`.
Only `ToolHand.ToolData["Spanner"]` pose values and the `ScrewBolt` 60-degree
work arc/easing were retained as `BehavioralReference` and
`ConfigurationTransferred`. Runtime uses a new coroutine over the real carried
Rigidbody; the DLL, mod classes and donor runtime are not loaded or shipped.
Docked wrench colliders are solid and selectable while the case is open.

Fastener hover uses the established interaction outline boundary. This
historical V1d.3 pass used white for work remaining, red for an incompatible
wrench and green for maximum stage. The later V1d.26 correction below replaces
that provisional mapping with the user-approved four-state contract and gives
the fastener its own donor renderer instead of outlining the nearest part.

Deterministic builder generation passed at version `11A-V1d.3`. Executed
coverage passed: generated Satsuma EditMode `18/18`, generic assembly EditMode
`22/22`, player interaction EditMode `27/27`, outline EditMode `6/6`, Items
EditMode `39/39`, and vehicle assembly PlayMode `8/8` (`120/120` total).
Manual rendered alignment, chassis settling,
wheel/spring response, held-wrench pose and door break-force acceptance remains
pending; full drivetrain, fluids, wiring, tuning and wear remain incomplete.

## 2026-08-14 — outside-milestone grill fuel and hinged container covers

Locked GAME scene components `108771` (charcoal trigger) and `114344`
(SetFire Use FSM) establish a `140` unit package, `100` unit grill capacity,
`12/s` physical pour rate, `80` degree package tilt threshold, strict `>10`
ignition threshold, `120 s` flame, `0.1/s` flame consumption, `0.04/s` ember
consumption and shutdown at `<=5`. They also establish that ignition is the
ordinary Use/F action; no matchbox item is present in this flow.

These values are `ConfigurationTransferred`; the source FSM graph is
`BehavioralReference` only. Project-owned `ItemCombustionDefinition`,
`ItemFuelPourReceiver` and existing item state/save fields reimplement the
flow. Fuel pours only through physical cavity overlap plus tilt, cannot be
added while hot, and drives the existing generic heat-source contract for food.
The active flame uses the established HDRP fire presenter and disappears when
the saved item enters ember state. Its center now follows the donor-relative
`FireTrigger` `(-0.017, 0, 0.111)` and local `+Z` up axis; a grill-owned
scale/emission/light profile prevents the full garbage-barrel plume from being
used on the small bowl.

The grill cover retains exact donor pivot/child transforms. Its travel timing,
and the hinged kilju-lid behavior requested by the user, are project-owned
presentation. The canonical kilju lid stable identity is retained while its
presentation is adopted by the bucket. Empty bucket water/ingredient helper
meshes and empty grill charcoal geometry are disabled from authoritative state.
The user subsequently confirmed the existing grill hinge is correct, so the
fire correction leaves the cover object and motion untouched.

Catalog and sanitized-presentation builders passed (`147` bindings: `128`
reviewed base-item plus `19` Expanded Shop bindings). Executed
coverage passed: Items `53/53`, Economy `5/5`, Services `57/57`, Save
Integration `37/37`, Fluid/fire `7/7`, and food PlayMode `2/2`. Manual carry
feel, rendered flame/cover collision, continuous pour feedback, official Wwise
and real-slot acceptance remain pending, so affected rows stay
`PartiallyImplemented`. Full evidence is in
`Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md`.

## 2026-08-14 — Satsuma systemic assembly, jack and paint pass

The locked `GAME.unity` mount ownership, suspension requirements, car/floor
jack Rigidbody and Use-FSM records, plus locked `MainMenu.unity` colour actions
were inspected read-only. The donor `CAR_PAINT_RUSTY` material resolves to
texture `body_rust.png`; `CAR_MASSE` is a separate non-paint surface. These are
`BehavioralReference`, `ConfigurationTransferred`, `PivotSource` and temporary
presentation evidence as applicable.

Project runtime reimplements logical mount-owner parenting, recursive installed
pose synchronization, dynamic chassis mass aggregation, NWH contact gating,
saved jack lift/drag/lower behavior, twelve-colour New Game selection and
material-slot-specific rust paint. No donor FSM, MonoBehaviour, PlayMaker graph,
old Unity runtime or BetterMSC DLL is loaded. Builder `11A-V1d.5`, EditMode
`62/62 + 54/54` and focused PlayMode `12/12` passed. Manual complete-garage,
rendered jack articulation and save/stream acceptance remain pending, so all
affected rows stay `PartiallyImplemented`.

## 2026-08-15 — Satsuma V1d.6 collision, targeting, jack and audio correction

A second read-only component audit corrected two misleading earlier
assumptions. `SATSUMA(557kg, 248)` is a donor object label; its serialized
Rigidbody mass is `389`. The live donor `/Colliders` hierarchy contains `29`
active non-trigger shapes: `23` convex MeshColliders, four `PlayerColl`
BoxColliders and two `floor` CapsuleColliders. Builder V1d.6 transfers that
complete shape contract, resets PhysX center of mass/inertia from the imported
shapes and then adds installed-part mass through the project-owned aggregate.

The two apparent subframe sockets are not valid alternatives. The
mesh-derived `mount.satsuma.sub-frame` is retained; the nearby FSM-derived
`mount.satsuma.subframe` is retired. Vehicle schema 1 remains current and loads
using the retired ID migrate to the canonical mount before validation. Conflicts
remain rejected rather than guessed.

Installed parts now expose independent kinematic trigger proxies. These proxies
exist only for bounded selection/removal and cannot collide with or push the
chassis. Nested wrench targets similarly bypass only their own registered
toolbox ancestor during occlusion resolution. The physical loose-wrench
implementation described by this V1d.6 snapshot was superseded by the focused
2026-08-15 correction below; case lid presentation still cannot inject angular
velocity into the case root.

Car/floor jack item evidence remains `BehavioralReference` and
`ConfigurationTransferred`; runtime state and force application are
`Reimplemented`. The car jack is pickupable while fully lowered, both reviewed
mechanisms articulate with lift height, floor-jack drag temporarily ignores only
nearby vehicle pairs, and lift contact latches one explicit chassis pad with a
blended capped support force.

The assembly audio error was traced to the project primary fallback library:
install/remove/fastener IDs all resolved to the same generic interaction clip,
which sounded like a door opening. A replaceable private
`TemporaryDirectImport` override now binds donor `assemble.wav`,
`disassemble.wav`, `bolt_screw.wav`, `crash_low1/2.wav` and
`crash_hi1/2.wav` through stable project IDs and `IAudioBackend`. Missing local
content fails silent for assembly actions instead of replaying the generic
door-like placeholder. Body collision presentation uses the four audited impact
variants with a bounded cooldown/intensity parameter.

Builder `11A-V1d.6` passed with `8/29/125/120/115/205/3/46` for shell
renderers/chassis colliders/loose parts/active loose parts/mounts/fasteners/
hinges/owned mounts. Executed coverage passed: generated Satsuma EditMode
`21/21`, Items/toolbox/jacks `45/45`, generic assembly EditMode `23/23`, vehicle
assembly PlayMode `10/10`, production Bootstrap PlayMode `1/1` and Unity audio
fallback PlayMode `9/9`. The Bootstrap test also proves the installed presenter
is bound and `audio.event.interaction.part.install` resolves to
`satsuma_part_install`, not the generic clip. Manual continuous garage, jack
load/settling, collision feel, rendered linkage and streaming acceptance remain
pending, so parity status stays `PartiallyImplemented`.

## 2026-08-15 — toolbox non-physical wrench-mode correction

Direct user comparison against the donor toolbox establishes that individual
wrenches are selection visuals rather than loose physical props. The prior
Rigidbody/pickup implementation is therefore rejected for fidelity. Runtime now
removes any stale per-key physical components, keeps docked keys as trigger-only
selection targets, and moves the selected real renderer into a project-owned
camera-local tool mode. Other keys and the 8 kg case receive no impulse.
The project-owned idle placement partially scales camera distance by the
authored wrench size, retaining physical mesh scale while keeping keys
`5..15 mm` visible and visually distinct. The 14 mm reference handle leaves
the lower viewport and a 22-degree longitudinal twist reveals mesh thickness.
The selected renderer eases from its exact case slot to the camera over
`0.28 s`; selection authority is immediate and bolt hover may interrupt the
presentation with an immediate snap.

While the mode is active, the bounded interaction query admits only explicit
fastener capabilities. Hovering a fastener immediately places the key at its
BetterMSC-referenced snap anchor; positive wheel input tightens and negative
input loosens one authored stage, including fasteners whose physical tightening
direction is counter-clockwise. Wrong-size and completed feedback stays
red/green through the existing outline boundary. Donor/BetterMSC runtime code
does not execute. The changed Interaction, Items and Player runtime assemblies
and Items EditMode coverage compile; Unity test execution and rendered in-game
acceptance remain pending because the Editor was open in Play Mode during the
hot correction.

## 2026-08-15 — Satsuma V1d.7 broad-collider and cabin-posture correction

The locked donor scene stores the 29 Satsuma chassis colliders across distinct
collision roles rather than one interchangeable pool: one broad convex
`CarCollider` on donor layer 17, 21 detailed body shapes on layer 22, four
`PlayerColl` floor/rocker boxes on layer 23, two floor capsules on layer 9 and
one rear-window shape on layer 2. The previous importer flattened every shape
onto Unity `Default`; this made the unique broad hull a solid cabin volume and
an incorrect ground-contact proxy.

Builder `11A-V1d.7` now parses and validates those donor layer roles. All 29
records remain in the generated prefab for provenance, but the layer-17 broad
proxy is disabled and the other 28 detailed physical shapes remain enabled.
No visual transform or arbitrary spawn-height offset is used. A project-owned
non-physical cabin trigger follows the reviewed `PlayerColl` footprint without
sealing the doors or affecting carried parts.

The trigger speaks only through an Interaction capability. The player owns the
posture policy: entering drops standing to crouch or crouch to deep crouch;
inside, posture cycling is limited to crouch/deep crouch; leaving restores the
entry posture, deferring standing until headroom is safe. This presentation and
state policy is `Reimplemented`; no donor FSM runs.

The builder completed with `8/29/125/120/115/205/3/46` for renderers/collider
records/loose parts/active loose parts/mounts/fasteners/hinges/owned mounts.
Focused EditMode coverage passed `2/2` for the generated collision/volume
contract and the player posture round trip. Continuous rendered ground settling,
doorway traversal and carried-part cabin insertion remain manual acceptance, so
the feature stays `PartiallyImplemented` until the current build is driven.

## 2026-08-15 — Satsuma V1d.8 PlayerColl filtering and spawn activation

Manual inspection of the V1d.7 build confirmed that the four donor
`PlayerColl` boxes were still supporting the chassis because their layer-23
role had been flattened to the project `Default` collision matrix. These boxes
are a simplified player floor/rocker boundary, not vehicle-to-world contact.

The project now reserves layer 9 as `Player` for the first-person
CharacterController. Builder `11A-V1d.8` keeps all four `PlayerColl` shapes
enabled and moving with the chassis, but assigns each a per-collider exclusion
mask that admits only layer 9. Consequently they support the player while
ignoring `WorldSurface`, `WorldSolid`, `WorldItem`, other vehicles and the
ordinary default layer. The 21 detailed body shapes, two floor rails and the
reviewed rear-window shape remain the physical chassis contact set; the broad
layer-17 `CarCollider` remains disabled.

The generated chassis also receives a bounded project-owned spawn activator.
It wakes a dynamic gravity-enabled Rigidbody for two fixed ticks after spawn or
stream reactivation, preventing a restored sleeping optimization bit from
suspending an unsupported shell. It does not change pose, apply an impulse or
keep the body permanently awake.

Builder `11A-V1d.8` completed with the unchanged
`8/29/125/120/115/205/3/46` content contract. Focused EditMode coverage passed
`3/3`. A real PlayMode PhysX test passed `1/1`: a sleeping 389 kg body woke,
fell through the deliberately lower PlayerColl proxy and settled on its
structural collider at the road surface. Manual Satsuma visual-height and cabin
traversal acceptance remain pending.

## 2026-08-15 — Satsuma V1d.9 rear-arm contact and visible fasteners

Read-only donor evidence confirms that `trigger_trailarm_rl` and
`trigger_trailarm_rr` each activate one exact installed arm and expose two
`BoltPM` markers. All four markers use a 12 mm wrench and retain their audited
local position/rotation. The prior generated graph already contained those
fastener definitions, but the runtime prefab created only invisible raycast
spheres, so an installed arm appeared to have no bolts.

Builder `11A-V1d.9` adds one shared project-owned hex-head presentation mesh and
HDRP metal material. Every inserted donor-evidenced fastener now shows that
presentation at its transferred marker pose; empty mounts hide both renderer
and collider. The existing white/red/green outline contract now resolves the
actual fastener renderer before falling back to the nearest part renderer.

The donor loose rear-arm Rigidbody/collider and installed pose are used as
`ConfigurationTransferred`, `CollisionReference` and `PivotSource`. Project
runtime reimplements the installed physical connection with an opt-in dynamic
`FixedJoint` from each rear arm to the chassis. The arm keeps collisions,
gravity and its own mass, so ground contact or an impulse reaches the chassis;
the chassis mass aggregator excludes that mass from its kinematic-part sum to
avoid double counting. Other installed parts retain the existing bounded
kinematic contract until individually reviewed.

The builder completed with unchanged content totals
`8/29/125/120/115/205/3/46`. Generated Satsuma EditMode coverage passed `22/22`.
A focused PlayMode test passed `1/1` and proved that an impulse applied to the
installed rear arm changes chassis velocity while the physical joint preserves
the mount pose. Manual rendered bolt scale/orientation and road-contact feel
remain pending. Unfastened load/breakaway collapse is intentionally not claimed
by this pass and remains the next assembly-physics correction.

## 2026-08-15 — Satsuma V1d.10 installed-part ray and world contact correction

Live inspection showed an installed rear trailing arm still exposed its loose
pickup capability and received a `7.1206565 x 2.5211093 x 0.349751 m`
interaction proxy. The arm presentation was not that large. The proxy bounds
had accidentally included the rear drum mount and its fastener helper because
the mount used a chassis-local donor pose after being parented below the loose
arm. This mixed coordinate spaces and left the logical mount near the house
while its owning arm was installed at the car.

Builder `11A-V1d.10` calculates both rear drum mounts relative to the exact donor
installed trailing-arm transform and authors explicit
`AssemblyOwnedMountAuthoring` ownership. The rebuilt mount is a direct child of
its arm with a bounded local pose; owned helper renderers are excluded from the
installed-part interaction proxy. The resulting arm proxy is
`0.32510844 x 0.51890254 x 0.349751 m`.

`PartInstance` now disables loose pickup on install and restores it on detach.
For reviewed dynamic installed parts it also copies the authoritative mount pose
to the Rigidbody before creating the physical joint. The solid donor-derived arm
collider remains enabled, so contact from the static world is transmitted into
the connected chassis instead of acting as non-physical decoration.

The builder completed at `8/29/125/120/115/205/3/48`; the final value is the
correct owned-mount count after both rear drums gained explicit ownership.
Generated Satsuma EditMode coverage passed `22/22`. Focused PlayMode coverage
passed `2/2`: one test transfers an impulse from the arm to the chassis and a
second disables every other solid vehicle collider before proving that static
world contact against the arm alone moves the chassis. Wrench/fastener behavior
and unfastened dependency collapse are explicitly deferred.

## 2026-08-15 — Satsuma V1d.11 nested ray, carry and rear-drum physics correction

Live testing identified three separate faults hidden behind the same rear-corner
symptom. A ray beginning inside a compact installed-part trigger returned no
hit; the registered chassis host and carry-only wheel mount could outrank the
visible arm or drum; and carried loose parts still collided with the owning
chassis, notably donor colliders `collider_right_92700` and
`collider_left_92311`.

The project ray now performs a bounded origin-overlap query only for explicit
`IRaycastOriginOverlapTarget` capabilities. Installed parts may bypass only an
ancestor interaction host owned by the same assembly. Carry-only assembly
sockets are excluded while the player has empty hands, and nested installed
parts receive a higher selection priority than their owner. This does not make
unrelated walls or unregistered triggers transparent.

`CarryCollisionBypassScope` records the 29 generated chassis colliders. While a
nested loose part is held, `PhysicalCarryController` ignores only collision
pairs between that part and the owning scope; player, ground and unrelated-world
collision remain active and every pair is restored on release. Generated tests
explicitly verify both named rear-fender colliders are present in the scope.

Both rear drums now use the reviewed dynamic installed-physics link. Their exact
donor-relative sockets remain authoritative, but solid contact and impulse pass
through the drum's `FixedJoint`, its trailing arm and finally the chassis rather
than leaving the drum as a kinematic visual follower.

Builder `11A-V1d.11` completed at `8/29/125/120/115/205/3/48`. Interaction
EditMode coverage passed `39/39`, generated Satsuma EditMode coverage passed
`22/22`, and focused installed-part physics PlayMode coverage passed `3/3`,
including the nested drum-to-arm-to-chassis chain. Manual hot-build acceptance
for close-range selection, insertion beneath both rear fenders and settled
visual pose remains pending. Fastener/tool behavior and unfastened dependency
collapse remain deferred.

## 2026-08-15 — Satsuma V1d.13 rear suspension articulation and staged NWH contact

Original-game comparison captures lock the reviewed rear order as trailing arm,
brake drum, coil spring and then shock absorber. The spring expands and raises
the body; the shock changes damping rather than being required to create spring
support. The locked donor scene places the installed left drum centre at chassis
local `(-0.6029993, -0.1500003, -1.16700029)` metres. Donor mesh bounds give a
temporary drum contact radius of `0.0871 m` and the loose collider gives an
`0.08 m` contact width.

The earlier rear arm `FixedJoint` prevented suspension articulation, while the
NWH WheelController transform was incorrectly raised by the `0.32 m` rest-length
configuration. NWH defines that transform as the suspension top and uses
`SpringMaxLength` for travel. Builder `11A-V1d.13` therefore places each reviewed
rear NWH top one `0.18 m` maximum travel above the exact donor drum centre. The
rear arm now uses a bounded chassis-connected `HingeJoint`; the drum remains a
`FixedJoint` child of the arm, so visible physical contact follows the articulated
assembly.

AssemblyGraph stays authoritative. Arm plus drum alone do not create invisible
support. Installing either accepted rear spring enables NWH contact at the drum
radius with low spring-only damping; installing the shock switches to the normal
damping profile; installing a road wheel switches to road-wheel radius, width
and grip. NWH's generated collider is rebuilt when that geometry stage changes,
preventing a hidden full-size tyre from remaining around a bare drum.

Installed-part interaction proxies no longer own nested kinematic Rigidbodies.
Their trigger is attached to the actual moving part body, eliminating the stale
ray target that stayed behind when an arm or drum articulated. The exact order
is now enforced on both rear corners: spring requires arm and drum, while shock
requires arm, drum and either accepted spring.

Builder `11A-V1d.13` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `22/22`. Focused physical PlayMode coverage
passed `5/5`, including arm world contact, drum-to-arm impulse transfer and an
NWH ground-contact fixture that measures non-zero spring force and verifies the
Rigidbody chassis rises. Rear hinge limits and final spring/damper calibration
remain provisional; front suspension, fasteners, unfastened dependency collapse
and manual garage acceptance remain pending. Audio was intentionally not changed.

## 2026-08-16 — Satsuma V1d.14 NWH-only rear suspension correction

Live comparison showed that the V1d.13 joint chain was still the wrong physical
boundary. It created a second rear-suspension solver beside NWH, left the donor
spring and shock as static mount presentation, and omitted the donor driveway's
Unity Default layer from the NWH ground mask. The result could pass an authored
test surface while the garage build showed a motionless spring and no support.

V1d.14 removes the rear `AssemblyInstalledPhysicsLink`, arm `HingeJoint` and
drum `FixedJoint`. NWH alone computes the contact and applies force to the
physical chassis. A project-owned rear presentation controller projects that
result onto the donor geometry: the arm rotates around its reviewed pivot, the
drum follows the NWH hub, the coil stretches between its donor top and lower-arm
seat, and the shock telescopes between its donor endpoints. The installed donor
objects stay kinematic and follow their mounts; their loose solid colliders are
disabled while a compact trigger remains available to the interaction ray.

The builder now records the reviewed left/right hub centres and both spring and
shock endpoint pairs. Installing arm plus drum alone still provides no hidden
support. Either accepted spring enables the `0.0871 m` bare-drum NWH contact and
its visible span follows physical travel; installing the shock adds damping.
The NWH mask includes Default as well as the project world-surface layers, which
matches the current temporary donor driveway collision.

Builder `11A-V1d.14` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `22/22`. Focused physical PlayMode coverage
passed `5/5`, including a Default-layer ground fixture, measured NWH spring
force, chassis `worldCenterOfMass` lift, visible spring compression/extension,
exact donor drum pose after the rendered-frame projection and close-range ray
selection. Manual two-corner garage acceptance and final rate/damping calibration
remain pending. Audio was intentionally not changed.

## 2026-08-16 — Satsuma V1d.15 exact static rear-suspension reset

The user rejected the V1d.13 articulated-joint attempt and the V1d.14 NWH
projection attempt after rendered comparison with the original game. Both had
allowed runtime simulation to overwrite the donor installation geometry. The
rear projector and spring/shock deformation components are therefore classified
`Rejected` and removed from the active runtime.

The new bounded authority is `MountPointSource;PivotSource;ConfigurationTransferred`:
exact car-local position and rotation are locked for the left/right trailing
arms, coil springs and shock absorbers. The import builder validates extracted
evidence against all six reviewed poses with a `2 mm` / `0.25°` drift gate and
writes the locked values to the generated mounts. Installed parts remain on
those mounts when the physical chassis translates or rotates.

Rear NWH support bindings and rear WheelController objects are disabled. NWH
does not position, deform or support any of the three rear parts in this pass.
Spring expansion, force, damping, ground contact and chassis lift are explicitly
unimplemented rather than approximated. The trailing-arm-owned drum socket is
inactive until its matching arm is installed and then follows that owner, which
removes the former garage-origin child transform failure.

Builder `11A-V1d.15` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode passed `23/23`; focused static-pose PlayMode passed `4/4`.
Manual two-corner rendered comparison is still required, so `P1.CAR.008`
remains `PartiallyImplemented`. Audio was intentionally not changed.

## 2026-08-17 — Satsuma V1d.19 physical rear spring after accepted pose lock

The user accepted the V1d.19 rear-part placement in the live garage, so this
pass preserves the locked arm, drum, spring and shock anchors. Read-only runtime
evidence was captured from the licensed donor after assembling the left rear
corner and saving. It confirms the installed relationship and the visible
compressed-to-expanded spring behavior; it is not used as donor runtime code.

The active rear corner is now an ordinary project-owned PhysX chain: a dynamic
trailing arm is hinged to the chassis and its drum is fixed to the arm. The
spring applies equal and opposite linear point forces between the reviewed
upper chassis seat and lower arm seat. It therefore pushes the arm down and the
Rigidbody chassis up instead of merely rotating a decorative hinge. Installing
the shock increases damping. Rear NWH authority remains disabled, as requested.

The donor coil renderer is not treated as an invalid unweighted skinned mesh.
A normal MeshFilter/MeshRenderer copy is fitted along its dominant mesh axis.
At installation it begins at a visible `0.062 m` compressed span and eases to
the physical seat-to-seat span over `0.55 s`, while the force ramps over the
same transition. The current stock/long rates (`32000`/`36000 N/m`), free
lengths (`0.24`/`0.27 m`) and damping (`650` spring-only, `2600 N*s/m` with
shock) are project-owned provisional calibration, not claimed donor constants.

Builder `11A-V1d.19` completed at `8/29/125/120/115/205/3/48`. Focused
physical PlayMode coverage passed `7/7`: the spring visibly extends by more than
`0.04 m`, drives the trailing-arm hinge, retains grounded drum contact and
raises the dynamic chassis by `0.0376 m` in the two-corner fixture. Generated
Satsuma EditMode coverage passed `23/23`, including explicit rate/damper guards.
Manual live acceptance of expansion feel and final calibration remains pending;
fasteners and audio are intentionally outside this pass.

## 2026-08-17 — Satsuma V1d.20 rear-drum weld stabilization

Live V1d.19 testing accepted the suspension motion but exposed visible rear-drum
jitter and separation from the moving trailing arm. The mount was correct; the
failure was the solver boundary. A separate dynamic drum on a basic FixedJoint
sat at the end of the `389 kg chassis -> hinge -> arm -> grounded drum` chain
while the spring could apply up to `6000 N`. Default solver quality allowed the
fixed constraint to stretch under simultaneous spring and ground contact.

The logical `Fixed` assembly-link mode now creates a fully locked
ConfigurableJoint weld. Its connected anchor is authored explicitly at the
accepted arm-owned socket; positional and rotational projection are limited to
`0.001 m` and `0.5 degrees`; preprocessing is disabled; and only the installed
arm/drum chain is raised to `20/8` solver/velocity iterations. Rigidbody
interpolation is matched and installation inherits point velocity. Detaching
still destroys the weld and restores the loose body's original solver and
interpolation settings. Spring geometry, rate, force and accepted part mounts
are unchanged.

Builder `11A-V1d.20` completed at `8/29/125/120/115/205/3/48`. The loaded
two-corner test continuously measured drum-to-socket error through all `150`
spring-expansion/settling physics steps: maximum translation was `0.0010 m`
and maximum rotation was `0.502 degrees`, while chassis lift remained over
`0.025 m`. Focused PlayMode passed `7/7`; generated Satsuma EditMode passed
`23/23`. Manual garage acceptance remains pending. True unfastened dependency
collapse remains a later fastener pass rather than being faked here.

## 2026-08-17 — donor floor-jack physical head and linkage correction

Read-only inspection of the locked `floor jack(itemx)` hierarchy and its Use,
Movement and CheckLift FSMs supersedes the earlier floor-jack virtual-pad
approximation. The vanilla root is a planar dynamic Rigidbody with mass, drag
and angular drag `9999`, Y plus pitch/roll frozen, and a `0.17 x 0.04 x 0.7 m`
base collider. Donor ground transport is the generic tool pickup path: a click
joins the still-dynamic planar base to the kinematic Hand through an unlimited
FixedJoint approximately `0.6 m` ahead of the camera and applies the donor
`0.8` tool movement multiplier. The layer matrix disables `Tools` base contact
against Datsun/vehicle-collider layers. The separate saddle lives on the `Parts`
layer, whose vehicle contact remains enabled; therefore the base passes under
the car while only the saddle transfers lift. Each raise command adds `0.015 m`
up to `0.38 m`; the target tweens for `0.5 s`, while that separate kinematic
solid `0.1 x 0.09 x 0.1 m` saddle follows at `0.32 m/s`. Lowering targets zero.
There are no donor jack points: normal PhysX contact against the vehicle
underside carries the load.

The project implementation now preserves that separation. The saddle renderer
uses an accepted local `+90 degree Y` presentation pivot, while its Rigidbody
and square collider remain on the unrotated physical head. The arm renderer is
restored to donor-local position `(0,0,0.2)` and its reviewed quaternion instead
of retaining a baked `-7.564 degree` flattening compensation. Each successful
pump separately drives the donor lever curve from rest to `40.18 degrees` local
Z at `0.3333334 s` and back to rest at `0.6666667 s`; lift authority remains
independent of presentation.

The repeated food-viewmodel animation was not a jack animation. Persisting
`lift-height` publishes the existing generic `Used` state event, whose
presentation fallback incorrectly classified every unknown tool as food. The
fallback is now `None`; real authored food/drink/smoke mappings are unchanged.

Generated presentation rebuilt successfully with `147` bindings. A subsequent
runtime stability correction now projects the camera/crosshair direction onto the
ground at the distance captured when dragging begins. The jack follows that XZ
target with a reviewed `6.5 m/s` transport cap. Its donor-authored saddle/front is
on local `-Z`, which is yaw-aligned to the projected camera direction on every
physics step. Camera yaw therefore moves the target and turns the jack in place
without orbiting its position around the player. The animated arm collider is
query-only; the separate saddle remains the sole solid moving load contact instead
of letting an animated child compound collider feed depenetration torque into the
base.

Vanilla's serialized `9999 kg` base is retained as donor evidence but rejected as
a modern transport mass: a dynamically moved body with that value could tow the
`389 kg` Satsuma like a bulldozer. The project uses the `30 kg` handling mass
measured in the inspected live MOPR/BetterMSC donor session. This does not change
lifting capacity because the independent kinematic saddle remains the physical
lift authority. During continuous ground drag, every discovered jack/vehicle
collision pair, including the moving kinematic saddle, stays ignored through the
following PhysX solves so neither part can tow the chassis. On release, and again
before any pump command, saddle/vehicle contact is restored immediately even when
the saddle already overlaps the underside; remaining base pairs restore once
clear. This closes the failure where a saddle released under the car stayed a
ghost and the jack passed through instead of lifting.

The lowered donor saddle also overlaps the floor-jack base collider by `40 mm`.
Because the base freezes Y, resolving that same-item contact pushed the whole
jack across XZ. Runtime binding now ignores only saddle contacts against solid
colliders attached to its own base Rigidbody. Saddle contact with the vehicle,
ground and arbitrary external loads remains enabled.

The earlier focused Items EditMode suite passed `47/47`; the revised handling
contract passed its exact EditMode check `1/1`. Focused floor-jack PlayMode now
passes `5/5`: one pump advances the physical head by `0.015 m`, the lever returns
to its authored rest pose, crosshair movement relocates the jack while keeping its
saddle/front aligned with camera yaw, drag filtering cannot tow a nearby vehicle,
saddle contact is restored on release, the same released saddle completes a full
physical lift/lower cycle under a vehicle-owned dynamic load, and its authored
overlap cannot self-propel or yaw the base. Final rendered in-game alignment
remains a manual acceptance step.

## 2026-08-18 — Satsuma V1d.21 front-suspension hub and strut correction

The licensed original was inspected read-only after both front corners had been
assembled, fully tightened, saved and reloaded. The external capture is stored
under
`runtime-inspection/satsuma-front-final-tightened-bilateral/20260818-005832/`.
It confirms that donor `wheelFL/wheelFR` are fixed steering carriers, while the
child `SpindleFL/SpindleFR` transforms are the moving physical hub centres. The
loaded hub positions are `(-0.6299995,-0.2587354,1.1670007)` and
`(0.6300010,-0.2587381,1.1670009)` in chassis-local space. The carrier-to-hub
vertical offset is about `-0.093737 m`; donor camber is `-1.4 degrees` left and
`+1.4 degrees` right. Wishbone body pivots, hub-to-wishbone offsets,
hub-to-spindle presentation offsets and both shock-bottom targets were captured
from the same settled state rather than inferred from loose-part poses.

Front support authority is now NWH-only. Each corner is enabled by the installed
wishbone, spindle and strut graph; a road wheel changes the existing support from
the bare `0.12 m` hub profile to the authored tire profile, but is not required
to make the assembled suspension react. NWH owns contact, travel and hub pose.
The project adapter projects that single physical result into the donor
wishbone, spindle mount, installed fasteners and ray targets. Rear NWH remains
disabled and the accepted project rear-suspension implementation is unchanged.

The donor front strut is not treated as a rigid loose mesh after installation.
Its reviewed left/right two-bind-pose mesh is presented through a project-owned
two-bone `SkinnedMeshRenderer`: the upper bone follows the installed strut mount
and the lower bone follows NWH's moving shock target. AssemblyGraph remains the
sole install/order/save authority; no donor FSM, code or runtime assembly enters
the project. Stable IDs, the `115` mount count and save schema are unchanged.

Builder `11A-V1d.21` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `24/24`; the focused real-prefab front-corner
PlayMode passed `1/1`; the complete Satsuma installed-part physics PlayMode class
passed `8/8`. The latter installs both front wishbones, spindles and struts,
verifies front-only NWH support, chassis lift, bare-hub dimensions and continuous
agreement between NWH hub position and both moving donor presentation targets.
Manual garage feel, steering under load and the deferred global fastener/tool
pass remain pending.

## 2026-08-18 — Satsuma V1d.22 suspension install-handoff correction

The accepted front and rear suspension simulations were not changed. The defect
was presentation order: the generic carry root interpolated toward the mount as
a rigid loose mesh, while the front strut two-bone skin and fitted rear spring
were enabled only after `PartInstance.IsInstalled`. This made a front strut or
rear spring fly toward the old horizontal pose and then snap into its correct
physical rig on the final frame.

`VehicleAssemblyController` now exposes a presentation-only install-transition
contract. The front strut reconstructs its exact loose appearance from the
reviewed bind poses, then interpolates its upper/lower preview bones to the mount
and NWH shock target. A rear spring replaces its loose renderer at handoff start,
fits between interpolated endpoints, reaches the mount compressed to `0.062 m`,
and only then lets the existing rear controller perform the accepted expansion.
AssemblyGraph, NWH front authority, custom rear authority, stable IDs, mount
count, physics state and save schema remain unchanged. Per user request, the
generic visual flight duration is reduced from `0.34 s` to `0.17 s`.

Builder `11A-V1d.22` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode passed `24/24`; the focused no-snap real-prefab PlayMode passed
`1/1`; the complete installed-part physics class passed `9/9`; generic assembly
EditMode and PlayMode passed `23/23` and `11/11`. Manual in-game visual
acceptance remains pending.

## 2026-08-18 — Satsuma V1d.24 incomplete-suspension and subframe force path

The live remake comparison exposed two authority gaps. Installed rear trailing
arms, front wishbones and front spindles had no world-contact authority until a
spring or strut enabled the completed suspension controller. The floor jack's
solid saddle could also contact the shell but passed through the installed
subframe because that part remained a kinematic presentation object.

Incomplete suspension now stays inside ordinary project-owned PhysX. A rear arm
uses its existing chassis hinge without a spring; a front corner forms the
temporary structural chain `shell -> fixed subframe -> hinged wishbone -> fixed
spindle`. Solid part colliders remain enabled and collide with the ground while
only self-conflicting chassis/part pairs are filtered. Installing the front
strut performs an explicit authority handoff: the temporary wishbone/spindle
joints are retired, their solid contact is disabled and the accepted NWH front
corner becomes the sole travel/contact solver. Removing the strut restores the
full-droop physical chain. No invisible NWH wheel supports an incomplete corner.

The installed subframe is now a dynamic solid body welded to the shell by a
project-owned projected `ConfigurableJoint`. Consequently the donor-style
kinematic jack saddle can contact the subframe itself and transfer lift through
the weld to the shell; there is no special-case jack point, parenting or virtual
lift force. Preferred connected-body bindings keep front forces in the actual
subassembly, while falling back safely to the shell if an out-of-order legacy
state is encountered and reattaching when the preferred owner becomes installed.

Builder `11A-V1d.24` completed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode passed `24/24`; complete installed-part physics PlayMode passed
`11/11`; complete physical floor-jack PlayMode passed `6/6`, including direct
subframe contact lifting the real generated shell. Stable IDs, `115` mounts and
the save schema are unchanged. Manual in-game contact feel and streaming reload
remain acceptance steps; global fastener/tool correction remains deferred.

### 2026-08-18 — Springless front-wishbone vehicle-frame axis correction

Live tilted-car testing showed that the temporary physical wishbones behaved
correctly on level ground but twisted around presentation-space axes when the
body rolled. The imported subframe root is rotated for donor mesh presentation;
its local `forward` is therefore not the car's longitudinal suspension axis.

The subframe remains the wishbone's physical connected body and force path, but
hinge construction now separates parent selection from axis authority. Both
front wishbones derive their hinge axis from the project-owned vehicle assembly
frame. This correction applies only while the strut is absent and the temporary
PhysX chain owns the corner; the accepted strut-installed NWH handoff is
unchanged. A bilateral PlayMode regression instantiates the complete generated
Satsuma already rolled and pitched, leaves both struts uninstalled, loads both
spindles and verifies chassis-axis alignment plus hinge-anchor stability.

Focused tilted-corner PlayMode passed `1/1`; the complete installed-part physics
class passed `12/12`. No generated asset, stable ID, mount, dependency or save
schema changed.

## 2026-08-18 — Satsuma front steering, brakes and road wheels

The licensed original was inspected read-only after the user installed and
tightened both front steering chains, brake discs and road wheels, then saved
and reloaded. The external capture is stored under
`runtime-inspection/satsuma-front-steering-brakes-wheels-final/20260818-174942/`.
It supplies the exact chassis-local steering-rack and steering-column poses,
left/right steering-rod roots and two-bone bind hierarchy, NWH non-rotating hub
targets, and the distinct left/right disc and road-wheel offsets.

The generated assembly keeps one physical authority. NWH `NonRotating` drives
the spindle and steering-rod outer target; NWH `Rotating` drives the installed
disc and road-wheel mounts. A project-owned two-bone skin presents each donor
steering rod between its fixed rack-side root and the moving hub target. The
disc and wheel remain ordinary assembly parts, while installing the wheel
switches the existing NWH contact from the bare-hub profile to the authored
`0.272667 m` road-tire radius and `0.169054 m` width. No decorative duplicate
wheel or second contact solver is created.

For the current manual assembly pass only, the four stock wheels retain their
stable IDs and definitions but override their remote donor startup coordinates
with a collision-safe stack beside the spawned Satsuma. GT wheels remain at
their donor-authored positions. Existing saves may still restore a previously
saved loose-wheel pose; the temporary stack is authoritative for a fresh game.

The baseline rebuild completed successfully. Focused generated EditMode checks
for the transferred rig and temporary stock-wheel stack passed `1/1` each; the
real-prefab PlayMode test installing both rods, discs and stock front wheels
passed `1/1`, including NWH rotating/non-rotating propagation. Fastener state is
intentionally deferred. The donor/user correction distinguishes two steering-
rod fasteners: the outer fastener closes the rod-to-hub mechanical connection,
while the separate rod adjuster governs/locks toe. Consequently the missing
linkage-driven hub alignment remains part of the deferred global fastener pass,
not a wheel-contact or suspension-authority defect.

## 2026-08-19 — Satsuma V1d.28 whole-car fastener, install routing and persistence correction

The generated aggregate now gives every one of its `235` bolt or nut targets
the shared project layer `bolt-gayka-only`. The selected-wrench query masks to
that layer exclusively, so installed panels and parts cannot occlude fasteners
and ordinary interactions cannot leak into tool mode. A target exists only
while its owning part is installed. Its visible presentation uses the reviewed
donor `bolt2` mesh, `BOLTS` material and donor fastener texture as a private
Phase-1 `TemporaryDirectImport`; gameplay state remains project-owned.

The user-approved outline contract is now authoritative: green is stage zero,
yellow is partially tightened, white is maximum stage and red means the held
wrench size is incompatible. The outline deliberately remains at the end stops.
The BetterMSC reference is reimplemented rather than loaded: aiming snaps the
selected non-physical wrench to a retained anchor on the fastener, and opposite
mouse-wheel directions advance or reverse the discrete AssemblyGraph stage.
One accepted notch starts a bounded work arc and regrip; further notches are
consumed until that cycle completes, so visual timing limits graph mutation.

Direct donor hierarchy evidence now locks the subframe to exactly four `10 mm`
BoltPM markers under its `Bolts` owner. The two front wishbones each retain two
`10 mm` markers reprojected from donor carrier space into the generated mount
frame. Three same-transform neighbour FSM markers were explicitly rejected as
non-subframe evidence. The read-only assembled-front runtime dump
`runtime-inspection/satsuma-front-steering-brakes-wheels-final/20260818-174942`
(SHA-256 `c8b9b6037838d6172b64bc44c676a116afbbd13f313ce70b14f8ba50056b9fce`)
adds four `13 mm` lug markers per wheel and one `14 mm` outer
steering-rod-to-hub marker per side. Those outer markers are parented to the
project-owned moving hub pivots; the distinct donor rod adjuster remains toe
tuning evidence rather than a duplicate joint bolt.

Surface handoff follows occupied prerequisites through subframe, wishbone,
spindle, strut/disc and wheel. The previous `32`-result physics query could be
filled by the chassis' dense collider/proxy set before the correct socket was
considered, which was the actual source of the intermittent side-angle-only
installation. The ordered fixed query now retains `256` hits, mount handoff can
bypass only an enclosing collider belonging to the same assembly owner, and the
disc/wheel direct volumes are bounded at `0.20/0.24 m`. This replaces the broken
selection path without reviving donor-sized full-wheel-well triggers.

Inspection of the actual local `slot-01/current.save.json` found the
`vehicle.satsuma` domain present but containing `vehicles=0`. The production
Satsuma is spawned beneath `GameCompositionRoot`, which Unity moves into its
hidden `DontDestroyOnLoad` scene; enumerating `SceneManager.sceneCount` therefore
never registered that hierarchy with `VehicleSaveParticipant`. Native save
initialization now explicitly and idempotently registers the persistent
composition hierarchy in addition to normal loaded scenes. A generated-prefab
round-trip proves one vehicle with `126` parts and `235` fasteners, including an
installed trailing arm, a partially tightened fastener and an arbitrary loose
GT-wheel world pose/rotation.

A second reload regression destroys the original aggregate, instantiates a
clean generated prefab and restores installed state, a fastener at stage `3`
and an arbitrary loose-wheel transform. The full save-integration fixture also
exposed and fixed a stale Rigidbody restore plan for an intentionally destroyed
streaming clone; a discarded canonical clone is now skipped instead of aborting
the load before the vehicle domain can restore.

This is a registration fix, not data resurrection. The already-written empty
slot contains no prior assembly state to recover; the corrected state is
captured by saves made after this build. Builder `11A-V1d.28` passed at
`8/29/125/120/115/235/3/50`. Generated/install EditMode passed `27/27`,
interaction EditMode `41/41`, assembly EditMode `23/23`, assembly/production
Bootstrap PlayMode `12/12`, and save integration `13/13`.

## 2026-08-20 — P0 Satsuma BoltCheck parity repair (11A-V1d.30)

The locked donor `Assembly`, `Use` and `BoltCheck` FSM evidence is now imported
as three separate project-owned states: a part may be `Installed`, its mount has
an aggregate integer `Tightness`, and the mount owns a hysteretic `IsBolted`
latch. `IsFullySafe` remains a fourth derived state at aggregate maximum. A
single non-zero fastener stage no longer blocks removal unless that mount's
evidence-backed group has actually crossed `BoltedOnThreshold`; after crossing,
the latch remains set until `BoltedOffThreshold` is reached. Physical attachment
still begins at `Installed`. Only a mount with an explicit donor BREAK policy
may detach from the speed-retention path.

Importer evidence collection reads both `db_PartRequired` and
`db_PartRequired1`, every `ActivateThis\d*` presentation root (including the
stock shock's second root), the special `TriggerWheel/Bolts` child, and donor
`BoltedYES`, `BoltedNO`, clamp/max, speed and Chance operands. A donor Assembly
that references a real `Bolts` object now fails generation when no compatible
generated fastener exists. Generation occurs in a bounded staging root; only a
validated staging result atomically replaces the canonical Satsuma folder, with
rollback if promotion fails.

Locked rear/wheel invariants are:

- each rear trailing arm: `2 x 12 mm`, aggregate max `16`, latch `12/0`;
- each rear drum: `1 x 14 mm`, aggregate max `8`, latch `8/0`;
- each stock rear shock: `12 + 6 + 6 mm`, aggregate max `24`, latch `2/0`, with
  both donor presentation roots retained;
- each wheel mount: four unique `13 mm` nuts, aggregate max `32`, latch `1/0`,
  and donor BREAK evaluation at `5/33 km/h` using the extracted normalized
  Chance formula.

Spring, stock shock and drum use their matching trailing arm's `Installed` state
as the placement gate and its `IsBolted` latch as structural-retention authority;
they are otherwise independent. Installing one on a loose arm is intentionally
accepted, then the unsupported arm and its dependent construction collapse back
to loose world parts. A wheel requires its matching disc or drum to be installed,
but no unsupported drum-Bolted gate was invented. Carried-part ray queries now
discard incompatible direct sockets before priority selection, so a neighbouring
arm/spindle socket can no longer mask the compatible target with a false
"incompatible part" result. The generated aggregate is now `126 parts / 117
mounts / 252 fasteners / 117 fastener groups / 52 owned mounts`.

Assembly save schema `2` persists the group latch. Exact legacy additive shapes
`115/205` and `115/220` are accepted. If an installed legacy part predates newly
authored fasteners, only its missing fasteners migrate to fully tightened and
latched; existing stages and every loose pose remain unchanged, while an empty
mount stays absent/reset. Older serialized mount assets whose new inline group
deserializes as an empty object receive the bounded compatibility group derived
from their existing `RequiredForRemoval` fasteners.

Strict rebuild `11A-V1d.30` passed at
`8/29/125/120/117/252/3/52`. After that prefab timestamp, the exact P0 contract
plus generated invariants, generic Assembly, native save integration and the
carried-socket selection regression passed together at `69/69 x3`; complete
rear/wheel Satsuma physics passed `16/16 x3`. The rear response fixture observed
unloaded/loaded/recovered spring
lengths `0.19751 / 0.08250 / 0.19751 m`, rebound peak `6.1069 m/s`, and a settled
tail of `0.0000 m/s`. Manual full-car wrench, road-speed BREAK and streaming
acceptance remain pending; no donor FSM or donor runtime assembly became runtime
authority.

## 2026-08-31 — Satsuma front fastener mesh / steering endpoint audit

Frozen GAME MeshFilters show six upper strut nuts, twenty short bolts and four
long wishbone bolts; forcing the nut mesh onto all thirty front targets was
incorrect. The outer steering bones also inherit an OFFSET parent translated
by +/-50 mm, omitted in the previous hub-local table. V1d.35 corrects these
presentation mappings without changing accepted front contact physics or saves.
The donor SteeringFL/FR connection requires rod Bolted (8/0 hysteresis) and an
installed strut, distinct from the always hub-parented rod skin. Full free-yaw
hinge/snap behavior and marker-scaled fastener animation remain known gaps.
Source IDs, hashes, tests and current validation status:
`Docs/Phase1/SATSUMA_FRONT_FASTENER_MESH_AND_ROD_OFFSET_2026-08-31.md`.

## 2026-08-21 — Satsuma road-wheel rest parity (11A-V1d.31)

Read-only inspection of donor `Wheel.cs` (SHA-256
`85ffbf994222e04ac61a32bebc9cdeae9c7efe53df7d7bf75910ea80bc051945`,
case-normalized) confirms that rolling resistance is load-dependent
(`normalForce * rollingFrictionCoefficient * radiusLoaded`) and that the donor
integrator snaps angular velocity to exact zero when the friction step would
cross zero. This is behavioral evidence, not donor runtime code in the remake.

The front NWH adapter now applies the same observable rest result only when the
wheel is grounded, loaded, undriven and carrying a small residual spin while the
chassis is stationary. It clears both NWH angular integration samples after the
contact step; moving, rotating, airborne, driven or deliberately fast-spinning
wheels remain untouched. The rear road wheels remain free axle hinges and use a
separate contact-backed rest latch against chassis motion, removing only the
axle component of residual angular velocity. Internal suspension-joint jitter
is not mistaken for vehicle motion, and no wheel is welded merely to hide the
symptom.

Builder `11A-V1d.31` retains the existing stable IDs, `117` mounts, `252`
fasteners, schema `2`, front-NWH/rear-custom authority split and donor wheel
poses. The generated prefab predates all final reports. NWH EditMode passed
`30/30`, generated Satsuma EditMode passed `27/27`, the focused rear loaded-rest
fixture passed `1/1`, and the complete installed-part physics suite passed
`16/16 x3`. Manual long-idle observation on level and sloped ground remains
pending.

## 2026-08-31 — Map vegetation placement and streaming rebuild

This section records the historical setup state. Its pending pilot/full-map
language is superseded by the active v7 evidence recorded below; the original
source and implementation chronology is retained for audit.

The user authorized removing/recreating the existing generated vegetation while
preserving the canonical map and accepted Phase 1 systems. Read-only evidence is
reused from source revision `msc-world-baseline-04a1.1-c3f2f337`, frozen donor GAME
SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Individual transforms and recovered paired-card roots are distinguished in the
source report. Canonical Grass1, Grass2 and Skijump grass define positive natural
ground evidence; Fields/TRACKFIELD, actual LakeSimple/LakeNice tiles, roads,
rail, structures and gameplay routes supply exclusions. Under-map water helpers
are not confused with real shoreline geometry.

Source positions are `WorldLayoutReference`; project-owned placement/grounding
algorithms are separately `Reimplemented`; generated private presentation is
separately `TemporaryDirectImport`. No donor runtime code, raw payload or
licensing/platform logic enters Git. Canonical source-to-project translation is
already applied; the new settings matrix is only an optional identity-default
post-import correction. Existing streaming and GPU vegetation contracts are
extended, not replaced. No gameplay stable ID or save schema changes.

Isolated runtime/Editor compilation passed twice. Actual Unity EditMode passed
42/42 at 2026-08-31 14:59:44Z; PlayMode, pilot output, full-map counts, boundary
occlusion and performance acceptance remain pending. Full implementation,
provenance, commands, limitations and current evidence:
`Docs/WorldRemaster/MAP_VEGETATION_REBUILD.md`.

### RailCol footprint correction — 2026-08-31

Read-only inspection confirmed that `MAP/MESH/RAILROAD/RailCol`, stable ID
`4d31d93f9a9fb2ae2a58947cc149a6aa`, is a legitimate pair of narrow rail
colliders, not a hidden map-wide blocker. The canonical collider manifest has
two `BoxCollider` records (source IDs 93225 and 93226), each sized
`(3300, 0.08, 0.2)` metres and centered at `(0, ±0.79, 0)`. The imported
parent quaternion is `(-0.67540944, -0.20933713, -0.2093374, 0.6754095)`;
project position is `(1285.2723, 0.21104085, -1705.8378)`.

The vegetation query incorrectly used each rotated collider's world-axis
bounds as its exclusion footprint. This expanded a narrow diagonal strip
to about 2.72 × 1.87 kilometres (5.08 square kilometres) and caused blanket
`Railway` rejection in several southern cells. The project-owned query now
projects the eight transformed box corners through its twelve face triangles;
the existing railway clearances remain applied to the actual strips. Bounds
remain only the spatial/debug envelope, and unsupported collider shapes retain
their documented conservative fallback. Canonical scene geometry, colliders,
rail layout and donor files are unchanged.

The measured rail dimensions/transforms are `WorldLayoutReference`; the
placement-query correction is `Reimplemented`. Regression fixtures cover the
actual 3300-metre diagonal pair, preserved rail buffers, and parent rotation,
nonuniform scale and nonzero collider centers. Execution of these newly added
fixtures is pending the next Unity EditMode run.

### 2026-09-01 — Vegetation presentation correction — active v7

The active private Phase 1 output keeps the accepted 66,910 tree
identities/positions and assigns 65% spruce, 20% pine, 7.5% birch and 7.5%
aspen: 43,492 / 13,382 / 5,018 / 5,018. No donor extraction, terrain edit or
tree-position replanning is performed. Canonical ground UV/base-colour remains a
grass-only `WorldLayoutReference` mask after natural-ground and gameplay
exclusions; colour cannot authorize roads, roofs, fields or water.

Spruce now uses five reviewed ALP optimized sources: `Big01`, `Big02`, `Big04`,
`Small03` and `Small04`. The independently recomputed archive SHA-256 is
`39D47D1A7A8424A3C1F39DD55DD64ED6CEE42542A13466CC04FB40280126B827`;
the selective import has 20 seeds, 105 imported assets, 11 pruned previous
assets and zero unresolved external GUIDs. `Big03`, `Small01`, broken-texture
`Small02`, group prefabs, demos, scripts and the non-flying AudioSource-only bird
prefab are excluded. Reviewed ALP `Rock01/02/03` and `stone01/02` feed the sparse
rock pool. This payload is `ThirdPartyPrivatePhase1Presentation`,
`productionReady=false`: no purchase receipt or other licence proof is stored,
so it remains a removable private dependency and blocks ProductionReady or
distribution claims.

Pine, birch and aspen retain their reviewed licensed Chernobyl non-spruce
bindings at 13,382 / 5,018 / 5,018; the old Chernobyl grass and Engelmann spruce
bindings are the superseded part. Meadow grass, grey willow, fern, moss,
selected Armillaria/Russula mushrooms and dead grass come from the licensed
NatureManufacture Finnish subset, classified
`LicensedThirdPartyPhase1Presentation`. Forest v1.8.8 SHA-256
`1EC0E2BFC04FE03DBE92D737B2FC236F4DE0BB9143221FB000E5CCE57B5BC864`
uses 22 seeds / 79 closure assets; Meadow v2.9.3 SHA-256
`34AE8C7EB27064DD001251F8B1A59444CE9C0A12AF119BA01F0A88F25179DC38`
uses 51 / 156. Both closures have zero overwritten assets, unresolved external
GUIDs, demo scenes, scripts or Editor assets.

Forest-floor binding v3, run `20260901-091800-66925a3b`, maps
`BranchLitter01` from `prefab_detail_branches_01` SHA-256
`1a7faf3a940bdc0d82890ded7a97083120737fbe5535983561e84b1d90b3d2ac`
onto the preserved `DeadGrass02` slot and `PoplarLeafLitter01` from
`prefab_detail_poplar_leaves_01_1` SHA-256
`02c9d8b11055014c4d19c2a18f107db0ff5a1f7231503c8364e56348d169a3fd`
onto `DeadGrass03`. Actual serialized-prefab references across all 88 scene
YAMLs contain 986 branch-litter and 951 poplar-leaf-litter instances. Slot/GUID
preservation adds no records; the shrubs/forest-floor total remains 28,759.

Final grass binding v6 uses reviewed Meadow detailed/regular/cross sources for
near/middle/far geometry, a 0.65 m grid and three-source-texel green-mask
dilation. Pilot `20260901-092200` in `cell_-4_-3` passed with
1,967 / 1,727 / 1,793 / 223,100 saved original-tree, boundary-tree,
forest-floor and grass counts from 620,500 candidates. Settings hash
`2ffbfeb149eadcfbe39801703dc0546c7c7f9011eddf1d2104f0bc5a5bf63569`,
source fingerprint
`55ee507db015d4e9fc68f3034e999487dfa174cb9ab8da3a165046c2114dc407`
and pilot fingerprint
`fa868c935f994e13a0a1fb637a45c0184729ae9cf003c3c8e9431983e12b5da6`
are locked. Its five-view HDRP capture passed on an NVIDIA GeForce RTX 4070
SUPER with zero missing/unsupported shaders; this is representative pilot
acceptance, not a whole-map continuous-carpet claim.

Full `RunAllBatch` run `20260901-092757` passed 88/88 with zero errors. A fresh
Unity process then ran `ValidateAllBatch` as `20260901-100750`: 88/88 passed,
`validationOnly=true`, with identical settings/source fingerprints and every
per-cell field, fingerprint and count. Totals are 37,678 original + 29,232
boundary = 66,910 trees, 28,759 shrubs/forest-floor and 8,077,749 grass from
54,599,726 candidates. The 88 ignored private scenes total 482,661,729 bytes;
largest `cell_-4_-3` is 27,079,109 bytes. Active evidence is WorldRemaster
EditMode 41/41, forest-floor binding 5/5, green-mask 15/15 and graphics-enabled
GPU PlayMode 6/6, all with zero failures/skips. Generated payload, reports,
backups and captures remain outside Git; project-owned tools and audit metadata
remain `Reimplemented`.

Runtime pacing and bounded metadata/GPU budgets reduce secondary spikes but do
not fix native scene completion. The retained fresh-process observation is
608.743 ms total (478.810 ms deserialization + 129.870 ms integration) in
`LoadSceneOperation.CompleteAwakeSequence`; v7 was not reprofiled by that v6
measurement. The one recommended next milestone is a measured compact-woody
runtime-catalog pilot. Full hashes, bindings and limits are in
`Docs/WorldRemaster/MAP_VEGETATION_PRESENTATION_REVISION.md`.

The earlier 50/35 mix, Chernobyl grass and Engelmann spruce correction and its
run IDs remain superseded historical evidence in the ledger; retained Chernobyl
pine/birch/aspen bindings are active and are not included in that supersession.

### 2026-09-02 — Vegetation dense-infill and packed-streaming correction — active v12

The v7 section above remains historical evidence. Active private Phase 1
vegetation v12 reuses the same canonical sanitized map source as a read-only
`WorldLayoutReference`; it does not extract new donor content or edit terrain,
roads, yards, fields, water, routes or `OpenSpace`. The project-owned
deterministic infill policy retains 37,678 accepted donor originals and adds
24,490 eligible forest trees, capped at 65% of that accepted basis. Together
they form 62,168 `OriginalTrees`; 5,811 grounded near-boundary trees bring the
near total to 67,979. The requested 65/20/7.5/7.5 policy resolves to 44,201
spruce, 13,576 pine, 5,052 birch and 5,150 aspen.

Grass v12 uses only the reviewed non-cereal Forest Environment
`Grass02_3`, `Grass01_3` and `Grass03_3` sources at 46/32/22. The active output
contains 5,157,636 records in profile counts 2,368,747 / 1,651,618 / 1,137,271;
shrubs and forest floor contain 18,747 records. Corrected grass-art provenance
is `Artifacts/VegetationRebuild/GrassArt/provenance.json`, SHA-256
`76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`.
The selected vendor art remains removable
`LicensedThirdPartyPhase1Presentation`, `productionReady=false`; no reauthored
production-vegetation claim is made.

Woody presentation is now packed. The 88 near-cell scenes contain 86,726
matrices and matching placement metadata, 2,130 prototype references, 47,138
batches and 62,168 bounded runtime collision records. Packed assets total
76,558,979 bytes and scene YAML totals 1,088,371 bytes. Those scenes serialize
zero prefab-instance GameObjects, direct mesh renderers or collider components.
Twenty-five collision-pool overflow estimates remain explicit profiling
warnings rather than hidden errors.

The collisionless distant layer contains 16,000 trees in 81 streamed scenes,
307 renderers and 349,465 vertices, with zero colliders and zero exact crown
gaps. Global presentation fingerprint
`d75455716d1faee823b0ceb4841c46358dd95c484b5f6ac3d234d3a719475097`
replaces all 45 canonical rock visuals with 149 renderers and zero generated
colliders; all missing, duplicate, unexpected-anchor and metadata-mismatch
counts are zero. Existing audited legacy rock collision remains authoritative.

Full run `20260902-001303` passed 88/88 with report SHA-256
`F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`.
Fresh `ValidateAllBatch` run `20260902-004112` passed the same 88 cells with
`validationOnly=true`; report SHA-256 is
`A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`.
Both use settings hash
`7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d`
and source fingerprint
`2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`.
`Artifacts/VegetationRebuild/full-validate-exact-v12.json` proves exact equality
of all 88 cell reports and 268 non-report artifacts; aggregate fingerprint is
`a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169`.

Targeted EditMode passed 77/77 and core PlayMode passed 22/22. The direct Editor
benchmark passed at 66.0798 ms maximum main-thread time, 71.6469 ms maximum
yield interval and 20.6325 ms p95 yield interval. The production-service route
passed at 63.8666 / 68.1513 / 14.5884 ms and completed cleanup with no owned
scenes, renderers or valid GPU buffers remaining. This is a substantial Editor
improvement, not Player traversal, resident-memory, representative GPU or 60
FPS proof. The generated private payload remains `TemporaryDirectImport`,
`productionReady=false`; no Phase 2 approval or save/gameplay migration follows.

### 2026-09-02 — Packed vegetation temporal-smear correction — runtime v12.1

The reported radial smearing of nearby trees was traced to the project-owned
packed draw contract, not to donor geometry, tree placement, authored LODs or
vendor materials. `PackedWoodyCellRenderer` submitted current `Matrix4x4[]`
instance data while requesting per-object motion vectors for its nearest LOD;
the draw supplied no per-instance `prevObjectToWorld` transform. Other packed
LODs forced zero vectors, so the packed path also represented camera motion
inconsistently.

Runtime v12.1 binds `MotionVectorGenerationMode.Camera` for every packed woody
`RenderParams` initializer. The 88 generated v12 cells and their fingerprints,
counts and provenance remain authoritative and require no regeneration. The
post-capture focused EditMode suite passed 26/26; XML SHA-256 is
`07BCE65CDF8647FCD72D7A41C65678E54C291DEF7C67A6E25146E5F3C60B4373`.
The isolated vegetation compile passed. The graphics-enabled temporal audit ran
with TAA, object-motion-vector frame settings and gameplay-equivalent motion
blur; its report passed all seven views with zero missing or unsupported
shaders, SHA-256
`7AC14981327EFDA338AE3E6AADF3442058F29F5F2DCB60BAD4E7D5966BEDBCC4`.
Manual inspection of the forest and road eye-level frames found sharp trees and
no radial smear. The user's same-location in-game comparison remains the only
visual acceptance gate. No donor payload, stable ID, save schema, terrain,
road, collider, streaming ownership or vendor file changed. Exact
wind-deformation object vectors remain outside this bounded correction and
would require previous per-instance transforms.

### 2026-09-02 — Phase 1 interaction pickup-hand presentation

Read-only staging inspection identified donor Texture2D `gui_uset.png` as the
requested open-palm interaction icon. The 32 x 32 source SHA-256 is
`7939FE87B8F6F3154595AFF54AE17B0B4A19EA7062DCE3D3281B254266BB62A2`.
`Phase1InteractionUiImporter` verifies that hash, performs a byte-for-byte copy
into the ignored private runtime baseline, applies deterministic GUID
`f627a294dcce4353b85d5269d8d90116`, and validates the player-prefab binding.

The copy is classified `TemporaryDirectImport` with replacement key
`ui.interaction.pickup-hand`. It contributes pixels only: no donor UI layout,
MonoBehaviour, FSM, controller, assembly or input logic is imported. The
project-owned `CrossdotPresenter`, interaction capabilities and locale adapter
remain runtime authority. The import command, focused interaction EditMode
49/49, item EditMode 47/47, icon-source contract 1/1, UI locale-routing
PlayMode 1/1 and voice-locale PlayMode 1/1 passed. Visual acceptance in the
normal Game view remains manual.

### 2026-09-02 — Satsuma body/material/fastener/rain parity V55

Read-only inspection of the frozen `GAME.unity`, donor paint/window/rim
materials, `windshield.cs` and the loose-fender Assembly records established
the active body contract. Starting `PaintType=0` retains
`CAR_PAINT_RUSTY` rather than selecting matte paint; body, doors, fenders, hood
and bootlid are seven independent paint owners. The nine stock body mounts use
32 short bolts with exact 5/6/8/10 mm groups. Inactive front-fender
`ActivateThis` mudflap renderers `5511/12503` are presentation placeholders,
while the separate mudflaps remain installable. Steel rims are wholly
`RIM_PAINT_RUSTY`; GT inners are rusty and GT outers metallic.

Dynamic rain writes were found on the five window renderers through
`windshield.cs` only; no equivalent whole-body car-paint wetness controller was
found in the locked donor. V55 therefore reimplements the 512-pixel glass atlas,
three donor rain types, gravity 40 and front/cabin intensity split behind
project-owned weather/HDRP boundaries. Generic whole-car wetness remains Phase
2. Wiper clearing remains a separate vehicle-electrics follow-up, not an
assembly follow-up: the donor wipers are fixed/non-removable, and their
`ButtonWipers/Function` FSM requires `Electrics/ElectricsOK` plus
`WiringSwitchLights/Data/Installed` before moving both pivots and feeding their
positions to `windshield.cs`. The donor `Electrics` FSM derives `ElectricsOK`
from installed/connected battery and electrical parts plus usable charge.
Builder V55 passed and the
generated-content EditMode suite passed 37/37. Exact values, causes, boundaries
and manual checks are recorded in
`Docs/Phase1/SATSUMA_BODY_MATERIALS_AND_RAIN_PARITY_2026-09-02.md`.

### 2026-09-02 — Satsuma installed-door impulse and hinge-pose correction

The frozen hinged-assembly audit retains the exact left/right parent pivots,
`+Z` axes, `0..80` / `-80..0` limits, opposite `120` torque vectors,
`1100` break values and four fasteners per door. The donor `Use` FSM applies
hinge limits and torque while held. Because the Satsuma joint is created during
assembly rather than serialized statically, connected-body collision is marked
as an inference from the frozen vehicle-door HingeJoint inventory, whose audited
records all have `EnableCollision=false`.

The recreation defect combined four errors: a kinematic installed door retained
solid collision against the dynamic chassis; generic late pose synchronization
overwrote the hinge angle with the closed mount rotation; the hinge incorrectly
exposed an `F` tool action; and release damping could erase velocity before its
remaining displacement was integrated. The runtime now ignores only
door-to-connected-body solid pairs while installed, restores them on detach,
delegates installed pose synchronization to the hinge angle owner, and uses
mouse-held acceleration with time-correct inertial decay. The raw donor states
also confirm a held-close snap/latch in the final approximately `10 deg` and
mouse-only input. Completely unfastened breakaway now occurs at full opening,
not the former project `12%` threshold.
No mount, fastener, stable-ID or save schema changed. Focused hinge PlayMode
passed `1/1`, assembly plus hinge passed `12/12`, and generated Satsuma EditMode
passed `37/37`. Manual left/right in-game acceptance remains pending; the
parallel Bootstrap/save lifecycle was not changed.

### 2026-09-03 — Player weight and bathroom-scale evidence

Read-only inspection of frozen `Assets/Resources/PlayMakerGlobals.asset`
(SHA-256 `0C232081E5DD2D6611D27CCD06274CBB31F5FABC5EF5E5F901B86D39DA25AFEC`)
confirmed `PlayerWeight=83`. Frozen `_Scenes/GAME.unity` component `111907`
on `YARD/Building/BEDROOM2/LOD_bedroom2/SCALE/Gauge` contains the `Measure`
FSM: `FloatOperator` maps `PlayerWeight * -2.78` into `Scale`, `EaseFloat`
transitions for `2 s`, `SetRotation` writes local Z, and the enter/leave
distance comparison uses `0.2 m`.

The project reimplements that presentation through stable gauge entity
`066254c4582fa4c1e44162588d101bb7`; no donor name lookup, FSM or script runs.
The slightly expanded project foot volume is documented compatibility tuning
for the different CharacterController origin. Gravity-equivalent support load,
jump-force tuning, faster posture rise and the raised eye line are project/user
tuning and are not claimed as exact donor calculations. Focused EditMode passed
`76/76`; focused locomotion PlayMode passed `14/14`; the production
Bootstrap/home smoke passed `1/1` and resolved the streamed gauge by stable ID.

A same-day regression audit found that the first project-owned support load
used `OnControllerColliderHit` together with the retained `90 degree` traversal
slope limit. A slightly upward vehicle-side normal could therefore replace the
floor as support, apply the full `83 kg` load as corner torque, and feed a
render-frame impulse back through sprung body motion. The correction uses a
dedicated downward feet probe with `normal.y >= 0.55`, searches through
the nearest non-player surface before selecting its receiver, and applies a
ramped continuous force in `FixedUpdate`. Static ground and unrelated kinematic
geometry now occlude deeper Rigidbodies; a kinematic hood/bootlid can route load
only to a dynamic chassis in its own parent chain. Horizontal nudging is restricted
to an explicit available `IPickupTarget`; vehicle bodies and arbitrary light
Rigidbodies are excluded. Full-project compilation passed with zero errors and
the Unity `6000.3.11f1` locomotion PlayMode run passed `21/21`, including
sprung-support settling, corner-contact rejection, static-ground occlusion and a
rolling `600 kg` body retaining its velocity under player contact. The new
occlusion regression was the sole failure against the pre-fix runtime (`20/21`),
which reproduced the reported continued vehicle pushing after it began rolling.
In-game scale motion, active Satsuma suspension, four-corner contact and
camera-feel acceptance remain manual.

A second same-day reproduction separated the remaining defects. A compound
vehicle with a low rocker and taller side made the centre-only feet ray report
false support while crouched; that regression failed alone (`21/22`). The motor
now requires centre plus four footprint rays, `0.2 s` confirmation and rejects
support whenever the capsule has a side contact with the same dynamic aggregate.
The corrected isolated support suite passed `22/22`.

The final rolling-car fault did not come from the scripted nudge or weight force:
raw PhysX contact between the moving CharacterController and an oncoming
`600 kg` body changed its velocity from `+0.2 m/s` to approximately
`-1.1157 m/s`. A callback-time velocity restore could not reliably precede the
later solver step and was rejected. Builder `11A-V1d.60` preserves the
frozen donor collider roles: 25 world-facing chassis records exclude project
layer 9, while the four `PlayerOnlyColl` records are parented below one kinematic
player-only proxy. This blocks the controller without connecting its solver
impulse to the dynamic chassis; weight probing can still resolve the structurally
nested chassis. Builder `11A-V1d.60` passed; after that rebuild the exact
generated-prefab contract passed `1/1`, runtime activation/isolation passed
`2/2`, and both isolated and
full-project locomotion suites passed `23/23`. The broader generated-content
fixture was `38/39`; its only failure was an unrelated existing NUnit
`Has.Count` bootlid-presentation assertion, while the collision contract passed
inside the same run and in isolation. Manual in-game pushing against the
assembled Satsuma remains the acceptance gate.

### 2026-09-03 — Satsuma panel lifetime and bootlid presentation V58

The user-observed partial-open-then-immediate-close fault was not intentional
door logic. The actual runtime `HingeJoint` was being destroyed because old
donor values (`500` bootlid, `1100` doors, `1000` hood) had been copied directly
into Unity 6 `breakForce` and `breakTorque`; ordinary moving-chassis constraint
impulses can cross those values. Once the component vanished, generic installed
pose synchronization restored the closed mount pose and the hinge capability
became unavailable. V57 keeps those exact values on
`AssemblyHingeMountAuthoring` for donor evidence, gives the actual Unity 6 joint
infinite break force/torque, and leaves visible unfastened breakaway under the
existing deterministic assembly-graph rule at full opening.

The donor bootlid's four `BoltPM` transforms (`45424`, `49935`, `55885`,
`69737`) each have local Z scale `0.5`. The donor `Screw` step moves its visible
child `-0.0025 m` in that scaled space, producing `1.25 mm` effective travel per
stage and `10 mm` total. Reparenting the visible child in the remake had lost
that scale and doubled the travel. `AssemblyFastenerInteractionTarget` now
accepts an authored presentation-travel scale; the builder transfers the source
Z magnitude for preserved loose body bolts, leaving all other accepted
fasteners at `1.0`.

The apparently missing bootlid handle was donor object `bootlid_emblem`
(`Transform51604`), active at identity pose beneath `bootlid(Clone)` and backed
by mesh `datsun_bootlid_001` with bounds extents
`(0.280406, 0.031166509, 0.108053)`. The blanket emblem filter disabled this
whole exterior handle/garnish assembly. V58 preserves this exact object while
continuing to suppress optional rally decals, starting registration plates and
other badge clutter.

The donor `Use` FSM `105480` enters `State 1` after the bootlid reaches its
opening endpoint. That state's only action narrows the live `HingeJoint` limits
to `-70..-69` degrees; the `Drop` close path first restores `-70..0`. V58 now
uses that exact one-degree hold for a fastened bootlid. Partial travel remains
fully physical and keeps angular inertia, while held RMB releases the narrow
window before applying close torque.

The initial V58 `bootlid_hooks` conclusion was incomplete because it inspected
only serialized active state. Assembly FSM `104306`, state `End`, deactivates
the chassis copy (`GameObject24240`, `Transform60308`) and activates the moving
lid copy (`GameObject17462`, `Transform53524`) plus `Handles`. Removal FSM
`110021`, state `Remove part`, restores the chassis copy and disables both
moving groups. V60 implements this exact mutually-exclusive presentation swap.

The same V60 correction replaces `HingeJoint.angle` latch decisions with the
Rigidbody's signed rotation in its authored mount frame. The one-degree
project-corrected endpoint must remain valid for two consecutive fixed steps,
preventing a false 60–70% left-door snap and the mirrored right-door refusal.

Builder `11A-V1d.60` completed with 125 loose parts, 117 mounts, 280 fasteners
and four hinges (log SHA-256
`17C5B67FC1AC7F10013DBBF2EEF2A5706305C03C4BFB39F536A0A8237ED82CB9`).
Generated-content EditMode passed `39/39` (SHA-256
`68D5F3D385CD164F527751EB97F8D4EE845F85D4F1A66EC827E83480F13CA41A`), and
focused physical hinge PlayMode passed `4/4` (SHA-256
`5C4D89DCE72C9CA28E276F9CEA5722804740F1EE86D4A8D394910315BC5CD9F9`).
The user accepted the V60 in-game result on 2026-09-03 after checking both
mirrored door latches and the corrected bootlid behavior. The bounded
hinged-panel correction is therefore user accepted; this does not promote the
complete Satsuma feature row to `Verified`.
