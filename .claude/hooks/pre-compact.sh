#!/bin/bash
# PreCompact hook: Dump working state before context compaction
# Output survives summarization so Claude can recover context

cd "c:/Users/mikew/source/repos/TAOM" || exit 0

echo "=== Pre-Compaction State Snapshot ==="
echo "Timestamp: $(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo 'unknown')"
echo "Branch: $(git branch --show-current 2>/dev/null || echo 'unknown')"

# Modified files (unstaged)
UNSTAGED=$(git diff --name-only 2>/dev/null)
echo ""
echo "Modified files (unstaged):"
if [[ -n "$UNSTAGED" ]]; then
  echo "$UNSTAGED" | sed 's/^/  /'
else
  echo "  (none)"
fi

# Modified files (staged)
STAGED=$(git diff --staged --name-only 2>/dev/null)
echo ""
echo "Modified files (staged):"
if [[ -n "$STAGED" ]]; then
  echo "$STAGED" | sed 's/^/  /'
else
  echo "  (none)"
fi

# Untracked files
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null)
echo ""
echo "Untracked files:"
if [[ -n "$UNTRACKED" ]]; then
  echo "$UNTRACKED" | sed 's/^/  /'
else
  echo "  (none)"
fi

echo ""
echo "NOTE: Read these files to recover context after compaction."
echo "======================================"
exit 0
