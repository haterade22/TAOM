#!/usr/bin/env python3
"""analyze_troop_balance.py ends on one error line and a nonzero exit when the roster cannot be settled
(#1533 item 4), not on a RuntimeError traceback.

Run:  python -m unittest tools/tests/test_analyze_troop_balance.py
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import analyze_troop_balance as atb  # noqa: E402
import rebalance_troops as rb  # noqa: E402
import sync_lord_inline_skills as sl  # noqa: E402
import taom_schema as ts  # noqa: E402


class BadRosterTests(unittest.TestCase):
    def test_a_roster_that_cannot_be_settled_prints_one_error_line_and_exits_1(self):
        err = io.StringIO()
        with tempfile.TemporaryDirectory() as modules, \
                mock.patch.object(rb, "DEFAULT_GAME_MODULES", modules), \
                mock.patch.object(ts, "build_item_class_registry", return_value={"x": 1}), \
                mock.patch.object(ts, "build_mount_difficulty_registry", return_value={"x": 1}), \
                mock.patch.object(sl, "load_skill_sets", return_value={}), \
                mock.patch.object(atb, "parse_troop_file", return_value=("gondor", [])), \
                mock.patch.object(atb, "settled_reference",
                                  side_effect=RuntimeError("gondor_x names skill_template SkillSet.nope")), \
                mock.patch.object(sys, "argv", ["analyze_troop_balance.py"]), \
                contextlib.redirect_stderr(err), self.assertRaises(SystemExit) as ctx:
            atb.main()
        self.assertEqual(ctx.exception.code, 1)
        lines = [ln for ln in err.getvalue().splitlines() if ln.strip()]
        self.assertEqual(len(lines), 1, err.getvalue())
        self.assertTrue(lines[0].startswith("ERROR: "), lines[0])
        self.assertIn("gondor_x", lines[0])


if __name__ == "__main__":
    unittest.main()
