# Satsuma service fluids — bounded implementation record

Status: the canonical operating source, five stock service openings and four
physical levels are implemented. Focused geometry/pouring tests and a real
HDRP 16-frame audit passed. Broad production lifecycle regression is in
progress; manual acceptance is separate. Petrol is excluded.

## Ownership and controls

- `AssemblyServiceCapState` adds one optional schema-1 part extension with an
  explicit presence bit. The brake master owns two ordered cap identities;
  radiator, clutch master and stock rocker cover own one each. Existing part,
  mount, fastener and vehicle IDs are unchanged. Missing old data means closed;
  existing litres are not changed or replenished. Unknown, duplicate, wrong-owner
  or reordered state fails restore preflight; snapshots/clones own their arrays.
- Wheel down subtracts 33 degrees, wheel up adds 33, clamped 1–359. Eleven
  downward notches open the cap. A positive notch closes the opening immediately.
  A 0.1-second input cooldown and pause guard match existing incremental controls.
  Only a separate cap renderer turns/hides; no part root or physical body moves.
- Five `SatsumaServiceFluidReceiver` components accept passive geometric streams,
  not the instantaneous held F transfer capability. Correct liquid, installed
  reservoir, fully open cap and opt-in operating state are required. Capacity
  clamps are authoritative in the existing vehicle simulation.
- `ServiceFluidPourController` is attached by `ItemWorldRuntime` only to the
  three reviewed service products. Open the real can with its existing action,
  then tilt it. The item owns all removed contents. First-solid-surface ballistic
  segment casts select a passive receiver; missed, wrong-fluid and overflow
  volumes publish actual `LiquidSpilled` events. Empty, closed, upright and
  paused cans do not discharge. A saturated query fails closed.
- Existing sauna and petrol controllers are unchanged. All new stream state is
  transient; item open/content state already persists through native item saves.
- The composition effects bridge attaches replaceable procedural streams.
  Motor oil uses a slower, thicker amber stream, coolant a green thinner stream,
  brake fluid a pale amber thinner stream. A tint/coalescing option on the
  existing puddle presenter handles service spills without changing rain/water
  callers or simulation authority.

## Frozen evidence and classifications

Source scene remains revision `msc-world-baseline-04a1.1-c3f2f337`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

| Point | Source cap FSM | Capacity / maximum pour rate |
|---|---|---|
| Motor oil | 108033 on stock cover, trigger 105616 on head | 3 L / 0.1 L/s |
| Coolant | 110124, trigger 104542 | 5.4 L / 0.2 L/s |
| Brake front | 113003, trigger 112275 | 1 L / 0.1 L/s |
| Brake rear | 113806, trigger 105908 | 1 L / 0.1 L/s |
| Clutch | 104869, trigger 105813 | 0.5 L / 0.1 L/s |

All cap controls have Rot=359, ScrewAmount=33, clamp 1–359. Source visual
leaf GUIDs are `7c0509b632ed0fd48a51297f7d78e393` (oil),
`7619b6ed3381ffe4baa418b26282c26b` (hydraulics), and
`22c31f3bc669f0f4faafbb39a6c896c1` (radiator).

The already imported loose radiator has the same body mesh
`a332cc8056323f14d8844e6e9bc7be3f` as the installed donor radiator, but lacks the
separate cap. The scoped author adds only that reviewed visual leaf, using the
existing generated material `ad2f7b6e8cc080845a7a7fd4264fbb83`. Imported
`Mesh/motor_radiator_cap.asset` hash:
`249536A66A0F6E8E906CE7E97F75D5E104FD6A9998BAA4D9F95122A690AB857F`.
It remains ignored, private `TemporaryDirectImport` with a project-derived GUID;
no original script or FSM is imported. All other cap leaves are reused.

Can source hashes match the existing item presentation provenance:

- `GameObject/brakefluid0.prefab`: `e195231e5375950b3a5fa8160d7507f66ffb58011e09cd20ac82b6af47624000`.
- `GameObject/coolant0.prefab`: `72126e1022a14c4a7917094a6773e7bd190ae600e689bcba70ed81584f85d355`.
- `GameObject/motoroil0.prefab`: `74d70f38a2cb8ed310b7075d42a895961830ff17dd33fbc7fc6eb2c6ba52f36c`.

Source particle outlet positions, expressed in existing item-local coordinates:
brake (-0.00004186542, 0.000037354956, 0.08690715), coolant
(-0.000027501277, 0.07163249, 0.11214107), oil
(-0.00055918, -0.06413196, 0.19270726). All mouths face local +Z.
The donor Euler-X pouring check is reimplemented as orientation-independent
gravity/fill-ratio logic. Ballistic tracing, viscosity-like presentation, spill
accounting and finite transfer batching are project-owned calibrations, not an
exact FSM port or particle-volume simulation. Transfers use 0.1-second bounded
batches; rendered particles never own state.

## Executed validation

Unity `6000.6.0f1`, stopped-editor scoped authoring; no full rebuild, no user save
writes and no donor modification.

1. First cap author run failed safely on the missing radiator-cap mesh before
   prefab mutation: `Logs/live-engine-caps-author-20260906.log`, exit 1.
2. Reviewed missing leaf imported and scoped authoring succeeded:
   `Logs/live-engine-caps-author-fixed-20260906.log`, exit 0, changed=5.
3. Cap/save/valve/operating EditMode regression:
   `Logs/live-engine-caps-initial-20260906.xml`, **100/100 passed**, no skips.
   Includes exact canonical five-cap bindings and idempotent reauthoring.
4. Physical pour plus cap EditMode checks:
   `Logs/live-engine-pour-initial-20260906.xml`, **19/19 passed**, no skips.
   Covers all three cans, real physics traces, walls, wrong fluid, closed/upright
   sources, bounded overflow conservation, empty sources and content restore.
5. `Logs/live-engine-levels-openings-20260906.xml`: **27/27 passed**, exit0,
   no skips. Actual instantiated canonical stock geometry accepts real cans at
   all five openings, with the bonnet removed; item plus reservoir litres are
   conserved. Source/operating binding, levels and previous pouring/cap checks
   run together. Early integration attempts exposed test-fixture issues: prefab
   preview scenes are not the default physics scene, and the baseline contains
   both GT and stock covers sharing a socket. The fixture now explicitly selects
   stock and uses a real scene instance. Generated item fallback proxies now
   remove their temporary collider safely in EditMode as well as PlayMode.

## Physical level presentation

The reviewed cap-axis mesh probes measure 0.08048m to the hydraulic-master
bottoms and 0.26855m into the radiator. The four replaceable
`ServiceReservoirLevelPresenter` instances are composed at vehicle spawn through
`SatsumaServiceLevelComposition`. They consume a read-only interaction-layer
level capability, render inside the actual openings and never own fluid state.
Opening/closing, actual fill, restore and idempotent composition are tested.
Volume-to-height is calibrated, not a measured vessel-volume integration; the
small surface is hidden when closed, empty, unavailable or severely tilted.
No false oil pool is drawn in the rocker cover: oil is held lower in the sump.
Stock caps are covered by this packet; the separate GT cover cap still needs
its own reviewed presentation binding. The original five stock cap IDs and DTO
ownership remain unchanged.

The canonical host is now activated and reads actual hardware/conditions; see
`SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md` (36/36 focused integration checks).
The pouring controller now uses non-allocating item status queries, clears
pending pour time on item restore and handles a mouth starting inside a receiver
without allowing flow through an overlapping solid. Five-opening collision and
level-state integration pass. `Logs/live-engine-service-graphics-20260906.xml`
passed **1/1**, exit 0, with 16 D3D11/HDRP captures under
`Logs/service-level-graphics-20260906/`: empty, half-full, full and closed for
coolant, both brake circuits and clutch. All sixteen images were inspected.
Actual geometry, liquid material and existing imported part materials are used;
only camera, exposure and test lights are temporary. The level changes pixels
inside each real opening, rises below its rim and disappears under the cap.
These are controlled inspection views, not a claim of player-view acceptance
under every weather/exposure condition. The coarse donor meshes and textures
remain temporary art; no materials are reauthored by the audit.

Integration inspection found that `LegacySatsumaLoosePartsRoot.Awake` detaches
loose parts before the production level composition runs. The composition now
walks the existing `VehicleAssemblyController.Parts` registry, not car ancestry;
it filters by each cap's actual owning part and stays idempotent. A loose-sibling
EditMode regression and a four-level production Bootstrap assertion cover this
path. No assembly/streaming lifecycle, stable ID or DTO is changed.

The real native nine-purchase vehicle/items/world round trips now pass; see
`SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md`. Broader gameplay regression is
still in progress; no entire parity row is Verified.
