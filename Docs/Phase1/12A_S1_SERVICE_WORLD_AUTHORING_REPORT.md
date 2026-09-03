# 12A-S1 Service World Authoring Report

## Scope

`ProductionServiceInteractionInstaller` materializes project-owned interaction
fixtures under the production composition root. Runtime resolution uses only
stable service/location/anchor/offer IDs from `Phase1ServiceCatalog`; it does
not query donor hierarchy names, paths, filenames or instance IDs.

The source catalog and measurements are locked to donor scene SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

## Materialized fixtures

| Location | Audited interaction surface | Runtime result |
|---|---|---|
| Teimo store | 41 reviewed donor product/cover groups plus register | Each offer uses the full renderer bounds of its shelf group, live stock removes/restores visible units, and the register performs checkout |
| Teimo pub | Five exact donor buttons: beer, vodka, sausage-and-fries, coffee and cigarettes | All five purchase paths are distinct; ordinary goods queue the below-counter handoff and the meal queues the full prepared handoff before either is placed on the counter |
| Gasoline 98, diesel and fuel oil pumps | Three project-owned stable physical nozzles at audited pump anchors | A carried nozzle fills an overlapping compatible open `ILiquidContainerTarget`; gasoline and diesel canisters now use their real liquid IDs |
| Fleetari | Audited brochure/catalog anchor | Mouse wheel browses all 32 offers; primary interaction toggles the save-backed workshop selection and enforces exclusive groups |
| Home parts catalog | Audited catalog pivot | Mouse wheel browses all 46 donor packages; primary interaction toggles the local order form and reports its total |
| Inspection | Audited order anchor | Explicit unavailable shell remains fail-closed until a real vehicle assessment backend exists |

The installer records 53 unique authored binding IDs. Each store fixture owns a
project target whose box collider is fitted to the complete reviewed shelf
group after streaming; its small fallback sphere remains active only until the
group is available. The fuel fixtures additionally own a
stable pickup identity, Rigidbody, bounded hose and trigger-only fill volume.
The existing 08A UI is unchanged; feedback continues through the established
life-action status presenter.

## Teimo presentation correction

- Idle, working and talking states now play `teimo_lean_table_in` once and
  clamp its final counter-lean pose; the cash-register clip is an explicit
  non-looping checkout action only, after which the lean state resumes.
- A middle finger aimed at Teimo and an actual urine-particle collision with
  his presentation hierarchy latch the imported angry pose until a service
  action or authoritative schedule/state change replaces it.
- The 20:00 store-to-pub transition follows all four position keys from the
  4.75-second donor root-motion curve. Its project-owned turn/door waypoints
  stay on the reviewed floor plane and run for 57 game seconds under the locked
  12x clock, so the route no longer cuts through the counter/walls or floor.
- A paid sausage-and-fries order remains pending while Teimo walks to the
  fridge, opens it, takes the food into a project-owned carry offset, closes
  it, walks to the microwave, opens and physically places the audited food prop
  inside, closes and waits, removes and carries it back to the player, then
  performs the handoff. Transactions and spawned items remain authoritative in
  `ServiceRuntime` and `ItemWorldRuntime`; the imported clips are presentation
  only.
- Checkout and pub handoff now resolve 80 reviewed item-presentation bindings.
  The added bindings cover food, refrigerated products, chips, automotive
  supplies, the prepared meal and all 13 spray-paint variants. Missing visual
  bindings still fail visibly through the existing cube fallback, but none of
  these reviewed shop/pub products use it.

## 2026-08-11 retail/catalog hotfix

- Shelf display groups are no longer reused as checkout handoff objects. The
  affected groceries, automotive supplies and all 13 spray cans now use
  hash-locked detached donor prefab meshes, preventing a complete store/interior
  combined mesh from spawning and being launched by physics.
- The beer-case wrapper retains the reviewed donor root rotation, so its 24
  bottles form a horizontal case instead of a vertical bottle wall.
- Checkout handoff offsets now separate basket lines as well as quantities,
  preventing different products from spawning inside each other.
- Shelf presentation detects a streamed donor group being destroyed, invalidates
  its visual index and reapplies authoritative remaining stock to the replacement
  group after the cell reloads. Purchased stock therefore stays absent.
- The Fleetari brochure and home parts magazine targets use their exact donor
  pivots and reviewed cover materials. The flashlight and parts magazine item
  wrappers also use reviewed texture/material bindings rather than the fallback
  surface.

## 2026-08-12 physical catalog interaction correction

- The home magazine remains an ordinary physics item: LMB picks it up and F
  opens it while it is placed in the world. Its prompt exposes both actions.
- The catalog capability is rebound to the stable physical item after streaming
  reload without removing the existing pickup capability.
- Opening derives the book pose and reading camera from the magazine's current
  world position and flattened yaw. The legacy fixed anchor is only a visible
  fail-safe while the physical item is unavailable.
- While reading, the magazine renderers and colliders are temporarily hidden and
  its Rigidbody is frozen; closing restores the previous physics flags without
  an impulse. Page count no longer collapses when a reviewed page material is
  missing, so the fallback page remains browsable and fail-visible.

## 2026-08-13 streamed catalog presentation correction

- Catalog presentation no longer snapshots the global legacy provider only at
  service-install time. Each physical book retries the provider binding after
  streamed scene startup and after provider replacement.
- A late provider supplies the complete 8-page home set, 6-page Fleetari set,
  both order pages and both cover materials to already-created controllers.
- If Fleetari's closed brochure was absent during initial installation, it is
  materialized at the audited interaction anchor as soon as the provider is
  available. The home fallback cover is likewise attached to the existing
  physical-item binding without replacing its pickup action.

## 2026-08-11 beer, pub handoff and Teimo-root hotfix

- A shelf beer case is now one logical stock unit containing its flattened
  provenance descendants. Removing a case hides both the plastic carrier and
  its separate bottle renderer, and the same authoritative state is reapplied
  after a streaming-cell reload.
- Donor walking and kitchen clips contain transform curves at the animation
  root. Project navigation already moves Teimo's wrapper, so the sanitized
  inner animation target is restored to its authored local pose in `LateUpdate`
  after each legacy evaluation. Bone animation is retained while the visible
  body can no longer drift through the floor, rotate onto the counter or detach
  from the logical collider.
- Meal choreography records Teimo's authoritative pre-order world pose and
  restores it after the counter handoff. This closes the case where the visual
  body remained behind the shop after cooking while the interaction collider
  returned to the counter.
- Beer, coffee, vodka and cigarettes now remain in pending fulfillment while
  the `give-drink` handoff plays as a below-counter retrieval. The resulting
  authoritative item/effect is fulfilled only after that action and is placed
  at the authored counter surface; a temporary matching hand prop is visible
  during the gesture. Cigarettes no longer reuse the store cash-register action.
- The prepared meal now binds the complete hash-locked
  `Sausage-Potatoes.prefab`: `grill_box` plus `grill_box_food`. Final handoff
  therefore produces the boxed meal, not a bare flat food insert. Food still
  follows the longer fridge/microwave/counter choreography.

## 2026-08-11 hand-prop, kitchen-route and counter-point correction

- The presentation request now retains the exact item definition, effect,
  variant and quantity from the paid handoff line. Beer, coffee and cigarettes
  instantiate their reviewed item presentation beneath donor `hand_left` for
  the retrieval gesture; the deferred vodka effect uses a bounded temporary
  shot-glass visual. The temporary prop is removed before the authoritative
  item/effect backend completes the handoff.
- The complete prepared-meal wrapper is attached to donor `hand_right`, placed
  at the audited microwave marker while heating, returned to the same hand and
  removed only when the authoritative handoff is retried. The flat donor
  microwave marker is never used as the delivered item.
- The kitchen trip uses the three intermediate/end positions from
  `teimo_move_kitchen_in`, with the pub counter pose as the implicit start, and
  reverses the same doorway route. Logical navigation drives the wrapper while
  the looping project-owned `service-walk`/`walk-hand-out` action bindings
  animate the skeleton, avoiding both the pinned-root moonwalk and direct wall
  traversal. The presentation-only action survives the authoritative pub
  `Working` tick instead of being replaced by the counter lean.
- The store-to-pub route is explicitly `AuthoredHeight`. Terrain projection
  remains the default for outdoor pedestrian routes and vehicle bindings, but
  can no longer drag this indoor route beneath the building floor.
- Pub fulfillment now targets the exact donor `FoodSpawnPoint`
  `(-1375.6228468, 6.3336284, 145.7850387)` on the service counter. The cash
  register remains an order/payment fixture and is no longer the item spawn
  point.

## 2026-08-11 appliance-facing, carry and release-timing correction

- The doorway curve now ends in separate front-of-appliance work positions:
  fridge `(-1378.79, 5.959, 144.62)` and microwave
  `(-1377.98, 5.959, 144.62)`. Teimo faces world yaw `180` at both positions,
  so the fridge retrieval no longer occurs from the microwave position or from
  the appliance side.
- While walking between appliances and back to the counter, the boxed meal is
  parented to a stable chest-front carry point `(0, 0.96, 0.38)` on Teimo's
  project-owned presentation root. It is attached to `hand_right` only for the
  take/place/handoff gestures, so the walking arm cycle cannot swing the meal
  beside his body.
- Ordinary below-counter goods stay hidden until normalized handoff time
  `0.42`. Once visible they remain attached to `hand_left` and separate only
  when the hand reaches the counter target, or after the measured closest-point
  pass. The temporary prop is snapped to the exact donor `FoodSpawnPoint` before
  authoritative fulfillment replaces it in the same location.
- Completing the below-counter, meal or store-register action now samples and
  holds the final `teimo_lean_table_in` pose at zero speed. The transition no
  longer restarts the lean-in clip after every sale or retrieval.

## 2026-08-11 exact donor Teimo choreography replacement

The hand-distance release, manually interpolated kitchen waypoints, separate
appliance-facing positions, chest-front meal carry and guessed eight-second
microwave wait above were fidelity experiments. They are superseded by this
correction and are no longer runtime behavior.

- The locked `Register/Data` PlayMaker evidence in `GAME.unity` was reduced to
  project-owned timing data and action IDs. Donor PlayMaker code/FSMs are not
  runtime dependencies.
- Under-counter delivery plays the complete six-second
  `teimo_give_drink` clip. The matching hand prop appears at `2.3 s`; the
  authoritative item/effect is created and the prop is removed at `5.3 s`.
  Ordinary goods use the exact donor `DrinkSpawnPoint` position and rotation.
  That transform is an object pivot, not a supporting-surface marker: the
  handoff backend no longer adds proxy half-height or performs a second
  collider settlement that made the delivered item hover above the counter.
- Food uses the original state order and waits: lean-out, four-second
  `teimo_move_kitchen_in` root curve plus looping `fat_walk`, ten-second cook
  gesture, fridge/open/hand/microwave stages, the donor random `55..70 s`
  heating interval plus the measured open/start/end sound-FSM durations,
  five-second `teimo_cook2`, four-second
  `teimo_move_kitchen_out` plus `teimo_walk_hand_out`, then
  `teimo_lean_bring_food`. The meal is fulfilled `0.4 s` into the final gesture
  at the exact `FoodSpawnPoint` pose.
- Movement is sampled from the donor root curves on a presentation-only proxy
  and transformed through the audited shop origin/yaw. Teimo therefore follows
  the original bent doorway trajectory and rotation while the separate body
  loop moves his legs; no project-authored `Lerp` route drives this sequence.
- `teimo_lean_table_out`, `teimo_lean_table_in`, `fridge_open`,
  `fridge_close` and `microwave_door` are now hash-locked sanitized bindings.
  Door curves are sampled relative to the runtime hinge baseline instead of
  approximated with a quaternion `Slerp`.
- Raw food uses the donor `hand_right` transform. Boxed carry uses the exact
  `ItemPivot` under `hand_left` plus the original `GrillBox` local transform;
  the meal is no longer attached in front of Teimo's face or chest.
- Return-to-counter ends in the already established lean pose without replaying
  the stand/lean transition.

## Executed validation

- `Milestone12AS1ServiceCatalogBuilder.BuildFromBatch`: passed; 7 locations,
  82 offers and 3 fuel grades.
- `Milestone10ANpcFoundationBuilder.BuildFromBatch`: passed; 20 character
  wrappers rebuilt with Teimo action bindings and the walking transition.
- `MSC.Services.Tests.EditMode`: 49/49 passed, including all 13 physical spray
  targets and streamed shelf-stock rebinding.
- `MSC.NPC.Tests.EditMode`: 34/34 passed, including the four-key floor-level
  route, 57 game-second schedule and clamped counter-lean pose.
- `MSC.Items.Tests.EditMode`: 35/35 passed, including bounded detached retail
  handoff meshes, beer-case rotation, reviewed flashlight/catalog materials and
  valid materialization of all 13 spray-paint checkout variants.
- Focused NPC presentation PlayMode: 1/1 passed; the rebuilt Teimo working clip
  advances the visible donor skeleton before clamping its final lean pose.
- Focused Teimo root-motion PlayMode: 1/1 passed; walking and
  `move-kitchen-in` advance while the animation target stays on its authored
  local pose and the project-owned logical root remains unchanged.
- Latest combined Services/NPC EditMode run after the hand-prop/route correction:
  83/83 passed. Focused Teimo root-motion PlayMode after recompilation: 1/1
  passed.
- Latest appliance/carry/release pass: NPC EditMode 34/34 and Services EditMode
  49/49 passed. Focused Teimo service-walk/working-tick/held-lean PlayMode 1/1
  passed after the private wrapper rebuild; no new compiler errors were emitted.
- Exact-donor choreography pass: the private character wrapper rebuild passed;
  NPC EditMode `34/34`, Services EditMode `49/49`, and focused Teimo donor
  clip/timing/root-motion PlayMode `1/1` passed. The focused test samples both
  kitchen motion clips and verifies their `(-6.5,0,0) <-> (-3.8,0,0.1)` ends.
- Exact pub spawn-pivot correction: Services EditMode `49/49` and Items
  EditMode `36/36` passed. The regression locks the first ordinary pub item to
  donor `DrinkSpawnPoint` without any added vertical offset.
- Physical catalog correction: Services EditMode `51/51` passed, including
  LMB/F capability coexistence, stable-item pose following and audited page
  counts when page art is unavailable. The related PlayerInteraction EditMode
  group passed `35/36`; its only failure is the separate authored-player camera
  FOV assertion (`88.5071` expected, `104.8218` actual), not catalog behavior.
- Streamed catalog presentation correction: Services EditMode `52/52` passed.
  The added regression creates both service controllers before the presentation
  provider, then verifies late page-material binding and Fleetari brochure
  materialization.
- `ItemLegacyPresentationPipeline.BuildFromMenu`: passed with 80/80 generated
  and validated bindings, 116 unique synchronized meshes and 13 remaining
  fallback material slots outside the corrected flashlight/catalog surfaces.
- `MSC.Presentation.Fluid.Tests.EditMode`: 6/6 passed after exposing the
  collision event used by Teimo's presentation-only urine-hit reaction.
- `MSC.Save.Integration.Tests.EditMode`: 37/37 passed with runtime-stable
  store/inspection location IDs and a real gasoline price in the save fixture.

## Remaining fail-visible limitations

- Three Suomi cover store offers have no project-owned vehicle/effect receiver.
  Preflight rejects them before debit; ordinary store products checkout and
  spawn normally.
- Fleetari selection is real and save-backed, but paid ordering and completed
  repair outcomes remain unavailable until a project-owned service vehicle and
  workshop outcome backend exist.
- The home catalog now browses and builds an order form, but the form is not yet
  persisted, paid, mailed or delivered. That requires the Phase 1 mail-order
  authority instead of an invented instant-spawn shortcut.
- The vodka shot is idempotently fulfilled and handed over, but does not yet
  apply an intoxication delta because no locked coefficient has been measured.
- Teimo's meal choreography now uses exact donor clips, root motion, FSM waits,
  appliance curves, hand/prop transforms and spawn poses, but still needs an
  in-world comparison for visual alignment, interruption, streaming unload and
  matching microwave/door audio.
- Vehicle fuel receivers and inspection remain unavailable until their owning
  vehicle systems exist. Open compatible canisters are supported now.

These limitations are documented rather than reported as parity-complete.
