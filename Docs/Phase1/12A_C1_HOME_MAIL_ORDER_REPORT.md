# 12A-C1 Home Mail Order Report

Date: 2026-08-13  
Status: implemented; latest catalog-fidelity correction passed automated
revalidation and awaits manual Play Mode acceptance of the full delivery route.

## Inspected donor evidence

- Physical magazine: `ITEMS/parts magazine(itemx)` with eight product page groups and 46 order hit regions.
- Envelope: `ITEMS/parts magazine(itemx)/EnvelopeSpawn/envelope(xxxxx)`.
- Submission target: `STORE/LOD/Post Box/OrderTrigger`, converted position `(-1378.4673, 6.2990003, 138.4563)`.
- Payment target: `STORE/LOD/ActivateStore/PostOffice/PostOrderBuy`, converted position `(-1381.2434, 6.2384853, 142.10864)`.
- Delivery presentation: all 46 reviewed roots under `STORE/Boxes/*`.
- Delivery FSM evidence: `Arrival` is rolled in the range 1999-2000. The current project-owned implementation provisionally treats the values as game minutes pending a narrower donor clock-unit trace.

Donor scripts, PlayMaker FSMs, controllers and runtime assemblies are not used by the remake runtime.

## Implemented flow

1. The physical home magazine opens over its actual world transform and displays four two-page product spreads plus one final order-form spread.
2. Product selections are stored in `ServiceStateDto.homeMailOrder`; the final form lists selected lines and deliberately does not show a total.
3. Pressing F on a non-empty final form creates a physical, saveable envelope beside the magazine. No money is charged yet.
4. The exact envelope is carried to Teimo's project-owned interaction wrapper at the donor `OrderTrigger`; successful handoff removes the item and starts the delivery clock.
5. When ready, payment is accepted at the donor `PostOrderBuy` position while the store is staffed.
6. Payment is idempotent. If physical delivery materialization fails after debit, retrying does not charge a second time.
7. Ordered content materializes as deterministic, saveable ordinary `WorldItemInstance` objects. The flow has no dependency on a Satsuma instance, mount point or vehicle assembly backend.

## Presentation policy

- The envelope and all 46 ordered definitions use generated `TemporaryDirectImport` wrappers.
- Mail-order part extraction excludes the donor package `_gfx` mesh.
- An active unpacked part representation is preferred. For donor boxes whose part root remains disabled, reviewed inactive part geometry is used.
- Delivered items are placed in a ground-plane grid instead of an unsupported vertical stack.

## Automated checks executed

- Unity compile: exit 0, `Logs/mail_order_compile_6.log`.
- Focused EditMode tests: 31 passed, 0 failed, 0 skipped, `Logs/mail_order_tests_3.xml`.
- Item catalog build: exit 0; 133 definitions, including the envelope and 46 mail-order definitions, `Logs/mail_order_catalog_build_3.log`.
- Presentation plan: 127 bindings and 182 mesh GUIDs, exit 0, `Logs/mail_order_presentation_plan_3.log`.
- Presentation build/validation: 127 bindings, exit 0, `Logs/mail_order_presentation_build_2.log`.
- Catalog-fidelity presentation rebuild: 127 bindings, exit 0,
  `Logs/catalog_fidelity_item_build.log`.
- Focused physical page-turn and carried-envelope teleport tests: 2 passed,
  0 failed, 0 skipped, `Logs/catalog_fidelity_tests.xml`.

## Manual Unity acceptance still required

1. Open `Assets/Game/Bootstrap/Bootstrap.unity` and enter Play Mode.
2. At home, place the magazine, press F, turn every spread and select several differently sized parts.
3. On the final spread verify the written lines, absence of `ИТОГО`, and press F to create the envelope.
4. Pick up the envelope with LMB and hand it to Teimo's outside order box.
5. Advance at least 2000 game minutes, enter during store hours and pay at the post-order counter.
6. Verify delivered items rest on the ground, have the correct unpacked presentation, can be picked up and survive save/load and streaming.

## Compatibility and remaining risk

- Existing 00-08A APIs, stable IDs, scenes and save domains were extended rather than replaced.
- `ServiceStateDto` keeps schema version 1 and normalizes a missing `homeMailOrder` field, so existing saves restore an empty order state.
- Satsuma assembly can later attach compatibility metadata to the delivered item definitions without migrating transaction or item identity.
- Manual visual/collider acceptance, exact donor delivery time units, donor audio, incoming phone/mail and bills remain pending.

## 2026-08-13 catalog-fidelity correction

- Logical spread order remains unchanged. Only the physical sheet motion was
  corrected: forward browsing now lifts the right sheet and turns it left;
  backward browsing performs the inverse motion.
- The temporary green selection cube was replaced by the hash-locked donor
  `x.png` alpha-cut mark.
- The home order form no longer uses one oversized multiline text object.
  It creates one row per selected offer at the 46 transferred donor
  `Sheets/Magazine/Products/*` positions, with donor `RAGE`, character size
  `0.0025` and colour RGBA `(57,64,208,255)`. Prices and total are absent from
  these handwritten product rows.
- Developer-menu teleport now asks `PhysicalCarryController` to atomically
  realign the held Rigidbody after moving the player. This preserves a carried
  mail-order envelope instead of dropping it beyond the 3.5 m separation gate.
- Source hashes for `x.png`, `RAGE.asset`, its material and native atlas are
  recorded in `Phase1ItemSurfacePresentationManifest.json`; generated payload
  remains ignored below `LegacyImport/RuntimeBaseline`.
- The generated provider rebuild and focused EditMode checks completed without
  errors. Manual end-to-end acceptance remains because it covers world-space
  placement, store hours, time advancement, delivery pickup and persistence.

Recommended next milestone: `11A-V1` physical Satsuma baseline plus a compatibility adapter for the already delivered mail-order item definitions.
