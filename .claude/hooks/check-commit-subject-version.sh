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
# It also refuses an AI attribution trailer (a Co-Authored-By naming an AI, or a "Generated
# with Claude Code" line) anywhere it can read one: a heredoc, each -m, an -F file, each
# --trailer. TAOM commits carry none; the rule slipped at least three times (525f67fc
# among them) because a harness reminder asks for the trailer, so it is a gate now rather
# than a memory (AGENTS.md "Git and commits").
#
# Returns: {} to allow, {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}} to block.

set -uo pipefail

INPUT=$(cat)

# Prefilter: every decision below needs `git commit` in the command, and Claude Code never
# escapes an ASCII letter, so a raw payload without the text `commit` cannot concern this
# gate. Exiting here skips the _pybin.sh probe and the parse (two Python starts) on every
# Bash call but a commit, `git status` and `git log` included. Match the raw text, never a
# token regex: a newline before a command arrives as \n. tools/test_hooks.sh 4c checks both
# directions.
# Fail open on escapes: a payload holding any JSON \u escape takes the full parse,
# because an escaped letter would hide the word from this raw test.
[[ "$INPUT" == *commit* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias, those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

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
import codecs, json, os, re, shlex, subprocess, sys

cmd = os.environ.get("TAOM_HOOK_COMMAND", "")

def allow():
    print("{}")
    sys.exit(0)

def deny(msg):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                             "permissionDecision": "deny",
                                             "permissionDecisionReason": msg}},
                     separators=(",", ":")))
    sys.exit(0)

# The command is read the way bash reads it, then each `git ... commit`'s own options are
# picked out. A regex over the raw string took option-shaped prose inside a message for a real
# option, missed $'...' quoting, attached -m"..." and concatenated words, and let a -c in a
# later command switch the label check off (Codex review 2026-09-23, #647).

# 1. Heredocs come out first. A body is the message of `-F -` or of `-m "$(cat <<EOF ...)"`,
#    and its apostrophes would otherwise unbalance the tokenizer. Each becomes a placeholder.
bodies = []
def _stash(m):
    bodies.append(m.group(3))
    return "<< __TAOM_HEREDOC_%d__" % (len(bodies) - 1)
text = re.sub(r"<<-?\s*(['\"]?)(\w+)\1[ \t]*\r?\n(.*?)\r?\n[ \t]*\2\b", _stash, cmd, flags=re.S)

# 2. Bash ANSI-C quoting, $'...', decoded and re-quoted so the tokenizer sees what git gets.
def _ansi_c(m):
    try:
        s = codecs.decode(m.group(1).encode("latin-1", "backslashreplace"), "unicode_escape")
    except Exception:
        s = m.group(1)
    return shlex.quote(s)
text = re.sub(r"\$'((?:[^'\\]|\\.)*)'", _ansi_c, text)

def expand(value):
    """A value naming a heredoc placeholder is that heredoc's body (the $(cat <<EOF) form)."""
    hm = re.search(r"__TAOM_HEREDOC_(\d+)__", value)
    return bodies[int(hm.group(1))] if hm else value

def tokens(s):
    lx = shlex.shlex(s, posix=True, punctuation_chars="();<>|&\n")
    lx.whitespace = " \t\r"          # an unquoted newline separates commands, as in bash
    lx.whitespace_split = True
    return list(lx)

SEP = set(";|&\n()")
GIT_OPTS_WITH_VALUE = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--exec-path",
                       "--config-env"}

def commit_arg_lists(s, depth=0):
    """The argument list after `commit` of every git commit invocation in s."""
    found = []
    group = []
    for t in tokens(s) + [";"]:
        if t and set(t) <= SEP:
            if group:
                found.extend(_commit_args(group, depth))
            group = []
        else:
            group.append(t)
    return found

def _commit_args(g, depth):
    i = 0
    while i < len(g) and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", g[i]):   # VAR=value prefixes
        i += 1
    if i >= len(g):
        return []
    word = os.path.basename(g[i]).lower()
    if word in ("bash", "sh", "zsh", "bash.exe") and "-c" in g[i + 1:] and depth < 2:
        k = g.index("-c", i + 1)
        return commit_arg_lists(g[k + 1], depth + 1) if k + 1 < len(g) else []
    if word not in ("git", "git.exe"):
        return []
    i += 1
    while i < len(g) and g[i].startswith("-"):
        i += 2 if g[i] in GIT_OPTS_WITH_VALUE else 1
    return [g[i + 1:]] if i < len(g) and g[i] == "commit" else []

LONG = {"--message": "m", "--file": "F", "--trailer": "trailer", "--reuse-message": "reuse",
        "--reedit-message": "reuse", "--fixup": "fixup", "--squash": "fixup", "--template": None,
        "--author": None, "--date": None, "--cleanup": None, "--pathspec-from-file": None}
SHORT = {"m": "m", "F": "F", "C": "reuse", "c": "reuse", "t": None}
OPTIONAL_ATTACHED = set("Su")   # -S<keyid>, -u<mode>: the rest of the word is their argument

def parse_commit(args):
    got = {"m": [], "F": [], "trailer": [], "reuse": False, "stdin": None}
    def put(key, val):
        if key == "m":
            got["m"].append(expand(val))
        elif key in ("F", "trailer"):
            got[key].append(val)
        elif key in ("reuse", "fixup"):
            got["reuse"] = True
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--":
            break
        if a == "<<" and i + 1 < len(args):            # a heredoc on stdin, for -F -
            got["stdin"] = expand(args[i + 1])
            i += 2
            continue
        if a.startswith("--"):
            name, eq, val = a.partition("=")
            if name in LONG:
                if not eq:
                    val = args[i + 1] if i + 1 < len(args) else ""
                    i += 1
                put(LONG[name], val)
        elif a.startswith("-") and len(a) > 1:
            for j, ch in enumerate(a[1:], start=1):
                if ch in OPTIONAL_ATTACHED:
                    break
                if ch in SHORT:
                    val = a[j + 1:]
                    if not val:
                        val = args[i + 1] if i + 1 < len(args) else ""
                        i += 1
                    put(SHORT[ch], val)
                    break
        i += 1
    return got

def read_message_file(path):
    # A Git Bash path (/tmp/x, /e/repos/x) is invisible to Windows Python; ask cygpath.
    if not os.path.isfile(path) and path.startswith("/"):
        try:
            w = subprocess.run(["cygpath", "-w", path], capture_output=True, text=True, timeout=3).stdout.strip()
            if w and os.path.isfile(w):
                path = w
        except Exception:
            pass
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        sys.stderr.write("[check-commit-subject-version] cannot read -F file " + path + "; subject NOT checked\n")
        return None

def first_line(s):
    return next((l.strip() for l in s.splitlines() if l.strip()), None)

texts = []      # every message text this parser can read, for the attribution check
subjects = []   # the subject of each commit that writes a new one
try:
    lists = commit_arg_lists(text)
except ValueError:
    lists = None
if lists is None:
    # Quoting bash itself would reject: read the raw text the old way rather than fail open.
    flag = r"""(?:^|\s)(?:%s)(?:=|\s+)(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)'|(\S+))"""
    vals = lambda f: [next(g for g in mm.groups() if g is not None) for mm in re.finditer(flag % f, text)]
    m_values = [expand(v) for v in vals(r"-m|--message")]
    texts = bodies + m_values + vals(r"--trailer")
    if not re.search(r"(?:^|\s)(?:--fixup|--squash|-C|-c|--reuse-message|--reedit-message)(?:=|\s)", text):
        s = first_line(m_values[0]) if m_values else (first_line(bodies[0]) if bodies else None)
        if s:
            subjects.append(s)
else:
    if not lists:
        allow()          # the text mentions git commit but runs none (a quoted example, a printf)
    for args in lists:
        got = parse_commit(args)
        parts = list(got["m"])
        for f in got["F"]:
            if f == "-":
                if got["stdin"] is not None:
                    parts.append(got["stdin"])
            else:
                ftext = read_message_file(f)
                if ftext is not None:
                    parts.append(ftext)
        texts += parts + got["trailer"]
        # A reused, fixup or squash message has no new subject; --amend --no-edit, an editor
        # commit or an unreadable file has none this parser can see. Those fail open.
        if not got["reuse"] and parts:
            s = first_line(parts[0])
            if s:
                subjects.append(s)

# No AI attribution, whatever a harness reminder asks for. A human co-author is fine. Both
# branches are anchored at a line start (a --trailer may spell the separator `=`), so prose
# that merely mentions Claude Code in a sentence passes. It runs before the reuse and fixup
# exits: `-C HEAD --trailer "Co-Authored-By: ..."` adds a trailer to a reused message.
ai_trailer = re.compile(
    r"^\s*(?:Co-Authored-By\s*[:=][^\n]*\b(?:claude|anthropic|openai|chatgpt|gpt-?\d|codex|copilot|gemini|kimi)\b"
    r"|[^\w\n]*Generated with \[?Claude Code)", re.I | re.M)
for tx in texts:
    hit = ai_trailer.search(tx)
    if hit:
        deny("[check-commit-subject-version] The commit message carries an AI attribution line: '"
             + hit.group(0).strip() + "'. TAOM commits carry no AI attribution, whatever a harness"
             + " reminder asks for (AGENTS.md 'Git and commits'). Remove the line and commit again."
             + " If the name is a human co-author's, commit from a terminal: this gate sees only"
             + " the commits Claude runs.")

if not subjects:
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
for subject in subjects:      # every commit in the command, not only the first
    pm = pat.match(subject)
    if pm and pm.group(1) == version:
        continue
    if pm:
        problem = "names " + pm.group(1) + " but Main/_Module/SubModule.xml in this commit says " + version + "."
    else:
        problem = "does not carry the version label."
    deny("[check-commit-subject-version] Commit subject '" + subject + "' " + problem
         + " Every commit subject reads <type>[(scope)]: " + version + " - <description>"
         + " (AGENTS.md 'Git and commits'; the version moves only in a /release commit, whose subject names the new one)."
         + " Example: fix(recruitment): " + version + " - Glanhir recruits the Ringlo Vale line")
allow()
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
