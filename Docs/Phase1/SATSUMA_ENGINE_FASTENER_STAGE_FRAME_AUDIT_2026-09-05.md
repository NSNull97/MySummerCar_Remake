# Engine fastener stage/frame audit — next bounded packet

Status: `BehavioralReference / ReadOnlyAudit / NotImplemented`.
This follows E2a mesh identity; no source, runtime, prefab, definitions or saves
were changed by this audit. It is not manual original-game capture.

## Evidence checked

Frozen GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Representative oilpan Screw105412/marker40758/child48062 and gearbox
Screw104337/marker36907/child60026 were inspected. Root independently reread
the raw states and decoded their binary parameters, read the staged source
actions SetPosition.cs, SetRotation.cs and GetChild.cs in the external staging
export, and checked the current runtime formula and serialized prefab frames.

| Evidence | Oilpan byte offset | Gearbox byte offset |
| --- | ---: | ---: |
| Screw component | 67812366 | 47992057 |
| Save data2 / GetChild + ArrayListGet Stage | 67812770 | 47992460 |
| Setup2 / Wait | 67860570 | 48040256 |
| Stage0 state / byteData | 67851770 / 67853330 | 48031457 / 48033017 |
| Stage8 state / byteData | 67845046 / 67846606 | 48024732 / 48026292 |

Setup2 waits0.2 seconds in realtime, then Save data2 resolves the actual
Untagged child into ThisBolt, replacing the stale serialized object reference,
and reads Stage before routing into the staged pose states. The raw oilpan
child-.02Z is therefore not an independently authoritative stage-zero rest
offset. It happens to match stage8; the raw gearboxzero matches stage0.

For both representatives the decoded stage0 Z is0 and stage8 Z is-.02m;
zAngle is0/360 degrees. The complete table reviewed by the agent uses
-.0025m and45 degrees per stage. Both action spaces are Self, with
everyFrame=false. Root's explicit numerical recheck covered stages0 and8.
FsmFloat payload here is float32 followed by a one-byte flag, not flag+float.

Important owner distinction: SetPosition references owner-default slot0,
ThisBolt; SetRotation references slot1, the Screw owner/BoltPM marker. The
current project rotates the moving child instead. Matching numeric angles
alone does not prove rotation/frame equivalence for every authored marker.

## Confirmed effective-travel difference

Donor representative markers carry approximately(.7,.7,.7) scale and their
moving mesh children have unit scale. Thus20mm child-local translation becomes
approximately14mm relative to the marker's parent. In the project the target
marker has unit scale and the moving presenter itself carries approximately.7.
A transform's own scale does not scale its localPosition; the existing
`AssemblyFastenerInteractionTarget.ApplyFastenerPresentation` therefore moves
these representatives20mm at stage8. The difference is approximately6mm.
This is a frame/scale calculation, not a measured gameplay capture.

Root checked project presenter/marker pairs:
gearbox5776217017677204248 /7936269980238583522 and
oilpan5000219197995489192 /3451893576515264466. Base position stayszero and
stage-travel scale stays1 in the current E2a contract. Raw-.02Z must NOT simply
be added to that base: it would double-count an initialized stage pose.

## Small next-step specification

1. Review the same stage action/owner contract for all16 E2a IDs, including
   the13mm drain and10mm gearbox bolt with non-uniform Z scale. Do not extend
   a representative.7 assumption to every fastener.
2. Prove marker-versus-child rotation equivalence or document the exact
   correction using donor-rest frames. Preserve mount/interaction poses,
   colliders, mesh scale and accepted suspension/body fastening behavior.
3. Prefer the existing per-target travel-scale authoring field if it faithfully
   reproduces the measured displacement. Do not move/reparent runtime targets
   or globally change the stage formula without separate dependency evidence.
4. Use a separately reviewed scoped refresh with exact preflight, backup and
   idempotence. E2a's preservation guard intentionally still expects scale1;
   update that contract and full-builder path together only in the new packet.
5. Test positions/axes at stages0,1,8 and reversals for all16 IDs, compare
   frame-scaled effective travel, preserve stages/latches/IDs/native17 and
   unrelated prefab payload, rerun assembly/save/front/rear regressions.

The0.2s pre-init visual transient, external late modifiers and original-game
visibility have not been captured live. Ownership retirement, ON15/36 latch
corrections and oil drain gameplay are different packets, not part of this fix.
