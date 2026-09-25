#!/bin/bash
# PreToolUse hook (Bash and PowerShell): hard-blocks a force push to a protected branch
# (AGENTS.md "Git and commits"). It is the only force-push guard: GitHub protects no branch (D29).
# Non-blocking stderr warning (which Claude does not see) for a plain push to a protected branch.

INPUT=$(cat)

# Prefilter: a push is only judged at a `push` token (below), and Claude Code never escapes an
# ASCII letter, so a raw payload without the text `push` cannot concern this gate. Exiting
# here skips the _pybin.sh probe and the parse on every Bash call without the word, other git
# calls included. Match the raw text, never a token regex: a newline before a command
# arrives as \n. tools/test_hooks.sh 4c checks it.
# Never skip on an escape: JSON writes a letter either literally or as a \u escape,
# so a payload holding any \u takes the full parse, and the raw test is safe
# whatever writes the payload.
[[ "$INPUT" == *push* || "$INPUT" == *'\u'* ]] || exit 0

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
# This MUST stay above the first "$PYBIN" use below. It was previously sourced at the
# bottom of the flag-parsing block, so PYBIN was empty when line 16 ran, COMMAND came back
# empty, and the force-push block below was unreachable. Verified dead 2026-08-31: a
# `git push --force origin bannerlord-1.4.5` payload returned rc=0 with no output.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
# Uses the shared helper rather than a bespoke branch, so `tools/test_hooks.sh` check 5b
# can verify every blocking gate carries it. This hook had its own hand-rolled version and
# was the lone exception until 2026-08-31, which is exactly the convention drift
# hook-authoring.md warns about ("mirror the sibling's FULL convention set").
taom_pybin_degraded "validate-push" "this push against the protected-branch policy" jq && exit 0

# Prefer jq; fall back to "$PYBIN" for robust JSON. The grep+sed fallback this used to
# carry truncated the command at the first escaped quote, and since jq is NOT on PATH in
# this Git Bash install (verified 2026-08-20) that fallback was the only path ever taken.
# A truncated command can drop a trailing --force, which is what this gate exists to catch.
# Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi

# Protected branches: master and main (unused here, kept) and exactly the two live trunks.
# bannerlord-1.4.5 was missing until 2026-08-20, and when the release tags moved to
# bannerlord-1.5.x (v2.0.29 and v2.0.30 are on it) the list did not follow, so a force push
# to it passed unchallenged until plan 011. Named, not a bannerlord-* pattern (maintainer
# decision D30): a port branch such as bannerlord-1.5.0-port stays force-pushable.
is_protected() {
  case "$1" in
    master|main|bannerlord-1.4.5|bannerlord-1.5.x) return 0 ;;
    *) return 1 ;;
  esac
}

# Judge every command (maintainer decision D38, widened by the plan 011 review). `read -a`
# below takes one line, and until plan 011 it took only the command's first, so a `cd <dir>`
# line hid a force push on the next. The CR goes first: Python's print writes CRLF on Windows,
# so every non-final line of a multi-line command ends in one. A continued line (a trailing \ in
# bash, a trailing ` in PowerShell) is one command, so the continuations are joined. Then each
# command on a line is split out at ; & |: judging a whole line took its last word as the
# refspec, so `git push --force origin bannerlord-1.5.x 2>&1 | tail -5` passed.
#
# Two splits are judged, and either one blocks (plan 011 convergence). The quote-blind split
# (COMMAND) keeps `bash -c "git push ...; echo x"` refused, since a gate cannot tell quoted or
# heredoc text from a command that `bash -c` or `bash <<EOF` runs. Alone it under-blocks: a
# separator inside a quoted value (`git -C "E:/R&D" push --force ...`, `-o "a;b"`) cut git
# from push. So the command is also split at ; & | outside quotes only (QSEGS), with each
# shell's own escape, the splitter mark-verification-run.sh uses. With jq and no Python, QSEGS
# keeps whole lines, which over-block rather than under-block.
COMMAND=${COMMAND//$'\r'/}
COMMAND=${COMMAND//$'\\\n'/ }
COMMAND=${COMMAND//$'`\n'/ }
QSEGS=$COMMAND
if [ -n "$PYBIN" ]; then
  Q=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    cmd = (d.get("tool_input") or {}).get("command") or ""
    esc = "`" if d.get("tool_name") == "PowerShell" else "\\"
except Exception:
    sys.exit()
cmd = cmd.replace("\r", "")
out, q, i, n = [], "", 0, len(cmd)
while i < n:
    c = cmd[i]
    if c == esc and q != "\x27":
        nxt = cmd[i + 1:i + 2]
        out.append(" " if nxt == "\n" else c + nxt)
        i += 2
        continue
    if q:
        if c == q:
            q = ""
        out.append(" " if c == "\n" else c)
    elif c in "\"\x27":
        q = c
        out.append(c)
    elif c in ";&|\n":
        out.append("\n")
    else:
        out.append(c)
    i += 1
sys.stdout.buffer.write(("".join(out) + "\n").encode("utf-8"))
' 2>/dev/null)
  [ -n "$Q" ] && QSEGS=$Q
fi
COMMAND=${COMMAND//[;&|]/$'\n'}

BLOCK_TARGET=""
WARN_TARGET=""

# Judges one command. Sets BLOCK_TARGET on a force push to a protected branch, and WARN_TARGET
# on a plain push to one.
judge_command() {
  local CLEAN PUSH_IDX GIT_SEEN FORCE ALL f ref i j tok skip
  local -a TOKENS POSITIONAL REFS

  # Locate the `push` subcommand by TOKEN, not by the substring "git push".
  #
  # The old substring test missed `git -C <dir> push` and `git -c k=v push` entirely: those
  # never contain "git" adjacent to "push", so the hook exited 0 and the gate was blind to
  # them. Verified 2026-08-31: `git -C /e/repos/TAOM push --force origin master` returned
  # rc=0, silently. Quotes are flattened first so a wrapped form (bash -c "git push ...")
  # still tokenises; that also preserves the old behaviour of matching a quoted mention.
  # Parentheses go too, so a subshell `(git push ...)` still shows its `git`.
  CLEAN=${1//[\"\'()]/ }
  read -r -a TOKENS <<< "$CLEAN"

  PUSH_IDX=-1
  for i in "${!TOKENS[@]}"; do
    if [[ "${TOKENS[$i]}" == "push" && $i -gt 0 ]]; then PUSH_IDX=$i; break; fi
  done
  [[ $PUSH_IDX -lt 0 ]] && return 0

  # Require an actual `git` invocation before it, so `npm push` or a stray word cannot trip.
  GIT_SEEN=0
  for ((j = 0; j < PUSH_IDX; j++)); do
    case "${TOKENS[$j]}" in
      git | */git | git.exe | */git.exe) GIT_SEEN=1 ;;
    esac
  done
  [[ $GIT_SEEN -eq 0 ]] && return 0

  # Split the push arguments into force flags and positionals. A redirection (2>, >, 2>/dev/null;
  # the & of 2>&1 was split off above) and its target are never refspecs, but a word glued to
  # its front is (`bannerlord-1.5.x>/dev/null` hands git the bare branch), unless it is an fd
  # number or PowerShell's `*`. A token ending in < or > takes the next token as its target.
  FORCE=false
  ALL=false
  POSITIONAL=()
  skip=0
  for tok in "${TOKENS[@]:PUSH_IDX+1}"; do
    if (( skip )); then skip=0; continue; fi
    if [[ "$tok" == *[\<\>]* ]]; then
      [[ "$tok" == *[\<\>] ]] && skip=1
      tok=${tok%%[\<\>]*}
      [[ -z "$tok" || "$tok" =~ ^[0-9]+$ || "$tok" == '*' ]] && continue
    fi
    case "$tok" in
      --force | --force-with-lease | --force-with-lease=* | --force-if-includes | -f)
        FORCE=true; continue ;;
      --all | --branches) ALL=true; continue ;;
      --mirror) ALL=true; FORCE=true; continue ;;   # --mirror force-updates every ref
      -*f | -f*)
        # A bundled short flag such as -fu. Still a force push.
        case "$tok" in --*) ;; *) FORCE=true ;; esac
        continue ;;
      -*) continue ;;
    esac
    POSITIONAL+=("$tok")
  done

  if [[ "$FORCE" == true && "$ALL" == true ]]; then
    BLOCK_TARGET="every branch, both trunks included (--all or --mirror)"
    return 0
  fi

  # `git push <remote> <refspec>...`: every positional after the remote is a refspec, and
  # --force applies to all of them; judging only the last let `bannerlord-1.5.x feature` through.
  # With no refspec, git pushes the current branch.
  REFS=("${POSITIONAL[@]:1}")
  (( ${#REFS[@]} )) || REFS=("")

  # Normalise each refspec. Every form below reached is_protected unmatched before 2026-08-31
  # and so passed silently:
  #   +branch          a leading + IS force, with no flag anywhere on the line
  #   src:dst          only the destination matters
  #   refs/heads/x     fully-qualified destination
  #   HEAD / @         resolve to the branch checked out in the hook's cwd, which is the main
  #                    tree even for a push run in a worktree (hooks-catalog.md, known gap)
  for ref in "${REFS[@]}"; do
    f=$FORCE
    case "$ref" in +*) f=true; ref="${ref#+}" ;; esac
    ref="${ref##*:}"
    ref="${ref#refs/heads/}"
    if [[ -z "$ref" || "$ref" == "HEAD" || "$ref" == "@" ]]; then
      ref=$(git branch --show-current 2>/dev/null)
    fi
    if is_protected "$ref"; then
      if [[ "$f" == true ]]; then BLOCK_TARGET="$ref"; return 0; fi
      WARN_TARGET="$ref"
    fi
  done
}

while IFS= read -r SEG; do
  [[ "$SEG" == *push* ]] || continue
  judge_command "$SEG"
  [[ -n "$BLOCK_TARGET" ]] && break
done <<< "$COMMAND"$'\n'"$QSEGS"

# Hard-block force push to a protected branch
if [[ -n "$BLOCK_TARGET" ]]; then
  echo "BLOCKED: force push to '$BLOCK_TARGET' is not allowed. Do not retry with --no-verify or as a plain push; explain the block and ask the user whether to push to a non-protected branch." >&2
  exit 2
fi

# Warn on any push touching a protected branch
if [[ -n "$WARN_TARGET" ]]; then
  echo "WARNING: pushing to protected branch '$WARN_TARGET'. Confirm this is intentional." >&2
fi

exit 0
