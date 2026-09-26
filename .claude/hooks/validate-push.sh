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

# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command (braces, the & call operator, comments and
# backtick escapes resolved) and names git `git` wherever it is the command (`GIT`, `git.exe`, a
# path). Python first, so the CI runner (which has jq) reads PowerShell too; with jq and no Python
# the raw command is judged as Bash text. The grep+sed fallback this once carried truncated the
# command at the first escaped quote, which could drop a trailing --force.
if [ -n "$PYBIN" ]; then
  COMMAND=$(taom_hook_command posix validate-push)
else
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
fi

# Protected branches: master and main (unused here, kept) and exactly the two live trunks.
# bannerlord-1.4.5 was missing until 2026-08-20, and when the release tags moved to
# bannerlord-1.5.x (v2.0.29 and v2.0.30 are on it) the list did not follow, so a force push
# to it passed unchallenged until plan 011. Named, not a bannerlord-* pattern (maintainer
# decision D30): a port branch such as bannerlord-1.5.0-port stays force-pushable.
PROTECTED=(master main bannerlord-1.4.5 bannerlord-1.5.x)
is_protected() {
  local p
  for p in "${PROTECTED[@]}"; do [[ "$1" == "$p" ]] && return 0; done
  return 1
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
# from push. So the command is also split at ; & | outside quotes only (QSEGS): _shellwords.py
# segments, shared with mark-verification-run.sh, over the command already read as POSIX-shell
# text, so its escape is always \ (plan 027). With jq and no Python, QSEGS keeps whole lines.
# The quote-blind split reads both that POSIX text and the raw command (plan 027 review), so
# reading PowerShell never makes this gate refuse less than it did before.
# Neither a whole line nor a quoted split is safe on its own: an apostrophe in a heredoc line
# (`Don't push`) opens a quote that never closes and glues the later lines into one segment,
# where the first `push` word is not the push (a # comment is dropped from both splits since plan
# 027, so a comment's apostrophe no longer does this). judge_command therefore anchors on the
# first `push` with a `git` before it (plan 011 final convergence). The same glue can also refuse
# a later, unrelated command after such a heredoc line; that over-block stays, since it errs on
# the safe side and ending a quote at a newline would let a quoted value spanning lines cut git
# from push.
COMMAND=${COMMAND//$'\r'/}
COMMAND=${COMMAND//$'\\\n'/ }
COMMAND=${COMMAND//$'`\n'/ }
QSEGS=$COMMAND
RAW=""
if [ -n "$PYBIN" ]; then
  Q=$(printf '%s' "$INPUT" | "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" segments 2>/dev/null)
  [ -n "$Q" ] && QSEGS=$Q
  # The raw command too, read as before plan 027 (CR dropped, continued lines joined). The
  # reader turns PowerShell ( ) into statement breaks and keeps a comma list in one word, so on
  # its text alone a Start-Process argument list ('push','--force',...), `& ("git") push ...` or
  # a refspec in parentheses passed, and all of them were refused before (plan 027 review).
  # Judging the raw text as well means reading PowerShell never makes this gate refuse less.
  RAW=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    c = str((d.get("tool_input") or {}).get("command") or "")
    c = c.replace("\r", "").replace("\\\n", " ").replace("`\n", " ")
    sys.stdout.buffer.write(c.encode("utf-8", "replace"))
except Exception:
    pass
' 2>/dev/null)
  [[ "$RAW" == "$COMMAND" ]] && RAW=""
fi
# The quote-blind split: both texts are cut at every ; & | first, and only then is a # comment
# dropped, per segment. A # that starts a word opens a comment in both shells, so a trunk named
# only in a trailing comment (`git push --force origin feature # bannerlord-1.5.x later`) is no
# longer refused (plan 027). Never strip per line before the split: a # inside a quoted value
# (`git log --grep "fix #1"; git push ...`) would delete every later command on its line, and
# after a heredoc line holding an apostrophe the quoted split cannot back that up. One tr and one
# sed over the whole text keep it linear (a bash read loop over both texts of a 1 MB PowerShell
# command took over 5 s). A command that is only a comment leaves the segments output empty, so
# QSEGS keeps the whole text and a trunk named there is still refused: a harmless over-block.
COMMAND=$(printf '%s\n%s\n' "$COMMAND" "$RAW" | tr ';&|' '\n\n\n' | sed -E 's/^#.*//; s/[[:space:]]#.*//')

BLOCK_TARGET=""
WARN_TARGET=""

# Judges one command. Sets BLOCK_TARGET on a force push to a protected branch, and WARN_TARGET
# on a plain push to one.
judge_command() {
  local CLEAN PUSH_IDX GIT_SEEN FORCE ALL f ref i j tok skip p
  local -a TOKENS POSITIONAL REFS

  # Locate the `push` subcommand by TOKEN, not by the substring "git push".
  #
  # The old substring test missed `git -C <dir> push` and `git -c k=v push` entirely: those
  # never contain "git" adjacent to "push", so the hook exited 0 and the gate was blind to
  # them. Verified 2026-08-31: `git -C /e/repos/TAOM push --force origin master` returned
  # rc=0, silently. Quotes are flattened first so a wrapped form (bash -c "git push ...")
  # still tokenises; that also preserves the old behaviour of matching a quoted mention.
  # Parentheses go too, so a subshell `(git push ...)` still shows its `git`.
  # Braces and backticks go too (plan 027): PowerShell's `{git push ...}` and bash's
  # `` x=`git push ...` `` still show their `git` token. The closing brace in the bracket is
  # escaped (\}): a bare } there ends ${...} early, bash -n still passes, and every call then
  # prints "}: command not found" with CLEAN empty, which allows every force push.
  CLEAN=${1//[\"\'(){\}\`]/ }
  read -r -a TOKENS <<< "$CLEAN"

  # Anchor on the first `push` with an actual `git` token before it, so `npm push` or a stray
  # word cannot trip, and a `push` word that an unclosed quote glued in front (a comment or
  # heredoc line such as "don't push") cannot hide the real push after it (plan 011 final
  # convergence: anchoring on the first `push` word returned early there, and a trunk force
  # push passed).
  PUSH_IDX=-1
  GIT_SEEN=0
  for i in "${!TOKENS[@]}"; do
    tok=${TOKENS[$i]}
    # git in any case and by any path is git (`GIT`, `git.exe`, C:\...\git.exe). The reader names it
    # `git` already; this keeps the jq-only fallback honest (plan 027). The subcommand stays exact:
    # git rejects `PUSH`.
    case "${tok,,}" in
      git | */git | *\\git | git.exe | */git.exe | *\\git.exe) GIT_SEEN=1; continue ;;
    esac
    if [[ "$tok" == push ]] && (( GIT_SEEN )); then PUSH_IDX=$i; break; fi
  done
  [[ $PUSH_IDX -lt 0 ]] && return 0

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
      -o | --push-option | --repo | --receive-pack | --exec)
        # The next word is this option's value, never the remote: `-o ci.skip origin` on a trunk
        # took ci.skip for the remote and origin for the refspec, and passed (plan 027).
        skip=1; continue ;;
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
    ref="${ref#refs/}"
    ref="${ref#heads/}"     # git reads a destination `heads/x` as refs/heads/x (plan 027)
    if [[ -z "$ref" || "$ref" == "HEAD" || "$ref" == "@" ]]; then
      # Asked once per run: a spawn per segment (about 28 ms) let 100 such lines outrun the
      # 5 s registration, and a killed gate fails open (plan 011 final convergence).
      [[ -n ${CUR_BRANCH+x} ]] || CUR_BRANCH=$(git branch --show-current 2>/dev/null)
      ref=$CUR_BRANCH
    fi
    if [[ "$ref" == *'*'* ]]; then
      # A pattern refspec (refs/heads/*, refs/heads/bannerlord-*) updates every branch it matches,
      # so it is judged as each protected name it matches (plan 027).
      for p in "${PROTECTED[@]}"; do
        # shellcheck disable=SC2053  # $ref is the refspec's glob, matched as a pattern on purpose
        if [[ "$p" == $ref ]]; then ref=$p; break; fi
      done
    fi
    if is_protected "$ref"; then
      if [[ "$f" == true ]]; then BLOCK_TARGET="$ref"; return 0; fi
      WARN_TARGET="$ref"
    fi
  done
}

# Most segments appear in both splits; a verdict depends only on the segment, so each distinct
# segment is judged once.
declare -A JUDGED
unset CUR_BRANCH
while IFS= read -r SEG; do
  [[ "$SEG" == *push* ]] || continue
  [[ -n ${JUDGED["$SEG"]+x} ]] && continue
  JUDGED["$SEG"]=1
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
