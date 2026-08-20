#!/bin/bash
# PreToolUse hook: Warn before git push to protected branches.
# Hard-blocks force pushes to master (CLAUDE.md policy).
# Non-blocking warning for regular pushes to master/main.

INPUT=$(cat)

# Prefer jq; fall back to python3 for robust JSON. The grep+sed fallback this used to
# carry truncated the command at the first escaped quote, and since jq is NOT on PATH in
# this Git Bash install (verified 2026-08-20) that fallback was the only path ever taken.
# A truncated command can drop a trailing --force, which is what this gate exists to catch.
# Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  COMMAND=$(printf '%s' "$INPUT" | python3 -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi

# Only intercept git push commands
[[ ! "$COMMAND" =~ git[[:space:]]+push ]] && exit 0

# Detect force push
FORCE=false
if [[ "$COMMAND" =~ --force || "$COMMAND" =~ [[:space:]]-f([[:space:]]|$) ]]; then
  FORCE=true
fi

# Detect target branch: strip flags, take the last non-flag positional arg after "push"
# Handles "git push", "git push origin", "git push origin master", "git push --force origin master"
AFTER_PUSH=$(echo "$COMMAND" | sed -E 's/.*git[[:space:]]+push[[:space:]]*//')
POSITIONAL=""
for tok in $AFTER_PUSH; do
  [[ "$tok" == -* ]] && continue
  POSITIONAL="$POSITIONAL $tok"
done
# shellcheck disable=SC2086
set -- $POSITIONAL
if [[ $# -ge 2 ]]; then
  TARGET="${!#}"
else
  TARGET=$(git branch --show-current 2>/dev/null)
fi

# Strip surrounding quotes. `git push origin "master" --force` is a legal invocation and
# the token arrives here as literal "master", quotes included, so the comparisons below
# silently failed to match and the force-push block did not fire. Only observable once
# the jq fix above let the full command reach this code at all.
TARGET="${TARGET%\"}"; TARGET="${TARGET#\"}"
TARGET="${TARGET%\'}"; TARGET="${TARGET#\'}"

# Protected branches. bannerlord-1.4.5 is this repo's actual trunk and was missing until
# 2026-08-20, so a force push to the branch everyone works on passed unchallenged while
# master and main, which this repo does not use, were the only names guarded.
is_protected() {
  case "$1" in
    master|main|bannerlord-1.4.5) return 0 ;;
    *) return 1 ;;
  esac
}

# Hard-block force push to a protected branch
if [[ "$FORCE" == true ]] && is_protected "$TARGET"; then
  echo "BLOCKED: force push to '$TARGET' is not allowed. CLAUDE.md policy." >&2
  exit 2
fi

# Warn on any push touching a protected branch
if is_protected "$TARGET"; then
  echo "WARNING: pushing to protected branch '$TARGET'. Confirm this is intentional." >&2
fi

exit 0
