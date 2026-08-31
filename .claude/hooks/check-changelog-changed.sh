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

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Extract the bash command from tool_input.
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)

# Detect `git commit` invocations including `git -C <dir> commit` and
# `git -c <key>=<val> commit`. Reject `git commit-tree`, `commit-graph`, etc.
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree / commit-graph etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

# Detect amend. Critically: do NOT blanket-skip on amend — the amend workflow
# is commonly used to add forgotten files ("oops, amend"), which is exactly
# the case this hook needs to catch. Instead, on amend we check the FULL
# post-amend file set: staged changes plus the files already in the commit
# being amended (HEAD).
IS_AMEND=0
case "$COMMAND" in
    *"--amend"*) IS_AMEND=1 ;;
esac

# Get the staged file list (the diff that becomes part of the commit).
STAGED=$(git diff --cached --name-only 2>/dev/null)

# `git commit -- <paths>` commits the WORKING TREE state of those paths and
# ignores the index entirely, so `git diff --cached` can be empty (or name a
# completely different set) while the commit still lands .claude/ changes. That
# is not hypothetical: on 2026-08-18 a commit touching .claude/rules/ and
# .claude/skills/ sailed past this gate with no CHANGELOG entry, because it was
# made with the pathspec form. Parse the pathspec off the command line too.
#
# The separator is a standalone ` -- ` token, so this does not match --amend or
# --no-verify. Over-inclusion is the safe direction here: extra paths can only
# make the gate MORE likely to fire, and it fires only on .claude/, CLAUDE.md or
# AGENTS.md. Quoting is approximated (surrounding quotes stripped); a path with
# embedded spaces may split, which again only over-includes.
PATHSPEC_FILES=""
case "$COMMAND" in
    *" -- "*)
        PATHSPEC_FILES=$(printf '%s' "${COMMAND##* -- }" \
            | tr ' \t' '\n\n' \
            | sed -e 's/^["'"'"']//' -e 's/["'"'"']$//' \
            | grep -v '^$' || true)
        ;;
esac

# For amends, also include files that are already part of HEAD (the commit
# being amended). The post-amend commit will contain both sets.
if [[ $IS_AMEND -eq 1 ]]; then
    HEAD_FILES=$(git show HEAD --name-only --pretty=format: 2>/dev/null | grep -v '^$' || true)
    ALL_FILES=$(printf '%s\n%s\n%s\n' "$STAGED" "$HEAD_FILES" "$PATHSPEC_FILES" | sort -u | grep -v '^$')
else
    ALL_FILES=$(printf '%s\n%s\n' "$STAGED" "$PATHSPEC_FILES" | sort -u | grep -v '^$')
fi

if [[ -z "$ALL_FILES" ]]; then
    echo '{}'  # nothing in commit — let git produce its own error / message-only amend
    exit 0
fi

# Decide whether the post-amend commit requires a CHANGELOG entry.
NEEDS_CHANGELOG=0
while IFS= read -r f; do
    case "$f" in
        .claude/* | CLAUDE.md | AGENTS.md )
            NEEDS_CHANGELOG=1
            break
            ;;
    esac
done <<< "$ALL_FILES"

if [[ $NEEDS_CHANGELOG -eq 0 ]]; then
    echo '{}'  # no documentation-bearing change; skip
    exit 0
fi

# Is CHANGELOG.md in the post-amend commit (staged or already in HEAD)?
HAS_CHANGELOG=0
while IFS= read -r f; do
    [[ "$f" == "CHANGELOG.md" ]] && { HAS_CHANGELOG=1; break; }
done <<< "$ALL_FILES"

if [[ $HAS_CHANGELOG -eq 1 ]]; then
    echo '{}'
    exit 0
fi

# Fail the commit with a clear message.
cat <<'EOF'
{"permissionDecision":"deny","message":"[check-changelog-changed] This commit touches .claude/, CLAUDE.md, or AGENTS.md but does NOT include a CHANGELOG.md update. Per CLAUDE.md 'Documentation Requirements (MANDATORY)', every session must update CHANGELOG.md. Add a CHANGELOG entry under today's date and re-stage. To bypass intentionally (rare), use git commit --no-verify -- but this hook is independent so that flag won't help; instead, stage CHANGELOG.md."}
EOF
