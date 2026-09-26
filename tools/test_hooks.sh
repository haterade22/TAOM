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

# A real interpreter for the harness itself.
#
# `python3` IS in this list, deliberately: the WindowsApps rejection below is what makes it
# safe, not the spelling. Omitting it would make this script exit 1 on any Linux box that
# ships only python3, which is most of them, and CI would then fail for the wrong reason.
HPY=""
for c in python python3 py; do
    p=$(command -v "$c" 2>/dev/null) || continue
    case "${p,,}" in *windowsapps*) continue ;; esac
    HPY="$p"; break
done
[[ -z "$HPY" ]] && { echo "test_hooks: no safe python for the harness itself; cannot run."; exit 1; }

# A PreToolUse gate's decision, read the way Claude Code reads it: only
# hookSpecificOutput.permissionDecision counts (hooks docs, "PreToolUse decision control").
# Empty output and {} are allow. A top-level permissionDecision or decision is BADSHAPE: the
# harness ignores it, which left nine gates inert until #647 while every text-matching test
# here passed.
decision_of() {
    printf '%s' "$1" | "$HPY" -c '
import json, sys
raw = sys.stdin.read().strip() or "{}"
try:
    d = json.loads(raw)
except Exception:
    print("invalid"); sys.exit()
if not isinstance(d, dict) or "permissionDecision" in d or "decision" in d:
    print("BADSHAPE"); sys.exit()
h = d.get("hookSpecificOutput")
if h is None:
    print("allow"); sys.exit()
ok = (isinstance(h, dict) and h.get("hookEventName") == "PreToolUse"
      and h.get("permissionDecision") in ("allow", "deny", "ask"))
print(h["permissionDecision"] if ok else "BADSHAPE")'
}

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
# DISCOVERED, not hardcoded. This check first shipped with a four-name array while the
# CHANGELOG, the RCA and hook-authoring.md all claimed it caught "any hook running an
# external tool with no inner bound". A confident instruction that outlives its truth is
# the exact shape of the hooks-catalog.md sentence that caused the original outage, so
# the list is derived from the hooks themselves and cannot rot.
TOOL_HOOKS=$(grep -lE '"\$PY(BIN)?" +[^ ]*tools/[A-Za-z0-9_]+\.py|tools/[A-Za-z0-9_]+\.py' .claude/hooks/*.sh 2>/dev/null              | xargs -r grep -lE '\$\{?PY' 2>/dev/null || true)
if [[ -z "$TOOL_HOOKS" ]]; then
    bad "discovery found no tool-invoking hooks at all; the check 3 pattern is broken"
fi
for hookfile in $TOOL_HOOKS; do
    hook=$(basename "$hookfile")
    REG=$("$HPY" - "$hook" <<'PYEOF'
import json, sys
name = sys.argv[1]
d = json.load(open('.claude/settings.json', encoding='utf-8'))
for ev, gs in d['hooks'].items():
    for g in gs:
        for h in g['hooks']:
            if h['command'].endswith(name):
                print(h.get('timeout', 600)); sys.exit()
print(0)
PYEOF
)
    [[ "$REG" == "0" ]] && { ok "$hook not registered in settings.json, skipped"; continue; }
    # Accept fractional bounds (timeout -k 0.2 0.8) as well as integers.
    INNER=$(grep -oE 'timeout -k [0-9.]+ [0-9.]+' "$hookfile" 2>/dev/null | head -1 | awk '{print $4}')
    if [[ -z "$INNER" ]]; then
        bad "$hook invokes a tools/*.py but has NO inner timeout: a harness kill would be silent"
    elif "$HPY" -c "import sys; sys.exit(0 if float(sys.argv[1]) >= float(sys.argv[2]) else 1)" "$INNER" "$REG"; then
        bad "$hook inner bound ${INNER}s >= registered ${REG}s (harness kills first, silently)"
    else
        ok "$hook inner ${INNER}s < registered ${REG}s"
    fi
done

# ---------------------------------------------------------------------------
# 3b. Every skill, agent and rule frontmatter must parse as YAML.
#     Four SKILL.md files shipped with an unquoted `argument-hint: [a] [b]`, which is
#     not valid YAML, so the WHOLE frontmatter was dropped and those skills lost their
#     eager description from the model's routing surface. Nothing detected it. For a
#     rule the failure is worse than a lost field: Claude Code loads a rule whose
#     frontmatter does not parse in EVERY session, as if it had no `paths:` (ADR-011).
# ---------------------------------------------------------------------------
head2 "3b. skill, agent and rule frontmatter parses as YAML"
FM=$("$HPY" - <<'PYEOF'
import pathlib, sys
try:
    import yaml
except ImportError:
    print("SKIP no pyyaml"); sys.exit(0)

def frontmatter(txt):
    """Line-based, deliberately. A regex here needs escaped newlines, and this file is
    a shell heredoc inside a shell script: the escaping was wrong the first time and the
    resulting SyntaxError was swallowed into a PASS."""
    lines = txt.splitlines()
    if not lines or lines[0].strip() != '---':
        return None
    for i in range(1, len(lines)):
        if lines[i].strip() == '---':
            return "\n".join(lines[1:i])
    return None

bad = []
n = 0
for p in (list(pathlib.Path('.claude/skills').glob('*/SKILL.md'))
          + list(pathlib.Path('.claude/agents').glob('*.md'))
          + list(pathlib.Path('.claude/rules').rglob('*.md'))):   # rules load recursively
    txt = p.read_text(encoding='utf-8', errors='replace')
    fm = frontmatter(txt)
    if fm is None:
        continue
    n += 1
    try:
        d = yaml.safe_load(fm)
    except Exception as e:
        bad.append(f"{p}: {type(e).__name__}: {str(e).splitlines()[0][:80]}")
        continue
    if not isinstance(d, dict):
        bad.append(f"{p}: frontmatter parsed as {type(d).__name__}, expected a mapping")
        continue
    # An unquoted `#` silently truncates a description into a YAML comment. That one is
    # PROVEN: /release's description was cut at "Enforces the" in the live skill listing,
    # and quoting it restored the full text in the same session.
    #
    # argument-hint is deliberately NOT type-checked. 16 skills write
    # `argument-hint: [feature-name]`, which YAML reads as a list. That is the conventional
    # way the hint is written and there is no evidence the harness mishandles it, so
    # failing on it would make this suite permanently red over an unverified claim. The
    # real defect in that field is a PARSE failure (`[--quick] [--write-report]` is two
    # flow sequences and takes the whole frontmatter down), and the try/except above
    # already catches exactly that.
    v = d.get('description')
    if v is not None and not isinstance(v, str):
        bad.append(f"{p}: description parsed as {type(v).__name__}, expected a quoted string")

    # The `#` truncation yields a VALID, SHORTER string, so no type check can see it.
    # Catch it in the raw text instead: an unquoted scalar containing " #" loses
    # everything from the # onward to a YAML comment. /release lost "the #371
    # Dependencies pairing." this way and read as "...Enforces the" in the live listing.
    for raw in fm.splitlines():
        stripped = raw.strip()
        for field in ('description:', 'argument-hint:'):
            if not stripped.startswith(field):
                continue
            val = stripped[len(field):].strip()
            if val[:1] in ('"', "'"):
                continue  # quoted, the # is safe
            if ' #' in val:
                bad.append(f"{p}: unquoted '#' in {field[:-1]} truncates it at that point (YAML comment); quote the value")
print(f"COUNT {n}")
for b in bad:
    print("BAD " + b)
PYEOF
)
# The check must be able to FAIL. The first version of this block died on a SyntaxError,
# $FM came back empty, and the else-branch reported a pass with a blank count: a broken
# check reading as a green one, which is the whole defect class this suite exists to catch.
if printf '%s' "$FM" | grep -q '^SKIP'; then
    # Loud, not silent: an unrun check must never read as a pass.
    echo "  note: pyyaml unavailable, frontmatter parse check SKIPPED (not passed)"
elif ! printf '%s' "$FM" | grep -q '^COUNT [0-9]'; then
    bad "frontmatter check did not run (no COUNT line). Output was: $(printf '%s' "$FM" | head -c 200)"
elif printf '%s' "$FM" | grep -q '^BAD '; then
    while IFS= read -r line; do
        [[ "$line" == BAD\ * ]] && bad "frontmatter: ${line#BAD }"
    done <<< "$FM"
else
    ok "all $(printf '%s' "$FM" | sed -n 's/^COUNT //p') frontmatters parse; description/argument-hint are strings"
fi

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

# Only PreToolUse, PostToolUse and PostToolUseFailure hooks must print JSON (PreToolUse under
# hookSpecificOutput, PRE_GATES below). SessionStart, PreCompact, PostCompact, SubagentStart and
# SessionEnd hooks print plain text, and Stop hooks print a top-level {"decision":"block"}
# (section 7a checks it). Applying the JSON rule to the plain-text hooks would be a false
# positive, so classify from settings.json first.
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
# The PreToolUse subset: only these must use hookSpecificOutput. PostToolUse keeps the
# top-level `decision` field as its current format, so it is judged by validity alone.
PRE_GATES=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(' '.join(sorted({h['command'].rsplit('/', 1)[-1]
                       for g in d.get('hooks', {}).get('PreToolUse', [])
                       for h in g.get('hooks', [])})))
PY
)
PRE_GATES="$PRE_GATES check-freeze.sh"
is_pre_gate() { [[ " $PRE_GATES " == *" $1 "* ]]; }

# bash-trigger holds every Bash hook's prefilter text (git, dotnet, commit, push, no-verify)
# but matches no hook's trigger (`committed` and `pushed` are not the subcommands, and
# `no-verify` lacks its `--`), so the contract covers the parse path behind each prefilter;
# `echo hi` alone now stops at the prefilter in every Bash hook (review of plan 013).
# powershell-trigger runs the same parse paths through the PowerShell reader (plan 027).
PAYLOADS=(
  'bash|{"tool_name":"Bash","tool_input":{"command":"echo hi"},"hook_event_name":"PreToolUse"}'
  'bash-trigger|{"tool_name":"Bash","tool_input":{"command":"git status && dotnet --info && echo committed pushed no-verify"},"hook_event_name":"PreToolUse"}'
  'powershell-trigger|{"tool_name":"PowerShell","tool_input":{"command":"git status; dotnet --info; Write-Output committed pushed no-verify"},"hook_event_name":"PreToolUse"}'
  'edit|{"tool_name":"Edit","tool_input":{"file_path":"'"$SANDBOX"'/Main/Thing.cs"},"hook_event_name":"PreToolUse"}'
  'mcp|{"tool_name":"mcp__serena__find_symbol","tool_input":{},"hook_event_name":"PreToolUse"}'
  'session|{"hook_event_name":"SessionStart","session_id":"test","source":"startup"}'
  'empty|{}'
)

for hookfile in .claude/hooks/*.sh .claude/skills/freeze/check-freeze.sh; do
    name=$(basename "$hookfile")
    [[ "$name" == _*.sh ]] && continue
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
            if is_pre_gate "$name" && [[ "$(decision_of "$OUT")" == BADSHAPE ]]; then
                bad "$name [$label] prints a decision Claude Code ignores (not under hookSpecificOutput): $(printf '%s' "$OUT" | head -c 80)"
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
# 4b. Every PreToolUse gate answers a commit payload on the REAL repo well inside its
#     registration. Check 3 times only hooks that launch a tools/*.py; check-claude-files-
#     tracked.sh ran git two or three times per file instead, took 6 s against its 5 s
#     registration, and was killed on every commit (Codex review 2026-09-23). The payload is
#     read-only for every gate: nothing is staged or written.
# ---------------------------------------------------------------------------
head2 "4b. every PreToolUse gate answers a commit inside 80% of its registration"
for name in $PRE_GATES; do
    [[ "$name" == check-freeze.sh ]] && continue
    REG=$("$HPY" - "$name" <<'PYEOF'
import json, sys
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(next((h.get('timeout', 600) for g in d['hooks'].get('PreToolUse', []) for h in g['hooks']
            if h['command'].endswith(sys.argv[1])), 0))
PYEOF
)
    S=$(date +%s%N)
    printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git commit -m \"docs: v0.0.0 - timing probe\""},"hook_event_name":"PreToolUse"}' \
        | timeout -k 2 65 env CLAUDE_PROJECT_DIR="$REPO" bash ".claude/hooks/$name" >/dev/null 2>&1
    MS=$(( ($(date +%s%N) - S) / 1000000 ))
    if (( MS * 10 >= REG * 1000 * 8 )); then
        bad "$name took ${MS}ms on a commit payload against its ${REG}s registration: the harness kills it (silently) under load"
    else
        ok "$name ${MS}ms of ${REG}s"
    fi
done

# ---------------------------------------------------------------------------
# 4c. A Bash call that cannot concern a hook starts no Python in it.
#     Every Bash-path hook used to source _pybin.sh (one Python start, the probe) and
#     parse the payload (a second) before it looked at the command: 256 to 451 ms per
#     hook on an `ls`, for 13 hooks on every Bash call. Each now tests the RAW payload
#     for its trigger text first. Each row runs the hook under `bash -x` and reads the
#     trace for its `source` of _pybin.sh, the only road to Python in these hooks. The
#     first oracle counted starts of a fake interpreter pinned through TAOM_PYBIN, and
#     it flaked under load: _pybin.sh drops a pin that misses its 0.8 s probe and finds
#     the real python, which counted nothing (review of plan 013). The trace does not
#     depend on timing; the fake stays to keep the parse cheap, and its count is a
#     second witness on the negative row. The trigger rows are multi-line on purpose: a
#     newline before `git` arrives as the two characters \n, which is how a token regex
#     over the raw JSON would have skipped a real commit. Hooks are discovered from
#     settings.json, so a new Bash hook that sources _pybin.sh before it filters fails.
# ---------------------------------------------------------------------------
head2 "4c. the Bash hooks start no Python on a payload that cannot concern them"
PF="$SANDBOX/prefilter"
mkdir -p "$PF"
FAKEPY="$PF/fakepy"
printf '#!/bin/sh\necho started >> "%s/starts"\nprintf taompy\n' "$PF" > "$FAKEPY"
chmod +x "$FAKEPY" 2>/dev/null
pf_payload() {  # $1 event, $2 command already JSON-escaped, $3 tool (default Bash); printf %s keeps its backslashes
    printf '{"tool_name":"%s","session_id":"taom-prefilter-test","hook_event_name":"%s","tool_input":{"command":"%s","description":"prefilter probe"},"tool_response":{"stdout":"ok","stderr":""}}' "${3:-Bash}" "$1" "$2"
}
pf_run() {      # $1 hook file name, $2 payload; prints "<times _pybin.sh was sourced> <fake starts>"
    rm -f "$PF/starts" "$PF/trace"
    printf '%s' "$2" | timeout -k 2 10 env PS4='+ ' CLAUDE_PROJECT_DIR="$SANDBOX" TAOM_PYBIN="$FAKEPY" \
        bash -x ".claude/hooks/$1" >/dev/null 2>"$PF/trace"
    local s n=0
    s=$(grep -cE '^\++ (source|\.) .*_pybin\.sh$' "$PF/trace" 2>/dev/null)
    [[ -f "$PF/starts" ]] && n=$(grep -c . "$PF/starts")
    echo "${s:-0} $n"
}
PF_ROWS=$("$HPY" - <<'PY'
import json, re
d = json.load(open('.claude/settings.json', encoding='utf-8'))
def matches_bash(m):  # the harness treats a matcher as a regex; '' and '*' mean every tool
    if m in ('', '*'):
        return True
    try:
        return re.fullmatch(m, 'Bash') is not None
    except re.error:
        return False
rows = set()
for ev in ('PreToolUse', 'PostToolUse', 'PostToolUseFailure'):
    for g in d.get('hooks', {}).get(ev, []):
        if matches_bash(g.get('matcher', '')):
            for h in g.get('hooks', []):
                rows.add(ev + ':' + h['command'].rsplit('/', 1)[-1])
print(' '.join(sorted(rows)))
PY
)
# The gates whose prefilter is their own word rather than `git`: each must also skip
# `git status`, `git diff` and `git log`.
PF_NARROWED_LIST=(check-claude-files-tracked.sh check-commit-subject-version.sh
                  check-moduledata-validation.sh check-native-dll-crt.sh check-doc-config-drift.sh
                  validate-push.sh block-no-verify.sh check-graphify-usage.sh)
PF_NARROWED="${PF_NARROWED_LIST[*]}"
PF_U='\'u    # the two characters backslash and u: a JSON escape prefix, as the raw payload holds it
if [[ -z "$PF_ROWS" ]]; then
    bad "4c discovery found no Bash-matched hooks in settings.json; the check is broken"
else
    for row in $PF_ROWS; do
        ev="${row%%:*}"; name="${row#*:}"
        [[ -f ".claude/hooks/$name" ]] || { bad "4c: $name is registered but missing from .claude/hooks/"; continue; }
        read -r s n <<< "$(pf_run "$name" "$(pf_payload "$ev" 'ls docs')")"
        if [[ "$s" == 0 && "$n" == 0 ]]; then
            ok "$name [$ev] no interpreter on a non-trigger payload"
        else
            bad "$name [$ev] reached _pybin.sh ($s source, $n start) on a payload that cannot concern it (ls docs): test the raw payload before sourcing _pybin.sh"
        fi
        if [[ "$ev" == PostToolUse* ]]; then
            triggers=('cd /x\ndotnet test TAOM.Tests')
            [[ "$name" == mark-verification-run.sh ]] && triggers+=('cd /x\npwsh ./build.ps1 -RunTests')
        else
            # Each gate's own word (maintainer decision D39): validate-push.sh filters on
            # `push`, block-no-verify.sh on `no-verify`, the five commit gates on `commit`,
            # and the two confirm gates on `git`; a `git commit` row reaches the last two sets.
            case "$name" in
                # validate-push.sh finds `push` by token, so `git -C <dir> push` is its trigger too.
                validate-push.sh)   triggers=('cd /x\ngit push origin x' 'cd /x\ngit -C /y push origin x') ;;
                block-no-verify.sh) triggers=('cd /x\ngit commit --no-verify -m x') ;;
                check-graphify-usage.sh) triggers=('cd /x\ngraphify update /y' 'echo a; graphify extract .') ;;
                # The commit gates also trigger on `git -C <dir> commit`, which holds `commit`
                # but not `git commit`: its own row keeps a prefilter from narrowing to the latter.
                *)                  triggers=('cd /x\ngit commit -m x' 'cd /x\ngit -C /y commit -m x') ;;
            esac
        fi
        for cmd in "${triggers[@]}"; do
            read -r s n <<< "$(pf_run "$name" "$(pf_payload "$ev" "$cmd")")"
            if [[ "$s" -ge 1 ]]; then
                ok "$name [$ev] reaches _pybin.sh on a multi-line trigger payload [$cmd]"
            else
                bad "$name [$ev] never reached _pybin.sh on a trigger payload [$cmd]: the prefilter is narrower than the hook's own trigger"
            fi
        done
        # The hook's word spelled with a JSON \u escape must still reach the parser
        # (maintainer decision D40): the raw test cannot read an escaped letter, so a
        # payload holding any \u takes the full path.
        esc=""
        case "$name" in
            validate-push.sh)       esc="cd /x\ngit ${PF_U}0070ush origin x" ;;
            block-no-verify.sh)     esc="cd /x\ngit commit --${PF_U}006eo-verify -m x" ;;
            check-graphify-usage.sh) esc="cd /x\n${PF_U}0067raphify update /y" ;;
            mark-verification-run.sh)
                                    esc="cd /x\n${PF_U}0064otnet test TAOM.Tests" ;;
            # The commit gates, the two confirm gates and any new Bash hook: the row holds no
            # literal `git` or `commit`, so a hook filtering on either word without the
            # escape arm skips it and fails here.
            *)                      esc="cd /x\n${PF_U}0067it ${PF_U}0063ommit -m x" ;;
        esac
        if [[ -n "$esc" ]]; then
            read -r s n <<< "$(pf_run "$name" "$(pf_payload "$ev" "$esc")")"
            if [[ "$s" -ge 1 ]]; then
                ok "$name [$ev] reaches _pybin.sh when its word is escaped [$esc]"
            else
                bad "$name [$ev] never reached _pybin.sh when its word is escaped [$esc]: an escaped letter hid the gated word from the prefilter"
            fi
        fi
        # A gate narrowed to its own word starts no Python on a git call it does not judge.
        if [[ " $PF_NARROWED " == *" $name "* ]]; then
            for cmd in 'git status --short' 'git diff --stat' 'git log --oneline -5'; do
                read -r s n <<< "$(pf_run "$name" "$(pf_payload "$ev" "$cmd")")"
                if [[ "$s" == 0 && "$n" == 0 ]]; then
                    ok "$name [$ev] no interpreter on a git call it does not gate [$cmd]"
                else
                    bad "$name [$ev] reached _pybin.sh ($s source, $n start) on a git call it does not gate [$cmd]: prefilter on the gate's own word, not \`git\`"
                fi
            done
        fi
    done
fi

# ---------------------------------------------------------------------------
# 4d. An escaped letter cannot hide a blocked command. JSON allows `\u0063` for `c`, and
#     the prefilters read the raw payload, so six of the ten blocking gates
#     (check-commit-subject-version.sh, validate-push.sh, block-no-verify.sh,
#     block-dangerous-git.sh, block-broad-git-add.sh, check-graphify-usage.sh) are each fed their blocked command
#     twice, plain and with the gated word's first letter escaped, and must answer both the
#     same way (maintainer decision D40; Codex's counter-payload in the plan 013 review).
#     The other four (check-claude-files-tracked.sh,
#     check-moduledata-validation.sh, check-native-dll-crt.sh, check-doc-config-drift.sh)
#     get 4c's escaped-word reach row only.
# ---------------------------------------------------------------------------
head2 "4d. a blocking gate answers the same when its word arrives escaped"
esc_verdict() {  # $1 hook, $2 project dir, $3 command already JSON-escaped: "rc=<n> <decision>"
    local out rc
    out=$(printf '{"tool_name":"Bash","tool_input":{"command":"%s"},"hook_event_name":"PreToolUse"}' "$3" \
          | timeout -k 2 30 env CLAUDE_PROJECT_DIR="$2" bash ".claude/hooks/$1" 2>/dev/null)
    rc=$?
    echo "rc=$rc $(decision_of "$out")"
}
for row in "check-commit-subject-version.sh|$REPO|rc=0 deny|cd /x\ngit commit -m \\\"no label here\\\"|cd /x\ngit ${PF_U}0063ommit -m \\\"no label here\\\"" \
           "validate-push.sh|$SANDBOX|rc=2 allow|git push --force origin master|git ${PF_U}0070ush --force origin master" \
           "block-no-verify.sh|$SANDBOX|rc=2 allow|git commit --no-verify -m x|git commit --${PF_U}006eo-verify -m x" \
           "block-dangerous-git.sh|$SANDBOX|rc=0 ask|cd /x\ngit reset --hard|cd /x\n${PF_U}0067it reset --hard" \
           "block-broad-git-add.sh|$SANDBOX|rc=0 ask|git add -A|${PF_U}0067it add -A" \
           "check-graphify-usage.sh|$SANDBOX|rc=0 deny|graphify update x|${PF_U}0067raphify update x"; do
    IFS='|' read -r hook dir want plain escaped <<< "$row"
    got_plain=$(esc_verdict "$hook" "$dir" "$plain")
    got_esc=$(esc_verdict "$hook" "$dir" "$escaped")
    if [[ "$got_plain" != "$want" ]]; then
        bad "$hook answered '$got_plain' to its plain blocked command [$plain], expected '$want': the row no longer proves anything"
    elif [[ "$got_esc" == "$want" ]]; then
        ok "$hook blocks [$escaped] as it blocks [$plain] ($want)"
    else
        bad "$hook answered '$got_esc' to [$escaped] but '$want' to [$plain]: an escaped letter got past the prefilter"
    fi
done

# ---------------------------------------------------------------------------
# 5. Starved environment: no jq, no python at all.
#    Every hook must still terminate promptly and must NOT block. This is the
#    fail-open mandate in .claude/rules/harness-facts.md, tested rather than assumed.
# ---------------------------------------------------------------------------
head2 "5. fail-open with no jq and no python on PATH"
# The never-fail-silent assertion below applies only to hooks that can actually BLOCK a
# Bash call: registered PreToolUse against a Bash-matching matcher, and carrying a deny or
# exit-2 path. Advisory hooks (notify-*, mark-verification-run) and hooks
# matched on other tools legitimately produce nothing for this payload, and demanding
# output from them would make the check noise rather than signal.
BLOCKING_BASH_GATES=$("$HPY" - <<'PYEOF'
import json, pathlib
d = json.load(open('.claude/settings.json', encoding='utf-8'))
out = []
for group in d.get('hooks', {}).get('PreToolUse', []):
    m = group.get('matcher', '')
    if m and 'Bash' not in m:
        continue
    for h in group.get('hooks', []):
        name = h['command'].rsplit('/', 1)[-1]
        p = pathlib.Path('.claude/hooks') / name
        if not p.exists():
            continue
        txt = p.read_text(encoding='utf-8', errors='replace')
        if '"deny"' in txt or 'exit 2' in txt:
            out.append(name)
print(' '.join(sorted(set(out))))
PYEOF
)
is_blocking_bash_gate() { [[ " $BLOCKING_BASH_GATES " == *" $1 "* ]]; }

# The runtime "did it say so" assertion needs an environment with NO jq and NO python.
# PATH=/usr/bin:/bin achieves that in Git Bash on the dev machine, and achieves NOTHING on
# ubuntu-latest, which keeps both in /usr/bin: the hooks resolved an interpreter, behaved
# correctly, stayed silent, and the assertion failed on a premise that was not true. (CI
# caught that on 2026-08-31, the first run after this suite got its own job and could
# execute at all.)
#
# Symlinking a private bin does not fix it either: an MSYS binary moved out of /usr/bin
# cannot resolve its shared libraries, so `bash` itself exits 127.
#
# So the contract is enforced STATICALLY below (check 5b), which is portable, and the
# runtime assertion runs only where the starvation demonstrably took.
STARVED_PATH="/usr/bin:/bin"
STARVATION_OK=1
PATH="$STARVED_PATH" command -v jq >/dev/null 2>&1 && STARVATION_OK=0
for p in python python3 py; do
    PATH="$STARVED_PATH" command -v "$p" >/dev/null 2>&1 && STARVATION_OK=0
done
[[ $STARVATION_OK -eq 1 ]] || echo "  note: this platform keeps jq or python in $STARVED_PATH, so the runtime silence assertion is SKIPPED (not passed); check 5b enforces the same contract statically"

for hookfile in .claude/hooks/*.sh .claude/skills/freeze/check-freeze.sh; do
    name=$(basename "$hookfile")
    [[ "$name" == _*.sh ]] && continue
    S=$(date +%s%N)
    ERRFILE=$(mktemp 2>/dev/null) || ERRFILE="$SANDBOX/stderr.$$"
    # The payload holds every gate's prefilter word (commit, push, no-verify, git, graphify), so
    # each blocking gate gets past its prefilter and must reach its taom_pybin_degraded branch.
    OUT=$(printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git push --force origin master && git commit --no-verify -m x && graphify update x"},"hook_event_name":"PreToolUse"}' \
          | timeout -k 2 10 env PATH="$STARVED_PATH" CLAUDE_PROJECT_DIR="$SANDBOX" TAOM_PYBIN= bash "$hookfile" 2>"$ERRFILE")
    RC=$?
    ERR=$(cat "$ERRFILE" 2>/dev/null); rm -f "$ERRFILE"
    E=$(date +%s%N); MS=$(( (E-S)/1000000 ))
    if [[ $RC -eq 124 || $RC -eq 137 ]]; then
        bad "$name HUNG with no interpreter available"
    elif [[ $RC -ne 0 && $RC -ne 2 ]]; then
        bad "$name exit $RC in a starved environment (must fail open, not error)"
    elif printf '%s' "$OUT" | grep -q '"permissionDecision":"deny"'; then
        bad "$name DENIED in a starved environment (a hook's own fault must never block)"
    elif [[ $STARVATION_OK -eq 1 ]] && is_blocking_bash_gate "$name" && [[ -z "$ERR" ]]; then
        # Fail open is only half the contract. hook-authoring.md: a detection hook must
        # fail open but NEVER fail silent, because for a gate no output IS a claim. On
        # 2026-08-31 nine of the ten PreToolUse gates allowed silently here, which is
        # indistinguishable from allowing because they looked and found nothing.
        bad "$name failed open SILENTLY (no stderr). A gate that cannot run must say so."
    else
        ok "$name failed open in ${MS}ms$( [[ -n "$ERR" ]] && printf ', and said so')"
    fi
done

# ---------------------------------------------------------------------------
# 5b. Every blocking Bash gate must CARRY the never-fail-silent branch.
#     Static, so it holds on every platform. The runtime check above can only run
#     where jq and python are both absent from /usr/bin:/bin, which is true on the
#     Windows dev machine and false on ubuntu-latest.
# ---------------------------------------------------------------------------
head2 "5b. every blocking Bash gate declares its degraded branch"
for name in $BLOCKING_BASH_GATES; do
    f=".claude/hooks/$name"
    [[ -f "$f" ]] || { bad "$name is registered but missing from .claude/hooks/"; continue; }
    if grep -q 'taom_pybin_degraded' "$f"; then
        ok "$name declares taom_pybin_degraded"
    else
        bad "$name has no taom_pybin_degraded branch: it will fail open SILENTLY when no JSON parser is available, which for a gate is indistinguishable from finding nothing"
    fi
done

# ---------------------------------------------------------------------------
# 5b2. The degraded-toolchain banner names every python-only gate. A gate with no jq
#      path dies whenever python is missing, and its exit-0 stderr never reaches Claude
#      (harness-facts.md), so session-start.sh's banner is the only signal. A hand-kept
#      list there once omitted check-commit-subject-version for ten days.
# ---------------------------------------------------------------------------
head2 "5b2. the degraded banner names every python-only gate"
# Only the call site's third argument marks a jq path (_pybin.sh), and only non-comment lines
# of session-start.sh print anything: a name in a comment there is not in the banner. Any
# non-comment call counts, whatever precedes it (`if`, `&&`), and a run that selects no gate
# fails, so the check cannot pass having checked nothing.
SS_CODE=$(grep -v '^[[:space:]]*#' .claude/hooks/session-start.sh)
n5b2=0
for f in .claude/hooks/*.sh; do
    name=$(basename "$f" .sh)
    call=$(grep -v '^[[:space:]]*#' "$f" | grep 'taom_pybin_degraded[[:space:]]') || continue
    grep -qE 'taom_pybin_degraded +"[^"]*" +"[^"]*" +jq( |$)' <<<"$call" && continue
    n5b2=$((n5b2+1))
    if grep -q -- "$name" <<<"$SS_CODE"; then
        ok "$name is named in the session-start degraded banner"
    else
        bad "$name has no jq path, so it fails open without python, but session-start.sh does not name it"
    fi
done
(( n5b2 > 0 )) || bad "5b2 found no python-only gate call site, so it checked nothing"

# ---------------------------------------------------------------------------
# 5c. Every PreToolUse gate prints its decision where Claude Code reads it.
#     Static, because most deny and ask paths need staged files or the game install
#     to reach at runtime. Claude Code ignores a top-level {"permissionDecision": ...}:
#     nine gates printed exactly that until #647, and each one passed a text match on
#     "permissionDecision":"deny" while the harness let every command through.
# ---------------------------------------------------------------------------
head2 "5c. every PreToolUse gate nests its decision in hookSpecificOutput"
for name in $PRE_GATES; do
    f=".claude/hooks/$name"
    [[ "$name" == check-freeze.sh ]] && f=".claude/skills/freeze/check-freeze.sh"
    [[ -f "$f" ]] || continue
    hit=$(grep -nF -e '{"permissionDecision' -e '{\"permissionDecision' "$f" | head -1 | cut -d: -f1)
    if [[ -n "$hit" ]]; then
        bad "$name:$hit prints a top-level permissionDecision, which Claude Code ignores; nest it under hookSpecificOutput with hookEventName PreToolUse"
    else
        ok "$name prints no top-level decision"
    fi
done

# ---------------------------------------------------------------------------
# 5c2. The confirm gates still confirm a multi-line command. A Claude Bash call often spans
#      lines, and a reason built by hand-escaping only quotes and backslashes put a raw
#      newline inside a JSON string: invalid JSON, which the harness treats as allow
#      (#647 convergence review).
# ---------------------------------------------------------------------------
head2 "5c2. the ask gates emit valid JSON for a multi-line command"
for pair in 'block-broad-git-add.sh|git add -A\necho hi' \
            'block-broad-git-add.sh|git add -A\techo hi' \
            'block-dangerous-git.sh|git reset --hard\necho hi' \
            'block-dangerous-git.sh|git checkout -- a.txt\n\tgit status'; do
    hook="${pair%%|*}"; cmd="${pair#*|}"
    OUT=$(printf '{"tool_name":"Bash","tool_input":{"command":"%s"},"hook_event_name":"PreToolUse"}' "$cmd" \
          | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash ".claude/hooks/$hook" 2>/dev/null)
    got=$(decision_of "$OUT")
    if [[ "$got" == ask ]]; then
        ok "$hook asks for [$cmd]"
    else
        bad "$hook returned '$got' for [$cmd]; a confirm gate must emit a valid ask: $(printf '%s' "$OUT" | head -c 120)"
    fi
done

# ---------------------------------------------------------------------------
# 5c3. /freeze judges a path the same however the JSON encodes it. It read file_path with a
#      grep that kept JSON escapes, so an in-bound file written as 漢 compared as out of
#      bounds (Codex review 2026-09-23). Its own project dir, so no other section sees a boundary.
# ---------------------------------------------------------------------------
head2 "5c3. check-freeze decodes the path before comparing it"
FZ="$SANDBOX/fz-proj"
mkdir -p "$FZ/.claude/hooks" "$FZ/.claude/tmp/freeze" "$FZ/漢/sub" "$FZ/other"
cp .claude/hooks/_pybin.sh "$FZ/.claude/hooks/" 2>/dev/null
FZ_NATIVE="$FZ"; command -v cygpath >/dev/null 2>&1 && FZ_NATIVE=$(cygpath -m "$FZ")
printf '%s\n' "$FZ_NATIVE/漢" > "$FZ/.claude/tmp/freeze/freeze-dir.txt"
for case in "allow|escaped|$FZ_NATIVE/漢/sub/a.cs" "allow|raw|$FZ_NATIVE/漢/sub/a.cs" "deny|escaped|$FZ_NATIVE/other/b.cs"; do
    expect="${case%%|*}"; rest="${case#*|}"; enc="${rest%%|*}"; path="${rest#*|}"
    payload=$("$HPY" -X utf8 -c 'import json,sys; print(json.dumps({"tool_name":"Edit","tool_input":{"file_path":sys.argv[1]},"hook_event_name":"PreToolUse"}, ensure_ascii=(sys.argv[2]=="escaped")))' "$path" "$enc")
    OUT=$(printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$FZ" bash .claude/skills/freeze/check-freeze.sh 2>/dev/null)
    got=$(decision_of "$OUT")
    if [[ "$got" == "$expect" ]]; then
        ok "freeze [$enc] $expect for ${path#"$FZ_NATIVE"/}"
    else
        bad "freeze [$enc] expected $expect, got $got for ${path#"$FZ_NATIVE"/}: $(printf '%s' "$OUT" | head -c 120)"
    fi
done
rm -rf "$FZ"

# ---------------------------------------------------------------------------
# 5d. The drift gate fires for every file the context budget measures.
#     The linter reads the entry docs from CLAUDE.md's @-imports; the hook's RELEVANT
#     case list is written by hand. A new import the list misses would let a commit grow
#     that file past the budget with the local gate silent (#647 review).
# ---------------------------------------------------------------------------
head2 "5d. check-doc-config-drift covers every entry doc"
DRIFT_HOOK=".claude/hooks/check-doc-config-drift.sh"
RELEVANT_PATTERNS=$(grep -E '^[[:space:]]+[^#[:space:]].*\) RELEVANT=1' "$DRIFT_HOOK" \
    | sed -E 's/^[[:space:]]+//; s/\) RELEVANT=1.*//')
ENTRY_DOC_LIST=$("$HPY" tools/lint_docs.py --context-budget-json 2>/dev/null \
    | "$HPY" -c 'import json,sys; print("\n".join(d["path"] for d in json.load(sys.stdin)["entry_docs"]))' 2>/dev/null \
    | tr -d '\r')
if [[ -z "$ENTRY_DOC_LIST" || -z "$RELEVANT_PATTERNS" ]]; then
    bad "could not read the entry docs from lint_docs.py or the RELEVANT list from $DRIFT_HOOK"
else
    while IFS= read -r doc; do
        matched=0
        while IFS= read -r pat; do
            IFS='|' read -r -a alts <<< "$pat"
            for alt in "${alts[@]}"; do
                # shellcheck disable=SC2053  # $alt is a glob on purpose, as in the hook's case
                [[ "$doc" == $alt ]] && matched=1
            done
        done <<< "$RELEVANT_PATTERNS"
        if [[ $matched -eq 1 ]]; then
            ok "entry doc $doc is in the drift hook's RELEVANT list"
        else
            bad "entry doc $doc (loaded by CLAUDE.md) is missing from $DRIFT_HOOK's RELEVANT list"
        fi
    done <<< "$ENTRY_DOC_LIST"
fi

# ---------------------------------------------------------------------------
head2 "6. check-commit-subject-version: subject cases against the real SubModule.xml"
# The gate reads <Version value="..."> from Main/_Module/SubModule.xml, so it runs here
# against the repo, not the sandbox, and the expected label is read the same way the hook
# reads it. A case is "label|expect|command"; expect is allow or deny.
CSV_HOOK=".claude/hooks/check-commit-subject-version.sh"
CSV_VER=$(grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml 2>/dev/null | head -1 | sed 's/.*"\(.*\)"/\1/')
if [[ ! -f "$CSV_HOOK" ]]; then
    bad "$CSV_HOOK missing"
elif [[ -z "$CSV_VER" ]]; then
    bad "could not read <Version> from Main/_Module/SubModule.xml for the subject cases"
else
    CSV_MSGFILE="$SANDBOX/subject-ok.txt"; printf 'docs: %s - from a file\n\nbody\n' "$CSV_VER" > "$CSV_MSGFILE"
    CSV_BADFILE="$SANDBOX/subject-bad.txt"; printf 'docs: from a file without the label\n' > "$CSV_BADFILE"
    CSV_AIFILE="$SANDBOX/subject-ai.txt"; printf 'docs: %s - from a file\n\nbody\n\nCo-Authored-By: Claude <noreply@anthropic.com>\n' "$CSV_VER" > "$CSV_AIFILE"
    CSV_CASES=(
      "labelled -m|allow|git commit -m \"fix(recruitment): $CSV_VER - Glanhir recruits the Ringlo Vale line\""
      "labelled -m, no scope|allow|git commit -m 'docs: $CSV_VER - update the changelog'"
      "labelled --message=|allow|git commit --message=\"chore: $CSV_VER - tidy\""
      "no label|deny|git commit -m \"fix(recruitment): Glanhir recruits the Ringlo Vale line\""
      "wrong version|deny|git commit -m \"fix: v9.9.9 - Glanhir recruits the Ringlo Vale line\""
      "label without the dash|deny|git commit -m \"fix: $CSV_VER Glanhir recruits\""
      "heredoc labelled|allow|git commit -m \"\$(cat <<'EOF'
feat(gondor): $CSV_VER - three harbor ships

Body line.
EOF
)\""
      "heredoc unlabelled|deny|git commit -m \"\$(cat <<'EOF'
feat(gondor): three harbor ships

Body line.
EOF
)\""
      "heredoc with an AI co-author trailer|deny|git commit -m \"\$(cat <<'EOF'
feat(gondor): $CSV_VER - three harbor ships

Body line.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
)\""
      "heredoc with a human co-author|allow|git commit -m \"\$(cat <<'EOF'
feat(gondor): $CSV_VER - three harbor ships

Body line.

Co-Authored-By: Jane Doe <jane@example.com>
EOF
)\""
      "AI co-author in a second -m|deny|git commit -m \"docs: $CSV_VER - x\" -m \"Co-Authored-By: Claude <noreply@anthropic.com>\""
      "Generated-with line in a second -m|deny|git commit -m \"docs: $CSV_VER - x\" -m \"Generated with [Claude Code](https://claude.com/claude-code)\""
      "Generated-with mid-sentence|allow|git commit -m \"docs: $CSV_VER - x\" -m \"The old file was not Generated with Claude Code.\""
      "AI co-author via --trailer|deny|git commit -m \"docs: $CSV_VER - x\" --trailer \"Co-authored-by: Claude Opus 5.5 <noreply@anthropic.com>\""
      "AI co-author via --trailer token=value|deny|git commit -m \"docs: $CSV_VER - x\" --trailer \"Co-authored-by=Claude <noreply@anthropic.com>\""
      "human co-author via --trailer|allow|git commit -m \"docs: $CSV_VER - x\" --trailer \"Co-authored-by: Jane Doe <jane@example.com>\""
      "AI co-author in ANSI-C quoting|deny|git commit -m \"docs: $CSV_VER - x\" -m \$'Co-Authored-By: Claude <noreply@anthropic.com>'"
      "AI co-author in an ANSI-C --trailer|deny|git commit -m \"docs: $CSV_VER - x\" --trailer=\$'Co-Authored-By: Claude <noreply@anthropic.com>'"
      "AI co-author in an attached -m|deny|git commit -m \"docs: $CSV_VER - x\" -m\"Co-Authored-By: Claude <noreply@anthropic.com>\""
      "AI trailer file in an attached -F|deny|git commit -F$CSV_AIFILE"
      "AI co-author from concatenated words|deny|git commit -m \"docs: $CSV_VER - x\" -m \"Co-Authored-By: Cla\"ude\" <noreply@anthropic.com>\""
      "option-shaped prose in a message|allow|git commit -m 'fix: $CSV_VER - review probe' -m 'Check --trailer=\"Co-Authored-By: Claude <noreply@anthropic.com>\" handling.'"
      "a literal backslash-n in single quotes|allow|git commit -m 'docs: $CSV_VER - x' -m 'mention \\nCo-Authored-By: Claude <noreply@anthropic.com>'"
      "trailer text in a later command|allow|git commit -m \"docs: $CSV_VER - x\" && printf '%s' '--trailer=Co-Authored-By: Claude'"
      "unlabelled, then python -c|deny|git commit -m \"docs: no label\" && python -c \"print(1)\""
      "unlabelled, -C only in the body|deny|git commit -m \"docs: no label\" -m \"use git -C path\""
      "second commit in the command unlabelled|deny|git commit -m \"docs: $CSV_VER - a\" && git commit --allow-empty -m \"docs: no label\""
      "git commit only inside a quoted argument|allow|printf '%s' 'git commit -m test'"
      "bash -c wrapper unlabelled|deny|bash -c \"git commit -m 'docs: no label'\""
      "bundled -am unlabelled|deny|git commit -am \"docs: no label\""
      "bundled -am labelled|allow|git commit -am \"docs: $CSV_VER - x\""
      "stdin heredoc with an apostrophe, labelled|allow|git commit -F - <<'EOF'
feat(gondor): $CSV_VER - Mike's harbor ships

Body line.
EOF"
      "stdin heredoc unlabelled|deny|git commit -F - <<'EOF'
feat(gondor): Mike's harbor ships
EOF"
      "reused message with an AI trailer|deny|git commit -C HEAD --trailer \"Co-Authored-By: Claude <noreply@anthropic.com>\""
      "fixup with an AI trailer|deny|git commit --fixup=abc1234 --trailer \"Co-Authored-By: Claude <noreply@anthropic.com>\""
      "-F file with an AI co-author trailer|deny|git commit -F $CSV_AIFILE"
      "-F file labelled|allow|git commit -F $CSV_MSGFILE"
      "-F file unlabelled|deny|git commit -F $CSV_BADFILE"
      "amend keeps HEAD subject|allow|git commit --amend --no-edit"
      "fixup|allow|git commit --fixup=abc1234"
      "git -C form|allow|git -C $REPO commit -m \"test: $CSV_VER - harness case\""
      "git -C form unlabelled|deny|git -C $REPO commit -m \"test: harness case\""
      "git in capitals|deny|GIT commit -m \"docs: no label\""
      "one word piped to -F -, unlabelled|deny|echo \"docs: no label\" | git commit -F -"
      "one word piped to -F -, labelled|allow|printf '%s\n' \"docs: $CSV_VER - x\" | git commit -F -"
      # Deep review of plan 027: only a producer whose output is known is read as the message.
      # A command's name, a variable or a printf format that changes the text stays unread, as
      # before plan 027 (they were denied with the invented subject 'pbpaste', '\$MSG' or 'x').
      "piped command|allow|pbpaste | git commit -F -"
      "piped executable|allow|./message-generator | git commit -F -"
      "piped variable|allow|echo \"\$MSG\" | git commit -F -"
      "printf format that labels the subject|allow|printf 'docs: $CSV_VER - %s\n' x | git commit -F -"
      "git in capitals after an assignment|deny|GIT_TRACE=0 GIT commit -m \"docs: no label\""
      "commit-tree is not a commit|allow|git commit-tree HEAD^{tree} -m \"x\""
      "not git|allow|echo hi"
    )
    for entry in "${CSV_CASES[@]}"; do
        label="${entry%%|*}"; rest="${entry#*|}"; expect="${rest%%|*}"; cmd="${rest#*|}"
        payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":sys.argv[1]},"hook_event_name":"PreToolUse"}))' "$cmd")
        OUT=$(printf '%s' "$payload" | timeout -k 2 12 env CLAUDE_PROJECT_DIR="$REPO" bash "$CSV_HOOK" 2>/dev/null)
        RC=$?
        if [[ $RC -ne 0 ]]; then
            bad "subject case [$label] exit $RC"
            continue
        fi
        got=$(decision_of "$OUT")
        if [[ "$got" == "$expect" ]]; then
            ok "subject case [$label] -> $got"
        else
            bad "subject case [$label] expected $expect, got $got: $(printf '%s' "$OUT" | head -c 160)"
        fi
    done
    # Non-ASCII in the command (#647). Claude Code sends raw UTF-8, and Python's stdio on
    # Windows defaults to the ANSI code page, so the JSON extraction failed on a character such
    # as an arrow and every gate let the command through unchecked. Both payload encodings, and
    # a labelled subject that must still pass.
    for enc in raw escaped; do
        for pair in "deny|git commit -m \"docs: no label → probe\"" \
                    "allow|git commit -m \"docs: $CSV_VER - Khamûl → Dol Guldur\""; do
            expect="${pair%%|*}"; cmd="${pair#*|}"
            payload=$("$HPY" -X utf8 -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":sys.argv[1]},"hook_event_name":"PreToolUse"}, ensure_ascii=(sys.argv[2]=="escaped")))' "$cmd" "$enc")
            OUT=$(printf '%s' "$payload" | timeout -k 2 12 env CLAUDE_PROJECT_DIR="$REPO" bash "$CSV_HOOK" 2>/dev/null)
            got=$(decision_of "$OUT")
            if [[ "$got" == "$expect" ]]; then
                ok "subject case [non-ASCII, $enc payload] -> $got"
            else
                bad "subject case [non-ASCII, $enc payload] expected $expect, got $got: $(printf '%s' "$OUT" | head -c 160)"
            fi
        done
    done
fi

# ---------------------------------------------------------------------------
head2 "7. check-deep-review: the Stop reminder mutes only after a logged deep-reviewer run"
# The mute greps a line log-agent.sh writes, and the two hooks share nothing else. Until
# 2026-09-18 the mute matched lens names the writer never logs, so it never fired. Drive the
# real writer and the real reader together, both directions: a rename or a format change on
# either side must fail here instead of silently re-arming or permanently muting the reminder.
CDR_REPO="$SANDBOX/cdr-repo"
mkdir -p "$CDR_REPO/.claude/logs" "$CDR_REPO/Main"
git -C "$CDR_REPO" init -q 2>/dev/null
printf 'class Foo {}\n' > "$CDR_REPO/Main/Foo.cs"
cdr_log() {
    printf '{"agent_type":"%s","agent_id":"t"}' "$1" \
        | CLAUDE_PROJECT_DIR="$CDR_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/log-agent.sh" >/dev/null 2>&1
}
cdr_reminds() {
    rm -f "$CDR_REPO/.claude/logs/.deep-review-reminded"
    printf '%s' '{"hook_event_name":"Stop","session_id":"t","stop_hook_active":false}' \
        | CLAUDE_PROJECT_DIR="$CDR_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-deep-review.sh" 2>/dev/null \
        | grep -q '"decision":"block".*/deep-review'
}
cdr_log Explore
if cdr_reminds; then
    ok "reminds on a dirty .cs when no deep-reviewer run is logged"
else
    bad "check-deep-review.sh stayed silent on a dirty .cs with no deep-reviewer run logged"
fi
cdr_log deep-reviewer
if cdr_reminds; then
    bad "check-deep-review.sh still reminds after log-agent.sh logged a deep-reviewer run: the writer's line format and the mute's pattern no longer agree"
else
    ok "a deep-reviewer run logged by log-agent.sh mutes the reminder"
fi

# ---------------------------------------------------------------------------
head2 "7a. Stop reminders reach Claude: one JSON block per streak, never bare stderr"
# Claude Code sends a Stop hook's exit-0 stderr and its plain stdout to the debug log only. The
# four reminders wrote there until plan 011 and none ever arrived. The Stop channel TAOM uses is
# {"decision":"block","reason":...} on stdout (hooks docs, "Stop decision control").
STOP_HOOKS=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(' '.join(sorted({h['command'].rsplit('/', 1)[-1]
                       for g in d.get('hooks', {}).get('Stop', [])
                       for h in g.get('hooks', [])})))
PY
)
[[ -z "$STOP_HOOKS" ]] && bad "no Stop registrations found in settings.json; the 7a discovery is broken"
for name in $STOP_HOOKS; do
    f=".claude/hooks/$name"
    [[ -f "$f" ]] || { bad "$name is registered on Stop but missing from .claude/hooks/"; continue; }
    hit=$(grep -n '>&2' "$f" | grep -vE '^[0-9]+:[[:space:]]*#' | head -1 | cut -d: -f1)
    if [[ -n "$hit" ]]; then
        bad "$name:$hit writes to stderr, which Claude never sees from a Stop hook; use taom_stop_block"
    else
        ok "$name writes nothing to stderr"
    fi
done

# The helper: JSON-escapes the reason, and the loop guard reads only the real key.
STOP_SAMPLE='quote " and backslash \ end'
HELPER_OUT=$(bash -c 'source .claude/hooks/_stop_reminder.sh && taom_stop_block "$1"' _ "$STOP_SAMPLE" 2>/dev/null)
if printf '%s' "$HELPER_OUT" | "$HPY" -c 'import json,sys; d=json.loads(sys.stdin.read()); sys.exit(0 if d.get("decision")=="block" and d.get("reason")==sys.argv[1] else 1)' "$STOP_SAMPLE" 2>/dev/null; then
    ok "taom_stop_block emits valid JSON and keeps quotes and backslashes"
else
    bad "taom_stop_block output is not a valid block with the reason intact: $(printf '%s' "$HELPER_OUT" | head -c 120)"
fi
# Through a variable: Git Bash drops a CR that $'\r' puts inside the text of a $( ).
STOP_CTRL=$'one\ntwo\tthree\rfour'
HELPER_OUT=$(source .claude/hooks/_stop_reminder.sh 2>/dev/null && taom_stop_block "$STOP_CTRL")
if printf '%s' "$HELPER_OUT" | "$HPY" -c 'import json,sys; d=json.loads(sys.stdin.read()); sys.exit(0 if d.get("reason")=="one two three four" else 1)' 2>/dev/null; then
    ok "taom_stop_block turns CR, LF and TAB into spaces and stays valid JSON"
else
    bad "taom_stop_block does not flatten CR, LF and TAB into valid JSON: $(printf '%s' "$HELPER_OUT" | head -c 120)"
fi
for pair in '0|{"stop_hook_active":true}' '0|{"stop_hook_active": true}' \
            '1|{"stop_hook_active":false}' \
            '1|{"last_assistant_message":"x \"stop_hook_active\":true","stop_hook_active":false}'; do
    want="${pair%%|*}"; js="${pair#*|}"
    bash -c 'source .claude/hooks/_stop_reminder.sh && taom_stop_hook_active "$1"' _ "$js" 2>/dev/null
    got=$?
    [[ "$got" == "$want" ]] && ok "taom_stop_hook_active $want for $js" || bad "taom_stop_hook_active returned $got, expected $want, for $js"
done

# Each hook against a sandbox repo that triggers all four: a tracked, modified .cs, an untagged
# version, an untouched CHANGELOG, no build marker, no logged review.
STOP_REPO="$SANDBOX/stop-repo"
mkdir -p "$STOP_REPO/.claude/logs" "$STOP_REPO/Main/_Module"
git -C "$STOP_REPO" init -q 2>/dev/null
printf 'class Foo {}\n' > "$STOP_REPO/Main/Foo.cs"
printf '<Module>\n  <Version value="v9.9.9" />\n</Module>\n' > "$STOP_REPO/Main/_Module/SubModule.xml"
printf '# CHANGELOG\n' > "$STOP_REPO/CHANGELOG.md"
git -C "$STOP_REPO" add Main CHANGELOG.md 2>/dev/null
git -C "$STOP_REPO" -c user.name=t -c user.email=t@example.invalid commit -qm init 2>/dev/null
printf 'class Foo { int x; }\n' > "$STOP_REPO/Main/Foo.cs"
stop_run() {
    printf '%s' "$2" | CLAUDE_PROJECT_DIR="$STOP_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/$1" 2>"$SANDBOX/stop.err"
    echo "$?" > "$SANDBOX/stop.rc"
}
stop_shape() {
    printf '%s' "$1" | "$HPY" -c '
import json, sys
raw = sys.stdin.read().strip()
if not raw:
    print("silent"); sys.exit()
try:
    d = json.loads(raw)
except Exception:
    print("invalid"); sys.exit()
ok = isinstance(d, dict) and d.get("decision") == "block" and isinstance(d.get("reason"), str) and d["reason"].strip()
print("block" if ok else "BADSHAPE")'
}
STOP_LIVE='{"hook_event_name":"Stop","session_id":"t","stop_hook_active":false}'
STOP_LOOP='{"hook_event_name":"Stop","session_id":"t","stop_hook_active":true}'
# One step: the hook's answer must have the expected shape, exit 0 and write no stderr. A shape
# check alone let a silent timeout or crash pass as "silent" (Codex review of plan 011).
stop_expect() {  # hook payload want label
    local got; got=$(stop_shape "$(stop_run "$1" "$2")")
    if [[ "$got" == "$3" && "$(cat "$SANDBOX/stop.rc" 2>/dev/null)" == 0 && ! -s "$SANDBOX/stop.err" ]]; then
        ok "$1 $4"
    else
        bad "$1: expected $3 ($4), got '$got', rc $(cat "$SANDBOX/stop.rc" 2>/dev/null), stderr: $(head -c 100 "$SANDBOX/stop.err" 2>/dev/null)"
    fi
}
stop_markers() { ls -A "$STOP_REPO/.claude/logs" 2>/dev/null | grep -c -- '-reminded$'; }
# Ends (clear) or restarts (set) each hook's streak the way a session would. The verification
# streak ends through the real writer, called as the PowerShell tool, so the writer's marker path
# and the reader's are proven to agree (a PowerShell build must mute the reminder: plan 011).
stop_condition() {  # hook clear|set
    local now; now=$(date +%s)
    case "$1:$2" in
        check-verification-evidence.sh:clear)
            touch -d "@$((now - 10))" "$STOP_REPO/Main/Foo.cs"
            "$HPY" -c 'import json; print(json.dumps({"tool_name":"PowerShell","tool_input":{"command":"dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="},"hook_event_name":"PostToolUse"}))' \
                | CLAUDE_PROJECT_DIR="$STOP_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/mark-verification-run.sh" >/dev/null 2>&1 ;;
        check-verification-evidence.sh:set) touch -d "@$((now + 3))" "$STOP_REPO/Main/Foo.cs" ;;
        check-deep-review.sh:clear)          git -C "$STOP_REPO" checkout -q -- Main/Foo.cs ;;
        check-deep-review.sh:set)            printf 'class Foo { int x; }\n' > "$STOP_REPO/Main/Foo.cs" ;;
        check-version-tagged.sh:clear)       git -C "$STOP_REPO" tag v9.9.9 ;;
        check-version-tagged.sh:set)         git -C "$STOP_REPO" tag -d v9.9.9 >/dev/null 2>&1 ;;
        *) return 2 ;;
    esac
    return 0
}
for name in $STOP_HOOKS; do
    # The shared triggering state: a modified Foo.cs, no build marker, CHANGELOG untouched, no tag,
    # no logged review, no streak marker.
    printf 'class Foo { int x; }\n' > "$STOP_REPO/Main/Foo.cs"
    git -C "$STOP_REPO" checkout -q -- CHANGELOG.md
    git -C "$STOP_REPO" tag -d v9.9.9 >/dev/null 2>&1
    rm -f "$STOP_REPO"/.claude/logs/.*-reminded "$STOP_REPO"/.claude/logs/.verification-ran "$STOP_REPO/.claude/logs/agent-audit.log"
    stop_condition "$name" set
    [[ $? == 2 ]] && { bad "7a has no clear and set steps for the Stop hook $name"; continue; }
    stop_expect "$name" "$STOP_LOOP" silent "is silent when stop_hook_active is true"
    [[ $(stop_markers) == 0 ]] && ok "$name writes no marker when stop_hook_active is true" \
        || bad "$name wrote a streak marker on a stop_hook_active Stop, which spends the reminder unseen"
    stop_expect "$name" "$STOP_LIVE" block "reminds through a JSON block"
    stop_expect "$name" "$STOP_LIVE" silent "mutes after one reminder"
    stop_condition "$name" clear
    stop_expect "$name" "$STOP_LIVE" silent "is silent once its condition clears"
    [[ $(stop_markers) == 0 ]] && ok "$name clears its streak marker once its condition clears" \
        || bad "$name left its streak marker after its condition cleared: the next streak stays muted"
    stop_condition "$name" set
    stop_expect "$name" "$STOP_LIVE" block "reminds again on the next streak"
    # Claude acts on the reminder in the continuation, so the streak ends before a Stop that carries
    # stop_hook_active; the next streak must still be reminded (Codex review of plan 011, P2).
    stop_condition "$name" clear
    stop_expect "$name" "$STOP_LOOP" silent "is silent on the continuation Stop"
    stop_condition "$name" set
    stop_expect "$name" "$STOP_LIVE" block "reminds on the next streak after one that ended in the continuation"
done
# A deep-reviewer run logged by the real writer ends the deep-review streak and clears its marker.
printf 'class Foo { int y; }\n' > "$STOP_REPO/Main/Foo.cs"
rm -f "$STOP_REPO"/.claude/logs/.*-reminded "$STOP_REPO/.claude/logs/agent-audit.log"
stop_expect check-deep-review.sh "$STOP_LIVE" block "reminds before a review is logged"
printf '{"agent_type":"deep-reviewer","agent_id":"t"}' \
    | CLAUDE_PROJECT_DIR="$STOP_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/log-agent.sh" >/dev/null 2>&1
stop_expect check-deep-review.sh "$STOP_LIVE" silent "is silent after log-agent.sh logs a deep-reviewer run"
[[ -f "$STOP_REPO/.claude/logs/.deep-review-reminded" ]] \
    && bad "check-deep-review.sh kept .deep-review-reminded after a logged review: the next streak stays muted" \
    || ok "check-deep-review.sh clears its streak marker when a review is logged"
rm -f "$STOP_REPO/.claude/logs/agent-audit.log"

# ---------------------------------------------------------------------------
head2 "7b. check-claude-files-tracked: denies with a valid decision, inside its registration"
# Until #647 it asked git two or three times per file, took 6 s against a 5 s registration,
# and the harness killed it on every commit. A sandbox repo with one untracked and one
# gitignored harness file must be denied, fast, and a clean tree allowed.
CFT_REPO="$SANDBOX/cft-repo"
mkdir -p "$CFT_REPO/.claude/skills/demo" "$CFT_REPO/.claude/hooks/bin"
git -C "$CFT_REPO" init -q 2>/dev/null
printf 'bin/\n' > "$CFT_REPO/.gitignore"
printf '# demo\n' > "$CFT_REPO/.claude/skills/demo/SKILL.md"
printf 'echo hi\n' > "$CFT_REPO/.claude/hooks/bin/check.sh"
printf 'x = 1\n' > "$CFT_REPO/.claude/hooks/_helper.py"
cft_run() {
    printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git commit -m x"},"hook_event_name":"PreToolUse"}' \
        | CLAUDE_PROJECT_DIR="$CFT_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-claude-files-tracked.sh" 2>/dev/null
}
S=$(date +%s%N); OUT=$(cft_run); MS=$(( ($(date +%s%N) - S) / 1000000 ))
if [[ "$(decision_of "$OUT")" == deny ]] && grep -q 'SKILL.md (untracked' <<< "$OUT" && grep -q 'check.sh (gitignored' <<< "$OUT" && grep -q '_helper.py (untracked' <<< "$OUT"; then
    ok "denies an untracked and a gitignored harness file, a .py helper included, in ${MS}ms"
else
    bad "check-claude-files-tracked did not deny both files with a valid decision: $(printf '%s' "$OUT" | head -c 160)"
fi
git -C "$CFT_REPO" add .claude/skills/demo/SKILL.md .claude/hooks/_helper.py 2>/dev/null
rm -rf "$CFT_REPO/.claude/hooks/bin"
OUT=$(cft_run)
if [[ "$(decision_of "$OUT")" == allow ]]; then
    ok "allows once the file is staged and the ignored one is gone"
else
    bad "check-claude-files-tracked still objects to a clean tree: $(printf '%s' "$OUT" | head -c 160)"
fi

# ---------------------------------------------------------------------------
head2 "7c. validate-push refuses a force push to either trunk, on any line, from either shell tool"
# The protected list named only master, main and bannerlord-1.4.5 while the release tags moved to
# bannerlord-1.5.x, and the hook was registered for Bash only (plan 011). It names exactly the two
# trunks, not bannerlord-* (maintainer decision D30), so a port branch stays force-pushable. It
# also read only the first line of the command, so a `cd` line before the push hid a force push
# (D38); a continued line (a trailing \ in bash, ` in PowerShell) is one command.
VP_CASES=(
  "2|git push --force origin bannerlord-1.5.x"
  "2|git push origin +bannerlord-1.5.x"
  "2|git push --force-with-lease origin bannerlord-1.5.x"
  "2|git push -f origin bannerlord-1.4.5"
  "2|git push --force origin main"
  "2|cd /x"$'\n'"git push --force origin bannerlord-1.5.x"
  "2|git status"$'\n'"git push origin feature"$'\n'"git push -f origin bannerlord-1.4.5"
  "2|git push --force \\"$'\n'"  origin bannerlord-1.5.x"
  "2|git push --force \`"$'\n'"  origin bannerlord-1.5.x"
  "0|git push origin bannerlord-1.5.x"
  "0|git push --force origin bannerlord-1.6.x"
  "0|git push --force origin bannerlord-1.5.0-port"
  "0|git push --force origin improve/011-stop-reminders-and-trunk-guard"
  "0|cd /x"$'\n'"git push --force origin improve/011-stop-reminders-and-trunk-guard"
  # Plan 011 review: anything after the push on its line became the "target", so a shell tail
  # let a trunk force push through; and only the last refspec was judged. Each command of a
  # line is now judged on its own, a redirection is never a refspec, and every refspec counts.
  "2|git push --force origin bannerlord-1.5.x 2>&1"
  "2|git push --force origin bannerlord-1.5.x 2>&1 | tail -5"
  "2|git push --force origin bannerlord-1.5.x && echo done"
  "2|git push --force origin bannerlord-1.5.x; git status"
  "2|git push --force origin bannerlord-1.5.x > /dev/null"
  "2|git push --force origin bannerlord-1.5.x | Out-Null"
  "2|git push --force origin bannerlord-1.5.x; if (\$?) { \"ok\" }"
  "2|git -C /e/repos/TAOM push -f origin bannerlord-1.4.5 2>/dev/null"
  "2|bash -c \"git push --force origin bannerlord-1.5.x; echo x\""
  "2|(git push --force origin bannerlord-1.5.x)"
  "2|git push -f origin bannerlord-1.4.5; git push origin feature"
  "2|git push --force origin bannerlord-1.5.x feature"
  "2|git push origin +bannerlord-1.5.x feature"
  "2|git push --force origin HEAD:bannerlord-1.5.x 2>&1"
  "2|git push origin \"bannerlord-1.5.x\" --force"
  "2|git push --force --all origin"
  "2|git push --mirror origin"
  "2|git push --force origin bannerlord-1.4.5 # note"
  "2|git push --force origin bannerlord-1.5.x"$'\n'"echo done"
  # Plan 011 convergence: a quote-blind split at ; & | cut a quoted -C, -c or -o value that
  # held one, separating git from push; and a refspec glued to its redirection was dropped.
  "2|git push --force -o \"ci.skip;x\" origin bannerlord-1.5.x"
  "2|git -C \"E:/R&D/TAOM\" push --force origin bannerlord-1.5.x"
  "2|git -c \"credential.helper=!f() { echo x; }; f\" push --force origin bannerlord-1.5.x"
  "2|git -C \"E:\\a;b\" push -f origin bannerlord-1.4.5"
  "2|git push --force origin bannerlord-1.5.x>/dev/null"
  "2|git push --force origin bannerlord-1.5.x>/dev/null 2>&1"
  "2|git push -f origin bannerlord-1.4.5>nul"
  "2|git push --force origin bannerlord-1.5.x>&2"
  # Plan 011 final convergence: an apostrophe in a comment or heredoc line opens a quote that
  # never closes, gluing the later lines into its segment, so the push is anchored on the first
  # `push` with a `git` before it, not on the first `push` word.
  "2|# don't push to the trunk"$'\n'"git -C \"E:/R&D/TAOM\" push --force origin bannerlord-1.5.x"
  "2|git commit -F - <<'EOF'"$'\n'"Don't push yet"$'\n'"EOF"$'\n'"git -C \"E:/R&D/TAOM\" push --force origin bannerlord-1.5.x"
  # Deliberately fail-safe: a gate cannot tell quoted or heredoc text from a command it runs
  # (bash -c "..." and bash <<EOF both run it), so a message that quotes a trunk force push is
  # refused. Write such a message with git commit -F <file>.
  "2|git commit -F - <<'EOF'"$'\n'"git push --force origin bannerlord-1.5.x"$'\n'"EOF"
  "0|git push --force origin feature 2>&1 | tail -5"
  "0|git push --force origin feature && git log --oneline bannerlord-1.5.x"
  "0|git push -f origin feature; git push origin bannerlord-1.5.x"
  "0|git push origin bannerlord-1.5.x 2>&1 | tail -3"
  "0|git push --all origin"
  "0|git push --force-with-lease=bannerlord-1.5.x:abc origin feature"
  "0|echo push"
  # Plan 027: a pattern or DWIM refspec, git by path or in capitals, a bash backtick
  # substitution, a trunk named only in a trailing comment, and a push option's value.
  "2|git push --force origin 'refs/heads/*'"
  "2|git push origin '+refs/heads/*:refs/heads/*'"
  "2|git push --force origin 'refs/heads/bannerlord-*'"
  "2|git push --force origin HEAD:heads/bannerlord-1.5.x"
  "2|git push --prune --force origin 'refs/heads/*:refs/heads/*'"
  "0|git push --force origin 'refs/heads/feature-*'"
  "0|git push --force origin refs/tags/v1"
  "2|GIT push --force origin bannerlord-1.5.x"
  "2|\"/c/Program Files/Git/cmd/git.exe\" push --force origin bannerlord-1.5.x"
  "2|x=\`git push --force origin bannerlord-1.5.x\`"
  "0|git push --force origin feature # bannerlord-1.5.x later"
  "0|git push --force -o bannerlord-1.5.x origin feature"
)
# The table above holds no escape, so PowerShell repeats one case to keep its path covered; the
# tool-tagged table below covers what PowerShell reads differently (its escape, quotes, braces and
# comments, through _shellwords.py), and the PowerShell registration is checked in 7e and live.
pre_payload() {  # $1 tool, $2 command: a PreToolUse payload as the harness sends it
    "$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$1" "$2"
}
vp_run() {  # $1 tool, $2 command; returns the hook's rc
    local payload
    payload=$(pre_payload "$1" "$2")
    printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/validate-push.sh >/dev/null 2>&1
}
for tool in Bash PowerShell; do
    for entry in "${VP_CASES[@]}"; do
        [[ "$tool" == PowerShell && "$entry" != "${VP_CASES[0]}" ]] && continue
        want="${entry%%|*}"; cmd="${entry#*|}"; shown="${cmd//$'\n'/\\n}"
        vp_run "$tool" "$cmd"; got=$?
        [[ "$got" == "$want" ]] && ok "validate-push [$tool] rc=$got for: $shown" \
            || bad "validate-push [$tool] expected rc=$want, got $got for: $shown"
    done
done
# The quote-aware split uses each shell's own escape (plan 011 final convergence; since plan 027
# _shellwords.py reads the tool_name): PowerShell keeps a backslash literal, so "a\" closes its
# quote and the & in the -C path stays quoted; Bash escapes the quote, so the value runs on to the
# next ". A hook that used one escape for both tools passed every row above.
VP_TOOL_CASES=(
  'PowerShell|2|git -c "user.name=a\" -C "E:/R&D" push --force origin bannerlord-1.5.x'
  'Bash|2|git -c "user.name=a\" b" -C "E:/R&D" push --force origin bannerlord-1.5.x'
  # Plan 027: PowerShell braces, the call operator, capitals, escapes and comments.
  'PowerShell|2|if ($true) {git push --force origin bannerlord-1.5.x}'
  'PowerShell|2|1..1 | ForEach-Object {git push --force origin bannerlord-1.5.x}'
  "PowerShell|2|& 'C:\\Program Files\\Git\\cmd\\git.exe' push --force origin bannerlord-1.5.x"
  'PowerShell|2|GIT push --force origin bannerlord-1.5.x'
  'PowerShell|2|git push --force origin bannerlord-1.5`.x'
  'PowerShell|2|$(git push --force origin bannerlord-1.5.x)'
  "PowerShell|2|git push --force origin 'refs/heads/*'"
  "PowerShell|2|git push \`"$'\n'"  --force origin bannerlord-1.5.x"
  'PowerShell|0|git push --force origin feature # bannerlord-1.5.x later'
  'PowerShell|2|git push --force origin bannerlord-1.4.5 # note'
  "PowerShell|0|git commit -m @'"$'\n'"Don't force push the trunk"$'\n'"'@; git push origin feature"
  'PowerShell|0|git push --force -o ci.skip origin feature'
  # Plan 027 review: refused before plan 027 and still refused. A # inside a quoted value after a
  # heredoc apostrophe must not hide the push, and PowerShell shapes the reader alone would miss
  # (a comma argument list, a parenthesised command) are judged on the raw command too.
  "Bash|2|echo \$'it\\'s'; echo \"a #b\"; git push --force origin bannerlord-1.5.x; echo done"
  "Bash|2|cat <<EOF"$'\n'"it's"$'\n'"EOF"$'\n'"echo \"a #b\"; git push --force origin bannerlord-1.5.x; echo done"
  "Bash|2|git commit -F - <<'EOF'"$'\n'"Don't stop"$'\n'"EOF"$'\n'"git log --grep \"fix #1\"; git push --force origin bannerlord-1.5.x; git log -1"
  "Bash|2|cat <<EOF"$'\n'"it's"$'\n'"EOF"$'\n'"git commit -m \"x"$'\n'"#1\"; git push --force origin bannerlord-1.5.x; echo"
  "PowerShell|2|Start-Process git -ArgumentList 'push','--force','origin','bannerlord-1.5.x' -Wait"
  "PowerShell|2|Start-Process git -ArgumentList 'push', '--force', 'origin', 'bannerlord-1.5.x'"
  "PowerShell|2|[Diagnostics.Process]::Start('git', 'push --force origin bannerlord-1.5.x')"
  "PowerShell|2|& (\"git\") push --force origin bannerlord-1.5.x"
  "PowerShell|2|& (Get-Command git) push --force origin bannerlord-1.5.x"
  # Deep review of plan 027: each was refused at 96afb6fb and passed at 05dbc0d4. A # after a quote
  # may be quoted text, so the quote-blind split keeps what follows it; PowerShell's typographic
  # quotes are quotes.
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"git -c \"user.name=a #b\" push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"Don't stop"$'\n'"EOF"$'\n'"git push --force -o 'ci #1' origin bannerlord-1.5.x"
  "Bash|2|echo \$'it\\'s'"$'\n'"git push --force -o 'ci #1' origin bannerlord-1.5.x"
  "Bash|0|git push --force origin \"feature\""
  # Convergence of plan 027: refused at 96afb6fb, passed after the review fixes. The blind split cuts
  # inside the quoted value, so the piece holding the push starts at the # and its opening quote
  # sits in the piece before; a comment is now dropped only when no quote comes anywhere before it.
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"X=\"a;b #c\" git push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"env \"X=a;b #c\" git push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"X=\"l1"$'\n'"#2\" git push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"X=\"a|#c\" git push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"say \"hi"$'\n'"EOF"$'\n'"X=\"a&#c\" git push --force origin bannerlord-1.5.x"
  "Bash|2|cat <<EOF"$'\n'"Don't stop"$'\n'"EOF"$'\n'"X='a;b #c' git push --force origin bannerlord-1.5.x"
  "Bash|2|echo \$'it\\'s'"$'\n'"X='a;b #c' git push --force origin bannerlord-1.5.x"
  "Bash|2|git commit -m \"x\"; git push --force origin feature # bannerlord-1.5.x later"
)
for entry in "${VP_TOOL_CASES[@]}"; do
    tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"
    shown="${cmd//$'\n'/\\n}"
    vp_run "$tool" "$cmd"; got=$?
    [[ "$got" == "$want" ]] && ok "validate-push [$tool] rc=$got for: $shown" \
        || bad "validate-push [$tool] expected rc=$want, got $got for: $shown"
done
# PowerShell's typographic quotes (deep review of plan 027): read as ASCII text, the ` #` inside
# them was taken for a comment and the push was dropped. Each shape is sent twice: with the literal
# U+2018 and U+2019 characters, and with the JSON escapes \u2018 and \u2019 (convergence).
for cmd in 'if (‘a #'\'' -ne '\''x’) { git push --force origin bannerlord-1.5.x }' \
           'git push --force -o ‘ci #1’ origin bannerlord-1.5.x' \
           'if (\u2018a #'\'' -ne '\''x\u2019) { git push --force origin bannerlord-1.5.x }' \
           'git push --force -o \u2018ci #1\u2019 origin bannerlord-1.5.x'; do
    printf '{"tool_name":"PowerShell","tool_input":{"command":"%s"},"hook_event_name":"PreToolUse"}' "$cmd" \
        | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/validate-push.sh >/dev/null 2>&1
    got=$?
    [[ "$got" == 2 ]] && ok "validate-push [PowerShell] rc=2 for: $cmd" \
        || bad "validate-push [PowerShell] expected rc=2, got $got for: $cmd"
done
# A push with no refspec pushes the checked-out branch, so a push option's value (-o ci.skip) must
# never be taken for the remote: run on a trunk, `git push --force -o ci.skip origin` passed
# (plan 027). The hook asks git for the branch in its own directory, so these run in scratch repos.
VP_TRUNK="$SANDBOX/vp-trunk"; VP_FEAT="$SANDBOX/vp-feature"
for pair in "$VP_TRUNK|bannerlord-1.5.x" "$VP_FEAT|feature"; do
    d="${pair%%|*}"; b="${pair#*|}"
    git init -q -b "$b" "$d" 2>/dev/null
    git -C "$d" -c user.name=t -c user.email=t@example.invalid commit -q --allow-empty -m init 2>/dev/null
done
vp_run_in() {  # $1 directory to run in, $2 tool, $3 command; returns the hook's rc
    local payload
    payload=$(pre_payload "$2" "$3")
    ( cd "$1" && printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$REPO/.claude/hooks/validate-push.sh" >/dev/null 2>&1 )
}
VP_BRANCH_CASES=(
  "$VP_TRUNK|2|git push --force -o ci.skip origin"
  "$VP_TRUNK|2|git push --force --push-option ci.skip origin"
  "$VP_TRUNK|2|git push --force origin"
  "$VP_TRUNK|0|git push -o ci.skip origin"
  "$VP_FEAT|0|git push --force -o ci.skip origin"
  # Plan 027 review: a trunk refspec in parentheses. The reader makes ( ) a statement break, so
  # its text alone holds a push with no refspec, which is the feature branch here.
  "$VP_FEAT|2|git push --force origin (\"bannerlord-1.5.x\")"
  "$VP_FEAT|2|git push --force origin \$(\"bannerlord-1.5.x\")"
  # Deep review of plan 027. Refused at 96afb6fb, passed at 05dbc0d4: a separator inside a quoted
  # path with the refspec in parentheses, and an empty option value that the quote flattening
  # dropped, so the skip took the remote and the refspec became the remote.
  "$VP_FEAT|2|git -C \"E:\\R&D\" push --force origin (\"bannerlord-1.5.x\")"
  "$VP_FEAT|2|git -C \"E:\\R;D\" push --force origin \$(\"bannerlord-1.5.x\")"
  "$VP_FEAT|2|git push --force -o \"\" origin HEAD:bannerlord-1.5.x"
  # Passed at 96afb6fb too: every way git hands the next word to an option as its value, and a
  # force flag anywhere in a short-option cluster.
  "$VP_TRUNK|2|git push -fo ci.skip origin"
  "$VP_TRUNK|2|git push --force -uo ci.skip origin"
  "$VP_TRUNK|0|git push -uo ci.skip origin"
  "$VP_FEAT|0|git push -fo ci.skip origin"
  "$VP_TRUNK|2|git push --force --push-opt ci.skip origin"
  "$VP_TRUNK|2|git push --force --recurse-submodules check origin"
  "$VP_TRUNK|2|git push --force --repo origin"
  "$VP_TRUNK|2|git push --force --receive-pack git-receive-pack origin"
  "$VP_TRUNK|2|git push --force --exec git-receive-pack origin"
  "$VP_TRUNK|2|git push --force -o \"ci variable\" origin"
  "$VP_TRUNK|0|git push --force --push-option=ci.skip origin feature"
  "$VP_FEAT|2|git push -vfu origin bannerlord-1.5.x"
)
for tool in Bash PowerShell; do
    for entry in "${VP_BRANCH_CASES[@]}"; do
        dir="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"
        vp_run_in "$dir" "$tool" "$cmd"; got=$?
        [[ "$got" == "$want" ]] && ok "validate-push [$tool] rc=$got on branch ${dir##*/} for: $cmd" \
            || bad "validate-push [$tool] expected rc=$want, got $got on branch ${dir##*/} for: $cmd"
    done
done
# Every segment is judged under both splits, and a push with no refspec asks git for the current
# branch: once per segment, 100 such lines took 7.9 s against the 5 s registration, and a killed
# gate fails open. The branch is now resolved once per run and a repeated segment judged once.
VP_LINES=""
for i in $(seq 1 100); do VP_LINES+="git -C /x/r$i push origin"$'\n'; done
S=$(date +%s%N); vp_run Bash "$VP_LINES"; got=$?; MS=$(( ($(date +%s%N) - S) / 1000000 ))
[[ "$got" == 0 && $MS -lt 4000 ]] && ok "validate-push judges 100 no-refspec push lines in ${MS}ms" \
    || bad "validate-push took ${MS}ms (rc=$got) on 100 no-refspec push lines; the limit is 4000ms"
VP_REG=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
def has(ev, hook, tool):
    return any(tool in g.get('matcher', '').split('|')
               and any(h['command'].endswith(hook) for h in g.get('hooks', []))
               for g in d.get('hooks', {}).get(ev, []))
# A command that exits non-zero raises PostToolUseFailure, not PostToolUse, so a failed build or
# test marks only while mark-verification-run.sh is registered on both events (plan 011 review).
need = [('PreToolUse', 'validate-push.sh'), ('PostToolUse', 'mark-verification-run.sh'),
        ('PostToolUseFailure', 'mark-verification-run.sh')]
gaps = [f'{hook} ({ev}, {tool})' for ev, hook in need for tool in ('Bash', 'PowerShell')
        if not has(ev, hook, tool)]
print('ok' if not gaps else 'missing: ' + '; '.join(gaps))
PY
)
[[ "$VP_REG" == ok ]] && ok "validate-push (PreToolUse) and mark-verification-run (PostToolUse, PostToolUseFailure) are registered for Bash and PowerShell" \
    || bad "settings.json registration $VP_REG"

# ---------------------------------------------------------------------------
head2 "7d. mark-verification-run marks the repo's own build and test commands, never a quoted mention"
# Its env-prefix strip read `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` as an
# assignment (a word, then `=`, then a space) and dropped `dotnet`, so the repo's canonical test
# command never marked; and it split on ; & | and newlines inside quotes, so a quoted mention
# marked (maintainer decision D41). A case is "1" for marked, "0" for not.
MVR_DIR="$SANDBOX/mvr"
MVR_CASES=(
  "1|dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="
  "1|dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId="
  "1|TEMP=E:/t TMP=E:/t dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="
  "1|cd \"E:/repos/x\" && dotnet test TAOM.Tests"
  "1|dotnet test TAOM.Tests --filter \"FullyQualifiedName~A|FullyQualifiedName~B\""
  "1|pwsh ./build.ps1 -RunTests"
  "1|echo start; dotnet build Main/TAOM.csproj"
  "1|env DOTNET_NOLOGO=1 dotnet test TAOM.Tests"
  "0|grep -rn \"dotnet test\" docs/"
  "0|grep -rn \"x; dotnet test\" docs/"
  "0|echo 'a | dotnet build'"
  "0|git commit -m \"first line"$'\n'"dotnet test TAOM.Tests\""
  "0|DOTNET_NOLOGO=1 echo dotnet test"
)
for tool in Bash PowerShell; do
    for entry in "${MVR_CASES[@]}"; do
        want="${entry%%|*}"; cmd="${entry#*|}"; shown="${cmd//$'\n'/\\n}"
        rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
        payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PostToolUse","tool_response":{"stdout":"ok"}}))' "$tool" "$cmd")
        printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
        got=0; [[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && got=1
        [[ "$got" == "$want" ]] && ok "mark-verification-run [$tool] marked=$got for: $shown" \
            || bad "mark-verification-run [$tool] expected marked=$want, got $got for: $shown"
    done
done
# Each shell's own escape (plan 011 review): PowerShell escapes with a backtick and keeps a
# backslash literal, so a path ending in \ closes its quote, and `" does not. Bash is the reverse.
# A command's non-final lines arrive with a CR, which must not hide a command on them.
MVR_TOOL_CASES=(
  'PowerShell|0|Write-Output "x`"; dotnet test"'
  'PowerShell|1|Set-Location "E:\repos\TAOM\"; dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId='
  'PowerShell|1|Push-Location E:\repos\TAOM\; dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId='
  'Bash|0|echo "a\"; dotnet test"'
  "Bash|1|cd E:/x"$'\n'"./build.ps1"$'\n'"echo done"
  "PowerShell|1|cd E:/x"$'\n'"./build.ps1"$'\n'"echo done"
  # Plan 027: the PowerShell reader splits a script block and drops a comment.
  'PowerShell|1|if ($true) { dotnet test TAOM.Tests }'
  "PowerShell|0|Write-Output @'"$'\n'"dotnet test TAOM.Tests"$'\n'"'@"
  'PowerShell|1|git status; dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= # built here'
  # The reader quotes a word holding \, so PowerShell's everyday .\build.ps1 arrives as '.\build.ps1'.
  'PowerShell|1|.\build.ps1 -RunTests'
  # Codex review of plan 027: a quoted path alone is a string PowerShell prints, not a run.
  "PowerShell|0|'.\\build.ps1'"
  "PowerShell|1|& '.\\build.ps1' -RunTests"
)
for entry in "${MVR_TOOL_CASES[@]}"; do
    tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"; shown="${cmd//$'\n'/\\n}"
    rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PostToolUse","tool_response":{"stdout":"ok"}}))' "$tool" "$cmd")
    printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
    got=0; [[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && got=1
    [[ "$got" == "$want" ]] && ok "mark-verification-run [$tool] marked=$got for: $shown" \
        || bad "mark-verification-run [$tool] expected marked=$want, got $got for: $shown"
done
# A 100 KB command still marks inside the 5 s registration: a per-character bash split took 9.5 s.
rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
S=$(date +%s%N)
"$HPY" -c 'import json; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"echo " + "x" * 100000 + "; dotnet test TAOM.Tests"},"hook_event_name":"PostToolUse"}))' \
    | timeout -k 1 5 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
MS=$(( ($(date +%s%N) - S) / 1000000 ))
[[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && ok "mark-verification-run marks a 100 KB command in ${MS}ms" \
    || bad "mark-verification-run did not mark a 100 KB command inside its 5 s registration (${MS}ms)"
# A failed test run arrives as PostToolUseFailure, with an `error` and no `tool_response`; it must
# mark too, or the Stop hook nags after every red run (7c checks the registration).
rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
printf '%s' '{"tool_name":"PowerShell","tool_input":{"command":"dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="},"hook_event_name":"PostToolUseFailure","error":"Exit code 1"}' \
    | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
[[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && ok "mark-verification-run marks a PostToolUseFailure payload" \
    || bad "mark-verification-run did not mark a PostToolUseFailure payload"
# PostToolUseFailure carries "is_interrupt": true when the tool call was aborted, so there is no
# result to count (plan 027; Claude Code 2.1.241 sets it from an abort error). A command that only
# mentions the field still marks: inside a JSON string its quotes are escaped.
for pair in '0|true' '1|false'; do
    want="${pair%%|*}"; irq="${pair#*|}"
    rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
    printf '{"tool_name":"PowerShell","tool_input":{"command":"dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="},"hook_event_name":"PostToolUseFailure","error":"Interrupted","is_interrupt":%s}' "$irq" \
        | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
    got=0; [[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && got=1
    [[ "$got" == "$want" ]] && ok "mark-verification-run marked=$got for a PostToolUseFailure with is_interrupt $irq" \
        || bad "mark-verification-run expected marked=$want, got $got for a PostToolUseFailure with is_interrupt $irq"
done
rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
"$HPY" -c 'import json; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"dotnet test TAOM.Tests; echo \"is_interrupt\":true"},"hook_event_name":"PostToolUse"}))' \
    | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
[[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && ok "mark-verification-run still marks a command that mentions is_interrupt" \
    || bad "mark-verification-run did not mark a command whose text mentions is_interrupt"
rm -rf "$MVR_DIR"

# ---------------------------------------------------------------------------
head2 "7e. the git gates read a PowerShell command as they read its Bash twin"
# Maintainer decision 61 (plan 027): eight PreToolUse gates were registered for the Bash tool only,
# so a git command run through the PowerShell tool skipped them, and every gate read its command as
# Bash text (a labelled PowerShell here-string commit read as the subject `@` and was denied). One
# `Bash|PowerShell` group now registers all nine, and each gate reads its command through _pybin.sh
# taom_hook_command, which runs _shellwords.py: PowerShell comes back as the Bash text of the same
# command, and a git named by a path or in capitals comes back as `git` in both shells.
BT='`'; NL=$'\n'; V=${CSV_VER:-v0.0.0}
# The nine gates are named here, never read from the settings under test: a list derived from the
# Bash registrations lost a gate that moved to a PowerShell-only group, and its parity row with it
# (Codex review of plan 027). A tenth Bash gate fails until it is added here. `own` lists shell
# hooks that are not git gates and read their command themselves: check-graphify-usage.sh splits
# both shells in tools/graphify_taom.py, and 7f checks it. A git gate never goes in `own`; it reads
# through taom_hook_command (hook-authoring.md).
G7E_GATES=$("$HPY" - <<'PY' | tr -d '\r'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
pre = d.get('hooks', {}).get('PreToolUse', [])
names = ["block-broad-git-add.sh", "block-dangerous-git.sh", "block-no-verify.sh",
         "check-claude-files-tracked.sh", "check-commit-subject-version.sh",
         "check-doc-config-drift.sh", "check-moduledata-validation.sh", "check-native-dll-crt.sh",
         "validate-push.sh"]
own = ["check-graphify-usage.sh"]
extra = sorted({h['command'].rsplit('/', 1)[-1] for g in pre
                if {'Bash', 'PowerShell'} & set(g.get('matcher', '').split('|'))
                for h in g.get('hooks', [])} - set(names) - set(own))
for n in extra:
    print(n, "unlisted", "unlisted")
for n in names:
    tools = [t for g in pre if any(h['command'].endswith('/' + n) for h in g.get('hooks', []))
             for t in g.get('matcher', '').split('|')]
    print(n, tools.count('Bash'), tools.count('PowerShell'))
PY
)
[[ -z "$G7E_GATES" ]] && bad "7e found no PreToolUse hook registered for Bash; the discovery is broken"
G7E_NAMES=""
while read -r name nb np; do
    [[ -z "$name" ]] && continue
    if [[ "$nb" == unlisted ]]; then
        bad "$name is a PreToolUse hook for a shell tool that 7e does not list; add it to the list above"
        continue
    fi
    G7E_NAMES+="$name "
    if [[ "$nb" == 1 && "$np" == 1 ]]; then
        ok "$name is registered once for Bash and once for PowerShell"
    else
        bad "$name is registered for Bash ${nb}x and for PowerShell ${np}x; a git gate needs one Bash|PowerShell registration"
    fi
    # hook-authoring.md: a gate reads its command through the shared reader, so both shells reach it.
    if grep -q 'taom_hook_command posix' ".claude/hooks/$name"; then
        ok "$name reads its command through taom_hook_command posix"
    else
        bad "$name does not read its command through taom_hook_command posix (hook-authoring.md)"
    fi
done <<< "$G7E_GATES"

# The prefilter reads the raw payload whatever the tool: a PowerShell call without the gate's word
# starts no Python, and one holding it reaches the parse.
for name in $G7E_NAMES; do
    read -r s n <<< "$(pf_run "$name" "$(pf_payload PreToolUse 'Get-ChildItem docs | Select-Object -First 3' PowerShell)")"
    if [[ "$s" == 0 && "$n" == 0 ]]; then
        ok "$name [PowerShell] no interpreter on a non-trigger payload"
    else
        bad "$name [PowerShell] reached _pybin.sh ($s source, $n start) on Get-ChildItem docs"
    fi
    case "$name" in
        validate-push.sh)   trig='git push origin x' ;;
        block-no-verify.sh) trig='git commit --no-verify -m x' ;;
        block-dangerous-git.sh | block-broad-git-add.sh) trig='git status' ;;
        *)                  trig="git commit -m @'\\nx\\n'@" ;;
    esac
    read -r s n <<< "$(pf_run "$name" "$(pf_payload PreToolUse "$trig" PowerShell)")"
    if [[ "$s" -ge 1 ]]; then
        ok "$name [PowerShell] reaches _pybin.sh on [$trig]"
    else
        bad "$name [PowerShell] never reached _pybin.sh on [$trig]"
    fi
done

g7e_verdict() {  # $1 hook, $2 tool, $3 project dir, $4 command: "rc=<n> <decision>"
    local payload out rc
    payload=$(pre_payload "$2" "$4")
    out=$(printf '%s' "$payload" | timeout -k 2 30 env CLAUDE_PROJECT_DIR="$3" bash "$REPO/.claude/hooks/$1" 2>/dev/null)
    rc=$?
    echo "rc=$rc $(decision_of "$out")"
}
G7E_OKFILE=${CSV_MSGFILE:-}; G7E_BADFILE=${CSV_BADFILE:-}
if command -v cygpath >/dev/null 2>&1; then
    G7E_OKFILE=$(cygpath -w "$G7E_OKFILE"); G7E_BADFILE=$(cygpath -w "$G7E_BADFILE")
fi
# hook|tool|project (S sandbox, R repo)|expected "rc=<n> <decision>"|command
G7E_ROWS=(
  # block-no-verify.sh blocks with exit 2 and prints no decision
  "block-no-verify.sh|PowerShell|S|rc=2 allow|git commit --no-verify -m \"x\""
  "block-no-verify.sh|PowerShell|S|rc=2 allow|GIT commit --no-verify -m x"
  "block-no-verify.sh|PowerShell|S|rc=2 allow|& 'C:\\Program Files\\Git\\cmd\\git.exe' push --no-verify origin feature"
  # A known gap, pinned (see Out of scope): the raw prefilter never sees the text no-verify here.
  "block-no-verify.sh|PowerShell|S|rc=0 allow|git commit --no-verif''y -m x"
  "block-no-verify.sh|PowerShell|S|rc=2 allow|git status; git commit ${BT}${NL}  --no-verify -m x"
  "block-no-verify.sh|PowerShell|S|rc=0 allow|npm publish --no-verify"
  "block-no-verify.sh|Bash|S|rc=2 allow|GIT commit --no-verify -m x"
  "block-no-verify.sh|Bash|S|rc=2 allow|git commit --no-verify -m x"
  # block-dangerous-git.sh confirms (ask) a command that can destroy work
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|& git reset --hard HEAD~1"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|GIT clean -fd"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|& 'C:\\Program Files\\Git\\cmd\\git.exe' stash drop"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|if (\$true) { git stash clear }"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|1..1 | ForEach-Object {git checkout -- .}"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch; git reset --hard origin/feature"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch && git reset --hard origin/feature"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch || git clean -f"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git reset ${BT}${NL}  --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git -C \"E:\\repos\\x\" reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|git restore --staged a.txt"
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|Write-Output \"git reset --hard\""
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|git commit -m \"docs: say why git reset --hard is gated\""
  "block-dangerous-git.sh|Bash|S|rc=0 ask|GIT reset --hard"
  "block-dangerous-git.sh|Bash|S|rc=0 ask|\"/c/Program Files/Git/cmd/git.exe\" clean -fd"
  # block-broad-git-add.sh confirms (ask) a command that stages everything
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git add -A"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|& git add --all"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|GIT add ."
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git status; git add -u"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git commit -am \"docs: x\""
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|if (\$true) {git add -A}"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git add ${BT}${NL}  -A"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -a -m 'x'"
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git add a.txt b.txt"
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git commit -m \"fix: add -a flag\""
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git commit -m @'${NL}fix: add -A handling${NL}'@"
  "block-broad-git-add.sh|Bash|S|rc=0 ask|GIT add -A"
  # check-commit-subject-version.sh denies an unlabelled subject or an AI attribution line
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m @'${NL}feat(hooks): $V - here-string subject${NL}${NL}Body line.${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m @'${NL}feat(hooks): here-string subject${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m @'${NL}feat(hooks): $V - x${NL}${NL}Co-Authored-By: Claude <noreply@anthropic.com>${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m @\"${NL}feat(hooks): $V - expandable here-string${NL}\"@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m \"docs: $V - x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m 'docs: $V - Mike''s harbor ships'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m \"docs: $V - x\" -m \"Body${BT}n${BT}nCo-Authored-By: Claude <noreply@anthropic.com>\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|GIT commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git add a.txt; git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git add a.txt && git commit -m \"docs: $V - x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit ${BT}${NL}  -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|if (\$true) { git commit -m \"docs: no label\" }"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|@'${NL}docs: no label${NL}'@ | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|@'${NL}docs: $V - piped here-string${NL}'@ | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -F '$G7E_OKFILE'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -F '$G7E_BADFILE'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m \"docs: $V - x\" # a trailing comment"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|Write-Output 'git commit -m \"docs: no label\"'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit-tree HEAD^{tree} -m \"x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit --amend --no-edit"
  # Deep review of plan 027: PowerShell runs the command on the right of an assignment and after the
  # . operator; ${name} is one variable, not a script block; a word that opens with a quote ends at
  # its close; a string or variable alone is a value, so only a literal is read as piped text.
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|\$r = git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|. git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git -C \${env:USERPROFILE}\\repo commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|'docs: no label' | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|Write-Output 'docs: no label' | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|Write-Output 'docs: $V - piped' | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|Get-Clipboard | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|\$msg | git commit -F -"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|\$null = git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git -C \${env:USERPROFILE}\\repo reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git reset 'HEAD'--hard"
  "block-dangerous-git.sh|Bash|S|rc=0 ask|GIT_TRACE=0 GIT reset --hard"
  "block-dangerous-git.sh|Bash|S|rc=0 ask|'git' reset --hard"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|\$out = git add -A"
  # Convergence of plan 027: ParseInput reads each as an assignment that runs git.
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|\$r =git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|\$x, \$y = git commit -m \"docs: no label\""
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|\$null =git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|[int] \$x = git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|\$a.b=git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|\$a[0]=git reset --hard"
)
for entry in "${G7E_ROWS[@]}"; do
    hook="${entry%%|*}"; rest="${entry#*|}"
    tool="${rest%%|*}"; rest="${rest#*|}"
    dirkey="${rest%%|*}"; rest="${rest#*|}"
    want="${rest%%|*}"; cmd="${rest#*|}"
    dir=$SANDBOX; [[ "$dirkey" == R ]] && dir=$REPO
    got=$(g7e_verdict "$hook" "$tool" "$dir" "$cmd")
    shown="${cmd//$'\n'/\\n}"
    if [[ "$got" == "$want" ]]; then
        ok "$hook [$tool] $got for: $shown"
    else
        bad "$hook [$tool] expected '$want', got '$got' for: $shown"
    fi
done

# The four gates that judge what is staged only need to know a commit is coming. Each reaches its
# `cd` to the project only past its two-stage commit test, so a bash -x trace shows the answer.
g7e_reaches() {  # $1 hook, $2 tool, $3 command: 1 when the gate got past its commit test
    local payload
    payload=$(pre_payload "$2" "$3")
    printf '%s' "$payload" | timeout -k 2 60 env PS4='+ ' CLAUDE_PROJECT_DIR="$SANDBOX" bash -x "$REPO/.claude/hooks/$1" >/dev/null 2>"$SANDBOX/g7e.trace"
    if grep -qE '^\+ cd ' "$SANDBOX/g7e.trace"; then echo 1; else echo 0; fi
}
G7E_REACH=(
  "PowerShell|1|GIT commit -m x"
  "PowerShell|1|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -m x"
  "PowerShell|1|git commit -m @'${NL}docs: x${NL}'@"
  "PowerShell|1|if (\$true) { git -C \"E:\\repos\\x\" commit --amend }"
  "PowerShell|0|git commit-tree HEAD -m x"
  "PowerShell|0|git log --grep commit"
  "Bash|1|GIT commit -m x"
)
for hook in check-claude-files-tracked.sh check-moduledata-validation.sh check-native-dll-crt.sh check-doc-config-drift.sh; do
    for entry in "${G7E_REACH[@]}"; do
        tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"
        got=$(g7e_reaches "$hook" "$tool" "$cmd")
        shown="${cmd//$'\n'/\\n}"
        if [[ "$got" == "$want" ]]; then
            ok "$hook [$tool] commit test $got for: $shown"
        else
            bad "$hook [$tool] commit test expected $want, got $got for: $shown"
        fi
    done
done
G7E_CFT="$SANDBOX/g7e-cft"
mkdir -p "$G7E_CFT/.claude/skills/demo"
git -C "$G7E_CFT" init -q 2>/dev/null
printf '# demo\n' > "$G7E_CFT/.claude/skills/demo/SKILL.md"
got=$(g7e_verdict check-claude-files-tracked.sh PowerShell "$G7E_CFT" "git commit -m @'${NL}docs: x${NL}'@")
if [[ "$got" == "rc=0 deny" ]]; then
    ok "check-claude-files-tracked denies a PowerShell here-string commit over an untracked skill"
else
    bad "check-claude-files-tracked answered '$got' to a PowerShell commit over an untracked skill; expected 'rc=0 deny'"
fi

# The reader's failure fallback (_pybin.sh taom_hook_command): a gate that cannot run _shellwords.py
# reads the raw command as Bash text and says so on stderr (deep review of plan 027).
NOREADER="$SANDBOX/noreader"
mkdir -p "$NOREADER"
cp .claude/hooks/*.sh "$NOREADER/"
out=$(pre_payload Bash "git reset --hard" \
    | timeout -k 2 30 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$NOREADER/block-dangerous-git.sh" 2>"$SANDBOX/noreader.err")
if [[ "$(decision_of "$out")" == ask ]] && grep -q '_shellwords.py failed' "$SANDBOX/noreader.err"; then
    ok "block-dangerous-git without _shellwords.py still asks, and says the reader failed"
else
    bad "block-dangerous-git without _shellwords.py answered '$(decision_of "$out")' (expected ask plus the stderr note)"
fi
( cd "$VP_TRUNK" && pre_payload Bash "git push --force origin" \
    | timeout -k 2 30 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$NOREADER/validate-push.sh" >/dev/null 2>&1 )
got=$?
[[ "$got" == 2 ]] && ok "validate-push without _shellwords.py still refuses a force push of the checked-out trunk" \
    || bad "validate-push without _shellwords.py answered rc=$got to a force push of the checked-out trunk (expected 2)"
rm -rf "$NOREADER"

# Large payloads under both tools stay inside 80% of each gate's registration (the plan 011 review
# saw a 5 s registration crossed under load, and a killed gate fails open).
# push-big puts `push` inside the long quoted segment, which the timing rows above never did
# (convergence of plan 027: validate-push re-split such a segment with a quadratic shlex).
for kind in ps-big ps-lines bash-big push-big; do
    "$HPY" - "$kind" > "$SANDBOX/g7e-$kind.json" <<'PY'
import json, sys
big = "x" * 100000
kind = sys.argv[1]
if kind == "ps-big":
    tool, cmd = "PowerShell", "Write-Output '" + big + "'; git status --no-verify; git push origin feature; git commit -m @'\ndocs: no label\n'@"
elif kind == "ps-lines":
    tool, cmd = "PowerShell", "\n".join('git -C E:\\x\\r%d commit -m "docs: no label"' % i for i in range(100))
elif kind == "push-big":
    tool, cmd = "Bash", "git commit -m \"" + "push the thing " * 7000 + "\" && git push --force origin feature"
else:
    tool, cmd = "Bash", "echo '" + big + "'; git status --no-verify; git push origin feature; git commit -m \"docs: no label\""
sys.stdout.write(json.dumps({"tool_name": tool, "tool_input": {"command": cmd}, "hook_event_name": "PreToolUse"}))
PY
done
for name in $G7E_NAMES; do
    REG=$("$HPY" - "$name" <<'PYEOF'
import json, sys
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(next((h.get('timeout', 600) for g in d['hooks'].get('PreToolUse', []) for h in g['hooks']
            if h['command'].endswith(sys.argv[1])), 0))
PYEOF
)
    REG=${REG%$'\r'}
    for kind in ps-big ps-lines bash-big push-big; do
        S=$(date +%s%N)
        timeout -k 2 65 env CLAUDE_PROJECT_DIR="$REPO" bash ".claude/hooks/$name" < "$SANDBOX/g7e-$kind.json" >/dev/null 2>&1
        MS=$(( ($(date +%s%N) - S) / 1000000 ))
        if (( MS * 10 >= REG * 1000 * 8 )); then
            bad "$name took ${MS}ms on the $kind payload against its ${REG}s registration: the harness kills it (silently) under load"
        else
            ok "$name ${MS}ms of ${REG}s on the $kind payload"
        fi
    done
done

# ---------------------------------------------------------------------------
head2 "7f. check-graphify-usage denies raw graphify writes and allows queries, from either shell tool"
# Every graphify write goes through tools/graphify_taom.py (#677). The judge's full case table is
# tools/tests/test_graphify_taom.py; these rows prove the hook wiring end to end: prefilter, the
# judge's path relative to the hook, the nested decision, and both shell tools.
GU_CASES=(
  "Bash|deny|graphify update E:/graphify/TAOM"
  "Bash|deny|cd /x"$'\n'"graphify extract E:/repos/TAOM --code-only"
  "Bash|deny|graphify claude install"
  "Bash|deny|bash -c \"graphify update x\""
  "Bash|allow|graphify affected \"IModLogger\" --depth 2 --graph g.json"
  "Bash|allow|python tools/graphify_taom.py refresh --if-stale"
  "Bash|allow|git commit -m \"docs: graphify update drops external nodes\""
  "PowerShell|deny|& \"C:\\Users\\mikew\\.local\\bin\\graphify.exe\" update x"
  "PowerShell|deny|graphify extract E:\\repos\\TAOM --code-only 2>&1 | Select-Object -Last 5"
  "PowerShell|allow|graphify god-nodes --top 15 --graph E:\\graphify\\TAOM\\graphify-out\\graph.json"
)
for entry in "${GU_CASES[@]}"; do
    tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"; shown="${cmd//$'\n'/\\n}"
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$tool" "$cmd")
    OUT=$(printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/check-graphify-usage.sh 2>/dev/null)
    got=$(decision_of "$OUT")
    [[ "$got" == "$want" ]] && ok "check-graphify-usage [$tool] $got for: $shown" \
        || bad "check-graphify-usage [$tool] expected $want, got $got for: $shown"
done
GU_REG=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
tools = [t for g in d.get('hooks', {}).get('PreToolUse', [])
         if any(h['command'].endswith('check-graphify-usage.sh') for h in g.get('hooks', []))
         for t in g.get('matcher', '').split('|')]
nb, np = tools.count('Bash'), tools.count('PowerShell')
print('ok' if (nb, np) == (1, 1) else f'Bash {nb}x, PowerShell {np}x')
PY
)
# Once per tool, as 7e requires of the git gates: a leftover PowerShell-only group beside the
# Bash|PowerShell one would run the gate twice on every PowerShell call.
[[ "$GU_REG" == ok ]] && ok "check-graphify-usage is registered once for Bash and once for PowerShell" \
    || bad "check-graphify-usage settings.json registration: $GU_REG, expected once each"

# ---------------------------------------------------------------------------
head2 "8. /context-budget scan.sh runs under set -u and measures the launch load"
# Nothing else runs this script, and it reads the budget from tools/lint_docs.py: an unbound
# variable or a broken JSON handshake would otherwise surface only when someone runs the skill.
for mode in "" "--verbose"; do
    SCAN_OUT=$(timeout -k 2 60 bash -u .claude/skills/context-budget/scan.sh $mode 2>&1)
    SCAN_RC=$?
    if [[ $SCAN_RC -ne 0 ]]; then
        bad "scan.sh ${mode:-(default)} exit $SCAN_RC: $(printf '%s' "$SCAN_OUT" | tail -2 | tr '\n' ' ')"
    elif ! grep -q 'Per custom-agent spawn' <<< "$SCAN_OUT"; then
        bad "scan.sh ${mode:-(default)} printed no per-spawn line"
    elif grep -q 'NOT measured' <<< "$SCAN_OUT"; then
        bad "scan.sh ${mode:-(default)} could not read the budget: $(grep 'NOT measured' <<< "$SCAN_OUT" | head -1)"
    else
        ok "scan.sh ${mode:-(default)} ran clean under set -u"
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
