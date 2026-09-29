#!/usr/bin/env bash
# block-dangerous-git.sh
# PreToolUse (Bash and PowerShell) hook: prompt for confirmation before a git command that can
# PERMANENTLY DISCARD uncommitted / unpushed work. Emits permissionDecision
# "ask" (confirm), NOT "deny" — a legitimate revert is still possible, you just
# have to approve it. Allows everything else with `{}`.
#
# Guarded (work-destroying ops TAOM did not previously cover):
#   git reset --hard        discards uncommitted working-tree + index changes
#   git clean -f[d]         deletes untracked files
#   git branch -D           force-deletes a branch (may orphan commits); also -d -f, -df, --delete -f
#   git checkout -- / . / ./ / dir/. / -f   discards working-tree changes
#   git switch --discard-changes / -f / --force   the same, spelled with switch
#   git restore <path>      discards working-tree changes (--staged / -S alone is SAFE → allowed)
#   git stash drop|clear    discards stashed work
#   git worktree remove --force / -f   deletes a worktree with its uncommitted and untracked files
#   git reflog expire|delete, git update-ref -d / --stdin / <all-zero id>   remove the recovery
#                           net every other op relies on
# Long options match by git's unambiguous-prefix rule (long_is): --del, --forc, --disc, --work.
#
# Left ungated: checkout -B, switch -C and branch -f move an existing branch, which the reflog
# can undo. Known gaps (recorded follow-ups): git global options the stripper below does not know
# (-P, --no-optional-locks) hide the subcommand, and a `;` inside a quoted argument splits a
# segment. The 2026-09-29 additions follow ECC's gateguard list (docs/reviews/adopt-ecc-2026-09-29.md).
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
# Never skip on an escape: JSON writes a letter either literally or as a \u escape,
# so a payload holding any \u takes the full parse, and the raw test is safe
# whatever writes the payload.
# git in any case (plan 027): the reader reads GIT as git, so the raw test must let it through.
[[ "$INPUT" == *[Gg][Ii][Tt]* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "block-dangerous-git" "destructive git commands" jq && { echo '{}'; exit 0; }

# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path). Without Python it reads the raw command with jq, as Bash text.
COMMAND=$(taom_hook_command posix block-dangerous-git)

# Fail-open: nothing to inspect → allow.
[[ -z "${COMMAND:-}" ]] && { echo '{}'; exit 0; }

REASON=""

# long_is WORD --option: WORD names --option the way git reads it. git takes any unambiguous prefix
# of a long option (--del is --delete, --forc is --force; validate-push learned this in #689), and
# `--opt=value` carries its value in the same word. Four characters minimum: shorter prefixes are
# ambiguous for every option gated here. A prefix git would reject as ambiguous only costs a prompt.
long_is() {
  local w=${1%%=*}
  [[ ${#w} -ge 4 && "$2" == "$w"* ]]
}

# Split on shell separators so a destructive verb is only matched when it is the
# actual command of a segment, never as text inside a quoted arg or a pipe target.
SEGMENTS=$(printf '%s' "$COMMAND" | sed -E 's/&&|\|\||;|\|/\n/g')

# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text, and a killed gate fails open (#681). An empty segment is dropped,
# which is harmless: it is no git invocation.
set -f; IFS=$'\n'; SEG_LIST=($SEGMENTS); IFS=$' \t\n'; set +f
for seg in "${SEG_LIST[@]}"; do
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
  elif [[ "$rest" =~ ^branch([[:space:]]|$) ]]; then
    # -D, or delete plus force in any spelling: -d -f, -df, -fd, --delete --force, --del --forc.
    del=0; frc=0
    set -f; WORDS=($rest); set +f
    for w in "${WORDS[@]:1}"; do
      if [[ "$w" == --* ]]; then
        long_is "$w" --delete && del=1
        long_is "$w" --force && frc=1
      elif [[ "$w" == -* ]]; then
        [[ "$w" == *D* ]] && { del=1; frc=1; }
        [[ "$w" == *d* ]] && del=1
        [[ "$w" == *f* ]] && frc=1
      fi
    done
    (( del && frc )) && REASON="git branch -D force-deletes a branch and may orphan unmerged commits"
  elif [[ "$rest" =~ ^switch([[:space:]]|$) ]]; then
    # --discard-changes, --force and -f throw away working-tree changes, like checkout -f.
    set -f; WORDS=($rest); set +f
    for w in "${WORDS[@]:1}"; do
      if [[ "$w" == --* ]]; then
        { long_is "$w" --discard-changes || long_is "$w" --force; } \
          && REASON="git switch --discard-changes/-f discards working-tree changes"
      elif [[ "$w" == -* && "$w" == *f* ]]; then
        REASON="git switch --discard-changes/-f discards working-tree changes"
      fi
    done
  elif [[ "$rest" =~ ^worktree[[:space:]]+remove([[:space:]]|$) ]]; then
    # Without --force git refuses a dirty worktree itself; with it, uncommitted and untracked work goes.
    set -f; WORDS=($rest); set +f
    for w in "${WORDS[@]:2}"; do
      if { [[ "$w" == --* ]] && long_is "$w" --force; } || [[ "$w" != --* && "$w" == -* && "$w" == *f* ]]; then
        REASON="git worktree remove --force deletes the worktree with its uncommitted and untracked files"
      fi
    done
  elif [[ "$rest" =~ ^reflog[[:space:]]+(expire|delete)([[:space:]]|$) ]]; then
    REASON="git reflog expire/delete removes the history that makes every other lost commit recoverable"
  elif [[ "$rest" =~ ^update-ref([[:space:]].*)?[[:space:]](-d|--stdin)([[:space:]]|$) ]] \
       || [[ "$rest" =~ ^update-ref[[:space:]].*[[:space:]]0{40}(0{24})?([[:space:]]|$) ]]; then
    # -d, a `delete <ref>` line on --stdin, or the all-zero id as the new value all delete a ref.
    # (update-ref has no --delete.)
    REASON="git update-ref deletes a ref directly and may orphan its commits"
  elif [[ "$rest" =~ ^stash[[:space:]]+(drop|clear) ]]; then
    REASON="git stash drop/clear permanently discards stashed work"
  elif [[ "$rest" =~ ^checkout([[:space:]]|$) ]]; then
    after="${rest#checkout}"
    if [[ "$after" =~ (^|[[:space:]])(--([[:space:]]|$)|--force|-f([[:space:]]|$)|\.(/)?([[:space:]]|$)) ]] || [[ "$after" =~ /\.([[:space:]]|$) ]]; then
      REASON="git checkout discards working-tree changes for the named paths"
    fi
  elif [[ "$rest" =~ ^restore([[:space:]]|$) ]]; then
    # restore --staged (or -S) WITHOUT --worktree (or -W) only unstages (safe) → allow; else
    # discards worktree. Short flags bundle: -SW is both. -s takes the rest of its word as the
    # source tree, so the S in -sSTABLE is part of a tree name, never --staged.
    staged=0; wt=0
    set -f; WORDS=($rest); set +f
    for w in "${WORDS[@]:1}"; do
      if [[ "$w" == --* ]]; then
        long_is "$w" --staged && staged=1
        long_is "$w" --worktree && wt=1
      elif [[ "$w" == -* ]]; then
        flags=${w#-}; flags=${flags%%s*}
        [[ "$flags" == *S* ]] && staged=1
        [[ "$flags" == *W* ]] && wt=1
      fi
    done
    if (( staged && ! wt )); then
      :
    else
      REASON="git restore discards working-tree changes for the named paths"
    fi
  fi
done

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
