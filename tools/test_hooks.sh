#!/usr/bin/env bash
# test_hooks.sh — contract regression test for every Claude Code hook in this repo.
#
# WHY THIS EXISTS
# On 2026-08-31 every JSON-parsing hook wedged: they parsed stdin with `jq`, falling back
# to `python3`, and on the dev machine `python3` is a Microsoft Store App Execution Alias
# that prints nothing, never exits and ignores SIGTERM. No registration carried a
# `timeout`, so a Bash tool call paid one 600s PreToolUse batch plus one 600s PostToolUse
# batch: the 20.0-minute stalls in the overnight transcripts.
#
# The repair pass that followed introduced a second, quieter failure: timeouts were sized
# against each hook's FAST path, so `check-moduledata-validation` (27.0s of work) was
# registered at 5s. A harness timeout kill DISCARDS the hook's output, so an overrun is
# indistinguishable from a clean pass, and the gate was silently dead.
#
# Both classes are mechanical and both are caught here. Run before committing any change
# under .claude/hooks/, .claude/skills/*/\*.sh, or .claude/settings.json.
#
#   bash tools/test_hooks.sh            # all checks
#   bash tools/test_hooks.sh --verbose  # show every hook/payload result
#
# Exit 0 = all checks pass. Exit 1 = at least one failed.

set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO" || exit 1

VERBOSE=0
[[ "${1:-}" == "--verbose" || "${1:-}" == "-v" ]] && VERBOSE=1

PASS=0; FAIL=0; FAILED_DETAIL=()

ok()   { PASS=$((PASS+1)); [[ $VERBOSE -eq 1 ]] && printf '  \033[32mok\033[0m   %s\n' "$1"; return 0; }
bad()  { FAIL=$((FAIL+1)); FAILED_DETAIL+=("$1"); printf '  \033[31mFAIL\033[0m %s\n' "$1"; return 0; }
head2() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# A real interpreter for the harness itself. Never `python3` (see header).
HPY=""
for c in python py; do
    p=$(command -v "$c" 2>/dev/null) || continue
    case "$p" in *[Ww]indows[Aa]pps*) continue ;; esac
    HPY="$p"; break
done
[[ -z "$HPY" ]] && { echo "test_hooks: no safe python for the harness itself; cannot run."; exit 1; }

# ---------------------------------------------------------------------------
# 1. No hook may ever spell it `python3`.
# ---------------------------------------------------------------------------
head2 "1. no UNGUARDED python3 (.claude/ and tools/*.sh)"
# The rule is not "never mention python3". On Linux `python3` is the correct spelling and
# `python` may not exist, so a portable candidate list may legitimately include it. What
# must never happen is EXECUTING it without first rejecting a WindowsApps path, because
# `command -v` succeeding proves only that a file exists at that name.
#
# So: a file may reference python3 only if it also carries a WindowsApps rejection, or
# delegates to _pybin.sh which does. Comments are ignored. .github/workflows/ is exempt
# entirely (Linux runner, real python3).
HITS=$("$HPY" - <<'PY'
import pathlib, re
bad = []
roots = [pathlib.Path('.claude'), pathlib.Path('tools')]
for root in roots:
    if not root.exists():
        continue
    for p in root.rglob('*'):
        if p.suffix not in ('.sh', '.md', '.json') or not p.is_file():
            continue
        if p.name in ('_pybin.sh', 'test_hooks.sh'):
            continue
        if root.name == 'tools' and p.suffix != '.sh':
            continue
        try:
            txt = p.read_text(encoding='utf-8', errors='replace')
        except OSError:
            continue
        code = [l for l in txt.splitlines() if not l.lstrip().startswith('#')]
        if not any('python3' in l for l in code):
            continue
        guarded = ('WindowsApps' in txt) or ('windowsapps' in txt.lower()) or ('_pybin.sh' in txt)
        if not guarded:
            for i, l in enumerate(txt.splitlines(), 1):
                if 'python3' in l and not l.lstrip().startswith('#'):
                    bad.append(f"{p}:{i}: {l.strip()[:90]}")
for b in bad:
    print(b)
PY
)
if [[ -z "$HITS" ]]; then
    ok "every python3 reference is behind a WindowsApps guard"
else
    bad "unguarded python3 (the Store alias hangs forever from Git Bash):"
    printf '       %s\n' "$HITS"
fi

# ---------------------------------------------------------------------------
# 2. Every hook registration carries an explicit timeout.
#    Covers settings.json AND skill-frontmatter registrations, which the
#    2026-08-31 pass missed entirely (5 in freeze/ and investigate/).
# ---------------------------------------------------------------------------
head2 "2. every registration has an explicit timeout"
CENSUS=$("$HPY" - <<'PY'
import json, re, sys, pathlib
missing, total = [], 0
d = json.load(open('.claude/settings.json', encoding='utf-8'))
for ev, groups in d.get('hooks', {}).items():
    for g in groups:
        for h in g.get('hooks', []):
            total += 1
            if 'timeout' not in h:
                missing.append(f"settings.json {ev} {h.get('command','?')}")
# Skill frontmatter: a YAML-free scan, so this runs without pyyaml installed.
for p in pathlib.Path('.claude/skills').glob('*/SKILL.md'):
    txt = p.read_text(encoding='utf-8', errors='replace')
    m = re.match(r'^---\n(.*?)\n---\n', txt, re.S)
    if not m or 'hooks:' not in m.group(1):
        continue
    fm = m.group(1)
    for blk in re.finditer(r'-\s*type:\s*command\n(?:\s+.*\n)*?(?=\s*-\s|\Z)', fm):
        total += 1
        if 'timeout:' not in blk.group(0):
            missing.append(f"{p} (frontmatter)")
print(total)
for x in missing:
    print("MISSING", x)
PY
)
TOTAL_REG=$(printf '%s' "$CENSUS" | head -1)
MISSING=$(printf '%s' "$CENSUS" | grep '^MISSING' || true)
if [[ -z "$MISSING" ]]; then
    ok "all $TOTAL_REG registrations have a timeout"
else
    bad "registrations with no timeout (they inherit the 600s default):"
    printf '       %s\n' "$MISSING"
fi

# ---------------------------------------------------------------------------
# 3. Every hook's registered timeout must exceed its measured slow path.
#    This is the check that would have caught the dead ModuleData gate.
# ---------------------------------------------------------------------------
head2 "3. registered timeout vs measured slow path"
declare -A SLOW_TOOL=(
    ["check-moduledata-validation.sh"]="tools/validate_moduledata.py --code BROKEN_ITEM_REF"
    ["check-doc-config-drift.sh"]="tools/lint_docs.py --fail-on-drift"
    ["check-polearm-shield-parity.sh"]="tools/audit_polearm_shield_parity.py"
    ["check-native-dll-crt.sh"]="tools/pe_inspect.py"
)
for hook in "${!SLOW_TOOL[@]}"; do
    REG=$("$HPY" -c "
import json,sys
d=json.load(open('.claude/settings.json',encoding='utf-8'))
for ev,gs in d['hooks'].items():
    for g in gs:
        for h in g['hooks']:
            if h['command'].endswith('$hook'): print(h.get('timeout',600)); sys.exit()
print(0)")
    [[ "$REG" == "0" ]] && { ok "$hook not registered, skipped"; continue; }
    INNER=$(grep -oE 'timeout -k [0-9]+ [0-9]+' ".claude/hooks/$hook" 2>/dev/null | head -1 | awk '{print $4}')
    if [[ -z "$INNER" ]]; then
        bad "$hook runs an external tool with NO inner timeout: a harness kill would be silent"
    elif (( INNER >= REG )); then
        bad "$hook inner bound ${INNER}s >= registered ${REG}s (harness kills first, silently)"
    else
        ok "$hook inner ${INNER}s < registered ${REG}s"
    fi
done

# ---------------------------------------------------------------------------
# 4. Contract test: every hook, every payload shape, in a sandbox.
#    Asserts: terminates fast, exit in {0,2}, stdout empty or valid JSON.
# ---------------------------------------------------------------------------
head2 "4. hook contract (exit code, JSON, no hang)"
SANDBOX=$(mktemp -d 2>/dev/null) || SANDBOX="/tmp/taom-hooktest-$$"
mkdir -p "$SANDBOX/.claude/logs" "$SANDBOX/.claude/hooks" "$SANDBOX/Main"
cp .claude/hooks/_pybin.sh "$SANDBOX/.claude/hooks/" 2>/dev/null
cleanup() { rm -rf "$SANDBOX"; }
trap cleanup EXIT

# Only PreToolUse / PostToolUse hooks speak the JSON decision protocol. SessionStart,
# PreCompact, PostCompact, Stop, SubagentStart and SessionEnd hooks deliberately print
# human-readable context to stdout, which the harness injects verbatim. Applying the JSON
# rule to those would be a false positive, so classify from settings.json first.
GATE_HOOKS=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
names = set()
for ev, groups in d.get('hooks', {}).items():
    if ev not in ('PreToolUse', 'PostToolUse', 'PostToolUseFailure'):
        continue
    for g in groups:
        for h in g.get('hooks', []):
            names.add(h['command'].rsplit('/', 1)[-1])
print(' '.join(sorted(names)))
PY
)
# check-freeze.sh is registered from skill frontmatter, not settings.json, but is a gate.
GATE_HOOKS="$GATE_HOOKS check-freeze.sh"
is_gate() { [[ " $GATE_HOOKS " == *" $1 "* ]]; }

PAYLOADS=(
  'bash|{"tool_name":"Bash","tool_input":{"command":"echo hi"},"hook_event_name":"PreToolUse"}'
  'edit|{"tool_name":"Edit","tool_input":{"file_path":"'"$SANDBOX"'/Main/Thing.cs"},"hook_event_name":"PreToolUse"}'
  'mcp|{"tool_name":"mcp__serena__find_symbol","tool_input":{},"hook_event_name":"PreToolUse"}'
  'session|{"hook_event_name":"SessionStart","session_id":"test","source":"startup"}'
  'empty|{}'
)

for hookfile in .claude/hooks/*.sh .claude/skills/freeze/check-freeze.sh; do
    name=$(basename "$hookfile")
    [[ "$name" == "_pybin.sh" ]] && continue
    for entry in "${PAYLOADS[@]}"; do
        label="${entry%%|*}"; payload="${entry#*|}"
        S=$(date +%s%N)
        OUT=$(printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$hookfile" 2>/dev/null)
        RC=$?
        E=$(date +%s%N); MS=$(( (E-S)/1000000 ))

        if [[ $RC -eq 124 || $RC -eq 137 ]]; then
            bad "$name [$label] HUNG (killed at 10s) — this is the 2026-08-31 bug class"
            continue
        fi
        if [[ $RC -ne 0 && $RC -ne 2 ]]; then
            bad "$name [$label] exit $RC (contract allows only 0=allow or 2=block)"
            continue
        fi
        if [[ -n "$OUT" ]] && is_gate "$name"; then
            if ! printf '%s' "$OUT" | "$HPY" -c 'import sys,json; json.loads(sys.stdin.read())' 2>/dev/null; then
                bad "$name [$label] stdout is not valid JSON: $(printf '%s' "$OUT" | head -c 80)"
                continue
            fi
        fi
        if (( MS > 3000 )); then
            bad "$name [$label] took ${MS}ms on a trivial payload (expected <3000ms)"
            continue
        fi
        ok "$name [$label] rc=$RC ${MS}ms"
    done
done

# ---------------------------------------------------------------------------
# 5. Starved environment: no jq, no python at all.
#    Every hook must still terminate promptly and must NOT block. This is the
#    fail-open mandate in .claude/rules/harness-facts.md, tested rather than assumed.
# ---------------------------------------------------------------------------
head2 "5. fail-open with no jq and no python on PATH"
for hookfile in .claude/hooks/*.sh .claude/skills/freeze/check-freeze.sh; do
    name=$(basename "$hookfile")
    [[ "$name" == "_pybin.sh" ]] && continue
    S=$(date +%s%N)
    OUT=$(printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git push --force origin master"},"hook_event_name":"PreToolUse"}' \
          | timeout -k 2 10 env PATH=/usr/bin:/bin CLAUDE_PROJECT_DIR="$SANDBOX" TAOM_PYBIN= bash "$hookfile" 2>/dev/null)
    RC=$?
    E=$(date +%s%N); MS=$(( (E-S)/1000000 ))
    if [[ $RC -eq 124 || $RC -eq 137 ]]; then
        bad "$name HUNG with no interpreter available"
    elif [[ $RC -ne 0 && $RC -ne 2 ]]; then
        bad "$name exit $RC in a starved environment (must fail open, not error)"
    elif printf '%s' "$OUT" | grep -q '"permissionDecision":"deny"'; then
        bad "$name DENIED in a starved environment (a hook's own fault must never block)"
    else
        ok "$name failed open in ${MS}ms"
    fi
done

# ---------------------------------------------------------------------------
head2 "Summary"
printf '  %d passed, %d failed\n' "$PASS" "$FAIL"
if (( FAIL > 0 )); then
    printf '\n  Failures:\n'
    printf '    - %s\n' "${FAILED_DETAIL[@]}"
    exit 1
fi
exit 0
