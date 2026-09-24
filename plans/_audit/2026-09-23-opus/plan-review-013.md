# Cold review: plan 013 (Bash hook prefilter)

Reviewer: cold, no prior context. Read `.claude/skills/improve/references/plan-template.md` and
`plans/013-bash-hook-prefilter.md`, then compared every "Current state" excerpt against
`git show b2e387db:<path>` for all 14 hooks, `_pybin.sh`, `tools/test_hooks.sh`,
`docs/reference/hooks-catalog.md`, `.claude/rules/hook-authoring.md` and `.claude/settings.json`.
`git diff --stat b2e387db..HEAD` over the drift-check paths prints nothing (HEAD `4b5662b2`).

**Verdict:** not executable as written by a weak executor. Two blocking defects. The design, the
superset argument and the excerpts hold up.

## Blocking

1. **The RED and intermediate counts are doubled by the suite's own failure summary (plan lines
   343, 386, 425, 477, 641).** `tools/test_hooks.sh` `bad()` prints each failure once inline
   (`:35`). Whenever FAIL > 0, the Summary block prints every message a second time
   (`:820-822`, `printf '    - %s\n' "${FAILED_DETAIL[@]}"`). So in Step 1 (rc=1),
   `grep -c "started an interpreter"` returns **26**, not 13. The later counts come out the same
   way: Step 2 gives 10 (plan says 5), Step 3 gives 6 (plan says 3), Step 4 gives 2 (plan says 1).
   The STOP condition at line 641 ("does not show exactly 13") fires at Step 1, so a literal
   executor halts before touching a hook. The "still parses" counts (14) are right because they
   are `ok` lines, and so are the Step 5 and done-criteria counts, because rc=0 prints no summary.
   Fix: grep only the inline form, for example `grep -c "FAIL .*started an interpreter"`, or
   halve the expected numbers.

2. **The plan never says to make paths absolute or re-enter the worktree (plan lines 200, 218,
   236, 248, 341, 506).** Every path in the plan is relative (`.claude/hooks/<file>`,
   `bash tools/test_hooks.sh`, parity's `WT=$(pwd)`). The only instruction is "Run every command
   from `E:/repos/wt-plan-013`". A subagent executor's Bash cwd resets to `E:\repos\TAOM` between
   calls, and Edit/Write need absolute paths. The weak reading resolves `.claude/hooks/...`
   against `E:\repos\TAOM`. That edits the main tree's live hooks, which every running session
   executes, in a tree the plan itself says holds another session's uncommitted work. The
   milder outcome: tests and parity run against unedited main-tree hooks, the counts come out
   wrong, and the executor stops. Fix: state that every Edit/Write/Read path is
   `E:/repos/wt-plan-013/<path>` and that every Bash call begins `cd /e/repos/wt-plan-013 &&`.

## Non-blocking

- **Bash tool timeout (lines 204, 209, 550, 606-607):** `dotnet test` over about 10k tests, the
  full `test_hooks.sh`, and the 156-case parity run (two hook runs per case, about 250 ms each
  for the old hooks) can each pass the Bash tool's 120 s default. None of these steps says to pass
  `timeout: 600000` or to run in the background.
- **Undeclared overlap with plan 011 (line 26, "Depends on: none"):** `plans/011-stop-reminders-and-trunk-guard.md`
  drift-checks and edits `validate-push.sh`, `mark-verification-run.sh`, `tools/test_hooks.sh`,
  `docs/reference/hooks-catalog.md` and `.claude/rules/hook-authoring.md`. Whichever lands second
  hits its drift STOP. If 011 lands first, the "100 bytes under its size cap" claim (line 579)
  can go stale. Name the ordering or the conflict.
- **Step 7 threshold wording (line 566):** "under 80 ms, or under (floor + 30) ms when the floor
  is 50 ms or more" does not say whether the larger bound is allowed. Say "under
  max(80, floor + 30) ms". Timing-based STOPs (line 646) are load-dependent; a rerun rule would
  help.
- **Insertion anchors are ambiguous (lines 264, 572):** the section 5 banner starts at the dashes
  line (`test_hooks.sh:416`), not at `# 5. Starved...` (`:417`). Anchoring on the quoted text
  splits the banner. That is cosmetic, since these are comment lines only. The hooks-catalog
  insertion after line 13 has no `>` separator line, so it merges into the preceding blockquote
  paragraph. Also cosmetic.
- **The superset premise is a serializer property (lines 173, 370-373):** JSON *permits*
  `\u0067` for `g`. The claim "JSON never escapes an ASCII letter" holds for the harness's
  serializer, not for JSON in general. Worth one clause in Maintenance notes. There is no defect
  under the real harness.
- **Judgment verifies:** Step 0's Verify (line 260, "you have ... written down") and the
  "Hook suite, every line" command row (line 205, no expected result) are not command/result
  pairs.
- **Commit gates read the main tree (line 239):** `check-changelog-changed.sh` and siblings `cd`
  to `CLAUDE_PROJECT_DIR`, so they judge the main tree's index, not the worktree's. It is empty
  today (`git diff --cached --name-only` printed nothing). If another session stages `.claude/`
  files there before the commit, the gate denies it. The plan does cover this with its STOP.
- **Unverified here:** the test baseline (line 212) was not re-run; the 2026-09-23 CHANGELOG
  entry at HEAD corroborates "10235 passed, 2 skipped, 2 failed" with the same two test names.
  The planning timings (line 179) were not re-measured. The fake-interpreter premise is
  consistent with a check made here: Git Bash reports `-x` on a shebang file (`/` is `noacl`).

## Excerpt check (item 3)

Everything matches `b2e387db` except one label:

- Line 68 labels the `check-changelog-changed.sh` excerpt `:14-32`, but it continues through the
  two `case` blocks at `:34-42`. The content is correct; only the range is short.

Verified to match: the settings table (line 45, every timeout); `_pybin.sh:88-90`, `:98`,
`:141-147`; all eight preamble ranges and non-trigger exit lines in the table (lines 99-106,
including the comma variant at `check-commit-subject-version.sh:31`); `block-no-verify.sh:1-4`,
`:27`, `:47-50`; `validate-push.sh:6-13`, `:54`, `:57-63`; `notify-test-results.sh:1-6`, `:39`;
`mark-verification-run.sh:11-14`, `:66-73`; `suggest-compact.sh:76-90`, `:114-138`, `:146`;
`test_hooks.sh` (825 lines, sections 4 at `:300`, 4b at `:388-414`, 5 at `:416-493`, 5c2 at
`:533`); `hooks-catalog.md:12-13`; `hook-authoring.md:153` and 12,188 bytes (cap
`SCOPED_RULE_MAX_BYTES = 12_288`); `SubModule.xml` `v2.0.30`. The plan does not excerpt
`Main/SubModule.cs` or `Main/IoC.cs`.

## Item 4 checklist

- TDD order: no C#; the hook test (Step 1 RED) precedes every hook edit. Pass.
- Issue-first: line 29, the orchestrator creates it. Pass.
- Binding conventions: ADR-011, harness-facts, hook-authoring, simplicity-criterion, and
  `.gitattributes` LF, each with a one-line summary; ADR-002/007/008 are declared N/A. Pass.
- Single-owner files: line 183 and out of scope at line 228. Pass.
- STOP conditions: specific to this change (counting premise, narrower prefilter, parity DIFF,
  new registration, section 5 silence). Pass, except that line 641 is miscalibrated
  (Blocking 1).
- Done criteria: all are commands with expected output. I checked the grep counts against the
  planned edits (11 files with `*git*`, 3 with `*dotnet*`; none at `b2e387db`). Pass.
- Planned-at SHA and drift paths: `b2e387db`; the drift list covers all in-scope paths except
  `CHANGELOG.md`, which is excluded with a stated reason, plus two out-of-scope guard files.
  Pass.
- Non-deploying commands: build and test both carry `-p:DisableModuleCopy=true -p:ModuleId=`;
  no `./build.ps1`. Pass.

## Item 5

- Em and en dashes appear only inside code blocks that quote existing hook comments (lines 72,
  88, 112, 138, 357, 377, 403, 432, 449); those are exempt. The prose has none. Pass.
- Secrets: none. `TAOM_PYBIN=C:/Python314/python.exe` is a path, not a credential. Pass.
