---
paths:
  - ".claude/hooks/**"
---

# Hook Authoring Conventions

Loads when a `.claude/hooks/` script is being written or edited. The durable harness facts
(hook lifecycle, fail-open mandate, JSON output contract) live in `harness-facts.md`; this rule
holds the authoring-time conventions moved out of it (repo-reorg 2026-07-12).

## Mirror the sibling's FULL convention set (EMPIRICAL: TAOM 2026-05-29)

When you add a hook to an existing category (a Stop reminder, a PreToolUse gate, a PostToolUse logger), do NOT copy only the part you're focused on. Enumerate and consciously **match-or-deviate** on the sibling hooks' entire convention set:

| Convention | Where to copy it from | The 2026-05-29 miss |
|---|---|---|
| Detection (git state vs stdin JSON) | the nearest sibling in the same event | (got this right) |
| **Muting / idempotency** (early-exit when already-handled) | `check-deep-review.sh` checks the audit log before re-reminding | `check-verification-evidence.sh` shipped without muting → re-nagged on every Stop while `.cs` stayed dirty (MED) |
| **I/O preamble** (`INPUT=$(cat)` etc.) | copy a sibling's verbatim | `mark-verification-run.sh` hand-wrote `cat 2>/dev/null` + `printf`, diverging from 13 siblings (LOW) |
| Exit semantics (`exit 0` non-blocking vs `exit 2`/JSON `deny`) | the sibling in the same event | (got this right) |

**Root cause** (RCA `docs/reviews/rca-superpowers-enforcement-2026-05-29.md`): treating a sibling as a *detection* template instead of a *full behavioral* template — same shape as the C++-port hot-path miss (`feedback_native_port_hot_path_audit.md`). The fix is a pre-flight pass over the sibling's whole body, not just the lines you need.

## Git invocation forms hooks must handle

When writing a PreToolUse(Bash) hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch — substring matching `*"git commit"*` MISSES the following real-world forms (Codex review 2026-04-26 found this gap):

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

**Reference pattern** (used by `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, and `suggest-compact.sh`):

```bash
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac
```

**MANDATORY for any new hook that detects git commits.** Codex review #29 caught `suggest-compact.sh` shipping in `79350f2` with a bare `*"git commit"*` substring matcher — the same recursion-risk class codified after review #28. The prevention rule existed but wasn't applied to its own first user.

When you write a NEW hook (or add commit detection to an existing one), grep for `git commit` substring matches in the diff before commit. If you find one that's NOT using the two-stage pattern above, that's a regression — fix before shipping. The `/skill-stocktake` checklist now includes this check.

## Amend exemptions in pre-commit hooks (recursion-risk pattern)

Do NOT blanket-skip `git commit --amend` in pre-commit hooks. `amend` is commonly used as a workflow ("oops, forgot a file, amend it in") — that's exactly the case the hook needs to catch. Codex review 2026-04-26 caught this as prevention theater: both `check-changelog-changed.sh` and `check-claude-files-tracked.sh` originally exempted `--amend`, defeating the very gates they were supposed to enforce.

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

The v1.4.7 → v1.4.8 bump proved it. **What is observed:** on 2026-08-10 the hook fired with
`source=startup` and printed branch, stashes and commits — but no drift banner, with the game on
v1.4.8 and the pin on v1.4.7. Not a race; the session transcript's birth time was 47 minutes after
the update finished.

**What is proven about the mechanism:** the pre-fix code had a silent-failure mode. It built its path
as `"${BANNERLORD_GAME_DIR:-<literal>}/bin/..."`, and the `:-` form substitutes the literal only when
the variable is **unset or empty** — so a variable that is *set but does not resolve in the hook's
environment* sails past it, the `-f` test goes false, and the whole block falls through without a
word. Exporting a bogus `BANNERLORD_GAME_DIR` reproduces total silence in one command.
`.claude/settings.json` does not define the variable, so the hook inherits whatever the harness
process carries.

**What is NOT proven:** that this was the actual trigger that morning. The same variable resolves
fine from an interactive shell, so the hook's environment must have differed in some way that was
not captured. Treat the mechanism as demonstrated and the specific trigger as undetermined — the
lesson does not depend on which it was, because a guard with *any* silent-failure mode is the defect.

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

Measured on 2026-08-31, after a well-intentioned pass added timeouts to all 27 registrations:

| Hook | Registered | Actual runtime | Result |
|---|---|---|---|
| `check-moduledata-validation.sh` | 5 s | **27.0 s** | killed every run; broken-ref / landless-culture / duplicate-id gate dead |
| `check-doc-config-drift.sh` | 5 s | **7.8 s** | killed every run; gate dead while real drift stood in the tree |
| `check-polearm-shield-parity.sh` | 5 s | 2.9 s + overhead | killed intermittently |

**The rules that follow from it:**

| Do | Why |
|---|---|
| **Time the hook's slow path before you pick a number.** `time <the exact command the hook runs>` | The fast path is the path you will not be debugging. A guess here is a dead gate. |
| **Bound external work INSIDE the script**, under the registered timeout: `timeout -k 2 45 "$PY" tools/x.py` | Keeps the overrun inside the hook, where it can still print something. The registered timeout becomes a backstop, not the budget. |
| **Handle rc 124 explicitly, and never as a pass.** Emit `permissionDecision: "ask"`, or write to stderr for an advisory hook | An overrun is an infrastructure fault. Fail open (never hard-block on your own bug) but say so, per the fail-open-not-fail-silent rule above. |
| **Use `-k`.** Bare `timeout N` sends SIGTERM and then WAITS | Against a process that ignores SIGTERM (exactly the Store-alias case) the guard itself hangs. |
| **Check skill-frontmatter registrations too** | The 2026-08-31 pass covered all 27 in `settings.json` and missed all 5 in `freeze/SKILL.md` + `investigate/SKILL.md`, which inherit the **600 s** default. |

`bash tools/test_hooks.sh` enforces every line of this: it fails if a registration has no
timeout, if a hook runs an external tool with no inner bound, or if an inner bound is not
strictly below its registered timeout. Run it before committing anything under `.claude/hooks/`.

## Never spell it `python3` (EMPIRICAL: TAOM 2026-08-31)

On the dev machine `python3` resolves only to
`C:\Users\mikew\AppData\Local\Microsoft\WindowsApps\python3`, a Microsoft Store App Execution
Alias. Run from Git Bash it prints nothing, never exits, and **ignores SIGTERM**. Guarding with
`command -v python3` does not help: it succeeds, because a file really does exist at that name.
That is what wedged every JSON-parsing hook, and with no `timeout` on the registrations each
Bash call paid a 600 s PreToolUse batch plus a 600 s PostToolUse batch, which is the 20.0-minute
stall in the transcripts.

**Inside a hook:** `source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"` at the TOP, above the
first `"$PYBIN"` use, then honour `[ -n "$PYBIN" ] || { echo '{}'; exit 0; }`. Putting the
`source` below the first use is not a style nit: `validate-push.sh` shipped that way on
2026-08-31 and its force-push block was unreachable.

**Outside a hook:** just write `python`. It resolves to real CPython here and is the repo
convention. A portable candidate list may still include `python3` provided the loop rejects any
resolved path matching `*[Ww]indows[Aa]pps*` first.

**Only `.github/workflows/` may use bare `python3`**: CI is Linux, where it is the correct
spelling and the alias cannot exist.

## Log-appending hooks: size-cap rotation (EMPIRICAL: TAOM 2026-07-12)

A hook that appends to a `.claude/logs/` file must size-cap-and-rotate it (see `session-stop.sh` /
`log-agent.sh`: `wc -c` check → `mv -f "$LOG" "$LOG.1"`). Unrotated logs grow unbounded AND silently
break sibling hooks that grep them (the pre-rotation `agent-audit.log` permanently satisfied
`check-deep-review.sh`'s reminder with months-old entries).
