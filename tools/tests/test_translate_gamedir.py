#!/usr/bin/env python3
"""Tests for the install-path resolution in tools/translate_with_claude.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

The tool reads its TAOM_Map and LOTRLOME_Armory sources from the game install.
That root was a hardcoded literal with no override, and both collection
branches guarded on `.exists()`, so a root that is not there produced

    Untranslated entries discovered: 0
    Estimated API cost: ~$0.00
    (dry run — no files written)

and exit 0 — for two of the three modules, on every machine but the author's.
That is the shape #404 point 4 describes, in the tool #432 and #434 both need.

`anthropic` is imported inside main(), so this module imports without the SDK.
The root resolves per call rather than at import: this module wraps
sys.stdout/sys.stderr at import time, so reloading it to pick up a changed
variable closes the real streams and takes the test runner with it.
"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402
import translate_with_claude as twc  # noqa: E402


class _EnvIsolated(unittest.TestCase):
    """Each test gets both variables in a known state and restores them after."""

    def setUp(self):
        self._saved = {v: os.environ.get(v)
                       for v in (_gamedir.ENV_VAR, _gamedir.MODULES_ENV_VAR)}
        for v in self._saved:
            os.environ.pop(v, None)

    def tearDown(self):
        for var, val in self._saved.items():
            if val is None:
                os.environ.pop(var, None)
            else:
                os.environ[var] = val


class WrongRootIsReported(_EnvIsolated):

    def test_a_root_that_is_not_there_exits_2_instead_of_reporting_nothing_to_do(self):
        with tempfile.TemporaryDirectory() as tmp:
            os.environ[_gamedir.ENV_VAR] = str(Path(tmp) / "no-such-install")
            with redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as ctx:
                    twc.discover_entries("DE", "all")
            self.assertEqual(2, ctx.exception.code,
                             "a wrong root is bad input (2), not a clean run (0) and not a data "
                             "fault (1)")

    def test_the_message_names_the_folder_it_could_not_read(self):
        with tempfile.TemporaryDirectory() as tmp:
            os.environ[_gamedir.ENV_VAR] = str(Path(tmp) / "no-such-install")
            err = io.StringIO()
            with redirect_stderr(err):
                with self.assertRaises(SystemExit):
                    twc.discover_entries("DE", "all")
            self.assertIn("no-such-install", err.getvalue(),
                          "the operator cannot fix a path the error does not name")


class ScopeIsRespected(_EnvIsolated):

    def test_the_TAOM_module_alone_does_not_need_the_game_install(self):
        # Its sources are in the repo. Failing here would make the tool unusable
        # for the module that holds the 96 keys of #432.
        with tempfile.TemporaryDirectory() as tmp:
            os.environ[_gamedir.ENV_VAR] = str(Path(tmp) / "no-such-install")
            twc.discover_entries("DE", "TAOM")   # must not raise


class APresentRootWithAMissingModuleSaysSo(_EnvIsolated):

    def test_a_module_absent_from_a_valid_install_warns_rather_than_passing_silently(self):
        # An install without TAOM_Map is a real state, so it is not fatal — but it
        # must not be the silent skip that produced "0 entries, $0.00, exit 0".
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "install"
            (root / "Modules").mkdir(parents=True)
            os.environ[_gamedir.ENV_VAR] = str(root)
            err = io.StringIO()
            with redirect_stderr(err):
                twc.discover_entries("DE", "all")
            message = err.getvalue()
            self.assertIn("TAOM_Map skipped", message)
            self.assertIn("Armory skipped", message)


class RootIsResolvedFromTheVariable(_EnvIsolated):

    def test_the_modules_folder_is_derived_from_the_install_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "install"
            (root / "Modules").mkdir(parents=True)
            os.environ[_gamedir.ENV_VAR] = str(root)
            self.assertEqual(root / "Modules", Path(twc.game_modules_root()))

    def test_an_unset_variable_keeps_the_shipped_default(self):
        self.assertEqual(Path(twc.DEFAULT_GAME_ROOT) / "Modules",
                         Path(twc.game_modules_root()))


if __name__ == "__main__":
    unittest.main()
