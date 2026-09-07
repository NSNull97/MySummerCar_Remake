# Unity 6000.6.0f1: first-frame handoff test expectation

Classification: test-contract correction; no runtime or presentation change.
Status: corrected handoff test passed; the executed first-step prediction
matches the runtime position exactly.

## Evidence and diagnosis

`Logs/unity660-playmode-final-20260906.xml` records
`VehicleAssemblyPlayModeTests.M4CarryHandoff_SmoothlyInstallsRepresentativeDrum`
failing at the immediate post-handoff position assertion on
2026-09-06 08:16:11 UTC: expected displacement below `0.01` m, observed
`0.0133733749` m. The same test passed in the previous 108-test run,
`Logs/codex-night-playmode-service-cabin-final-20260906.xml`, at 01:09:19 UTC.

The test, `PhysicalCarryController`, `AssemblyMountHandoffTarget`,
`VehicleAssemblyController` and `VehicleAssemblyPrototype.unity` matched the
external pre-6000.6 backup before this correction. The migration introduced no
handoff algorithm or scene change in those files.

The accepted `BeginInstallFromHandoff` starts `RunInstallTransition` immediately.
Before its first yield, the coroutine accumulates `Time.unscaledDeltaTime`,
evaluates cubic smoothstep over `InstallTransitionDurationSeconds` (`0.17` s),
and writes both Transform and Rigidbody pose. Returning from `TryHandoff`
therefore does not mean the part remains at its released pose.

For the test's `0.15` m release offset, first-step displacement is:

```text
t = Clamp01(frameDeltaTime / 0.17)
displacement = 0.15 * t * t * (3 - 2 * t)
```

The old `<0.01` m release-distance assertion implicitly required a frame shorter
than about `26.7882` ms. The observed displacement corresponds to approximately
`31.2888` ms under the existing transition formula. That frame duration was not
recorded in the failed run, so this numerical correspondence is an inference,
not a claimed timing measurement.

## Smallest correction and verification

Only this test changes. It now calculates the expected synchronous first-step
position from the actual `Time.unscaledDeltaTime`, current start/mount positions
and the public runtime duration. The existing `<0.01` m tolerance is unchanged,
but measures deviation from that expected first step, not from an assumed
unmoved release pose. A diagnostic line records actual delta time, duration,
eased progress, expected position, actual position and deviation.

The successful handoff, released carry state, not-yet-installed state, existing
waits, final installation, kinematic body, preserved scale and visible renderers
remain checked exactly as before. No gameplay clock, easing, activation, physics,
DTO, stable ID, scene, prefab or runtime code was changed. Source/backup diff and
whitespace checks were inspected.

## Executed confirmation

`Logs/unity660-playmode-final-r3-20260906.xml` records this test **Passed** at
**2026-09-06 08:31:55 UTC**, including the unchanged final installation checks.
Its diagnostic records the following values (decimal separators normalized):

| Measurement | Executed value |
| --- | --- |
| Actual first-frame delta | `0.0373128951` s |
| Runtime transition duration | `0.17` s |
| Eased first-frame progress | `0.1233769` |
| Expected position | `(154.515, 1.60149348, -1037.98)` |
| Actual position | `(154.515, 1.60149348, -1037.98)` |
| Position deviation | `0` m |

This measured frame exceeds the old implicit `26.7882` ms limit, yet the actual
handoff follows the existing easing contract with zero prediction error. This
confirms the corrected first-step expectation without a tolerance increase or
runtime modification. The full r3 run recorded 169 passed, 2 failed and 2 skipped
out of 173; this scoped result does not claim that the entire run was green.
