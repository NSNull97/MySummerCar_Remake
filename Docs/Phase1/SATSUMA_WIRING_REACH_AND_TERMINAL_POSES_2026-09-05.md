# Wiring reach, occlusion and terminal rest poses — 2026-09-05

## Scope / classification

Bounded runtime correction and isolated Editor refresh, not a new electrical architecture. BehavioralReference / ConfigurationTransferred / Reimplemented; existing donor-derived visible meshes remain TemporaryDirectImport. No electrical DTO/schema, installed wire flag, terminal stage, mount or part stable ID is changed. No user save is edited.

Unity execution belongs to the integrating session. This report records source inspection and source-ready tests, not a claimed passed runtime comparison.

## Confirmed reach defect

SatsumaWiringConnectorInteractionTarget previously called both CanActivateCluster and ActivateCluster with its own transform.position. This made the0.1m tolerance test tautologically pass for the selected endpoint and allowed other nearby endpoints to be gathered around the selected point rather than the held wiring spool.

Frozen donor evidence remains the existing project-owned Phase1SatsumaElectricalFsmAudit.csv / Phase1SatsumaElectricalStateAudit.csv. Representative Assemble FSM104461 (GaugeExtra/HarnessGauge):

- State1/Assemble use GetDistance against WiringTool, compare Distance with Tolerance=.1.
- Assemble listens to Use.
- Sound sends CLOSELOOP to _OtherEnd; the other endpoint must participate.
- Finish assembly sets database Installed, enables the wire presentation and sends RESETWIRING.

The existing cluster handshake is preserved. A physically overlapping pair can legitimately finish with one Use; the already-accepted OneUseCanReachBothDistinctEndsWhenDonorEndpointsPhysicallyOverlap test remains valid. No artificial minimum two-keypress rule is introduced. Existing pending reset/prerequisite rules remain unchanged.

The actual held tool identity is obtained by PlayerInteractionController from PhysicalCarryController; the real wiring WorldItemInstance implements IHeldToolIdentity as a component on the carried spool. The corrected target requires this physical component and uses its transform.position. A nonphysical synthetic identity cannot pretend to provide a physical wiring position.

Two regression cases distinguish the bug:

1. Endpoints8cm apart; spool9cm outward from the first,17cm from the second. First Use arms only the first end. Moving the spool to the second completes the wire.
2. A distant spool cannot arm an endpoint, while a spool within0.1m of both real overlapping ends may still complete in one Use.

No installed wire flags are rolled back. In particular an unexpected installed Alternator wire in an existing save has no recorded input history; this patch cannot prove how it was originally installed.

## Availability and narrow occlusion

The authored registry still has52 endpoints for26 fixed pairs. The known stock gates are unchanged: engine block plus alternator/starter/electrics; battery; dashboard plus meters; steering column; radiator or racing radiator; tank; individual lights/radio. Optional accessory ends remain unavailable while their required accessory part is missing. Installed connections hide their endpoints. Armed endpoints are temporarily hidden until completion/reset. Starter ground retains the donor mounting-bolt Stage<8 gate, not the positive cable nut gate.

The six reported stock missing circuits had authored points, unlike the missing mechanical bolts. No connector coordinates are relocated by this patch. It addresses a targeting limitation: an endpoint inside its own chassis collider could lose to the solid ancestor before the trigger was reached.

SatsumaWiringConnectorInteractionTarget now uses the existing IParentColliderOcclusionBypass capability only when:

- endpoint remains available;
- the occluder host belongs to its same SatsumaElectricalSystem;
- the endpoint is actually a descendant of that registered host.

A sibling installed part or another vehicle does not qualify. Unregistered walls do not qualify. Ordinary no-bypass query behavior is unchanged.

The shared query had a related narrow hazard: once the nearest collider was bypassed it did not check another solid wall farther along the ray. Only for a bypass candidate, the patch scans the existing fixed hit buffer and rejects any other non-bypass solid before the target. It performs no new raycast, allocation, layer-mask change or global through-wall exception.

Typed query tests cover own registered ancestor allowed; unrelated wall blocked; sibling part blocked; a wall behind the allowed ancestor blocked. They prove the narrow mechanism, not a manually observed ray from every possible bodywork angle. If another physical component blocks a stock point, that is not silently made transparent.

Optional diagnostics: logWiringActivations defaults false on the connector. When explicitly enabled it emits one SATSUMA_WIRING_USE line per accepted Use containing selected connection/endpoint, actual spool position, selected endpoint position, pending bits before, completion count and installed result. It does not log every frame.

## Confirmed terminal reload defect

Builder BuildSatsumaElectricalFastener creates a donor-relative owner marker, then an identity child renderer. It passes the OWNER as fastenerPresentation. Consequently the marker's nonidentity rotation is essential; it is not safe to use identity as the animation base.

The runtime cached baseRotation only in a nonserialized private field. Configure captured the proper rest rotation while building, but prefab instantiation reset the cache to identity; OnEnable/Update then applied identity times the stage rotation to the owner. The serialized prefab itself still contained the correct donor marker rotation, proving a runtime cache/reload defect rather than bad source coordinates.

Source: same frozen GAME.unity SHA c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4, read-only.

| Fastener enum | Connection presentation Transform | Intermediate transform | Marker Transform | Identity mesh child | Physical marker scale |
| --- | ---: | ---: | ---: | ---: | ---: |
| BatteryPositiveTerminal | 60044 | 42507 | 53709 | 55114 | .8 |
| BatteryNegativeTerminal | 65291 | 45577 | 63573 | 44832 | .8 |
| StarterCable | 67843 | none | 66724 | 48986 | .5 |

The starter marker has approximately90degrees about localY, directly contradicting an identity animation base. Battery markers combine the measured intermediate parent rotation with their own local rotation. The scoped helper contains the exact source parent/marker transforms and derives the final rest pose; it does not use Euler guesses or current live turned geometry.

Corrections:

- baseRotation and hasAuthoredBaseRotation are serialized presentation fields.
- Existing unbound prefabs capture their authored marker rotation once before any stage rotation is applied.
- Re-enable never captures an already turned rotation as a new base.
- Phase1SatsumaElectricalTerminalPoseAuthoring validates the three explicit electrical presentation bindings, marker frame/scale and renderer ownership before storing the reviewed rest rotations.
- Both current correct authored pose and the specifically identified old identity-cache error are accepted; unrelated pose drift rejects before mutation.
- Existing13degree-per-stage presentation is preserved; its original tuning is not newly certified by this rest-frame patch. No terminal size, mesh, travel, stage or electrical thresholds are changed.

Typed tests clone the object through Unity serialization at all stages0..8 and re-enable each terminal, with a translated/rotated vehicle fixture. They compare final world position against the original connection-relative source pose and local rotation against donor rest times the existing stage rotation. A third-marker drift test checks preflight atomicity.

## Integration

Hook after the electrical system and its three terminals are configured:

Phase1SatsumaElectricalTerminalPoseAuthoring.ApplyToInstance(electrical)

Scoped executeMethod:

MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaElectricalTerminalPoseAuthoring.RefreshElectricalTerminalRestPosesBatch

No full vehicle/world rebuild and no save mutation. Expected unbound old prefab3rest bindings; repeated refresh0. A fresh builder using the now-serialized Configure path may already be bound and return0.

Source-ready suites:

- SatsumaElectricalTerminalPoseTests:3 cases.
- SatsumaWiringReachAndOcclusionTests:6 cases.
- Existing SatsumaElectricalWiperTests should also run, especially pending reset, wrong/empty tool, ground bolt gate and one-Use overlapping endpoints.

The integrating session records actual compile/test/refresh results. Physical obstruction from sibling components remains a legitimate blocker; full52endpoint manual play verification is not claimed. Separate optional accessory implementation and the old radio-on-loose-dashboard ancestry question remain outside this packet.

## Actual held-spool access: fuel-tank connector (2026-09-06)

The opt-in `CanonicalWiringReachPlayModeTests` restores a read-only copy of the
reviewed native record, uses the actual item presentation/provider and physical
carry, and dispatches ordinary F through the interaction query. The native file
is hash-guarded before/after; the reviewed snapshot SHA is
`2990953C2A6882395D658353C2829416772ECC7CF7F8B3D506AED80EC64E16AC`.

The first real-contact run reached the tank endpoint by query but stopped its
spool pivot at 0.13285m. This was an incorrect **test approach from above**, not
evidence of a runtime reach regression. Diagnostics identified the sole held
collider as the donor-sized Box `(0.09,0.09,0.055)`, center `(0,0,0.013)`, with
contact against `collider_floor3_92417` on the shell (surface gap 0.000113m,
penetration 0.000325m). The connector lies inside that solid floor.

Frozen source MeshCollider92417 is convex, mesh GUID
`6fced4532db64dc45ac69cff5068620f`; the generated mesh is byte-identical to
`Assets/Mesh/CarCollider_floor3.asset`, SHA256
`0A2B5698D7220C74D9EDC0AB91410ADAF2D43CDF82D3851C54CD51FC16076675`.
Source Transform70127 has positionY -0.539191 and rotation
`(0,0.7071068,0.7071068,0)`, also preserved. Its mesh Z extent
`0.4774335 ± 0.027957499` becomes vehicle-local floor Y
`[-0.089715,-0.033800]`. TankConnector Transform45978 is
`(0.1799,-0.0858,-1.0901)`: approximately 4mm above the underside and 52mm below
the top. Access is therefore from **under the car**, not through the boot floor.

The fixture now supplies explicit service-stand ground and four supports under
actual shell surfaces, preserving the copied vehicle poses. Only the measured
FuelTank/0 endpoint uses one bounded lower approach, with the camera verified
above the stand ground; the other five circuits retain exterior/service
approaches. The 0.1m gate, normal carry, all installed colliders and foreign-body
occlusion remain unchanged. The source compiles; the updated lower-approach
Unity service-stand run passed both fuel-tank ends, both rear-light circuits and
both alternator ends through actual carry/query/F. FuelTank/0 reached 0.07883m
from below. The executed foreign-solid F rejection/recovery case also passed on
the real rear harness.

That run exposed the same approach-side mistake at the front-light shared
connector: from the nose, the spool stopped at 0.11336m on
`collider_front_91938`. Its mesh GUID
`a42b37d9a5337da488128a1ad1ab7e4e` remains byte-identical to frozen
`CarCollider_front.asset`, SHA256
`59849057FE5E8625E5E190B361902407603A949745B6BEAB3D0112D92F53651D`.
The preserved collider frame puts its Z bounds at `[1.603675,1.722306]`.
Shared front-light connector Z1.633 is approximately 29mm from the engine-bay
face and 89mm from the nose face; the individual lamp connectors Z1.599 are
behind that panel. The fixture therefore adds two bounded rearward/upward
service approaches for the two front-light circuits, behind the installed
lamps through the normally opened hood. It does not remove lamps or change
their prerequisite gates, radius or collision. This final front-service
approach update compiles; its Unity run remains pending.

### Front shared connector: executed contacts and ordinary spool rotation

The subsequent broad PlayMode run, recorded in
`Logs/codex-night-playmode-complete-final-20260906.xml`, passed 104/105 tests.
The six-circuit test still stopped at `HeadlightLeft/0`: the first rearward
approach reached 0.10993m, with contacts near the front shell and inner right
fender. Thus the earlier AABB-based rearward approach was not itself sufficient;
this was not reported as a completed front-light connection.

The frozen `MeshCollider91938` and `MeshCollider92698` both have `m_Convex: 1`,
`m_Enabled: 1`, `m_IsTrigger: 0`; the generated colliders preserve those flags.
The generated cooking option is 30; the old serialized-version-2 source does
not expose that field. Identical meshes and flags do not certify identical
PhysX cooking between Unity versions, but no convex/trigger authoring mismatch
was found. There is no permission to replace or disable these solids.

A read-only decode of the 15 front-mesh and 12 inner-fender-mesh vertices, in
their preserved vehicle frame, locates the common connector at
`(0.3393,0.007,1.633)`. The nearest rear convex plane has outward normal
approximately `(0,0.082256,-0.996611)`; the connector is 14.52mm inside it.
The nearby inward-facing fender plane has outward normal
`(-0.977605,0.209919,-0.014916)`; the connector is 20.30mm outside that plane.
These face distances refine the earlier 29mm global-AABB estimate. The front
mesh's bottom is roughly 20cm below the connector, so a straight underbody
approach is not the measured short access path for this endpoint.

The actual spool has a 90x90x55mm BoxCollider with centre `(0,0,0.013)`.
For the existing rearward approach `(0,0.65,-1)`, a normal local yaw of -90
degrees points that +13mm offset toward the engine-bay centre, away from the
+X inner fender. The opposite +90-degree turn points the offset toward the
fender; the two signs are not equivalent. A conservative separating-plane
calculation for this oriented box clears both measured hulls with its pivot
85.73mm from the connector. This calculation excludes PhysX contact margins,
other geometry, and the held-body solver: it motivates a physical test, not a
claim of runtime reach. One bounded lower, central service approach
`(-0.5,0.15,-1)` with the same turn has a calculated threshold of 83.27mm.

The fixture now tries those two measured approaches through
`PlayerInteractionController.RotateHeldObject(new Vector2(-90f,0f))`, after
asserting `CanRotateHeldObject`. No spool Transform/Rigidbody pose is written
after pickup; the existing 65 physics steps must resolve the rotation and
contacts. The command is recorded as `rotateYaw` in success/failure diagnostics.
This is limited to the common harness endpoints `HeadlightLeft/0` and
`HeadlightRight/1` (the right circuit stores its pair in the opposite order).
Individual lamp endpoints and all other service approaches retain their prior
orientation. The 0.1m production gate, tighter 0.095m test threshold, actual
colliders, camera/start overlap checks, source-save hash guard, and ordinary
query/F route are unchanged. The rotated physical run remains pending.

### Reach-qualified candidate selection — 2026-09-06 source checkpoint

The rotated physical run did execute afterward:
`Logs/codex-night-wiring-rotation-20260906.xml` passed the foreign-wall negative
case, but the six-circuit case failed at `HeadlightLeft/0`. Ordinary spool
rotation reached **0.07873m**, inside the unchanged 0.1m gate. The selected
candidate nevertheless remained endpoint `41844` (Regulator/Alternator), nearer
to the camera but outside the spool's reach. This is a confirmed candidate
selection defect, not evidence that these body colliders need to be disabled.

Frozen donor source was re-read, without modification, for Assemble FSMs
`104480` (HeadlightLeft/ConnectorLight, Transform37388), `105732`
(RegulatorAlternator, Transform41844), and representative `104461`.
`Phase1SatsumaElectricalStateAudit.csv` rows117-118 and183-184 identify the
relevant states; the original serialized action parameters establish:

- `State 1` sets `GUIassemble=false` and clears `GUIinteraction`.
- `GetDistance` uses the endpoint owner and global `WiringTool`. The authored
  `Tolerance` variable is 0.1m. `FloatCompare.lessThan=ASSEMBLE` enters `Assemble`.
- `Assemble` sets `GUIassemble=true` and the connector-specific label. It keeps
  checking distance; `greaterThan=LOOP` leaves for `State 1`. Only this state
  listens for `Use` and enters `Sound`.
- Both affected FSMs have identical byteData for those two states. The
  `SetBoolValue.boolValue` two-byte field at offset13 is `0000` in `State 1`
  and `0001` in `Assemble`. The donor's exact-equality transition is unset;
  this bounded correction preserves the existing remake's inclusive `<=0.1m`
  selection/activation boundary rather than introducing new hysteresis.

The component now reuses one actual-tool-position reach predicate in
`CanSelectForCarriedObject` and `CanActivateHeldTool`. Existing query filtering
runs before ranking both ordinary hits and origin-overlap candidates, so an
unreachable endpoint no longer masks a reachable one. No generic query,
collider, radius, other-car exception, connection flag, DTO, or overlap/Use
handshake is changed. Foreign solids still use the existing occlusion checks.

Seven new typed-query cases cover two aligned endpoints (ordinary hit and
origin-overlap), each with/without a foreign wall, plus spool distances
0.09999/0.1/0.10001m. The existing four own-parent/sibling/wall cases and one-Use
overlapping-endpoint tests remain. The old `EndpointRayAcceptsWiringMessOnlyAndUsesReviewedTolerance`
fixture now positions its synthetic held tool at the endpoint instead of world
zero; its original 75mm off-axis ray geometry is unchanged.

Status at this checkpoint: source-ready; the new typed cases and a repeat of
the actual canonical six-circuit PlayMode route still require the integrating
session's Unity run. Physical reach alone is not claimed as completed wiring.
