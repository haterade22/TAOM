#!/bin/bash
# PostToolUse (Edit|Write): record which file this session edited, and note once when another
# session edited the same file in the last 24 hours.
#
# WHY THIS EXISTS
# check-verification-evidence.sh (Stop) judged every dirty *.cs in the tree, so it told a session to
# build C# that another session had edited: 16 of 32 Stop blocks in one week
# (docs/reviews/harness-retro-2026-11-10.md P1, #1138). This hook keeps the ledger the Stop hook now
# reads, so the reminder judges the session, not the tree. The ledger format and the shared parsing
# are in _session_ledger.sh.
#
# WHAT IT DOES
# 1. Appends <epoch><TAB><path> to .claude/logs/sessions/<session id>.edits on every call, for every
#    file type. A subagent's payload carries its parent's session_id, so its edits count there.
# 2. When that ledger is new, deletes ledger files older than 7 days. A ledger over 4,000 lines
#    keeps its last 2,000 (the size cap in hook-authoring.md).
# 3. Cross-session note: when another session's ledger, modified in the last 24 hours, holds the same
#    path (case folded) with an epoch in the last 24 hours, and this session has not been told about
#    that (other session, path) pair (<id>.warned), it records the pairs, then sends one
#    additionalContext naming them. One awk reads every candidate ledger. A path under a user's
#    .claude folder gets no note: it sits in no checkout, so git diff cannot show it.
# 4. Cross-worktree note (#1248): the same repo-relative path in another worktree of this checkout
#    matches too, in this ledger folder and in each other worktree's, and the note says the two
#    sessions may be doing one task twice and names tools/work_claims.py. Two sessions once fixed one
#    red trunk test at once, one in a sibling worktree and one in the main checkout.
#
# Channel: PostToolUse additionalContext, the check-polearm-shield-parity.sh shape (exit-0 stderr
# reaches only the debug log). Never blocks: always exits 0, and any failure exits 0 silently. No
# session id or no path: no output and nothing written. Starts one git process (bounded to 2 s) per
# call, and no interpreter unless the path arrives with a \u escape (D40).

INPUT=$(cat)

source "$(dirname "${BASH_SOURCE[0]}")/_session_ledger.sh" 2>/dev/null || exit 0

taom_session_id "$INPUT"
[[ -n "$TAOM_SID" ]] || exit 0
taom_payload_file_path "$INPUT" || exit 0

taom_ledger_dir
DIR=$TAOM_LEDGER_DIR
SID=$TAOM_SID
FILE_PATH=$TAOM_PATH
EDITS="$DIR/$SID.edits"
WARNED="$DIR/$SID.warned"

# Bash's own clock (an environment value cannot override it) or date: digits either way.
NOW=${EPOCHSECONDS:-$(date +%s)}

if [[ ! -f "$EDITS" ]]; then
    mkdir -p "$DIR" 2>/dev/null || exit 0
    # 10,080 minutes is 7 days. Bounded to the one folder.
    find "$DIR" -maxdepth 1 -type f \( -name '*.edits' -o -name '*.verified' -o -name '*.reminded' -o -name '*.warned' \) \
        -mmin +10080 -delete 2>/dev/null
fi
printf '%s\t%s\n' "$NOW" "$FILE_PATH" >> "$EDITS" 2>/dev/null || exit 0

LINES=$(wc -l < "$EDITS" 2>/dev/null)
LINES=${LINES//[!0-9]/}
if (( 10#${LINES:-0} > 4000 )); then
    # A temp name per process: two subagents of one session can cross the cap together, and a
    # shared name let one mv a file the other had just truncated, which emptied the ledger.
    tail -n 2000 "$EDITS" > "$EDITS.$$.tmp" 2>/dev/null && mv -f "$EDITS.$$.tmp" "$EDITS" 2>/dev/null
    rm -f "$EDITS.$$.tmp" 2>/dev/null
fi

# A file under a user's .claude folder (memory, plans, settings) sits in no checkout, so a note that
# says to read its git diff is wrong. The ledger keeps the line. The pattern is harness_retro.py's
# _CLAUDE_HOME, plus the Git Bash /c/ form.
HOME_RE='^(/[a-z]|[a-z]:)/users/[^/]+/\.claude/'
[[ "${FILE_PATH,,}" =~ $HOME_RE ]] && exit 0

# The same repo file in another worktree of this checkout (#1248): two sessions in two worktrees of
# one repo edit two absolute paths for one file, so an absolute match alone never sees the other.
# One git call, bounded below the 5 s registration, lists the worktrees. The longest root that holds
# this path is this file's worktree; the same relative path under each other root is a candidate,
# and each root's own ledger folder is read too, because a session started in a sibling worktree
# keeps its ledger there. Outside a git checkout, or when git fails, only the absolute path matches.
FP_LOW=${FILE_PATH,,}
OWN_ROOT=""
ROOTS=()
WT_OUT=$(timeout -k 1 2 git -C "${FILE_PATH%/*}" worktree list --porcelain 2>/dev/null)
set -f; IFS=$'\n'; WT_LINES=($WT_OUT); IFS=$' \t\n'; set +f
for l in "${WT_LINES[@]}"; do
    [[ "$l" == "worktree "* ]] || continue
    r=${l#worktree }
    r=${r//\\//}
    r=${r%/}
    [[ -n "$r" ]] || continue
    ROOTS+=("$r")
    r_low=${r,,}
    if [[ "$FP_LOW" == "$r_low/"* ]] && (( ${#r} > ${#OWN_ROOT} )); then OWN_ROOT=$r; fi
done
CANDS=""
LEDGERS=("$DIR")
DIR_LOW=${DIR//\\//}
DIR_LOW=${DIR_LOW,,}
if [[ -n "$OWN_ROOT" ]]; then
    REL=${FILE_PATH:${#OWN_ROOT}+1}
    for r in "${ROOTS[@]}"; do
        [[ -d "$r/.claude/logs/sessions" && "${r,,}/.claude/logs/sessions" != "$DIR_LOW" ]] && LEDGERS+=("$r/.claude/logs/sessions")
        [[ "$r" == "$OWN_ROOT" ]] || CANDS+="$r/$REL"$'\n'
    done
fi

# Other sessions' ledgers touched in the last 24 hours (1,440 minutes), read by one awk: the latest
# epoch per other session for this path or a candidate, and the path that session edited, skipping
# pairs already in this session's .warned. Paths compare lower-cased on both sides by awk, so the
# comparison is one function. The .warned file (or /dev/null) is always the first file operand, so
# awk tells it apart by ARGV[1]: a -v copy of the name would have its backslashes read as escapes and
# never match. The candidates arrive through the environment for the same reason.
SEEN=$WARNED
[[ -f "$SEEN" ]] || SEEN=/dev/null
MIN=$((NOW - 86400))
HITS=$(TAOM_WT_CANDS="$CANDS" find "${LEDGERS[@]}" -maxdepth 1 -type f -name '*.edits' ! -name "$SID.edits" -mmin -1440 \
    -exec awk -F '\t' -v p="$FILE_PATH" -v min="$MIN" '
        BEGIN {
            lp = tolower(p); want[lp] = 1
            n = split(ENVIRON["TAOM_WT_CANDS"], c, "\n")
            for (i = 1; i <= n; i++) if (c[i] != "") want[tolower(c[i])] = 1
        }
        FILENAME == ARGV[1] { seen[$1 "\t" tolower($2)] = 1; next }
        FNR == 1 { s = FILENAME; sub(/.*[\/\\]/, "", s); sub(/\.edits$/, "", s) }
        (tolower($2) in want) && ($1 + 0) >= min && !((s "\t" lp) in seen) {
            if (!(s in last) || ($1 + 0) > last[s]) { last[s] = $1 + 0; at[s] = $2 }
        }
        END { for (k in last) printf "%d\t%s\t%s\n", last[k], k, at[k] }
    ' "$SEEN" {} + 2>/dev/null)
[[ -n "$HITS" ]] || exit 0

TZ=UTC0 printf -v TODAY '%(%Y-%m-%d)T' "$NOW"
WHO=()
TWIN=()
PAIRS=""
# Split into an array under set -f, never a here-string (#681).
set -f; IFS=$'\n'; HIT_LIST=($HITS); IFS=$' \t\n'; set +f
for h in "${HIT_LIST[@]}"; do
    e=${h%%$'\t'*}
    e=${e//[!0-9]/}
    rest=${h#*$'\t'}
    o=${rest%%$'\t'*}
    at=${rest#*$'\t'}
    [[ -n "$e" && -n "$o" ]] || continue
    TZ=UTC0 printf -v HM '%(%H:%M)T' "$e"
    TZ=UTC0 printf -v DAY '%(%Y-%m-%d)T' "$e"
    [[ "$DAY" == "$TODAY" ]] && DAY=today || DAY="on $DAY"
    if [[ "${at,,}" == "$FP_LOW" || "$at" == "$rest" ]]; then
        WHO+=("${o:0:8} at $HM UTC $DAY")
    else
        TWIN+=("${o:0:8} edited $at at $HM UTC $DAY")
    fi
    PAIRS+="$o"$'\t'"$FILE_PATH"$'\n'
done
(( ${#WHO[@]} + ${#TWIN[@]} > 0 )) || exit 0

printf '%s' "$PAIRS" >> "$WARNED" 2>/dev/null
MSG=""
if (( ${#WHO[@]} == 1 )); then
    W=${WHO[0]}
    MSG="record-session-edit: another session (${W%% at *}) edited $FILE_PATH at ${W#* at }. Before you go on, read git diff -- $FILE_PATH and keep that session's changes (AGENTS.md \"Preserve others' work\"). This note comes once per file and session."
elif (( ${#WHO[@]} > 1 )); then
    LIST=""
    for W in "${WHO[@]}"; do LIST+="${LIST:+, }$W"; done
    MSG="record-session-edit: other sessions ($LIST) edited $FILE_PATH. Before you go on, read git diff -- $FILE_PATH and keep their changes (AGENTS.md \"Preserve others' work\"). This note comes once per file and session."
fi
if (( ${#TWIN[@]} > 0 )); then
    LIST=""
    for W in "${TWIN[@]}"; do LIST+="${LIST:+; }$W"; done
    T="the same repo file in another worktree: $LIST. You may be doing the same task twice (#1248): run python tools/work_claims.py list, find that session by its id, and ask it (SendMessage) or Mike before you go on."
    if [[ -n "$MSG" ]]; then
        MSG+=" Also $T"
    else
        MSG="record-session-edit: $T This note comes once per file and session."
    fi
fi
MSG=${MSG//\\/\\\\}
MSG=${MSG//\"/\\\"}
printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"%s"}}\n' "$MSG"
exit 0
