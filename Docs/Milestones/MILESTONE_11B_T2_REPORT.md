# Milestone 11B-T2 — Public transport and scripted route traffic

Date: 2026-08-06  
Status: Implemented; automated validation passed; manual comprehensive acceptance pending  
Transfer classifications: `WorldLayoutReference`, `BehavioralReference`, `ConfigurationTransferred`, `TemporaryDirectImport`, `Reimplemented`

## Scope inspected

- Locked donor scene hash: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- `BusRoute` with 1,084 ordered points, the Loppe/Kesseli/Rykipohja stops and the donor two-hour departure cycle.
- Two eight-point AI-boat waypoint loops.
- Two train endpoint routes, donor speed `30 m/s` and endpoint wait `250` simulation seconds.
- Donor presentation roots `BUS` (`65181`), `TRAIN` (`55611`), `AIboat1` (`57586`) and `AIboat2` (`48730`).
- Existing 11B-R3/T1 physical traffic, `IAudioBackend`, streaming and optional `traffic.state` boundaries.

## Implemented

- `TrafficRoadNetworkCatalog` now owns four stable transport definitions: one bus, one train and two boats.
- The complete traffic catalog contains 13 routes, 8,819 ordered donor points and 20 road-graph connections. Transport routes are deliberately not treated as road-lane joins.
- Bus departures are reconstructed from game time and repeat at 00/02/04/06/08/10/12/14/16/18/20/22. Route progress, stop dwell, active departure and last departure are persistent logical state.
- The bus uses a project-owned NWH physical wrapper near the player and logical route progression offscreen. A project-owned boarding/exit target parents the player to explicit seat and exit anchors without donor scripts.
- The train follows both locked directions, reverses through endpoint route selection, pauses at endpoints and retains a project-owned collider and persistent route state.
- Both boats retain independent stable identities and exact waypoint loops. Near the player they use Rigidbody force/turn/water-height control; outside residency they advance as persistent logical actors.
- Four sanitized `TemporaryDirectImport` wrapper prefabs were generated. The runtime does not search donor names or hierarchy and imports no donor controller, FSM, Rigidbody or old Unity assembly.
- `traffic.state` schema 1 gained an optional `transportActors` payload. Empty payloads from older saves initialize fresh transport state, so the document version does not change.
- The bus interaction script is a dedicated Unity component asset. The generated transport prefabs contain no null script references, and the regression suite checks this explicitly.

## Automated validation

- Unity batch build: `Logs/CodexTrafficT2Build3.log` — success, exit code 0.
- Traffic EditMode: `Logs/CodexTrafficT2EditMode2.xml` — 6/6 passed.
- Save integration EditMode: `Logs/CodexTrafficT2SaveIntegration3.xml` — 28/28 passed.
- Production NPC PlayMode: `Logs/CodexTrafficT2NpcPlayMode3.xml` — 10/10 passed.
- Generated transport YAML scan: no `m_Script: {fileID: 0}` under `Generated/TransportPrefabs`.

The production PlayMode log still reports pre-existing missing scripts while loading `World_Global_Legacy` and legacy cell scenes. The generated bus/train/boat prefabs are not the source of those warnings.

## Manual acceptance for the next comprehensive test

1. Observe the bus across at least one two-hour departure boundary; verify the route, three stops and stop dwell.
2. Board and leave the bus, including after a streaming unload/reload and save/load.
3. Observe a complete train endpoint traversal and wait; check track crossing/collision behavior in play.
4. Observe both boats near and beyond the residency boundary; check that neither duplicates or jumps after reload.
5. Perform a multi-hour/long-distance pass with Jani/Petteri, ambient traffic and all four transports active; watch for duplicates, stalls and large discontinuities.

## Known differences and risks

- Bus speed (`12.5 m/s`) and boat speed/force tuning are provisional project values; exact donor timing still needs an executed comparison.
- Bus seat/exit anchors are bounded project-authored anchors. Exact door motion, ticket payment/request-button flow and passenger population are not yet implemented.
- Train whistle/crossing warning, player damage/death consequences and final collision-safety behavior remain open.
- Transport audio currently uses the temporary spatial Petteri fallback through `IAudioBackend`; dedicated bus/train/boat Wwise events and media are absent.
- Final surface classification, long-route profiling and manual duplicate/recovery acceptance remain open. Therefore `P1.TRAFFIC.003–.010` stay `PartiallyImplemented`, not `Verified`.

## Compatibility impact

Milestones 00–08A remain compatible. Existing stable IDs, scene ownership, UI, weather, vehicle APIs and save document version are unchanged. The only save change is an additive optional field in the already optional `traffic.state` domain; no migration is required for old saves.

## Next milestone

`12A-E1 — money, prices and transaction foundation`.
