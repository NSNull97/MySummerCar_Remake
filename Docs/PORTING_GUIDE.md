# Donor Porting and Reconstruction Guide

## Principle

Use the donor game to preserve identity and behavior, not to preserve obsolete implementation mistakes.

The donor is a measuring rig, behavioral oracle, layout source, and occasional code/data donor. The Unity 6 project is a new production runtime.

## Transfer decision table

| Donor content | Normal strategy | Notes |
|---|---|---|
| Meshes | Reference/blockout, then reauthor | Preserve dimensions, pivots, mount points |
| Textures | Reference only | Recreate all production PBR maps |
| Materials/shaders | Reauthor | Built-in pipeline logic does not become HDRP automatically |
| Terrain/layout | Transfer data, rebuild presentation | Preserve world scale and key locations |
| Object transforms | Transfer after validation | Store provenance and coordinate conversion |
| Colliders | Reference, often rebuild | Gameplay dimensions matter more than topology |
| Audio clips | Reference or temporary prototype | Final target is new Wwise-ready sound design |
| Animation clips | Case-by-case | Verify rig, alignment, and licensing boundary |
| Config values | Transfer with units documented | Convert and test |
| Pure algorithms | Selective code port | Map dependencies and add comparison tests |
| PlayMaker/generated FSM | Behavioral spec and rewrite | Do not paste generated state-machine spaghetti |
| Save format | Inspect and document first | Build optional importer behind native save format |
| UI | Reimplement | New input, resolution, accessibility, architecture |

## Visual reconstruction workflow

```text
Donor mesh
  -> inventory and hash
  -> reference import outside production
  -> measure scale, pivot, axes, mount points
  -> new high-poly or clean production model
  -> retopology
  -> new UVs
  -> bake maps
  -> new PBR textures/materials
  -> LODs and collision
  -> dimensional comparison
  -> production prefab
  -> ledger update
```

The production prefab must not depend on the donor reference mesh or texture.

## Code-porting workflow

For every candidate class or method:

1. Record origin assembly and symbol.
2. Describe purpose in plain language.
3. Map all dependencies.
4. Classify coupling.
5. Decide: port, adapt, specify/rewrite, reject, or block.
6. Write fixtures from donor values or captured behavior.
7. Implement using current project architecture.
8. Compare outputs within documented tolerances.
9. Record known differences.
10. Do not retain old Unity runtime references.

## Good direct-port candidates

- torque or interpolation tables;
- unit conversions;
- small formulas;
- isolated tuning constants;
- value clamping rules;
- save transformations;
- simple mechanical state transitions.

## Poor direct-port candidates

- giant MonoBehaviours;
- global controllers;
- `GameObject.Find` forests;
- coroutine chains tied to donor scenes;
- old input and UI;
- audio routing;
- rendering and lighting;
- physics glue dependent on old PhysX behavior;
- compiler-generated or obfuscated code;
- generated PlayMaker logic.

## Coordinate and unit policy

Document:

- donor coordinate system;
- scale conversion;
- pivot conventions;
- forward/up axes;
- units for speed, force, torque, temperature, pressure, volume, and time.

Use SI units internally when practical. If donor values are preserved in another unit, isolate conversion at the boundary and test it.

## Import proof rule

The first controlled import may include only a few representative items:

- one donor environment mesh as reference;
- one newly rebuilt environment replacement;
- one donor vehicle part as reference;
- one newly rebuilt production part;
- preserved pivot/mount points;
- a new HDRP material;
- a new collision proxy;
- one LOD group.

No mass import until the proof is repeatable, idempotent, and validated.

The implemented manifest schema, planner/executor split, validation commands, failure recovery, and current extraction blocker are documented in `Docs/Porting/DONOR_PIPELINE.md`.
