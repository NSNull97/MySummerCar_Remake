# Satsuma body, fastener, wheel-material and rain parity pass

Date: 2026-09-02; V58 follow-up: 2026-09-03  
Evidence baseline for this pass: `11A-V1d.58`; current generated Satsuma
baseline is `11A-V1d.60` after the separately documented hinge-arm and
mirrored-door latch correction.  
Donor lock: frozen `GAME.unity`, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`

## Scope boundary

This pass covers the nine reported body/presentation issues. Save/New Game
startup sequencing is deliberately excluded because it is being changed in a
parallel task. Spray-can painting is also excluded by user decision; its future
interaction is intended to follow the Cheap Car Repair-style workflow.

The donor installation and extracted staging were inspected read-only. No donor
script, FSM, old shader, runtime assembly or hierarchy lookup is used by the new
runtime.

## Donor findings and implemented differences

### 1. Starting paint is not matte

The donor `PaintType` value `0` means no material replacement. On a fresh car it
therefore keeps `CAR_PAINT_RUSTY`; it does not select `CAR_PAINT_MATTE`.
The reviewed material responses are:

| Donor profile | Metallic | Smoothness |
|---|---:|---:|
| `CAR_PAINT_RUSTY` | 0.5 | 0.3 |
| `CAR_PAINT_REGULAR` | 0.0 | 0.587 |
| `CAR_PAINT_METALLIC` | 0.8 | 1.0 |
| `CAR_PAINT_MATTE` | 0.0 | 0.0 |

The remake previously flattened donor-specific shader values during generic HDRP
conversion, making every body finish look matte. The generated baseline now has
seven donor-aligned material profiles and preserves the selected New Game colour
on all seven independently painted surfaces: shell, both doors, both fenders,
hood and bootlid.

### 2. Starting rust and Fleetari removal

The donor starts from `CAR_PAINT_RUSTY`, tinted over `body_rust.png`, with the
reviewed rust detail albedo/normal, rust mask and metallic/smoothness data. The
base colour recorded on the donor material is
`(0.44705883, 0.40784314, 0.09019608, 1)`.

The old remake path retained only a flat tint, so the rust layer effectively
disappeared. V55 bakes/repacks the incompatible legacy detail and mask channels
for HDRP while retaining their source identity. Fleetari body/panel repair swaps
only the requested surface to `CAR_PAINT_REGULAR`, which removes the starting
rust in the same logical place. Spray-can hiding/overpainting remains deferred.

### 3. Loose doors are pickups

Both loose doors now retain a usable physical collider, Rigidbody ownership and
`PhysicsPickupTarget`. Installation still hands authority to the assembly/hinge
path. No special name-driven pickup branch was introduced.

### 4. Body fasteners are donor short bolts, not generic nuts

The nine stock body mounts now expose exactly 32 short bolt presentations. They
remain visible on the loose part, move with that part, become wrench targets when
installed, and do not retain a second inert mount-owned copy.

| Mount | Count | Tool size | Aggregate max | Bolted-on threshold |
|---|---:|---:|---:|---:|
| rear bumper | 2 | 8 mm | 16 | 6 |
| right fender | 5 | 5 mm | 40 | 28 |
| grille | 2 | 6 mm | 16 | 6 |
| bootlid | 4 | 6 mm | 32 | 24 |
| left door | 4 | 10 mm | 32 | 28 |
| hood | 4 | 6 mm | 32 | 8 |
| right door | 4 | 10 mm | 32 | 28 |
| front bumper | 2 | 8 mm | 16 | 6 |
| left fender | 5 | 5 mm | 40 | 28 |

Every listed bolt has eight stages, is inserted on installation and is required
for removal. The earlier generic fastener pass guessed a common visual and did
not bind the donor renderer to part ownership, producing always-visible but
non-interactive hardware.

The bootlid is the important scaled exception: all four donor `BoltPM` parents
have local Z scale `0.5`. Their `-0.0025 m` local screw step is therefore
`1.25 mm` in part space, or `10 mm` over eight stages. V57 transfers that scale
into the reparented presentation; the old unscaled `20 mm` travel is what made
the tightened bolts pierce the outer skin.

### 5. Fenders follow the selected car colour

The donor stores both fenders as independent Paint FSM surfaces. The remake had
left their authored original colour untouched, so they ignored the colour chosen
in the New Game menu. Both fenders now participate in the same selected-colour
application as the shell and other detachable panels. Stock-only rally stickers
and registration plates remain suppressed.

The donor-active child `bootlid_emblem` (GameObject `23017`, Transform `51604`)
is deliberately retained. Despite that misleading hierarchy name, its mesh is
`datsun_bootlid_001` (source GUID
`235bdb14cc16d8643a3b3f848f0608e1`) and it is the complete 0.56 m-wide exterior
handle/garnish around the bootlid opening. The former blanket `emblem` filter
disabled this required stock geometry and left a visible empty hole.

### 6. Glass is transparent and keeps donor response

The old material fallback treated the donor window shader as an opaque surface.
V55 maps both donor glass materials to transparent HDRP/Lit materials, disables
depth writing and shadow casting, and retains the shared donor alpha texture.

| Glass | Donor source GUID | Base-colour scalar | Smoothness | Count |
|---|---|---:|---:|---:|
| windshield | `ac664fa7a2ad68d4ca5eca5c8b6c02cf` | 0.8161765 | 0.85 | 1 |
| cabin windows | `423931766c7a0b14ea1fa5c0f98790a4` | 0.9264706 | 0.6 | 4 |

### 7. Front fenders no longer contain preinstalled mudflaps

The donor loose-fender hierarchy contains inactive `ActivateThis` presentation
copies at GameObject IDs `5511` and `12503`. The old importer copied all child
renderers without preserving that FSM-owned inactive meaning, so each fender
visually arrived with a mudflap while a separate installable mudflap also existed.

Those two embedded copies are now excluded. The separate
`vehicle.satsuma.part.mudflap-fl/fr` parts and their one-bolt fender-owned mounts
remain. The donor trigger is active under the loose fender, so attachment to a
removed fender intentionally remains allowed; it is not gated on the fender
already being installed on the car.

### 8. Stock and GT wheel materials

Donor assignments are:

- all four steel rims: `RIM_PAINT_RUSTY` over the complete rim;
- all four GT wheels: `RIM_PAINT_RUSTY` on the inner part and
  `RIM_PAINT_METALLIC` on the outer part.

The remake already had the correct material IDs on the meshes, but its generic
converter dropped the legacy AO/detail-normal/specular-gloss texture contract,
which left the rims visually white/flat. V55 builds an sRGB rust base colour,
linear HDRP detail map and linear mask map, retains the donor specular map, and
sets the rusty material to specular workflow (`smoothness 0.3`, detail normal
scale `0.5`). The GT outer material remains metallic `0.5`, smoothness `0.975`.

### 9. Rain on Satsuma glass

The locked donor does not provide a generic dynamic whole-car wetness path.
Searches of its managed scripts found dynamic rain material writes only in
`windshield.cs` (`_Raining` and `_Rain`). The donor car-paint shaders/materials
do not expose an equivalent runtime body-wetness controller. Therefore whole-body
wetting is a Phase 2 remaster enhancement, not missing Phase 1 donor parity.

The donor windshield contract is now reimplemented behind project-owned weather
outputs and HDRP materials:

- shared 512 x 512 atlas for five window renderers;
- front/side/rear atlas bands `0..0.4`, `0.4..0.7`, `0.7..1.0`;
- gravity multiplier `40`;
- rain types `(0, 2.45, 1)`, `(450, 2, 3)`, `(900, 2, 4)` where fields are
  drops per 60 Hz frame, drying speed and drop size;
- windshield intensity `0.5`, other four windows `0.2`;
- six-second rain exposure transition;
- vehicle velocity and project weather wind affect flow;
- a five-probe roof test stops new drops under cover and existing drops dry.

The implementation creates an HDRP Detail Map at runtime and applies it through
renderer property blocks. It does not import the donor custom windshield shader
or component. Donor wiper-clearing arcs are not wired yet because the separate
wiper control/assembly mechanic is outside this body-material pass.

### Follow-up: registration plates are not initial car equipment

The donor contains two physical `register plate(Clone)` objects, both inactive
at New Game (`GameObject 6850 / Transform 42906` and `GameObject 32470 /
Transform 68529`). They belong to the inspection-station flow, not the garage
`CARPARTS` starting roster. Front-bumper FSM `109990` consumes one carried plate
through `trigger_regplate_front`; bootlid FSM `109898` does the same through
`trigger_regplate_rear`. Each action enables the corresponding embedded visual
and destroys the carried presentation. No plate bolt group is involved.

The generated car therefore keeps both embedded plate renderers inactive at
New Game. The two loose inspection rewards and their install actions are not
invented inside the Satsuma prefab: they remain owned by the pending inspection
service/reward implementation. This preserves the correct initial appearance
without spawning donor station rewards in the home garage or silently changing
the vehicle save graph.

### Follow-up: body fitment, fastener focus and steering hardware

- The donor right-door `handle_75660` is a direct child at local position zero,
  identity rotation and scale `(-1,1,1)`. Generic negative-determinant matrix
  decomposition introduced an extra 180-degree rotation and moved the combined
  handle/keyhole mesh. Direct children now retain their serialized local TRS.
- Body-panel bolt presentations intentionally follow the movable loose/installed
  panel and are not children of the mount's interaction marker. Fastener focus
  now resolves the explicitly authored presentation renderer first; hovering a
  bolt outlines that bolt rather than the fender, bumper, door or bootlid.
- Donor `trigger_grille` is structurally orphaned, but its Assemble action
  reparents to `pivot_grille` (`Transform 68091`). Using the trigger transform
  lost the compound pivot rotation and installed the grille sideways. The mount
  now uses the exact donor pivot while retaining its two 6 mm short bolts
  (`Transform 47050` and `72072`).
- Both donor steering-wheel variants carry one central 10 mm `bolt2` nut:
  stock marker `62790`, GT marker `52786`, eight stages. The decorative embedded
  `bolt0` is excluded. The optional wheel cover remains inactive because it is
  separately installed in the donor.
- The steering column is retained by exactly two 8 mm short bolts at markers
  `57828` and `60175`. The Assembly FSM also references tachometer marker
  `59336`; that unrelated 5 mm screw is now excluded from the column group.
- Predecessor saves are migrated transactionally: the former third column ID is
  remapped to the surviving second physical bolt, and the new steering-wheel
  nut is inferred only from mount occupancy. Malformed legacy shapes are
  rejected without mutating live state.

The physical four-panel controls and cabin hood release are specified separately
in `Docs/Phase1/SATSUMA_HINGED_PANEL_RUNTIME_FIX_2026-09-02.md`.

## Root causes of the reported presentation faults

1. The generic legacy material converter treated distinct paint, transparent
   glass and Standard-Specular rim shaders as one fallback surface.
2. Shader-specific detail/mask/specular channels were discarded instead of
   repacked for HDRP.
3. The import pass copied inactive FSM `ActivateThis` renderers as ordinary
   visible presentation.
4. Paint authority covered the shell but not all seven donor Paint FSM owners.
5. Fastener visuals were treated as generic mount decoration instead of
   part-owned donor short bolts with their real wrench groups.
6. Sanitization correctly removed the donor `windshield` MonoBehaviour and old
   shaders, but there was no project-owned replacement consuming modern weather
   outputs.

## Automated evidence

- Builder log: `Logs/codex-satsuma-body-build-r10.log`
  - result: `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.58`, 125 loose
    parts, 117 mounts, 280 fasteners and four hinged mounts;
  - SHA-256:
    `D6A95372613B43837BC0433C92DE2FD844CC002ED9F0D839BA3FA5EF534D75D7`.
- Final generated-content EditMode result:
  `Logs/codex-satsuma-body-edit-r12.xml`
  - `39/39` passed, zero failures/skips;
  - SHA-256:
    `EBCFF8A304A0F0B85C4859E949FDEE21189701A0375AF368EE1992AD6B7C071B`.

The rain smoke test additionally proves that a sheltered presenter produces no
wet pixels, an exposed presenter produces drops, and the generated runtime atlas
is applied to the glass renderer property block.

## Manual acceptance still required

1. Start with two visibly different menu colours and confirm that shell, both
   fenders, both doors, hood and bootlid agree while the rust texture remains.
2. Pick up both loose doors, install them, and verify all four 10 mm bolts per
   door are usable and gate removal at the correct state.
3. Confirm stock fenders have no baked mudflaps; install each separate front
   mudflap once on a loose fender and once after fitting the fender.
4. Compare all four stock rims and GT inner/outer materials in daylight.
5. During rain, compare windshield versus side/rear intensity, drive slowly to
   observe flow, move under the garage roof, and confirm drops stop and dry.
6. Start a fresh game and confirm neither front nor rear registration plate is
   already visible. Separate plate acquisition/installation is an inspection
   service acceptance item, not part of this fresh-car body check.
7. Confirm the stock exterior handle/garnish is visible across the bootlid hole,
   then check all four bootlid bolts at stages zero and eight: none may pass
   through the exterior skin.

Save restore and New Game startup stability are not part of this acceptance run.

## Exactly one next milestone

Perform the in-game V57 body/material/rain and hinged-panel acceptance. Only
after that gate, implement the inspection reward and separate plate-install flow
as one bounded service/vehicle integration milestone.
