# Current Repository State — Milestone 05 complete

Captured: 2026-07-14

Open workspace: `E:\GAYmDev_Studio\MySummerCar_Remake`

## Project baseline

| Item | Current state |
|---|---|
| Unity | `6000.3.11f1 (3000ef702840)` |
| Render pipeline | HDRP `17.3.0`, Linear color space |
| Input | Input System `1.19.0`; project-authored M4 action map |
| Build scenes | Bootstrap first, M4 PlayerInteractionPrototype second, M05 VehicleAssemblyPrototype third, M3 GarageArtPrototype fourth, existing Outdoors fifth |
| Runtime boundary | Independent Unity 6 runtime; no donor executable/assemblies/assets required |
| Tests | M05 focused EditMode `22/22`, focused PlayMode `8/8`, full PlayMode `17/17`; full EditMode `102/103` with the known unrelated 04A1 donor-hash drift; foundation, donor, M3, M4 and M05 validators pass |

## Milestone 4 runtime state

`MSC.Interaction.Runtime` now owns explicit interaction capabilities, target hosts, bounded raycast query, physical pickup targets, carry physics and carried-object snapshot schema v1. `MSC.Player.Runtime` owns CharacterController movement/crouch, camera yaw/pitch, Input System intent routing, interaction orchestration and debug overlay.

The generated prototype content is:

- `Assets/Game/Player/Content/Input/M4_Player.inputactions`;
- `Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab`;
- `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`;
- `Assets/Game/Player/Content/Materials/M4_InteractionDebug.mat`.

The scene contains light and heavy stable-ID Rigidbody targets, contextual interaction, tool activation and an isolated mount handoff receiver. It reuses the project-owned M3 neutral lighting prefab. No M4 production dependency points to `LegacyImport/ReferenceOnly` or `Imported/DonorGenerated`.

## Ownership and boundaries

- Input produces intent; it does not own gameplay state.
- Candidate discovery is a bounded physics query, not a scene-wide/name lookup.
- The held Rigidbody is explicitly excluded from the query so it cannot hide a mount; unrelated colliders remain occluders.
- Target behavior is exposed through explicit concrete capabilities registered by `InteractionTargetHost`.
- Carried bodies are not parented to the camera.
- Carry ownership restores Rigidbody and collision state on explicit release, disable and destroy.
- The stable-ID snapshot is domain data only; file storage, entity resolution and load application remain in the future Save implementation.
- Mount handoff does not implement compatibility, constraints, fasteners or assembly state.
- `PlayerInteractionController` implements `IInteractionService`; `GameServiceBindings.CreatePartial` can register implemented milestone services without fake future implementations or exposing a service locator.

## Preserved repository state

The worktree was dirty before M4. Existing user modifications to `.gitattributes`, `.gitignore`, `Assets/OutdoorsScene.unity`, HDRP assets and ProjectSettings were preserved. M4 intentionally changes only its own runtime/editor/tests/content/docs plus `ProjectSettings/EditorBuildSettings.asset` and relevant asmdef references. Machine-local configuration and generated test/log outputs remain ignored.

M4 used no donor data and made no donor filesystem changes. The existing donor audit contamination warning and exact M2/M3 provenance remain unchanged.

## Known limitations

- No jump, sprint, full-body presentation or IK.
- Carry spring, mass limits and throw impulse are prototype tuning, not donor-calibrated configuration.
- Placement collision uses a conservative bounds overlap approximation.
- Tool activation has no `ToolDefinition`, inventory or compatibility rules.
- Prototype mount accepts any eligible pickup object and becomes occupied; no unmount flow exists.
- Save snapshot capture exists, but storage/load/resolution/migration are not implemented.
- A manual Game View feel/collision review remains necessary.
- The original working tree remains intentionally dirty with pre-existing Unity/settings/prompt-pack changes; no automatic discard, stash or mixed commit was performed.
- A representative integrated player + world GPU/memory/physics performance capture does not yet exist; creating that slice belongs to the bounded integration milestone rather than this fix task.

## Milestone 04A world-layout pilot state

`MSC.World.Runtime` now contains serializable, project-owned layout records and pure coordinate helpers for exactly one bounded garage-road pilot. Durable data is stored at `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json`; it records the garage anchor, seven `DirtRoad` route samples, numeric bounds, source hashes/PathIDs, stable IDs, conversion, dependencies and limitations.

The external staging manifest contains metadata only and is bound by SHA-256. The comparison scene is generated under ignored `Assets/Game/LegacyImport/ReferenceOnly/Comparison/`, is absent from Build Settings and can be removed without breaking production content. The M3 garage and road prefabs are used only as an unchanged visual overlay; the project road is not claimed to match the measured `204.768 m` garage-to-nearest-sample relationship.

No Player or Interaction source, prefab, scene, input asset or assembly definition changed during 04A. No purpose-specific production scene, terrain, building, vegetation, water, vehicle, weather, audio or save implementation was added.

## Milestone 04B reference-data state

`MSC.Core.Runtime` now owns the donor-independent reference schema, stable IDs, deterministic serialization, migration, unit conversion and validation. The dataset is `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureDatabase.json`; separate remake tuning is `ReferenceTuningOverrides.json`. It contains no donor binary payload and performs no donor file I/O.

`MSC.Editor` owns the dashboard/import/manual-observation/evidence-resolution workflow and batch validator. Absolute roots are resolved only from ignored local configuration. `Docs/ReferenceCapture/` owns procedures, index, source map, missing queue and session log. Raw screenshots, video and audio stay outside Git.

The current dataset `04B.4` contains 40 measurements plus 11 behavior fixtures. Static world/player/vehicle facts are imported where sources are unambiguous. The representative rear-left drum has a traced candidate trigger, install/removal gates, one `BoltPM` marker, discrete `0..8` endpoints, wrench `14` and scroll mapping, three clean runtime repetitions and a user-confirmed wheel-installed removal blocker. Both assembly requirements are `Covered` and the fixture is `Ready`. Runtime sprint/crouch/interaction, powertrain, vehicle dynamics, time/weather, audio and UI values remain explicitly `Missing`/`Partial`; fitted-wheel identity and curb mass also remain `Partial`. M4 tuning is not reclassified as donor evidence.

## Milestone 05 assembly state

`MSC.Vehicle.Assembly` owns immutable definitions, mutable runtime state, mount/fastener instances, explicit dependency graph, deterministic queries, operation results, validation and schema-v1 DTOs. It depends only on Core and Interaction. Player and Interaction keep their M4 responsibilities and communicate through the existing handoff/tool/context capabilities.

The shared `M4_FirstPersonPlayer.prefab` also owns a presentation-only `CrossdotPresenter`: a permanent centered dot with a contrast outline that identifies the exact camera-ray direction without changing candidate selection or interaction state. The updated builder is version `1.1.0`; focused Player/Interaction tests pass `8/8` EditMode and `5/5` PlayMode, and both M4 and M05 validators accept the updated prefab.

Post-M05 scale correction makes installation preserve a part's world scale even when consuming an older scaled mount pose. Vehicle Assembly builder `1.1.0` additionally separates unit-scale logical mount transforms from scaled debug marker geometry. Focused M05 PlayMode passes `8/8` both against the current scene and a freshly generated scene.

The reproducible scene `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity` contains 15 clean project-authored prototype parts, 14 mount points and the M4 player prefab. The rear-left drum consumes the `04B.4` behavioral fixture without donor runtime dependencies. Static validation, 22 focused EditMode tests and 8 focused PlayMode tests pass; the 10 000-query performance audit records 0 managed allocations and 0 graph mutations.

## Next boundary

## Milestone 05A bounded world-remaster state

The production layer now has a full 13,509-record replacement registry, 51 discovered zone/status groups, 263 grouped manual-art tasks and a deterministic pilot production cell for `cell_0_-3`. The pilot contains project-authored terrain/road/ditch, home and garage shells, a representative interior, six moving hinges, props/infrastructure and 64 LOD spruce instances. It is integrated with the existing M4 player and M05 assembly scene.

Coverage is deliberately narrow: 24 direct bindings globally (0.178%) and 24/671 in the pilot source cell (3.577%). Four records are `ProductionCandidate`, twenty are `FirstPass`, none is `Approved`/`Verified`, and the other 13,485 records remain `Unassigned` with explicit backlog links. Production dependencies are donor-binary independent and the two-run production-cell SHA-256 is stable.

The next recommended milestone is exactly `Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md`, after manual acceptance of the pilot. Final terrain/road measurements, hero modelling, authored textures, map-scale vegetation/water/infrastructure, HLOD and standalone GPU profiling remain explicit work.

The freshly reinstalled donor installation has different `sharedassets3.assets` / `.resource` hashes from the frozen 04A1 extraction provenance. The 04A1 records were intentionally not rewritten; current full EditMode regression therefore has one expected world-transfer provenance mismatch until a separate audited extraction/reconciliation is performed.
