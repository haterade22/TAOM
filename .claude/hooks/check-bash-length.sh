#!/bin/bash

# PreToolUse (Bash): refuse a command too long for the Bash tool on this machine, before it fails.
#
# WHY
# The harness retro of 2026-10-21 (#962, docs/reviews/harness-retro-2026-10-21.md) traced the most
# repeated tool error, "unexpected EOF while looking for matching", to command length, not to the
# quotes or heredocs CLAUDE.md had blamed: `bash -n` accepts 58 of 61 such commands, and of the Bash
# calls whose length with each ' counted as 4 is 8,000 or more, 140 of 142 failed (11 of 76 between
# 7,000 and 8,000, none below). The tool's single-quoted wrapper is cut short, so bash never finds
# its closing quote. Refusing costs nothing (the command would fail anyway) and the reason teaches
# the fix: write the script with the Write tool, or build the file in parts.
#
# Returns exit 2 to block, 0 to allow. Fail-open: an unparseable payload allows.

INPUT=$(cat)

# Prefilter: no command can reach the limit inside a payload this short. The limit is counted with
# each ' as 4 characters, and a command that dense with quotes (about 1 in 10) still needs some 5,800
# raw characters to reach it, while JSON escaping only lengthens the raw payload. So a payload under
# 5,500 characters exits here, with no Python start, on nearly every Bash call. tools/test_hooks.sh 4c.
(( ${#INPUT} < 5500 )) && exit 0

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "check-bash-length" "the Bash command length limit" && exit 0

LIMIT=7500
LENGTH=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import json, sys
try:
    cmd = json.loads(sys.stdin.read()).get("tool_input", {}).get("command") or ""
    print(len(cmd) + 3 * cmd.count(chr(39)))
except Exception:
    print(0)
' 2>/dev/null)
LENGTH=${LENGTH//[^0-9]/}

# Fail-open: nothing measured -> allow.
[[ -z "$LENGTH" ]] && exit 0

if (( LENGTH >= LIMIT )); then
  printf '%s\n' \
    "BLOCKED: this Bash command is ${LENGTH} characters long (each ' counts as 4), over the about ${LIMIT} the Bash tool runs on this machine." \
    "Longer commands fail with \"unexpected EOF while looking for matching\": 140 of 142 at 8,000 or more did (harness retro 2026-10-21)." \
    "Write the script to a file with the Write tool and run it by path. Without a Write tool, build the file in parts, each command under the limit:" \
    "a quoted heredoc into the file with cat >, the next parts appended with cat >>, then run the file." >&2
  exit 2
fi

exit 0
