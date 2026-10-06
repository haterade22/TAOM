# RCA: Lindon missing from the troop weight and survival bonus tables (#749, 2026-10-06)

**Summary.** A player reported that Lindon's troops take one party slot each and its parties get no
cultural survival bonus, while the Rivendell tree it clones takes 2 or 3 slots per elite and a 0.4
bonus. Both were true from `cc1713eb3` (2026-08-11, v2.0.20), which promoted Lindon from a
Rivendell-culture kingdom to its own `Culture.lindon` with a cloned `lindon_*` tree. Tables keyed on
`rivendell` or on Rivendell's troop ids stopped matching Lindon, and nothing checked them. The fix
mirrors Rivendell in `troop_weights.xml` (22 rows), `battle_balance_config.json` and the compiled
default, with a clone-parity test. The seven-lens `/deep-review` of the fix found no CRITICAL or HIGH;
its findings are below.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The new shipped-JSON test asserted `ContainsKey("lindon")`, which could never fail: the test deserialized with Json.NET's default `ObjectCreationHandling.Auto`, which merges the file into the compiled dictionary, and that dictionary now held `lindon` too. Deleting the JSON row left it green. | Test that cannot fail | The test copied the provider's own deserialize call, so it read the merged table the provider builds, not the file. RED was checked only before the data change, when both rows were absent, so the test was never run against "compiled row present, file row absent". | Fixed: the test moved to `ShippedBattleBalanceConfigTests.cs` and reads with `ObjectCreationHandling.Replace`; proven RED with the JSON row removed. Lesson in `lessons/testing-qa.md`. |
| 2 | LOW | `EveryLindonTroop_WeighsTheSameAsItsRivendellTwin` skipped a Lindon troop with no resolvable twin (`continue`), so a renamed Rivendell troop would have dropped its Lindon twin out of the check unnoticed. | Silent skip in a guard test | The empty-sweep assert counted Lindon troops before the filter, not the set actually compared. | Fixed: an unpaired troop now fails with a message naming the way out. |
| 3 | MEDIUM | The doc sample of the shipped JSON (`docs/features/battle-balance.md`) lacked the new key; `lint_docs.py --drift-only` exits 1 and `check-doc-config-drift.sh` denies the commit. Also stale: row counts and culture lists in `troop-weight-system.md`. | Doc drift | Docs were scheduled for after the review, and the drift gate runs at commit time, not at test time. | Fixed in the same change, except `docs/reference/feature-map.md`'s "105 live rows", left for a later edit because that file holds another session's uncommitted hunk. The gate worked as designed; no new rule. |

## Root cause of the reported bug

A culture promotion moves a kingdom off its host culture id, and every table keyed on the host culture
id or on the host tree's troop ids silently stops applying. `tools/promote_borrowed_cultures.py`
writes the culture and the tree, and `tools/retag_promoted_cultures.py` the retag; neither writes or
lists a keyed balance table. `cc1713eb3` did add Lindon by hand to a few code-side lists (Elite
Emissary and Custom Battle commander id sets, volunteer recruitment), but not to these two tables. The #585 level-band test only examines troops that already have
a weight row, so a tree with zero rows passed it.

The review found the same class still open in other tables (none fixed here, all recorded on the
follow-up list): Lindon's two clans spawn from Rivendell party templates; Elven Wine special resource,
troop resource costs and the Elite Emissary have no Lindon entry; banner bearers, siege-defense
messages, menu link colours and `tools/rebalance_lords.py` have none either.

## Why each lens missed nothing it should have caught

Every finding above was caught in this review. The original bug predates any review of this change:
the promotion commit's review (2026-08-11) had no rule asking which tables were keyed on the host
culture, and the xml-data rule's culture table still lists neither `lindon` nor the other promoted
cultures, so an author following it would reject the key this fix adds.

## Lessons appended

- `lessons/data-content-cultures.md`: a culture promotion drops every table keyed on the host culture
  or its troop ids.
- `lessons/testing-qa.md`: a shipped-config test reads the file without the provider's merge.
