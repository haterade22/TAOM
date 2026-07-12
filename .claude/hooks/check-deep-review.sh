#!/bin/bash
# Stop hook: Remind to run /deep-review if real work was done but review wasn't run
# This is a soft reminder, not a hard block

# Check if deep-review was already run RECENTLY by looking at the agent audit log.
# Recency-scoped (last 8h, matching session-stop.sh's window): before 2026-07-12 this
# grepped the whole never-rotated log, so months-old runs permanently muted the reminder.
# Fail-open: if date arithmetic is unavailable, fall back to the old whole-file grep.
AUDIT_LOG=".claude/logs/agent-audit.log"
PATTERN="deep-review\|Standards Compliance\|Bannerlord.*Compat\|Efficiency.*Performance\|Completeness Check"
if [[ -f "$AUDIT_LOG" ]]; then
  CUTOFF=$(date -d '-8 hours' '+%Y-%m-%d %H:%M:%S' 2>/dev/null)
  if [[ -n "$CUTOFF" ]]; then
    if awk -v c="$CUTOFF" -F'[][]' '$2 >= c' "$AUDIT_LOG" 2>/dev/null | grep -q "$PATTERN" 2>/dev/null; then
      exit 0
    fi
  elif grep -q "$PATTERN" "$AUDIT_LOG" 2>/dev/null; then
    exit 0
  fi
fi

# Check if any C# or XML files were modified (indicating real work was done)
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
UNTRACKED_FILES=$(git ls-files --others --exclude-standard 2>/dev/null)
ALL_FILES="$CHANGED_FILES"$'\n'"$UNTRACKED_FILES"

if echo "$ALL_FILES" | grep -qE '\.(cs|xml|xslt|json)$'; then
  echo "REMINDER: Run /deep-review before closing out. It launches parallel agents to check standards, engine compatibility, efficiency, and completeness." >&2
fi

exit 0
