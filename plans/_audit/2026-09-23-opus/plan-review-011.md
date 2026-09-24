# Cold review: plan 011 (Stop reminders reach Claude, trunk force-push guard)

Reviewer: cold, no prior context. Read `.claude/skills/improve/references/plan-template.md` and
`plans/011-stop-reminders-and-trunk-guard.md` (1194 lines), then compared every "Current state"
excerpt against `git show b2e387db:<path>` for the four Stop hooks, `validate-push.sh`,
`mark-verification-run.sh`, `config-protection.sh`, `.claude/settings.json`, `tools/test_hooks.sh`,
`docs/reference/hooks-catalog.md`, `.claude/rules/harness-facts.md`,
`.claude/rules/hook-authoring.md`, `CLAUDE.md`, `.gitignore` and the top of `CHANGELOG.md`.
The drift check (plan line 12) prints nothing at HEAD `4b5662b2`. The vendor quotes (plan lines
62-82) match the raw `https://code.claude.com/docs/en/hooks.md` fetched with curl (its lines 810,
822, 958, 1668-1681, 2554, 2620-2622). `claude --version` prints `2.1.241`.
`python tools/audit_claude_config.py --min MED` prints `HIGH:1  MED:1` with the two named findings.
`python tools/lint_docs.py --fail-on-drift` exits 0 in the main tree. I also ran the plan's
`_stop_reminder.sh` and its 7a helper checks in a scratch copy: all five pass, and the escaping
round-trips through `python` on this machine.

**Verdict:** the design is sound and every code excerpt matches. A weak executor still cannot
finish it as written. Three defects are blocking.

## Blocking

1. **Mike's OK cannot take effect, so Step 9 always STOPs (plan lines 28-32, 852-854, 1093-1095).**
   `config-protection.sh` (b2e387db, lines 40-67) refuses Edit and Write on any file named
   `settings.json` with exit 2. It lets the edit through only when
   `/tmp/claude-config-override-${CLAUDE_SESSION_ID}` exists, and the plan forbids creating that
   file (lines 31 and 1094). No instruction says how Mike's OK reaches the hook, so the executor's
   Edit is refused whether or not Mike agreed. The executor then stops at Step 9 with Steps 2-8
   uncommitted in the worktree and the 7c registration check still red, and never reaches the
   docs, CHANGELOG or commit steps. Fix: before dispatch, the orchestrator or Mike makes the two
   settings.json insertions in the worktree, and Step 9 becomes a verification step. Or name the
   approved mechanism (Mike creates the override file for the executor's session) and say how the
   executor checks for it. Or move the settings edit to the end and define a partial-completion
   commit.

2. **Step 3's check fails every time (plan lines 665-666).** The pattern
   `grep -E "taom_stop_(block|hook_active)"` also matches the 7a static FAIL message from Step 2c
   (plan line 485, `...; use taom_stop_block`). Those four FAILs stay until Steps 4-7 convert the
   hooks. The suite's Summary block (`tools/test_hooks.sh:820-822`) repeats each message too.
   I checked this by piping the FAIL line through the plan's grep: it matches. The expected
   result "five ok lines and no FAIL" is therefore false at Step 3. Under the "fails twice" STOP
   (line 1104), a literal executor stops there, or it "fixes" the hooks or the test out of order.
   Fix: grep only the helper lines, for example
   `grep -E "taom_stop_block (emits|output)|taom_stop_hook_active (returned|[01] for)"`. Or say
   that the four `writes to stderr` FAILs are expected until Step 7.

3. **Edit, Write and Read paths are never made absolute (plan lines 343-345, 408-411, 445, 611,
   670, 706, 775, 803, 899-962).** The plan tells Bash commands to `cd` into the worktree first.
   Every file the Edit or Write tool touches is still given as a relative path
   (`tools/test_hooks.sh`, `.claude/hooks/check-*.sh`, `CLAUDE.md`, `.claude/rules/*.md`,
   `docs/reference/hooks-catalog.md`); only Step 9 gives an absolute one. A subagent's cwd is
   `E:\repos\TAOM`. A weak executor that resolves those paths there edits the main tree: the live
   hooks every running session executes, and `CLAUDE.md` / `harness-facts.md`, which hold another
   session's uncommitted edits (plan lines 53-56 say so). Line 411 ("Work only inside ...") names
   the rule but gives no mechanism. Fix: one line in "Commands you will need" saying every Edit,
   Write and Read path is `E:/repos/wt-011-stop-reminders/<path>`, plus a STOP condition for any
   edit under `E:/repos/TAOM`.

## Non-blocking

- **The dash check can never fail (plan line 965).** `grep -P '[\x{2013}\x{2014}]'` in this Git
  Bash (LANG empty) exits 2 with "character value in \x{} or \o{} is too large" and prints
  nothing, so "prints nothing" passes even when a dash is there. Tested here. Use
  `LC_ALL=en_US.UTF-8 grep -P ...`, which I confirmed matches, or `grep -n $'\xe2\x80\x94\|\xe2\x80\x93'`,
  and expect rc 1.
- **The RED list is imprecise (plan lines 600-606).** "The helper checks FAIL": with the helper
  missing, `source` fails and returns 1, so the two `want 1` cases of `taom_stop_hook_active`
  (lines 500-501) pass. Only three of the five helper checks fail. The four
  `is silent when stop_hook_active is true` checks and the four `mutes after one reminder` checks
  also pass at RED, because the old hooks print nothing on stdout. A strict reading of STOP line
  1099 could halt on this. List the expected passes too.
- **The same line appears twice (plan line 445).** `[[ "$name" == "_pybin.sh" ]] && continue` is
  identical at `:354` and `:470`. The Edit tool refuses a non-unique `old_string`, so say
  `replace_all: true`.
- **Line numbers after insertions are pre-edit (plan lines 688, 692, 789, 793-795, 816, 820).**
  After Step 4.2 or 6.2 inserts lines, "line 46", "line 49", "lines 27, 35, 44" and similar no
  longer point where they say. Add one sentence: numbers are pre-edit, so match by the quoted
  text.
- **Shell (plan lines 343-350, 597, 665).** Every verify command uses Bash pipes (`tail`, `grep`,
  `2>&1 |`). The primary shell here is PowerShell, where `tail` does not exist. State "run every
  command in this plan with the Bash tool".
- **Timeouts (plan lines 349, 355, 1000-1006).** The full `tools/test_hooks.sh` (4b probes, 8
  runs `scan.sh` twice) and a 10k-test `dotnet test` can take longer than the default 120 s Bash
  timeout. Give an explicit `timeout` (for example 600000).
- **The dotnet test baseline is not re-measured (plan lines 359-365, 1006, 1081).** I did not
  verify the 10,239 / 2-fail claim. The two failures read the live Armory, which another session
  is editing, so the set can change by execution time. Step 1 records a hook-suite baseline but no
  `dotnet test` baseline. Record both in Step 1 and judge Step 12 and the done criterion against
  that run.
- **Scoped-rule size (plan lines 930-949).** `hook-authoring.md` is 12,188 B at b2e387db against
  `SCOPED_RULE_MAX_BYTES = 12_288` (`tools/lint_docs.py:69`). Step 10c adds about 300 B. Step 10b
  adds about 450 B to `harness-facts.md` (11,794 B). Both will likely cross the cap. The finding
  is report-only (`SCOPED_RULE_BUDGET_ENFORCE = False`), so `--fail-on-drift` still exits 0.
  The plan should expect the new size-warn or trim the rows.
- **Step 1 scratch path (plan lines 430-434).** It uses `"$TMP"` and says to use the scratchpad
  instead. Here `$TMP` is `C:\Users\mikew\AppData\Local\Temp`, which the Read tool can open, so
  this is harmless but self-contradictory.
- **Drift-check paths (plan line 12 against Scope, lines 372-388).** They match Scope except
  `plans/README.md`. The plan explains why `CHANGELOG.md` is left out but not `plans/README.md`.
- **Riskiest assumption (plan lines 1114-1118).** It rests on the docs. The raw page confirms
  `decision: "block"` on Stop and the `stop_hook_active` / 8-block cap. Nothing in the repo can
  prove delivery on 2.1.241, and the plan says so and hands it to Mike's live check A. That is
  correct.

## Rubric

- **Self-contained:** yes, apart from blocking items 1 and 3 and the shell and timeout notes.
- **Every step ends in a command:** yes. Step 3's expected result is wrong (blocking 2), and the
  Step 10 dash check cannot fail.
- **TDD order:** Step 2 writes the failing checks first (RED), Steps 3-9 make them pass.
- **Issue first:** line 27 ("create before implementation lands (orchestrator)").
- **ADRs:** lines 319-322 correctly say no C# ADR binds, and they name the hook rules that do.
- **Single-owner files:** `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` and
  `Directory.Build.props` are out of scope (lines 392-393) and have a STOP (line 1105).
- **STOP conditions:** specific to this plan (settings refusal, commit gates reading the main
  tree, a changed RED premise, the block-delivery assumption).
- **Done criteria:** all machine-checkable (lines 1074-1085). I confirmed the `grep -c` and
  `python` one-liners against the planned end state.
- **Planned-at SHA:** `b2e387db`, consistent with the drift check.
- **Non-deploying builds:** every `dotnet` command carries `-p:DisableModuleCopy=true
  -p:ModuleId=` (lines 354-356, 1005-1006, 1081), and `./build.ps1` is forbidden (line 367).
- **Dashes:** every em dash in the plan is inside a code span or a verbatim code excerpt
  (lines 111, 124, 188, 193, 793-795, 820-821), so all are exempt.
- **Secrets:** none. Line 351 names a fixture location only.

## Excerpt mismatches

The code excerpts all match b2e387db, both line numbers and text. The mismatches are in edit
anchors that quote file text without its formatting. An Edit `old_string` copied from the plan
would fail on each:

- Plan line 924: the quoted two sentences are on one line with bare `deny` and `ask`. In
  `hooks-catalog.md:54-56` they span three lines and read `` a PreToolUse `deny` reason `` and
  `` An `ask` reason ``.
- Plan line 931: the anchor `an ask reason is shown to the user, not to Claude.` is
  `` an `ask` reason is shown to the user, not to Claude. `` at `harness-facts.md:60`.
- Plan line 904: the quoted line 3 text leaves out the backticks around
  `check-commit-subject-version.sh`, `settings.json`, `/freeze`, `/investigate`, `check-freeze.sh`
  and `_pybin.sh`. Line 907 tells the executor to keep them, but it has to rebuild the anchor
  itself.
