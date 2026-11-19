# Deep review: plan 060, UPGRADE_SKILL_REGRESSION judges templated upgrade edges on the SkillSet (#626)

```
DEEP REVIEW REPORT
===================
Feature: UPGRADE_SKILL_REGRESSION judges an upgrade edge with a skill_template side on the SkillSet
         with the inline rows laid over it, and the two skill writers resolve and refuse the same
         way (#626), plan 060
Date: 2026-11-19

Scope:   scripts (tools/taom_schema.py, sync_lord_inline_skills.py, ranged_ladder.py,
         rebalance_ranged_ladders.py, rebalance_troops.py, validate_moduledata.py), their tests, and
         four docs. Diff af0f5f330..8dc68388b on improve/060-upgrade-skill-regression-templated-edges.
Blast radius: NOT IN SCOPE for graphify (Python tools only). Callers checked with git grep:
         load_skill_sets has five callers (the gate, both writers, the sync CLI and
         validate_moduledata.skill_template_mismatch_issues); the new keyword defaults to the old
         reading, so the last two are unchanged.
Waves:   one wave: Agent 5 (Data flow), Tooling correctness. Tier 2 tooling.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: SKIPPED BY TIER
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     FAIL, 0 gaps, 3 inconsistencies (trace 3 confirmed and fixed as F1, trace 5 NEEDS MIKE,
               trace 9 false positive as F3)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE (no XML in the diff)
TOOLING:       FAIL, 4 findings (F1 MEDIUM confirmed and fixed, F2 LOW confirmed and fixed, F3 LOW
               false positive, F4 LOW confirmed and fixed)
```

## Details

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 2 rule). Not run.

### Agent 2, Engine compatibility

SKIPPED BY TIER. Not run.

### Agent 3, Efficiency

SKIPPED BY TIER (orchestrator note: lenses 3, 4 and 6 skipped by the tier 2 rule). Not run.

### Agent 4, Completeness

SKIPPED BY TIER. Not run.

### Agent 5, Data flow

Twelve flows traced; nine connected. The lead re-read each inconsistency:

- Trace 3 (LOW in the lens, MEDIUM as Tooling F1): the empty-SkillSet refusal could not fire on the
  real tree. Same defect as F1 below. CONFIRMED, fixed.
- Trace 5 (LOW, hypothetical): the ladder writer now reads a templated ladder troop's Bow or
  Crossbow through its SkillSet, while `ranged_ladder.troop_values` (`tools/ranged_ladder.py:786-799`,
  `int(troop.skills.get(cls, 0))`), which `inversions()` and `RANGED_LADDER_INVERSION` use, reads
  the inline rows only. Before this change the writer wrote the cell inline, so both agreed; now a
  templated troop whose SkillSet holds its cell gets no edit while the gate reads 0. The lead
  confirmed the reach is zero today: `git grep -c skill_template -- Main/_Module/ModuleData/troops`
  prints nothing. The fix site is pre-existing code, and the two possible fixes change behaviour in
  opposite directions (thread the SkillSets into `troop_values` and the validator's ladder pass, or
  let the writer mirror an inline row equal to the SkillSet value, which `SKILL_TEMPLATE_MISMATCH`
  allows). NEEDS MIKE; not applied.
- Trace 9 (LOW): the `print_report` totals. Same item as F3 below. FALSE POSITIVE as a regression.
- Trace 11 (FOLLOW-UP in the lens): the `UPGRADE_SKILL_REGRESSION` row still said "Only militia to
  militia is exempt", but `_RESPECIALIZATION_EXEMPT_EDGES` (`tools/taom_schema.py:2143-2148`) exempts
  `sagarun_crossbowman` to `sagarun_naffatun` on Bow and Crossbow. The row is a line this diff
  rewrote, so the lead corrected it (docs only, see APPLIED).
- Traces 8 and 12: `analyze_troop_balance.py` calling `load_roster` without SkillSets is the
  orchestrator's known follow-up; the C# mirror skipping templated edges is documented and intended.

### Agent 6, Design and elegance

SKIPPED BY TIER. Not run.

### Agent 7, XML and ModuleData integrity

NOT IN SCOPE (no XML in the diff). The shipped-data result was still checked (see Verification).

### Tooling correctness

- **F1, MEDIUM, CONFIRMED, fixed.** The "no SkillSet found" refusal in `Validator._skill_sets`,
  `rebalance_troops.main` and `rebalance_ranged_ladders.main` tested the merged dict for emptiness.
  `sync_lord_inline_skills.skill_set_files` always adds the repo's own `*skill_sets*.xml`
  (`taom_lord_skill_sets.xml` and `taom_wanderer_skill_sets.xml`), so on the real tree the dict is
  never empty and the refusal could not fire. With an install root whose vanilla modules hold no
  SkillSet file, every vanilla template (the `infantry_heavyinfantry_level*_template_skills` ids the
  `npcs_*` files name) resolved through `sets.get(..., {})` to an empty set, and a real drop on a
  templated edge passed clean. The lead reproduced it: the new test
  `test_an_install_with_no_vanilla_skill_sets_gives_one_error_beside_the_repo_ones` returned 0
  issues against 8dc68388b (`AssertionError: 1 != 0 : []`). Fix: `load_skill_sets(...,
  require_vanilla=True)` raises `ValueError` when `vanilla_skill_set_files(game)` is empty
  (`tools/sync_lord_inline_skills.py:78-84`, `:273-283`); the gate turns it into its one
  `(skill sets)` ERROR (`tools/taom_schema.py:2163-2170`) and both writers refuse
  (`tools/rebalance_ranged_ladders.py:692-696`, `tools/rebalance_troops.py:1510-1515`). The default
  stays `False`, so `validate_moduledata.skill_template_mismatch_issues` and the sync CLI read as
  before.
- **F2, LOW, CONFIRMED, fixed.** Neither main's refusal was tested. New tests:
  `RebalanceToolTests.test_an_install_with_no_vanilla_skillset_is_refused_though_the_repo_has_one`
  (dry run returns 0 against 8dc68388b, `AssertionError: 0 != 2`) and
  `ClampOnlyRunTests.test_an_install_with_no_vanilla_skillset_stops_before_anything_is_written`
  (exit 0 against the head's loader, `AssertionError: 0 != 1`). The lead's first draft of the ladder
  test used `--apply` and passed against the old code for an unrelated reason (missing ladder items
  also return 2); it was rewritten as a dry run that must also leave no report.
- **F3, LOW, FALSE POSITIVE.** The claim that "Changed + Unchanged + Skipped no longer sums to Total
  troops" and that templated records are "listed nowhere" does not hold. `print_report`
  (`tools/rebalance_troops.py:1382-1396`) already left the `EXTERNAL SOURCE (read-only, outside
  troops/)` records (`:1298`, every villager from the 22 `npcs_*` files) out of its three buckets
  before this diff, so the summary never summed to the total. A `SKILLSET` record gets `final`
  from the clamp loop (`:839-841`) like every record, so it is printed in its level table
  (`:1406`). The summary buckets are pre-existing; listed under FOLLOW-UP.
- **F4, LOW, CONFIRMED, fixed.** A SkillSet file that is not UTF-8 raised `UnicodeDecodeError` out
  of `rebalance_ranged_ladders.main`, which exits 1, the tool's "edits still pending" code (`:750`).
  `UnicodeDecodeError` is a `ValueError`, so the F1 handler returns 2 with "Nothing was written".
  Test `test_an_unreadable_skillset_file_is_a_refusal_not_pending_edits` failed first with the
  traceback and passes now.
- Checked and clean (lens, re-read by the lead): the `_skill_sets` exception path and memo, no read
  on a tree with no templated edge, the template branch placed before `SKIP_TROOP_IDS`
  (`tools/rebalance_troops.py:1162-1173`), no stale `templated` reader.

## Action items

1. Done: a SkillSet load that finds no vanilla SkillSet file is one ERROR in the gate and a refusal
   in both writers, even though TAOM's own SkillSet files load.
2. Done: an unreadable SkillSet file makes `rebalance_ranged_ladders.py` exit 2, not 1.
3. Mike: choose how `RANGED_LADDER_INVERSION` and the ladder writer agree on a templated ladder
   troop (trace 5). No troop carries a template today.

## Improvements (Step 4)

- APPLIED: `tools/tests/test_troop_skill_curve.py:369-385`, `_FixtureRun` now builds its own
  install with one vanilla SkillSet file and passes its `Modules` folder. Before, `--game-modules`
  was the fixture folder, so the SkillSets were read from the system temp folder plus the real
  repo's ModuleData; 16 tests failed once the refusal could fire. Test-only.
- APPLIED: `docs/reference/moduledata-validator-codes.md`, the `UPGRADE_SKILL_REGRESSION` row names
  the respecialisation exemption beside militia to militia (Agent 5 trace 11). Docs only.
- APPLIED: the F1 wording in `docs/features/moduledata-validation.md`, `troop-skill-balance.md`
  (test count 34 to 35, read from `Ran 35 tests`) and `ranged-ladders.md`.
- NOT APPLIED: trace 5 (writer and `RANGED_LADDER_INVERSION` disagree on a templated ladder troop).
  Behaviour-changing either way and in pre-existing code; NEEDS MIKE.
- NOT APPLIED: F3's summary bucket. Disproved as a regression (see F3).
- FOLLOW-UP: `validate_moduledata.skill_template_mismatch_issues` (`tools/validate_moduledata.py:538-543`)
  has the same `if not sets` blind spot as F1 (whether its unresolved-template issue still catches a
  root with no vanilla SkillSet is UNVERIFIED). no issue: an /improve lead files none; the
  orchestrator decides at wrap-up.
- FOLLOW-UP: `analyze_troop_balance.py:223` calls `load_roster` without SkillSets and fails on a
  future templated troop. no issue: the orchestrator recorded it as a known follow-up.
- FOLLOW-UP: `print_report`'s summary counts omit the `EXTERNAL SOURCE` and `SKILLSET` buckets. no
  issue: cosmetic, pre-existing.

## Verification

- Base totals in the worktree at 8dc68388b, before any edit: Python `Ran 10567 tests`, `OK
  (skipped=13)`; C# `Failed!  - Failed:     3, Passed: 43028, Skipped:     9, Total: 43040` (the three
  known language-file failures: `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles`).
- Final totals after every edit: Python `Ran 10572 tests`, `OK (skipped=13)` (the base plus the five
  new tests); C# `Failed!  - Failed:     3, Passed: 43028, Skipped:     9, Total: 43040`, the same three
  known failures (no C# changed).
- Focused run after the fix: `tools.tests.test_sync_lord_inline_skills`,
  `test_upgrade_skill_monotonicity`, `test_ranged_ladder`, `test_skill_template_mismatch`: `Ran 186
  tests`, `OK`; `test_troop_skill_curve` after the fixture change and before its new refusal test:
  `Ran 59 tests`, `OK`.
- `python -B tools/lint_docs.py --fail-on-drift`: exit 0, 0 dashes in new prose.
- Shipped data: `python -B tools/validate_moduledata.py --code UPGRADE_SKILL_REGRESSION --code
  UPGRADE_INDEX_EMPTY --code SKILL_TEMPLATE_MISMATCH` printed `PASS: no validation issues found.`,
  exit 0, after the fix.
- Differential sweep of the gate (`git show af0f5f330:tools/taom_schema.py` against the worktree's,
  `Validator._upgrade_skill_regressions`): 1,008 synthetic cases over a templated source, target,
  both or neither; SkillSet values 0, 40 and 60 on each side; inline rows over the template; a
  dangling template; militia pairs; and four install states (none, a vanilla SkillSet file, an empty
  Modules folder, an empty Modules folder beside a repo SkillSet file). Result: `old refused and new
  allows: 0`, `new refuses where old did not: 256` (188 before the F1 fix), and on the shipped data
  with the real install `old 0 issue(s), new 0 issue(s), identical: True`. The gate is a validator,
  not a shell hook, so the Bash and PowerShell tool-name axis does not apply.
- Not covered: no game run (tools only); `rebalance_troops.py --apply` and
  `rebalance_ranged_ladders.py` were not run on the real tree (they write by default).

## Codex review

Codex not run for this item (no paid dispatch was authorised for it).

| # | Codex finding | Codex severity | Lead severity | Agree? | Reason |
|---|---|---|---|---|---|
| - | Codex not run | - | - | - | - |

Codex lessons (pending): one missed category to add to `.ai/review-reference.md` "Lessons From
Prior Reviews" when Codex next reviews a loader guard: an emptiness check on a merged load is dead
when one source (here the repo's own files) always contributes; check the source that can be
missing.

VERDICT: READY FOR COMMIT
