## Phase model and Legacy Feature Complete gate

The project has two mandatory development phases.

### Phase 1 — Legacy Feature Complete

Before production remastering/polish begins, recreate the complete selected donor
version as a playable private build on the new runtime.

Phase 1 requires all donor-evidenced NPCs, vehicles, items, mechanics, jobs,
services, economy, story/event chains, media/minigames, save domains, and
progression paths. A vertical slice is not sufficient.

Phase 1 prioritizes feature completeness, runtime independence, correctness,
saveability, and donor identity over production-quality art.

### Phase 2 — Production Remaster and Polish

Only after the Phase 1 gate is explicitly approved may the project replace the
Legacy presentation baseline with newly authored production geometry, materials,
textures, rigs, animations, audio, vegetation, collision and expanded remake-only
features.

Do not begin Phase 2 merely because one vertical slice looks polished.

## Temporary donor gameplay presentation baseline

During private Phase 1 development, sanitized donor-derived presentation content
may be used as `TemporaryDirectImport` beyond the world baseline.

Allowed temporary presentation categories:

```text
Assets/Game/LegacyImport/RuntimeBaseline/Characters/
Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/
Assets/Game/LegacyImport/RuntimeBaseline/Items/
Assets/Game/LegacyImport/RuntimeBaseline/Animation/
Assets/Game/LegacyImport/RuntimeBaseline/Audio/
Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/
```

This may include donor-derived meshes, textures, materials, rigs, compatible
animation clips, audio clips, icons, transforms, colliders and presentation
metadata needed to make the Phase 1 build visibly and audibly complete.

Mandatory rules:

1. Donor code, `MonoBehaviour`s, PlayMaker FSMs, controllers, runtime assemblies,
   old `UnityEngine` references, Steam/platform/DRM logic, save managers and
   gameplay state machines remain forbidden.
2. Project-owned definitions, controllers, stable IDs, DTOs, services and state
   machines are authoritative.
3. A project-owned wrapper prefab/presenter owns every temporary visual/audio
   binding. Gameplay must not search donor hierarchy names or paths.
4. Each temporary asset has provenance, source hash when practical,
   `TemporaryDirectImport` classification and a production replacement key.
5. Raw extraction and generated donor payload remain outside Git. Commit only
   project-owned tools, manifests, mappings, metadata and newly authored code.
6. Temporary donor presentation may appear only in explicitly private local
   Phase 1 builds. It is not `ProductionReady` and is excluded from any
   distributable/public profile without explicit rights.
7. Temporary donor animation clips may drive project-owned Animators/presenters,
   but donor AnimatorControllers/FSM logic are not runtime authority.
8. Temporary donor audio may be routed only through `IAudioBackend` and the
   project event map. No gameplay system may depend on a clip filename.
9. Replacing presentation content in Phase 2 must not change stable IDs, gameplay
   coordinates, save schema or event identity.
10. Missing production polish is acceptable in Phase 1; missing required donor
    content or mechanics is not.

## Feature parity authority and tracking

- Lock one exact donor game version using hashes/build evidence.
- Maintain `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv` as the authoritative
  scope ledger.
- Every feature row requires evidence, implementation status, save coverage,
  presentation status, tests, known differences and owning milestone.
- Do not mark a row `Verified` from code inspection alone; execute the relevant
  comparison or play flow.
- Do not move to Phase 2 with unknown or missing critical rows.
- Examples in prompts are discovery categories, not an exhaustive donor-content
  list. The donor audit is authoritative.

## Phase 1 scope control

Do not add remake-only systems during Phase 1 unless required for safety,
runtime independence or testability. Put enhancements in the Phase 2 backlog.

Examples normally deferred to Phase 2:

- production art reauthoring;
- full-body/physical hands;
- swimming when absent from the selected donor version;
- deep dynamic paint/rust/bodywork;
- production vegetation replacement;
- expanded NPC intelligence or new relationships;
- new jobs, map areas, seasons or story content.

## Phase 1 completion gate

Phase 1 is complete only when:

- the parity matrix is closed;
- all required NPCs, vehicles, mechanics, jobs and progression are playable;
- full-game save/load coverage exists;
- a representative full-game playthrough has no critical blocker;
- a private Windows build runs independently from donor runtime files;
- the user explicitly approves the Phase 1 gate.
