#!/bin/bash
# PostToolUse hook: summarize dotnet test results prominently
INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

if echo "$COMMAND" | grep -q "dotnet test"; then
  RESPONSE=$(echo "$INPUT" | jq -r '.tool_response // empty')
  if echo "$RESPONSE" | grep -q "Failed"; then
    FAILED=$(echo "$RESPONSE" | grep -oP 'Failed:\s*\K[0-9]+' | head -1)
    PASSED=$(echo "$RESPONSE" | grep -oP 'Passed:\s*\K[0-9]+' | head -1)
    echo "=== TEST RESULTS: FAILED (Failed: ${FAILED:-?}, Passed: ${PASSED:-?}) ===" >&2
  elif echo "$RESPONSE" | grep -q "Passed"; then
    PASSED=$(echo "$RESPONSE" | grep -oP 'Passed:\s*\K[0-9]+' | head -1)
    echo "=== TEST RESULTS: PASSED (${PASSED:-?} tests) ===" >&2
  fi
fi

exit 0
