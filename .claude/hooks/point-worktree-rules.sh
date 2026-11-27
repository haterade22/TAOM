#!/bin/bash
# PostToolUse (Read|Edit|Write): name the path rules of a file that sits in a sibling checkout.
# SessionStart (a start, a resume, the restart after a compaction): forget what the session was told,
# because by then it has lost the rule text. The two events share this script, told apart by the
# payload's hook_event_name, so no registration needs an argument.
#
# WHY THIS EXISTS
# A rule with paths: loads only for a file in the checkout the session started in. The trunk edit
# gate of 2026-11-16 sent all edits to sibling worktrees, and from that day the area rules, the trap
# sheets and the lessons promoted into them stopped reaching the work: of 200 transcripts that
# touched C# only in a worktree, csharp-architecture.md loaded in 2
# (docs/reviews/harness-retro-2026-11-25.md, P1).
#
# WHAT IT DOES
# 1. Stops at once for a file inside $CLAUDE_PROJECT_DIR: the harness loads the rules there. The one
#    exception is a worktree under the project's own .claude/worktrees/ (EnterWorktree, isolation:
#    "worktree"), which is a checkout of its own.
# 2. Walks up from the file to the first .git entry, with no git process. It goes on only when that
#    checkout shares this project's git folder and is not the project folder itself: a linked
#    worktree of it, or the main checkout when the session started in a worktree. Another repository,
#    a submodule, or no repository gets nothing.
# 3. Reads the paths: globs of every rule in THAT checkout's .claude/rules, at any depth (the rules
#    of its branch), and matches the file's path inside the checkout against them: * stays in one
#    folder, ** crosses folders, a glob with no slash matches a name at any depth (memory docs,
#    "Path-specific rules"). It folds case, as NTFS does.
# 4. Names each matching rule file once per session, and once per subagent, whose context is its
#    own: <id>.rules and <id>.<agent id>.arules in the ledger folder (_session_ledger.sh), one line
#    per rule file. A new state file deletes state older than 7 days.
# A Read of a rule file gets no note: reading csharp-architecture.md would call for the rule about
# rules. A checkout whose .git file holds no gitdir line, or names a folder that cannot be opened,
# gets one "unchecked" note, because silence would read as "no rule applies" (hook-authoring.md: fail
# open, never silent).
#
# Limits: a file read through the shell (cat, sed) gets no note, and a subagent keeps its state
# through a compaction. tools/test_hooks.sh 7r holds the rows, and fails when a rule gains a glob
# character the matcher below does not know.
#
# Channel: PostToolUse additionalContext, the record-session-edit.sh shape. Never blocks: always
# exits 0, and any failure exits 0. Starts no git process, and no interpreter unless the path
# arrives with a \u escape (D40).

# This hook runs on every Read, so it starts as few processes as it can. The payload is read with cat:
# a `read -d ''` would save about 10 ms on a small payload and cost about 400 ms on a 200 KB one (a
# Write of a large file), because bash reads a pipe one byte at a time.
INPUT=$(cat)

WR_HERE=${BASH_SOURCE[0]%/*}
[[ "$WR_HERE" == "${BASH_SOURCE[0]}" ]] && WR_HERE=.
source "$WR_HERE/_session_ledger.sh" 2>/dev/null || exit 0

taom_session_id "$INPUT"
[[ -n "$TAOM_SID" ]] || exit 0
taom_ledger_dir
DIR=$TAOM_LEDGER_DIR
SID=$TAOM_SID

# The file path first. A PostToolUse payload carries the tool's response too, which for a Read is the
# file's text, and a regex that cannot match scans every byte of it (27 ms on 200 KB, 282 ms on 2 MB,
# measured 2026-11-25). So SessionStart is looked for only in a payload with no file path: that one has
# no tool input and is small. A quote inside a JSON string is always escaped, so file content that
# spells either key cannot match.
if ! taom_payload_file_path "$INPUT"; then
    start_re='"hook_event_name"[[:space:]]*:[[:space:]]*"SessionStart"'
    [[ "$INPUT" =~ $start_re ]] && rm -f "$DIR/$SID.rules" 2>/dev/null
    exit 0
fi
FILE_PATH=$TAOM_PATH

# WR_N: $1 with / separators, a /c/ drive spelled c:/, no trailing slash, and lower case. Every
# comparison below uses this form.
wr_norm() {
    local p=${1//\\//}
    [[ "$p" =~ ^/([A-Za-z])/(.*)$ ]] && p="${BASH_REMATCH[1]}:/${BASH_REMATCH[2]}"
    p=${p%/}
    WR_N=${p,,}
}

PROJ_RAW=${CLAUDE_PROJECT_DIR:-$PWD}
PROJ_RAW=${PROJ_RAW//\\//}
PROJ_RAW=${PROJ_RAW%/}
wr_norm "$PROJ_RAW"
PROJ=$WR_N
wr_norm "$FILE_PATH"
FP=$WR_N
# A worktree under the project's own .claude/worktrees (EnterWorktree, isolation: "worktree") is inside
# the project folder and still a checkout of its own: the project's globs do not match a path under it.
# A file directly in .claude/worktrees belongs to the project.
WR_NESTED="$PROJ/.claude/worktrees/"
if [[ "$FP" == "$PROJ/"* ]]; then
    [[ "$FP" == "$WR_NESTED"*/* ]] || exit 0
fi

# The checkout that holds the file: the first folder above it with a .git entry.
d=${FILE_PATH%/*}
ROOT=""
for ((i = 0; i < 40; i++)); do
    if [[ -e "$d/.git" ]]; then ROOT=$d; break; fi
    [[ "$d" == */* ]] || break
    d=${d%/*}
done
[[ -n "$ROOT" ]] || exit 0
wr_norm "$ROOT"
ROOT_N=$WR_N
# A plain folder under .claude/worktrees is no checkout: the walk reached the project's own .git.
[[ "$ROOT_N" == "$PROJ" ]] && exit 0

# WR_C: the shared git folder of the checkout at $1, in wr_norm form: <root>/.git for a main
# checkout, the part before /worktrees/ for a linked worktree. Returns 1 for another kind of .git
# file (a submodule), 2 when the file holds no gitdir line, and 3 when a relative gitdir names a
# folder that cannot be opened.
wr_common() {
    local g="$1/.git" line="" c
    WR_C=""
    if [[ -d "$g" ]]; then wr_norm "$g"; WR_C=$WR_N; return 0; fi
    { IFS= read -r line < "$g"; } 2>/dev/null
    line=${line%$'\r'}
    [[ "$line" == "gitdir: "* ]] || return 2
    c=${line#gitdir: }
    c=${c//\\//}
    [[ "$c" == */worktrees/* ]] || return 1
    c=${c%/worktrees/*}
    # A relative gitdir (worktree.useRelativePaths) is read from the checkout root. pwd -W gives the
    # Windows form in Git Bash, where a plain pwd can answer with a mount name (/tmp for E:/Temp) that
    # compares unequal to the same folder spelled with its drive; elsewhere -W fails and pwd stands.
    if [[ ! "$c" =~ ^([A-Za-z]:)?/ ]]; then
        c=$(cd "$1/$c" 2>/dev/null && { pwd -W 2>/dev/null || pwd; }) || return 3
    fi
    wr_norm "$c"
    WR_C=$WR_N
    return 0
}

wr_common "$PROJ_RAW" || exit 0
OWN_C=$WR_C
wr_common "$ROOT"
ROOT_RC=$?
(( ROOT_RC == 1 )) && exit 0
if (( ROOT_RC == 0 )); then
    [[ "$WR_C" == "$OWN_C" ]] || exit 0
fi

taom_agent_id "$INPUT"
if [[ -n "$TAOM_AID" ]]; then STATE="$DIR/$SID.$TAOM_AID.arules"; else STATE="$DIR/$SID.rules"; fi
declare -A TOLD=()
if [[ -f "$STATE" ]]; then
    while IFS= read -r line || [[ -n "$line" ]]; do
        [[ -n "$line" ]] && TOLD["$line"]=1
    done < "$STATE"
fi

# Append the keys in KEYS to the state file, then print MSG as additionalContext. A state file that
# cannot be written means no note: the same note on every call would be worse.
wr_say() {
    if [[ ! -f "$STATE" ]]; then
        mkdir -p "$DIR" 2>/dev/null || exit 0
        # 10,080 minutes is 7 days. Bounded to the one folder.
        find "$DIR" -maxdepth 1 -type f \( -name '*.rules' -o -name '*.arules' \) -mmin +10080 -delete 2>/dev/null
    fi
    { printf '%s\n' "${KEYS[@]}" >> "$STATE"; } 2>/dev/null || exit 0
    MSG=${MSG//\\/\\\\}
    MSG=${MSG//\"/\\\"}
    printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"%s"}}\n' "$MSG"
    exit 0
}

KEYS=()
if (( ROOT_RC >= 2 )); then
    key="unchecked:$ROOT_N"
    [[ -n "${TOLD[$key]:-}" ]] && exit 0
    KEYS=("$key")
    WHY="$ROOT/.git holds no gitdir line"
    (( ROOT_RC == 3 )) && WHY="the gitdir line of $ROOT/.git names a folder that cannot be opened"
    MSG="point-worktree-rules: unchecked, not absent. $WHY, so this hook cannot tell whether $FILE_PATH is in a worktree of this repository, and it looked up no path rule. If it is one, read the rule files in $ROOT/.claude/rules whose paths: globs match the file. This note comes once per checkout and session."
    wr_say
fi

REL=${FP:${#ROOT_N}+1}
TOOL=""
tool_re='"tool_name"[[:space:]]*:[[:space:]]*"([A-Za-z]+)"'
[[ "$INPUT" =~ $tool_re ]] && TOOL=${BASH_REMATCH[1]}
[[ "$TOOL" == Read && "$REL" == .claude/rules/* ]] && exit 0

# WR_RE: an anchored ERE for a lower-case path inside the checkout, from the paths: glob $1.
# Returns 1 for a character the matcher does not know, so that glob matches nothing.
wr_glob_re() {
    local g=${1,,} re="" c i n
    if [[ "$g" == /* ]]; then g=${g#/}; elif [[ "$g" != */* ]]; then re='(.*/)?'; fi
    n=${#g}
    for ((i = 0; i < n; i++)); do
        c=${g:i:1}
        case "$c" in
            '*')
                if [[ "${g:i:3}" == '**/' ]]; then re+='(.*/)?'; i=$((i + 2))
                elif [[ "${g:i:2}" == '**' ]]; then re+='.*'; i=$((i + 1))
                else re+='[^/]*'; fi ;;
            '?') re+='[^/]' ;;
            '.') re+='\.' ;;
            [a-z0-9/_-]) re+=$c ;;
            *) return 1 ;;
        esac
    done
    WR_RE="^${re}\$"
}

NEW=()
# Every depth, as the rule loader finds them (tools/lint_docs.py _rule_files reads them the same way).
shopt -s globstar nullglob
for f in "$ROOT"/.claude/rules/**/*.md; do
    [[ -f "$f" ]] || continue
    wr_norm "$f"
    key=$WR_N
    [[ -n "${TOLD[$key]:-}" ]] && continue
    n=0
    inside=0
    hit=0
    while IFS= read -r line || [[ -n "$line" ]]; do
        line=${line%$'\r'}
        n=$((n + 1))
        if (( n == 1 )); then
            line=${line#$'\xef\xbb\xbf'}
            [[ "$line" == '---' ]] || break
            continue
        fi
        [[ "$line" == '---' ]] && break
        (( n > 80 )) && break
        if [[ "$line" =~ ^paths:[[:space:]]*$ ]]; then inside=1; continue; fi
        (( inside )) || continue
        if [[ "$line" =~ ^[[:space:]]+-[[:space:]]+(.*[^[:space:]])[[:space:]]*$ ]]; then
            g=${BASH_REMATCH[1]}
            g=${g#[\"\']}
            g=${g%[\"\']}
            # A glob with a slash is anchored, so it can match only a path that starts with its
            # literal head (Main/ for Main/**/*.cs). Skipping the others saves the translation.
            lit=${g#/}
            lit=${lit%%[*?]*}
            [[ "$g" == */* && "$REL" != "${lit,,}"* ]] && continue
            if wr_glob_re "$g" && [[ "$REL" =~ $WR_RE ]]; then hit=1; break; fi
        elif [[ -n "$line" && "$line" != [[:space:]]* ]]; then
            inside=0
        fi
    done < "$f"
    if (( hit )); then NEW+=("$f"); KEYS+=("$key"); fi
done
(( ${#NEW[@]} > 0 )) || exit 0

LIST=""
for f in "${NEW[@]}"; do LIST+="${LIST:+, }$f"; done
MSG="point-worktree-rules: $FILE_PATH is in $ROOT, a checkout where path rules do not load by themselves. Read these rule files now, before you go on, and follow them for files like this one: $LIST. This note comes once per rule file and session."
wr_say
