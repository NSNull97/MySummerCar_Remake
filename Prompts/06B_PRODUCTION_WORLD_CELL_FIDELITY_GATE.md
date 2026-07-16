/plan

# DEPRECATED — DO NOT RUN

The former production-cell fidelity repair gate is no longer the next milestone.

The user confirmed that:

- the original donor map has already been extracted and visually reviewed;
- world streaming has already been connected;
- two custom production cells do not resemble the original locations.

Do not spend the feature-parity phase manually rebuilding those two cells. Their
custom visual content must be marked `PrototypeOnly / RejectedForFidelity` and
disabled from the active world profile. The exact donor map will be used as a
temporary runtime baseline instead.

Run, in order:

1. `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION.md`
2. `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md`
3. `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`

The fidelity capture guide remains useful later when production override art is
created cell by cell.
