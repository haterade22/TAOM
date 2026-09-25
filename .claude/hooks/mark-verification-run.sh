#!/bin/bash
# PostToolUse(Bash and PowerShell) hook: record that a build/test verification command ran.
#
# Touches .claude/logs/.verification-ran so the check-verification-evidence Stop
# hook can tell whether C# source was edited AFTER the most recent verification.
# Pairs with: check-verification-evidence.sh (Stop).
#
# Non-blocking; always exits 0. Concurrent invocations are safe: mkdir -p and
# touch are idempotent and the hook never blocks.

INPUT=$(cat)

# Prefilter: a mark needs `dotnet` or `build.ps1` in the command (the segment loop below),
# and Claude Code never escapes an ASCII letter, so a raw payload with neither cannot concern this
# hook. Exiting here skips the _pybin.sh probe and the parse on most Bash calls
# (tools/test_hooks.sh 4c).
# Never skip on an escape: JSON writes a letter either literally or as a \u escape,
# so a payload holding any \u takes the full parse, and the raw test is safe
# whatever writes the payload.
[[ "$INPUT" == *dotnet* || "$INPUT" == *build.ps1* || "$INPUT" == *'\u'* ]] || exit 0

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

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
#
# The split honours quotes (maintainer decision D41): splitting on ; & | with `tr` cut
# `grep "x; dotnet test" docs/` into a segment that starts with dotnet, so a mention marked.
# A newline inside quotes (a commit message) becomes a space, so it cannot start a segment
# either, and a backslash outside single quotes keeps the next character. Byte-wise under
# LC_ALL=C and linear: about 0.4 s for a 20 KB command.
split_segments() {
  local LC_ALL=C
  local s="$1" out="" q="" c i n=${#1}
  for ((i = 0; i < n; i++)); do
    c="${s:i:1}"
    if [[ "$c" == "\\" && "$q" != "'" ]]; then
      out+="$c${s:i+1:1}"; i=$((i + 1)); continue
    fi
    if [[ -n "$q" ]]; then
      [[ "$c" == "$q" ]] && q=""
      [[ "$c" == $'\n' ]] && c=" "
      out+="$c"; continue
    fi
    case "$c" in
      \" | \') q="$c"; out+="$c" ;;
      ';' | '&' | '|' | $'\n') out+=$'\n' ;;
      *) out+="$c" ;;
    esac
  done
  printf '%s\n' "$out"
}

MARK=0
while IFS= read -r seg; do
  seg="${seg#"${seg%%[![:space:]]*}"}"          # left-trim
  # Drop env-var prefixes (FOO=bar cmd), and only a word that IS an assignment. The old
  # pattern took any word with an `=` and a space somewhere after it, so it read
  # `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, the repo's canonical
  # test command, as a prefix and dropped `dotnet`: that command never marked (D41).
  while :; do
    word="${seg%%[[:space:]]*}"
    [[ "$word" == "$seg" ]] && break             # a lone word is the command itself
    [[ "$word" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || break
    seg="${seg#"$word"}"; seg="${seg#"${seg%%[![:space:]]*}"}"
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
done <<< "$(split_segments "$COMMAND")"

if [[ $MARK -eq 1 ]]; then
  mkdir -p "$LOGDIR" 2>/dev/null
  touch "$LOGDIR/.verification-ran" 2>/dev/null || true
fi

exit 0
