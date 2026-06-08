#!/bin/bash
# PreToolUse hook (mcp__*): Lightweight MCP health check
# Checks if an MCP server is responsive before allowing tool calls
# Exit 2 = block (unhealthy), Exit 0 = allow

INPUT=$(cat)

# Parse tool_name from input
if command -v jq >/dev/null 2>&1; then
  TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')
else
  TOOL_NAME=$(echo "$INPUT" | grep -oE '"tool_name"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# Only check MCP tools (prefixed with mcp__)
if [[ ! "$TOOL_NAME" == mcp__* ]]; then
  echo "$INPUT"
  exit 0
fi

# Extract server name (mcp__<server>__<tool> -> <server>)
SERVER_NAME=$(echo "$TOOL_NAME" | sed 's/^mcp__//' | sed 's/__.*//')

if [[ -z "$SERVER_NAME" ]]; then
  echo "$INPUT"
  exit 0
fi

# Health state file (persists across tool calls within session)
HEALTH_DIR="/tmp/claude-mcp-health"
mkdir -p "$HEALTH_DIR" 2>/dev/null
HEALTH_FILE="${HEALTH_DIR}/${SERVER_NAME}.state"

# Check if server was recently marked unhealthy (backoff for 60 seconds)
if [[ -f "$HEALTH_FILE" ]]; then
  MARKED_AT=$(cat "$HEALTH_FILE" 2>/dev/null | tr -cd '0-9')
  NOW=$(date +%s)
  if [[ -n "$MARKED_AT" && -n "$NOW" ]]; then
    AGE=$((NOW - MARKED_AT))
    if [[ "$AGE" -lt 60 ]]; then
      echo "[MCPHealth] ${SERVER_NAME} was marked unhealthy ${AGE}s ago. Blocking ${TOOL_NAME} — retry in $((60 - AGE))s or use non-MCP alternatives." >&2
      exit 2
    else
      # Backoff expired, clear state and allow retry
      rm -f "$HEALTH_FILE" 2>/dev/null
    fi
  fi
fi

# Allow the call — if it fails, the PostToolUseFailure hook will mark it unhealthy
echo "$INPUT"
exit 0
