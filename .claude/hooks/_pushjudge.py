"""validate-push.sh's judge (#680): which candidate line, if any, force-pushes a protected branch.

_shellwords.py verdict reads the payload, splits it into the candidate lines (push_candidates) and
hands them to Judge.run in the same Python start. This is a port of the bash judge the hook ran until
#680 (judge_command, judge_refs and its line loop), which cost about 25 microseconds a word: under
load a trunk force push carrying 250 KB of quoted text holding `push` outran the 5 s registration it
then had (idle, 400 KB took 4.9 s), and a killed gate fails open. Every verdict and every named
target must stay what bash gave, so each place where a natural Python choice reads a line
differently is written the bash way (tools/tests/test_pushjudge.py pins each one, with the verdict
read from the bash judge):
  - a line is judged as bash's $( ) received it: a lone surrogate the reader wrote as ?, every NUL
    dropped, then each distinct line once;
  - words split at space, tab and newline only, never str.split(), which also splits at \\v, \\f, NBSP
    and the \\x1e and \\x1f the reader puts inside a word to keep an argument whole;
  - git is recognised after lowering A-Z only, as bash's ${tok,,} does in the hook's C.UTF-8 locale;
  - an fd number is ASCII digits, never str.isdigit();
  - a refspec pattern of * and plain characters matches as bash's [[ == ]] does. One holding any other
    glob character (? [ ] \\ ( ) |) is taken to match every protected name: git refuses such a
    refspec, so this can only refuse more, and it spares a port of bash's bracket matcher.
#689 then made it refuse more than the bash judge did, each shape one git 2.55 runs as a forced push
of a trunk: a long option read as git reads it (an unambiguous prefix is that option, so --force-w
is --force-with-lease and force-updates the trunk it names, while --mir is --mirror and --al is
--all, which reach every trunk), the refspec : when forced, and a forced push with no refspec under
push.default=matching, from -c or from git's own config. A --force-if-includes prefix counts as force
too, a deliberate over-block: --force-i alone is no forced push (git rejects a non-fast-forward).
The #680 review read the value options by the same prefix rule (--e is --exec and takes a value),
on top of the bash judge's table, so no word takes a value that did not before.
"""
import re
import subprocess

BLOCK_ALL = "every branch, both trunks included (--all or --mirror)"
BLOCK_MATCHING = "every matching branch, both trunks included (a : refspec or push.default=matching)"
_CLEAN = str.maketrans({c: " " for c in "\"'(){}`"})
_IFS = re.compile(r"[ \t\n]+")
_ASCII_LOWER = str.maketrans("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyz")
_REDIR = re.compile(r"[<>]")
_FD = re.compile(r"[0-9]+")
_FORCE = ("--force", "--force-with-lease", "--force-if-includes")
_EVERY = ("--all", "--branches")
_VALUE_OPTS = ("--push-option", "--repo", "--receive-pack", "--recurse-submodules", "--exec")
_VALUE_LEADS = ("--pu", "--rep", "--rece", "--recu", "--ex")   # the bash judge's table of them
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


def long_option(tok, names):
    """Whether git reads the long option tok as one of names (#689). git takes an unambiguous prefix
    of a long option as that option, so the name before any = counts from 3 characters, `--f`
    included; git refuses an ambiguous prefix, so counting one only refuses what git refuses."""
    name = tok.partition("=")[0]
    return len(name) >= 3 and any(n.startswith(name) for n in names)


def git_out(args):
    """`git <args> 2>/dev/null` as $( ) gives it: trailing newlines and NUL dropped, empty on any
    failure. Asked in the hook's own directory: the session's directory, the main tree unless an
    earlier Bash cd moved it (#690), even for a push run in a worktree or under -C (a known gap,
    hooks-catalog.md)."""
    try:
        r = subprocess.run(["git"] + args, stdout=subprocess.PIPE,
                           stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL, timeout=2)
    except (OSError, subprocess.SubprocessError):
        return ""
    return r.stdout.decode("utf-8", "replace").replace("\x00", "").rstrip("\n")


def current_branch():
    return git_out(["branch", "--show-current"])


def push_default():
    return git_out(["config", "--get", "push.default"])


class Judge:
    """One hook run. block is the first forced protected target found (the run stops there); warn is
    the last plain push to a protected name, overwritten as bash's WARN_TARGET was."""

    def __init__(self, protected, branch=current_branch, push_default=push_default):
        self.protected = list(protected)
        self._branch = branch          # asked at most once a run: 100 no-refspec lines stay cheap
        self._cur = None
        self._push_default = push_default    # likewise, and only for a forced push with no refspec
        self._default = None
        self.block = ""
        self.warn = ""

    def cur_branch(self):
        if self._cur is None:
            self._cur = self._branch()
        return self._cur

    def default_matching(self):
        if self._default is None:
            self._default = self._push_default()
        return self._default.strip() == "matching"

    def judge_refs(self, positionals, force, matching=False):
        """The remote, then each refspec: every refspec counts, and --force applies to all. A forced
        push of the refspec :, or with no refspec under push.default=matching, pushes every branch
        the remote also has (#689)."""
        if force and not positionals[1:] and (matching or self.default_matching()):
            self.block = BLOCK_MATCHING
            return
        for ref in positionals[1:] or [""]:
            f = force
            if ref.startswith("+"):
                f, ref = True, ref[1:]
            if f and ref == ":":
                self.block = BLOCK_MATCHING
                return
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
        push_idx, git_idx = -1, -1
        for i, tok in enumerate(toks):
            if is_git(tok):
                git_idx = i if git_idx < 0 else git_idx
                continue
            if tok == "push" and git_idx >= 0:
                push_idx = i
                break
        if push_idx < 0:
            return
        # -c push.default=matching between git and push; the key is case-blind to git. git refuses
        # the glued -cpush.default=matching as an unknown option, so it needs no rule.
        opts = toks[git_idx + 1:push_idx]
        matching = any(a == "-c" and b.translate(_ASCII_LOWER) == "push.default=matching"
                       for a, b in zip(opts, opts[1:]))
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
            if tok.startswith("--"):
                if long_option(tok, _FORCE):
                    force = True
                elif long_option(tok, ("--mirror",)):
                    every = force = True
                elif long_option(tok, _EVERY):
                    every = True
                else:
                    # A value option takes the next word: a prefix git reads as one (--e is --exec,
                    # #680 review), or a word the bash judge's table took for one. git refuses those
                    # that are no prefix (--expire) as unknown, so reading them so only refuses more.
                    vskip = "=" not in tok and (long_option(tok, _VALUE_OPTS) or tok.startswith(_VALUE_LEADS))
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
        self.judge_refs(skip, force, matching)
        if self.block or len(keep) == len(skip):
            return
        self.judge_refs(keep, force, matching)

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
