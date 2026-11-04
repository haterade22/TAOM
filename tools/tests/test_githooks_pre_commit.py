#!/usr/bin/env python
"""End-to-end tests for .githooks/pre-commit and .githooks/pre-merge-commit.

Run:  python -m unittest tools.tests.test_githooks_pre_commit

Each test commits in a throwaway repository whose core.hooksPath points at this repo's .githooks
folder, with stub tools that record that they ran and exit with a chosen code. So these prove what
git really hands the hook: the final index, a temporary index for a path commit, and a merge.
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
HOOKS = REPO / ".githooks"
XML = "Main/_Module/ModuleData/items.xml"

sys.path.insert(0, str(REPO / "tools"))
import lint_docs  # noqa: E402

STUB = """import os, sys
from pathlib import Path
name = Path(__file__).stem
Path(os.environ["STUB_DIR"], name + ".ran").write_text(" ".join(sys.argv[1:]))
if os.environ.get("STUB_CRASH") == name:
    raise RuntimeError("the tool itself broke")
print(name + " says broken")
sys.exit(int(os.environ.get("STUB_RC_" + name.upper(), "0")))
"""

# The git inside Visual Studio and GitHub Desktop: sh, grep, sed and tail, but no bash and no GNU
# timeout. Absent on a machine without them, where those tests skip.
# The real linter, with the checks a throwaway repository cannot satisfy stubbed out.
REAL_LINT = """import os, pathlib, sys
sys.path.insert(0, os.environ["TAOM_REAL_TOOLS"])
import lint_docs as ld
ld.REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
ld.DOCS_DIR = ld.REPO_ROOT / "docs"
ld.DOC_ROOTS = [ld.DOCS_DIR]
ld.check_context_budget = lambda *a, **k: []
ld.check_version_consistency = lambda *a, **k: []
sys.exit(ld.entry(sys.argv[1:]))
"""

BUNDLED_GIT_GLOBS = (
    "E:/Visual Studio/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git",
    os.path.expandvars("%LOCALAPPDATA%/GitHubDesktop/app-*/resources/app/git"),
)


def bundled_gits():
    return [Path(p) for pattern in BUNDLED_GIT_GLOBS for p in glob.glob(pattern)
            if (Path(p) / "cmd" / "git.exe").exists()]


class RepoCase(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="taom-precommit-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.work = self.root / "work"
        self.stubs = self.root / "stubs"
        self.work.mkdir()
        self.stubs.mkdir()
        self.env = dict(os.environ, STUB_DIR=str(self.stubs))
        for key in ("GIT_DIR", "GIT_INDEX_FILE", "GIT_WORK_TREE"):
            self.env.pop(key, None)
        self.git("init", "-q", "-b", "trunk")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "user.name", "Test")
        self.git("config", "core.autocrlf", "false")
        self.write("README.txt", "hello\n")
        self.write(XML, "<Items/>\n")
        self.write("CLAUDE.md", "# one\n")
        self.git("add", "-A")
        self.git("commit", "-q", "--no-verify", "-m", "base")
        self.git("config", "core.hooksPath", str(HOOKS))

    def git(self, *args, check=True):
        result = subprocess.run(["git", *args], cwd=self.work, env=self.env, capture_output=True, text=True)
        if check and result.returncode != 0:
            self.fail(f"git {args} failed: {result.stdout}{result.stderr}")
        return result

    def write(self, rel, text):
        path = self.work / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")

    def install_tools(self, *names):
        for name in names:
            self.write(f"tools/{name}.py", STUB)

    def ran(self, name):
        return (self.stubs / (name + ".ran")).exists()

    def forget_runs(self):
        for path in self.stubs.iterdir():
            path.unlink()

    def commit(self, *args):
        return self.git("commit", "-m", "subject", *args, check=False)

    def shim_on_path(self, name, body):
        """A shell script called `name`, first on PATH for the commit."""
        shims = self.root / "shims"
        shims.mkdir(exist_ok=True)
        (shims / name).write_text("#!/bin/sh\n" + body + "\n", encoding="utf-8", newline="\n")
        self.env["PATH"] = str(shims) + os.pathsep + self.env["PATH"]


class ModuleDataGate(RepoCase):
    def setUp(self):
        super().setUp()
        self.install_tools("validate_moduledata", "generate_career_kits", "lint_docs")

    def test_a_validator_error_refuses_the_commit_and_shows_the_findings(self):
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.write(XML, "<Items a='1'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("validate_moduledata says broken", result.stderr)
        self.assertEqual(self.git("rev-list", "--count", "HEAD").stdout.strip(), "1")

    def test_a_clean_validator_lets_the_commit_through(self):
        self.write(XML, "<Items a='2'/>\n")
        self.git("add", XML)
        self.assertEqual(self.commit().returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))
        self.assertTrue(self.ran("generate_career_kits"))

    def test_career_kit_drift_refuses_the_commit(self):
        self.env["STUB_RC_GENERATE_CAREER_KITS"] = "1"
        self.write(XML, "<Items a='3'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("generate_career_kits says broken", result.stderr)

    def test_a_commit_with_no_moduledata_never_runs_the_validator(self):
        self.write("README.txt", "changed\n")
        self.git("add", "README.txt")
        self.assertEqual(self.commit().returncode, 0)
        self.assertFalse(self.ran("validate_moduledata"))

    def test_a_moduledata_path_with_a_space_is_still_judged(self):
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.write("Main/_Module/ModuleData/two words.xml", "<Items/>\n")
        self.git("add", "Main/_Module/ModuleData/two words.xml")
        self.assertNotEqual(self.commit().returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_a_validator_that_cannot_judge_allows_the_commit_and_says_so(self):
        for code in ("2", "3"):
            with self.subTest(code=code):
                self.env["STUB_RC_VALIDATE_MODULEDATA"] = code
                self.write(XML, f"<Items a='{code}x'/>\n")
                self.git("add", XML)
                result = self.commit()
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn("NOT checked", result.stderr)

    def test_a_path_commit_of_another_file_ignores_staged_moduledata(self):
        """Git gives the hook a temporary index holding what the path commit records."""
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.write(XML, "<Items a='4'/>\n")
        self.git("add", XML)
        self.write("README.txt", "path commit\n")
        self.git("add", "README.txt")
        result = self.commit("--", "README.txt")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertFalse(self.ran("validate_moduledata"))

    def test_a_path_commit_of_unstaged_moduledata_is_judged(self):
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.write(XML, "<Items a='5'/>\n")
        result = self.commit("--", XML)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_a_clean_merge_is_judged_too(self):
        """A clean merge skips pre-commit, so pre-merge-commit runs the same gates."""
        self.git("switch", "-q", "-c", "side")
        self.write(XML, "<Items a='6'/>\n")
        self.git("add", XML)
        self.assertEqual(self.commit().returncode, 0)
        self.git("switch", "-q", "trunk")
        self.write("README.txt", "trunk moved\n")
        self.git("add", "README.txt")
        self.assertEqual(self.commit().returncode, 0)
        self.forget_runs()
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        result = self.git("merge", "--no-ff", "--no-edit", "side", check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_an_xslt_is_judged_too(self):
        self.write("Main/_Module/ModuleData/a.xslt", "<xsl/>\n")
        self.git("add", "Main/_Module/ModuleData/a.xslt")
        self.assertEqual(self.commit().returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_a_non_ascii_moduledata_path_is_judged(self):
        """Git quotes such a name by default, and a quoted name would miss the case arms."""
        self.write("Main/_Module/ModuleData/éowyn.xml", "<Items/>\n")
        self.git("add", "Main/_Module/ModuleData/éowyn.xml")
        self.assertEqual(self.commit().returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_moving_xml_out_of_moduledata_is_judged(self):
        """A rename must show its source, or the commit that removes the file runs no gate."""
        self.git("mv", XML, "items.xml")
        self.assertEqual(self.commit().returncode, 0)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_unstaged_moduledata_is_named_because_the_gates_read_the_working_tree(self):
        self.write(XML, "<Items a='staged'/>\n")
        self.git("add", XML)
        self.write(XML, "<Items a='not staged'/>\n")
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(XML, result.stderr)
        self.assertIn("working tree", result.stderr)

    def test_a_commit_with_nothing_unstaged_says_nothing_about_it(self):
        self.write(XML, "<Items a='clean'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("working tree", result.stderr)


class Timeouts(RepoCase):
    def setUp(self):
        super().setUp()
        self.install_tools("validate_moduledata", "generate_career_kits")
        self.write(XML, "<Items a='t'/>\n")
        self.git("add", XML)

    def test_an_overrun_allows_the_commit_and_says_so(self):
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.shim_on_path("timeout", 'case "$1" in --version) exit 0 ;; esac\nexit 124')
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("overran", result.stderr)
        self.assertIn("NOT checked", result.stderr)

    def test_windows_timeout_exe_is_never_read_as_a_finding(self):
        """Under a bundled git the first timeout on PATH is System32's, which exits 1 on these
        arguments. The gate must run, and judge."""
        self.shim_on_path("timeout", 'echo "ERROR: Invalid syntax." >&2\nexit 1')
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(self.ran("validate_moduledata"))

    def test_with_windows_timeout_exe_a_real_finding_still_refuses(self):
        self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
        self.shim_on_path("timeout", 'echo "ERROR: Invalid syntax." >&2\nexit 1')
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("validate_moduledata says broken", result.stderr)


class DocBudgetGate(RepoCase):
    def setUp(self):
        super().setUp()
        self.install_tools("lint_docs")

    def real_linter(self):
        self.write("tools/lint_docs.py", REAL_LINT)
        self.env["TAOM_REAL_TOOLS"] = str(REPO / "tools")

    def test_a_clean_merge_of_a_doc_the_branch_already_has_is_not_refused(self):
        """Git writes MERGE_HEAD only after pre-merge-commit, so the new-doc check cannot see that the
        merged branch holds the doc, and a plain `git commit` of the same tree would pass."""
        self.real_linter()
        self.git("add", "tools/lint_docs.py")
        self.git("commit", "-q", "--no-verify", "-m", "tool")
        self.git("switch", "-q", "-c", "side")
        self.write("docs/features/x.md", "# x\n\nNo verify heading.\n")
        self.git("add", "docs/features/x.md")
        self.git("commit", "-q", "--no-verify", "-m", "old doc")
        self.git("switch", "-q", "trunk")
        self.write("README.txt", "trunk moved\n")
        self.git("add", "README.txt")
        self.assertEqual(self.commit().returncode, 0)
        result = self.git("merge", "--no-ff", "--no-edit", "side", check=False)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_a_new_doc_without_the_verify_section_is_still_refused_outside_a_merge(self):
        self.real_linter()
        self.git("add", "tools/lint_docs.py")
        self.git("commit", "-q", "--no-verify", "-m", "tool")
        self.write("docs/features/x.md", "# x\n\nNo verify heading.\n")
        self.git("add", "docs/features/x.md")
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("no-verify-section", result.stderr)

    def test_a_budget_failure_refuses_the_commit(self):
        self.env["STUB_RC_LINT_DOCS"] = "1"
        self.write("CLAUDE.md", "# grown\n")
        self.git("add", "CLAUDE.md")
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("lint_docs says broken", result.stderr)
        self.assertEqual((self.stubs / "lint_docs.ran").read_text(), "--drift-only --staged")

    def test_a_commit_the_checks_cannot_concern_never_runs_the_linter(self):
        """Budget checks read the working tree, so another session's edit must not refuse this."""
        self.env["STUB_RC_LINT_DOCS"] = "1"
        self.write("README.txt", "unrelated\n")
        self.git("add", "README.txt")
        self.assertEqual(self.commit().returncode, 0)
        self.assertFalse(self.ran("lint_docs"))

    def test_each_surface_the_claude_gate_watches_runs_the_linter(self):
        for rel in ("AGENTS.md", "docs/ai-includes/orientation.md", "docs/features/a.md",
                    "docs/reviews/rca-x-2026-11-03.md", ".claude/rules/a.md",
                    ".claude/pinned-game-version.txt", "Main/_Module/ModuleData/a.json",
                    "docs/reference/taleworlds-api-snapshot/a.md"):
            with self.subTest(rel=rel):
                self.forget_runs()
                self.write(rel, f"{rel} changed\n")
                self.git("add", rel)
                self.assertEqual(self.commit().returncode, 0)
                self.assertTrue(self.ran("lint_docs"), rel)

    def test_each_directory_the_rule_drift_checks_scan_runs_the_linter(self):
        """A commit that adds a dead citation must run the check that reads it."""
        dirs = set(lint_docs.DEAD_MEMORY_DIRS) | set(lint_docs.DOTNET_FLAG_DIRS) | {lint_docs.RULES_DIR_REL}
        self.assertGreaterEqual(len(dirs), 5, "the directory lists are empty")
        for folder in sorted(dirs):
            rel = f"{folder}/sample/x.md"
            with self.subTest(rel=rel):
                self.forget_runs()
                self.write(rel, "changed\n")
                self.git("add", rel)
                self.assertEqual(self.commit().returncode, 0)
                self.assertTrue(self.ran("lint_docs"), rel)


class IgnoredHarnessFile(RepoCase):
    def test_a_gitignored_file_under_claude_refuses_the_commit(self):
        self.write(".gitignore", "bin/\n")
        self.write(".claude/skills/freeze/bin/check.sh", "echo\n")
        self.git("add", ".gitignore")
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(".claude/skills/freeze/bin/check.sh", result.stderr)

    def test_another_sessions_untracked_file_does_not_refuse_the_commit(self):
        self.write(".claude/skills/new/SKILL.md", "not mine\n")
        self.write("README.txt", "mine\n")
        self.git("add", "README.txt")
        self.assertEqual(self.commit().returncode, 0)

    def test_an_amend_with_nothing_new_staged_is_refused_too(self):
        """An amend is commonly how a harness file gets lost, so this runs before the empty-index exit."""
        self.write(".gitignore", "bin/\n")
        self.git("add", ".gitignore")
        self.assertEqual(self.commit().returncode, 0)
        self.write(".claude/skills/freeze/bin/check.sh", "echo\n")
        result = self.git("commit", "--amend", "--no-edit", check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(".claude/skills/freeze/bin/check.sh", result.stderr)


class FailOpen(RepoCase):
    def test_missing_tools_allow_the_commit_and_say_so(self):
        self.write(XML, "<Items a='7'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("NOT checked", result.stderr)

    def test_a_gate_that_crashes_allows_the_commit_and_says_so(self):
        """A traceback exits 1, which must not read as a finding: a broken hook must not block work."""
        self.install_tools("validate_moduledata", "generate_career_kits")
        self.env["STUB_CRASH"] = "validate_moduledata"
        self.write(XML, "<Items a='8'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("crashed", result.stderr)
        self.assertIn("NOT checked", result.stderr)

    def test_a_tool_with_a_syntax_error_allows_the_commit_and_says_so(self):
        """Python prints a SyntaxError with no Traceback header, and exits 1."""
        self.write("tools/validate_moduledata.py", "def (\n")
        self.install_tools("generate_career_kits")
        self.write(XML, "<Items a='8d'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("crashed", result.stderr)

    def test_a_commit_msg_checker_with_a_syntax_error_allows_the_commit(self):
        self.write("tools/check_commit_msg.py", "def (\n")
        self.write("README.txt", "checker broke twice\n")
        self.git("add", "README.txt")
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("NOT checked", result.stderr)
        self.assertIn("SyntaxError", result.stderr)

    def test_a_commit_msg_finding_still_refuses_and_shows_itself(self):
        self.write("tools/check_commit_msg.py", "import sys\nprint('the subject is wrong')\nsys.exit(1)\n")
        self.write("README.txt", "checker refuses\n")
        self.git("add", "README.txt")
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("the subject is wrong", result.stdout + result.stderr)

    def test_a_finding_that_quotes_a_traceback_still_refuses(self):
        """Only output that opens with the traceback is the tool's crash."""
        self.write("tools/validate_moduledata.py",
                   "import sys\nprint('docs/a.md: Traceback (most recent call last):')\nsys.exit(1)\n")
        self.install_tools("generate_career_kits")
        self.write(XML, "<Items a='8b'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("crashed", result.stderr)

    def test_a_store_alias_python_is_never_run(self):
        """A WindowsApps alias can hang, and under a bundled git the gates have no time limit."""
        marker = self.stubs / "alias.ran"
        store = self.root / "WindowsApps"
        store.mkdir()
        (store / "python").write_text(f'#!/bin/sh\necho ran > "{marker.as_posix()}"\nexit 0\n',
                                      encoding="utf-8", newline="\n")
        self.env["PATH"] = str(store) + os.pathsep + self.env["PATH"]
        self.install_tools("validate_moduledata", "generate_career_kits")
        self.write(XML, "<Items a='8c'/>\n")
        self.git("add", XML)
        self.commit()
        self.assertFalse(marker.exists())

    def test_a_commit_msg_checker_that_cannot_judge_allows_the_commit(self):
        self.write("tools/check_commit_msg.py", "import sys\nsys.exit(3)\n")
        self.write("README.txt", "checker broke\n")
        self.git("add", "README.txt")
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("NOT checked", result.stderr)

    def test_no_python_allows_the_commit_and_says_so(self):
        roots = [p for p in Path(shutil.which("git") or "").parents if (p / "usr" / "bin").is_dir()]
        if not roots:
            self.skipTest("cannot find git's own tools on this machine")
        path = os.pathsep.join(str(roots[0] / p) for p in ("cmd", "mingw64/bin", "usr/bin"))
        if shutil.which("python", path=path) or shutil.which("py", path=path):
            self.skipTest("no PATH without python on this machine")
        self.env["PATH"] = path
        self.write(XML, "<Items a='9'/>\n")
        self.git("add", XML)
        result = self.commit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("no python", result.stderr)


class EveryHook(unittest.TestCase):
    def test_each_hook_runs_under_sh_because_a_bundled_git_has_no_bash(self):
        hooks = sorted(p for p in HOOKS.iterdir() if p.is_file())
        self.assertGreaterEqual(len(hooks), 3)
        for hook in hooks:
            with self.subTest(hook=hook.name):
                self.assertEqual(hook.read_text(encoding="utf-8").splitlines()[0], "#!/bin/sh")

    def test_each_hook_is_executable_in_the_index_because_git_skips_it_otherwise(self):
        listed = subprocess.run(["git", "ls-files", "-s", "--", ".githooks"], cwd=REPO,
                                capture_output=True, text=True, check=True).stdout.splitlines()
        self.assertGreaterEqual(len(listed), 3)
        for row in listed:
            with self.subTest(row=row):
                self.assertTrue(row.startswith("100755 "))


class BundledGit(RepoCase):
    """Run the real hooks through the git an IDE ships: its own sh, no bash, System32's timeout.exe."""

    def setUp(self):
        super().setUp()
        self.gits = bundled_gits()
        if not self.gits:
            self.skipTest("no bundled git on this machine")
        self.install_tools("validate_moduledata", "generate_career_kits")
        self.write(XML, "<Items a='b'/>\n")
        self.git("add", XML)

    def bundled(self, root, *args):
        env = dict(self.env)
        system32 = os.path.join(os.environ.get("SystemRoot", "C:/Windows"), "System32")
        python_dir = os.path.dirname(sys.executable)
        env["PATH"] = os.pathsep.join([str(root / "cmd"), str(root / "usr" / "bin"), python_dir, system32])
        return subprocess.run([str(root / "cmd" / "git.exe"), *args], cwd=self.work, env=env,
                              capture_output=True, text=True, encoding="utf-8", errors="replace")

    def test_a_clean_commit_goes_through_and_a_finding_refuses(self):
        for n, root in enumerate(self.gits):
            with self.subTest(git=str(root)):
                self.write(XML, f"<Items a='b{n}'/>\n")
                self.git("add", XML)
                self.env["STUB_RC_VALIDATE_MODULEDATA"] = "1"
                result = self.bundled(root, "commit", "-m", "fix: v1.0.0 - subject")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("validate_moduledata says broken", result.stderr)
                self.env["STUB_RC_VALIDATE_MODULEDATA"] = "0"
                result = self.bundled(root, "commit", "-m", "fix: v1.0.0 - subject")
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_the_commit_msg_hook_runs_too(self):
        root = self.gits[0]
        self.write("tools/check_commit_msg.py", "import sys\nprint('checked')\nsys.exit(1)\n")
        result = self.bundled(root, "commit", "-m", "fix: v1.0.0 - subject")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("checked", result.stdout + result.stderr)
        self.assertNotIn("WSL", result.stdout + result.stderr)


class CodeList(unittest.TestCase):
    @staticmethod
    def codes(path):
        return set(re.findall(r"--code\s+([A-Z_]+)", path.read_text(encoding="utf-8")))

    def test_the_hook_names_every_code_the_claude_gate_names(self):
        ours = self.codes(HOOKS / "pre-commit")
        self.assertGreaterEqual(len(ours), 10, "the code scan found nothing")
        claude_gate = REPO / ".claude" / "hooks" / "check-moduledata-validation.sh"
        if claude_gate.exists():
            self.assertEqual(ours, self.codes(claude_gate))


if __name__ == "__main__":
    unittest.main()
