#!/bin/bash
# PostCompact hook: Remind Claude to re-hydrate state after summarization
# Context survives, but fresh re-reads of MEMORY.md and modified files anchor
# the post-compaction conversation in current state.

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

echo "=== Context Restored After Compaction ==="

MEMORY_INDEX=".claude/memory/MEMORY.md"
if [[ -f "$MEMORY_INDEX" ]]; then
  LINE_COUNT=$(wc -l < "$MEMORY_INDEX" 2>/dev/null | tr -d ' ')
  echo "Memory index: $MEMORY_INDEX (${LINE_COUNT} lines)"
  echo "IMPORTANT: Read MEMORY.md now to restore durable context (user prefs, feedback, project state)."
fi

# List files currently in flight so Claude knows which to re-read
STAGED=$(git diff --cached --name-only 2>/dev/null)
UNSTAGED=$(git diff --name-only 2>/dev/null)

if [[ -n "$STAGED" || -n "$UNSTAGED" ]]; then
  echo ""
  echo "Files in flight (re-read before continuing):"
  [[ -n "$STAGED" ]] && echo "$STAGED" | sed 's/^/  [staged]   /'
  [[ -n "$UNSTAGED" ]] && echo "$UNSTAGED" | sed 's/^/  [unstaged] /'
fi

echo "=========================================="
exit 0