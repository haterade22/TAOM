#!/bin/bash
INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

if [[ "$FILE_PATH" == *.cs ]]; then
  echo "C# file modified: $FILE_PATH" >&2
fi

exit 0
