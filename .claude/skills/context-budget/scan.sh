#!/usr/bin/env bash
# context-budget scanner — TAOM-adapted
# Estimates token consumption across .claude/ components and reports.
# Heuristics: prose words*1.3, code chars/4, MCP tool ~500, server ~200.
# Verbose mode (--verbose) prints per-file breakdown.

set -uo pipefail

VERBOSE=0
[[ "${1:-}" == "--verbose" ]] && VERBOSE=1

# Resolve repo root from script location.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# --- token estimators ---

# words * 1.3
estimate_prose_tokens() {
    local file="$1"
    [[ ! -f "$file" ]] && { echo 0; return; }
    local words
    words=$(wc -w < "$file" 2>/dev/null | tr -d ' ')
    echo $(( words * 13 / 10 ))
}

# chars / 4
estimate_code_tokens() {
    local file="$1"
    [[ ! -f "$file" ]] && { echo 0; return; }
    local chars
    chars=$(wc -c < "$file" 2>/dev/null | tr -d ' ')
    echo $(( chars / 4 ))
}

# words count for description-length flag
count_words() {
    local s="$1"
    echo "$s" | wc -w | tr -d ' '
}

# --- frontmatter description extractor (rough but works for our format) ---
extract_description() {
    local file="$1"
    awk '/^---$/{f++} f==1 && /^description:/{
        sub(/^description:[[:space:]]*/, "")
        print
        exit
    }' "$file" 2>/dev/null
}

# --- per-component scans ---

scan_claude_md() {
    local file="$REPO_ROOT/CLAUDE.md"
    local lines tokens
    lines=$(wc -l < "$file" 2>/dev/null | tr -d ' ')
    tokens=$(estimate_prose_tokens "$file")
    CLAUDE_LINES=$lines
    CLAUDE_TOKENS=$tokens
    if [[ $VERBOSE -eq 1 ]]; then
        echo "  CLAUDE.md: $lines lines, ~$tokens tokens"
    fi
    if [[ $lines -gt 300 ]]; then
        ISSUES+=("CLAUDE.md is $lines lines (>300) — consider splitting repeating rules into .claude/rules/")
    fi
}

scan_agents() {
    AGENTS_TOKENS=0
    AGENTS_COUNT=0
    AGENTS_HEAVY=()
    AGENTS_BLOATED_DESC=()
    local dir="$REPO_ROOT/.claude/agents"
    [[ ! -d "$dir" ]] && return
    while IFS= read -r -d '' file; do
        local name lines tokens desc desc_words
        name=$(basename "$file" .md)
        lines=$(wc -l < "$file" | tr -d ' ')
        tokens=$(estimate_prose_tokens "$file")
        desc=$(extract_description "$file")
        desc_words=$(count_words "$desc")
        AGENTS_TOKENS=$(( AGENTS_TOKENS + tokens ))
        AGENTS_COUNT=$(( AGENTS_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  agent %-40s %4d lines  ~%5d tokens  desc=%d words\n" "$name" "$lines" "$tokens" "$desc_words"
        fi
        if [[ $lines -gt 200 ]]; then
            AGENTS_HEAVY+=("$name ($lines lines)")
        fi
        if [[ $desc_words -gt 30 ]]; then
            AGENTS_BLOATED_DESC+=("$name (${desc_words}w description)")
        fi
    done < <(find "$dir" -maxdepth 1 -name '*.md' -type f -print0)

    [[ ${#AGENTS_HEAVY[@]} -gt 0 ]] && ISSUES+=("Heavy agents (>200 lines, loaded into every Task spawn): ${AGENTS_HEAVY[*]}")
    [[ ${#AGENTS_BLOATED_DESC[@]} -gt 0 ]] && ISSUES+=("Bloated agent descriptions (>30 words, loaded always): ${AGENTS_BLOATED_DESC[*]}")
}

scan_skills() {
    SKILLS_TOKENS=0
    SKILLS_COUNT=0
    SKILLS_HEAVY=()
    local root="$REPO_ROOT/.claude/skills"
    [[ ! -d "$root" ]] && return
    while IFS= read -r -d '' skilldir; do
        local name=$(basename "$skilldir")
        local skillmd="$skilldir/SKILL.md"
        [[ ! -f "$skillmd" ]] && continue
        local lines tokens
        lines=$(wc -l < "$skillmd" | tr -d ' ')
        tokens=$(estimate_prose_tokens "$skillmd")
        SKILLS_TOKENS=$(( SKILLS_TOKENS + tokens ))
        SKILLS_COUNT=$(( SKILLS_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  skill %-40s %4d lines  ~%5d tokens\n" "$name" "$lines" "$tokens"
        fi
        if [[ $lines -gt 400 ]]; then
            SKILLS_HEAVY+=("$name ($lines lines)")
        fi
    done < <(find "$root" -mindepth 1 -maxdepth 1 -type d -print0)

    [[ ${#SKILLS_HEAVY[@]} -gt 0 ]] && ISSUES+=("Heavy skills (>400 lines): ${SKILLS_HEAVY[*]}")
}

scan_rules() {
    RULES_TOKENS=0
    RULES_COUNT=0
    RULES_HEAVY=()
    local dir="$REPO_ROOT/.claude/rules"
    [[ ! -d "$dir" ]] && return
    while IFS= read -r -d '' file; do
        local name=$(basename "$file" .md)
        local lines tokens
        lines=$(wc -l < "$file" | tr -d ' ')
        tokens=$(estimate_prose_tokens "$file")
        RULES_TOKENS=$(( RULES_TOKENS + tokens ))
        RULES_COUNT=$(( RULES_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  rule  %-40s %4d lines  ~%5d tokens\n" "$name" "$lines" "$tokens"
        fi
        if [[ $lines -gt 100 ]]; then
            RULES_HEAVY+=("$name ($lines lines)")
        fi
    done < <(find "$dir" -maxdepth 1 -name '*.md' -type f -print0)

    [[ ${#RULES_HEAVY[@]} -gt 0 ]] && ISSUES+=("Heavy rules (>100 lines): ${RULES_HEAVY[*]}")
}

scan_hooks() {
    HOOKS_TOKENS=0
    HOOKS_COUNT=0
    local dir="$REPO_ROOT/.claude/hooks"
    [[ ! -d "$dir" ]] && return
    while IFS= read -r -d '' file; do
        local name=$(basename "$file")
        local tokens
        tokens=$(estimate_code_tokens "$file")
        HOOKS_TOKENS=$(( HOOKS_TOKENS + tokens ))
        HOOKS_COUNT=$(( HOOKS_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  hook  %-40s        ~%5d tokens (script size, not in context)\n" "$name" "$tokens"
        fi
    done < <(find "$dir" -maxdepth 1 -name '*.sh' -type f -print0)
    # Hooks aren't loaded into context, just script files invoked. Don't count toward total.
}

scan_mcp() {
    MCP_SERVERS=0
    MCP_TOOLS_EST=0
    MCP_TOKENS=0
    local mcp="$REPO_ROOT/.mcp.json"
    [[ ! -f "$mcp" ]] && return

    # Convert /c/... -> C:/... so native Windows Python can open the file.
    local mcp_native="$mcp"
    if command -v cygpath >/dev/null 2>&1; then
        mcp_native=$(cygpath -w "$mcp" 2>/dev/null || echo "$mcp")
    fi

    # Find a working python.
    local PY=""
    for cand in python3 python py; do
        if command -v "$cand" >/dev/null 2>&1; then PY="$cand"; break; fi
    done

    local server_list=""
    if [[ -n "$PY" ]]; then
        server_list=$("$PY" -c "
import json,sys
try:
    with open(sys.argv[1]) as f:
        d = json.load(f)
    for k in d.get('mcpServers', {}):
        print(k)
except Exception as e:
    sys.stderr.write(str(e))
" "$mcp_native" 2>/dev/null)
    fi

    # Fallback: parse with grep if python failed or returned nothing.
    if [[ -z "$server_list" ]]; then
        # Top-level keys under mcpServers — they are the only 4-space-indented
        # quoted keys followed by `: {` in the file. (Brittle but works for our
        # simple .mcp.json layout.)
        server_list=$(awk '
            /"mcpServers"[[:space:]]*:/ { in_block=1; depth=0; next }
            in_block && /\{/ { depth++ }
            in_block && /\}/ { depth--; if (depth==0) { in_block=0 } }
            in_block && depth==1 && /^[[:space:]]+"[^"]+"[[:space:]]*:[[:space:]]*\{/ {
                match($0, /"[^"]+"/)
                key = substr($0, RSTART+1, RLENGTH-2)
                print key
            }
        ' "$mcp" 2>/dev/null)
    fi

    MCP_SERVERS=$(echo "$server_list" | grep -c '[^[:space:]]' 2>/dev/null || echo 0)

    # Tool counts per known server (heuristic — actual requires connecting).
    declare -A SERVER_TOOLS=(
        [serena]=25
        [github]=30
        [filesystem]=12
        [git]=14
        [ilspy]=8
        [sequential-thinking]=1
        [context7]=2
        [playwright]=24
    )
    local total=0
    while IFS= read -r srv; do
        # Skip empty AND whitespace-only lines (here-string + tr can leave one).
        [[ ! "$srv" =~ [^[:space:]] ]] && continue
        # Strip surrounding whitespace defensively.
        srv="${srv#"${srv%%[![:space:]]*}"}"
        srv="${srv%"${srv##*[![:space:]]}"}"
        # Default to 15 for unknown servers — see "Token estimation" in SKILL.md.
        # If you see "(unknown server: X)" warnings repeatedly, add X to SERVER_TOOLS above.
        local n=${SERVER_TOOLS[$srv]:-15}
        if [[ -z "${SERVER_TOOLS[$srv]:-}" && $VERBOSE -eq 1 ]]; then
            printf "  mcp   %-40s        (unknown server, defaulting to %d tools)\n" "$srv" "$n"
        fi
        total=$(( total + n ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  mcp   %-40s        ~%d tools (est)\n" "$srv" "$n"
        fi
    done <<< "$server_list"

    MCP_TOOLS_EST=$total
    # 500 tokens per tool schema + 200 per server overhead.
    MCP_TOKENS=$(( total * 500 + MCP_SERVERS * 200 ))

    if [[ $MCP_TOOLS_EST -gt 50 ]]; then
        ISSUES+=("MCP tool count is ~$MCP_TOOLS_EST across $MCP_SERVERS servers — schemas dominate context")
    fi
}

# --- main ---

ISSUES=()
echo
echo "TAOM Context Budget Report"
echo "=========================="
echo

if [[ $VERBOSE -eq 1 ]]; then
    echo "Per-file breakdown (verbose):"
    echo
fi

scan_claude_md
scan_agents
scan_skills
scan_rules
scan_hooks
scan_mcp

[[ $VERBOSE -eq 1 ]] && echo

# Total (excludes hooks — they're scripts, not context)
TOTAL=$(( CLAUDE_TOKENS + AGENTS_TOKENS + SKILLS_TOKENS + RULES_TOKENS + MCP_TOKENS ))

# Opus 4.7 with 1M context window per CLAUDE.md notes
WINDOW=1000000
HEADROOM=$(( WINDOW - TOTAL ))
PCT_USED=$(( TOTAL * 100 / WINDOW ))

cat <<EOF
Context model:                    Claude Opus 4.7 (1M window)
Total estimated baseline:         ~${TOTAL} tokens
Effective available:              ~${HEADROOM} tokens (~$(( 100 - PCT_USED ))% headroom)
Baseline as % of window:          ${PCT_USED}%

Component breakdown:
+------------------+--------+-----------+
| Component        | Count  | Tokens    |
+------------------+--------+-----------+
EOF
printf "| %-16s | %6d | %9d |\n" "CLAUDE.md"     1                    "$CLAUDE_TOKENS"
printf "| %-16s | %6d | %9d |\n" "Agents"        "$AGENTS_COUNT"      "$AGENTS_TOKENS"
printf "| %-16s | %6d | %9d |\n" "Skills"        "$SKILLS_COUNT"      "$SKILLS_TOKENS"
printf "| %-16s | %6d | %9d |\n" "Rules"         "$RULES_COUNT"       "$RULES_TOKENS"
printf "| %-16s | %6d | %9d |\n" "MCP servers"   "$MCP_SERVERS"       "$MCP_TOKENS"
printf "| %-16s | %6d | %9s |\n" "Hooks (.sh)"   "$HOOKS_COUNT"       "(not in ctx)"
echo "+------------------+--------+-----------+"
echo

if [[ ${#ISSUES[@]} -gt 0 ]]; then
    echo "Issues found (${#ISSUES[@]}):"
    n=1
    for issue in "${ISSUES[@]}"; do
        echo "  $n. $issue"
        n=$(( n + 1 ))
    done
    echo
fi

# Top trim recommendations — heuristic, ranked by approximate savings
echo "Top trim opportunities (approximate savings):"
RECS=()
[[ $MCP_TOKENS -gt 15000 ]]   && RECS+=("Audit MCP servers — currently ~${MCP_TOKENS} tokens. If any wrap CLI tools (gh, git), prefer Bash + the CLI to save ~5K-15K tokens.")
[[ $CLAUDE_LINES -gt 400 ]]   && RECS+=("CLAUDE.md is ${CLAUDE_LINES} lines. Move repeating rules into scoped rules/*.md to defer load. Estimated savings: ~$(( (CLAUDE_LINES - 300) * 13 )) tokens.")
[[ ${#AGENTS_BLOATED_DESC[@]} -gt 0 ]] && RECS+=("Tighten ${#AGENTS_BLOATED_DESC[@]} bloated agent description(s) — descriptions load into every Task spawn.")
[[ $SKILLS_TOKENS -gt 10000 ]] && RECS+=("Skills total ~${SKILLS_TOKENS} tokens. If Claude Code loads SKILL.md bodies eagerly (verify), consider two-layer skill injection — could reclaim 50-70%.")

if [[ ${#RECS[@]} -eq 0 ]]; then
    echo "  No clear high-leverage trims at current overhead. Re-run after adding components."
else
    n=1
    for r in "${RECS[@]}"; do
        echo "  $n. $r"
        n=$(( n + 1 ))
    done
fi

echo
echo "Run with --verbose for per-file breakdown."
echo "Record this baseline in docs/context-budget-baseline.md if first run."
