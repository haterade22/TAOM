#!/usr/bin/env bash
# check-commit-subject-version.sh
# PreToolUse(Bash) hook: every `git commit` subject must read
#
#     <type>[(scope)][!]: vX.Y.Z - <description>
#
# where vX.Y.Z is EXACTLY the <Version value="..."> of Main/_Module/SubModule.xml as it
# will be in the commit (the staged copy when that file is staged, else the working
# tree). Refuses the commit otherwise.
#
# Why (user rule, 2026-09-13): a crash bundle reports TaomVersion from that file, and the
# version moves only in a /release commit, so the 102 commits pushed after the v2.0.28
# tag all read as v2.0.28 in a player's report. With the version in every subject,
# `git log --grep 'v2.0.28 - '` lists exactly the commits a v2.0.28 build can contain,
# and the release commit's subject names the new version it introduces.
#
# Forms that bring no new subject are allowed through unchanged: --amend without a
# message source (keeps HEAD's subject), -C/-c/--reuse-message, --fixup, --squash, and an
# editor commit. Plumbing (`git commit-tree`, `git commit-graph`) is not a commit.
#
# Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.

set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias, those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "check-commit-subject-version" "the commit-subject version label" && { echo '{}'; exit 0; }

# Extract the bash command from tool_input.
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)

# Detect `git commit` invocations including `git -C <dir> commit` and
# `git -c <key>=<val> commit`. Reject `git commit-tree`, `commit-graph`, etc.
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree / commit-graph etc, a different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

# The subject is parsed and judged in Python: heredoc bodies, quoted -m strings and -F
# files are all easier there than in shell. Bounded well under the registered timeout so
# an overrun can still speak (hook-authoring.md, "a timeout is a kill").
DECISION=$(TAOM_HOOK_COMMAND="$COMMAND" timeout -k 2 8 "$PYBIN" - <<'PY' 2>/dev/null
import json, os, re, subprocess, sys

cmd = os.environ.get("TAOM_HOOK_COMMAND", "")

# Options are judged only AFTER the `commit` token: `git -C <dir> commit` and `git -c k=v
# commit` carry the same letters as the reuse-message flags `commit -C/-c <commit>`.
cm = re.search(r"\scommit(?=\s|$)", cmd)
args = cmd[cm.end():] if cm else cmd

def allow():
    print("{}")
    sys.exit(0)

def deny(msg):
    print(json.dumps({"permissionDecision": "deny", "message": msg}, separators=(",", ":")))
    sys.exit(0)

# Forms whose subject git derives from another commit: nothing new to judge here.
if re.search(r"(?:^|\s)--(?:fixup|squash)(?:=|\s)", args):
    allow()
if re.search(r"(?:^|\s)(?:-C|-c|--reuse-message|--reedit-message)(?:=|\s)", args):
    allow()

subject = None

# 1. A heredoc body: git commit -m "$(cat <<'EOF' ... EOF)" or git commit -F - <<'EOF'.
m = re.search(r"<<-?\s*(['\"]?)(\w+)\1[ \t]*\r?\n(.*?)\r?\n[ \t]*\2\b", cmd, re.S)
if m:
    for line in m.group(3).splitlines():
        if line.strip():
            subject = line.strip()
            break

# 2. -m / --message, first occurrence (git treats the first -m as the subject paragraph).
if subject is None:
    m = re.search(r"""(?:^|\s)(?:-m|--message)(?:=|\s+)(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)'|(\S+))""", args)
    if m:
        raw = next(g for g in m.groups() if g is not None)
        raw = raw.replace("\\n", "\n")
        for line in raw.splitlines():
            if line.strip():
                subject = line.strip()
                break

# 3. -F / --file with a real path (a "-" means stdin, handled by the heredoc branch).
if subject is None:
    m = re.search(r"""(?:^|\s)(?:-F|--file)(?:=|\s+)(?:"([^"]+)"|'([^']+)'|(\S+))""", args)
    if m:
        path = next(g for g in m.groups() if g is not None)
        if path != "-":
            # A Git Bash path (/tmp/x, /e/repos/x) is invisible to Windows Python; ask cygpath.
            if not os.path.isfile(path) and path.startswith("/"):
                try:
                    w = subprocess.run(["cygpath", "-w", path], capture_output=True, text=True, timeout=3).stdout.strip()
                    if w and os.path.isfile(w):
                        path = w
                except Exception:
                    pass
            if os.path.isfile(path):
                try:
                    with open(path, encoding="utf-8", errors="replace") as fh:
                        for line in fh:
                            if line.strip():
                                subject = line.strip()
                                break
                except OSError:
                    pass
            if subject is None:
                sys.stderr.write("[check-commit-subject-version] cannot read -F file " + path + "; subject NOT checked\n")

# --amend --no-edit, an editor commit, or a form this parser does not know: no subject to
# judge, so let git proceed (fail open) rather than block on the hook's own blind spot.
if subject is None:
    allow()

# The version the commit will carry: the staged SubModule.xml when it is staged (a
# release commit bumps it there), else the working tree.
sub = "Main/_Module/SubModule.xml"
text = ""
try:
    staged = subprocess.run(["git", "diff", "--cached", "--name-only", "--", sub],
                            capture_output=True, text=True, timeout=4).stdout
    if staged.strip():
        text = subprocess.run(["git", "show", ":" + sub],
                              capture_output=True, text=True, timeout=4).stdout
except Exception:
    text = ""
if not text:
    try:
        with open(sub, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
    except OSError:
        text = ""
vm = re.search(r'<Version\s+value="([^"]+)"', text)
if not vm:
    sys.stderr.write("[check-commit-subject-version] could not read <Version> from " + sub + "; subject NOT checked\n")
    allow()
version = vm.group(1)

pat = re.compile(r"^[a-z][a-z0-9]*(?:\([^)]+\))?!?: (v\d+\.\d+\.\d+(?:\.\d+)?) - \S")
pm = pat.match(subject)
if pm and pm.group(1) == version:
    allow()

if pm:
    problem = "names " + pm.group(1) + " but Main/_Module/SubModule.xml in this commit says " + version + "."
else:
    problem = "does not carry the version label."
deny("[check-commit-subject-version] Commit subject '" + subject + "' " + problem
     + " Every commit subject reads <type>[(scope)]: " + version + " - <description>"
     + " (CLAUDE.md 'Commits'; the version moves only in a /release commit, whose subject names the new one)."
     + " Example: fix(recruitment): " + version + " - Glanhir recruits the Ringlo Vale line")
PY
)
RC=$?
if [[ $RC -eq 124 || $RC -eq 137 ]]; then
    echo "[check-commit-subject-version] parser timed out; subject NOT checked" >&2
    echo '{}'
    exit 0
fi
if [[ -z "$DECISION" ]]; then
    echo "[check-commit-subject-version] parser produced no decision (rc $RC); subject NOT checked" >&2
    echo '{}'
    exit 0
fi
printf '%s\n' "$DECISION"
exit 0
