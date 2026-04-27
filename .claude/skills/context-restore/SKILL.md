---
name: context-restore
description: Load a snapshot saved by /context-save and present it so this session can pick up where the last one left off without re-deriving decisions.
allowed-tools:
  - Bash
  - Read
  - AskUserQuestion
---

# /context-restore — Restore a Saved Snapshot

Reads the most recent (or named) `/context-save` snapshot and presents it to the agent + user so work resumes without losing the decisions and in-flight state captured before.

Adapted from [garrytan/gstack/context-restore](https://github.com/garrytan/gstack/tree/main/context-restore).

## When to Use

- Start of a fresh session that's continuing prior work
- After `/compact` if the trimmed context lost important task state
- When switching back to TAOM from another repo and you need to remember where you were
- When asked: "where was I", "resume", "pick up where I left off", "restore context"

## How it works

1. **List available snapshots:**
   ```bash
   STATE_DIR="${CLAUDE_PROJECT_DIR}/.claude/state/context"
   if [[ ! -d "$STATE_DIR" ]] || [[ -z "$(ls -A "$STATE_DIR" 2>/dev/null)" ]]; then
       echo "No snapshots found. Use /context-save to create one."
       exit 0
   fi
   ls -t "$STATE_DIR"/*.md 2>/dev/null | head -10
   ```

2. **Pick which to restore.** Default: most recent. If the user asks for a specific one (e.g., "restore the careerSystem snapshot"), match by filename slug. Use `AskUserQuestion` if more than one recent snapshot is plausible (within last 24h).

3. **Read the picked snapshot** and surface its key sections to the user, in this order:
   - In-flight task (1 line)
   - Next concrete step (1 line)
   - Files in flight (count + names)
   - Decisions made (full list)
   - Open questions / blockers (if any)
   - Anything that surprised you (if present — this is the highest-value field)

4. **Cross-check with current git state.** Has the branch moved? Are the files-in-flight still showing as modified? If yes, surface as "still in flight." If no, ask: "the snapshot says you were editing X, but current git is clean — was that committed since?"

5. **Optionally re-Read files-in-flight.** Per `harness-facts.md` stale-file rule, if you're going to edit them, re-Read first.

## Modes

| Trigger | Behavior |
|---------|----------|
| `/context-restore` (no arg) | Load the most recent snapshot |
| `/context-restore <slug>` | Load by filename slug (partial match OK) |
| `/context-restore list` | Show all snapshots, don't restore — user picks next |
| `/context-restore clear` | Delete all snapshots after confirmation (`AskUserQuestion`) |

## Output format

After loading, summarize like:

```
Loaded: .claude/state/context/20260426-153012-tier2-impl.md (saved 2h ago)

In-flight: Implementing Tier 2 + Tier 3 picks from ecosystem review (#93)
Next step: Port refactoring-specialist subagent, run /context-budget, commit

Decisions carried over:
- Skipped pick #8 (TodoWrite sufficient for solo)
- effort: high only on /deep-review
- Dropped gstack telemetry scaffolding from port

Files in flight (1, all consistent with current git):
- Main/Features/CareerSystem/Services/CareerService.cs

Open questions: none
Surprises worth remembering: amend bypass was a real prevention-theater finding (now codified in harness-facts.md).

Ready to resume — say "continue" or specify a different next step.
```

## Pair with

- `/context-save` — the writer side
- `/compact` — context-restore is what you run AFTER compact if you want the pre-compact decisions back

## Notes

- Read-only by default (no file edits).
- "clear" mode is the only destructive path; always confirms via `AskUserQuestion`.
- If the snapshot references files that no longer exist (e.g., renamed), surface that as a discrepancy rather than failing silently.
