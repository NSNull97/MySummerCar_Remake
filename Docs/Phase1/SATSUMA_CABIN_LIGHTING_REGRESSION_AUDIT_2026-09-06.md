# Cabin controls / lighting regression triage

Input: actual NUnit results `Logs/codex-night-editmode-20260906.xml`.
No Unity process was launched and no user save was edited by this subtask.

## Findings

1. **Real lighting blocker, not a test-only failure:** generated
   `mount.satsuma.headlight-left` and `headlight-right` contain no fasteners,
   with group ON1/OFF0. A bolted lamp can therefore never be represented by
   those definitions. The lighting presenter requires a bolted owner.
   Fresh frozen-source reading confirms BeamsShort106670 reads
   `Data.Bolted` on headlamp databases557/5205 in addition to bulb wear and
   wiring. The gate must remain; exact headlamp fastening authoring is needed.
   Its independent mapping audit is assigned to the fastener agent. The test
   now reports this missing authored prerequisite explicitly, rather than
   weakening the light-output assertion or synthesizing a bolted flag.
2. Cabin EditMode fixture flattened every PartInstance subtree, so one hood
   target was returned through both its own dashboard and ancestor part roots.
   The fixture now queries only the exact dashboard definition owner. The
   equivalent PlayMode selector was corrected too. Production authoring was
   already owner-scoped and is unchanged; no duplicate real target was deleted.
3. The hazard test passed the electrical gates and .4s/.4s waveform, then
   failed its immediate OnDisable assertion on a non-ExecuteAlways component
   instantiated in EditMode. The unit test now invokes that lifecycle cleanup
   explicitly, and a new PlayMode test verifies actual disabling clears every
   output. Gameplay is not made ExecuteAlways to satisfy an editor fixture.
4. Wiper tests passed mode restore and serialized rest-basis assertions, but
   failed the final ResetState visual assertion after whole-component
   EditorJsonUtility overwrite. Runtime ResetState already explicitly sets
   mode Off then applies its stored base rotation. The fixture now serializes
   only controller-owned mode/base/presence fields, leaving the Unity native
   component/reference envelope to real scene/prefab serialization. Additional
   assertions require the same knob reference and actual Off mode. This is a
   bounded fixture correction, **not proof of the precise engine-internal
   cause of the former overwrite behavior**; the next Unity run must verify it.
   No wiper runtime axis or reset behavior was retuned.

## Changed scope / checks

Only three test files changed: SatsumaCabinControlFrameTests,
SatsumaDashboardControlsTests, SatsumaCabinControlFramePlayModeTests.

`dotnet build Logs/cabin-dashboard-regression-compile.csproj --nologo -v:q`
compiled all three against current Unity/project references:0errors/0warnings.
`git diff --check` was clean. Tests have not been rerun by this subtask.

Next check: root's single Unity pass after the real headlamp fastening
authoring is integrated. Keep all bulb-condition, wire and Bolted guards.
