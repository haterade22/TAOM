"""_shellwords.py (plan 027): a PowerShell tool command read as the Bash text of the same command,
git named by a path or in capitals read as `git`, and the quote-aware split the hooks share. The
PowerShell argument lists below were read from PowerShell 7's own parser
([System.Management.Automation.Language.Parser]::ParseInput) when the plan was written."""
import json
import os
import shlex
import subprocess
import sys
import unittest

HOOKS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".claude", "hooks")
sys.path.insert(0, HOOKS)
sys.dont_write_bytecode = True  # no __pycache__ folder inside .claude/hooks
import _shellwords as sw  # noqa: E402

BT = "`"  # PowerShell's escape character


def argvs(posix_text):
    """Each simple command of POSIX-shell text as its argument list, split at ; & | and newlines."""
    lexer = shlex.shlex(posix_text, posix=True, punctuation_chars=";&|\n")
    lexer.whitespace = " \t"
    lexer.whitespace_split = True
    commands, current = [], []
    for token in lexer:
        if token and set(token) <= set(";&|\n"):
            if current:
                commands.append(current)
            current = []
        else:
            current.append(token)
    if current:
        commands.append(current)
    return commands


def ps(command):
    return argvs(sw.to_posix(command, "PowerShell"))


def run_cli(mode, stdin):
    return subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), mode],
                          input=stdin, capture_output=True, timeout=30)


class PowerShellQuotingTests(unittest.TestCase):
    def test_single_quotes_double_an_apostrophe(self):
        self.assertEqual(ps("git commit -m 'it''s'"), [["git", "commit", "-m", "it's"]])

    def test_double_quotes_take_backtick_escapes(self):
        self.assertEqual(ps('git commit -m "a' + BT + 'nb"'), [["git", "commit", "-m", "a\nb"]])

    def test_double_quotes_double_a_quote(self):
        self.assertEqual(ps('git commit -m "a""b"'), [["git", "commit", "-m", 'a"b']])

    def test_backslash_is_literal(self):
        self.assertEqual(ps('git -C "E:\\R&D\\" fetch origin y'),
                         [["git", "-C", "E:\\R&D\\", "fetch", "origin", "y"]])

    def test_here_string_is_one_word(self):
        self.assertEqual(ps("git commit -m @'\nfeat: v2.0.30 - x\n\nbody\n'@"),
                         [["git", "commit", "-m", "feat: v2.0.30 - x\n\nbody"]])

    def test_expandable_here_string_takes_escapes_and_keeps_variables(self):
        self.assertEqual(ps('git commit -m @"\nfeat: $x ' + BT + '$y\n"@'),
                         [["git", "commit", "-m", "feat: $x $y"]])

    def test_quoted_pieces_join_into_one_word(self):
        self.assertEqual(ps("git commit --no-verif''y -m x"),
                         [["git", "commit", "--no-verify", "-m", "x"]])

    def test_bare_backtick_escape(self):
        self.assertEqual(ps("git fetch origin trunk-1.5" + BT + ".x"),
                         [["git", "fetch", "origin", "trunk-1.5.x"]])

    def test_unreadable_text_comes_back_unchanged(self):
        for text in ("git commit -m 'unclosed", "git commit -m @'\nno end", "git status <# no end"):
            with self.subTest(text=text):
                self.assertEqual(sw.to_posix(text, "PowerShell"), text)


class PowerShellStatementTests(unittest.TestCase):
    def test_backtick_continuation_joins_lines(self):
        self.assertEqual(ps("git fetch " + BT + "\n --prune origin z"),
                         [["git", "fetch", "--prune", "origin", "z"]])

    def test_comment_is_dropped(self):
        self.assertEqual(ps("git fetch origin feature # trunk later"),
                         [["git", "fetch", "origin", "feature"]])

    def test_block_comment_is_dropped(self):
        self.assertEqual(ps("git reset --hard <# c #> HEAD"), [["git", "reset", "--hard", "HEAD"]])

    def test_braces_and_parentheses_separate_commands(self):
        self.assertEqual(ps("if ($true) {git fetch origin t}"),
                         [["if"], ["$true"], ["git", "fetch", "origin", "t"]])
        self.assertEqual(ps("1..1 | ForEach-Object {git fetch origin t}"),
                         [["1..1"], ["ForEach-Object"], ["git", "fetch", "origin", "t"]])
        self.assertEqual(ps("$(git fetch origin x)"), [["git", "fetch", "origin", "x"]])

    def test_statement_separators(self):
        self.assertEqual(ps('git add -A && git commit -am "x" || echo no'),
                         [["git", "add", "-A"], ["git", "commit", "-am", "x"], ["echo", "no"]])

    def test_call_operator_is_dropped(self):
        self.assertEqual(ps("&git status"), [["git", "status"]])
        self.assertEqual(ps("x = 1; & git log"), [["x", "=", "1"], ["git", "log"]])

    def test_ampersand_after_a_word_runs_in_the_background(self):
        self.assertEqual(sw.to_posix("git status & git log", "PowerShell"), "git status & git log")

    def test_redirection_stays_an_operator(self):
        self.assertEqual(sw.to_posix("git fetch origin x 2>&1 | Out-Null", "PowerShell"),
                         "git fetch origin x 2>&1 | Out-Null")
        self.assertEqual(sw.to_posix("git status 2>$null", "PowerShell"), "git status 2> '$null'")

    def test_glued_redirection_stays_in_the_word(self):
        # PowerShell's parser hands git `trunkx*>$null` as one argument.
        self.assertEqual(ps("git fetch origin trunkx*>$null"),
                         [["git", "fetch", "origin", "trunkx*>$null"]])

    def test_stop_parsing_keeps_the_rest_of_the_line(self):
        self.assertEqual(ps("git log --% --format=%H; git status"),
                         [["git", "log", "--%", "--format=%H;", "git", "status"]])

    def test_braces_inside_a_word_split_it(self):
        # PowerShell reads HEAD^{tree} as HEAD^ followed by a script block.
        self.assertEqual(ps('git commit-tree HEAD^{tree} -m "x"')[0], ["git", "commit-tree", "HEAD^"])


class GitWordTests(unittest.TestCase):
    def test_powershell_git_by_path_or_capitals(self):
        self.assertEqual(ps("& 'C:\\Program Files\\Git\\cmd\\git.exe' fetch origin x"),
                         [["git", "fetch", "origin", "x"]])
        self.assertEqual(ps("GIT commit -m x"), [["git", "commit", "-m", "x"]])
        self.assertEqual(ps("& git.exe -C 'E:\\a b' add -A"), [["git", "-C", "E:\\a b", "add", "-A"]])

    def test_powershell_git_word_only_as_the_command(self):
        self.assertEqual(ps("echo GIT commit"), [["echo", "GIT", "commit"]])
        self.assertEqual(ps("git log -- C:\\x\\git.exe"), [["git", "log", "--", "C:\\x\\git.exe"]])

    def test_bash_git_by_path_or_capitals(self):
        for before, after in (("GIT fetch origin x", "git fetch origin x"),
                              ("cd /x && GIT commit -m 'a'", "cd /x && git commit -m 'a'"),
                              ('"/c/Program Files/Git/cmd/git.exe" reset --hard', "git reset --hard"),
                              ("/mingw64/bin/git add -A", "git add -A"),
                              ("  Git.EXE status", "  git status"),
                              ("x=$(GIT rev-parse HEAD)", "x=$(git rev-parse HEAD)"),
                              ("(GIT stash drop)", "(git stash drop)"),
                              ("git status\nGIT reset --hard", "git status\ngit reset --hard")):
            with self.subTest(before=before):
                self.assertEqual(sw.to_posix(before, "Bash"), after)

    def test_bash_text_is_otherwise_unchanged(self):
        for text in ("echo GIT commit", "git commit -m 'GIT commit'", "GIT_TRACE=1 git fetch",
                     "git log -- .git", "legit status", "git commit -F - <<'EOF'\nDon't stop yet\nEOF",
                     'bash -c "git fetch origin x"', "git commit -m $'a\\nb'"):
            with self.subTest(text=text):
                self.assertEqual(sw.to_posix(text, "Bash"), text)


class SegmentsTests(unittest.TestCase):
    def test_split_outside_quotes_only(self):
        self.assertEqual(sw.segments('git -C "E:/R&D" fetch; git status'),
                         'git -C "E:/R&D" fetch\n git status')

    def test_newline_inside_quotes_becomes_a_space(self):
        self.assertEqual(sw.segments('git commit -m "a\nb"'), 'git commit -m "a b"')

    def test_escaped_newline_joins(self):
        self.assertEqual(sw.segments("git fetch \\\n origin x"), "git fetch   origin x")

    def test_comment_drops_the_rest_of_its_line(self):
        self.assertEqual(sw.segments("git fetch origin feature # trunk later\necho a#b; x=${#y}"),
                         "git fetch origin feature \necho a#b\n x=${#y}")

    def test_powershell_text_is_split_after_posix(self):
        text = sw.to_posix('Set-Location "E:\\repos\\TAOM\\"; dotnet test', "PowerShell")
        self.assertEqual(sw.segments(text), "Set-Location 'E:\\repos\\TAOM\\' \n dotnet test")


class CliTests(unittest.TestCase):
    def test_posix_mode_writes_utf8_with_lf_only(self):
        payload = json.dumps({"tool_name": "PowerShell",
                              "tool_input": {"command": "GIT commit -m @'\nfeat: x \u2192 y\n'@"}})
        r = run_cli("posix", payload.encode("ascii"))
        self.assertEqual(r.returncode, 0)
        self.assertEqual(r.stdout, "git commit -m 'feat: x \u2192 y'\n".encode("utf-8"))
        self.assertNotIn(b"\r", r.stdout)

    def test_segments_mode(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "a; b"}})
        self.assertEqual(run_cli("segments", payload.encode("ascii")).stdout, b"a\n b\n")

    def test_bad_json_prints_an_empty_line(self):
        r = run_cli("posix", b'{"tool_name":')
        self.assertEqual((r.returncode, r.stdout), (0, b"\n"))

    def test_unknown_mode_exits_2(self):
        self.assertEqual(run_cli("bogus", b"{}").returncode, 2)


if __name__ == "__main__":
    unittest.main()
