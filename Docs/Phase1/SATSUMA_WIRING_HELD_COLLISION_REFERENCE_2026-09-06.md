# Wiring mess: original held-collision reference — 2026-09-06

## Result and scope

**The current 90 × 90 × 55 mm spool box is donor-correct. The original changes
the held spool's collision category: it does not collide with the front car
hulls that stop the remake's held-body test.** This is a general donor `PART`
pickup rule, not a special collision-disable action in the wiring FSM.

Read-only `BehavioralReference` / `ConfigurationTransferred` evidence. This
audit changes only this report: no Assets, importer execution, runtime code,
user save, global collision matrix or Unity process. It does not claim an
executed original-game physical comparison. The smallest modern binding is
being evaluated separately; the whole donor pickup policy is **not approved
for an indiscriminate port**.

## Sources and reproducibility

Frozen exported project root (below, `Export/`):
`E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/`.

Original read-only data root (below, `Original/`):
`D:/SteamLibrary/steamapps/common/My Summer Car/mysummercar_Data/`.

SHA-256 was computed directly in this audit:

| Source | SHA-256 |
| --- | --- |
| `Export/Assets/_Scenes/GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| `Export/ProjectSettings/TagManager.asset` | `147e632aa67c0082ebd22a1252268e5f225ee1d597b28dc25b89e5ba9bd616cd` |
| `Export/ProjectSettings/DynamicsManager.asset` | `0c1e8241cda99607c2df4c9ebe6c77daafdd4fd52ec99a0b1b0d3515b7f69b05` |
| `Original/mainData` (241,040 bytes) | `bdeb2298a71b45bcce81d1d91e6f3fde5c8954b5b76f88535de8fe17bdd66931` |
| `Export/Assets/Scripts/Assembly-CSharp/HutongGames/PlayMaker/Actions/SetLayer.cs` | `967366c002db7f9964e5520f45d00e55a7578dc938a5a2cbaf6c635fe4745096` |
| Same action directory, `SetParent.cs` | `993c48ab13913834179f201b19e9f0a5c398573618fc7537721b6e7f5d56ff86` |
| Same action directory, `SetJointConnectedBody.cs` | `9e6929c21cda57fc2bf5eb8851a1faede8690a7d4d3f8bc159c65eaf2eaf1fcf` |
| Same action directory, `SetIsKinematic.cs` | `93be66a3406a167b0328bf76f7dae496b2597726667a32cb16bc8f1713e6b300` |

`Original/mainData` matches `Docs/Phase1/DONOR_VERSION_LOCK.md:58`. Its matrix
bytes were read directly; no executable or platform/DRM behavior was invoked.

## Exact spool frame and its own authority

Frozen `GAME.unity`:

- GO **29115**, name `wiring mess(itemx)`, tag **PART**, layer **19 Parts**,
  active. GO block starts at line 470697.
- Transform **65162**, line 962284: local scale **(1,1,1)**. Root-local
  rotation is `(-.5870199,-.3942178,-.3942177,.58702)` and initial position is
  `(-1.0777,.9805,-13.6241)` relative to parent Transform68539. These initial
  scene coordinates are not a car-mount offset or a camera-based hold pose.
- Rigidbody **91329**, line 1372688: mass **1 kg**, drag0, angularDrag .05,
  `useGravity=true`, `isKinematic=false`, constraints0,
  collisionDetection serialized1.
- BoxCollider **93841**, line 1404552: enabled, **not a trigger**,
  size **(.09,.09,.055) m**, center **(0,0,.013) m**, no explicit material.
  Size and center are in the **spool root's local frame**. Rotation changes
  which side of the actual pivot has the extra 13 mm; a visual AABB or a
  quarter-meter provisional item proxy is not this collider.
- Only attached FSM: **112423**, `Use`, line 7246250. Its sole state contains
  `GetOwner` and `SetGameObject`: it publishes the owner into global
  **WiringTool**. There is no collider toggle, trigger switch, layer action,
  kinematic action or car-collision exception in this FSM.

Current `Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset`
`item.wiring-mess` has mass1 and the same explicit box at line4490. Its older
`.25 m` `proxySize` is not the authored physical box. This report does not
propose shrinking the box, recentering it or changing the wiring distance.

## The active original pickup path

Do not confuse two similar hand objects:

| Object | Frozen status |
| --- | --- |
| **Hand GO32546**, Transform68605, `PickUp` **FSM113435** | Active; parent **1Hand_Assemble GO7209 / Transform43266**, also active. This is the inspected authority. |
| HandOld GO13461, `PickUp` FSM107978 | GO inactive; not used to establish current behavior. |

FSM113435 begins at GAME line7869342. The spool's actual **PART** tag establishes
the path, regardless of the remake's `Tool` item category or its `(itemx)` name:

`Check if Item` (tag ITEM fails) → `Check if Part` (tag PART succeeds) →
`Is Jonnez` (spool is not the explicit Jonnez object) → `Set pivot` →
`Part picked`. The special `block(Clone)` motor branch in `Set pivot` also
does not match the spool.

`Set pivot` records the picked-object position and positions the hand/ItemPivot.
`Part picked` executes, in order, a vector reset, `SetLayer`, `SetParent`,
`SetJointConnectedBody`, then drop/throw input listeners:

- `SetLayer` targets **PickedObject**, with raw integer **16** at its
  `byteData` offset **0x29** (`paramName=layer`, size4). TagManager names this
  layer **Wheel**. The inspected action implementation assigns only the target
  GameObject's `.layer`; it does not recursively change children or geometry.
- `SetParent` targets PickedObject, parent **ItemPivot GO408**, without
  resetting local position/rotation. Its inspected implementation uses
  `transform.parent`, not a collision toggle.
- `SetJointConnectedBody` targets the owner's joint and sets its connected
  body to PickedObject's Rigidbody. The actual component is **FixedJoint102024**
  (GAME line1659535), **not ConfigurableJoint**. Hand Rigidbody91543 is
  kinematic, gravity-disabled, mass1. Joint break force/torque are infinite;
  `enableCollision=false`, `enablePreprocessing=false`.
- The ordinary spool pickup route does not toggle its BoxCollider, trigger
  flag or gravity/kinematic fields. Its dynamic body is constrained to the
  kinematic hand through that FixedJoint.

`Drop part` and `Drop part 2` clear the joint connection, remove the temporary
parent, restore a dynamic picked body and set its layer back to **19 Parts**.
The layer integer is **19** at `byteData` offset **8** in both states. The
throw variant then continues into the ordinary mass-dependent force branch.
No special wiring Use/drop lifecycle resets a wire connection here.

## Collision matrix: decoded and independently checked

The exported `m_LayerCollisionMatrix` uses a malformed signed-hex spelling in
some nibbles (`,` / `-` / `.` / `/`). For this frozen field the diagnostic
normalization is `,→c`, `-→d`, `.→e`, `/→f`, followed by ordinary hex decode.
This is a **source-format diagnostic**, not a general runtime parser or a
license to guess other malformed fields.

Validation performed:

1. Result length **128 bytes** = 32 little-endian UInt32 layer rows.
2. For all 32×32 pairs, `(row[i] >> j) & 1` equals `(row[j] >> i) & 1`:
   **zero asymmetric pairs**.
3. That **entire exact 128-byte sequence occurs once in the hash-locked
   original `mainData`, at zero-based byte offset34912**. The exported-field
   interpretation is therefore corroborated by original bytes, not only by
   a plausible layer name or symmetry.
4. SHA-256 of the 128 bytes:
   **`08ca0de7991d7d82c6c9b1d2fc034236cc5ad1c97b12ad2728dd7a3e66889667`**.

Row16 is **0xC0000CC9**, row19 is **0xC06806CD**. Relevant decoded interactions:

| Other original layer | Held spool16 | Loose spool19 |
| --- | --- | --- |
| 0 Default (ordinary world solids) | Collides | Collides |
| 9 HingedObjects | Ignores | Collides |
| 10 Terrain | Collides | Collides |
| 16 Wheel | Ignores | Ignores |
| 17 Collider | Ignores | Ignores |
| 18 Datsun | Ignores | Ignores |
| 19 Parts | Ignores | Collides |
| 20 Player | Ignores | Ignores |
| 21 DynamicBodies | Ignores | Collides |
| **22 Collider2** | **Ignores** | **Collides** |
| 23 PlayerOnlyColl | Ignores | Ignores |
| 25 Glass | Ignores | Ignores |

The complete set of bits enabled in row16 is **0,3,6,7,10,11,30,31**. Several
are unnamed/reserved in this TagManager; no semantic interpretation of those
reserved layers is made. The original has not made the held spool nonphysical:
ordinary Default/Terrain contact remains enabled.

## Proven front-hull mappings in the current canonical prefab

Both frozen source GOs are enabled **layer22 Collider2**, with enabled,
non-trigger, convex MeshColliders. This establishes that the original held
spool ignores precisely the two hulls identified by the contact diagnostics.

Current prefab:
`Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab`.

| Original evidence | Current serialized reference (audit checkpoint) |
| --- | --- |
| GO5770, Transform41828, **MeshCollider91938**, `collider_front`, GAME line93076 | Generated GO5072345507336463588, Transform3690705893695516681, **MeshCollider2780350095703386140**, mesh GUID `e5b8d74d35f3edf886d860616ef6adbf` |
| GO31933, Transform67992, **MeshCollider92698**, `collider_fender_in_right`, GAME line516350 | Generated GO4541553459332502824, Transform8415705552854753497, **MeshCollider2668852539468047906**, mesh GUID `08a407b85bdc66aece013e6d3e666d2d` |

The generated colliders are currently on **modern layer0**, with per-collider
exclude bits512; copying original layer-number16 into the remake is therefore
not a valid semantic translation. Their geometry is flattened into the vehicle
frame (approximately positionY=-.539191, rotation=(0,.7071068,.70710677,0));
do not apply a spool-local offset in that frame.

These Unity fileIDs and generated names are **Editor/audit locators**, not new
persistent IDs and not runtime lookup instructions. Runtime code must receive
explicit serialized Collider references from project-owned authoring. The
existing vehicle `CarryCollisionBypassScope` already references front collider
2780350095703386140; this fact alone does not make an unrelated loose spool a
child of that scope or prove that scope discovery will select it.

The parallel read-only lifecycle audit additionally cross-checked the exact
**21 source-layer22 chassis shapes** against their original GO layer fields.
Their current canonical references were then resolved from the existing
29-entry scope without changing it:

| Source collider / diagnostic name | Current Collider fileID |
| --- | --- |
| 91784 `collider_floor` | 2426652824721967365 |
| 91847 `collider_roofright` | 5409829865517052756 |
| 91867 `collider_fender_in_left` | 1454762837190187513 |
| 91869 `collider_wheelwell_right` | 8622838834133956300 |
| 91871 `collider_rear` | 2057076239990979871 |
| 91938 `collider_front` | 2780350095703386140 |
| 92003 `collider_firewall_right` | 6914369566790125843 |
| 92187 `collider_floor2` | 5597161648936222179 |
| 92200 `collider_firewall` | 949446353584813432 |
| 92201 `collider_rear_left` | 2040552009184837321 |
| 92207 `collider_pillar_left` | 1484454904067569160 |
| 92284 `collider_roof` | 2071659355208888617 |
| 92311 `collider_left` | 669738231838286590 |
| 92334 `collider_rear_right` | 3545488953384474276 |
| 92417 `collider_floor3` | 1420832522787432207 |
| 92590 `collider_firewall_left` | 6330036034958541940 |
| 92604 `collider_pillar_right` | 4001496779123510087 |
| 92606 `collider_roofleft` | 4087700418907878231 |
| 92698 `collider_fender_in_right` | 2668852539468047906 |
| 92700 `collider_right` | 7298318019489079908 |
| 92736 `collider_wheelwell_left` | 7815804036320653808 |

This is the evidence-backed chassis **layer22 subset**, not a request to use
the entire existing 29-entry scope. In particular `collider_rearwindow_92252`
belongs to original layer2, not22. The remaining differently categorized
scope shapes, loose/installed part colliders and other vehicles require a
separate explicit scope decision. These generated fileIDs are a checkpoint
map only; generation may change them, while runtime must use actual serialized
component references rather than donor names or numbers.

## Bounded implementation constraints for the integrating task

`Assets/Game/Interaction/Runtime/Carrying/CarryCollisionBypassScope.cs` already
holds an explicit collider array. `PhysicalCarryController` finds a scope from
the held object's ancestry and applies/restores exact `Physics.IgnoreCollision`
pairs while held. This is the compatible seam to evaluate; no fake installed
assembly part, graph rebuild, source-cell reassignment or global matrix port is
required merely to express the measured held collision exception.

The following limits remain mandatory:

- **Physics layer filtering is not ray-query occlusion.** Neither the original
  layer transition nor this report authorizes skipping query occluders,
  promoting invisible/unreachable interaction targets, or selecting through a
  foreign wall. PhysX `IgnoreCollision` alone is not such an interaction rule.
- Preserve the **actual physical WiringTool position**, existing **0.1 m**
  endpoint tolerance, candidate reach filtering and F / endpoint-cluster
  handshake. Do not fix contact failure by inflating tolerance, moving source
  endpoints or completing missing wires automatically.
- Only an explicit, reviewed collider set may be bypassed, **only while the
  wiring item is actually carried**. Default world/ground solids remain solid;
  release, throw, disable, source streaming/restore and target loss must restore
  the ordinary loose-state pairs without leaving leaked exceptions.
- The proof covers the original general PART policy but does not authorize
  migrating that policy for every held engine/body/item. A spool-only modern
  binding is a deliberate bounded compatibility adaptation, not a claim that
  the donor had a wiring-only exception.
- Current row16 also ignores layer19 parts; the two layer22 hull references
  above are not an assertion that those are the only possible lamp-side
  contacts. Any additional collider binding needs its own source-layer and
  owning-vehicle evidence. Do not include every scene collider by convenience.

Acceptance for a separate implementation: actual carry + F wiring fixture
reaches both individual headlights; foreign-wall negative remains blocked;
ground contact remains active; loose spool again contacts the relevant hulls;
release/throw/disable/restore do not leak ignored pairs; other held items and
accepted suspension/engine pickup behavior are unchanged. NUnit/Unity and
manual original comparison were **not** executed by this audit.
