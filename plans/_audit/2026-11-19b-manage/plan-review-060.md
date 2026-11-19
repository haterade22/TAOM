# Plan review 060, round 1 (cold)

Plan: `plans/060-upgrade-skill-regression-templated-edges.md`, read against the worktree at `db5680c8a`.
No earlier review file exists for this plan, so there are no earlier blocking items to re-check.

Verdict: executable after two small fixes to verification text (B1, B2). No gate loosens. The planned
code, applied literally to a scratch copy, passes every test the plan prescribes, and the shipped data
gives the same result before and after.

## What I ran

All runs were outside the repository, in a scratch folder: two copies of `tools/*.py` and the
affected test modules (`sim_base` at `db5680c8a`, `sim_new` with the plan's Steps 3, 5, 7 and 9 applied
literally), with read-only links to the worktree's `Main` and `docs`. The worktree stayed clean
(`git status --porcelain` shows only the untracked `plans/` files).

- Baseline in the worktree: `test_sync_lord_inline_skills` `Ran 16 tests` OK, `test_upgrade_skill_monotonicity`
  `Ran 25 tests` OK, `test_ranged_ladder` `Ran 112 tests` OK; the seven modules Step 9 names together
  `Ran 317 tests` OK. The plan's 16, 25 and 112 are right.
- RED (base code plus the plan's tests): sync `Ran 21 tests`, `FAILED (errors=5)`, all
  `AttributeError: module 'sync_lord_inline_skills' has no attribute 'effective_skills'` (matches Step 2).
  Gate: the three Step 4 tests fail `AssertionError: 1 != 0` (matches Step 4). Ladder `Ran 116 tests`,
  `FAILED (failures=1, errors=5)`: four `TypeError ... unexpected keyword argument 'skill_sets'`, the
  loader test `AttributeError: 'RangedTroop' object has no attribute 'template'`, and
  `test_a_templated_edge_without_skillsets_is_refused` `LadderError not raised` (matches Step 6).
  Step 8: two `TypeError`, `test_a_templated_troop_without_skillsets_is_refused` `RuntimeError not raised`,
  and `test_the_clamp_refuses_to_raise_a_templated_character` **`KeyError: 'status'`** (see B1).
- GREEN (planned code plus tests): sync `Ran 21 tests` OK; gate module `Ran 32 tests` OK; ladder
  `Ran 116 tests` OK; Step 9's second command (main_weapon_slots, troop_skill_curve,
  militia_elite_bonus, kingdom_caps, ranged_ladder) `Ran 280 tests` OK.
- Shipped data, base and planned `Validator._upgrade_skill_regressions()` with the real install:
  `indexed 3118 templated 1440 edges 913 tmpl-edges 0`, `findings 0`, and the SkillSet memo is never
  created (no templated edge, so no SkillSet file is read). `load_skill_sets(<install>, <ModuleData>)`
  returns 744. The plan's measured numbers are right.
- An XML scan of `troops/troops_*.xml`, `fords_of_war/fords_troops.xml` and `characters/npcs_*.xml`:
  1440 templated characters, all in `characters/`, 0 with inline skill rows, 0 with upgrade targets.
  So `analyze_troop_balance.py` and every test that calls `load_roster` on real data without
  `skill_sets` cannot reach the new RuntimeError today (the plan's Out of scope claim holds).
- Engine claims, re-read from the v1.5.7 decompile (`pwsh tools/taom-src.ps1 path BasicCharacterObject`):
  `BasicCharacterObject.Deserialize` copies the template and calls `Init` on the `<skills>` child as
  quoted; `PropertyOwner<T>.Deserialize` only calls `SetPropertyValue`; `RegisterType`'s
  `autoCreateInstance = true`; `Game.cs:321` registers `MBCharacterSkills` as `SkillSet`;
  `GetPresumedObject` creates and registers a presumed object when the type auto-creates. Confirmed.

## Safety gate simulation

The plan changes no hook file. `check-moduledata-validation.sh` passes the same `--code` list and
the same exit-code handling before and after, so a Bash payload and a PowerShell payload reach the
same validator run; the only delta is the validator's findings. I ran 15 rows through
`Validator._upgrade_skill_regressions()` in both copies (base vs planned):

| Row | Base | Planned |
|---|---|---|
| untemplated regression | ERROR `c` | ERROR `c` (same text) |
| untemplated clean | none | none |
| templated source, vanilla level16 SkillSet, install | none | ERROR `c` (Athletics 80->60, ...) |
| same, no install | none | none (skipped, CLI warns) |
| same, install folder with no SkillSets | none | one ERROR `(skill sets)` |
| two templated edges, no SkillSets | none | exactly one ERROR `(skill sets)` |
| single-quoted `skill_template='...'` | none | ERROR |
| dangling template on the target | none | ERROR (inline rows alone, as the engine) |
| templated target above its source | none | none |
| templated target, inline row below source | none | ERROR (Polearm 90->80) |
| militia pair, templated | none | none |
| templated villager in `characters/` | none | ERROR on its target |
| registry `skill_sets={}` | none | ERROR (inline rows alone) |
| `skill_template` inside a comment only | ERROR | ERROR |
| `my_skill_template=` attribute | ERROR | ERROR |

No row loosens; every change is a templated edge that base skipped and the plan now judges. No row
crashes. The hook allows on a validator crash (exit 3), so a new crash path would be a fail-open; see N1.

## Blocking

- **B1 (plan :675 to :678, :700 to :701): Step 8's RED oracle for the clamp test is wrong.** The test
  builds its records like `ClampTests._record` (`tools/tests/test_upgrade_skill_monotonicity.py:259`
  to :262), which has no `status` key. At `db5680c8a` the clamp does not refuse, so it reaches its final
  loop and, for a record with `new` None, reads `c['status']` (`tools/rebalance_troops.py:839`). The
  test ERRORs with `KeyError: 'status'`, not the `RuntimeError not raised` Step 8 tells the executor to
  expect (simulated). A literal executor sees a failed verification. Fix: add
  `child["status"] = "SKIPPED"` (or any string) to the test, which makes the RED fail as stated and
  changes nothing in GREEN; or change the expected RED to `KeyError: 'status'`.
- **B2 (plan :666 to :667): Step 7's grep cannot pass as written.** After the step,
  `git grep -n "templated" -- tools/ranged_ladder.py tools/rebalance_ranged_ladders.py tools/tests/test_ranged_ladder.py`
  prints the five new test method names the plan itself prescribes
  (`test_skill_clamp_judges_a_templated_source_on_its_skillset` and four more, simulated), which are
  neither comments nor docstrings, and "prints only prose lines" is a judgment, not an exact result.
  Fix: use the Done criteria's form for these three files,
  `git grep -n -E '\.templated\b|templated=' -- tools/ranged_ladder.py tools/rebalance_ranged_ladders.py tools/tests/test_ranged_ladder.py`
  prints nothing.

## Non-blocking

- **N1 (plan :559 to :560): put the `sync_lord_inline_skills` import inside the templated branch, not
  at the top of `_upgrade_skill_regressions`.** The hook allows a commit when the validator crashes
  (exit 3, `check-moduledata-validation.sh`). A top-of-method import makes every run of the gate, with
  or without the install and through `taom_query.py` and `check_release_pair.py`, depend on
  `sync_lord_inline_skills` and `_gamedir` importing cleanly. Inside `_skill_sets`'s `try` an import
  failure becomes the one `(skill sets)` ERROR instead of a crash that unchecks every code. (With the
  install, `validate_moduledata.py:531` already imports it outside a `try`, so the desktop path gains
  no new crash; the no-install and MCP paths do.) Name one placement rather than offering two.
- **N2 (plan :722 to :732): the template branch in `process_file` sits before the kit plan**, so a
  templated troop in `troops/` or the second root also loses its `--fix-purpose` and
  `--main-weapon-slots` planning (`main_weapon`, `item0_swaps`), as a `SKIP_TROOP_IDS` troop does. No
  such troop exists today (0 templated in both roots). Say so in the commit body or the Maintenance
  notes so a later reader does not take it for an accident.
- **N3 (plan :769 to :771): the Step 10 count would trip on a machine without the install.** Step 5.6
  adds a WARNING line containing `UPGRADE_SKILL_REGRESSION` to the no-install branch, so
  `grep -c UPGRADE_SKILL_REGRESSION` on the after log would rise by one there. On this machine the
  install exists and the line is not printed. Count finding lines only, for example
  `grep -c "^ERROR.*UPGRADE_SKILL_REGRESSION"`, or state that the run needs the install.
- **N4 (plan :562 to :565): the appended message sentence must not contain `undeclared` or `Polearm`**,
  because `test_a_templated_target_reads_its_skillset_for_an_undeclared_skill` asserts both absent.
  The draft is safe; say so beside the draft so a reworded sentence does not break the test.
- **N5: the template id split differs from the engine for an id with two dots.** The engine reads
  `value.Split('.')[1]` (`MBObjectManager.ReadObjectReferenceFromXml`); `effective_skills` uses
  `template.split(".", 1)[-1]`, as `_scan` already does. `SkillSet.a.b` resolves to `a` in the game
  and to `a.b` here. No shipped id has two dots that I know of (UNVERIFIED across the install); one
  line in the Maintenance notes would do.
- **N6: "Current state" line numbers that are off by one** (content matches):
  - `Registries` is :66 to :91, and `couchable_ids` is :91, not :90 (plan :120, :519).
  - `ranged_ladder.py` imports are :59 to :65, not :59 to :64 (plan :221).
  - The replaced ladder test is :717 to :731, not :718 to :732; `test_loader_marks_a_skill_template`
    is :733 to :745, not :734 to :745 (plan :581, :605).
  - `RangedTroop` starts at :135 (`@dataclass`) and the class line is :136 (plan :193: fine).
- **N7: the Step 4 RED totals line assumes Step 8's tests are not yet written.** That is the plan's
  order, so it holds; just noting that the module reads `Ran 32` once Step 8 lands.

## Checklist

1. Plan-only execution: yes, apart from B1 and B2. Every symbol, line and fixture resolved in the
   worktree; the fixtures for Steps 4, 6 and 8 built as described and behaved as predicted (except B1).
2. Exact expected results: every step ends in a command with a totals line or an exact grep, except
   Step 7's grep (B2). Step 1's "or the difference is explained" is acceptable for a baseline record.
3. Excerpt mismatches: line offsets only (N6). Every quoted code body matches `db5680c8a` verbatim.
4. TDD: RED before each GREEN (Python only; no C#). Issue #626 named. ADR-002, 007, 008 named as
   C#-only. No protected or single-owner file in scope. STOP conditions are specific (templated edge
   found, new finding, empty SkillSet load, engine contradiction). Done criteria are commands.
   Planned-at `db5680c8a` and the drift-check list equal the 13 Scope files. dotnet commands carry
   `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path or branch name. No CHANGELOG step.
   `<Version>` is `v2.0.40` in `Main/_Module/SubModule.xml:6`.
5. No em or en dash in the plan (checked by code point); no secret values; no local absolute paths.

## Not verified here

- The C# baseline (43028 passed, 3 failed, 9 skipped): read-only role, no dotnet run. UNVERIFIED.
- The docs Step 11 edits and `lint_docs.py`: not simulated.

Verified separately: the full Python suite at `db5680c8a` in the worktree gave `Ran 10549 tests` and
`OK (skipped=13)`, as the plan's baseline states.
