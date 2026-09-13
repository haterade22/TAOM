# RCA: the ranged range ladders deep review (#582, 2026-09-12)

**Top line.** Six review agents (standards, engine-claim verification, efficiency, completeness,
cross-system data flow, tooling correctness) reviewed the tooling that put 227 ranged troops on a
kingdom-rank by tier-band grid. No HIGH. Five findings were confirmed by re-reading the code or the
decompile and fixed in the same session: the validator's new launcher index parsed every item XML
under eight module roots with ElementTree and cost each `validate_moduledata.py` run 1.2 s (now
0.18 s, a byte pre-filter parses only files that mention a bow or crossbow); a non-numeric
`band_base` crashed `validate_spec` with a traceback instead of a report; a `files` token naming
no `troops_*.xml` produced a line with no troops and read as clean; two lines whose prefixes overlap
were decided by list order with no check; and `MAX_TIER = 10` was commented as an engine constant
when it is TAOM's `TaomCharacterStatsModel` override over a vanilla formula that caps at 6. One
doc-only coupling was recorded (the donor `culture=` on a clone is inert only because
`is_merchandise="false"` gates the tournament prize pool first). The data-flow agent re-derived
both ladder rules from the rewritten files without the library and got 0 inversions, matching the
tool and the gate.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `build_launchers` re-parsed every item XML under the validator's eight roots with ElementTree: +0.98 s on a 5.2 s run, paid by the commit hook on every ModuleData commit. The sibling `build_item_armour` walks the same roots with a regex over `_read_stripped` in 0.47 s. | Tooling cost | The library was written for the two CLI tools, where 1 s is nothing, and reused in the validator without measuring. The efficiency agent's rule set is C#-shaped; only the explicit instruction to time the validator produced the number. | Byte pre-filter (`weapon_class="Bow|Crossbow"`) before any parse: 0.18 s over the eight roots. Rule of thumb recorded in the lessons file: a registry builder added to `build_registries` is measured, because the hook pays for it. |
| 2 | LOW | `validate_spec` wrapped the `rank_step` int parse in try/except and the `band_base` parse three lines above it in nothing, so a non-numeric base raised a traceback through all three callers. | Input validation | The two checks were written minutes apart in different shapes. A test covered the `rank_step` shape only. | Both parsed the same way; `test_validate_spec_reports_a_non_numeric_base_instead_of_raising`. |
| 3 | LOW | A `files` token with no matching troop file (a `rhun` for `rhun_new` typo) was a line with zero troops, and nothing said so: the report showed no rows for it and the gate had nothing to compare. | Gate that quietly checks nothing | `validate_spec` was a pure function of the spec dict and never saw the filesystem; the "unassigned troop" check covers the opposite direction only. Same class as the `SETTLEMENT_ECONOMY_FLOOR` culture-with-no-settlement finding. | `validate_spec(..., cultures=troop_file_cultures(moduledata))` in both tools and the gate; a claimed token with no file is a finding; `test_validate_spec_checks_files_tokens_against_the_troop_files_present`. |
| 4 | LOW | Two lines with overlapping prefixes (`man_sp_` and `man_sp_ranger_`) were resolved by list order alone, documented in a docstring and checked nowhere. | Spec ambiguity | The shipped spec has no overlap, so no run showed it. | `validate_spec` reports overlapping prefixes across lines; `test_validate_spec_reports_prefixes_that_overlap_across_lines`. |
| 5 | LOW (doc) | `MAX_TIER = 10  # MaxCharacterTier` and the feature doc read as if the cap were an engine constant. Vanilla `DefaultCharacterStatsModel.MaxCharacterTier` is 6; TAOM registers `TaomCharacterStatsModel` with 10. The arithmetic is right for the running game; the attribution sends a reader checking the dump to the wrong conclusion. | Attribution | The value was copied from `taom_schema._MAX_CHARACTER_TIER = 10`, which carries the same comment, and the decompile citation stopped at the formula. | Comment and doc name the override and the vanilla value. |
| 6 | Doc note | Every clone keeps its donor's `culture=`; the vanilla arbalest clones carry `Culture.vlandia`. `DefaultTournamentModel.GetRegularRewardItems` buckets prizes by `item.Culture == town.Culture`, and only `is_merchandise="false"` keeps the clones out of that pool. Flipping the flag later would sort half the clones into the wrong town's prizes. | Hidden coupling | The clone is verbatim by design; the reviewer opened the consumer of the flag and found the second gate behind it. | Recorded in the feature doc's item section; no code change. |

## Root-cause pattern: a spec validator that never looks past the spec

Findings 3 and 4 share one shape. `validate_spec` proved the spec consistent with itself and, given
an index, with the Armory. It never asked whether the spec was consistent with the directory it
claims to cover, so the one typo a human will actually make (a culture token) produced the one
failure the report cannot show (an empty line). The settlement-economy gate learned this on
2026-08-14 (a culture in the spec that owns no settlement is a finding) and the lesson did not
carry across because it was recorded as a fact about settlements. The generalisation: **a spec that
names external things is validated against those things, and a name that matches nothing is a
finding, not silence.**

Finding 1 is a different pattern, cheap to state: a library written for one caller's budget was
reused by a caller with a different one, and nobody timed the second caller until the review asked.

## Why each agent missed these

- **Standards** (haiku): its rules are I/O conventions, dashes, budgets; all passed. Findings 1 to 5
  are behaviour, not convention.
- **Engine claims** (sonnet): found finding 5, the only one in its scope. It verified nine claims
  line by line against the installed DLLs and caught the tenth's attribution by reading the model
  that owns the value rather than the formula alone.
- **Efficiency** (haiku): found finding 1 because the prompt asked it to time the validator with
  and without the new registry. The generic C# hot-path rules would not have fired on an offline
  script.
- **Completeness** (haiku): scope is tests, docs, issue, CHANGELOG; all present. It cannot see a
  missing check.
- **Data flow** (sonnet): traced eight flows and found finding 6 by opening the consumer of
  `is_merchandise` and the consumer behind it. It did not find 3 or 4 because both are about a
  spec the shipped data does not exercise.
- **Tooling correctness** (sonnet): found 2, 3 and 4 by asking what `validate_spec` does with inputs
  the shipped spec never produces. That agent was launched because the changeset writes files
  outside the repo; the skill's rule that file-writing Python gets its own agent is what put those
  questions on the table.

## Lessons codified

- `docs/reviews/lessons/build-tooling-workflow.md`: a spec that names external things is checked
  against them; a registry builder added to `build_registries` is timed.
- `docs/reviews/lessons/data-content-cultures.md` (written before the review): reach is
  `missile_speed`, skill is not range, a ladder needs a gate.

## Not findings

- `write_changes` (imported from `fix_upgrade_armour_regressions`) detects CRLF by presence rather
  than majority. The tooling agent flagged it as inherited risk; all 16 troop files are uniformly
  CRLF (`git diff --stat -w` equals `git diff --stat`). Pre-existing, outside this change, left
  alone.
- The efficiency agent's O(n^2) sweep: 13 ms on live data, bounded by the roughly 230 ranged
  troops. Not worth an index.
