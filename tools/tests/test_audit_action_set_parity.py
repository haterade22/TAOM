#!/usr/bin/env python3
"""tools/audit_action_set_parity.py finds the install and tells a missing file from a finding.

Run:  python -m unittest tools.tests.test_audit_action_set_parity
"""
import contextlib
import importlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_action_set_parity as asp  # noqa: E402


class InstallPathTests(unittest.TestCase):
    def tearDown(self):
        importlib.reload(asp)

    def test_the_defaults_follow_the_install_variable(self):
        with mock.patch.dict(os.environ, {"BANNERLORD_GAME_DIR": r"C:\Games\BL"}):
            importlib.reload(asp)
        self.assertEqual(os.path.join(r"C:\Games\BL", "Modules", "Native", "ModuleData", "action_sets.xml"),
                         asp.DEFAULT_NATIVE)
        self.assertEqual(os.path.join(r"C:\Games\BL", "Modules", "LOTRLOME_Armory", "ModuleData", "action_sets.xml"),
                         asp.DEFAULT_LIVE)


class UnreadableFileTests(unittest.TestCase):
    def test_a_missing_file_exits_two_and_names_the_path(self):
        err = io.StringIO()
        with contextlib.redirect_stderr(err), self.assertRaises(SystemExit) as caught:
            asp.load(os.path.join(tempfile.gettempdir(), "taom-no-such-action-sets.xml"))
        self.assertEqual(2, caught.exception.code)
        self.assertIn("taom-no-such-action-sets.xml", err.getvalue())

    def test_a_malformed_file_stays_a_finding(self):
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "action_sets.xml"
            bad.write_text("<action_sets><oops>", encoding="utf-8")
            with self.assertRaises(SystemExit) as caught:
                asp.load(str(bad))
        self.assertNotEqual(2, caught.exception.code)


if __name__ == "__main__":
    unittest.main()
