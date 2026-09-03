# Phase 1 world-door binding — 2026-08-09

## Scope and ownership

This pass replaces the former pass-through limitation for the confirmed
hand-operated world doors without importing donor gameplay code. The temporary
baseline supplies removable leaf meshes and frozen transforms; project code
owns input priority, interaction state, motion, collision and streaming reload
state.

Evidence is the locked `GAME.unity`, the donor `door_white`, `store_door` and
left/right `garage_door` animation clips, `M04A1_WorldEntities.csv`, and the 79-row
`ExcludedDoorRequiresBinding` set in
`LEGACY_SOLID_COLLIDER_DISPOSITIONS.csv`. The runtime catalog contains 32 door
states backed by 63 project-known stable transfer IDs across seven streamed
cells:

- player home and garage: 11;
- cabin and shed: 3;
- island cottage: 3;
- dance pavilion: 2;
- abandoned house: 2;
- terrace/town doors: 4;
- Teimo store/pub, uncle, Fleetari, two inspection doors and the water
  facility door: 7.

The collider-only `YARD/Building/BEDROOM2/house_door1` row has no donor
animation owner or handle capability and is not treated as an openable door.
The inspection office and water-facility doors do have donor animation
evidence and are included.

Static facade panels, stove/refrigerator doors, jail presentation, vehicle
doors, and open/closed visibility pairs for service garage doors are not part
of this generic hinged-door pass. Teimo's arrival service door stable ID
`41889effb8941e0ef1aa015c802f648d` remains owned by the existing NPC schedule
and weather-portal presenter, preventing competing transform authorities.

## Player contract

- LMB on an ordinary bounded handle zone toggles open/close.
- Tool activation (`F`) does not operate doors; door use is reserved for the
  primary handle interaction.
- Home garage leaves use directional hold control: LMB held opens, RMB held
  closes. Releasing either button leaves a short decelerating coast instead of
  freezing the leaf instantly.
- A handle interaction has priority over dropping a carried object, so no
  modifier key is required while carrying.
- The garage handle also has priority over RMB throw/use while closing.
- Existing mount handoff keeps higher priority than a door interaction.
- Clicking outside a valid context zone retains ordinary pickup/drop behavior.
- Ordinary travel is 85 degrees at 200 degrees/second (about 0.425 seconds), a
  slight slowdown from the previous 220 degrees/second while remaining faster
  than the one-to-1.5-second donor clips.
- Garage travel is 140 degrees with a 145 degrees/second maximum, 1100
  degrees/second-squared held acceleration and 1300 degrees/second-squared
  release deceleration. A full uninterrupted opening takes about 1.03 seconds;
  garage leaves remain slower and heavier than ordinary doors.
- Opening direction is authored per leaf and never derived from the player's
  side. Donor clips establish `+85°` for ordinary/store doors, `+140°` for
  left garage-style leaves and `-140°` for right garage-style leaves.
- The solid leaf collider follows the moving leaf. The handle uses a nonblocking
  0.24-metre trigger zone (previously a 0.14-metre solid sphere), making it
  easier to acquire without creating an invisible physical obstacle. The
  interaction ray accepts only triggers with an explicit `InteractionTargetHost`
  and ignores unrelated gameplay volumes.

## Compatibility and current validation

The implementation extends the existing `IContextInteractionTarget`,
`InteractionTargetHost`, stable-ID metadata and additive streaming service. It
does not rename accepted APIs, modify generated baseline scenes, or depend on
donor object names at runtime. State survives cell unload/reload in the current
session. Full save-document persistence, time/lock authorization, door audio,
weather portals for the remaining doors and manual in-world route acceptance
remain pending; therefore the Phase 1 parity rows remain `PartiallyImplemented`.

Executed evidence for this pass:

- scoped compilation of Interaction and Player runtime completed with zero
  errors; standalone installer and focused test-source compile checks also
  completed with zero errors;
- all 63 catalog stable IDs were found in the generated streaming scenes and
  in `M04A1_WorldEntities.csv`;
- source/catalog validation found 32 definitions and zero missing stable IDs;
- focused tests cover faster-than-donor travel, fixed per-leaf direction,
  second-click closing, moving solid collision, garage LMB/RMB hold and release
  inertia, `F` exclusion, registered-trigger filtering, and handle use while
  carrying. The focused Unity EditMode suite passed 6/6 on 12 August 2026.
