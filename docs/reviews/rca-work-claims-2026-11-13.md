# RCA: work claims across sessions (#1248)

Findings from the review of #1248: `tools/work_claims.py`, the claim list in `session-start.sh` and the cross-worktree match in `record-session-edit.sh`. The change touches hooks and a tool, not C# or XML, so it ran as tier 2: the data flow and tooling lenses at `high`. Codex did not run: the maintainer chose no Codex this session. No lens reported a CRITICAL or HIGH, so no second wave ran. Each row was read again against the files before it was fixed.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Tooling M1: a claim the session registry cannot confirm (no row for the pid, or a row that names another session) returned exit 0, and every other session read it as ended | Silent success | The tests built a registry row for every session, so the unconfirmed case never ran | `claim` returns 4 UNCONFIRMED with the reason, and `list` with no registry folder returns 4 and prints UNCHECKED; `test_a_claim_the_registry_cannot_confirm_is_reported`, `test_list_without_a_registry_is_unchecked` |
| 2 | MED | Tooling M2, data flow 15: no hook test covered `ss_claims`, though the feature doc said both hooks had one | Untested consumer | It was tested by hand, and the tool had no seam for a fake registry | `TAOM_CLAIMS_REGISTRY` names the registry; a `test_hooks.sh` block with two sleeping processes as live sessions checks the other session's line, the own claim left out, and UNCHECKED |
| 3 | MED | Data flow 5, tooling L4: AGENTS.md tells every AI client to claim, but only a Claude session has the id a claim needs, and nothing said what exit 2 means for Codex | A rule wider than its tool | The tool was written from the Claude environment | The row and the doc say that a client with no Claude session runs `check`, which needs none |
| 4 | LOW | Data flow 2: the claim list did not run on `resume` or `/clear`, the moment a session most often starts its next task | Missing trigger | `session-start.sh` ran its body on startup only, and the claims followed that | `resume` and `clear` run the claim list and nothing else; the hook test runs both |
| 5 | LOW | Data flow 10: the hook note named the other session by its id prefix, and the claim list by its name only, so a reader could not match them | Two names for one thing | Each text was written in its own file | A claim line shows the name and the first 8 characters of the id |
| 6 | LOW | Data flow 11: a twin-only note opened with "another session edited ...: X edited", and a combined note said "once per file and session" twice | Text built from two templates | The combined case was tested for its content, not read whole | The twin text stands alone, and the sentence comes once |
| 7 | LOW | Tooling L2: `pid_running` read an elevated process that refuses the query as not running, and the default `c_int` result type could cut a 64-bit handle | Windows API use | The probe ran against normal sessions only | Access denied (error 5) reads as running; the handle types are set |
| 8 | LOW | Tooling L3: an I/O error in the store (a file held open by another process) exited 1 with a traceback | Undocumented exit | Only the happy path and the held path were tested | `main` catches `OSError` and exits 1 with one line; `test_a_store_that_cannot_be_written_exits_1` |
| 9 | LOW | Tooling L5: `check` of your own claim said "free" | Wrong message | `check` asked only whether another session holds the key | It says "held by you"; `test_check_of_your_own_claim_says_you_hold_it` |
| 10 | LOW | Data flow 3, 4 and 12, tooling L6: the docs said a reused pid is never the same session, listed two exit codes, said a step "renews" a claim (claims have no lease), and did not say that `#1245: notes` is another key than `#1245` | Docs ahead of the code | The docs were written from the design | Each line corrected in the tool docstring and `work-claims.md` |

## Not applied

- Tooling L1, data flow 16: two sessions that claim one free key in the same few milliseconds can both get exit 0. A guard for a race this rare needs the maintainer's word (`simplicity-criterion.md`); the cheap form is `os.link` for a fresh claim. `work-claims.md` "Limits" states it.
- Tooling L2, second half: a stale registry row and a pid reused by a non-Claude process keep a claim live. Comparing the row's `procStart` with the process start time would close it. FOLLOW-UP no issue: every registry row read today belongs to a running process, and `work-claims.md` states the limit.
- Tooling L6, second half: a payload path in Git Bash form (`/e/...`) matches nothing in the ledger. The fix changes the shared ledger format in `_session_ledger.sh`. FOLLOW-UP no issue: this was so before #1248, and the Edit and Write tools send Windows paths.
- Data flow 9: `check-verification-evidence.sh` reads two ledger folders while the cross-worktree note reads every worktree's. FOLLOW-UP no issue: this was so before #1248; `work-claims.md` "Limits" states it.

## Why each agent missed these

- Rows 1 and 2 needed a registry that does not match: the builder's tests and hand run used the live registry, where every row matched.
- Row 3 needed a reader from another client. The data flow lens traced the AGENTS.md row to Codex, which has no Claude environment.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/build-tooling-workflow.md`.
