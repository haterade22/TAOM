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


def run_cli(mode, stdin, *args):
    return subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), mode, *args],
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
        for text in ("git commit -m 'unclosed", "git commit -m @'\nno end", "git status <# no end",
                     "git commit -m @'\nx '@\n", "git -C ${env:X"):
            with self.subTest(text=text):
                self.assertEqual(sw.to_posix(text, "PowerShell"), text)

    # Plan 027 review: PowerShell 7.6.6's parser, read on each input below.
    def test_typographic_quotes_are_quotes(self):
        self.assertEqual(ps("git push -o \u2018ci #1\u2019 origin x"),
                         [["git", "push", "-o", "ci #1", "origin", "x"]])
        self.assertEqual(ps("tool \u2018a #b' c"), [["tool", "a #b", "c"]])
        self.assertEqual(ps("git commit -m \u201ca b\u201d"), [["git", "commit", "-m", "a b"]])

    def test_a_word_that_opens_with_a_quote_ends_at_its_close(self):
        self.assertEqual(ps("git reset 'HEAD'--hard"), [["git", "reset", "HEAD", "--hard"]])

    def test_braced_variable_is_one_word(self):
        self.assertEqual(ps("git -C ${env:USERPROFILE}\\repo commit -m x"),
                         [["git", "-C", "${env:USERPROFILE}\\repo", "commit", "-m", "x"]])

    def test_unicode_escape_takes_one_to_six_hex_digits(self):
        self.assertEqual(ps('x "a' + BT + 'u{2192}b"'), [["x", "a\u2192b"]])
        self.assertEqual(ps('x "' + BT + 'u{0000041}"'), [["x", "u{0000041}"]])

    def test_no_break_space_separates_words(self):
        self.assertEqual(ps("git\u00a0status"), [["git", "status"]])

    def test_empty_argument_is_kept(self):
        self.assertEqual(ps('git commit -m ""'), [["git", "commit", "-m", ""]])


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
                         [["if"], ["echo", "$true"], ["git", "fetch", "origin", "t"]])
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

    def test_stop_parsing_ends_at_a_pipe(self):
        # Compared as text: argvs() would read a quoted '|' word as a separator too.
        self.assertEqual(sw.to_posix("git log --% a | git status", "PowerShell"), "git log --% a | git status")

    def test_subexpression_runs_its_commands(self):
        self.assertEqual(ps("@(git status)"), [["git", "status"]])

    # Plan 027 review: an assignment runs the command on its right.
    def test_assignment_runs_the_command(self):
        self.assertEqual(ps("$r = git commit -m x"), [["$r", "="], ["git", "commit", "-m", "x"]])
        self.assertEqual(ps("$null=GIT reset --hard"), [["$null", "="], ["git", "reset", "--hard"]])
        self.assertEqual(ps("[string]$y += git log"), [["[string]$y", "+="], ["git", "log"]])

    # Convergence of plan 027: ParseInput (PowerShell 7.6.6) reads each of these as an assignment
    # whose right side runs git; the reader read them as a value it prints.
    def test_every_assignment_target_ends_the_statement_head(self):
        reset = ["git", "reset", "--hard"]
        self.assertEqual(ps("$null =git reset --hard"), [["$null", "="], reset])
        self.assertEqual(ps("[int] $x = git reset --hard"), [["[int]", "$x", "="], reset])
        self.assertEqual(ps("$a.b=git reset --hard"), [["$a.b", "="], reset])
        self.assertEqual(ps("$a[0]=git reset --hard"), [["$a[0]", "="], reset])
        self.assertEqual(ps("$x, $y = git reset --hard"), [["$x,", "$y", "="], reset])
        self.assertEqual(ps("$x,$y=git reset --hard"), [["$x,$y", "="], reset])
        self.assertEqual(ps('$r =git commit -m "docs: x"'), [["$r", "="], ["git", "commit", "-m", "docs: x"]])

    def test_a_value_that_is_not_assigned_stays_a_value(self):
        self.assertEqual(ps("$x -eq 1"), [["echo", "$x", "-eq", "1"]])
        self.assertEqual(ps("$x | git push"), [["echo", "$x"], ["git", "push"]])
        self.assertEqual(ps("[int] 5"), [["[int]", "5"]])
        self.assertEqual(ps("[Console]::WriteLine('x')"), [["[Console]::WriteLine"], ["echo", "x"]])

    def test_dot_source_operator_runs_the_command(self):
        self.assertEqual(ps(". git commit -m x"), [["git", "commit", "-m", "x"]])
        self.assertEqual(ps(". .\\build.ps1"), [[".\\build.ps1"]])

    # A string or variable that opens a statement is a value PowerShell prints, never a command
    # (a quoted path with no & is a parse error), so it reads as `echo <value>`.
    def test_a_value_alone_is_echoed(self):
        self.assertEqual(ps("'.\\build.ps1'"), [["echo", ".\\build.ps1"]])
        self.assertEqual(ps("& '.\\build.ps1'"), [[".\\build.ps1"]])
        self.assertEqual(ps("'docs: x' | git commit -F -"), [["echo", "docs: x"], ["git", "commit", "-F", "-"]])
        self.assertEqual(ps("$msg | git commit -F -"), [["echo", "$msg"], ["git", "commit", "-F", "-"]])
        self.assertEqual(ps("@'\ndocs: x\n'@ | git commit -F -"),
                         [["echo", "docs: x"], ["git", "commit", "-F", "-"]])
        self.assertEqual(ps("'git' reset --hard"), [["echo", "git", "reset", "--hard"]])

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
                              ("git status\nGIT reset --hard", "git status\ngit reset --hard"),
                              # Plan 027 review: after environment assignments, and quoted.
                              ("GIT_TRACE=0 GIT commit -m x", "GIT_TRACE=0 git commit -m x"),
                              ("cd x; A=1 B=2 Git.exe status", "cd x; A=1 B=2 git status"),
                              ("'git' reset --hard", "git reset --hard")):
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

    def test_raw_powershell_split_takes_the_backtick_escape(self):
        self.assertEqual(sw.segments('Write-Output "x' + BT + '"; dotnet test"', BT),
                         'Write-Output "x' + BT + '"; dotnet test"')
        self.assertEqual(sw.segments('Set-Location "E:\\x\\"; dotnet test', BT),
                         'Set-Location "E:\\x\\"\n dotnet test')


class CommandWordTests(unittest.TestCase):
    """command_index, peel and commands (#985): the command of a segment found past wrapper words and
    shell grammar, so the gates see the git or dotnet call a `timeout 60` or a `{` hid."""

    def lines(self, cmd, tool="Bash", blind=False):
        return sw.commands(cmd, tool, blind).split("\n")

    def dirs(self, cmd):
        return sw.git_call_dirs(cmd, "Bash", "E:/w", "commit")

    def test_every_bypass_form_peels_to_git(self):
        for cmd, want in (
                ("timeout 60 git reset --hard", "git reset --hard"),
                ("timeout -k 5 60 git reset --hard", "git reset --hard"),
                ("timeout --kill-after=5 --foreground 60 git reset --hard", "git reset --hard"),
                ("timeout -s KILL 60 git reset --hard", "git reset --hard"),
                ("command git clean -fd", "git clean -fd"),
                ("command -p git clean -fd", "git clean -fd"),
                ("env git reset --hard", "git reset --hard"),
                ("env -i -u HOME A=1 git reset --hard", "git reset --hard"),
                ("env -C /e/x git reset --hard", "git reset --hard"),
                ("time git reset --hard", "git reset --hard"),
                ("time -p git reset --hard", "git reset --hard"),
                ("nice -n 5 git reset --hard", "git reset --hard"),
                ("nice -5 git reset --hard", "git reset --hard"),
                ("nohup git reset --hard", "git reset --hard"),
                ("stdbuf -oL git reset --hard", "git reset --hard"),
                ("stdbuf -o L git reset --hard", "git reset --hard"),
                ("xargs git checkout --", "git checkout --"),
                ("xargs -n 1 -I {} git checkout --", "git checkout --"),
                ("timeout 60 time nice -n 5 env A=1 git reset --hard", "git reset --hard"),
                ("{ git reset --hard; }", "git reset --hard"),
                ("(git reset --hard)", "git reset --hard"),
                ("! git reset --hard", "git reset --hard"),
                ("if true; then git reset --hard; fi", "git reset --hard"),
                ("for f in a; do git clean -fd; done", "git clean -fd"),
                ("while true; do git clean -fd; done", "git clean -fd"),
                ("cd X && timeout -k 5 60 git reset --hard", "git reset --hard"),
                ("X=1 timeout 60 GIT reset --hard", "git reset --hard"),
                ("X+=1 timeout 60 /usr/bin/git reset --hard", "git reset --hard")):
            with self.subTest(cmd=cmd):
                self.assertIn(want, self.lines(cmd))

    def test_dotnet_is_peeled_too(self):
        for cmd in ("timeout 600 dotnet test TAOM.Tests", "timeout -k 10 600 dotnet test TAOM.Tests",
                    "time dotnet test TAOM.Tests", "env DOTNET_NOLOGO=1 dotnet test TAOM.Tests"):
            with self.subTest(cmd=cmd):
                self.assertIn("dotnet test TAOM.Tests", self.lines(cmd))

    def test_text_that_only_mentions_a_command_stays_unpeeled(self):
        for cmd in ('echo "timeout 60 git reset --hard"',
                    'git commit -m "time git reset --hard"',
                    'grep -rn "xargs git checkout" docs/',
                    "command -v git",
                    "command -V git",
                    'git commit -m "a | git reset --hard"',
                    'git -c "a;b" reset --hard'):
            with self.subTest(cmd=cmd):
                self.assertEqual(self.lines(cmd), [cmd])

    def test_quote_aware_split_keeps_a_quoted_separator_inside_its_segment(self):
        self.assertEqual(self.lines('timeout 5 git -c "a;b" reset --hard'), ['git -c "a;b" reset --hard'])

    def test_heredoc_apostrophe_does_not_hide_the_next_command(self):
        lines = self.lines("git commit -F - <<'EOF'\nit's done\nEOF\ntimeout 5 git reset --hard")
        self.assertIn("git reset --hard", lines)
        self.assertEqual(lines[0], "git commit -F - << __TAOM_HEREDOC_0__")

    def test_ansi_c_word_with_an_escaped_quote_does_not_hide_the_next_command(self):
        self.assertIn("git reset --hard", self.lines("echo $'it\\'s'; timeout 5 git reset --hard"))

    def test_heredoc_body_run_by_a_shell_is_still_judged(self):
        self.assertIn("git reset --hard", self.lines("bash <<EOF\ngit reset --hard\nEOF"))
        self.assertIn("git reset --hard", self.lines("bash <<EOF\ntimeout 5 git reset --hard\nEOF"))

    def test_only_the_first_32_words_are_looked_at(self):
        self.assertEqual(self.lines("env " * 31 + "git reset --hard")[-1], "git reset --hard")
        past = "env " * 32 + "git reset --hard"
        self.assertEqual(self.lines(past), [past.strip()])

    def test_powershell_tool_keeps_timeout_as_the_windows_program(self):
        for cmd in ("timeout 600 dotnet test TAOM.Tests", "env git reset --hard", "time git reset --hard"):
            with self.subTest(cmd=cmd):
                self.assertEqual(self.lines(cmd, "PowerShell"), [cmd])

    def test_powershell_tool_still_reads_blocks_and_capitals(self):
        self.assertIn("git reset --hard", [l.strip() for l in self.lines("Measure-Command { git reset --hard }", "PowerShell")])
        self.assertIn("git reset --hard", self.lines("GIT reset --hard", "PowerShell"))

    def test_a_shell_script_given_to_dash_c_is_read(self):
        for blind in (False, True):
            with self.subTest(blind=blind):
                self.assertIn("git reset --hard", self.lines('bash -c "cd x && git reset --hard"', blind=blind))
                self.assertIn("git clean -fd", self.lines("sh -c 'git status; git clean -fd'", blind=blind))
                self.assertIn("git reset --hard", self.lines("timeout 5 /bin/zsh -c 'timeout 5 git reset --hard'", blind=blind))

    def test_a_commit_message_heredoc_is_data_not_commands(self):
        cmd = "git commit -m \"$(cat <<'EOF'\nfix: x\n\ndotnet test TAOM.Tests passes\nEOF\n)\""
        self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])

    def test_a_heredoc_fed_to_a_non_shell_is_data(self):
        for cmd in ("python - <<'EOF'\ndotnet test TAOM.Tests\nEOF",
                    "cat > File.cs <<'EOF'\ndotnet test TAOM.Tests\nEOF",
                    "git commit -F - <<'EOF'\ndotnet test TAOM.Tests\nEOF"):
            with self.subTest(cmd=cmd):
                self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])

    def test_a_heredoc_a_shell_runs_is_commands(self):
        self.assertIn("dotnet test X", self.lines("bash <<'EOF'\ndotnet test X\nEOF"))
        self.assertIn("dotnet test X", self.lines("timeout 5 sh <<EOF\ndotnet test X\nEOF"))

    def test_blind_mode_keeps_what_the_old_sed_split_caught(self):
        cmd = "cat <<'EOF' > f.txt\ndon't\nEOF\ngit reset --hard"
        self.assertIn("git reset --hard", self.lines(cmd, blind=True))
        pieces = self.lines('git commit -m "a | git reset --hard"', blind=True)
        self.assertTrue([l for l in pieces if l.startswith("git reset --hard")])
        self.assertFalse([l for l in self.lines('git commit -m "a | git reset --hard"') if l.startswith("git reset")])

    def test_blind_mode_still_peels_and_dedupes(self):
        lines = self.lines("timeout 5 git reset --hard; git status", blind=True)
        self.assertEqual(lines, sorted(set(lines), key=lines.index))
        self.assertIn("git reset --hard", [l.strip() for l in lines])

    def test_r1_blind_cut_is_the_old_sed_cut_not_a_lone_ampersand(self):
        for cmd in ('echo don\'t; git -C "E:/R&D" reset --hard',
                    "cat <<'EOF' > f.txt\ndon't\nEOF\ngit -C \"E:/R&D\" reset --hard"):
            with self.subTest(cmd=cmd):
                self.assertTrue([l for l in self.lines(cmd, blind=True)
                                 if l.startswith('git -C "E:/R&D" reset --hard')])

    def test_r3_short_bundles_and_double_dash_before_a_script(self):
        for blind in (False, True):
            with self.subTest(blind=blind):
                self.assertIn("git add -A", self.lines('bash -lc "git add -A"', blind=blind))
                self.assertIn("git clean -fd", self.lines("zsh -ec 'git clean -fd'", blind=blind))
                self.assertIn("git reset --hard", self.lines("bash -c -- 'git reset --hard'", blind=blind))
                self.assertNotIn("--", self.lines("bash -c -- 'git reset --hard'", blind=blind))

    def test_known_limit_heredoc_with_text_after_its_delimiter_is_not_stripped(self):
        # Parity with 5c8003b72: the opening line must end at the delimiter. Widening it was quadratic in
        # the delimiter's length and hid `echo $((1 << n))` NL `git commit ...` from the label gate (round 5).
        self.assertEqual(sw.strip_heredocs("cat <<'EOF' > f.txt\nx\nEOF\n"), ("cat <<'EOF' > f.txt\nx\nEOF\n", []))
        self.assertEqual(sw.strip_heredocs("echo $((1 << n))\ngit commit -m 'no label here'\nn=3")[1], [])
        # The confirm gates' blind pass still sees the git line after such a heredoc (F3).
        self.assertIn("git reset --hard", self.lines("cat <<'EOF' > f.txt\ndon't\nEOF\ngit reset --hard", blind=True))

    def test_heredoc_regex_is_linear_in_a_long_delimiter(self):
        import time
        cmd = "git reset --hard && python -c '" + "".join("x = 1 << %s = 2\n" % ("a" * 200) for _ in range(64)) + "y = 2\n" * 30000 + "'"
        for blind in (False, True):
            start = time.perf_counter()
            sw.commands(cmd, "Bash", blind)
            self.assertLess(time.perf_counter() - start, 2.0)
        start = time.perf_counter()
        sw.git_call_dirs("git commit -m x && python -c '" + "x = 1 << abcdefghijklmnopqrstuvwxyz\n" * 3000 + "'", "Bash", "E:/w")
        self.assertLess(time.perf_counter() - start, 5.0)

    def test_v3_cd_behind_a_wrapper_runs_in_a_child_process(self):
        self.assertEqual(self.dirs("timeout 5 cd /e/repos/TAOM; git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("nohup cd /e/x && git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("env A=1 pushd /e/x; git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("{ cd /e/x; git commit -m y; }"), ["E:/x"])
        self.assertEqual(self.dirs("A=1 cd /e/x && git commit -m y"), ["E:/x"])
        self.assertEqual(self.dirs("time cd /e/x && git commit -m y"), ["E:/x"])

    def test_v4_a_heredoc_fed_to_a_shell_with_an_operand_is_that_scripts_stdin(self):
        data = "bash tools/foo.sh <<'EOF'\ndotnet test TAOM.Tests\nEOF"
        self.assertFalse([l for l in self.lines(data) if l.startswith("dotnet test")])
        self.assertIn("dotnet test X", self.lines("bash -s <<'EOF'\ndotnet test X\nEOF"))
        self.assertIn("dotnet test X", self.lines("bash -e -o pipefail <<'EOF'\ndotnet test X\nEOF"))

    def test_v5_noexec_runs_nothing(self):
        for cmd in ("bash -n -c 'dotnet test TAOM.Tests'", "bash -nc 'dotnet test TAOM.Tests'",
                    "bash -n <<'EOF'\ndotnet test TAOM.Tests\nEOF"):
            with self.subTest(cmd=cmd):
                self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])
        self.assertEqual(self.dirs("bash -n -c 'git commit -m x'"), [])

    def test_r6_a_closing_paren_is_not_part_of_the_last_word(self):
        for cmd, want in (("(git add -A)", "git add -A"), ("(git add .)", "git add ."),
                          ("(cd sub && git checkout .)", "git checkout ."), ("( git add -A )", "git add -A")):
            with self.subTest(cmd=cmd):
                self.assertIn(want, self.lines(cmd))
        self.assertEqual(self.lines('git commit -m ")"'), ['git commit -m ")"'])

    def test_r8_wrapper_option_prefixes_and_bundles(self):
        self.assertEqual(sw.command_index(["timeout", "--k", "5", "60", "git"], "Bash"), 4)
        self.assertEqual(sw.command_index(["env", "-iC", "sub", "git"], "Bash"), 3)
        self.assertEqual(sw.command_index(["env", "-Csub", "git"], "Bash"), 2)
        self.assertEqual(sw.command_index(["xargs", "-n1", "git"], "Bash"), 2)
        self.assertIn("git reset --hard", self.lines("timeout --k 5 60 git reset --hard"))
        self.assertIn("git reset --hard", self.lines("env -iC sub git reset --hard"))

    def test_t1_a_placeholder_inside_a_script_is_judged_by_the_scripts_own_command(self):
        for cmd in ("bash -lc \"cat > f.txt <<'EOF'\ndotnet test\nEOF\"",
                    "bash -c \"git commit -F - <<'EOF'\nfix: x\n\ndotnet test passes\nEOF\""):
            with self.subTest(cmd=cmd):
                self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])
        self.assertIn("dotnet test X", self.lines("bash -c \"bash <<'EOF'\ndotnet test X\nEOF\""))

    def test_t2_many_openers_or_a_huge_text_skip_heredoc_stripping_in_time(self):
        import time
        worst = 'python -c "' + "x = 1 << 5\n" * 20000 + '"'
        for blind in (False, True):
            start = time.perf_counter()
            self.lines(worst, blind=blind)
            self.assertLess(time.perf_counter() - start, 1.0)
        near = "\n".join("x = %d << 5" % i for i in range(sw.HEREDOC_MAX_OPENERS)) + "\n" + "y" * 200000
        start = time.perf_counter()
        self.lines(near, blind=True)
        self.assertLess(time.perf_counter() - start, 1.0)
        many = "\n".join("x = 1 << 5" for _ in range(sw.HEREDOC_MAX_OPENERS + 6)) + "\ndotnet test"
        self.assertFalse([l for l in self.lines(many) if l.startswith("dotnet test")])
        self.assertIn("git reset --hard", self.lines(many + "\ngit reset --hard", blind=True))

    def test_t5_env_split_string_runs_its_value(self):
        for cmd in ("env -S 'git reset --hard'", "env --split-string='git reset --hard'",
                    "env -i -S 'timeout 5 git reset --hard'", "env --split 'git reset --hard'"):
            with self.subTest(cmd=cmd):
                self.assertIn("git reset --hard", self.lines(cmd))

    def test_t7_scan_stops_at_the_first_operand(self):
        self.assertFalse([l for l in self.lines("bash script.sh -c 'dotnet test'") if l.startswith("dotnet test")])
        for cmd in ("bash -o pipefail -c 'git reset --hard'", "bash --norc -c 'git reset --hard'",
                    "bash --rcfile x -c 'git reset --hard'"):
            with self.subTest(cmd=cmd):
                self.assertIn("git reset --hard", self.lines(cmd))

    def test_t8_two_heredocs_on_one_line(self):
        cmd = "cat <<A <<B\na\nA\ndotnet test\nB"
        self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])
        # Known limit, parity with 5c8003b72: `<<A` is not followed by a newline, so only `<<B` opens a body.
        self.assertEqual(sw.strip_heredocs(cmd)[1], ["a\nA\ndotnet test"])

    def test_t9_escaped_quotes_inside_a_double_quoted_script(self):
        cmd = 'bash -c "echo \\"; dotnet test; \\""'
        self.assertFalse([l for l in self.lines(cmd) if l.startswith("dotnet test")])

    def test_command_index_unit(self):
        self.assertEqual(sw.command_index(["timeout", "-k", "5", "60", "git"], "Bash"), 4)
        self.assertEqual(sw.command_index(["timeout", "-k", "5", "60", "git"], "PowerShell"), 0)
        self.assertEqual(sw.command_index(["A=1", "{", "git"], "PowerShell"), 2)
        self.assertEqual(sw.command_index(["command", "-v", "git"], "Bash"), 0)
        self.assertEqual(sw.command_index(["env", "-S", "git status"], "Bash"), 0)
        self.assertEqual(sw.command_index(["timeout", "--", "60", "git"], "Bash"), 3)


class PushCandidatesTests(unittest.TestCase):
    """validate-push.sh's candidate lines (plan 027 review): each shape below was refused before
    plan 027, so a line that still shows the push must come back."""

    def test_only_lines_holding_push(self):
        self.assertEqual(sw.push_candidates("git status; echo hi", "Bash"), [])

    def test_raw_command_split_with_its_own_escape(self):
        cmd = 'git -C "E:\\R&D" push --force origin ("bannerlord-1.5.x")'
        self.assertIn(cmd, sw.push_candidates(cmd, "PowerShell"))

    def test_quoted_hash_after_a_heredoc_quote_keeps_the_push(self):
        cmd = 'cat <<EOF\nsay "hi\nEOF\ngit -c "user.name=a #b" push --force origin T'
        self.assertIn('git -c "user.name=a #b" push --force origin T', sw.push_candidates(cmd, "Bash"))

    def test_typographic_quote_keeps_the_push(self):
        cmd = "if (\u2018a #' -ne 'x\u2019) { git push --force origin T }"
        self.assertTrue(any("git push --force origin T" in line
                            for line in sw.push_candidates(cmd, "PowerShell")))

    def test_trailing_comment_is_dropped(self):
        for tool in ("Bash", "PowerShell"):
            with self.subTest(tool=tool):
                lines = sw.push_candidates("git push --force origin feature # T later", tool)
                self.assertFalse(any("T" in line for line in lines))

    # Convergence of plan 027: the blind split cuts inside a quoted value, so the piece holding the
    # push can start at a # whose opening quote sits in the piece before it. Each was refused at
    # 96afb6fb and passed after the review fixes.
    def test_blind_split_inside_a_quoted_value_keeps_the_push(self):
        for pre in ('cat <<EOF\nsay "hi\nEOF\n', "cat <<EOF\nDon't stop\nEOF\n", "echo $'it\\'s'\n"):
            for value in ('X="a;b #c"', "X='a;b #c'", 'env "X=a;b #c"', 'X="l1\n#2"', 'X="a|#c"', 'X="a&#c"'):
                cmd = pre + value + " git push --force origin T"
                with self.subTest(cmd=cmd):
                    self.assertTrue(any("git push --force origin T" in line
                                        for line in sw.push_candidates(cmd, "Bash")))

    def test_comment_after_any_quote_is_judged(self):
        # The safe side: a quote anywhere before the # may open the value the # sits in.
        lines = sw.push_candidates('git commit -m "x"; git push --force origin feature # T later', "Bash")
        self.assertTrue(any("T" in line for line in lines))

    # A long quoted segment holding `push` is not re-split with argument boundaries: shlex is
    # quadratic in one word's length (400 KB took 1.7 s of the 5 s registration it then had).
    def test_argument_boundaries_skip_a_long_segment(self):
        cmd = 'git commit -m "' + "push the thing " * 400 + '" && git push -o "ci skip" origin'
        lines = sw.push_candidates(cmd, "Bash")
        self.assertIn("git push -o ci\x1fskip origin", lines)
        self.assertFalse(any("push\x1fthe" in line for line in lines))

    def test_argument_boundaries_are_kept_once(self):
        lines = sw.push_candidates('git push -o "ci variable" origin; git push -o "" x y', "Bash")
        self.assertIn("git push -o ci\x1fvariable origin", lines)
        self.assertIn("git push -o \x1e x y", lines)

    # The lines come back in the order they are produced, each once: the quote-blind pieces, the
    # quoted segments, those segments with their argument boundaries kept, then the raw split. The
    # Python judge answers well inside its deadline in any order (#680), so nothing is sorted, and
    # the target a message names follows from where the push sits in the command.
    def test_candidates_keep_the_order_they_are_produced(self):
        cmd = 'git commit -m "push the thing"; git push --force origin T'
        self.assertEqual(sw.push_candidates(cmd, "Bash"),
                         ['git commit -m "push the thing"', " git push --force origin T",
                          "git commit -m push\x1fthe\x1fthing", "git push --force origin T"])


class GitRepoTests(unittest.TestCase):
    """git_invocations and git_call_dirs (2026-10-16): the directory each git call runs in, read once for
    every git gate. _pybin.sh taom_git_repo_dir resolves each to its top level, and more than one work tree
    is a command a gate cannot judge as one repository."""

    def dirs(self, cmd, cwd="E:/w", tool="Bash", sub="commit"):
        return sw.git_call_dirs(cmd, tool, cwd, sub)

    def test_plain_commit_runs_in_the_cwd(self):
        self.assertEqual(self.dirs("git commit -m x"), ["E:/w"])

    def test_cd_before_the_commit_names_its_repo(self):
        self.assertEqual(self.dirs("cd /e/repos/x && git commit -m x"), ["E:/repos/x"])

    def test_relative_cds_chain_onto_the_cwd(self):
        self.assertEqual(self.dirs("cd a && cd b && git commit -m x"), ["E:/w/a/b"])

    def test_c_option_of_another_git_command_does_not_count(self):
        self.assertEqual(self.dirs("git -C /e/other status && git commit -m x"), ["E:/w"])

    def test_c_option_after_another_option_counts(self):
        self.assertEqual(self.dirs("git -c user.name=x -C sub commit -m y"), ["E:/w/sub"])

    def test_relative_c_option_joins_an_earlier_cd(self):
        self.assertEqual(self.dirs("cd /e/abs && git -C rel commit -m y"), ["E:/abs/rel"])

    def test_cd_inside_a_subshell_ends_at_its_close(self):
        self.assertEqual(self.dirs("(cd a && ls) && git commit -m x"), ["E:/w"])

    def test_subshell_close_inside_a_punctuation_run(self):
        # shlex hands `);` and `)&&` back as one token (Codex review 2026-10-16).
        self.assertEqual(self.dirs("(cd /e/other && git status); git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("(cd /e/other && git status)&& git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("((cd /e/other)); git commit -m x"), ["E:/w"])

    def test_cd_after_the_commit_does_not_count(self):
        self.assertEqual(self.dirs("git commit -m x && cd /e/elsewhere"), ["E:/w"])

    def test_cd_minus_is_unknown_and_moves_nothing(self):
        self.assertEqual(self.dirs("cd - && git commit -m x"), ["E:/w"])

    def test_pushd_then_popd_returns(self):
        self.assertEqual(self.dirs("pushd /e/other; popd; git commit -m x"), ["E:/w"])
        self.assertEqual(self.dirs("pushd /e/other && git commit -m x"), ["E:/other"])

    def test_powershell_push_and_pop_location(self):
        self.assertEqual(self.dirs("Push-Location E:/other; Pop-Location; git commit -m y", tool="PowerShell"), ["E:/w"])

    def test_bash_c_script_is_read(self):
        self.assertEqual(self.dirs('bash -c "cd /e/x && git commit -m y"'), ["E:/x"])

    def test_heredoc_apostrophe_keeps_the_cd(self):
        cmd = "cd /e/x && git commit -F - <<'EOF'\nfix: don't\nEOF"
        self.assertEqual(self.dirs(cmd), ["E:/x"])

    def test_unbalanced_quote_keeps_the_words_before_it(self):
        self.assertEqual(self.dirs('cd /e/x && git commit -m "oops'), ["E:/x"])

    def test_every_commit_names_its_repo_once(self):
        self.assertEqual(self.dirs("git -C /e/a commit -m x; git -C /e/b commit -m y"), ["E:/a", "E:/b"])
        self.assertEqual(self.dirs("git commit -m x && git commit --amend --no-edit"), ["E:/w"])

    def test_powershell_set_location_moves_the_commit(self):
        self.assertEqual(self.dirs("Set-Location -Path E:/x; git commit -m y", tool="PowerShell"), ["E:/x"])

    def test_no_cwd_leaves_a_relative_cd_relative(self):
        self.assertEqual(sw.git_call_dirs("cd sub && git commit -m x", "Bash", "", "commit"), ["sub"])
        self.assertEqual(sw.git_call_dirs("git commit -m x", "Bash", "", "commit"), [""])

    def test_env_chdir_moves_the_commit(self):
        for cmd in ("env -C E:/repos/other git commit -m x", "env --chdir E:/repos/other git commit -m x",
                    "env --chdir=E:/repos/other A=1 git commit -m x",
                    "timeout 5 env -C /e/repos/other git commit -m x"):
            with self.subTest(cmd=cmd):
                self.assertEqual(self.dirs(cmd, cwd="E:/repos/TAOM"), ["E:/repos/other"])
        self.assertEqual(self.dirs("env -C sub git -C inner commit -m x"), ["E:/w/sub/inner"])
        self.assertEqual(self.dirs("env A=1 git commit -m x -C y"), ["E:/w"])

    def test_r2_xargs_builds_the_message_at_run_time_so_it_is_unjudged(self):
        self.assertEqual(sw.git_call_dirs("xargs -0 -I{} git commit --amend -m {}", "Bash", "E:/w"), [])
        self.assertEqual(sw.git_call_dirs("echo a | xargs git commit -m x", "Bash", "E:/w"), [])
        text, bodies = sw.strip_heredocs("xargs -0 -I{} git commit --amend -m {}")
        self.assertEqual(sw.git_invocations(text, bodies, "commit"), [])

    def test_r3_bundled_c_and_double_dash_reach_the_script(self):
        self.assertEqual(self.dirs('bash -lc "git commit -m x"'), ["E:/w"])
        self.assertEqual(self.dirs('bash -c -- "cd /e/x && git commit -m y"'), ["E:/x"])

    def test_t3_other_shell_names(self):
        self.assertEqual(self.dirs('zsh.exe -c "cd /e/x && git commit -m y"'), ["E:/x"])
        self.assertEqual(self.dirs('sh.exe -c "git commit -m y"'), ["E:/w"])

    def test_t6_env_options_in_every_spelling(self):
        for cmd in ("env -iC sub git commit -m x", "env -Csub git commit -m x", "env --ch sub git commit -m x",
                    "env -u FOO -C sub git commit -m x", "env --chdir=sub git commit -m x"):
            with self.subTest(cmd=cmd):
                self.assertEqual(self.dirs(cmd), ["E:/w/sub"])

    def test_t7_a_commit_after_a_script_operand_is_not_read(self):
        self.assertEqual(self.dirs("bash script.sh -c 'git commit -m x'"), [])

    def test_wrapped_commit_keeps_its_c_option(self):
        self.assertEqual(self.dirs("timeout 60 git -C /e/x commit -m y"), ["E:/x"])

    def test_commit_in_a_group_or_loop_runs_in_the_cwd(self):
        self.assertEqual(self.dirs("{ git commit -m x; }"), ["E:/w"])
        self.assertEqual(self.dirs("for b in a; do git commit -m x; done"), ["E:/w"])

    def test_other_subcommand(self):
        self.assertEqual(self.dirs("cd /e/x && git add -A", sub="add"), ["E:/x"])
        self.assertEqual(self.dirs("cd /e/x && git add -A"), [])

    def test_invocations_carry_the_args_and_a_piped_word(self):
        text, bodies = sw.strip_heredocs("echo feat-x | git commit -F -")
        calls = sw.git_invocations(text, bodies, "commit")
        self.assertEqual(calls, [(["<<", "__TAOM_HEREDOC_0__", "-F", "-"], None)])
        self.assertEqual(sw.expand_heredoc("__TAOM_HEREDOC_0__", bodies), "feat-x")

    def test_strict_mode_still_raises_on_bad_quoting(self):
        with self.assertRaises(ValueError):
            sw.git_invocations('git commit -m "oops', [], "commit")

    def test_command_past_the_scan_cap_reads_no_call_in_time(self):
        # shlex builds each word one character at a time: a 1 MB quoted message took 6.5 s here and pushed
        # three gates past their registrations, where a killed gate fails open (deep review 2026-10-16).
        import time
        big = "x" * (sw.REPO_SCAN_MAX + 1)
        start = time.perf_counter()
        got = self.dirs('cd /e/x && git commit -m "' + big + '"')
        self.assertEqual(got, [])
        self.assertLess(time.perf_counter() - start, 1.0)

    def test_a_long_heredoc_body_is_out_before_the_cap(self):
        body = "y" * 400000
        self.assertEqual(self.dirs("cd /e/x && git commit -F - <<'EOF'\n" + body + "\nEOF"), ["E:/x"])

    def test_cli_prints_the_call_dirs_then_the_cwd(self):
        payload = json.dumps({"tool_name": "Bash", "cwd": "E:/w",
                              "tool_input": {"command": "cd /e/x && git commit -m y"}})
        r = run_cli("git-repo", payload.encode("ascii"))
        self.assertEqual((r.returncode, r.stdout), (0, b"E:/x\n--\nE:/w\n"))


class CliTests(unittest.TestCase):
    def test_posix_mode_writes_utf8_with_lf_only(self):
        payload = json.dumps({"tool_name": "PowerShell",
                              "tool_input": {"command": "GIT commit -m @'\nfeat: x \u2192 y\n'@"}})
        r = run_cli("posix", payload.encode("ascii"))
        self.assertEqual(r.returncode, 0)
        self.assertEqual(r.stdout, "git commit -m 'feat: x \u2192 y'\n".encode("utf-8"))
        self.assertNotIn(b"\r", r.stdout)

    def test_commands_mode(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "a; timeout 5 git status"}})
        self.assertEqual(run_cli("commands", payload.encode("ascii")).stdout, b"a\ngit status\n")

    def test_commands_blind_mode(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": 'git commit -m "a | git reset --hard"'}})
        out = run_cli("commands", payload.encode("ascii"), "--blind").stdout
        self.assertIn(b"\ngit reset --hard", out)

    def test_segments_mode_is_gone(self):
        self.assertEqual(run_cli("segments", b"{}").returncode, 2)

    def test_bad_json_prints_an_empty_line(self):
        r = run_cli("posix", b'{"tool_name":')
        self.assertEqual((r.returncode, r.stdout), (0, b"\n"))

    def test_payload_of_the_wrong_shape_reads_as_no_command(self):
        for raw in ('{"tool_input": {"command": 5}}', '{"tool_input": "git status"}', "{}"):
            with self.subTest(raw=raw):
                self.assertEqual(sw.read_payload(raw), ("", ""))

    def test_payload_that_is_no_json_object_is_unread(self):
        for raw in ('{"tool_name":', "[1]", "", "push"):
            with self.subTest(raw=raw):
                self.assertIsNone(sw.read_payload(raw))

    def test_unknown_mode_exits_2(self):
        self.assertEqual(run_cli("bogus", b"{}").returncode, 2)


if __name__ == "__main__":
    unittest.main()
