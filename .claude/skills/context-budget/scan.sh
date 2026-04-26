#!/usr/bin/env bash
# context-budget scanner — TAOM-adapted
# Estimates *startup* token consumption across .claude/ components.
#
# Important Claude Code load semantics (per https://code.claude.com/docs/en/skills):
#   - Skill DESCRIPTIONS load at conversation start. Skill BODIES load only
#     when the skill is invoked. Therefore the "baseline" charge per skill is
#     only the frontmatter, not the full SKILL.md.
#   - Agent DESCRIPTIONS load into the Task tool's tool-definition context for
#     every Task spawn. Agent BODIES load only when that specific agent is
#     spawned. Same frontmatter-only charge applies.
#   - Hooks are .sh scripts invoked by the harness — not in any model context.
#   - MEMORY.md loads first ~200 lines / ~25KB at conversation start.
#
# Verbose mode (--verbose) prints per-file breakdown plus the "if-invoked"
# (lazy) body size for skills/agents so you see the heavy-load worst case.
#
# Token heuristics: prose words*1.3, code chars/4, MCP tool ~500, server ~200.

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

# Frontmatter-only token estimate. This is what actually loads at startup
# for skills and into Task tool context for agents — NOT the full file body.
estimate_frontmatter_tokens() {
    local file="$1"
    [[ ! -f "$file" ]] && { echo 0; return; }
    # Extract content between the first and second `---` line.
    local fm
    fm=$(awk '/^---$/{f++; next} f==1' "$file" 2>/dev/null)
    [[ -z "$fm" ]] && { echo 0; return; }
    local words
    words=$(echo "$fm" | wc -w | tr -d ' ')
    echo $(( words * 13 / 10 ))
}

# words count for description-length flag
count_words() {
    local s="$1"
    echo "$s" | wc -w | tr -d ' '
}

# --- frontmatter description extractor (rough but works for our format) ---
# LIMITATION: only captures single-line `description: text` form. Multiline YAML
# (`description: |\n  text\n  more text\n`) is NOT handled — the awk picks up
# only the literal value after the colon (empty for multiline). This means the
# bloat lint silently bypasses on multiline descriptions. No skill currently
# uses multiline; if one is added, replace this with a real YAML parser or at
# minimum extend the awk to fold the block-scalar continuation.
# Caught by Codex review 2026-04-26 as suspect 6 (downgraded LOW since no
# current usage; documented here so the next author who writes multiline
# doesn't silently bypass).
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
    AGENTS_TOKENS=0          # eager (frontmatter only) — what actually loads at startup
    AGENTS_LAZY_TOKENS=0     # lazy (full body) — loaded only when invoked
    AGENTS_COUNT=0
    AGENTS_HEAVY=()
    AGENTS_BLOATED_DESC=()
    local dir="$REPO_ROOT/.claude/agents"
    [[ ! -d "$dir" ]] && return
    while IFS= read -r -d '' file; do
        local name lines eager lazy desc desc_words
        name=$(basename "$file" .md)
        lines=$(wc -l < "$file" | tr -d ' ')
        eager=$(estimate_frontmatter_tokens "$file")
        lazy=$(estimate_prose_tokens "$file")
        desc=$(extract_description "$file")
        desc_words=$(count_words "$desc")
        AGENTS_TOKENS=$(( AGENTS_TOKENS + eager ))
        AGENTS_LAZY_TOKENS=$(( AGENTS_LAZY_TOKENS + lazy ))
        AGENTS_COUNT=$(( AGENTS_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  agent %-40s %4d lines  eager=~%4d  lazy=~%5d  desc=%d words\n" "$name" "$lines" "$eager" "$lazy" "$desc_words"
        fi
        if [[ $lines -gt 200 ]]; then
            AGENTS_HEAVY+=("$name ($lines lines)")
        fi
        if [[ $desc_words -gt 30 ]]; then
            AGENTS_BLOATED_DESC+=("$name (${desc_words}w description)")
        fi
    done < <(find "$dir" -maxdepth 1 -name '*.md' -type f -print0)

    [[ ${#AGENTS_HEAVY[@]} -gt 0 ]] && ISSUES+=("Heavy agent bodies (>200 lines, loaded when agent is spawned): ${AGENTS_HEAVY[*]}")
    [[ ${#AGENTS_BLOATED_DESC[@]} -gt 0 ]] && ISSUES+=("Bloated agent descriptions (>30 words, loaded into every Task spawn): ${AGENTS_BLOATED_DESC[*]}")
}

scan_skills() {
    SKILLS_TOKENS=0          # eager (frontmatter only) — what actually loads at startup
    SKILLS_LAZY_TOKENS=0     # lazy (full body) — loaded only when skill is invoked
    SKILLS_COUNT=0
    SKILLS_HEAVY=()
    SKILLS_BLOATED_DESC=()
    local root="$REPO_ROOT/.claude/skills"
    [[ ! -d "$root" ]] && return
    while IFS= read -r -d '' skilldir; do
        local name=$(basename "$skilldir")
        local skillmd="$skilldir/SKILL.md"
        [[ ! -f "$skillmd" ]] && continue
        local lines eager lazy desc desc_words
        lines=$(wc -l < "$skillmd" | tr -d ' ')
        eager=$(estimate_frontmatter_tokens "$skillmd")
        lazy=$(estimate_prose_tokens "$skillmd")
        desc=$(extract_description "$skillmd")
        desc_words=$(count_words "$desc")
        SKILLS_TOKENS=$(( SKILLS_TOKENS + eager ))
        SKILLS_LAZY_TOKENS=$(( SKILLS_LAZY_TOKENS + lazy ))
        SKILLS_COUNT=$(( SKILLS_COUNT + 1 ))
        if [[ $VERBOSE -eq 1 ]]; then
            printf "  skill %-40s %4d lines  eager=~%4d  lazy=~%5d  desc=%d words\n" "$name" "$lines" "$eager" "$lazy" "$desc_words"
        fi
        if [[ $lines -gt 400 ]]; then
            SKILLS_HEAVY+=("$name ($lines lines)")
        fi
        # Skill descriptions load eagerly into every proactive-invoke decision —
        # bloat tax compounds across all skills. Same 30-word threshold as agents.
        if [[ $desc_words -gt 30 ]]; then
            SKILLS_BLOATED_DESC+=("$name (${desc_words}w description)")
        fi
    done < <(find "$root" -mindepth 1 -maxdepth 1 -type d -print0)

    [[ ${#SKILLS_HEAVY[@]} -gt 0 ]] && ISSUES+=("Heavy skill bodies (>400 lines, loaded when skill is invoked): ${SKILLS_HEAVY[*]}")
    [[ ${#SKILLS_BLOATED_DESC[@]} -gt 0 ]] && ISSUES+=("Bloated skill descriptions (>30 words, loaded eagerly): ${SKILLS_BLOATED_DESC[*]}")
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

scan_memory() {
    # MEMORY.md is loaded at conversation start, capped at first ~200 lines
    # or ~25KB by current Claude Code (whichever cap binds first).
    MEMORY_TOKENS=0
    MEMORY_LINES=0
    MEMORY_BYTES=0

    local mem_root="$HOME/.claude/projects"
    [[ ! -d "$mem_root" ]] && return

    # Derive the EXACT Claude project slug from the full repo path.
    # Convention: slug is the absolute path with `/` -> `-`, `\` -> `-`, and ':'
    # stripped, then prefixed by `<drive>--` (e.g., `c--Users-mikew-source-repos-TAOM`).
    # Substring matching ("anything containing TAOM") is unreliable when the
    # machine has multiple projects whose paths share a substring (TAOM,
    # TAOM-Online, taommod). Build the canonical slug instead.
    local repo_native
    if command -v cygpath >/dev/null 2>&1; then
        repo_native=$(cygpath -w "$REPO_ROOT" 2>/dev/null || echo "$REPO_ROOT")
    else
        repo_native="$REPO_ROOT"
    fi
    # Lowercase the drive letter, strip the colon, replace separators with `-`.
    local slug
    slug=$(printf '%s' "$repo_native" \
        | sed -E 's|^([A-Za-z]):[\\/]|\L\1--|; s|[/\\]|-|g')
    # On non-Windows the input may not have a drive letter — leave as-is.

    local mem_file="$mem_root/$slug/memory/MEMORY.md"
    if [[ ! -f "$mem_file" ]]; then
        # Fallback: substring search (warn in verbose mode that the slug
        # heuristic missed). This preserves behavior on platforms where the
        # canonical slug derivation doesn't match Claude Code's actual format.
        local fallback
        fallback=$(find "$mem_root" -maxdepth 3 -name MEMORY.md -path "*$(basename "$REPO_ROOT")*/MEMORY.md" 2>/dev/null | head -1)
        if [[ -n "$fallback" && -f "$fallback" ]]; then
            mem_file="$fallback"
            [[ $VERBOSE -eq 1 ]] && printf "  memory (slug %s not found; fell back to %s)\n" "$slug" "$mem_file"
        else
            return
        fi
    fi

    MEMORY_LINES=$(wc -l < "$mem_file" 2>/dev/null | tr -d ' ')
    MEMORY_BYTES=$(wc -c < "$mem_file" 2>/dev/null | tr -d ' ')

    # Estimate tokens from min(first 200 lines, first 25KB) — whichever cap
    # binds first. Both caps must be enforced together; counting `head -200`
    # alone overcounts when the byte cap binds before the line cap.
    local words
    if [[ $MEMORY_BYTES -le 25600 ]]; then
        # Byte cap not binding — just count first 200 lines (or whole file if shorter).
        words=$(head -200 "$mem_file" 2>/dev/null | wc -w | tr -d ' ')
    else
        # Byte cap binding — slice the first 25KB, then take up to 200 lines of that.
        words=$(head -c 25600 "$mem_file" 2>/dev/null | head -200 | wc -w | tr -d ' ')
    fi
    MEMORY_TOKENS=$(( words * 13 / 10 ))

    if [[ $VERBOSE -eq 1 ]]; then
        printf "  memory %-39s %4d lines  %5d bytes  ~%5d tokens\n" "MEMORY.md ($mem_file)" "$MEMORY_LINES" "$MEMORY_BYTES" "$MEMORY_TOKENS"
    fi
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

    # Tool counts per known server. Mix of EXACT (counted from source) and
    # HEURISTIC (estimate from upstream docs). Counts updated 2026-04-26.
    # When adding/removing servers, update these AND document the source.
    #
    #   serena:      HEURISTIC ~25 symbol/edit tools — https://github.com/oraios/serena
    #   github:      HEURISTIC ~30 issue/PR/repo tools — GitHub Copilot MCP
    #   filesystem:  EXACT 13 — counted from upstream README:
    #                https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem
    #   git:         HEURISTIC ~14 git-operation tools — mcp-server-git
    #   ilspy:       EXACT 4 — counted from server.py (decompile_assembly,
    #                list_types, generate_diagrammer, get_assembly_info)
    declare -A SERVER_TOOLS=(
        [serena]=25
        [github]=30
        [filesystem]=13
        [git]=14
        [ilspy]=4
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
scan_memory

[[ $VERBOSE -eq 1 ]] && echo

# EAGER total — what Claude Code actually loads at conversation start.
# Counts: CLAUDE.md (full), agent FRONTMATTER only, skill FRONTMATTER only,
# rules (full — most are conditional via paths: but counted at worst case),
# MCP tool schemas + per-server overhead, MEMORY.md (capped to first ~25KB).
# Excludes: skill bodies (lazy, on invocation), agent bodies (lazy, on Task spawn),
# hook scripts (run by harness, never in model context), Claude Code's own system
# prompt boilerplate (~3-5K, fixed per-version).
TOTAL=$(( CLAUDE_TOKENS + AGENTS_TOKENS + SKILLS_TOKENS + RULES_TOKENS + MCP_TOKENS + MEMORY_TOKENS ))

# LAZY total — eager + worst-case if every skill and agent were invoked.
LAZY_DELTA=$(( (SKILLS_LAZY_TOKENS - SKILLS_TOKENS) + (AGENTS_LAZY_TOKENS - AGENTS_TOKENS) ))
WORST_CASE=$(( TOTAL + LAZY_DELTA ))

# Opus 4.7 with 1M context window per CLAUDE.md notes
WINDOW=1000000
HEADROOM=$(( WINDOW - TOTAL ))
PCT_USED=$(( TOTAL * 100 / WINDOW ))

cat <<EOF
Context model:                    Claude Opus 4.7 (1M window)
Eager (startup) baseline:         ~${TOTAL} tokens
Effective available:              ~${HEADROOM} tokens (~$(( 100 - PCT_USED ))% headroom)
Baseline as % of window:          ${PCT_USED}%
Worst-case (all skills + agents invoked): ~${WORST_CASE} tokens (+~${LAZY_DELTA} lazy)

Component breakdown:
+------------------+--------+-----------+--------------+
| Component        | Count  | Eager tok | If-invoked   |
+------------------+--------+-----------+--------------+
EOF
printf "| %-16s | %6d | %9d | %12s |\n" "CLAUDE.md"     1                    "$CLAUDE_TOKENS" "—"
printf "| %-16s | %6d | %9d | %12d |\n" "Agents"        "$AGENTS_COUNT"      "$AGENTS_TOKENS" "$AGENTS_LAZY_TOKENS"
printf "| %-16s | %6d | %9d | %12d |\n" "Skills"        "$SKILLS_COUNT"      "$SKILLS_TOKENS" "$SKILLS_LAZY_TOKENS"
printf "| %-16s | %6d | %9d | %12s |\n" "Rules"         "$RULES_COUNT"       "$RULES_TOKENS" "—"
printf "| %-16s | %6d | %9d | %12s |\n" "MCP servers"   "$MCP_SERVERS"       "$MCP_TOKENS" "—"
printf "| %-16s | %6d | %9d | %12s |\n" "MEMORY.md"     1                    "$MEMORY_TOKENS" "—"
printf "| %-16s | %6d | %9s | %12s |\n" "Hooks (.sh)"   "$HOOKS_COUNT"       "(not in ctx)" "—"
echo "+------------------+--------+-----------+--------------+"
echo "Eager = loaded at startup."
echo "If-invoked = total bytes that load if EVERY agent / skill in that row is invoked"
echo "  in one session (full body, not delta from eager). The WORST_CASE total above"
echo "  adds only the delta (if-invoked minus eager) since eager is already counted."
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
