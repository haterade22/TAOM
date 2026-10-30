# RCA: career forks content, C2 Gondor pilot (#769), 2026-10-15

## Top-line

C2 is the first kingdom written by the career forks generator: Gondor's three careers (Captain of
Osgiliath, Ranger of Ithilien, Knight of Belfalas) now have a root and six either/or forks at
`tree_version` 2, so old Gondor saves are refunded to the root once. The balance numbers were approved
overnight under the maintainer's delegation; his review and the in-game check are owed.

`/deep-review` ran three lenses (XML, data flow, completeness; the change holds no C# code). The XML was
correct: every id, key and keystone target resolved, bytes were preserved and no other career changed.
One finding was HIGH: an option promised more than the engine does. The rest were stale docs, an
in-game check that could not run as written, weak shipped-data tests and wording.

## Findings and root causes

| # | Sev | Finding (lens) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | "Spears of the Rammas" promised knockdown resistance against charges, but Combat Mechanics knocks a man down on a full-speed heavy charge (Branch A) before any resistance is read; the approved plan keeps that branch automatic (data flow 1) | Description promises a path the effect does not reach | The option text was copied from the plan's worked example ("resist charge knockdown"), written before E10 settled that Branch A stays automatic; the generator checks numbers, not words | Text now "blows and light charges"; the in-game check judges it by troops staying on their feet against blows |
| 2 | LOW | Two options named Castar, but the resource follows the player's kingdom first; a Gondor hero serving another realm gets that realm's resource (data flow 2) | Description hard-codes a runtime-resolved value | The spec was written for the common case | Neutral wording ("your realm's special resource") |
| 3 | LOW | "Shields hold" also covers weapon parries (data flow 3); the influence half of a bundle is 0 outside a kingdom (data flow NIT 7) | Description narrower or wider than the effect | Same as 1 | "Blocks hold"; "while you serve a kingdom" |
| 4 | MED | The 111 replaced Gondor keys stay registered and translated; nothing references them (XML 1, data flow 4) | Orphaned data | The localization tool's career category never owned these hand-kept keys; the C2 summary called them retired | Left in place until C5, which must remove the English registrations first, then the language rows, caches and tool JSON (recorded in the docs) |
| 5 | MED | Docs still said the fork paths were inert, counted 31 choices per career and the old pip totals (completeness 1, 3; XML 2) | Living doc left behind | The docs were written for the engine steps, before any data shipped | Each corrected; counts measured from the XML |
| 6 | MED | The plan's in-game step 6 could not run in the written order (no infantry bonus held), and step 9 needed gold and a companion (completeness 2, 4) | Owed check that cannot be performed | The steps were drafted before the forks were designed | A Gondor pilot checklist in `career-system.md`, with a step for the newly shipped types |
| 7 | LOW | No shipped-data test could tell a forked tree from a flat one, and the troop_class scan passed on zero pips (completeness 8) | Test that passes on empty input | The tests were written while no data used the attributes | Asserts that at least one shipped group has `max_picks` and at least one pip a troop class |
| 8 | LOW | Nothing enforced the plan's rule that F1 B, F5 A and F6 B speak for the career's own class (completeness 9) | Design rule left to the author | Gondor was written by hand correctly; 67 careers remain | The generator refuses another class in slots 1, 5 and 6 |

**Recorded, not fixed here:** `<Culture id="empire_w"/>` in the Gondor careers is a kingdom id that is no
culture, so it never matches (older; the generator copies `EligibleCultures`); a returning co-op joiner
whose career was migrated by the host may never see the refund message (UNVERIFIED); the ability
tooltips do not mention the reach the capstone extends.

## Root-cause pattern

**Descriptions are checked by nobody** (1, 2, 3). The generator prices numbers and validates types; the
text a player reads came from the plan's drafting language. For C3 batches: before a table is approved,
read each option's description against its consumer's description in `CareerPerkConsumerMap.Describe`
and the trap notes in `career-system.md`.

## Lessons

- `data-content-cultures.md`: "A generated option's text is checked against its consumer, not only its
  numbers" (pattern).
