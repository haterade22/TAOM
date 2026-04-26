---
name: unfreeze
description: Release the directory edit lock set by /freeze. Edit and Write are unrestricted again. No-op if /freeze was never set.
allowed-tools:
  - Bash
---

# /unfreeze — Release the Freeze Boundary

Removes the `freeze-dir.txt` state file. Once cleared, the `/freeze` skill's PreToolUse hooks will allow every Edit/Write again (because the hook short-circuits to `{}` when the state file is missing).

```bash
STATE_DIR="${CLAUDE_PROJECT_DIR}/.claude/tmp/freeze"
FREEZE_FILE="$STATE_DIR/freeze-dir.txt"

if [[ -f "$FREEZE_FILE" ]]; then
    PREV=$(cat "$FREEZE_FILE")
    rm -f "$FREEZE_FILE"
    echo "Freeze boundary released. Was: $PREV"
else
    echo "No freeze boundary was active."
fi
```

## Notes

- This affects only the freeze state file. `/freeze`'s declared hooks remain registered (they're inert without the state file).
- If you want to switch boundaries instead of releasing, just run `/freeze` again — it overwrites the state file.
- Ending the conversation also clears the boundary (state file lives in `.claude/tmp/freeze/`, gitignored, but persists across sessions until manually cleared — re-run `/unfreeze` if a stale boundary is still active when you start work).
