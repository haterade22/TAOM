#!/bin/bash
# PostToolUse hook: summarize dotnet test results prominently
INPUT=$(cat)

# Extract tool_input.command and tool_response. Prefer jq; fall back to python3 for
# robust JSON. jq is NOT on PATH in this Git Bash install (verified 2026-08-20), so
# the bare jq calls this hook shipped with made it inert: both fields came back empty
# and the TEST RESULTS banner never fired once. Mirrors block-dangerous-git.sh.
#
# One helper, one field per call: a single call returning both fields needs a
# delimiter, and any delimiter that survives shell round-tripping is more fragile than
# just paying for a second interpreter start on the rare `dotnet test` call.
json_field() {
  printf '%s' "$INPUT" | python3 -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
except Exception:
    sys.exit(0)
cur = d
for key in sys.argv[1].split("."):
    if not isinstance(cur, dict):
        sys.exit(0)
    cur = cur.get(key, "")
# tool_response may be an object rather than a string; the callers only scan text.
print(cur if isinstance(cur, str) else json.dumps(cur))
' "$1" 2>/dev/null
}

if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  COMMAND=$(json_field "tool_input.command")
fi

if echo "$COMMAND" | grep -q "dotnet test"; then
  if command -v jq >/dev/null 2>&1; then
    RESPONSE=$(printf '%s' "$INPUT" | jq -r '.tool_response // empty' 2>/dev/null)
  else
    RESPONSE=$(json_field "tool_response")
  fi
  # Branch on the COUNT, not on the word. `dotnet test` prints "Failed: 0, Passed: 6380"
  # on a fully green run, so a substring test for "Failed" reports every passing suite as
  # FAILED. That went unnoticed because the hook was inert until the jq fix above; it
  # would have mislabelled every green run the moment it started working.
  FAILED=$(echo "$RESPONSE" | grep -oP 'Failed:\s*\K[0-9]+' | head -1)
  PASSED=$(echo "$RESPONSE" | grep -oP 'Passed:\s*\K[0-9]+' | head -1)
  if [[ -n "$FAILED" && "$FAILED" -gt 0 ]]; then
    echo "=== TEST RESULTS: FAILED (Failed: ${FAILED}, Passed: ${PASSED:-?}) ===" >&2
  elif [[ -n "$PASSED" ]]; then
    echo "=== TEST RESULTS: PASSED (${PASSED} tests) ===" >&2
  elif echo "$RESPONSE" | grep -q "Failed"; then
    # Counts unavailable (a crash or a truncated response) but the word is there: say so
    # rather than staying silent, which would read as "no test run happened".
    echo "=== TEST RESULTS: FAILED (counts unavailable) ===" >&2
  fi
fi

exit 0
