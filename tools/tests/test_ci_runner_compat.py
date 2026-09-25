"""CI runs the tool tests with the standard library only: `python -m unittest discover -s tools/tests -t .`
(.github/workflows/build.yml), with no pytest installed. A module that imports pytest fails to import there, and one
written as module-level `def test_*` functions collects 0 tests, so its checks never run on CI while passing locally
under pytest (the hill troll binder's 13 tests, 2026-09-25 deep review).

This is a ratchet: the modules below predate the gate and are the only ones allowed to import pytest. Convert one to
`unittest.TestCase` and remove it from the list; never add to it.
"""
import os
import re
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
PYTEST_BASELINE = {
    "test_analyze_melee_ladder.py",
    "test_audit_map_scene_memory.py",
    "test_melee_damage.py",
    "test_melee_ladder.py",
    "test_restat_melee_blades.py",
}
PYTEST_IMPORT = re.compile(r"^\s*(import pytest\b|from pytest\b)", re.M)


def pytest_modules(folder=HERE):
    found = set()
    for name in os.listdir(folder):
        if name.startswith("test_") and name.endswith(".py"):
            with open(os.path.join(folder, name), encoding="utf-8", errors="replace") as fh:
                if PYTEST_IMPORT.search(fh.read()):
                    found.add(name)
    return found


class CiRunnerCompatTests(unittest.TestCase):
    def test_no_new_tool_test_module_needs_pytest(self):
        new = sorted(pytest_modules() - PYTEST_BASELINE)
        self.assertEqual(new, [], "these tool test modules import pytest, which CI's unittest runner does not have; "
                                  "write them as unittest.TestCase classes (tempfile for tmp_path, "
                                  "contextlib.redirect_stdout for capsys)")

    def test_the_baseline_names_only_modules_that_still_need_it(self):
        stale = sorted(PYTEST_BASELINE - pytest_modules())
        self.assertEqual(stale, [], "converted or removed; drop them from PYTEST_BASELINE so the ratchet tightens")

    def test_the_scan_sees_an_import_and_ignores_a_mention(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            for name, text in (("test_a.py", "import os\nimport pytest\n"),
                               ("test_b.py", '"""run with pytest or unittest"""\nimport unittest\n'),
                               ("test_c.py", "def f():\n    from pytest import approx\n")):
                with open(os.path.join(d, name), "w", encoding="utf-8") as fh:
                    fh.write(text)
            self.assertEqual(pytest_modules(d), {"test_a.py", "test_c.py"})


if __name__ == "__main__":
    unittest.main()
