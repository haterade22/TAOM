#!/bin/bash
# PreToolUse hook (Bash and PowerShell): hard-blocks a force push to a protected branch
# (AGENTS.md "Git and commits"). It is the only force-push guard: GitHub protects no branch (D29).
# Non-blocking stderr warning (which Claude does not see) for a plain push to a protected branch.

# The deadline starts at the first command (#680). A killed gate fails open, so the judge gets what
# is left of 3.0 s and an overrun asks; the 10 s registration is only a backstop. The digits of
# EPOCHREALTIME (bash 5.0 or later) are microseconds; it is empty in an older bash.
T0=${EPOCHREALTIME//[!0-9]/}
INPUT=$(cat)

# Prefilter: a push is only judged at a `push` token, and Claude Code never escapes an
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
# bottom of the flag-parsing block, so PYBIN was empty when the reader ran, the command came
# back empty, and the force-push block was unreachable. Verified dead 2026-08-31: a
# `git push --force origin bannerlord-1.4.5` payload returned rc=0 with no output.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Protected branches: master and main (unused here, kept) and exactly the two live trunks.
# bannerlord-1.4.5 was missing until 2026-08-20, and when the release tags moved to
# bannerlord-1.5.x (v2.0.29 and v2.0.30 are on it) the list did not follow, so a force push
# to it passed unchallenged until plan 011. Named, not a bannerlord-* pattern (maintainer
# decision D30): a port branch such as bannerlord-1.5.0-port stays force-pushable. The trunks come
# first, so a pattern refspec such as refs/heads/* is reported by the branch it would hit. This
# array is the only list: the judge takes it as its arguments. An unforced delete of a trunk is not
# refused.
PROTECTED=(bannerlord-1.5.x bannerlord-1.4.5 main master)

# An ask, under hookSpecificOutput (a top-level decision is ignored, harness-facts.md): the user
# confirms or cancels. It prompts even in bypass mode, so it answers only a push this hook could not
# judge. $1 says why; it holds no quote or backslash, so it needs no JSON escaping.
ask() {
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"validate-push could not judge this push: %s. Cancel it if it force-pushes a protected branch (%s)."}}\n' \
    "$1" "${PROTECTED[*]}"
  exit 0
}

# No verdict: no safe Python, a payload the reader could not parse (it prints unread), or a judge
# that failed or printed something else. Nothing can read the command then, so the raw payload
# decides (maintainer decision, #680; the bash judge that ran here was deleted). A force marker
# asks: a short option holding f (-f, -vfu, and --force through its second dash), --m (--mirror and
# each prefix of it git accepts; it also asks on --message and the like, the safe side), a + (a
# +refspec), or a JSON \u escape, which could spell any of them. Anything else is allowed with a
# note. The scan is linear, about 85 ms a MB here with dash runs included, so it needs no size cap.
# It reads the payload from its "tool_input": key on, looked for in the first 2 KB only: the session
# id and transcript path before that key hold a UUID that usually matches, and a scan of the whole
# payload asked on most pushes (#680 review), which saw Claude Code 2.1.241 put the key at byte 445
# to 449. Past 2 KB, or with the key escaped, the whole payload is read, which only asks more. A
# search of the whole payload for the key was quadratic in its offset (17.7 s for one after 256 KB).
FORCE_MARK='-[^[:space:]-]*f|--m|\+|\\u'
coarse() {
  local lead=${INPUT:0:2048} text=$INPUT pre
  if [[ $lead == *'"tool_input":'* ]]; then
    pre=${lead%%'"tool_input":'*}
    text=${INPUT:${#pre}}
  fi
  [[ $text =~ $FORCE_MARK ]] && ask "$1, and its text holds a force marker"
  [[ -n $PYBIN ]] && echo "validate-push: $1, so a push whose raw text holds no force marker is NOT checked. Gate failed OPEN." >&2
  exit 0
}

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report". The shared
# helper names what goes unchecked (tools/test_hooks.sh 5b verifies every blocking gate carries it),
# and the coarse answer covers the rest.
taom_pybin_degraded "validate-push" "a push whose raw text holds no force marker" && coarse "no safe python"

# One Python run reads the payload and judges it (_shellwords.py verdict): push_candidates splits
# the command every way this gate reads it, and _pushjudge.py judges each line holding `push` until
# one refuses. The bash judge that ran here cost about 25 microseconds a word, so under load 250 KB
# of quoted text holding `push` outran the 5 s registration it then had, and the push ran unjudged
# (#680). The payload goes in through a pipe, never a here-string (#681: that hangs in bash itself,
# beyond any timeout). EPOCHREALTIME is the wall clock, which can be set back, so the budget is
# capped at 3.0 s as well.
LEFT=2500000
if [[ -n $T0 ]]; then
  LEFT=$(( 3000000 - ${EPOCHREALTIME//[!0-9]/} + T0 ))
  (( LEFT > 3000000 )) && LEFT=3000000
  (( LEFT < 300000 )) && ask "time ran out before it could be judged"
fi
printf -v BUDGET '%d.%06d' $(( LEFT / 1000000 )) $(( LEFT % 1000000 ))
VERDICT=$(printf '%s' "$INPUT" | timeout -k 0.3 "$BUDGET" "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" verdict "${PROTECTED[@]}" 2>/dev/null)
RC=$?
(( RC == 124 || RC == 137 )) && ask "the judge ran out of time"

TARGET=${VERDICT#*$'\t'}
case "$RC:$VERDICT" in
  0:block$'\t'?*)
    echo "BLOCKED: force push to '$TARGET' is not allowed. Do not retry with --no-verify or as a plain push; explain the block and ask the user whether to push to a non-protected branch." >&2
    exit 2 ;;
  0:warn$'\t'?*)
    echo "WARNING: pushing to protected branch '$TARGET'. Confirm this is intentional." >&2
    exit 0 ;;
  0:allow)
    exit 0 ;;
esac
coarse "the judge gave no verdict (exit $RC)"
