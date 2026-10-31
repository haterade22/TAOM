#!/bin/bash
# SessionStart hook: upstream review drift, in a registration of its own (2026-10-16, the maintainer's request; until
# then this block ran last in session-start.sh and got what that hook's other checks left of one 10 s registration).
# yotthani's repos change every day, and TAOM adopts from them. The last reviewed SHAs live in
# docs/reviews/upstream-watch.json. The check needs the network: tools/upstream_watch.py makes one GitHub read per
# watched repo and one per watched release (UPSTREAM RELEASE lines) and caches a live answer for 20 hours, so a session start pays the network at most once a day; a
# failure is not cached and is retried at the next session start.
# Time budget: registered for 10 s; it never runs past 9 s from its first command (8.5 s, plus the 0.5 s kill grace).
# The tool gets what is left as `timeout` and as `--budget`, so each gh call is shortened to fit; under 1 s left it is
# not run at all.
# Fail-open, never silent: gh missing, logged out, offline or slow, a crash or a spent budget prints an UNCHECKED note,
# because silence would read as "nothing new". The tool itself prints the note for a missing state file.
# SessionStart hooks run in parallel, so these lines can land before or after session-start.sh's. tools/test_hooks.sh 7l.

# The digits of EPOCHREALTIME (bash 5.0 or later) are microseconds; it is empty in an older bash.
T0=${EPOCHREALTIME//[!0-9]/}
INPUT=$(cat)

# Only on a fresh startup, as session-start.sh: resume, compact and clear already have the context.
if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi
[[ "$SOURCE" == "startup" ]] || exit 0

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh" 2>/dev/null || PYBIN=""

# The ceiling of the variable inner bound below, in microseconds; tools/test_hooks.sh 3 reads it.
UP_BUDGET_MAX_US=8500000
if [[ -n "${PYBIN:-}" && -f tools/upstream_watch.py ]]; then
  UP_LEFT=$UP_BUDGET_MAX_US
  if [[ -n $T0 ]]; then
    UP_LEFT=$(( UP_BUDGET_MAX_US - ${EPOCHREALTIME//[!0-9]/} + T0 ))
    (( UP_LEFT > UP_BUDGET_MAX_US )) && UP_LEFT=$UP_BUDGET_MAX_US
  fi
  if (( UP_LEFT < 1000000 )); then
    echo "NOTE: upstream review drift UNCHECKED this session (session-start time budget spent)."
  else
    printf -v UP_BUDGET '%d.%06d' $(( UP_LEFT / 1000000 )) $(( UP_LEFT % 1000000 ))
    UPSTREAM_STATUS=$(timeout -k 0.5 "$UP_BUDGET" "$PYBIN" tools/upstream_watch.py status --brief --budget "$UP_BUDGET" 2>/dev/null)
    UPSTREAM_RC=$?
    if [[ $UPSTREAM_RC -eq 124 || $UPSTREAM_RC -eq 137 ]]; then
      echo "NOTE: upstream review drift UNCHECKED this session (the status call overran its ${UP_BUDGET%????} s budget)."
    elif [[ $UPSTREAM_RC -ne 0 ]]; then
      echo "NOTE: upstream review drift UNCHECKED this session (the status call exited ${UPSTREAM_RC})."
    elif [[ -n "$UPSTREAM_STATUS" ]]; then
      printf '%s\n' "$UPSTREAM_STATUS"
    fi
  fi
else
  echo "NOTE: upstream review drift UNCHECKED this session (no safe python or no tools/upstream_watch.py)."
fi
exit 0
