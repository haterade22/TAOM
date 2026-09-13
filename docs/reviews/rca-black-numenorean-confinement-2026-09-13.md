# RCA: Black Numenorean confinement (#584) and the level-41 weight band (#585) deep review, 2026-09-13

Six agents over a data-only changeset: 183 `mordor_num_*` stacks removed from 15 Mordor party
templates and the maxes rescaled to 260, 41 `troop_weights.xml` rows moved from 2.0 to 3.0, two
shipped-data tests, one MCM hint string, the BN wiring script retargeted. Standards found one dash,
compatibility verified the fill formula, the patrol spawner, the template resolution timing and the
size-limit clamp on the installed 1.4.8 DLLs (0 unverified), efficiency and completeness were clean
bar one stale count, tooling had two comment notes. Data flow traced 8 flows and found the one
finding that changed code.

## Findings

| # | Sev | Finding | Category | Verdict | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | MEDIUM (latent) | `generate_clan_heraldry.py`'s `upsert_party_template` refused a stale spec only when it would DROP a troop id. Removing the Black Numenorean stacks from 13 Mordor templates made their live id sets identical to the stale `clan_heraldry/mordor.json`, so for those 13 the guard went blind; only the two house templates still tripped it. The spec's counts predate the 2026-08-14 retarget (max 2 to 4 against a live 23 to 44), so a future `--apply` after someone "fixed" the spec for the two houses would have silently reverted 13 templates' ceilings. | Guard keyed on a symptom that the change removed | CONFIRMED, fixed: the guard also refuses when the spec's `max_value` sum is below the live template's. Dry-running every spec now refuses 19 of 21 (`bandits` and `khand` are current), which is the true state the id check had been hiding since 2026-08-17. `tools/tests/test_generate_clan_heraldry.py` pins both halves (the tool had no test). | The guard was written on 2026-08-17 against the failure in front of it (the spec lacked 13 ids) and named that failure, not the property it stood for (the spec is behind the live file). A deletion that removes the ids the guard keyed on is exactly the change that cannot trip it. Nothing in the changeset touched the tool, so no per-file agent had reason to open it; the data-flow agent found it by asking what else reads `taom_partyTemplates.xml` and running the tool dry. | Lesson below: a guard that protects a file is keyed on the invariant, not on the instance that motivated it, and when a change deletes data, every guard that keyed on that data is re-run. |
| 2 | LOW | The `EnableTroopWeight` hint line I rewrote kept its pre-existing em dash. | Prose tell | CONFIRMED, fixed (colon). | I edited the tail of the string and read the head as "existing prose". Exemption 4 of the dash rule covers lines you are not rewriting; I was rewriting this one. | None new; the linter cannot see C# strings, so the standards agent's grep is the gate. |
| 3 | LOW | `docs/reviews/lessons/gamemodels-services.md:420` still quoted the 2026-08-14 weight count (87 rows, 75 at 2.0, 10 at 3.0). | Stale count | CONFIRMED, fixed with a dated re-count beside the original. | The line is dated and `balance-levers.md` already called it stale, so I classed it historical. The user asked for stale documentation to be updated, which overrides that reading. | None new; the "grep for the OLD LITERAL" lesson (build-tooling-workflow) already covers it and was followed for the feature docs, not the lessons file. |
| 4 | LOW | `wire_black_numenorean_troops.py`'s `WEIGHTS` table reads as a source of truth for the 2.0/3.0 band, but `do_weights` only adds an id the file lacks and never corrects an existing row. | Docstring overclaim | CONFIRMED, fixed with a comment naming `TroopWeightLevelBandTests` as the gate. | The values were changed so the table would state the truth; the comment did not say the table cannot enforce it. | None new. |
| 5 | LOW | The same script's `read`/`write` pair (`encoding="utf-8"` + `newline=""` on both sides) is byte-faithful but is neither sanctioned idiom verbatim, so a cleanup could turn it into the forbidden mixed shape. | I/O convention drift risk | CONFIRMED, fixed with a comment naming why both flags stay. | Pre-existing code; the agent verified it round-trips a BOM and CRLF before flagging the risk. | None new. |

Two notes that were not findings: the compatibility and efficiency agents both reported that
`Main/Features/TaomSettings.cs` carried a second hunk (`SmartCavalryMaxLineUpSeconds`) that is not
this changeset's. It belongs to a concurrent session and is staged around, not with, this work.

## Codex pass (review 105, gpt-6-astra at ultra, 183k tokens)

Ran after the fixes above. No P1 or P2 in the changeset. Nine Known Suspects answered from the
installed DLLs and by executing the arithmetic (S1 recomputed the per-band max shares of all 15
templates and found no survivor band moved more than 0.5 points; S4 diffed every troop level
against HEAD and the index; S9 found `taom_spider_creature` in `characters/spider_creature.xml` at
level 20, outside the test's `troops_*.xml` glob, which is why the test names it as a package).

| # | Sev | Finding | Verdict | Why missed | Preventive action |
|---|---|---|---|---|---|
| 6 | P3 | The new `Level 11 (2.0)` comment above `mirkwood_recruit`; the troop is level 36. | CONFIRMED, fixed. | I wrote the band comment from the first explore agent's "L11 (1)" bucket without checking which troop it was (it was `orc_warg_scout`). | The "grep for the OLD LITERAL" lesson has a sibling: a NEW literal is checked against the data before it is written, not after. Codex's section 2 table is the check. |
| 7 | P3 | `do_weights` still emits `the 2.0 elite band` as the comment above a row it would restore at 3.0. | CONFIRMED, fixed; Codex reproduced it by mutating the file in memory. | The generated string was not in the diff; I changed the table and not the emitter that describes it. | When a table's values change, grep the module for the prose that describes the table. |
| 8 | P3 | CHANGELOG said the culture default carried `initiate 0/2`; HEAD had `0/1` there and `0/2` only in the clan templates. | CONFIRMED, fixed in the CHANGELOG; the issue body carries the same sentence and is corrected in the close comment. | Written from the explore report's summary of the clan block, generalised to the default. | Quote a count per template class, or quote the removed-line total only. |
| 9 | P3 | The BN doc's "was 1.91" for the pre-#585 house ratio; the same midpoint with the old weights is 260.5 / 135 = 1.93. | CONFIRMED, fixed in the doc and the CHANGELOG. The 1.91 predates the 260 retarget and was never recomputed. | I recomputed the NEW ratio and copied the OLD one from the doc. | Recompute both sides of a before/after from the same script. |
| 10 | P2 PRE-EXISTING | `SubtractResultFramePenalty` promises an exact integer-slot subtraction but feeds `(B - p/s) * s` through float and the engine's `(int)` cast: Gondor's 0.025 feat turned an intended 70 into 69.99999 (read 69) and the floor of 1 into 0.99999 (read 0). Larger weights reach the clamped case with fewer troops. | CONFIRMED on the installed struct; FIXED in this changeset. The method probes the subtraction on a copy and lifts an undershoot by 0.001 in the result frame; five new `ResultFramePenaltyTests` cases assert the truncated cap, one asserts a fractional base keeps its fraction. | `ResultFramePenaltyTests` asserted `ResultNumber` with a 0.01 tolerance in the float frame, and the consumer is `(int)ResultNumber`. A tolerance assertion is blind to a truncation boundary by construction. Shipped 2026-07-17 with the result-frame fix and reviewed twice since. | Lesson in `lessons/testing-qa.md`: assert at the consumer's frame. When the engine reads a cast, the test reads the same cast, on the exact values a real feat produces. |

Codex also disputed two things this session had stated: that the base limit is bounded by
vanilla's 40 to 203 for every settings combination (it is not, AI scaling runs before the weight
penalty), and that `taom_spider_creature` resolves to no troop (it resolves to a level-20
`NPCCharacter` outside the troop files). Both are recorded here rather than in the docs, since
neither changes a shipped value.

## Root-cause pattern

Two. Finding 1 is a guard written against the instance in front of it rather than the property it
protects. The 2026-08-17 guard's own docstring states the property ("a spec that has fallen behind
the live file silently deletes whatever the live file gained since") and then checks one of the two
ways a spec falls behind. The other way, a retarget, had already happened to every spec at the time
the guard was written, and the guard could not see it because the id mismatch fired first on every
Mordor clan. A guard that fires on every input for the wrong reason looks complete.

## Why each agent missed finding 1

- Standards, efficiency, completeness: the tool was not in the changeset and their prompts scope to
  changed files.
- Compatibility: engine semantics only.
- Tooling: scoped by its prompt to `wire_black_numenorean_troops.py` and the two data files; it did
  not enumerate the other writers of `taom_partyTemplates.xml`.
- Data flow: found it, by enumerating every tool that writes the file the changeset edited and
  running the relevant one dry against the new data. That enumeration is the step the other five
  prompts do not ask for.

Finding 10 is a test asserting in a frame the consumer never reads: the float `ResultNumber`
within 0.01, where the engine reads `(int)ResultNumber`. Every case the 2026-07-17 fix was
written for passed with room to spare, and the exact-integer promise in the method's own doc
comment was never asserted as an integer.

## Feedback memories to codify

None as harness memory. The lessons go to `docs/reviews/lessons/build-tooling-workflow.md`
(finding 1) and `docs/reviews/lessons/testing-qa.md` (finding 10).
