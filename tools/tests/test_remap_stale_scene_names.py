"""Tests for tools/remap_stale_scene_names.py on a synthetic temp tree (never the real install).

The tool writes the live, unversioned TAOM_Map settlements.xml, so these pin what it must keep:
the BOM, every line ending, a backup before the write, and no write when nothing changes or the
result would not parse."""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import remap_stale_scene_names as rm  # noqa: E402

BOM = b"\xef\xbb\xbf"
ROW = '<Location id="prison" scene_name="{}" scene_name_1="{}" />'


def _doc(name: str, eol: str) -> bytes:
    body = eol.join(["<Settlements>", f"  <Settlement id=\"a\">{ROW.format(name, name)}</Settlement>",
                     "  <Settlement id=\"b\"><Location id=\"x\" scene_name=\"keep_me\" /></Settlement>",
                     "</Settlements>", ""])
    return body.encode("utf-8")


class RemapStaleSceneNamesTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"
        (self.modules / "SandBox" / "SceneObj" / "empire_dungeon_stealth").mkdir(parents=True)
        self.live = self.modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
        self.live.parent.mkdir(parents=True)
        self.running = False
        for patch in (mock.patch.object(rm, "MODULES", self.modules),
                      mock.patch.object(rm, "LIVE", self.live),
                      mock.patch.object(rm, "game_or_kit_running", lambda: self.running)):
            patch.start()
            self.addCleanup(patch.stop)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *args):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(sys, "argv", ["remap_stale_scene_names.py", *args]), \
                redirect_stdout(out), redirect_stderr(err):
            try:
                code = rm.main()
            except SystemExit as exc:
                code = exc.code
        return code, out.getvalue() + err.getvalue()

    def _backups(self):
        return sorted(self.live.parent.glob("settlements.xml.bak_scenes_*"))

    def test_bom_crlf_file_changes_only_the_names(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n")
        self.live.write_bytes(original)
        code, _ = self._run("--apply")
        self.assertEqual(code, 0)
        expected = original.replace(b"empire_dungeon_a", b"empire_dungeon_stealth")
        self.assertEqual(self.live.read_bytes(), expected)

    def test_second_run_writes_nothing_and_makes_no_second_backup(self):
        self.live.write_bytes(BOM + _doc("empire_dungeon_a", "\r\n"))
        self._run("--apply")
        after_first = self.live.read_bytes()
        mtime = self.live.stat().st_mtime_ns
        code, _ = self._run("--apply")
        self.assertEqual(code, 0)
        self.assertEqual(self.live.read_bytes(), after_first)
        self.assertEqual(self.live.stat().st_mtime_ns, mtime)
        self.assertEqual(len(self._backups()), 1)

    def test_apply_leaves_a_backup_with_the_original_bytes(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n")
        self.live.write_bytes(original)
        _, out = self._run("--apply", "--backup")
        backups = self._backups()
        self.assertEqual(len(backups), 1)
        self.assertEqual(backups[0].read_bytes(), original)
        self.assertIn(str(backups[0]), out)
        self.assertRegex(backups[0].name, r"^settlements\.xml\.bak_scenes_\d{8}-\d{6}$")

    def test_lf_file_without_bom_stays_lf_and_bom_free(self):
        self.live.write_bytes(_doc("empire_dungeon_a", "\n"))
        self._run("--apply")
        data = self.live.read_bytes()
        self.assertFalse(data.startswith(BOM))
        self.assertNotIn(b"\r", data)
        self.assertIn(b"empire_dungeon_stealth", data)

    def test_unparsable_result_is_refused(self):
        # Valid UTF-8 text that is not XML: the replacement would still leave it broken.
        original = BOM + b'<Settlements><Location scene_name="empire_dungeon_a" ></Settlements>'
        self.live.write_bytes(original)
        code, out = self._run("--apply")
        self.assertNotEqual(code, 0)
        self.assertIn("would not parse", out)
        self.assertEqual(self.live.read_bytes(), original)
        self.assertEqual(self._backups(), [])

    def test_invalid_utf8_is_refused_not_replaced(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n") + b"\xff\xfe"
        self.live.write_bytes(original)
        code, out = self._run("--apply")
        self.assertNotEqual(code, 0)
        self.assertIn("not valid UTF-8", out)
        self.assertEqual(self.live.read_bytes(), original)

    def test_dry_run_with_apply_is_refused(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n")
        self.live.write_bytes(original)
        code, _ = self._run("--dry-run", "--apply")
        self.assertEqual(code, 2)
        self.assertEqual(self.live.read_bytes(), original)

    def test_dry_run_writes_nothing(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n")
        self.live.write_bytes(original)
        code, _ = self._run("--dry-run")
        self.assertEqual(code, 0)
        self.assertEqual(self.live.read_bytes(), original)
        self.assertEqual(self._backups(), [])

    def test_mixed_case_old_name_is_replaced(self):
        self.live.write_bytes(_doc("Empire_Dungeon_A", "\r\n"))
        self._run("--apply")
        data = self.live.read_bytes()
        self.assertNotIn(b"Empire_Dungeon_A", data)
        self.assertEqual(data.count(b"empire_dungeon_stealth"), 2)

    def test_running_game_blocks_apply_but_not_dry_run(self):
        original = BOM + _doc("empire_dungeon_a", "\r\n")
        self.live.write_bytes(original)
        self.running = True
        code, out = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertIn("running", out)
        self.assertEqual(self.live.read_bytes(), original)
        self.assertEqual(self._run("--dry-run")[0], 0)


if __name__ == "__main__":
    unittest.main()
