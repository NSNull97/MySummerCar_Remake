# Phase 1 Locked Traffic Behavior Audit

Status: **behavioral evidence lock; implementation and manual acceptance are not complete**

Audit date: 2026-08-06

Locked donor revision: `msc-world-baseline-04a1.1-c3f2f337`

Owning Phase 1 areas: `P1.TRAFFIC.*`, `P1.VEHICLE.011` through
`P1.VEHICLE.028`, and the corresponding individual event-vehicle rows

## 1. Purpose and authority

This document prevents three different things from being treated as one
traffic design:

1. **Locked original behavior** is the Phase 1 parity authority. It is derived
   from the frozen donor scene, its serialized PlayMaker configuration, route
   transforms, and actor hierarchies. It is classified as
   `BehavioralReference`, `ConfigurationTransferred`, or
   `WorldLayoutReference` as stated below.
2. **Project runtime behavior** is a project-owned `Reimplemented` system. It
   may use NWH Vehicle Physics 2 for wheel contact, but donor and mod runtime
   code are not runtime dependencies.
3. **TrafficCarExpansion (TCE) behavior** is an optional enhancement reference.
   It may inform a later project-owned extension, including its town and fuel
   station excursion. It does not override the original actor roster,
   lifecycle, route residency, or clock semantics required for Phase 1 parity.

Where this audit contradicts an older milestone report or generated catalog,
the hash-locked evidence below is authoritative until a newer donor audit is
explicitly approved. In particular, the older descriptions of a repeating
two-hour bus timetable, a generic dirt-road KUSKI loop, and clock-driven route
catch-up are not donor-exact.

## 2. Evidence lock and reproducibility

| Evidence | Location / identity | Classification | Use |
| --- | --- | --- | --- |
| Locked donor scene | External staging `_Scenes/GAME.unity`; SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` | `BehavioralReference`; `ConfigurationTransferred`; `WorldLayoutReference` | Original roots, actors, FSM constants, routes, transforms and presentation hierarchy |
| Donor-to-project translation | `(169.98, 1.611, -1040.625)` metres | `ConfigurationTransferred` | Applied after reading donor-space route/gate coordinates |
| Project evidence extractor | `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1TrafficRouteEvidence.cs` | `Reimplemented` | Deterministic project-owned route extraction |
| Current presentation importer | `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1StoryTrafficPresentationImporter*.cs` | `Reimplemented` | Sanitized temporary wrapper generation |
| Current runtime | `Assets/Game/Traffic/Runtime/TrafficWorldRuntime*.cs`, `Assets/Game/NPC/Runtime/NpcWorldRuntime.cs` | `Reimplemented` | Logical actors, physical residency, story traffic and transport runtime |
| Current checked-in catalog | `Assets/Game/Traffic/Content/Phase1/TrafficRoadNetworkCatalog.asset` | Project generated data | Must be rebuilt after source/evidence corrections; several serialized values are stale as recorded below |
| TCE DLL supplied by the user | `TrafficCarExpansion.dll`; SHA-256 `7b626ca489a4abd7acdd34ca987199e6a27dd5bcd6cd398bab441360471313f5` | `BehavioralReference` only | Read-only managed-code and embedded-route inspection |
| TCE embedded bundle | SHA-256 `adc45a6b161834ab88cb507d51d529ffff09687e0781c84b7ac5749b10e2bcaa` | External evidence only | Route payload source; remains outside Git |
| TCE AssetRipper route prefab | `Routes_Modded.prefab`; SHA-256 `76854020dff8d9562a88d9fd77e36155aa8c185f8a11b71e436db7a5af878752` | External evidence only | Exact ordered town/fuel route points |
| Current TCE manifest | `Assets/Game/LegacyImport/Manifests/TrafficCarExpansionBehaviorEvidence.json` | Project-owned evidence manifest | Audit-start stride-four defect has been replaced in the working tree by all `68/321/112` source points plus metrics/provenance; generated Unity catalog and physical acceptance remain pending |

Raw donor and mod payloads remain outside Git. No donor `MonoBehaviour`,
PlayMaker runtime, old `UnityEngine` assembly, or TCE assembly is permitted in
the recreated runtime.

## 3. User-reported failures and evidence-backed diagnosis

| Reported symptom | Evidence-backed cause | Required outcome |
| --- | --- | --- |
| Petteri and the beer truck have geometry protruding near the wheels | The importer takes every active renderer below a whole vehicle root. Donor axle, halfshaft, driveshaft, spindle and tandem-arm presentation is consequently baked as static chassis geometry while separate wheel pivots also move. Exact renderer IDs are listed in section 12. | Rebuild wrappers from explicit body and visible-wheel allowlists. Mechanical branches may only be retained by an independently articulated presenter. |
| Cars rock side to side or pitch like toys | Current mass/centre-of-mass, spring, damper, tire stiffness and angular damping values are partly project tuning, not a captured per-model donor suspension set. Heavy vehicles use the same four-contact abstraction even when six visible wheels exist. | Remove renderer corruption first, then calibrate each vehicle class using physical tests. Preserve six-wheel presentation/contact intent for the truck instead of hiding a pair. |
| Jani and Petteri use the shoulder, cut corners, leave the road, or hit the pole near Teimo | A long generic look-ahead can put the steering target outside a curved lane. Old residency/time reconciliation could also reset logical and physical targets. A direct transform/clock correction is not valid steering. | Use the full donor route in original order, curvature-bounded look-ahead, physical steering, route projection only for progress, and exact root residency. Complete a full Perajarvi-to-highway comparison before acceptance. |
| A small Jani collision ejects Suski | Collision magnitude had been allowed to become terminal authority. The donor does not use an arbitrary ordinary collision to eject her; it uses a hidden structural-failure joint with specific arming conditions. | Ordinary impact produces braking/recovery and spatial collision audio. Only the locked structural-failure condition enters the fatal state. Suski and Jani ragdoll inside the cabin; no launch or pickup-object substitution occurs at the impact frame. |
| Fatal Jani crash leaves traffic stopped or Suski behaves like an item | The current story transition is incomplete if it disables unrelated actors or swaps presentation before establishing stable in-car ragdolls. | Stop Jani's engine/navigation, keep the wreck and both in-car ragdolls stable, and let the separate rescue/story state own later Suski interaction. Petteri and ambient traffic remain independent. |
| Bus is slow and stops behind the sewage station | The checked-in catalog still contains `12.5 m/s` and obsolete projected stop distances `61.393 / 2038.777 / 3598.210 m`. Locked route projection yields a target speed of `22.9167 m/s` and stops `5207.049 / 7774.381 / 12027.058 m`. | Rebuild the generated catalog and test the entire 13.698 km loop plus all three stops. |
| Skipping time makes traffic go in arbitrary directions | Donor traffic advances by runtime simulation frames. It does not analytically move route actors by skipped game-clock duration. Physical roots also freeze when disabled. | Clock/sleep changes schedule eligibility only. They must not advance distance, reroll direction, rematerialize, or teleport an already-running actor. Save/load restores the same route state. |
| Traffic has no useful collision/engine/music sound or is audible across the map | Audio ownership and spatial ranges are incomplete or generated bindings are stale. Ordinary collision sound and fatal story transition have been conflated in earlier behavior. | Keep per-emitter spatial events behind `IAudioBackend`; collision audio is a non-terminal event. Validate attenuation at source, 30 m, configured maximum range, and across streaming. |

## 4. Locked route inventory

The following point counts are direct transform evidence, not a desired spline
resolution:

| Route | Ordered points | Locked use |
| --- | ---: | --- |
| `Highway` | 1,889 | Ambient highway roster and sections of scripted traffic |
| `BusRoute` | 1,084 | Bus loop |
| `DirtRoad` | 3,719 | ordinary Pena/Fittan, Saturday KUSKI sections and official rally |
| `Village` | 295 | Jani/Petteri Perajarvi departure |
| `HomeRoad` | 462 | KUSKI ride-to-player-home branch |
| `Dancehall` | 269 | KUSKI initial/return section |
| `Dragrace` | 166 | Drag event course |
| `RoadRace` | 624 | Jani/Petteri highway/race section and KUSKI route section |
| `Trackfield` | 291 | Jani/Petteri and KUSKI route sections |
| `Waypoints1` | 8 | AI boat 01 loop |
| `Waypoints2` | 8 | AI boat 02 loop |
| Train eastbound / westbound | 2 endpoints each | Bidirectional train traversal |

Point order and route identity are gameplay data. They must not be silently
resampled, reversed, reduced by a fixed stride, or replaced with a generic
Teimo/highway loop.

## 5. Traffic-root residency and clock semantics

### 5.1 Original root ownership

The locked `TRAFFIC` root contains, among other objects, `Routes`,
`VehiclesHighway`, `VehiclesDirtRoad`, `TriggerManager`, `Police`, `Lake`,
`FittanSpawns`, three bus-stop markers, low-beam areas and skidmark support.

`VehiclesDirtRoad` is frequently misidentified. It has one direct child named
`Rally`, and that child owns `FITTAN T58170`. Despite the hierarchy token
`Rally`, its FSMs contain the cousin's dirt-road navigation, lift, home branch,
crash and crime behavior. It is the ordinary dirt-road Pena/Fittan context, not
one of the three official rally competitors.

`KUSKI T37302` is under the independent `NPC_CARS` root. Its Saturday-evening
setup explicitly retires the dirt-road Fittan off-screen before activating.
The evidence therefore describes two mutually exclusive presentation/behavior
contexts associated with the cousin role, not two unrelated cars that should be
simultaneously active. Their stable-ID/story mapping must be reconciled before
the catalog is rebuilt. Therefore:

- highway root residency gates the ten base highway actors;
- dirt-road root residency gates the ordinary Pena/Fittan context;
- KUSKI eligibility and off-screen activation are independent, but mutually
  exclusive with that context;
- the official rally competitors belong to top-level `RALLY T61205`;
- replacing either context with one generic always-active dirt loop is a parity
  bug.

### 5.2 Exact gate pairs

The donor uses paired convex quad trigger volumes. The values below are donor
scene coordinates; the project translation is applied afterwards. The quoted
span is the quad's approximate X-by-Z extent.

| Gate | `Out` centre / yaw / span | `In` centre / yaw / span |
| --- | --- | --- |
| CarShop | `(1584, 10.6, 882.5)`, `-166.4077 deg`, `50 x 6 m` | `(1585.3396, 10.6, 888.0404)`, paired orientation/`50 x 6 m` |
| Fields | `(1869.8, 4.9, -991.4)`, `-10.717 deg`, `60 x 6 m` | `(1870.2705, 4.9, -997.1122)`, paired orientation/`60 x 6 m` |
| Centrum | `(-1606.1, 5.46, 1100.8)`, `30 deg`, `26 x 6 m` | `(-1609.383, 5.46, 1096.1137)`, paired orientation/`26 x 6 m` |
| Airport | `(-1514.5, 14.9, -557.8)`, `146.6002 deg`, `40 x 6 m` | `(-1517.1368, 14.9, -552.7111)`, paired orientation/`40 x 6 m` |
| River | `(1433.5, -1.5, 963.6)`, `-178.5391 deg`, `90 x 9 m` | `(1433.6453, -1.4832, 969.2981)`, paired orientation/`90 x 9 m` |

`TriggerOut` sets Highway active and Dirtroad inactive; `TriggerIn` reverses
those values. After entry the struck half is disabled and both trigger volumes
are rearmed only when the player is more than `35 m` away. The `35 m` value is
debounce/hysteresis, not a nearest-route residency radius.

The project applies the locked donor-to-project translation to all five gate
pairs. One bounded runtime recovery differs from the serialized donor scene:
`traffic.state` does not persist the transient root selection, so a fresh or
loaded session deterministically starts with `VehiclesHighway`. Without this
bootstrap both roots remained dormant until a gate happened to be crossed and
normal road traffic could be absent for the whole session. A discontinuous
player move of more than `120 m` is still **not** treated as a physical gate
crossing. It repairs only a legacy/missing `None` selection to Highway and
preserves an already known Highway or DirtRoad selection. Continuous movement
continues to use the exact paired gates and `35 m` rearm rule.

Focused scene verification for this recovery found `VehiclesHighway GO26147`
and `VehiclesDirtRoad GO8268` both serialized inactive. `TriggerManager
GO29611/MB112570` is the `AIReset` FSM: its setup writes `TrafficReset` and its
global `WAKEUP` returns to that reset state; it does not serialize or restore
which of the two traffic roots was last selected. Root ownership therefore
cannot be reconstructed from that donor manager or the current project save
payload after an arbitrary load position.

This bootstrap does not analytically catch traffic up to game time. The current
project implementation deliberately differs from the donor's all-physical root
freeze/resume policy: ordinary actors in the selected root advance their
project-owned logical route state in **real time** while outside physical
residency. Game-clock jumps still do not advance route distance. Near the
player, ordinary actors materialize at `440 m`, remain resident until `520 m`,
and are limited to six physical wrappers; already resident actors win the cap
before new candidates to avoid residency churn. Offscreen ordinary collisions
are not simulated.

Pena's ordinary context and the separate Saturday context are not counted as
ordinary ambient residency candidates. By explicit user decision, Jani,
Petteri, the bus and Pena remain physically simulated independent of player
distance because their crashes and route outcomes can affect story state. This
is an approved project safety/fidelity divergence, not transferred donor
configuration. The ordinary and Saturday Pena contexts remain mutually
exclusive even though they use a separate residency policy.

In the donor, an inactive physical root freezes actor transforms and waypoint
state and resumes the same state on re-entry. The project retains this fact as
behavioral evidence but uses the bounded real-time logical policy above to
avoid an empty/dead ambient world under streaming.

### 5.3 Time contract

- Game time can choose a one-time setup branch or eligibility window.
- Runtime driving then advances from actual simulation time and vehicle state.
- A sleep/clock jump must not add `speed * skippedGameSeconds` to a route.
- A clock jump must not respawn, despawn, reroll direction, choose a new random
  start, or move a physical target to the scene origin.
- Save/load restores route identity, direction, point/progress, random decisions,
  active/fatal state, and vehicle pose. It does not infer a new pose from the
  current hour.

## 6. Actor behavior lock

### 6.1 Jani, Petteri, Suski and the two race cars

#### Eligibility and residency

- Serialized `GlobalDay` values: `3, 5, 6, 7` (the current calendar mapping is
  Wednesday, Friday, Saturday and Sunday).
- Setup hours checked by the donor: `16, 18, 20, 22, 24, 2`.
- Every five real seconds, the actor checks distance to the player.
- Full car presentation/physics is active at `<= 500 m` and disabled beyond
  `500 m`; disabling freezes the current transform and waypoint.
- Re-enabling resumes the same route state. There is no clock-distance catch-up.

#### Exact one-shot route program

The route is not the Teimo bicycle loop and not a random highway spawn. The
ordered program is:

1. `Trackfield[228..290]`;
2. `Village[0..294]`;
3. `RoadRace[0..623]`;
4. `Trackfield[0..201]`;
5. seven additional `Trackfield[73..201]` circuits;
6. `FULLSTOP`, then navigation disabled.

The captured program resolves to 2,088 ordered targets and approximately
20.47 km. Jani and Petteri launch together and travel in the same direction;
their distinct formation offsets are preserved. The dense donor sequence is
evaluated linearly rather than with Catmull-Rom interpolation, so steering does
not cut inside the already sampled Trackfield, Village or Teimo-area route.

Road settings include a `+2 m` lane target, `7 m` required target distance and
`40 m` emergency range. Highway/race speed is `115..185 km/h`. Four authored
Perajarvi handbrake/drift zones provide the small village slides; the village is
not a no-drift zone, but it is not the continuous high-speed highway race
profile either.

#### Physical and crash behavior

Locked donor Rigidbody masses are Jani `1010 kg` and Petteri `875 kg`; both use
linear drag `0.02`, angular drag `0.005`, gravity and a non-kinematic body.
Those values do not by themselves constitute a complete captured NWH tire and
suspension setup.

Jani's fatal transition is governed by a hidden `0.5 kg` `DeathForce` body at
local `(0, 0.63, -0.029)`, attached to the chassis by a `FixedJoint`:

1. break force/torque remains infinite until vehicle speed exceeds `55 km/h`
   and the player is within `1500 m`;
2. the armed break force becomes `600`;
3. only joint break enters the terminal crash state;
4. seated models are disabled and Jani plus Suski ragdolls are enabled **inside
   the car**;
5. navigation and engine are disabled.

An ordinary contact is not this terminal event. It can produce braking,
recovery, deformation later, and collision sound without ejecting Suski or
stopping Petteri.

#### Current deltas requiring acceptance

- Current physical values include project-derived interpolation and damping:
  Jani/Petteri springs `28,000 / 24,000 N/m`, dampers `2,000 / 1,700 Ns/m`,
  rest length `0.112 m`, travel `0.16 m`, and angular damping `0.16`.
- Locked centres of mass now used by the source are Jani `(0, 0.065, 0)` and
  Petteri `(0, 0.1, -0.05)`, but they still require full-route roll/pitch tests.
- Curvature/profile-specific look-ahead is required. The current intended bands
  are Perajarvi `clamp(10 + speed*0.55, 14, 32)`, RoadRace
  `clamp(18 + speed*0.85, 28, 62)`, and gravel
  `clamp(8 + speed*0.32, 10, 24)` metres. These are project tuning, not donor
  constants, and must be judged by lane fidelity rather than recorded as
  transferred configuration.
- Inside the four Teimo/Perajarvi handbrake zones, current project tuning
  shortens look-ahead to `7..10 m` for the gas-station pass and drift return.
  An anticipatory speed cap starts approximately `245 m` before the RoadRace
  exit so the cars brake before the Perajarvi transition rather than after it.
  Both values are reimplemented tuning and still require final manual route
  acceptance.
- The story collision must remain sensor/structural-failure authoritative. A
  closing velocity along a contact normal may inform an ordinary impact; total
  Rigidbody velocity magnitude must not be a shortcut into the fatal state.

### 6.2 Original highway traffic roster

`VehiclesHighway` contains exactly ten base actors:

| Donor actor | Transform evidence | Multiplicity |
| --- | --- | ---: |
| `TRUCK` | `T57659` | 1 |
| `SVOBODA` | `T66128` | 1 |
| `LAMORE` | `T51149`, `T71200` | 2 |
| `VICTRO` | `T63484`, `T59256`, `T47143` | 3 |
| `MENACE` | `T67193` | 1 |
| `FITTAN` | `T67911` | 1 |
| `POLSA` | `T44840` | 1 |

There are three distinct Fittan-related evidence domains and their names must
not be collapsed: highway ambient `FITTAN T67911`, ordinary Pena/cousin
`FITTAN T58170`, and the three differently named official `RALLYCAR` bodies.

The normal actors, including the beer truck, use `95..105 km/h`. `MENACE`
uses `115..185 km/h`. Lane is `+2 m` or `-2 m` according to route direction;
required distance is `5 m` and emergency distance is `40 m`.

Passing behavior uses mirrored drive/pass lanes (`+2/-2`), `2.5 s` lane
change, `6 s` attempt duration, `55 m` maximum pass distance, and `2 m`
tolerance. Direction, clockwise/counter-clockwise choice and initial waypoint
are randomized once on first activation and then retained.

`MENACE` begins disabled. On the first activation of the highway root it rolls
once: `10%` weekday and `40%` weekend probability. The donor does not reroll
every 60 seconds until success.

While `VehiclesHighway` is active, all ten actors simulate on the full route.
The per-vehicle approximately `200 m` LOD affects presentation, not navigation.
This is materially different from stopping or resetting each actor merely
because the player is farther from it.

Known generated-catalog mismatches at audit time:

- truck `15..23 m/s` (`54..83 km/h`) instead of `95..105 km/h`;
- other normal cars mostly `18..30 m/s` instead of `95..105 km/h`;
- Menace `22..34 m/s` (`79..122 km/h`) instead of `115..185 km/h`;
- base lane `1.65 m` instead of the locked `2 m`;
- repeated `inactiveRetrySeconds = 60` for Menace instead of one roll per
  activation lifecycle;
- generic route policy in place of the captured passing and root behavior.

### 6.3 Pena/cousin contexts: dirt-road Fittan and KUSKI

#### Ordinary dirt-road Fittan

`VehiclesDirtRoad/Rally/FITTAN GO22116/T58170` starts at donor
`(-1454.97, 5.48, -628)` and has a `690 kg` Rigidbody. Its hierarchy name must
not cause it to be confused with the three official rally cars.

Locked vehicle and navigation configuration:

- shifts `2750 / 6500 rpm`;
- speed `95..105 km/h`, acceleration command `1`, cruise command `0.1`, donor
  `DeathSpeedMPS = 5`;
- `DirtRoad` start `16`, end `3718`, lane `0`, required distance `6 m`,
  emergency distance `30 m`;
- a `RouteHome` branch, cousin lift FSM, forward raycast `200 m` with a two-second
  repeat, seven authored brake zones and `30 m` base brake distance;
- `CrashEvent FSM109666` owns the cousin accident/ragdoll/crime transition with
  `CrimeDistanceTolerance = 8`.

`FittanSpawns T45531` contains exactly eleven candidate anchors:

1. `(-836.75, 2.31, -376.15)`;
2. `(-280.69, 2.01, -811.05)`;
3. `(435.72, 1.77, -1311.43)`;
4. `(1086.9, 4.03, -932.25)`;
5. `(1849.31, 4.9, -935.15)`;
6. `(1950.67, -1.49, 19.06)`;
7. `(1537.03, 5.21, 735.69)`;
8. `(626.6, -0.85, 1285.2)`;
9. `(-505.19, 2.2, 1249.77)`;
10. `(-1432.58, 3.36, 1219.72)`;
11. `(-1454.97, 5.48, -628)`.

The donor chooses an anchor and then applies distance, kinematic and navigation
activation logic. There is no recurring clock schedule that analytically moves
this actor. `CheckRally` prevents conflict with the official rally and also
considers `CousinInJail`, `PlayerWanted` and player distance.

#### Saturday-evening KUSKI context

KUSKI root `T37302` belongs to `NPC_CARS T50786`. The root transform is local
to `NPC_CARS`; its local position must not be interpreted as a world position.
The locked lifecycle is:

- starts disabled, waits five real seconds, and is eligible on serialized day
  `6` (Saturday);
- setup hours are `18, 20, 22, 24, 2`; hours `4..16` disable it;
- proceeds only while `RallyDay == false`;
- requires player distance greater than `500 m` from the dirt-road Fittan, then
  disables that Fittan;
- requires player distance greater than `200 m` from KUSKI before off-screen
  activation;
- when `RallyDay` becomes true, retirement is delayed until the player is more
  than `500 m` away;
- its own LOD changes the visual at `200 m` but does not define route authority.

Its exact route program is:

1. `Dancehall[268..2]`, descending, lane `0`;
2. `RoadRace[0..113]`, lane `2`;
3. `DirtRoad[0..1894]`, lane `0`;
4. `Trackfield[28..290]`, lane `2`;
5. `RoadRace[3..113]`, lane `2`;
6. return to `DirtRoad[0]` and repeat.

Initial waypoint is `268`; required target distance is `6 m`. The hitchhike
branch begins around `DirtRoad[771]`, drives `HomeRoad[0..461]`, then resumes at
`DirtRoad[773]`. The initial start is randomized once per new game according to
the donor change history and must be persisted; it is not rerolled by the clock.

Base speed is `95..105 km/h`. Passing uses drive/pass lanes `+2/-2`, a `40`
speed-difference threshold, brake multiplier `0.0367`, emergency acceleration
`0.8`, `2.5 s` lane change, `6 s` attempt and `55 m` maximum distance. The
forward obstacle ray is `200 m` and repeats every two seconds.

Authored brake zones include Strawberry, FarHouse, Fleetari, School,
HomeIntersection, Home, PeraIntersection and SkiHillIntersection. Typical
detection is `30 m`; Pera uses `150 m` and Ski Hill `180 m`. Locked targets are
approximately `40 km/h` at Strawberry, `25 km/h` at FarHouse/Fleetari/School/
HomeIntersection, and `5 km/h` at Home.

The lift behavior stops when the player is within `10 m` and requests a lift;
it releases when the request clears, distance exceeds `25 m`, or after `30`
real seconds. On KUSKI crash, the donor destroys driver/navigation control,
activates ragdoll/DeathForce/blood, waits `0.3 s`, detaches the ragdoll, sends
`DEAD`, and holds throttle `0`, brake `1`, clutch `1`, gear `1`.

The current generic always-active `DirtRoad` loop, `11..23 m/s` speed and
`+/-1.1 m` lane are neither ordinary Fittan nor Saturday KUSKI parity. The
current generated KUSKI presentation also has empty crash arrays. The current
roster's separate `KUSKI scripted car` and `Dirt-rally Fittan` rows must be
reviewed as context presentations of the stable cousin role, not allowed to
produce duplicate persistent Pena actors.

### 6.4 Public bus

The bus owns a `1,084`-point, `13,698.389 m` `BusRoute` loop. Point `1083`
connects back to point `0`; the bus does not end the route.

The donor setup chooses a starting phase once according to the current hour:

| Hours | Start point / location |
| --- | --- |
| `12, 18, 24, 6` | point `3`, Perajarvi |
| `14, 20, 2, 8` | point `890`, Rykipohja |
| `16, 22, 4, 10` | point `482`, Loppe |

This is a startup phase selection, not a recurring two-hour teleport or
departure scheduler. Once started, the bus continuously drives the loop.

Locked navigation values are lane `0`, pass lane `-4`, lane change `2.5 s`,
attempt `6 s`, maximum pass distance `55 m`, tolerance `3 m`, and speed bands
`60..105 km/h`. The throttle hysteresis toggles at `80..85 km/h`; the midpoint
target is `82.5 km/h` or `22.9167 m/s`.

Projected stop distances on the locked route are:

- Loppe: `5207.049 m`;
- Kesseli: `7774.381 m`;
- Rykipohja: `12027.058 m`.

The stale `12.5 m/s` ceiling and old stop distances explain both the previously
observed slow bus and the stop behind the sewage station. Current project
tuning uses the locked `60 km/h` minimum and `82.5 km/h` target/maximum for the
physical driver, with generic wander and drift disabled. The bus is kept
physically resident by the approved story-traffic policy. These changes are
implemented and covered by automated checks pending the final traffic run; they
do not by themselves verify door, ticket, passenger, request-button, sound or
full-loop behavior.

### 6.5 Train

The donor `Move` FSM (`FSM109599`) runs this real-time loop:

1. move from SpawnEast `T38994 (2493.9, 0, 280.2)` to TargetWest
   `T41202 (-521.9, 0, -1788)` at `30 m/s`;
2. hide the train mesh and delay `250` real seconds;
3. reparent to SpawnWest `T55698 (-402.8, 0, -1706.3)` and move to TargetEast
   `T56362 (2597.59, 0, 351.3)` at `30 m/s`;
4. hide and delay `250` real seconds, then repeat.

The locomotive's visible forward axis is local `-Z`, with freight cars behind
on `+Z`; the current presentation yaw of `180 degrees` is therefore intentional.
Train movement and endpoint delay are not advanced by a game-clock skip.

### 6.6 AI boats

The locked `TRAFFIC/Lake GO29624/T65671` root owns two actors:

- `AIboat1 GO21527/T57586`, donor position `(0, -4.5, 365)`, active by default;
- `AIboat2 GO12684/T48730`, donor position `(-239, -4, 675)`, inactive by
  default.

Both have `EndWaypoint = 8`, `WaypointInt = 1`, `Logic Thrust = 350`,
`MaxEnergy = 25.667402`, rise-front rotation `0.10702024`, rise-front speed
`-0.05351012`, vertical velocity `3.202478`, target Y `-9.926451`, and a
`900 m` presentation LOD. These constants do not expose a single donor cruise
speed in metres per second; the current `8 m/s` value is project tuning.

The two eight-point arrays are:

- `Waypoints1 T64950`: `(358,0,935)`, `(-1302,0,816)`,
  `(-819.4,0,550.8)`, `(-774,0,343)`, `(-1138,0,123)`, `(-554,0,24)`,
  `(-80,0,225)`, `(886,0,149)`;
- `Waypoints2 T43822`: `(-1030,0,847)`, `(-1303,0,676)`,
  `(-994,0,291)`, `(81,0,-674)`, `(321,0,-574)`, `(-500,0,-141)`,
  `(-330,0,347)`, `(-482,0,710)`.

They are **not** two permanent independent loops. Boat 1 LOD and steering use
`Waypoints1`. Boat 2's LOD chooses a random initial child from `Waypoints2` and
copies that child index into its waypoint index; boat 2 steering then targets
the corresponding index in `Waypoints1` and joins that loop.

The Lake activation FSM waits two real seconds at initialization. Boat 1 is the
continuous actor. Boat 2 is eligible on Sunday (`GlobalDay = 7`), in daytime,
and within `700 m` of the player; it deactivates outside `700 m` or at night and
reevaluates every eight real seconds. The current documentation/runtime claim of
two permanently persistent independent loops is a parity defect.

The current runtime's Rigidbody force/turn/water-height implementation and
sanitized wrappers still require exact activation/merge behavior, wakes,
collision consequences, save handling and dedicated spatial audio. Clock skips
must not advance either route.

### 6.7 Official rally

Top-level `RALLY GO25148/T61205` owns separate Saturday/Sunday content, start
positions, brake zones, spectators, parc ferme, leaderboard, results, Rally TV
and exactly three reusable physical competitor cars:

| Car | Transform / start position | Rigidbody mass | Throttle tuning |
| --- | --- | ---: | --- |
| RALLYCAR1 | `T63136`, `(-1292.79, 0.119, 1267.68)` | `875 kg` | shifts `4200/7300`, speed target adjust `0` |
| RALLYCAR2 | `T44034`, `(-1276.13, -0.32, 1275.77)` | `1050 kg` | shifts `4500/7600`, speed target adjust `-3` |
| RALLYCAR3 | `T45376`, `(-1283.8, -0.15, 1272.29)` | `850 kg` | shifts `4400/7500`, speed target adjust `-2` |

`RallyCars GO637/T36689` begins inactive. `Reset FSM MB111197` owns event
lifecycle, `AIdrivers MB104277` selects cars, and the per-car Navigation
components are `MB105539 / MB113399 / MB106175`.

All three use `DirtRoad`, required distance `9 m`, emergency distance `35 m`,
lane `0`, speed `135..145 km/h`, acceleration `1`, cruise `0.65`, brake distance
`101 m`, and `DeathSpeedMPS = 5`. Saturday runs route indices `2168..3640`;
Sunday runs `56..1875`. Exact start anchors are Saturday
`T58349 (-1258.81, -0.37, 1281.19)` and Sunday
`T57116 (-1282.29, -1.77, -691.25)`.

The reset/lifecycle uses Saturday `GlobalDay = 6`, Sunday `GlobalDay = 7`,
serialized active hours `10..18`, and player-distance thresholds `400/200 m`.
The AI driver selector chooses one of the three physical cars and waits `80`
raw seconds before the next selection. The results table has nine competitor
names: Pasi Kuska, Tommi Yli-Jani, Patu Viska, Steba Saarikivi, Leiska Louko,
Alpo Almala, Airut &Aring;keblom, Jalmari Ilmakko and Jere Papatti. That does not
mean nine simultaneous physical cars. The FleetariRally copies at the start
area are event presentation, not additional moving competitors.

No official rally vehicle/runtime is currently implemented. It must not be
approximated by the ordinary Pena/Fittan or generic ambient logic.

### 6.8 Drag race

Top-level `DRAGRACE GO8093/T44145` owns the strip, trigger, distance point,
spawn, LOD, timing/leaderboard systems and exactly two bespoke physical cars:

- Drag1/dragcar1, donor position `(-742.154, 2.72, -903.2053)`, `1150 kg`,
  route lane `-3`, Navigation `MB110824`;
- Drag2/dragcar2, donor position `(-727.9729, 2.69, -897.855)`, `950 kg`,
  route lane `0`, Navigation `MB109546`.

Both use `Dragrace[0..165]`, required distance `9 m`, emergency distance
`35 m`, maximum speed field `500 km/h`, acceleration `0.4`, staging throttle
`0.25`, staging brake `0.5`, `DeathSpeedMPS = 5`, and shifts `4200/7300 rpm`.
The staging graph includes crawl, burnout, reverse, line-up, rev-up, tree launch
and finish. Launch delay is randomized in `2.8..3.8 real seconds`.

Opening is Friday (`GlobalDay = 5`) for serialized time values
`6,8,10,12,14,16,18,20`; `22` is not included. The outer trigger is `10 m` and
event/LOD residency uses `720/800 m`. The event can also reposition existing
Amis2, Kylajani and Menace actors at DragAmis2
`T54408 (-1113.5139, 3.143, -789.2678)`, DragKylaJani
`T61848 (-1015.1033, 2.83, -784.0542)`, and DragMenace
`T70304 (-649.1874, 3.1, -849.1772)`. Those are event anchors for existing
actors, not extra bespoke drag cars.

The current drag group and both individual drag-car rows remain
evidence-captured with no runtime implementation.

### 6.9 Police checkpoint and chase vehicles

Locked evidence confirms a checkpoint/chase system, not an ordinary free-roaming
patrol. `TRAFFIC/Police GO11890/T47936` owns three checkpoint sites and two
reusable `1330 kg` police cars:

- Airport `T50677 (339.7, -1.1, -1413.1)`;
- Ski Hill `T39536 (-169.61, 0.25, 603.31)`;
- Fields `T66836 (-134, 0, 182)`.

The master waits two seconds, runs only in daytime, and splits chance by day:
Monday through Thursday `10% on / 90% off`; Friday through Sunday
`50% on / 50% off`. Each site has one-third selection probability and root
residency is `1200 m`. Cars, breath-test officers, radar officers and checkpoint
presentation are reparented to the chosen site.

Exact car anchors are:

| Site | Police car 1 | Police car 2 |
| --- | --- | --- |
| Airport | `(345.7076, -0.857, -1418.6848)` | `(367.9433, -0.905, -1405.8883)` |
| Ski Hill | `(-1611.9026, -0.55, 366.4197)` | `(-1599.4359, -0.47, 405.3204)` |
| Fields | `(-24.6243, 9.85, 1606.7385)` | `(-41.9863, 9.16, 1594.7474)` |

Normal navigation uses Highway lane `-2`, required distance `12 m`, emergency
distance `40 m`; car 1 starts at `422` and descends to `0`, while car 2 starts
at `416` with its serialized opposite direction. Both use `95..105 km/h`, cruise
`0.2`, a `400 m` raycast repeated each second, and donor passing behavior. Chase
retunes speed to `130..170 km/h`, owns sirens and contains distance bands
including `40/60/120 m`, close brake at `5 m`, and player stop/accelerate bands
around `3/5 km/h`.

Until contrary evidence is captured, roster labels should say
"police checkpoint/chase vehicles", not claim free roaming. Police patrol and
checkpoint rows are currently unimplemented.

### 6.10 Moving event-roster completeness

A Rigidbody/hierarchy audit of the relevant roots found only two AI boats,
three official rally cars, one ordinary dirt-road Fittan context, two bespoke
drag cars and two reusable police cars. Other bodies in those roots are
official/spectator/NPC ragdolls or trigger volumes. No additional moving rally,
drag or police support car was found.

Teimo's bicycle remains owned by its NPC behavior rather than the generic
traffic runtime.

## 7. Physical driving contract

The target is a physical vehicle controlled by steering, throttle, brake,
clutch/gear and wheel forces. A route supplies intent; it does not directly drag
the transform back to a line.

Required runtime boundaries:

- route geometry selects an ordered lane target and speed/brake context;
- the AI driver converts that target to bounded controls;
- the NWH/project vehicle backend produces motion through wheel contact;
- route projection records progress and recovers after a physically plausible
  maneuver; it does not overwrite the chassis every frame;
- obstacle perception brakes, waits and attempts a bounded reverse/steer-around
  recovery; it never teleports behind the obstacle or unloads merely because it
  is blocked;
- root streaming freezes/resumes only where the original root does so;
- a non-terminal impact posts collision feedback and remains recoverable;
- terminal story crashes are actor-specific state transitions.

This contract rules out the observed "script pulls the car onto the line"
motion even when the resulting transform happens to match the route.

## 8. TrafficCarExpansion behavioral reference

### 8.1 What can be adopted as a project-owned extension

TCE's `AiNavigation` controls the donor `CarController` from Rigidbody velocity.
It targets a child of the current waypoint at local `(LanePosition, 0, 0)`, uses
lane `2`, required distance `5`, and steering approximately
`clamp(atan2(localX, localZ) * Rad2Deg / 25)`. Throttle is governed by
minimum/maximum speed hysteresis.

Forward obstacle-ray distances vary with speed:

| Speed | Ray distance |
| --- | ---: |
| below `50 km/h` | `22 m` |
| below `75 km/h` | `26 m` |
| below `105 km/h` | `34 m` |
| below `120 km/h` | `41 m` |
| `120 km/h` and above | `50 m` |

Passing retains donor-like `+/-2 m` lanes, `2.5 s` lane transition, `6 s`
attempt and `55 m` maximum distance.

The optional Perajarvi/fuel excursion is evaluated every `90 real seconds` with
a `4%` chance, only for actors in lane `-2`. Names containing `gifu` or
`hayosikopace` are excluded and only one excursion is active at once. Entry uses
Highway points `1699..1704` and requires speed below `130 km/h`.

The extension then follows:

1. `IntoTown[0..67]`, lane `0`, approach at `15..20 km/h` then `45..55 km/h`;
2. `65%` chance of `TownLoop[0..320]`, or `35%` chance of
   `GasPump[0..110]`;
3. fuel occupancy check at donor `(-1560, 3, 1176)`, radius `3 m`, collider tag/
   identity `carcollider`;
4. fuel approach `20..25 km/h`, then `5..10 km/h`, wait `45..60 real seconds`;
5. return through Highway points `1658..1888`, lane `-2`.

Signals and braking are part of this branch. This is the evidence-backed basis
for an optional fuel-station/town excursion; a generic random side offset is not.

TCE spawning defaults to min `10`, max `20`, desired `10`; night range is
`8..10`. Occupied spawn points use a `2 m` check and a `3 s` retry. Initial
throttle is applied for `8 s` (`0.85`, or `0.5` when max power is at least
`230`). These spawning values are TCE behavior, not original base-roster parity.

TCE collision audio reacts on first contact with donor road/world layers when
relative velocity magnitude exceeds `1 m/s`. It selects among
`crash_hi1/hi2/low1/low2` and clamps volume from relative speed divided by five
to `0.25..1.25`. This is ordinary collision feedback, not a story-fatal test.
The recreated equivalent remains spatial and routes through `IAudioBackend`.

TCE destroys and regenerates its cars when its spawner root disables/enables.
That mod behavior must **not** replace the original freeze/resume and no-clock-
catch-up contract for the locked base traffic roster.

### 8.2 Full-route extraction decision

At audit start the TCE manifest kept every fourth point. That historical data
was not suitable for physical steering:

| Route | Full points | Full segment min / average / max | Historical stride-four points | Historical min / average / max |
| --- | ---: | --- | ---: | --- |
| IntoTown | 68 | `2.886 / 4.604 / 5.673 m` | 18 | `11.539 / 18.103 / 22.059 m` |
| TownLoop | 321 | `0.028 / 5.098 / 8.571 m` | 81 | `3.668 / 20.319 / 33.041 m` |
| GasPump | 112 | `0.000218 / 3.708 / 7.546 m` | 29 | `1.129 / 14.608 / 28.756 m` |

**Decision and current status:** preserve all `68 / 321 / 112` ordered source
points as evidence. The working-tree manifest, generator validation and EditMode
coverage now implement this replacement with `sourcePointStride = 1`, while
retaining legacy-stride metrics that quantify the prior defect. A Unity catalog
rebuild and physical excursion test are still required. The former `20..33 m`
chords cut corners and could directly cause shoulder driving.

Deterministic extraction method:

1. hash the supplied DLL;
2. read its embedded `bundle` with Mono.Cecil into external staging and hash it;
3. export the bundle with AssetRipper into external staging and hash
   `Routes_Modded.prefab`;
4. parse direct child transforms under Route_IntoTown `T470262`,
   Route_TownLoop `T417927`, and Route_GasPump `T457141`;
5. order children by serialized `m_RootOrder`, not file ID or object name;
6. preserve source index and coordinates, validate finite values/counts, then
   apply the locked donor-to-project translation;
7. commit only project-owned manifests/configuration and provenance, never the
   raw mod bundle/prefab.

Root order is mandatory because AssetRipper produced duplicate Transform file ID
`410764` at TownLoop orders `21` and `56`, while GasPump order `33` has the
misleading GameObject name `50`.

The source also contains near-duplicates: TownLoop `0->1` is `0.028 m`, and
GasPump `32->33`/`33->34` are `0.002266 m`/`0.000218 m`. The existing route
validator rejects squared distance below `1e-6`, so the full source cannot be
silently dropped into the current runtime array. Preserve a raw ordered evidence
array, then derive steering knots with a documented epsilon-collapse rule, or
advance across coincident targets with a bounded loop. Never discard source
points without retaining the authoritative raw sequence and provenance.

## 9. Current mismatch matrix

| Domain | Locked behavior | Current state at audit | Disposition |
| --- | --- | --- | --- |
| Traffic time | Frame-driven; no clock-distance catch-up | Runtime ignores game-time jumps; selected-root ordinary actors advance only from real runtime seconds | Implemented/automated validation passed; manual time-skip acceptance remains open |
| Root residency | Five exact paired trigger gates; Highway/Dirt root freeze/resume | Project divergence: selected-root ordinary actors advance logically in real time offscreen, use retained-first `440/520 m` residency with a six-wrapper cap, and do not simulate offscreen contacts; Pena/Saturday contexts remain separate | Implemented/automated validation passed; preserve mutually exclusive cousin contexts and official rally distinction |
| Jani/Petteri route | Full one-shot Trackfield/Village/RoadRace program | Route exists, but physical lane fidelity and full completion are not manually accepted | P0 complete-loop telemetry and video comparison |
| Jani fatal crash | Armed hidden joint; both ragdolls inside; engine/navigation stop | Structural sensor exists, but ordinary collision had been able to trigger terminal logic | P0 sensor-only terminal authority and cabin-ragdoll test |
| Highway roster | 10 exact actors, `95..105`; Menace `115..185`; one activation roll | Multiplicity exists, but speeds/lane/probability cadence are stale/generic | P0 rebuild definitions; P1 passing/probability acceptance |
| Pena contexts | Ordinary Fittan: 11 spawn anchors, DirtRoad/Home/lift/crash; KUSKI: Saturday-evening multi-route context | One generic always-active KUSKI dirt loop; ordinary Fittan context absent; no complete crash binding | P1 one stable cousin role with two exact mutually exclusive contexts |
| Bus | 22.9167 m/s (`82.5 km/h`) target; `60 km/h` minimum; three exact projected stops; one-time start phase | Physical driver now uses `60..82.5 km/h`, zero generic wander/drift and permanent physical residency | Implemented/automated validation passed; full-loop ride/manual service test remains open |
| Train | 30 m/s, 250 real-second hidden waits | Core values captured; collision/audio/manual traversal incomplete | P2 validation and feedback |
| Boats | Boat1 continuously uses WP1; Sunday/daytime Boat2 starts at WP2[n] then joins WP1[n] | Runtime/documentation model two permanently persistent independent loops | P0 data/identity correction; P2 force, wake, save/stream and audio acceptance |
| TCE routes | Full 68/321/112 source order | Working-tree manifest/generator/tests now use all points; generated Unity catalog and physical drive are not yet accepted | Implemented in source; P0 rebuild and excursion test |
| Presentation | Body + intended visible wheels only | Whole-root selection imports donor mechanics as static renderers | P0 allowlist and deterministic wrapper rebuild |
| Audio | Per-emitter spatial engine/music/skid/collision | Some source mappings exist; manual playback/attenuation absent or stale | P1 rebuild and spatial acceptance |
| Rally/drag/police | Three reusable official rally cars, two bespoke drag cars, two reusable checkpoint/chase police cars | Evidence-captured only; no event runtime | Later owning milestones, still mandatory before Phase 1 gate |

## 10. Implementation order

### P0 — fix before the next comprehensive traffic test

1. Replace whole-root presentation capture with explicit renderer allowlists and
   rebuild all generated wrappers.
2. Rebuild the road catalog with exact bus target/stops, base traffic speed/lane
   values, one-roll Menace policy, correct Highway/Fittan/KUSKI ownership, and
   full TCE route evidence. The full-point manifest/generator part is complete
   in source; the generated Unity catalog still must be rebuilt.
3. Make game-clock events non-mutating for already-running route state and add a
   save/load/time-jump regression test.
4. Make Jani terminal failure structural-sensor-only; activate Jani and BetterMSC
   Suski ragdolls inside the car and stop only Jani's engine/navigation.
5. Complete exact gate-based root residency without origin fallback,
   nearest-route remap, blocked-car unload or teleport recovery.
6. Drive Jani/Petteri from their full route with bounded curvature look-ahead
   and physical controls; record progress from the chassis instead of pulling
   it to the line.

### P1 — complete base-road behavior

1. Calibrate car and heavy-vehicle suspension, tire stiffness, anti-roll,
   centre of mass, gears and engine control using repeatable physical tests.
2. Implement/verify base passing, obstacle detection and non-teleport recovery.
3. Implement one stable Pena/cousin role across the exact ordinary Fittan and
   Saturday KUSKI lifecycles, routes, brake zones, lift and crash behavior.
4. Verify all per-emitter engine/music/skid/collision events and attenuation.
5. Add the TCE town/fuel excursion only as an explicitly identified extension
   on top of a correct original base roster.

### P2 and event-owned work

1. Complete boat force/water calibration, activation/route merge, wake/audio and
   persistence.
2. Complete train collision, warning/audio and traversal validation.
3. Implement the audited official rally, drag and police checkpoint/chase
   systems in their owning milestones; do not fold them into ambient AI.

## 11. Acceptance tests for the next build

Run tests from a fresh save and again after save/load. Keep the Unity Console
visible and capture vehicle telemetry for route ID/index, target distance,
speed, gear, RPM, steering, throttle, brake, wheel contact, root state and
terminal-state reason.

1. **Presentation idle test:** inspect Petteri, each representative ambient car,
   KUSKI, beer truck and player Gifu from all sides. No axle/shaft/tandem-arm
   duplicate may protrude. All intended tires/rims/hubs remain visible.
2. **Jani/Petteri launch:** on an eligible fresh start, verify the exact
   Perajarvi formation, same travel direction and no origin spawn.
3. **Village lane test:** follow through Perajarvi. Both cars remain on the road,
   preserve small authored drift zones, avoid the Teimo pole, and do not receive
   transform corrections.
4. **Full route test:** observe the complete one-shot route through final
   `FULLSTOP`; no reset to start, route reversal, forest excursion or generic
   Highway loop.
5. **Ordinary obstacle test:** place a cube in the lane. Cars brake and attempt a
   bounded physical recovery; they neither teleport through/behind it nor
   unload because blocked.
6. **Light crash test:** contact below the structural condition posts a spatial
   impact sound and remains recoverable. Suski stays seated and no fatal flag is
   set.
7. **Fatal Jani test:** meet the locked arming/failure condition. Jani and Suski
   ragdoll in the cabin, engine/navigation stop, Petteri continues, and neither
   character appears at origin.
8. **Clock jump test:** note several actor route indices/poses, skip time, and
   confirm no movement/reroll/respawn occurs solely from the clock event.
9. **Streaming test:** cross each root gate in both directions, move beyond the
   `35 m` rearm distance, and return. Actors resume their frozen root state; no
   route reset or physical/logical split occurs.
10. **Bus full-loop test:** verify approximately `82.5 km/h` cruise behavior,
    stops at Loppe/Kesseli/Rykipohja, loop continuity and save/load during dwell.
11. **Pena context test:** ordinary dirt-road Fittan selects one of 11 anchors,
    follows DirtRoad/Home/lift/crash behavior, then the Saturday KUSKI context
    replaces it only under its exact off-screen conditions. No duplicate cousin
    car or identity appears.
12. **Spatial audio test:** engine/music/skid/impact are audible near the actor,
    attenuate by configured range, do not restart on a harmless stream boundary,
    and are not audible across the map.

No item is `Verified` from an EditMode assertion alone. The physical tests above
must be executed in the production bootstrap with the rebuilt generated assets.

## 12. Renderer allowlist evidence

The safest importer contract is a role-based allowlist: body/interior/glass/
driver presentation plus explicit visible tire/rim/hub components. A name-only
denylist is insufficient because donor roots contain repeated generic names and
duplicate vehicle variants.

### 12.1 Petteri selected roots

Keep visible wheels:

- FL tire/rim `MR73276 / MR72823`;
- FR tire/rim `MR77830 / MR78498`;
- RL tire/rim `MR73786 / MR73994`;
- RR tire/rim `MR75169 / MR78892`.

Exclude from static presentation:

- RL shaft `MR74394`, RL spindle axle `MR78124`;
- RR shaft `MR76836`, RR spindle axle `MR81098`.

`_gfx MR73185` and `racing exhaust(Clone) MR72430` belong below separate
`T61351`, itself below `Boxes T44916`; they are not below selected wheel root
`T61349`. They should remain excluded from the current Petteri spec, but they
are not the source of a renderer captured through `T61349`. This distinction is
important when diagnosing the protruding rear-wheel geometry.

### 12.2 KUSKI representative root `T37302`

Keep tires/rims:

- FL `MR81161 / MR79201`;
- FR `MR81177 / MR74306`;
- RL `MR76590 / MR81569`;
- RR `MR75635 / MR78464`.

Exclude static mechanics: RL axle `MR79146`, RL halfshaft
`SMR101926`, RR halfshaft `SMR101915`, and RR axle `MR80967`.
Crash/dead/passenger renderers must be selected by lifecycle role rather than
blindly copied from the whole root.

### 12.3 Beer truck representative root `T57659`

Keep the six visible tire/rim/hub groups:

- FL `MR74440 / MR77273 / MR78981`;
- FR `MR81661 / MR79598 / MR81438`;
- RL1 `MR81646 / MR75966 / MR81055`;
- RR1 `MR78854 / MR73887 / MR80662`;
- RL2 `MR76727 / MR75578 / MR74234`;
- RR2 `MR77276 / MR81506 / MR79060`.

Exclude static mechanics: rear axle `MR73192`, tandem arms
`MR80849 / MR78329`, and LOD driveshaft `SMR101730`. The front-axle renderer
`MR73121` has no assigned donor mesh and is therefore absent from the active
static-renderer slice rather than listed as an exclusion. The simulation currently
chooses only FL/FR/RL2/RR2 contact points; that is a provisional four-contact
model and is not permission to remove the middle visible wheel pair.

### 12.4 Other selected ambient representatives

| Root | Keep visible tire/rim renderers | Exclude static mechanics |
| --- | --- | --- |
| VICTRO `T63484` | `73586/82001`, `79998/73385`, `72393/74178`, `79337/73855` | RR axle `81106` |
| LAMORE `T51149` | `79212/73970`, `76642/75997`, `79804/78832`, `81612/79044` | RL/RR axles `77778/76541` |
| POLSA `T44840` | car `78326/81392`, `73111/81206`, `77874/74533`, `80932/76494`; caravan `78282/73860`, `80100/77238` | car RR axle `75911`; caravan RR axle `77825` |
| FITTAN `T67911` | `74785/72488`, `79819/73354`, `77002/77207`, `78243/77411` | RL/RR axles `74690/80596` |
| SVOBODA `T66128` | `81399/74501`, `80612/80815`, `76597/76999`, `76076/77493` | rear shafts `78029/77713`; axles `74727/77033` |
| MENACE `T67193` | `77357/79140`, `77445/79747`, `76567/73461`, `75345/72369` | spindle brakes `81371/76416/74411/76043`; RR axle `77769` |

The second Lamore and remaining Victro instances require role/name validation
against their own roots; renderer component IDs from one representative must
not be applied to a different instance blindly.

### 12.5 Player Gifu reference root `T48011`

If the same sanitizer is reused for the player Gifu, keep visible groups:

- RL1 `MR81086 / MR78541 / MR78702`;
- RR1 `MR78385 / MR74669 / MR72451`;
- RL2 `MR77633 / MR72931 / MR77146`;
- RR2 `MR78314 / MR80691 / MR77394`;
- FL `MR72993 / MR77790 / MR73622`;
- FR `MR78178 / MR74606 / MR78885`.

Exclude from static body capture: rear axle `MR75921`, tandem arms
`MR77971 / MR73918`, front axle `MR76719`, driveshaft `SMR101763`, and pump
driveshaft `SMR101842`. The last two may later be retained only as explicit
articulated presentation. The similarly named menu object `Gifu T60204` is not
the vehicle root.

## 13. Known evidence gaps and non-claims

- NWH parameters are a project-owned adaptation. This audit does not claim that
  current tire, spring, damper, anti-roll, gearbox or engine curves are exact
  donor values unless a value is explicitly identified above.
- TCE is not an original-game authority for actor count, lifecycle, time skip,
  base route residency or event behavior.
- The rally, drag and police lifecycle/configuration is now hash-locked here,
  but human-readable decoding of every serialized PlayMaker transition and all
  player-facing results/consequence flows remains incomplete.
- No free-roaming police lifecycle is claimed; the locked scene proves
  checkpoint placement and subsequent chase only.
- The donor does not serialize a single boat cruise speed in metres per second;
  final force/speed calibration still needs a donor comparison.
- Current audio assets/event mappings do not prove audible output or attenuation;
  production-bootstrap listening tests remain mandatory.
- Generated donor presentation is `TemporaryDirectImport`, private Phase 1 only,
  and remains replaceable in Phase 2 without changing stable IDs or simulation.
- This document does not mark any traffic feature `Verified`. It records the
  behavior target and the discrepancies that must be closed first.
