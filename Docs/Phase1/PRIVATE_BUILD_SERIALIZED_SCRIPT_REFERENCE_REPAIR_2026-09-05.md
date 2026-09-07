# Private Phase 1 serialized script reference repair — 2026-09-05

## Failure and evidence

The first private Phase 1 Development player completed the Unity build pipeline,
but both D3D12 and D3D11 smoke launches failed before the main menu. The player
reported `level20` as corrupted; build index 20 is
`World_Cell_-3_0_Legacy.unity`.

Two project-owned Unity object types were declared as secondary public types in
files named after a different primary type:

- `MSC.LegacyImport.DonorWorldSupplementalEntityMetadata` was declared in
  `DonorWorldBaselineEntityMetadata.cs`. Exactly 328 instances in nine enabled
  legacy cell scenes were serialized against embedded, session-local,
  GUID-less MonoScript records. Those local IDs resolve inside the authoring
  YAML, but they do not provide persistent secondary-class identity when the
  scenes are packed into a player. The same nine scenes produced the build's
  missing-script diagnostics; `level20` contains 75 of these records. This is
  a build-time script-binding defect, not proof that the source YAML itself or
  any transform is corrupt.
- `MSC.Lighting.LightingCalibrationProfile` was declared in
  `LightingQualityProfile.cs`. `Phase1LightingCalibration.asset` was serialized
  with `m_Script: {fileID: 0}`. Both smoke logs then reported a
  `ProductionLightingInstaller.InitializeRuntime` null reference.

The world-scene defect is the leading crash cause until a rebuilt player passes
smoke. The lighting defect is an independently confirmed startup failure and is
repaired in the same bounded iteration to avoid knowingly producing another
broken player.

## Compatibility and dependency audit

This is a serialization-identity repair, not a gameplay or content migration.

- Namespaces, fully qualified type names, public APIs, attributes, serialized
  field names/types/order/defaults, and authoring methods remain unchanged.
- The original `DonorWorldBaselineEntityMetadata.cs.meta` GUID and
  `LightingQualityProfile.cs.meta` GUID remain unchanged, preserving the primary
  types and all existing references to them.
- Each secondary Unity object type moves unchanged to a filename-matching `.cs`
  file with its own permanent `.meta` GUID.
- Stable entity IDs, replacement keys, source object IDs, transforms, meshes,
  colliders, manifests, catalog links, asset GUIDs, scene paths, and build-index
  order remain unchanged.
- The authored lighting values remain unchanged, including
  `duskOnSunElevationDegrees: 12` and `dawnOffSunElevationDegrees: 2`; class
  defaults are not written back into the asset.
- Runtime and Editor assembly boundaries remain unchanged. No assembly
  definition, save DTO, stable-ID schema, donor payload, or accepted 00–08A API
  is renamed or replaced.
- No save migration is required: neither repaired type is a save DTO and the
  serialized gameplay/provenance payload stays byte-identical apart from the
  targeted `m_Script` lines.

## Allowed write boundary

1. Split the two secondary classes into matching source files and add their
   `.meta` files.
2. Repair only the 328 proven supplemental `m_Script` lines in the nine known
   build scenes, the corresponding 328 lines in the ignored canonical generated
   source scene, and the single calibration asset `m_Script` line.
3. Add a pre-build validator and focused EditMode tests for permanent script
   identity, GUID-backed YAML references, real asset loading, and the catalog's
   calibration binding.
4. Store pre-repair backups and machine-readable before/after evidence below
   `Artifacts/PrivateBuildRecovery/`; do not delete earlier build output.

Before any YAML write, the repair must prove the exact class identifier and old
script-reference shape for every target, back up every target file, and count
exactly 328 build-scene + 328 generated-source + 1 calibration records. After
the write, a masked-payload hash must prove that only target `m_Script` lines
changed. Unknown GUID-less references are reported and fail validation; they
are never mass-repaired.

Explicitly out of scope: scene-wide save/reserialization, presentation rebuild,
donor regeneration, lighting regeneration, transform/data edits, manifest
changes, build-order changes, Library deletion, save deletion, or modification
of the failed build artifact.

## Verification gate

The repair is accepted only after Unity compiles, focused EditMode and existing
supplemental reload PlayMode coverage pass, a new uniquely named private Phase 1
Development player builds, and the new player reaches a usable main menu without
the missing-script diagnostics, lighting null reference, level corruption, or
native crash.

## Executed repair evidence

- Recovery root:
  `Artifacts/PrivateBuildRecovery/SerializedScriptRepair_20260905_021208/`.
  It retains all pre-repair targets, including the 740,430,586-byte canonical
  generated source scene.
- Nine build scenes: 328/328 target blocks repaired with per-file exact
  class/old-shape preflight and per-file masked-payload SHA-256 equality.
- Canonical generated source: 328/328 target blocks repaired using a bounded
  streaming rewrite. Stable-ID differences before/after: 0. Before SHA-256:
  `AD38F0ED329E52D90473E2AA7B1C18FBA824DDC226BCC61C6DEB74ED931E80E8`;
  after SHA-256:
  `1B778147AC8E8DA514C4B7C993E933DE4DCBF84099F0F2E75E45F148F9C70F21`;
  shared masked-payload SHA-256:
  `4D6CAE9C95B95E25D26D6D2165130B6C3249F612FB9E414F09C093955F0C1DBA`.
- Calibration asset: 1/1 target line repaired; masked-payload SHA-256 remained
  `BFA8BC8212D1E7584F5FC324DF7EE3FC38E6B129B4673C049C83EAC4F6FF6168`.
- Total targeted YAML lines repaired: 657. No scene-wide save, regeneration, or
  component remove/re-add operation was used.

## Executed verification

- Cold Unity compile: exit code 0, with no compiler errors and no missing-script
  diagnostics. Log: `Artifacts/PrivateBuildRecovery/SerializedScriptRepair_20260905_021208/unity-compile.log`.
- Focused EditMode tests: 4/4 passed in 35.2799 seconds. Results:
  `Artifacts/PrivateBuildRecovery/SerializedScriptRepair_20260905_021208/editmode-focused.xml`.
- The first supplemental reload PlayMode attempt using `-nographics` was rejected
  as an invalid HDRP test environment because the Null graphics device could not
  create the menu preview RenderTexture. This is retained as a failed harness
  attempt, not counted as a product failure or a pass.
- The first real D3D11 PlayMode attempt loaded the farm cell and its non-empty
  supplemental array, then exposed one stale test-only manifest expectation
  (`r1-v3`). The authoritative manifest and serialized records are `r3-v6`; the
  assertion literal was updated with explicit scope approval. The rerun passed
  1/1 in 22.0933 seconds, including unload and reload of `cell_-2_0`. Results:
  `Artifacts/PrivateBuildRecovery/SerializedScriptRepair_20260905_021208/playmode-supplemental-reload-d3d11-r2.xml`.
- Fresh private Development build:
  `Builds/PrivatePhase1_20260905_024640/MySummerCar_Remake_PrivatePhase1.exe`.
  Unity reported `Success`, exit code 0, 2,276,716,155 bytes, and a duration of
  00:06:23.7833360. The build guard passed for 328 cell records, 328 matching
  source records, and the calibration asset. All 233 enabled scenes were opened
  and packed as `level0` through `level232`; missing-script diagnostics,
  `BuildFailedException`, corruption diagnostics, and compiler errors were all
  zero. `level20` is 1,638,960 bytes with SHA-256
  `43E943D616057E1386AF0D89E1D5F09CB394012917904F89C147A3858988892C`.

## Player smoke and post-build Wwise packaging repair

The formal build exposed a separate Wwise packaging defect: its pre-build
activator could not write the installed third-party plugin's
`x86_64/Profile/AkUnitySoundEngine.dll.meta`, so the formal output omitted the
native DLL even though Unity still returned build success. The unpatched D3D11
player remained responsive and initialized the UI without any level corruption,
but repeatedly logged `DllNotFoundException`. This failed audio smoke is retained
in `player-smoke-d3d11.log`.

For this private Development artifact only, the single intact native plugin was
copied post-build from the installed Wwise integration into
`MySummerCar_Remake_PrivatePhase1_Data/Plugins/x86_64/AkUnitySoundEngine.dll`.
Source and destination are both 5,270,824 bytes with SHA-256
`418DE8D6B9D10728A3D9F4A778B010B1507DD56BA54931214257776D137416D9`.
No Wwise vendor source or metadata was edited manually. This output is therefore
explicitly a **post-build-patched private Development artifact**, not an
unmodified formal build and not a production/release candidate. A future clean
build is not protected from the importer defect until Wwise packaging is fixed
separately.

Post-patch real-player smokes passed on both D3D11 and D3D12. In each run the
player process stayed alive and responsive, Wwise logged `Sound engine initialized
successfully`, the UI root initialized, and counts were zero for `level20 is
corrupted`, `Position out of bounds`, native crash, missing script, lighting null
reference, `DllNotFoundException`, type-initializer failure, entry-point failure,
bad-image failure, SEH/access-violation failure, and Wwise error/warning records.
Logs:

- `Builds/PrivatePhase1_20260905_024640/player-smoke-d3d11-patched.log`
- `Builds/PrivatePhase1_20260905_024640/player-smoke-d3d12-patched.log`

All five native current/backup save files retained their exact pre-smoke SHA-256
hashes after both runs. No New Game or save-slot action was invoked. Automated
window capture was unavailable for this Unity player on the host
(`SetIsBorderRequired` returned `0x80004002`), so menu readiness is evidenced by
the responsive native window, completed UI-root initialization, and clean runtime
logs rather than a screenshot or input-driven menu traversal.

Two existing `SERVICE-WORLD-BLOCKER` warnings remain (Fleetari paid-order outcome
and inspection assessment backend), together with known convex-hull and local
diagnostic/certificate warnings. These are unrelated Phase 1 parity debt and do
not change the serialized-reference repair result.
