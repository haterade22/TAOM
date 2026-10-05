---
paths:
  - ".claude/hooks/**"
---

# Hook Authoring Conventions

Loads when a `.claude/hooks/` script is being written or edited. The durable harness facts
(hook lifecycle, fail-open mandate, JSON output contract) live in `harness-facts.md`; this rule
holds the authoring-time conventions. The incidents behind them are lessons in
`docs/reviews/lessons/build-tooling-workflow.md`, named below.

## Mirror the sibling's FULL convention set (EMPIRICAL: TAOM 2026-05-29)

When you add a hook to an existing category (a Stop reminder, a PreToolUse gate, a PostToolUse logger), do NOT copy only the part you're focused on. Enumerate and consciously **match-or-deviate** on the sibling hooks' entire convention set:

| Convention | Where to copy it from | Past miss |
|---|---|---|
| Detection (git state vs stdin JSON) | the nearest sibling in the same event | (got this right) |
| **Muting / idempotency** (early-exit when already-handled) | `check-deep-review.sh` checks the audit log before re-reminding | `check-verification-evidence.sh` shipped without muting → re-nagged on every Stop while `.cs` stayed dirty (MED) |
| **I/O preamble** (`INPUT=$(cat)` etc.) | copy a sibling's verbatim | `mark-verification-run.sh` hand-wrote `cat 2>/dev/null` + `printf`, diverging from 13 siblings (LOW) |
| Exit semantics (`exit 0` non-blocking vs `exit 2`/JSON `deny`) | the sibling in the same event | (got this right) |
| **Output channel** (what Claude actually reads) | Stop: `_stop_reminder.sh`; PreToolUse: `block-dangerous-git.sh` (`hookSpecificOutput`); PostToolUse: `check-polearm-shield-parity.sh` (`additionalContext`) | the four Stop reminders wrote to stderr, which Claude never sees, until plan 011; the polearm gate did the same until 2026-09-29 |

The fix is a pre-flight pass over the sibling's whole body, not just the lines you need (lesson "Authoring a new hook: mirror the sibling's FULL convention set").

## Git invocation forms hooks must handle

When writing a PreToolUse hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch: substring matching `*"git commit"*` MISSES the following real-world forms:

| Form | Purpose | Handled by `*"git commit"*` substring? |
|------|---------|----------------------------------------|
| `git commit` | Bare commit | YES |
| `git commit -m "msg"` | Commit with message | YES |
| `git commit --amend` | Amend (must NOT blanket-skip — see "amend exemptions" below) | YES |
| `git commit -F file.txt` | Commit with message file | YES |
| `git -C /path commit` | Run as if from /path (no leading `cd`) | NO — needs `*"git -"*" commit"*` |
| `git -c key=val commit` | One-time config override | NO — same |
| `git --git-dir=/path commit` | Operate on a specific git-dir | NO — would need a separate pattern |
| `git commit-tree` | Plumbing — DIFFERENT command, must NOT match | YES (false positive) — needs explicit `*"git commit-"*` rejection |
| `git commit-graph` | Plumbing — same | YES (false positive) — same |

**Reference pattern** (used by `check-claude-files-tracked.sh`):

```bash
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac
```

**MANDATORY for any new hook that detects git commits** (review 29 in `docs/reviews/REVIEW-LOG.md` found a bare matcher after the rule was written).

When you write a NEW hook (or add commit detection to an existing one), grep for `git commit` substring matches in the diff before commit. If you find one that's NOT using the two-stage pattern above, that's a regression — fix before shipping. The `/skill-stocktake` checklist now includes this check.

**Both shell tools (plan 027).** Register a git gate in the `Bash|PowerShell` matcher group and
read its command with `COMMAND=$(taom_hook_command posix <gate>)` (`_pybin.sh`), never from
`tool_input.command` alone: it hands PowerShell back as Bash text, so the forms above cover both
shells, and it names git `git` when it is called by a path or in capitals. The PowerShell forms it
resolves are listed in `docs/reference/hooks-catalog.md` "Both shell tools" and pinned by
`tools/tests/test_shellwords.py`; `tools/test_hooks.sh` 7e fails a listed gate that does not read
through it. `validate-push.sh` is the one exception: it reads and judges the push in one Python run,
`_shellwords.py verdict` (#680), and 7e checks that call instead.

## Amend exemptions in pre-commit hooks (recursion-risk pattern)

Do NOT blanket-skip `git commit --amend` in pre-commit hooks. `amend` is commonly used as a workflow ("oops, forgot a file, amend it in"), which is exactly the case the hook needs to catch (two gates exempted it until the 2026-04-26 Codex review, `docs/reviews/REVIEW-LOG.md`).

Two correct patterns depending on what the hook checks:

| Hook checks | Correct amend handling |
|-------------|------------------------|
| Files in the commit's diff (e.g., is CHANGELOG.md staged?) | Compute the **post-amend file set** as `staged ∪ HEAD` and apply the same gate. If CHANGELOG was already in HEAD's diff, it's still in the post-amend commit — the gate correctly allows. |
| Working-tree state (e.g., is a file gitignored?) | Don't exempt amend at all. Working-tree state is amend-independent — a gitignored file on disk is just as broken in an amended commit as in a fresh one. |

## A detection hook must fail open, but NEVER fail silent (EMPIRICAL: TAOM 2026-08-10)

`harness-facts.md` mandates that a hook's own bug must never block the user. That is about *gating*.
For a hook whose job is to **detect and warn**, fail-open is only half the contract — because for
those, **no output is itself a claim**. A drift check that prints nothing is read as "no drift", not
as "never ran".

The v1.4.7 to v1.4.8 bump printed no drift banner (2026-08-10): a path built as
`"${BANNERLORD_GAME_DIR:-<literal>}/..."` covers an unset or empty variable, never a set but wrong
one, and a guard with *any* silent-failure mode is the defect (lesson "A fail-open guard whose
failure mode is silence").

**When writing or reviewing a detect-and-warn hook:**

| Do | Why |
|---|---|
| Probe candidates in order and **always** fall through to the known-good literal | `:-` covers unset/empty, not *wrong*. A set-but-broken value is the common case, not the rare one. |
| When no candidate resolves, print an explicit **"unchecked, not absent"** line | Silence is indistinguishable from a clean result. Name which inputs were tried. |
| Ask "what does this hook print when its input is missing?" before shipping | If the answer is "nothing", the hook has no failure signal at all. |
| Test the broken-input path, not just the happy path | Export a bogus value and run the hook. The 2026-08-10 bug reproduced in one command. |

Still `exit 0`, still never blocks — but loud about not knowing. Applies to `session-start.sh`'s
drift/stash/worktree checks, `check-doc-config-drift.sh`, `mcp-health-check.sh`, and any future
hook whose value is the warning it emits.

## A timeout must be measured against the SLOW path (EMPIRICAL: TAOM 2026-08-31)

A harness timeout is a **kill**, and a kill cannot speak: the harness discards a timed-out
hook's output and surfaces nothing. So for a gate, "killed" and "passed cleanly" are the same
observable event. A timeout sized against the fast path does not make a hook safe, it makes the
gate **silently dead**, which is strictly worse than the hang it replaced because the hang was
at least visible.

On 2026-08-31 a pass that added timeouts to all 27 registrations killed two gates on every run
(lesson "A gate the harness kills is not a gate").

**The rules that follow from it:**

| Do | Why |
|---|---|
| **Time the hook's slow path before you pick a number.** `time <the exact command the hook runs>` | The fast path is the path you will not be debugging. A guess here is a dead gate. |
| **Bound external work INSIDE the script**, under the registered timeout: `timeout -k 2 45 "$PY" tools/x.py` | Keeps the overrun inside the hook, where it can still print something. The registered timeout becomes a backstop, not the budget. |
| **Handle rc 124 explicitly, and never as a pass.** Emit an `ask` decision under `hookSpecificOutput` (`harness-facts.md` "PreToolUse output contract"), or, for an advisory hook, use its event's visible channel (`harness-facts.md` "Visibility"; exit-0 stderr reaches no one) | An overrun is an infrastructure fault. Fail open (never hard-block on your own bug) but say so, per the fail-open-not-fail-silent rule above. |
| **Use `-k`.** Bare `timeout N` sends SIGTERM and then WAITS | Against a process that ignores SIGTERM (exactly the Store-alias case) the guard itself hangs. |
| **Count the bound from the script's first command** when the work before it varies (#680) | `validate-push.sh` reads `EPOCHREALTIME` in its first command and gives its judge what is left of 3.0 s, so an overrun asks after about 3.3 s and its 10 s registration is only a backstop; a slow `_pybin.sh` probe asks at once. |
| **Check skill-frontmatter registrations too** | The 2026-08-31 pass covered all 27 in `settings.json` and missed all 5 in `freeze/SKILL.md` + `investigate/SKILL.md`, which inherit the **600 s** default. |

`bash tools/test_hooks.sh` enforces this: no registration without a timeout, no external tool
without an inner bound below it, (4b) every PreToolUse gate answers a commit inside 80% of its
registration, and (7j) every registration is anchored on `"$CLAUDE_PROJECT_DIR"` (a relative one
dies after a `cd`, #690). Run it before committing `.claude/hooks/`.

## Prove a gate live (EMPIRICAL: TAOM 2026-09-23, #647)

A gate is done when a real tool call it must stop has been shown stopped: the harness refused
it, not a test read the hook's output: nine gates once passed every test while the harness ignored
their decision (`docs/reviews/rca-adr011-batch1-2026-09-23.md`).
Test the inputs that go missing too: a non-ASCII command, a multi-line one.

## Never feed hook text through a here-string (EMPIRICAL: TAOM 2026-09-26, #681)

Never feed unbounded text through `<<<` or an expanding here-document (Git Bash 5.3 hangs forever on a document of 65,537 to 65,664 bytes, text of 65,536 to 65,663 plus the newline a here-string adds, and a killed gate fails open): split it under `set -f` with `set -f; IFS=$'\n'; A=($X); IFS=$' \t\n'; set +f` and a `for` loop, or `set -f; T=($X); set +f` for words, never `< <(printf ...)`, which forks per call; `tools/test_hooks.sh` 4e enforces it in every script a registration runs, comments included, so name the construct in words there.

## Never spell it `python3` (EMPIRICAL: TAOM 2026-08-31)

On the dev machine `python3` resolves only to
`C:\Users\mikew\AppData\Local\Microsoft\WindowsApps\python3`, a Microsoft Store App Execution
Alias. Run from Git Bash it prints nothing, never exits, and **ignores SIGTERM**. Guarding with
`command -v python3` does not help: it succeeds, because a file really does exist at that name.
It wedged every JSON-parsing hook on 2026-08-31 (the `command -v` lesson).

**Inside a hook:** `source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"` after any raw-payload
prefilter and above the first `"$PYBIN"` use, then honour
`[ -n "$PYBIN" ] || { echo '{}'; exit 0; }` (`validate-push.sh` gives its coarse answer instead,
#680). Below the first use it leaves `PYBIN` empty: `validate-push.sh`'s force-push block was
unreachable that way on 2026-08-31.

**Outside a hook:** just write `python`. It resolves to real CPython here and is the repo
convention. A portable candidate list may still include `python3` provided the loop rejects any
resolved path matching `*[Ww]indows[Aa]pps*` first.

**No file may use bare `python3`**: the Linux GitHub CI workflows, where it was the correct
spelling, were removed on 2026-10-05.

## Log-appending hooks: size-cap rotation (EMPIRICAL: TAOM 2026-07-12)

A hook that appends to a `.claude/logs/` file must size-cap-and-rotate it (see `session-stop.sh` /
`log-agent.sh`: `wc -c` check → `mv -f "$LOG" "$LOG.1"`). Unrotated logs grow unbounded AND silently
break sibling hooks that grep them (the pre-rotation `agent-audit.log` permanently satisfied
`check-deep-review.sh`'s reminder with months-old entries).
