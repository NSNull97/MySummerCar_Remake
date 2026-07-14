# ADR-0001: Donor-assisted recreation with reauthored production art

## Status

Accepted.

## Context

The project has access to a licensed local copy of My Summer Car and aims to preserve its mechanics, scale, layout, and identity while moving to Unity 6 HDRP.

Blind clean-room recreation would waste reliable measurements and behavioral evidence. Blind project restoration would retain obsolete architecture and visual assets.

## Decision

Use a donor-assisted approach:

- inspect the original installation read-only;
- transfer measurements, transforms, configuration, relationships, world layout, and selected isolated algorithms;
- use donor meshes as reference/blockout;
- reauthor production models, UVs, textures, materials, LODs, and collision;
- rewrite tightly coupled gameplay systems in a modular Unity 6 architecture;
- keep raw donor content and decompiled material outside Git.

## Consequences

Positive:

- higher fidelity to original dimensions and behavior;
- modern maintainable runtime;
- clean production art pipeline;
- easier provenance and audit.

Negative:

- more up-front tooling and documentation;
- duplicate reference/production asset workflow;
- calibration effort;
- selective direct code transfer requires careful review and tests.
