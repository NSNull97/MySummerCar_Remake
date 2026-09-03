# Phase 1 lighting and reflection baseline

Status: `Leak calibration implemented / Manual visual re-acceptance pending`.

## Scope

This bounded pass adds project-owned presentation to the sanitized donor world
without modifying donor geometry, importing donor lights, or changing Enviro 3
ownership of the sun, moon, sky, fog and weather.

`Phase1WorldLightingProbeBuilder` reads only project metadata attached to the
ignored generated legacy cell scenes. It recognizes reviewed fixture
categories, transfers their world positions into a project-owned catalog and
binds that catalog to Bootstrap.

Generated baseline:

- 49 active local-light definitions: 46 donor-evidenced sources and three
  project-owned Teimo refrigerator fill lights;
- 10 box-projected local reflection probes;
- streetlights enabled only between configured sunset and sunrise;
- indoor/exterior fixture lights owned by their streamed cell;
- one scripted realtime probe refresh per frame to avoid a single refresh
  spike;
- explicit `HDAdditionalLightData` and `HDAdditionalReflectionData` components,
  with HDRP on-demand probe refresh;
- no donor hierarchy lookup in runtime code.

Catalog:

`Assets/Game/World/Content/Lighting/Phase1WorldLightingProbeCatalog.asset`

Generator:

`MSC/World/Build Phase 1 Lighting And Probes`

## Current tuning

The first HDRP pass was manually reported as too weak and too warm. The
generated profile was therefore recalibrated. All 14 streetlights now share the
same measured HDRP profile:

- spot light at the actual lamp head rather than the donor object's centre;
- 400 lux measured at 15.6 m, 6,570 K, zero indirect multiplier;
- 0.025 m HDRP source radius and explicit volumetric contribution at full
  dimmer;
- 107-degree inner and 138-degree outer cone;
- 35 m range, soft dynamic shadows at 512 px;
- the two Teimo fixtures preserve the manually verified positions, rotations
  while using the same shared streetlight range.

Other reviewed fixture profiles remain:

- office: 3,200 intensity, 14 m range;
- domestic: 2,600 intensity, 12 m range;
- exterior: 3,500 intensity, 16 m range;
- hall: 4,000 intensity, 20 m range.

On 2026-08-01, playtest evidence rejected the earlier Inspector capture as a
runtime profile: the 13 home fixtures used 11,529–40,000 lm, garage cones up to
179 degrees and one outdoor lamp with a 423.0976 m range. Those values lit
through several rooms and projected garage light far outside the building.
Fixture transforms remain unchanged, but the home property is now bounded to
650–1,800 lm and 3–8 m ranges. The three garage fixtures use 1,800 lm / 4.2 m /
78–112 degree cones; the outdoor fixture uses 1,100 lm / 8 m / 65–95 degrees
and is `NightOnly`. Indirect multipliers are limited to 0–0.35.

The later production adapter retains those reviewed garage source floors but
promotes the three fixtures to one-sided 4,200 K rectangle lights, applies a
bounded additional 1.35x garage compensation and uses the 7.5 m fluorescent
profile range. This supersedes the older cone description above without
changing the donor fixture transforms.

Local shadow bias is now explicit (`0.02` bias, `0.15` normal bias,
`0.05` near plane; HDRP slope bias `0.25`). Bootstrap's Enviro-owned sun keeps
the same ownership and direction but uses reduced thin-shell-safe bias
(`0.025` / `0.2` / `0.1`; HDRP normal `0.2`, slope `0.35`). This targets tree
shadows and sunlight crossing the paired 0.08 m roof shells without adding a
second sun or changing donor geometry.

Selected generated lights cast soft shadows. Static room occlusion uses one
HDRP `OnDemand` request; the nearest centrally budgeted subset switches to
`EveryFrame` and contact shadows for moving characters/items. High therefore
keeps up to 12 occluding local lights but updates only the nearest 4 every
frame. Lights outside the complete shadow budget allocate no cached record.
Selection updates at 5 Hz without managed allocations. This preserves wall
containment without updating every house and street shadow map every frame.

General local reflection probes use a restrained `0.18` multiplier and broad
HDRP blend distances. The Teimo/store probe is deliberately smaller and uses
`0.08`, so it does not project the bright shop interior onto the road and
outside terrain. This targets the previous rectangular reflection transition
without disguising it as a streetlight problem.

The former single `YARD` probe is replaced by bounded living, utility and
garage probes. Their capture points sit at playable eye height inside the
corresponding enclosure, their influence blends are capped at 0.75 m, and the
influence box is also the box-projection proxy. Realtime probe cameras exclude
atmospheric scattering and volumetric fog from their cubemaps: outdoor fog is
still rendered by the gameplay camera, but it can no longer be baked into an
interior reflection and appear intermittently after weather/time refreshes.

The home fixture classifier now includes the hallway `lamp_hallway` and the
player-bedroom `lamp_paper`. Bootstrap starts the player beside the bed facing
the bedroom door.

These are Phase 1 presentation values, not donor-photometric claims. Individual
switches, emissive fixture materials, baked production lighting and final probe
volumes remain later work.

## Manual validation

Open `Assets/Game/Bootstrap/Bootstrap.unity`, start a new game and verify:

1. At night, streetlights visibly illuminate the road and switch off after
   sunrise.
2. Home, shop/pub and other catalogued fixtures are clearly visible without
   washing out the whole room.
3. Check all 13 home-property spot cones against their fixture meshes. With
   garage lights on, walk around the exterior walls and verify there is no
   room-sized pool outside the building.
4. Stream away from home and back; lights and probes unload/recreate with their
   owning cells and do not duplicate.
5. Check shiny donor-baseline surfaces around the home and Teimo area for a
   locally plausible reflection response. Temporary donor material defects are
   not a blocker for this Phase 1 baseline.
6. Scrub/advance time normally and confirm sun shadows move continuously rather
   than in visible quarter-second steps.
7. In the kitchen at midday, verify the roof/walls block tree shadows and direct
   sun except through actual windows and open doorways.

## Known limits

- The catalog covers reviewed fixture classes found in streamed legacy cell
  scenes; it is not a claim that every donor switch and bulb behaviour is
  already reconstructed.
- Streetlight shadows are limited by streamed-cell ownership and the shared
  dynamic-shadow budget; their performance cost must still be checked during
  vehicle traversal at night.
- The cached/dynamic split depends on static world renderers being authored as
  static shadow casters and movable items remaining dynamic. Incorrect static
  flags on an item can therefore put it into the wrong half of the shadow map.
- Probes are realtime and refreshed through a bounded queue. Their placement is
  coarse Phase 1 coverage and will be replaced or rebaked during Phase 2.
- Weather wetness/puddles, emissive material polish and production lightmaps
  remain separate work.
