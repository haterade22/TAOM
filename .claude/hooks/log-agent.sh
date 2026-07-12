#!/bin/bash
# SubagentStart hook: Log agent invocations for audit trail
# Silent logger — no stdout output, just appends to log file

INPUT=$(cat)

# Parse agent fields — jq preferred, grep fallback
if command -v jq >/dev/null 2>&1; then
  AGENT_ID=$(echo "$INPUT" | jq -r '.agent_id // "unknown"')
  AGENT_TYPE=$(echo "$INPUT" | jq -r '.agent_type // "unknown"')
else
  AGENT_ID=$(echo "$INPUT" | grep -oE '"agent_id"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  AGENT_TYPE=$(echo "$INPUT" | grep -oE '"agent_type"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  AGENT_ID=${AGENT_ID:-unknown}
  AGENT_TYPE=${AGENT_TYPE:-unknown}
fi

LOG_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}/.claude/logs"
LOG_FILE="${LOG_DIR}/agent-audit.log"

mkdir -p "$LOG_DIR" 2>/dev/null

# Size-capped rotation: keep one previous generation (~512 KB bound total).
# NOTE: check-deep-review.sh greps this log for deep-review evidence; rotation
# keeps its window recent instead of matching months-old runs.
MAX_BYTES=262144
if [[ -f "$LOG_FILE" ]] && (( $(wc -c < "$LOG_FILE" 2>/dev/null || echo 0) > MAX_BYTES )); then
  mv -f "$LOG_FILE" "$LOG_FILE.1" 2>/dev/null || true
fi

TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo 'unknown')
echo "[${TIMESTAMP}] agent_type=${AGENT_TYPE} agent_id=${AGENT_ID}" >> "$LOG_FILE" 2>/dev/null

exit 0
