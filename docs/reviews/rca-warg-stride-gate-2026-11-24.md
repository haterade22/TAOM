# RCA: the review of the warg in the gait stride gate (#1565 item 2, 2026-11-24)

`/deep-review` at tier 2 (a data gate, its tests and docs; no live data, no C#, no XML): a Codex pre-review at
`xhigh` (prompt `docs/reviews/codex-prereview-warg-stride-gate-2026-11-24.prompt.md`, raw log
`docs/reviews/raw/codex-prereview-warg-stride-gate-2026-11-24.md`) and the Data flow and Tooling lenses in one
wave. Codex rated one finding P1, so this file exists and a second wave ran on the corrected code (its result is
at the end). Every finding was read in the code or run before it was fixed. The P1 repeats a cause that F3 of
[rca-spider-locomotion-2026-11-22.md](rca-spider-locomotion-2026-11-22.md) recorded for the same tool: a way out
of the check that is wider than its reason.

## Findings

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| F1 | HIGH | The `known` list excused every finding on a listed clip. A listed side step could gain a paw that never plants, lose its travel or get a master with another stride, and the gate exited 0 | Codex (P1), Data flow lens (which showed it on the install with `--max-slip 0.5`) | The author tested that a listed clip which starts to pass fails, and no listed clip that fails in a second way | An entry states the failure as numbers, (metres claimed, metres covered, why). `known_verdict` gives KNOWN for that record on those feet only; the slip and the stance are judged against the ground the feet cover. `KnownFailureTests` |
| F2 | MED | A gait whose animation is in two master files was measured on the first file in the list, without a word | Codex (P2), both lenses | `master_by_guid` returned the first match, and the record files already had a rule for two files with one clip name that the masters did not get | `masters_by_guid`; two owners are a finding that names the files. `test_an_animation_that_two_master_files_hold_is_a_finding` |
| F3 | MED | A listed gait that no named set bound, or that was not installed, was a note, and the run said "all gait clips cover" | Codex (P2), both lenses | The check was `known` minus `gaits`, and both side steps are in `gaits` by name | A listed clip must be bound by a named set and measured. A `pending` clip that no named set binds is a finding too, as is a clip in both lists |
| F4 | MED | Any value of `cooked` other than `"stamp"` chose the date rule without a line of output | Codex (P2), Tooling lens | One `==` chose the rule | `COOK_RULES`; another value is a finding. `cook_state` is the one place that chooses, and the check of clips on the rig's package uses it too |
| F5 | LOW | The check of the standing pose looked for a master in the rig's package and the clip folder only, where the gaits were looked for under the whole creature folder | Data flow lens; a new test of the author then failed on it | Two lists built in two functions | `report` passes its list to `stand_findings` |
| F6 | LOW | The stamp rule does not see a record edited by hand without a new checksum, and the tool said only that it does not see a changed master | Both lenses, Codex | The author listed one of the two things the date rule saw | Stated in `stamp_state`, the ledger and the lesson. Not closed: see below |
| F7 | LOW | The test of the slip argument used a fixture with no slip; no test gave an explicit 0.0, measured along another axis, found a master outside the clip folder or cut a record short | Codex (P3), Tooling lens by mutation | The author's 13 mutations were the ones the author thought of | Tests for each |
| F8 | LOW | `InstalledWargTests` ran on an install that has the warg's animations and not its rig, and would fail there | Codex (P3) | The guard named one of the gate's three inputs | It skips unless all three are there |
| F9 | LOW | The ledger said the stride gate "reads no `Duration`, no twin", and gave "18:08" for every entry | Codex (P3), Data flow lens | Written from memory of the tool, not from a read of it | Both sentences corrected against the tool and a file listing |
| F10 | MED | The gate, the ledger and #1591 said the two side steps "claim 1.60 m" that their paws do not cover, called the node's use of that value "not read", and proposed to set the travel to the measured stride. The node takes a loop time from travel / (`Duration` x its sideways input), and the right record also sets the rate of a turn in place | Engine lens (second wave), on v1.5.7; the author then read 0x749C20 and the phase lines of 0x74E9C0 | The formula was in the tool's docstring since 2026-11-19; the author did not connect it to the row the measure was pointed at | No record was changed, which the read supports. The wording is corrected in the tool, the ledger, the feature doc and the README; #1591 is rewritten; a lesson in `animation-skeleton.md` |
| F11 | MED | "No other gate sees it" (the `warg-gaits` note and the ledger) became false with the new gate, which fails on a reverted travel | Completeness lens (second wave) | The sentence was true when written and the change did not search for claims about the set of gates | Both reworded. Also: `warg-combat.md`, `doc-lookup.md` and `release-process.md` name the new gate |
| F12 | LOW | The elephant's and the spider's rows had no glob for the cache entries their checks read; the first version of this file parked that under "Not applied" with only an issue comment | Data flow lens (first wave), Standards lens (second wave) | "Older than this change" was taken for a reason not to fix a two-word gap in a tuple the change already edits | `CACHE_GLOBS` in all three creatures' rows; `test_a_cache_entry_selects_every_gate_that_reads_the_cook` |
| F13 | LOW | The docstring counted "four things" over five headings and said "to two places"; `stand_findings` kept a default master list that only tests used; the rule clause had two of the lesson's three tests | Standards lens (second wave) | Each was written before the fix it describes was final | Corrected; `masters` is a required argument |

## The disagreement that a run settled

The Tooling lens proposed to close F6 with `set_clip_balance_name.checksum_ok`. The Data flow lens said that
would not work. A run on the install decided it: `checksum_ok` is false for 30 of the 41 records of `as_warg`,
every one the Kit wrote and no tool here rewrote, and true for the 11 that `repoint_anim_clip.py` wrote. The gate
cannot recompute a checksum the Kit wrote, so F6 is a stated limit. An edited travel still fails the stride
measure, which reads the record.

## Root-cause pattern

F1, F3 and F4 are one fault: a way out of the check that was drawn by membership (the clip is in the list, the
name is in `gaits`, the word is not `"stamp"`) and not by the fact it stands for. The `pending` list of the
spider had the same fault on 2026-11-22 (a pace action on a pending clip escaped the stride check), and the rule
written then named only the case found then.

## Why each reviewer missed what another found

- **The author's mutations** were all killed, and all were of code the author had a test for.
- **The Tooling lens** found F1's neighbours by mutation (F3, F4) and rated the stamp's blind spot as fixable
  without running the fix on the install.
- **The Data flow lens** traced F1 on the installed data and did not try a clip in two lists.
- **Codex** could not run the measure (no `numpy`, `lz4` or `xxhash` in its sandbox) and built its
  counterexamples from the functions in memory; it could not compare the elephant and spider reports.
- **All three** of the first wave took the author's reading of the side steps as given. Codex had no native
  tools, and the two lenses were not asked an engine question. F10 needed the lens that reads the DLL.

## Not applied

- `tools/bind_elephant_clips.py` and `bind_spider_clips.py` still look for `<clip>_geo.tpac` (through
  `clip_problems`). Every clip they bind has a master of its own name, and neither tool is part of this change.
- The gate does not read the "none" rows or the backward walk's strafe rows of a usage set. For the warg the plan
  check holds the twins' `Duration`.
- A cache for the master lookup. The gate now reads every master package once per gait, to find two owners of one
  animation, and the elephant's run takes about 1.5 s where trunk's takes 1.0 s (Efficiency lens, four runs
  each). An index built once would be faster than trunk, at the cost of parsing packages the gate never needed
  to parse and of three changed signatures. Rejected by the simplicity criterion for a gate that runs by hand.
- The stride judgement of a standing strafe for the elephant and the spider, which F10 puts in question for
  every creature (their records give travel / `Duration` of 1.18 and 1.50). It is in #1591.
- Two owners of one animation as a finding for a bound clip that is not a gait (a pose, an idle): the key range
  and the standing pose still take the first owner. No file of the install has two owners, so this is a guard
  for a case nobody has seen, and by the simplicity criterion it is the maintainer's decision (Design lens).

## Follow-ups

- FOLLOW-UP #1591: the warg's two side-step records, how the gate judges a standing strafe, the dire wolf's
  backward walk and the wolf in the stride gate. The first two points under "Not applied" are in a comment on
  that issue.

Lessons: [build-tooling-workflow.md](lessons/build-tooling-workflow.md) (two entries of 2026-11-24 for #1565
item 2) and [animation-skeleton.md](lessons/animation-skeleton.md) (the standing side step).

## The second wave

Four lenses on the corrected code (Standards, Engine compatibility, Efficiency, Completeness), then the Design
lens on the result. None found a critical or high defect. F10 to F13 above are theirs. The Design lens kept the
design and the `known` list as the home of the two side steps: a third list would be the same mechanism under
another name, and judging a standing strafe by the node's relation cannot be built before the input's unit is
read. Applied from it: the summary line no longer calls the two records a failure, one guard in
`known_verdict` where there were two, one count of the listed clips, and a path in `report` that no input
reached. The Engine lens also verified on v1.5.7 what
the tool states about a moving pace's strafe record and about where the node reads travel and limits (the clip
object's usage, cloned from the record's metadata), and left three things unverified: the unit and the writer of
the node's sideways and turn inputs, the cache entry's name on v1.5.7, and what the client does with an entry
whose stamp does not match.
