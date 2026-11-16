#!/bin/bash
# PreToolUse (Edit|Write): refuse an edit to a file in a checkout that sits on the trunk branch.
#
# WHY THIS EXISTS
# Many people and sessions merge PRs into bannerlord-1.5.x. A session that edits files in the trunk
# checkout leaves a dirty tree, and the dirty tree blocks fetch, pull and push for everyone who shares
# that checkout. The rule is AGENTS.md "Branch first"; this hook enforces it for Claude sessions.
#
# WHAT IT DOES
# 1. Reads file_path and session_id from the payload.
# 2. Finds the repository from the FILE's own directory, never from $CLAUDE_PROJECT_DIR: a session that
#    entered a worktree still runs the main checkout's copy of this hook (harness-facts.md).
# 3. Denies when that repository's HEAD is the trunk branch and the file is not gitignored.
# 4. Allows everything else: a file in no repository (memory, plans, scratchpad, the live Armory and
#    TAOM_Map), a gitignored file, a detached HEAD, any other branch.
#
# OVERRIDE
# /tmp/claude-trunk-override-<session id> unlocks one session, the config-protection.sh shape. Create it
# only on Mike's explicit word to edit on trunk, and delete it after.
#
# LIMITS
# It sees the Edit and Write tools only. A shell redirect, sed -i or a formatter run from Bash still
# writes on trunk; AGENTS.md carries the rule for those. It judges Claude sessions only.
#
# Fail-open: any parse or git failure allows. A deny is only ever the deliberate gate decision.
# The trunk branch name is the one constant below.

TRUNK_BRANCH="bannerlord-1.5.x"

INPUT=$(cat)

# Prefilter: the gated word is the trunk branch name, which the payload never holds. So the payload is
# parsed, but only for a file path; no Python starts for a payload with no file_path key.
[[ "$INPUT" == *file_path* || "$INPUT" == *'"file"'* || "$INPUT" == *'\u'* ]] \
  || { echo '{}'; exit 0; }

source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
taom_pybin_degraded "block-trunk-edit" "edits on the trunk branch" jq && { echo '{}'; exit 0; }

if [[ -n "$PYBIN" ]]; then
  PARSED=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    ti = d.get("tool_input") or {}
    path = (ti.get("file_path") or ti.get("file") or "").replace("\n", "")
    sid = str(d.get("session_id") or "").replace("\n", "")
    sys.stdout.buffer.write((path + "\n" + sid).encode("utf-8"))   # LF only: Windows text mode writes CRLF
except Exception:
    pass
' 2>/dev/null)
else
  PARSED=$(printf '%s' "$INPUT" | jq -r '(.tool_input.file_path // .tool_input.file // "") + "\n" + (.session_id // "")' 2>/dev/null)
fi
PARSED=${PARSED//$'\r'/}
FILE_PATH=${PARSED%%$'\n'*}
SESSION_ID=""
[[ "$PARSED" == *$'\n'* ]] && SESSION_ID=${PARSED#*$'\n'}

[[ -n "$FILE_PATH" ]] || { echo '{}'; exit 0; }

# The per-session override, named like config-protection.sh's. No id means no override, and the id keeps
# only the characters a session id holds, so it cannot name another path.
SESSION_ID=${SESSION_ID:-${CLAUDE_CODE_SESSION_ID:-${CLAUDE_SESSION_ID:-}}}
SESSION_ID=${SESSION_ID//[^A-Za-z0-9_-]/}
if [[ -n "$SESSION_ID" && -f "/tmp/claude-trunk-override-${SESSION_ID}" ]]; then
  echo '{}'
  exit 0
fi

# An absolute path only. Edit and Write always pass one; a relative path cannot be placed, so it allows.
FS_PATH=${FILE_PATH//\\//}
[[ "$FS_PATH" == /* || "$FS_PATH" =~ ^[A-Za-z]:/ ]] || { echo '{}'; exit 0; }

# The nearest existing directory: a Write may name a file in a folder that does not exist yet.
DIR=${FS_PATH%/*}
while [[ -n "$DIR" && "$DIR" == */* && ! -d "$DIR" ]]; do DIR=${DIR%/*}; done
[[ -d "$DIR" ]] || { echo '{}'; exit 0; }

TOP=$(timeout -k 1 3 git -C "$DIR" rev-parse --show-toplevel 2>/dev/null)
[[ -n "$TOP" ]] || { echo '{}'; exit 0; }

BRANCH=$(timeout -k 1 3 git -C "$TOP" symbolic-ref --short -q HEAD 2>/dev/null)
[[ "$BRANCH" == "$TRUNK_BRANCH" ]] || { echo '{}'; exit 0; }

# A gitignored file (a build output, a log, a state file) cannot dirty the tree for git.
if timeout -k 1 3 git -C "$TOP" check-ignore -q -- "$FS_PATH" 2>/dev/null; then
  echo '{}'
  exit 0
fi

REASON="[block-trunk-edit] $TOP is on $TRUNK_BRANCH, the shared trunk. Nothing is edited there: other people merge into it, and a dirty tree blocks their fetch, pull and push (AGENTS.md Branch first). Make a new branch, or a sibling worktree on E: when other sessions share this checkout (EnterWorktree, or git worktree add -b <branch> <path> $TRUNK_BRANCH), then edit the same file there. If Mike asked for this edit on trunk, he can create /tmp/claude-trunk-override-<session id>."
REASON=${REASON//\\/\\\\}
REASON=${REASON//\"/\\\"}
printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$REASON"
exit 0
