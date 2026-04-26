#!/usr/bin/env bash
# check-changelog-changed.sh
# PreToolUse(Bash) hook: when `git commit` is about to run with .claude/ or
# CLAUDE.md or AGENTS.md changes staged, refuse to commit unless CHANGELOG.md
# is also in the staged set.
#
# Why: across three review passes on the Tier 1 adoption (efbde5b, 5df21ea)
# we shipped two commits without updating CHANGELOG.md despite the mandatory
# rule in CLAUDE.md "Documentation Requirements". Codex caught it both times.
# This hook catches it FIRST so the commit doesn't ship.
#
# Returns: {} to allow, {"permissionDecision":"deny", "message":"..."} to block.

set -uo pipefail

INPUT=$(cat)

# Extract the bash command from tool_input.
COMMAND=$(printf '%s' "$INPUT" | python3 -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)

# Only fire on actual git commit invocations.
case "$COMMAND" in
    *"git commit"*) ;;  # proceed
    *) echo '{}'; exit 0 ;;
esac

# Skip amends — they're meant to update an existing commit; CHANGELOG status
# is the prior commit's responsibility.
case "$COMMAND" in
    *"--amend"*) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

# Get the staged file list.
STAGED=$(git diff --cached --name-only 2>/dev/null)
if [[ -z "$STAGED" ]]; then
    echo '{}'  # nothing staged — let git produce its own error
    exit 0
fi

# Decide whether the staged set requires a CHANGELOG entry.
NEEDS_CHANGELOG=0
while IFS= read -r f; do
    case "$f" in
        .claude/* | CLAUDE.md | AGENTS.md )
            NEEDS_CHANGELOG=1
            break
            ;;
    esac
done <<< "$STAGED"

if [[ $NEEDS_CHANGELOG -eq 0 ]]; then
    echo '{}'  # no documentation-bearing change; skip
    exit 0
fi

# Is CHANGELOG.md also staged?
HAS_CHANGELOG=0
while IFS= read -r f; do
    [[ "$f" == "CHANGELOG.md" ]] && { HAS_CHANGELOG=1; break; }
done <<< "$STAGED"

if [[ $HAS_CHANGELOG -eq 1 ]]; then
    echo '{}'
    exit 0
fi

# Fail the commit with a clear message.
cat <<'EOF'
{"permissionDecision":"deny","message":"[check-changelog-changed] This commit touches .claude/, CLAUDE.md, or AGENTS.md but does NOT include a CHANGELOG.md update. Per CLAUDE.md 'Documentation Requirements (MANDATORY)', every session must update CHANGELOG.md. Add a CHANGELOG entry under today's date and re-stage. To bypass intentionally (rare), use git commit --no-verify -- but this hook is independent so that flag won't help; instead, stage CHANGELOG.md."}
EOF
