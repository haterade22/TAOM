#!/bin/bash
# PreToolUse hook (Edit|Write): Block modifications to critical config files
# Prevents AI from weakening configs instead of fixing code
# Exit 2 = block, Exit 0 = allow

INPUT=$(cat)

# Parse file_path from tool input JSON
if command -v jq >/dev/null 2>&1; then
  FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // .tool_input.file // empty')
else
  FILE_PATH=$(echo "$INPUT" | grep -oE '"file_path"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# No file path = not a file edit, allow
if [[ -z "$FILE_PATH" ]]; then
  echo "$INPUT"
  exit 0
fi

# Check for user-approved override (created manually or via explicit user request)
OVERRIDE_FILE="/tmp/claude-config-override-${CLAUDE_SESSION_ID:-none}"
if [[ -f "$OVERRIDE_FILE" ]]; then
  echo "$INPUT"
  exit 0
fi

# Extract basename for matching
BASENAME=$(basename "$FILE_PATH")

# Protected files list
# CLAUDE.md removed 2026-07-02 by explicit user decision (solo developer; the agent keeps
# CLAUDE.md current as living documentation, and the block forced manual approval on every
# routine doc correction). Directory.Build.props / settings*.json / ADRs stay protected —
# those guard against the agent weakening build config, permissions, and architecture decisions.
PROTECTED_FILES=(
  "Directory.Build.props"
  "settings.json"
  "settings.local.json"
)

for PROTECTED in "${PROTECTED_FILES[@]}"; do
  if [[ "$BASENAME" == "$PROTECTED" ]]; then
    echo "BLOCKED: Modifying $BASENAME is not allowed without explicit user request." >&2
    echo "Fix the source code to satisfy the rules, not the config." >&2
    echo "If this is a legitimate change, ask the user first." >&2
    exit 2
  fi
done

# Also protect ADR files from accidental weakening
if [[ "$FILE_PATH" == *"/docs/adrs/"* && "$BASENAME" == *.md ]]; then
  echo "BLOCKED: Modifying ADR $BASENAME is not allowed without explicit user request." >&2
  echo "ADRs are architectural decisions — changing them requires deliberate review." >&2
  exit 2
fi

echo "$INPUT"
exit 0
