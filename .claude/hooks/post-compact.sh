#!/bin/bash
# PostCompact hook: Remind Claude to re-hydrate state after summarization
# Context survives, but fresh re-reads of MEMORY.md and modified files anchor
# the post-compaction conversation in current state.

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

echo "=== Context Restored After Compaction ==="

# Live auto-memory location. A RELATIVE autoMemoryDirectory is silently ignored
# by Claude Code (docs require absolute or ~/ paths — harness-facts.md "Memory
# file semantics"), so the real memory is the default per-project path under
# ~/.claude/projects/. Slug derivation is EMPIRICAL (same rule): drive letter
# lowercased + '--' + separators replaced by '-'. Fail open: no path, no nag.
MEMORY_INDEX=""
if command -v cygpath >/dev/null 2>&1; then
  WIN_PATH=$(cygpath -w "$(pwd)" 2>/dev/null)
  SLUG=$(printf '%s' "$WIN_PATH" | sed -e 's/^\([A-Za-z]\):/\L\1-/' -e 's/[\\\/]/-/g' 2>/dev/null)
  CAND="$HOME/.claude/projects/$SLUG/memory/MEMORY.md"
  [[ -f "$CAND" ]] && MEMORY_INDEX="$CAND"
fi
if [[ -z "$MEMORY_INDEX" ]]; then
  # Fallback: newest MEMORY.md under a project slug containing this repo's basename.
  BASE=$(basename "$(pwd)")
  CAND=$(ls -t "$HOME"/.claude/projects/*"$BASE"*/memory/MEMORY.md 2>/dev/null | head -1)
  [[ -f "$CAND" ]] && MEMORY_INDEX="$CAND"
fi
if [[ -n "$MEMORY_INDEX" ]]; then
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