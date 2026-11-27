#!/bin/bash
# PostToolUse (Read|Edit|Write): print the knowledge entries that match the file the agent just read,
# edited or wrote, and name the entries that call this file what makes them act.
# SessionStart (a start, a resume, the restart after a compaction): forget what the session was told,
# because by then it has lost the text. The two events share this script, told apart by the payload's
# hook_event_name, so no registration needs an argument.
#
# WHY THIS EXISTS
# The repository keeps its techniques as one TOML file each under docs/knowledge/techniques/ (#1613,
# docs/adrs/016-technique-library-and-procedure-database.md). A worker sees an entry only if something
# prints it, so this hook prints the lines of the matching entries beside the tool result, with no
# search: in the main checkout, in a sibling worktree, inside a subagent, and for a file outside the
# repository (the live Armory, an asset work folder).
#
# WHAT IT DOES
# 1. Reads the session id, the tool name and the file path from the payload, as point-worktree-rules.sh
#    does, and normalises the path the same way: / separators, a /c/ drive spelled c:/, lower case.
# 2. A file under <project>/docs/knowledge/ gets no note. An Edit or a Write there deletes the session's
#    index, so the next call builds it again.
# 3. The index is <ledger>/<session id>.kindex, built on the first call by
#    "python tools/knowledge.py index --out ... --root <project>" under timeout -k 1 3, which with its
#    kill grace is 4 s. It is built again when the techniques folder or an entry file is newer than it:
#    git, a shell command or another session changed an entry. Several first calls can build at once, so
#    the hook judges by the index file and never by one build's exit code. A missing techniques folder,
#    no Python, no index after the build, or a file that does not open with the header line gives one
#    "unchecked, not absent" note (hook-authoring.md: fail open, never silent).
# 4. Reads the index with one loop. A `t` row whose glob matches the whole path, with
#    [[ $path == $glob ]], is an entry; a `w` row (a file an entry names as what makes it act) is a
#    watch, and counts only for an Edit or a Write.
# 5. Claims each entry for the reader: a marker file made under noclobber, <session id>.k-<id> for the
#    session and <session id>.<agent id>.ak-<id> for a subagent, whose context is its own. The claim is
#    one step, because a ledger read and its append are two: six parallel Reads gave six copies of
#    point-worktree-rules.sh's note on 2026-11-26. Only what THIS process claimed is printed.
# 6. One call prints the id, the level and the line of at most three entries and claims only those. The
#    rest stay unclaimed and come with the next matching file, three at a time; the note counts them.
#    Watches are rare and are not part of the limit. A watch sentence carries the entry's line.
# 7. For the deep-reviewer, an entry's ask replaces its line.
# 8. Entries that could not be read are counted in the index header. The hook says so once per reader,
#    also when no other entry matched: the broken entry may be the one for this file.
# 9. SessionStart deletes the index, the session's own markers and every "unchecked" and "could not be
#    read" marker of its subagents; a subagent's entry markers stay, as point-worktree-rules.sh keeps
#    its .arules. A state file of this hook older than 7 days is deleted when an index is built.
#
# Limits: a file read through the shell (cat, sed) gets no note, and work driven only through Bash or
# the Blender MCP gets none. A session reads its hooks and its store from the checkout it started in,
# so an entry on an unmerged branch is not served to a session that started elsewhere. After one
# "unchecked" note the hook does not try to build the index again until SessionStart, an Edit or Write
# in docs/knowledge/, or a change of the store or the tool, because a broken tool would otherwise cost
# 3 s on every Read. Entries come in the index's order (the ladder's order from sentence to code, then
# by id), not by how well each fits the file. The inner bound counts the Python run only: _pybin.sh can
# add up to 4 s when its first candidates fail, so the registration is 10 s.
#
# Channel: PostToolUse additionalContext, the record-session-edit.sh shape. Never blocks: always exits
# 0, and any failure exits 0. Starts no git process, and Python only to build the index (or for a path
# that arrives with a \u escape, D40).
# Tests: tools/tests/test_point_knowledge_hook.py. Not registered in .claude/settings.json: the
# registration is the maintainer's (docs/features/knowledge-library.md, "Switching the hook on").

# This hook runs on every Read, so it starts as few processes as it can. The payload is read with cat:
# a `read -d ''` would cost about 400 ms on a 200 KB payload (a Write of a large file), because bash
# reads a pipe one byte at a time.
INPUT=$(cat)

KN_HERE=${BASH_SOURCE[0]%/*}
[[ "$KN_HERE" == "${BASH_SOURCE[0]}" ]] && KN_HERE=.
source "$KN_HERE/_session_ledger.sh" 2>/dev/null || exit 0

taom_session_id "$INPUT"
[[ -n "$TAOM_SID" ]] || exit 0
taom_ledger_dir
DIR=$TAOM_LEDGER_DIR
SID=$TAOM_SID

# Forget that an index could not be built or read, for the session and for each of its subagents.
kn_forget_unchecked() {
    rm -f "$DIR/$SID.k-_unchecked" "$DIR/$SID.k-_skipped" "$DIR/$SID".*.ak-_unchecked "$DIR/$SID".*.ak-_skipped 2>/dev/null
}

# The file path first: a PostToolUse payload carries the tool's response too, and the SessionStart
# payload has no tool input and is small (see point-worktree-rules.sh for the measurement).
if ! taom_payload_file_path "$INPUT"; then
    start_re='"hook_event_name"[[:space:]]*:[[:space:]]*"SessionStart"'
    if [[ "$INPUT" =~ $start_re ]]; then
        rm -f "$DIR/$SID.kindex" "$DIR/$SID.k-"* 2>/dev/null
        kn_forget_unchecked
    fi
    exit 0
fi
FILE_PATH=$TAOM_PATH

# KN_N: $1 with / separators, a /c/ drive spelled c:/, no trailing slash, and lower case.
kn_norm() {
    local p=${1//\\//}
    [[ "$p" =~ ^/([A-Za-z])/(.*)$ ]] && p="${BASH_REMATCH[1]}:/${BASH_REMATCH[2]}"
    p=${p%/}
    KN_N=${p,,}
}

PROJ_RAW=${CLAUDE_PROJECT_DIR:-$PWD}
PROJ_RAW=${PROJ_RAW//\\//}
PROJ_RAW=${PROJ_RAW%/}
kn_norm "$PROJ_RAW"
PROJ=$KN_N
kn_norm "$FILE_PATH"
FP=$KN_N

TOOL=""
tool_re='"tool_name"[[:space:]]*:[[:space:]]*"([A-Za-z]+)"'
[[ "$INPUT" =~ $tool_re ]] && TOOL=${BASH_REMATCH[1]}
IS_EDIT=0
[[ "$TOOL" == Edit || "$TOOL" == Write ]] && IS_EDIT=1

INDEX="$DIR/$SID.kindex"

# The store's own files get no note. Writing one makes the index stale.
if [[ "$FP" == "$PROJ/docs/knowledge/"* ]]; then
    if (( IS_EDIT )); then
        rm -f "$INDEX" 2>/dev/null
        kn_forget_unchecked
    fi
    exit 0
fi

taom_agent_id "$INPUT"
AID=$TAOM_AID
if [[ -n "$AID" ]]; then MP="$DIR/$SID.$AID.ak-"; else MP="$DIR/$SID.k-"; fi

# Make the marker $1, and succeed only for the one process that created it.
kn_claim() {
    [[ -d "$DIR" ]] || mkdir -p "$DIR" 2>/dev/null || return 1
    ( set -C; : > "$1" ) 2>/dev/null
}

# Print $1 as additionalContext and exit.
kn_say() {
    local m=${1//\\/\\\\}
    m=${m//\"/\\\"}
    printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"%s"}}\n' "$m"
    exit 0
}

# The note for an index that could not be had: once per reader and session.
kn_unchecked() {
    kn_claim "${MP}_unchecked" || exit 0
    kn_say "point-knowledge: unchecked, not absent. The knowledge index could not be built ($1), so no knowledge entry was looked up for $FILE_PATH. Run python tools/knowledge.py check. This note comes once per session."
}

# Whether the store or the tool changed after the file $1 was written: the techniques folder, an entry
# file or tools/knowledge.py is newer. Builtin tests only: no process starts.
TECH="$PROJ_RAW/docs/knowledge/techniques"
kn_newer_than() {
    local f
    [[ "$TECH" -nt "$1" || "$PROJ_RAW/tools/knowledge.py" -nt "$1" ]] && return 0
    for f in "$TECH"/*.toml; do
        [[ "$f" -nt "$1" ]] && return 0
    done
    return 1
}

# An entry that git, a shell command or another session changed leaves this session's index old.
if [[ -f "$INDEX" ]] && kn_newer_than "$INDEX"; then
    rm -f "$INDEX" 2>/dev/null
    kn_forget_unchecked
fi

if [[ ! -f "$INDEX" ]]; then
    # An earlier call already said the index could not be built: do not pay for the attempt again, unless
    # the store or the tool changed since then.
    UNCHECKED="${MP}_unchecked"
    [[ -e "$UNCHECKED" ]] || UNCHECKED="$DIR/$SID.k-_unchecked"
    if [[ -e "$UNCHECKED" ]]; then
        kn_newer_than "$UNCHECKED" || exit 0
        kn_forget_unchecked
    fi
    [[ -d "$TECH" ]] || kn_unchecked "no folder docs/knowledge/techniques in $PROJ_RAW"
    source "$KN_HERE/_pybin.sh" 2>/dev/null
    [[ -n "${PYBIN:-}" ]] || kn_unchecked "no Python for hooks"
    mkdir -p "$DIR" 2>/dev/null || exit 0
    # 10,080 minutes is 7 days. Bounded to the one folder.
    find "$DIR" -maxdepth 1 -type f \( -name '*.kindex' -o -name '*.kindex.part-*' -o -name '*.k-*' -o -name '*.ak-*' \) -mmin +10080 -delete 2>/dev/null
    # The exit code is not read: of several builds that run at once, all but one can fail at the rename
    # while the index is there and whole.
    timeout -k 1 3 "$PYBIN" -B "$PROJ_RAW/tools/knowledge.py" index --out "$INDEX" --root "$PROJ_RAW" >/dev/null 2>&1
    [[ -f "$INDEX" ]] || kn_unchecked "tools/knowledge.py failed or overran"
fi

SKIPPED=0
STORE=""
HEAD_OK=0
E_ID=()
E_LV=()
E_TX=()
E_ASK=()
W_ID=()
W_LV=()
MORE=0
declare -A HANDLED=()
declare -A LINE_OF=()

n=0
while IFS=$'\t' read -r kind id level glob text ask || [[ -n "$kind" ]]; do
    n=$((n + 1))
    if (( n == 1 )); then
        # The header: kind holds "#knowledge-index v1", id the entries count, level skipped=, glob store=.
        [[ "$kind" == "#knowledge-index v1"* ]] || break
        HEAD_OK=1
        SKIPPED=${level#skipped=}
        SKIPPED=${SKIPPED//[!0-9]/}
        SKIPPED=${SKIPPED:0:6}
        SKIPPED=${SKIPPED:-0}
        STORE=${glob#store=}
        continue
    fi
    [[ "$id" =~ ^[a-z0-9-]+$ ]] || continue
    if [[ "$kind" == t ]]; then
        [[ -n "${LINE_OF[$id]:-}" ]] || LINE_OF[$id]=$text
        [[ -n "${HANDLED[$id]:-}" ]] && continue
        # shellcheck disable=SC2053  # the glob is the index's, and a pattern here on purpose
        [[ "$FP" == $glob ]] || continue
        HANDLED[$id]=1
        if (( ${#E_ID[@]} < 3 )); then
            if kn_claim "$MP$id"; then
                E_ID+=("$id"); E_LV+=("$level"); E_TX+=("$text"); E_ASK+=("$ask")
            fi
        elif [[ ! -e "$MP$id" ]]; then
            MORE=$((MORE + 1))
        fi
    elif [[ "$kind" == w ]] && (( IS_EDIT )); then
        [[ -n "${HANDLED[$id.w]:-}" ]] && continue
        # shellcheck disable=SC2053  # the glob is the index's, and a pattern here on purpose
        [[ "$FP" == $glob ]] || continue
        HANDLED[$id.w]=1
        if kn_claim "$MP$id.w"; then
            W_ID+=("$id"); W_LV+=("$level")
        fi
    fi
done < "$INDEX"

if (( ! HEAD_OK )); then
    rm -f "$INDEX" 2>/dev/null
    kn_unchecked "the index file is not an index"
fi

SKIP_NOTE=""
if (( 10#$SKIPPED > 0 )) && kn_claim "${MP}_skipped"; then
    SKIP_NOTE="point-knowledge: $SKIPPED knowledge entries could not be read, so an entry for this file may be missing: run python tools/knowledge.py check."
fi

ENT_N=${#E_ID[@]}
if (( ENT_N + ${#W_ID[@]} == 0 )); then
    [[ -n "$SKIP_NOTE" ]] && kn_say "$SKIP_NOTE"
    exit 0
fi

REVIEWER=0
reviewer_re='"agent_type"[[:space:]]*:[[:space:]]*"deep-reviewer"'
[[ "$INPUT" =~ $reviewer_re ]] && REVIEWER=1

MSG=""
if (( ENT_N > 0 )); then
    WORD=entries
    (( ENT_N == 1 )) && WORD=entry
    if (( REVIEWER )); then
        MSG="point-knowledge: $FILE_PATH matches $ENT_N knowledge $WORD. As the reviewer, check the change against each line and answer each question with evidence."
    else
        MSG="point-knowledge: $FILE_PATH matches $ENT_N knowledge $WORD: how this kind of work is done here, with how each acts today in brackets. Follow each, or say in your report why it does not apply."
    fi
    LIST=""
    for ((i = 0; i < ENT_N; i++)); do
        t=${E_TX[i]}
        (( REVIEWER )) && [[ -n "${E_ASK[i]}" ]] && t=${E_ASK[i]}
        LIST+="${LIST:+ | }${E_ID[i]} [${E_LV[i]}]: $t"
    done
    LIST=${LIST%.}
    MSG+=" $LIST."
    if (( MORE > 0 )); then
        MSG+=" $MORE more entries match and come with the next files you touch (all at once: python tools/knowledge.py for \"$FILE_PATH\")."
    fi
    MSG+=" Detail: python tools/knowledge.py show <id>. Each entry comes once per session. (store: $STORE)"
fi
for ((i = 0; i < ${#W_ID[@]}; i++)); do
    wid=${W_ID[i]}
    wline=${LINE_OF[$wid]:-}
    wline=${wline%.}
    MSG+="${MSG:+ }point-knowledge: $FILE_PATH is named by the knowledge entry $wid as what makes it act (${W_LV[i]})${wline:+: $wline}. If this change alters that, update docs/knowledge/techniques/$wid.toml in the same change."
done
[[ -n "$SKIP_NOTE" ]] && MSG+=" $SKIP_NOTE"
kn_say "$MSG"
