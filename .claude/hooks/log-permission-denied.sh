#!/bin/bash
# PermissionDenied hook: Audit log blocked tool calls
# Fires after auto mode classifier denials, deny rules, or PreToolUse exit 2

INPUT=$(cat)

# Parse tool name and reason
if command -v jq >/dev/null 2>&1; then
  TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // "unknown"')
  REASON=$(echo "$INPUT" | jq -r '.reason // "unspecified"')
else
  TOOL_NAME=$(echo "$INPUT" | grep -oE '"tool_name"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  REASON=$(echo "$INPUT" | grep -oE '"reason"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  TOOL_NAME="${TOOL_NAME:-unknown}"
  REASON="${REASON:-unspecified}"
fi

# Log to audit file
LOG_DIR="c:/Users/mikew/source/repos/TAOM/.claude/logs"
mkdir -p "$LOG_DIR" 2>/dev/null
LOG_FILE="${LOG_DIR}/permission-denied.log"

TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')
echo "[${TIMESTAMP}] DENIED: ${TOOL_NAME} — ${REASON}" >> "$LOG_FILE"

exit 0
