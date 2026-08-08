#!/usr/bin/env python3
"""Tests for tools/_gamedir.py — the one place that answers "where is the install".

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

The cases that matter are the ones a plain os.environ.get(VAR, default) gets
wrong: an exported-but-blank variable, and a root that is not there. Both were
reported in issue #404 as producing a clean-looking result rather than saying
the root is wrong.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402

DEFAULT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"


class _EnvIsolated(unittest.TestCase):
    """Each test gets the variable in a known state and restores it after."""

    def setUp(self):
        self._saved = os.environ.get(_gamedir.ENV_VAR)
        os.environ.pop(_gamedir.ENV_VAR, None)

    def tearDown(self):
        if self._saved is None:
            os.environ.pop(_gamedir.ENV_VAR, None)
        else:
            os.environ[_gamedir.ENV_VAR] = self._saved


class GameDirTests(_EnvIsolated):

    def test_unset_returns_the_literal_unchanged(self):
        # The whole point of the fallback: behaviour is unchanged off a
        # configured machine, byte for byte.
        self.assertEqual(_gamedir.game_dir(DEFAULT), DEFAULT)

    def test_set_returns_the_override(self):
        os.environ[_gamedir.ENV_VAR] = r"D:\SteamLibrary\Bannerlord"
        self.assertEqual(_gamedir.game_dir(DEFAULT), r"D:\SteamLibrary\Bannerlord")

    def test_blank_is_treated_as_unset(self):
        # os.environ.get(VAR, default) returns "" here, and Path("") is "." —
        # the tool then reports every file missing instead of the root being
        # wrong. #404 point 4.
        os.environ[_gamedir.ENV_VAR] = ""
        self.assertEqual(_gamedir.game_dir(DEFAULT), DEFAULT)

    def test_whitespace_only_is_treated_as_unset(self):
        os.environ[_gamedir.ENV_VAR] = "   "
        self.assertEqual(_gamedir.game_dir(DEFAULT), DEFAULT)

    def test_value_is_returned_verbatim(self):
        # No normalising: callers compose their own paths and several keep a
        # forward-slash literal, so the separator style they get back is theirs.
        os.environ[_gamedir.ENV_VAR] = "D:/SteamLibrary/Bannerlord"
        self.assertEqual(_gamedir.game_dir(DEFAULT), "D:/SteamLibrary/Bannerlord")


class EnsureExistsTests(_EnvIsolated):

    def test_returns_a_path_when_the_root_is_there(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(_gamedir.ensure_exists(d), Path(d))

    def test_exits_2_when_the_root_is_absent(self):
        missing = os.path.join(tempfile.gettempdir(), "taom_no_such_root_404")
        with self.assertRaises(SystemExit) as ctx:
            _gamedir.ensure_exists(missing)
        self.assertEqual(ctx.exception.code, 2)

    def test_the_message_names_the_path_and_the_variable(self):
        missing = os.path.join(tempfile.gettempdir(), "taom_no_such_root_404")
        import io
        import contextlib
        err = io.StringIO()
        with contextlib.redirect_stderr(err), self.assertRaises(SystemExit):
            _gamedir.ensure_exists(missing)
        self.assertIn(missing, err.getvalue())
        self.assertIn(_gamedir.ENV_VAR, err.getvalue())

    def test_the_message_can_say_what_was_being_looked_for(self):
        missing = os.path.join(tempfile.gettempdir(), "taom_no_such_root_404")
        import io
        import contextlib
        err = io.StringIO()
        with contextlib.redirect_stderr(err), self.assertRaises(SystemExit):
            _gamedir.ensure_exists(missing, what="the Erebor item folder")
        self.assertIn("the Erebor item folder", err.getvalue())


class GameModulesTests(_EnvIsolated):
    """Precedence for the Modules folder, which taom_mcp_server.py needs.

    BANNERLORD_GAME_MODULES predates BANNERLORD_GAME_DIR and names the Modules
    folder directly. It stays as an explicit override, but setting only
    BANNERLORD_GAME_DIR has to be enough for a correct setup — an MCP server
    answering item_exists against the old install is hard to notice (#404).
    """

    def setUp(self):
        super().setUp()
        self._saved_modules = os.environ.get(_gamedir.MODULES_ENV_VAR)
        os.environ.pop(_gamedir.MODULES_ENV_VAR, None)

    def tearDown(self):
        if self._saved_modules is None:
            os.environ.pop(_gamedir.MODULES_ENV_VAR, None)
        else:
            os.environ[_gamedir.MODULES_ENV_VAR] = self._saved_modules
        super().tearDown()

    def test_neither_set_falls_back_to_the_literal(self):
        self.assertEqual(_gamedir.game_modules(DEFAULT), Path(DEFAULT) / "Modules")

    def test_game_dir_alone_is_enough(self):
        os.environ[_gamedir.ENV_VAR] = r"D:\SteamLibrary\Bannerlord"
        self.assertEqual(_gamedir.game_modules(DEFAULT),
                         Path(r"D:\SteamLibrary\Bannerlord") / "Modules")

    def test_the_modules_override_wins(self):
        os.environ[_gamedir.ENV_VAR] = r"D:\SteamLibrary\Bannerlord"
        os.environ[_gamedir.MODULES_ENV_VAR] = r"E:\elsewhere\Modules"
        self.assertEqual(_gamedir.game_modules(DEFAULT), Path(r"E:\elsewhere\Modules"))

    def test_a_blank_modules_override_defers_to_game_dir(self):
        os.environ[_gamedir.ENV_VAR] = r"D:\SteamLibrary\Bannerlord"
        os.environ[_gamedir.MODULES_ENV_VAR] = ""
        self.assertEqual(_gamedir.game_modules(DEFAULT),
                         Path(r"D:\SteamLibrary\Bannerlord") / "Modules")


if __name__ == "__main__":
    unittest.main()
