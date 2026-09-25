# Deep review: plan 011, Stop reminders and trunk guard (#654)

```
DEEP REVIEW REPORT
===================
Feature: plan 011, "Make the Stop-hook reminders reach the session and guard both trunks
         against force pushes" (improve/011-stop-reminders-and-trunk-guard, #654)
Date:    2026-09-25 (review of 43e6780e against bec0389d)

Scope:   harness (hooks, settings.json registrations, rules), scripts (tools/test_hooks.sh),
         docs (hooks catalog, CHANGELOG, CLAUDE.md). No C#, C++, XML or XSLT.
Waves:   Standards (1), Efficiency (3), Completeness (4), Data flow (5), Design (6) and
         Tooling. Codex adversarial pass complete ("END OF CODEX REVIEW" present).

STANDARDS:     FAIL: 1 HIGH, 1 MED, 6 LOW (all confirmed; 5 fixed, 1 fail-safe kept by design,
               1 optional settings merge for the orchestrator)
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    FAIL: 1 issue fixed (L: quadratic split, superseded by the Python split),
               1 applied (L: duplicate 7c pass), 2 follow-ups
COMPLETENESS:  INCOMPLETE at review; now COMPLETE: silence and re-arm tests added, writer and
               reader driven together, #654 cited, plan-only facts moved to docs
DATA FLOW:     FAIL: 1 gap (PostToolUseFailure; settings change for the orchestrator),
               6 inconsistencies (5 fixed or documented, 1 needs Mike)
DESIGN:        5 KEEP proposals (3 applied inside the HIGH fix, 1 needs Mike, 1 settings merge
               for the orchestrator)
XML:           NOT IN SCOPE
TOOLING:       FAIL: 8 findings (HIGH fixed, 3 MED fixed, 1 MED follow-up, the rest follow-ups
               or fixed)
```

## Details

### Classification of every finding

The numbering is the reporting lens's. "Fixed" means a failing test first, then the fix. The
RED log is `E:\repos\taom-improve\scratch\review-011\lead\red.txt`: 28 failures, each one a
finding below.

| Finding | Lens(es) | Verdict | Resolution |
|---|---|---|---|
| Same-line tail, second refspec, `--all`/`--mirror` bypass the force-push block | 1 HIGH, 5 F1, Tooling F1/F6, 4 U1, 6 KEEP 1-2, Codex P1 | CONFIRMED HIGH (reproduced: 18 of the new 7c rows failed at `43e6780e`, plus the trailing-comment form recorded on 2026-09-23) | Fixed. Each line split at `; & \|`, redirections skipped, every refspec judged, `--all` with force and `--mirror` refused, `(`/`)` flattened |
| `stop_hook_active` early exit skips marker cleanup, so the next streak is muted | Codex P2 (#2) | CONFIRMED MED (all four hooks failed the new continuation row) | Fixed. The guard sits just before the block; cleanup always runs |
| PowerShell backtick-escaped quote marks falsely; a path ending in `\` does not mark | Codex P2 (#3), 1 LOW, 4 F2, Tooling F4, 6 note | CONFIRMED MED (false mark fails unsafe) | Fixed. Split in Python with the escape from `tool_name` |
| Failed build or test never marks (PostToolUseFailure) | 5 trace 4 | CONFIRMED MED (2.1.241 bundle: a non-zero shell exit throws; only `RZn` runs, and its payload carries `tool_input`) | Needs `settings.json`: see "Settings changes for the orchestrator". Hook comment and catalog row state the gap |
| Quadratic bash split, wrong "linear" comment | Tooling F2, 3 #1 | CONFIRMED MED (100 KB: 5,370 ms at RED, killed by `timeout 5`) | Fixed by the Python split: 1,508 ms, pinned in 7d |
| 7a lacks clear, re-arm and writer-reader handshake; exit status and stderr unchecked | 1 MED, 4 F1, Tooling F3, Codex P3 (#6) | CONFIRMED MED | Fixed. `stop_expect` requires rc 0 and empty stderr. Full cycle for every discovered Stop hook. Verification cleared by the real writer called as PowerShell. Logged review clears its marker |
| Non-final line hides a command (CRLF from Python `print`) | 5 #5, 4 F3 | CONFIRMED LOW (`./build.ps1` on a middle line: marked=0) | Fixed in the split. Middle-line rows in 7c and 7d. `validate-push.sh` comment corrected |
| `env DOTNET_NOLOGO=1 dotnet test` stopped marking (marked at base) | Tooling F4 | CONFIRMED LOW regression (base hook from `git show bec0389d`: marked=1; HEAD: 0) | Fixed. `env` dropped with the assignments; 7d row |
| A commit message quoting a trunk force push is refused | 1 LOW, 5 #8, Codex P2 (#4) | CONFIRMED LOW, kept by design | A gate cannot tell quoted or heredoc text from what `bash -c` or `bash <<EOF` runs. Pinned in 7c; the catalog names `git commit -F <file>` |
| Upgrade keeps markers the silent hooks wrote | Codex P2 (#5) | CONFIRMED LOW (main tree: `.verification-reminded` 11:51, `.verification-ran` 10:55) | Merge step for the orchestrator. It self-heals when each streak ends |
| Visibility row contradicts itself; `additionalContext` denied | 1 LOW, 5 #13, 4 F5b | CONFIRMED LOW | Fixed in `harness-facts.md`, `_stop_reminder.sh`, `test_hooks.sh` section 4 comment. ADR-011:52: needs Mike |
| Header comments say "soft reminder, not a hard block" | 1 LOW | CONFIRMED LOW | Fixed |
| CHANGELOG lacks #654 | 1 LOW, 4 F4 | CONFIRMED LOW | Fixed |
| `finish-branch` says the hook "warns on master/main" | 5 #14 | CONFIRMED LOW | Fixed; names both trunks |
| Facts only in the plan | 4 F5 | CONFIRMED LOW | `additionalContext` in harness-facts; HEAD and delete gaps in the catalog row; headless Stop in the catalog, marked unverified |
| 7c and 7d run every case twice | 3 #2 | CONFIRMED LOW for 7c | 7c applied (PowerShell repeats one case). 7d not applied: the hook now reads `tool_name` |
| CHANGELOG heading date 2026-09-24 against a 2026-09-25 commit | 4 F4 | FALSE POSITIVE | The sprint's entries share the 2026-09-24 heading; not a defect |
| How four simultaneous blocks merge | 5 trace 2 | UNVERIFIED | Agent 6 read `blockingErrors` aggregation in the 2.1.241 bundle [Likely]; live check A settles it |
| `.deep-review-reminded` carries across sessions | 5 #9 | NEEDS MIKE | Plan chose checkout-shared markers (plan 011:1247-1248); scoping by `session_id` is a design change |
| Trunk deletion (`--delete`, `:branch`) passes | 5 F2, Tooling F6, 6 note | NEEDS MIKE | D30 scoped the hook to force pushes; documented as a known gap |

### Agent 1: Standards

H1 to H6 applied (no C#). `test_hooks.sh` at review: 441 passed, 1 timing flake on
`check-changelog-changed.sh`, a hook this diff does not touch. Its HIGH, MED and five LOWs are in
the table. Its optional LOW (merge `mark-verification-run.sh`'s two PostToolUse groups) is a
`settings.json` change, listed below. Follow-ups carried to the list at the end.

### Agent 3: Efficiency

The commit's net per-call hook cost is strongly positive (the deleted `suggest-compact.sh` cost
185 ms on every tool call). Finding 1 (quadratic split) is superseded by the Python split.
Finding 2 is applied to 7c only. Findings 3 and 4 are follow-ups.

### Agent 4: Completeness

The tests, catalog, issue and CHANGELOG were present, with coverage gaps. F1 to F5 are resolved
as in the table. Orchestrator to-dos it raised are under "For the orchestrator".

### Agent 5: Data flow

17 flows. Trace 4 (PostToolUseFailure) is the one gap: it needs `settings.json`. Traces 5, 8, 13
and 14 are fixed or documented. Trace 9 needs Mike. Trace 12 (running sessions keep the deleted
`suggest-compact.sh` registration until restart) is a merge note.

### Agent 6: Design & Elegance

KEEP 1 (split at `; & |`), KEEP 2 (every refspec) and KEEP 3 (delete the dead quote strip) are
applied as the HIGH fix. KEEP 4 (delete the invisible `WARN_TARGET` warning) is behaviour-changing
and the plan said "leave it": needs Mike. KEEP 5 (one `Bash|PowerShell` group per shared hook) is
PRESERVING but touches `settings.json`: listed for the orchestrator.

### Tooling correctness

F1, F2, F3 fixed. F4 fixed for the escape and the `env` prefix. `timeout 900 dotnet test`
never marked at base either, so it is a follow-up. F5, F7 and F8 are follow-ups. F8 is
softened: 7a now loops over the Stop hooks it discovers in `settings.json`. If plan 020 deletes
`check-changelog-updated.sh`, its `stop_condition` arm is simply unused, and a new Stop hook with
no arm fails loudly.

## Action items

1. Orchestrator: apply the PostToolUseFailure registration below, add its 7c assertion, and
   recount the catalog.
2. Orchestrator, at merge: delete the stale `.claude/logs/.*-reminded` markers in the main tree,
   and restart running sessions (they still call the deleted `suggest-compact.sh`).
3. Mike: the NEEDS MIKE items below.
4. Mike: live checks A and B are still owed. Extend B with a tail
   (`git push --force origin bannerlord-1.5.x 2>&1 | Select-Object -Last 5` from PowerShell),
   and add a failing `dotnet test --filter` followed by a Stop once item 1 lands.

## Settings changes for the orchestrator

`.claude/settings.json` is config-protected, so none of these were made here.

1. **Required (Data flow trace 4, MED).** Append to `hooks.PostToolUseFailure`:
   ```json
   {
     "matcher": "Bash|PowerShell",
     "hooks": [
       { "type": "command", "command": ".claude/hooks/mark-verification-run.sh", "timeout": 5 }
     ]
   }
   ```
   Then add a check to the `VP_REG` block of 7c: `has('PostToolUseFailure', 'mark-verification-run.sh')`
   for both `Bash` and `PowerShell` (the matcher split on `|`). Recount the catalog header (29
   `settings.json` registrations, 34 total). Change the `mark-verification-run.sh` row's event to
   "PostToolUse and PostToolUseFailure (Bash, PowerShell)" and drop its "does not mark yet"
   sentence. Remove the last two lines of the hook's "pass OR fail" comment.
2. **Optional, PRESERVING (Agent 1 LOW, Agent 6 KEEP 5).** Merge the two PostToolUse groups for
   `mark-verification-run.sh` into one `"matcher": "Bash|PowerShell"` group. Move
   `validate-push.sh` out of the PreToolUse `Bash` group into its own `"Bash|PowerShell"` group,
   replacing the `PowerShell` group. Sections 2, 4b, 4c, 5 and 7c already accept the combined
   matcher. Recount the catalog afterwards.

## Improvements (Step 4)

APPLIED:
- `.claude/hooks/validate-push.sh`: per-command split (KEEP 1), every-refspec loop (KEEP 2), dead
  quote strip deleted (KEEP 3). Proven by 7c: 20 new rc=2 rows (the 2026-09-23
  trailing-comment form included) and 7 new rc=0 rows; the 14 original rows stay green.
- `tools/test_hooks.sh` 7c: the PowerShell pass repeats one case (Efficiency 2). PRESERVING: the
  hook never reads `tool_name`; the registration check and live check B cover PowerShell.
- `.claude/hooks/mark-verification-run.sh`: Python split. Behaviour for Bash commands is preserved
  (every original 7d row is green before and after). It supersedes Efficiency 1 and Tooling F2's
  "minimum" fix.

NOT APPLIED:
- `validate-push.sh` `WARN_TARGET` deletion (Agent 6 KEEP 4): behaviour-changing, and the plan
  said leave it. Needs Mike.
- Registration merges (Agent 6 KEEP 5, Agent 1 LOW): `settings.json` is protected; listed for the
  orchestrator.
- Efficiency 2 for 7d: disproved by this fix, because `mark-verification-run.sh` now reads
  `tool_name`.
- Efficiency 1's chunked bash loop: superseded by the Python split.
- `hook-authoring.md` is still 95 B over its 12,288 B cap (report-only warning; it was 207 B
  over at review). The remaining narrative, the frontmatter-registration row, is outside this
  diff.

FOLLOW-UP (pre-existing code; no issue filed, because filing a public issue needs Mike's go-ahead):
- `tools/test_hooks.sh:146`: the timeout census misses the last hook in each skill's frontmatter
  and prints 31 instead of 33 (Agent 1 F1, Tooling F5).
- `check-polearm-shield-parity.sh` and `notify-csharp-edit.sh` write exit-0 stderr, the class this
  plan fixed (Agent 1 F2). Needs Mike: convert or delete.
- The other nine PreToolUse gates are Bash-only, so a PowerShell call bypasses them (Data flow F3).
- `mark-verification-run.sh`: `timeout 900 dotnet test` and `dotnet.exe test` never mark, and an
  unquoted heredoc body line starting `dotnet test` marks (Data flow F4, Tooling F4).
- `_pybin.sh:136-137` reports a missing parser on exit-0 stderr (Tooling F7).
- Stop hooks read git state three times per Stop (Efficiency 3).
- 127 stale `/tmp/claude-tool-count-*` and `/tmp/claude-last-boundary-*` files from the deleted
  hook (Efficiency 4): Mike's machine, delete after merge.
- Running the hook suite writes a real serena unhealthy mark for 60 s (Agent 1 F4).
- `docs/reference/release-process.md:137-141` blames per-version muting for the v2.0.20 miss;
  #654 shows the reminder never arrived (Completeness U2).
- Branch 020 deletes `check-changelog-updated.sh`, whose reason still cites the per-session
  CHANGELOG rule D18 retires (Tooling F8, Agent 1 F3).

VERDICT: see the end of this report.

## Codex review

Source: `docs/reviews/raw/codex-adversarial-011-stop-reminders-and-trunk-guard-2026-09-24.md`
(gpt-6-astra, ultra, 122,619 tokens, complete). Codex made no writes and ran nothing. It traced
each claim to a source line and quoted the vendor and Microsoft docs for the harness and
PowerShell contracts.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P1 | HIGH | Yes | Reproduced: `...; Write-Output done` and `... bannerlord-1.5.x improve/example` returned rc 0. Fixed |
| 2 | P2 | MED | Yes | Reproduced in all four hooks by the new 7a continuation row. Fixed |
| 3 | P2 | MED | Yes | ``Write-Output "x`"; dotnet test"`` marked=1 at RED. Fixed |
| 4 | P2 | LOW | Partly | Real, but fail-safe, and a quote-aware gate would reopen `bash -c`. Kept, pinned, documented |
| 5 | P2 | LOW | Yes, lower | Real (stale marker in the main tree), self-healing. Merge step |
| 6 | P3 | MED | Yes, higher | The oracle gap hid Finding 2. Fixed |
| 7 | P3 | none | Yes (process) | The plan text predates D29, D30, D38, D41 and D42; the implementation follows the decisions. The orchestrator keeps the plan |

Known suspects: Codex marked 1 and 8 CONFIRMED and 9 PARTLY CONFIRMED, which agrees with this
review. Suspects 2 to 6 are UNVERIFIED execution history, which git cannot prove. 7 and 10 are
DISPUTED, and I agree: no MCM or default changed, and the gates are reachable.

**Confirmed bugs:** 1, 2, 3, 6, plus 4 and 5 as low-severity notes (above).
**False positives:** none.
**Design questions:** 4 (fail-safe over-blocking), kept.
**Things Codex missed:** PostToolUseFailure (Data flow), the quadratic split (Tooling), the
middle-line CRLF miss (Data flow), `--all`/`--mirror` (Tooling, Standards), the `env` prefix
regression (Tooling), the stale `finish-branch` line (Data flow).

### Phase 3e

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Tail or second refspec bypasses the only force-push guard | Logic error | Plan premise "harmless tokens"; the 2026-09-23 follow-up never became a test | 7c rows; lesson in build-tooling-workflow |
| 2 | Continuation Stop skips marker cleanup | Stale state / lifecycle | Plan prescribed the early return; tests never cleared a condition | 7a full-cycle rows; lesson in state-lifecycle-save |
| 3 | Bash escaping applied to PowerShell | Convention inconsistency | PowerShell registration reused a Bash parser; tests reused Bash strings | 7d per-shell rows; same build-tooling lesson |
| 6 | Stop tests ignore exit status and stderr | Other: test oracle | Shape-only assertions | `stop_expect` |

## AGENTS.md lessons (pending)

For "Lessons From Prior Reviews" (Phase 3h, consolidated later):
- **What Codex does well:** traced a marker through the harness's continuation cycle (the
  `stop_hook_active` cleanup gap) that five lenses called connected, and checked another shell's
  quoting against Microsoft's grammar.
- **Bugs Codex typically misses:** harness event semantics that need the installed CLI read (a
  failed command raises PostToolUseFailure, not PostToolUse), timing cliffs against a hook's
  registered timeout, and platform line endings (Python `print` writes CRLF on Windows).
- **No new false-positive pattern.**

## For the orchestrator

- `plans/README.md:27` still reads "TODO, unblocked".
- The plan's check A names `E:/repos/wt-011-stop-reminders`, which does not exist; the worktree is
  `E:\repos\taom-improve\wt-011`. Its section C (ruleset) is superseded by D29.
- `docs/reviews/codex-adversarial-011-stop-reminders-and-trunk-guard-2026-09-24.prompt.md` is
  untracked (sibling prompts are tracked); left as found.
- #654's only comment records D29 and D30; D38, D41, D42 and this review widened the scope.

## NEEDS MIKE

1. Block deleting a trunk (`git push origin --delete bannerlord-1.5.x`, `:bannerlord-1.5.x`)?
   D30 scoped the guard to force pushes, and no ruleset exists (D29).
2. Delete `validate-push.sh`'s stderr warning for a plain trunk push, which no one sees (Agent 6
   KEEP 4)?
3. Scope the Stop streak markers to a session (`session_id`) instead of the checkout?
4. ADR-011:52 restates Visibility without the Stop block channel (protected ADR).
5. `check-polearm-shield-parity.sh` and `notify-csharp-edit.sh` print to exit-0 stderr: convert
   to a visible channel, or delete?
6. File an issue for the nine Bash-only PreToolUse gates that a PowerShell call bypasses?

VERDICT: READY FOR COMMIT (pending the orchestrator's settings change in item 1 and the live
checks A and B). Step 4's convergence pass (one deep-reviewer over the fix diff) is owed: this
lead cannot spawn agents. Final suites: see below.

Final suites, run after the last edit: `bash tools/test_hooks.sh` printed "493 passed, 0 failed"
(442 at review, rc 0); `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` printed
"Failed: 0, Passed: 10629, Skipped: 2, Total: 10631" (base `bec0389d` contains `a39a9c86`).
`lint_docs.py --dash-base bec0389d`: 0 new dashes, 0 dead links; `--drift-only` rc 0;
`audit_claude_config.py --min MED`: HIGH 1, MED 1, the two known findings.

## Convergence

One reviewer over the review-fix commit `c80c4108` (`43e6780e..c80c4108`) reported four defects.
The review lead's second pass checked each against the code; all four held, none was a false
positive.

| # | Severity | Verdict | Proof and fix |
|---|---|---|---|
| D1 | MEDIUM | Confirmed | The quote-blind split at `;`, `&` and `\|` cut a quoted `-C`, `-c` or `-o` value holding one, separating `git` from `push`. Four new 7c rows (`-o "ci.skip;x"`, `-C "E:/R&D/TAOM"`, a `-c` helper with `;`, `-C "E:\a;b"`) failed with rc 0 before the fix. `validate-push.sh` now judges both the quote-blind segments and segments split outside quotes only (the `mark-verification-run.sh` splitter, escape chosen by `tool_name`), and blocks when either blocks. With jq and no Python the second view keeps whole lines, which over-block. Comment, catalog row and lesson corrected. |
| D2 | LOW | Confirmed | A token holding `<` or `>` was discarded whole, so `bannerlord-1.5.x>/dev/null` and `>&2` passed (also at `43e6780e`). The text before the first `<` or `>` is now judged as a normal argument unless it is empty, an fd number or `*`; a token ending in `<` or `>` still skips its target. Four 7c rows added, all rc 0 before the fix. |
| D3 | LOW | Confirmed | `check-verification-evidence.sh:7`, `check-version-tagged.sh:6` and `tools/test_hooks.sh` 7a still said the JSON block is the only Stop output Claude reads. Reworded to "the Stop channel TAOM uses", as `_stop_reminder.sh:8` does. |
| D4 | LOW | Confirmed | The section 4 comment said PreToolUse and PostToolUse hooks speak the PreToolUse protocol; the classification only requires valid JSON, covers PostToolUseFailure, and the `hookSpecificOutput` rule is `PRE_GATES`. Reworded to match. |

Ad hoc checks after the fix: the four D1 shapes, `bannerlord-1.5.x>$null` and
`bannerlord-1.5.x *>$null` return rc 2 under the PowerShell tool name; `git push --force origin
feature *>$null` and `... feature 2>&1 | Out-Null` return rc 0. A 130 KB payload with 10,000
segments took 2.7 s against 2.2 s for `c80c4108`, under the 5 s registration.

Suites after the last edit: `bash tools/test_hooks.sh` printed "501 passed, 0 failed" (493 before,
8 new 7c rows, all RED first); `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
printed "Failed: 0, Passed: 10629, Skipped: 2, Total: 10631"; `lint_docs.py --drift-only` rc 0.
One intermediate hook run failed section 8 twice with exit 124: `scan.sh` took 58 s standalone
against its 60 s bound under machine load, it is untouched by this diff, and the final run passed.

The orchestrator then applied the required settings change (PostToolUseFailure, matcher
`Bash|PowerShell`, `mark-verification-run.sh`, timeout 5); the 7c registration check fails against
the previous `settings.json` and passes against the new one, a 7d row shows a failure payload marks,
and `bash tools/test_hooks.sh` printed "506 passed, 0 failed".

## Final convergence

A second convergence pass over `c80c4108..18961c1e` reported four defects, each proven against a
patched copy of the hook. Three are applied; one is kept by decision.

| # | Severity | Outcome | Proof and fix |
|---|---|---|---|
| D1 | MEDIUM | Applied | An apostrophe in a comment or heredoc line (`# don't push to the trunk`, `Don't push yet`) opened a quote that never closed, so the quoted split glued the next line into its segment, and `judge_command` anchored on the first `push` word, found no `git` before it and returned; a following `git -C "E:/R&D/TAOM" push --force origin bannerlord-1.5.x` passed (rc 0) under both tools. The jq-only fallback failed the same way (`echo push && git -C "E:/R&D/TAOM" push --force ...`). The hook now anchors on the first `push` token with a `git` token before it. Two 7c rows, rc 0 before the fix; the jq-only case checked by hand with a `jq` shim and no Python (rc 0 before, 2 after), since the suite has no jq-only mode. The hook comment and the catalog row no longer claim that whole lines or the quoted split cannot under-block. |
| D2 | LOW | Kept | The same unclosed quote refuses a later, unrelated command (`# it's a feature branch`, then an ordinary push and a `git log` naming a trunk). It errs on the safe side, and the proposed fix (end a quote at a newline) would let a quoted value spanning lines cut `git` from `push` (`git -C "E:/R&D` newline `x" push --force ...` went from rc 2 to 0). The hook comment and the catalog row say so. |
| D3 | LOW | Applied | 7c ran every row but one under Bash only, so a hook using one escape for both tools passed it. A tool-tagged table now holds a PowerShell row (`git -c "user.name=a\" -C "E:/R&D" push ...`) and a Bash row (`git -c "user.name=a\" b" -C "E:/R&D" push ...`); each fails against the matching mutant and passes at HEAD. The stale "The hook never reads tool_name" comment is replaced. |
| D4 | LOW | Applied | Each segment was judged under both splits, and each push with no refspec spawned `git branch --show-current`, so 100 such lines took 7.9 s against the 5 s registration. The branch is now resolved once per run (and `CUR_BRANCH` is unset first, so the environment cannot preset it), and a segment already judged is skipped. A 7c timing row runs 100 distinct no-refspec push lines under a 4 s limit; HEAD's hook hit the row's 10 s kill (rc 124). |

Parity: the reviewer's `diff_vp.py`, run over all nine corpora (`cases_all.py`, which holds the 49
7c rows, and the other `cases_*.py`) under both tool names, compared HEAD's hook with the fixed
one on 596 payloads. The only verdict changes are the two D1 payloads under each tool, rc 0 to 2
(they appear in three corpora). `cases_cwd.py`, rerun from the trunk checkout, is unchanged.

Moved to plan 027 (PowerShell and parsing gaps), all rc 0 before and after this diff: glob and
DWIM refspecs (`'refs/heads/*'`, `'+refs/heads/*:refs/heads/*'`, `'refs/heads/bannerlord-*'`,
`HEAD:heads/bannerlord-1.5.x`); PowerShell braces glued to the command (`if ($true) {git push
...}`, `ForEach-Object {git ...}`); git named another way (`& 'C:\Program Files\Git\cmd\git.exe'`,
`GIT push`); an option value taken as the remote (`git push --force -o ci.skip origin` on a trunk);
Bash backtick substitution; the Bash `$'...'` quote the splitter does not model; the refusal of
`git push --force origin feature # bannerlord-1.5.x later`; and the unverified question whether an
interrupted `dotnet test` raises a PostToolUseFailure payload with `"is_interrupt": true` that marks.

Suites after the last code edit: `bash tools/test_hooks.sh` printed "511 passed, 0 failed" (506
before, five new 7c rows; the timing row took 419 ms); `dotnet test TAOM.Tests
-p:DisableModuleCopy=true -p:ModuleId=` printed "Failed: 0, Passed: 10629, Skipped: 2, Total:
10631"; `lint_docs.py --fail-on-drift` rc 0.
