#!/usr/bin/env python3
"""check_prefab_budget.py sums every module's Prefabs folder and finds the install.

Run:  python -m unittest tools.tests.test_check_prefab_budget
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import check_prefab_budget as cpb  # noqa: E402


def _entities(path: Path, count: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("<prefabs>" + "<game_entity name='x'></game_entity>" * count + "</prefabs>", encoding="utf-8")


def _run(*argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = cpb.main(list(argv))
    return code, out.getvalue()


class PrefabBudgetTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"
        _entities(self.modules / "A" / "Prefabs" / "x.xml", 2)
        _entities(self.modules / "B" / "Prefabs" / "y.xml", 3)

    def tearDown(self):
        self._tmp.cleanup()

    def test_all_modules_prints_the_total_and_each_module(self):
        code, out = _run("--all-modules", "--modules", str(self.modules))
        self.assertEqual(code, 0, out)
        self.assertIn("TOTAL: 5", out)
        self.assertRegex(out, r"(?m)^\s*2\s+A$")
        self.assertRegex(out, r"(?m)^\s*3\s+B$")

    def test_summing_is_the_default_without_dir(self):
        code, out = _run("--modules", str(self.modules))
        self.assertEqual(code, 0, out)
        self.assertIn("TOTAL: 5", out)

    def test_a_tree_over_the_cap_exits_two(self):
        with mock.patch.object(cpb, "CAP", 5), mock.patch.object(cpb, "WARN", 4):
            self.assertEqual(_run("--all-modules", "--modules", str(self.modules))[0], 2)

    def test_a_tree_over_warn_exits_one(self):
        with mock.patch.object(cpb, "CAP", 100), mock.patch.object(cpb, "WARN", 4):
            self.assertEqual(_run("--all-modules", "--modules", str(self.modules))[0], 1)

    def test_verdict_bands(self):
        self.assertEqual(cpb.verdict(10, 10, 5)[0], 2)
        self.assertEqual(cpb.verdict(6, 10, 5)[0], 1)
        self.assertEqual(cpb.verdict(5, 10, 5)[0], 0)

    def test_dir_keeps_the_single_folder_count(self):
        code, out = _run("--dir", str(self.modules / "B" / "Prefabs"))
        self.assertEqual(code, 0, out)
        self.assertIn("TOTAL: 3", out)
        self.assertIn("y.xml", out)

    def test_a_missing_modules_folder_exits_two_and_names_the_path(self):
        gone = Path(self._tmp.name) / "nowhere" / "Modules"
        code, out = _run("--modules", str(gone))
        self.assertEqual(code, 2)
        self.assertIn(str(gone), out)

    def test_no_args_with_a_missing_install_exits_two_and_names_the_path(self):
        gone = Path(self._tmp.name) / "nowhere" / "Modules"
        with mock.patch.object(cpb, "game_modules", return_value=gone):
            code, out = _run()
        self.assertEqual(code, 2)
        self.assertIn(str(gone), out)


if __name__ == "__main__":
    unittest.main()
