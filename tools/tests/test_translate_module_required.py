#!/usr/bin/env python3
"""translate_with_claude.py refuses to run without --module.

Run:  python -m unittest tools.tests.test_translate_module_required

The old default, `all`, also writes the live Armory (#1056). A run that names no module now
stops at the parser, so only an explicit `--module all` reaches it.
"""
import contextlib
import io
import os
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import translate_with_claude as twc  # noqa: E402


class ModuleRequiredTests(unittest.TestCase):
    def _run(self, argv):
        err = io.StringIO()
        with contextlib.redirect_stderr(err), self.assertRaises(SystemExit) as raised:
            twc.main(argv)
        return raised.exception.code, err.getvalue()

    def test_no_module_is_a_parser_error(self):
        code, err = self._run(["--lang", "PL", "--dry-run"])
        self.assertEqual(code, 2)
        self.assertIn("--module", err)

    def test_the_apply_form_is_refused_too(self):
        code, err = self._run(["--lang", "PL", "--apply"])
        self.assertEqual(code, 2)
        self.assertIn("--module", err)


if __name__ == "__main__":
    unittest.main()
