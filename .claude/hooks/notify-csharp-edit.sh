#!/bin/bash
INPUT=$(cat)

# Extract tool_input.file_path. Prefer jq; fall back to python3 for robust JSON.
# jq is NOT on PATH in this Git Bash install (verified 2026-08-20), so the bare jq
# call this hook shipped with made it inert: FILE_PATH was always empty and the
# notice never fired. Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  FILE_PATH=$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // empty' 2>/dev/null)
else
  FILE_PATH=$(printf '%s' "$INPUT" | python3 -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("file_path", ""))
except Exception:
    pass
' 2>/dev/null)
fi

if [[ "$FILE_PATH" == *.cs ]]; then
  echo "C# file modified: $FILE_PATH" >&2
fi

exit 0
