# Garage daylight zone correction — 2026-08-14

## Problem

The three garage fluorescents were electrically on but looked almost inert in
daylight while the temporary donor garage remained too dark. The lighting zone
was inferred only from lamp positions (`3.50 x 3.00 x 8.08 m`) and did not match
the audited indoor weather volume (`4.59 x 2.36 x 9.50 m`). The weather volume
also ended about `0.43 m` behind the reviewed garage-door threshold, so a camera
standing inside the opening still used exterior fixed exposure.

## Correction

- `WorldLightingBindingCatalogBuilder` now treats the audited garage interior
  as the authoritative base bounds.
- A narrow `GarageEntryExposureTransition` overlaps the interior and reaches
  the reviewed door threshold plus a `0.12 m` blend lip. Its side inset keeps
  the correction inside the door opening instead of brightening the yard.
- `zone.home.garage` is the union of the audited interior and transition:
  `4.59 x 2.51 x 10.05 m`.
- The no-bake garage multiplier is now `6x`, producing approximately
  `16.2 klm` per fluorescent area source from the captured `1.8 klm` floor.
  The earlier `4x` pass remained visually weak once the rejected fixed garage
  exposure was removed.

No stable ID, switch, electrical circuit, fuse, billing load, save field,
streaming identity, fixture position, colour temperature, shadow policy or
donor asset was changed.

## Validation

- Lighting binding generator: PASS twice, 49 accepted streamed lights rebound
  and exactly one doorway transition remained after the repeat build.
- `MSC.Lighting.Tests.EditMode`: PASS, `36/36`.
- The full Bootstrap lighting PlayMode test reached an unrelated existing
  failure at `ProductionLightingLifecycleTests.cs:259`: the public street
  circuit did not power the streamed Teimo lamps. It failed before reaching
  the garage assertions, so no PlayMode PASS is claimed for this correction.
- Manual rendered validation remains required at noon with the player at the
  garage threshold, centre and workbench, with the garage switch both off and
  on.

Classification: `Reimplemented`; the frozen garage shell remains a
`DimensionalReference` and its evidence AABB is unchanged.
