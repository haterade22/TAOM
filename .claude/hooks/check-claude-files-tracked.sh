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
# Returns: {} to allow, {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny", ...}} to block.

set -uo pipefail

INPUT=$(cat)

# Prefilter: every decision below needs the text `git` in the command, and Claude Code never
# escapes an ASCII letter, so a raw payload without it cannot concern this gate. Exiting
# here skips the _pybin.sh probe and the parse (two Python starts) on most Bash calls.
# Match the raw text, never a token regex: a newline before `git` arrives as \n.
# tools/test_hooks.sh 4c checks both directions.
[[ "$INPUT" == *git* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "check-claude-files-tracked" "untracked or gitignored files under .claude/" && { echo '{}'; exit 0; }

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

# Two bulk queries answer for every protected file at once. Until #647 this asked git two or
# three times per file (about 200 files), took 6 s against its 5 s registration, and the
# harness killed it on every commit, which reads as a pass (Codex review 2026-09-23).
#   --others --ignored --exclude-standard: untracked AND gitignored (a tracked file is never
#     "ignored"), so it will not commit;
#   --others --exclude-standard: untracked and not ignored, i.e. created and never staged (a
#     staged new file is in the index and is not listed).
# Bounded under the registered 5 s, and an overrun asks rather than passing in silence.
DIRS=(.claude/skills .claude/agents .claude/rules .claude/hooks)
IGNORED=$(timeout -k 1 3 git ls-files --others --ignored --exclude-standard -- "${DIRS[@]}" 2>/dev/null)
RC1=$?
UNTRACKED=$(timeout -k 1 3 git ls-files --others --exclude-standard -- "${DIRS[@]}" 2>/dev/null)
RC2=$?
if [[ $RC1 -eq 124 || $RC2 -eq 124 ]]; then
    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"[check-claude-files-tracked] git ls-files overran its 3 s budget, so this commit is UNCHECKED for untracked or gitignored files under .claude/. This is NOT a pass. Run: git ls-files --others -- .claude/skills .claude/agents .claude/rules .claude/hooks"}}'
    exit 0
fi

PROBLEMS=$(
    { printf '%s\n' "$IGNORED" | sed '/^$/d; s/$/ (gitignored: will not commit)/'
      printf '%s\n' "$UNTRACKED" | sed '/^$/d; s/$/ (untracked and unstaged)/'
    } | grep -E '\.(md|sh|json|ya?ml) \(' | sed 's/^/  - /'
)

if [[ -z "$PROBLEMS" ]]; then
    echo '{}'
    exit 0
fi

MSG="[check-claude-files-tracked] These files exist under .claude/ but will NOT be committed:
${PROBLEMS}

A gitignored file silently breaks any skill or hook that depends on it (the original /freeze regression: bin/check-freeze.sh excluded by .gitignore's bin/ pattern). Either: (a) move the file out of the gitignored path with a descriptive directory name, (b) git add the untracked file, or (c) explicitly delete it if it's not meant to ship."

# JSON-encode the reason as a whole (paths may hold quotes, backslashes or newlines).
REASON_JSON=$(printf '%s' "$MSG" | "$PYBIN" -c 'import sys, json; sys.stdout.write(json.dumps(sys.stdin.read()))' 2>/dev/null)
if [[ -z "$REASON_JSON" ]]; then
    echo "[check-claude-files-tracked] could not encode the deny message; this commit was NOT checked" >&2
    echo '{}'
    exit 0
fi
printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$REASON_JSON"
