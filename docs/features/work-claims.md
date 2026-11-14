# Work Claims

## Overview

Each session claims the task or sub-task it starts, so a second session that wants the same work
finds out before its first edit. A claim names the session, its worktree, its branch, the start time
and a note. The claims live in the git common folder, which every worktree of the checkout shares.

## Why This Exists

On 2026-11-13 two sessions fixed the same red trunk test (#1245) at the same time. One session
worked in a sibling worktree and merged the fix as #1247. A session that started later ran the same
process in the main checkout: it added the same two `<LanguageFile>` lines to the 12
`language_data.xml` files and seeded the same rows. Nothing told either session about the other, and
the second session's work had to be dropped (#1248).

- **Before:** no step made a session say what it worked on, so a later session could not see that a
  task already had an owner.
- **`record-session-edit.sh` before:** it compared absolute paths, so the same repo file in two
  worktrees never matched. It also cannot see a file that a script writes through Bash.

## The rule

AGENTS.md "Before each task" (the maintainer's rule, 2026-11-13). It applies to a brand new session
before its first task, and to a running session before each new task or sub-task in its list.

1. **Deconflict.** Run `python tools/work_claims.py claim <issue or key> --note "<what>"`.
   - Exit 0: the work is yours. A claim you already hold keeps its start time.
   - Exit 3 (HELD): another live session owns it. Do not start. Ask that session (SendMessage) or
     the maintainer.
   - Exit 4 (UNCONFIRMED): the claim was written, but the session registry does not name your
     session, so other sessions read it as ended. Tell the maintainer, and name the task in your
     messages to other sessions.
   - Exit 2 from a client with no Claude session (Codex, for example): run `check <key>` instead. It
     needs no session; exit 3 still means stop and ask.
   - A sub-task gets its own key when it is its own work item (its own issue, or a red test). A step
     inside the same claimed item needs no new claim.
   - Run `python tools/work_claims.py release <key>` after the push.
2. **Relevance.** Ask whether the result you just got back matters to the job in hand. If it does
   not, go on to the next step. Do not start a side task for it; record it as a follow-up if it must
   not be lost. The check never skips a step the rules require (a test, a review gate, `/verify`):
   those always matter to the job.

The rules exist to save usage and time. A claim is one tool call, and the relevance check is one
question with no tool call. One duplicated task (#1245) cost a whole session's work.

## How it works

| Piece | What it does |
|---|---|
| `tools/work_claims.py` | `claim`, `check`, `release`, `list [--others] [--brief] [--session ID]`. A key is an issue number alone (`1245`, `#1245`, `issue-1245` are one key; `#1245: notes` is another) or a short name, folded to a safe file name. Each claim is one JSON file in `<git common dir>/taom-work-claims/`, written through a temporary file and a rename. A claim line names the session and the first 8 characters of its id, the form the hook notes use. Exit codes: 0 done, 1 the store could not be written or read, 2 usage, an unusable key, no session or no git checkout, 3 held, 4 unconfirmed (or, for `list`, no registry). `TAOM_CLAIMS_REGISTRY` names another registry folder, for tests. |
| Session identity | `CLAUDE_CODE_SESSION_ID` and `CLAUDE_PID` from the environment, and the name from `~/.claude/sessions/<pid>.json`. The tool reads only `<pid>.json`, never the `.key` files there. |
| Liveness | A claim is live while the registry file of its pid names its session and that process runs. A claim of a session that ended is taken over with a note. A pid that a new Claude session reuses is not the same session, because its registry row names another session id. A pid that a non-Claude process reuses after a crash that left the row behind reads as live until the row goes. An elevated process that refuses the query (access denied) reads as running. |
| `session-start.sh` | On startup, after a compaction, on a resume and on a `/clear`, prints the `DECONFLICT:` instruction and one `CLAIM:` line per live claim of another session. It fails open: a spent probe budget or a tool error prints `UNCHECKED`. |
| `record-session-edit.sh` | Also matches the same repo-relative path in another worktree of the checkout, in this ledger folder and in each worktree's own `.claude/logs/sessions`. That note says the two sessions may be doing one task twice and names `work_claims.py list`. One `git worktree list` call per edit, bounded to 2 s. |

## Limits

- A claim is only as good as the session that makes it: nothing blocks an edit without a claim. The
  session-start list and the cross-worktree note are the backstops.
- The cross-worktree note sees Edit and Write only. A file that a script writes through Bash is in no
  ledger, which is why the claim comes first.
- Claims are local to one machine and one checkout. The laptop and the desktop do not see each
  other's claims.
- Two sessions that claim one free key within the same few milliseconds can both get exit 0. No lock
  guards it: a guard for a race this rare needs the maintainer's word (simplicity criterion).
- `check-verification-evidence.sh` (Stop) still reads two ledger folders, while the cross-worktree
  note reads every worktree's. A session started in one worktree that edits C# in another can draw
  a Stop reminder there for that file. This was so before #1248.

## Key Files

| File | Purpose |
|------|---------|
| `tools/work_claims.py` | The claim store and the command line |
| `tools/tests/test_work_claims.py` | Unit tests |
| `.claude/hooks/session-start.sh` | `ss_claims`: the claim list at start, after a compaction, on a resume and on a `/clear` |
| `.claude/hooks/record-session-edit.sh` | Cross-worktree path match |
| `tools/test_hooks.sh` | Hook tests for both hooks |

## Testing

- `python -m unittest tools.tests.test_work_claims`
- `bash tools/test_hooks.sh` (the record-session-edit cross-worktree cases use a sandbox repo with
  one worktree)

The review and its fixes: [rca-work-claims-2026-11-13.md](../reviews/rca-work-claims-2026-11-13.md).

## How to verify in game

Not applicable: this is harness tooling for AI sessions and changes nothing the game loads.
