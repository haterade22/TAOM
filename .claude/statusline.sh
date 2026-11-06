#!/bin/bash
# Claude Code status line: receives JSON on stdin, emits one line, or two with usage data.
# Format: ctx: N% | model | branch | Ns/Nu/Nt   (counts omitted when the tree is clean)
#         5h: N% (resets 1h30m) | week: N% (!) (resets 1d4h) | cache: warm until HH:MM
#         The second line appears only when Claude Code sends rate_limits (#1012); (!) marks 80%+.
#         Tests: tools/tests/test_statusline.py.
#         Shown by the terminal CLI and the VS Code extension's terminal mode only; the graphical
#         panel shows a context indicator in the prompt box and /usage instead (2026-10-26).
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
# Context use from the context window's own key (#1012): "used_percentage" also names each
# rate-limit window, so the first one in the payload can be the 5-hour usage. Only the context
# window carries remaining_percentage; the old read is the fallback.
REMAINING=$(json_num remaining_percentage || true)
if [[ -n "$REMAINING" ]]; then
  USED_PCT=$((100 - REMAINING))
else
  USED_PCT=$(json_num used_percentage || true)
fi
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

# --- usage line (#1012): the subscription windows and the prompt cache ---------------
# Only when Claude Code sends rate_limits (a subscriber login, after the first response). Pure
# bash: $EPOCHSECONDS and printf %()T, no subprocess. The idea comes from jarrodwatts/claude-hud
# (docs/reference/provenance-register.md); no code of it is used.
NOW="${TAOM_STATUSLINE_NOW:-$EPOCHSECONDS}"
# The helpers set variables instead of printing: on Git Bash every $( ) is a fork, and four of
# them doubled the repaint time (133 ms to 257 ms on 2026-10-26).
obj_of() {                        # $1 = key -> OBJ = the flat object after "key":{, up to its first }
    OBJ="${INPUT#*\"$1\":\{}"
    [ "$OBJ" = "$INPUT" ] && return 1
    OBJ="${OBJ%%\}*}"
}
num_of() {                        # $1 = key -> NUM = value of "key":123 inside OBJ
    NUM="${OBJ#*\"$1\":}"
    [ "$NUM" = "$OBJ" ] && return 1
    NUM="${NUM#"${NUM%%[!' ']*}"}"
    NUM="${NUM%%[!0-9]*}"
    [ -n "$NUM" ]
}
left_of() {                       # $1 = epoch seconds -> LEFT = "45m", "1h30m", "1d4h" or "now"
    local s=$(( $1 - NOW ))
    if (( s <= 0 )); then LEFT=now
    elif (( s < 3600 )); then printf -v LEFT '%dm' $(( s / 60 ))
    elif (( s < 86400 )); then printf -v LEFT '%dh%02dm' $(( s / 3600 )) $(( s % 3600 / 60 ))
    else printf -v LEFT '%dd%dh' $(( s / 86400 )) $(( s % 86400 / 3600 ))
    fi
}
USAGE=()
add_window() {                    # $1 = label, $2 = object key -> appends "5h: 25% (resets 1h30m)"
    obj_of "$2" && num_of used_percentage || return 0
    local w="$1: ${NUM}%"
    (( NUM >= 80 )) && w+=" (!)"
    if num_of resets_at; then left_of "$NUM"; w+=" (resets $LEFT)"; fi
    USAGE+=("$w")
}
if [[ "$INPUT" == *'"rate_limits":{'* ]]; then
  add_window 5h five_hour
  add_window week seven_day
  if obj_of prompt_cache; then
    if [[ "$OBJ" == *'"warm":true'* ]] && num_of expires_at; then
      printf -v T '%(%H:%M)T' "$NUM"
      USAGE+=("cache: warm until $T")
    else
      USAGE+=("cache: cold")
    fi
  fi
  if (( ${#USAGE[@]} )); then
    line="${USAGE[0]}"
    for w in "${USAGE[@]:1}"; do line+=" | $w"; done
    printf '\n%s' "$line"
  fi
fi
