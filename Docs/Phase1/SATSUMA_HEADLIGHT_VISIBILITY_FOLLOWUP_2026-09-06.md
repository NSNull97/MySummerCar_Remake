# Satsuma — front light and switch follow-up

Status: scoped implementation, final graphics and combined broad regression
passed; manual night-road acceptance pending. User save remains read-only. This does not change assembly,
electrical ownership, save schema or the accepted UI layout.

## Actual source state and cabin evidence

The selected native `slot-01/current.save.json` contains both installed headlamp
owners and two purchased bulbs. All four headlamp mounting bolts have stage 0.
The existing, donor-evidenced `Bolted` requirement consequently prevents both
beams. It is not a missing electrical bypass: tightening the four actual bolts
with the registered **7 mm wrench** on a disposable restored copy makes both
existing HDRP spotlights active. The user's source document is never serviced.

The existing physical switch is visible at the far left of the instrument panel,
beside the driver's door, with a headlamp symbol. From the canonical cabin ray
origin it lies at car-local approximately `(-.539975, .312400, .550768)`.
The reused source mesh is `5af90b41049184c4e8382b7cf0ce4f1d`. No second switch,
new hierarchy-name lookup or substituted dashboard was added. The established
mouse interaction already cycles Off → Parking → Headlights → Off.
The first position is **not** dipped beams. The current stock wrapper has no
optional front marker parts; rear tails are the available parking output.

The existing interaction prompt now explicitly shows current and next mode:
`Выключено · ЛКМ — габариты`, `Габариты · ЛКМ — фары`,
`Фары · ЛКМ — выключить`. This binds existing state to the existing HUD field,
without changing 08A presentation or adding an alternative input path.

## Confirmed missing presentation and repair

Rendered isolated night captures proved that the old light beams illuminated
the road after the original electrical/assembly requirements were met, while
the actual headlamp glass still remained black. The baseline lens material's
HDRP `_EmissiveColor` is zero and no output presenter controlled it.

Frozen `GAME.unity` evidence (same locked source revision as the engine pass):

| Side | Part transform | Lens transform / object | Filter / renderer |
| --- | --- | --- | --- |
| Left | 45890 | 46559 / 10505 | 84283 / 75171 |
| Right | 63443 | 65648 / 29601 | 88289 / 80274 |

Both explicit lens leaves use source mesh
`85ee7b66475902b4187d62d12fad42cd` and material
`4b8e6df3a198e5c408de5ebdff1e0147`. These are Editor provenance selectors only.
Runtime binds each renderer through its existing stable lamp binding and owner.
No donor scripts, controllers or event logic were imported.

The existing `SatsumaDashboardLampBinding` gains optional lens renderer/emission
fields. Its already-computed light enable also controls glass emission. A cached
property block preserves other renderer overrides; the original material/texture
and mesh are not replaced. Emission follows the existing warm lamp tint with a
40 HDRP presentation calibration, explicitly **not** a transferred donor
photometric value. Base-color modulation keeps the existing lens texture.
No extra source lights, bloom override, volumetric cone or emissive authority
independent of wiring/bulb health was added. Disable and reconfiguration clear
the lens output as well as the beam.

`RefreshDashboardLightingBatch` updates only the existing canonical prefab,
creates an ignored pre-edit backup and accepts only the old missing optional
lens binding or the exact reviewed binding. Other lamp/wire/bulb/owner changes
still fail preflight. The actual generated diff is **16 added YAML lines**
(eight lamp bindings, two optional fields each); no other prefab data changed.
The full deterministic authoring path uses the same extension.

The first 1500-level lens trial passed visibility assertions but visual inspection
rejected its excessive bloom under the existing post-processing. Calibration was
reduced to 40 without changing global bloom/exposure. A new upper image-change
bound rejects emission covering most of the test frame. The numerical test alone
was not accepted as visual quality approval.

## Executed checks and test isolation

- `Logs/headlight-lens-author-20260906.log`: scoped refresh exited 0,
  `changed=1`, `fullRebuild=false`. Pre-edit copy:
  `Logs/satsuma-lighting-before-20260906-161308-1441150.prefab`.
- `Logs/native-headlights-isolated-20260906.xml`: **1/1 passed**, no skips,
  2.539 s, D3D11/HDRP, **before** the new glass-emission binding. Six captures
  showed the real visible switch, native unfastened gate, actual wrench
  servicing on the copy, parking versus beams and driver's forward view.
  Beam-on produced 137,629 brighter pixels than off on a test-only matte plane.
- Initial graphics fixture attempts used unsuitable scene creation APIs; the
  final fixture owns temporary roots in the runner's current Editor scene and
  removes only those roots. It does not close, save or replace the user's scene.
- Two interim night comparisons were invalidated by the runner's existing
  100,000-lux default Directional Light. The final isolated check temporarily
  mutes non-fixture lights, restores them in `finally`, disables sky indirect
  contribution through a **test-only** volume, and records effective exposure,
  sky and indirect-light values. No production sky/weather setting was changed.
- Captures remain local in `Logs/native-headlight-graphics-20260906/`.
  The post-repair run additionally asserts visible pixels at both lens centers,
  not only `Light.enabled`. Its result is recorded after execution below.

Known limits: the capture isolates lighting on a matte plane, not a complete
night road drive under Enviro. Lens calibration and source-derived 3000-candela,
50-metre, 10-degree-down beam presentation require manual visual acceptance.
The actual donor speed/crash break branch and optional marker assemblies are
not newly claimed complete. Saves and source assets remain unchanged.

## Post-repair verification

`Logs/native-headlights-lens-calibrated-final-20260906.xml`: **13/13 EditMode
passed**, no failures/skips, 4.216 s, exit 0. This includes all 12 dashboard
tests and the actual read-only native graphics test. Six final captures were
checked; both lit lenses are visible with restrained local glow, road beams
remain distinct, and Off/Parking do not fabricate headlamp output. Added
regressions confirm emission follows side-specific bulb health, broken bulbs
and removal of the negative battery connection. The earlier 1500-level run
was 13/13 numerically green but was visually rejected as documented above.

The final combined EditMode run (`live-engine-complete-edit-20260906.xml`)
passed **1359 cases**, zero failures and one unrelated historical skip,
including this native graphics test and all dashboard cases. Final broad
PlayMode passed **127**, zero failures/three explicit unrelated or audio-device
skips, including all seven real cabin-input checks. Final full Bootstrap passed
**3/3**. The source native SHA256 remains unchanged after those final reloads.

Final fresh `RefreshDashboardLightingBatch`:
`Logs/live-engine-headlight-idempotent-final-20260906.log`, exit 0,
**changed=0 / fullRebuild=false**. The canonical prefab and metadata are
byte-identical across this repeat; no manual refresh is required in this
already-authored local checkout.
