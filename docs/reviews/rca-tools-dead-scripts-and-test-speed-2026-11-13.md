# RCA: plan 051, tools dead scripts and test speed (deep review, 2026-11-13)

## Top line

Plan 051 deletes three retired scripts, rewrites the game-dir paragraphs of `tools/README.md` around
two `git grep` commands, hoists the sibling-file parse of `translate_with_claude.py` out of its
`discover_entries` loop, and serves two docs-graph tests from one build. Two lenses ran (Data flow
and Tooling correctness). Neither found a behaviour defect: both proved the hoist gives the base
code's skip sets on all 276 shipped (language, source) pairs and the same `SystemExit` on every
malformed-file case. They found five LOW items, all in the change, all confirmed against the code
and all fixed here: a new test that compared the code with itself, no test through the hoisted call
site, README prose whose quantifiers no longer matched the list the new command prints, an
overstated "the run ends" line in a feature doc, and a redundant local import.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | `test_hoisted_siblings_give_the_per_call_skip_set_for_every_target` and `test_a_target_that_does_not_parse_is_not_a_sibling_error` (`tools/tests/test_translate_batching.py`) compared `skip_ids_for(..., siblings=sibling_string_ids(lang))` with `skip_ids_for(...)`. Since the hoist, `siblings=None` calls `sibling_string_ids` too (`translate_with_claude.py:375-376`), so both sides ran the same new code; a collector that kept the files but dropped their ids left both tests green. | Test oracle shared with the code under test | The plan framed the test as "hoisted equals per-call", which was a real equivalence before the refactor routed the per-call form through the new collector. Nobody re-asked which side still held the old code. | The tests now pin the skip sets computed with the 664f19c5b code (`EXPECTED`). Under the "drop every id" mutant three of five tests go red. Lesson in `lessons/testing-qa.md`. |
| F2 | LOW | No test went through `discover_entries`, the one caller the hoist changed (`translate_with_claude.py:264-267`). A mutant handing `skip_ids_for` empty siblings there kept all 420 tests in the 11 translator-importing modules green (Tooling lens). | Missing caller-level test | The new tests exercised `skip_ids_for` and `sibling_string_ids` directly; `test_translate_gamedir.py` calls `discover_entries` but asserts only the game-dir errors. | Two synthetic-tree tests call `discover_entries` with `english_source_files` and `TAOM_LANG_DIR` patched: a key whose row lives only in another file of the language is skipped, and a sibling that does not parse raises `SystemExit`. Both pass on the base code and fail under the mutant. Same lesson. |
| F3 | LOW | `tools/README.md` "Game install": "Every one falls back to `E:\Steam\...`" now ranged over the 50-file `git grep -l` list, which holds `weapon_xml/config.py` (no fallback, `config.py:109-111`). The three "inheritors" were presented as extra although each is in the 50; 54 of the 90 `_gamedir` importers never name the variable; six of the 90 import only `game_or_kit_running` or `ensure_exists`, so "lists the tools that do [resolve it]" was too broad. | Doc prose against a computed list | The count was replaced by a command, but the sentences around the old hand count kept their quantifiers and were not re-checked against the command's output. | The paragraphs now say the three are members of the 50, that most importers honour the variable without naming it, name the `weapon_xml/config.py` exception, and say six importers use only the helpers. Each claim re-checked by scan on 2026-11-13. Lesson in `lessons/build-tooling-workflow.md`. |
| F4 | LOW | `docs/features/gondor-armor-revamp.md:110` said the successor's run "ends" with the PASS line; two scope lines print after it (`validate_all_troop_refs.py:96-103`). | Doc against output | Written from the PASS line in the source, not from a run's last lines. | Reworded to "prints ... (two scope lines follow it)". One-off. |
| F5 | LOW | `FailureTextTests.run_main` kept `from unittest import mock` after the change added the same import at module level (`tools/tests/test_doc_graph_ratchet.py:17`). | Dead code | Ruff does not flag a shadowing local import. | Deleted. One-off. |

## Root-cause pattern

F1 and F2 share one habit: a behaviour-preserving refactor was proven by comparing the new path
with another path that the same refactor had rerouted through the new code. Proof of parity needs an
oracle that comes from the old code (a value computed at the base commit, or the base module loaded
from `git show`), and a test through every caller the refactor touched. The lenses did exactly this
in scratch (`eq_check.py`, `hoist_vs_base.py`); the shipped tests did not. This category has shipped
before in another shape: `lessons/testing-qa.md` "A wiring test that registers its own fake cannot
prove the feature registers the real one" (#1108) is the caller-level half of the same rule.

F3 is the second instance this month of prose drifting from a number it sits beside (the war ram
RCA, `rca-war-ram-cooldown-2026-11-11.md`, found the same for a tuning value). Here the number was
made correct and computable, and the error moved into the words around it.

## Why each agent missed these

Only two lenses ran (the change is Python tools and docs, with no C#, XML, hook or harness file). Both found
every item above, so no lens missed one. The misses were the executor's:

- **Executor, F1 and F2:** followed the plan's test list, which named "hoisted equals per-call" as
  the oracle. Standing rule 12 says a test oracle a plan supplies is a draft to verify; it was not
  re-derived after the per-call path changed.
- **Executor, F3 and F4:** reproduced the two counts (50 and 90 are right) but not the sentences
  that quantify over them.
- **Executor, F5:** ruff is clean on a redundant local import; nothing else checks it.

## Feedback memories to codify

None beyond the two lessons. The F1/F2 rule is a test-design rule for any refactor, and the F3 rule
belongs with the existing "a count names the command that prints it" convention (CLAUDE.md "Where
new knowledge goes").

## Follow-ups outside the change

Pre-existing items the lenses found outside the change (no issue: an `/improve` agent files none;
listed for the orchestrator to route):

- FOLLOW-UP (no issue: orchestrator routes it) `tools/README.md` row for `extract_wanderers.py` has
  no warning that it writes `ModuleData/taom_wanderer_equipment.xml` while the shipped file is under
  `equipmentsets/`; the deleted batch2 row was the only one that carried it.
- FOLLOW-UP (no issue: orchestrator routes it) `docs/modding/file-catalogue.md` `SubModule.xml`
  line citations past line 234 are 18 low (for example `:117` cites 1106, the row is 1124), and now
  disagree with `wanderers-and-named-companions.md:259`. Re-measure the whole table.
- FOLLOW-UP (no issue: orchestrator routes it) line citations into `tools/README.md`
  (`skill-sets.md:23, :161, :177`, `module-taom.md:343`) and into `translate_with_claude.py`
  (`localization-map.md:21`, `AccentStrippedTranslationTests.cs:16`) were already wrong; cite rows
  and functions by name.
- FOLLOW-UP (no issue: orchestrator routes it) migration templates `troops-and-parties.md:657, :724`
  and `equipment-rosters.md:659, :670` still tell the reader to run the deleted
  `validate_gondor_refs.py`; repoint them to `validate_all_troop_refs.py`.
- FOLLOW-UP (no issue: orchestrator routes it) `tools/package_quad_toolkit.py:33-38` leaves a
  partial ZIP at `--out` (replacing any good one) when a listed file is missing. Behaviour-changing
  fix: check every file before opening the ZIP, or write a `.partial` and `os.replace` it.
- FOLLOW-UP (no issue: orchestrator routes it) `tools/analyze_war_theaters.py:69-72` and
  `tools/audit_mbproj_registration.py:350` use `os.environ.get(VAR, default)`, the set-but-blank
  failure the README describes; route them through `_gamedir.game_dir`.
