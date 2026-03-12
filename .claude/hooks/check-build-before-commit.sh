#!/bin/bash
INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

# Only intercept git commit commands
if [[ ! "$COMMAND" =~ git\ commit ]]; then
  exit 0
fi

echo "Running dotnet build before commit..." >&2

cd "c:/Users/mikew/source/repos/TAOM"
if ! dotnet build --no-restore 2>&1 | tail -20; then
  echo "BUILD FAILED — commit blocked." >&2
  exit 2
fi

exit 0
