"""The command a Bash or PowerShell tool call runs, read the way the TAOM gates need it (plan 027).

Every PreToolUse git gate was written for Bash text. The PowerShell tool sends PowerShell text, so
to_posix() rewrites it as the POSIX-shell (Bash) text of the same command, and each gate keeps its
Bash logic. In both shells a git named by a path or in capitals (GIT, git.exe, a Windows path to
git.exe) becomes `git` where it is the command.

Usage: <python> _shellwords.py posix|segments < the hook payload (JSON)
  posix     the command as POSIX-shell text
  segments  the posix text split at ; & | and newlines outside quotes, one segment per line, with a
            # comment dropped (validate-push.sh and mark-verification-run.sh)
It writes UTF-8 with LF line ends and exits 0; an unknown mode exits 2. PowerShell text it cannot
follow (an unclosed quote, here-string or block comment) comes back unchanged, which is how every
gate read a command before plan 027.

Not read, by design: a $(...) inside a double-quoted PowerShell string stays text, a $variable is
never expanded, and PowerShell's comma array syntax stays inside one word.
"""
import json
import re
import sys

GIT = re.compile(r"(?:.*[\\/])?git(?:\.exe)?", re.I)
SAFE = re.compile(r"[\w@%+=:,./^~-]+")
PS_ESCAPES = {"0": "", "a": "\a", "b": "\b", "e": "\x1b", "f": "\f", "n": "\n", "r": "\r",
              "t": "\t", "v": "\v"}
HERE_OPEN = re.compile(r"@(['\"])[ \t]*\n")
REDIRECT = re.compile(r"[<>]{1,2}(?:&[12])?")
FD_PREFIX = {"*", "1", "2", "3", "4", "5", "6"}
BASH_GIT = re.compile(r"""((?:^|[;&|(){}`\n])[ \t]*|\$\([ \t]*)("[^"\n]*"|'[^'\n]*'|[^\s;&|(){}'"`<>]+)""")


class Unreadable(ValueError):
    """PowerShell text this reader cannot follow."""


def _escape(s, i):
    """The text a PowerShell backtick at s[i] stands for, and the index after it."""
    nxt = s[i + 1:i + 2]
    if nxt == "u" and s[i + 2:i + 3] == "{":
        end = s.find("}", i + 3)
        if end > 0:
            try:
                return chr(int(s[i + 3:end], 16)), end + 1
            except ValueError:
                pass
    return PS_ESCAPES.get(nxt, nxt), i + 2


def ps_tokens(s):
    """PowerShell source as (kind, text) pairs: w a word, op a separator, call the & call
    operator, redir a redirection operator."""
    s = s.replace("\r", "")
    # word is None between words, else the current word's non-empty pieces. A list, so a long
    # word grows in linear time: string += on this closure variable was quadratic.
    out, word, i, n = [], None, 0, len(s)

    def add(text):
        nonlocal word
        if word is None:
            word = []
        if text:
            word.append(text)

    def flush():
        nonlocal word
        if word is not None:
            out.append(("w", "".join(word)))
            word = None

    while i < n:
        c = s[i]
        if c in " \t":
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
        elif c == "'":
            j, buf = i + 1, []
            while True:
                if j >= n:
                    raise Unreadable("an unclosed ' string")
                if s[j] == "'":
                    if s[j + 1:j + 2] == "'":
                        buf.append("'")
                        j += 2
                        continue
                    break
                buf.append(s[j])
                j += 1
            add("".join(buf))
            i = j + 1
        elif c == '"':
            j, buf = i + 1, []
            while True:
                if j >= n:
                    raise Unreadable('an unclosed " string')
                if s[j] == "`":
                    ch, j = _escape(s, j)
                    buf.append(ch)
                    continue
                if s[j] == '"':
                    if s[j + 1:j + 2] == '"':
                        buf.append('"')
                        j += 2
                        continue
                    break
                buf.append(s[j])
                j += 1
            add("".join(buf))
            i = j + 1
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
            out.append(("w", body))
            i = end.end()
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
                out.append(("op", "&") if out and out[-1][0] in ("w", "redir") else ("call", "&"))
                i += 1
        elif c in "<>" and (word is None or (len(word) == 1 and word[0] in FD_PREFIX)):
            fd, word = (word or [""])[0], None
            m = REDIRECT.match(s, i)
            out.append(("redir", fd + m.group(0)))
            i = m.end()
        elif word is None and s.startswith("--%", i) and s[i + 3:i + 4] in ("", " ", "\t", "\n"):
            j = s.find("\n", i)                   # stop-parsing: the rest of the line is verbatim
            j = n if j < 0 else j
            out.append(("w", "--%"))
            out.extend(("w", p) for p in s[i + 3:j].split())
            i = j
        else:
            add(c)
            i += 1
    flush()
    return out


def quote(word):
    """One word as a POSIX-shell word."""
    if word and SAFE.fullmatch(word):
        return word
    return "'" + word.replace("'", "'\\''") + "'"


def ps_to_posix(s):
    parts, command_position = [], True
    for kind, text in ps_tokens(s):
        if kind == "w":
            if command_position and GIT.fullmatch(text):
                text = "git"
            parts.append(quote(text))
            command_position = False
        elif kind == "call":
            command_position = True
        elif kind == "redir":
            parts.append(text)
        else:
            parts.append(";" if text in "(){}" else text)
            command_position = True
    return " ".join(parts)


def bash_to_posix(s):
    """Bash text with a git named by a path or in capitals renamed `git` where it is the command;
    every other character is left as it is."""
    def fix(m):
        word = m.group(2)
        bare = word[1:-1] if word[:1] in "\"'" else word
        return m.group(1) + "git" if bare != "git" and GIT.fullmatch(bare) else m.group(0)
    return BASH_GIT.sub(fix, s)


def to_posix(cmd, tool):
    if tool == "PowerShell":
        try:
            return ps_to_posix(cmd)
        except Unreadable:
            return cmd
    return bash_to_posix(cmd)


def segments(text):
    """POSIX text split at ; & | and newlines outside quotes, one segment per line. A newline
    inside quotes becomes a space, an escaped newline joins its lines, and a # that starts a word
    outside quotes drops the rest of its line."""
    text = text.replace("\r", "")
    out, q, i, n = [], "", 0, len(text)
    while i < n:
        c = text[i]
        if c == "\\" and q != "'":
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
    if mode not in ("posix", "segments"):
        sys.stderr.write("usage: _shellwords.py posix|segments < payload.json\n")
        return 2
    tool, cmd = read_payload(sys.stdin.buffer.read().decode("utf-8", "replace"))
    text = to_posix(cmd, tool)
    if mode == "segments":
        text = segments(text)
    sys.stdout.buffer.write((text + "\n").encode("utf-8", "replace"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
