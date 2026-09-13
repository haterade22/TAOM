# RCA: the kingdom armour overview (#581) deep review

**Scope.** Six review agents (standards, engine compatibility, efficiency, completeness,
cross-system data flow, tooling correctness) over the uncommitted #581 changeset: the new read-only
`tools/analyze_kingdom_armour.py` and its tests, the `CROSS_CULTURE_ARMOUR_INVERSION` validator
gate in `tools/taom_schema.py`, the additive loader keys in `tools/fix_upgrade_armour_regressions.py`,
the `mumak` rider marker, and the docs. No C# changed. Standards, efficiency (the tool runs in
0.8 s, the validator pass in 0.18 s) and completeness passed. Compatibility verified all four engine
claims the arithmetic rests on against the installed 1.4.8 DLLs (`Equipment.Get*ArmorSum` sums one
stat over slots 5 to 9, `DefaultCharacterStatsModel.GetTier`, the per-slot seeded draw in
`Equipment.GetRandomEquipmentElements`, `ArmorComponent.Deserialize`). The data-flow agent
reproduced the validator's four-cell verdict from the tool's own preview to the digit and reported
three dormant gaps and three documentation gaps. The tooling agent returned two MEDIUM findings,
both real on today's data.

**Fix state.** Every confirmed finding below is fixed in the same changeset; the tools suite is
1,319 tests green (up from 1,316), the validator reports 0 errors and the same four warning cells,
and the regenerated report reproduces the hand-verified numbers (`gondor_mt_fountain_guard` 194 =
33/71/61/29, `gondor_ith_moon_guard` 180, Dunland T6 max 182, Dunland T5 head 40).

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (confirmed) | The ladder-exempt troops (`_ARMOUR_LADDER_EXEMPT`) were left out of the gate but kept in the matrices and the pairwise list, so `gondor_ithilien_ranger` (L51, light kit by design, 125 armour) filled Gondor's T10 cell and topped the executive summary's worst-pairs list five times over, reading as the loudest live regression in the game. | exclusion scope | The exemption was designed for the validator and bolted onto the tool's gate preview only; "which view should an exempt troop appear in" was never asked per view. The plan text even recorded "matrices include everything except bodyless" as a choice, without noticing the summary is built from the pairs. | `_ladder_records` is one predicate shared by the matrices and the pair list, and the gate's own cell builder sits on top of it; a test pins that an exempt troop is absent from all three and present in the records. The report states the rule in "How armour is measured". Lesson below. |
| 2 | MEDIUM (confirmed) | `ceilings()` judged an unworn item's tier on the WEARER's curve (`CURVE_CULTURE_ALIASES.get(culture, culture)`). Umbar wears Harad's folder (85 percent of its slot fills) and carries protection -1 where Harad has -3, so two Harad elite pieces (`harad05_v3_cape`, an elite helmet) read as heavy and vanished from Umbar's reserve list. Goblin and Lindon were safe only because their aliases happen to name the folder they wear. | curve identity | The alias table was built to answer "what curve predicts this TROOP", then reused to answer "what tier is this ITEM", which is a different question: an item was statted on its folder's curve. Nothing in the fixture had a culture wearing a foreign folder with a different modifier, so the tests could not see it. | The tier is judged on `rec['folder']`'s curve; a test monkeypatches a +30 modifier onto the wearer and asserts the foreign elite helmet stays in the reserve. Docstring and doc say which curve each view uses. |
| 3 | LOW (dormant) | A troop with no `level=` is filed at tier 0 by the analyzer (`fx.load_troops` reads an absent level as 0) and skipped by the validator (`level None`). No such troop exists in the 16 files today. | loader divergence | Two indexes of the same files, written months apart, with different defaults for a missing attribute; the analyzer inherited the fix tool's default without asking what the validator does. | `load_troops` records `has_level`; the analyzer excludes `no_level` troops; a loader test and a classification test pin both. |
| 4 | LOW (dormant) | The analyzer excludes creatures and bespoke riders by name marker; the validator knows only `_ARMOUR_LADDER_EXEMPT`. They agree today because every such troop was hand-listed in the exempt set, and nothing enforced that. | exclusion drift | The two exclusion vocabularies were reconciled by hand on the day and left with no invariant. | A test over the shipped troop files fails when a troop the analyzer excludes by name is missing from `_ARMOUR_LADDER_EXEMPT`. |
| 5 | LOW (dormant, pre-existing) | The item roots differ: `fx.load_item_armour` reads `LOTRLOME_Armory`, `SandBoxCore/ModuleData/items` and the repo; `build_registries` also reads `SandBox`, `Native`, `StoryMode`, `CustomBattle`, `NavalDLC`. None of the extra roots defines an `<Armor>` item on the installed 1.4.8, so the two tables are identical today. `build_item_armour` also iterates `rglob` unsorted, so on a duplicate id the validator's winner is filesystem order while the tool's is sorted; `DUPLICATE_ITEM_DEF` reports zero duplicates. | pre-existing divergence between the fix tool and the validator | Outside the #581 edit scope; the gate inherited the validator's table as the edge gate did. | Recorded here, not fixed (edit-scope discipline). Follow-up: point both loaders at one root list and sort the validator's iteration. |
| 6 | LOW (docs) | Three behaviours were documented in code comments only: items worn only by an excluded troop (the troll plate) count as worn but their folder counts for no ceiling; the goblin armour alias (mordor) disagrees on purpose with the skill curve, which keeps goblin its own culture; the per-slot draw holds for the campaign spawn path (seed never -1), not for the method in isolation. | docs | Written into the tool while the doc section was drafted from the plan. | All three are now in `docs/features/armor-balance.md` and the report's data-quality footer. |

**Not a finding.** The tooling agent's premise that most Armory files carry a BOM does not hold on
the current install (0 of 115 `LOTRLOME_items` files); ElementTree reads both shapes. The HERO_NAMES
substring match in `rebalance_armor.is_excluded` (`dain` inside a hypothetical "Dunedain") has no
live instance and is that tool's pre-existing behaviour; the `and iid not in worn_by` guard was
shown to do real work (`erkenbrand_torso`, `theodred_armour` are worn by an ordinary Rohan troop and
stay eligible).

## Root-cause pattern

Findings 1 and 2 share one shape: **a rule designed for one consumer was reused by a second
consumer that asks a different question.** The exempt set answers "which cells may the gate judge"
and was reused for "which troops may the summary rank"; the alias table answers "which curve
predicts this troop" and was reused for "which curve statted this item". Each reuse was a one-line
convenience, and each produced a wrong number in the one place the tool exists to be read. The
lesson is to name the question a rule answers, and when a second question arrives, write the second
predicate rather than borrow the first.

## Why each agent missed or caught these

- **Standards** checked structure, the read-only promise, prose and test hygiene, all correctly;
  view-membership rules are outside its checklist.
- **Compatibility** verified the four engine claims and added the seed nuance (finding 6); that
  was its whole remit.
- **Efficiency** measured rather than estimated (0.8 s, 0.18 s, 729k pair checks) and found
  nothing, correctly.
- **Completeness** checked every stated number against the report and the JSON; the numbers were
  right for the code as it stood, which is why a wrong-scope number reads as a correct one.
- **Data flow** found findings 3, 4, 5 and 6 by enumerating every way the two indexes can
  disagree and then proving agreement on the live data; it did not question which troops belong in
  the pairwise view because the doc described that view accurately.
- **Tooling correctness** found findings 1 and 2 by running the tool against the live install per
  culture and reading the output as a user would (Umbar's reserve list, the executive summary). This
  is the third review in a row where the agent the skill adds for tooling is the one that found the
  defects that change what a reader concludes.

## Feedback memories to codify

One new lesson, appended to `docs/reviews/lessons/build-tooling-workflow.md`: a rule reused by a
second consumer answers the second consumer's question, or it gets its own predicate. No new memory
file.
