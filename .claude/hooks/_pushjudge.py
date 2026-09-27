"""validate-push.sh's judge (#680): which candidate line, if any, force-pushes a protected branch.

_shellwords.py verdict reads the payload, splits it into the candidate lines (push_candidates) and
hands them to Judge.run in the same Python start. This is a port of the bash judge the hook ran until
#680 (judge_command, judge_refs and its line loop), which cost about 25 microseconds a word: a trunk
force push carrying 250 KB of quoted text holding `push` outran the 5 s registration, and a killed
gate fails open. Every verdict and every named target must stay what bash gave, so each place where
a natural Python choice reads a line differently is written the bash way (tools/tests/test_pushjudge.py
pins each one, with the verdict read from the bash judge):
  - a line is judged as bash's $( ) received it: a lone surrogate the reader wrote as ?, every NUL
    dropped, then each distinct line once;
  - words split at space, tab and newline only, never str.split(), which also splits at \\v, \\f, NBSP
    and the \\x1e and \\x1f the reader puts inside a word to keep an argument whole;
  - git is recognised after lowering A-Z only, as bash's ${tok,,} does in the hook's C.UTF-8 locale;
  - an fd number is ASCII digits, never str.isdigit();
  - a refspec pattern of * and plain characters matches as bash's [[ == ]] does. One holding any other
    glob character (? [ ] \\ ( ) |) is taken to match every protected name: git refuses such a
    refspec, so this can only refuse more, and it spares a port of bash's bracket matcher.
"""
import re
import subprocess

BLOCK_ALL = "every branch, both trunks included (--all or --mirror)"
_CLEAN = str.maketrans({c: " " for c in "\"'(){}`"})
_IFS = re.compile(r"[ \t\n]+")
_ASCII_LOWER = str.maketrans("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyz")
_REDIR = re.compile(r"[<>]")
_FD = re.compile(r"[0-9]+")
_FORCE = {"--force", "--force-with-lease", "--force-if-includes", "-f"}
_VALUE_OPTS = ("--pu", "--rep", "--rece", "--recu", "--ex")   # --push-option, --repo, ... and prefixes
_GLOB_OTHER = re.compile(r"[?\[\]\\()|]")


def bash_view(line):
    """The line as the bash loop read it: written with encode("utf-8", "replace"), NUL dropped by $( )."""
    return line.encode("utf-8", "replace").decode("utf-8").replace("\x00", "")


def is_git(tok):
    low = tok.translate(_ASCII_LOWER)
    return low in ("git", "git.exe") or low.endswith(("/git", "\\git", "/git.exe", "\\git.exe"))


def star_match(pattern, name):
    """bash's [[ name == pattern ]] for a pattern whose only glob character is *."""
    parts = pattern.split("*")
    head, tail = parts[0], parts[-1]
    if len(parts) == 1:
        return name == pattern
    if len(head) + len(tail) > len(name) or not name.startswith(head) or not name.endswith(tail):
        return False
    i, end = len(head), len(name) - len(tail)
    for part in parts[1:-1]:          # leftmost is enough when * is the only wildcard
        i = name.find(part, i, end)
        if i < 0:
            return False
        i += len(part)
    return True


def current_branch():
    """`git branch --show-current 2>/dev/null` as $( ) gives it: trailing newlines and NUL dropped,
    empty on any failure. Asked in the hook's own directory, which is the main tree even for a push
    run in a worktree (a known gap, hooks-catalog.md)."""
    try:
        r = subprocess.run(["git", "branch", "--show-current"], stdout=subprocess.PIPE,
                           stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL, timeout=2)
    except (OSError, subprocess.SubprocessError):
        return ""
    return r.stdout.decode("utf-8", "replace").replace("\x00", "").rstrip("\n")


class Judge:
    """One hook run. block is the first forced protected target found (the run stops there); warn is
    the last plain push to a protected name, overwritten as bash's WARN_TARGET was."""

    def __init__(self, protected, branch=current_branch):
        self.protected = list(protected)
        self._branch = branch          # asked at most once a run: 100 no-refspec lines stay cheap
        self._cur = None
        self.block = ""
        self.warn = ""

    def cur_branch(self):
        if self._cur is None:
            self._cur = self._branch()
        return self._cur

    def judge_refs(self, positionals, force):
        """The remote, then each refspec: every refspec counts, and --force applies to all."""
        for ref in positionals[1:] or [""]:
            f = force
            if ref.startswith("+"):
                f, ref = True, ref[1:]
            ref = ref.rpartition(":")[2]
            if ref.startswith("refs/"):
                ref = ref[5:]
            if ref.startswith("heads/"):
                ref = ref[6:]
            if ref in ("", "HEAD", "@"):
                ref = self.cur_branch()
            if "*" in ref:
                other = _GLOB_OTHER.search(ref)
                ref = next((p for p in self.protected if other or star_match(ref, p)), ref)
            if ref in self.protected:
                if f:
                    self.block = ref
                    return
                self.warn = ref

    def judge_command(self, line):
        toks = [t for t in _IFS.split(line.translate(_CLEAN)) if t]
        # The first `push` with a git before it: a `push` word glued in front by an unclosed quote
        # ("don't push") must not hide the real push after it.
        push_idx, git_seen = -1, False
        for i, tok in enumerate(toks):
            if is_git(tok):
                git_seen = True
                continue
            if tok == "push" and git_seen:
                push_idx = i
                break
        if push_idx < 0:
            return
        force = every = False
        keep, skip = [], []                # the positionals with and without an option's value
        rskip = vskip = False
        for tok in toks[push_idx + 1:]:
            if rskip:                      # a redirection's target; a pending value skip survives it
                rskip = False
                continue
            isval, vskip = vskip, False
            m = _REDIR.search(tok)
            if m:
                rskip = tok[-1] in "<>"
                tok = tok[:m.start()]
                if not tok or _FD.fullmatch(tok) or tok == "*":
                    continue
            if tok in _FORCE or tok.startswith("--force-with-lease="):
                force = True
            elif tok in ("--all", "--branches"):
                every = True
            elif tok == "--mirror":
                every = force = True
            elif tok.startswith("--"):
                vskip = "=" not in tok[2:] and tok.startswith(_VALUE_OPTS)
            elif tok.startswith("-"):      # a short cluster: -fu is -f -u, and in -fo x the o takes x
                force = force or "f" in tok
                flags = tok[1:]
                vskip = bool(flags) and flags.find("o") == len(flags) - 1
            else:
                keep.append(tok)
                if not isval:
                    skip.append(tok)
        if force and every:
            self.block = BLOCK_ALL
            return
        self.judge_refs(skip, force)
        if self.block or len(keep) == len(skip):
            return
        self.judge_refs(keep, force)

    def run(self, lines):
        """(kind, target): block, warn or allow, over the candidate lines in the reader's order."""
        seen = set()
        for line in lines:
            seg = bash_view(line)
            if "push" not in seg or seg in seen:
                continue
            seen.add(seg)
            self.judge_command(seg)
            if self.block:
                return "block", self.block
        return ("warn", self.warn) if self.warn else ("allow", "")
