#!/bin/bash
# PreToolUse hook (*): Suggest manual compaction at logical intervals
# Counts tool calls and suggests /compact at thresholds
# Prevents mid-task auto-compaction by encouraging proactive compaction

INPUT=$(cat)

# Session-specific counter file
SESSION_ID="${CLAUDE_SESSION_ID:-default}"
SESSION_ID=$(echo "$SESSION_ID" | tr -cd 'a-zA-Z0-9_-')
COUNTER_FILE="/tmp/claude-tool-count-${SESSION_ID}"
THRESHOLD=${COMPACT_THRESHOLD:-50}

# Read and increment counter
COUNT=1
if [[ -f "$COUNTER_FILE" ]]; then
  PREV=$(cat "$COUNTER_FILE" 2>/dev/null | tr -cd '0-9')
  if [[ -n "$PREV" && "$PREV" -gt 0 && "$PREV" -lt 1000000 ]]; then
    COUNT=$((PREV + 1))
  fi
fi
echo "$COUNT" > "$COUNTER_FILE" 2>/dev/null

# Suggest compaction at threshold
if [[ "$COUNT" -eq "$THRESHOLD" ]]; then
  echo "[Compact] ${THRESHOLD} tool calls reached — consider /compact if transitioning phases." >&2
fi

# Suggest at intervals after threshold (every 25 calls)
if [[ "$COUNT" -gt "$THRESHOLD" ]]; then
  PAST=$((COUNT - THRESHOLD))
  if [[ $((PAST % 25)) -eq 0 ]]; then
    echo "[Compact] ${COUNT} tool calls — good checkpoint for /compact if context is stale." >&2
  fi
fi

echo "$INPUT"
exit 0
