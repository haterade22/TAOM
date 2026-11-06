#!/bin/bash
# PostToolUse and PostToolUseFailure (Bash and PowerShell) hook: record that a build/test verification command ran.
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

# PostToolUseFailure carries "is_interrupt": true when the tool call was aborted: the build or test
# never finished, so there is no result to count, and marking would mute the Stop reminder with
# nothing in hand (plan 027). Claude Code 2.1.241 sets it from the thrown error being an abort;
# whether a timed-out command sets it is UNVERIFIED, so a timeout still marks. Inside a JSON string
# a quote is escaped, so a command that merely mentions the field cannot match.
IRQ='"is_interrupt"[[:space:]]*:[[:space:]]*true'
[[ "$INPUT" =~ $IRQ ]] && exit 0

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Parse the command field precisely, with "$PYBIN" (jq is not on PATH here, and the split
# below needs a real parser).
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
#
# Match an INVOCATION, not a mention. A substring test on the whole command still marks
# verification for `grep -rn "dotnet test" docs/`, which mutes the reminder that backs
# evidence-over-claims.md. So the command is split into segments at ; & | and newlines outside
# quotes, and each segment's leading token is inspected below, the shape block-dangerous-git.sh
# uses. Quotes are honoured (maintainer decision D41): splitting with `tr` cut
# `grep "x; dotnet test" docs/` into a segment that starts with dotnet, so a mention marked. A
# newline inside quotes (a commit message) becomes a space, so it cannot start a segment.
#
# The split is _shellwords.py commands (plan 027, #985): the command is first read as POSIX-shell
# text (a PowerShell command as the Bash text of the same command), then split outside quotes with
# \ as the escape, in time linear in its length (a per-character bash loop took 9.5 s on a 100 KB
# command, plan 011 review), and a # comment is dropped. An escaped newline joins its lines, and the
# output is bytes with LF only, so a CR never hides a command. Each segment starts at its command
# word: VAR=value, shell grammar and, in the Bash tool only, wrappers (timeout, env, time, nice,
# nohup, xargs, stdbuf, command) are already read past, so `timeout 600 dotnet test` marks. A heredoc body
# is marked only when a shell runs it (a heredoc given to bash), never a commit message or a script piped to python. In the
# PowerShell tool `timeout` is the Windows timeout.exe and stays the command, so it does not mark.
SEGMENTS=""
if [ -n "$PYBIN" ]; then
  # Bounded to 3 s of the 5 s registration (a kill cannot speak). A reader that fails or overruns (rc 124)
  # leaves no segments, so nothing marks: the Stop reminder nags once, the safe direction.
  SEGMENTS=$(printf '%s' "$INPUT" | timeout -k 1 "${TAOM_READER_TIMEOUT:-3}" "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" commands 2>/dev/null)
  RC=$?
  [ $RC -eq 0 ] || SEGMENTS=""
fi

# Touch on any build/test invocation (pass OR fail: a failed build is still
# verification evidence; you have the output). build.ps1 -RunTests, plain
# dotnet build/test, and /verify all route through one of these substrings.
# A command that exits non-zero raises PostToolUseFailure, not PostToolUse (Claude Code
# 2.1.241), so the "fail" half holds only while this hook is registered on both events
# (tools/test_hooks.sh 7c checks both).
#
# Anchor the marker to the project, not the inherited cwd. A relative path here is how
# a stray .claude/logs/ tree got written under .claude/hooks/ on 2026-08-31 when these
# scripts were run from that directory.
LOGDIR="${CLAUDE_PROJECT_DIR:-$(pwd)}/.claude/logs"

MARK=0
# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text (#681). An empty segment is dropped, which is harmless: it runs nothing.
set -f; IFS=$'\n'; SEG_LIST=($SEGMENTS); IFS=$' \t\n'; set +f
for seg in "${SEG_LIST[@]}"; do
  seg="${seg#"${seg%%[![:space:]]*}"}"          # left-trim
  # The reader already dropped env-var prefixes, grammar and wrappers (#985): it does what the prefix
  # loop that stood here did (the D41 fix, which kept `dotnet test ... -p:ModuleId=` from being read as
  # an assignment), and more.
  first="${seg%% *}"
  first=${first//\'/}                            # the reader quotes a word holding \ (plan 027)
  case "$first" in
    dotnet)
      case "$seg" in "dotnet build"* | "dotnet test"*) MARK=1 ;; esac ;;
    ./build.ps1 | build.ps1 | *[/\\]build.ps1)
      MARK=1 ;;
    pwsh | powershell | powershell.exe)
      case "$seg" in *build.ps1*) MARK=1 ;; esac ;;
  esac
done

if [[ $MARK -eq 1 ]]; then
  mkdir -p "$LOGDIR" 2>/dev/null
  touch "$LOGDIR/.verification-ran" 2>/dev/null || true
fi

exit 0
