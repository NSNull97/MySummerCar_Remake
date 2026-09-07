# Night run — 5 September 2026

User-authorized autonomous work ends at **08:00 Asia/Yekaterinburg / 03:00 UTC**.
No new implementation starts after that cutoff. Finish any already-running
check safely and report what actually ran; do not imply manual acceptance.

Final safe checkpoint: E2a and its separate read-only stage/frame audit are
finished. All implementation/test lanes are frozen, Unity is closed, and no
new mutation packet is started in the remaining minutes before08:00. The night
automation can be retired; the next work is specified, not silently implemented.

## Completed in this run

- Suspension A/B behaviour accepted by the user remains unchanged.
- C access/manual-removal package: scoped14→0, Edit278/278 and final Play61/61.
  Manual C acceptance remains pending. The uncertain rear-arm removal with a
  remaining spring is deliberately not guessed; rally variants and other
  speed-break/ground gates remain separate work.
- Engine E1 access package: eight existing definitions, refresh8→0,
  Edit125/125 (including19E1), assembly Play11/11. Exactly8 of120 protected files
  changed versus pre-E1; the other109 definitions, prefab, full manifest and
  build settings stayed byte-identical. No physics, fastener-threshold,
  shared-crank-B, Drop, schema or stable-ID changes.
- E1 report and provenance records updated; the bounded engine donor audit is
  available in `SATSUMA_ENGINE_ASSEMBLY_AUDIT_2026-09-05.md`.
- Cockpit C1a: meters ON12/OFF0 and structural wiper-switch availability
  implemented. Scoped refresh1→0; Edit190/190 (49new+141regressions), Play31/31
  (assembly11+installedphysics20), zero failures/skips. Final120 hashes unchanged
  versus refresh; only the meters definition differs versus pre-C1a. Save
  hysteresis preserved, no schema/physics/prefab change. Report/provenance/manual
  checklist: `SATSUMA_COCKPIT_DASHBOARD_GATE_FIX_2026-09-05.md`. Manual pending;
  this packet is NOT included in the earlier delivered player build024640.
- Cockpit C1b: wheel installation requires column Installed, not Bolted, via
  an additive installation-only predicate and physical surface routing; ON2/OFF0.
  Scoped refresh1→0; Edit411/411 (49new) and Play61/61 passed, failed0/skipped0.
  Final120 hashes match refresh; only wheel SO changed versus pre-C1b, all119
  others unchanged. Legacy wheel-without-column and B=true/T1 saves round-trip.
  No inverse removal/collapse/ownership/physics/schema change. Manual pending.
  Report: `SATSUMA_COCKPIT_STEERING_RULES_FIX_2026-09-05.md`. Not in build024640.
- Cockpit C2 physical ignition/key access: LMB Off/ACC/START, realtime0.4s,
  donor attempted-start latch, installed-column gate, explicit starter circuit
  and input adapter preserving all other channels. Required logical key-access
  domain adds native17 with deterministic16->17 migration; existing ignitionOn
  bool restores Off/ACC, not transient START. Scoped prefab refresh1then0;
  final Edit518/518, fixturePlay63/63 and real-D3D11 nativeBootstrapPlay1/1 passed,
  zero failures/skips. Native test uses two actual RequestLoad/Bootstrap reloads
  for savedfalse and migratedtrue before world reveal. All6 existing user save
  hashes unchanged; onlyprefab changed among120 protected files, final hashes
  stable after tests;19 added YAML blocks,0 removed,only4old reference blocks
  changed. Manual pending. Report: `SATSUMA_COCKPIT_IGNITION_C2_2026-09-05.md`
  retains initial compile/assertion/hold-ownership/fixture failures and fixes.
  **Build024640 predates C1a/C1b/C2 and cannot load native17. Never write an
  upgraded v17 slot using that old v16 player; use a new build for C2 testing.**
- Engine E2a presentation: oilpan9 short bolts; gearbox6 long + boltpm-5 short.
  Scoped refresh16then0, Edit542/542 and fixturePlay63/63 passed with zero
  failures/skips. Only16 MeshFilter GUIDs changed in the canonical prefab;
  all other7382-record payload is unchanged, no records added/removed. Only
  prefab changed among404 protected files; all6 user-save/backup hashes remain
  unchanged. No ownership/count/size/pose/stage/latch/schema/physics changes.
  Donor oilpan child-.02Z versus gearboxzero is validated by the source hook,
  not blindly applied to the runtime base pose. Full stage/rest-frame parity
  remains separate. Report: `SATSUMA_ENGINE_FASTENER_PRESENTATION_E2A_2026-09-05.md`.
  Manual pending; not included in build024640.

## Completed build window — retained failure/repair history

User-requested separate task: `01a06e01-469a-7e93-b6ba-d50695c79625`.
It owns the complete private Windows x64 build from the canonical dirty project,
not a stale clean-worktree copy. It received `BUILD_WINDOW_RELEASED` after E1
tests and a fresh empty Unity-process check. The first full build finished with
Success (233 scenes; 2,281,957,387 bytes; 00:10:57), but D3D12 and D3D11 smoke
both crashed before the menu with `level20 is corrupted` / `Position out of
bounds`. That first executable is **not accepted as a working build**. The later
024640 build passed after repair, and the window was returned; see below.

`level20` maps to `World_Cell_-3_0_Legacy.unity`. The builder identified a
concrete source defect: `DonorWorldSupplementalEntityMetadata` is a second public
MonoBehaviour in the baseline metadata class's source file. Its 328 records in
nine generated scenes have guidless script references, with corresponding
missing-script build warnings. Root independently checked the source class and
cell-3_0 records. Whether this fully explains the crash requires another smoke.

The builder alone is authorized for the bounded correction: move that class
unchanged to its matching source file with a stable new script GUID; repair only
the reviewed supplemental `m_Script` bindings; preserve all other serialized
payload, stable IDs, transforms, meshes, manifests and scene ordering. Require
preflight/backups, targeted validators/tests and a new unique build/smoke. Do not
delete level20, old builds, user saves, Library or donor data. Build artifacts
and crash logs are under `Builds/PrivatePhase1_20260905_014400`.

Second narrowly authorized repair: `LightingCalibrationProfile` is another
secondary Unity object class in `LightingQualityProfile.cs`; the existing
`Phase1LightingCalibration.asset` has `m_Script: {fileID: 0}`. Both smoke logs
show `ProductionLightingInstaller.InitializeRuntime` dereferencing the missing
calibration. The builder may extract that class unchanged into a matching
source file/new script GUID and repair exactly the calibration asset's script
reference. Preserve its asset GUID `0e90f74987c27c84182a4fee278e3691`, the old
quality script GUID `6b33a83f12b8d5e43a88ce6173208989`, catalog bindings and all
authored values (including dusk12/dawn2). No lighting rebuild or default-value
replacement. Require exact preflight, backup, masked-payload preservation and
MonoScript/type/asset/catalog validation before the next full build/smoke.

The same GUID-only supplemental repair is authorized for the single ignored
upstream scene `Assets/Scenes/Generated/FullMap_UnifiedBaseTerrain.unity` so
future cellization does not reintroduce the bindings. Independent streamed
inspection found328 unique source IDs,328 unique cell IDs and identical sets.
The source is740430586bytes; pre-repair SHA256
`AD38F0ED329E52D90473E2AA7B1C18FBA824DDC226BCC61C6DEB74ED931E80E8`.
Require memory-bounded rewrite of only328 script-reference lines, preserved
encoding/newlines and masked payload hash, not whole-scene resave or generation.
The original guidless scene references resolve to embedded MonoScript records;
they are a build-only script-binding defect, not dangling local object IDs.
Leave those embedded records and all other payload unchanged.

Builder checkpoint after repair: compilation completed without reported
compile/missing-script warnings, and four focused EditMode checks passed.
The D3D11 farm reload test obtained typed supplemental components, then failed
on its obsolete expected manifest literal `10b-r1-v3`. Root independently
checked the authoritative manifest and cell records are `10b-r3-v6`; the test
diff changes only that literal. The builder is rerunning that test. Retain the
original failed result; its earlier nographics attempt was invalid for HDRP.
These are intermediate results, not proof the standalone player crash is fixed.

Fresh build `Builds/PrivatePhase1_20260905_024640` completed successfully.
Builder reports its D3D11 smoke reached initialized UI without the earlier
level20 corruption/crash or lighting null reference. A separate packaging
failure omitted the installed Wwise native library: build log580/597 records
failure writing the Profile `AkUnitySoundEngine.dll.meta`, and the first smoke
contains `DllNotFoundException`. Root verified the source DLL and the old build
have the same SHA256
`418DE8D6B9D10728A3D9F4A778B010B1507DD56BA54931214257776D137416D9`.
Builder may repair only the private build artifact by adding that exact
5270824-byte Windows x86_64 Profile DLL to its Data/Plugins/x86_64 directory.
Root verified the copied artifact hash matches. Label this post-build patched;
this is an installed third-party plugin, not project-owned or donor code.
Do not change vendor source/meta. Retain the failed smoke and require a new
smoke checking native initialization, banks, responsiveness and new errors.
The importer packaging defect remains documented debt for future fresh builds.

**BUILD_WINDOW_RETURNED received, 2026-09-05 around03:09 local.** Builder
finished both patched D3D11 and D3D12 smokes: responsive player, UI initialized,
Wwise sound engine initialized, no former serialization/lighting/native-DLL
errors. Root read both logs and the repair report, verified the executable
exists and no Unity/player process remains. The user can test the executable
in `Builds/PrivatePhase1_20260905_024640`. No New Game or load/save UI journey
was exercised by the builder; all five existing save/backup hashes reportedly
remained unchanged. Window capture was unavailable, so do not claim screenshot
or input-driven menu verification. Wwise packaging debt above still applies.

Root can resume scoped cockpit work after a fresh process/worktree check;
agents retain their read-only scope until given explicit file ownership. No
more build polling is needed. Full report:
`PRIVATE_BUILD_SERIALIZED_SCRIPT_REFERENCE_REPAIR_2026-09-05.md`.

## Next work, in order

1. C1a, C1b and C2 are finished at their bounded automated gates; do not repeat
   them. All runtime/Editor/test lanes are frozen except explicitly assigned
   next work. Preserve accepted save hysteresis, install-only wheel rules,
   disabled device input map and ignition/access restore semantics. The cockpit
   audit and C2 report distinguish verified evidence from pending full cockpit,
   key audio, seated driving, gauges, warning lamps and progression work.
2. E2a is finished at its automated gate: refresh16then0, Edit542/542,
   fixturePlay63/63, protected404 and user-save6 checks passed. Code/test lanes
   are frozen. The bounded read-only follow-up is complete and root checked
   its raw states/parameters/source actions/current frames. Raw oilpan-.02Z
   is pre-init pose, not a special stage-zero base. A separate effective travel
   gap is proven for the two.7-scale representatives:20mm project versus14mm
   donor at stage8; marker/child rotation ownership also needs all16-ID review.
   No runtime correction was made. Next packet specification:
   `SATSUMA_ENGINE_FASTENER_STAGE_FRAME_AUDIT_2026-09-05.md`.
   No new full rebuild or player build; no new mutation packet near the cutoff.
3. Rocker-cover duplicate retirement, rocker-shaft valve-adjuster separation,
   shared crank B, piston Drop and other engine fastener/sequence work remain
   separate packages requiring explicit save-safe design and donor evidence.
   A visual E2a does not establish full engine assembly or driving readiness.

The engine fastener ownership audit has completed and root reviewed all329
lines. Confirmed checkpoint: eight valve adjusters were counted
with the rocker shaft's five mounting fasteners. Final agent result reports
six exact duplicated cover pose-pairs, correct oilpan8x7+drain13 and gearbox
6x7+1x10 ownership, and default nut meshes for all41 current presentations.
See `SATSUMA_ENGINE_FASTENER_OWNERSHIP_AUDIT_2026-09-05.md`; E2a changes only
the sixteen oilpan/gearbox presentation meshes. No fastener definitions or
ownership/counts have changed from these findings.

The detailed engine audit's E2–E6 list is a future breakdown, not permission to
skip the user's intervening cockpit pass or to lump those changes together.

## Guardrails

### Утренняя ручная проверка

Проверять текущий Unity-проект или новый player, собранный после ночных правок.
Билд024640 их не содержит; его native16 нельзя использовать для перезаписи
обновлённых native17-слотов. Ни один пункт ниже ещё не принят пользователем.

1. Перед: установленная тяга блокирует снятие стойки даже при полностью
   ослабленном крепеже тяги. Снятие тяги освобождает этот запрет. Затянутый диск
   блокирует снятие полуоси; собственная затяжка полуоси всё равно учитывается.
2. Зад: установленный stock-амортизатор блокирует установку рычага и пружины
   своего угла. Пружину по-прежнему снимаем после амортизатора. Сжатие, провисание
   и положение колёс не должны измениться; неизвестную судьбу оставшейся пружины
   при снятии рычага ночной пакет не переопределял.
3. Руль: без установленной колонки не ставится; с установленной, но незатянутой
   колонкой ставится. Центральная10мм гайка: T0->T1 ещё не держит, T2 держит;
   после затяжки T2->T1 продолжает держать, T0 освобождает.
4. Приборка/дворники: без dashboard или до первого достижения суммарной затяжки
   крепежа meters12 переключатель недоступен. После защёлкивания доступ сохраняется
   до ослабления в0. Без питания переключатель не должен оживлять дворники.
5. Замок: пустые руки, короткая ЛКМ переводит в ACC; удержание0.4с даёт START,
   отпускание возвращает в ACC. После попытки START следующий клик выключает.
   Без питания ключ вращается, но стартер не должен получать рабочий запрос.
   Полный запуск двигателя этим пакетом не обещан.
6. Двигатель: проверить доступ к коленвалу/крышкам/поршням по чеклисту E1.
   На поддоне теперь9 коротких болтов, включая13мм сливную пробку; на КПП6
   длинных7мм и один короткий10мм. E2a не исправляет слив масла, пороги затяжки
   или базовую позицию/ход крепежа — такие расхождения фиксировать отдельно.
7. Сохранить ACC и проверить загрузку текущей версией: ACC сохраняется, состояние
   удерживаемого START не возобновляется. Собранные детали, их затяжка и принятая
   физика не должны ломаться. Старые пользовательские файлы ночные тесты не меняли.

### Общие ограничения

Keep the licensed original and staging read-only. No full donor regeneration,
new dependencies, destructive operations, commits or pushes. Preserve unrelated
parallel work. Existing generated payload stays ignored by Git. Refer to
`SATSUMA_MANUAL_REMOVAL_RULES_FIX_2026-09-04.md` and
`SATSUMA_ENGINE_ACCESS_RULES_FIX_2026-09-05.md` for executed checks and hashes.
