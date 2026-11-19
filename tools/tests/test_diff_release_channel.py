#!/usr/bin/env python3
"""tools/diff_release_channel.py: what a release channel lacks against the live install (#1480).

Every test builds a synthetic live Modules folder and a synthetic channel, because a channel under
E:/LOTRAOM_Releases is the maintainer's package."""
import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import diff_release_channel as drc  # noqa: E402


def _write(path: Path, data: str, mtime: float = 1_700_000_000.0) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data.encode("utf-8"))
    os.utime(path, (mtime, mtime))


class DiffTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.live = root / "live" / "Modules"
        self.channel_root = root / "Releases" / "public"
        self.channel = self.channel_root / "Modules"
        self.live.mkdir(parents=True)
        self.channel.mkdir(parents=True)
        (self.live / "TAOM").mkdir()
        (self.channel / "TAOM").mkdir()
        self.stubs = root / "Stubs"

    def tearDown(self):
        self._tmp.cleanup()

    def _diff(self, **kw):
        kw.setdefault("modules", ("TAOM",))
        kw.setdefault("stubs_root", self.stubs)
        return drc.diff(self.live, self.channel, **kw)

    def _verdicts(self, report):
        return {(e.module, e.rel): e.verdict for e in report.entries}

    def test_new_changed_same_and_channel_only(self):
        _write(self.live / "TAOM/ModuleData/a.xml", "same")
        _write(self.channel / "TAOM/ModuleData/a.xml", "same")
        _write(self.live / "TAOM/ModuleData/b.xml", "longer text")
        _write(self.channel / "TAOM/ModuleData/b.xml", "short")
        _write(self.live / "TAOM/ModuleData/c.xml", "new")
        _write(self.channel / "TAOM/ModuleData/gone.xml", "stale")
        v = self._verdicts(self._diff())
        self.assertEqual(v[("TAOM", "ModuleData/a.xml")], drc.SAME)
        self.assertEqual(v[("TAOM", "ModuleData/b.xml")], drc.CHANGED)
        self.assertEqual(v[("TAOM", "ModuleData/c.xml")], drc.NEW)
        self.assertEqual(v[("TAOM", "ModuleData/gone.xml")], drc.CHANNEL_ONLY)

    def test_same_size_different_mtime_is_settled_by_hash(self):
        _write(self.live / "TAOM/ModuleData/x.xml", "abcd", mtime=1_700_000_500.0)
        _write(self.channel / "TAOM/ModuleData/x.xml", "abce")
        _write(self.live / "TAOM/ModuleData/y.xml", "abcd", mtime=1_700_000_500.0)
        _write(self.channel / "TAOM/ModuleData/y.xml", "abcd")
        v = self._verdicts(self._diff())
        self.assertEqual(v[("TAOM", "ModuleData/x.xml")], drc.CHANGED)
        self.assertEqual(v[("TAOM", "ModuleData/y.xml")], drc.SAME)

    def test_paths_compare_case_insensitively(self):
        _write(self.live / "TAOM/GUI/Prefabs/p.xml", "one")
        _write(self.channel / "TAOM/GUI/PreFabs/p.xml", "one")
        report = self._diff()
        self.assertEqual([e.verdict for e in report.entries], [drc.SAME])

    def test_live_exclusions_are_named_and_never_copied(self):
        _write(self.live / "TAOM/RuntimeDataCache/x.rdc", "r")
        _write(self.live / "TAOM/AssetSources/x.psd", "s")
        _write(self.live / "TAOM/Shaders/D3D11/compressed_shader_cache.sack", "k")
        _write(self.live / "TAOM/ModuleData/a.xml.bak-20261116", "b")
        _write(self.live / "TAOM/Assets/Race Test/.vs/slnx.sqlite", "v")
        _write(self.live / "TAOM/diag.log", "d")
        report = self._diff()
        rules = {e.rel: e.rule for e in report.entries if e.verdict == drc.EXCLUDED}
        self.assertEqual(rules, {
            "RuntimeDataCache/x.rdc": "RUNTIME_DATA_CACHE",
            "AssetSources/x.psd": "ASSET_SOURCES",
            "Shaders/D3D11/compressed_shader_cache.sack": "MODULE_SHADER_SACK",
            "ModuleData/a.xml.bak-20261116": "BACKUP_SIDECAR",
            "Assets/Race Test/.vs/slnx.sqlite": "VS_IDE_STATE",
            "diag.log": "RUNTIME_STATE",
        })
        self.assertFalse(report.groups)

    def test_live_backups_and_prefabs_unused_never_copy(self):
        _write(self.live / "TAOM_Map/Prefabs_Unused/p.xml", "u")
        _write(self.live / "TAOM_Map/SceneObj/Backups/s1/scene.xscene", "b")
        _write(self.live / "TAOM_Map/ModuleData/settlements.xml.backup", "b")
        report = self._diff(modules=("TAOM_Map",))
        self.assertEqual({e.rule for e in report.entries}, {"PREFABS_UNUSED", "SCENE_BACKUPS", "BACKUP_SIDECAR"})
        self.assertTrue(all(e.verdict == drc.EXCLUDED for e in report.entries))
        self.assertFalse(report.groups)

    # --- delete junk --------------------------------------------------------------
    def _junk_channel(self):
        _write(self.channel / "TAOM/ModuleData/a.xml.bak-1", "b")
        _write(self.channel / "TAOM/SceneObj/Backups/s1/scene.xscene", "b")
        _write(self.channel / "TAOM/SceneObj/s1/scene.xscene", "keep")
        _write(self.live / "TAOM/SceneObj/s1/scene.xscene", "keep")
        _write(self.channel / "TAOM/Prefabs_Unused/p.xml", "u")
        _write(self.channel / "TAOM/diag.log", "d")
        _write(self.channel / "TAOM/ModuleData/stale.xml", "s")

    def test_delete_junk_removes_backups_and_prefabs_unused_only(self):
        self._junk_channel()
        report = self._diff()
        log = drc.delete_junk(report, self.channel, set_aside_root=self.channel_root.parent / "_set-aside",
                              stamp="20261119-120000")
        self.assertFalse((self.channel / "TAOM/ModuleData/a.xml.bak-1").exists())
        self.assertFalse((self.channel / "TAOM/SceneObj/Backups").exists())
        self.assertFalse((self.channel / "TAOM/Prefabs_Unused").exists())
        self.assertTrue((self.channel / "TAOM/diag.log").exists())
        self.assertTrue((self.channel / "TAOM/ModuleData/stale.xml").exists())
        self.assertTrue((self.channel / "TAOM/SceneObj/s1/scene.xscene").exists())
        self.assertEqual(len(log.read_text(encoding="utf-8").splitlines()), 3)

    def test_cli_delete_junk_is_a_dry_run_without_apply(self):
        self._junk_channel()
        code, out, _ = self._main(str(self.channel), "--delete-junk")
        self.assertEqual(code, 1)
        self.assertIn("would delete 3", out)
        self.assertTrue((self.channel / "TAOM/Prefabs_Unused/p.xml").exists())
        code, out, _ = self._main(str(self.channel), "--delete-junk", "--apply")
        self.assertEqual(code, 0, out)
        self.assertFalse((self.channel / "TAOM/Prefabs_Unused").exists())

    def test_scene_sack_ships(self):
        _write(self.live / "TAOM_Map/SceneObj/s1/ShaderCache/D3D11/compressed_shader_cache.sack", "k")
        report = self._diff(modules=("TAOM_Map",))
        self.assertEqual([e.verdict for e in report.entries], [drc.NEW])

    def test_channel_junk_is_flagged_by_rule(self):
        _write(self.channel / "TAOM/ModuleData/a.xml.bak_syncids_20261116T1", "b")
        report = self._diff()
        (entry,) = report.entries
        self.assertEqual((entry.verdict, entry.rule), (drc.CHANNEL_JUNK, "BACKUP_SIDECAR"))
        self.assertIn("TAOM/set-aside/BACKUP_SIDECAR", report.groups)

    def test_channel_kit_package_is_reported_never_set_aside(self):
        # An editor-published channel packs TAOM's UI into AssetPackages/pack0.tpac; live has loose Assets.
        _write(self.channel / "TAOM/AssetPackages/pack0.tpac", "p")
        report = self._diff()
        self.assertEqual([e.verdict for e in report.entries], [drc.KIT_PACKAGE])
        self.assertFalse(report.groups)

    def test_groups_split_by_directory_and_kind(self):
        _write(self.live / "LOTRLOME_Armory/Assets/creature/elephant/a.tpac", "1")
        _write(self.live / "LOTRLOME_Armory/Assets/creature/direwolf/b.tpac", "2")
        _write(self.live / "LOTRLOME_Armory/SubModule.xml", "3")
        report = self._diff(modules=("LOTRLOME_Armory",))
        self.assertEqual(sorted(report.groups), [
            "LOTRLOME_Armory/(root)/new",
            "LOTRLOME_Armory/Assets/creature/direwolf/new",
            "LOTRLOME_Armory/Assets/creature/elephant/new",
        ])

    def test_unknown_live_file_is_reported_not_grouped(self):
        _write(self.live / "TAOM/Junkdir/x.txt", "u")
        report = self._diff()
        self.assertEqual([e.verdict for e in report.entries], [drc.UNKNOWN])
        self.assertFalse(report.groups)

    def test_module_absent_in_channel(self):
        _write(self.live / "TAOM.CoopCompat/SubModule.xml", "c")
        report = self._diff(modules=("TAOM", "TAOM.CoopCompat"))
        self.assertIn("TAOM.CoopCompat", report.absent_modules)
        self.assertIn("TAOM.CoopCompat/(root)/new", report.groups)

    def test_module_absent_in_live_is_bad_input(self):
        with self.assertRaises(drc.InputError):
            self._diff(modules=("Nope",))

    def test_stubs_compare_against_repo(self):
        _write(self.stubs / "Bannerlord.Harmony/_Module/SubModule.xml", "<stub/>")
        report = self._diff(stubs=("Bannerlord.Harmony",))
        self.assertIn("Bannerlord.Harmony/(stub)/new", report.groups)
        _write(self.channel / "Bannerlord.Harmony/SubModule.xml", "<stub/>")
        report = self._diff(stubs=("Bannerlord.Harmony",))
        self.assertFalse(report.groups)

    # --- apply ------------------------------------------------------------------
    def _apply(self, report, groups, **kw):
        kw.setdefault("set_aside_root", self.channel_root.parent / "_set-aside")
        kw.setdefault("stamp", "20261119-120000")
        return drc.apply(report, self.channel, groups, **kw)

    def test_apply_copies_only_chosen_groups_and_sets_aside_replaced_files(self):
        _write(self.live / "TAOM/ModuleData/b.xml", "new text", mtime=1_700_000_900.0)
        _write(self.channel / "TAOM/ModuleData/b.xml", "old")
        _write(self.live / "TAOM/GUI/g.xml", "gui")
        report = self._diff()
        log = self._apply(report, ["TAOM/ModuleData/changed"])
        self.assertEqual((self.channel / "TAOM/ModuleData/b.xml").read_text(), "new text")
        self.assertFalse((self.channel / "TAOM/GUI/g.xml").exists())
        aside = self.channel_root.parent / "_set-aside/public-20261119-120000/Modules/TAOM/ModuleData/b.xml"
        self.assertEqual(aside.read_text(), "old")
        self.assertTrue(log.exists())
        moves = json.loads(log.read_text())
        self.assertEqual([m["action"] for m in moves], ["set-aside", "copy"])

    def test_apply_keeps_channel_casing_for_a_changed_file(self):
        _write(self.live / "TAOM/GUI/Prefabs/p.xml", "newer!")
        _write(self.channel / "TAOM/GUI/PreFabs/p.xml", "old")
        report = self._diff()
        self._apply(report, ["TAOM/GUI/Prefabs/changed"])
        names = [p.name for p in (self.channel / "TAOM/GUI").iterdir()]
        self.assertEqual(names, ["PreFabs"])
        self.assertEqual((self.channel / "TAOM/GUI/PreFabs/p.xml").read_text(), "newer!")

    def test_apply_moves_channel_junk_never_deletes(self):
        _write(self.channel / "TAOM/ModuleData/a.xml.bak-1", "b")
        report = self._diff()
        self._apply(report, ["TAOM/set-aside/BACKUP_SIDECAR"])
        self.assertFalse((self.channel / "TAOM/ModuleData/a.xml.bak-1").exists())
        aside = self.channel_root.parent / "_set-aside/public-20261119-120000/Modules/TAOM/ModuleData/a.xml.bak-1"
        self.assertTrue(aside.exists())

    def test_apply_refuses_unknown_group(self):
        report = self._diff()
        with self.assertRaises(drc.InputError):
            self._apply(report, ["TAOM/nope/new"])

    def test_apply_refuses_bin_without_allow_bin(self):
        _write(self.live / "TAOM/bin/Win64_Shipping_Client/TAOM.dll", "dll")
        report = self._diff()
        with self.assertRaises(drc.InputError):
            self._apply(report, ["TAOM/bin/Win64_Shipping_Client/new"])
        self._apply(report, ["TAOM/bin/Win64_Shipping_Client/new"], allow_bin=True)
        self.assertTrue((self.channel / "TAOM/bin/Win64_Shipping_Client/TAOM.dll").exists())

    def test_apply_copies_stub_from_repo(self):
        _write(self.stubs / "Bannerlord.Harmony/_Module/SubModule.xml", "<stub/>")
        report = self._diff(stubs=("Bannerlord.Harmony",))
        self._apply(report, ["Bannerlord.Harmony/(stub)/new"])
        self.assertEqual((self.channel / "Bannerlord.Harmony/SubModule.xml").read_text(), "<stub/>")

    def test_rerun_after_apply_shows_no_difference(self):
        _write(self.live / "TAOM/ModuleData/b.xml", "new text", mtime=1_700_000_900.0)
        _write(self.channel / "TAOM/ModuleData/b.xml", "old")
        self._apply(self._diff(), ["TAOM/ModuleData/changed"])
        self.assertFalse(self._diff().groups)

    # --- cli --------------------------------------------------------------------
    def _main(self, *argv):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = drc.main([*argv, "--live", str(self.live), "--modules", "TAOM", "--no-stubs"])
        return code, out.getvalue(), err.getvalue()

    def test_cli_exit_codes(self):
        code, out, _ = self._main(str(self.channel))
        self.assertEqual(code, 0, out)
        _write(self.live / "TAOM/ModuleData/c.xml", "new")
        code, out, _ = self._main(str(self.channel))
        self.assertEqual(code, 1)
        self.assertIn("TAOM/ModuleData/new", out)
        code, _, err = self._main(str(self.channel / "missing"))
        self.assertEqual(code, 2, err)

    def test_cli_apply_without_groups_is_refused(self):
        _write(self.live / "TAOM/ModuleData/c.xml", "new")
        code, _, err = self._main(str(self.channel), "--apply")
        self.assertEqual(code, 2)
        self.assertIn("--groups", err)
        self.assertFalse((self.channel / "TAOM/ModuleData/c.xml").exists())

    def test_cli_json(self):
        _write(self.live / "TAOM/ModuleData/c.xml", "new")
        out_json = Path(self._tmp.name) / "r.json"
        self._main(str(self.channel), "--json", str(out_json))
        data = json.loads(out_json.read_text())
        self.assertEqual(data["groups"]["TAOM/ModuleData/new"]["files"], ["ModuleData/c.xml"])


if __name__ == "__main__":
    unittest.main()
