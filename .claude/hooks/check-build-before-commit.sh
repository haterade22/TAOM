#!/bin/bash
INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

# Block --no-verify flag on any git command (protects pre-commit hooks)
if [[ "$COMMAND" =~ --no-verify ]]; then
  echo "BLOCKED: --no-verify flag is not allowed. Fix the issue that's causing the hook to fail." >&2
  exit 2
fi

# Only intercept git commit commands
if [[ ! "$COMMAND" =~ git\ commit ]]; then
  exit 0
fi

echo "Running dotnet build before commit..." >&2

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
if ! dotnet build --no-restore 2>&1 | tail -20; then
  echo "BUILD FAILED — commit blocked." >&2
  exit 2
fi

exit 0
