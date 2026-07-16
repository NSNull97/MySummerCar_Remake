# MSC REMAKE — FIXED DESIGN GUARDRAILS

These constraints are authoritative for this prompt pack unless the user
explicitly changes them.

## Product direction

- The first large goal is **donor feature parity on a new Unity foundation**.
- The result should feel like the original My Summer Car rebuilt today, not a
  different rural driving game inspired by it.
- Preserve recognizable layout, systems, pacing, difficulty, interaction logic,
  progression, humor, and inconvenient-but-intentional gameplay.
- Separate donor parity, technical modernization, temporary donor baseline, and
  production remaster work in architecture, backlog, reports, and acceptance
  criteria.
- Complete a playable parity baseline before broad new content or full art
  replacement.

## Evidence hierarchy

For donor-fidelity questions, use this priority:

1. Real original-game runtime captures made from the donor build.
2. Original serialized transforms/data and verified extracted geometry.
3. The canonical sanitized donor runtime baseline.
4. Verified BeamNG/reference-port geometry for scale/layout only.
5. Project-owned measurement fixtures created from the above.
6. Human notes explicitly approved by the user.

The following are **not valid authority for layout, architecture, object
placement, proportions, terrain profile, or landmark identity**:

- AI-generated concept art;
- remake mood boards;
- generic Finnish references;
- artist memory;
- procedural generation without donor constraints;
- an attractive result that merely feels plausible.

Concept art may inform rendering mood and material quality only after donor
identity is already correct.

## Temporary donor world runtime baseline

- The original map has already been extracted and visually inspected.
- Reuse the canonical existing extraction/import; do not create duplicate full
  imports without evidence.
- During feature parity, the sanitized donor world is the active temporary
  runtime baseline and is classified `TemporaryDirectImport`.
- The baseline may include donor-derived static geometry, terrain, roads,
  vegetation, temporary materials/textures, props and collision.
- It must not include donor scripts, PlayMaker FSMs, runtime assemblies, old
  UnityEngine references, player/NPC/vehicle/gameplay managers, weather,
  lighting, audio, UI, cameras, save logic, Steam/platform or DRM logic.
- Raw donor payload stays outside Git. Tools, hashes, manifests, mappings and
  reports are project-owned and versioned.
- The baseline may be required by private local feature-parity builds, but it is
  not `ProductionReady` and is not permitted in a distributable/public build
  without explicit rights.
- Gameplay, saves and streaming must never depend on donor hierarchy names,
  object paths, scene instance IDs or imported component identity.
- Keep logical separation between legacy visuals, project-owned gameplay data,
  and future production overrides.
- Do not destructively split continuous terrain/roads/water/large meshes before a
  tested seam-safe tool exists.

## World reconstruction and visual identity

### Feature-parity phase

- Use the exact donor baseline rather than manually rebuilding locations.
- Existing custom cells that do not resemble the donor location are
  `PrototypeOnly / RejectedForFidelity / Inactive`.
- Preserve useful cell IDs, streaming registries, tests and project-owned
  gameplay metadata from those prototypes.
- Disable inaccurate custom visual content and show the donor baseline in those
  regions.
- Do not spend the current phase polishing or artistically repairing those cells.

### Remaster phase

- Production zones are reconstructions, not reinterpretations.
- Preserve donor road centerlines, junctions, driveways, landmark positions,
  building footprints, façade proportions, roof silhouettes, terrain profile,
  shoreline, sightlines, travel distances, and gameplay-critical clearances.
- New meshes/materials/vegetation may improve fidelity and detail but must not
  silently redesign the place.
- Preserve characteristic clutter, asymmetry, cheap construction, empty space,
  worn surfaces, and slightly awkward rural placement where those define the
  original location.
- Do not beautify a donor location into a generic Scandinavian postcard.
- Do not use dramatic lighting, fog, depth of field, camera angle, or weather to
  hide structural mismatch.
- Every production override cell requires matched donor/baseline comparison and
  human approval before it can be marked `Approved` or `Complete`.
- When the exact original cannot be established, stop and request reference
  evidence. Do not invent.

## Missing terrain and legacy hacks

- Internal terrain voids, fake tree walls, sprite forests, missing ground,
  clipped fields, world-edge holes, flat proxy objects, and under-map water/swamp
  hacks may remain temporarily in the donor runtime baseline only when they are
  catalogued as legacy visual debt.
- They must not be mistaken for production-ready art or allowed into a final
  distributable remastered build.
- Fix immediate traversal, collision, out-of-bounds, streaming and softlock risks
  with explicit temporary safeguards.
- Reconstruct missing areas later with continuous terrain and believable passive
  landscape while keeping the original playable footprint and sightlines.
- Do not automatically turn repaired voids into new gameplay locations.
- Keep reference, legacy baseline, production override, collision, interaction,
  LOD, navigation, and streaming representations separate.

## Difficulty and player guidance

- Do not add permanent GPS, minimap routes, floating world markers, modern quest
  trackers, or automatic task completion as baseline features.
- Do not add an RPG inventory or bottomless abstract backpack.
- World items remain physical unless a bounded container/pocket system explicitly
  supports them.
- Do not expose exact hidden simulation values to the normal player HUD merely
  because they exist in telemetry or DEV tools.
- Accessibility options may reduce motion or increase readability without
  silently changing gameplay rules.

## Player representation and hands

- Do not create a physical full-body first-person player for the current scope.
- Do not add world-space arm collision, generic hand IK, physical hand joints,
  animated vehicle entry/exit, or body placement inside vehicles.
- Use lightweight first-person viewmodel arms only for approved presentation:
  drinking, smoking, driving, and a small set of optional tool gestures.
- Pickup, carry, opening, installation, fastening, and vehicle entry remain
  gameplay-driven and must not depend on an animation frame.
- Viewmodel animations are presentation-only and safely interruptible/resettable.

## Weather and environment

- Enviro 3 is the approved visual sky/weather backend.
- Project-owned systems remain authoritative for:
  - game time and calendar;
  - deterministic weather scheduling and transitions;
  - gameplay weather outputs;
  - accumulated wetness and drying;
  - gameplay lightning selection/effects;
  - save state;
  - road, vehicle, water, UI, NPC, and audio integration.
- Gameplay/core assemblies must not reference Enviro types outside a dedicated
  integration assembly.
- Do not modify, move, rename, reformat, or patch Enviro vendor files.
- Inspect the exact locally installed Enviro version and included docs/API.
- Do not run Enviro and Azure Sky or duplicate HDRP sky/cloud/fog owners together.
- Fog ownership must be explicit and singular, especially around HDRP water.
- Enviro autonomous time/weather scheduling must not compete with project-owned
  services.
- Enviro audio must remain disabled when Wwise/project audio is authoritative.
- Do not require the optional Enviro Terrain Shader add-on.
- Temporary donor materials may have partial wetness support during feature
  parity. Record coverage gaps instead of remastering the full map inside the
  weather milestone.
- Enviro visual lightning never directly damages gameplay entities. Gameplay
  strikes come from the project-owned lightning director.

## Physics and persistent objects

- Preserve physical clutter and donor-style persistence without keeping every
  distant object as an active Rigidbody.
- Prefer sleeping, pooling, streaming-cell persistence, simple colliders,
  container abstraction, and centralized systems over per-object Update loops.
- Critical objects require deterministic recovery paths for out-of-bounds or
  corrupted states without creating duplication exploits.

## UI

- Default HUD remains restrained, survival-focused, and donor-compatible.
- Prefer a compact vertical needs panel: dark translucent treatment, clear
  icons/bars, minimal animation, no default numeric percentages.
- Vehicle HUD respects physical gauges and does not duplicate every instrument
  by default.
- Main menu should feel modern but recognizably MSC: rural, practical,
  mechanical, slightly rough, not sci-fi or live-service styled.

## Dependency and vendor policy

- Third-party packages are dependencies, not project architecture.
- Do not install/update paid packages silently.
- Keep adapters narrow, replaceable, documented, and testable.
- If a required paid dependency is absent, provide exact manual setup and stop
  dependent implementation rather than creating fake types.

## Process

- One bounded prompt at a time.
- Keep the project compiling and launchable after every closed milestone.
- Do not claim tests, captures, comparisons, or performance measurements that
  were not actually executed.
- Do not cross milestone boundaries because adjacent work seems convenient.
- Do not mark production override art complete without human approval.
- Prefer explicit stop conditions over expensive guesses.
