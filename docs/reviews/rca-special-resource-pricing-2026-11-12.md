# RCA: special-resource battle pay and emissary prices (deep review, 2026-11-12)

## Top-line

The change scaled battle pay by the enemy's size (`EarnFromBattle`) and multiplied the Elite Emissary's resource
price by 20 through a per-resource `emissary_price_multiplier`. A tier 2 `/deep-review` ran two lenses, 5 Data flow
and 7 XML, at `effort: "high"`. Together they reported 2 MEDIUM, 4 LOW and 1 unverified item. I checked the code
behind each claim before acting. The MEDIUM and LOW items that touch the shipped behavior are fixed in the same
commit, and one LOW item (F4) is fixed in a follow-up commit. Codex was not run: the maintainer waived it on 2026-11-12 because the Codex usage is exhausted, so no
Codex pass is owed.

Lenses 1 Standards, 2 Compatibility, 3 Efficiency, 4 Completeness and 6 Design were skipped by tier. No engine API
was called by this change.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | The emissary price used the multiplier of the resource the settlement owner resolves to (kingdom first, then culture), while the offers follow the owner clan's culture. When the two resolve to different resources, the price moves by a factor of 20 (an Aserai clan inside a Mordor realm would sell the Mumakil rider for 56,000, above the 10,000 cap). The encyclopedia badge priced by the troop's own resource and would disagree with the charge. Lens 7 F1, lens 5 flow 4. | A price keyed on a runtime lookup instead of the data row | I put the multiplier on the resource because the two charge methods already held a resolved resource. Every test had the troop and the owner agree, and the shipped data never has them differ (lens 5 checked 177 clans), so nothing failed. The state needs a defected clan or a vassal of another culture. | Price on the row: `TroopResourceCostEntry.EmissaryPrice` is folded in at load from the row's own `resource_id`. Tests now cover an owner resource that differs from the troop's, and the data gate checks every price against the smallest cap of any resource. Lesson added. |
| F2 | MEDIUM | The map-bar tooltip row "Per battle" shows the base (`+50`). After the change only a 100-man enemy pays 50, so the row overstates a typical small fight by up to 20 times. Lens 5 flow 8. | A display of a number whose meaning changed | For the earning change I searched for callers of `EarnFromBattle` and for docs that mention the ratio. The tooltip reads the input constant `PerBattleVictoryBase` directly, and a search for callers cannot find it. The code graph covers types, not config fields. | The row now reads "Per {SIZE}-man battle" (new key `taom_res_tt_per_battle_ref`). Lesson added: when a configured value changes meaning, search for every reader of that field and every text that names it. |
| F3 | LOW | The MCM hint for the gold rate says the gold price is "the troop's resource price times this number". The resource price is now 20 times the base, so a player computes 112,000 and sees 5,600. Lens 5 flow 9. | Same as F2 | Same as F2. | New key `taom_specres_mcm_gold_rate_hint_base` names the base emissary cost. |
| F4 | LOW | `EarnerRef.PartyManCount` and its adapter reads are dead data now that nothing divides by it. Lens 5 flow 11. | Residue of a removed consumer | I removed the only consumer and did not search for the producers. | Fixed in a follow-up commit: the field, its two adapter reads and the test-kit parameter are deleted. |
| F5 | LOW | Three comments were stale: `ISpecialResourceService` (two), the `EarnerRef` comment, and the header of `elite_emissary_config.xml`. Lens 5 flow 12, lens 7 F2. | Doc drift | Same as F2. | Fixed. |
| F6 | LOW, unverified | An outside mod, RealmUI, reads `special_resources_config.xml` as a file (`Map/MapBarSeal.cs:59-60`). A reader that matches attributes in order would break on an attribute inserted after `id`. Its source is not in this checkout. Lens 5 flow 14, lens 7 Q4. | Unverifiable external reader | The couplings list names the file, but nobody can read the reader. | The attribute moved to the end of each `<Resource>` start tag, which costs nothing. Whether RealmUI tolerates it stays unverified. |

Agent accuracy: lens 7 cited `WatchfulHoldsAdapter.cs:84` as the place the owner culture is read. That is a different
adapter. The path the emissary uses is `SettlementOwnerAdapter.cs:31-34`, which I read before acting. The mechanism
the lens described is correct and its citation was wrong.

## Root-cause pattern: grep the field, not only the function

F2, F3 and F5 share one cause. I treated "who calls `EarnFromBattle`" as the blast radius of changing what its
argument means. The readers of the input (`PerBattleVictoryBase`, the word "price" in a hint, the word "ratio" in a
comment) were outside that question. `graphify affected` answers it for types only and sees no XML attribute, no
config property and no localized string.

F1 has a different cause. A price depended on a lookup that the data does not guarantee to agree with the row it
prices. The shipped data happened to agree, so every check passed.

## Why each lens that ran did not prevent these

Lens 5 and lens 7 found F1 to F6. The findings came from the reviewers, not from my checks before the launch. Before
launching I ran `graphify affected`, a grep of `Client/` and `Main/` for `MerchantCost` readers, and a sweep of the
live modules. None of them reads a display string or a config property by name. Whether lens 4 Completeness, which
the tier skipped, would have found F2 is unknown.

## Follow-up

- FOLLOW-UP: no issue: the project lead's language pass covers the two new keys. The old keys
  `taom_res_tt_per_battle` and `taom_specres_mcm_gold_rate_hint` are now unused and can be pruned in that pass.
- FOLLOW-UP: no issue: RealmUI's reader of the config file stays unverified until its source is available.

## Watch in game

- Income against upkeep. A fight against 15 orcs now pays 7.5 War Spoils, and elite troops cost 0.05 to 0.3 a day
  each. A player who relies on many small fights to cover a large elite roster may meet desertion earlier than
  before. The upkeep values are unchanged by design.
- Saves made before this change buy 20 times fewer emissary troops with the same balance.

## Feedback memories to codify

None. The two lessons below are cross-feature rules and live in the lessons files.
