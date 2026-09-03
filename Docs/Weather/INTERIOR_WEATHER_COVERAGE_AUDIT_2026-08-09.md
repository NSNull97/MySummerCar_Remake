# Interior weather coverage audit — 2026-08-09

Status: automated configuration coverage complete; in-game visual/audio route pending.

## Scope and evidence

The audit uses the locked donor placement table
`normalized/world/milestone-04a1/WorldObjectPlacements.csv` with SHA-256
`B894B8A6416FC1B74EABB1821B71E407CBEDBD3AC9AFABEEE262C7891004758A`.
The authoritative selection is every active record whose original name is
`NoRain`. This yields 24 records. No interior volume was guessed from an object
name or a broad renderer bound.

The machine halls at the farm and strawberry field are present through the
existing `PHASE1_JOB_LOCATIONS_TEMPORARY_DIRECT_IMPORT` overlays in their normal
streaming cell scenes, so their donor `NoRain` footprints remain valid runtime
configuration evidence.

Machine-readable evidence:
`Assets/Game/Weather/Enviro3Integration/Editor/Evidence/DonorNoRainCoverageAudit.json`.

## Result

All 24 donor records are classified:

- 23 static or Phase 1 overlay records have runtime coverage;
- 20 definitions are in the streamed weather-zone catalog;
- three home-house records form the existing measured compound home zone;
- one record belongs to the moving bus cabin and is intentionally not a static
  world zone. It remains assigned to the `P1.TRAFFIC.003` vehicle-cabin
  presenter boundary.

The closed-interior profile is used for the cabin, island cottage, jail,
rowhouse apartment, Fleetari, Teimo shop/pub, inspection hall/office and home.
It suppresses precipitation and attenuates weather audio and indirect exterior
lighting.

The open-shelter profile is used for the cabin shed, dance pavilion, abandoned
house, factory/farm/strawberry/home-yard machine halls and both bridges. It
retains exterior audio, fog, wind, thunder and lighting while removing 90% of
local rain. This prevents the earlier false indoor sound transition below a
roof or awning.

Migration v7 also replaced old approximate horizontal bounds for the island
cottage, rowhouse, jail, abandoned house and factory hall with the corresponding
donor `NoRain` footprint. Vertical spans remain the explicit playable
camera/listener bands used by the project integration rather than the donor's
thin legacy helper volume.

## Newly covered locations

| Location | Streaming cell | Profile |
|---|---|---|
| Farm machine hall | `cell_-2_0` | Shelter |
| Strawberry-field machine hall | `cell_-3_-4` | Shelter |
| Home-yard machine hall | `cell_0_-3` | Shelter |
| Dirt-road bridge | `cell_3_-1` | Shelter |
| Highway bridge | `cell_3_0` | Shelter |

## Manual acceptance still required

Run a heavy-rain route through every closed interior and open shelter. At each
boundary verify precipitation, splash spawning, weather audio, fog, indirect
sunset light and camera-angle brightness. Doors and windows are still static
zone boundaries unless a location already has an explicit portal binding; a
full dynamic portal pass is outside this correction.
