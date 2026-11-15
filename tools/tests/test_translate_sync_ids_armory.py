#!/usr/bin/env python
"""--sync-ids writes the live LOTRLOME_Armory language files only with --allow-live-write, backs each one up
first, and skips keys another Armory source owns or another file of the language carries (#1056).

Run:  python -m unittest tools.tests.test_translate_sync_ids_armory

The Armory files live in the game install, outside any repo, so a write there cannot be undone from git. On
2026-10-28 a run meant to seed TAOM keys wrote 48 Armory files with no backup. Every test runs against a fake
install named by BANNERLORD_GAME_DIR, and the model call raises, so no network request is ever made.
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402
import translate_with_claude as twc  # noqa: E402


def _boom(*_a, **_k):
    raise AssertionError("the model call must not be reached")


SOURCE_A = ('<strings>\n'
            '  <string id="a_one" text="{=a_one}One" />\n'
            '  <string id="a_two" text="{=a_two}Two" />\n'
            '  <string id="shared" text="{=shared}Shared" />\n'
            '</strings>\n')
SOURCE_B = ('<strings>\n'
            '  <string id="shared" text="{=shared}Shared" />\n'
            '  <string id="b_one" text="{=b_one}B one" />\n'
            '  <string id="b_two" text="{=b_two}B two" />\n'
            '</strings>\n')
TARGET_A = ('<strings>\r\n'
            '  <string id="a_one" text="Eins" />\r\n'
            '</strings>\r\n')
TARGET_B = ('<strings>\r\n'
            '  <string id="b_one" text="B eins" />\r\n'
            '  <string id="a_two" text="carried here already" />\r\n'
            '</strings>\r\n')


def _run(argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
        try:
            code = twc.main(argv)
        except SystemExit as exc:
            code = exc.code
    return code, out.getvalue()


class _ArmoryInstall(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        root = Path(tmp.name) / "install"
        self.langs = root / "Modules" / "LOTRLOME_Armory" / "ModuleData" / "Languages"
        (self.langs / "DE").mkdir(parents=True)
        (self.langs / "loc_a.xml").write_text(SOURCE_A, encoding="utf-8")
        (self.langs / "loc_b.xml").write_text(SOURCE_B, encoding="utf-8")
        self.target_a = self.langs / "DE" / "loc_a.xml"
        self.target_b = self.langs / "DE" / "loc_b.xml"
        self.target_a.write_bytes(TARGET_A.encode("utf-8"))
        self.target_b.write_bytes(TARGET_B.encode("utf-8"))

        env = mock.patch.dict(os.environ, {_gamedir.ENV_VAR: str(root)})
        env.start()
        self.addCleanup(env.stop)
        os.environ.pop(_gamedir.MODULES_ENV_VAR, None)
        for p in (mock.patch.object(twc, "load_overrides", lambda lang: {}),
                  mock.patch.object(twc, "load_cache", lambda lang, *a, **k: {}),
                  mock.patch.object(twc, "save_cache", _boom),
                  mock.patch.object(twc, "call_model", _boom),
                  mock.patch.object(twc, "call_claude_batched", _boom),
                  mock.patch.object(twc, "discover_entries", _boom),
                  mock.patch.object(twc, "write_back", _boom)):
            p.start()
            self.addCleanup(p.stop)

    def backups(self):
        return sorted(p.name for p in (self.langs / "DE").iterdir() if ".bak_syncids_" in p.name)

    def ids(self, path):
        return set(twc._parse_string_xml(path, strip_keys=False))


class WithoutTheOptIn(_ArmoryInstall):
    def test_apply_leaves_the_armory_files_byte_identical_and_says_why(self):
        before = (self.target_a.read_bytes(), self.target_b.read_bytes())
        code, out = _run(["--lang", "DE", "--module", "Armory", "--sync-ids", "--seed-only", "--apply"])
        self.assertEqual(0, code)
        self.assertEqual(before, (self.target_a.read_bytes(), self.target_b.read_bytes()))
        self.assertEqual([], self.backups())
        self.assertIn("--allow-live-write", out)

    def test_module_all_leaves_the_armory_alone_too(self):
        before = self.target_a.read_bytes()
        with mock.patch.object(twc, "english_source_files", lambda module: []):
            code, out = _run(["--lang", "DE", "--module", "all", "--sync-ids", "--seed-only", "--apply"])
        self.assertEqual(0, code)
        self.assertEqual(before, self.target_a.read_bytes())
        self.assertIn("--allow-live-write", out)

    def test_a_dry_run_still_reports_what_it_would_seed(self):
        before = self.target_a.read_bytes()
        code, out = _run(["--lang", "DE", "--module", "Armory", "--sync-ids", "--seed-only", "--dry-run"])
        self.assertEqual(0, code)
        self.assertEqual(before, self.target_a.read_bytes())
        self.assertIn("to seed into DE/loc_a.xml", out)


class WithTheOptIn(_ArmoryInstall):
    ARGS = ["--lang", "DE", "--module", "Armory", "--sync-ids", "--seed-only", "--apply", "--allow-live-write"]

    def test_the_missing_row_is_seeded_after_a_byte_identical_backup(self):
        before = self.target_a.read_bytes()
        code, out = _run(self.ARGS)
        self.assertEqual(0, code)
        self.assertIn("b_two", self.ids(self.target_b))
        backups = [n for n in self.backups() if n.startswith("loc_a.xml.")]
        self.assertEqual(1, len(backups), self.backups())
        self.assertEqual(before, (self.langs / "DE" / backups[0]).read_bytes())
        self.assertIn("backup", out)

    def test_a_key_another_armory_source_owns_goes_only_to_its_owner(self):
        _run(self.ARGS)
        self.assertIn("shared", self.ids(self.target_a))       # loc_a.xml declares it first
        self.assertNotIn("shared", self.ids(self.target_b))

    def test_a_key_another_file_of_the_language_carries_is_not_seeded_again(self):
        _run(self.ARGS)
        self.assertNotIn("a_two", self.ids(self.target_a))     # DE/loc_b.xml already has a row for it

    def test_a_file_with_nothing_to_seed_gets_no_backup(self):
        self.target_b.write_bytes(('<strings>\r\n  <string id="b_one" text="x" />\r\n'
                                   '  <string id="b_two" text="y" />\r\n  <string id="a_two" text="z" />\r\n'
                                   '</strings>\r\n').encode("utf-8"))
        _run(self.ARGS)
        self.assertEqual([], [n for n in self.backups() if n.startswith("loc_b.xml.")])

    def test_a_sibling_that_does_not_parse_stops_the_run_before_any_write(self):
        self.target_b.write_bytes(b"<strings><string id=")
        before = self.target_a.read_bytes()
        code, out = _run(self.ARGS)
        self.assertNotEqual(0, code)
        self.assertIn("does not parse", str(code))  # SystemExit(message): Python prints it to stderr
        self.assertEqual(before, self.target_a.read_bytes())
        self.assertEqual([], self.backups())

    def test_a_second_run_seeds_nothing_and_backs_up_nothing(self):
        _run(self.ARGS)
        first = self.backups()
        code, out = _run(self.ARGS)
        self.assertEqual(0, code)
        self.assertEqual(first, self.backups())
        self.assertIn("Ids seeded: 0", out)


if __name__ == "__main__":
    unittest.main()
