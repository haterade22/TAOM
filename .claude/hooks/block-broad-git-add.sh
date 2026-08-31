#!/usr/bin/env bash
# block-broad-git-add.sh
# PreToolUse(Bash) hook: confirm before a git command that stages EVERYTHING in the
# working tree, rather than the paths you actually touched. Emits permissionDecision
# "ask" (confirm), NOT "deny" — a deliberate sweep is still possible, you just have to
# approve it after reading what it would take. Allows everything else with `{}`.
#
# Guarded (every form that stages paths the caller did not name):
#   git add -A / --all / .      stages every change in the tree, including other sessions'
#   git add -u / --update       stages every tracked modification — same hazard
#   git commit -a / -am         stages every tracked modification, then commits it
#
# WHY THIS EXISTS. TAOM is routinely worked by more than one session at once, so the
# working tree regularly holds edits their author has not finished. CLAUDE.md has said
# "Stage explicitly (git add <paths>), never git add -A" since 2026-08-07, and the rule
# has been broken three times anyway:
#   2026-08-07  a rebase auto-stash was left unrestored; a session committed over the top
#               and another session's source silently reverted to HEAD.
#   2026-08-08  a CHANGELOG entry was appended into a file carrying live conflict markers
#               and swept into one commit with a second session's work.
#   2026-08-09  two #434 CHANGELOG entries were swept into an unrelated enlistment commit
#               (6d96f81d), which was then pushed. The prose half of a change landed under
#               a subject that does not describe it, and the code half did not land at all.
# Prose did not stop it because nothing enforced the prose. This does.
#
# The message lists what the sweep would actually stage, because the decision is not
# "did I mean to type -A" — it is "is every one of these files mine".
#
# Deliberately NOT guarded: `git add <explicit paths>` (the correct form, however many
# paths), `git add -p` (interactive, you see each hunk), `git commit` without -a.
#
# Detection is SEGMENT-ANCHORED, not substring-anywhere: the command is split on shell
# separators (| ; && ||) and each segment is checked only if it is an actual
# `git <subcommand>` invocation (after stripping env-var prefixes and git global flags).
# So `git commit -m "use git add -A"` and `echo "git add -A"` both ALLOW.
# Mirrors block-dangerous-git.sh's parser verbatim — same category, same conventions.
#
# Calibrated to TAOM: confirm-not-block; fail-open (any parse error → allow).
#
# Returns: {} to allow, {"permissionDecision":"ask","message":"..."} to confirm.

set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Extract tool_input.command. Prefer jq; fall back to python3 for robust JSON
# (handles escaped quotes). Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi

# Fail-open: nothing to inspect → allow.
[[ -z "${COMMAND:-}" ]] && { echo '{}'; exit 0; }

REASON=""

# Split on shell separators so a flag is only matched when it belongs to the segment's
# own git invocation, never as text inside a quoted arg or a pipe target.
SEGMENTS=$(printf '%s' "$COMMAND" | sed -E 's/&&|\|\||;|\|/\n/g')

while IFS= read -r seg; do
  [[ -n "$REASON" ]] && break
  seg="${seg#"${seg%%[![:space:]]*}"}"                       # ltrim
  # strip leading env-var assignments: VAR=value ...
  while [[ "$seg" =~ ^[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+(.*)$ ]]; do
    seg="${BASH_REMATCH[1]}"; seg="${seg#"${seg%%[![:space:]]*}"}"
  done
  # only inspect actual git invocations
  [[ "$seg" =~ ^git([[:space:]]|$) ]] || continue
  rest="${seg#git}"; rest="${rest#"${rest%%[![:space:]]*}"}"
  # strip git global flags that precede the subcommand
  while [[ "$rest" =~ ^(-c[[:space:]]+[^[:space:]]+|-C[[:space:]]+[^[:space:]]+|--git-dir[=[:space:]][^[:space:]]+|--work-tree[=[:space:]][^[:space:]]+|--no-pager|-p|--paginate|--bare|--no-replace-objects|--literal-pathspecs)[[:space:]]+(.*)$ ]]; do
    rest="${BASH_REMATCH[2]}"; rest="${rest#"${rest%%[![:space:]]*}"}"
  done

  # `git commit-tree` / `commit-graph` are different commands — never match them.
  [[ "$rest" =~ ^commit- ]] && continue

  # Strip quoted spans before looking at flags, so a MESSAGE that contains a flag is
  # never read as one: `git commit -m "fix: add -a flag"` must not trip the -a branch.
  # Segment-splitting alone does not cover this — the quote is inside the segment.
  rest=$(printf '%s' "$rest" | sed -E "s/\"[^\"]*\"//g; s/'[^']*'//g")

  if [[ "$rest" =~ ^add([[:space:]]|$) ]]; then
    after="${rest#add}"
    # -A / --all / -u / --update anywhere in the args, or a bare `.` / `./` pathspec.
    if [[ "$after" =~ (^|[[:space:]])(-[a-zA-Z]*A[a-zA-Z]*|--all)([[:space:]]|$) ]]; then
      REASON="git add -A stages every change in the tree, not the paths you touched"
    elif [[ "$after" =~ (^|[[:space:]])(-[a-zA-Z]*u[a-zA-Z]*|--update)([[:space:]]|$) ]]; then
      REASON="git add -u stages every tracked modification, not the paths you touched"
    elif [[ "$after" =~ (^|[[:space:]])\.(/)?([[:space:]]|$) ]]; then
      REASON="git add . stages every change under the current directory"
    fi
  elif [[ "$rest" =~ ^commit([[:space:]]|$) ]]; then
    after="${rest#commit}"
    # -a / --all, including bundled short flags like -am. Exclude --amend, which is a
    # different thing entirely and is gated by check-changelog-changed.sh.
    if [[ "$after" =~ (^|[[:space:]])--all([[:space:]]|$) ]]; then
      REASON="git commit --all stages every tracked modification before committing"
    elif [[ "$after" =~ (^|[[:space:]])-[a-zA-Z]*a[a-zA-Z]*([[:space:]]|$) ]]; then
      REASON="git commit -a stages every tracked modification before committing"
    fi
  fi
done <<< "$SEGMENTS"

# Not a broad-staging form → allow.
[[ -z "$REASON" ]] && { echo '{}'; exit 0; }

# Show what the sweep would actually take. The decision is "is every one of these mine",
# which the caller cannot make from the flag alone.
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }
DIRTY=$(git status --porcelain 2>/dev/null | sed 's/^...//' | head -20)
DIRTY_COUNT=$(git status --porcelain 2>/dev/null | grep -c . || true)
STASH_COUNT=$(git stash list 2>/dev/null | grep -c . || true)

FILE_LIST=""
if [[ -n "$DIRTY" ]]; then
  FILE_LIST=$(printf '%s' "$DIRTY" | tr '\n' '~' | sed 's/~/, /g; s/, $//')
  [[ "${DIRTY_COUNT:-0}" -gt 20 ]] && FILE_LIST="${FILE_LIST} (+$((DIRTY_COUNT - 20)) more)"
fi

STASH_NOTE=""
[[ "${STASH_COUNT:-0}" -gt 0 ]] && STASH_NOTE=" There ${STASH_COUNT} stash(es) present — an unrestored auto-stash is how this went wrong on 2026-08-07."

# Escape for JSON embedding (backslashes + quotes; Windows paths).
CMD_ESC=$(printf '%s' "$COMMAND" | sed 's/\\/\\\\/g; s/"/\\"/g')
FILES_ESC=$(printf '%s' "$FILE_LIST" | sed 's/\\/\\\\/g; s/"/\\"/g')

MSG="CONFIRM broad staging: ${REASON}. Command: \\\"${CMD_ESC}\\\". TAOM is worked by more than one session at a time, so the tree can hold edits you did not make — this has swept another session's work into the wrong commit three times (2026-08-07, 08-08, 08-09). Would stage ${DIRTY_COUNT:-0} path(s): ${FILES_ESC}.${STASH_NOTE} Approve ONLY if every one of those is yours. Otherwise cancel and stage explicitly: git add <paths>."

printf '{"permissionDecision":"ask","message":"%s"}\n' "$MSG"
exit 0
