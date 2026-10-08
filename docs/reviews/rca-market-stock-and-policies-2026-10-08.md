# RCA: market stock rows and kingdom policy ids (#755, #756, 2026-10-08)

**Summary.** Two follow-ups from the Arthedain review.

- **#755:** cultures whose gear carries another culture's tag (Arthedain: Arnor armour tagged Gondor, Númenórean blades untagged; Lindon: Rivendell's) could not stock it beyond `armour_from`'s character armour. The fix adds `<Stock>` rows to the market config.
- **#756:** 15 kingdoms listed two policy ids the engine does not define, and vanilla's misspelt `policy_land_grants_for_veterans` left Gondor without its kingdom policy. The fix removes the dead rows, corrects Gondor's id, and adds a test against the engine's list.

A seven-lens review of the first implementation found no CRITICAL or HIGH. Its confirmed findings are below.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | A `<Stock from>` row re-classified every item, ignoring `<Routing>` and the donor's blacklist; and it ran before `armour_from`, so a donor's Stock-drawn armour could pass to a third culture | Parallel derivation | The new path re-derived culture membership instead of reading the pools already built | One draw path: `from` reads the donor's built pool, and `armour_from` is the implicit row `<Stock from="donor" kind="armour" />` (`MergeArmourDonors` deleted). Tests for routing, the donor blacklist and parity with the six `armour_from` tests |
| 2 | MED | A typo in `from` or `match`, or an Armory rename, silently emptied a row: no runtime warning, no validator check | Silent config | Only a positive total was logged; ARMOUR_ACQUISITION_REF covered `<Culture id>` and `armour_from` only | Per-row "adds no item" warning; the validator fails an unknown `from`, a `match` that does not compile, and a `match` that finds no item id |
| 3 | MED | `KingdomPolicyIdsTests` covered the kingdom files but not the culture `<default_policies>`, where a dead id adds a null that the Policies screen reads | Test scope | The test followed the #756 report, not every reader of the same id kind | Culture files added as DataRows |
| 4 | LOW | The weapon flag was a hand-kept `ItemTypeEnum` list that missed `Sling` and `SlingStones` | Hand copy of an engine fact | Written from the enum, not from the engine's own test | `ItemObject.HasWeaponComponent` |
| 5 | LOW | The engine policy list is a hand copy that stays green if the engine drops an id | Engine-bump drift | No bump step named it | `/engine-bump` step 7 lists it with `ENGINE_REGISTERED_ITEMS` |
| 6 | LOW | Stale docs and comments: `armour_from` "fills the markets" for the two Stock cultures, policy line refs and counts, "no `<Culture>` blocks", the culture checklist without a market row | Doc drift | Docs written around the old single path | Updated; the checklist gains row 15 |

## Root cause

Both issues are the same class: data the engine or a service drops without a word. The fix for each closes the silence at the gate (a test or a validator check that fails on the dead value) as well as the instance. For #755, the first implementation was also the class "a second derivation of a rule the first derivation already enforces". Reading the result of the first derivation removed the routing and blacklist gaps with no new rule to keep in sync.

## Why each lens caught what it caught

- **Standards and Engine compatibility:** the culture policy files (CultureObject's null add, the Policies screen).
- **Data flow:** routing, the build order, and the per-culture draw shares.
- **XML:** what each row selects against the live Armory.
- **Design:** the single-path refactor.
- **Completeness:** the validator gap and the stale docs.

## Lessons appended

- `lessons/data-content-cultures.md`: a culture whose gear is tagged another culture's needs market rows, and an engine id copied by hand needs a gate that fails on a dead value.
