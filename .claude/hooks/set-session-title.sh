#!/bin/bash
# UserPromptSubmit hook: Set session title from current branch name
# Uses hookSpecificOutput.sessionTitle (v2.1.94+)

cd "c:/Users/mikew/source/repos/TAOM" || exit 0

BRANCH=$(git branch --show-current 2>/dev/null)
if [[ -z "$BRANCH" ]]; then
  exit 0
fi

# Output JSON with sessionTitle
if command -v jq >/dev/null 2>&1; then
  jq -n --arg title "TAOM: $BRANCH" '{"hookSpecificOutput": {"sessionTitle": $title}}'
else
  echo "{\"hookSpecificOutput\": {\"sessionTitle\": \"TAOM: $BRANCH\"}}"
fi

exit 0
