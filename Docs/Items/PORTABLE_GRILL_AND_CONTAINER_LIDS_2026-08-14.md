# Portable grill fuel and container lids — 2026-08-14

## Scope and ownership

This is an outside-milestone correction requested alongside the Expanded Shop
pass. It does not change the milestone sequence. Runtime authority remains the
project-owned Items, Interaction, PhysX, Save and Audio boundaries; donor FSMs
are read-only behavioral/configuration evidence and are not loaded.

Locked scene evidence is
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`
at the established GAME hash
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

Relevant donor records:

- grill charcoal trigger FSM component `108771`;
- `Assets/GameObject/grillcharcoal0.prefab`, SHA-256
  `D4A0C3AD8F4473F0E6346FE79282BFE3F896811C20C5B3382925152F3337E98E`,
  with reviewed `Rigidbody.mass = 1.8`;
- grill ignition `ITEMS/grill(itemx)/Fireplace/SetFire`, Use FSM component
  `114344`;
- grill cover
  `ITEMS/grill(itemx)/CoverPivot/grill_cover`, stable evidence ID
  `b31242fd3bf7e9ae115d9ede47303e8f`;
- grill `FireTrigger` relative pose, with center
  `(-0.017, 0, 0.111)`, size `(0.5, 0.5, 0.24)` and local `+Z` as world-up;
- grill charcoal mesh
  `ITEMS/grill(itemx)/Fireplace/HiillosPivot/Pivot/HiillosMesh`, stable
  evidence ID `1ab5a87f5233c1fd5ee38ce380a71e6e`;
- separate kilju lid item `item.kilju-lid`, canonical stable entity
  `216a0dc4d7da58e5ae2fe6d1784ff54c`.

## Transferred configuration and behavior

The donor charcoal package owns `140` units, its reviewed Rigidbody mass is
`1.8 kg`, the grill capacity is `100`, and the pour rate is `12` units per
second. The serialized rotation checks use the package X-angle band beginning
at `80` degrees. The project implements this as
an orientation-independent local-up tilt threshold of `80` degrees while the
package physically overlaps the authored grill heat/cavity volume. This is a
documented `Reimplemented` difference that also works when the carried item is
rotated around another local axis.

Ignition remains an ordinary F action on the grill body. No matchbox/lighter
item was invented: the locked SetFire FSM itself uses `Use`. Ignition requires
strictly more than `10` fuel units and fails while the saved `wet` flag is set.
The visible-flame phase lasts `120` real seconds and consumes `0.1` fuel units
per second. It then becomes cooking-hot embers at `0.04` units per second and
switches off at `<=5` units. Refuelling while burning or embering is rejected.

`ItemCombustionDefinition` owns all of these values. Runtime state reuses the
existing versioned item DTO:

- `content` — current charcoal amount;
- `isEnabled` — burning or embering heat state;
- scalar `burn-time` — remaining visible-flame seconds;
- flag `wet` — ignition/extinguish condition;
- `isOpen` — independent lid state.

The generic heat-source adapter therefore heats loose sausages during both the
flame and ember phases without a grill-name switch. Save schema versions and
stable IDs do not change.

## Presentation and interaction

- The grill cover uses the exact donor pivot
  `(0.2045002,-0.000000007581667,0.063599624)` and reviewed child pose. Its
  `105` degree travel and `220 deg/s` speed are project-owned provisional
  interaction presentation. The user confirmed the current hinge/opening is
  correct; this correction does not alter the cover object, pivot or motion.
- The grill body remains pickup-capable and owns ignition; aiming at the cover
  exposes a separate ordinary F target backed by `isOpen`.
- The kilju bucket body remains pickup/liquid authority while its separate F
  lid target adopts the canonical lid presentation without replacing the lid's
  stable identity or save record. The bucket hinge/travel are project-owned
  because the donor lid is a separate loose item.
- Empty kilju/sauna buckets disable the reviewed water/ingredient helper
  renderers. The blue water surface appears only when authoritative liquid
  content exists. Empty grill charcoal geometry is likewise hidden.
- Active flame uses the established HDRP fire presenter at the exact reviewed
  `FireTrigger` center and aligns particle up to the grill's local `+Z`, instead
  of treating local `+Y` as vertical and throwing the fire beside the bowl.
  The grill-specific data profile scales the preset to `0.1728`, applies `16%`
  of the barrel emission density, and limits its point light to `145 / 1.65 m`.
  The garbage-barrel profile is unchanged. Embers retain heat while visible
  flame is disabled.
- Interaction feedback remains routed through the existing interaction/audio
  boundary. A dedicated continuous charcoal-pour Wwise event is not yet
  authored; batch mode cannot validate final Wwise output.

## Automated evidence

- item catalog builder: passed, 152 valid definitions; the generated charcoal
  record carries the reviewed `1.8 kg` mass;
- sanitized item presentation builder/validator: passed, `147` bindings
  (`128` reviewed base-item bindings plus `19` Expanded Shop bindings);
- Items EditMode: `53/53` passed, including physical overlap + 80-degree tilt,
  exact rates/thresholds, wet rejection/extinguish, flame-to-ember transition,
  independent lids, state-driven content surfaces and state round-trip;
- Economy EditMode: `5/5` passed;
- Services EditMode: `57/57` passed;
- Save Integration EditMode: `37/37` passed;
- Fluid/fire presentation EditMode: `7/7` passed;
- production-Bootstrap food PlayMode: `2/2` passed.

Manual in-game acceptance remains required for carry-angle feel, coal stream
feedback, cover collision at extreme poses, rendered flame placement, official
Wwise output and a real user save slot. These rows remain
`PartiallyImplemented`, not `Verified`.
