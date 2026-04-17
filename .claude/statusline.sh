#!/bin/bash
# Claude Code status line: receives JSON on stdin, emits one line.
# Format: ctx: N% | model | branch | uncommitted: Ns/Nu/Nt

INPUT=$(cat)

if command -v jq >/dev/null 2>&1; then
  MODEL=$(echo "$INPUT" | jq -r '.model.display_name // "?"')
  USED_PCT=$(echo "$INPUT" | jq -r '.context_window.used_percentage // empty')
  CWD=$(echo "$INPUT" | jq -r '.workspace.current_dir // .cwd // ""')
else
  MODEL=$(echo "$INPUT" | grep -oE '"display_name"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  USED_PCT=$(echo "$INPUT" | grep -oE '"used_percentage"\s*:\s*[0-9]+' | head -1 | sed 's/.*:\s*//')
  CWD=$(echo "$INPUT" | grep -oE '"current_dir"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

[[ -z "$MODEL" ]] && MODEL="?"
[[ -z "$CWD" ]] && CWD="."
CWD=$(echo "$CWD" | sed 's|\\|/|g')

# ctx segment
if [[ -n "$USED_PCT" ]]; then
  CTX="ctx: ${USED_PCT}%"
else
  CTX="ctx: --"
fi

# branch + uncommitted counts (cd into cwd for accurate git state)
BRANCH="?"
COUNTS=""
if [[ -d "$CWD/.git" || -f "$CWD/.git" ]]; then
  BRANCH=$(git -C "$CWD" branch --show-current 2>/dev/null || echo "?")
  STAGED=$(git -C "$CWD" diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
  UNSTAGED=$(git -C "$CWD" diff --name-only 2>/dev/null | wc -l | tr -d ' ')
  UNTRACKED=$(git -C "$CWD" ls-files --others --exclude-standard 2>/dev/null | wc -l | tr -d ' ')
  if [[ "$STAGED" != "0" || "$UNSTAGED" != "0" || "$UNTRACKED" != "0" ]]; then
    COUNTS=" | ${STAGED}s/${UNSTAGED}u/${UNTRACKED}t"
  fi
fi

printf "%s | %s | %s%s" "$CTX" "$MODEL" "$BRANCH" "$COUNTS"