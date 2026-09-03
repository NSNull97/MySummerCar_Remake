# Local lighting implementation

Status: **Partially Implemented**. The production runtime, audit tooling, 49
accepted streamed fixtures, electrical/save integration, quality budgets and
VLB boundary are implemented. The 294 audit rows in
`LightingManualReview.md`, a successful uninterrupted production PlayMode run,
manual visual acceptance and matched GPU profiling remain open, so this work is
not classified `Verified`.

## Runtime ownership

`ProductionLightingInstaller` composes one `LightingRuntimeManager`, one
event-driven `ElectricalGridService`, the accepted world-light adapter, Enviro
read bridge, service/NPC availability bridge, vehicle electrical adapter,
Wwise-ready audio sink and validation runner. `GameLightFixture` has no
`Update`; fixtures register through lifecycle events and streamed-scene load
events. The manager owns state transitions, distance hysteresis, zone priority,
shadow budgets and VLB budgets.

The final state is:

```text
IsActuallyOn = PowerSourceAvailable
            && CircuitEnabled
            && SwitchState
            && RuntimePolicyAllows
            && FixtureIsAvailable
```

Grid restore is transactional: an unknown/duplicate persistent ID rejects the
whole DTO before any source, circuit or switch is mutated.
`lighting.electrical-grid` schema 1 is an optional native-save participant so
existing document version 15 remains compatible while new saves capture the
domain. Runtime-derived flashlight and story-traffic presentation nodes are not
independent save authority and are excluded from new payloads. Existing schema-1
payloads are normalized during preflight by removing only those explicitly
recognized runtime-derived IDs; flashlight state is restored by `items.instances`
and traffic lighting is recomputed from the active presentation/environment.

The current grid is still a binary lighting-power boundary, not the complete
donor home-electricity simulation. It does not yet meter watts/kWh, trip and
replace the seven physical fuses, issue the 0.78 MK/kWh donor-evidenced bill or
cut/restore utility service after payment. Lighting fixtures are correctly
bound to source/circuit/switch state, but power consumption and billing must be
implemented once for every home appliance rather than faked inside the visual
lighting adapter.

## Profiles

The catalog contains 19 project-owned profiles:

- DomesticIncandescent, EnclosedCeiling, Fluorescent;
- TeimoShop, RefrigeratedDisplay, TeimoPub, FleetariWorkshop;
- StreetLamp, ExteriorBuilding;
- VehicleLowBeam, VehicleHighBeam, VehicleTail, VehicleBrake,
  VehicleIndicator, VehicleReverse, VehicleLicensePlate, VehicleDashboard,
  VehicleInterior;
- PlayerFlashlight.

They use physical HDRP units, 2900-6000 K room sources (plus the deliberately
cool 15000 K refrigerator fill), authored ranges/source sizes, emissive on/off through a reusable
`MaterialPropertyBlock`, smooth switching and per-tier shadow/beam distances.
Cookies and IES slots exist but remain unassigned where no authored source is
available; no fake IES asset was fabricated.

## World bindings, circuits and zones

The accepted 09C source catalog remains authoritative. The deterministic
generator currently transfers 46 donor-evidenced sources through stable
`world.light.*` IDs and adds three project-owned temporary refrigerator
rectangles, bringing the active Phase 1 catalog to 49 fixtures across 11
streamed cells.

Bound circuits are:

- `grid.street.public` (14);
- `grid.building.fluorescent` (7), `grid.building.general` (3) and
  `grid.exterior.buildings` (1);
- `grid.teimo.shop` (5), `grid.teimo.refrigeration` (3), `grid.teimo.pub` (1),
  `grid.fleetari.workshop` (2);
- home: living room (1), garage (3), bathroom (2), kitchen (1), hallway (3),
  toilet (1), boy bedroom (1), parents bedroom (1);

Zones are `zone.exterior`, `zone.interior.other`, the home room zones,
`zone.teimo.shop`, `zone.teimo.pub` and `zone.fleetari.workshop`. The Editor builder creates
project-owned trigger bounds and adjacency from accepted fixture coordinates.
Eight physical home switch targets are anchored to extracted donor switch
button transforms. The entrance and hallway rockers share one project-owned
switch ID and control the entrance, corridor and living-hall fixtures together;
the WC has its own circuit and rocker.

Teimo/Fleetari ceiling-light policy reads `ServiceRuntime.IsLocationOpen`, which
already combines schedule and the NPC staffing source. A four-second shutdown
grace prevents abrupt loss when the owner leaves. Teimo's three refrigerator
lights instead use `AlwaysWhenPowered` on the dedicated
`grid.teimo.refrigeration` circuit: leaving or closing the shop does not turn
them off, while loss of the grid source or refrigerator circuit still does.
The validation override is explicit and never used by ordinary gameplay.

Five shared-building ceiling fixtures belong to Teimo's shop and one to the
pub. Both groups use white 6000 K, 32500 lm area lights and follow Teimo's
actual service location independently. Three 15000 K, 15000 lm refrigerator
rectangles use the exact transforms and individual rectangle sizes accepted in
the current Unity scene. They deliberately create no generated emissive lens.

Point fixtures do not receive generated sphere meshes. Their emission is
applied only to the resolved donor renderer, preserving the original shade
silhouette, texture and transparency; translucent temporary shades are removed
from shadow casting so they cannot turn an omnidirectional bulb into a hard
floor spotlight. Only rectangle/tube sources use a thin project-owned emissive
lens.

## Shadows, distance and volumetrics

The home-garage lighting zone is not inferred from the three lamp positions.
It is aligned to the audited indoor volume and extended by a narrow doorway
transition to the reviewed garage threshold. This keeps daylight exposure and
local-light relevance active while the camera crosses the opening without
classifying the yard or side walls as interior. Because the temporary garage
has no baked bounce, each 1.8 klm donor reference is presented as a bounded
16.2 klm fluorescent area source; the existing garage switch, circuit, fuse,
metering and save authority are unchanged.

The rejected garage-only `7.75 EV` override is not used: rendered comparison
showed that it brightened the room even when the circuit was off. Garage and
exterior now share the authoritative daily exposure curve. Dusk adaptation
starts at 17:00 and reaches the night floor at 22:30, while ambient darkness
provides a restrained 0.35 EV readability lift instead of darkening an already
dark frame. Thus an off garage follows the environment and an on garage is lit
by its actual fixtures.

Exterior photometry is bounded at 9000 lm for public street lamps, 3500 lm for
building exterior lamps and 4000 lm for the white home-garage exterior lamp.
Dusk-to-dawn loads engage at the calibrated approximate solar elevation of 12
degrees so they are useful during visible dusk rather than switching on only
after the sky is already black.

The manager sorts active fixtures by logical state, distance, current/adjacent
zone and stable ID without per-frame allocation. Light distance has a 5 m
hysteresis band and uses the fixture transition time, avoiding hard popping.
Every-frame shadows are reserved for the nearest relevant dynamic set;
remaining selected shadows use OnDemand/Cached updates. This preserves static
wall/ceiling occlusion outside the four-light High realtime budget instead of
changing those fixtures to `LightShadows.None`. Contact shadows remain limited
to the realtime subset. The Low/Medium/High/
Ultra budgets are respectively:

| Tier | Shadowed | Every Frame | HD beams | SD beams |
|---|---:|---:|---:|---:|
| Low | 4 | 1 | 0 | 6 |
| Medium | 8 | 2 | 1 | 12 |
| High | 12 | 4 | 3 | 18 |
| Ultra | 18 | 6 | 5 | 28 |

VLB 2.2.3 is behind `VolumetricBeamAdapter`; the runtime assembly does not
compile against vendor `Assembly-CSharp`. HD is budgeted for selected hero
beams, SD is the fallback, and off means the component is disabled. Fog/rain/
snow multiply beam intensity through project weather outputs. Reflection work
is cached and skipped when the applied state is unchanged.

Directional sun/moon lights and environment-owner lights are explicitly outside
the local-fixture audit and auto-placement path. A cleanup guard removes only
builder-owned accidental auto fixtures and restores their directional
photometry; Enviro and `NativeHdrpWeatherBridge` retain accepted ownership.

## Adding content

1. Create/select a `LightFixtureProfile` in the catalog.
2. Add a project-owned `Light` at visually verified source geometry; do not
   guess from a parent name alone.
3. Add `GameLightFixture`, a stable fixture ID, power source, circuit, zone and
   optional switch/business/vehicle channel.
4. Bind emissive renderers; the fixture owns their on/off property block.
5. For a room, add a `LightingZone` with verified bounds and adjacency.
6. For a vehicle, add real low/high/tail/brake/indicator/reverse/plate/dash/
   interior fixtures and one `VehicleLightingElectricalAdapter` at the vehicle
   root. It reads public NWH light-state bits and the real battery voltage.
7. Run `Tools > Lighting > Lighting Map Builder`, then the validation runner.

If a light does not work, check fixture validation, stable-ID duplication,
source/circuit/switch state, policy, availability, distance tier, current zone,
profile range/exposure and the validation snapshot in that order.
