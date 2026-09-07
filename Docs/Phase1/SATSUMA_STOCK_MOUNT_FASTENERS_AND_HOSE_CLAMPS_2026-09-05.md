# Stock mount fasteners + hose clamps — 2026-09-05

## Scope and status

Source-ready bounded Phase 1 authoring repair; Unity compilation, scoped prefab refresh and tests are owned by the integrating session. No Unity process was launched by this task, no generated asset/save/donor file was edited by it. Classification: ConfigurationTransferred / MountPointSource / BehavioralReference; visible meshes remain TemporaryDirectImport, not ProductionReady.

The missing bolts were not merely obscured by the installed parts. All six current mount definitions had an empty fastener array and an empty group (max0, on1, off0), so no authoritative interaction target existed. Sanitized loose-part presentations already contained their donor bolt renderers, but these inherited inactive state. This patch retains those originals disabled and adds explicit project-owned targets, definitions and presenters at measured part-relative poses.

## Frozen evidence

Read-only source: donor staging raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity.
SHA-256: c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4.
No donor scripts/FSM/runtime assemblies are copied into the independent runtime.

### Four hose1/hose3 clamp screws

Existing IDs, declared legacy sizes, max8 stages and groups are preserved. Tool matching changes from Wrench6/7 to the existing project screwdriver (Screwdriver, None/0); declared legacy size is provenance, not the tool requirement.

| Existing stable fastener suffix | Marker Transform | Screw FSM | Existing declared size |
| --- | ---: | ---: | ---: |
| radiator-hose1.boltpm-1 | 40414 | 105316 | 6 |
| radiator-hose1.boltpm-2 | 52612 | 108796 | 6 |
| radiator-hose3.boltpm-1 | 39723 | 105152 | 6 |
| radiator-hose3.boltpm-2 | 68964 | 113546 | 7 |

Each original marker scale .65 multiplies visible-child .8: physical presentation scale .52. Child mesh source GUID d42cbf361095bad4f9091a43a07fe91a (slotted screw), not the previous universal nut. Every state0..8 disables SetPosition and enables SetRotation (actionEnabled0001): authored axial travel must be zero, with 45 degrees per stage. Tool-selection proof and the shared screwdriver constant are already recorded in SATSUMA_ENGINE_SCREWDRIVER_BINDINGS_2026-09-05.md (Tool pickup FSM110228, screwdriver scale .65; size comparison FSM105041).

Implementation: Phase1SatsumaHoseClampAuthoring and SatsumaHoseClampAuthoringTests. Complete preflight before mutation rejects unknown pose, mesh, tool or missing fourth clamp. Reapply is no-op. Existing engine hose2, alternator and distributor screw bindings are not rewritten.

### Twenty-one stock mount bolts

IDs are project-owned: mount.satsuma.{slug}; fastener.satsuma.{slug}.boltpm-{N}; part definition vehicle.satsuma.part.{slug}. Numbering below is ascending source marker Transform ID, not the donor mesh filename. Existing part/mount IDs do not change.

| Slug | N range | Tool | Aggregate max | Bolted ON | Bolted OFF | BoltCheck FSM | Data GO | Removal FSM |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| exhaust-pipe | 1..3 | Wrench7 | 24 | 6 | 0 | 112050 | 33059 | 112051 |
| exhaust-muffler | 1 | Wrench7 | 8 | 2 | 0 | 106964 | 26370 | 106963 |
| fuel-tank | 1..7 | Wrench11 | 56 | 12 | 0 | 107694 | 12246 | 107695 |
| seat-driver | 1..4 | Wrench9 | 32 | 7 | 0 | 114222 | 4742 | 114223 |
| seat-passenger | 1..4 | Wrench9 | 32 | 7 | 0 | 108905 | 35953 | 108906 |
| seat-rear | 1..2 | Wrench9 | 16 | 6 | 0 | 114113 | 1827 | 114114 |

All21: max stage8, clockwise tightens, insertedOnInstall=true, requiredForRemoval=true. Group thresholds are read from active FSM variables, not stale embedded action literals. Notably rear-seat Bolts OFF FloatCompare has useVariable=1, BoltedYES=6, although its embedded float2 literal is24.

Bolts OFF sets Data.Bolted=false, then compares aggregate Tightness to BoltedYES; equality/greater enters ON. Bolts ON compares against BoltedNO=0. The five non-tank Removal Requirements states read their own Data.Bolted and permit REMOVE only when false. Tank Removal107695 reads db_RemoveReq1(30714).Data.Installed (FuelTankPipe) and its own Data.Bolted; BoolNoneTrue permits removal only if both are false. Authoring appends mount.satsuma.fuel-tank-pipe to the existing removal-blocked occupied mounts, preserving other requirements.

Seat Assembly FSMs106891/109994/111335 explicitly GetChild Bolts + ActivateGameObject during Assemble2. The project-owned fastener target uses installed+inserted availability for renderer and trigger; removal hides both, and old inactive copies never become duplicate visible geometry.

All21 use short-bolt source mesh GUID aec6c756751308a4d830708366ad5cdb. Per-target original marker scale is applied to the child mesh, with unit marker scale. Source Screw stage0..8 SetPosition+SetRotation actions are enabled (0101): displacement -0.0025*stage on marker-localZ and rotation45*stage. With unit runtime marker the travel factor is original markerScale.z. Two front pipe bolts have shortened Z .59 despite X/Y .7; do not replace it with toolSize/10.

| Stable suffix (slug.N) | Source part Transform | Marker Transform | mm | Part-local position (m) | Travel scale |
| --- | ---: | ---: | ---: | --- | ---: |
| exhaust-pipe.1 | 64076 | 39814 | 7 | (-0.075448490, 1.1761678, 0.25142130) | 0.590 |
| exhaust-pipe.2 | 64076 | 40656 | 7 | (-0.12525661, 1.1761644, 0.25176153) | 0.590 |
| exhaust-pipe.3 | 64076 | 41558 | 7 | (0.16671117, -0.82419634, 0.049632326) | 0.700 |
| exhaust-muffler.1 | 46044 | 37241 | 7 | (0.048160000, -0.12776000, 0.029000000) | 0.700 |
| fuel-tank.1 | 48576 | 42281 | 11 | (0.10182512, -0.32332683, -0.071612250) | 1.100 |
| fuel-tank.2 | 48576 | 42980 | 11 | (-0.082500815, -0.32332706, -0.071612366) | 1.100 |
| fuel-tank.3 | 48576 | 50849 | 11 | (0.28212620, -0.32332683, -0.071612250) | 1.100 |
| fuel-tank.4 | 48576 | 63263 | 11 | (-0.26146215, -0.32332706, -0.071612366) | 1.100 |
| fuel-tank.5 | 48576 | 63543 | 11 | (-0.082500815, 0.30778510, -0.071612015) | 1.100 |
| fuel-tank.6 | 48576 | 64798 | 11 | (0.10182524, 0.30778533, -0.071611896) | 1.100 |
| fuel-tank.7 | 48576 | 66640 | 11 | (0.28212547, 0.30778533, -0.071611780) | 1.100 |
| seat-driver.1 | 71493 | 38061 | 9 | (-0.22300000, -0.19000000, -0.094000000) | 0.900 |
| seat-driver.2 | 71493 | 40558 | 9 | (0.23470000, 0.14650000, -0.079200000) | 0.900 |
| seat-driver.3 | 71493 | 41290 | 9 | (0.23500000, -0.18990000, -0.093600000) | 0.900 |
| seat-driver.4 | 71493 | 70665 | 9 | (-0.22300000, 0.14700000, -0.079000000) | 0.900 |
| seat-passenger.1 | 53013 | 37166 | 9 | (0.23380000, 0.20100000, -0.078200000) | 0.900 |
| seat-passenger.2 | 53013 | 52283 | 9 | (0.23410000, -0.13540000, -0.092600000) | 0.900 |
| seat-passenger.3 | 53013 | 56578 | 9 | (-0.22390000, 0.20150000, -0.078000000) | 0.900 |
| seat-passenger.4 | 53013 | 56767 | 9 | (-0.22390000, -0.13550000, -0.093000000) | 0.900 |
| seat-rear.1 | 71066 | 42421 | 9 | (-0.57600000, 0.10220000, -0.065300000) | 0.900 |
| seat-rear.2 | 71066 | 46247 | 9 | (0.57600000, 0.10200000, -0.065000000) | 0.900 |

Quaternions and nonuniform physical scales are captured in the typed GetBindings() table in Phase1SatsumaStockMountFastenerAuthoring. Original Bolts roots: pipe61731, muffler37802, tank45132, driver68564, passenger48237, rear71095. All are identity relative to their part except passenger48237 +.01m localX; that offset is explicitly included in the table. Source renderer matching uses exact explicit part ownership + shared mesh + measured local-chain matrix, not runtime names or donor IDs. All21 sanitized prefab-local poses were independently checked against frozen source and agree within approximately2micrometres.

### Excluded12mm nut is not a tank mounting bolt

Marker45288 / Screw106716 / visible47938 has nut mesh e711c8a15b1135c4089caad19b8f56e8, scale(1.2,1.2,.8). However its PartAssembled points to GO22525, whose original name is fuel line(xxxxx), not tank GO12532. It belongs to the separate fuel-line connection and MUST NOT be inserted into the seven-bolt tank aggregate. No new fuel-line fastener/state is added by this packet; that separate connection remains an explicit unimplemented binding to be integrated under its own reviewed owner.

## Integration and compatibility contract

Hook after registering the authored controller/mounts/tools:

- Phase1SatsumaHoseClampAuthoring.ApplyToInstance(assembly, generatedRoot)
- Phase1SatsumaStockMountFastenerAuthoring.ApplyToInstance(assembly, generatedRoot)

Scoped executeMethods:

- MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaHoseClampAuthoring.RefreshHoseClampsBatch
- MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaStockMountFastenerAuthoring.RefreshStockMountFastenersBatch

No full rebuild is required. Batch guards Play, validates the frozen manifest, loads only canonical vehicle prefab, saves scoped definitions/prefab. Expected first stock-mount apply28 changes (six groups,21 targets, one tank removal rule); repeat0. It accepts exact old empty and exact new full shapes, not partial arrays or foreign targets. Editor Configure does not initialize a live assembly graph.

Graph count changes273→294, mounts remain117; all old fastener IDs/stages remain intact. Save-layer migration belongs to the integrating save session, NOT these Editor helpers. Only a known old save missing all21 exact new IDs may append them: occupied mount creates installed-default stage0 states, empty mount creates absent states; the six new latches start false; all other states and latch history remain unchanged. Partial/unknown/contradictory shapes must reject before source mutation. Existing source save must never be rewritten by authoring. Hose mesh/tool change does not need a schema or stage migration.

## Verification ownership and limits

Added typed fixtures:

- SatsumaHoseClampAuthoringTests (6 cases): exact4 bindings/tool, all9 stages travel0, three atomicity guards, no unrelated registry mutation.
- SatsumaStockMountFastenerAuthoringTests (10 cases): exact21 contract, transformed vehicle/world pose, all9 stages, installed/removed visibility plus fastening removal gate, preserved sequence plus filler blocker, four atomicity guards, idempotence/drift detection.

Static read-only YAML comparison and git diff --check were run. Unity compile/tests/manual gameplay are NOT claimed passed by this authoring task; the integrating session records executed results.

Existing PhysX/collision/accepted vehicle retention implementation is preserved. Donor joint-force expressions, crash-detachment thresholds and seat rigid-state transitions are not newly ported by this missing-fastener patch. No claim of complete original crash physics parity is made. No raw donor content enters Git; helpers/tests/report are project-owned, generated payloads remain ignored.

Next bounded item: separately audit the fuel-line12mm fitting under its actual owner, without changing the seven-bolt tank group.

