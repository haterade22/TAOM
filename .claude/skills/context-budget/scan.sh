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
#   - MEMORY.md loads first ~200 lines / ~25KB at conversation start, main session only.
#   - CLAUDE.md's @-imports load at launch with it, so they are counted in full. Which files
#     load, whether a rule is path-scoped, and every ADR-011 cap come from
#     `tools/lint_docs.py --context-budget-json`, the same code the CI and commit gate run.
#   - MCP tool schemas are DEFERRED behind tool search by default; only the names load
#     (code.claude.com/docs/en/context-window, verified 2026-09-23). ENABLE_TOOL_SEARCH=false
#     loads every schema eagerly, and only then do schemas count.
#   - Custom and general-purpose subagents load CLAUDE.md, its imports and the rules without
#     paths: on every spawn (code.claude.com/docs/en/sub-agents); see the per-spawn line.
#
# Verbose mode (--verbose) prints per-file breakdown plus the "if-invoked"
# (lazy) body size for skills/agents so you see the heavy-load worst case.
#
# Token heuristics: eager markdown bytes/4 (words*1.3 undercounted path- and code-dense text),
# frontmatter descriptions words*1.3, MCP schema ~500 per tool, a deferred tool name ~15.

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

# --- a python that is safe to run ---
# `python` FIRST, and never a WindowsApps path: `python3` on this machine is a Microsoft Store
# App Execution Alias that hangs forever and ignores SIGTERM, and `command -v` succeeding only
# proves a file exists at that name. That made /context-budget unrunnable until 2026-08-31.
PY=""
for cand in python python3 py; do
    cand_path=$(command -v "$cand" 2>/dev/null) || continue
    case "$cand_path" in *[Ww]indows[Aa]pps*) continue ;; esac
    PY="$cand_path"; break
done

# --- the budget inputs, from the gate itself ---
# `tools/lint_docs.py --context-budget-json` names the files CLAUDE.md loads (its @-imports,
# followed the way Claude Code follows them), each rule's bytes and whether it is scoped, and
# every cap. Reading it here means the report and the gate cannot disagree.
load_budget() {
    BUDGET=""
    if [[ -z "$PY" ]]; then
        ISSUES+=("No usable python: CLAUDE.md, its imports and the rules were NOT measured")
        return
    fi
    BUDGET=$("$PY" "$REPO_ROOT/tools/lint_docs.py" --context-budget-json 2>/dev/null | "$PY" -c '
import json, sys
d = json.load(sys.stdin)
for e in d["entry_docs"]:
    print("ENTRY", e["path"], e["bytes"], e["lines"])
for r in d["rules"]:
    print("RULE", r["path"], r["bytes"], int(r["scoped"]))
for k, v in d["caps"].items():
    print("CAP", k, v)
for m in d["missing_imports"]:
    print("MISSING", m["file"] + ":" + str(m["line"]), m["target"])
' 2>/dev/null | tr -d '\r')
    [[ -z "$BUDGET" ]] && ISSUES+=("tools/lint_docs.py --context-budget-json failed: CLAUDE.md, its imports and the rules were NOT measured")
}
cap() { printf '%s\n' "$BUDGET" | awk -v k="$1" '$1 == "CAP" && $2 == k { print $3; exit }'; }

# --- per-component scans ---

scan_claude_md() {
    CLAUDE_BYTES=0
    CLAUDE_IMPORTS=0
    CLAUDE_MAX_BYTES=$(cap ENTRY_DOCS_MAX_BYTES)
    local max_lines kind path bytes lines where target
    max_lines=$(cap ENTRY_DOC_MAX_LINES)
    while read -r kind path bytes lines; do
        [[ "$kind" == ENTRY ]] || continue
        CLAUDE_BYTES=$(( CLAUDE_BYTES + bytes ))
        [[ "$path" != CLAUDE.md ]] && CLAUDE_IMPORTS=$(( CLAUDE_IMPORTS + 1 ))
        [[ -n "$max_lines" && $lines -gt $max_lines ]] && \
            ISSUES+=("$path is $lines lines (over ENTRY_DOC_MAX_LINES, $max_lines); it loads at launch")
        [[ $VERBOSE -eq 1 ]] && printf "  entry %-40s %4d lines  %6d bytes\n" "$path" "$lines" "$bytes"
    done <<< "$BUDGET"
    while read -r kind where target; do
        [[ "$kind" == MISSING ]] && ISSUES+=("$where imports $target, which does not exist")
    done <<< "$BUDGET"
    CLAUDE_TOKENS=$(( CLAUDE_BYTES / 4 ))
    [[ $VERBOSE -eq 1 ]] && echo "  CLAUDE.md with $CLAUDE_IMPORTS import(s): $CLAUDE_BYTES bytes, ~$CLAUDE_TOKENS tokens"
    [[ -n "$CLAUDE_MAX_BYTES" && $CLAUDE_BYTES -gt $CLAUDE_MAX_BYTES ]] && \
        ISSUES+=("CLAUDE.md with its imports is $CLAUDE_BYTES bytes (over ENTRY_DOCS_MAX_BYTES, $CLAUDE_MAX_BYTES)")
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
    # Rules WITHOUT a `paths:` frontmatter field load at conversation start (eager); rules
    # WITH paths: load on a matching read. lint_docs.py decides which is which, from the
    # frontmatter block (harness-facts.md "Rule loader").
    RULES_TOKENS=0          # eager (always-load) only
    RULES_COND_TOKENS=0     # conditional (paths:-gated)
    RULES_COUNT=0
    RULES_EAGER_COUNT=0
    RULES_EAGER_BYTES=0
    RULES_HEAVY=()
    local scoped_max unscoped_max kind path bytes scoped name tokens label
    scoped_max=$(cap SCOPED_RULE_MAX_BYTES)
    unscoped_max=$(cap UNSCOPED_RULES_MAX_BYTES)
    while read -r kind path bytes scoped; do
        [[ "$kind" == RULE ]] || continue
        name=$(basename "$path" .md)
        tokens=$(( bytes / 4 ))
        if [[ "$scoped" == 1 ]]; then
            label="cond "
            RULES_COND_TOKENS=$(( RULES_COND_TOKENS + tokens ))
            [[ -n "$scoped_max" && $bytes -gt $scoped_max ]] && RULES_HEAVY+=("$name ($bytes bytes)")
        else
            label="EAGER"
            RULES_TOKENS=$(( RULES_TOKENS + tokens ))
            RULES_EAGER_COUNT=$(( RULES_EAGER_COUNT + 1 ))
            RULES_EAGER_BYTES=$(( RULES_EAGER_BYTES + bytes ))
        fi
        RULES_COUNT=$(( RULES_COUNT + 1 ))
        [[ $VERBOSE -eq 1 ]] && printf "  rule  %-40s %6d bytes  ~%5d tokens  [%s]\n" "$name" "$bytes" "$tokens" "$label"
    done <<< "$BUDGET"

    [[ ${#RULES_HEAVY[@]} -gt 0 ]] && ISSUES+=("Path-scoped rules over SCOPED_RULE_MAX_BYTES ($scoped_max): ${RULES_HEAVY[*]}")
    [[ -n "$unscoped_max" && $RULES_EAGER_BYTES -gt $unscoped_max ]] && \
        ISSUES+=("Rules without paths: total $RULES_EAGER_BYTES bytes (over UNSCOPED_RULES_MAX_BYTES, $unscoped_max); they load in every session and spawn")
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

    # Estimate tokens from what actually loads: the first 200 lines, cut at 25KB,
    # whichever binds first. Counting `head -200` alone overcounts when the byte cap binds.
    local loaded
    loaded=$(head -200 "$mem_file" 2>/dev/null | head -c 25600 | wc -c | tr -d ' ')
    MEMORY_TOKENS=$(( loaded / 4 ))

    if [[ $VERBOSE -eq 1 ]]; then
        printf "  memory %-39s %4d lines  %5d bytes  ~%5d tokens\n" "MEMORY.md ($mem_file)" "$MEMORY_LINES" "$MEMORY_BYTES" "$MEMORY_TOKENS"
    fi

    # ADR-011: memory holds only machine-local resume cards; MEMORY.md stays under 40 lines
    # and 4 KB. A link to a missing file, or a memory file nothing links, is drift.
    [[ $MEMORY_LINES -gt 40 || $MEMORY_BYTES -gt 4096 ]] && \
        ISSUES+=("MEMORY.md is $MEMORY_LINES lines / $MEMORY_BYTES bytes (ADR-011 target: 40 lines, 4 KB; memory holds resume cards only)")
    if [[ -z "$PY" ]]; then
        ISSUES+=("No usable python: MEMORY.md's links were NOT checked for dead targets or orphans")
        return
    fi
    local linkcheck
    linkcheck=$("$PY" - "$mem_file" <<'PYEOF' 2>/dev/null
import pathlib, re, sys
mem = pathlib.Path(sys.argv[1])
root = mem.parent
text = mem.read_text(encoding="utf-8", errors="replace")
targets = set()
for m in re.finditer(r"\]\(([^)\s]+)\)", text):
    t = m.group(1).split("#")[0]
    if t and not re.match(r"[a-z]+:", t):
        targets.add(t)
dead = sorted(t for t in targets if not (root / t).exists())
linked = {(root / t).resolve() for t in targets if (root / t).exists()}
orphans = sorted(str(p.relative_to(root)).replace("\\", "/") for p in root.rglob("*.md")
                 if p.name != "MEMORY.md" and p.resolve() not in linked)
print("DEAD " + " ".join(dead))
print("ORPHAN " + " ".join(orphans))
PYEOF
)
    local dead orphans
    dead=$(printf '%s\n' "$linkcheck" | sed -n 's/^DEAD //p' | tr -d '\r')
    orphans=$(printf '%s\n' "$linkcheck" | sed -n 's/^ORPHAN //p' | tr -d '\r')
    [[ -n "$dead" ]] && ISSUES+=("MEMORY.md links files that do not exist: $dead")
    [[ -n "$orphans" ]] && ISSUES+=("Memory files nothing in MEMORY.md links: $orphans")
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
    MCP_DEFERRED=1
    MCP_IF_LOADED=0
    local mcp="$REPO_ROOT/.mcp.json"
    [[ ! -f "$mcp" ]] && return

    # Convert /c/... -> C:/... so native Windows Python can open the file.
    local mcp_native="$mcp"
    if command -v cygpath >/dev/null 2>&1; then
        mcp_native=$(cygpath -w "$mcp" 2>/dev/null || echo "$mcp")
    fi

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
    #   taom-moduledata: EXACT 9 — counted 2026-08-05 from tools/taom_mcp_server.py
    #                (grep -c "@mcp.tool")
    #   imagine:     HEURISTIC ~5 — HTTP server (mcp.imagine.art); unauthenticated
    #                sessions load no tools, so this is a when-authed estimate
    declare -A SERVER_TOOLS=(
        [serena]=25
        [github]=30
        [filesystem]=13
        [git]=14
        [ilspy]=4
        [sequential-thinking]=1
        [context7]=2
        [playwright]=24
        [taom-moduledata]=9
        [imagine]=5
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
    # What the schemas would cost if loaded: 500 tokens per tool + 200 per server.
    MCP_IF_LOADED=$(( total * 500 + MCP_SERVERS * 200 ))
    if [[ "${ENABLE_TOOL_SEARCH:-}" == "false" ]]; then
        MCP_DEFERRED=0
        MCP_TOKENS=$MCP_IF_LOADED
        if [[ $MCP_TOOLS_EST -gt 50 ]]; then
            ISSUES+=("MCP tool count is ~$MCP_TOOLS_EST across $MCP_SERVERS servers and ENABLE_TOOL_SEARCH=false loads every schema eagerly")
        fi
    else
        # Default: schemas are deferred behind tool search and only the names load
        # (context-window docs, verified 2026-09-23). ~15 tokens per name.
        MCP_DEFERRED=1
        MCP_TOKENS=$(( total * 15 ))
    fi
}

scan_plugins() {
    # Enabled plugins add their skill/command DESCRIPTIONS to the eager load,
    # exactly like project skills — previously invisible to this scan (the
    # 2026-07-12 baseline's biggest blind spot). Reads enabledPlugins from
    # .claude/settings.json, then measures each plugin's commands/*.md +
    # skills/*/SKILL.md frontmatter descriptions from the local plugin cache
    # (~/.claude/plugins/cache/<marketplace>/<plugin>/<ver>/). HEURISTIC:
    # description words * 1.3, same as project skills. Fail-open: a plugin
    # missing from the cache is counted as unmeasured, never an error.
    PLUGINS_COUNT=0
    PLUGINS_SKILLS=0
    PLUGINS_TOKENS=0
    local settings="$REPO_ROOT/.claude/settings.json"
    [[ ! -f "$settings" ]] && return
    local cache="$HOME/.claude/plugins/cache"
    local unmeasured=()
    while IFS= read -r entry; do
        entry="${entry%$'\r'}"   # Windows python prints \r\n on pipes
        [[ -z "$entry" ]] && continue
        PLUGINS_COUNT=$(( PLUGINS_COUNT + 1 ))
        local pname="${entry%%@*}" market="${entry#*@}"
        local pdir
        pdir=$(find "$cache/$market/$pname" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | head -1)
        if [[ -z "$pdir" ]]; then
            unmeasured+=("$entry")
            continue
        fi
        local f words
        while IFS= read -r f; do
            [[ -z "$f" ]] && continue
            PLUGINS_SKILLS=$(( PLUGINS_SKILLS + 1 ))
            words=$(grep -m1 '^description:' "$f" 2>/dev/null | wc -w | tr -d ' ')
            PLUGINS_TOKENS=$(( PLUGINS_TOKENS + words * 13 / 10 ))
            [[ $VERBOSE -eq 1 ]] && printf "  plugin %-39s %s\n" "$pname" "$(basename "$f")"
        done < <(find "$pdir/commands" -maxdepth 1 -name '*.md' 2>/dev/null; \
                 find "$pdir/skills" -maxdepth 2 -name 'SKILL.md' 2>/dev/null)
    done < <([[ -n "$PY" ]] && "$PY" - "$settings" <<'PYEOF' 2>/dev/null
import json, sys
try:
    d = json.load(open(sys.argv[1]))
    for k, v in d.get("enabledPlugins", {}).items():
        if v:
            print(k)
except Exception:
    pass
PYEOF
)
    [[ ${#unmeasured[@]} -gt 0 ]] && ISSUES+=("Plugins not in local cache (eager cost unmeasured): ${unmeasured[*]}")
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

load_budget
scan_claude_md
scan_agents
scan_skills
scan_rules
scan_hooks
scan_mcp
scan_plugins
scan_memory

[[ $VERBOSE -eq 1 ]] && echo

# EAGER total: what Claude Code actually loads at conversation start.
# Counts: CLAUDE.md with its imports (full), agent and skill FRONTMATTER only, the rules
# without paths: (full), MCP tool names (schemas only when ENABLE_TOOL_SEARCH=false),
# enabled-plugin skill/command descriptions, MEMORY.md (first 200 lines / 25KB).
# Excludes: skill bodies (lazy, on invocation), agent bodies (lazy, on Task spawn),
# hook scripts (run by harness, never in model context), Claude Code's own system
# prompt boilerplate (~3-5K, fixed per-version).
TOTAL=$(( CLAUDE_TOKENS + AGENTS_TOKENS + SKILLS_TOKENS + RULES_TOKENS + MCP_TOKENS + PLUGINS_TOKENS + MEMORY_TOKENS ))

# LAZY total — eager + worst-case if every skill and agent were invoked.
LAZY_DELTA=$(( (SKILLS_LAZY_TOKENS - SKILLS_TOKENS) + (AGENTS_LAZY_TOKENS - AGENTS_TOKENS) ))
WORST_CASE=$(( TOTAL + LAZY_DELTA ))

# 1M-class window assumed; session models vary (the label no longer names one —
# the pre-2026-08-05 script claimed "Opus 4.7" regardless of the actual model).
WINDOW=1000000
HEADROOM=$(( WINDOW - TOTAL ))
PCT_USED=$(( TOTAL * 100 / WINDOW ))

cat <<EOF
Context model:                    session model (1M-class window assumed)
Eager (startup) baseline:         ~${TOTAL} tokens
Effective available:              ~${HEADROOM} tokens (~$(( 100 - PCT_USED ))% headroom)
Baseline as % of window:          ${PCT_USED}%
Worst-case (all skills + agents invoked): ~${WORST_CASE} tokens (+~${LAZY_DELTA} lazy)
Per custom-agent spawn:           ~$(( CLAUDE_TOKENS + RULES_TOKENS )) tokens (CLAUDE.md + imports + rules without paths:; no MEMORY.md)

Component breakdown:
+------------------+--------+-----------+--------------+
| Component        | Count  | Eager tok | If-invoked   |
+------------------+--------+-----------+--------------+
EOF
printf "| %-16s | %6d | %9d | %12s |\n" "CLAUDE.md+imp"   $(( 1 + CLAUDE_IMPORTS )) "$CLAUDE_TOKENS" "—"
printf "| %-16s | %6d | %9d | %12d |\n" "Agents"        "$AGENTS_COUNT"      "$AGENTS_TOKENS" "$AGENTS_LAZY_TOKENS"
printf "| %-16s | %6d | %9d | %12d |\n" "Skills"        "$SKILLS_COUNT"      "$SKILLS_TOKENS" "$SKILLS_LAZY_TOKENS"
printf "| %-16s | %6d | %9d | %12d |\n" "Rules"         "$RULES_COUNT"       "$RULES_TOKENS" "$RULES_COND_TOKENS"
printf "| %-16s | %6d | %9d | %12s |\n" "MCP servers"   "$MCP_SERVERS"       "$MCP_TOKENS" "$( [[ $MCP_DEFERRED -eq 1 ]] && echo "${MCP_IF_LOADED} if eager" || echo "—" )"
printf "| %-16s | %6d | %9d | %12s |\n" "Plugins"       "$PLUGINS_COUNT"     "$PLUGINS_TOKENS" "—"
printf "| %-16s | %6d | %9d | %12s |\n" "MEMORY.md"     1                    "$MEMORY_TOKENS" "—"
printf "| %-16s | %6d | %9s | %12s |\n" "Hooks (.sh)"   "$HOOKS_COUNT"       "(not in ctx)" "—"
echo "+------------------+--------+-----------+--------------+"
echo "Eager = loaded at startup. MCP counts tool names only while schemas are deferred"
echo "  (the default); the If-invoked column shows what the schemas would cost if loaded."
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
[[ $MCP_DEFERRED -eq 0 && $MCP_TOKENS -gt 15000 ]] && RECS+=("MCP schemas load eagerly (~${MCP_TOKENS} tokens): unset ENABLE_TOOL_SEARCH=false, or drop servers that wrap a CLI (gh, git).")
[[ -n "$CLAUDE_MAX_BYTES" && $CLAUDE_BYTES -gt $CLAUDE_MAX_BYTES ]] && RECS+=("CLAUDE.md with its imports is ${CLAUDE_BYTES} bytes, over ${CLAUDE_MAX_BYTES}: route detail to its owning doc, a path rule or a skill (ADR-011). Each byte cut saves a quarter token per session and spawn.")
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
