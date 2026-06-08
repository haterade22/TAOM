#!/usr/bin/env bash
# check-freeze.sh — PreToolUse hook for /freeze skill
# Reads JSON from stdin, checks if file_path is within the freeze boundary.
# Returns {"permissionDecision":"deny","message":"..."} to block, or {} to allow.
#
# Adapted from gstack (https://github.com/garrytan/gstack/blob/main/freeze/bin/check-freeze.sh).
# TAOM differences:
#   - State file at .claude/tmp/freeze/ (project-local, gitignored)
#   - No telemetry to ~/.gstack/
#   - Windows / Git Bash path-normalization (cygpath when available)

set -uo pipefail

# Read tool input JSON from stdin.
INPUT=$(cat)

# Locate the freeze state file. CLAUDE_PROJECT_DIR is set by Claude Code at hook invocation.
PROJ_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
STATE_DIR="$PROJ_DIR/.claude/tmp/freeze"
FREEZE_FILE="$STATE_DIR/freeze-dir.txt"

# No freeze configured -> allow everything.
if [[ ! -f "$FREEZE_FILE" ]]; then
    echo '{}'
    exit 0
fi

# Read the boundary path verbatim. Earlier versions used `tr -d '[:space:]'`
# which destroyed legitimate spaces in paths like
# `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/...`.
# IFS= read -r preserves the line as-is; we only strip trailing CR/LF.
IFS= read -r FREEZE_DIR < "$FREEZE_FILE" || true
FREEZE_DIR="${FREEZE_DIR%$'\r'}"  # strip trailing CR if file has CRLF endings
if [[ -z "$FREEZE_DIR" ]]; then
    echo '{}'
    exit 0
fi

# Extract file_path from tool_input JSON.
# Try grep first (cheap, works for flat JSON), Python fallback for escaped quotes / nested.
FILE_PATH=$(printf '%s' "$INPUT" \
    | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | head -1 \
    | sed 's/.*:[[:space:]]*"//;s/"$//' \
    || true)

if [[ -z "$FILE_PATH" ]]; then
    # Python fallback. Try python3 first, then python.
    for PY in python3 python py; do
        if command -v "$PY" >/dev/null 2>&1; then
            FILE_PATH=$(printf '%s' "$INPUT" | "$PY" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("file_path", ""))
except Exception:
    pass
' 2>/dev/null || true)
            [[ -n "$FILE_PATH" ]] && break
        fi
    done
fi

# If we still cannot extract a file path, allow (don't block on parse failure).
if [[ -z "$FILE_PATH" ]]; then
    echo '{}'
    exit 0
fi

# --- Path normalization for Windows / Git Bash ---
# Convert Windows backslashes to forward slashes so case-style comparison works.
FILE_PATH=$(printf '%s' "$FILE_PATH" | sed 's|\\|/|g')
FREEZE_DIR=$(printf '%s' "$FREEZE_DIR" | sed 's|\\|/|g')

# If cygpath is present, normalize to Unix style for both.
if command -v cygpath >/dev/null 2>&1; then
    FILE_PATH=$(cygpath -u "$FILE_PATH" 2>/dev/null || echo "$FILE_PATH")
    FREEZE_DIR=$(cygpath -u "$FREEZE_DIR" 2>/dev/null || echo "$FREEZE_DIR")
fi

# Resolve relative file path against the project root.
case "$FILE_PATH" in
    /* | [A-Za-z]:/* ) ;; # absolute already (Unix or Windows-style)
    *) FILE_PATH="$PROJ_DIR/$FILE_PATH" ;;
esac

# Collapse double slashes and trailing slash.
FILE_PATH=$(printf '%s' "$FILE_PATH" | sed 's|/\+|/|g;s|/$||')
FREEZE_DIR=$(printf '%s' "$FREEZE_DIR" | sed 's|/\+|/|g;s|/$||')

# Resolve via cd+pwd to handle .. and symlinks (POSIX-portable).
_resolve_path() {
    local _dir _base
    _dir="$(dirname "$1")"
    _base="$(basename "$1")"
    _dir="$(cd "$_dir" 2>/dev/null && pwd -P || printf '%s' "$_dir")"
    if [[ "$_base" == "/" || -z "$_base" ]]; then
        printf '%s' "$_dir"
    else
        printf '%s/%s' "$_dir" "$_base"
    fi
}
FILE_PATH=$(_resolve_path "$FILE_PATH")
FREEZE_DIR=$(_resolve_path "$FREEZE_DIR")

# Reject if either path failed to resolve to absolute. Without an absolute
# path, the boundary check is meaningless — fail-open (allow) is safer than
# silently denying every edit.
case "$FREEZE_DIR" in
    /* | [A-Za-z]:/* ) ;; # absolute, OK
    *)
        # Boundary state is malformed — clear it implicitly by allowing.
        echo '{}'
        exit 0
        ;;
esac
case "$FILE_PATH" in
    /* | [A-Za-z]:/* ) ;;
    *)
        echo '{}'
        exit 0
        ;;
esac

# Case-insensitive comparison on Windows (NTFS is case-insensitive by default).
shopt -s nocasematch 2>/dev/null || true

# Escape both paths for safe embedding in JSON output. Real Windows paths
# routinely contain backslashes; if they reach printf without escaping, the
# resulting JSON is malformed (\U / \m become invalid escape sequences) and
# the harness will likely fail-open silently, allowing the edit. Quote chars
# are unlikely in TAOM paths but cheap to handle.
_json_escape() {
    local s="$1"
    s="${s//\\/\\\\}"   # \  ->  \\
    s="${s//\"/\\\"}"   # "  ->  \"
    printf '%s' "$s"
}

case "$FILE_PATH" in
    "$FREEZE_DIR" | "$FREEZE_DIR"/* )
        # Inside boundary — allow.
        echo '{}'
        ;;
    *)
        # Outside boundary — deny.
        FILE_PATH_JSON=$(_json_escape "$FILE_PATH")
        FREEZE_DIR_JSON=$(_json_escape "$FREEZE_DIR")
        printf '{"permissionDecision":"deny","message":"[freeze] Blocked: %s is outside the freeze boundary (%s/). Run /unfreeze to release the boundary, or pick a wider scope when you started /freeze."}\n' \
            "$FILE_PATH_JSON" "$FREEZE_DIR_JSON"
        ;;
esac
