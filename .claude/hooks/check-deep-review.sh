#!/bin/bash
# Stop hook: Remind to run /deep-review if real work was done but review wasn't run
# This is a soft reminder, not a hard block

# Check if deep-review was already run RECENTLY by looking at the agent audit log.
# Recency-scoped (last 8h, matching session-stop.sh's window): before 2026-07-12 this
# grepped the whole never-rotated log, so months-old runs permanently muted the reminder.
# Fail-open: if date arithmetic is unavailable, fall back to the old whole-file grep.
AUDIT_LOG=".claude/logs/agent-audit.log"
# log-agent.sh writes "[TS] agent_type=<type> agent_id=<id>" and nothing else, so the only
# evidence of a review is its reviewer's type. tools/test_hooks.sh section 7 drives both
# hooks together; change this pattern and that line format only as a pair.
PATTERN="agent_type=deep-reviewer"
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

# C# and C++ code, and XML is code too (Mike, 2026-09-18). git cannot see edits in the live
# TAOM_Map / Armory installs, so those never trigger this; /deep-review Step 1 sweeps them.
if echo "$ALL_FILES" | grep -qE '\.(cs|cpp|h|xml|xsl|xslt|mbproj|json)$'; then
  echo "REMINDER: Run /deep-review before closing out. It launches parallel agents to check standards, engine compatibility, efficiency, completeness, data flow, design and XML integrity, then applies the better ways it finds." >&2
fi

exit 0
