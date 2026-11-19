# Cold review: plan 059, round 1

Plan: `plans/059-validate-moduledata-mcp-parity.md`, read at `db5680c8a` in the run worktree. No earlier
review file for this plan exists in this folder (only `PROGRESS.md`), so nothing is carried over.

Verdict: **2 blocking, 12 non-blocking.** Both blocking items are verification commands that cannot pass
as written; the code the plan prescribes behaves as claimed, and the commit gate's verdict does not
loosen under either tool name.

## How this was checked

The plan's code was lifted into a scratch copy of the touched `tools/` files (the review-059-r1 scratch
folder: `sim/` holds the planned code, `sim_base/` the code at `db5680c8a`, every other `tools/*.py`
copied beside them so no import falls back to the worktree). The elided "verbatim" parts of Step 4 were
filled from `main()` at :832-843, :870-871 and :898-918. Then:

- RED set (Steps 3, 8, 10) against the base code: every predicted failure reproduced.
  `test_every_issue_pass_is_reached_from_run_all_passes` ERROR `AttributeError: 'NoneType' object has no
  attribute 'group'`; both parity tests ERROR `TypeError: ModuleDataQuery.__init__() got an unexpected
  keyword argument 'game_modules'`; `test_no_install_given_is_a_not_verified_warning` ERROR `TypeError:
  argument should be a str or an os.PathLike object ... not 'NoneType'`;
  `test_main_and_the_mcp_tool_both_call_run_all_passes` FAIL on the assertIn; the server test ERROR
  `AttributeError: 'ModuleDataQuery' object has no attribute 'game_modules'`; the fallback test FAILs on
  both subtests; the `Languages/` test FAILs `['from_items', 'from_languages'] != ['from_items']`; the
  default-modules test FAILs; `test_loaded_tpacs_reads_taom_map_packs` FAILs;
  `test_loaded_tpacs_takes_taom_from_the_repo_module` ERROR `TypeError: _loaded_tpacs() takes 1
  positional argument but 2 were given`; the line-number characterization test passes at base, as the
  plan says.
- Planned code, per file: `test_validate_mesh_refs.py` "Ran 73 tests ... OK"; `test_taom_query.py` "Ran
  15 tests ... OK"; `test_taom_mcp_server.py` "Ran 7 tests ... OK". `test_validate_moduledata.py` "Ran
  452" against the base sim's 446: the failure set differs from the base sim's by exactly one name,
  `test_every_emitted_code_has_a_row_in_the_codes_doc` (blocking item 1); with the Step 6 row added it
  passes ("Ran 1 test ... OK"). The other failures in both sims are sim artifacts (no repo data beside the
  copy) and identical in both.
- CLI on the live install with the hook's exact `--code` list (read from the hook): base rc 0, planned
  rc 0, stdout identical, issue sets `added 0`, `removed 0`; 14.1 s base, 12.5 s planned (the hook's inner
  bound is 45 s). A full run: rc 0 both; the only differing issues are four warnings whose message embeds
  the sim folder's own path.
- The shipped hook (`check-moduledata-validation.sh`, unchanged by the plan) driven in a scratch git
  sandbox against the base and the planned validator, Bash and PowerShell payloads:

  | Case | Bash base / planned | PowerShell base / planned |
  |---|---|---|
  | clean live install | `{}` / `{}` | `{}` / `{}` |
  | a staged troop with `Item.zz_no_such_item` | deny / deny | deny / deny |
  | no install (exit 2) | `{}` / `{}` | `{}` / `{}` |
  | a raise inside `run_all_passes` | (n/a) / allow + CRASHED context | (n/a) / allow + CRASHED context |

  No row loosens. A crash in the new composition reaches `entry()` and exits 3, as before.
- Step 7's new 7m cases against the shipped hook: exit 0 `{}`, exit 2 `{}`, plain exit 3 allow with
  CRASHED, under both tool names. The three mutation strings each match exactly once in the hook, and
  each mutant gives the non-expected answer (exit 0 and exit 2 get an UNCHECKED context, exit 3 gets
  `{}`); the unmutated copy answers `{}`.
- MCP probe (Step 5.2 shape) on the live install: 0 characters on stdout, 0 `PARTIAL_RUN` notices,
  `added 0`, `removed 0` against the planned CLI's full JSON, 10.8 s. Step 5.3 will not trigger today.
- `extract_refs` base against planned, four runs each: refs identical for both roots (Armory 6823, repo
  281, 0 under `Languages/`); Armory 1.87 to 2.12 s down to 0.59 to 0.69 s, repo 1.83 to 2.09 s down
  to 0.69 to 0.74 s. The Step 9 "not higher" timing gate has a wide margin.
- Mesh CLI `--no-rgl-log --scan-bodies`: `Tier B: scanning 5827 .tpac files` (base) and `7993` (planned),
  `0 pack(s) unparsed` in both, 0 findings in both.
- `python -m ruff check --config ruff.toml` over the eight planned files: "All checks passed!"
- Item 6: the base script form ran 247 tests against discover's 446; after the move both ran 452.

My fill of the elided Step 4 text is one executor's reading; the comparisons prove the design, not
the executor's future keystrokes.

## Blocking

1. **Step 4's Verify cannot pass (plan lines 705 to 708, 742 to 743).** `run_all_passes`'s `notice()`
   builds `ts.Issue(severity=ts.Severity.WARNING, code=PARTIAL_RUN_CODE, ...)` and `PARTIAL_RUN_CODE =
   "PARTIAL_RUN"` sits at module level, so `CommitGateCoverageTests._named_codes`
   (`tools/tests/test_validate_moduledata.py:5046-5051`, the regex
   `severity=(?:ts\.)?Severity\.WARNING,\s*code=([A-Z_]+_CODE)\b`) counts `PARTIAL_RUN` as emitted, and
   `test_every_emitted_code_has_a_row_in_the_codes_doc` (:5150) fails until the codes doc has its row.
   The plan adds that row only in Step 6.1, but Step 4's Verify requires the whole
   `test_validate_moduledata.py` to show OK "or only the base's own failures". Simulated: `AssertionError:
   Lists differ: ['PARTIAL_RUN'] != []`. A literal executor fails Step 4 and either STOPs or edits a gate.
   Fix: move the `docs/reference/moduledata-validator-codes.md` row from Step 6.1 into Step 4 (it is still
   staged in the Step 6 commit), or name this one failure as expected in Step 4's Verify.

2. **Step 2's Verify cannot pass (plan lines 456 to 457).** `grep -n '__main__'
   tools/tests/test_validate_moduledata.py` prints two lines after the move, because :4321 holds
   `runpy.run_path(..., run_name='__main__')`. Checked on the planned copy: lines 4340 and 5434. Fix:
   `grep -n '^if __name__ == "__main__":' tools/tests/test_validate_moduledata.py` prints one line, within
   the last three lines of the file.

## Non-blocking

1. **Half of #623 item 1 is dropped without saying so (plan lines 374 to 375, 756, 1136).** The issue's
   item 1 is "The MCP tool and `/verify` never run the install-backed passes". The plan puts
   `.claude/skills/verify/SKILL.md` out of scope with no reason, Step 6.3 rewrites `(MCP, /verify: #623)`
   to `(and the MCP tool, #623)` (the `/verify` gap vanishes from the diagram while still true), and the
   orchestrator step closes #623. Name the `/verify` half as deferred (or rejected, with the reason) in
   Maintenance notes and in the Step 6 commit body, and keep #623 open or file the follow-up.
2. **A current-tense stale claim stays (plan line 368, 756, 1110).** `docs/features/armory-ref-audit.md:84-86`,
   under "### Solution Approach" (not the Changelog), says `CommitGateCoverageTests` checks "that every
   `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not run it (#623)".
   Both halves go stale with Step 4 (the scan moves to `run_all_passes`). The Scope limits that file to
   line 104, and the done-criteria grep misses it because the phrase is split across lines 85 and 86.
   Widen the scope to :84-86 and grep for `reached from .main()` too.
3. **Step 7.2's RED script can produce a false RED on this machine (plan lines 801 to 809).** From
   Python, `subprocess.run(["bash", ...])` resolves to the WSL `bash.exe` in System32: measured rc 1 with
   `WSL ... execvpe(/bin/bash) failed`, empty stdout, while `shutil.which("bash")` names Git Bash. An empty
   answer is "not `{}`" and "no CRASHED", so all three mutations would look demonstrated; only the
   control catches it. Tell the executor to call `shutil.which("bash")` (or write `hook_red` as a bash
   script) and to treat an empty answer as "did not run".
4. **Step 9.1's line range is one short (plan line 891).** The comment being replaced starts at
   `tools/validate_mesh_refs.py:845` (`# Only if a pack we needed soft-failed ...`); :846-848 leaves that
   first line behind as a duplicate. Say :845-848.
5. **The moved no-install text carries an em dash and CLI wording (plan lines 592 to 594, 628).** The
   text at `validate_moduledata.py:842` holds an em dash (U+2014) between `not 0` and `a degraded sweep`, and it says "This run will exit 2"
   and "pass --game-modules <path>", which the MCP caller now receives as a notice. Dispatch rule 14 bans
   writing an em dash, so a careful executor may "fix" it and break the byte-identical stderr the plan
   requires (no test or diff in Step 5 compares stderr). State that the moved texts keep the dash
   verbatim, and either accept the CLI wording in the MCP notice or say so in Maintenance notes.
6. **Step 10's removed-finding check omits SandBox (plan lines 1034 to 1037).** The mesh CLI's default list
   gains `SandBox` as well as TAOM and `TAOM_Map`, and the CLI reads TAOM from the install, not the repo.
   A finding removed because SandBox ships the art would trip the gate and undo Step 10. Measured today:
   0 findings in both mesh CLI runs, so no effect now. Add SandBox and the installed TAOM to the check.
7. **The "no packs" guard can no longer fire on the commit path after Step 10 (plan lines 1013 to 1027).**
   `_loaded_tpacs(game_modules, moduledata)` always adds the repo module's packs (`git ls-files
   Main/_Module | grep -ci "\.tpac$"` prints 85), so `if not tpacs` in
   `missing_collision_body_issues` (:355) stays false even when the install's art modules hold no packs;
   the pass then reports per-ref MISSING instead of "NOT verified". The repo ModuleData has body refs in
   four files, so such a run still denies: not a loosening today. Mention it in Maintenance notes, or
   test the guard on the install modules' packs only.
8. **The MCP now pays the CLI's cost and keeps its cache (plan lines 683 to 690, 1146 to 1148).**
   `test_taom_mcp_server.py` now runs every install-backed pass on the live install through
   `test_validate_tool_returns_shape` (7.2 s for the file in the sim). Each MCP `validate` call refills
   `ts._read_stripped`, which `taom_mcp_server.py:56-58` clears after the registry build because it holds
   about 200 MB; the plan never clears it after a run. Consider `ts._read_stripped.cache_clear()` after
   `run_all_passes` in `validate()`, or name the cost in Maintenance notes.
9. **`<Modules>` has no derivation (plan lines 417, 737, 1036).** Give the command that prints it, for
   example `python -B -c "import sys; sys.path.insert(0, 'tools'); import validate_moduledata as vm;
   print(vm.DEFAULT_GAME_MODULES)"`.
10. **Step 10.1's RED run has no command (plan line 993).** Give both lines with their counts; note that
    `-k test_loaded_tpacs` also matches the existing `test_loaded_tpacs_keeps_the_modules_path_it_was_given`
    (3 ran in the sim, not 2).
11. **The new codes-doc row's style (plan line 743).** The table's 54 rows say `(error)` or `(warn)`; the
    plan writes `(warning)`, and "near the other tooling rows" names no anchor row. Say `(warn)` and name
    the row to insert after.
12. **A Current state claim is incomplete (plan line 179).** "`check-moduledata-validation.sh` reads stdout
    and the exit code only": on exit 3 it also quotes the last 15 stderr lines
    (`check-moduledata-validation.sh:170`). Harmless here, because the moved texts keep their wording.

## Excerpt mismatches

- Plan lines 55 and 68: `taom_query.validate()` is cited as :124 to :136. At `db5680c8a` :124 is the
  `# -- validation --` header; `def validate` is :125 and the method ends at :137.
- Plan line 891: the classify fallback is cited as :846-848; the replaced comment starts at :845.
- Plan line 179: the hook also reads stderr on exit 3 (non-blocking item 12).

Every other excerpt and line citation matched at `db5680c8a`: `validate_moduledata.py` :308-317, :341-342,
:354-358, :366-367, :529-530, :586-616 (the `is_dir` check at :606), :663-665, :802-945 with each sub-range
in Item 1, :948-963, and 963 lines in all; `taom_query.py` :36-39 and :45 (137 lines);
`taom_mcp_server.py` :52-59, :73-79 (142 lines); `validate_mesh_refs.py` :97-100, :214-215, :266-318 (the
three `_lineno` calls at :281, :288, :295), :426, :543, :772-773, :836-851, :1137-1140, :1183,
:1237-1238 (1279 lines); `test_validate_moduledata.py` :3168-3169 (22 classes after it), :4052,
:5053-5066, :5150, the stderr tests, 5346 lines; `test_taom_query.py` :156-170; `test_hooks.sh` 7m at
:3982-4054 with the cases at :3994-4025, :4027-4033, :4035-4039 and the career kit comment at :4040; the
hook at :18-23, :166-182, :185-191; `taom_schema.py:477-478` and :1710; `SubModule.xml:21-25`;
`coop-dedicated-server.md:44`; `hook-authoring.md:80`; `.githooks/pre-commit:105`; `data_gates.py:143`;
`audit_armory_refs.py:105`, :340, :355, :357; `mesh-ref-validation.md:149`; `armory-ref-audit.md:104`. All
eight Python files are CRLF throughout; `test_hooks.sh` is LF.

## Checklist items

- Executability: the knowledge gaps are non-blocking items 3, 9 and 10.
- Exact expected results: every step has one, apart from the two blocking items and item 10.
- TDD: each implementation step follows a RED step whose failures reproduce (above). No C#, so no
  binding ADR applies; the plan says so (line 329).
- Issue line: #623, with the maintainer's approval for `tools/` named. No protected file changes; Step 0
  says none. Single-owner files are out of scope.
- STOP conditions are specific to this plan (Languages refs, differentials, stdout, budgets).
- Drift-check paths (plan line 8) cover every in-scope file, plus the hook, which is read but not edited.
- Commands are non-deploying with `-p:ModuleId=`; no worktree path or branch name; no CHANGELOG step.
- Prose: the only em dashes in the plan are inside the two quoted code excerpts at lines 230 and 234
  (the current code); no secret values; no local absolute path.

## Not verified

- The dotnet baseline (43028 passed, 3 failed, 9 skipped): this role does not run dotnet.
- The Python suite's 10549 total and the hook suite's base count: not run here.
- `lint_docs.py --fail-on-drift` budgets after the doc additions: the plan's STOP covers an overrun.
