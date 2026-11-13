#!/usr/bin/env bash
# _session_ledger.sh: sourced by the hooks that keep the per-session edit ledger. Never registered.
#
# WHY THIS EXISTS
# check-verification-evidence.sh judged every dirty *.cs in the tree against one shared build marker,
# so a session was told to build C# that another session had edited (32 Stop blocks in 16 sessions in
# one week, 16 of them for another session's files: docs/reviews/harness-retro-2026-11-10.md P1,
# #1138). The fix judges the session, not the tree. Three hooks share the pieces below:
#   record-session-edit.sh (PostToolUse Edit|Write) appends to <id>.edits and writes <id>.warned
#   mark-verification-run.sh (PostToolUse Bash|PowerShell, PostToolUseFailure) touches <id>.verified
#   check-verification-evidence.sh (Stop) reads <id>.edits and <id>.verified, reads every other
#     session's .edits as claims, keeps <id>.reminded and <id>.tree.reminded
#
# THE LEDGER
# Folder: $CLAUDE_PROJECT_DIR/.claude/logs/sessions (gitignored with .claude/logs/). A subagent's
# payload carries its parent's session_id, so its edits and builds count for the parent session.
#   <id>.edits     one line per Edit or Write: <epoch><TAB><path>, path normalised as below
#   <id>.verified  touched when a build or test ran (mtime is the signal)
#   <id>.reminded  the Stop hook's streak marker for the session's own edits
#   <id>.tree.reminded  its streak marker for C# that no ledger holds
#   <id>.warned    <other id><TAB><path, as edited>: the cross-session notes already given
#
# No Python here except the \u branch of taom_payload_file_path: the Stop and Bash hooks fire often,
# and a regex on the raw payload is enough (the cwd regex in enter_payload_tree,
# check-verification-evidence.sh).
# Every function sets a variable rather than printing, so a caller pays no subshell.

# TAOM_SID: the payload's session_id, kept to [A-Za-z0-9_-] (the config-protection.sh rule) and cut
# to 64 characters; empty when absent. A quote inside a JSON string is always escaped, so an escaped
# copy of the key inside a message or file content cannot match.
taom_session_id() {
    local re='"session_id"[[:space:]]*:[[:space:]]*"(([^"\\]|\\.)*)"' v=""
    [[ "$1" =~ $re ]] && v=${BASH_REMATCH[1]}
    v=${v//[!A-Za-z0-9_-]/}
    # shellcheck disable=SC2034  # read by the hooks that source this file
    TAOM_SID=${v:0:64}
}

# TAOM_LEDGER_DIR: the absolute ledger folder.
taom_ledger_dir() {
    # shellcheck disable=SC2034  # read by the hooks that source this file
    TAOM_LEDGER_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}/.claude/logs/sessions"
}

# TAOM_PATH: $1, a raw JSON string value, with each escaped backslash (\\) and escaped slash (\/)
# turned into /, \" into ", and any backslash left into /. The original case is kept; every
# comparison folds case, because NTFS ignores it. \\ goes first and straight to /, so \\\" (an
# escaped backslash, then an escaped quote) gives /".
taom_ledger_norm_path() {
    local p="$1" sl=/ q='"'
    p=${p//\\\\/$sl}
    p=${p//\\\//$sl}
    p=${p//\\\"/$q}
    TAOM_PATH=${p//\\/$sl}
}

# TAOM_PATH from tool_input.file_path. Returns 1, leaving TAOM_PATH empty, when the payload has no
# path or cannot be read. A raw value holding a real \u escape is parsed with "$PYBIN" instead,
# never skipped (maintainer decision D40); when that fails, nothing is recorded. The escaped
# backslash pairs are dropped before that test, so C:\\proj\\units (an escaped backslash before a
# u) is no escape and needs no interpreter.
taom_payload_file_path() {
    local re='"file_path"[[:space:]]*:[[:space:]]*"(([^"\\]|\\.)*)"' raw out
    TAOM_PATH=""
    [[ "$1" =~ $re ]] || return 1
    raw=${BASH_REMATCH[1]}
    out=${raw//\\\\/}
    if [[ "$out" != *'\u'* ]]; then
        taom_ledger_norm_path "$raw"
        [[ -n "$TAOM_PATH" ]]
        return
    fi
    source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh" 2>/dev/null
    [[ -n "${PYBIN:-}" ]] || return 1
    # Bounded below the 5 s registration. Prints the path; a path holding a control character (a
    # decoded \u000a) is refused, because it would break the ledger's line format.
    out=$(printf '%s' "$1" | timeout -k 1 2 "$PYBIN" -c '
import json, sys
d = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
p = (d.get("tool_input") or {}).get("file_path")
if not isinstance(p, str) or not p or any(ord(c) < 32 for c in p):
    sys.exit(1)
sys.stdout.write(p.replace("\\", "/"))
' 2>/dev/null) || return 1
    # shellcheck disable=SC2034  # read by the hooks that source this file
    TAOM_PATH=$out
    [[ -n "$TAOM_PATH" ]]
}
