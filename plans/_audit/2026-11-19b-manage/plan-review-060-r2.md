# Plan review 060, round 2 (cold)

Plan: `plans/060-upgrade-skill-regression-templated-edges.md` (936 lines), read against the worktree at
`db5680c8a`. Earlier review: `plan-review-060.md` (round 1).

Verdict: one blocking item (B1, a Done criterion the plan's own code makes impossible). The gate does
not loosen: every row the base refuses, the planned gate refuses too, and the shipped data gives the
same validator result before and after. Both round 1 blocking items are fixed.

## Round 1 items re-checked

- **B1 (clamp RED oracle): fixed.** Plan :687 to :691 now sets `child["status"] = "SKILLSET"`.
  Simulated RED: `test_the_clamp_refuses_to_raise_a_templated_character` fails `AssertionError: RuntimeError not raised`, as Step 8 (:713 to :715) says.
- **B2 (Step 7 grep): fixed.** Plan :674 to :677. At `db5680c8a` the pattern prints exactly the 8 lines the
  plan names (`git grep` run in the worktree). On the simulated tree it prints nothing and exits 1.
- N1 (import placement), N2 (kit planning note, :928 to :932 and :375 to :376), N3 (finding-line count,
  :784 to :787), N4 (message wording, :571 to :573), N5 (two-dot ids, :933 to :936) and the N6 line offsets
  (:120, :221, :589, :613) are all addressed. One new wording problem in the N1 fix: see N1 below.

## What I ran

All runs were outside the repository in `scratch/review-060-r2`: `sim_base` (tools/ at `db5680c8a`) and
`sim_new` (the plan's Steps 3, 5, 5.6, 7 and 9 applied literally, with the import inside `_skill_sets`'s
`try` and again inside the templated branch, as Steps 5.3 and 5.4 say), each with junctions to the
worktree's `Main`, `docs` and `.claude`. The worktree stayed clean apart from its existing untracked
`plans/` files.

- RED (base code plus the plan's tests): sync `Ran 21 tests`, `FAILED (errors=5)`, all
  `AttributeError: module 'sync_lord_inline_skills' has no attribute 'effective_skills'`. Gate module
  (Steps 4 and 8 together) `Ran 32 tests`, `FAILED (failures=5, errors=2)`: the three Step 4 tests
  `AssertionError: 1 != 0`; Step 8 two `RuntimeError not raised` and two `TypeError ... unexpected keyword
  argument 'skill_sets'`. Ladder `Ran 116 tests`, `FAILED (failures=1, errors=5)`: four `TypeError`, the
  loader test `AttributeError: 'RangedTroop' object has no attribute 'template'`, and
  `test_a_templated_edge_without_skillsets_is_refused` `LadderError not raised`. All as the plan states.
- GREEN: sync `Ran 21 tests` OK; gate module `Ran 32 tests` OK; ladder `Ran 116 tests` OK; Step 9's
  second command `Ran 280 tests` OK.
- The 30 test modules that import the changed tools, both sims: `Ran 1359 tests` each. `sim_new` has one
  failure, `test_rebalance_equipment.RespecializationExemptionMirrorTests.test_csharp_gate_agrees_with_the_python_pair`,
  which fails in `sim_base` too (the sim has no `TAOM.Tests` folder); every other failure in `sim_base` is
  one of the 18 RED tests.
- Shipped data: the Step 1 probe prints `indexed: 3118; templated: 1440; edges: 913; edges with a templated side: 0`
  on the worktree, `sim_base` and `sim_new`. `load_skill_sets` returns 744 from 5 files (Native,
  SandBoxCore, SandBox, `taom_lord_skill_sets.xml`, `taom_wanderer_skill_sets.xml`), and the two
  `infantry_heavyinfantry_level*` values match plan :113 to :116.
- Full `validate_moduledata.py` from each sim: exit 0 both; 1823 finding lines each; the Step 10
  finding-line diff is empty; `grep -c -E "^  (ERROR|WARNING|INFO) +UPGRADE_SKILL_REGRESSION"` gives 0 on both.
- Engine excerpt: `BasicCharacterObject.Deserialize` lines 337 to 364 of the v1.5.7 decompile
  (`pwsh tools/taom-src.ps1 path BasicCharacterObject`) match plan :50 to :75 verbatim.

## Safety gate simulation

The plan edits no hook. `check-moduledata-validation.sh` turns a PowerShell payload into the same POSIX
text as a Bash payload (`taom_hook_command posix`, :51) before any decision, and its verdict depends only
on the validator's exit code and output (:123 to :215): 1 denies, 3 (crash) and 2 allow. So a Bash and a
PowerShell `git commit` reach the same validator run, and the plan can loosen the gate only through a
changed finding or a new crash.

1. The hook's exact validator command line (all 32 `--code` flags, :123 to :135) from both sims against
   the worktree's ModuleData: `rc=0`, output `PASS: no validation issues found.`, byte-identical.
2. 24 rows through `Validator._upgrade_skill_regressions()` in both sims (`gate_rows.py`):

| Row | Base | Planned |
|---|---|---|
| R01 untemplated regression | ERROR `c` | ERROR `c`, same text |
| R02 untemplated clean | none | none |
| R03 templated source, vanilla level16 set, install | none | ERROR `c` (Athletics 80->60, ...) |
| R04 same, no install | none | none (skipped; the CLI warns) |
| R05 same, install with no SkillSets | none | one ERROR `(skill sets)` |
| R06 two templated edges, install with no SkillSets | none | exactly one ERROR `(skill sets)` |
| R07 single-quoted `skill_template='...'` | none | ERROR |
| R08 dangling template on the target | none | ERROR (inline rows alone, as the engine) |
| R09 templated target above its source | none | none |
| R10 templated target, inline row below source | none | ERROR (Polearm 90->80) |
| R11 militia pair, templated | none | none |
| R12 templated villager in `characters/` | none | ERROR on its target |
| R13 registry `skill_sets={}` | none | ERROR (inline rows alone) |
| R14 `skill_template` only inside a comment | ERROR | ERROR |
| R15 `my_skill_template=` attribute | ERROR | ERROR |
| R16 SkillSet file that is not UTF-8 | none | one ERROR `(skill sets)` (decode error) |
| R17 both sides templated, same set | none | none |
| R18 both sides templated, target row lower | none | ERROR (Bow 50->10) |
| R19 `skill_template = "..."` with spaces (regex miss) | ERROR (inline rows) | ERROR, same |
| R20 `SkillSet.` with an empty id | none | ERROR (inline rows alone) |
| R21 two-dot id | none | ERROR (inline rows alone; see plan :933) |
| R22 `sync_lord_inline_skills` import fails, install | none | one ERROR `(skill sets)` |
| R23 import fails, registry `skill_sets` given | none | CRASH `ModuleNotFoundError` (see N7) |
| R24 import fails, untemplated regression | ERROR | ERROR |

No row loosens. The only crash (R23) needs a registry `skill_sets`, which only tests pass; the CLI the
hook runs never sets it, and with the install the `_skill_sets` `try` turns an import failure into the
`(skill sets)` ERROR (R22), which `--code UPGRADE_SKILL_REGRESSION` keeps, so the hook denies.

UNVERIFIED: I did not run the hook script itself on a staged commit. That needs a git repository holding
a staged ModuleData change, and building one means copying the repository, which a read-only reviewer may
not do. The reasoning above (unchanged hook, same validator output) covers the gap.

## Blocking

- **B1 (plan :867 against :757 to :760): the Done criteria grep cannot pass.** The Done criterion
  `git grep -n -E '"templated"|\.templated\b|templated=|[[:space:]]templated:' -- tools` must print nothing,
  but Step 9.6's prescribed clamp code names a local list `templated` and tests it with `if templated:`,
  which `[[:space:]]templated:` matches. Simulated on the planned tree:
  `tools/rebalance_troops.py:828:    if templated:`. A literal executor fails the Done criteria and stops.
  Fix: rename the Step 9.6 local (for example `from_template = [...]` and `if from_template:`), or drop
  `[[:space:]]templated:` from the Done grep (Step 7's grep keeps it over the three ladder files, where it
  is the only form that finds the dataclass field `templated: bool`).

## Non-blocking

- **N1 (plan :534 to :537 against :562 to :567): two import instructions contradict each other.** Step 5.3
  puts the `sync_lord_inline_skills` import inside `_skill_sets`'s `try`; Step 5.4 says "Import `sl` in one
  place only: inside the templated branch". An executor who takes 5.4 literally drops the 5.3 import, and
  `_skill_sets` then raises `NameError` inside its own `try`, so every templated edge with the install gets
  the `(skill sets)` ERROR. No prescribed test would catch this (N2). Say "in `_upgrade_skill_regressions`,
  import `sl` in one place only" and keep 5.3 as written. Also, :566 to :567 claims a tree with no
  templated edge "never imports `sync_lord_inline_skills`". With the install that is false after Step 9.1:
  `Validator._troop_purpose` imports `rebalance_troops` (`tools/taom_schema.py:1730`), which then imports
  the sync module at the top, and `validate_moduledata.py:531` imports it anyway. The claim about reading
  SkillSet files holds. Keep the false half out of any code comment.
- **N2: the install-read path of `Validator._skill_sets` has no test.** Every Step 4 test injects
  `skill_sets` through the registry, so nothing tests the load through `self.game_modules.parent`, the
  empty-dict ERROR, the exception ERROR, or "once per run". Plan :299 says every implementation step
  follows a RED. My R05, R06, R16 and R22 rows show the plan's design works, but an executor who passes
  `self.game_modules` instead of its parent would ship a gate that silently misses every vanilla SkillSet:
  TAOM's two files keep the dict non-empty, so no ERROR fires. Suggested test: a temp install
  `<tmp>/Modules/Native/ModuleData/x_skill_sets.xml` holding one set, `Validator(..., game_modules=<tmp>/Modules)`,
  one templated edge judged on it; and an empty `<tmp>/Modules` with two templated edges giving exactly one
  `(skill sets)` ERROR.
- **N3 (plan :834, :871): "`git status --porcelain` is empty after the commit" assumes no other untracked
  files.** The run worktree at `db5680c8a` already holds untracked `plans/` files (`?? plans/059-...`,
  `?? plans/_audit/...`). If the executor's worktree does too, the check fails without fault. Compare it
  with the Step 1 output instead.
- **N4: the writers do not fail closed on an empty SkillSet load.** The validator turns an empty
  `load_skill_sets` result into an ERROR (Step 5.3), but `rebalance_troops.main` (Step 9.7) and
  `rebalance_ranged_ladders.main` (Step 7.5) pass `{}` on, so a templated character would read as its
  inline rows alone. Nothing is templated in their inputs today. A one-line refusal when the dict is empty
  would match the gate.
- **N5: excerpt line numbers.** `taom_schema.format_report` is at :4049, not :4058 (plan :779). Step 7.4
  says the docstring's last lines are :972 to :976 (plan :664) while Current state says :973 to :976
  (plan :198); the sentence to rewrite starts at the end of :972 ("An edge with"), so "last four lines" is
  one short.
- **N6 (Step 9.7, plan :767 to :769):** `print` of `len(skill_sets)` has no exact text. Not checked by
  any step, so harmless.
- **N7 (R23):** with registry `skill_sets` and a broken `sync_lord_inline_skills`, the branch import
  crashes outside any `try`. Only tests can reach it. No change needed.

## Checklist

1. Plan-only execution: yes, apart from B1 and the N1 wording. Every symbol, fixture and line I checked
   resolves at `db5680c8a`.
2. Exact expected results: every step ends in a command with a totals line, an exit code or an exact
   grep. Step 1's "or the difference is explained" is acceptable for a baseline record.
3. Excerpt mismatches: N5 only. Every quoted code body matches `db5680c8a`; the baseline (C# 43028
   passed, 3 failed, 9 skipped; Python `Ran 10549 tests`, `OK (skipped=13)`) matches the run's
   `scratch/base-summary.txt` and `scratch/base-py.txt`.
4. TDD: a RED step precedes each GREEN (Python only), except the `_skill_sets` install path (N2). Issue
   #626 named; ADR-002, 007 and 008 named as C# only; no protected or single-owner file; STOP conditions
   are specific (templated edge found, new finding, empty SkillSet load, engine contradiction). Planned-at
   `db5680c8a`; the drift-check list equals the 13 Scope files and the 13 staged paths. dotnet commands
   carry `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path or branch name. No CHANGELOG step.
5. No em or en dash and no local absolute path in the plan (checked by code point and pattern); no
   secret values.
