#!/usr/bin/env python
"""--max-entries 0 is a real cap of zero, and --seed-only seeds ids without translating.

Run:  python -m unittest discover -s tools/tests -p "test_translate_caps_and_seed.py"

Both guard the paid phase. Until now `--max-entries 0` read as "no cap" (a falsy check), so the
run a caller meant to make free called the provider for every entry. Every test here patches the
model call to raise: reaching it fails the test, and no network request is ever made.
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


SOURCE = ('<strings>\n'
          '  <string id="k_one" text="{=k_one}One" />\n'
          '  <string id="k_two" text="{=k_two}Two" />\n'
          '</strings>\n')
TARGET = ('<strings>\n'
          '  <string id="k_one" text="One" />\n'
          '</strings>\n')


def _run(argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
        try:
            code = twc.main(argv)
        except SystemExit as exc:
            code = exc.code
    return code, out.getvalue()


class _Tree(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        root = Path(self._tmp.name)
        self.src = root / "src.xml"
        self.src.write_text(SOURCE, encoding="utf-8")
        self.lang_dir = root / "Languages"
        (self.lang_dir / "DE").mkdir(parents=True)
        self.target = self.lang_dir / "DE" / "tpl_deu-DE.xml"
        self.target.write_bytes(TARGET.encode("utf-8"))

        patches = [
            mock.patch.object(twc, "TAOM_LANG_DIR", self.lang_dir),
            mock.patch.object(twc, "english_source_files",
                              lambda module: [("TAOM", self.src, "tpl_{locale}.xml")]),
            mock.patch.object(twc, "load_overrides", lambda lang: {}),
            mock.patch.object(twc, "load_cache", lambda lang, *a, **k: {}),
            mock.patch.object(twc, "save_cache", _boom),
            mock.patch.object(twc, "call_model", _boom),
            mock.patch.object(twc, "call_claude_batched", _boom),
            mock.patch.object(twc, "write_back", _boom),
        ]
        for p in patches:
            p.start()
            self.addCleanup(p.stop)


class MaxEntriesZero(_Tree):
    def test_zero_is_a_cap_of_zero_and_never_reaches_the_provider(self):
        entries = [twc.Entry(self.target, f"k{i}", "English", "English") for i in range(3)]
        with mock.patch.object(twc, "discover_entries", lambda lang, module: entries):
            code, out = _run(["--lang", "DE", "--module", "TAOM", "--apply", "--max-entries", "0"])
        self.assertEqual(0, code)
        self.assertIn("cap applied: 0/3", out)

    def test_negative_cap_is_an_error_not_an_almost_unlimited_slice(self):
        # need_llm[:-1] would send all but one entry to the paid provider.
        code, _ = _run(["--lang", "DE", "--module", "TAOM", "--dry-run", "--max-entries", "-1"])
        self.assertEqual(2, code)

    def test_no_cap_flag_leaves_the_list_alone(self):
        entries = [twc.Entry(self.target, f"k{i}", "English", "English") for i in range(3)]
        with mock.patch.object(twc, "discover_entries", lambda lang, module: entries):
            code, out = _run(["--lang", "DE", "--module", "TAOM", "--dry-run"])
        self.assertEqual(0, code)
        self.assertNotIn("cap applied", out)


class SeedOnly(_Tree):
    def setUp(self):
        super().setUp()
        p = mock.patch.object(twc, "discover_entries", _boom)
        p.start()
        self.addCleanup(p.stop)

    def test_seed_only_without_sync_ids_is_an_error(self):
        code, _ = _run(["--lang", "DE", "--module", "TAOM", "--apply", "--seed-only"])
        self.assertEqual(2, code)

    def test_apply_seeds_the_missing_id_and_returns_before_discovery(self):
        code, out = _run(["--lang", "DE", "--module", "TAOM", "--sync-ids", "--seed-only", "--apply"])
        self.assertEqual(0, code)
        self.assertIn('id="k_two"', self.target.read_text(encoding="utf-8"))
        self.assertIn("Ids seeded: 1", out)

    def test_dry_run_reports_without_writing(self):
        before = self.target.read_bytes()
        code, out = _run(["--lang", "DE", "--module", "TAOM", "--sync-ids", "--seed-only", "--dry-run"])
        self.assertEqual(0, code)
        self.assertEqual(before, self.target.read_bytes())
        self.assertIn("to seed", out)

    # Ported from the career forks branch (its RCA rca-career-forks-generator-2026-10-15.md): TAOM_Map has no seed pass, so a
    # seed-only run there would exit 0 having done nothing.
    def test_seed_only_taom_map_is_refused(self):
        before = self.target.read_bytes()
        code, _ = _run(["--lang", "DE", "--module", "TAOM_Map", "--sync-ids", "--seed-only", "--apply"])
        self.assertEqual(2, code)
        self.assertEqual(before, self.target.read_bytes())

    # A sync that includes the Armory needs the install before it writes the TAOM rows, or it writes them and then fails.
    def test_sync_ids_all_or_armory_with_a_missing_install_exits_2_and_writes_nothing(self):
        before = self.target.read_bytes()
        missing = str(Path(self._tmp.name) / "no-such-install")
        for module in ("all", "Armory"):
            with self.subTest(module=module), mock.patch.dict(os.environ, {_gamedir.ENV_VAR: missing}):
                os.environ.pop(_gamedir.MODULES_ENV_VAR, None)
                code, _ = _run(["--lang", "DE", "--module", module, "--sync-ids", "--seed-only", "--apply"])
                self.assertEqual(2, code)
                self.assertEqual(before, self.target.read_bytes())


if __name__ == "__main__":
    unittest.main()
