# Expanded Shop extension — 2026-08-14

## Scope and ownership

This is an outside-milestone, user-requested extension. The supplied
`ExpandedShop.dll` is a read-only third-party behavioral/configuration and
presentation reference. Neither that assembly nor its complete embedded
AssetBundle is copied into the Unity project or loaded by the game. The Editor
pipeline copies only the hash-locked mesh/material/texture closure needed by
the selected product prefabs into the ignored private Phase-1 runtime baseline,
then emits sanitized project wrappers. Runtime authority remains the
project-owned Items, Economy, Services, Interaction and Save systems.

Evidence locks:

- `ExpandedShop.dll` SHA-256:
  `2E51250495D97E628AE98A8892B733FE76C775A7278D104DD73E2BE719789A7E`;
- embedded `drinks` AssetBundle SHA-256:
  `997E3F2D227AAED732B91DAAF2EB40EE180D75A9051319813E28B534B3AB0773`;
- selected presentation bindings and prefab hashes are locked in
  `Assets/Game/LegacyImport/Manifests/ExpandedShopPresentationManifest.json`;
- the complete authored shelf-layout prefab is additionally locked at SHA-256
  `E2D111BEFBED27315A57BBB1DD9778B9D64B95F66BD62D953BF491A0B0DD44AE`;
- bundle export and inspection output stays outside Git under the ignored
  donor staging root.

Managed IL proves that `ShopBuying.Inventory` is the last valid child index,
not a count. Purchase is permitted while the value is `>= 0`, then decrements
it. Effective shelf capacity is therefore `Inventory + 1`; this is why the
serialized can-opener value `0` means one purchasable opener. Mustard and
ketchup each have two shelf sections in the mod and are represented as one
project offer with the two capacities summed.

## Transferred catalog

| Product | Price (MK) | Effective capacity | Project item definition |
|---|---:|---:|---|
| Buttermilk | 5.25 | 25 | `item.buttermilk` |
| Orange juice | 7.49 | 11 | `item.orange-juice` |
| Bug spray | 69.95 | 8 | `item.bug-spray` |
| Laundry detergent | 19.95 | 5 | `item.laundry-detergent` |
| Sponge | 7.29 | 9 | `item.sponge` |
| Shampoo | 6.95 | 6 | `item.shampoo` |
| Hand soap | 5.49 | 6 | `item.hand-soap` |
| Dish soap | 9.49 | 7 | `item.dish-soap` |
| Soap | 8.75 | 9 | `item.soap` |
| Wheat flour | 6.49 | 10 | `item.wheat-flour` |
| Rye flour | 6.99 | 9 | `item.rye-flour` |
| Mustard | 7.95 | 8 | `item.mustard` |
| Ketchup | 8.95 | 11 | `item.ketchup` |
| Meat soup | 9.79 | 8 | `item.meat-soup` |
| Pea soup | 8.49 | 15 | `item.pea-soup` |
| Canned meatballs | 10.99 | 14 | `item.canned-meatballs` |
| Sausage | 15.79 | 4 | existing `item.loose-sausage` |
| Fishstick box | 9.95 | 39 | `item.fishstick-box` |
| Can opener | 79.00 | 1 | `item.can-opener` |

The extension adds nineteen retail offers and price records. It adds eighteen
purchasable root definitions plus `item.fishstick`; the sausage offer reuses
the established loose-sausage definition and its freshness/cooking/save
behavior. A fishstick box contains the mod-evidenced ten physical sticks.
Fishsticks use the existing generic food simulation with the mod defaults of
40 seconds to cooked plus 10 seconds to burned, and the captured ambient/fridge
decay rates `0.1 / 0.01`.

## World and interaction integration

The mod instantiates `TeimoDrinksMod` at the donor `STORE` root with identity
local transform. The exact project STORE root is
`(-1378.33, 4.941, 142.245)`. The importer transfers all `21` physical
`ShopBuying` groups, not merely the `19` unique offers: mustard and ketchup
retain both of their independently placed groups. Every group preserves its
source local position, quaternion, scale, exact `Col` child transform,
BoxCollider centre/size/trigger flag, `Inventory` child order and every stock
unit transform. Runtime uses only stable offer/group IDs and never searches the
donor/mod hierarchy.

AssetRipper emitted two duplicate component file IDs in the source
`TeimoDrinksMod.prefab`, so that prefab is deliberately never imported as a
Unity asset. The Editor pipeline parses the hash-locked YAML as evidence and
emits clean project-owned group prefabs. This keeps the duplicate IDs, missing
mod scripts and old runtime components outside the project asset database.

The Editor importer builds nineteen sanitized presentation bindings: eighteen
Expanded Shop purchasable roots plus the spawned physical fishstick. The loose
sausage offer deliberately uses the reviewed original-game sausage wrapper
instead of the mod's duplicate sausage prefab. Generated wrappers contain only
`Transform`, `MeshFilter` and `MeshRenderer`; mod scripts, old Rigidbody,
colliders, tags, layers and state machines are excluded. Source materials are
converted to project-owned HDRP/Lit materials.

Checkout-spawned items still resolve through the normal
`ItemPresentationProvider`. Shelf presentation instead uses `20` sanitized
group prefabs generated directly from the exact mod layout (`21` groups minus
the sausage collider-only group). A child `Col` resolves its parent
`InteractionTargetHost`; its group's renderers provide hover outline feedback,
and `ServiceRuntime` stock drives the exact ordered unit range. Mustard uses
ranges `0..4` and `5..7`; ketchup uses `0..6` and `7..10`. The sausage entry
creates no fake shelf sausage because its mod group has no `Inventory` or
renderer. Purchased items keep the normal `ItemWorldRuntime`
Rigidbody/collider/pickup/save materialization.

## Persistence compatibility

Service state remains schema-compatible. Restore makes a defensive clone and
fills only retail lines absent from an additive catalog extension at the new
offer's full capacity; unknown, duplicated, out-of-range or malformed records
still fail normal validation. Existing item instances, basket lines and saved
world poses are not rewritten.

## Deliberate boundaries

- This is not a full port of every utility-use mechanic in the mod. Purchase,
  physical pickup, stock and persistence are implemented; the can opener owns
  the project tool identity `CanOpener`, while a future generic can-opening
  capability can consume that identity without changing the catalog or saves.
- The selected third-party models/textures are `TemporaryDirectImport` for the
  private Phase-1 baseline, not production art. Every wrapper remains
  replaceable by the existing project `replacementKey`.
- The mod's apparent dish-soap/hand-soap script mix-up was not copied as runtime
  authority.

## Automated evidence

- item catalog build: 152 valid definitions, including 19 extension records;
- economy build: 38 locked donor prices plus 19 extension prices;
- service build: 101 offers, including 60 physical Teimo shelf offers;
- sanitized item presentation builder/validator: `147` bindings passed —
  `128` reviewed base-item bindings plus `19` Expanded Shop bindings;
- Items EditMode: `53/53` passed;
- Needs EditMode: `16/16` passed, including the data-driven Drink routing
  for milk, both juices, buttermilk, mustard and ketchup;
- Economy EditMode: `5/5` passed;
- Services EditMode: `57/57` passed, including exact catalog/fixture coverage,
  checkout contracts, renderer-backed stock and additive stock migration;
- Save Integration EditMode: `37/37` passed;
- production-Bootstrap food PlayMode: `2/2` passed.

### Exact shelf-layout correction evidence

- generated layout: `21` physical groups / `19` unique offers / `20`
  sanitized shelf visual prefabs;
- isolated Editor rebuild:
  `EXPANDED_SHOP_PRESENTATION_BUILD_OK bindings=19 physicalGroups=21`;
- focused `ProductionServiceInteractionInstallerTests`: `5/5` passed;
- the focused test resolves every child BoxCollider back to the correct parent
  interaction capability, verifies renderer outline ownership, exact
  buttermilk world pose/quaternion, duplicate mustard offsets `0` and `5`, and
  live stock hide/restore across the second mustard group;
- the broader item-presentation pipeline generated the catalog successfully,
  then its pre-existing global validator stopped on the unrelated
  `VehicleJackInteractionController` allow-list rule for the car-jack prefab.
  That validator result is not claimed as passed by this correction.
