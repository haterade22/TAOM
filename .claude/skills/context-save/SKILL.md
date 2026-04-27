---
name: context-save
description: Snapshot current working context (git state, in-flight tasks, key decisions, modified files) so a future session or post-compact resumption can pick up without losing progress. Pair with /context-restore.
allowed-tools:
  - Bash
  - Read
  - Write
  - AskUserQuestion
---

# /context-save — Snapshot Working Context

Saves a structured snapshot of the current session's state to `.claude/state/context/` so a future session (or this session after `/compact`) can re-engage without re-reading everything.

Adapted from [garrytan/gstack/context-save](https://github.com/garrytan/gstack/tree/main/context-save) — TAOM version drops the gstack-specific telemetry/sync scaffolding and writes plain markdown.

## When to Use

- Before `/compact` if you want to preserve task state beyond what compaction keeps
- End of a working session, before stepping away
- Switching between unrelated work streams (e.g., TAOM → another repo → back)
- After hitting a non-trivial decision you don't want to re-derive next session

## What gets captured

Run via Bash to gather:

```bash
SLUG=$(date +%Y%m%d-%H%M%S)
[[ -n "${1:-}" ]] && SLUG="${SLUG}-${1// /-}"  # optional descriptor

STATE_DIR="${CLAUDE_PROJECT_DIR}/.claude/state/context"
mkdir -p "$STATE_DIR"
SNAP="$STATE_DIR/$SLUG.md"

{
    echo "# Context Snapshot — $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo
    echo "## Git state"
    echo "- Branch: \`$(git branch --show-current 2>/dev/null || echo unknown)\`"
    echo "- HEAD: \`$(git log -1 --oneline 2>/dev/null || echo unknown)\`"
    echo
    echo "### Staged"
    git diff --cached --name-only 2>/dev/null | sed 's/^/- /'
    echo
    echo "### Unstaged"
    git diff --name-only 2>/dev/null | sed 's/^/- /'
    echo
    echo "### Untracked"
    git ls-files --others --exclude-standard 2>/dev/null | sed 's/^/- /'
    echo
    echo "## Recent commits (last 5)"
    git log -5 --oneline 2>/dev/null | sed 's/^/- /'
} > "$SNAP"

echo "Snapshot started at: $SNAP"
```

Then **append** the agent-supplied narrative — these are the parts the user / agent know that git doesn't:

1. **In-flight task:** what feature/fix is being worked on (1-3 sentences). If a GitHub issue is open, link it.
2. **Decisions made this session:** bullet list of choices that shaped the current state and shouldn't be re-litigated. Include the *why*, not just the *what*.
3. **Open questions / blockers:** what's NOT yet decided that the next session needs to address.
4. **Files in flight:** which files are mid-edit and why (one line each).
5. **Next concrete step:** the single next action a fresh session should take.
6. **Anything that surprised you this session:** the kind of thing future-you would forget. (Optional — but the highest-value field when present.)

Use `AskUserQuestion` to gather any of these the user can fill faster than you can infer.

## Output format (what gets written)

```markdown
# Context Snapshot — 2026-04-26T15:30:12Z

## Git state
- Branch: bannerlord-1.3.15
- HEAD: 2c4d414 process(.claude): retroactive RCA on Codex review #28
### Staged
- (none)
### Unstaged
- Main/Features/CareerSystem/Services/CareerService.cs
### Untracked
- (none)

## Recent commits (last 5)
- 2c4d414 process(.claude): retroactive full RCA on Codex review #28 + preventives
- 5fd9719 fix(.claude): close prevention-theater holes flagged by Codex (#92)
- ...

## In-flight task
Implementing Tier 2 + Tier 3 picks from Claude Code ecosystem review (#93). 6 of 8 picks done; refactoring-specialist subagent next.

## Decisions made this session
- Skipped #8 (persistent task DAG) — TodoWrite is sufficient for solo dev
- Used `effort: high` only on /deep-review (verified docs allow it)
- Dropped gstack telemetry scaffolding from /context-save port (not relevant solo)

## Open questions / blockers
- (none — clear runway)

## Files in flight
- `Main/Features/CareerSystem/Services/CareerService.cs` — mid-investigation of Codex finding on stale buff cache; not yet modified

## Next concrete step
Port refactoring-specialist subagent to .claude/agents/, run /context-budget to confirm headroom impact, then commit.

## Anything that surprised you this session
The amend bypass (Codex review #28) was a real prevention-theater finding. Now codified in harness-facts.md "Amend exemptions" section so the same mental model error can't recur.
```

## Storage

- Snapshots live at `.claude/state/context/<timestamp>[-<slug>].md`
- Directory is gitignored (snapshots are local working state, not part of the repo)
- Naming convention: ISO-style timestamp first so `ls` orders chronologically
- Old snapshots are NOT auto-pruned — clean up manually with `rm` or via /context-restore's "clear" mode

## Pair with

- `/context-restore` — load the most recent (or named) snapshot
- `/compact` — actual context compression. `/context-save` complements it: compact reduces in-context tokens, context-save persists state OUT of context for later retrieval.

## Notes

- This skill writes ONE file. It does not modify your git tree, settings, or memory.
- If you have an active `/freeze` boundary, the write goes into `.claude/state/context/` which is OUTSIDE most freeze boundaries by default — but if you've frozen to `.claude/`, the write will be blocked. Run `/unfreeze` first or widen the boundary.
