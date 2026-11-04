#!/usr/bin/env python
"""Shell lint gate (issue #1020).

Runs ShellCheck (warning severity, no rc file) over every .sh git lists (tracked and untracked,
not ignored). A finding fails the test with ShellCheck's own message. The one allowed pattern is
SC2206 on a line that starts with `set -f;`, the split hook-authoring.md mandates for hook text
("Never feed hook text through a here-string").

ShellCheck is an external binary, not a Python package. When it cannot be found the test FAILS and
names the install, so a machine without it cannot pass the gate by accident. Set
TAOM_ALLOW_NO_SHELLCHECK=1 to skip knowingly (same convention as test_python_lint_gate.py).
Location: TAOM_SHELLCHECK, else `shellcheck` on PATH, else the default below.

Run:  python -m unittest tools.tests.test_shell_lint_gate
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SHELLCHECK_ENV = "TAOM_SHELLCHECK"
DEFAULT_SHELLCHECK = r"E:\Tools\shellcheck-0.11.0\shellcheck.exe"
SPLIT_CODE = 2206
NO_SHELLCHECK = ("ShellCheck was not found, so the shell lint gate did not run. Install ShellCheck "
                 "0.11.0 (https://github.com/koalaman/shellcheck/releases/tag/v0.11.0) to "
                 + DEFAULT_SHELLCHECK + ", or set TAOM_SHELLCHECK to its path "
                 "(docs/reference/development-machines.md; "
                 "set TAOM_ALLOW_NO_SHELLCHECK=1 to skip knowingly).")


def find_shellcheck():
    env = os.environ.get(SHELLCHECK_ENV)
    if env and Path(env).is_file():
        return env
    on_path = shutil.which("shellcheck")
    if on_path:
        return on_path
    return DEFAULT_SHELLCHECK if Path(DEFAULT_SHELLCHECK).is_file() else None


def shell_scripts(root):
    """Every .sh and git hook git lists under `root`, tracked or untracked and not ignored, that exists
    on disk."""
    out = subprocess.run(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard",
                          "--", "*.sh", ".githooks/*"], cwd=str(root), capture_output=True, check=True,
                         timeout=60).stdout.decode("utf-8")
    return sorted({p for p in out.split("\0") if p and (Path(root) / p).is_file()})


def shellcheck_findings(exe, root, paths):
    """Run ShellCheck on `paths`; return [(file, line, code, message)], allowed split hits dropped."""
    proc = subprocess.run([exe, "--norc", "-S", "warning", "-f", "json1", *paths], cwd=str(root),
                          capture_output=True, text=True, encoding="utf-8", errors="replace",
                          timeout=300)
    if proc.returncode > 1:
        raise AssertionError("ShellCheck could not run (exit %d):\n%s" % (proc.returncode, proc.stderr))
    found = []
    for c in json.loads(proc.stdout)["comments"]:
        if c["code"] == SPLIT_CODE:
            text = Path(root, c["file"]).read_text(encoding="utf-8", errors="replace")
            if text.splitlines()[c["line"] - 1].lstrip().startswith("set -f;"):
                continue
        found.append((c["file"], c["line"], c["code"], c["message"]))
    return sorted(found)


def write_script(root, name, text):
    """Write with LF endings: text mode on Windows would add CRs, which ShellCheck reports."""
    with open(Path(root, name), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def git(root, *args):
    subprocess.run(["git", *args], cwd=str(root), check=True, capture_output=True, timeout=60)


class ShellLintGateTests(unittest.TestCase):
    def setUp(self):
        self.exe = find_shellcheck()
        if not self.exe:
            if os.environ.get("TAOM_ALLOW_NO_SHELLCHECK") == "1":
                self.skipTest(NO_SHELLCHECK)
            self.fail(NO_SHELLCHECK)

    def test_shell_scripts_are_clean(self):
        paths = shell_scripts(REPO)
        self.assertTrue(paths, "git found no .sh files")
        found = shellcheck_findings(self.exe, REPO, paths)
        self.assertEqual(found, [], "ShellCheck findings:\n" + "\n".join(
            "%s:%d: SC%d %s" % f for f in found))

    def test_only_the_set_f_split_passes(self):
        text = ("#!/bin/bash\n"
                "set -f; A=($X); set +f\n"
                "B=($Y)\n"
                "set -f; C=($(ls)); set +f\n"
                "ls 2>&1 >/dev/null\n"
                'echo "${A[@]}" "${B[@]}" "${C[@]}"\n')
        with tempfile.TemporaryDirectory() as tmp:
            write_script(tmp, "t.sh", text)
            found = shellcheck_findings(self.exe, tmp, ["t.sh"])
        self.assertEqual([(line, code) for _, line, code, _ in found],
                         [(3, 2206), (4, 2207), (5, 2069)])

    def test_discovery_finds_untracked_and_drops_deleted(self):
        with tempfile.TemporaryDirectory() as tmp:
            git(tmp, "init", "-q")
            write_script(tmp, "gone.sh", "#!/bin/bash\n:\n")
            git(tmp, "add", "gone.sh")
            os.remove(Path(tmp, "gone.sh"))
            write_script(tmp, "new.sh", "#!/bin/bash\n:\n")
            self.assertEqual(shell_scripts(tmp), ["new.sh"])

    def test_non_ascii_filename_is_found_and_linted(self):
        with tempfile.TemporaryDirectory() as tmp:
            git(tmp, "init", "-q")
            write_script(tmp, "caf\u00e9.sh", "#!/bin/bash\nB=($Y)\necho \"${B[@]}\"\n")
            paths = shell_scripts(tmp)
            self.assertEqual(paths, ["caf\u00e9.sh"])
            found = shellcheck_findings(self.exe, tmp, paths)
        self.assertEqual([(line, code) for _, line, code, _ in found], [(2, 2206)])


if __name__ == "__main__":
    unittest.main()
