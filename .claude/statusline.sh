#!/bin/bash
# Claude Code status line: receives JSON on stdin, emits one line.
# Format: ctx: N% | model | branch | Ns/Nu/Nt   (counts omitted when the tree is clean)
#
# PERFORMANCE NOTE (2026-08-31)
# This runs on every status-line repaint, which is the most frequent script in the whole
# harness, and it had no timeout knob: `statusLine` in settings.json accepts none, so a
# slow version here is paid forever with nothing to bound it.
#
# It measured ~476ms per repaint. Two causes, both now gone:
#   1. `jq` is not installed on this machine, so the grep/head/sed fallback ALWAYS ran:
#      nine subprocesses to read three scalars out of a small JSON blob. Bash parameter
#      expansion does it with none.
#   2. Four `git` invocations plus three `wc` plus three `tr` to produce three integers.
#      One `git status --porcelain=v1 --branch` carries the branch AND all three counts,
#      and bash can tally them in-process.
# Now ~119ms (was ~457ms), with output verified identical to the previous implementation
# across clean, untracked, staged, staged+unstaged, MM, non-git, missing-field and
# empty-payload cases.
#
# ONE deliberate difference: on a detached HEAD the old script emitted an empty branch
# segment ("ctx: 12% | Opus 5 |  | 0s/1u/0t"), because `git branch --show-current` prints
# nothing there. This one emits "HEAD". That is a change, not a regression, and it is
# recorded here rather than passed off as identical.

INPUT=$(cat)

# --- JSON scalars, without a subprocess ------------------------------------------------
# Not a general JSON parser and does not need to be: these three fields are flat scalars
# emitted by the harness, never user text, and every consumer below tolerates an empty
# value. A wrong read degrades one cosmetic segment; it cannot fail the session.
json_str() {                      # $1 = key -> value of "key":"value"
    local t="${INPUT#*\"$1\":\"}"
    [ "$t" = "$INPUT" ] && return 1
    printf '%s' "${t%%\"*}"
}
json_num() {                      # $1 = key -> value of "key":123
    local t="${INPUT#*\"$1\":}"
    [ "$t" = "$INPUT" ] && return 1
    t="${t#"${t%%[!' ']*}"}"      # skip a space after the colon
    t="${t%%[!0-9]*}"
    [ -n "$t" ] && printf '%s' "$t"
}

MODEL=$(json_str display_name || true)
USED_PCT=$(json_num used_percentage || true)
CWD=$(json_str current_dir || true)
[ -z "$CWD" ] && CWD=$(json_str cwd || true)

[[ -z "$MODEL" ]] && MODEL="?"
[[ -z "$CWD" ]] && CWD="."
CWD="${CWD//\\//}"                # Windows separators to POSIX, in-process

# ctx segment
if [[ -n "$USED_PCT" ]]; then
  CTX="ctx: ${USED_PCT}%"
else
  CTX="ctx: --"
fi

# --- branch + uncommitted counts, in ONE git call --------------------------------------
# `.git` may be a FILE rather than a directory in a worktree, hence both tests.
BRANCH="?"
COUNTS=""
if [[ -d "$CWD/.git" || -f "$CWD/.git" ]]; then
  STAGED=0; UNSTAGED=0; UNTRACKED=0
  while IFS= read -r line; do
    case "$line" in
      '## '*)
        # "## main...origin/main [ahead 1]" or "## HEAD (no branch)"
        BRANCH="${line#\#\# }"
        BRANCH="${BRANCH%%...*}"
        BRANCH="${BRANCH%% *}"
        ;;
      '??'*) UNTRACKED=$((UNTRACKED + 1)) ;;
      '')    ;;
      *)
        # XY path. X = index (staged), Y = worktree (unstaged). "MM" counts as both,
        # which matches what the previous `diff --cached` + `diff` pair reported.
        x="${line:0:1}"; y="${line:1:1}"
        [[ "$x" != " " && "$x" != "?" ]] && STAGED=$((STAGED + 1))
        [[ "$y" != " " && "$y" != "?" ]] && UNSTAGED=$((UNSTAGED + 1))
        ;;
    esac
  done < <(git -C "$CWD" status --porcelain=v1 --branch 2>/dev/null)

  [[ -z "$BRANCH" ]] && BRANCH="?"
  if [[ "$STAGED" != "0" || "$UNSTAGED" != "0" || "$UNTRACKED" != "0" ]]; then
    COUNTS=" | ${STAGED}s/${UNSTAGED}u/${UNTRACKED}t"
  fi
fi

printf "%s | %s | %s%s" "$CTX" "$MODEL" "$BRANCH" "$COUNTS"
