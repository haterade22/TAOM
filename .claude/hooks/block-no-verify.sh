#!/bin/bash

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
# PreToolUse(Bash): refuse any git command carrying --no-verify.
#
# WHY THIS IS NOT ALSO A BUILD GATE
# This shipped as check-build-before-commit.sh and ran `dotnet build` before every
# `git commit`. It never once did so: it read tool_input.command with a bare `jq`, and
# jq is NOT on PATH in this Git Bash install (verified 2026-08-20), so COMMAND was always
# empty, the `git commit` test never matched, and BOTH halves of the hook were inert.
#
# Fixing the parse alone would have re-armed the build on every commit, which is wrong
# here for two measured reasons. Hooks run with cwd set to the MAIN project directory
# regardless of where the command executes (proven 2026-08-20: a `dotnet build` run from
# a worktree updated the main tree's .verification-ran marker), so a commit made in a
# worktree would be gated on a different tree's build state. And the build lacked
# `-p:DisableModuleCopy=true -p:ModuleId=`, so with Bannerlord running the module copy
# fails and would block the commit for a reason unrelated to the code.
#
# Verification is already covered without either flaw: check-verification-evidence.sh
# reminds at Stop when C# changed with no build/test since, and /verify runs the real
# thing. So this keeps the half that is cheap and correct.
#
# Returns exit 2 to block, 0 to allow. Fail-open: an unparseable payload allows.

INPUT=$(cat)

# Prefer jq; fall back to python3 for robust JSON (handles escaped quotes).
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

# Fail-open: nothing to inspect -> allow.
[[ -z "${COMMAND:-}" ]] && exit 0

# Only git commands. --no-verify means something else entirely to other tools.
[[ ! "$COMMAND" =~ (^|[[:space:]])git([[:space:]]|$) ]] && exit 0

if [[ "$COMMAND" =~ --no-verify ]]; then
  echo "BLOCKED: --no-verify is not allowed. Fix what is making the hook fail instead of bypassing it." >&2
  exit 2
fi

exit 0
