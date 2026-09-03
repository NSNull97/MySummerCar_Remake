# Milestone 12A-E1 — money, prices and transaction foundation

Status: `Implemented / automated validation passed / manual acceptance pending`.

## Inspected evidence

- locked `GAME.unity` SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- locked `PlayMakerGlobals.asset` SHA-256 `0c232081e5dd2d6611d27ccd06274cbb31f5fabc5ef5e5f901b86d39da25afec`;
- donor `PlayerMoney = 3000`;
- Teimo `Prices` table: 38 key/value entries;
- `Inflationrate = 0.054`, `PriceMultiplier = 1`, `RestockDay = 4` and the Thursday additive price update.

The Editor evidence reader verifies both source hashes before decoding values.
No donor PlayMaker controller or runtime script is transferred.

## Implemented boundary

- fixed-point money in integer minor units (`long`), with a 3000.00 MK fresh balance;
- immutable project-owned price catalog containing the 38 locked Teimo prices;
- weekly additive 5.4% Thursday inflation with skipped-day handling;
- atomic debit/credit ledger, insufficient-funds rejection, idempotent transaction IDs and bounded refunds;
- transaction kinds for purchase, refund, reward, fine, service and bill payments for later service/job integration;
- required `economy.player` schema-1 save domain and document migration 13 -> 14;
- live binding of the accepted 08A money HUD through `IPlayerMoneyService`;
- deterministic Editor builder and JSON build report.

## Executed validation

- Unity batch rebuild: PASS, 3000.00 MK, 38 prices, 5.4% Thursday inflation;
- Economy EditMode: 4/4 PASS;
- Save Integration EditMode: 29/29 PASS;
- focused live-money UI PlayMode: 1/1 PASS;
- production Bootstrap lifecycle PlayMode: 1/1 PASS;
- full UI PlayMode after capability-agnostic DLSS assertion: 19/19 PASS.

The first full UI run exposed a pre-existing hardware-dependent assertion that
expected only the unavailable DLSS row. The assertion now accepts either the
truthful unavailable state or the supported quality control; runtime UI behavior
was not changed by that correction.

## Manual acceptance

1. Start a fresh game and confirm the top-right money HUD displays `3,000.00 MK`.
2. Save to a new slot, return to the menu, load it and confirm the same amount.
3. Load an existing document-version-13 slot and confirm it migrates without an error and receives the fresh 3000.00 MK economy domain.
4. Let time cross from Wednesday into Thursday and confirm there are no Console errors. Visible price comparison becomes available in 12A-S1 when store price labels and checkout are connected.
5. Confirm the existing needs/time/FPS HUD layout is visually unchanged.

## Limitations and compatibility

- E1 is the shared economy foundation, not a store/service gameplay pass. No cash register, basket, fuel pump, pub purchase, repair order, fine UI or job reward flow is claimed yet.
- The locked price catalog currently covers the 38 decoded Teimo store entries. Service, fuel, pub, workshop, fine and reward amounts are captured and integrated by their owning later batches.
- Completed 00–08A presentation remains intact; only the unavailable money provider is replaced by live data.
- Existing save schemas and stable IDs are unchanged. Save document version increases from 13 to 14 through an additive required domain migration.

Exactly one next milestone is recommended: `12A-S1 — stores, fuel, pub, workshop and authorities`.
