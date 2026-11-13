# Cold review: plan 051, round 1

Plan: `plans/051-tools-dead-scripts-and-test-speed.md` (964 lines). Code read from a worktree checked out at
`7087fa633` (`git rev-parse --short HEAD` printed `7087fa633`, `git status --porcelain` empty). No earlier
review file for this plan exists in this folder (Glob `plan-review-051*.md` found nothing), so there are no
earlier blocking items to re-check.

Method: every "Current state" excerpt opened and compared; the three stem greps, the count greps, the
user-path grep and the inherit greps re-run; Part C1 and Part C2 code blocks lifted into scratch copies
(base tool and test copied to scratch, edits applied by script, never in the worktree) and run.

## Blocking

1. **Step 8's memo recurses forever (plan lines 615 to 633).** `_live_graph()` calls
   `ratchet._orphans_and_components()`, but inside `with _shared_graph():` that attribute IS the mock whose
   `side_effect` is `_live_graph`, so the first live call re-enters itself. Simulated in scratch with the
   plan's exact code: `RecursionError: maximum recursion depth exceeded`, `Ran 15 tests`, `FAILED (errors=2)`
   (both wrapped tests). The Step 8.4 mutation check would not reveal this, because it presets
   `_LIVE_GRAPH` and never builds. Fix: capture the real builder at import, before any patch, for example
   `_BUILD = ratchet._orphans_and_components` under the `_LIVE_GRAPH = None` line, and call `_BUILD()` in
   `_live_graph`. With that one change the scratch copy gave `Ran 15 tests in 2.159s`, `OK` (then 1.700 s),
   ruff with the repo's `ruff.toml` printed `All checks passed!`, and the mutation check printed
   `FAILED (failures=1)` and `failures 1`.

2. **Step 2's hit table is incomplete, and the plan makes that a STOP at the base (lines 389 to 390, 416).**
   `git grep -n -w validate_gondor_refs` at `7087fa633` also hits
   `docs/migration/templates/equipment-rosters.md:670` (the "NPC appears naked / underwear" row); the table
   lists only `:659` of that file. A literal executor stops at Step 2 before any edit. Fix: add `:670` to
   the `docs/migration/templates/...` history row (or make the row `docs/migration/**`, as the
   changelog-archive row already is).

3. **Step 4.5 cites the wrong lines in `docs/modding/validation-and-testing.md` (lines 405, 465 to 468), and
   Step 4 says a mismatch is a STOP (line 440).** At the base: `:199` is the heading (correct), but `:202`
   is `no preview and no confirmation step. Commit or copy the target files first.`; the comment ending
   `seven still return 0 on 2026-10-27 -->` is at **`:204`**. The list runs from **206 to 209**, not 206 to
   208 (seven items: two each on 206, 207, 208, one on 209), so "Count the remaining items in the list
   (lines 206 to 208): six" counts five. Fix: `:204` for the comment, "lines 206 to 209" for the count, in
   both Step 2's table and Step 4.5.

4. **The user-path grep result in "Current state" and Step 7.1 is incomplete (lines 135 to 137, 581 to
   584), and "A different result is a STOP condition."** `git grep -n -i -E 'c:.{1,2}Users' -- 'tools/*.py'
   ':!tools/tests'` at the base also hits `tools/check_public_text.py:11` (its own docstring describing the
   local-path rule) and `tools/oneoff/blender_prep_tripo_prop.py:41` (a worked-example line in its
   docstring). Both are docstrings, so the intended claim still holds, but the plan names only
   `tools/blender/` and `tools/painter/painter_run.py:4` as the docstring hits, and a weak executor told
   that a different result is a STOP will stop. Fix: list every hit with its file:line (10 lines at the
   base: five in `tools/blender/`, `check_public_text.py:11`, `generate_xslt.py:295`,
   `oneoff/blender_prep_tripo_prop.py:41`, `oneoff/lords-migration/replace_equipment_templates.py:12`,
   `painter/painter_run.py:4`) and state the expected post-Part-A set exactly (nine lines, the
   `generate_xslt.py` one gone).

## Non-blocking

1. **Phantom table row (line 394).** `git grep -n -w generate_xslt` does not hit `tools/generate_xslt.py`
   at all: lines 89 and 263 hold `generate_xslt_template`, which `-w` does not match. The base grep hits
   seven lines in six files, none in that file. Harmless (an extra row is not a missing one), but delete it
   so the table is the exact hit list it claims to be.
2. **Excerpt `tools/tests/test_check_generator_item_refs.py:131-136` (lines 88 to 96)** omits line 135,
   `self.assertIn(spec.mode, ("run", "tables"))`; the method spans 131 to 135. The RED reasoning is
   unaffected.
3. **Quad packager list (line 144):** `LICENSE.txt` and `NOTICE.txt` are `tools/quad_toolkit/LICENSE.txt`
   and `tools/quad_toolkit/NOTICE.txt` in `PACKAGE_FILES`. The proposed row text (line 590) is correct.
4. **Step 4.6 grep expectation (lines 470 to 474).** `grep -n -E "TAOM_BASE|taom_wanderer_equipment"
   tools/extract_wanderers.py` prints lines 7, 18, 461, 466, 470, 475, not "18 and 469 to 471"; 469 to 471
   is the `write_equipment(...)` call that holds line 470. "Use the line numbers you find" invites a
   different citation. Say: cite 18 and the three lines of the `write_equipment(` call (469 to 471 at the
   base). `SubModule.xml:1124` and the `0` from the argparse count are confirmed.
5. **Diagram alignment (line 491).** The base line has 28 leading spaces and `-->` at column 54.
   `validate_all_troop_refs.py` is three characters longer than `validate_gondor_refs.py`, so three fewer
   spaces (25) keep `-->` in column 54; the plan's two fewer (26) shift it to 55. Cosmetic.
6. **Step 11.2 has no command and no exact expected output (lines 806 to 808).** Give the script, for
   example: import the test module, set `t.skip_ids_for = lambda *a, **k: set(t.key_owners([s for _, s, _
   in t.english_source_files("TAOM")]))` on the module the test imports, run
   `ShippedKeyCoverageTests`, expect `FAILED (failures=1)` with `move each key's rows` in the message.
   "Two-edit check" is also an unexplained name.
7. **Step 11.4 (line 834):** the redirect into `<scratch>\old_twc\translate_with_claude.py` fails unless
   the folder exists; add `mkdir -p` first.
8. **Absolute timing expectations (lines 377, 634, 854).** My base run of
   `python -B -m unittest tools.tests.test_doc_graph_ratchet` gave `Ran 15 tests in 5.107s`, not about
   10 s. The 60 percent relative rule (lines 914, 931) is the real gate; drop "near 10 s and 25 s" or mark
   it informational so an executor does not treat it as a check.
9. **Dash check encoding (lines 514 to 523).** It reads `sys.stdin` as text, which is UTF-8 here only
   because `PYTHONIOENCODING=utf-8` is set on this machine (checked: a sample with U+2014 gave exit 1).
   Reading `sys.stdin.buffer.read().decode("utf-8")` makes it independent of that.
10. **Done criterion line 903 to 904** needs a manual comparison with Step 5.1. A machine form:
    the same grep with `-- ':!docs/changelog-archive' ':!docs/archive' ':!docs/migration'
    ':!docs/reviews' ':!plans'` and the exact expected list (the "deleted" lines, the four dated
    `gondor-armor-revamp.md` bullets and `multi-culture-armor-revamp.md:71`).
11. **Drift check (line 9)** omits files whose excerpts the plan relies on: `tools/package_quad_toolkit.py`,
    `tools/extract_wanderers.py`, `Main/_Module/SubModule.xml`, `tools/_loc_sources.py`,
    `tools/analyze_armor_balance.py`, `tools/apply_settlement_buildings.py`, `tools/derive_armor_tiers.py`,
    `tools/generate_enlistment_rosters.py`, `docs/migration/templates/equipment-rosters.md`. Each is
    re-derived inside a step, so this is a gap in the early warning only.
12. **ShippedKeyCoverage excerpt (line 259):** the base line has three spaces before `#`, the excerpt two.
    Trivial.

## Verified as written (at 7087fa633)

- Line counts 303, 687, 74; `generate_xslt.py:295` opens `spcultures.xslt` through a user-profile path.
- `validate_all_troop_refs.py:5` docstring text; `check_generator_item_refs.py:90-99` and the line-95
  entry; 18 test methods in `test_check_generator_item_refs.py`; `test_tables_mode_...` loads each path, so
  a deleted file errors there as Step 3.2 says.
- Every Part A "old" text in Step 4 except the two in Blocking 3: `tools/README.md:53, 173, 174, 357`,
  `file-catalogue.md:50, 65, 66, 67, 93, 117`, `skill-sets.md:23`, `wanderers-and-named-companions.md:259,
  287`, `provenance-register.md:301`, `gondor-armor-revamp.md:30, 51, 98, 110, 117, 131`. No link targets
  `seven-scripts`. A broader grep (`batch2|gondor_refs|generate_xslt`, case-insensitive, outside history)
  found nothing the `-w` greps miss.
- `tools/README.md:684-713` matches the excerpt (the user name is real on line 712, as the plan says).
  Counts `51` and `91`; `validate_gondor_refs.py` both names the variable and imports `_gamedir`, and the
  other two deleted scripts do neither, so `50` and `90` after Part A is right. The three inherit imports
  (`analyze_armor_balance.py:43`, `apply_settlement_buildings.py:42`, `derive_armor_tiers.py:49`),
  `generate_enlistment_rosters.py:87`, and `a932bd677 2026-08-07`. "Some only in a help line" holds: those
  three name the variable only in a help or error line. `rebuild_anim_from_json.py` exists with no user
  path. No other repo file holds `29 tools` or `22 of the 25`.
- `package_quad_toolkit.py`: docstring, `PACKAGE_FILES`, `FileNotFoundError`, SHA-256 return, `--repo`
  default `.`, required `--out`, both print lines; no test file names it.
- `check_doc_graph_ratchet.py:52-67` and `test_doc_graph_ratchet.py:89-101, 113-126` match; 15 tests.
- `translate_with_claude.py:253-267, 343-363, 405-423, 1016-1027` match; the only `skip_ids_for` callers
  are the two in the tool and the seven test lines listed.
- Part C2 simulated end to end on scratch copies: RED gave three errors, each
  `AttributeError: module 'translate_with_claude' has no attribute 'sibling_string_ids'`; the per-call
  oracle printed exactly the plan's three lists; GREEN gave `Ran 30 tests in 6.359s`, `OK` for the whole
  test module with the Step 11.1 rewrite; `hoist_equality` logic printed `pairs 276 mismatches 0`;
  `discover_equality` logic printed `languages 12 different []`. Ruff with the repo config: `All checks
  passed!`.
- Commit subjects: 57, 67 and 71 characters; `tools/check_commit_msg.py:26` accepts any lowercase type;
  `SubModule.xml:6` is `v2.0.38`.
- Base lint: `python -B tools/lint_docs.py --fail-on-drift --summary` exit 0, `context_budget: 5`,
  `ai_dashes: 0`, `total_findings: 5`; `python -m ruff check tools --no-cache`: `All checks passed!`.
- All 14 in-scope files are CRLF in the worktree (`file` output).
- Checklist item 4: no C#, so TDD for C# and binding ADRs are not applicable and the plan says why; the
  issue line is present; no protected or single-owner file is touched; STOP conditions are specific;
  commands carry `-p:DisableModuleCopy=true -p:ModuleId=`; no worktree path or branch name; no CHANGELOG
  step. Drift-check paths are a superset of Scope.
- Checklist item 5: the only U+2013 or U+2014 characters in the plan are inside quoted excerpts of
  existing text (lines 108, 112, 114, 120, 555); no secret values; drive paths appear only as `<user>`
  placeholders or as quoted existing README text.

- Python baseline, run at the base with `timeout 900`: `Ran 8578 tests in 552.437s`,
  `FAILED (failures=7, errors=2, skipped=13)`, and the failing and erroring names are exactly the nine the
  plan lists (three `test_generator_regions_in_sync` subtests). The run took about 9 minutes, not the
  plan's "about 6 to 7 minutes" (line 366); still inside the 15-minute limit, so non-blocking.

## UNVERIFIED

- The dotnet baseline (`Passed: 39250, Skipped: 9, Total: 39259`, 11 warnings): this review is read-only
  and ran no dotnet.
