#!/bin/bash
# PostToolUse(Bash) hook: record that a build/test verification command ran.
#
# Touches .claude/logs/.verification-ran so the check-verification-evidence Stop
# hook can tell whether C# source was edited AFTER the most recent verification.
# Pairs with: check-verification-evidence.sh (Stop).
#
# Non-blocking; always exits 0. Concurrent invocations are safe: mkdir -p and
# touch are idempotent and the hook never blocks.

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Parse the command field precisely. jq if present, else "$PYBIN".
#
# There is deliberately NO raw-payload fallback. The old code did
# `[ -z "$COMMAND" ] && COMMAND="$INPUT"`, and since jq is absent that was the ONLY
# path ever taken, so ANY Bash call whose payload merely MENTIONED "dotnet test" or
# "build.ps1" (a grep for it, a doc edit, this comment) touched the marker and muted
# check-verification-evidence.sh. The old note called that safe because it "only
# suppresses a soft reminder" — but suppression is precisely the failure the sibling
# hook exists to prevent, and evidence-over-claims.md is built on that reminder.
#
# If the command cannot be parsed, do nothing. The marker stays unset, the Stop
# reminder still fires, and the worst case is one redundant nudge instead of a
# silently skipped verification.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
elif [ -n "$PYBIN" ]; then
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
else
  COMMAND=""
fi

# Touch on any build/test invocation (pass OR fail: a failed build is still
# verification evidence; you have the output). build.ps1 -RunTests, plain
# dotnet build/test, and /verify all route through one of these substrings.
#
# Anchor the marker to the project, not the inherited cwd. A relative path here is how
# a stray .claude/logs/ tree got written under .claude/hooks/ on 2026-08-31 when these
# scripts were run from that directory.
LOGDIR="${CLAUDE_PROJECT_DIR:-$(pwd)}/.claude/logs"

# Match an INVOCATION, not a mention. A substring test on the whole command still marks
# verification for `grep -rn "dotnet test" docs/`, which mutes the reminder that backs
# evidence-over-claims.md. Split on shell separators and inspect each segment's leading
# token, the same shape block-dangerous-git.sh already uses.
MARK=0
while IFS= read -r seg; do
  seg="${seg#"${seg%%[![:space:]]*}"}"          # left-trim
  while :; do                                    # drop env-var prefixes: FOO=bar cmd
    case "$seg" in
      [A-Za-z_]*=*\ *) seg="${seg#* }"; seg="${seg#"${seg%%[![:space:]]*}"}" ;;
      *) break ;;
    esac
  done
  first="${seg%% *}"
  case "$first" in
    dotnet)
      case "$seg" in "dotnet build"* | "dotnet test"*) MARK=1 ;; esac ;;
    ./build.ps1 | build.ps1 | *[/\\]build.ps1)
      MARK=1 ;;
    pwsh | powershell | powershell.exe)
      case "$seg" in *build.ps1*) MARK=1 ;; esac ;;
  esac
done <<< "$(printf '%s' "$COMMAND" | tr ';&|' '\n')"

if [[ $MARK -eq 1 ]]; then
  mkdir -p "$LOGDIR" 2>/dev/null
  touch "$LOGDIR/.verification-ran" 2>/dev/null || true
fi

exit 0
