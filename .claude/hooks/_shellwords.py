"""The command a Bash or PowerShell tool call runs, read the way the TAOM gates need it (plan 027).

Every PreToolUse git gate was written for Bash text. The PowerShell tool sends PowerShell text, so
to_posix() rewrites it as the POSIX-shell (Bash) text of the same command, and each gate keeps its
Bash logic. In both shells a git named by a path or in capitals (GIT, git.exe, a Windows path to
git.exe) becomes `git` where it is the command.

Usage: <python> _shellwords.py posix|commands [--blind]|verdict [protected...]|git-repo [subcommand] < the hook payload (JSON)
  posix     the command as POSIX-shell text
  commands  the posix text split at ; & | and newlines outside quotes (a # comment dropped), each
            segment from its command word on, one per line (#985): past VAR=value, shell grammar
            ({ ! if then do ...) and, for the Bash tool, the wrappers env, timeout, command, time,
            nice, nohup, xargs and stdbuf. The script of a `bash|sh|zsh -c` and the body of a heredoc
            such a shell reads are split too; a heredoc any other command reads is data and adds
            nothing. mark-verification-run.sh reads this form, so a mention never marks. With
            --blind (block-dangerous-git.sh, block-broad-git-add.sh) it also adds the pieces of the
            text cut at every ; & | and newline whatever the quotes, heredoc bodies inline: a
            superset of the old sed split, so a confirm gate never asks less than it did (lesson
            #947). Only the first 32 words of a segment are looked at. Not peeled: sudo, exec, rtk,
            eval, watch, $(...), case arms, cmd /c, command -v. The string of env -S is read as a script.
  verdict   validate-push.sh's answer for the protected branch names given as arguments (#680): one
            line, block<TAB>target, warn<TAB>target or allow; _pushjudge.py judges the candidate
            lines (push_candidates). A payload that is no JSON object prints unread, which the hook
            answers from the raw text, never with a silent allow
  git-repo  the directory of every `git <subcommand>` (default commit) in the command, one per line
            (a cd or -C before it joined onto the payload's cwd, or the cwd), then a line `--`, then
            the payload's cwd (_pybin.sh taom_git_repo_dir resolves each to its work tree's top level)
It writes UTF-8 with LF line ends and exits 0; an unknown mode exits 2. PowerShell text it cannot
follow (an unclosed quote, here-string, block comment or ${) comes back unchanged, which is how
every gate read a command before plan 027.

Not read, by design: a $(...) inside a double-quoted PowerShell string stays text, a $variable is
never expanded, and PowerShell's comma array syntax stays inside one word. A string or variable that
opens a PowerShell statement is a value it prints, never a command, so it reads as `echo <value>`.
"""
import codecs
import json
import os
import re
import shlex
import sys

GIT = re.compile(r"(?:.*[\\/])?git(?:\.exe)?", re.I)
SAFE = re.compile(r"[\w@%+=:,./^~-]+")
PS_ESCAPES = {"0": "", "a": "\a", "b": "\b", "e": "\x1b", "f": "\f", "n": "\n", "r": "\r",
              "t": "\t", "v": "\v"}
HERE_OPEN = re.compile(r"@(['\"])[ \t]*\n")
REDIRECT = re.compile(r"[<>]{1,2}(?:&[12])?")
FD_PREFIX = {"*", "1", "2", "3", "4", "5", "6"}
# PowerShell reads the typographic quotes as quotes too (plan 027 review: `-o 'ci #1'` written with
# them, read with ASCII quotes only, took the ` #` for a comment and dropped the push).
SQ = "'\u2018\u2019\u201a\u201b"
DQ = '"\u201c\u201d\u201e'
BLANK = " \t\u00a0"
ASSIGN = {"=", "+=", "-=", "*=", "/=", "%=", "??="}
# An assignment's left side, as ParseInput reads one (convergence of plan 027): a variable, cast
# ([int]$x, or [int] $x spaced), member ($a.b), index ($a[0]) or a comma list of them ($x, $y).
_VAR = r"(?:\$[\w:?]+|\$\{[^}]*\})"
_CAST = r"\[[\w.]+(?:\[\])?\]"
_ONE = rf"(?:{_CAST})*{_VAR}(?:\.\w+|\[[^\]]*\])*"
_TARGET = rf"(?:,?{_ONE}(?:,(?:{_ONE})?)*|,|{_CAST})"
_OP = r"\?\?=|[-+*/%]?="
TARGET = re.compile(_TARGET)
TARGET_OP = re.compile(rf"({_TARGET})({_OP})(.*)", re.S)          # $r=git, $a.b=git, $x,$y=
BARE_OP = re.compile(rf"({_OP})(.*)", re.S)                       # the =git of `$null =git`
# Environment assignments may come before the command (`GIT_TRACE=0 GIT commit`, plan 027 review).
BASH_GIT = re.compile(r"""((?:^|[;&|(){}`\n])[ \t]*(?:[A-Za-z_]\w*=[^\s;&|(){}`]*[ \t]+)*|\$\([ \t]*)"""
                      r"""("[^"\n]*"|'[^'\n]*'|[^\s;&|(){}'"`<>]+)""")


class Unreadable(ValueError):
    """PowerShell text this reader cannot follow."""


def _escape(s, i):
    """The text a PowerShell backtick at s[i] stands for, and the index after it."""
    nxt = s[i + 1:i + 2]
    if nxt == "u" and s[i + 2:i + 3] == "{":
        end = s.find("}", i + 3, i + 10)          # 1 to 6 hex digits; an unbounded search was quadratic
        if end > 0:
            try:
                return chr(int(s[i + 3:end], 16)), end + 1
            except ValueError:
                pass
    return PS_ESCAPES.get(nxt, nxt), i + 2


def ps_tokens(s):
    """PowerShell source as (kind, text) pairs: w a word, e a word that opens as a value (a string,
    a here-string or a $variable), op a separator, call the & or . call operator, redir a
    redirection operator."""
    s = s.replace("\r", "")
    # word is None between words, else the current word's non-empty pieces. A list, so a long
    # word grows in linear time: string += on this closure variable was quadratic.
    out, word, value, i, n = [], None, False, 0, len(s)

    def add(text, opens_value=False):
        nonlocal word, value
        if word is None:
            word, value = [], opens_value
        if text:
            word.append(text)

    def flush():
        nonlocal word
        if word is not None:
            out.append(("e" if value else "w", "".join(word)))
            word = None

    def quoted(j, closers, escapes):
        """The text of a string opened at s[j - 1], and the index after its close. A doubled
        closing quote is one quote; in a double-quoted string a backtick escapes."""
        buf = []
        while True:
            if j >= n:
                raise Unreadable("an unclosed string")
            if escapes and s[j] == "`":
                ch, j = _escape(s, j)
                buf.append(ch)
                continue
            if s[j] in closers:
                if s[j + 1:j + 2] and s[j + 1] in closers:
                    buf.append(s[j])
                    j += 2
                    continue
                return "".join(buf), j + 1
            buf.append(s[j])
            j += 1

    while i < n:
        c = s[i]
        if c in BLANK:
            flush()
            i += 1
        elif c == "`":
            if s[i + 1:i + 2] == "\n":           # a line continuation
                flush()
                i += 2
            else:
                ch, i = _escape(s, i)
                add(ch)
        elif c == "#" and word is None:           # a comment, to the end of the line
            j = s.find("\n", i)
            i = n if j < 0 else j
        elif word is None and s.startswith("<#", i):
            j = s.find("#>", i + 2)
            if j < 0:
                raise Unreadable("an unclosed <# comment")
            i = j + 2
        elif c in SQ or c in DQ:
            opens = word is None
            text, i = quoted(i + 1, SQ if c in SQ else DQ, c in DQ)
            add(text, opens)
            if opens:                             # 'HEAD'--hard is two arguments to PowerShell
                flush()
        elif word is None and HERE_OPEN.match(s, i):
            m = HERE_OPEN.match(s, i)
            quote_char = m.group(1)
            end = re.compile("^" + re.escape(quote_char) + "@", re.M).search(s, m.end())
            if not end:
                raise Unreadable("an unclosed here-string")
            body = s[m.end():end.start()]
            if body.endswith("\n"):
                body = body[:-1]
            if quote_char == '"':
                parts, k = [], 0
                while k < len(body):
                    if body[k] == "`":
                        ch, k = _escape(body, k)
                        parts.append(ch)
                    else:
                        parts.append(body[k])
                        k += 1
                body = "".join(parts)
            out.append(("e", body))
            i = end.end()
        elif c == "$" and s[i + 1:i + 2] == "{":  # ${name} is one variable, never a script block
            j = s.find("}", i + 2)
            if j < 0:
                raise Unreadable("an unclosed ${")
            add(s[i:j + 1], True)
            i = j + 1
        elif c in "$@" and s[i + 1:i + 2] == "(":  # a subexpression runs its commands
            flush()
            out.append(("op", "("))
            i += 2
        elif c in "(){};\n":
            flush()
            out.append(("op", c))
            i += 1
        elif c == "|":
            flush()
            t = "||" if s.startswith("||", i) else "|"
            out.append(("op", t))
            i += len(t)
        elif c == "&":
            flush()
            if s.startswith("&&", i):
                out.append(("op", "&&"))
                i += 2
            else:                                 # after a word: run in the background
                out.append(("op", "&") if out and out[-1][0] in ("w", "e", "redir") else ("call", "&"))
                i += 1
        elif c in "<>" and (word is None or (len(word) == 1 and word[0] in FD_PREFIX)):
            fd, word = (word or [""])[0], None
            m = REDIRECT.match(s, i)
            out.append(("redir", fd + m.group(0)))
            i = m.end()
        elif word is None and s.startswith("--%", i) and s[i + 3:i + 4] in ("", " ", "\t", "\n"):
            # stop-parsing: the rest of the line is verbatim, up to a newline or a pipe
            j = min(k for k in (s.find("\n", i), s.find("|", i), n) if k >= 0)
            out.append(("w", "--%"))
            out.extend(("w", p) for p in s[i + 3:j].split())
            i = j
        else:
            add(c, c == "$")
            i += 1
    flush()
    return out


def quote(word):
    """One word as a POSIX-shell word."""
    if word and SAFE.fullmatch(word):
        return word
    return "'" + word.replace("'", "'\\''") + "'"


def _assignment_head(toks, j):
    """(targets, operator, index after the operator, text glued after it) when the statement that
    opens at toks[j] is an assignment, whose right side PowerShell runs; else None."""
    targets = []
    while j < len(toks) and toks[j][0] in ("w", "e"):
        text = toks[j][1]
        if targets and text in ASSIGN:
            return targets, text, j + 1, ""
        m = BARE_OP.fullmatch(text) if targets else None
        if m:
            return targets, m.group(1), j + 1, m.group(2)
        m = TARGET_OP.fullmatch(text)
        if m:
            return targets + [m.group(1)], m.group(2), j + 1, m.group(3)
        if not TARGET.fullmatch(text):
            return None
        targets.append(text)
        j += 1
    return None


def ps_to_posix(s):
    toks = ps_tokens(s)
    # command_position: the next word names a command. called: an & or . operator ran it, so even
    # a quoted word or a $variable is the command there.
    parts, command_position, called, i = [], True, False, 0
    while i < len(toks):
        kind, text = toks[i]
        i += 1
        if kind in ("w", "e"):
            if command_position and not called:
                head = _assignment_head(toks, i - 1)                           # $r = git ...
                if head:
                    targets, op, i, rest = head
                    parts += [quote(t) for t in targets] + [op, ";"]
                    if rest:
                        i -= 1
                        toks[i] = ("w", rest)
                    continue
                if kind == "w" and text == ".":                               # . git commit ...
                    called = True
                    continue
                if kind == "e":                                                # a value it prints
                    parts.append("echo")
                    command_position = False
            if command_position and GIT.fullmatch(text):
                text = "git"
            parts.append(quote(text))
            command_position = called = False
        elif kind == "call":
            command_position = called = True
        elif kind == "redir":
            parts.append(text)
        else:
            parts.append(";" if text in "(){}" else text)
            command_position, called = True, False
    return " ".join(parts)


def bash_to_posix(s):
    """Bash text with a git named by a path, in capitals or in quotes renamed `git` where it is the
    command; every other character is left as it is."""
    def fix(m):
        word = m.group(2)
        bare = word[1:-1] if word[:1] in "\"'" else word
        return m.group(1) + "git" if word != "git" and GIT.fullmatch(bare) else m.group(0)
    return BASH_GIT.sub(fix, s)


def to_posix(cmd, tool):
    if tool == "PowerShell":
        try:
            return ps_to_posix(cmd)
        except Unreadable:
            return cmd
    return bash_to_posix(cmd)


def segments(text, esc="\\"):
    """Text split at ; & | and newlines outside quotes, one segment per line. A newline inside
    quotes becomes a space, an escaped newline joins its lines, and a # that starts a word outside
    quotes drops the rest of its line. esc is the shell's escape: \\ for POSIX text, a backtick for
    a raw PowerShell command."""
    text = text.replace("\r", "")
    out, q, i, n = [], "", 0, len(text)
    while i < n:
        c = text[i]
        if c == esc and q != "'":
            nxt = text[i + 1:i + 2]
            out.append(" " if nxt == "\n" else c + nxt)
            i += 2
            continue
        if q:
            if c == q:
                q = ""
            out.append(" " if c == "\n" else c)
        elif c in "\"'":
            q = c
            out.append(c)
        elif c == "#" and (i == 0 or text[i - 1] in " \t\n;&|()"):
            j = text.find("\n", i)
            i = n if j < 0 else j
            continue
        elif c in ";&|\n":
            out.append("\n")
        else:
            out.append(c)
        i += 1
    return "".join(out)


# A quote anywhere before a # means the # may be quoted text, so the quote-blind split never drops
# what follows it. Plan 027 review: `git -c "user.name=a #b" push --force ...` after a heredoc line
# holding one ", and a typographic quote, both hid the push. Its convergence: judging only the
# piece was not enough, since the blind split cuts inside a quoted value (`X="a;b #c" git push`),
# and the piece holding the push then starts at the # with its opening quote in the piece before.
QUOTE = re.compile("['\"`\u2018-\u201e]")
WORD_HASH = re.compile(r"(?:^|(?<=[ \t]))#")


def _blind_pieces(text):
    """text cut at every ; & | and newline whatever the quotes, a # that starts a word dropped with
    the rest of its piece only when no quote occurs anywhere in text before it. Linear."""
    q = QUOTE.search(text)
    limit = q.start() if q else len(text)
    out, start = [], 0
    for piece in re.split(r"[;&|\n]", text):
        m = WORD_HASH.search(piece) if start < limit else None
        out.append(piece[:m.start()] if m and start + m.start() < limit else piece)
        start += len(piece) + 1
    return out


# Only a segment this short is re-split with argument boundaries: shlex builds each word one
# character at a time, quadratic in its length (400 KB of quoted text holding `push` took 1.7 s of
# the 5 s registration validate-push then had, and a killed gate fails open). The shapes the pass
# exists for, -o "" and -o "ci skip", are short, and without it the positionals are still judged
# unskipped.
WORDS_KEPT_MAX = 4096


def _words_kept(seg):
    """One quote-aware segment with each argument's boundary kept: a blank inside an argument
    becomes \\x1f, and an empty argument \\x1e, so `-o "ci skip" origin` and `-o "" origin` still
    hand validate-push the value as one word. None when it does not parse as POSIX words or is
    longer than WORDS_KEPT_MAX."""
    if len(seg) > WORDS_KEPT_MAX:
        return None
    try:
        words = shlex.split(seg, comments=False, posix=True)
    except ValueError:
        return None
    return " ".join(re.sub(r"[\s'\"]", "\x1f", w) or "\x1e" for w in words)


def push_candidates(cmd, tool):
    """validate-push.sh's candidate lines, only those holding `push`, each once:
    1. the posix text and the raw command, cut at every ; & | and newline whatever the quotes, a #
       comment dropped only where no quote comes anywhere before it (bash -c "git push ..." stays
       judged);
    2. the posix text split outside quotes (a separator inside a quoted value keeps git and push
       together), and each such segment up to WORDS_KEPT_MAX again with its argument boundaries
       kept;
    3. the raw command split outside quotes with the tool's own escape, the split validate-push ran
       before plan 027, so reading PowerShell never loses a push the raw text showed.
    In the order they are produced, never sorted: the judge stops at the first refused line, so the
    order only decides which target a message names, and it answers inside its deadline in any
    order (#680)."""
    def unfold(t):
        return t.replace("\r", "").replace("\\\n", " ").replace("`\n", " ")
    posix = to_posix(cmd, tool)
    texts = [unfold(posix)]
    if unfold(cmd) != texts[0]:
        texts.append(unfold(cmd))
    lines = [p for t in texts for p in _blind_pieces(t)]
    quoted = segments(posix).split("\n")
    lines += quoted
    lines += [w for w in (_words_kept(q) for q in quoted if "push" in q) if w]
    lines += segments(cmd, "`" if tool == "PowerShell" else "\\").split("\n")
    seen, keep = set(), []
    for line in lines:
        if "push" in line and line not in seen:
            seen.add(line)
            keep.append(line)
    return keep


# ---- git invocations and the repository each runs in (2026-10-16) -------------------------------
# Moved here from check-commit-subject-version.sh, so every commit gate reads a command one way. The
# command is read the way bash reads it, then each `git ... <subcommand>`'s own options are picked out.
# A regex over the raw string took option-shaped prose inside a message for a real option, missed
# $'...' quoting, attached -m"..." and concatenated words, and let a -c in a later command switch the
# label check off (Codex review 2026-09-23, #647).

HEREDOC = re.compile(r"<<-?\s*(['\"]?)(\w+)\1[ \t]*\r?\n(.*?)\r?\n[ \t]*\2\b", re.S)
CMD_SEP = set(";|&\n()")
GIT_OPTS_WITH_VALUE = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--exec-path",
                       "--config-env"}
# Commands that change the shell's directory: bash's, and PowerShell's as to_posix() leaves them. A push
# saves the directory it leaves and a pop returns to it (Codex review 2026-10-16: a pushd with its popd
# ignored judged the pushed directory).
CHDIR = {"cd", "chdir", "set-location", "sl"}
PUSHD = {"pushd", "push-location"}
POPD = {"popd", "pop-location"}
# Wrappers that run their command in a child process, whose cd leaves the shell's directory alone. `command`
# and `time` run a builtin in the shell itself, so a cd behind them still moves.
CHILD_WRAPPERS = {"env", "timeout", "nice", "nohup", "xargs", "stdbuf"}


def strip_heredocs(cmd):
    """(text, bodies): every heredoc body taken out and replaced by a placeholder. A body is the
    message of `-F -` or of `-m "$(cat <<EOF ...)"`, and its apostrophes would otherwise unbalance
    the tokenizer."""
    bodies = []

    def stash(m):
        bodies.append(m.group(3))
        return "<< __TAOM_HEREDOC_%d__" % (len(bodies) - 1)
    return HEREDOC.sub(stash, cmd), bodies


def decode_ansi_c(text):
    """Bash ANSI-C quoting, $'...', decoded and re-quoted so the tokenizer sees what git gets."""
    def one(m):
        try:
            s = codecs.decode(m.group(1).encode("latin-1", "backslashreplace"), "unicode_escape")
        except Exception:
            s = m.group(1)
        return shlex.quote(s)
    return re.sub(r"\$'((?:[^'\\]|\\.)*)'", one, text)


def expand_heredoc(value, bodies):
    """A value naming a heredoc placeholder is that heredoc's body (the $(cat <<EOF) form)."""
    hm = re.search(r"__TAOM_HEREDOC_(\d+)__", value)
    return bodies[int(hm.group(1))] if hm else value


# ---- the command of a segment, past wrapper words and shell grammar (#985) ----------------------
# A gate that finds a segment's command by its first word misses `timeout 60 git reset --hard`,
# `{ git reset --hard; }` and `! git ...`: command_index skips what runs nothing itself, so every
# reader (the commit gates through _git_call, and the `commands` mode the confirm gates and
# mark-verification-run.sh read) finds the same word. The idea comes from rtk-ai/rtk's permission
# matcher (docs/reference/provenance-register.md); no rtk code is used.
GRAMMAR ={"{", "!", "if", "then", "do", "else", "elif", "while", "until"}
ASSIGNMENT = re.compile(r"[A-Za-z_][A-Za-z0-9_]*\+?=")
PEEL_WORDS = 32
# Wrapper -> (short options that take a value, long options that take a value). A short option is
# complete when glued (-k5); a long option when it carries `=`. xargs -i -l -e and their long forms
# --replace --max-lines --eof take a glued value only (GNU xargs --help), so none is listed.
WRAPPERS = {
    "env": ("uC", ("--unset", "--chdir")),
    "timeout": ("ks", ("--kill-after", "--signal")),
    "command": ("", ()),
    "time": ("", ()),
    "nice": ("n", ("--adjustment",)),
    "nohup": ("", ()),
    "xargs": ("ILnPsEda", ("--arg-file", "--delimiter", "--max-args", "--max-procs", "--max-chars",
                           "--process-slot-var")),
    "stdbuf": ("ioe", ("--input", "--output", "--error")),
}


def _takes_value(wrapper, word):
    shorts, longs = WRAPPERS[wrapper]
    if word.startswith("--"):
        name, eq, _ = word.partition("=")
        return not eq and len(name) >= 3 and any(l.startswith(name) for l in longs)
    for n, ch in enumerate(word[1:], 1):          # a bundle: the first value letter takes the rest of the word
        if ch in shorts:                          # as its value, or the next word when it is the last letter
            return n == len(word) - 1
    return False


def _skip_options(wrapper, words, i):
    """The index after a wrapper's options (and, for timeout, its duration), or None where the
    wrapper runs text rather than a command: command -v and -V, env -S."""
    n = len(words)
    while i < n:
        w = words[i]
        if w == "--":
            i += 1
            break
        if not w.startswith("-"):
            break
        name = w.partition("=")[0]
        if wrapper == "command" and re.fullmatch(r"-[pvV]+", w) and re.search("[vV]", w):
            return None
        if wrapper == "env" and (w.startswith("-S") or (len(name) >= 3 and "--split-string".startswith(name))):
            return None
        i += 2 if _takes_value(wrapper, w) else 1
    return min(i + 1 if wrapper == "timeout" else i, n)


def command_index(words, tool):
    """The index of the word that names the command: past grammar words (`{ ! if then do else elif
    while until`, a glued leading `(`), assignments (NAME=, NAME+=) and, for the Bash tool only, the
    wrappers in WRAPPERS with their options. PowerShell's `timeout` is the Windows timeout.exe, which
    runs no command, so there only grammar and assignments are skipped. len(words) when none is left."""
    i, n = 0, len(words)
    while i < n:
        w = words[i].lstrip("(")
        if not w or w in GRAMMAR or ASSIGNMENT.match(w):
            i += 1
        elif tool == "Bash" and w in WRAPPERS:
            j = _skip_options(w, words, i + 1)
            if j is None:
                return i
            i = j
        else:
            return i
    return n


_PLAIN = re.compile(r"[^ \t'\"\\]+")
_DQ_END = re.compile(r'["\\]')
DQ_ESCAPE = re.compile(r'\\([\\"$`])')          # the escapes bash honours inside double quotes


def _lead_words(seg, limit):
    """(start, end, text) of the first `limit` words of a segment read as bash words: quotes and
    backslashes taken out of text, a quote left open running to the end. Linear in what it reads."""
    out, i, n = [], 0, len(seg)
    while len(out) < limit:
        while i < n and seg[i] in " \t":
            i += 1
        if i >= n:
            break
        start, buf = i, []
        while i < n and seg[i] not in " \t":
            c = seg[i]
            if c == "'":
                j = seg.find("'", i + 1)
                j = n if j < 0 else j
                buf.append(seg[i + 1:j])
                i = j + 1
            elif c == '"':
                j = i + 1
                while True:
                    m = _DQ_END.search(seg, j)
                    if not m:
                        j = n
                        break
                    if m.group() == '"':
                        j = m.start()
                        break
                    j = m.end() + 1
                inner = seg[i + 1:j]
                buf.append(DQ_ESCAPE.sub(r"\1", inner) if "\\" in inner else inner)
                i = j + 1
            elif c == "\\":
                buf.append(seg[i + 1:i + 2])
                i += 2
            else:
                m = _PLAIN.match(seg, i)
                buf.append(m.group())
                i = m.end()
        out.append((start, min(i, n), "".join(buf)))
    return out


def _trim_parens(text):
    """text without the unquoted `)` that close a subshell and the blanks before them: `(git add -A)`
    must end `-A`, not `-A)`. A quoted ")" stays."""
    t = text.rstrip()
    if not t.endswith(")"):
        return text
    q, closers, i = "", set(), 0
    while i < len(t):
        c = t[i]
        if c == "\\" and q != "'":
            i += 2
            continue
        if q:
            q = "" if c == q else q
        elif c in "\"'":
            q = c
        elif c == ")":
            closers.add(i)
        i += 1
    j = len(t)
    while j > 0 and (t[j - 1] in " \t" or (t[j - 1] == ")" and j - 1 in closers)):
        j -= 1
    return t[:j]


def peel(seg, tool):
    """The segment from its command word on, with that word written `git` when it names git (`GIT`,
    a path, quotes). Only the first PEEL_WORDS words are looked at; with no command word among them
    the segment comes back unchanged."""
    words = _lead_words(seg, PEEL_WORDS)
    k = command_index([w[2] for w in words], tool)
    if k >= len(words):
        return seg
    start, end, text = words[k]
    while start < end and seg[start] == "(":
        start += 1
    if GIT.fullmatch(text.lstrip("(")):
        return _trim_parens("git" + seg[end:])
    return _trim_parens(seg[start:])


PLACEHOLDER = re.compile(r"__TAOM_HEREDOC_(\d+)__")
SHORT_C = re.compile(r"-[A-Za-z]*c[A-Za-z]*")
# Where the sed split the confirm gates ran before #985 cut a line: && || ; | and the newline. Not a lone &.
GATE_CUT = re.compile(r"&&|\|\||;|\||\n")
SCRIPT_DEPTH = 2          # how many bash -c scripts and shell heredocs deep commands() reads, as _git_call does


SHELL_VALUE_OPTIONS = {"-o", "-O", "+o", "+O", "--rcfile", "--init-file"}
SHORT_N = re.compile(r"-[A-Za-z]*n[A-Za-z]*")
SHORT_S = re.compile(r"-[A-Za-z]*s[A-Za-z]*")
REDIRECT_WORD = re.compile(r"\d*[<>]|&>")


def _shell_scan(words):
    """(script index, reads stdin) for a shell command line, words[0] the shell: the index of the script
    given to `-c`, or to a short bundle holding c (-lc, -ec), a `--` right after it skipped; and whether
    a heredoc on its stdin is run, which it is with neither -c nor an operand, or with -s. The scan stops
    at the first operand (`bash script.sh -c x` runs the file) and skips the values of -o, -O, +o, --rcfile
    and --init-file. A short bundle holding n (`bash -n`) runs nothing: (None, False)."""
    i = 1
    while i < len(words):
        w = words[i]
        if SHORT_N.fullmatch(w):
            return None, False
        if SHORT_C.fullmatch(w):
            j = i + 1 + (1 if words[i + 1:i + 2] == ["--"] else 0)
            return (j if j < len(words) else None), False
        if SHORT_S.fullmatch(w):
            return None, True
        if w in SHELL_VALUE_OPTIONS:
            i += 2
        elif w.startswith("-") and w != "--" or w.startswith("+"):
            i += 1
        elif REDIRECT_WORD.match(w):        # `bash << EOF`, `bash > out`: a redirection is no operand
            return None, True
        else:
            return None, False
    return None, True


def shell_script_index(words):
    """The index in words (words[0] is bash, sh or zsh) of the script given to -c, else None (_shell_scan)."""
    return _shell_scan(words)[0]


def _is_shell(word):
    """Whether a command word names bash, sh or zsh (a path, with or without .exe)."""
    name = os.path.basename(word).lower()
    return (name[:-4] if name.endswith(".exe") else name) in ("bash", "sh", "zsh")


def _runs_a_shell(words):
    """Whether the command word of these (start, end, text) words is a shell."""
    return bool(words) and _is_shell(words[0][2])


def _env_split_string(names):
    """The string given to `env -S` / `--split-string` (env runs it as a command line), or None. names[0]
    is env."""
    i = 1
    while i < len(names) and names[i].startswith("-") and names[i] != "--":
        w = names[i]
        if w.startswith("--"):
            name, eq, val = w.partition("=")
            if len(name) >= 3 and "--split-string".startswith(name):
                return val if eq else (names[i + 1] if i + 1 < len(names) else None)
        else:
            for n, ch in enumerate(w[1:], 1):
                if ch == "S":
                    return w[n + 1:] or (names[i + 1] if i + 1 < len(names) else None)
                if ch in "uC":
                    break
        i += 2 if _takes_value("env", w) else 1
    return None


# A command with more `<<` openers than this, or a text over REPO_SCAN_MAX holding one, is not stripped of
# heredocs: strip_heredocs scans to the end of the text for each opener that has no closing line, so
# `python -c` with 20,000 `x = 1 << 5` lines took 30 s (a killed gate fails open). 64 openers over 256 KB
# measured under 0.4 s.
HEREDOC_MAX_OPENERS = 64


def commands(cmd, tool, blind=False, depth=0, inherited=None):
    """One peeled segment per line, each from its command word on, in first-seen order and once each:
    1. the command as POSIX text with heredocs taken out and $'...' words read, split outside quotes;
    2. the script of a `bash|sh|zsh -c` segment, and the body of a heredoc such a shell reads
       (`bash <<EOF`), read the same way, two levels deep. A heredoc another command reads (cat,
       python, `git commit -F -`, a `$(cat <<EOF)` message) is data and adds nothing, so a body line
       that starts `dotnet test` never marks;
    3. blind only: the pieces of the text with its heredocs inline cut exactly where the sed split the
       confirm gates used before #985 cut a line (&&, ||, ;, | and the newline, whatever the quotes; not a
       lone &). An extra confirm is the safe direction there (lesson #947), so nothing the old split
       caught goes unasked: a quoted `;`, a heredoc whose body holds an apostrophe, text the quote-aware
       split reads as data.
    A multi-line `bash -c '...'` script reaches step 1 with its newlines collapsed to spaces, so only its
    first command is read there; that fails in the safe direction (the blind pass still sees every line
    for the confirm gates, and the marker at worst nags once).
    mark-verification-run.sh reads the plain form (a mention must never mark), the two confirm gates
    `commands --blind`. Only the first PEEL_WORDS words of a segment are looked at.
    A heredoc placeholder inside a `-c` script is judged by the command of the script's own segment, never
    by the outer shell. Past HEREDOC_MAX_OPENERS openers, or REPO_SCAN_MAX characters with one, the
    heredocs are not stripped: the blind pass still reads every piece, and the plain form reads nothing
    after the first line holding an opener, so a body it cannot tell from commands never marks."""
    posix = to_posix(cmd, tool)
    if inherited is not None:               # a -c script: its heredocs were taken out with the whole command
        text, bodies = posix, inherited
    elif posix.count("<<") > HEREDOC_MAX_OPENERS or (len(posix) > REPO_SCAN_MAX and "<<" in posix):
        text, bodies = posix, []
        if not blind:
            nl = posix.find("\n", posix.find("<<"))
            text = posix if nl < 0 else posix[:nl]
    else:
        text, bodies = strip_heredocs(posix)
    out, seen = [], set()

    def add(line):
        if line.strip() and line not in seen:
            seen.add(line)
            out.append(line)

    def nested(script, scripts=False):
        for line in commands(script, "Bash", blind, depth + 1, bodies if scripts else None).split("\n"):
            add(line)

    for seg in segments(decode_ansi_c(text)).split("\n"):
        peeled = peel(seg, tool)
        add(peeled)
        words = _lead_words(peeled, PEEL_WORDS) if depth < SCRIPT_DEPTH else []
        names = [w[2] for w in words]
        if words and names[0] == "env":
            script = _env_split_string(names)
            if script is not None:
                nested(script, True)
            continue
        if not _runs_a_shell(words):
            continue
        k, reads_stdin = _shell_scan(names)
        if k is not None:
            nested(names[k], True)      # with -c the shell reads the script, never its stdin
            continue
        if not reads_stdin:             # `bash tools/foo.sh <<EOF` feeds the script; `bash -n` runs nothing
            continue
        for m in PLACEHOLDER.finditer(peeled):
            if int(m.group(1)) < len(bodies):
                nested(bodies[int(m.group(1))])
    if blind:
        for piece in GATE_CUT.split(posix):
            add(peel(piece, tool))
    return "\n".join(out)


def command_tokens(s, partial=False):
    """The words and separators of POSIX text; an unquoted newline separates commands, as in bash.
    Quoting bash itself would reject raises ValueError, unless partial: then the words read before
    the fault come back."""
    lx = shlex.shlex(s, posix=True, punctuation_chars="();<>|&\n")
    lx.whitespace = " \t\r"
    lx.whitespace_split = True
    if not partial:
        return list(lx)
    out = []
    try:
        for t in lx:
            out.append(t)
    except ValueError:
        pass
    return out


def piped_text(group):
    """What a command that only prints one literal word hands the next command through a pipe:
    echo or Write-Output with one word (the reader writes a PowerShell string or here-string that
    stands alone as `echo <word>`), or printf with the format %s or %s\\n and one word. None for
    anything else, so the commit is judged as it was before plan 027: a lone word is a command
    (pbpaste), a word holding $ is a variable nobody here can read, and any other printf format
    changes the text (deep review of plan 027)."""
    name = os.path.basename(group[0]).lower() if group else ""
    if len(group) == 2 and name in ("echo", "write-output"):
        word = group[1]
    elif len(group) == 3 and name == "printf" and group[1] in ("%s", "%s\\n"):
        word = group[2]
    else:
        return None
    return None if "$" in word else word


def native_path(p):
    """A Git Bash drive path (/e/repos/x) as a Windows path this Python and bash both open (E:/repos/x)."""
    m = re.match(r"^/([A-Za-z])(/|$)", p)
    return (m.group(1).upper() + ":/" + p[m.end():]) if m else p


def join_dir(base, p):
    """p as the shell would reach it from base: an absolute p stands alone; a relative one joins
    base with a forward slash, which Git Bash reads whatever separators base holds."""
    p = native_path(p)
    if os.path.isabs(p) or re.match(r"^[A-Za-z]:", p) or not base:
        return p
    return base.rstrip("/\\") + "/" + p


def _chdir_target(group):
    """The directory a cd, pushd or Set-Location group moves to, or None (no argument, or `cd -`,
    the previous directory, which nothing here knows)."""
    words = group[1:]
    for i, word in enumerate(words):
        if word == "-":
            return None
        if word == "--":
            return words[i + 1] if i + 1 < len(words) else None
        if not word.startswith("-"):      # -L, -P, -LiteralPath, -Path are skipped
            return word
    return None


def git_invocations(text, bodies, subcommand="commit", base=None, partial=False, depth=0):
    """[(args, repo)] for every `git ... <subcommand>` in POSIX text whose heredocs strip_heredocs
    took out: args is the argument list after the subcommand, repo the directory the call runs in.
    That is base, moved by each cd before the call (a cd inside ( ) ends at its ), and then by the
    call's own -C. A commit whose stdin is piped from a command that only prints one word (a
    PowerShell here-string piped to `git commit -F -`, plan 027) gets that word first as a heredoc
    placeholder, so `-F -` reads it. Raises ValueError on quoting bash would reject, unless partial."""
    found = []
    group, prev, sep = [], [], ""
    cwd, stack, pushed = base, [], []
    for t in command_tokens(text, partial) + [";"]:
        if t and set(t) <= CMD_SEP:
            if group:
                k = command_index(group, "Bash")
                head = group[k:] or group                       # `{ cd x; ...` moves too
                name = os.path.basename(head[0]).lower()
                if any(w.lstrip("(") in CHILD_WRAPPERS for w in group[:k]):
                    pass                                        # `timeout 5 cd x` changes a child's directory only
                elif name in CHDIR or name in PUSHD:
                    target = _chdir_target(head)
                    if target is not None:
                        if name in PUSHD:
                            pushed.append(cwd)
                        cwd = join_dir(cwd, target)
                elif name in POPD and pushed:
                    cwd = pushed.pop()
                calls = _git_call(group, bodies, subcommand, cwd, partial, depth)
                piped = piped_text(prev) if sep == "|" else None
                if piped is not None:
                    bodies.append(piped)
                    mark = ["<<", "__TAOM_HEREDOC_%d__" % (len(bodies) - 1)]
                    calls = [(mark + args, repo) for args, repo in calls]
                found.extend(calls)
            # shlex hands a run of punctuation back as one token (`);`, `)&&`, `))`), so every
            # parenthesis in it opens or closes a subshell's directory scope (Codex review 2026-10-16).
            for ch in t:
                if ch == "(":
                    stack.append(cwd)
                elif ch == ")" and stack:
                    cwd = stack.pop()
            prev, group, sep = group, [], t
        else:
            group.append(t)
    return found


def _env_dirs(prefix, k):
    """The directories named by the env options that start at prefix[k]: -C DIR, -CDIR, a bundle ending
    in C (-iC DIR), --chdir DIR, --chdir=DIR and any unambiguous prefix of --chdir (--ch)."""
    dirs = []
    while k < len(prefix) and prefix[k].startswith("-") and prefix[k] != "--":
        w = prefix[k]
        if w.startswith("--"):
            name, eq, val = w.partition("=")
            if len(name) >= 3 and "--chdir".startswith(name):
                if eq:
                    dirs.append(val)
                elif k + 1 < len(prefix):
                    dirs.append(prefix[k + 1])
        else:
            for n, ch in enumerate(w[1:], 1):
                if ch in "uC":
                    if ch == "C":
                        value = w[n + 1:]
                        if value:
                            dirs.append(value)
                        elif k + 1 < len(prefix):
                            dirs.append(prefix[k + 1])
                    break
        k += 2 if _takes_value("env", w) else 1
    return dirs


def _env_chdir(prefix, cwd):
    """cwd moved by each directory an `env` among the words command_index skipped names (_env_dirs),
    the way git -C moves it."""
    for k, w in enumerate(prefix):
        if w == "env":
            for d in _env_dirs(prefix, k + 1):
                cwd = join_dir(cwd, d)
    return cwd


def _git_call(g, bodies, subcommand, cwd, partial, depth):
    # Bash's wrappers and grammar are skipped whatever the tool: the commit gates cannot tell, and
    # a PowerShell `timeout 60 git commit` runs no commit, so judging it only over-blocks (#985).
    i = command_index(g, "Bash")
    if i >= len(g):
        return []
    if "xargs" in g[:i]:      # xargs builds the argument list at run time: the text holds no real message
        return []
    cwd = _env_chdir(g[:i], cwd)
    word = os.path.basename(g[i]).lower()
    if _is_shell(word) and depth < 2:
        k = shell_script_index(g[i:])
        # The script's heredocs were taken out with the whole command, so its placeholders index bodies.
        return git_invocations(g[i + k], bodies, subcommand, cwd, partial, depth + 1) if k is not None else []
    if word not in ("git", "git.exe"):
        return []
    i += 1
    repo = cwd
    while i < len(g) and g[i].startswith("-"):
        if g[i] == "-C" and i + 1 < len(g):
            repo = join_dir(repo, g[i + 1])
        i += 2 if g[i] in GIT_OPTS_WITH_VALUE else 1
    if i < len(g) and g[i] == subcommand:
        return [(g[i + 1:], repo)]
    return []


# The longest command text (heredoc bodies already out) git_repo_candidates tokenizes. shlex builds
# each word one character at a time, quadratic in its length: one 1 MB quoted message took 6.5 s and
# pushed three gates past their registrations, and a killed gate fails open (deep review 2026-10-16).
# About 0.4 s at the cap; a longer command is judged in the payload's cwd.
REPO_SCAN_MAX = 262144


def git_call_dirs(cmd, tool, cwd, subcommand="commit"):
    """The directory of every `git <subcommand>` in this tool call, in order and each once: the call's
    own cd or -C joined onto the payload's cwd, or the cwd itself when it names none ("" when neither
    is known). The caller resolves each to its work tree's top level; more than one work tree is a
    command a gate cannot judge as one repository (Codex review 2026-10-16: an earlier commit in one
    repository went unchecked behind a later one in another). Past REPO_SCAN_MAX no call is read."""
    text, bodies = strip_heredocs(to_posix(cmd, tool))
    dirs = []
    if len(text) <= REPO_SCAN_MAX:
        for _args, repo in git_invocations(decode_ansi_c(text), bodies, subcommand, cwd or None, partial=True):
            d = repo or ""
            if d not in dirs:
                dirs.append(d)
    return dirs


def read_payload(raw):
    """(tool_name, tool_input.command) of a hook payload, empty strings where it holds none; None when
    it is no JSON object."""
    try:
        d = json.loads(raw)
    except ValueError:
        return None
    if not isinstance(d, dict):
        return None
    ti = d.get("tool_input")
    cmd = ti.get("command") if isinstance(ti, dict) else None
    return str(d.get("tool_name") or ""), cmd if isinstance(cmd, str) else ""


def read_cwd(raw):
    """The payload's cwd: the session's directory, the worktree root once it has entered one."""
    try:
        d = json.loads(raw)
    except ValueError:
        return ""
    return str(d.get("cwd") or "") if isinstance(d, dict) else ""


def main(argv):
    mode = argv[1] if len(argv) > 1 else ""
    if mode not in ("posix", "commands", "verdict", "git-repo"):
        sys.stderr.write("usage: _shellwords.py posix|commands [--blind]|verdict [protected...]|git-repo [subcommand] < payload.json\n")
        return 2
    raw = sys.stdin.buffer.read().decode("utf-8", "replace")
    payload = read_payload(raw)
    tool, cmd = payload or ("", "")
    if mode == "git-repo":
        cwd = read_cwd(raw)
        try:
            dirs = git_call_dirs(cmd, tool, cwd, argv[2] if len(argv) > 2 else "commit")
        except Exception:                    # never worse than the cwd alone
            dirs = []
        text = "\n".join([d.replace("\n", "") for d in dirs] + ["--", cwd.replace("\n", "")])
    elif mode == "verdict" and payload is None:
        text = "unread"
    elif mode == "verdict":
        sys.dont_write_bytecode = True       # no __pycache__ folder inside .claude/hooks
        import _pushjudge
        kind, target = _pushjudge.Judge(argv[2:]).run(push_candidates(cmd, tool))
        text = kind + "\t" + target if target else kind
    elif mode == "commands":
        text = commands(cmd, tool, "--blind" in argv[2:])
    else:
        text = to_posix(cmd, tool)
    sys.stdout.buffer.write((text + "\n").encode("utf-8", "replace"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
