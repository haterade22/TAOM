#!/bin/bash
# SessionStart hook: Print recent project context on fresh startup
# Only fires on "startup" source — resume/compact/clear already have context

INPUT=$(cat)

# Parse source from stdin JSON
if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# Only run on fresh startup
if [[ "$SOURCE" != "startup" ]]; then
  exit 0
fi

cd "c:/Users/mikew/source/repos/TAOM" || exit 0

echo "=== TAOM Session Context ==="

# Current branch
BRANCH=$(git branch --show-current 2>/dev/null || echo "unknown")
echo "Branch: $BRANCH"

# Last 5 commits
echo ""
echo "Recent commits:"
git log --oneline -5 2>/dev/null || echo "  (no commits)"

# Latest CHANGELOG entry (date + feature titles only)
echo ""
echo "Latest CHANGELOG:"
if [[ -f CHANGELOG.md ]]; then
  awk '/^## [0-9]/{if(found) exit; found=1; print; next} found && /^### /{print}' CHANGELOG.md
else
  echo "  (no CHANGELOG.md)"
fi

# Uncommitted changes count
STAGED=$(git diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
UNSTAGED=$(git diff --name-only 2>/dev/null | wc -l | tr -d ' ')
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | wc -l | tr -d ' ')
echo ""
echo "Uncommitted: ${STAGED} staged, ${UNSTAGED} unstaged, ${UNTRACKED} untracked"

# TODO/FIXME count in Main/
if [[ -d Main ]]; then
  TODO_COUNT=$(grep -rE 'TODO|FIXME' Main/ --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
  echo "TODO/FIXME in Main/: ${TODO_COUNT}"
fi

echo "==========================="
exit 0
