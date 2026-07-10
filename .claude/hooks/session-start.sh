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

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

echo "=== TAOM Session Context ==="

# Current branch
BRANCH=$(git branch --show-current 2>/dev/null || echo "unknown")
echo "Branch: $BRANCH"

# Game-version drift check (the 1.4.5->1.4.6 Steam force-bump cost a morning of
# misattributed crashes before anyone noticed). Pin lives in .claude/pinned-game-version.txt;
# on drift, warn loudly and point at /engine-bump. Fail-open: any missing file = silence.
GAME_VERSION_XML="${BANNERLORD_GAME_DIR:-E:/Steam/steamapps/common/Mount & Blade II Bannerlord}/bin/Win64_Shipping_Client/Version.xml"
PIN_FILE=".claude/pinned-game-version.txt"
if [[ -f "$GAME_VERSION_XML" && -f "$PIN_FILE" ]]; then
  INSTALLED=$(grep -oE 'v[0-9]+\.[0-9]+\.[0-9]+' "$GAME_VERSION_XML" 2>/dev/null | head -1)
  PINNED=$(tr -d '[:space:]' < "$PIN_FILE" 2>/dev/null)
  if [[ -n "$INSTALLED" && -n "$PINNED" && "$INSTALLED" != "$PINNED" ]]; then
    echo ""
    echo "!!! GAME VERSION DRIFT: installed $INSTALLED but TAOM is pinned to $PINNED !!!"
    echo "!!! Steam likely force-updated. Run /engine-bump BEFORE trusting any test run. !!!"
  fi
fi

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
