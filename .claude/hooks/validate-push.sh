#!/bin/bash
# PreToolUse hook: Warn before git push to protected branches.
# Hard-blocks force pushes to master (CLAUDE.md policy).
# Non-blocking warning for regular pushes to master/main.

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
# This MUST stay above the first "$PYBIN" use below. It was previously sourced at the
# bottom of the flag-parsing block, so PYBIN was empty when line 16 ran, COMMAND came back
# empty, and the force-push block below was unreachable. Verified dead 2026-08-31: a
# `git push --force origin bannerlord-1.4.5` payload returned rc=0 with no output.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Prefer jq; fall back to python3 for robust JSON. The grep+sed fallback this used to
# carry truncated the command at the first escaped quote, and since jq is NOT on PATH in
# this Git Bash install (verified 2026-08-20) that fallback was the only path ever taken.
# A truncated command can drop a trailing --force, which is what this gate exists to catch.
# Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
elif [[ -z "$PYBIN" ]]; then
  # No jq and no safe interpreter: fail open per harness-facts.md, but say so. Silence
  # here would read as "not a push", which is the failure mode this hook exists to avoid.
  echo "validate-push: no jq and no safe python; push NOT checked against branch policy." >&2
  exit 0
else
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi

# Locate the `push` subcommand by TOKEN, not by the substring "git push".
#
# The old substring test missed `git -C <dir> push` and `git -c k=v push` entirely: those
# never contain "git" adjacent to "push", so the hook exited 0 and the gate was blind to
# them. Verified 2026-08-31: `git -C /e/repos/TAOM push --force origin master` returned
# rc=0, silently. Quotes are flattened first so a wrapped form (bash -c "git push ...")
# still tokenises; that also preserves the old behaviour of matching a quoted mention.
CLEAN=${COMMAND//\"/ }
CLEAN=${CLEAN//\'/ }
read -r -a TOKENS <<< "$CLEAN"

PUSH_IDX=-1
for i in "${!TOKENS[@]}"; do
  if [[ "${TOKENS[$i]}" == "push" && $i -gt 0 ]]; then PUSH_IDX=$i; break; fi
done
[[ $PUSH_IDX -lt 0 ]] && exit 0

# Require an actual `git` invocation before it, so `npm push` or a stray word cannot trip.
GIT_SEEN=0
for ((j = 0; j < PUSH_IDX; j++)); do
  case "${TOKENS[$j]}" in
    git | */git | git.exe | */git.exe) GIT_SEEN=1 ;;
  esac
done
[[ $GIT_SEEN -eq 0 ]] && exit 0

# Split the push arguments into force flags and positionals.
FORCE=false
POSITIONAL=()
for tok in "${TOKENS[@]:PUSH_IDX+1}"; do
  case "$tok" in
    --force | --force-with-lease | --force-with-lease=* | --force-if-includes | -f)
      FORCE=true; continue ;;
    -*f | -f*)
      # A bundled short flag such as -fu. Still a force push.
      case "$tok" in --*) ;; *) FORCE=true ;; esac
      continue ;;
    -*) continue ;;
  esac
  POSITIONAL+=("$tok")
done

# `git push <remote> <refspec>`: the refspec is the last positional. With fewer than two,
# git pushes the current branch.
if [[ ${#POSITIONAL[@]} -ge 2 ]]; then
  TARGET="${POSITIONAL[${#POSITIONAL[@]} - 1]}"
else
  TARGET=""
fi

# Strip surrounding quotes. `git push origin "master" --force` is a legal invocation and
# the token arrives here as literal "master", quotes included, so the comparisons below
# silently failed to match and the force-push block did not fire.
TARGET="${TARGET%\"}"; TARGET="${TARGET#\"}"
TARGET="${TARGET%\'}"; TARGET="${TARGET#\'}"

# Normalise the refspec. Every form below reached is_protected unmatched before
# 2026-08-31 and so passed silently:
#   +branch          a leading + IS force, with no flag anywhere on the line
#   src:dst          only the destination matters
#   refs/heads/x     fully-qualified destination
#   HEAD / @         resolve to the branch actually checked out
case "$TARGET" in
  +*) FORCE=true; TARGET="${TARGET#+}" ;;
esac
TARGET="${TARGET##*:}"
TARGET="${TARGET#refs/heads/}"
if [[ -z "$TARGET" || "$TARGET" == "HEAD" || "$TARGET" == "@" ]]; then
  TARGET=$(git branch --show-current 2>/dev/null)
fi

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
