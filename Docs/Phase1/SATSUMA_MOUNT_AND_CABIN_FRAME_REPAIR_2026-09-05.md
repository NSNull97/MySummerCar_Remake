# Satsuma mount and cabin coordinate repair — 2026-09-05

## Scope and compatibility

Bounded Phase1 `ConfigurationTransferred` / `Reimplemented` fixes for the manual
slot-01 report. The audited snapshot was read once: 2026-09-05 20:32:39 +05,
266021 bytes. No user save is rewritten. Stable IDs, DTOs, fastener stage/count,
dependencies, thresholds and accepted suspension/hinge simulation are unchanged.

New authoring is limited to four existing mount frames and two existing cabin
control bindings. `AssemblyMountInteractionAnchor` is additive: an unbound mount
still aims at its physical `Pose`. The existing hood target, collider and host
retain their component identity. Wiper rest orientation is authored/serialized
configuration, not a new mutable save domain.

The original installation/staging remain read-only. Frozen GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.

## Halfshafts: container frame was not the loose-part frame

The old `AddAssemblyTargetMounts` path selected donor `ActivateThis` containers
48337/60434 and placed the loose part at their origins. That loses the installed
skinned hierarchy's offsets. FL container Z is0.032746315m; its matching
`gearbox_axle2` mesh40854 is offset by X1.129778m below the Y-90deg shaft48021.
The corresponding FR chain is60434→57559→43830. Loose axle children51045/66668
both use position(-0.069,0,0), rotationX-90deg and the same axle mesh as the
installed instances (GUID3384e67ec1d3c2a48928ffa673c6d0a9).

Aligning those common meshes yields the reviewed loose-root frames:

| Mount | Position in chassis metres | Rotation |
|---|---|---|
| halfshaft-fl | (-0.06927559,-0.2229250004,1.162524315) | Y180deg |
| halfshaft-fr | (0.06538736,-0.2229254177,1.1625237096) | identity |

Each of the existing three marker poses is converted by inverse(newFrame) ×
oldFrame × oldMarker. Thus all six physical bolt world points, orientations and
presentation stage travel remain unchanged. Both interchangeable part IDs still
fit either side. Installed save restoration already resolves the mount pose, so
old installed records receive the repaired placement without a DTO migration.
This is a static installation-frame correction, not a claim that donor shaft
skinning/articulation has been implemented.

Raw frozen GAME evidence: Transform48337 byte17373240,48021 byte17250558,
40854 byte14411750,60434 byte22149028,57559 byte20934025,43830 byte15564027,
51045 byte18420359,66668 byte24560677. Existing CSV:
`Phase1SatsumaV1cFastenerAudit.csv` rows37–42 and `MountPoseAudit.csv` row10.

## Doors: aim point and hinge are separate

Door pivots45349/61987 are correctly imported at chassis(±0.677,0.1808086,0.639).
Original trigger37060/36990 is instead at(±0.827,0.1808086,0.113898), approximately
54.6cm away. Original trigger Sphere99027/99025 has radius0.03m and center0.
The old builder copied the radius but placed the ray sphere at the hinge.

The repair leaves the hinge, part snap pose, four10mm bolts and all latch/physics
settings untouched. The existing sphere gets the reviewed trigger center in its
own frame, and an explicit aim anchor makes the surface fallback use that same
point. Handoff installation still uses the mounting pose for part-distance/snap
and preserves the existing pose-snap angular contract.

Loose door bolt visibility is deliberately unchanged: accepted body authoring
keeps visual hardware on the loose panel; wrench availability requires an
occupied mount. This packet does not reinterpret visible loose bolts as a save
or fastener-lifecycle bug.

## Cabin controls

### Hood release

Old control referenced the stationary `dash_hood_lock_80638` housing. The reviewed
animated mesh is GO13396/Transform49450, GUID1b88f46a46e9c8e44b9040dd4972d6a6,
under Handle46962/HoodLocking39435/dashboard37889. The flattened handle pose is
(0.5443001,-0.01560072,-0.13640001) with quaternion
(0.17081988,-0.00000038543052,-0.0000013644272,0.98530227).

The ray sphere now represents Trigger55163/Sphere99736: dashboard-local position
(0.5443,-0.0493,-0.1484), radius0.03, original center
(0.0000016093254,-0.000094999734,0.001113) in its original rotated frame. The
component/host are retained on their established owner; only the explicit mesh
binding, sphere center and outline renderer change.

The original `hood_lock_handle` clip (GUIDc70c9c29ae203b34d9f0d02b5d5c840c)
contains only local-Y position keys: t0=-0.0002, t1/6=-0.0088, t1/4=-0.0002;
slopes-0.0516,+0.025800005,+0.10320001. A project-owned Hermite calculation
reproduces the8.6mm rest-relative pull; there is no invented housing rotation.
The reviewed handle can animate with a missing/open hood, as donor FSM109473
does; an installed dashboard is still needed to send the latch release.
The accepted immediate release transaction is retained, not changed to the
original event-dispatch delay0.04sec in this coordinate/interaction patch.

### Ignition key

FSM107294 sets local-Y angles0/-30/-60deg. The controller previously used X.
Only the axis changes; its authored lock/rest basis, gates, real-time0.4sec hold,
attempted-start latch, starter eligibility and persisted ignition state remain.
A presentation-only `KeySoundRequested(bool)` hook reports accepted physical
gestures for the parallel audio packet; restore/availability calls emit nothing.

### Wiper knob

Knob47794 is locally identity under donor parent57724. Flattening produces its
reviewed basis(-0.1560646,0.00000010121124,-0.00000006454299,0.9877469). Assigning
Euler(0,-45×mode,0) erased this basis. The controller now composes the mode with
a serialized rest rotation and never recaptures the current mode as a new rest.
The scoped helper repairs the previously generated identity basis. Existing
unpowered switch operation, dashboard/meter-fastener gates, wiring and sweep
cycle timing are unchanged. Its existing ray sphere remains on the rotating
knob's Y axis and therefore stays aligned through Off/Slow/Fast.

## Tools and validation

Both authoring helpers preflight known legacy/current shapes before writes and
return zero for repeated current application. Unknown offsets are rejected.
Each batch saves a recoverable prefab backup under ignored Logs before saving.
No full rebuild, donor write or save mutation is required.

Execute methods:

- `MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaMountFrameRepairAuthoring.RefreshMountFramesBatch`
- `MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaCabinControlFrameAuthoring.RefreshCabinControlFramesBatch`

Full-builder hooks: call each helper's `ApplyToInstance(assembly)` after existing
mount/fastener interactions, hood release and `BuildSatsumaElectricalAndWipers`
have been authored. No replacing builder or runtime graph initialization.

Added test fixtures:

- EditMode `SatsumaMountFrameRepairTests`: independent common-mesh transform,
  original/current frame repair, translated/rotated chassis, bolt-world and hinge
  preservation, whole-packet rejection before mutation, unbound mount fallback.
- EditMode `SatsumaCabinControlFrameTests`: existing target identity, true handle
  mesh/trigger, known legacy migration/repeat0, knob modes/DTO/component
  serialization/lifecycle, independent clip keys/tangent calculation.
- PlayMode `SatsumaCabinControlFramePlayModeTests`: real generated cabin ray and
  mouse Off→Slow→Fast→Off while unpowered, true hood handle without installed
  hood, both door rays→handoff using donor trigger with unchanged hinge snap.
- Updated prior ignition axis and wiper-basis assertions; added key feedback
  no-replay/guard test to `SatsumaIgnitionTests`.

At authoring handoff: source and `git diff --check` reviewed. Unity refresh,
compilation and test execution are owned by the root task and are not yet
claimed passed in this report. User visual verification remains required.
