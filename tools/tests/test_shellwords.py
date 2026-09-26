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


class PushLinesTests(unittest.TestCase):
    """validate-push.sh's candidate lines (plan 027 review): each shape below was refused before
    plan 027, so a line that still shows the push must come back."""

    def test_only_lines_holding_push(self):
        self.assertEqual(sw.push_lines("git status; echo hi", "Bash"), "")

    def test_raw_command_split_with_its_own_escape(self):
        cmd = 'git -C "E:\\R&D" push --force origin ("bannerlord-1.5.x")'
        self.assertIn(cmd, sw.push_lines(cmd, "PowerShell").split("\n"))

    def test_quoted_hash_after_a_heredoc_quote_keeps_the_push(self):
        cmd = 'cat <<EOF\nsay "hi\nEOF\ngit -c "user.name=a #b" push --force origin T'
        self.assertIn('git -c "user.name=a #b" push --force origin T', sw.push_lines(cmd, "Bash").split("\n"))

    def test_typographic_quote_keeps_the_push(self):
        cmd = "if (\u2018a #' -ne 'x\u2019) { git push --force origin T }"
        self.assertTrue(any("git push --force origin T" in line
                            for line in sw.push_lines(cmd, "PowerShell").split("\n")))

    def test_trailing_comment_is_dropped(self):
        for tool in ("Bash", "PowerShell"):
            with self.subTest(tool=tool):
                self.assertNotIn("T", sw.push_lines("git push --force origin feature # T later", tool))

    # Convergence of plan 027: the blind split cuts inside a quoted value, so the piece holding the
    # push can start at a # whose opening quote sits in the piece before it. Each was refused at
    # 96afb6fb and passed after the review fixes.
    def test_blind_split_inside_a_quoted_value_keeps_the_push(self):
        for pre in ('cat <<EOF\nsay "hi\nEOF\n', "cat <<EOF\nDon't stop\nEOF\n", "echo $'it\\'s'\n"):
            for value in ('X="a;b #c"', "X='a;b #c'", 'env "X=a;b #c"', 'X="l1\n#2"', 'X="a|#c"', 'X="a&#c"'):
                cmd = pre + value + " git push --force origin T"
                with self.subTest(cmd=cmd):
                    self.assertTrue(any("git push --force origin T" in line
                                        for line in sw.push_lines(cmd, "Bash").split("\n")))

    def test_comment_after_any_quote_is_judged(self):
        # The safe side: a quote anywhere before the # may open the value the # sits in.
        self.assertIn("T", sw.push_lines('git commit -m "x"; git push --force origin feature # T later', "Bash"))

    # A long quoted segment holding `push` is not re-split with argument boundaries: shlex is
    # quadratic in one word's length (400 KB took 1.7 s of the 5 s registration).
    def test_argument_boundaries_skip_a_long_segment(self):
        cmd = 'git commit -m "' + "push the thing " * 400 + '" && git push -o "ci skip" origin'
        lines = sw.push_lines(cmd, "Bash").split("\n")
        self.assertIn("git push -o ci\x1fskip origin", lines)
        self.assertFalse(any("push\x1fthe" in line for line in lines))

    def test_argument_boundaries_are_kept_once(self):
        lines = sw.push_lines('git push -o "ci variable" origin; git push -o "" x y', "Bash").split("\n")
        self.assertIn("git push -o ci\x1fvariable origin", lines)
        self.assertIn("git push -o \x1e x y", lines)

    # validate-push stops at the first refused line, and judge_command reads every word after a
    # `push` as a refspec, so a long message holding `push` judged first could outrun the 5 s
    # registration before the short force push after it (plan 027 convergence: 400 KB took 5.2 s).
    def test_shortest_line_first(self):
        cmd = 'git commit -m "' + "push the thing " * 400 + '" && git push --force origin T'
        lines = sw.push_lines(cmd, "Bash").split("\n")
        self.assertEqual(lines[0].strip(), "git push --force origin T")
        self.assertEqual([len(line) for line in lines], sorted(len(line) for line in lines))


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

    def test_push_mode(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "git push origin x; ls"}})
        self.assertEqual(run_cli("push", payload.encode("ascii")).stdout, b"git push origin x\n")

    def test_bad_json_prints_an_empty_line(self):
        r = run_cli("posix", b'{"tool_name":')
        self.assertEqual((r.returncode, r.stdout), (0, b"\n"))

    def test_payload_of_the_wrong_shape_reads_as_no_command(self):
        for raw in ("[1]", '{"tool_input": {"command": 5}}', '{"tool_input": "git status"}'):
            with self.subTest(raw=raw):
                self.assertEqual(sw.read_payload(raw), ("", ""))

    def test_unknown_mode_exits_2(self):
        self.assertEqual(run_cli("bogus", b"{}").returncode, 2)


if __name__ == "__main__":
    unittest.main()
