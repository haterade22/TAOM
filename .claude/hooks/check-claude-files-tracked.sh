#!/usr/bin/env bash
# check-claude-files-tracked.sh
# PreToolUse(Bash) hook: when `git commit` is about to run, check whether
# any file under .claude/skills/, .claude/agents/, or .claude/rules/ exists
# on disk but is gitignored. If so, refuse to commit — the file would silently
# not be shipped, defeating its purpose.
#
# Why: in efbde5b we shipped .claude/skills/freeze/ with check-freeze.sh
# excluded by .gitignore's bin/ pattern. The /freeze skill was non-functional
# for anyone cloning the repo. Codex caught it on review pass 1; this hook
# would have caught it pre-commit.
#
# Returns: {} to allow, {"permissionDecision":"deny", ...} to block.

set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

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
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

# DO NOT skip --amend. This hook checks working-tree state (files on disk
# vs git tracking), which is not amend-dependent. A gitignored file on disk
# is just as broken in an amended commit as in a fresh one — that's the
# bug class this hook catches. Codex review 2026-04-26 caught this as
# prevention-theater risk in the original implementation.

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

# Find every file under the protected directories that exists on disk.
# Then for each, ask git whether it's tracked OR ignored.
PROBLEMS=()
for dir in .claude/skills .claude/agents .claude/rules .claude/hooks; do
    [[ ! -d "$dir" ]] && continue
    while IFS= read -r f; do
        # Is the file gitignored?
        if git check-ignore -q "$f" 2>/dev/null; then
            PROBLEMS+=("$f (gitignored — will not commit)")
            continue
        fi
        # Is the file untracked AND unstaged? (Newly created and forgotten.)
        if ! git ls-files --error-unmatch "$f" >/dev/null 2>&1; then
            # Check whether it's at least staged.
            if ! git diff --cached --name-only 2>/dev/null | grep -qFx "$f"; then
                PROBLEMS+=("$f (untracked and unstaged)")
            fi
        fi
    done < <(find "$dir" -type f \( -name '*.md' -o -name '*.sh' -o -name '*.json' -o -name '*.yaml' -o -name '*.yml' \) 2>/dev/null)
done

if [[ ${#PROBLEMS[@]} -eq 0 ]]; then
    echo '{}'
    exit 0
fi

# Build an escaped JSON message.
MSG="[check-claude-files-tracked] These files exist under .claude/ but will NOT be committed:\\n"
for p in "${PROBLEMS[@]}"; do
    # Escape backslashes and quotes for JSON.
    p_esc=$(printf '%s' "$p" | sed 's/\\/\\\\/g; s/"/\\"/g')
    MSG+="  - ${p_esc}\\n"
done
MSG+="\\nA gitignored file silently breaks any skill or hook that depends on it (the original /freeze regression: bin/check-freeze.sh excluded by .gitignore's bin/ pattern). Either: (a) move the file out of the gitignored path with a descriptive directory name, (b) git add the untracked file, or (c) explicitly delete it if it's not meant to ship."

printf '{"permissionDecision":"deny","message":"%s"}\n' "$MSG"
