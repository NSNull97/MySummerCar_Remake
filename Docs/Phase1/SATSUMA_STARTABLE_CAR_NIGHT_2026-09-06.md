# Satsuma: startable-car overnight packet, 5–6 September 2026

Status: **Core packet generated and checked by broad EditMode, PlayMode,
graphics composition and native Bootstrap. Actual purchased-plug start/idle/
rev/stop and old-cube bulb replacement passed. Bounded fixed-fuel-fitting and
loose-door-bolt follow-ups passed their regression in the 1240-test EditMode
run. Reach-qualified wiring selection is implemented; the common front-light
connector now works through actual carry/query/F. All six reviewed wiring
circuits and three real mouse-driven dashboard controls passed in the final
108-test PlayMode packet. Manual acceptance and listed
parity gaps remain. Physical mount shake and purchased-battery extensions
are proposals awaiting the user's explicit compatibility approval.**
Deadline: **2026-09-06 09:00 Asia/Yekaterinburg (04:00 UTC)**.

Deadline checkpoint reached at09:00 on6September. No further runtime or
generated-content changes were started after the passing06:20 checkpoint.
Scheduled read-only checks found no new user approvals; all three compatibility
proposals remain unimplemented. Final XML results were re-read (not re-executed):
1247 EditMode,108 PlayMode and3 Bootstrap passes, zero failures/skips.
The source slot-01 SHA256 is still unchanged and no Unity process is running.
The overnight implementation window is closed pending the user's manual test
and explicit direction for the remaining extensions; this is not full parity
or manual-acceptance approval.

Русский отчёт по недособранному slot-01 и инструкция первого запуска:
[чек-лист проверки](SATSUMA_NIGHT_CHECKLIST_2026-09-06.md).

## Latest executed checkpoint — 06:20 local

| Gate | Actual result | Evidence |
| --- | --- | --- |
| Broad EditMode, including migration/fastener/control/wiring rules |1247 passed,0failed,0skipped | `Logs/codex-night-editmode-wiring-selection-final-20260906.xml` |
| Headless PlayMode, including real purchases/start/controls/6wire circuits |108 passed,0failed,0skipped | `Logs/codex-night-playmode-service-cabin-final-20260906.xml` |
| D3D11 graphics and native Bootstrap |3 passed,0failed,0skipped | `Logs/codex-night-bootstrap-final-combined-20260906.xml` |
| Fresh scoped authoring/repeat |0changes /0repeat changes; no full rebuild or save writes | `Logs/codex-night-refresh-final-idempotent-20260906.log` |

All root-owned Unity processes have exited normally. Original slot-01 SHA256
is unchanged. Manual first-start/audible/visual/driving acceptance is still
required; the automated engine flow uses the approved explicit fluid bypass.
Physical engine/body shake and purchased battery require approval of their
documented compatibility proposals. The original held-spool collision policy
is a further measured fidelity difference, not silently included in these passes.

## User-approved scope

1. Repair wiring connection availability and part mounting/fastening.
2. Install purchased engine consumables through an explicitly approved compatible
   item-to-assembly bridge: same stable IDs and wrappers, separate dynamic roster,
   no reconstruction of the existing assembly graph or loss of old save state.
3. Investigate purchased light bulbs (currently proxy cubes) and donor-evidenced
   installation; do not assume their purpose from an item label alone.
4. Basic fluids or an explicit developer-only start-test bypass while the reported
   refuelling bug remains. No silent full-tank state or changes to the user's save.
5. Engine starting/running, ignition/engine audio and bounded engine/car vibration.
   Carburettor, ignition and other tuning are explicitly deferred for these tests.
6. Preserve accepted suspension, body physics, UI, streaming and parallel work.
   Only root launches Unity, one process at a time; no full donor baseline rebuild.

The user explicitly approved the bridge on 5 September after being informed of
the compatibility boundary in SATSUMA_ENGINE_CONSUMABLE_BRIDGE_PROPOSAL_2026-09-05.md.
That proposal is historical: its statement that filter hand tightening is absent
is superseded by the existing OilFilter adjustment implementation.

## Read-only slot-01 audit

Snapshot: 2026-09-05 20:32:39.919183+05, 266021 bytes.
SHA-256: `2990953C2A6882395D658353C2829416772ECC7CF7F8B3D506AED80EC64E16AC`.
No user save was edited. 126 base parts: 101 installed, one chassis root,
24 loose. The three seats are loose; other remaining loose entries are optional
trim/accessories or alternate GT/long-spring/cover parts.

Electrical schema2 has twelve installed connections: Alternator, BatteryHarness,
CoilHarness, Dash1, Dash2, GroundBattery, FrontLightsHarness, Ignition, RadiatorFan,
RegulatorHarness, Starter, SwitchLights. Battery terminals are8/8, starter positive
cable0/8, voltage12.6V. FuelTank, four individual light circuits and Radio are absent.
Both dashboard availability and wiper power gates are satisfied; mode is Off.

The user did not intentionally connect the alternator. Its saved flag does NOT
prove an intentional action. Current Awake/Update/reset/restore do not invent
this wire; generated initial flags are zero. The alternator-side endpoint is
isolated (>0.34m from any other endpoint), whereas its regulator-side shares a
cluster with RegulatorHarness. The old one-press wiring bug is documented, but
there is insufficient history to attribute this particular flag. Do not clear it.

### Confirmed defects and boundaries

| Area | Evidence / intended repair |
| --- | --- |
| FL halfshaft | Container pivot was used for a different loose mesh; approximately1.13m longitudinal error plus rotation. Use mesh-equivalent installed frame, preserve bolt world points. No corrupt saved mount reference. |
| Exhaust, tank, seats | Six mount definitions have empty fastener arrays. Donor structural evidence: pipe3x7mm, muffler1x7mm, tank7x11mm, front seats4x9mm each, rear seat2x9mm. Audit thresholds/lifecycle before adding21 bolts. Tank also has a separate12mm nut requiring ownership review. |
| Hose1/3 | Four slotted screws became Wrench6/7 nuts. Correct screwdriver, mesh, total scale0.52 and zero axial travel; stages0..8 rotate only. Hose2 is already in the earlier screwdriver repair. |
| Door handoff | Interaction sphere is at hinge instead of donor trigger, approximately0.546m away. Separate interaction anchor from mounting pose; retain accepted hinge physics. |
| Loose door bolts | Follow-up original FSM inspection disproved the prior always-visible contract. Both original Bolts groups start inactive, installation enables them and removal hides them. Apply a door-only explicit external-renderer visibility binding to eight bolts; retain their panel parenting/hinge motion and all positions/stages/IDs. |
| Electrical bolts | Nonserialized base rotation is captured only by Editor Configure; runtime OnEnable/Update overwrite authored rotation from identity. Positions need an executed visual check separately. |
| Hood release | Bound to fixed dash_hood_lock instead of Handle/Mesh; trigger offset approximately0.191m. Correct explicit trigger and moving-only visual. |
| Choke/hazards/lights | Presentation exists; actual gameplay controllers/consumers were not implemented. Missing lamp wires are an independent issue. |
| Wipers | Correct switch target exists on the dashboard knob (not a steering stalk); gates pass in this snapshot. Flattened rest rotation is overwritten. Remaining reported click failure needs a real raycast test; occlusion is not yet proven. |
| Ignition key | Donor FSM107294 SetRotation uses localY; current runtime uses localX. Correct runtime and stale X-axis report/tests. |
| Tank/rear wiring | Definitions, targets and donor-matching coordinates exist; required parts are installed. Investigate actual query accessibility/pending endpoint state, not duplicate endpoints. |

There are16 genuine under-tightened existing mounting fasteners in10 groups:
brake-master-cylinder#2, clutch-master-cylinder#1, clutch-line#2,
gear-linkage#1–3, stock radiator#2/#7, hose1#1(7/8)/#2, hose3#1,
front-left disc central, halfshaftFL#1–3 and hose2 clamp. All others listed are0/8.
GT gear-stick duplicates and leaked rocker-shaft valve adjusters are excluded.
The left wing, instrument cluster, camshaft bolts and filter are now tightened.

## Evidence authority

Frozen GAME.unity SHA-256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Direct read-only transform/FSM/mesh comparisons; no original runtime execution
claimed for this audit. Temporary audio/visual payload remains ignored,
`TemporaryDirectImport`, behind project-owned controllers and stable bindings.

## Work ownership

- Root: integration, all Unity runs, purchased unit visuals, start/fluids gates.
- engine_mount_report: halfshaft/door/cabin frames, seven consumable sockets,
  plug threads and installed belt rig; additional donor startup audit and graph tests.
- engine_fastener_report: four clamps,21 missing bolts, wiring reach/occlusion,
  terminal poses and bounded night authoring; no full rebuild.
- toolbox_report: engine/ignition audio and bounded vibration via IAudioBackend,
  mouse dashboard controls and wired/installed-bulb light outputs.
- Separate task `01a0725b-1e99-7e73-8800-1e2b4acd7033`: save audit followed by
  the explicitly approved bridge, native v18 migrations, rollback/ownership and
  dashboard state extension, written in the shared authoritative tree. The initial
  independent audit checkout is not the implementation source.

## Compatibility and test gates

- Keep126 authored part IDs and all accepted mount/fastener states intact.
- Exact ID-set revision migrations, not count-only permissive fallbacks; old
  missing bolts must not silently become fully tightened.
- New consumables: immutable item mapping, same stable identity, dynamic registration
  without Configure; item/world/vehicle ownership and transactional rollback.
- Four plugs with a fifth refused by occupied sockets; replacements work; belt,
  purchased filter and donor-verified bulbs. No pre-spawned fake base replacements.
- Old save, current snapshot copy, new game, save/load and source/car streaming
  in both orders. No direct edits to current/bak/quarantined user saves.
- Input-to-ray-to-target tests with real generated geometry, not only controller calls.
- Engine key crank/catch/idle/rev/stop and audio transitions; missing circuit/component
  gates; explicit fluid-test bypass off restores ordinary prerequisites.
- Scoped authoring refresh and a second zero-change run. Preserve generated assets
  outside scope; backups in ignored Logs before modifying serialized content.
- Report only tests actually run. Compile/test failures are fixed before more
  generated refreshes. At09:00 report actual completed work and remaining risks;
  do not equate the deadline with completion or silently continue scope expansion.

## Execution log

- Initial audit completed; root reported all12 observations to the user.
- Compatible consumable bridge explicitly approved; bought bulbs added to scope.
- Implementation agents dispatched. No overnight integrated test executed yet.
- Source packets now published. Private csc compilation executed successfully for
  Simulation, Audio.Runtime, Assembly, Vehicle.Runtime, Items.Runtime,
  ItemsIntegration, Save.Runtime/Migration/Integration, Audio.UnityFallback,
  LegacyImport.Editor, Items.Editor and Bootstrap.Runtime. Focused LegacyImport,
  VehicleAssembly, VehicleSimulation and Items test sources also compile. This is
  **not** a Unity/NUnit execution. Existing five rear CS0414 and the pre-existing
  PhysicalServiceCatalogController CS0108 remain; no new compiler warning observed.
- Exact additive topology:126 fixed parts remain;117→124 mounts;273→298 assembly
  fastener targets (+21 stock bolts, +4 spark plug threads). Purchased wrappers
  remain item-owned identities in a separate dynamic roster. Door/suspension
  physics are not rebuilt by the packet.
- Donor startup split is now explicit: block Bolted and starter/electrical supply
  gate cranking; powertrain/cylinder/fuel failures prevent combustion but do not
  make the starter silent. Existing M06 prototype defaults retain their previous
  contract. Native DTOs never store this opt-in or the fluid-test toggle.
- Stock structure uses Installed crank/cam/chain/rocker/flywheel/Electrics;
  Bolted cylinder head/distributor; firing pairs(1 OR4) AND(2 OR3). No all-four
  plugs/MAX8 veto. Item-owned plug condition<1/broken cannot fire; random misfire,
  piston wear and tuning remain explicitly out of this first-start implementation.
- Fuel delivery uses installed tank/strainer/pump, bolted stock carb and installed
  chain. Unbolted hard line and absent air filter are not invented binary gates.
  The donor0.2 fuel threshold and detailed leaks/consumption are not a completed
  fluid-parity claim; F10 explicit fluid readiness bypass is available for tests,
  default off and never saved/refills native values.
- Engine feedback source packet: seven reviewed clips, key/starter/catch and
  on/off-load loops. Renderer-leaf vibration is bounded; body-force shake, advanced
  failure sounds, detailed tuning and production audio mixing are not claimed.
- Dashboard: choke mouse hold, hazards, Off/Parking/Headlights and actual light
  outputs require their circuits/installed parts/healthy bulbs. High-beam stalk,
  emissive lenses/flare and full electrical load/aging remain separate limitations.
- Generated imports, integrated EditMode/PlayMode, native snapshot smoke and manual
  ray/visual acceptance are still pending at this checkpoint. Source slot SHA is
  unchanged. No user save has been filled, auto-wired or rewritten.

### Executed Unity checks, 6 September (local time)

- Purchased-unit refresh completed at approximately00:20, Unity PID16852,
  exit0: `PURCHASED_UNIT_PRESENTATION_OK units=2 meshes=3 fullRebuild=false`.
  Log: `Logs/codex-night-purchased-units-final-20260906.log`. Only the two bought
  unit presentation bindings and their catalog dimensions/masses were refreshed;
  existing provider bindings were retained. This is asset generation, not a
  successful in-game installation or lighting test.
- Earlier purchased-unit attempts caught five invalid33-character source/test
  meta GUIDs and an Editor scene-switch asset-lifetime bug. GUIDs are now32hex;
  asset paths are captured before switching scenes and references reloaded after
  the switch. Previously ignored test files still require actual NUnit execution.
- Initial integrated night refresh, PID3320 at00:31, exited1 at the final strict
  topology check: `Duplicate/missing declared fastener definition`. The canonical
  prefab was not saved. Audio/belt/new definition imports may already exist;
  backup: `Logs/satsuma-startable-night-before-20260905-193159-4011991`.
  Diagnose the authoring defect before retrying; do not weaken the topology gate.
- Slot-01 source SHA remains the audit hash after the successful unit refresh.
- Root cause of the failed night refresh: stock authoring bound21 transient
  ScriptableObject fasteners into persistent mount definitions before creating
  their assets. Reimport serialized those references as `fileID:0`. The corrected
  authorer performs the complete read-only preflight, persists the definitions,
  reloads persistent references, and only then binds them. Strict validation is
  retained and now identifies the exact mount/fastener on failure.
- Root restored only the six affected MountDefinitions from the pre-run backup
  (exhaust-pipe, exhaust-muffler, fuel-tank, seat-driver/passenger/rear). All six
  restored files match their backup SHA byte-for-byte; the canonical prefab also
  remains byte-identical (`2181F8CC901998A65C4C28DC6319F8FB907803D7884EB888BD414DFA4FE522E2`).
  Created fastener/plug/belt/audio assets are retained for a bounded retry. No
  permissive unknown-shape recovery or full rebuild was introduced.
- All project meta GUIDs now pass the32hex syntax check. Added source tests cover
  persistent fastener save/reimport and actual generated bulb/plug wrappers plus
  fresh native-document round-trip. These new tests are not executed yet.
- Engine audio source now has eight reviewed clips and a third exhaust emitter.
  Original109210 installed stock chain controls outlet/gains; missing/downstream
  detached parts recalculate it. Full damage/tuning and physical chassis shake
  remain outside this completed source packet, not silently marked as parity.
- Corrected integrated refresh **passed**, Unity PID34564 at01:11–01:14, exit0:
  `SATSUMA_STARTABLE_CAR_NIGHT_OK changed=97 repeat=0 parts=126 mounts=124
  fasteners=298 consumableMounts=7 fullRebuild=false nativeSaveWrites=false`.
  Log: `Logs/codex-night-refresh-retry-20260906.log`; backup:
  `Logs/satsuma-startable-night-before-20260905-201211-3685696`.
  This supersedes the earlier generation blocker, not the pending gameplay tests.
- Integrated focused EditMode run started01:15, PID20932, including actual
  generated content, simulation/assembly, Items and SaveIntegration; XML/log base:
  `Logs/codex-night-editmode-20260906`. Explicit `-engineSavePath` enables the
  read-only native-start smoke instead of silently skipping it.
- First integrated EditMode result: **1119 total /1092 passed /27 failed /0
  skipped**,252s, Unity exit2. XML and log are retained above. Failures include
  inactive aggregate collider-owner lookup, double-applied skinned bounds scale,
  physics-pose rollback, outdated canonical/schema expectations, plug thread layer,
  cabin/light fixture and gate issues. No successful engine-start claim yet.
- Native smoke failed before simulation because Unity cannot attach a
  MonoBehaviour from an Editor assembly. The two new, never-authored synthetic
  smoke components were moved to `Vehicle/Runtime`, keeping their meta GUIDs,
  under `#if UNITY_EDITOR` and namespace `MSC.Vehicle.Diagnostics`. They are absent
  from player compilation and are never saved into the car prefab. This is a
  bounded diagnostic compatibility correction, not a production backend swap.
- Root fixed two previously unimported test assertions (array Count constraint
  and sub-microunit serialized scale tolerance), and replaced EditMode
  SendMessage lifecycle simulation with explicit managed callback invocation.
  Runtime electrical-terminal behavior was not changed to satisfy a test.
- Layer-only night refresh passed, PID31060 at01:40, exit0: changed4/repeat0,
  still126/124/298. Log `Logs/codex-night-refresh-layer-20260906.log`.
- **Standalone native start smoke passed**, PID17256 at01:43, exit0. Log:
  `Logs/codex-night-native-start-20260906.log`. Source273 fasteners restored to298;
  no required installed stock parts or starter wires missing. In-memory tightened
  copy +four synthetic healthy plugs +explicit fluid override reached Cranking
  and Running. Fuel-pump loss stalled it; empty-engine teardown could not run.
  AlternatorUnavailable remains expected because the actual source has no belt.
  Source DTO and native file hash unchanged. This is a simulation/integration
  check, not a manual drive or proof that the untouched slot is immediately ready.
- Newly confirmed prerequisite for actual bulb lighting: both headlight mounts
  lack their donor2x7mm bolts. Bolted light gating is correct and must remain.
  A separate exact+4headlight packet is being added (target126/124/302), retaining
  Stock21 and accepting the complete prior298 shape. Evidence:
  `SATSUMA_HEADLIGHT_MOUNT_FASTENER_AUDIT_2026-09-06.md`.
  The unrelated12mm fuel-line fitting is independently documented and remains
  unimplemented; it is not an eighth tank mounting bolt or a direct start gate.
- Headlight refresh passed at02:07, PID7860, exit0: changed6/repeat0,
  126 fixed parts /124 sockets /302 fasteners. Log:
  `Logs/codex-night-refresh-headlights-20260906.log`; backup:
  `Logs/satsuma-startable-night-before-20260905-210711-9217006`.
  Two headlight groups and four targets only; existing Stock21 unchanged.
- Read-only YAML audit of GlobalLegacy provider confirms `item.spark-plug`
  points to generated prefab GUID322ed219bdc6cb04cb6b56c83c62bf68 and
  `item.light-bulb` to GUID8c8db675af1eacf48872ae22206a86e2, both matching
  their generated unit prefab metas. Slot-01 SHA is still the original audit
  hash. Integrated EditMode rerun uses XML/log base
  `Logs/codex-night-editmode-retry-20260906`; runtime PlayMode remains pending.
- Second focused EditMode run, PID19776 at02:09–02:14: **1139 total /1135
  passed /4 failed /0 skipped**,295s. Items/SaveIntegration, headlight bolt
  authoring, dashboard/light gates, cabin/wipers and native-start tests passed.
  Remaining failures: generic validator lacks an explicit dynamic-definition
  input (seven newly known socket types), one residual117 front authoring guard,
  historic hose-mesh expectation, and a scale assertion stricter than the
  existing importer tolerance. Source repairs and isolated PlayMode bulb/audio
  lifecycle coverage are in progress; no full-pass claim yet.
- Travel assertion diagnosis: generated gearbox bolt1 has retained scale
  (0.69999987,0.70000094,0.700001), versus rounded reference(0.7,0.7,0.7).
  The authorer already validates at1e-5. Test now uses that same source-scale
  bound before animation and requires exact preservation of the actual scale
  at every stage/reversal. Translation and rotation tolerances stay unchanged.
- Coverage correction discovered by inspecting XML class names: the second run
  did **not** discover `CanonicalHeadlightFastenerSaveTests` (26 cases plus one
  partial-class native test) or `SatsumaReviewedGraphShapeTests`. Both new metas
  had33hex GUIDs, and Unity logged that the scripts were ignored. Existing
  Items/SaveIntegration tests passed; the new headlight save matrix remains
  compiler-only until rerun. No test count or clean compile substitutes for
  discovery. Owned invalid metas are being repaired and the next result must
  explicitly contain these class/method names. The issue also produced an
  IPostBuildCleanup log error; it is not an acceptable clean run.
- Corrective EditMode pass at02:34, PID27624: **76/76 passed,0 failures/skips**,
  29.8s, `Logs/codex-night-corrective-editmode-20260906.xml` and matching log.
  Explicit XML discovery confirms26 headlight migration cases,3 canonical native
  consumable tests (including two bulbs),16 reviewed-graph cases and4 dynamic
  definition validator cases. All four previous failures also passed. This
  supersedes the missing-discovery/remaining-correction blockers, not the pending
  full regression and PlayMode passes.
- First PlayMode pass, PID24676 at02:36: **101 total /95 passed /6 failed /0
  skipped**,109.6s, `Logs/codex-night-playmode-20260906.xml` and matching log.
  Actual bulb pickup/ray/handoff coroutine/lighting/condition/removal passed;
  three engine-feedback lifecycle tests passed. Most accepted physical scenarios
  also passed. Remaining fixture issues: old unclipped audio-gain expectation,
  two cabin restore fixtures fabricating an empty dashboard group's true latch,
  and old strut removal order with an installed steering rod. Runtime contracts
  are not weakened to satisfy these fixtures.
- The Bootstrap test failed because the current menu uses HDRP render requests
  on the deliberately Null graphics device (`-nographics`). Its persistent menu
  survived teardown and contaminated one following engine-contact test with the
  same rendering exception. No menu/UI production source is changed. Bootstrap
  will run separately with a graphics device; its test teardown now destroys the
  owned composition as well as the car. Remaining logic/physics tests can use
  the bounded headless runner without the Bootstrap rendering case.
- Headless PlayMode retry passed at02:50, PID35208: **100/100 passed,0
  failures/skips**,95.8s, `Logs/codex-night-playmode-retry-20260906.xml`.
  This includes the actual purchased bulb ray/carry/install coroutine, live
  light condition/removal and all three engine-feedback lifecycle cases.
- Graphics Bootstrap ultimately passed at03:02, PID36376: **1/1 passed,0
  failures/skips**,16.3s, `Logs/codex-night-bootstrap-graphics-final-20260906.xml`.
  Real D3D11/HDRP composition has exactly one car,126/124/302 topology, seven
  purchased sockets, bound assembly and engine audio, three engine/key/exhaust
  emitters and eight actual clips. The test also prepares the real world,
  activates gameplay, and confirms chassis dynamics/collisions/gravity released
  without installing a part or mutating the body from the fixture.
  Two preceding GPU fixture attempts exposed stale assertions: the accepted
  startup guard deliberately makes the car kinematic before support cells load;
  the hood-release target belongs to the initially loose dashboard, not chassis
  Transform ancestry. Assertions now follow those existing lifecycle/ownership
  contracts. Runtime physics and parallel menu/UI code were not changed.
- Independent read-only final reference audit confirmed52 wiring endpoints /26
  pairs, matching electrical owner, existing required part IDs, and serialized
  host/ignition/input/prerequisite/bridge bindings. Optional unimplemented audio
  accessories and extra gauges are not startup requirements. No new first-start
  blocker found by this inspection; physical access with the real held spool is
  a separate verification target, not proven by synthetic query tests.
- Final broad EditMode passed at03:08, PID2804: **1186/1186 passed,0
  failures/skips**,293.7s, `Logs/codex-night-editmode-final-20260906.xml`.
  XML explicitly contains all26 headlight migration tests,3 canonical purchased
  native-save tests,16 reviewed-graph tests and4 dynamic-definition validator
  tests; no malformed-meta silently missing coverage remains in this packet.
- Actual native Bootstrap load tests passed at03:15, PID13424: **2/2
  passed,0 failures/skips**,48.2s, `Logs/codex-night-native-bootstrap-20260906.xml`.
  Real RequestLoad reopens Bootstrap and restores a temporary test slot; current
  key-access false round-trips and native version16 migrates key access to true.
  Fixtures create/remove only their GUID-named test slots. User slot-01 SHA
  remains exactly the audited value after this run.
- Added two bounded real-object integration fixtures, privately compiled before
  Unity: purchased four-plug engine start/idle/rev/key-off on the authored host
  and NWH, plus physical held-spool/F-route access to six stock circuits and
  foreign-wall blocking. They do not force Running or rewrite user native state.
  First Unity execution uses `Logs/codex-night-real-plugs-wiring-20260906`;
  result pending, no manual driving/geometry acceptance claim.
- Actual purchased-plug ignition test passed in that run: four real item-owned
  plugs are carried into the authored sockets and tightened with the actual
  spark-plug tool. Normal host/NWH fixed frames, held ignition interaction,
  release to three simulated seconds of idle, throttle/release and key-off all
  pass without forcing Running or directly ticking the simulation. The explicit
  F10 fluid bypass remains required; fluid amounts stay zero. This closes the
  synthetic-plug limitation of the earlier smoke, not manual driving acceptance.
- The two wiring cases failed before reaching the connection query. Initial
  fixture used a kinematic chassis rejected by NWH; it now uses a dynamic,
  gravity-disabled FreezeAll test stand. Subsequent panel diagnostics proved the
  fixture restored the prefab while its parent was inactive, so the existing
  ancestor-based hinge owner resolution could not attach the hood. Activation
  now precedes restore, matching native Bootstrap lifecycle. No runtime hinge,
  latch, collision or wiring gate was relaxed. Intermediate diagnostic logs:
  `codex-night-real-wiring-retry-20260906`,
  `codex-night-real-wiring-panels-20260906`; corrected execution:
  `Logs/codex-night-real-wiring-lifecycle-20260906.xml`, PID29872 at03:49.
- That corrected lifecycle run passed the real foreign-wall blocking case and
  failed the six-circuit case at FuelTank endpoint0: the held spool stopped at
  .13285m despite a selected wiring target. The .1m donor distance gate remains
  unchanged. Contact/owner diagnostics are being added before deciding whether
  this is geometry, carrying or an invalid fixture approach; no through-solid
  carry exception or endpoint relocation is assumed.
- Read-only shop ingress audit confirmed actual service handoff backend and
  stable operation IDs: sparkplugs offer -> sparkplug-box ->4 spark-plug items;
  lightbulb offer -> lightbulb-box ->1 light-bulb item; belt/filter offers directly
  yield their compatible item IDs. All four current GlobalLegacy provider refs
  match the generated models. Save DTOs do not store a proxy mesh: fresh restore
  reapplies ID/condition before current-provider presentation binding. Existing
  tests cover the individual boundaries, not one joined checkout-to-mount flow.
- Added two explicit old-proxy regressions using a real absent-provider cube:
  serialize its ItemRuntimeSaveRecord and materialize into a fresh canonical
  bridge with real provider, plus late-provider replacement of the same wrapper.
  Both assert preserved ID/state, no duplicate presentation/part, and actual
  headlight-socket acceptance. Private compilation passed; Unity execution with
  wiring contact diagnostics uses
  `Logs/codex-night-wiring-contact-bulb-proxy-20260906.xml`, PID35648 at04:00.
- That four-case run completed **3 passed /1 failed /0 skipped**. Both real
  cube replacement cases passed, as did foreign-wall blocking. FuelTank reach
  still fails; diagnostics show the correct9x9x5.5cm spool collider, not an old
  25cm proxy. Contact is the body-shell `collider_floor3_92417`, with the endpoint
  inside its volume. Donor floor/endpoint geometry and the intended service-side
  approach must be compared before changing any runtime behavior.
- Frozen floor mesh and current collision are byte-identical. Connector45978
  lies about4mm above the lower face and52mm below the upper face: its service
  side is the underside. A fixture-only ground/support stand and underside
  approach now pass FuelTank, both rear lights and Alternator using real spool
  physics. `Logs/codex-night-wiring-service-pit-20260906.xml`, PID29756 at04:09,
  totals1 passed /1 failed: the remaining case stops at HeadlightLeft0 because
  its five original approaches omit the engine-bay/back-of-headlamp side.
  No runtime distance/occlusion/carry changes follow from these fixture errors.
- Bounded mechanical follow-up approved for implementation within the user's
  requested part connections: independent fixed12mm fuel-line fitting, outside
  the126/124/302 generic graph and outside the tank's seven-bolt group. Stage0..8
  and sticky ON8/OFF0 history will use optional validated vehicle save state;
  old saves default0/false. Tank removal hides the target but preserves history.
  This does not authorize a guessed leak rate, electrical sender start veto or
  broad fluid rewrite. Leakage consequences remain explicitly unimplemented;
  the user-approved fluid-test override remains the first-start path.
- A final donor recheck confirmed another actual user-reported defect: original
  loose-door Bolts groups31026/21881 are inactive; Removal FSM110760/112704
  enables them on installation and disables them recursively on removal. The
  previous builder comment and generated test incorrectly claimed permanent
  loose-door visibility. An opt-in external-renderer binding is being added for
  only the eight door fasteners, preserving part-parented hinge motion and all
  IDs/stages/poses. Unrelated body fastener contracts are not inferred from this
  two-door evidence. Runtime hinge physics is outside this correction.
- Fuel-fitting/door visibility refresh passed at04:42, PID1176, exit0:
  `SATSUMA_STARTABLE_CAR_NIGHT_OK changed=11 repeat=0 parts=126 mounts=124
  fasteners=302 consumableMounts=7 fullRebuild=false nativeSaveWrites=false`.
  Log: `Logs/codex-night-refresh-fuel-door-20260906.log`; backup:
  `Logs/satsuma-startable-night-before-20260905-234237-2886720`.
  Three fuel-fitting bindings and eight door bindings changed; the independent
  fitting does not become a303rd generic assembly fastener. Fresh read-only
  prefab inspection confirms eight unique disabled loose-door renderers, one
  fuel connection/target, initial stage0/false and explicit persistence binding.
  The compilation stack mentioning EmitExceptionAsError is an existing empty
  World.Streaming assembly warning, not a compilation exception or failed import.
  Broad EditMode rerun started04:50, PID16564, XML/log base
  `Logs/codex-night-editmode-fuel-door-final-20260906`; results pending.
- Held-spool integration coverage is deliberately bounded: six circuits are
  exercised independently on an in-memory disconnected copy, not all accumulated
  in one assembly. Camera/pickup are scripted without a full locomotion capsule;
  the raised service stand does not test a working jack or vehicle load response.
  Installed shell, tank, headlight and engine colliders remain active. Hood and
  boot use normal controllers; the spool is never teleported after pickup.
  Real carry physics, query, F and the unchanged0.1m connection gate remain the
  authority, including a foreign-solid negative case. No manual ease-of-access
  or whole-world driving acceptance follows from this fixture.
- Post-follow-up EditMode run completed **1226 passed /1 failed /0 skipped**,
  309.5s, `Logs/codex-night-editmode-fuel-door-final-20260906.xml`. All31 new
  fitting/optional-save/authoring cases and all10 door cases passed. The single
  existing generated-content assertion still expected a loose hidden door bolt
  to return a visible outline renderer. It now expects null only for the two
  doors; mesh, pose, tool, stage, parent and the other seven panel contracts are
  unchanged. Full test-assembly compilation passed; rerun is required before
  treating the complete suite as green. Source slot-01 hash remains unchanged.
- Physical engine/chassis vibration remains a genuine protected-foundation
  change: original block HingeJoint limits are±.25degrees and healthy alternating
  local impulses act on that dynamic block, not directly on the chassis.
  Current accepted installed block is kinematic. The minimum bounded proposal
  and required docking/mass/collision/restore tests are recorded in
  `SATSUMA_ENGINE_VIBRATION_AUDIO_AUDIT_2026-09-06.md`. Root requested approval
  for this separate physical-mount step without blocking remaining night tests;
  until a reply, accepted engine mount physics stays unchanged.
- A final defensive audit found that a vehicle binding without electricalSystem
  could silently discard a populated saved electrical DTO. Canonical Satsuma
  has the correct component, so this is not an explanation of the pre-existing
  Alternator flag or proof of user-save corruption. A bounded preflight guard
  plus five cases was added; those cases and the corrected canonical door test
  passed6/6,0skips in `Logs/codex-night-final-protective-check-20260906.xml`
  at05:05, PID34064. No schema change or file rewrite was made.
- Full headless Play rerun at05:07, PID34156, completed **104 passed /1 failed
  /0 skipped**,110.5s, `Logs/codex-night-playmode-complete-final-20260906.xml`.
  Real purchased-plug ignition, both old-cube replacement cases, actual bulb
  carry/handoff and all existing suspension/body/feedback cases pass. Only the
  six-circuit test remains blocked at HeadlightLeft endpoint0 (best.10993m).
  Both added engine-bay approaches used the same unrotated held spool. Exact
  convex planes and its+13mm local collider-center offset justify testing the
  normal mouse yaw-90degrees, placing its thin dimension toward the inner wing;
  this has not yet passed in PhysX. Runtime distances/collisions remain unchanged.
- The next broad EditMode run at05:11, PID33456, completed **1221 passed /11
  failed /0 skipped**,311.6s, `Logs/codex-night-editmode-complete-final-20260906.xml`.
  All failures expose an over-strict new electrical binding guard: JsonUtility
  materializes a valid empty inline DTO from the synthetic vehicles' null
  electrical field. Dashboard/fitting/deferred JSON fixtures legitimately have
  no electrical component. The compatible correction must admit only a validated
  fully empty DTO (no IDs, stages or legacy flags), while rejecting any actual
  saved electrical state without its binding. Original fixtures are retained;
  real null-roundtrip and omitted-field regression cases are being added.
- The corrected electrical guard now accepts only validated empty inline state
  without a runtime owner; all IDs/stages/five legacy flags remain protected.
  Thirteen focused cases include actual JsonUtility null roundtrip, field
  omission, all legacy flags and unsupported schema. Private compile passed;
  broad EditMode rerun started05:27, PID13152, XML/log base
  `Logs/codex-night-editmode-json-compat-20260906`.
- Actual held yaw-90 test at05:23, PID15572, reached the common headlight
  endpoint at.07873m, proving physical reach. The test still failed because a
  farther-from-spool endpoint41844 closer to the camera captured selection.
  `Logs/codex-night-wiring-rotation-20260906.xml`:1passed/1failed/0skips.
  Original FSM104480 and105732, like104461, show UI/selection only in Assemble
  after WiringTool distance passes tolerance.1. Current CanSelectForCarriedObject
  omitted this check although CanActivateHeldTool enforced it. A component-only
  selection/activation shared distance gate is being prepared; the global ray
  query, physical geometry, foreign-solid rejection and handshake stay intact.
- Read-only purchased-product audit found a separate boundary, not a first-start
  blocker: car-battery is not one of the four approved bridge mappings, and its
  per-item charge is not connected to the vehicle-global voltage. A mapping-only
  patch would give batteries each other's charge. The measured original battery
  transfers its own charge into/out of car Data and requires both shoes unbolted
  before removal. The compatible fifth-mapping/state/removal proposal is in
  `SATSUMA_PURCHASED_BATTERY_BOUNDARY_2026-09-06.md`; root requested approval.
  Until answered, no battery ownership/schema/physics change is made. Teimo
  extinguisher fitting and three deferred SUOMI-cover purchase effects remain
  explicit separate backlog, not falsely covered by the four-type bridge.
- Corrected complete EditMode run at05:27–05:32, PID13152, passed
  **1240/1240,0failures/0skips**,309.9s:
  `Logs/codex-night-editmode-json-compat-20260906.xml`. This includes all13
  electrical-binding guard cases and all prior dashboard/fuel deferred/native
  null-roundtrip cases that exposed the initial over-strict guard. No fixture
  was given an invented electrical component to make it pass. The only pending
  runtime change after this result is the donor-proven wiring selection gate.
- The separate physical-engine proposal is now concrete:
  `SATSUMA_ENGINE_PHYSICAL_MOUNT_COMPATIBILITY_PROPOSAL_2026-09-06.md`.
  A naive hinge would leave engine descendants' mass in the chassis; simply
  retaining compound proxies as well would double-count that mass. The proposed
  opt-in mode must coordinate aggregate mass/contact ownership, one momentum
  transfer and restore finalization. It requires no new save schema in the
  minimal form, but remains approval-pending and has not changed runtime physics.
- Reach-qualified selection was then exercised through the real held spool:
  `Logs/codex-night-wiring-selection-20260906.xml` (PID12908,05:37) passed
  the foreign-wall case and connected the common front-light endpoint through
  ordinary F at0.09334m. The six-circuit case proceeded to individual
  HeadlightLeft/1 and failed physical reach there. This is progress beyond the
  previous camera-ranking bug, not a passing six-circuit result.
- The follow-up fixture opens the hood fully before each front endpoint and
  the bootlid into its actual open-hold window, using normal release/held mouse
  actions with the spool dropped. No panel is forced or held while carrying.
  `Logs/codex-night-wiring-open-panels-20260906.xml` (PID22996,05:50) again
  passed1/2: the same individual left endpoint remains outside reach. New
  diagnostics show hood OpenNormalized1.0 at all closest contacts, so a falling
  or partly opened hood is **not** the explanation in this executed fixture.
  Best bounded approach was0.12193m; inner-fender endpoint-to-surface distance
  was0.001237m. Individual lamp solids are normally disabled when installed;
  their interaction proxies are triggers and do not justify a new occlusion
  bypass. Measured ordinary carry orientation/access is still being audited.
- Complete EditMode including the reach-qualified selection correction finished
  at05:58, PID23972: **1247/1247 passed,0failed/0skipped**,312.777s,
  `Logs/codex-night-editmode-wiring-selection-final-20260906.xml`.
  This includes the seven new ordinary/origin-overlap/distance-boundary cases
  as well as the complete previous1240-test packet. The source slot-01 hash
  remained identical and there were no invalid C# .meta GUIDs or leaked owned
  fuel-line/night/door test folders after the run.
- Final headless PlayMode at06:08–06:11, PID29200, passed
  **108/108,0failed/0skipped**,105.030s:
  `Logs/codex-night-playmode-service-cabin-final-20260906.xml`.
  All six circuits complete via actual held spool/query/F; the foreign-wall
  negative/recovery case also passes. Individual lamp points are reached from
  their measured exterior/lower side with ordinary yaw180: left0.08956m,
  right0.08963m. Common harness points use the measured yaw-90 engine-bay
  approach at0.09302/0.09335m. No runtime geometry, physical body, gate or
  connection flag was forced to pass. This remains a bounded service-stand
  test with scripted camera approaches, not a full player/jack walkthrough.
- The same108-test run includes three additional actual InputSystem mouse
  flows through cabin rays: choke left-hold/release/right-hold; two hazard
  clicks without hold-repeat; light switch Off/Parking/Headlights/Off with
  preserved authored knob frame. Their unpowered mechanical controls do not
  invent lighting or electrical supply. Existing wiper/hood/door input and
  engine/bulb/suspension/save/feedback tests remain in the passing packet.
- Final graphics/native combined Bootstrap at06:12, PID16216, passed
  **3/3,0failed/0skipped**,65.307s:
  `Logs/codex-night-bootstrap-final-combined-20260906.xml`. This repeats the
  two real native RequestLoad/migration cases after the electrical preflight
  and optional fuel-fitting extension. The graphics composition case now also
  verifies eight explicitly bound hidden loose-door bolts and the independent
  fuel-fitting owner/target/state, in addition to126/124/302, seven consumable
  sockets, eight clips and actual world activation/startup-guard release.
- Final fresh-process scoped refresh at06:15, PID34760, exited0:
  `Logs/codex-night-refresh-final-idempotent-20260906.log` records
  **changed=0,repeat=0,parts=126,mounts=124,fasteners=302,consumableMounts=7**,
  **fullRebuild=false,nativeSaveWrites=false**. No generated content changed
  after the final passing tests. The process completed normally; no second
  Unity editor was run concurrently.
- Further read-only original evidence identifies a remaining fidelity
  difference, not a failure of the passing six-circuit route. Original active
  Hand/PickUp113435 changes a held PART spool from layer19 to16; the locked
  collision matrix excludes16x22 but includes19x22 and16xDefault/Terrain.
  Its128bytes match original mainData at34912. Current held-spool collision
  still contacts the21 layer22-derived chassis hulls, requiring the measured
  exterior/rotation approaches used above. See
  `SATSUMA_WIRING_HELD_COLLISION_REFERENCE_2026-09-06.md` for exact evidence.
  An authoring-only scope assignment would miss live collider-cache updates,
  prior ignored-pair state and release/save inside a hull. No global collision
  matrix, generic carry foundation or other held-item behavior was changed.
- The minimal wiring-only collision compatibility proposal is now documented
  in `SATSUMA_WIRING_CARRY_COLLISION_COMPATIBILITY_PROPOSAL_2026-09-06.md`.
  Root read the complete evidence and proposal. Existing item events and an
  explicit21-collider scope avoid new identities/mass/save schemas, but live
  carry-cache and prior-pair restoration need a bounded foundation extension.
  Release/save from inside a hull remains a physical safety test, not an excuse
  to leave permanent collision exceptions. This proposal is **not implemented
  or approved** and does not change the passing core. Subsequent scheduled work
  must not infer approval for it or for the physical-engine/battery proposals;
  retain this checkpoint while awaiting the user's response/manual acceptance.
