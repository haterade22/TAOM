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
# delegates to _pybin.sh which does. Comments are ignored. (.github/workflows/ was exempt
# until the Linux CI workflow was removed, 2026-10-05.)
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
# 3b. Every skill/agent frontmatter must parse as YAML.
#     Four SKILL.md files shipped with an unquoted `argument-hint: [a] [b]`, which is
#     not valid YAML, so the WHOLE frontmatter was dropped and those skills lost their
#     eager description from the model's routing surface. Nothing detected it.
# ---------------------------------------------------------------------------
head2 "3b. skill/agent frontmatter parses as YAML"
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
for p in list(pathlib.Path('.claude/skills').glob('*/SKILL.md')) + list(pathlib.Path('.claude/agents').glob('*.md')):
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
# 4e. No here-string, and no expanding here-document, in any script a hook registration runs (#681).
#     Git Bash 5.3 on the desktop hangs forever writing a here-string or here-document of 65,537 to
#     65,664 bytes into its own pipe (a smaller one fits the 64 KiB pipe, a larger one goes to a temp
#     file), and a killed gate fails open. Split text with `set -f; IFS=$'\n'; A=($X); IFS=$' \t\n';
#     set +f` (no fork; drops empty lines) or word-split it under set -f; never `< <(printf ...)` on a
#     payload, which forks and reads a pipe a byte at a time (1 MB took 5.5 s). A quoted here-document
#     (<<'PY') is static text; its body must stay at or under 65,536 bytes. Full-line comments and
#     here-document bodies are skipped. The self-test file proves the scan still sees each form.
# ---------------------------------------------------------------------------
head2 "4e. no here-string or expanding here-document in a hook script (#681)"
HS_SELF="$SANDBOX/herestring-selftest.sh"
printf '%s\n' '# a comment naming <<< is not code' 'done <<< "$X"' '"$PY" - <<'"'"'PY'"'" \
    'print("<<< in a quoted body is not shell")' 'PY' 'cat <<EOF' '$X' 'EOF' > "$HS_SELF"
HS_OUT=$("$HPY" - "$HS_SELF" <<'PY'
import pathlib, re, sys
PIPE_MAX = 65536
HEREDOC = re.compile(r"(?<!<)<<(?!<)(-?)[ \t]*(\\?)(['\"]?)([A-Za-z_][A-Za-z0-9_]*)\3")
SCRIPT_REF = re.compile(r"\.claude/[^\s\"']+\.sh")
root = pathlib.Path('.')
found = set((root / '.claude' / 'hooks').glob('*.sh'))
for src in [root / '.claude' / 'settings.json'] + list((root / '.claude' / 'skills').glob('*/SKILL.md')):
    text = src.read_text(encoding='utf-8', errors='replace')
    if src.name == 'SKILL.md':
        text = text.split('\n---', 1)[0] if text.startswith('---') else ''
    found |= {root / m.group(0) for m in SCRIPT_REF.finditer(text) if (root / m.group(0)).is_file()}
def scan(path):
    out, pending, body = [], [], None
    for i, line in enumerate(path.read_text(encoding='utf-8', errors='replace').split('\n'), 1):
        if body:
            delim, strip, quoted, start, size = body
            if (line.lstrip('\t') if strip else line).rstrip('\r') == delim:
                if quoted and size > PIPE_MAX:
                    out.append(f'{path.as_posix()}:{start}: static here-document body of {size} bytes (over {PIPE_MAX})')
                body = pending.pop(0) if pending else None
            else:
                body = (delim, strip, quoted, start, size + len(line.encode('utf-8')) + 1)
            continue
        if line.lstrip().startswith('#'):
            continue
        if '<<<' in line:
            out.append(f'{path.as_posix()}:{i}: here-string: {line.strip()[:90]}')
        for m in HEREDOC.finditer(line):
            quoted = bool(m.group(2) or m.group(3))
            if not quoted:
                out.append(f'{path.as_posix()}:{i}: expanding here-document <<{m.group(4)}: {line.strip()[:90]}')
            pending.append((m.group(4), m.group(1) == '-', quoted, i, 0))
        if pending:
            body = pending.pop(0)
    if body:
        out.append(f'{path.as_posix()}:{body[3]}: here-document <<{body[0]} never closes')
    return out
self_hits = [h.split(': ', 1)[0].rsplit(':', 1)[1] for h in scan(pathlib.Path(sys.argv[1]))]
print('SELF ' + ' '.join(self_hits))
for p in sorted(found):
    for h in scan(p):
        print(h)
PY
)
HS_SELFLINE=$(printf '%s\n' "$HS_OUT" | grep '^SELF ')
HS_HITS=$(printf '%s\n' "$HS_OUT" | grep -v '^SELF ')
if [[ "$HS_SELFLINE" != "SELF 2 6" ]]; then
    bad "4e's scan no longer sees a here-string and an expanding here-document (self-test found: ${HS_SELFLINE#SELF })"
elif [[ -n "$HS_HITS" ]]; then
    bad "here-string or expanding here-document in a hook script (#681: Git Bash hangs on 65,537 to 65,664 bytes, and a killed gate fails open):"
    printf '%s\n' "$HS_HITS" | sed 's/^/       /'
else
    ok "no hook script feeds text through a here-string or an expanding here-document"
fi

# ---------------------------------------------------------------------------
# 5. Starved environment: no jq, no python at all.
#    Every hook must still terminate promptly and must NOT block. This is the
#    fail-open mandate in .claude/rules/harness-facts.md, tested rather than assumed.
# ---------------------------------------------------------------------------
head2 "5. fail-open with no jq and no python on PATH"
# The never-fail-silent assertion below applies only to hooks that can actually BLOCK a
# Bash call: registered PreToolUse against a Bash-matching matcher, and carrying a deny or
# exit-2 path. Advisory hooks (notify-*, suggest-compact, mark-verification-run) and hooks
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
    [[ "$name" == "_pybin.sh" ]] && continue
    S=$(date +%s%N)
    ERRFILE=$(mktemp 2>/dev/null) || ERRFILE="$SANDBOX/stderr.$$"
    OUT=$(printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git push --force origin master"},"hook_event_name":"PreToolUse"}' \
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
#      list there omitted check-commit-subject-version until this check was added.
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
    # Another worktree carries its own version: a commit run there through `git -C <dir>` or
    # `cd <dir> &&` is judged against that tree's SubModule.xml. It is a git work tree: the gate
    # resolves the commit's directory to its work tree's top level (_pybin.sh taom_git_repo_dir), and
    # a directory in no work tree falls back to the project.
    mkdir -p "$SANDBOX/otherwt/Main/_Module"
    git -C "$SANDBOX/otherwt" init -q 2>/dev/null
    printf '<Module>\n  <Version value="v1.2.3"/>\n</Module>\n' > "$SANDBOX/otherwt/Main/_Module/SubModule.xml"
    CSV_OTHER=$(cygpath -m "$SANDBOX/otherwt" 2>/dev/null || echo "$SANDBOX/otherwt")
    CSV_CASES=(
      "git -C another worktree, its label|allow|git -C \"$CSV_OTHER\" commit -m \"docs: v1.2.3 - x\""
      "git -C another worktree, this repo's label|deny|git -C \"$CSV_OTHER\" commit -m \"docs: $CSV_VER - x\""
      "cd another worktree then commit, its label|allow|cd \"$CSV_OTHER\" && git commit -m \"docs: v1.2.3 - x\""
      "cd another worktree then commit, this repo's label|deny|cd \"$CSV_OTHER\" && git commit -m \"docs: $CSV_VER - x\""
      "git -C a missing directory falls back to this repo|allow|git -C \"$CSV_OTHER/nope\" commit -m \"docs: $CSV_VER - x\""
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
cft_run() {
    printf '%s' '{"tool_name":"Bash","tool_input":{"command":"git commit -m x"},"hook_event_name":"PreToolUse"}' \
        | CLAUDE_PROJECT_DIR="$CFT_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-claude-files-tracked.sh" 2>/dev/null
}
S=$(date +%s%N); OUT=$(cft_run); MS=$(( ($(date +%s%N) - S) / 1000000 ))
if [[ "$(decision_of "$OUT")" == deny ]] && grep -q 'SKILL.md (untracked' <<< "$OUT" && grep -q 'check.sh (gitignored' <<< "$OUT"; then
    ok "denies an untracked and a gitignored harness file in ${MS}ms"
else
    bad "check-claude-files-tracked did not deny both files with a valid decision: $(printf '%s' "$OUT" | head -c 160)"
fi
git -C "$CFT_REPO" add .claude/skills/demo/SKILL.md 2>/dev/null
rm -rf "$CFT_REPO/.claude/hooks/bin"
OUT=$(cft_run)
if [[ "$(decision_of "$OUT")" == allow ]]; then
    ok "allows once the file is staged and the ignored one is gone"
else
    bad "check-claude-files-tracked still objects to a clean tree: $(printf '%s' "$OUT" | head -c 160)"
fi
# A commit in another worktree is checked in THAT repository (2026-10-16). The registration runs this
# script from the project root, which stays where the session started after it enters a worktree, so
# another session's untracked skill in the main checkout denied every worktree commit. The repository
# is the one `git -C` or an earlier `cd` names, then the payload's cwd, then the project root.
CFT_WT="$SANDBOX/cft-worktree"
mkdir -p "$CFT_WT" "$CFT_REPO/.claude/skills/other"
git -C "$CFT_WT" init -q 2>/dev/null
printf '# another session\n' > "$CFT_REPO/.claude/skills/other/SKILL.md"
CFT_WT_M=$(cygpath -m "$CFT_WT" 2>/dev/null || echo "$CFT_WT")
cft_payload() {   # $1 the command, $2 the payload cwd or ""; neither holds a quote or a backslash
    local cwd=""
    [[ -n "$2" ]] && cwd=",\"cwd\":\"$2\""
    printf '{"tool_name":"Bash","tool_input":{"command":"%s"}%s,"hook_event_name":"PreToolUse"}' "$1" "$cwd" \
        | CLAUDE_PROJECT_DIR="$CFT_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-claude-files-tracked.sh" 2>/dev/null
}
OUT=$(cft_payload "git commit -m x" "")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'other/SKILL.md (untracked' <<< "$OUT" \
    && ok "no cwd and no named directory: the project root is checked, as before" \
    || bad "a commit with no cwd did not check the project root: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(cft_payload "git commit -m x" "$CFT_WT_M")
[[ "$(decision_of "$OUT")" == allow ]] \
    && ok "the payload cwd (a clean worktree) is checked, not the project root" \
    || bad "a commit from a clean worktree cwd was judged on the project root: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(cft_payload "git -C $CFT_WT_M commit -m x" "")
[[ "$(decision_of "$OUT")" == allow ]] \
    && ok "git -C <clean worktree> commit is checked in that worktree" \
    || bad "git -C <worktree> commit was judged on the project root: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(cft_payload "cd $CFT_WT && git commit -m x" "")
[[ "$(decision_of "$OUT")" == allow ]] \
    && ok "cd <clean worktree> && git commit (a Git Bash path) is checked in that worktree" \
    || bad "cd <worktree> && git commit was judged on the project root: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(cft_payload "cd $CFT_WT_M/nowhere && git commit -m x" "")
[[ "$(decision_of "$OUT")" == deny ]] \
    && ok "a named directory that does not exist falls back to the project root" \
    || bad "a missing named directory skipped the check: $(printf '%s' "$OUT" | head -c 160)"
# A subdirectory is judged as its work tree's top level: every gate reads root-relative paths, so a
# subdirectory let all three pass in silence (deep review 2026-10-16, HIGH).
mkdir -p "$CFT_REPO/sub" "$CFT_WT/sub"
CFT_REPO_M=$(cygpath -m "$CFT_REPO" 2>/dev/null || echo "$CFT_REPO")
OUT=$(cft_payload "git commit -m x" "$CFT_REPO_M/sub")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'other/SKILL.md (untracked' <<< "$OUT" \
    && ok "a cwd in a subdirectory is judged at the repository's top level" \
    || bad "a commit from a subdirectory passed unchecked: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(cft_payload "cd sub && git commit -m x" "$CFT_REPO_M")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'other/SKILL.md (untracked' <<< "$OUT" \
    && ok "cd <subdirectory> && git commit is judged at the repository's top level" \
    || bad "cd <subdirectory> && git commit passed unchecked: $(printf '%s' "$OUT" | head -c 160)"
mkdir -p "$CFT_WT/.claude/skills/wt"; printf '# wt\n' > "$CFT_WT/.claude/skills/wt/SKILL.md"
OUT=$(cft_payload "git commit -m x" "$CFT_WT_M")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'wt/SKILL.md (untracked' <<< "$OUT" \
    && ok "an untracked skill in the worktree itself still denies its commit" \
    || bad "an untracked skill in the worktree did not deny: $(printf '%s' "$OUT" | head -c 160)"
# The resolver itself (_pybin.sh taom_git_repo_dir over _shellwords.py git-repo), which every git
# gate that reads the repository goes through. It answers a work tree's top level, in git's form.
top_of() { git -C "$1" rev-parse --show-toplevel 2>/dev/null; }
CFT_WT_TOP=$(top_of "$CFT_WT"); CFT_REPO_TOP=$(top_of "$CFT_REPO")
crd() {   # $1 the command, $2 the payload cwd or "", $3 the tool (Bash), $4 the git subcommand (commit)
    local payload
    payload=$("$HPY" -c '
import json, sys
d = {"tool_name": sys.argv[3], "tool_input": {"command": sys.argv[1]}, "hook_event_name": "PreToolUse"}
if sys.argv[2]:
    d["cwd"] = sys.argv[2]
print(json.dumps(d))' "$1" "$2" "${3:-Bash}")
    INPUT="$payload" CLAUDE_PROJECT_DIR="$CFT_REPO" \
        bash -c 'source "$1/.claude/hooks/_pybin.sh"; taom_git_repo_dir "$2"' _ "$REPO" "${4:-commit}" 2>/dev/null
}
crd_is() {   # $1 the expected directory, $2 the case's name, then crd's arguments
    local want="$1" name="$2" got
    shift 2
    got=$(crd "$@")
    [[ -n "$want" && "$got" == "$want" ]] && ok "taom_git_repo_dir: $name" \
        || bad "taom_git_repo_dir: $name gave '$got', not '$want'"
}
crd_is "$CFT_WT_TOP" "the payload cwd" "git commit -m x" "$CFT_WT_M"
crd_is "$CFT_WT_TOP" "a cwd in a subdirectory" "git commit -m x" "$CFT_WT_M/sub"
crd_is "$CFT_WT_TOP" "a backslashed Windows cwd" "git commit -m x" "$(cygpath -w "$CFT_WT" 2>/dev/null || echo "$CFT_WT")"
crd_is "$CFT_WT_TOP" "a cd into a subdirectory before the commit" "cd $CFT_WT/sub && git commit -m x" ""
crd_is "$CFT_WT_TOP" "git -C <subdirectory> commit" "git -C $CFT_WT_M/sub commit -m x" ""
crd_is "$CFT_REPO_TOP" "a -C on another git command does not count" "git -C $CFT_WT_M status && git commit -m x" "$CFT_REPO_M"
crd_is "$CFT_WT_TOP" "a cd after the commit does not count" "git commit -m x && cd /" "$CFT_WT_M"
crd_is "$CFT_WT_TOP" "a multi-line command" "cd $CFT_WT_M"$'\n'"git commit -m x" ""
crd_is "$CFT_WT_TOP" "a heredoc message with an apostrophe" "$(printf "cd %s && git commit -F - <<'EOF'\nfix: don't\nEOF" "$CFT_WT_M")" ""
crd_is "$CFT_WT_TOP" "a PowerShell Set-Location" "Set-Location $CFT_WT_M; git commit -m x" "" PowerShell
crd_is "$CFT_WT_TOP" "git add after a cd" "cd $CFT_WT_M && git add -A" "" Bash add
crd_is "$CFT_REPO_TOP" "the project root when nothing names a directory" "git commit -m x" ""
# Commits in more than one work tree: a gate judges one repository per command, so the resolver answers
# nothing (rc 2) and the commit gates deny (Codex review 2026-10-16: an earlier commit went unchecked
# behind a later one in another repository).
[[ -z "$(crd "git -C $CFT_WT_M commit -m a; git -C $CFT_REPO_M commit -m b" "")" ]] \
    && ok "taom_git_repo_dir: commits in two work trees answer nothing" \
    || bad "taom_git_repo_dir picked one repository for a command that commits in two"
crd_is "$CFT_WT_TOP" "two commits in one work tree are one repository" "git -C $CFT_WT_M commit -m a; git -C $CFT_WT_M/sub commit -m b" ""
crd_is "$CFT_WT_TOP" "pushd then popd returns to the cwd" "pushd $CFT_REPO_M; popd; git commit -m x" "$CFT_WT_M"
crd_is "$CFT_WT_TOP" "a cd inside ( ) closed by ');' ends there" "(cd $CFT_REPO_M && git status); git commit -m x" "$CFT_WT_M"
OUT=$(cft_payload "git -C $CFT_WT_M commit -m a; git -C $CFT_REPO_M commit -m b" "")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'more than one repository' <<< "$OUT" \
    && ok "check-claude-files-tracked denies a command that commits in two repositories" \
    || bad "check-claude-files-tracked judged one repository of a two-repository command: $(printf '%s' "$OUT" | head -c 160)"
for hook in check-claude-files-tracked.sh check-moduledata-validation.sh check-doc-config-drift.sh \
            check-commit-subject-version.sh block-broad-git-add.sh; do
    if grep -qF 'taom_git_repo_dir' "$REPO/.claude/hooks/$hook" \
        && ! grep -qF 'cd "${CLAUDE_PROJECT_DIR' "$REPO/.claude/hooks/$hook"; then
        ok "$hook judges the repository its git call runs in"
    else
        bad "$hook still judges \$CLAUDE_PROJECT_DIR, not the call's repository (taom_git_repo_dir)"
    fi
done
# A gate that runs the repository's tools/*.py runs them only in a work tree of this project
# (_pybin.sh taom_is_project_repo): a commit in another repository never runs its code.
PRJ="$SANDBOX/prj-main"; PRJ_WT="$SANDBOX/prj-wt"; PRJ_FOREIGN="$SANDBOX/prj-foreign"
mkdir -p "$PRJ" "$PRJ_FOREIGN"
git -C "$PRJ" init -q 2>/dev/null
git -C "$PRJ" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init 2>/dev/null
git -C "$PRJ" worktree add -q --detach "$PRJ_WT" 2>/dev/null
git -C "$PRJ_FOREIGN" init -q 2>/dev/null
pir() { CLAUDE_PROJECT_DIR="$PRJ" bash -c 'source "$1/.claude/hooks/_pybin.sh"; taom_is_project_repo "$2"' _ "$REPO" "$1" 2>/dev/null; }
pir "$PRJ_WT" && ok "taom_is_project_repo: a linked worktree of the project" \
    || bad "taom_is_project_repo refused a linked worktree of the project"
pir "$PRJ_FOREIGN" && bad "taom_is_project_repo took another repository for the project" \
    || ok "taom_is_project_repo: another repository is not the project"
# The doc-drift gate end to end, with a stub linter that always finds drift and leaves a marker:
# from a subdirectory of the project it must deny; in another repository it must neither deny nor
# run that repository's stub.
for d in "$PRJ_WT" "$PRJ_FOREIGN"; do
    mkdir -p "$d/tools" "$d/docs/features" "$d/sub"
    printf 'import os, sys\nopen(os.path.join(os.path.dirname(os.path.abspath(__file__)), "ran.txt"), "w").close()\nprint("## Config-example drift")\nprint("- stub drift")\nsys.exit(1)\n' > "$d/tools/lint_docs.py"
    printf '# a\n' > "$d/docs/features/a.md"
    git -C "$d" add docs/features/a.md 2>/dev/null
done
drift_run() {   # $1 the payload cwd
    "$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"git commit -m x"},"cwd":sys.argv[1],"hook_event_name":"PreToolUse"}))' "$1" \
        | CLAUDE_PROJECT_DIR="$PRJ" timeout -k 2 30 bash "$REPO/.claude/hooks/check-doc-config-drift.sh" 2>/dev/null
}
OUT=$(drift_run "$(cygpath -m "$PRJ_WT/sub" 2>/dev/null || echo "$PRJ_WT/sub")")
[[ "$(decision_of "$OUT")" == deny ]] && grep -q 'stub drift' <<< "$OUT" \
    && ok "check-doc-config-drift from a worktree subdirectory runs the top level's linter and denies" \
    || bad "check-doc-config-drift from a subdirectory did not deny: $(printf '%s' "$OUT" | head -c 160)"
OUT=$(drift_run "$(cygpath -m "$PRJ_FOREIGN" 2>/dev/null || echo "$PRJ_FOREIGN")")
if [[ "$(decision_of "$OUT")" == allow && ! -e "$PRJ_FOREIGN/tools/ran.txt" ]]; then
    ok "check-doc-config-drift leaves another repository alone and runs none of its code"
else
    bad "check-doc-config-drift judged another repository or ran its tools/lint_docs.py"
fi
# A commit staging only an RCA runs the linter (plan 136 D4, #984), the linter is asked to read the
# index, and the deny quotes the linter's RCA section. A staged non-RCA review doc runs nothing.
printf 'import os, sys\nopen(os.path.join(os.path.dirname(os.path.abspath(__file__)), "ran.txt"), "w").write(" ".join(sys.argv[1:]))\nprint("# Doc lint report")\nprint("## RCA FOLLOW-UP lines with no issue or reason")\nprint("- docs/reviews/rca-x-2026-10-23.md:1 [followup-no-owner]")\nsys.exit(1)\n' > "$PRJ_WT/tools/lint_docs.py"
rm -f "$PRJ_WT/tools/ran.txt"
git -C "$PRJ_WT" reset -q 2>/dev/null
mkdir -p "$PRJ_WT/docs/reviews"
printf '# notes\n' > "$PRJ_WT/docs/reviews/notes.md"
git -C "$PRJ_WT" add docs/reviews/notes.md 2>/dev/null
OUT=$(drift_run "$(cygpath -m "$PRJ_WT" 2>/dev/null || echo "$PRJ_WT")")
if [[ "$(decision_of "$OUT")" == allow && ! -e "$PRJ_WT/tools/ran.txt" ]]; then
    ok "check-doc-config-drift ignores a commit staging only a non-RCA review doc"
else
    bad "check-doc-config-drift ran for a staged docs/reviews/notes.md: $(printf '%s' "$OUT" | head -c 160)"
fi
git -C "$PRJ_WT" reset -q 2>/dev/null
printf '# rca\n' > "$PRJ_WT/docs/reviews/rca-x-2026-10-23.md"
git -C "$PRJ_WT" add docs/reviews/rca-x-2026-10-23.md 2>/dev/null
OUT=$(drift_run "$(cygpath -m "$PRJ_WT" 2>/dev/null || echo "$PRJ_WT")")
if [[ "$(decision_of "$OUT")" == deny && -e "$PRJ_WT/tools/ran.txt" ]] && grep -q 'followup-no-owner' <<< "$OUT"; then
    ok "check-doc-config-drift runs the linter for a staged RCA and quotes its section"
else
    bad "check-doc-config-drift did not deny a staged RCA with the linter's section: $(printf '%s' "$OUT" | head -c 200)"
fi
grep -q -- '--drift-only --staged' "$PRJ_WT/tools/ran.txt" 2>/dev/null \
    && ok "check-doc-config-drift asks the linter to read the index (--drift-only --staged)" \
    || bad "check-doc-config-drift did not pass --drift-only --staged: $(cat "$PRJ_WT/tools/ran.txt" 2>/dev/null)"
git -C "$PRJ_WT" reset -q 2>/dev/null
# The subject gate reads the <Version> of the repository the session works in, the payload's cwd.
SUBJ_WT="$SANDBOX/subj-wt"
mkdir -p "$SUBJ_WT/Main/_Module"
git -C "$SUBJ_WT" init -q 2>/dev/null
printf '<Module>\n  <Version value="v7.7.7"/>\n</Module>\n' > "$SUBJ_WT/Main/_Module/SubModule.xml"
SUBJ_WT_M=$(cygpath -m "$SUBJ_WT" 2>/dev/null || echo "$SUBJ_WT")
subj_run() {   # $1 the subject
    "$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"git commit -m \"" + sys.argv[1] + "\""},"cwd":sys.argv[2],"hook_event_name":"PreToolUse"}))' "$1" "$SUBJ_WT_M" \
        | CLAUDE_PROJECT_DIR="$REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-commit-subject-version.sh" 2>/dev/null
}
[[ "$(decision_of "$(subj_run "docs: v7.7.7 - x")")" == allow ]] \
    && ok "check-commit-subject-version: a worktree cwd, that tree's label allows" \
    || bad "check-commit-subject-version denied the worktree's own label"
[[ "$(decision_of "$(subj_run "docs: v0.0.1 - x")")" == deny ]] \
    && ok "check-commit-subject-version: a worktree cwd, another version's label denies" \
    || bad "check-commit-subject-version did not judge the worktree's version"
# The broad-staging confirm lists the files of the repository the git add runs in.
printf 'x\n' > "$CFT_WT/wtonly.txt"
OUT=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"git add -A"},"cwd":sys.argv[1],"hook_event_name":"PreToolUse"}))' "$CFT_WT_M" \
    | CLAUDE_PROJECT_DIR="$CFT_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/block-broad-git-add.sh" 2>/dev/null)
[[ "$(decision_of "$OUT")" == ask ]] && grep -q 'wtonly.txt' <<< "$OUT" \
    && ok "block-broad-git-add lists the worktree's files for a git add -A run there" \
    || bad "block-broad-git-add did not list the worktree's files: $(printf '%s' "$OUT" | head -c 160)"
OUT=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"git -C " + sys.argv[1] + " add -A; git -C " + sys.argv[2] + " add x"},"hook_event_name":"PreToolUse"}))' "$CFT_WT_M" "$CFT_REPO_M" \
    | CLAUDE_PROJECT_DIR="$CFT_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/block-broad-git-add.sh" 2>/dev/null)
[[ "$(decision_of "$OUT")" == ask ]] && grep -q 'more than one repository' <<< "$OUT" \
    && ok "block-broad-git-add says its list is incomplete when git add runs in two repositories" \
    || bad "block-broad-git-add listed one repository for a two-repository add: $(printf '%s' "$OUT" | head -c 160)"
git -C "$PRJ" worktree remove --force "$PRJ_WT" >/dev/null 2>&1
rm -rf "$CFT_REPO/.claude/skills/other" "$CFT_REPO/sub" "$CFT_WT" "$PRJ" "$PRJ_WT" "$PRJ_FOREIGN" "$SUBJ_WT"


# ---------------------------------------------------------------------------
head2 "7c. validate-push refuses a force push to either trunk, master or main"
for row in "2|git push --force origin bannerlord-1.4.5" "2|git push --force origin HEAD:bannerlord-1.5.x" \
           "2|git push -f origin master" "2|git push origin +bannerlord-1.5.x" \
           "0|git push origin port148/103-x" "0|git push --force origin port148/103-x"; do
    want="${row%%|*}"; cmd="${row#*|}"
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":sys.argv[1]},"hook_event_name":"PreToolUse"}))' "$cmd")
    rc=0
    printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/validate-push.sh >/dev/null 2>&1 || rc=$?
    if [[ "$rc" == "$want" ]]; then
        ok "validate-push rc=$rc for: $cmd"
    else
        bad "validate-push expected rc=$want, got rc=$rc for: $cmd"
    fi
done

# ---------------------------------------------------------------------------
# 7h. A payload in Git Bash's here-string hang window still gets its answer (#681).
#     Git Bash 5.3 hangs forever on a here-string or here-document of 65,537 to 65,664 bytes, and a
#     killed gate fails open. On 1.4.x each gate reads the raw command, so each row sizes its command
#     into that window directly. 4e keeps the construct out; these rows prove the gates still answer,
#     inside 80% of each registration.
# ---------------------------------------------------------------------------
head2 "7h. hooks answer inside 80% of their registration on a payload in the here-string hang window (#681)"
hw_payload() {  # $1 kind, $2 tool, $3 command length: writes $SANDBOX/hw.json
    "$HPY" - "$1" "$2" "$3" > "$SANDBOX/hw.json" <<'PY'
import json, sys
kind, tool, n = sys.argv[1], sys.argv[2], int(sys.argv[3])
head, tail = {"push": ('git -c x="', '" push --force origin bannerlord-1.4.5'),
              "reset": ('echo "', '"; git reset --hard'),
              "addall": ('echo "', '"; git add -A'),
              "test": ('echo "', '"; dotnet test TAOM.Tests')}[kind]
k = n - len(head) - len(tail)
cmd = head + ("abc def " * (k // 8 + 1))[:k] + tail
assert len(cmd) == n
event = "PostToolUse" if kind == "test" else "PreToolUse"
p = {"tool_name": tool, "tool_input": {"command": cmd}, "hook_event_name": event}
if event == "PostToolUse":
    p["tool_response"] = {"stdout": "ok", "stderr": ""}
sys.stdout.write(json.dumps(p))
PY
}
hw_reg() {  # $1 event, $2 hook: its registered timeout in seconds
    "$HPY" -c 'import json, sys
d = json.load(open(".claude/settings.json", encoding="utf-8"))
print(next((h.get("timeout", 600) for g in d["hooks"].get(sys.argv[1], []) for h in g["hooks"]
            if h["command"].endswith(sys.argv[2])), 0))' "$1" "$2" | tr -d '\r'
}
for row in "validate-push.sh|push|Bash|65536|rc2" "validate-push.sh|push|Bash|65600|rc2" \
           "validate-push.sh|push|Bash|65663|rc2" "block-dangerous-git.sh|reset|Bash|65548|ask" \
           "block-broad-git-add.sh|addall|Bash|65548|ask" "mark-verification-run.sh|test|Bash|65548|mark"; do
    IFS='|' read -r hook kind tool len want <<< "$row"
    event=PreToolUse; [[ $want == mark ]] && event=PostToolUse
    REG=$(hw_reg "$event" "$hook")
    hw_payload "$kind" "$tool" "$len"
    rm -rf "$SANDBOX/hw-proj"; mkdir -p "$SANDBOX/hw-proj"
    S=$(date +%s%N)
    out=$(timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX/hw-proj" bash ".claude/hooks/$hook" < "$SANDBOX/hw.json" 2>/dev/null)
    rc=$?
    MS=$(( ($(date +%s%N) - S) / 1000000 ))
    case $want in
        rc2)  got="rc=$rc"; [[ $rc == 2 ]] ;;
        ask)  got=$(decision_of "$out"); [[ $got == ask ]] ;;
        mark) got="rc=$rc, marker $([[ -f "$SANDBOX/hw-proj/.claude/logs/.verification-ran" ]] && echo set || echo unset)"
              [[ $got == *"marker set" ]] ;;
    esac
    answered=$?
    label="$hook [$tool] on a ${len}-character $kind command"
    if (( rc == 124 )); then
        bad "$label hung until the 10 s bound (a harness kill fails open): here-string hang window, #681"
    elif (( answered != 0 )); then
        bad "$label answered $got, expected $want"
    elif (( MS * 10 >= REG * 1000 * 8 )); then
        bad "$label took ${MS}ms against its ${REG}s registration"
    else
        ok "$label: $got in ${MS}ms of ${REG}s"
    fi
done
rm -rf "$SANDBOX/hw-proj" "$SANDBOX/hw.json"

# ---------------------------------------------------------------------------
head2 "7j. every hook registration is anchored on CLAUDE_PROJECT_DIR, so a gate still runs after a cd (#690)"
# Claude Code runs a hook command in the session's current directory, and a Bash `cd` into a project
# subdirectory persists across tool calls. A registration written as `.claude/hooks/x.sh` then fails
# to start ("No such file or directory"), which the harness treats as a non-blocking error, so every
# gate fell silent after `cd tools` (proven live 2026-09-26, #690). Each settings.json registration
# names its script as "$CLAUDE_PROJECT_DIR"/.claude/..., and each skill or agent frontmatter one (the
# surface section 5 says gets forgotten) names it through ${CLAUDE_PROJECT_DIR} or "$CLAUDE_PROJECT_DIR".
J7_UNANCHORED=$("$HPY" - <<'PY' | tr -d '\r'
import json, pathlib, re
d = json.load(open('.claude/settings.json', encoding='utf-8'))
for ev, gs in d.get('hooks', {}).items():
    for g in gs:
        for h in g.get('hooks', []):
            c = h.get('command', '')
            if not c.startswith('"$CLAUDE_PROJECT_DIR"/.claude/'):
                print('settings.json ' + ev + ' ' + c)
root = pathlib.Path('.')
for src in sorted((root / '.claude' / 'skills').glob('*/SKILL.md')) + sorted((root / '.claude' / 'agents').glob('*.md')):
    text = src.read_text(encoding='utf-8', errors='replace').replace('\r\n', '\n')
    fm = text[3:].split('\n---', 1)[0] if text.startswith('---') else ''
    for m in re.finditer(r'^\s*(?:-\s*)?command:\s*(.+?)\s*$', fm, re.M):
        if not re.search(r'"?\$\{?CLAUDE_PROJECT_DIR\}?"?/\.claude/', m.group(1)):
            print(src.as_posix() + ' ' + m.group(1))
PY
)
if [[ -z "$J7_UNANCHORED" ]]; then
    ok 'every hook registration (settings.json and skill or agent frontmatter) is anchored on CLAUDE_PROJECT_DIR'
else
    bad "hook registrations not anchored on \$CLAUDE_PROJECT_DIR (they stop running after a cd into a subdirectory): $(printf '%s' "$J7_UNANCHORED" | tr '\n' ';')"
fi
# End to end, the way the harness runs it: the registered command string through bash -c, from a
# subdirectory, with CLAUDE_PROJECT_DIR set. The gate must still refuse a trunk force push.
J7_CMD=$("$HPY" -c 'import json; d = json.load(open(".claude/settings.json", encoding="utf-8")); print(next(h["command"] for g in d["hooks"]["PreToolUse"] for h in g["hooks"] if h["command"].endswith("/validate-push.sh")))' | tr -d '\r')
J7_PAYLOAD=$("$HPY" -c 'import json; print(json.dumps({"tool_name": "Bash", "tool_input": {"command": "git push --" + "force origin HEAD:bannerlord-1.4.5"}, "hook_event_name": "PreToolUse"}))')
for j7_dir in tools .claude/hooks; do
    J7_RC=0
    (cd "$REPO/$j7_dir" && printf '%s' "$J7_PAYLOAD" | timeout -k 2 15 env CLAUDE_PROJECT_DIR="$REPO" bash -c "$J7_CMD" >/dev/null 2>&1) || J7_RC=$?
    if [[ "$J7_RC" == 2 ]]; then
        ok "validate-push, run as registered from $j7_dir/, refuses a trunk force push (rc 2)"
    else
        bad "validate-push, run as registered ($J7_CMD) from $j7_dir/, answered rc=$J7_RC to a trunk force push; expected 2 (rc 127: the relative path did not resolve, #690)"
    fi
done

# ---------------------------------------------------------------------------
head2 "7k. the git gates run for the Bash and PowerShell tools; Serena's editors are denied (D19, D3)"
K7=$("$HPY" - <<'PY' | tr -d '\r'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
gates = {"block-no-verify.sh", "validate-push.sh", "check-claude-files-tracked.sh",
         "check-commit-subject-version.sh", "block-dangerous-git.sh", "block-broad-git-add.sh",
         "check-moduledata-validation.sh", "check-native-dll-crt.sh", "check-doc-config-drift.sh"}
for g in d["hooks"]["PreToolUse"]:
    names = {h["command"].rsplit("/", 1)[-1] for h in g["hooks"]}
    if names & gates and g.get("matcher") != "Bash|PowerShell":
        print("matcher %r for %s" % (g.get("matcher"), ",".join(sorted(names & gates))))
    gates -= names
for n in sorted(gates):
    print("unregistered " + n)
deny = set(d.get("permissions", {}).get("deny", []))
for n in ["create_text_file", "replace_content", "replace_in_files", "delete_lines", "replace_lines",
          "insert_at_line", "replace_symbol_body", "insert_after_symbol", "insert_before_symbol",
          "rename_symbol", "safe_delete_symbol", "execute_shell_command", "jet_brains_move",
          "jet_brains_safe_delete", "jet_brains_rename", "jet_brains_inline_symbol"]:
    if "mcp__serena__" + n not in deny:
        print("not denied mcp__serena__" + n)
for n in ["find_symbol", "find_referencing_symbols", "get_symbols_overview", "read_memory", "write_memory"]:
    if "mcp__serena__" + n in deny:
        print("read or memory tool denied mcp__serena__" + n)
if d.get("attribution", {}).get("commit") != "" or d.get("attribution", {}).get("pr") != "":
    print("attribution.commit and attribution.pr are not both empty")
PY
)
if [[ -z "$K7" ]]; then
    ok "git-gate group matches Bash|PowerShell; 16 Serena editors denied, read and memory tools kept; attribution off"
else
    bad "settings.json: $(printf '%s' "$K7" | tr '\n' ';')"
fi

# ---------------------------------------------------------------------------
head2 "7m. check-moduledata-validation fails open on a validator crash and says so, and still denies on findings"
pre_payload() {  # $1 tool, $2 command: a PreToolUse payload as the harness sends it
    "$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$1" "$2"
}
# validate_moduledata.py exits 1 for ERROR findings, and Python exits 1 for an uncaught exception too,
# so a validator bug used to DENY the commit with a bare status. entry() now turns a crash into exit 3,
# and the hook allows it, with the traceback beside the tool result (PreToolUse additionalContext: an
# allow's stderr reaches no one). The stub imports the real validator and makes main() raise, so the
# whole chain is under test, not a mock of it. A sandbox repo stages one ModuleData file.
VM_REPO="$SANDBOX/vm-repo"
mkdir -p "$VM_REPO/tools" "$VM_REPO/Main/_Module/ModuleData"
git -C "$VM_REPO" init -q 2>/dev/null
printf '<Items />\n' > "$VM_REPO/Main/_Module/ModuleData/x.xml"
git -C "$VM_REPO" add Main/_Module/ModuleData/x.xml 2>/dev/null
VM_TOOLS=$(cygpath -m "$REPO/tools" 2>/dev/null || echo "$REPO/tools")
cat > "$VM_REPO/tools/validate_moduledata.py" <<STUB
import sys
sys.path.insert(0, r"$VM_TOOLS")
import validate_moduledata as vm
def boom():
    raise RuntimeError("stub validator crash")
vm.main = boom
sys.exit(vm.entry())
STUB
vm_run() {  # stdin: the payload; prints the hook's answer
    CLAUDE_PROJECT_DIR="$VM_REPO" timeout -k 2 30 bash "$REPO/.claude/hooks/check-moduledata-validation.sh" 2>/dev/null
}
VM_PAYLOAD=$(pre_payload Bash 'git commit -m "data: v0.0.0 - stub"')
VM_OUT=$(printf '%s' "$VM_PAYLOAD" | vm_run)
VM_VERDICT=$(printf '%s' "$VM_OUT" | "$HPY" -c '
import json, sys
try:
    d = json.loads(sys.stdin.read() or "{}")
except Exception:
    print("invalid"); sys.exit()
h = d.get("hookSpecificOutput", {}) if isinstance(d, dict) else {}
if h.get("permissionDecision") == "deny":
    print("deny")
elif "stub validator crash" in h.get("additionalContext", "") and "Traceback" in h.get("additionalContext", ""):
    print("allow-with-traceback")
else:
    print("allow-silent")')
case "$VM_VERDICT" in
    allow-with-traceback) ok "a validator crash (exit 3) allows the commit and carries the traceback in additionalContext" ;;
    deny)                 bad "a validator crash DENIED the commit: a hook's own fault must fail open" ;;
    *)                    bad "a validator crash was $VM_VERDICT, expected allow-with-traceback: $(printf '%s' "$VM_OUT" | head -c 200)" ;;
esac
# Control: a genuine findings exit (1) still denies.
printf 'import sys\nsys.exit(1)\n' > "$VM_REPO/tools/validate_moduledata.py"
VM_OUT=$(printf '%s' "$VM_PAYLOAD" | vm_run)
if [[ "$(decision_of "$VM_OUT")" == deny ]]; then
    ok "a validator findings exit (1) still denies the commit"
else
    bad "a validator findings exit (1) no longer denies: $(printf '%s' "$VM_OUT" | head -c 200)"
fi
rm -rf "$VM_REPO"

# ---------------------------------------------------------------------------
head2 "Summary"
printf '  %d passed, %d failed\n' "$PASS" "$FAIL"
if (( FAIL > 0 )); then
    printf '\n  Failures:\n'
    printf '    - %s\n' "${FAILED_DETAIL[@]}"
    exit 1
fi
exit 0
