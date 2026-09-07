# My Summer Remake Save Master — implementation record

Date: 2026-09-05. Scope: the user's explicitly requested standalone save-editing utility, independent of Phase 1 runtime milestones. Status: implemented and tested as an offline utility; gameplay acceptance of arbitrary edited saves is not claimed.

## Inspected and reused

- Repository instructions and the existing dirty working tree; accepted runtime/UI baseline remained intact.
- `Prompts/09A_FULL_GAME_NATIVE_SAVE_FOUNDATION.md`, Phase 1 scope/execution documents and `Docs/Save/NATIVE_SAVE_ARCHITECTURE.md`.
- Current project-owned `SaveDocument`, `SaveDocumentCodec`, validation, storage, native participants and their DTOs. Current document version is 17; previous installed player build uses 16.
- Assembly, fastener, electrical, needs, item, calendar and economy validation, and existing authored configuration definitions. Original game installation, original runtime binaries and MSC Editor source/assets were not accessed or incorporated.

## Delivered behavior

A .NET 9 / WinForms Windows x64 application, titled **My Summer Remake Save Master**, in `Tools/SaveMaster`. Current release **1.1.0** opens native save documents 16, 17 and 18 without migrations, exposes all existing payload scalars through a domain tree and global search, and retains unknown content. The initial real v17 QA copy exposed 15 domains and 8,265 fields; the later v18 copy exposed 9,394 fields, and the current operating-model copy exposes **11,685 fields**.

The application provides Russian labels/help, field editing, a raw JSON section editor, protected identity/schema fields, diff review, undo/redo, validation, save-as export and safe overwrite with a unique verified backup. Advanced JSON editing preserves object fields, scalar types and protected collection membership; it is not an unrestricted entity generator.

Workshop actions edit inserted bolts on occupied mounts with per-definition limits and group hysteresis, including automatic group synchronization when editing an individual bolt stage in the table. The electrical panel uses the 26 current connection identities and synchronizes terminal prerequisites. Separate money/date actions maintain economy ledger and time/calendar invariants. Full mounting/dismantling automation, new-item generation and systems not present in the save are outside this version.

Existing runtime game code, scenes, stable IDs, save DTOs, document version and the approved 08A UI are unchanged. No migration was added. The application lives outside `Assets`; it adds no Unity, native plugin, NuGet or donor runtime dependencies.

## Files

- `Tools/SaveMaster/Core/`: native codec, session/history/diff, atomic storage, schema metadata/validators, workshop actions and scalar configuration catalogs.
- `Tools/SaveMaster/App/`: Windows application, field index, themed controls, dialogs and opt-in application-level UI check.
- `Tools/SaveMaster/Tests/`: standalone regression harness, synthetic Unity fixtures, vehicle/action tests and isolated original-codec compatibility probe.
- `Tools/SaveMaster/Build.ps1`, `Start.cmd`, `Update-SchemaCatalogs.ps1`, `README_RU.md`, `SCHEMA.md`, `.gitignore`: reproducible build, entry point, catalog regeneration/hash checks, instructions, limits and audit.
- `.gitignore`: allow the hand-authored tool `.csproj` files while keeping generated projects and build output ignored.
- `Tools/README.md`, this document: discoverability and evidence.

The generated executable and adjacent required runtime configuration/DLL files are in ignored `Builds/SaveMaster`. Machine-specific QA copies and reports are in ignored `Artifacts/SaveMaster`. No commits or pushes were made.

## Validation

Executed commands:

```powershell
dotnet build Tools/SaveMaster/App/SaveMaster.App.csproj -c Release --nologo
dotnet run --project Tools/SaveMaster/Tests/SaveMaster.Tests.csproj -c Release
./Tools/SaveMaster/Tests/Verify-UnityCodec.ps1
./Tools/SaveMaster/Build.ps1
```

The native probe runs a separate minimal Unity project under ignored `Artifacts`, with copies of four current project-owned save source files and an author-written probe. It neither opens nor modifies the main Unity project.

- Original Unity codec compatibility: **264/264** synthetic edited saves verified, including precise randomized double metadata, Unicode, control characters and opaque unknown domain preservation.
- Existing native slots: **3/3** read without modifications (v17/v17/v16); schema checks found zero errors. Copies edited only under `Artifacts` were accepted by the original Unity codec **3/3**. Source hashes remained unchanged.
- Core/action regression: **57/57 passed**; see `Artifacts/SaveMaster/regression-tests.txt`. Coverage includes integrity, corruption, duplicate JSON, numeric limits, unsupported versions, unknown data, concurrent file changes, backups, history, individual and bulk fasteners/group thresholds, wiring, legal gearbox values, balance ledger and calendar coherence.
- Application-level UI check: actual controls populated all 8,265 fields, searched globally, applied a fractional hunger value using the editor button, undid/redid it, filtered changed fields and exported/reopened a valid copy without modifying its source. The workshop dialog was instantiated with actual copied vehicle data. The app's own controls were rendered to PNG and visually inspected for layout.
- The Windows Computer Use helper could enumerate/read accessible controls, but screen capture failed with `SetIsBorderRequired: E_NOINTERFACE (0x80004002)` and click geometry was unavailable. No claim is made of a completed external mouse-driven acceptance pass; application-level control checks and rendered QA were used instead.

UI check command accepts only an explicitly supplied fixture and output directory:

```powershell
& 'Builds/SaveMaster/My Summer Remake Save Master.exe' --ui-check '<save-copy-path>' '<new-qa-output-directory>'
```

## Safety boundaries and remaining acceptance

### 1.0.1 responsiveness investigation

The user reported a genuine Windows "not responding" condition after save information was already visible, through any opening route. This is distinct from a stale editor selection. The user's actual hung process has not yet been available for thread capture; the exact hang is **unconfirmed and not claimed fixed**.

An actual visible WinForms message-loop check reproduced a separate defect in 1.0.0: `SelectionChanged` ran before `CurrentCell` was updated, leaving the editor on the previous field. Heartbeats remained healthy during this failure. Binding now uses `CurrentCellChanged`, guards re-entry, retains typed input when the same field is reselected, and refuses an apply if editor/selection identity differs.

Opening now prepares the read-only document, checksum, domain snapshots, schema checks and field index on a worker. It publishes one completed model on the UI thread, preserves the previous session on a read/prepare failure, and disables editing during loading. File/slot dialogs are disposed before opening begins; drag/drop defers opening until the Explorer OLE callback has returned. Global error dialogs have an explicit window owner. The broad wait cursor spanning interactive dialogs was removed. The runtime save contracts and write/backup implementation are unchanged.

Local asynchronous load-stage logs are written under `%LOCALAPPDATA%/MySummerRemakeSaveMaster/Logs`. They include paths/timing/error text, no field payload or network transmission. An ignored diagnostic helper under `Artifacts/SaveMaster/HangDiagnostics` can capture CPU samples, the UI thread's Windows wait chain, a five-second EventPipe sample trace and a normal process dump of an existing Save Master process, using the .NET diagnostics client already installed with the SDK. No package was installed. Wait-chain absence alone cannot rule out a managed deadlock.

Executed visible message-loop regression evidence: `Artifacts/SaveMaster/ResponsivenessAsyncFull/responsiveness-check.txt` and its JSONL detail. Numeric/bool selection, preserved uncommitted text, focus, scrolling, debounce and the actual My Saves modal/open route passed. A deliberately blocked worker continued to receive UI timer ticks and repaint for two seconds (33 ticks, maximum gap 94 ms); the previous model remained intact until completion. An injected I/O exception restored controls and retained the previous session. Hashes of all three native source slots remained unchanged. This fault-injection evidence demonstrates UI responsiveness under delayed I/O; it does **not** reproduce or explain the user's later freeze.

The opt-in check runs against an explicit fixture and writes only diagnostic output, without saving the supplied slot:

```powershell
& 'Builds/SaveMaster/My Summer Remake Save Master.exe' --responsiveness-check '<save-path>' '<new-report-directory>'
```

Final packaged 1.0.1 checks: `--responsiveness-stress` completed in 45.2 seconds, including resize, maximize, restore and ten seconds of idle after loading. UI heartbeat, real repaint, bounded layout activity, selection and row membership passed (`Artifacts/SaveMaster/Responsiveness101Stress`). The packaged `--ui-check` separately passed typed editing, undo/redo, changed filtering, workshop rendering and export/reopen to a new QA file (`Artifacts/SaveMaster/Ui101Final`). Core regression was rerun: 57 passed, zero failed; release build: zero errors/warnings.

The user raised possible launch-path confusion. Several diagnostic builds existed and the canonical `Builds/SaveMaster` had deliberately remained at 1.0.0 for comparison. It was then updated to 1.0.1; `Tools/SaveMaster/Start.cmd` is the canonical launcher and the title displays the version. The prior build is retained separately under ignored `Artifacts/SaveMaster` for reproducibility. This resolves which binary to launch, but does not establish the cause of the originally reported hang.

Subsequent observation of a user-launched process found the intermediate `App/bin/Release/net9.0-windows` executable, already displaying 1.0.1. Its log confirmed slot 01 loaded 15 domains / 8,265 fields and returned to the message pump in 181 ms. Five external process samples all returned `Responding=true`, with 0–2.1% CPU relative to one core. A local trace/dump was captured under `Artifacts/SaveMaster/UserProcess33304`; this is evidence of a healthy observed opening, not a capture of the earlier hang.

### 1.0.2 document 18 compatibility — 2026-09-06

The later report that fields could not be edited had a confirmed cause: the user's slot had `Header.DocumentVersion=18`, while 1.0.1 permitted only 16/17. In addition, the Satsuma payload now used assembly schema 3, which the editor's assembly validator did not support. Both gates needed updating; simply permitting header 18 would still leave the vehicle domain read-only.

Inspected the existing project-owned `SatsumaDynamicAssemblySaveMigration`, `VehicleAssemblySaveData`, `VehicleItemPartCatalog` and `VehicleItemSaveRestorePlanFactory`. Extended the standalone validator to combine base and dynamic part occupancy, validate the four current consumable mappings and physical state, and reject contradictory ownership across item/vehicle/world domains. Existing individual/bulk fastener actions now accept schema 3. Stable IDs, collection membership, optional presence bits, file version and unknown data remain preserved; no game migration is executed by the editor.

Updated `Core/SaveSession.cs`, `SchemaValidator.cs`, `SchemaActions.cs`, `SchemaCatalog.cs`, and the two embedded scalar catalogs; `App/MainWindow.cs`, `MainWindow.Loading.cs`, `MainWindow.Dialogs.cs`, and the application project version; `Tests/Program.cs`, `VehicleRegression.cs`, and tool documentation. Unsupported document/domain versions now have a specific explanation in the field editor and status bar instead of a generic disabled control. The canonical `Builds/SaveMaster` release and `Start.cmd` entry point serve 1.0.2.

Catalog regeneration reused only current project scalar definitions: 334 fasteners (+29), 149 mounts (+7), 152 item types. No prior fastener maxima changed and no IDs were removed; eight existing mounts gained their current fastener composition. `Update-SchemaCatalogs.ps1 -Check` passed. No donor files, Unity runtime code, scenes, accepted milestone APIs or save DTOs were modified; no migrations or dependencies were added.

Validation: core **67/67** passed, including preserved v16/v17 behavior, read-only v19/99, dynamic loose/installed items, invalid ownership, fastener actions and edit/undo/export with unknown fields. An isolated Unity **6000.6.0f1** project generated 264 v18 fixtures; .NET edited/exported **264/264**, then the current Unity codec accepted **264/264** (`Artifacts/SaveMaster/V18/CodecCompatibility`). This did not open or migrate the actual game project.

The current v18 slot was inspected read-only: all 15 domains editable, zero errors/warnings; bulk tighten/loosen on in-memory clones preserved `dynamicParts`. The packaged UI check used an ignored copy with 9,394 fields, applied an actual editor-button change, undid/redid it, filtered, opened the workshop and exported/reopened a new file (`Artifacts/SaveMaster/V18/Ui102`). The actual Unity codec separately accepted that GUI export **1/1**, preserving header 18 (`Artifacts/SaveMaster/V18/NativeUiVerify`). The final canonical executable passed the real message-loop responsiveness check in 28.4 seconds, including delayed/failed loading, field selection and the My Saves route (`Artifacts/SaveMaster/V18/Responsiveness102`). All three native slot hashes matched before/after snapshots. Full gameplay acceptance of edited copies remains the single next milestone; codec/DTO validation does not simulate gameplay. No manual Unity setup is needed to use the editor.

Required local edits are checked before disk writes, with checksum verification before and after serialization. Backup bytes are verified before atomic replacement; stale sources and save-as collisions are refused. A second displaced-file backup detects a narrow race where another process renames a save during replacement. The editor is not a cross-process transaction with the game: close the player and exit Unity Play Mode before replacing a live slot.

The local validator cannot prove every catalog-dependent gameplay outcome, every NPC/service transition or loaded-scene binding. Save integrity is not gameplay acceptance. Header metadata, including its `UpdatedUtc` representation, remains unchanged; filesystem modification time reflects the edit. Export retains the original `SlotId` and is not automatically registered as another slot. Refer to `SCHEMA.md` for precise domain limitations.

No Unity setup steps are required to use the application. Manual acceptance still needed: with the game closed, edit a copied slot, keep its backup, then load that copy in the matching remake build and inspect player, wiring, selected fasteners and engine adjustments. The current tuning copy uses document 18. No live user slot was replaced during implementation.

Provenance: this tool reuses project-owned native contracts and already-integrated scalar configuration/mapping data. It performs no new donor transfer and contains no donor binary payload, so the donor porting ledger/matrix need no fabricated transfer rows. No Phase 1 parity row was upgraded to Verified and no Phase 2 work began.

### 1.1.0 current vehicle tuning — 2026-09-06

User scope: both compatibility with the current Satsuma implementation and a convenient dedicated tuning panel. Audited the current assembly mechanical condition, valve adjustment, service caps, operating state, simulation persistence, dynamic item mapping, and the Phase 1 engine player guide/live-engine/operating-model documentation. Reused the existing session, codec, domain validation, Russian WinForms theme, workshop and undo stack.

Delivered **Настройка авто** with four tabs, vehicle selection, editable value cells, checkboxes, per-field help/ranges, optional explicit suggested values, staged edits across tabs, and one atomic apply/undo. The current copied slot supplies **81 fields**. Numeric display is shortened without rewriting unedited values. Invalid input remains in the dialog and does not partially mutate the session. Read-only simulation snapshots are distinguished from real adjustments; loose camshaft gear/steering alignment are read-only because game restore randomizes them.

The catalog describes only existing active fields and links purchased condition to the corresponding item stable ID. Optional default inline DTOs with a false/absent presence bit remain inactive. Active unknown DTO versions make the domain read-only. Protects presence flags, service-cap kinds/order and existing identities/structure. Battery voltage couples to existing normalized charge; mechanical condition 0 couples to broken=true. Increasing condition preserves a pre-existing broken flag unless explicitly cleared. Old oil contents above 3 L remain valid: service capacity is a suggestion, not a new save-format maximum.

New files: `Core/VehicleTuningCatalog.cs`, `Core/SaveSession.VehicleTuning.cs`, `Core/SchemaValidator.VehicleTuning.cs`, `App/MainWindow.VehicleTuning.cs`, and `Tests/VehicleTuningRegression.cs`. Extended `Core/SaveSession.cs`, `SchemaValidator.cs`, `SchemaCatalog.cs`, refreshed the two embedded scalar catalog snapshots, wired the new window through `App/MainWindow.cs`, `MainWindow.Dialogs.cs`, `MainWindow.UiCheck.cs`, bumped `SaveMaster.App.csproj`, registered tests in `Tests/Program.cs`, and updated README/schema/this record. The refreshed mount definition now recognizes the five actual rocker-shaft mounting bolts; the eight valve screws have their own saved settings. Catalog counts remain 334 fasteners / 149 mounts / 152 item types.

Executed validation:

```powershell
./Tools/SaveMaster/Update-SchemaCatalogs.ps1
./Tools/SaveMaster/Update-SchemaCatalogs.ps1 -Check
dotnet run --project Tools/SaveMaster/Tests/SaveMaster.Tests.csproj -c Release
./Tools/SaveMaster/Build.ps1
# Final packaged EXE against an ignored fresh copy:
& 'Builds/SaveMaster/My Summer Remake Save Master.exe' --ui-check '<copy>' '<Ui110Final>'
& 'Builds/SaveMaster/My Summer Remake Save Master.exe' --responsiveness-check '<copy>' '<Responsiveness110>'
./Tools/SaveMaster/Tests/Verify-UnityCodec.ps1 -Mode Verify -ArtifactRoot '<NativeTuningVerify>' -InputDirectory '<CodecInputs>'
```

- Core regression **86/86 passed**: 19 added tuning scenarios plus 67 prior cases. Includes old 16/17 saves, active future schemas, hidden defaults, all nine mechanical owners, eight valves, invalid ranges/owner/kinds, dynamic item ownership, atomic rejection, one-step history, export and unknown-data preservation. Found and fixed the missing `batteryCharge01` branch in old saves. Existing balance/ledger tests pass; money behavior was not changed.
- Release app build: **zero errors/warnings**. Final packaged UI check rendered all four tabs, rejected invalid input without closing/mutating, applied a grid edit, exported a tuned copy, restored the earlier state with one undo, and completed ordinary search/edit/export. Rendered images were visually inspected.
- Final packaged responsiveness check: **28.4 seconds**, passed blocked/failed I/O, real message-loop heartbeat/repaint, field selection, search/scrolling and My Saves route. This does not claim to reproduce the earlier user-reported hang.
- Current Unity **6000.6.0f1** codec accepted both actual GUI-produced exports: **2/2**, including the tuning edit. Probe runs only the four project-owned codec files in an isolated ignored project. Initial invocation found zero inputs because the probe filters `*.mscsave.json`; copied exports with that fixture extension, then reran successfully. No game project was opened and no gameplay/drive acceptance is claimed.
- All **3/3 native source hashes unchanged** before/after. Current copy has 15 editable domains and zero validation errors. Evidence under ignored `Artifacts/SaveMaster/Tuning110/`: `regressions.log`, `build.log`, `inspection.log`, `Ui110Final`, `Responsiveness110`, `NativeTuningVerify`, source hash snapshots.

Canonical package is `Builds/SaveMaster`, launched by `Tools/SaveMaster/Start.cmd`; versioned copy and archive are `Builds/SaveMaster-1.1.0` and `Builds/MySummerRemakeSaveMaster-1.1.0-win-x64.zip`. Existing game code, donor installation, scenes, accepted 00–08A foundations/UI, native DTOs, stable IDs and save versions were not modified by this update. No migrations, dependencies or donor transfer were added. Full in-game assembly/engine behavior remains dependent on the actual vehicle state; the panel does not guarantee a start or automatically construct missing parts.

Exactly one next milestone: **Save Master — edited-copy gameplay acceptance** (native load and representative player/Satsuma checks in the matching remake build).
