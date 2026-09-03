# Milestone 10B-R1 — ServiceOrRelationship NPCs

Status: **In progress / automated core, presentation and bounded voice
validation passed / Fleetari placement user-accepted / Teimo store-pub endpoint
correction user-accepted / Teimo bicycle route, pedalling and shop-arrival flow
conditionally user-accepted /
bounded greeting-babble voice, bicycle collision response and dependent
service-job dialogue pending**.

10B-R1 is the first bounded package of the full 75-row NPC roster. It owns only
`P1.NPC.001`, `.002`, `.007` and `.008`; it does not redefine 10B as a four-NPC
milestone. `PHASE1_EXECUTION_PLAN.md` now accounts exhaustively for all 75 rows
across R1–R5, including 73 required and two evidence/exclusion rows.

## Implemented in this pass

- retained all accepted 10A stable IDs, public overloads, Bootstrap, streaming,
  08A UI and `npc.state` behavior;
- added Fleetari, Farmer and Berryman definitions with exact project anchors,
  donor-evidenced schedules and project-owned stable instance IDs;
- promoted Teimo from framework-only to the R1 row while preserving his accepted
  instance/presentation IDs;
- added a six-waypoint Farmer route with distance-weighted ping-pong traversal;
  the mailbox return follows the same terrain anchors at the same path speed
  instead of closing directly to the first point;
- ground-conformed only `Walking` presentation poses to the nearest loaded
  `WorldSurface`/`WorldSolid` collider through a non-allocating probe; authored
  anchors, simulation progress and save state remain unchanged;
- connected a validated dialogue catalog to an explicit interaction target,
  locked UI status feedback, stable `IAudioBackend` event IDs and job/service
  event-hook boundary;
- persisted dialogue cooldowns in `npc.state` schema 2 and save document v9;
  migration v8→v9 preserves all compatible accepted snapshots and adds missing
  R1 instances without resetting progress;
- upgraded the character presentation manifest/importer through schema 5 revision
  7: seven wrappers, four body meshes, ten distinct static accessory/vehicle
  meshes, seven clips, 19 donor textures and 20 generated HDRP/Lit materials
  with exact donor slot
  order;
- calibrated Teimo, Fleetari, Alpo and Berryman root presentation, restored
  Teimo/Berryman/Latanen/Farmer headwear and Teimo/Fleetari/Farmer glasses, compensated
  Fleetari's visual root against the exact serialized chair-parent transform,
  and retained Berryman's donor seated left-hand loop instead of replacing it
  with a standing pose;
- corrected Teimo's reused stationary-service anchor from the serialized pub
  endpoint to the exact shop endpoint. The source `teimo_move_store` and
  `teimo_move_bar` root curves prove a `6.5 m` separation; the pub anchor now
  retains the exact serialized endpoint instead of a guessed `0.794 m` offset;
- corrected overnight day-mask ownership so a Saturday shift continues into
  Sunday 02:00 without incorrectly enabling a Sunday-night Monday tail;
- transferred Teimo's two donor bicycle splines into a project-owned schema-1
  route manifest: 37 exact waypoints per direction, `4 m/s` source traversal,
  Mon-Sat `08:00` ride to the shop and Tue-Sun `02:00` ride home. Schedule
  blocks switch the same stable Teimo instance to a replaceable bicycle wrapper,
  continue off-screen and retain route progress through save/streaming reload;
- reconstructed the private Phase 1 bicycle wrapper from the selected body,
  hat, clear glasses, frame, pedals, two tires and two rims. The donor motor-parts
  atlas is hash locked and routed through one generated HDRP/Lit material; the
  reviewed greeting wave is available for `Talking`, while route motion remains
  project-owned and does not depend on a donor controller;
- transferred the donor bicycle presentation constants from read-only FSM
  `106628`: both tire/rim assemblies rotate at `170 degrees per travelled metre`
  and the pedal crank at `1/3.5` of that rate. The importer now removes the exact
  `0.184355 m` tire clearance computed from the locked hierarchy and mesh radius;
- bound the exact Teimo hip/knee/ankle chains to the rotating crank with a
  project-owned two-bone presentation solve; the donor set contains no dedicated
  pedalling clip, so no fabricated donor-animation claim is made;
- replaced waypoint-to-waypoint snap turns with the isolated donor Catmull-Rom
  calculation, arc-length travel and analytic tangents. The same 37 source
  points and `4 m/s` timing remain authoritative evidence, while project-owned
  route/schedule state remains save authority;
- added the exact 26.1-second `teimo_move_in` root path after the ride: Teimo
  switches to walking, goes around the shop, opens/closes the service door and
  reaches the counter. The six-part parked bicycle is hidden outside the shop
  state and shown after arrival through stable world IDs only;
- reimplemented the one-shot bicycle greeting behind explicit player and time
  dependencies. A clear line within `5 m` triggers the reviewed wave at most
  once per game day, with eligibility retained through streaming and save via
  existing `npc.state` cooldown data. The donor evidence used `10 m`; `5 m` is
  the user-directed manual-comparison calibration;
- replaced the four fabricated R1 placeholder lines with a hash-locked private
  voice manifest: seven Teimo shop greetings, four Teimo pub lines, Fleetari
  and Farmer greetings, and four Berryman babble lines. Exact donor FSM
  subtitles, MasterAudio group/variation identities, AudioClip GUIDs and PCM
  resource hashes are retained as provenance; runtime sees only 17 stable
  project event IDs and ordinary generated WAV imports;
- added the `ActiveScheduleBlock` dialogue condition so Teimo's shop and pub
  pools cannot cross, plus deterministic per-session rotation so every selected
  variation is reachable without donor random/FSM authority;
- extended the Unity fallback with explicit supplemental libraries. Preferred
  Wwise mappings still win; an unmapped Phase 1 NPC event falls through to the
  removable private Unity library through `IAudioBackend`;
- added a narrow `TemporaryDirectImport` world supplement for the previously
  excluded StrawberryField (including both tent renderers) and five septic-job
  house/waste-well roots, plus the missing static Farm buildings, yard and
  well, without donor NPC/FSM/job logic or the dynamic combine hierarchy;
- gave every wrapper independent geometry, materials/textures and animation
  production replacement keys for Phase 2.

## Validation executed so far

- Unity 6000.3.11f1 compilation and 10B-R1 catalog/presentation builder: PASS;
- schema 5 revision-8 rebuild report: PASS with 7 wrappers, 20 materials, 19
  textures and 15 accessory/vehicle renderer instances; donor
  scripts/FSM/controllers/shaders excluded;
- focused `MSC.NPC.Tests.EditMode` plus
  `MSC.Save.Integration.Tests.EditMode`: PASS `32/32` after correcting the test
  harness for multi-clip wrappers;
- tests cover textured material slots, bone motion, deterministic distance-
  weighted loop/ping-pong routes, dialogue conditions/cooldowns/hooks, streaming
  reconciliation and v8→v9 save migration;
- generated-character real-frame PlayMode smoke: PASS `1/1`; all six local
  wrappers advance a renderer-linked bone over rendered frames;
- post-calibration NPC EditMode: PASS `9/9`; NPC animation PlayMode: PASS `1/1`;
- post-glasses/chair compensation NPC EditMode: PASS `9/9`; focused NPC
  PlayMode: PASS `1/1`;
- post store/pub correction NPC EditMode: PASS `11/11`; focused NPC PlayMode:
  PASS `1/1`;
- supplemental job-location selection EditMode: PASS `1/1`; streaming,
  unload/reload and replacement-key PlayMode at the restored Farm: PASS `1/1`;
- job-location generator: PASS with 252 renderers, 80 effective static
  colliders and seven existing streaming cells; two donor `MeshCollider`
  records with no mesh are explicitly audited and omitted;
- complete `MSC.Save*` EditMode regression: PASS `53/53`;
- post-voice production-environment/Bootstrap integration regression: PASS
  `12/12`;
- CSV parse and scoped `git diff --check`: PASS at the current checkpoint.
- generated Unity C# projects: `MSC.Editor`, `MSC.NPC.Tests.EditMode` and
  `MSC.Audio.UnityFallback.Tests.PlayMode` compile with zero errors;
- full private R1 voice/catalog builder: PASS with 17/17 hash-locked WAV imports,
  17/17 supplemental event definitions and no duplicate output folders;
- post-Farmer grounding `MSC.NPC.Tests.EditMode`: PASS `14/14`; focused NPC
  PlayMode: PASS `2/2`; Farm Bootstrap streaming PlayMode: PASS `1/1`;
  `MSC.Audio.UnityFallback.Tests.PlayMode`: PASS `7/7`.
- post-Teimo-bicycle motion/greeting `NpcFoundationTests`: PASS `15/15`;
  focused generated character PlayMode: PASS `3/3`. Coverage includes both
  37-point routes,
  schedule masks/times, once-only completion, terrain conformance, presentation
  switching, mid-route save restore, stream unload/reload, exact tire contact,
  wheel/pedal ratios and the persisted once-per-day wave gate.
- post-visibility coordinate correction: catalog builder PASS, focused NPC
  EditMode `15/15` and PlayMode `2/2`. All 74 donor-world spline points now use
  the audited M04A1 source-to-project translation and are required to resolve to
  existing `cell_-2_0` / `cell_-3_0` entries in the active streaming manifest.
  The previous unconverted positions and nonexistent `cell_-2_2` through
  `cell_-4_2` ownership are rejected by the builder/test gate.
- post-pedalling/curved-route/shop-arrival rebuild: PASS with character manifest
  schema 5 revision 8; focused NPC EditMode `16/16` and PlayMode `3/3`. Coverage
  includes both leg chains, timed arrival knots, schedule transition, parked-bike
  visibility, service-door timing and saved mid-route progress.

The schema 5 revision-8 report records source manifest hash
`eba23a28f8abe171d96aff278952c1cd7778581d1e10eab901d62af858aebc9b`.
The voice report records manifest hash
`f3e19eff247df14fda756b267def56bc82a8dc304756eb7dc067bb3574efb9c1`
and the frozen scene hash `c3f2f337...0476c4`. The AssetRipper YAML
`AudioClip.m_Length` values do not consistently equal the duration of the
exported RIFF PCM data, so the manifest pins and validates both independently;
Unity playback length is checked against the PCM data chunk.
The user accepted the character/accessory placement and Fleetari's final
chair/desk-relative pose in game on 2026-08-01. A later observation correctly
showed that Teimo's daytime schedule still used the serialized pub endpoint.
After the exact endpoint correction, the user manually confirmed on 2026-08-01
that Teimo's daytime and evening placement is now correct. This does not revoke
Fleetari's accepted pose and is not verification of either NPC's complete donor
behavior.

On 2026-08-02 the user also accepted the Farmer's corrected movement and terrain
grounding, and confirmed that the Berryman's tent is present and his seated pose
inside it is correct. These decisions close the two bounded presentation defects;
they do not verify the remaining job, dialogue, audio, save or streaming flows.

On 2026-08-02 the user conditionally accepted the revised Teimo bicycle pass
after observing the moving bicycle and shop transition. This closes the current
R1 iteration for progression purposes, but deliberately leaves the final visual
comparison, greeting repetition and donor collision response open; neither the
NPC nor the bicycle is promoted to `Verified`.

## Not complete and not claimed

- remaining conditional donor dialogue beyond the bounded greeting/pub/babble
  selection (payments, repairs, junk cars, hay/combine and berry settlement);
- Teimo bicycle collision response and donor ragdoll transition; the original
  permanent sleeping-doll result remains the Phase 1 parity target. Recovery by
  remounting or walking is recorded only as a Phase 2 candidate;
- final manual comparison of leg contact, curved road clearance, dismount,
  walk-around path, service-door swing, parked-bicycle visibility and greeting
  repetition after the bounded pass was conditionally accepted;
- repair, hay/combine and berry-sale gameplay authority;
- full R1 save/streaming manual comparison beyond the accepted Farmer movement
  and Berryman tent/pose presentation;
- complete-map visual audit beyond the user-reported StrawberryField, septic
  job locations and Farm;
- the remaining 69 roster rows in R2–R5.

The four R1 rows are therefore `PartiallyImplemented`, not `Verified`.
