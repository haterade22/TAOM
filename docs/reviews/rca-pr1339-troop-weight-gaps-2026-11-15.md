# RCA: review of PR #1339, troop weight coverage gaps

PR #1339 (REDACTEDSTUFF, head cf2f3b186) adds 58 rows to `troop_weights.xml`. Commit 1 fills the clear gaps: the level
26 Imladris and Mithlond Infantry now match their Bowman at 2.0, every Hired copy weighs the same as its original, nine
Dual Wield troops follow the level rule, and two Gundabad rows close an upgrade that lowered the weight. Commit 2 sets
every level 31 to 36 troop of the evil and dwarf trees to 2.0. Mike chose to keep both commits on 2026-11-15. The review
ran Lens 7 (XML and ModuleData) on the data, tests and docs. Codex did not run: it is out of usage (Mike, 2026-11-15).

Lens 7 resolved all 58 ids (49 in the repo troop files, 9 in the installed `DualWield` module, v0.10.7), found no
duplicates, no upgrade that lowers the weight, and every comment claim about levels and parents true.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The sweep's comment says every level 31 to 40 evil and dwarf troop now pays 2.0, but the test's culture set held six cultures and left out three: `eredluin` (four troops at level 31 and 36) and the goblin cultures `bluecraig` and `mistymountainorcs` (each a level 36 twin of `goblin_bolgs_ironfang`, which now pays 2.0). All six stayed at 1.0 (Lens 7 finding 1; the session read their levels and cultures) | Hand-written scope list | The culture list was typed from memory of the big factions; nothing compared it with the culture table | Fixed: six rows added, and the test's set now holds nine cultures. The test failed on the six with the rows removed and passed with them. Lesson appended to `data-content-cultures.md` |
| 2 | LOW | Two comments said Gondor, Rohan, Rhun, Harad and Dunland "carry no weights", but `gondor_pg_vet_cavalry` pays 2.0 and the two Harad mount packages pay 12.0 and 20.0 (Lens 7 finding 2) | Loose wording | The claim was true of the troop trees, not of the file | Fixed: both comments name the exceptions |
| 3 | LOW | The comments, tests and doc named the module `DualWield_TAOM`; the installed module's id is `DualWield`, which carries the TAOM troop set (Lens 7 finding 3; the session read its `SubModule.xml`) | Stale module name | The name came from an older module | Fixed: all five places now say `DualWield`. The ids and levels match the installed v0.10.7 |
| 4 | LOW | The doc's 2.0 row described only the old row groups, and the counts rose by six with the fix (Lens 7 finding 4) | Doc drift | The table text predates the PR | Fixed: the row names the Hired copies, the level 31 Dual Wield troops and the sweep; 224 rows, 137 at 2.0, in all three docs |
| 5 | LOW | `configs-balance.md` showed the new counts with the old date 2026-11-03 (Lens 7 finding 5) | Stale date | The count changed, the date cell did not | Fixed: 2026-11-15 |

## Why the lens caught these

Lens 7 rebuilt the culture of every level 31 to 40 troop and compared it with the test's set, which found row 1. It read
the installed module folder for row 3. The full verify before the fix passed, because the test checks only the cultures
it names.

## In-game checks owed

From Lens 7: recruit an Imladris Infantry and Bowman (both show the x2 tag), upgrade an Imladris Infantry to
Blade-Dancer and a Blademaster to Blade-Maiden (the party-size numerator does not drop), and hire a Hired Imladris Bowman
(x2). Also an Ered Luin King's Guard now shows x2.
