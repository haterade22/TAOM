#!/bin/bash
# Stop hook: Append a session summary (commits + modified files) to a rolling log.
# Enables "what did I do yesterday" reconstruction without git-log spelunking.

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

LOG_DIR=".claude/logs"
mkdir -p "$LOG_DIR" 2>/dev/null
LOG_FILE="$LOG_DIR/session-log.md"

TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo 'unknown')
BRANCH=$(git branch --show-current 2>/dev/null || echo 'unknown')
RECENT_COMMITS=$(git log --oneline --since="8 hours ago" 2>/dev/null)
MODIFIED=$(git diff --name-only 2>/dev/null)
STAGED=$(git diff --cached --name-only 2>/dev/null)

# Nothing interesting happened — skip the write
[[ -z "$RECENT_COMMITS" && -z "$MODIFIED" && -z "$STAGED" ]] && exit 0

{
  echo "## Session End: $TIMESTAMP ($BRANCH)"
  if [[ -n "$RECENT_COMMITS" ]]; then
    echo "### Commits (last 8h)"
    echo '```'
    echo "$RECENT_COMMITS"
    echo '```'
  fi
  if [[ -n "$STAGED" ]]; then
    echo "### Staged"
    echo "$STAGED" | sed 's/^/- /'
  fi
  if [[ -n "$MODIFIED" ]]; then
    echo "### Unstaged"
    echo "$MODIFIED" | sed 's/^/- /'
  fi
  echo ""
  echo "---"
  echo ""
} >> "$LOG_FILE" 2>/dev/null

exit 0
