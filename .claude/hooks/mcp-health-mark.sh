#!/bin/bash
# PostToolUseFailure hook (mcp__*): Mark MCP server as unhealthy after failure
# Works in tandem with mcp-health-check.sh

INPUT=$(cat)

# Parse tool_name from input
if command -v jq >/dev/null 2>&1; then
  TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')
else
  TOOL_NAME=$(echo "$INPUT" | grep -oE '"tool_name"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# Only handle MCP tools
if [[ ! "$TOOL_NAME" == mcp__* ]]; then
  exit 0
fi

# Extract server name
SERVER_NAME=$(echo "$TOOL_NAME" | sed 's/^mcp__//' | sed 's/__.*//')

if [[ -z "$SERVER_NAME" ]]; then
  exit 0
fi

# Mark server as unhealthy with current timestamp
HEALTH_DIR="/tmp/claude-mcp-health"
mkdir -p "$HEALTH_DIR" 2>/dev/null
echo "$(date +%s)" > "${HEALTH_DIR}/${SERVER_NAME}.state" 2>/dev/null

echo "[MCPHealth] ${SERVER_NAME} marked unhealthy after failed ${TOOL_NAME} call. Will block calls for 60s." >&2

exit 0
