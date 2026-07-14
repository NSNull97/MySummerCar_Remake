# Current Repository State — Milestone 4 complete

Captured: 2026-07-14

Open workspace: `E:\GAYmDev_Studio\MySummerCar_Remake`

## Project baseline

| Item | Current state |
|---|---|
| Unity | `6000.3.11f1 (3000ef702840)` |
| Render pipeline | HDRP `17.3.0`, Linear color space |
| Input | Input System `1.19.0`; project-authored M4 action map |
| Build scenes | Bootstrap first, M4 PlayerInteractionPrototype second, M3 GarageArtPrototype third, existing Outdoors fourth |
| Runtime boundary | Independent Unity 6 runtime; no donor executable/assemblies/assets required |
| Tests | Post-review fix gate: EditMode `48/48`; PlayMode `5/5`; foundation, donor, M3 and M4 validators pass in an isolated copy using Unity `6000.3.11f1` |

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

## Next boundary

The next recommended milestone is exactly `Prompts/04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md`. It may use the completed 04A pilot as evidence, but must not reinterpret it as a complete-world database or silently promote the blocked combined terrain mesh. No 04B or Milestone 5 work has started.
