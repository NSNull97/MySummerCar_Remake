## 29A. Approved UI reference lock

For Milestone 08A and subsequent work on its locked screens, the files under:

```text
References/UI/Approved/08A/
```

are authoritative visual specifications, not loose mood references.

Rules:

- Preserve their layout, panel proportions, hierarchy, spacing, selection logic,
  and persistent HUD contents as closely as practical.
- In a conflict between generic UI guidance and an approved 08A visual reference,
  the approved reference wins unless doing so would falsely claim unsupported
  functionality or violate project architecture/safety.
- Do not redesign a locked screen merely to make it cleaner or more conventional.
- Do not crop, trace, or bake reference screenshot pixels into runtime UI.
- Rebuild the interface with project-owned widgets, icons, materials, text, and
  live scene content.
- Do not reproduce obvious AI text artifacts, misspellings, or impossible data.
  Preserve the intended semantic control and its visual slot.
- Unsupported features may retain their visual row for fidelity but must be
  disabled and labeled truthfully.
- Reference PNGs are Editor/review inputs only and must be excluded from shipping
  runtime dependencies.
- Use the reference-overlay and comparison-capture workflow required by the 08A
  prompt.
- Codex may mark a locked screen `ImplementationComplete`; only the user may mark
  it `VisuallyApproved`.
