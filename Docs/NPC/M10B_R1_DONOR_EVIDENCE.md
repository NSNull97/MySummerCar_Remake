# 10B-R1 — locked donor evidence

Status: **read-only evidence captured; implementation automated-valid;
Fleetari placement user-accepted; Teimo store-pub endpoint corrected and
user-accepted; Teimo bicycle route/presentation automated-valid; remaining
comparison pending**.

Authority is the frozen AssetRipper scene:

```text
E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/
milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity
SHA-256 c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4
```

The donor installation and staging were read only. Runtime code, PlayMaker FSMs,
AnimatorControllers, animation events, donor shaders, old Unity assemblies and
hierarchy/name lookup were not transferred.

## Roster package

10B-R1 is exactly the four `ServiceOrRelationship` rows. It is the first
bounded package of the 75-row NPC roster, not the total NPC count.

| Feature | Scene evidence | Project anchor/schedule | Temporary presentation |
|---|---|---|---|
| `P1.NPC.001` Teimo | renderer `101670`, hat renderer `74452`, glasses renderer `77775`, root transform `67949`, animation target `59524`; STORE opening/speak/bicycle FSM metadata; `teimo_move_store`/`teimo_move_bar` root endpoints | stable instance retained; shop `(-1381.5231, 5.959, 142.64197)`, pub `(-1376.0472, 5.9590025, 146.14417)`; Mon–Sat shop 10:00–20:00, pub 20:00–02:00 | `bodymesh` + `teimo_hat` + `eye_glasses`; ordered `shirt02`, `pants02`, `face01`; donor `glass`/`metal_shiny` slot order; `teimo_cash_register`; calibrated visual-root Y `+0.201 m` |
| `P1.NPC.002` Fleetari | renderer `101833`, glasses renderer `75489`, visual root `64672`, chair renderer `80242`, animation target `39079`; REPAIRSHOP opening/work/service metadata | `(1725.0482, 6.3119974, -301.45422)`, `cell_3_-1`; weekdays 08:00–16:00 | `bodymesh_1` + `eye_glasses`; ordered `shirt07`, `pants01`, `face02`; donor `glass`/`metal_shiny` slot order; `fleetari_breathe`; root compensation reproduces the serialized body/chair world relation while the existing world-baseline chair remains the context owner |
| `P1.NPC.007` Farmer | renderer `101903`, GIFU-cap renderer `76292`, dark-glasses renderer `76308`, root `49007`, animation target `66111`; JOBS/Farm move/target/job/dialogue metadata | farm root plus six exact target anchors in `cell_-2_0`; 06:00–20:00 distance-weighted ping-pong traversal | `bodymesh_1` + `gifu_hat` + `eye_glasses2`; ordered `shirt18`, `pants02`, `face10` material identity using `face08` texture; dark glasses keep `glass`/`bottle_beer` slot identity; `fat_walk`/`fat_standing` |
| `P1.NPC.008` Berryman | renderer `101702`, cap renderer `73813`, root `70503`, `hand_left` animation target `54889`; StrawberryField opening/babble metadata | `(-1034.2885, 2.372983, -1673.2478)`, `cell_-3_-4`; 06:00–12:00 | `bodymesh_2` + `Latsa` cap; ordered `shirt03`, `pants03`, `face03`; seated donor pose and `strawberryman_left_hand_loop`; tent is restored by the supplemental world layer |

Farmer target positions, in donor order:

1. `(-663.62, 3.611, 308.115)`
2. `(-669.92, 3.511, 293.31494)`
3. `(-688.42, 3.511, 272.115)`
4. `(-664.22, 3.511, 285.81494)`
5. `(-638.82, 2.3709998, 269.615)`
6. `(-627.95, 2.611, 231.44495)`

Teimo's frozen scene stores `STORE/TeimoInShop/Pivot` at local X `-6.5`, the
pub endpoint, while its parent `TeimoInShop` is the local-zero shop endpoint at
world `(-1381.5231, 6.16, 142.64197)`. The collider-authoritative logical Y is
`5.959`. The complementary root curves are hash locked as:

- `teimo_move_bar.anim`:
  `8935b9fa20061956d4040204b03d2271b243a470124027939998fd99dae43375`;
- `teimo_move_store.anim`:
  `8a32105f67a6812ae48367e045404f621e5960afb05bb4eb58a8b67943e38338`.

The curves move between local X `0` and `-6.5`; no hierarchy-name lookup or
donor animation controller is used at runtime. The project schedule switches
between the two project-owned anchors.

## Teimo bicycle route and presentation

The frozen scene contains `TeimoInBike` GameObject `6272`, transform `42325`,
project-reviewed Move FSM component `105851` and spline component `105852`.
The FSM is evidence only. Its two events select separate path managers:

- `TOSTORE` -> path manager component `106459`, 37 points, `777.0235 m`, source
  speed `4 m/s`, Mon-Sat start `08:00`, project traversal duration
  `2331.0705` game seconds;
- `TOHOME` -> path manager component `112267`, 37 points, `778.3277 m`, source
  speed `4 m/s`, Tue-Sun start `02:00`, project traversal duration
  `2334.9830` game seconds.

The `12 game seconds / real second` conversion is independently locked by the
donor clock evidence. Both exact point arrays live in
`Phase1TeimoBicycleRouteManifest.json`; runtime consumes generated project-owned
anchors, routes, schedule IDs and `vehicle.teimo.bicycle`, never the source
component IDs or hierarchy names. Read-only SWS evidence marks both paths
curved; the isolated uniform Catmull-Rom calculation is adapted as `CodePorted`
while project-owned arc-length sampling and analytic tangents own traversal.

The selected bicycle wrapper uses rider renderer `101713`, root `42325`, hat
`72504`, clear glasses `78400`, bicycle frame `80370`, pedals `75334`, tires
`75111`/`77306` and rims `77540`/`76523`. Frame, pedals, tire and rim meshes plus
`ATLAS_MOTORPARTS.png` are individually GUID/SHA locked. The sole reviewed
bicycle clip, `teimo_bicycle_waving_hello`, is a greeting on `collar_right`
transform `63350`; it is not falsely looped as a riding cycle. Translation and
ground conformance are owned by the deterministic route simulation.

Read-only inspection of the bicycle `Speed` object (`8921`) and its `Rotate`
FSM (`106628`) records the missing moving-part presentation precisely. The Move
FSM sends source speed `4` into `Rotate.Speed`; `SpeedTire` is speed multiplied
by `170`, and `SpeedPedals` is `SpeedTire / 3.5`. The three donor Rotate actions
target pedals `11102` and tires `10266`/`18450` on local X. The project wrapper
therefore advances both tire parents (their rim children follow) by `170 degrees
per travelled metre` and the pedal crank by one third-and-a-half of that. It
uses actual project route displacement, so pause, time scale and curved-route
motion remain coherent without executing donor FSMs.

The wrapper's serialized hierarchy places wheel centres `0.532 m` above its
logical root; the locked tire mesh radius is `0.347645 m`. The resulting
`0.184355 m` clearance caused the reported floating bicycle when the logical
root was correctly projected onto the road. Import revision 8 computes the
lowest point of both locked tire meshes and applies that exact correction to the
visual root. A `0..0.25 m` build guard prevents accidental broad offsets.

The donor `Waving` FSM (`106828`) waits for `Move.SeesPlayer`, compares player-
camera distance against `10 m`, plays `teimo_bicycle_waving_hello`, then enters
an idle state for the rest of that bicycle activation. Following the manual
comparison request, the project uses the stricter `5 m` unobstructed-distance
gate and allows one greeting per game-day boundary. Its next-day eligibility is
stored in the existing Teimo `npc.state` cooldown array, so streaming and save
reload cannot repeat the wave during the same day. The clip remains presentation
only; no animation event owns gameplay state.

The donor collision-to-ragdoll behavior is not yet implemented and remains an
explicit Phase 1 parity gap. The proposed recovery branch (`Fallen -> Remount`
or `Walk`) is a remake-only Phase 2 candidate and is not active in Phase 1.

The spline waypoint values are donor-world coordinates, not coordinates in the
active garage-anchored world. Runtime anchors apply the already audited M04A1
translation `(169.98, 1.611, -1040.625)`. For example, the donor endpoint
`(-1546.3608, 2.8819933, 1187.805)` becomes
`(-1376.3809, 4.4929934, 147.18005)` beside the active Teimo store. The builder
now resolves every converted waypoint through the real production streaming
manifest and fails if its `cell_-2_0` or `cell_-3_0` owner does not exist.

## Teimo shop arrival

The ride-to-store completion sends the donor `GetIn` transition. Its locked
`teimo_move_in` root clip (`06214e5f...88ecd3`) contains 17 reviewed position
keys over `26.1` real seconds, including the stop at the service door and the
final local-zero counter position. The project converts those keys through the
same audited source origin/rotation and world translation, then retains their
non-uniform time fractions in `route.teimo.shop-arrival`. The stationary wrapper
uses `fat_walk` during that route and `fat_standing` while waiting for the 10:00
shop block.

The service-door OPEN/CLOSE evidence occurs at `15.0` and `16.0` seconds. The
project binds generated world entity `41889effb8941e0ef1aa015c802f648d`, creates
the reviewed hinge pivot and presents the `85` degree swing from route progress.
The six reviewed parked-bicycle stable IDs are shown only for arrival/shop/pub
states and hidden while Teimo is at home or riding. These IDs are generated
project provenance, not donor hierarchy names. Manual in-world comparison is
still required for leg contact, road/object clearance, the dismount boundary,
walk-around alignment and door swing.

## Provenance and replacement

`Phase1CharacterPresentationManifest.json` schema 5 revision 8 pins 40 source
hashes: four body meshes, ten distinct accessory/vehicle meshes, seven animation
clips and 19 textures. It
also pins the exact donor renderer material-GUID order, static accessory
renderer IDs and the reviewed root calibration overrides. Generated wrappers
validate three non-null textured HDRP/Lit body material slots plus the selected
accessory renderer/material closure. The glasses use the hash-pinned donor
`glass.png` alpha texture with a transparent HDRP/Lit material and the reviewed
opaque `metal_shiny` values; donor shader code remains excluded.

Manual calibration findings addressed through schema 5 are Teimo's missing hat,
glasses and ground offset; Fleetari's missing glasses and incorrect
chair-relative transform; Alpo's inherited vertical offset; Latanen's missing
bus-driver cap; Berryman's missing cap; and Farmer's missing GIFU cap and dark
glasses. Fleetari's logical host stays upright for interaction, while his visual
root compensates the source parent tilt so the body returns to the serialized
chair-relative pose. Berryman is not converted to a standing character: the
raised-knee silhouette belongs to his seated donor presentation and was
misleading only because the StrawberryField tent was absent from the original
world allowlist.

The Farmer route is deliberately `PingPong`, not a synthetic last-to-first
`Loop`. The donor-ordered mailbox path is traversed back through the same six
anchors. Segment selection is weighted by physical path length, preventing the
long mailbox return leg from running faster than the shorter yard segments or
cutting directly across terrain.

The sparse donor anchor heights remain simulation/configuration evidence, but
they do not describe every road undulation between anchors. Walking
presentations therefore project the authored pose onto the nearest loaded
`WorldSurface`/`WorldSolid` collider with a non-allocating downward probe. This
does not rewrite route progress, anchor data or save state; non-walking NPCs and
walking poses without a valid nearby surface retain their authored height.

Every wrapper has stable and independent production replacement keys for:

- geometry;
- materials/textures;
- animation.

All are `TemporaryDirectImport`, private Phase 1 presentation only. Phase 2
replaces mesh, UV/material/texture, rig and animation without changing the
character stable ID, FeatureId, schedule, dialogue/event identity or save state.

## Bounded R1 voice evidence

`Phase1NpcR1VoiceManifest.json` schema 1 selects 17 lines from the frozen scene:

- Teimo `Greetings` FSM `111413`: `paivaa1..7` for
  `schedule.teimo.shop` and `pubi1..4` for `schedule.teimo.pub`;
- Fleetari `Work` FSM `110774`: `Fleetari/hello01`;
- Farmer `Speak` FSM `108734`: `Tohvakka/hello`;
- Berryman `Babble` FSM `110172`: `hitaus1..3` and `halla`.

For every entry the manifest pins the exact donor GameObject, AudioSource,
AudioClip GUID, `.audioclip` SHA-256, PCM resource SHA-256, original subtitle,
serialized donor duration, source PCM duration and AudioSource min/max distance.
The selected `.resS` payloads are ordinary RIFF/WAVE PCM data. Their exported
PCM chunk durations do not consistently match AssetRipper's serialized
`AudioClip.m_Length`, so both evidence values are pinned and verified instead of
silently treating one as the other. The importer copies only those hash-locked
WAV resources to ignored generated `.wav` assets, verifies mono 22050 Hz 16-bit
PCM and creates a project-owned Unity fallback event library. Donor AudioClip
serialized assets, MasterAudio components, PlayMaker FSMs/controllers and
runtime code are excluded.

Dialogue selection is project-owned. Teimo pools are gated by the active stable
schedule block; eligible variants rotate deterministically within a session.
That rotation is a deliberate known difference from the donor's random action
and carries no simulation or save authority. A preferred-backend mapping is
attempted first; if it does not know a selected Phase 1 NPC event, the same
stable event request is posted to the explicit Unity fallback backend.

The 2026-08-01 automated voice rebuild generated all 17 clips and all 17 event
definitions. The 2026-08-02 bicycle rebuild generated the seventh character
wrapper. After the first manual visibility failure exposed an unconverted donor
coordinate space and nonexistent streaming owners, the corrected rebuild passes
NPC EditMode `16/16` plus NPC PlayMode `3/3`. Unity fallback PlayMode remains
`7/7` and the last Bootstrap integration suite remains `12/12`. Audible
comparison plus bicycle height, leg/crank contact, curved road clearance,
shop-arrival flow and greeting remain manual gates
and are not inferred from these tests.

## Honest open evidence

- Teimo bicycle collision/ragdoll behavior is not implemented. The route,
  schedule, save/streaming state and temporary visible bicycle are automated-
  valid, but `P1.VEHICLE.027` remains only `PartiallyImplemented` until manual
  comparison and the collision outcome are complete.
- Payment, repair, junk-car, hay/combine and berry-settlement lines remain
  deliberately unmapped until their 12A/12B condition owners exist. Ambient
  autonomous babble timing is also not claimed by this interaction package.
- Repair, hay/combine and berry-sale mechanics are owned by their later service/
  job domains. R1 exposes stable event hooks but does not fabricate those flows.
- No R1 row is `Verified` until the manual comparison and remaining dependent
  behavior are complete.

On 2026-08-01 the user accepted the bounded character/accessory placement and
Fleetari's chair/desk-relative pose after the schema 4 rebuild. The subsequent
report that Teimo remained in the pub exposed the incorrect schedule endpoint;
after the exact store/pub transfer, the user manually confirmed that Teimo's
placement is now correct. This does not accept the bicycle or complete
dialogue/audio/repair-service behavior. Farmer's schema-5 accessories, Farm
context, corrected return traversal and ground-conformed walking are accepted
by the latest user review. The user also confirmed the Berryman tent and seated
pose. These acceptances do not close the remaining dialogue/job/audio/save
comparison or Teimo bicycle collision gap.
