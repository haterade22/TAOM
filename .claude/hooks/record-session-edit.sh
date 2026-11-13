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
#    additionalContext naming them. One awk reads every candidate ledger.
#
# Channel: PostToolUse additionalContext, the check-polearm-shield-parity.sh shape (exit-0 stderr
# reaches only the debug log). Never blocks: always exits 0, and any failure exits 0 silently. No
# session id or no path: no output and nothing written. Starts no interpreter unless the path
# arrives with a \u escape (D40).

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

# Other sessions' ledgers touched in the last 24 hours (1,440 minutes), read by one awk: the latest
# epoch per other session for this path, skipping pairs already in this session's .warned. Paths
# compare lower-cased on both sides by awk, so the comparison is one function. The .warned file
# (or /dev/null) is always the first file operand, so awk tells it apart by ARGV[1]: a -v copy of
# the name would have its backslashes read as escapes and never match.
SEEN=$WARNED
[[ -f "$SEEN" ]] || SEEN=/dev/null
MIN=$((NOW - 86400))
HITS=$(find "$DIR" -maxdepth 1 -type f -name '*.edits' ! -name "$SID.edits" -mmin -1440 \
    -exec awk -F '\t' -v p="$FILE_PATH" -v min="$MIN" '
        BEGIN { lp = tolower(p) }
        FILENAME == ARGV[1] { seen[$1 "\t" tolower($2)] = 1; next }
        FNR == 1 { s = FILENAME; sub(/.*[\/\\]/, "", s); sub(/\.edits$/, "", s) }
        tolower($2) == lp && ($1 + 0) >= min && !((s "\t" lp) in seen) {
            if (!(s in last) || ($1 + 0) > last[s]) last[s] = $1 + 0
        }
        END { for (k in last) printf "%d\t%s\n", last[k], k }
    ' "$SEEN" {} + 2>/dev/null)
[[ -n "$HITS" ]] || exit 0

TZ=UTC0 printf -v TODAY '%(%Y-%m-%d)T' "$NOW"
WHO=()
PAIRS=""
# Split into an array under set -f, never a here-string (#681).
set -f; IFS=$'\n'; HIT_LIST=($HITS); IFS=$' \t\n'; set +f
for h in "${HIT_LIST[@]}"; do
    e=${h%%$'\t'*}
    e=${e//[!0-9]/}
    o=${h#*$'\t'}
    [[ -n "$e" && -n "$o" ]] || continue
    TZ=UTC0 printf -v HM '%(%H:%M)T' "$e"
    TZ=UTC0 printf -v DAY '%(%Y-%m-%d)T' "$e"
    [[ "$DAY" == "$TODAY" ]] && DAY=today || DAY="on $DAY"
    WHO+=("${o:0:8} at $HM UTC $DAY")
    PAIRS+="$o"$'\t'"$FILE_PATH"$'\n'
done
(( ${#WHO[@]} > 0 )) || exit 0

printf '%s' "$PAIRS" >> "$WARNED" 2>/dev/null
if (( ${#WHO[@]} == 1 )); then
    W=${WHO[0]}
    MSG="record-session-edit: another session (${W%% at *}) edited $FILE_PATH at ${W#* at }. Before you go on, read git diff -- $FILE_PATH and keep that session's changes (AGENTS.md \"Preserve others' work\"). This note comes once per file and session."
else
    LIST=""
    for W in "${WHO[@]}"; do LIST+="${LIST:+, }$W"; done
    MSG="record-session-edit: other sessions ($LIST) edited $FILE_PATH. Before you go on, read git diff -- $FILE_PATH and keep their changes (AGENTS.md \"Preserve others' work\"). This note comes once per file and session."
fi
MSG=${MSG//\\/\\\\}
MSG=${MSG//\"/\\\"}
printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"%s"}}\n' "$MSG"
exit 0
