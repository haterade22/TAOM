"""Tests for tools/audit_scene_names.py on a synthetic temp tree (never the real install).

A run that read nothing printed "(none)" and exited 0 (the default root is one machine's, so on any
other PC the tool checked nothing and read as clean). It must refuse instead, and it must not read
TAOM's own ModuleData, which loads no settlements."""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_scene_names as asn  # noqa: E402

SETTLEMENT = '<Settlements><Settlement id="town_a"><Location id="prison" scene_name="{}" /></Settlement></Settlements>'


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class AuditSceneNamesTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"
        self.live = self.modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
        files = {label: self.modules / p.relative_to(asn.MODULES) for label, p in asn.SETTLEMENT_FILES.items()}
        for patch in (mock.patch.object(asn, "MODULES", self.modules),
                      mock.patch.object(asn, "SETTLEMENT_FILES", files)):
            patch.start()
            self.addCleanup(patch.stop)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            try:
                code = asn.main()
            except SystemExit as exc:
                code = exc.code
        return code, out.getvalue(), err.getvalue()

    def _scene(self, name: str) -> None:
        _write(self.modules / "SandBox" / "SceneObj" / name / "scene.xscene", "<scene/>")

    def test_a_missing_install_is_refused_not_passed(self):
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("(none", out)
        self.assertIn("not found", err)

    def test_a_missing_map_file_is_refused_not_passed(self):
        self._scene("empire_dungeon_stealth")
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertIn("settlements.xml", err)

    def test_no_scene_folder_anywhere_is_refused_not_passed(self):
        _write(self.live, SETTLEMENT.format("empire_dungeon_stealth"))
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("(none", out)
        self.assertIn("nothing was checked", err)

    def test_a_scene_no_folder_holds_is_a_crash_suspect(self):
        self._scene("empire_dungeon_stealth")
        _write(self.live, SETTLEMENT.format("gone_scene"))
        code, out, _ = self._run()
        self.assertEqual(code, 0)
        self.assertIn("gone_scene", out.split("DIFF")[0])
        self.assertNotIn("(none", out)

    def test_every_scene_present_reads_none(self):
        self._scene("empire_dungeon_stealth")
        _write(self.live, SETTLEMENT.format("empire_dungeon_stealth"))
        code, out, _ = self._run()
        self.assertEqual(code, 0)
        self.assertIn("(none", out)

    def test_taoms_own_moduledata_is_not_read(self):
        # TAOM's SubModule.xml registers no Settlements; a stale copy left in an install loads nothing.
        self._scene("empire_dungeon_stealth")
        _write(self.live, SETTLEMENT.format("empire_dungeon_stealth"))
        _write(self.modules / "TAOM" / "ModuleData" / "settlements.xml", SETTLEMENT.format("gone_scene"))
        code, out, _ = self._run()
        self.assertEqual(code, 0)
        self.assertNotIn("gone_scene", out)
        self.assertEqual(sorted(asn.SETTLEMENT_FILES), ["TAOM_Map", "vanilla_NavalDLC", "vanilla_SandBox"])


if __name__ == "__main__":
    unittest.main()
