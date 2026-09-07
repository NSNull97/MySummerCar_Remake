# Unity 6000.6.0f1 — bounded vendor API compatibility

Status: local source compatibility patch, **not an official vendor upgrade**.
Target: installed Unity `6000.6.0f1`. Existing versions remain NWH Vehicle
Physics 2 `v13.5` and Wwise SDK `2025.1.9.9197` / Unity bundle `2025.1.9.4241`.

## Evidence and scope

First import (`Logs/unity660-first-import-20260906.log`) fails with CS0619:
`Object.GetInstanceID()` is an error-obsolete API in Unity 6.6. Initial failures
were NWH `NUIDrawer` and Wwise `AkDelegates`; a complete source search found the
same API behind dependent Wwise/NWH assemblies and optional Timeline branches.

The installed editor's `Editor/Data/Managed/UnityEngine.xml` and
`UnityEditor.xml` confirm the replacements: `Object.GetEntityId()`,
`EntityId.ToULong(EntityId)` returning `ulong`, `EntityId.FromULong(ulong)`,
`EditorUtility.EntityIdToObject(EntityId)`, and
`ModifiableContactPair.bodyEntityId` / `otherBodyEntityId`.
These local version-matched API references are authoritative for this patch;
older online Unity 6.0 pages do not describe this changed API contract.

All changes use `UNITY_6000_6_OR_NEWER`; existing older-version branches remain.
Identity is retained as the full `EntityId` or raw 64-bit value, never a hash or
truncated integer. These IDs are session-local; no save/stable-ID schema changes.

## Exact source changes

Paths below are relative to the repository root.

| Source | Delta |
|---|---|
| `Assets/NWH/Common/Scripts/NUI/NUIDrawer.cs` | Both inspector cache-key overloads stringify `GetEntityId()` on 6.6. |
| `Assets/NWH/WheelController/Scripts/WheelController.cs` | Cached contact rigidbody identity is `EntityId`; contact ownership compares the new body identity properties. The contact modification algorithm is unchanged. |
| `Assets/Wwise/Utilities/Runtime/AkDelegates.cs` | Destroyed-target diagnostic prints `GetEntityId()` instead of removed API. Delegate invocation/unsubscription behavior unchanged. |
| `Assets/Wwise/API/Runtime/Handwritten/Common/AkUnitySoundEngine.cs` | Default live GameObject identity becomes `EntityId.ToULong(gameObject.GetEntityId())`; null still returns `AK_INVALID_GAME_OBJECT`. User-supplied hash delegate and registration/event flow unchanged. |
| `Assets/Wwise/API/Runtime/Handwritten/Common/AkCallbackManager.cs` | Editor monitor resolves the full callback `ulong` through `EntityId.FromULong`; no `(int)` narrowing on 6.6. Legacy 6.3 branch retained. |
| `Assets/Wwise/MonoBehaviour/Runtime/AkRoomPortal.cs` | Portal native ID uses full raw `EntityId`. |
| `Assets/Wwise/MonoBehaviour/Runtime/AkSurfaceReflector.cs` | Reflector ID and obsolete-but-compilable mesh-filter geometry ID use full raw `EntityId`. |
| `Assets/Wwise/MonoBehaviour/Editor/AkPortalManager.cs` | Editor intersection-cache keys use `EntityId`; older-version alias remains `Int32`. Cache has no external consumers in audited source. |
| `Assets/Wwise/MonoBehaviour/Editor/WwiseSetupWizard/AkWwiseSetupWizard.cs` | Both object-migration loops keep full identity lists and use matching editor object resolution. No migration is executed by this patch. |
| `Assets/Wwise/Timeline/Runtime/AkTimelineEventPlayable.cs` | Editor-only clip-update identity list and comparisons use `EntityId`. |
| `Assets/Wwise/Timeline/Editor/AkEventPlayableInspector.cs` | Legacy Timeline inspector identity list/comparisons use `EntityId`. |

Wwise C# sources are already tracked, so their focused Git diffs carry the
compatibility fix. No Wwise binaries, banks, event IDs, routing, RTPCs, settings,
native plugins, vendor version declarations or audio semantics were changed by
this patch. The DLL `.meta` change already present at handoff is outside this
patch. **Enviro vendor source remains untouched.**

## Reproducing ignored NWH changes

NWH is licensed local content and remains ignored. Do not force-add full vendor
sources. Commit only the small project-owned delta and its fingerprints:

- `Tools/Compatibility/NwhUnity660.patch`
- `Tools/Compatibility/NwhUnity660.hashes.json`

The input is the project's accepted pre-upgrade NWH v13.5 source (including its
existing PhysicMaterial API compatibility fix), not a newly reimported arbitrary
package version. Verify its SHA-256 against the manifest; normalized hashes
allow CRLF/LF checkout differences without ignoring actual source differences.

From the project root, first check whether the exact patch is already present:

```powershell
git apply --reverse --check Tools/Compatibility/NwhUnity660.patch
```

If that succeeds, do nothing. For an unpatched matching baseline only:

```powershell
git apply --check Tools/Compatibility/NwhUnity660.patch
git apply Tools/Compatibility/NwhUnity660.patch
```

Then verify normalized output hashes. Never force or fuzz-apply against a
different vendor version; re-audit the two affected methods instead. Unity must
be closed while restoring source. The patch contains no machine-specific paths.

## Executed checks and remaining validation

- Audited `GetInstanceID`, object resolution, scene handles and contact body IDs
  throughout `Assets/NWH` and `Assets/Wwise`; remaining old calls are guarded
  legacy branches.
- `git diff --check -- Assets/Wwise`: pass (only existing CRLF advisory output).
- `git apply --check` against the pre-import backup: pass, read-only.
- `git apply --reverse --check` against the patched project: pass, read-only.
- Both original byte hashes match the pre-import backup; normalized before and
  after hashes recorded in the manifest.
- Main-task `Logs/unity660-compile2-20260906.log` reached all audited vendor
  assemblies with no NWH/Wwise `error CS` entries (overall project still failed
  on other assemblies). Its three new `GetRawData()` obsolescence warnings led
  to replacement with the non-obsolete static `EntityId.ToULong(EntityId)` API.
  Existing vendor API/serialization warnings are not silently suppressed or
  misrepresented as fixed by this bounded patch.
- **No Unity/test processes were launched by the vendor compatibility task.**
  Compilation, live Wwise registration/listener/event playback and NWH contact
  regression runs are owned by the main upgrade task; source checks alone are
  not a claim of full vendor support on Unity 6.6.

Next step: compile and run the existing targeted audio and wheel/contact
regression suites with Unity `6000.6.0f1`, before removing the old editor.

## Additional bounded HDRP 17.6 shader compatibility

Main-task `Logs/unity660-compile6-20260906.log` completed C# compilation with
exit 0, but reported three `Undefined area shadow filter algorithm` errors:

- `AE/Leaves`: `Assets/Chernobyl/Other/CR_Leaves.shader`;
- `NatureManufacture/HDRP/Foliage/Foliage`:
  `Assets/NatureManufacture Assets/Foliage Shaders/NM_Foliage.shader`;
- `NatureManufacture/HDRP/Foliage/Cross`:
  `Assets/NatureManufacture Assets/Foliage Shaders/NM_Cross.shader`.

All fail in their Forward fragment variant. Their old template declares one
`SHADOW_LOW SHADOW_MEDIUM SHADOW_HIGH SHADOW_VERY_HIGH` multi-compile set. HDRP
17.6's `Runtime/Lighting/Shadow/HDShadowAlgorithms.hlsl` selects independent
punctual, directional and area algorithms. Its compatibility mapping for
`SHADOW_LOW` supplies only the first two; the area algorithm is undefined.

The exact three replacement directives are copied from the installed HDRP
17.6 `Runtime/Material/Lit/Lit.shader` Forward/TransparentBackface passes:

```hlsl
#pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
#pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
#pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH
```

Only that one pragma site in each source is changed (plus final newline).
Material properties/GUIDs, geometry, alpha testing, wind functions, textures,
non-fragment and DXR branches remain unchanged. This aligns quality variants to
the new pipeline; it does not hard-code lower-quality shadows to silence the
compiler. **PackageCache and Enviro vendor sources were not modified.** No
vendor package or external editor plugin was upgraded or installed. These are
generated legacy vendor shader templates; re-exporting them from their original
authoring tools may overwrite the local patch and requires revalidation.

### Runtime usage audit

Before patching, an ignored-inclusive GUID scan of all `.mat`, `.asset`,
`.prefab`, and `.unity` files found `AE/Leaves` only in eight source Chernobyl
materials. The two NatureManufacture shader GUIDs had no such references.
`Assets/Game` and `ProjectSettings` reference none of these three shader GUIDs;
Game scenes/prefabs/assets reference none of the eight source materials.
All 19 `VegetationRebuild/Materials` materials bind `HDRP/Lit` instead.
`MapVegetationMaterialBindings.Apply` explicitly substitutes the approved HDRP
bindings and rejects unmapped `AE/Leaves`. These errors therefore affected
import/vendor preview, not a demonstrated active-world foliage regression.
This static evidence does not replace the main-task graphics smoke.

### Reproducibility and validation

The three vendor shader files remain ignored. Project-owned reproducible delta:
`Tools/Compatibility/FoliageHdrp176.patch`, with before/after byte and normalized
SHA-256 in `Tools/Compatibility/FoliageHdrp176.hashes.json`.
Use the same already-applied reverse-check and matching-baseline forward-check
procedure as the NWH patch above, substituting `FoliageHdrp176.patch`.
Read-only `git apply --check` against the pre-import backup and
`git apply --reverse --check` against the patched project both passed.
GPU import/compilation and any further old-template incompatibilities must be
checked by the main upgrade task. No Unity process was started by this subtask;
the shadow fix is not yet claimed graphics-verified at this checkpoint.
