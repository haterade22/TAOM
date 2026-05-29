#!/bin/bash
# PostToolUse(Bash) hook: record that a build/test verification command ran.
#
# Touches .claude/logs/.verification-ran so the check-verification-evidence Stop
# hook can tell whether C# source was edited AFTER the most recent verification.
# Pairs with: check-verification-evidence.sh (Stop).
#
# Non-blocking; always exits 0. Concurrent invocations are safe: mkdir -p and
# touch are idempotent and the hook never blocks.

INPUT=$(cat)

# Prefer the precise command field via jq (as the other PostToolUse hooks do).
# jq is OPTIONAL here: if it is absent or fails, fall back to scanning the raw
# payload so the marker still works without a hard jq dependency. A false match
# only SUPPRESSES a soft reminder — it never produces a wrong one — so the looser
# fallback is safe. (printf, not echo, so backslashes / a leading -n in the
# payload are not mangled before jq sees them.)
COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
[ -z "$COMMAND" ] && COMMAND="$INPUT"

# Touch on any build/test invocation (pass OR fail — a failed build is still
# verification evidence; you have the output). build.ps1 -RunTests, plain
# dotnet build/test, and /verify all route through one of these substrings.
case "$COMMAND" in
  *"dotnet build"* | *"dotnet test"* | *"build.ps1"* )
    mkdir -p .claude/logs 2>/dev/null
    touch .claude/logs/.verification-ran 2>/dev/null || true
    ;;
esac

exit 0
