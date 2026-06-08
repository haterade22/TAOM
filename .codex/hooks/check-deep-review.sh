#!/bin/bash
# Stop hook: Remind to run /deep-review if real work was done but review wasn't run
# This is a soft reminder, not a hard block

# Check if deep-review was already run this session by looking at agent audit log
AUDIT_LOG=".claude/logs/agent-audit.log"
if [[ -f "$AUDIT_LOG" ]] && grep -q "deep-review\|Standards Compliance\|Bannerlord.*Compat\|Efficiency.*Performance\|Completeness Check" "$AUDIT_LOG" 2>/dev/null; then
  exit 0
fi

# Check if any C# or XML files were modified (indicating real work was done)
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
UNTRACKED_FILES=$(git ls-files --others --exclude-standard 2>/dev/null)
ALL_FILES="$CHANGED_FILES"$'\n'"$UNTRACKED_FILES"

if echo "$ALL_FILES" | grep -qE '\.(cs|xml|xslt|json)$'; then
  echo "REMINDER: Run /deep-review before closing out. It launches parallel agents to check standards, Bannerlord 1.3 compatibility, efficiency, and completeness." >&2
fi

exit 0
