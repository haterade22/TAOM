#!/bin/bash
# PreToolUse hook: Warn before git push to protected branches.
# Hard-blocks force pushes to master (CLAUDE.md policy).
# Non-blocking warning for regular pushes to master/main.

INPUT=$(cat)

if command -v jq >/dev/null 2>&1; then
  COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')
else
  COMMAND=$(echo "$INPUT" | grep -oE '"command"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
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

# Hard-block force push to master/main
if [[ "$FORCE" == true && ( "$TARGET" == "master" || "$TARGET" == "main" ) ]]; then
  echo "BLOCKED: force push to '$TARGET' is not allowed. CLAUDE.md policy." >&2
  exit 2
fi

# Warn on any push touching master/main
if [[ "$TARGET" == "master" || "$TARGET" == "main" ]]; then
  echo "WARNING: pushing to protected branch '$TARGET'. Confirm this is intentional." >&2
fi

exit 0
