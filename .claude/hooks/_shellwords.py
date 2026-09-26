"""The command a Bash or PowerShell tool call runs, read the way the TAOM gates need it (plan 027).

Every PreToolUse git gate was written for Bash text. The PowerShell tool sends PowerShell text, so
to_posix() rewrites it as the POSIX-shell (Bash) text of the same command, and each gate keeps its
Bash logic. In both shells a git named by a path or in capitals (GIT, git.exe, a Windows path to
git.exe) becomes `git` where it is the command.

Usage: <python> _shellwords.py posix|segments|push < the hook payload (JSON)
  posix     the command as POSIX-shell text
  segments  the posix text split at ; & | and newlines outside quotes, one segment per line, with a
            # comment dropped (mark-verification-run.sh)
  push      validate-push.sh's candidate lines: every split it judges, only the lines holding `push`
It writes UTF-8 with LF line ends and exits 0; an unknown mode exits 2. PowerShell text it cannot
follow (an unclosed quote, here-string, block comment or ${) comes back unchanged, which is how
every gate read a command before plan 027.

Not read, by design: a $(...) inside a double-quoted PowerShell string stays text, a $variable is
never expanded, and PowerShell's comma array syntax stays inside one word. A string or variable that
opens a PowerShell statement is a value it prints, never a command, so it reads as `echo <value>`.
"""
import json
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
GLUED_ASSIGN = re.compile(r"(\$[\w:?]+|\$\{[^}]*\})(\?\?=|[-+*/%]?=)(.*)", re.S)
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
                nxt = toks[i] if i < len(toks) else ("", "")
                if text[:1] and text[0] in "$[" and nxt[0] == "w" and nxt[1] in ASSIGN:  # $r = git ...
                    parts += [quote(text), nxt[1], ";"]
                    i += 1
                    continue
                m = GLUED_ASSIGN.fullmatch(text)
                if m:                                                          # $r=git commit ...
                    parts += [quote(m.group(1)), m.group(2), ";"]
                    if m.group(3):
                        i -= 1
                        toks[i] = ("w", m.group(3))
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


# A quote before a # means the # may be quoted text, so the quote-blind split never drops what
# follows it (plan 027 review: `git -c "user.name=a #b" push --force ...` after a heredoc line
# holding one ", and a typographic quote, both hid the push).
FIRST_QUOTE_OR_HASH = re.compile("['\"`\u2018-\u201e#]")


def _drop_comment(piece):
    m = FIRST_QUOTE_OR_HASH.search(piece)
    if m and m.group() == "#" and (m.start() == 0 or piece[m.start() - 1] in " \t"):
        return piece[:m.start()]
    return piece


def _words_kept(seg):
    """One quote-aware segment with each argument's boundary kept: a blank inside an argument
    becomes \\x1f, and an empty argument \\x1e, so `-o "ci skip" origin` and `-o "" origin` still
    hand validate-push the value as one word. None when it does not parse as POSIX words."""
    try:
        words = shlex.split(seg, comments=False, posix=True)
    except ValueError:
        return None
    return " ".join(re.sub(r"[\s'\"]", "\x1f", w) or "\x1e" for w in words)


def push_lines(cmd, tool):
    """validate-push.sh's candidate lines, only those holding `push`, each once:
    1. the posix text and the raw command, cut at every ; & | and newline whatever the quotes, a #
       comment dropped only where no quote comes before it (bash -c "git push ..." stays judged);
    2. the posix text split outside quotes (a separator inside a quoted value keeps git and push
       together), and each such segment again with its argument boundaries kept;
    3. the raw command split outside quotes with the tool's own escape, the split validate-push ran
       before plan 027, so reading PowerShell never loses a push the raw text showed."""
    def unfold(t):
        return t.replace("\r", "").replace("\\\n", " ").replace("`\n", " ")
    posix = to_posix(cmd, tool)
    texts = [unfold(posix)]
    if unfold(cmd) != texts[0]:
        texts.append(unfold(cmd))
    lines = [_drop_comment(p) for t in texts for p in re.split(r"[;&|\n]", t)]
    quoted = segments(posix).split("\n")
    lines += quoted
    lines += [w for w in (_words_kept(q) for q in quoted if "push" in q) if w]
    lines += segments(cmd, "`" if tool == "PowerShell" else "\\").split("\n")
    seen, keep = set(), []
    for line in lines:
        if "push" in line and line not in seen:
            seen.add(line)
            keep.append(line)
    return "\n".join(keep)


def read_payload(raw):
    """(tool_name, tool_input.command) of a hook payload; empty strings when it does not parse."""
    try:
        d = json.loads(raw)
    except ValueError:
        return "", ""
    if not isinstance(d, dict):
        return "", ""
    ti = d.get("tool_input")
    cmd = ti.get("command") if isinstance(ti, dict) else None
    return str(d.get("tool_name") or ""), cmd if isinstance(cmd, str) else ""


def main(argv):
    mode = argv[1] if len(argv) > 1 else ""
    if mode not in ("posix", "segments", "push"):
        sys.stderr.write("usage: _shellwords.py posix|segments|push < payload.json\n")
        return 2
    tool, cmd = read_payload(sys.stdin.buffer.read().decode("utf-8", "replace"))
    if mode == "push":
        text = push_lines(cmd, tool)
    else:
        text = to_posix(cmd, tool)
        if mode == "segments":
            text = segments(text)
    sys.stdout.buffer.write((text + "\n").encode("utf-8", "replace"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
