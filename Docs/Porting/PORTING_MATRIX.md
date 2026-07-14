# Porting Matrix — Milestone 4

Status: controlled reference pipeline, Milestone 3 garage art prototype and the clean-room Milestone 4 player/interaction slice are complete. M4 did not inspect or transfer donor input/FSM code, constants or configuration; player and interaction are `Reimplemented`. Two donor meshes remain `ReferenceOnly`. No donor visual asset is classified as production-ready.

Evidence basis: donor file inventory, serialized-file headers/strings, managed assembly names, reflection-only type/member metadata, AssetRipper 1.3.14 object export, staged hashes, and Unity validation.

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
| Time/needs | No reliable dedicated non-PlayMaker type identified; likely serialized FSM variables/actions in GAME scene | Very high | Save, UI, player state, world events, weather, audio | Behavioral capture and clean scheduled simulation; transfer only observed rates/thresholds/config | High | M7.1 | Time scale, sleep skip, hunger/thirst/stress/fatigue/urine formulas, death/failure transitions |
| NPCs/traffic | SWS spline/bezier movement types; serialized scene paths/FSMs; no authoritative NPC-domain class inventory | Very high | World splines, schedules/time, physics, audio, save state, scene loading | Defer population; preserve routes/timing as world-layout/behavioral reference; reimplement a small traffic slice only when needed | Very high | Post-vertical-slice except minimal traffic proof | Agents, spawn/despawn, schedules, route graphs, AI state, persistence, collision rules |
| World/terrain/roads | `mainData`, `level0`–`level3`, shared assets; scene paths; SWS spline/bezier types; largest level is 104 MB but mapping is unproven | Very high | Terrain/meshes, colliders, splines, vegetation, streaming, persistent entities, weather | Controlled extraction of layout/measurements; rebuild terrain/roads/assets; use streaming cells/additive scenes and project-owned IDs | Very high | M2 reference proof; M3 garage slice; M7 expansion | Scene mapping, coordinate origin/scale, terrain format, road centerlines, cell boundaries, key-location transforms |
| Weather | `CameraFog`, `RainNearClip`, `SetRainClip`, `windshield.RainType`, PlayMaker weather actions, serialized assets | High | Time, camera, particles, materials, audio, indoor/outdoor state, save | Behavioral capture and HDRP reimplementation; transfer only intensity/timing/visibility observations | High | M7.2 | Weather state machine, transition durations, wetness persistence, indoor detection, wind/cloud parameters |
| Audio | Shared-assets sound containers/derived sound directories; `MasterAudio`, `EventSounds`, `SoundController`, playlists; Unity Audio | High | FSM events, player/vehicle/world state, mixer, listener, save settings | Treat clips as reference/prototype only with ledger; reauthor final soundscape; new `IAudioBackend` plus Unity fallback, Wwise later | High | M1 boundary; M6 parameters; M8 Wwise | Event map, clip provenance, mixer routing, RPM/load layers, interior/exterior logic, licensing/authorship |
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
