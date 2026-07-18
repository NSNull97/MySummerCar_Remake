# World audio zones, portals and streaming

Status: **RuntimeHooksAutomatedValidated / ProductionPlacementDeferred**

## Authored contracts

`AudioEnvironmentZone` is a trigger-backed stable-ID volume with priority,
listener space, shelter, obstruction and reverb-send values.
`AudioListenerContextPresenter` chooses the highest-priority valid active zone
and publishes one listener/environment context. The supported spaces are
exterior, sheltered, interior and vehicle interior.

`AudioPortalAuthoring` connects up to two explicit zones by stable ID and stores
normalized openness. Door/window gameplay owns the value; audio only consumes
it. `AudioSurfaceAuthoring` exposes explicit material/surface metadata. Player
footsteps consume it now; generic impacts can consume the same typed boundary
later. None of these systems uses donor names or hierarchy paths.

The home house/garage production exposure output is also connected as a
fallback listener context. Explicit audio zones still win, so future authored
rooms can override that coarse fallback without changing weather ownership.

## Current ambience IDs

The declared world events are forest ambience, lake ambience, garage room tone,
generic interior room tone, distant traffic, birds and insects. They are
authoring/runtime-map hooks; production zone placement, scheduling and streamed
loop composition are **DeferredHook**.

## Streaming safety

Emitters expose the owning Unity scene handle. `AudioBackendRouter` and both
backends remove scene-owned emitter/voice state when a scene unloads and perform
idempotent teardown on disable/session end. No persistent gameplay state is
stored in a Wwise object or donor hierarchy.

Domain-bank leasing, per-cell ambience manifests and Wwise spatial-audio room/
portal objects remain later work. This milestone does not pretend the metadata
hooks already provide production obstruction, diffraction or reverb.

## Budget boundary

There is no per-frame raycast for every emitter. Obstruction and reverb are
context values until a measured, queued occlusion system receives an explicit
query budget. The verified Milestone 08 occlusion budget is `0` queries/frame.
The post-remediation bounded scenario observes 2 emitters/0 voices at baseline,
3/5 at peak and 2/0 after cleanup. Production streamed-zone placement and
audible spatial mix
remain deferred/manual.

## Donor content

Private lake/wind/bird/room-tone donor clips may be locally auditioned only when
hash-ledgered as `TemporaryDirectImport`. They remain ignored and do not make a
zone production-ready. Final ambience and room tones are newly authored.
