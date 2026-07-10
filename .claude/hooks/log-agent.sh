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

TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo 'unknown')
echo "[${TIMESTAMP}] agent_type=${AGENT_TYPE} agent_id=${AGENT_ID}" >> "$LOG_FILE" 2>/dev/null

exit 0
