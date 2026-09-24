#!/usr/bin/env bash
# block-dangerous-git.sh
# PreToolUse(Bash) hook: prompt for confirmation before a git command that can
# PERMANENTLY DISCARD uncommitted / unpushed work. Emits permissionDecision
# "ask" (confirm), NOT "deny" — a legitimate revert is still possible, you just
# have to approve it. Allows everything else with `{}`.
#
# Guarded (work-destroying ops TAOM did not previously cover):
#   git reset --hard        discards uncommitted working-tree + index changes
#   git clean -f[d]         deletes untracked files
#   git branch -D           force-deletes a branch (may orphan commits)
#   git checkout -- / . / ./ / dir/. / -f   discards working-tree changes
#   git restore <path>      discards working-tree changes (restore --staged-only is SAFE → allowed)
#   git stash drop|clear    discards stashed work
#
# Deliberately does NOT touch `git push` — owned by validate-push.sh.
#
# Detection is SEGMENT-ANCHORED, not substring-anywhere: the command is split on
# shell separators (| ; && ||) and each segment is checked only if it is an
# actual `git <subcommand>` invocation (after stripping env-var prefixes and git
# global flags like -C / -c / --no-pager). This avoids false-positives on
# commands that merely MENTION a destructive phrase as data — e.g.
# `echo "git reset --hard"`, `git log | grep "git reset --hard"`,
# `git commit -m "git reset --hard"` all ALLOW. (Found by review 2026-05-29.)
#
# Calibrated to TAOM: confirm-not-block; fail-open (any parse error → allow).
# Concept (recalibrated) from mattpocock/skills git-guardrails, whose version
# hard-blocked ALL pushes with no override — wrong for TAOM.
#
# Returns: {} to allow, {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"..."}} to confirm.

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
taom_pybin_degraded "block-dangerous-git" "destructive git commands" jq && { echo '{}'; exit 0; }

# Extract tool_input.command. Prefer jq; fall back to python3 for robust JSON
# (handles escaped quotes — the grep+sed fallback truncated those). Mirrors the
# parser in check-claude-files-tracked.sh.
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

# Split on shell separators so a destructive verb is only matched when it is the
# actual command of a segment, never as text inside a quoted arg or a pipe target.
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

  # `rest` now begins with the git subcommand + its args. Match destructive forms.
  if [[ "$rest" =~ ^reset([[:space:]].*)?[[:space:]]--hard ]]; then
    REASON="git reset --hard discards all uncommitted working-tree and index changes"
  elif [[ "$rest" =~ ^clean([[:space:]].*)?[[:space:]](-[a-zA-Z]*f|--force) ]]; then
    REASON="git clean -f deletes untracked files permanently"
  elif [[ "$rest" =~ ^branch([[:space:]].*)?[[:space:]](-D|--delete([[:space:]].*)?[[:space:]]--force|--force([[:space:]].*)?[[:space:]]--delete) ]]; then
    REASON="git branch -D force-deletes a branch and may orphan unmerged commits"
  elif [[ "$rest" =~ ^stash[[:space:]]+(drop|clear) ]]; then
    REASON="git stash drop/clear permanently discards stashed work"
  elif [[ "$rest" =~ ^checkout([[:space:]]|$) ]]; then
    after="${rest#checkout}"
    if [[ "$after" =~ (^|[[:space:]])(--([[:space:]]|$)|--force|-f([[:space:]]|$)|\.(/)?([[:space:]]|$)) ]] || [[ "$after" =~ /\.([[:space:]]|$) ]]; then
      REASON="git checkout discards working-tree changes for the named paths"
    fi
  elif [[ "$rest" =~ ^restore([[:space:]]|$) ]]; then
    # restore --staged WITHOUT --worktree only unstages (safe) → allow; else discards worktree.
    if [[ "$rest" =~ --staged ]] && [[ ! "$rest" =~ (--worktree|[[:space:]]-W([[:space:]]|$)) ]]; then
      :
    else
      REASON="git restore discards working-tree changes for the named paths"
    fi
  fi
done <<< "$SEGMENTS"

# Not a guarded op → allow.
[[ -z "$REASON" ]] && { echo '{}'; exit 0; }

MSG="CONFIRM destructive git op: ${REASON}. Command: \"${COMMAND}\". This can permanently lose uncommitted/unpushed work and TAOM has no undo for it. Approve ONLY if you intend to discard those changes — otherwise commit or stash first. (Pushes are guarded separately by validate-push.sh.)"

# JSON-encode the reason as a whole. Escaping only backslashes and quotes by hand left the raw
# newline of a multi-line Bash call inside the JSON string: invalid JSON, which the harness
# reads as allow, so no confirm appeared (#647 convergence review).
if [[ -n "${PYBIN:-}" ]]; then
  REASON_JSON=$(printf '%s' "$MSG" | "$PYBIN" -c 'import sys, json; sys.stdout.write(json.dumps(sys.stdin.read()))' 2>/dev/null)
else
  REASON_JSON=$(printf '%s' "$MSG" | jq -Rs . 2>/dev/null)
fi
if [[ -z "$REASON_JSON" ]]; then
  echo "[block-dangerous-git] could not encode the confirm message; this destructive op was NOT confirmed" >&2
  echo '{}'
  exit 0
fi

printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":%s}}\n' "$REASON_JSON"
exit 0
