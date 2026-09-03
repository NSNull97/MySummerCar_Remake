# Porting Matrix — Phase 1 status synchronized 2026-09-03

## Current status summary

The row-level authority is
`Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`; the full audit is
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md`.

| Transfer area | Current status |
|---|---|
| World layout / 49-cell streaming | `Verified` |
| 08A project-owned UI | `Verified` |
| Native save core | `ImplementedUnverified` at document v16; full-game coverage Partial |
| Player / Interaction | Partial; forward lean implemented, sprint/deep crouch/full seated parity open |
| Items / Needs / Home | Partial foundations; no complete donor domain verification |
| NPC | 21 required rows Partial, 51 Evidence-only |
| Satsuma | Partial; current generated baseline is `11A-V1d.60` with 125 loose parts, 117 mounts, 280 fasteners, four operable body hinges and 39/39 generated-content tests |
| Other vehicles | 11 Partial, 29 Evidence-only |
| Traffic | 10 Partial |
| Economy / Services / Communications | 11 Partial rows in total; remaining rows Evidence-only |
| Jobs / Story / Authority / Rally / Media | Evidence captured; complete runtime domains absent |
| Legacy presentation | Private removable `TemporaryDirectImport`, not ProductionReady |

No row is promoted to `Verified` by the existence of a class, prefab, scene,
generated asset or automated test alone.

Current-tree note: the complete generated Satsuma gate has been rerun at V1d.60
and passes `39/39`. The physical hinged-panel PlayMode gate passes `4/4`, the
current 280-fastener migration passes `5/5`, front alignment regression passes
`24/24`, and the focused native-save assembly round trip passes `1/1`. Full
Bootstrap/Player coverage was not rerun because that ownership is being changed
in parallel and remains outside this body pass.

Scoped V1d.58 hinged panels: frozen `Use`/Assembly evidence identifies four
runtime-created donor hinges—both doors, bootlid and hood—with exact pivots,
axes, limits, asymmetric held-mouse torque, break values and four fasteners
each. The remake now uses real dynamic `HingeJoint` physics, retains inertia,
ignores collision only against the connected chassis, remains blocked by world
objects, and latches at the physical closed endpoint with a donor-strength
`FixedJoint`. `F` is absent. A fully unfastened panel detaches only at full
opening; the hood additionally requires the cabin release. The one-degree
latch threshold is an approved project correction to the donor FSM's visible
roughly-ten-degree final snap. Audited old-Unity break values remain authoring
evidence, while the actual Unity 6 joint is unbreakable so ordinary chassis
constraint impulses cannot destroy the component and disable interaction.
Bootlid `Use` component `105480` additionally narrows the open endpoint from
`-70..0` to `-70..-69` degrees; V1d.58 reproduces that hold and restores full
travel on close input.
Manual four-panel acceptance is pending.

Scoped V1d.58 bootlid presentation: the four donor `BoltPM` parents have local
Z scale `0.5`, producing `1.25 mm` effective screw travel per stage and `10 mm`
total. The generated fastener targets retain that multiplier instead of driving
the bolts `20 mm` through the exterior skin. Donor-active child
`bootlid_emblem` / mesh `datsun_bootlid_001` is the approximately `0.56 m`
exterior handle/garnish assembly and is preserved rather than filtered as an
optional emblem. Initial active state places `bootlid_hooks` on chassis
`pivot_bootlid`, but donor Assembly/Removal FSM actions swap that copy with the
lid-owned copy. Exactly one pair is visible, and the installed pair rotates with
the lid.

Scoped V1d.55 registration-plate audit: both donor plate items are initially
inactive inspection-station rewards. Front-bumper and bootlid install triggers
consume a carried plate and activate their embedded presentation without a bolt
group. The remake correctly hides both initial plate renderers. Loose reward
creation and installation remain pending inspection-service scope; they are not
invented in the Satsuma starting roster or current vehicle save graph.

Scoped 2026-09-02 streamed persistent-physics correction: project-owned
`Reimplemented` lifecycle now guards scene-owned and spatially persistent
pickup/vehicle bodies before base-cell collision teardown, and releases them
only after base collision rematerialization plus a three-stage PhysX sync
barrier. Native load performs initial static-world refresh before restore and a
second refresh for restored focus/deferred cells. Below-world repair applies
only to the vehicle aggregate, saved `Loose` parts and item definitions marked
`CriticalRecovery`; stable IDs and installed assembly state are preserved.
V1d.43/V1d.44 and NWH authority are unchanged. See
`Docs/Phase1/ITEM_STREAMING_PHYSICS_AND_SATSUMA_SAVE_AUDIT_2026-09-02.md`.

Scoped V1d.46 rear spring removal parity: BehavioralReference from donor
Removal FSMs `113824`, `112850`, `107537` and `107450` proves that stock and
long/rally rear springs expose removal only while both same-corner shock
variants report `Installed=false`. The remake now expresses this as an
additive corner-specific mount predicate; a loosened but still installed shock
blocks both removal and the spring prompt/outline, while the opposite corner
does not. Baseline build passed; P0 BoltCheck `5/5`, generated Satsuma `29/29`,
generic assembly `23/23`, rear droop `4/4` and Bootstrap `1/1` pass. Manual
in-game acceptance is pending. No suspension tuning, pose, stable ID or save
DTO changed.

Scoped V1d.45 installed-part removal UI: ConfigurationTransferred donor
`BoltedOnThreshold`/`BoltedOffThreshold` and the existing hysteretic
`FastenerGroupState.IsBolted` remain authoritative. The project-owned removal
target now exposes `СНЯТЬ` only when the authoritative removal evaluation
succeeds; every refusal hides its prompt, RMB binding and prompt-driven outline.
The direct 2026-09-02 UI follow-up also clears name/localization metadata while
a part is installed and removes name metadata from fastener targets. Fastener
targeting, nested mount handoff and independently valid hinge/tool actions stay
available. Current generic vehicle assembly passes `27/27`, player interaction
passes `49/49`, BoltCheck parity passes `5/5`, and the installed Satsuma hinge
PlayMode contract passes `1/1`; the complete generated Satsuma suite was not
rerun after this UI-only correction. Manual in-game acceptance is pending. No
physics, mount, stable-ID, prefab or save contract changed.

Scoped V1d.44 Satsuma save restore: BehavioralReference from the donor's
kinematic setup/release ordering is Reimplemented through the project-owned
`IVehiclePhysicsRestoreSynchronizer` boundary. Persistence now restores graph
and simulation under a kinematic guard, force-refreshes NWH support,
front/rear authority, installed poses and compound mass, synchronizes PhysX,
then releases the chassis. The donor's `useGravity=true` contract is retained;
its temporary FixedJoint and asynchronous 0.1/8-second FSM waits are not.
Builder V1d.44, immediate/fresh restore 1/1 each, full physics 20/20,
generated 29/29, save integration 13/13, simulation 22/22 and Bootstrap 1/1
pass. Native-save manual acceptance is `USER PASS` as of 2026-09-02: no launch,
separation or part-mutation recovery was observed.

Scoped V1d.42 rear fastener presentation: PivotSource/BehavioralReference and
TemporaryDirectImport bind 12 frozen rear marker Transform IDs to their actual
MeshFilter assets: 4 long bolts, 6 short bolts and 2 nuts. Rear springs retain
zero fasteners and wheel lugs retain four nuts per corner. This corrects only
the V1d.41 default-nut presentation; wrench sizes, stages, latches, owners,
mounts, NWH and saves are unchanged. Build, focused/expanded EditMode 46/46,
physics 18/18 and Bootstrap 1/1 pass. Installed rear bolt/nut presentation is
`USER PASS` as of 2026-09-02.

Scoped V1d.41 road-wheel seating: BehavioralReference/PivotSource transferred
from the donor's `wheel_regula`/`wheel_offset` branch and paired Pivot1/Pivot2
transforms. All current stock and GT wheel parts are regular, so their standard
seat is `-0.043 m` front and `-0.040 m` rear in mirrored mount-local X. Build,
generated 29/29, installed physics 18/18, rear regression 9/9 and Bootstrap 1/1
pass. Current regular wheel seating is `USER PASS` as of 2026-09-02. Future
offset-family import must add the per-part selector. NWH, fasteners, suspension
tuning and saves are unchanged.

Scoped V1d.39 rear issue 4A: ConfigurationTransferred exact rear Wheel stages,
shock damping, drum contact and zero anti-roll; Reimplemented force ownership
through NWH chassis contact plus kinematic donor-IK arm presentation. Focused
formula, generated prefab, lifecycle, known-load/settling, droop, front
regression and Bootstrap gates pass. The broad installed-part result is now
18/18 after the separate V1d.41 road-wheel test migration. The accepted
V1d.38 envelope/front systems are retained. The user accepted bounded live 4A
loading/compression on 2026-09-01. The scoped correction report owns evidence;
wheel seating subsequently received its separate V1d.41 `USER PASS` on
2026-09-02.

Scoped V1d.38 rear issue 4: donor Wheel carrier/travel profiles and arm IK
direction now bound the existing physical rear hinge. ConfigurationTransferred
and Reimplemented; no donor FSM runtime, geometry stretch or force-owner change.
Baseline generation and 38/38 physics regressions pass. The user accepted the
reported issue 4 on 2026-08-31; deferred EditMode/Bootstrap checks remain
outstanding. Bounded acceptance and remaining validation are tracked in
`Docs/Phase1/SATSUMA_REAR_DROOP_AND_SHOCK_FIX_2026-08-31.md`.
Rear wheel fitting and general saves remain outside this correction.

Scoped 11A follow-up: V1d.36 retains V1d.35's front mesh/50 mm endpoint fixes
and reimplements the rod8/0 connection latch, separate14 mm toe adjustment,
free-yaw hinge and front installation gates. ConfigurationTransferred,
Reimplemented and TemporaryDirectImport; no donor runtime or FSM dependency.
Build passed; current tests and explicit compatibility limits are recorded in
`Docs/Phase1/SATSUMA_FRONT_ALIGNMENT_PARITY_2026-08-31.md`.
Overall save repairs are user-deferred until the whole suspension is accepted.

Runtime-only follow-up fixes free-hinge anchor initialization at rotated spawn,
not donor geometry or the connection specification. Regression evidence:
`Docs/Phase1/SATSUMA_FRONT_FREE_YAW_FRAME_FIX_2026-08-31.md`.

Status: controlled donor-reference/world-baseline work is recorded through the
frozen 06B sequence. Milestone 07A is fixed in commit `61250e2`; Milestone 07B
adds clean-room, project-owned GameTime/weather/wetness/lightning domains and a
read-only Enviro presentation adapter. Milestone 07C night/dawn follow-up tests
pass, and the user accepted the corrected dawn/night presentation on 2026-07-18
without capture artifacts. Performance and other scoped manual gates remain
pending, and current generated-material contract validation is not clean. No
donor weather code, state machine, configuration value or visual asset is
classified as ported or production-ready.

Milestone 08 audio remediation is `Completed / AutomatedValidated /
UserAcceptedBoundedBaseline`: official
Wwise authoring/runtime/build gates pass, footsteps and the separate vehicle
playtest are wired, and donor prototype clips remain local ignored
`TemporaryDirectImport`. The user confirms Wwise / 6 banks / 0 missing. Audible
fine-grained zone/mix calibration and real-device Wwise Profiler CPU are
deferred to polishing; full EditMode/PlayMode retain unrelated Garage/World/
VehiclePhysics baseline failures and are not claimed as passes.

Milestones 09A–09C add project-owned save, needs/items and home foundations.
Milestone 10A adds the bounded NPC foundation described below. Earlier accepted
systems remain integration dependencies; these later milestones do not
reclassify donor presentation as production-ready.

Milestone 10B-R1 promotes only the four `ServiceOrRelationship` roster rows to
`PartiallyImplemented`. Teimo, Fleetari, Farmer and Berryman have exact
evidence-backed anchors/schedules, project-owned dialogue/job/service hooks,
schema-2 save state and hash-locked textured temporary presentation. A bounded
17-clip greeting/pub/babble library is now manifest-backed behind stable
`IAudioBackend` event IDs; conditional dialogue and owning service/job flows
remain incomplete, so none of these rows is `Verified`. The private voice
  builder generated 17/17 clips and event definitions; focused validation passes
  NPC EditMode `14/14`, NPC PlayMode `2/2` and Unity fallback PlayMode `7/7`.
Audible in-world comparison remains pending.

The post-review character presentation revision is schema 5: it adds four
explicitly selected headwear meshes, the exact Teimo/Fleetari and Farmer glasses
meshes with their glass/metal or dark-lens material closure, plus reviewed root
calibration while keeping all
stable identities and simulation state unchanged. Fleetari's visual-root
compensation restores the serialized relation to the existing world-baseline
chair without making furniture part of NPC simulation. A separate narrow Phase-1
world supplement restores the excluded StrawberryField/tent and five septic-job
static sites plus the Farm buildings/yard/well in their existing cells. Both remain private
`TemporaryDirectImport`; neither is Phase-2 production art, and complete-map
coverage is still unaudited.

The user accepted the bounded character/accessory placement and Fleetari's
  chair-relative presentation on 2026-08-01 after schema 4. A later report exposed
the Teimo schedule endpoint defect: the scene was serialized at pub-local X
`-6.5`, while shop is local X `0`. Exact hash-locked move-clip endpoints now
drive separate project anchors and pass NPC EditMode `11/11` plus PlayMode
`1/1`; the user accepted the corrected daytime/evening placement on 2026-08-01.
Neither R1 row is promoted beyond `PartiallyImplemented`; dialogue/audio,
bicycle collision response and service/job authority remain open. The bicycle
road traversal, procedural pedalling presentation and timed shop-arrival/service-
door flow are automated-valid but still require manual in-world acceptance.
Here, dialogue/audio means the
  remaining conditional sets and autonomous timing, not the implemented bounded
  17-line voice slice.

The later Farmer review exposed two further R1 defects: missing GIFU cap/dark
glasses and a synthetic closing route segment from the mailbox. Schema 5 restores
the exact accessory renderers. The project-owned route is now a distance-weighted
ping-pong traversal through the same six anchors in reverse. The restored Farm
and Farmer accessories passed user review. A later capture exposed sparse-anchor
height interpolation above the road; walking presentation now conforms to the
nearest loaded world surface without changing simulation or save state. That
last grounding correction is automated-valid but remains manual-comparison
pending.

Evidence basis: donor file inventory, serialized-file headers/strings, managed assembly names, reflection-only type/member metadata, AssetRipper 1.3.14 object export, staged hashes, and Unity validation.

## Milestone 10A NPC foundation addendum

Milestone 10A adds a project-owned `MSC.Characters.Runtime` and
`MSC.NPC.Runtime` foundation without changing the transfer recommendation for
the complete NPC roster. Three bounded fixtures exercise stationary service,
scheduled roaming and vehicle-linked roles. Their definitions, stable instance
IDs, schedules, dialogue hooks, save DTOs and streaming behavior are
`Reimplemented`; their short schedules/routes are framework fixtures and are
not donor configuration transfers.

The selected Teimo, Alpo and Latanen mesh/clip slices are manifest-locked
`TemporaryDirectImport` presentation only. They are generated into the ignored
private `RuntimeBaseline/Characters` boundary and remain removable without
changing `npc.state`. No donor script, PlayMaker FSM, AnimatorController,
runtime assembly, object-name lookup, input, dialogue, schedule or save logic is
used by the new runtime. Full roster parity remains owned by 10B, and no 10A
NPC row is classified `Verified` or `ProductionReady`.

The bounded foundation is `Completed / AutomatedValidated /
UserAcceptedBoundedFoundation` as of 2026-08-01. Manual evidence covers the
three fixture animations, Alpo route motion and streaming transitions; complete
roster behavior remains 10B.

## Coupling scale

- **Low**: plain managed value/calculation type with no direct Unity/PlayMaker base type observed.
- **Medium**: useful isolated data or algorithm exists but depends on nearby runtime state.
- **High**: direct Unity, scene, input, physics, audio, or save-format dependency.
- **Very high**: behavior is primarily serialized PlayMaker/scene state or spans multiple legacy subsystems.

## System matrix

| System | Observed donor sources | Coupling | Direct dependencies observed or expected | Recommended transfer strategy | Technical risk | Proposed implementation order | Unknowns |
|---|---|---|---|---|---|---|---|
| Player | `mainData`/`level*`; `SimpleSmoothMouseLook`, `SmoothMouseLook`; PlayMaker `MouseLook*`, `SetCharacterMotorProperties`, `WherePlayerIsLooking`; `cInput.dll` | High | Legacy input, Rigidbody/character motor, camera, FSMs, scene objects | Behavioral capture and clean reimplementation with Input System; transfer only measured speeds, eye height, crouch, and interaction reach after validation | High | M4.1 | Actual controller owner, movement constants, grounding rules, body/animation coupling |
| Interaction/carrying | Serialized GAME-scene/FSM data; PlayMaker `Raycast`, `GetRaycastHitInfo`, `SetDrag` actions | Very high | Player camera, physics, item rigidbodies, object-name/FSM events, save state | Specify intent/capabilities and reimplement; measure distances/forces; never port the scene lookup/FSM graph wholesale | High | M4.2 | Exact pickup target rules, carry pose/rotation, throw force, placement collision behavior |
| Vehicle assembly | No dedicated managed assembly-controller type identified; likely serialized scene hierarchies and PlayMaker FSMs in `level*` | Very high | Part transforms, colliders, FSM events, object names, save keys, tools/fasteners | Extract hierarchy/transforms only after controlled inventory; build `PartDefinition`/instances, mounts, and assembly graph anew | Very high | M5.1 | Part identifiers, compatible mounts, install order, pivots, mount transforms, detach rules |
| Fasteners/tools | No reliable game-specific type-name hit; behavior likely serialized FSM/action state | Very high | Assembly parts, tool selection, raycasts, animations, audio, saves | Behavioral specification and reimplementation; transfer verified bolt positions, sizes, turns, and tool compatibility as configuration | Very high | M5.2 | Fastener count/coordinates, tightness units, stripping/wear, tool edge cases |
| Engine | `Drivetrain` fields/method names including `CalcEngineTorque*`, `CalcEnginePower`, RPM/torque arrays; Unity-coupled `MonoBehaviour` | High | Clutch, gearbox, fuel, powered wheels, physics tick, audio | Review isolated formulas/config tables with `ilspycmd`; port only pure calculations with comparison fixtures; reimplement orchestration | High | M6.2 | Torque curve values, units, stall/start rules, thermal and damage ownership, frame-rate assumptions |
| Clutch | Plain `Clutch` with `GetDragImpulse`, position and torque fields; `Drivetrain.CalcClutchTorque` | Medium | Engine/wheel angular velocity, timestep, gearbox, driver input | Strong selective-code-review candidate; transfer constants only with units and tolerance tests; wrap in a new simulation type | Medium | M6.1 | Method body, impulse units, timestep dependence, slip/heat/wear behavior |
| Gearbox | `Drivetrain`, `Transmissions` enum, gear ratios, shift timers and shift methods | High | Engine, clutch, differential, player input, auto-clutch logic | Reimplement explicit gearbox state; transfer verified ratios, thresholds, and delays; use behavior fixtures | Medium-high | M6.3 | Reverse/neutral indexing, missed shifts, damage, auto-clutch behavior, shift timing |
| Differential | `Drivetrain` fields `differentialLockCoefficient`, `differentialSpeed`, `lockingTorqueImpulse`; no separate type observed | High | Gearbox output, powered wheels, wheel angular velocity, physics timestep | Reimplement as a separate powertrain node; use legacy fields/formulas as behavioral/configuration reference only | High | M6.4 | Differential type, torque split, lock behavior, sign conventions, per-vehicle variants |
| Wheels/tires | `Wheel`, `TireParameters`, `CarDynamics`, `Axle`; force/slip/tire method names and many tire coefficient fields | High | PhysX ray/contact state, suspension, drivetrain, surfaces, timestep, skid/audio | Phase 1 simple backend; later isolate and compare useful tire equations/config; keep `IWheelPhysicsBackend` boundary | Very high | M6.5 | Coefficient provenance, units, relaxation model, substeps, surface tables, donor PhysX quirks |
| Suspension/brakes | `Axle`/`AxleInfo` fields; `Wheel.SuspensionForce`, travel/rate/damping fields; `BrakeLights` | High | Wheels, chassis mass/COM, road contact, inputs, handbrake, presentation | Transfer verified dimensions/rates; reimplement suspension/brake simulation and presentation separation | High | M6.6 | Travel datum, bump/rebound units, anti-roll formulation, brake balance/temperature/wear |
| Fluids/electrical | `FuelTank`; drivetrain fuel fields; remaining behavior not identified outside serialized FSM data | Very high | Engine, assembly completeness, time, save keys, gauges, failure states | Reimplement subsystem models; transfer tank capacities/densities and wiring/config only when directly observed and unit-checked | High | M5.3 then M6.7 | Oil/coolant ownership, leaks, wiring topology, battery/starter/alternator logic, fluid save schema |
| Damage/wear | `CarDamage`, `CarDamage2`, `Repair`; mesh-deformation fields/method names; likely FSM/save state | High | Collisions, meshes, assembly state, vehicle simulation, save, audio/VFX | Preserve behavioral thresholds/dimensions where measured; reimplement stateful damage/wear; reauthor mesh deformation presentation | High | M6.8 | Part-level wear, permanent deformation persistence, repair rules, failure thresholds |
| Save/load | `ES2.dll`, `MoodkieSecurity.dll`, `UniqueSaveManager`; PlayMaker save/player-pref actions; LocalLow files such as `defaultES2File.txt` and `items.txt` | Very high | Scene object names, FSM variables, third-party serializer, mod data, platform storage | Document format first; build native versioned DTOs/stable IDs; optional donor importer only in M9 and only if safely understood | Very high | M1 identity foundation; M5/M6 records; M9 importer study | File schema, encoding/security layer, atomicity, key ownership, scene-path dependence, corrupted-save behavior |
| Time/needs | No reliable dedicated non-PlayMaker type identified; likely serialized FSM variables/actions in GAME scene | Very high | Save, UI, player state, world events, weather, audio | GameTime is now a project-owned clean-room service; needs remain deferred; any future donor rates still require measured evidence | High | GameTime M07B complete; needs later | Donor time scale/sleep skip and all hunger/thirst/stress/fatigue/urine formulas remain unknown; 07B time values are `RemakeDesignTarget` |
| NPCs/traffic | SWS spline/bezier movement types; serialized scene paths/FSMs; no authoritative NPC-domain class inventory | Very high | World splines, schedules/time, physics, audio, save state, scene loading | Defer population; preserve routes/timing as world-layout/behavioral reference; reimplement a small traffic slice only when needed | Very high | Post-vertical-slice except minimal traffic proof | Agents, spawn/despawn, schedules, route graphs, AI state, persistence, collision rules |
| World/terrain/roads | `mainData`, `level0`–`level3`, shared assets; scene paths; SWS spline/bezier types; largest level is 104 MB but mapping is unproven | Very high | Terrain/meshes, colliders, splines, vegetation, streaming, persistent entities, weather | Controlled extraction of layout/measurements; rebuild terrain/roads/assets; use streaming cells/additive scenes and project-owned IDs | Very high | M2 reference proof; M3 garage slice; M7 expansion | Scene mapping, coordinate origin/scale, terrain format, road centerlines, cell boundaries, key-location transforms |
| Weather | `CameraFog`, `RainNearClip`, `SetRainClip`, `windshield.RainType`, PlayMaker weather actions, serialized assets | High | Time, camera, particles, materials, audio, indoor/outdoor state, save | Project-owned seeded fronts, outputs, wetness and lightning are clean-room `Reimplemented`; Enviro 3 is presentation only; future donor comparison remains behavioral evidence | High | M07A/M07B complete; M07C night/dawn follow-up automated tests pass and manual dawn/night retest is `USER PASS` 2026-07-18 without capture artifacts | Donor schedule, transition durations, wetness persistence and wind/cloud values remain unknown; all logical/runtime presentation values are project-owned `RemakeDesignTarget` |
| Audio | Shared-assets sound containers/derived sound directories; `MasterAudio`, `EventSounds`, `SoundController`, playlists; Unity Audio; frozen `GAME.unity` `AudioEngineSatsuma`, `MasterAudio/Starting` and `MAP/SoundAmbience` mapping | High | FSM events, player/vehicle/world state, mixer, listener, save settings | Hash-ledgered prototype clips remain local ignored `TemporaryDirectImport` and routing metadata `ReferenceOnly`; project-owned `IAudioBackend`, router, Unity fallback and isolated official Wwise 2025.1.9 adapter are `Reimplemented`; final soundscape is newly authored | High | M1 boundary; M6 diagnostic; M8 accepted foundation; 2026-08-02 parity pass validates 65 events, 32 RTPCs, 4 switches, 3 states, six banks, the full time/weather/spatial main-world ambience roster, local wind-chime/insect hooks, rare near-home chainsaw, runtime footsteps and +4 dB project master ceiling | Manual audible/spatial acceptance; final authored content/mix; temporary MSC_World streaming/compression; Wwise Profiler polish; deferred extended interaction/UI producers and local gameplay-hook owners |
| Animation | `HOTween.dll`, iTween, `SimpleIKSolver`, `IKLimb_BrunoFerreira`, SWS movement, many PlayMaker animation actions | High | Rigs, transforms, interaction targets, FSM timing, tools/vehicle controls | Case-by-case reference; reimplement interaction IK and presentation; retarget only compatible verified clips | High | M4 presentation; M5 tool/assembly IK | Clip/rig inventory, generic/humanoid compatibility, event authority, steering/tool target transforms |
| UI | `SettingsMenu`, `ShowcaseGUI`, legacy Unity UI/GUI, `cInput`, many PlayMaker GUI/input actions, menu scenes | Very high | Input, save/options, localization, gameplay FSMs, audio settings | Reimplement with current UI/input architecture; transfer labels/flows/config only as behavioral reference | Medium-high | M4 minimal HUD; later feature UI | Screen inventory, accessibility, localization, option semantics, state ownership, resolution behavior |

## Candidate review queue

The following are review candidates, not approved ports:

1. `Clutch.GetDragImpulse` and clutch-position transitions — plain-object metadata, potentially isolated.
2. `Drivetrain.CalcEngineTorque*`, `CalcEnginePower`, and curve lookup — promising names but embedded in a large `MonoBehaviour`.
3. `Wheel` tire/slip/force functions — potentially valuable formulas, but deeply tied to legacy physics/timestep state.
4. `Axle` and `TireParameters` coefficient tables — configuration candidates after provenance, unit, and vehicle applicability checks.

Each candidate requires an `ilspycmd` body review, dependency map, origin hash, comparison fixture, units, tolerance, known differences, and a ledger update before it can become `CodePorted` or `ConfigurationTransferred`.

## Milestone 3 world/art transfer state

| Элемент | Donor evidence | Классификация результата | Статус и граница |
|---|---|---|---|
| Garage roof scale/pivot | `garage_shed_roof`, Mesh PathID `2186`, exact container/OBJ hashes и bounds | `DimensionalReference`, `PivotSource`, `ReauthoredGeometry` | Проверено с допуском `0.005 m`; reference остаётся только в comparison scene |
| Garage shell/interior/props | Только roof envelope; полный layout не измерен | `ReauthoredGeometry` | `PrototypeReady`; самостоятельная реконструкция, не точный порт |
| Materials/textures | Donor textures/materials не переносились | `ReauthoredMaterial`, `ReauthoredTexture` | 12 HDRP materials и 36 project-authored maps; `PrototypeReady` |
| Road/terrain/vegetation | Donor centerline/terrain не извлекались | `Reimplemented`, `ReauthoredGeometry` | 180 m local context baseline; не классифицируется как `WorldLayoutReference` |
| Lighting/atmosphere | Только художественная идентичность и milestone brief | `Reimplemented` | Neutral/late-day HDRP presets; runtime weather ещё не реализована |

Значения wall openings, interior arrangement, road curve, terrain heights и vegetation placement не являются `ConfigurationTransferred`. Ни один M3 production prefab или build scene не зависит от donor payload.

## Milestone 04A bounded world-layout transfer state

| Element | Exact evidence | Classification | Status and boundary |
|---|---|---|---|
| Garage scene anchor | `level2` hash `39e5…06c31`; `CABIN` GO 18336/T 54392 > `Shed` GO 19567/T 55631 | `WorldLayoutReference` | Reviewed project-local origin; not a complete building-layout claim |
| Adjacent dirt-road route | `DirtRoad` GO 21869/T 57925; seven retained waypoints 1655–1685 | `WorldLayoutReference` | `191.353 m` sampled polyline; traffic-route approximation with possible lane offset |
| Terrain/render subset | `TERRAIN_OBJ` GO 34670/T 70724; render road GO 1616/T 37675; candidate mesh PathID 3273 | `Blocked` | No payload exported because available separation is combined/full-world |
| Durable pilot record | `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json` | `WorldLayoutReference` | Project-owned stable IDs, portable provenance, bounds, conversion and limitations |
| Comparison scene | `Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity` | `ReferenceOnly` | Ignored, outside Build Settings and removable without breaking production |

The retained samples place the nearest reviewed road-route point `204.768 m` from the garage anchor. The M3 road prefab is explicitly still a project-authored visual prototype and does not match that relationship; 04A records the gap but does not rewrite production terrain/road content. No full scene, full route, complete terrain or second world zone was transferred.

## Milestone 4 player/interaction transfer state

| Система | Evidence used | Classification | Текущее состояние |
|---|---|---|---|
| First-person movement/look/crouch | Project prompt, architecture and authored Input System map only | `Reimplemented` | CharacterController prototype; donor controller values/code не использованы |
| Interaction query/context | Project capability contract only | `Reimplemented` | Bounded raycast + explicit `InteractionTargetHost`; без object-name/FSM dispatch |
| Pickup/carry/place/drop/throw/rotate | Project physical-interaction design only | `Reimplemented` | Rigidbody carry prototype с восстановлением physics state |
| Tool activation | Project capability contract only | `Reimplemented` | Basic target/counter proof; donor tool rules не переносились |
| Mount handoff | Future project architecture boundary only | `Reimplemented` | Односторонний interface proof; assembly graph/compatibility остаются M5 |
| Carried-object save state | Project-owned stable IDs and native DTO design | `Reimplemented` | Snapshot schema v1; storage/load resolver не реализованы |

Нет оснований классифицировать M4 значения как `BehavioralReference`, `ConfigurationTransferred` или `CodePorted`: donor runtime и decompiled reference в этом этапе не читались. Ledger не дополнялся искусственными donor-записями.

### Post-08A player correction (2026-08-10)

The table above records the original Milestone 4 boundary and is retained as
history. The bounded player audit now uses exact locked donor controller/camera
serialization and staged CharacterMotor grounding behavior. The current result
is `ConfigurationTransferred` plus clean-room `Reimplemented`: a constant
`0.5 m x 0.12 m` traversal capsule, `0.4 m` step, camera-only posture,
ground-normal projection/snap, collision-limited `40 degree` lean from the exact
donor `Y = -0.3 m` body hinge, a smooth `120 degree/s` return and a running
wall-impact event. The project-owned response adds camera/eyelid feedback and
posts the existing typed interaction-impact audio event. BetterMSC is a
hash-locked third-party `BehavioralReference`; its DLL, assets and runtime are
not dependencies.

### Secondary player-feel reference (2026-08-12)

Cheap Car Repair build `24237531` / game `1.2.0025` was inspected read-only
outside Phase 1 donor scope. Movement and camera measurements are
`BehavioralReference;ConfigurationTransferred`; runtime behavior is a
project-owned clean-room `Reimplemented` extension. The accepted My Summer Car
traversal, lean, interaction, save and stable-ID contracts remain authoritative.

## Milestone 04A1 full world reference state

| Layer | Classification | Status | Ограничение |
|---|---|---|---|
| Full GAME placement inventory | `WorldLayoutReference` | 36 045 placements normalized | Serialized state текущей mod-contaminated установки |
| Geometry entity database | `WorldLayoutReference` | 13 509 records, stable IDs unique | Semantic review продолжается |
| Reference world subset | `ReferenceOnly` | 3 842 entities, 49 cells + global | Neutral proxies, не donor render fidelity |
| Mesh payload | `ReferenceOnly` | 1 362 referenced GUID во внешнем staging | Не production geometry и не Git content |
| Colliders | `CollisionReference` | 5 001 metadata records | Production colliders не генерировались |
| Terrain/roads/water | `WorldLayoutReference` | Placement/mesh records представлены | Heightfield, road graph и shoreline topology не доказаны |
| Generated scenes/materials | `ReferenceOnly` | Ignored, clear/rebuild validated | Unity YAML byte hashes не стабильны, stable IDs/plan стабильны |

Ни один 04A1 результат не классифицирован как `ProductionReady`, `CodePorted`, `ReauthoredGeometry` или `ReauthoredTexture`. Player/Interaction остаются независимыми `Reimplemented` модулями.

## Milestone 04B reference-data state

| Domain | Evidence/classification | Dataset status | Boundary |
|---|---|---|---|
| World/garage coordinates and roof | `WorldLayoutReference`, `DimensionalReference`, `PivotSource` | P0 anchor/scale/roof covered | Garage opening/interior and road surface remain missing |
| Player base settings | `ConfigurationTransferred` | Camera anchor, walk speed, controller and acceleration/gravity static records | Sprint, crouch, reach and pickup/throw runtime behavior missing |
| Satsuma body/wheels | `DimensionalReference`, `ConfigurationTransferred` | Body mesh envelope, four wheel anchors, wheelbase/tracks, root mass | Overall envelope, curb state and fitted tire identity partial |
| Rear brake drum | `DimensionalReference`, `PivotSource`, `MountPointSource`, `BehavioralReference` | Mesh/pivot/installed transform, 0.01 m collider candidate, install/removal gates, one marker, discrete stages 0..8, wrench 14, scroll direction, three clean runtime repetitions and wheel-installed blocker recorded | Both M05 gates `Covered`; blocker attestation has `Medium` confidence, physical torque is outside the discrete donor contract |
| Powertrain/dynamics | `BehavioralReference` procedures only | Fixtures `Missing` | No simulation constants or algorithms implemented |
| Time/weather/audio/UI | `BehavioralReference` procedures only | Fixtures/checklists `Missing` | No runtime implementation and no raw capture payload |
| M4 remake tuning | project-authored `Reimplemented` | Separate tuning overrides | Never stored as donor measurement |

`ReferenceCaptureDatabase` is project-owned metadata and calibration input, not donor runtime content. No 04B item is classified `CodePorted`, `TemporaryDirectImport` or `ProductionReady`.

### Player base-settings update (2026-08-10)

Controller, camera, gravity, posture heights, grounding, the exact below-body
lean pivot, smooth return, collision limit, eyelid/audio response and the
wall-impact gate are integrated and automated-tested. Override dataset
`04B.5` retains stable record IDs while aligning controller/camera/gravity with
evidence. Walk speed `3 m/s` remains explicit project tuning pending repeated
donor runtime comparison. Exact active-world garage-pit and irregular geometry
acceptance remains manual.

## Milestone 05 assembly implementation state

| System | Source evidence | Classification | Implemented boundary |
|---|---|---|---|
| General part/mount graph | Project architecture and M05 requirements | `Reimplemented` | Project-owned IDs, definitions/instances, explicit dependencies and deterministic operations |
| Rear-left drum mount | Dataset `04B.4`, pivot/mount fixture and three runtime repetitions | `Reimplemented` from `MountPointSource`/`BehavioralReference` | Clean mount definition; donor marker retained as provenance; remake tolerances labelled tuning |
| Drum fastener/tool | One BoltPM, wrench 14, discrete `0..8` and direction observations | `Reimplemented` | Integer fastener stages and tool-size rule; no torque/thread simulation |
| Drum removal blocker | Static wheel gate plus user runtime attestation | `Reimplemented` | Explicit removal dependency on `vehicle.wheel_rl` |
| Other 14 representative parts | Project-authored prototype requirements | `Reimplemented` | Graybox definitions/configuration; not donor-calibrated or production-ready |
| Assembly save state | Project-owned stable IDs and DTO architecture | `Reimplemented` | Schema-v1 capture/validated restore; storage and legacy import excluded |

## Milestone 05A production-world state

| Area | Reference input | Classification | Bounded result |
|---|---|---|---|
| Production registry | 13,509 frozen 04A1 geometry records and cell IDs | `WorldLayoutReference` | Every record has a replacement status; 33 direct bindings and 13,476 explicit backlog links |
| Home/garage pilot | `YARD/Building/Garage` anchor and selected building/opening/cable records | `ReauthoredGeometry`, `ReauthoredMaterial` | Deterministic `cell_0_-3` production cell; first-pass rather than final art |
| Home shoreline/pier Batch 01 | Nine `cell_0_-2` pier/water/hedge anchors | `WorldLayoutReference`, `ReauthoredGeometry`, `ReauthoredMaterial` | Deterministic `cell_0_-2`; `9/15` mapped, six unrelated gameplay records remain backlog; manual visual/performance gates pending |
| Terrain/road/water context | Cell assignment and gameplay-space needs | `Reimplemented` | Project-authored pilot geometry; direct donor terrain/road/water parity remains unverified |
| Moving architecture | Source pivots/relationships where mapped plus project fit rules | `Reimplemented` | Six separate hinges using the existing interaction capability; manual arc/parity review pending |
| Vegetation/props | Reference categories/masses | `ReauthoredGeometry` | Reusable pilot prototypes; raw-record direct coverage remains zero and is backlogged |
| Comparison layer | 04A1 bounds/placements | `ReferenceOnly` | Metadata proxies only; removable and not a production dependency |

## Milestone 05C1 safety-topology state

| Area | Reference input | Classification | Bounded result |
|---|---|---|---|
| Teimo void boundary | 04A1/05C placements for `TREEWALL_HI`, `TERRAINOUT`, store foundation and lakebed | `WorldLayoutReference`, `DimensionalReference` | Six explicit station profiles in a fingerprinted project-owned CSV; no donor payload |
| Continuous ground pieces | Project-authored interpolation of the approved profile | `BlockoutSource`, `ReauthoredGeometry` | Two cell-owned meshes with exact seam and static collision; bounded manual gate passed 2026-07-15 |
| Reference overview/captures | Removable 05C geometry | `ReferenceOnly` | Editor evidence only; excluded from production dependencies and Build Settings |

No 05C1 asset is classified `ProductionReady`. The marker explicitly records
that the surface is safety topology and does not open a gameplay area.

## Milestone 05B validation state

| Area | Classification | Validated result | Remaining boundary |
|---|---|---|---|
| Canonical replacement inventory | `WorldLayoutReference` | 13,509 donor records, 33 direct production bindings | 13,476 records remain without direct production binding |
| Eligible world coverage | `WorldLayoutReference` | 33/3,842 (0.858928%), 2/49 bound cells | `Approved/Verified=0`; FullWorld coverage is not claimed |
| Bounded spatial fixtures | `DimensionalReference` | max 0.232306 m, mean 0.051240 m, p95 0.232306 m; all measured tolerances pass | Full terrain/road/water/building parity unavailable |
| Production dependency graph | `Reimplemented` validation | 49 seeds, 230 assets, 482 edges, zero prohibited references | Current-world player assembly audit pending |
| Production-cell lifecycle | `Reimplemented` validation | Two clean load/unload/reload cycles per cell, stable ID set preserved | No production focus-driven streamer |
| 05C1 supplemental topology | `ReauthoredGeometry` | 2/2 pieces validated separately | Not donor replacement coverage; runtime integration pending |

05B adds validation tooling and factual status only. It does not promote any
first-pass replacement to `ProductionReady`, does not port donor code and does not
claim `PilotGate`, `VerticalSliceGate` or `FullWorldGate`.

## Milestone 06 vehicle-simulation state

| Area | Evidence/input | Transfer classification / validation level | Implemented boundary / status |
|---|---|---|---|
| Wheel anchors, wheelbase and tracks | Reviewed 04B vehicle geometry records | `DimensionalReference` / `DerivedReference` | Exact four anchors in the proxy and logical fixture; validator tolerance `0.001 m`; strict validation PASS |
| Root mass/body/radius interpretation | `389 kg` root component, body AABB, candidate `0.272667 m` mesh radius | `DimensionalReference` / radius `PlausibilityTarget` | 389 kg is not curb mass; radius remains `NeedsReview`; neither is promoted to fitted/assembled truth |
| Pure powertrain/support simulation | Project architecture and M06 requirements | `Reimplemented` | Small clean-room nodes behind central config; one configurable driven-wheel pair with current authored FL/FR (`0/1`) FWD mapping; all numeric dynamics labelled `RemakeDesignTarget` / `ProvisionalProjectTuning` |
| Assembly prerequisite adapter | Separate 12-part project-owned logical fixture and M05 `AssemblyGraph` | `Reimplemented` | Cached typed failure snapshot keyed by graph mutation; logical graph remains separate from dynamic proxy |
| Four-wheel raycast backend | Project-authored PhysX adapter | `Reimplemented` | `IWheelPhysicsBackend` sample/command boundary; provisional tire/suspension/brake behavior |
| Bounded surface route | Project-authored paved/gravel/dirt/grass graybox | `BlockoutSource` | Backend fixture only; not donor/production-world parity; `WORLD-COL-003` open |
| Simulation state and telemetry | Project-owned schema and diagnostics | `Reimplemented` | Schema-1 state DTO and development CSV/overlay; full save and production-audio integration excluded |
| Post-manual rest/view remediation | User observation: about 5 km/h startup creep and environment shake | `Reimplemented` | First-sample suspension history, planar speed, bounded no-overshoot passive force, level-only startup settling and detached smoothed camera; focused PlayMode `4/4 PASS` proves a six-degree incline is not pinned and later external wake/velocity is not re-slept; bounded user recheck accepted 2026-07-16 |
| Local Satsuma auditory diagnostic | Seven hash-pinned clips in frozen external staging; static `GAME.unity` mapping | Clips `TemporaryDirectImport`; routing evidence `ReferenceOnly` | Editor/local-only perceptual aid sampled at `FixedUpdate`; starter event order tested; missing staging is silent fallback; no donor binary in Git, production `Assets` or builds; no proven stop/stall clip; final soundscape remains reauthored |

Reviewed donor dynamic fixtures remain `Missing`; no torque, ratios, tire, suspension, brake, acceleration or handling algorithm/value was ported. Within the M06 vehicle scope, no implementation row is `CodePorted` or `ProductionReady`, and `TemporaryDirectImport` applies only to the seven frozen external diagnostic clips. The M06 automated gate passes: focused EditMode `18/18`, focused PlayMode `4/4`, full PlayMode `31/31` and all M06 cases inside the fresh `157/160` full EditMode run (`44.4875523 s`). Its three failures are the same known unrelated baselines. Every focused PlayMode case verified all seven staged hashes/loads with configured staging; missing staging remains silent fallback. The first manual drive confirmed core start/shift/stall/RPM behavior and exposed creep/view-shake defects; the bounded post-remediation drive/listening recheck was accepted on 2026-07-16.

## Milestone 06B1 temporary donor-world runtime baseline

| Layer | Source evidence | Classification | Implemented boundary / status |
|---|---|---|---|
| Canonical extracted scene | Frozen AssetRipper `GAME.unity`, SHA-256 `c3f2f337…0476c4`; path map and normalized manifests | `TemporaryDirectImport` source for private local feature parity | Source revision `msc-world-baseline-04a1.1-c3f2f337`; current donor hash-drift install is not mixed into it |
| Sanitized static world presentation | 3,842 eligible world records, audited meshes and static-batch subsets | `TemporaryDirectImport` | 2,605 static renderers; exact transforms/effective activation retained; 1,237 metadata-only |
| Character/NPC presentation | 62 skinned records and 117 static records below `/skeleton/` | `Rejected` for map-only baseline presentation | Metadata retained; runtime renderers excluded |
| Runtime logic | Donor MonoBehaviours, PlayMaker, assemblies, camera/audio/weather/UI/gameplay components | `Rejected` | Zero transferred donor runtime components; explicit whitelist validator |
| Collision | 5,001 normalized source records | `CollisionReference` | Runtime collider count `0`; safe transfer and traversal validation deferred to 06B2 |
| Canonical local payload | Generated scene, 2,129 mesh assets and 22 neutral materials | `TemporaryDirectImport` | Ignored by Git, outside Build Settings, private-local-only, not `ProductionReady` |
| Existing prototype cells | `cell_0_-3`, `cell_0_-2` custom scenes and project-owned streaming fixtures | `PrototypeOnly / RejectedForFidelity` | Infrastructure retained; visual activation in donor feature-parity profile deferred to 06B2 |

Repeated generation is locked by semantic and generated-payload fingerprints,
not by self-comparison after manifest overwrite. Cold validation, focused
EditMode `4/4` and PlayMode `1/1` passed. No result is `CodePorted` or
`ProductionReady`.

## Milestone 06B2 active donor-world streaming baseline

| Layer | Input/evidence | Classification | Implemented boundary / status |
|---|---|---|---|
| Global/cell legacy presentation | Frozen 06B1 canonical scene and project-owned 512 m partition | `TemporaryDirectImport` | 1 global + 49 cell scenes; 3,842 exact-transform entities; active private-local profile; automated validation PASS and user visual/traversal acceptance recorded |
| Deterministic ownership | Frozen source cell assignments, large-object policy and explicit exceptions | `WorldLayoutReference` | 3,754 cell-owned and 88 global entities; fingerprint `1abf88e…bf28b`; no geometry split or layout reinterpretation |
| Donor material closure | 292 exported material definitions plus built-in material `10302` on two renderers | `TemporaryDirectImport` | 293 shared project-owned HDRP compatibility materials including one reviewed orange fallback; no donor shader code imported |
| Donor texture closure | 265 referenced source images / 301,818,115 source bytes | `TemporaryDirectImport` | 267 shared role-specific variants with mipmaps/streaming/read-only import; one reflection cubemap excluded; detailed source hashes in `LEGACY_MATERIAL_TEXTURE_MANIFEST.csv` |
| Legacy presentation modes | Ordered material GUID slots and project-owned runtime bindings | `Reimplemented` | `LegacyTextured` default, `LegacyDiagnostic` comparison, `PrototypeHidden` active-profile invariant; only `Renderer.sharedMaterials`, zero runtime material instances in performance tests |
| Safe runtime collision subset | Frozen collider inventory plus project-owned 32-record allowlist | `TemporaryDirectImport` | 20 static mesh and 12 static box colliders; no triggers, bodies, joints or donor behavior; representative raycasts PASS |
| Legacy identity/replacement | Source provenance IDs plus project-owned mapping | `Reimplemented` | Unique `LegacyWorldObjectId` and `legacy-world:<stable-id>` replacement key; donor name/path is not runtime or save identity |
| Gameplay anchors | 15 project-owned stable anchors in the two former prototype regions | `Reimplemented` | Separate `WorldGameplayCellCatalog`; persists independently of legacy visual load/unload and override state |
| Existing prototype visuals | Custom `cell_0_-3` and `cell_0_-2` scenes | `Rejected` for feature-parity fidelity | Removed from active donor profile; retained as `PrototypeOnly` regression fixture; no asset deletion |
| Streaming lifecycle | Existing manifest/service/bootstrap architecture | `Reimplemented` | Global lifetime, focus streaming, radius hysteresis, 12 m/s vehicle preload, owned-scene reconciliation and out-of-bounds recovery |
| Distribution boundary | Ignored generated RuntimeBaseline plus exact-scene build guard | `Reimplemented` | Private local Development only with explicit acknowledgement; public/distributable build blocked |

No 06B2 result is `CodePorted` or `ProductionReady`. Donor visual/collision
content remains temporary and non-distributable. Human acceptance was recorded
2026-07-16 for Bootstrap, donor-map geometry fidelity, lake/Teimo traversal,
streaming reload, OOB recovery and the v5.1 textured presentation including
corrected water. Legacy textures, terrain banding and tree-wall artifacts were
accepted only as temporary visual debt. On 2026-07-16 the user walked the
bridges and moved the character across cell boundaries without observing
traversal, collision, seam, duplicate, popping or load/unload issues. This
manual bridge/cell-boundary gate is `PASS / HumanAccepted`; dedicated vehicle
driving was not repeated, while automated high-speed preload validation passed.
The flat lake and original-game voids remain explicit remaster debt. The later
06B3 validation/freeze closed as `PASS / Frozen / HumanAccepted`; this does not
promote donor baseline art to `ProductionReady`.

## Milestones 07A/07B environment implementation state

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Enviro 3 local package and API | Hash-identified third-party installation; 07A audit and WeatherLab | Third-party presentation dependency; not donor content and not a transfer classification | 07A implementation committed as `61250e2`; vendor source stays read-only; accepted 07B baseline is `538 / 305967931 / 8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44` |
| Game time/calendar | Project architecture and M07B requirements | `Reimplemented`; `RemakeDesignTarget` | `MSC.Core.Runtime` owns deterministic progression, pause/scale, scheduling, exact snapshot/restore and versioned DTOs; callback/subscriber failure restores clock and due queue, reentrant mutation fails closed, and Enviro cannot feed gameplay time back |
| Weather fronts and stable outputs | Project-authored profile/front catalog, seeded RNG and output contracts | `Reimplemented`; `RemakeDesignTarget` | `MSC.Weather.Runtime` owns logical state, transitions, overrides, timeline/RNG and cross-system snapshots; no gameplay check uses Enviro preset names |
| Wetness and material proof | Project-authored accumulation/drying/exposure model | `Reimplemented`; `RemakeDesignTarget` | Ground/road/puddle/vegetation state and DTOs are authoritative; WeatherLab proves shared global/property-block material outputs without per-object material instances |
| Lightning | Project-authored candidates, attractors, protection, cooldown and fairness rules | `Reimplemented`; `RemakeDesignTarget` | Ambient presentation and gameplay strikes are separated; gameplay selection/thunder/effect hooks exist, while a parallel health system and production damage are intentionally absent |
| Presentation mapping | Stable project binding IDs plus validated Enviro public API | `Reimplemented` adapter over read-only vendor dependency | `MSC.Weather.Presentation.Runtime` is vendor-neutral; only `MSC.Weather.Enviro3Integration` references Enviro and maps time, weather, quality and visual requests in WeatherLab |
| Save/dev validation boundary | Project-owned schema DTOs, Time and Weather tooling and WeatherLab | `Reimplemented` | DTO round trips and diagnostics exist; failed composite DEV advance transactionally restores time/weather/wetness/lightning; final file persistence belongs to M09; production-world rollout belongs only to 07C |

Final automated core evidence is `101/101 PASS`: GameTime `20`, WeatherDomain
`29`, WeatherPresentation `52`. Enviro integration is `13/13 PASS`, WeatherLab
time-domain integration is `3/3 PASS`, and the Editor PlayMode performance
harness is `1/1 PASS`. Builder/fresh preflight
`Logs/M07B_WeatherLabBuilder_Final_03.log` is `PASS`. Automated-only performance
evidence is stored in
`PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`; manual
comparison captures and standalone/real-GPU sign-off remain `PENDING`, so they
are not inferred from the automated evidence.

The ledger uses explicit `ProjectAuthored/Milestone07B` rows with empty source
hashes because these are clean-room project systems. They do not create donor
provenance records, do not change any donor transfer classification and do not
claim `CodePorted` or `ConfigurationTransferred`. This was the 07B handoff into
`07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`; current 07C status follows.

## Milestone 07C production rollout state

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Production environment composition | Project-owned 07B domains, Bootstrap lifetime and read-only Enviro API | `Reimplemented`; Enviro is a third-party presentation dependency | One persistent `ProductionEnvironmentController` and one adapter; linked inactive Enviro prefab activates only after session ownership; no direct Enviro reference from core/gameplay assemblies |
| Scene and streaming lifecycle | Existing additive donor-world streamer and project composition root | `Reimplemented` | Coalesced post-lifecycle owner validation, shelter rebuild, duplicate Bootstrap rejection, same/different Single-scene root handoff and clean teardown; repeat cellization preserves production mode and explicit fixture conversion removes weather ownership; focused PlayMode `6/6 PASS`, legacy world-only compatibility `1/1 PASS` |
| Wetness/material compatibility | 07B wetness outputs plus reviewed legacy material-slot allowlist | `Reimplemented` compatibility layer over `TemporaryDirectImport` visuals | Shared globals/property blocks only; no unique material instantiation; wet smoothness remains capped at `0.45` opaque / `0.25` alpha-clip and temporary baseline reflections at `0.6`; the user accepted the metallic look as temporary donor material/shader debt, but current generated-material contract validation is not clean |
| Home shelter measurements | Frozen home-house/garage renderer bounds | `DimensionalReference` input; project-owned runtime identities | Two stable-ID interior AABBs with pinned selected-geometry fingerprint; full cell-scene SHA is audit-only metadata; deterministic removal ellipsoids preserve horizontal tiling and enforce vertical stretch `>= 1`; Teimo and other interiors are not claimed; living-room rain retest is user-accepted |
| Time/weather/lightning/save orchestration | Project-authored 07B state and 07C lifecycle requirements | `Reimplemented` | Restore precedes reveal, initial sync is instant, scheduled fronts use remaining simulation time, ambient lightning is director-owned and load/spawn bounded; runtime presentation uses solar calibration `60 N / 27.3 E / UTC+3`, approximately `04:59`/`21:35` horizon crossings on the reference date, and same-pass sun updates; no Enviro runtime object is serialized |
| Low/Medium/High presentation mapping | Project quality IDs and isolated Enviro runtime clones | `Reimplemented` adapter mapping | Structural and focused automated tests pass; aurora is suppressed across tiers; smooth minimum night exposure is `7.5 EV` from `solarTime <= 0.43` toward unchanged daylight at `0.5`; manual dawn/night comparison is `USER PASS` 2026-07-18 without capture artifacts, while real-GPU performance remains `PENDING` |

Fresh night/dawn follow-up evidence is Enviro integration `17/17`, combined
production EditMode `32/32` and production lifecycle PlayMode `6/6`, with exact
XML artifacts under `Logs/M07C_VisualRemediation3_*`. Full EditMode is `328/334`:
the same four historical failures plus two current WorldBaseline material-
contract failures caused by four ignored generated materials that were already
rewritten before this follow-up. A fresh focused WorldBaseline rerun is `8/10`
with the same two failures, confirming persistent generated-payload drift rather
than full-suite ordering pollution. The previous result in
`Logs/M07C_VisualRemediation2_WorldFreeze.log` remains the last strict world-
freeze `PASS` with SHA-256
`10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`;
current generated-material validation is not clean. Rain and the current sunset
are user-accepted, metallic-looking temporary surfaces are accepted visual debt,
and the corrected dawn/night presentation is `USER PASS` on 2026-07-18 without
capture artifacts. Matched fidelity, other interiors and Development Player
performance remain pending; this is not whole-milestone acceptance. No 07C item
is classified `CodePorted` or `ConfigurationTransferred`.

## Milestone 08A1 donor-world baseline hardening candidate

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| HDRP material compatibility | Frozen 06B2 material/texture manifests and 2,605 renderer bindings | Donor presentation stays `TemporaryDirectImport`; generator and policy are `Reimplemented` | Generator `06B2-v5.2-08A1`, policy `08A1-temporary-hdrp-compatibility-v2`; capped metallic/smoothness/emission, corrected detail-luminance packing and two-sided normals; no donor shader code and no `ProductionReady` promotion |
| Renderer shadow compatibility | Frozen renderer identities, reviewed material category and bounds | Source presentation `TemporaryDirectImport`; deterministic compatibility policy `Reimplemented` | Explicit cast/receive/two-sided rules plus four exact frozen-ID semantic overrides; 2,605 generated renderer bindings retain original transforms and stable replacement keys |
| Static traversal collision | Frozen 1,488-record collider inventory and immutable 32-record safety allowlist | Selected shapes `TemporaryDirectImport`; disposition, stable ownership and generator policy `Reimplemented` | Policy `08A1.6`: 586 solids total (32 safety-critical global + 554 cell-owned static); 276 mesh, 279 box, 31 capsule; source convex state audited while static runtime mesh colliders are non-convex |
| Excluded collision domains | Donor triggers, actors, Rigidbody ancestry, vehicles, disabled/inactive records and doors | `Rejected` for the bounded static-solid candidate, not deleted from evidence | 79 doors deliberately remain pass-through pending project-owned door mechanics; 108 Rigidbody-ancestry records remain dynamic; no trigger or donor behavior is imported |
| Daylight and indirect-lighting compatibility | Existing project-owned Enviro/HDRP presentation boundary | `Reimplemented`; not donor `ConfigurationTransferred` | Runtime-cloned HDRP `IndirectLightingController` applies bounded exterior/sheltered diffuse uplift while neutral/interior diffuse and reflections remain 1.0; vendor source remains unmodified |
| Candidate/rollback boundary | Deterministic v002 manifests, candidate records and automated logs | `Candidate / NotPromoted / HumanAcceptancePending` | v002 is active only in the main local workspace for manual validation; accepted v001 remains recorded and is externally backed up as `DonorWorldBaseline-v001_before_08A1_20260720`; focused candidate EditMode `41/41`, PlayMode `14/14`, protected UI EditMode `33/33`, UI PlayMode `11/11` pass |

No 08A1 result is `CodePorted` or `ProductionReady`. Automated validation does
not replace the pending manual checks for roof/shadow leakage, temporary
material readability, representative obstacle collision, cell reload and the
intentional pass-through-door limitation.

## Phase 1 item/lighting playtest corrections (2026-08-01)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Loose item gravity and throw | Existing v9 save payloads plus project carry/world-entity code | `Reimplemented`; project regression repair | Document `9 -> 10` repairs only IDs owned by `items.instances`; loose items restore gravity/collision and keep throw impulse/rotation; Item/Save `50/50`, targeted throw `1/1` |
| Basketball surface and dynamics | Frozen basketball material/texture GUIDs and spherical donor item evidence | textures `TemporaryDirectImport`; HDRP material/physics `Reimplemented` | Hash-locked albedo/normal, SphereCollider, 0.68 bounce, low damping; manual VotV-like feel check pending |
| Helmet surface and paint state | Frozen `racing_accessories` atlas and read-only helmet `Paint` FSM | atlas `TemporaryDirectImport`; paint state/presenter `Reimplemented` | Project-owned color/matte state, migration `10 -> 11`, shell MPB binding; spray color mapping remains pending evidence |
| Home and garage light leakage | User playtest captures; project light catalog and thin paired roof shells | `Reimplemented` presentation calibration | Home fixtures bounded to 650–1,800 lm / 3–8 m, outdoor lamp NightOnly, reduced local/sun shadow bias; lighting EditMode `9/9`, manual visual re-acceptance pending |

## Phase 1 Teimo bicycle addendum (2026-08-02)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Bicycle routes and schedule | Frozen `TeimoInBike` Move/SplineMove plus path managers `106459`/`112267` | Waypoints, speed and start times `ConfigurationTransferred`; FSM event flow `BehavioralReference` | Project-owned route manifest generates two 37-point once-only routes, stable schedule blocks and terrain-conforming poses; off-screen progress and `npc.state` save remain authoritative |
| Rider/bicycle presentation | Hash-locked rider/accessory/bicycle meshes, motor-parts atlas and greeting clip | Selected private payload `TemporaryDirectImport`; wrapper/material mapping `Reimplemented` | Seven-wrapper catalog revision 8 switches the existing Teimo stable instance only for bicycle schedule blocks; bounded iteration conditionally user-accepted 2026-08-02; no donor controller, FSM, script, shader or hierarchy lookup at runtime |
| Collision outcome | Donor behavior reported as immediate ragdoll/sleeping doll after contact | `BehavioralReference`; implementation pending only for this bounded response, not for route motion | Not implemented or claimed. Remount/walk recovery is recorded as a Phase 2 candidate and cannot replace Phase 1 parity before the gate |

Automated validation passes focused NPC EditMode `16/16` and PlayMode `3/3`.
The bounded iteration is conditionally user-accepted; final route/presentation,
greeting repetition and collision response remain pending, so
`P1.VEHICLE.027` is `PartiallyImplemented`, not `Verified`.
## Milestone 10B-R2 NPC foundation state

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| R2 character identity | Locked GAME scene actor/state nodes for P1.NPC.003–.006, .009–.017 and .101 | `BehavioralReference`; `ConfigurationTransferred`; project stable IDs | Fourteen saved identities added; Jokke variants are one identity and his wife is explicitly `StateOnly` |
| R2 physical presentation | Hash-locked selected meshes, textures, material identities, accessories and clips | `TemporaryDirectImport`; replaceable project wrappers | Thirteen wrappers generated; 20 total fixture records including accepted R1 overrides; manual comparison pending |
| Anchors/streaming | Exact locked-scene transforms plus audited M04A1 translation | `ConfigurationTransferred`; project cell IDs | Fourteen anchors resolve through the existing streaming foundation; no donor hierarchy lookup at runtime |
| Availability | Active/inactive scene evidence is insufficient to prove full schedules | Project-authored review baseline, not donor parity | Eleven actors visible for review; Uncle, Suski and wife state remain hidden; owning milestones must install exact gates. Suski begins as Jani's passenger and only unlocks the store-hiker state after rescue from the crash. |
| Save | Existing `npc.state` schema 2 and native document v11 | `Reimplemented` compatible migration | Document v12 adds R2 identities while preserving accepted snapshots; focused migration tests pass 3/3 |
| Dialogue/audio | Identity and interaction boundary only | Project hook; donor voice transfer pending | Identity-only non-quotation lines and stable pending audio IDs; no R2 voice parity claim |

## Milestone 10B-R3 story-traffic foundation state

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Jani / KYLAJANI | Locked GAME car, driver, wheel and passenger-anchor transforms; user-confirmed name | `BehavioralReference`; `ConfigurationTransferred`; selected presentation `TemporaryDirectImport` | Stable `P1.NPC.049`, streamed wrapper, rotating wheels and smooth provisional review route |
| Petteri / AMIS2 | Locked GAME car, driver and wheel transforms; user-confirmed name | `BehavioralReference`; `ConfigurationTransferred`; selected presentation `TemporaryDirectImport` | Stable `P1.NPC.050`, streamed wrapper, rotating wheels and a separate smooth provisional review route |
| Suski passenger | BetterMSC Suski prefab/avatar/materials and `Suski_car_sit`; locked KYLAJANI passenger pelvis/ankle skeleton | Presentation `TemporaryDirectImport`; switching/calibration logic `Reimplemented` | `P1.NPC.006` is the single passenger root in Jani's car by default; manifest v7 restores the serialized 43-node pose with source-compatible `HeadPivot` ownership and zero-offset `Bip01 Head`, aligns yaw and pelvis to exact donor transform IDs, checks seat height/car-forward direction, excludes four original detail renderers and incompatible hand IK, preserves the saved rescue switch, and rolls back generated output/catalog on import failure. Corrected seat height, facing, neck and arms manually accepted 2026-08-04. |
| Save compatibility | Existing document v12 with 20-character R2 state | Project migration `Reimplemented` | Document v13 migration preserves all compatible snapshots/flags and appends fresh Jani/Petteri rows |
| Validation | Generated private assets plus project runtime | Project-owned automated checks | V7 Unity batch rebuild passes with live Jani/Petteri catalog references; NPC EditMode `16/16`, NPC PlayMode `4/4`; importer and NPC EditMode assemblies also compile directly with Unity's Roslyn response files |

The review routes and rescue-state API establish an integration boundary; they
do not claim the donor crash/event graph, damage behavior or BetterMSC logic.

## Phase 1 base-map Terrain migration tooling (2026-08-03)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Canonical map inventory and Blender interchange | Sanitized `World_Global_Legacy` plus 50 legacy cells; donor provenance components | Source `TemporaryDirectImport`; scanner/exporter `Reimplemented` | 2,606 instances inventoried and exported as OBJ/MTL with transforms, GUID/file IDs, materials, bounds, collider/activity state and source fingerprint; no FBX claim |
| Base-ground conversion | Seven classified ground renderers | Conversion `Reimplemented`; residual presentation `TemporaryDirectImport` | Separate 9 x 8 Terrain scene, 513 samples/tile, 2 m spacing; 2,528 unsupported triangles preserved in five replaceable residual meshes |
| Road compatibility | Six road-bed and nine road-structure renderers | Existing road presentation preserved; constraints `Reimplemented` | Roads/transforms/materials/colliders unchanged; zero Terrain protrusion and bridge-burial failures; 2.205 m shoulder warning remains manual-review debt |
| Material/visual QA | Three source ground textures and generated validation reports | Safe placeholder `Reimplemented`; no production-art promotion | Matte HDRP TerrainLayers, three side-by-side elevation/difference PNGs; exact source UV/material identity intentionally not claimed |

## Milestone 11B-R1 story-traffic road-network slice

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Route network | Locked `GAME.unity` traffic route transforms and canonical scene hash | `WorldLayoutReference`; `ConfigurationTransferred`; reader `Reimplemented` | Exact counts/contiguous indices enforced for Highway, BusRoute, DirtRoad, six local/race routes, two boat routes and two train directions |
| Jani/Petteri road presence | Locked formation 61904, exact car transforms 43020/48255, Village/RoadRace/Highway routes and AMIS setup time evidence | `BehavioralReference`; positions/route joins `ConfigurationTransferred`; schedule/route state project-owned | Existing stable drivers/cars spawn in exact Perajarvi formation at 16:00, follow a once-only reverse Village -> RoadRace departure, then enter the same reverse Highway loop at anchor 464 through measured sub-metre joins |
| Vehicle motion | Donor speed/throttle/shifting/steering/passing FSM and physics configuration inspected; no donor controller transferred | `Reimplemented` R1 boundary; richer behavior pending | Bounded acceleration, braking, steering, interpolation, obstacle response, road-normal grounding and collision event; route backlog no longer causes obstacle-bypass teleports. Donor 115–185 km/h choice, passing/lane/drift/recovery remain R2 |
| Audio | Existing project `IAudioBackend` and vehicle audio parameter contract | `Reimplemented`; media mapping deferred | Four-gear RPM/load parameters replace flat speed-derived RPM; streaming does not post the starter one-shot. Stable per-car music hooks exist, but persistent music ownership and donor clip parity are not claimed |
| Save/streaming | Existing `npc.state`, stable IDs and cell availability | Compatible extension | Route progress remains save authority and wrappers can rematerialize; no schema migration; full ambient `traffic.state` remains open |

Focused validation passes NPC EditMode 17/17 and PlayMode 6/6. Manual road,
grounding, audio and accepted-Suski regression acceptance is pending, so no 11B
parity row is promoted to `Verified`.

### 11B-R2A maneuver extension

The R2A extension adds donor-range cruise speeds (115–185 km/h), project-owned
brake/pass/return/reverse states, adjacent-lane clearance, physical-cell
presentation residency and matching four-gear audio parameters. Automated NPC
validation remains EditMode 17/17 and PlayMode 6/6. Tire-force drift,
handbrake/burnout, crash/recovery, persistent in-car music and the story graph
remain pending; this row is not `Verified`.

The pipeline passed 21/21 focused EditMode tests and independent saved-scene
validation. It introduces no save/UI/stable-ID migration and does not change
the accepted 00–08A runtime contracts.

### 11B-R2B incident, recovery and persistent-audio extension

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Perajarvi formation departure | Locked 61904 parent, KYLAJANI 43020, AMIS2 48255 and Village/RoadRace/Highway route joins | Positions/joins `ConfigurationTransferred`; schedule composition `Reimplemented` | Exact 16:00 formation replaces mid-Highway substitutes; once-only departure hands off at Highway 464 without a cross-country chord or save-ID change |
| Contact/crash | Locked Jani `CollisionEvent -> CRASH`, `DeathSpeedMPS=5` | `BehavioralReference`; threshold `ConfigurationTransferred`; state machine `Reimplemented` | Obstacle probes no longer fabricate contact; actual typed incidents cross the exact 5 m/s threshold |
| Drift/recovery | Separate donor handbrake and high-speed behavior evidence | `BehavioralReference`; bounded response `Reimplemented` | High-speed corner slip is clamped; crash hold returns toward a retained safe road pose without teleporting |
| Streaming | Existing `npc.state`, physical-cell residency and project stable IDs | Compatible runtime extension | Speed/maneuver/safe pose plus engine/music ownership survive wrapper removal during the active session; no save schema change |
| Temporary audio | Five hash-locked donor media files and inspected source radii | Payload `TemporaryDirectImport`; importer/event ownership `Reimplemented` | Seven stable Unity fallback events; engine/music/skid/crash are spatialized to 65/55/45/80 m; emitter-scoped RPM drives fallback engine pitch |
| Wwise boundary | Existing `IAudioBackend`, vehicle RTPCs and Wwise-ready emitter contract | Project-owned stable IDs; bank authoring pending | Runtime code is backend-neutral, but dedicated R2B Wwise events and SoundBank media are not falsely claimed as authored |

Focused validation passes NPC EditMode 17/17, NPC PlayMode 8/8 and Unity
fallback PlayMode 8/8. Manual combined road/audio/story review remains pending,
so Jani/Petteri whole-feature parity is still not `Verified`.

### 2026-08-05 11B-R4 physical story-traffic replacement

R4 supersedes the R1/R2 transform-driven loaded-car implementation for Jani
and Petteri. Licensed NWH Vehicle Physics 2 v13.5 now supplies only the four
wheel/contact units. Project-owned engine, clutch, gearbox, differential and
driver code apply throttle, front/rear service brakes, handbrake and steering
to dynamic Rigidbody chassis. The route is a look-ahead reference; no ordinary
loaded-driving path writes a car pose.

The route begins at the locked Perajarvi formation, follows the measured
Village -> RoadRace -> Highway joins, completes the Highway loop and returns
through RoadRace/Village in the same direction for both cars. The inspected
donor `115-185 km/h` range and GlobalDay 3/5/6/7 `16:00-02:00` activation gate
are transferred. The clock activates the encounter but is not loaded-position
authority. While materialized, route progress is projected from the actual
chassis; offscreen coarse motion exists only for streaming continuity.

Project-owned maneuver states cover lane variation, obstacle braking, a
clear-lane pass, lane return, blocked-lane reverse escape, bounded corner drift,
typed collision and non-teleporting recovery. A deterministic Teimo-area social
stop is explicitly an approximation until its exact donor trigger is captured.
Durable damage, rivalry/story consequences and final Wwise authoring remain
open, so the vehicle and traffic rows stay `PartiallyImplemented`.

Executed evidence: NWH EditMode `4/4`, generated physical-car PlayMode `4/4`
(including a 3 m road-block pass with a `0.310 m` maximum observed Rigidbody
step), NPC EditMode `18/18`, NPC PlayMode `8/8` and shared vehicle-simulation
EditMode `29/29`. Full-loop subjective handling/audio acceptance is pending.

### 2026-08-05 11B-R3 exact routes and durable story incidents

This section supersedes the R2/R4 Highway route claim while retaining the
physical NWH/Rigidbody implementation.

| Area | Donor evidence | Transfer | Current project state |
|---|---|---|---|
| Race route | Navigation `WaypointStart=228`, TrackField/Village/RoadRace owners and eight TrackField circuits | Route transforms `ConfigurationTransferred`; composition/control tuning `Reimplemented` | Driver-specific 2,088-point route: TrackField 228..290, Village 0..294, RoadRace 0..623, TrackField 0..201, then seven TrackField 73..201 circuits; no Highway splice; dense donor samples evaluate linearly, Teimo handbrake zones use `7..10 m` look-ahead, and braking begins about `245 m` before RoadRace exit |
| Dancehall route | Separate Dancehall 0..268 branch | Route transforms `ConfigurationTransferred`; branch selection `Reimplemented` | Separate 537-point out-and-reverse cycle for each driver |
| Trigger | Current-vehicle Satsuma proximity and race event | `BehavioralReference`; temporary trigger `Reimplemented` | 16:04 or 35 m/two-second proximity substitute is explicit debt until Satsuma rev authority exists |
| Terminal crash | Jani/Petteri CrashEvent; `DeathSpeedMPS=5` | Threshold/configuration transferred; incident logic `Reimplemented` | Typed >=5 m/s incident, terminal full-brake hold and stable wreck transform across streaming/save |
| Suski rescue | Knocked-out passenger after Jani crash and parents' bed destination | `BehavioralReference`; carry/sleep state machine `Reimplemented` | One stable Suski identity progresses through passenger, pickup, transport, bed and rescued states; no duplicate passenger |
| Save | Existing v13 `npc.state` plus stable character IDs | Compatible additive `Reimplemented` domain | Optional `traffic.state` schema 1 depends on core time/NPC, preserving incidents and rescue pose/stage without a v13 migration |

The physical route and incident integration is `PartiallyImplemented` until the
manual long-loop/rescue test passes. T1/T2 now implement ambient traffic, bus,
train and boats, but their comprehensive manual acceptance remains open.

### 2026-08-06 11B-T1 road graph and ambient traffic

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Road catalog | Eight locked route owners and 7,715 contiguous transforms | `WorldLayoutReference`; `ConfigurationTransferred`; catalog/evaluator `Reimplemented` | Exact linear route records plus 20 directional joins; no donor spline/controller runtime |
| Highway population | Exact ten direct children of `VehiclesHighway` | Multiplicity/transforms `ConfigurationTransferred`; identities/simulation `Reimplemented` | 3 Victro, 2 Lamore, and 1 each Truck/Polsa/Fittan/Svoboda/Menace with stable project IDs |
| Presentation | Selected locked meshes/materials/wheels | Sanitized `TemporaryDirectImport` behind project wrappers | Seven replaceable NWH/Rigidbody archetypes; donor components and scripts excluded |
| Residency | Existing streaming/player boundaries plus explicit user story-risk decision | Project-owned `Reimplemented` | Selected-root ordinary actors advance logically in real time offscreen; retained-first `440 m` materialize / `520 m` remove / maximum six physical actors. Pena and the separate Saturday context are outside the ordinary cap. Offscreen ambient collisions are not simulated; game-clock jumps do not advance route distance. |
| Save | Existing optional `traffic.state` schema 1 | Compatible additive `Reimplemented` state | Actor spawn, route progress, circuits and deterministic spawn attempt round-trip; old saves remain valid |
| Audio | Existing `IAudioBackend` and spatial emitter contract | Temporary project fallback | Unique emitters use the Petteri profile temporarily; per-model Wwise media remains open |

Unity batch build passed with 8 routes, 7,715 points, 20 joins, 10 actors and
7 physical wrappers. Traffic EditMode `5/5`, Save Integration EditMode `28/28`
and full NPC PlayMode `10/10` pass. Manual comprehensive density, collision,
long-traversal and listening acceptance remains pending, so the affected rows
remain `PartiallyImplemented` rather than `Verified`.

### 2026-08-11 local-light interaction and mobile-source correction

| Area | Donor/project evidence | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Home exterior | `switch_garage` -> donor `Garage` group; `HouseLightSwitchGarage`; static `outdoor_lamp` mesh with no Light/FSM/sensor | Configuration transferred; electrical/visual behavior `Reimplemented` | Neutral-white `HomeExterior` profile on the existing garage switch, home fuse, metering and save path; donor body stays white when off and the generated diffuser sits on its outward surface |
| Garage | Existing accepted fluorescent placement; no baked interior bounce in temporary shell | Project calibration | Garage fluorescent compensation raised to 3.0x; exterior load excluded from this multiplier |
| Teimo shop/pub | Donor fluorescent luminaires; user-confirmed five-shop/one-pub split and Teimo location authority | Configuration transferred; photometry/presentation calibrated | Shop 5200 lm per fixture, pub 3500 lm; both use 4200 K room light and pure-white emitter presentation |
| Street/exterior emission | Donor pole/body renderer plus fixture-head placement | Presentation `Reimplemented` | Spot sources own a generated emissive head lens; the complete donor pole/body is never emissive |
| Switch acquisition | Eight accepted donor rocker stable IDs and existing interaction ray | Donor motion retained; interaction envelope `Reimplemented` | Existing/tiny colliders are normalized to 0.28 x 0.34 x 0.16 m without expanding global reach |
| Flashlight | Donor hard-shadow spot plus `FlashlightCharge`, batteries, local X +90-degree Light transform and `headlight_glass` lens | Photometry/configuration transferred; adapter `Reimplemented` | Stable item on/charge state drives one bounded 300 lm / 20 m HDRP spot from item-local `-Y`; the battery-cover end is excluded from emitter selection |
| Traffic headlights | Existing story-traffic wrapper and shared vehicle-light profile | Project-owned presentation `Reimplemented` | Two dusk-gated 6500 cd / 35 m low beams per active wrapper; HDRP volumetric dimmer is 0.015 and maximum VLB weather density is 0.025 |
| Streamed/editor visibility | Existing probe-runtime lifecycle plus user Scene View captures at remote cells | Project runtime repair; Editor-only authoring aid | Existing fixtures republish idempotent registration every second; `UNITY_EDITOR` budgets against the nearer player/Scene View camera while shipping builds remain player-centred |
| Headlamp fascia correction | Locked Jani/Petteri `BeamShortAILeft/Right` local transforms; ambient four-wheel bindings | `ConfigurationTransferred;Reimplemented` | Jani and Petteri use exact donor-local headlamp positions and three-degree pitch; other generated traffic derives a stable fascia from FL/FR/RL/RR anchors. Both HDRP Lights and emission lenses share the result; renderer bounds are excluded |
| Home colour correction | User night-interior comparison against accepted domestic profiles | Project presentation calibration | Domestic bulbs are 3300 K and enclosed ceiling fittings are 3500 K; circuits, switches, fuses, metering and save behavior are unchanged |
| Scene View budget correction | Unity Editor retained Scene View cameras; user remote-location capture | Editor-only project repair | Budget distance accepts `CameraType.SceneView` even when `Camera.enabled` is false; no shipping/runtime streaming authority changes |

Focused automation passes Lighting EditMode `33/33` and the streamed lighting
PlayMode lifecycle `1/1` in `57.14 s`. Manual night driving, held-flashlight aim,
switch feel and GPU acceptance remain open; no row is promoted to `Verified`.

### 2026-08-11 local lighting system

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Accepted world fixtures | 46 donor-evidenced catalog rows plus 3 project refrigerator fills and recovered donor hierarchy paths | `ConfigurationTransferred;BehavioralReference;Reimplemented` | 49 stable project electrical/profile/zone bindings across 11 streamed cells; legacy visual scenes unchanged |
| Candidate audit | 70 scenes, 168 prefabs, mesh/material/emissive/Light/switch evidence; directional/environment lights excluded | Project Editor tooling | 297 candidates; 3 safe WeatherLab direct bindings; 294 explicit ManualReview |
| Home power | Eight extracted donor switch-button transforms | `WorldLayoutReference;Reimplemented` | Project-owned interaction targets and stable switch IDs; save schema 1 |
| Teimo/Fleetari | Existing ServiceRuntime schedule plus NPC staffing authority | `Reimplemented` adapter | Shop/workshop lighting follows real availability with bounded shutdown grace |
| Teimo pub lighting | User-confirmed 5 shop / 1 pub split in the shared donor lamp bank | `ConfigurationTransferred;Reimplemented` | One stable eastern fixture follows pub staffing independently; five remain shop-owned |
| Vehicle lights | Existing project/NWH light states and audited prefab candidates | `PartiallyImplemented` | Public-state/battery adapter exists; incomplete generated vehicle fixture geometry remains ManualReview |
| Volumetric beams | User-supplied Tech Salad VLB 2.2.3 | Licensed third-party, project adapter | Cached HD/SD public-API boundary; vendor source unchanged |
| Verification | Unity 6000.3.11f1 focused runs | Automated partial pass | Lighting EditMode 33/33 and D3D11 streamed Bootstrap PlayMode 1/1 pass; manual image and GPU/GC profiling remain open |

### 2026-08-06 11B-T2 public transport and route traffic

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Bus | Locked 1,084-point route, three stop distances, startup phase and `BUS` transform 65181 | Layout/configuration transferred; timing, state and control `Reimplemented` | Permanently physical bus by approved story-risk policy; NWH road driver uses `60 km/h` minimum, `82.5 km/h` target/maximum, zero generic wander/drift, stop dwell and board/exit target |
| Train | Locked east/west endpoints, `30 m/s`, `250` simulation-second delay and `TRAIN` transform 55611 | Layout/configuration transferred; state/presenter `Reimplemented` | Bidirectional persistent actor with endpoint waits and project collider |
| Boats | Two locked eight-point loops and exact `AIboat1`/`AIboat2` roots | Layout/configuration transferred; physics/state `Reimplemented` | Independent stable actors; logical offscreen progress and Rigidbody force/turn/water-height control near player |
| Presentation | Donor renderers/materials for four actor roots | Sanitized `TemporaryDirectImport` | Four project wrapper prefabs; donor controllers, FSMs, physics and runtime hierarchy excluded |
| Save/streaming | Existing optional `traffic.state` schema 1 | Compatible additive `Reimplemented` payload | Optional `transportActors`; older empty payloads initialize fresh state without document migration |
| Audio | Existing `IAudioBackend` boundary | Temporary project fallback | Unique spatial owners; dedicated bus/train/boat Wwise events remain open |

The complete catalog now contains 13 routes, 8,819 points, 20 road joins, ten
ambient actors and four route transports. Traffic EditMode `6/6`, Save
Integration `28/28` and production NPC PlayMode `10/10` pass. Ticketing/door
behavior, train hazard consequences, exact bus/boat calibration, dedicated
audio and representative long manual traversal remain open, so the affected
rows stay `PartiallyImplemented`.

### 2026-08-08 traffic residency and route-control correction

The working-tree runtime now applies a hybrid policy. Jani, Petteri, the bus
and Pena remain physical regardless of player distance so an offscreen race or
story-relevant crash cannot be removed by streaming. Ordinary selected-root
traffic advances only its logical route state in real time outside a retained-
first six-wrapper `440/520 m` residency band; offscreen collisions are not
simulated. The Saturday Pena context remains separately owned and mutually
exclusive with ordinary Pena.

Jani/Petteri's dense 2,088-point donor route is linear, the four Teimo
handbrake zones use a short `7..10 m` look-ahead, and a project-owned braking
cap begins approximately `245 m` before RoadRace exit. Bus control is bounded
to `60..82.5 km/h` with generic wander/drift disabled. This correction is
`Implemented`; automated validation passed (`79/79` combined EditMode, `4/4`
focused physical story PlayMode and `1/1` long traffic/bus PlayMode). Manual
full-route acceptance remains pending, so no traffic row is promoted to
`Verified`.

### 2026-08-06 12A-E1 economy foundation

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Fresh balance | Locked `PlayMakerGlobals.asset`, `PlayerMoney = 3000` | Value `ConfigurationTransferred`; wallet `Reimplemented` | Fixed-point 300000 minor-unit balance |
| Store prices | Locked `GAME.unity` Teimo `Prices` table | 38 values `ConfigurationTransferred`; catalog/lookup `Reimplemented` | Stable price IDs, exact base prices and hash-gated builder |
| Price change | `Inflationrate=0.054`, `PriceMultiplier=1`, `RestockDay=4`, Thursday FSM comparison | Rule `BehavioralReference`; time integration `Reimplemented` | Additive 5.4% Thursday cycles, including skipped days |
| Transactions | Purchase/refund/payment behavior categories | `Reimplemented` project authority | Atomic ledger, idempotency, insufficient funds and bounded refunds |
| Save/UI | Existing native save and accepted 08A HUD | Compatible extension | Required `economy.player` schema 1, document v14 migration and live money provider |

Automated validation passes Economy EditMode `4/4`, Save Integration `29/29`,
focused money UI PlayMode `1/1`, production Bootstrap lifecycle `1/1` and full
UI PlayMode `19/19`. Store/service interaction remains owned by 12A-S1, so the
economy rows remain `PartiallyImplemented` pending manual acceptance and their
consumer integrations.

### 2026-08-08 hybrid Enviro / native HDRP environment migration

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Time, calendar, weather, wetness, lightning and saves | Accepted project implementation through 08A | Protected project authority; `Reimplemented` | Existing `ProductionEnvironmentController` retained; normalized read-only state adapter added |
| Sky/cloud/precipitation presentation | Installed Enviro 3 API and current Bootstrap | Enviro presentation ownership | One Enviro Manager and sky-only volume; Enviro native HDRP Fog/Exposure writers disabled |
| Native Fog/Exposure/indirect lighting | HDRP 17.3 volume API and audited mixed profile | Project-owned `Reimplemented` writer | Separate native profile and one `NativeHdrpWeatherBridge`; state blending and local fog voids |
| Interior/shelter exposure | Measured project bounds plus donor interior manifest | `WorldLayoutReference;BehavioralReference;Reimplemented` | Independent smoothed factors and explicit zones/portals; house/garage only are authored |
| Weather audio | Existing `IAudioBackend`, Wwise weather bank/events | Project-owned event/parameter identity; Wwise rendering | Persistent hybrid rain event and independent RTPC mapping; Wwise authoring/banks still pending |
| KWS/water | Repository/package audit | Unchanged / out of scope | No KWS reference or water change; future adapter boundary documented |

Focused automation passes 16/16 EditMode and 2/2 lifecycle PlayMode, with zero
static ownership/zone validation errors and an idempotent Bootstrap migration.
All-building zones, real portals, vehicle cabins, Wwise production authoring,
visual calibration and performance capture remain pending; no Phase 1 parity row
should be promoted to `Verified` from this architectural pass alone.

### 2026-08-09 hybrid exposure and precipitation performance correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Day/night exposure | User captures at 16:17 and 03:27; accepted project clock and Enviro light owner | Project-owned `Reimplemented` | Camera-independent Fixed Exposure follows `7.25 EV` night / `12.5 EV` day with smooth 03:00-07:00 and 19:30-23:00 ramps |
| Rain visual density | Existing Enviro Rain prefab and weather intensity overrides | Enviro presentation through isolated runtime clone | Sixfold scale retained up to `8,000 particles/s`; live drops capped at `8,000`; accepted proportional drop/streak sizing retained |
| Surface splashes | Existing collision sub-emitter | Project runtime policy `Reimplemented` | `512` live splash cap, `20%` impact sample, Low-quality static world collision, `128` shapes and a bounded project world-layer mask |
| Verification | Unity 6000.3.11f1 focused runs | Automated passed; manual GPU pending | EditMode `28/28`, PlayMode `2/2`; matched clear/rain/storm GPU capture and visual acceptance remain open |

### 2026-08-11 12A-S1 service interaction correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Store | 41 exact product/cover groups and audited register | Layout/configuration transferred; bounds, stock presentation, basket, preflight and checkout `Reimplemented` | Full shelf-group colliders and live visual unit depletion; 80 reviewed handoff wrappers cover food/automotive goods and 13 paint variants; three vehicle-cover effects fail closed |
| Pub | Five exact buttons, fridge/microwave stable entities, donor hand sockets, `DrinkSpawnPoint`/`FoodSpawnPoint`, meal mesh, Register/Data FSM evidence and Teimo action/root clips | Layout/clip/item presentation `TemporaryDirectImport`; transforms/timings `ConfigurationTransferred`; transaction and project-owned choreography `Reimplemented` | Under-counter props appear at donor `2.3 s` and fulfill at `5.3 s`; food follows exact lean/root-walk/cook/door/55..70 s heat/carry/return stages and fulfills `0.4 s` into bring-food; exact spawn poses are treated as object pivots with no added surface lift or collider settlement |
| Fuel | Three pump anchors and authored canister liquid/cap state | Configuration transferred; nozzle, hose and accounting boundary `Reimplemented` | Stable physical nozzles fill only compatible open containers; accepted volume is recorded after receiver mutation |
| Fleetari | Audited six-page brochure, order page and 32 catalog offers | Page art `TemporaryDirectImport`; hit maps/configuration transferred; physical book and selection `Reimplemented` | World brochure opens under a pinned overhead camera, pages turn physically and visible checkboxes drive the save-backed selection/exclusive groups/final-drive variants; paid vehicle outcome remains fail-closed |
| Home catalog | Audited eight-page magazine hierarchy, order form, 46 hit regions and package prices | Page art `TemporaryDirectImport`; page/hit layout transferred; physical book/form `Reimplemented` | World object opens as a book, the camera pins above it, pages turn and visible product regions update the right-hand form/total; persistence, payment, post and delivery remain pending |
| Teimo | Counter-lean/register/anger/kitchen/movement evidence | Sanitized clip presentation plus project action IDs and schedule authority | Default state clamps the donor lean; checkout holds it; provocation latches anger until the next action/state; kitchen travel samples exact donor root curves while separate body loops animate the legs; store-to-pub uses four `AuthoredHeight` donor keys over 57 game seconds |

The catalog builder and complete NPC builder pass. The latest exact-donor
choreography runs pass Services EditMode `49/49`, NPC EditMode `34/34`, and
focused Teimo clip/timing/root-motion PlayMode `1/1`. Earlier focused Services
EditMode passes `49/49`; NPC EditMode passes `34/34`; Items EditMode passes `32/32`;
the item builder validates `80/80` bindings; Fluid Presentation EditMode passes
`6/6`; Save Integration passes `37/37`; focused character PlayMode passes
`1/1`. Manual physical alignment,
interruption, streaming and audio acceptance remains open, so all affected rows
remain `PartiallyImplemented` rather than `Verified`.

The 2026-08-12 pub spawn-pivot regression passes Services EditMode `49/49` and
Items EditMode `36/36`. Manual in-world height acceptance remains open.

### 2026-08-13 secondary item-interaction feel reference

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Physical grab point | Cheap Car Repair `GrabbingHandler` IL; existing project interaction candidate hit | `BehavioralReference;Reimplemented` | Existing `PhysicalCarryController` retains project authority and follows the exact clicked surface point |
| Follow response | Reference defaults spring `90`, retention `0.8`; user reports current project sluggish and later reports run-induced trailing | `ConfigurationTransferred;Reimplemented` | Project-tuned spring `110`, retention `0.82`, `18 m/s` relative cap, lower temporary damping and `96%` owner-motion inheritance capped at `12 m/s`; manual feel acceptance pending |
| Distance and rotation | User reports items too far and requests initial-distance retention; reference `6°` wheel and free mouse rotation | Distance project-authored; rotation `ConfigurationTransferred;Reimplemented` | Initial selected-point distance retained in `0.32–0.82 m`; wheel `6°`; middle-mouse free rotation freezes camera look |
| Ownership/scope | Existing stable IDs, capability registry, physics restore, save and handoff | Protected project authority | No My Summer Car parity row or Phase 1 scope changed |

### 2026-08-13 player voice reaction pass

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Manual swear / finger | Locked Setup `&108669`, Speech `&110839`, PlayerFunctions `&111301`; accepted middle-finger viewmodel | Input values `ConfigurationTransferred`; policy `Reimplemented`; existing viewmodel protected | `N` selects the 16-way Swearing catalog; `M` drives the existing gesture and selects the separate 11-way Fuck catalog |
| Stress reaction | Speech stress delta `-0.5`; Simulation `&112251` threshold `100`; project range `0..100` | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Manual/automatic swears apply `-0.5` through `IPlayerNeedsEffectSink`; automatic reaction rearms after stress drops |
| Insufficient funds | Locked rejection branches reference `Swearing`; project atomic transaction receipt | `BehavioralReference;Reimplemented` | Optional rejection event; player reacts only to `InsufficientFunds`; wallet, ledger and needs remain unchanged |
| Voice presentation | MasterAudio `Swearing` group `&105622` (16) and `Fuck` group `&105307` (11), with Finger subtitle table `&107451`; 27 hash-locked PCM resources | `TemporaryDirectImport` behind separate project event IDs; subtitle text `ConfigurationTransferred` | Removable 2D Unity fallback supplement maps `player.swear.01..16` and `player.finger.01..11`; Wwise production mapping and manual mix acceptance pending |
| Controls UI | Accepted 08A shell and existing GUID-based override persistence | Protected project presentation; compatible extension | Fixed panel remains unchanged; binding rows scroll and expose active player/vehicle defaults including `Swear` |

The previous Swearing-only baseline passed EditMode `10/10`, controller PlayMode
`2/2`, Controls PlayMode `5/5` and production Bootstrap voice/library PlayMode
`1/1` (`44.03 s`). The Finger correction passes the exact ordered donor
group/subtitle evidence check, 27-source hash/PCM audit, CSV/JSON validation,
zero-error production/importer compilation and standalone policy tests `5/5`.
The corrected 27-clip importer and focused Unity rerun
remain pending while the interactive Editor is in Play Mode. Affected rows stay
`PartiallyImplemented` until that rerun and manual audio/gesture/checkout/stress
acceptance are complete.

### 2026-08-13 Satsuma V1a physical baseline

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Body shell and pose | Locked `GAME.unity` root `&64200`, 8 enabled shell renderers | `TemporaryDirectImport;ConfigurationTransferred` behind project wrapper | `vehicle.satsuma` spawns at the locked home pose; full loose-part presentation remains V1b |
| Collision | 23 donor component hull candidates plus monolithic `CarCollider` | `CollisionReference;TemporaryDirectImport` | 22 component convex colliders retained; Unity-6-truncated monolithic duplicate rejected |
| Runtime/save | Existing M05/M06 and native vehicle aggregate | Protected project authority | Stable ID, assembly root, simulation host and schema-1 persistence are connected before save registration |
| Mail-order parts | Existing 46 `item.mail-order.*` IDs | `Reimplemented` compatibility adapter | All packages resolve once to Satsuma part IDs or kit members; world-item-to-PartInstance expansion waits for V1b roster |

The deterministic builder passed with `8/22/46`; focused EditMode passed `7/7`
and production Bootstrap PlayMode passed `1/1` (`9.46 s`).
Manual material, garage alignment, collision and new-save/load acceptance remains
open, so P1.CAR rows remain `PartiallyImplemented`, not `Verified`.

### 2026-08-13 Satsuma V1b complete direct CARPARTS roster

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Logical roster | Locked direct roots below `CARPARTS/PartsCar`, `PartsMotor`, `PartsGT`, `PartsExtra` | Donor hierarchy is build-time `BehavioralReference`; runtime IDs are project-owned | 125 logical roots: 75 car, 39 motor, 6 GT, 5 extra; `WoodSheet` fixture excluded |
| Initial state | Frozen GameObject activation | `ConfigurationTransferred` with unresolved startup-FSM caveat | 78 donor-active parts materialize; 47 variants remain registered inactive until behavioral audit |
| Physical/pickup | Donor Rigidbody/collider records and renderer-relative transforms | `TemporaryDirectImport;CollisionReference;Reimplemented` | Every logical root has presentation, dynamic Rigidbody, collision/fallback bounds, explicit pickup capability and stable ID |
| Save isolation | Existing M05 schema-1 assembly and native vehicle aggregate | Protected project authority | Body + 125 parts registered; loose root detaches as a composition sibling so parts never follow chassis |

The V1b builder passes `8/22/125/78/46`; focused EditMode passes `8/8` and
the production Bootstrap PlayMode passes `1/1` (`6.91 s`). Mount points,
fasteners, fluids, wiring, drive authority and manual garage/save acceptance
remain open, so affected parity rows are still `PartiallyImplemented`.

### 2026-08-13 Satsuma V1c verified mount subset

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Mount poses | Locked installed Satsuma hierarchy plus loose/installed mesh identity and Assembly-parent pivots | `PivotSource;BehavioralReference;ConfigurationTransferred` | 46 mounts are authored: the earlier exact-name/rear-drum subset plus 15 fixed Assembly parents and exact parent pivots for the bootlid and both doors; ambiguous matches stay audit-only |
| Assembly FSM inventory | 102 serialized `fsm.name = Assembly` records | `BehavioralReference` only | Trigger, held-part, installed-object, prerequisite, detach and bolt references are parsed to a tracked audit; donor FSMs/scripts never enter runtime |
| Suspension order | Assembly prerequisite records for subframe, wishbones, spindles, front struts and steering | `ConfigurationTransferred;Reimplemented` | 20 project-owned directed install/removal dependencies guard the first mount subset |
| Fasteners/tools | 85 locked-scene `BoltPM` markers and serialized `Screw` FSM stages | `PivotSource;ConfigurationTransferred;Reimplemented` | 57 markers on verified mounts are live with exact local pose, 0..8 stages and wrench sizes 5/6/7/8/9/10/12/14; 28 unresolved-mount markers remain audit-only |
| Hinged panels | Assembly parent pivots plus loose-part `Use` FSM limit/torque evidence | `PivotSource;ConfigurationTransferred;Reimplemented` | Bootlid and both doors open on their exact local axes/ranges; each carries four fastener targets, and schema-1 installed-part rotation restores the open angle without donor joint/FSM runtime |
| New Game paint | Existing project-owned ten-colour main-menu selector | `Reimplemented` | Fresh Satsuma receives selected colour before activation via per-vehicle property blocks; optional vehicle-save payload restores it and does not repaint legacy saves |
| Save | Existing schema-1 assembly DTO plus optional paint extension | Protected project authority | Round-trip covers body + 125 parts, 46 mounts, 57 fasteners and installed hinge angle; Satsuma EditMode 10/10, spanner/catalog 8/8, Bootstrap PlayMode 1/1 and colour PlayMode 1/1 pass |

Twenty-eight unresolved fastener markers, remaining mount alternatives and the full
engine/body graph remain open; the vehicle remains fail-closed and kinematic.

### 2026-08-14 Satsuma V1d loose-part and engine assembly graph

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Loose-part mounts | 76 serialized `Assembly` FSM records below donor `CARPARTS` loose roots | `BehavioralReference;PivotSource;ConfigurationTransferred;Reimplemented` | 37 project-owned mounts follow their loose owner; engine internals may be assembled before the block enters the body |
| Engine/body pose | Three donor chassis motor triggers paired to three engine-block trigger points | `PivotSource;ConfigurationTransferred;Reimplemented` | One rigid engine mount is triangulated with 3.92 mm maximum residual and secured by three 11 mm fasteners |
| Assembly order | `db_PartRequired1` and `DetachPart` values plus the established suspension evidence | `BehavioralReference;ConfigurationTransferred;Reimplemented` | 34 directed edges enforce suspension order and the evidenced crankshaft/piston, timing-cover/chain, flywheel/clutch and dashboard/meter relationships |
| Player handoff | Project interaction candidate and carry boundaries | `Reimplemented` | All 126 parts expose owner-surface handoff; a visible body/part collider forwards a carried compatible part to the nearest valid owned mount without name lookup |
| Save migration | Existing schema-1 aggregate | Protected project authority | Exact former `46/57` and intermediate `83/167` Satsuma shapes migrate additively to 84 mounts/170 fasteners; malformed or unknown shapes remain rejected |
| New Game paint | Existing ten-colour selector and optional vehicle paint DTO | Protected project authority | Selected colour applies before fresh vehicle activation and restores per vehicle; older saves without paint preserve authored colour |

Current generated counts are body `1` + loose parts `125`, mounts `84`, live
fasteners `170`, loose-part-owned mounts `37`, hinges `3`, dependency edges `34`
and wrench sizes `5..14`. Builder, generated-content EditMode `14/14`, assembly
EditMode `22/22`, vehicle-assembly PlayMode `9/9` and New Game paint PlayMode
`1/1` pass. Fluids, wiring, tuning, wear, startup variants and drive authority
remain open; this is not complete Satsuma parity.

### 2026-08-14 Satsuma V1d.2 assembly handoff and physical toolbox

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Front mudflaps | Two donor Assembly records with `ActivateThis` but no `Parent`, plus nearest sibling `BoltPM` | `BehavioralReference;PivotSource;ConfigurationTransferred;Reimplemented` | Exact left/right mounts follow their fenders; one donor bolt is retained per mount |
| Install presentation | User-owned Cheap Car Repair behavior reference plus existing project handoff | `BehavioralReference;Reimplemented` | A valid handoff glides for 0.34 s into the exact pose; strict order/compatibility commit remains authoritative |
| Spanner case | Donor item hierarchy, lid pivot/open quaternion, clip durations and wrench meshes 5..15 | `TemporaryDirectImport;BehavioralReference;ConfigurationTransferred;Reimplemented` | F opens/closes the physical case and LMB takes an individual wrench; the case itself has no magic tool identity |
| Save/streaming | Existing vehicle schema 1 and `items.instances.native.v1` | Protected project authority | Vehicle aggregate is 126 parts / 116 mounts / 205 fasteners; case open state survives dynamic restore and cell rematerialization |

Deterministic generation passed with 116 mounts, 205 fasteners, 46 owned mounts
and 34 dependency edges. Focused validation passed: Satsuma EditMode 16/16,
vehicle assembly PlayMode 8/8, production bootstrap PlayMode 1/1 and spanner
definition/open/save/stream EditMode 5/5. Manual in-game feel and presentation
acceptance are still pending. The complete item EditMode regression passed
45/45.

### 2026-08-14 Satsuma V1d.3 physical chassis and assembly interaction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Chassis/support | Locked `SATSUMA(557kg, 248)` named root plus serialized Rigidbody mass `389` and existing project raycast-wheel backend | Mass `ConfigurationTransferred`; dynamics `Reimplemented` | The 389 kg shell is gravity-driven and non-kinematic; installed-part masses update it; each wheel contact requires its authored suspension, spring alternative and wheel mounts |
| Handoff targeting | Exact mount ownership plus player ray hit | Project-owned `Reimplemented` | Direct spheres are `0.03..0.075 m`; owner-surface handoff requires the aimed owner and stays within `0.16 m` of the aim ray; no global fallback can install a steering rod through the hood |
| Assembly order | Existing donor Assembly evidence and user-observed invalid sequences | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Steering rods require rack + matching spindle; rear arm precedes spring, spring precedes shock, normal/long spring alternatives exclude each other; graph now has 36 edges |
| Hinged panels | Exact V1c pivots/limits/fasteners and old-Unity donor break torque evidence | `PivotSource;ConfigurationTransferred;Reimplemented` | Current superseding contract uses real held mouse torque, retained Rigidbody inertia and a one-degree physical endpoint latch; donor break values remain audited authoring evidence while the Unity 6 joint cannot self-destruct from an ordinary chassis impulse; an entirely unfastened panel detaches only at full opening into normal Rigidbody pickup state |
| Wrench action | Hash-locked local BetterMSC `ToolHand`/`ScrewBolt` IL plus user donor comparison inspected read-only | `BehavioralReference;ConfigurationTransferred;Reimplemented` | LMB immediately selects a non-physical wrench mode while the real renderer eases `0.28 s` from its case slot to a partially size-compensated camera pose; authored mesh scale remains physical and visibly different across `5..15 mm`, the handle exits the lower viewport, a 22-degree longitudinal twist reveals thickness, fastener-only raycast snaps immediately to the retained bolt pose, and wheel-up/down tightens/loosens through a 60-degree eased work arc; no BetterMSC DLL/type executes at runtime |
| Bolt feedback | Existing project interaction candidate and EPO outline adapter | Protected outline presentation plus compatible extension; superseded by V1d.26 below | Historical V1d.3 mapping was white/incomplete, red/wrong and green/complete; V1d.26 replaces it with the four-state bolt-and-nut contract |

Builder `11A-V1d.3` and focused coverage pass: generated Satsuma EditMode
`18/18`, assembly EditMode `22/22`, player interaction EditMode `27/27`,
outline EditMode `6/6`, Items EditMode `39/39`, and vehicle assembly PlayMode
`8/8`. Manual garage feel and rendered physics acceptance remain pending; the
affected Phase 1 rows are not promoted to `Verified`.

### 2026-08-14 Satsuma V1d.5 ownership, jacks and donor paint

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Physical ownership | Donor Assembly owner/parent records plus project AssemblyGraph | `BehavioralReference;PivotSource;Reimplemented` | Mounts reparent to their logical root/part owner with world pose preserved; triggers, fasteners and installed parts follow the dynamic aggregate recursively |
| Chassis and NWH | Locked Satsuma object label plus serialized donor Rigidbody mass `389`, NWH Vehicle Physics 2 integration and project assembly prerequisites | Mass `ConfigurationTransferred`; support/controller `Reimplemented` | PhysX owns the incomplete dynamic chassis; NWH owns contact only for graph-complete corners; AssemblyGraph remains authoritative |
| Car/floor jack | Locked donor Rigidbody, collider, lift child, linkage transforms, lever clip and Use-FSM state records; inspected live MOPR/BetterMSC handling state | `BehavioralReference;ConfigurationTransferred;PivotSource;Reimplemented` | Saved project lift height; F raises and held RMB lowers; car jack retains explicit chassis-pad support; floor jack uses the donor planar base with a reviewed `30 kg` handling mass plus an independent kinematic solid `0.1 x 0.09 x 0.1 m` saddle that transfers ordinary PhysX contact at any underside point. Ground drag follows the camera/crosshair XZ target while yaw-aligning the saddle/front local `-Z` with the projected camera direction, without orbiting around the player. Base and saddle vehicle contacts stay filtered only while dragging so neither can tow the chassis; saddle contact force-restores on release and before pumping even under an overlapping underside. Animated linkage colliders are query-only, and the saddle ignores only same-item base contacts at rest so the authored `40 mm` overlap cannot self-propel the Y-locked base. Its reviewed linkage, one-pump lever cycle and pickup lock follow donor values; no floor-jack snap point or virtual lift force remains |
| New Game palette | Twelve locked MainMenu SetColor actions | `ConfigurationTransferred;Reimplemented` | Exact twelve Color32 values flow through the existing New Game paint callback before gameplay activation |
| Starting rust | `CAR_PAINT_RUSTY`, `body_rust.png`, separate `CAR_MASSE` | `TemporaryDirectImport;BehavioralReference;Reimplemented` | Property blocks tint only exact rust-paint material slots; the rust texture remains and masse/interior surfaces are not painted |
| Save/stream | Existing schema-1 vehicle aggregate and native item/world save | Protected project authority plus migration | Vehicle schema unchanged; optional paint sentinel preserves old saves; native document v16 adds only jack `lift-height`; chassis-first and batch-frozen restore prevent detached assembly/shelf explosions |

Builder `11A-V1d.5` passed. Focused validation passed EditMode `62/62` and
`54/54`, plus PlayMode `12/12`. Complete manual garage, jack articulation,
stream-boundary and rendered-paint acceptance remains pending; affected rows
remain `PartiallyImplemented`.

### 2026-08-15 Satsuma V1d.6 systemic correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Chassis collision/dynamics | Exact donor `/Colliders` component inventory and serialized Rigidbody | `CollisionReference;ConfigurationTransferred;Reimplemented` | 29 active solid shapes (23 convex mesh, 4 boxes, 2 capsules), 389 kg bare mass, automatic COM/inertia, installed-part aggregate mass; player push policy cannot explicitly shove bodies above 35 kg |
| Mount targeting/removal | Donor mesh pose, Assembly FSM candidates and project ray selection | `PivotSource;BehavioralReference;Reimplemented` | Duplicate subframe FSM socket retired to one canonical mount with schema-1 alias migration; every installed part has a non-physical kinematic selection proxy; aimed-owner handoff remains bounded to the exact compatible mount |
| Toolbox and jacks | Donor case/key presentation, jack Rigidbody/collider/FSM records, BetterMSC wrench pose/arc | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Nested wrench selection bypasses only the case shell; keys have no Rigidbody or pickup physics and cannot launch the case/other keys; case cannot gain lid-motion velocity; car jack can be picked up lowered; both jack mechanisms articulate; lift latches one pad and blends capped physical support |
| Assembly/body audio | Donor `assemble`, `disassemble`, `bolt_screw`, `crash_low1/2`, `crash_hi1/2` clips | `TemporaryDirectImport;BehavioralReference;Reimplemented` | Stable `IAudioBackend` events use a private override library; successful graph mutations emit install/remove/fastener events; chassis collision emits low/high variants; missing private content is silent instead of falling back to the generic door-like clip |

Builder `11A-V1d.6` passed at `126 parts / 115 mounts / 205 fasteners / 29
chassis colliders`. Focused coverage passed generated Satsuma `21/21`, Items
`45/45`, generic assembly `23/23`, vehicle assembly PlayMode `10/10`, production
Bootstrap `1/1` and Unity fallback audio `9/9`. Complete manual garage,
loaded-jack settling, collision feel and stream-boundary acceptance remain
pending; affected rows remain `PartiallyImplemented`.

### 2026-08-15 Satsuma V1d.13 rear suspension staging

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Rear installation order | Original-game comparison captures plus locked Assembly records | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Both corners require arm -> drum -> either spring -> shock; the wheel remains a later independent contact stage |
| Articulated contact | Exact donor arm/drum poses and installed left-drum centre | `PivotSource;CollisionReference;Reimplemented` | Arm uses a chassis `HingeJoint`; drum is fixed to the arm; their solid collision and interaction proxy move together |
| Spring and damper | Licensed NWH Wheel Controller 3D API plus donor spring/shock behavior | `BehavioralReference;ConfigurationTransferred;Reimplemented` | NWH support begins only after arm + drum + spring, contacts on a `0.0871 m` drum, raises the Rigidbody chassis, and adds full damping only after the shock is installed |
| Road-wheel transition | Existing project wheel definitions and NWH geometry contract | Protected project authority plus compatible extension | Installing a road wheel switches radius, width and grip and rebuilds the generated NWH collider; no invisible road-size tyre remains in the drum stage |
| Ray selection | Moving installed-part body and existing explicit interaction capability | Project-owned `Reimplemented` | Trigger proxies share the part Rigidbody instead of using stale nested bodies, preserving close-range selection during articulation |

Builder `11A-V1d.13` passed. Generated Satsuma EditMode coverage passed
`22/22`; focused physical PlayMode coverage passed `5/5`, including measured NWH
spring force and Rigidbody chassis lift. Front suspension, fasteners, collapse of
unsecured dependency chains, final calibration and manual garage acceptance are
still pending, so `P1.CAR.008` remains `PartiallyImplemented`.

### 2026-08-16 Satsuma V1d.14 rear suspension physical correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Force authority | Licensed NWH Wheel Controller 3D API and failed live dual-solver comparison | Project-owned `Reimplemented` | NWH is now the only rear support-force authority; the obsolete rear-arm `HingeJoint`, drum `FixedJoint` and installed dynamic link are absent |
| Donor geometry | Locked hub centre, arm pivot, spring top/bottom and shock top/bottom coordinates | `PivotSource;ConfigurationTransferred;Reimplemented` | Arm rotates about its donor pivot, drum follows the physical NWH hub, spring expands/compresses between donor seats and the shock telescopes between donor endpoints |
| Ground and collision | Live donor driveway on Unity Default plus installed-part collision/ray reports | `CollisionReference;Reimplemented` | NWH accepts Default and authored world-surface layers; kinematic installed presentation disables loose solid colliders while retaining one compact query trigger |
| Assembly staging | Existing AssemblyGraph order and original-game comparison captures | Protected project authority | Arm + drum do not support the chassis; either rear spring enables physical contact and force; the shock only adds damping; removal returns the corner to the matching lower stage |

Builder `11A-V1d.14` passed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `22/22`; focused physical PlayMode coverage
passed `5/5` on a Default-layer contact fixture, including non-zero NWH force,
Rigidbody centre-of-mass lift, visible spring-span change, exact settled donor
drum pose and close-range installed-part ray selection. This supersedes the
V1d.13 dual PhysX-joint presentation strategy. Manual garage acceptance remains
pending; `P1.CAR.008` stays `PartiallyImplemented`.

### 2026-08-16 Satsuma V1d.15 static rear-suspension reset

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Three-part installation pose | Frozen donor mounts plus original-game comparison captures | `MountPointSource;PivotSource;ConfigurationTransferred;Reimplemented` | Left/right trailing arm, coil spring and shock absorber use six exact car-local poses guarded by a `2 mm` / `0.25°` extraction-drift check |
| Runtime pose authority | Failed V1d.13/V1d.14 rendered comparisons | Previous rear projector/deformer `Rejected`; AssemblyGraph protected authority | Rear NWH bindings and rear WheelControllers are disabled; no simulation backend may move or deform the accepted static parts |
| Owned child socket | Donor arm/drum relationship and observed garage-origin failure | `MountPointSource;Reimplemented` | Each drum socket belongs to its matching arm, is unavailable while that arm is loose and follows the arm after installation |
| Physics boundary | User explicitly requested geometry first without NWH | Honest partial implementation | Spring force/expansion, damping, ground support and body lift are not claimed by this reset and remain a separate pass |

Builder `11A-V1d.15` passed at `8/29/125/120/115/205/3/48`. Generated
Satsuma EditMode coverage passed `23/23`; focused static-pose PlayMode coverage
passed `4/4`, including chassis translation/rotation with all three installed
parts retaining their exact local mounts. V1d.15 supersedes the active rear
runtime authority described by V1d.13/V1d.14; those sections remain historical
audit evidence. Manual garage comparison is pending and `P1.CAR.008` remains
`PartiallyImplemented`.

### 2026-08-17 Satsuma V1d.19 physical rear-spring completion

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Accepted installed geometry | User live acceptance plus hash-locked donor runtime transform dump | `MountPointSource;PivotSource;ConfigurationTransferred` | The accepted arm, drum, spring and shock endpoints remain unchanged; physics acts through those anchors rather than replacing their pose |
| Spring installation behavior | User-observed donor compressed installation followed by expansion | `BehavioralReference;Reimplemented` | The visible coil begins at `0.062 m`, expands over `0.55 s`, and its force ramps with the same transition |
| Rear force path | Existing project Rigidbody chassis, arm hinge and arm-owned drum | Project-owned `Reimplemented` | Equal/opposite linear point forces push the arm down and chassis up; the shock changes damping; rear NWH remains disabled |
| Coil presentation | Sanitized donor coil mesh behind the existing project wrapper | `TemporaryDirectImport;Reimplemented` | A normal renderer is fitted along the mesh's dominant axis; no unweighted SkinnedMeshRenderer or animation-owned simulation state remains |

Builder `11A-V1d.19` passed at `8/29/125/120/115/205/3/48`. Focused
rear-physics PlayMode coverage passed `7/7` and generated Satsuma EditMode
coverage passed `23/23`. The grounded two-corner fixture measured `0.0376 m`
of chassis lift and more than `0.04 m` of visible coil extension. Rates and
damping remain provisional pending live feel acceptance; fasteners and audio
remain separate work. `P1.CAR.008` stays `PartiallyImplemented`.

### 2026-08-17 Satsuma V1d.20 rear-drum constraint correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Failure | Live V1d.19 screenshots and loaded two-corner physics fixture | `BehavioralReference;Reimplemented` | Correct mount pose was retained; the basic FixedJoint solver boundary was replaced rather than moving the drum again |
| Installed weld | Existing arm-owned drum socket and detachable assembly state | Project-owned `Reimplemented` | Six ConfigurableJoint degrees are locked, preprocessing is disabled and 1 mm / 0.5 degree projection prevents visible joint stretch while retaining ground-force transfer |
| Solver quality | Measured chassis/arm/drum mass chain under the existing 6000 N force cap | Project-owned configuration | Installed chain uses 20 position and 8 velocity iterations; detach restores the drum's loose Rigidbody defaults |
| Regression guard | 150 loaded fixed steps on both grounded drums | Automated verification | Maximum socket error is continuously bounded while the same fixture must still raise the chassis by more than 25 mm |

Builder `11A-V1d.20` passed. Rear PlayMode passed `7/7` with maximum measured
drum errors of `0.0010 m` and `0.502 degrees`; generated Satsuma EditMode passed
`23/23`. No mount, spring-force, NWH, save or stable-ID authority changed.
Manual garage acceptance and the later unfastened-collapse mechanic remain open,
so `P1.CAR.008` remains `PartiallyImplemented`.

### 2026-08-14 outside-milestone food/fridge/item correction

| Area | Donor evidence | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| 43 canonical `ITEMS` instances | Frozen `ITEMS` transforms, parent/layer and Rigidbody/collider records | `WorldLayoutReference;ConfigurationTransferred;CollisionReference;Reimplemented` | Catalog preserves every captured startup field; project layer, safe collision adaptation and native saved-pose precedence are automated |
| Home fridge | Stable door/paper/handle/shelf leaves, coarse solid-volume LOD collider, captured pivot and door travel | `PivotSource;CollisionReference;ConfigurationTransferred;Reimplemented` | Project hinge and ordinary handle interaction bind by stable ID; obsolete static moving-leaf copies and the solid interior volume are disabled; real interaction ray resolves stored food; Home owns power/door state, Items owns cooling query, `IAudioBackend` owns sound |
| Food freshness | Captured `Condition`, `SpoilRate` and `FridgeRate` values | `ConfigurationTransferred;Reimplemented` | Authoritative game-time decay, restore catch-up, closed/powered/electrified cooling and per-item persistence are live |
| Sausage package/loose sausage | Four-unit requirement plus captured loose raw/grilled/burned states, 30/10 thresholds/effects and reviewed loose-sausage prefab closure | `BehavioralReference;ConfigurationTransferred;TemporaryDirectImport;Reimplemented` | Package dispenses four deterministic physical children; loose sausage uses the reviewed sanitized mesh wrapper, consumes/cooks independently and saves membership/freshness/cook state |
| Stove, grill and mangal | Captured appliance centers and existing project power/fire states | `WorldLayoutReference;Reimplemented` | Explicit heat volumes feed one generic cooking simulation; no appliance-name switch exists in food logic |
| Spoiled/burned presentation/effects | State existence is evidenced; normalized adverse coefficients are incomplete | `BehavioralReference;Reimplemented` | Data-driven tint and negative effects are functional but provisional; manual donor acceptance pending, status remains `PartiallyImplemented` |
| Drink-food presentation | Juice-concentrate `Use` FSM dispatches `Drink` to the donor hand FSM; existing project beer viewmodel/grip is the protected presentation boundary | `BehavioralReference;Reimplemented` | Authored `ConsumptionPresentation=Drink` routes milk, juice concentrate, buttermilk, orange juice, mustard and ketchup through the existing drink viewmodel with the real carried object; ordinary food still uses no arms and state completion remains logic-owned |

Executed coverage: Items 53/53, Home 9/9, Needs 16/16, Save Integration
37/37 and production-Bootstrap PlayMode 2/2. See
`Docs/Items/FOOD_FRIDGE_INITIAL_PLACEMENT_FIX_2026-08-14.md`.

### 2026-08-14 outside-milestone Expanded Shop purchase extension

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Product/price/stock roster | User-supplied hash-locked `ExpandedShop.dll` and embedded `drinks` bundle | `BehavioralReference;ConfigurationTransferred`; third-party payload excluded | 19 unique price-backed Teimo offers; zero-based `Inventory` is normalized to effective capacity; duplicate mustard/ketchup shelf sections are merged by stable product ID |
| Physical items | Mod prefab/config fields plus existing project Items catalog | Presentation `TemporaryDirectImport`; state/physics `Reimplemented` | 18 new purchasable roots, one spawned fishstick and reuse of `item.loose-sausage`; sanitized renderer-only wrappers feed normal Rigidbody, pickup, food state and native item persistence |
| Shelf interaction | Mod STORE-local trigger transforms converted through the established world offset | `WorldLayoutReference;TemporaryDirectImport;Reimplemented` | 19 ordinary store targets resolve the same presentation provider used after checkout; reviewed renderer bounds drive hover, collider and per-unit stock visibility, with primitives retained only as provider-startup fallback |
| Checkout/save | Existing Economy, ServiceRuntime, item handoff and native service state | Protected project authority | +19 prices and offers; checkout materializes normal physical items; additive catalog restore fills newly absent stock lines without accepting malformed or unknown records |

No mod assembly, old Unity runtime type, PlayMaker logic, physics or script
enters the game. The selected hash-locked meshes/textures are an ignored private
Phase-1 `TemporaryDirectImport`, sanitized behind project wrappers; utility-use
mechanics beyond purchase remain explicit follow-up boundaries. See
`Docs/Items/EXPANDED_SHOP_EXTENSION_2026-08-14.md`.

Automated coverage: presentation validator `147` bindings (`128` reviewed base
items plus `19` Expanded Shop), Items `53/53`, Needs `16/16`, Economy `5/5`,
Services `57/57`, Save Integration `37/37`, and food PlayMode `2/2`.

### 2026-08-14 outside-milestone grill fuel and container lids

| Area | Donor evidence | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Charcoal pouring | GAME charcoal FSM `108771`: package `140`, grill `100`, rate `12/s`, X tilt threshold `80` | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Generic physical overlap + tilt receiver transfers authoritative item content; upright bags, full/hot grills and incompatible definitions fail closed |
| Ignition and burn | SetFire Use FSM `114344`: `>10` ignition, `120 s`, `0.1/s`, then `0.04/s`, off at `<=5`, water/rain stop | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Ordinary F ignition on body, saved flame/ember/wet state, generic cooking heat; global rain-to-local-wet binding remains a manual/integration boundary |
| Grill cover | Exact `CoverPivot/grill_cover` pivot and child pose | `PivotSource;TemporaryDirectImport;Reimplemented` | Separate cover F target uses saved `isOpen`; 105-degree travel and 220 deg/s are provisional project presentation |
| Grill flame presentation | Donor-relative `FireTrigger` center/size and grill local `+Z` up axis | `ConfigurationTransferred;Reimplemented` | Flame is centered in the bowl and uses grill-authored scale/emission/light values; the confirmed cover hinge and garbage-barrel profile are unchanged |
| Kilju bucket/lid | Separate donor bucket and canonical lid identities | Lid mesh `TemporaryDirectImport`; hinge `Reimplemented` | Bucket remains pickup/liquid authority; lid presentation is adopted without replacing stable identity; empty blue/helper surfaces are state-hidden |
| Save and compatibility | Existing item schema-1 content, scalar, flag, enabled/open fields | Protected project authority | No schema bump or ID migration; native item state round-trip covers fuel, wetness, flame/ember timer and both lid states |

Automated coverage: Items `53/53`, Economy `5/5`, Services `57/57`, Save
Integration `37/37`, Fluid/fire `7/7`, food PlayMode `2/2`; sanitized
presentation validator `147` bindings. See
`Docs/Items/PORTABLE_GRILL_AND_CONTAINER_LIDS_2026-08-14.md`.

### 2026-08-18 Satsuma V1d.21 front suspension

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Hub hierarchy and settled poses | Read-only bilateral donor runtime capture after full tightening, save and reload | `BehavioralReference;PivotSource;ConfigurationTransferred` | Donor wheel object remains the fixed steering carrier; child Spindle is the moving hub. Exact left/right loaded hub, wishbone pivot, spindle offset and shock-bottom transforms are transferred |
| Contact and travel | Donor `0.093737 m` carrier-to-hub offset, `1.4 degree` mirrored camber and existing licensed NWH API | Project-owned `Reimplemented` | NWH is the sole front support authority. Wishbone + spindle + strut enable a bare `0.12 m` hub contact; an installed road wheel switches to the tire profile without creating a second suspension solver |
| Installed presentation | Donor left/right strut meshes, bind poses and installed hierarchy | `TemporaryDirectImport;PivotSource;Reimplemented` | A project-owned two-bone skin follows the fixed upper mount and NWH-driven lower shock target; wishbone, spindle, fasteners and ray targets synchronize to the same hub pose |
| Compatibility | Existing AssemblyGraph, stable IDs, save schema and accepted rear implementation | Protected project authority | No mount-count, stable-ID or save migration. Rear NWH remains disabled and the accepted rear physical controller is untouched |

Builder `11A-V1d.21` passed at `8/29/125/120/115/205/3/48`.
Generated Satsuma EditMode passed `24/24`, focused front physical PlayMode
passed `1/1`, and the full Satsuma installed-part physics PlayMode class passed
`8/8`. Manual garage feel and the later whole-car fastener/tool pass remain
open, so `P1.CAR.008` remains `PartiallyImplemented`.

### 2026-08-18 Satsuma V1d.22 suspension install handoff

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Failure | User live comparison: front strut and rear spring first flew in their obsolete loose horizontal pose, then snapped to the accepted installed rig | `BehavioralReference;Reimplemented` | Simulation and mount transforms were retained; only the short player-facing presentation handoff was corrected |
| Front strut | Existing reviewed two-bind-pose strut and NWH-driven upper/lower targets | `TemporaryDirectImport;Reimplemented` | Preview bones reproduce the loose mesh on frame zero and continuously reach the exact installed bones before assembly state changes |
| Rear spring | Existing accepted rear top/bottom anchors and compressed-to-expanded controller | `TemporaryDirectImport;Reimplemented` | The fitted preview continuously rotates and compresses to `0.062 m`; normal physical expansion begins after installation |
| Timing and compatibility | User-requested two-times faster flight; existing AssemblyGraph/NWH/rear-controller boundaries | Project-owned presentation configuration | Duration is `0.17 s`; stable IDs, `115` mounts, physics authority and save schema are unchanged |

Builder `11A-V1d.22` passed. Generated Satsuma EditMode passed `24/24`,
focused no-snap PlayMode `1/1`, complete Satsuma physics PlayMode `9/9`, and
generic assembly EditMode/PlayMode `23/23` and `11/11`. Manual rendered garage
acceptance remains open, so `P1.CAR.008` stays `PartiallyImplemented`.

### 2026-08-18 Satsuma V1d.24 incomplete suspension and structural jack contact

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Incomplete rear corner | Existing reviewed rear hinge geometry plus live report that the arm ignored the world before spring installation | `BehavioralReference;PivotSource;Reimplemented` | Installed trailing arm remains a dynamic hinged actor with solid ground contact; spring installation adds support rather than creating the arm's first physics authority |
| Incomplete front corner | Accepted V1d.21 bilateral donor transforms plus live report for wishbone/spindle ground contact | `BehavioralReference;PivotSource;Reimplemented` | Before strut installation, PhysX owns `subframe -> wishbone -> spindle`; after strut installation the temporary actors hand off cleanly to the existing NWH front authority |
| Subframe lift path | Donor floor-jack physical saddle contract and generated Satsuma subframe collision | `CollisionReference;Reimplemented` | Subframe stays a dynamic solid projected weld to the shell, so ordinary saddle contact lifts the shell through it without named jack points or body-only filtering |
| Compatibility | Existing AssemblyGraph, mount identities, NWH adapter and save schema | Protected project authority | Preferred physical parents are explicit component references; no mount/stable-ID/schema migration and no donor FSM/runtime dependency |

Builder `11A-V1d.24` passed at `8/29/125/120/115/205/3/48`.
Generated Satsuma EditMode passed `24/24`, complete Satsuma physical PlayMode
`11/11`, and complete physical floor-jack PlayMode `6/6`. Manual garage and
streaming-reload acceptance remain open, so `P1.CAR.008` remains
`PartiallyImplemented`.

### 2026-08-18 Springless front-wishbone axis correction

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Failure | User live screenshots of both wishbones twisting when an incomplete car was tilted | `BehavioralReference;Reimplemented` | Connected-body choice and hinge-axis authority are now separate; imported part-root axes are never vehicle kinematics authority |
| Physical incomplete corner | Existing V1d.24 `subframe -> wishbone -> spindle` chain without a strut | Project-owned PhysX | Wishbone stays connected to the installed subframe but rotates only about the assembly-frame longitudinal axis |
| Completed corner | Existing accepted strut-installed NWH handoff | Protected project authority | Unchanged; the correction is inactive once the strut retires the temporary wishbone/spindle actors |
| Validation | Bilateral generated Satsuma instantiated with pitch and roll, no front struts, spindle load applied | Automated PlayMode comparison | Focused `1/1` and complete installed-part physics `12/12` passed; no content rebuild or save migration |

`P1.CAR.008` remains `PartiallyImplemented` pending manual in-game confirmation
and the deferred whole-car fastener/tool pass.

### 2026-08-18 Satsuma front steering, discs and wheels

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Rack, column and steering rods | Read-only bilateral donor runtime capture after install, tightening, save and reload | `BehavioralReference;PivotSource;ConfigurationTransferred;TemporaryDirectImport;Reimplemented` | Exact rack/column roots and left/right two-bone rod hierarchy are transferred; the rod outer bone follows NWH `NonRotating` |
| Front brake discs | Donor hub-relative left/right poses | `PivotSource;ConfigurationTransferred;TemporaryDirectImport` | Disc mounts follow NWH `Rotating`, retaining side-specific offsets and rotations |
| Stock front wheels | Donor wheel-relative poses plus existing licensed NWH contact API | `PivotSource;ConfigurationTransferred;TemporaryDirectImport;Reimplemented` | Installed wheels follow NWH `Rotating` and switch the physical contact stage to the authored road radius/width/grip; they are not decorative meshes |
| Assembly-test availability | User-requested temporary garage placement | Project-owned temporary configuration | The four stock wheels are stacked beside the fresh-game Satsuma with stable identities unchanged; GT placement and save schema are untouched |

The baseline build, focused generated placement EditMode and focused real-prefab
steering/disc/wheel PlayMode checks passed. Global fastener behavior and the
reported linkage-driven hub alignment remain open: the outer rod fastener must
close the rod-to-hub connection, while the distinct rod adjuster controls toe.
`P1.CAR.008` therefore stays `PartiallyImplemented`.

### 2026-08-19 Satsuma V1d.28 fasteners, install routing and native persistence

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Bolt/nut presentation | Locked donor `bolt2` mesh, `BOLTS` material and fastener texture; assembled-front runtime dump `20260818-174942` | `TemporaryDirectImport;ConfigurationTransferred` | All `235` project-owned targets render the reviewed donor fastener only while the owning part is installed; the subframe has four donor `10 mm` bolts, each front wishbone two donor `10 mm` bolts, every wheel four donor-positioned `13 mm` lug nuts and each outer steering-rod/hub joint one moving `14 mm` fastener; runtime state never depends on donor hierarchy or FSM code |
| Install routing | Donor assembly ownership, prerequisite order and exact mount poses | `BehavioralReference;MountPointSource;Reimplemented` | Aiming at an already installed prerequisite routes the held part through the nested chain subframe -> wishbone -> spindle -> strut/disc -> wheel; a 256-result ordered query, same-assembly parent-collider bypass and bounded 0.20/0.24 m disc/wheel handoff volumes prevent dense chassis geometry from truncating the real socket without restoring giant world-space triggers |
| Tool query | User contract plus existing BetterMSC read-only reference | `BehavioralReference;Reimplemented` | Selected wrench masks ray/overlap queries to shared layer `bolt-gayka-only`, sees bolts and nuts through parts, snaps to the fastener anchor and uses opposite wheel directions for discrete tighten/loosen stages; one graph stage is committed per completed work/regrip cycle rather than per unbounded wheel event |
| Feedback | User-approved four-state contract on established outline adapter | Protected presentation extension | Green = completely loose, yellow = partial, white = fully tightened, red = incompatible wrench; end-stage targets remain selectable and outlined |
| Native save discovery | Actual local save inspection: `vehicle.satsuma` existed with `vehicles=0`; production vehicle lives under hidden `DontDestroyOnLoad` composition root | Project-owned bug fix | Native initialization explicitly registers the persistent root through idempotent `VehicleSaveParticipant.RegisterHierarchy`; loaded-scene registration and schema 1 remain unchanged |
| Save coverage | Generated V1d.28 Satsuma round-trip and clean-prefab reload | Project-owned authority | Captures/restores one vehicle, `126` parts, `235` fasteners, mount occupancy, fastener stage and arbitrary loose-part world pose/rotation; a clean prefab instance restores a three-stage fastener and loose wheel after the original aggregate is destroyed; an old empty vehicle record cannot be reconstructed retroactively |

Builder `11A-V1d.28` passed at `8/29/125/120/115/235/3/50`.
Generated/install EditMode passed `27/27`; interaction EditMode passed `41/41`;
assembly EditMode passed `23/23`; assembly/Bootstrap PlayMode passed `12/12`;
save integration passed `13/13`.
`P1.CAR.008` remains `PartiallyImplemented` until
manual in-game wrench/save acceptance and the remaining linkage/collapse work.

### 2026-08-20 P0 Satsuma BoltCheck parity repair (11A-V1d.30)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Runtime state | Donor `Installed`, aggregate `Tightness`, `BoltedYES/NO`, maximum and BREAK branches | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Mount-owned fastener group separates Installed, hysteretic IsBolted and FullySafe; removal is blocked by the group latch, while physical attachment remains Installed-owned |
| Rear groups/order | Locked donor trailing-arm, drum and stock-shock BoltCheck plus both Assembly prerequisite variables | `BehavioralReference;ConfigurationTransferred` | Arms `2x12/max16/on-off12/0`; drums `1x14/max8/8/0`; shocks `12+6+6/max24/2/0`; matching arm Installed permits placement, while its Bolted latch retains the independently installed spring/shock/drum; installing on a loose arm collapses the unsupported construction |
| Carried mount targeting | Project-owned direct mount and installed-surface interaction targets | `Reimplemented` | Candidate compatibility is filtered before interaction priority, preventing an unrelated nearby arm/spindle socket from masking the valid mount and reporting a false incompatibility |
| Wheel groups/BREAK | Donor TriggerWheel child `Bolts`, four 13 mm nuts, speed comparisons and Chance formula | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Every wheel has four unique nuts, max32/on-off1/0; zero tightness deterministically breaks above 5 km/h, partial retention is seeded above 33 km/h; wheel install requires disc/drum Installed only |
| Import safety | `db_PartRequired`, `db_PartRequired1`, `ActivateThis\d*`, `ReferencesBolts` | Project-owned importer guard | Dual prerequisites and all presentation roots are extracted; unresolved real Bolts evidence fails validation; validated staging is atomically promoted with rollback |
| Save/migration | Existing schema-1 additive 115/205 and 115/220 aggregates | Project-owned schema 2 | Saves IsBolted latch; missing newly introduced fasteners on an installed legacy part migrate fully tight, empty mounts remain absent, existing stages/stable IDs/loose poses remain unchanged |
| Rear observable physics | Accepted custom rear solver plus generated real-prefab fixture | `BehavioralReference;Reimplemented` | Unloaded/loaded/recovered spring length `0.19751/0.08250/0.19751 m`; rebound peak `6.1069 m/s` settles to `0.0000 m/s`; no legacy Wheel constants copied into another solver |

Builder `11A-V1d.30` passed at `8/29/125/120/117/252/3/52`.
Post-build combined P0/generated/Assembly/save/carried-socket EditMode passed
`69/69 x3`, and rear/wheel PlayMode passed `16/16 x3`.
`P1.CAR.002/.003/.008/.018` remain
`PartiallyImplemented` pending manual full-car fastening, road BREAK and
streaming acceptance.

### 2026-08-21 Satsuma road-wheel rest parity (11A-V1d.31)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Donor low-speed wheel result | Read-only `Wheel.cs`: load/radius rolling-resistance torque and exact-zero branch when friction crosses zero | `BehavioralReference;ConfigurationTransferred` | The observable zero-rest outcome is locked; donor `MonoBehaviour` code and legacy solver are not runtime dependencies |
| Front road wheels | Licensed NWH contact state plus project-owned chassis authority | `Reimplemented` | Grounded, loaded, undriven residual spin is cleared only while the chassis is stationary; current and previous NWH angular samples are reset after simulation |
| Rear road wheels | Accepted donor wheel poses, project-owned rear hinge chain and real ground contact | `PivotSource;BehavioralReference;Reimplemented` | Each installed wheel remains a free `RoadWheelAxle`; the rest latch removes only low axle spin and does not weld, brake or clamp an airborne/moving wheel |

No stable ID, mount, fastener, save DTO or solver-ownership migration was made.
NWH EditMode passed `30/30`, generated Satsuma EditMode `27/27`, focused rear
loaded-rest PlayMode `1/1`, and complete installed-part physics `16/16 x3`, all
newer than the generated prefab. `P1.CAR.008` remains `PartiallyImplemented`
until manual level/slope idle, rolling, steering, road BREAK and streaming
acceptance.

### 2026-09-02 Satsuma rear visible-wheel rotation (11A-V1d.43)

| Area | Evidence/input | Classification / ownership | Implemented boundary / status |
|---|---|---|---|
| Donor rear model roll | Read-only `Wheel.cs`: all four wheel models receive axle angle; rear tyre/drum/fastener/pivot share the rotating branch | `BehavioralReference` | Rear wheels remain unpowered but visibly rotate from road motion; donor handbrake can intentionally stop the rear pair |
| Rear contact/roll authority | Existing licensed NWH rear `NonRotating` and `Rotating` containers | `Reimplemented` | Absolute world roll delta is forwarded to the arm-owned drum and road-wheel mount owners; no donor component or second physical axle/collider is introduced |
| Seating and idempotence | Accepted V1d.41 rear standard seat plus current generated prefab | `PivotSource;ConfigurationTransferred` | Mount centres and `-0.040 m` seat remain fixed; neutral reset prevents repeated `ApplyNow()` from accumulating roll |

Builder `11A-V1d.43` was promoted. Focused roll and post-review idempotence
tests passed `1/1` each, the complete installed-part fixture passed `18/18`,
and generated content passed `29/29`. Gravity-driven rolling received
`USER PASS` on 2026-09-02. A donor handbrake-release comparison remains
inapplicable until the remake implements that separate mechanic. Save/load is
claimed only by the separate V1d.44 row.

### 2026-08-31 — Map vegetation rebuild

Historical setup state: the pending execution language in this subsection is
retained for chronology and is superseded by the active v7 matrix below.

| Area | Classification | Implemented boundary / status |
|---|---|---|
| Canonical individual tree transforms, recovered batch-card roots, shrub and billboard-boundary geometry | `WorldLayoutReference` | Frozen 04A1.1 source reused read-only; method/confidence and mesh hashes remain explicit; no fabricated individual-transform claim |
| Surface filtering, configurable exclusions, deterministic planting and cell-layer registration | `Reimplemented` | Extends existing mesh vegetation/streaming services; copied geometry supports source-cell unloading; exact source X/Z, channels and category ownership are validated |
| Generated per-cell private vegetation presentation | `TemporaryDirectImport` | Uses existing licensed modern assets with donor-derived placement; runtime-baseline output is excluded from Git; pilot/full-map generation pending |

Isolated compilation passed twice; actual Unity EditMode passed 42/42.
PlayMode, visual boundary closure, saved pilot/full-map counts and performance
remain pending. No Phase 1 feature is promoted to Verified and no Phase 2 gate is
approved by this tooling change. See `Docs/WorldRemaster/MAP_VEGETATION_REBUILD.md`.

### 2026-09-01 — Vegetation presentation correction — active v7

| Area | Classification | Boundary / evidence |
|---|---|---|
| Existing tree identities and canonical ground UV/base-colour | `WorldLayoutReference` | 66,910 saved positions retained; colour remains a grass rejection/eligibility input after independent road/building/field/water exclusions; source geometry/textures unchanged |
| ALP spruce and reviewed rock/stone art | `ThirdPartyPrivatePhase1Presentation` | Five reviewed optimized spruce wrappers supply 65% / 43,492 trees; `Rock01/02/03` and `stone01/02` feed sparse ground clutter. Archive SHA-256 `39D47D1A7A8424A3C1F39DD55DD64ED6CEE42542A13466CC04FB40280126B827`; `productionReady=false` because no reviewed licence proof is stored |
| Reviewed Chernobyl non-spruce bindings | `LicensedThirdPartyPhase1Presentation` | Pine 20% / 13,382, birch 7.5% / 5,018 and aspen 7.5% / 5,018 retain the active prefab GUID bindings. The earlier Chernobyl grass and Engelmann spruce presentation is superseded history |
| NatureManufacture Finnish grass/forest-floor subset | `LicensedThirdPartyPhase1Presentation` | Meadow detailed/regular/cross grass; grey willow, fern, moss, selected mushrooms/dead grass, `BranchLitter01` and `PoplarLeafLitter01`. Forest SHA-256 `1EC0...BC864` is 22 seeds / 79 closure; Meadow `34AE...DC38` is 51 / 156; unresolved/demo/script/Editor count is zero |
| Slot-preserving litter presentation | `LicensedThirdPartyPhase1Presentation` | Binding v3 run `20260901-091800-66925a3b` keeps `DeadGrass02/03` GUID slots; actual 88-scene YAML references are 986 branch-litter + 951 poplar-leaf-litter with no added records; shrubs/forest floor remains 28,759 |
| Species allocation, HDRP wrappers, 0.65 m grass grid, three-texel green-mask dilation and deterministic placement | `Reimplemented` | Active exact mix is 65/20/7.5/7.5. Pilot `20260901-092200` passed at 1,967 / 1,727 / 1,793 / 223,100 from 620,500 candidates with settings hash `2ffb...3569` and fingerprint `fa86...5da6`; matching five-view HDRP capture passed on RTX 4070 SUPER with zero missing shaders |
| Full generated private vegetation payload | `TemporaryDirectImport` | `RunAllBatch` `20260901-092757` passed 88/88, errors 0; fresh `ValidateAllBatch` `20260901-100750` passed 88/88 with `validationOnly=true` and identical per-cell fields/fingerprints/counts. Totals: 37,678 + 29,232 trees; 28,759 floor/shrubs; 8,077,749 grass from 54,599,726 candidates. Generated payload remains outside Git and is not ProductionReady |
| Runtime pacing, metadata/GPU budgets and cell integration | `Reimplemented` | Active graphics-enabled GPU PlayMode 6/6 passed. Secondary work is bounded, but retained fresh-process evidence still includes a 608.743 ms scene load (478.810 ms deserialize + 129.870 ms integration); v7 was not reprofiled and the hitch is not claimed fixed |

Current tests are WorldRemaster vegetation 41/41, forest-floor binding 5/5,
green texture mask 15/15 and graphics-enabled GPU PlayMode 6/6, all with zero
failures/skips. The old 40/40 result, 50/35 mix, Chernobyl grass and Engelmann
spruce rows are superseded history; retained Chernobyl pine/birch/aspen bindings
remain active. Exact source hashes and remaining limits are in
`Docs/WorldRemaster/MAP_VEGETATION_PRESENTATION_REVISION.md`. This entry does not
promote a Phase 1 feature to Verified or approve Phase 2. The one recommended
next milestone is a measured compact-woody runtime-catalog pilot.

### 2026-09-02 — Map vegetation correction — active v12 superseding v7

The v7 matrix above is retained for chronology. The following rows are the
active private Phase 1 presentation state.

| Area | Classification | Boundary / evidence |
|---|---|---|
| Canonical tree evidence and deterministic natural infill | `WorldLayoutReference;Reimplemented` | 37,678 accepted donor originals remain fixed; 24,490 independently filtered forest candidates are added within the 65% cap, preserving road, yard, field, water, route and `OpenSpace` exclusions. `OriginalTrees` totals 62,168; grounded near-boundary trees add 5,811. No terrain, road, gameplay ID or save migration |
| Species and grass presentation | `LicensedThirdPartyPhase1Presentation;ThirdPartyPrivatePhase1Presentation` | Requested 65/20/7.5/7.5 policy yields spruce/pine/birch/aspen 44,201 / 13,576 / 5,052 / 5,150. Grass uses reviewed non-cereal Forest `Grass02_3/Grass01_3/Grass03_3` only, with corrected provenance SHA-256 `76343A8450F0BC106E61C300294AC2C34A195C2ABC476F58853CEE2821EFD4E9`; 5,157,636 records split 2,368,747 / 1,651,618 / 1,137,271. Vendor presentation remains removable and `productionReady=false` |
| Packed near-cell payload | `TemporaryDirectImport;Reimplemented` | Across 88 cells: 18,747 shrub/floor records, 86,726 matrices and metadata records, 2,130 prototypes, 47,138 batches and 62,168 collision records. Scene YAML is 1,088,371 bytes and packed assets 76,558,979 bytes; prefab-instance GameObjects, direct renderers and serialized collider components are all zero. Twenty-five collision-pool overflow warnings remain a profiling risk |
| Distant boundary and canonical rocks | `WorldLayoutReference;TemporaryDirectImport;Reimplemented` | Collisionless backdrop: 16,000 trees / 81 streamed scenes / 307 renderers / 349,465 vertices / zero colliders / zero crown gaps. Global fingerprint `d75455716d1faee823b0ceb4841c46358dd95c484b5f6ac3d234d3a719475097`: 45 canonical rocks, 149 replacement renderers, zero generated colliders and zero missing/duplicate/unexpected-anchor or metadata-mismatch counts; audited legacy rock collision remains authoritative |
| Full generation and fresh validation | `Reimplemented` | `RunAllBatch` `20260902-001303` report SHA-256 `F44EA72B4C45C870A1E40BFD4BE7ED1C65D92D1FAE8E8CB85FBE1B48C7932AE6`; fresh `ValidateAllBatch` `20260902-004112` SHA-256 `A0E0CFC375FA869251BF673EAA6FE72E0999BD993B20AFED9F5734D81F640310`. Both passed 88/88 using settings `7f22119bd3661cad44e1f5cb983165a7cc0b5da3dfebd0e4a1f6b8dc99ccba0d` and source `2b606dd62164133bfd0d1252b4cca80cc6a8b3947f9b8f4887f208cef0ac0671`; exact comparison passed all cell reports and 268 non-report artifacts, aggregate `a631e2a1b330fc3037863b658f6e222675404f1f17701128b4a08497af2fc169` |
| Runtime route and performance evidence | `Reimplemented` | Direct Editor route passed at 66.0798 / 71.6469 / 20.6325 ms maximum-main/maximum-yield/p95-yield; production service passed at 63.8666 / 68.1513 / 14.5884 ms with clean owned-scene/renderer/GPU-buffer cleanup. Targeted EditMode 77/77 and core PlayMode 22/22 passed. This is not complete-world Player, resident-memory, representative GPU or 60 FPS acceptance |

The generated scenes, packed data, reports, captures and vendor payload remain
outside Git and private. This v12 evidence improves presentation and Editor
streaming behavior without promoting the art to `ProductionReady`, closing the
25 collision-pool warnings, or approving Phase 2. The next milestone is one
integrated Player traversal and long-session memory benchmark on this exact v12
output.

### 2026-09-02 — Context HUD localization and donor pickup hand

| Area | Classification | Boundary / evidence |
|---|---|---|
| Donor `gui_uset` 32 x 32 open-palm pixels | `TemporaryDirectImport` | Hash-locked byte-for-byte private Phase 1 copy; deterministic GUID and replacement key `ui.interaction.pickup-hand`; generated payload remains outside Git and requires Phase 2 reauthoring |
| Reticle binding, stronger halo and action availability | `Reimplemented` | Existing project-owned `CrossdotPresenter` binds the donor texture; unavailable empty/wet item actions no longer advertise no-op plaques; no donor controller or input logic |
| RU/EN target/action/status presentation | `Reimplemented` | One settings-driven adapter covers all 152 current item definitions, all 108 service rows and every currently generated Satsuma part display name, while stable localization keys remain separate from simulation/save identity |
| Context title/subtitle typography | `Reimplemented` | Both rows use the same loaded font and 18 px size; bold title versus regular subtitle preserves hierarchy without the previously undersized target name |
| Automated evidence | `Verified for bounded change` | Import command passed; focused interaction EditMode 49/49, item EditMode 47/47, donor-icon contract 1/1, settings-locale PlayMode 1/1 and voice-locale PlayMode 1/1 passed; in-game visual acceptance remains pending |

### 2026-09-03 — Satsuma body/material/rain V57

| Area | Classification | Boundary / evidence |
|---|---|---|
| Seven paint surfaces and material types | `BehavioralReference;ConfigurationTransferred;TemporaryDirectImport;Reimplemented` | Donor `PaintType=0` retains rusty paint; Regular/Metallic/Matte and three special profiles remain distinct; selected New Game colour reaches shell and six detachable panels |
| Starting rust and Fleetari body repair | `ConfigurationTransferred;Reimplemented` | HDRP-packed donor rust detail/mask/specular data remains under tint; per-surface regular paint removes rust. Spray-can input is explicitly deferred |
| Body fasteners and loose doors | `PivotSource;ConfigurationTransferred;Reimplemented` | Nine mounts expose 32 part-owned short bolts at exact sizes/thresholds; both doors remain physical pickups while loose. Preserved source `BoltPM` Z scale limits all four bootlid bolts to the donor `10 mm` total travel instead of the erroneous `20 mm` |
| Bootlid exterior handle/garnish | `TemporaryDirectImport;PivotSource` | Donor-active `bootlid_emblem` / `datsun_bootlid_001` remains at its exact direct-child local pose; it is the approximately `0.56 m` handle/garnish assembly and is no longer suppressed as optional emblem clutter |
| Fender mudflaps | `BehavioralReference;Reimplemented` | Inactive donor `ActivateThis` copies are not baked into stock fenders; separate one-bolt mudflap parts remain usable, including on a loose fender |
| Stock/GT rims | `ConfigurationTransferred;TemporaryDirectImport` | Four steel rims use rusty material; GT inner/outer split is rusty/metallic with legacy texture channels repacked for HDRP |
| Glass and rain | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Five transparent HDRP windows consume a project-owned 512 atlas with donor rain/gravity/intensity values and shelter gating; no donor runtime/shader ships. Wiper clearing is pending |
| Automated evidence | `ImplementedAutomatedValidationPassedManualInGameAcceptancePending` | Current builder `11A-V1d.60` passed; generated-content EditMode 39/39 and physical hinge PlayMode 4/4 passed. Whole-body rain wetness is not donor-evidenced and remains Phase 2 |

### 2026-09-03 — Satsuma installed-panel lifetime correction

| Area | Classification | Boundary / evidence |
|---|---|---|
| Panel hinge behavior | `BehavioralReference;ConfigurationTransferred;Reimplemented` | Both doors, bootlid and hood retain exact pivots, axes, limits, torque direction, audited old-Unity break values and four-fastener ownership; only held LMB/RMB drives the project hinge, release preserves physical inertia, and `F` has no hinge action. Closing uses the Rigidbody's mount-relative signed rotation and requires the one-degree endpoint for two consecutive fixed steps, avoiding both broad-travel snap and mirrored-door refusal. The bootlid additionally uses the donor `-70..-69` full-open hold, restores `-70..0` before closing, and swaps chassis/moving `bootlid_hooks` copies exactly through install/remove state. The actual Unity 6 joint is unbreakable so ordinary chassis constraints cannot destroy it; the graph still detaches a completely unfastened panel at full opening |
| Connected-body collision | `BehavioralReference;Reimplemented` | Installed door retains world collision but ignores only its connected chassis-body pairs; donor Satsuma value is inferred from all frozen vehicle-door hinge records using `EnableCollision=false`, not claimed as a direct serialized Satsuma capture |
| Pose synchronization | `Reimplemented` | Generic installed-part sync delegates to the hinge's mount-plus-angle world pose, so late synchronization cannot reset an open panel |
| Automated evidence | `ImplementedAutomatedValidationPassedManualInGameAcceptancePending` | Builder `11A-V1d.60`, generated content `39/39` and focused physical hinge PlayMode `4/4` passed; full Bootstrap/Player coverage remains owned by the parallel task and was not rerun here |

### 2026-09-03 — Player mass, scale and jump/posture correction

| Area | Classification | Boundary / evidence |
|---|---|---|
| Default player weight | `ConfigurationTransferred;Reimplemented` | Frozen `PlayMakerGlobals.asset` stores `PlayerWeight=83`; the existing needs DTO remains authoritative and now synchronizes kilograms into `FirstPersonMotor` |
| Dynamic support loading | `Reimplemented` | A downward feet probe lets the nearest non-player surface own the support decision. Upward-facing (`normal.y >= 0.55`) dynamic support receives ramped gravity-equivalent `ForceMode.Force` on the fixed physics clock; static ground and unrelated kinematic geometry occlude deeper bodies. A kinematic installed panel routes load only to a dynamic Rigidbody in its own parent chain. Centre plus four `0.8 radius` footprint samples, `0.2 s` confirmation and same-aggregate side-contact rejection prevent a low rocker from becoming transient support |
| Dynamic-body side contact | `BehavioralReference;Reimplemented` | Horizontal motor nudging requires a matching currently available `IPickupTarget` plus the retained `35 kg` cap. Separately, the generated Satsuma restores donor `Collider2` / `PlayerOnlyColl` roles: world-facing chassis shapes exclude layer 9 and four donor `PlayerColl` shapes live on one kinematic player-only proxy. The controller is blocked without putting its effectively infinite PhysX solver mass against the rolling chassis; support rays through the proxy still route controlled weight to the dynamic ancestor |
| Bathroom scale | `BehavioralReference;ConfigurationTransferred;TemporaryDirectImport;Reimplemented` | Frozen `Measure` FSM maps weight by `-2.78 deg/kg`, eases on/off over `2 s`, and uses a `0.2 m` distance gate. Project presentation resolves the streamed gauge only by stable ID; the foot volume is widened for controller-origin compatibility |
| Jump and landing camera | `Reimplemented` | `6.65 m/s` launch force preserves the former `1.05 m` arc; structured landing evidence adds bounded force influence to stronger `0.14 m` / `3 deg` presentation while retaining `Landed(float)` |
| Posture and eye line | `Reimplemented` | Jumping from crouch commits to standing after headroom validation; rise is `6 m/s`; eye targets and canonical camera are raised `0.08 m` |
| Automated evidence | `ImplementedAutomatedValidationPassedManualInGameAcceptancePending` | Unity compile and deterministic Satsuma `11A-V1d.60` build passed; focused EditMode `76/76`; original locomotion PlayMode `14/14`; production Bootstrap/home stable-ID binding smoke `1/1`; prior full-project locomotion PlayMode `21/21`; final isolated and full-project Unity `6000.3.11f1` locomotion regressions each passed `23/23`; the generated-prefab collision contract was revalidated `1/1` and runtime activation/isolation `2/2` after the `.60` rebuild. Static-ground occlusion first failed alone (`20/21`); compound-rocker first failed alone (`21/22`); raw CharacterController contact reversed an oncoming `600 kg` body from `+0.2` to about `-1.1157 m/s`, while the kinematic-proxy regression passed. Broader generated-Satsuma fixture is `38/39` with one unrelated existing NUnit `Has.Count` bootlid-test error; the collision contract passed within that run. Subjective camera, active Satsuma suspension, all body-part contacts and gauge motion acceptance remain manual |
