"""rebuild_translation_files.py writes only on --apply and only the modules it names.

1.4.x has no tools/name_tags.py, so the grammar-tag cases of this file on 1.5.x are not here. Nothing here touches the game install or the repo's language files: every main() runs against a temp tree, named to
the tool through its module constants or BANNERLORD_GAME_MODULES.
"""

import contextlib
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebuild_translation_files as rb  # noqa: E402


def doc(rows):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<base>", "  <strings>"]
    lines += [f'    <string id="{i}" text="{t}" />' for i, t in rows]
    lines += ["  </strings>", "</base>", ""]
    return "\r\n".join(lines)


class MapDiffTests(unittest.TestCase):

    def test_summarize_map_diff_IdenticalFiles_ReportNothing(self):
        d = doc([("a", "{.NS}Erebor")])
        s = rb.summarize_map_diff(d, d)
        self.assertEqual((s["other_changes"], s["unchanged"]), ([], 1))

    def test_summarize_map_diff_RowOnlyInOneFile_IsNamed(self):
        s = rb.summarize_map_diff(doc([("a", "A")]), doc([("a", "A"), ("b", "B")]))
        self.assertEqual(s["only_in_new"], ["b"])
        self.assertEqual(s["only_in_current"], [])


class WriteTextFileTests(unittest.TestCase):
    def test_write_text_file_DryRun_WritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "sub" / "out.xml"
            rb.write_text_file(p, "x", dry_run=True)
            self.assertFalse(p.exists())
            self.assertFalse(p.parent.exists())

    def test_write_text_file_Write_CreatesTheFile(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "sub" / "out.xml"
            rb.write_text_file(p, "x", dry_run=False)
            self.assertEqual(p.read_text(encoding="utf-8"), "x")

    def test_write_text_file_WithBackup_BacksUpWhenTheBytesChange(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "out.xml"
            p.write_text("old", encoding="utf-8")
            rb.write_text_file(p, "new", dry_run=False, backup=True)
            self.assertEqual(p.read_text(encoding="utf-8"), "new")
            backups = [f for f in Path(td).iterdir() if ".bak_nametags_" in f.name]
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_text(encoding="utf-8"), "old")

    def test_write_text_file_WithBackup_SameBytes_MakesNoBackup(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "out.xml"
            p.write_bytes(b"same\r\nline\r\n")
            rb.write_text_file(p, "same\r\nline\r\n", dry_run=False, backup=True)
            self.assertEqual([f.name for f in Path(td).iterdir()], ["out.xml"])

    def test_write_text_file_KeepsCrlfAsItIs(self):
        # Text mode on Windows wrote CR CR LF for content that already holds CRLF.
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "out.xml"
            rb.write_text_file(p, "a\r\nb\r\n", dry_run=False)
            self.assertEqual(p.read_bytes(), b"a\r\nb\r\n")

    def test_write_text_file_WithoutBackup_NeverBacksUp(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "out.xml"
            p.write_text("old", encoding="utf-8")
            rb.write_text_file(p, "new", dry_run=False)
            self.assertEqual([f.name for f in Path(td).iterdir()], ["out.xml"])


class RebuildMainTests(unittest.TestCase):
    """main() on a temp repo and a temp Modules tree (BANNERLORD_GAME_MODULES style), BR only.

    A run writes nothing unless it carries --apply, and it must name every module it may touch.
    """

    ALL = ["--module", "TAOM", "--module", "TAOM_Map", "--module", "Armory"]

    def _tree(self, td):
        repo = Path(td) / "repo"
        modules = Path(td) / "Modules"
        (repo / "Main" / "_Module" / "ModuleData").mkdir(parents=True)
        (repo / "Main" / "_Module" / "ModuleData" / "taom_module_strings.xml").write_text(
            '<strings><string id="k_erebor" text="{=k_erebor}Erebor" /></strings>', encoding="utf-8")
        (modules / "TAOM_Map" / "ModuleData").mkdir(parents=True)
        (modules / "TAOM_Map" / "ModuleData" / "settlements.xml").write_text(
            '<S><Settlement name="{=k_erebor}Erebor" /></S>', encoding="utf-8")
        (modules / "LOTRLOME_Armory" / "ModuleData" / "Languages").mkdir(parents=True)
        (modules / "LOTRLOME_Armory" / "ModuleData" / "Languages" / "loc_items.xml").write_text(
            '<base><strings><string id="k_sword" text="Sword" /></strings></base>', encoding="utf-8")
        return repo, modules

    def _run(self, repo, modules, *args):
        with mock.patch.object(rb, "REPO_ROOT", repo), mock.patch.object(rb, "GAME_ROOT", modules), \
                mock.patch("sys.argv", ["rebuild_translation_files.py", *args]), \
                contextlib.redirect_stdout(io.StringIO()):
            rb.main()

    def _apply(self, repo, modules, *args):
        self._run(repo, modules, "--lang", "BR", "--apply", *self.ALL, *args)

    def _paths(self, repo, modules):
        taom = repo / "Main" / "_Module" / "ModuleData" / "Languages" / "BR" / "std_taom_module_strings_por-BR.xml"
        live = modules / "TAOM_Map" / "ModuleData" / "Languages" / "BR" / "loc_settlements.xml"
        return taom, live

    def _armory(self, modules):
        return modules / "LOTRLOME_Armory" / "ModuleData" / "Languages" / "BR" / "loc_items.xml"

    def test_main_MapFile_BacksUpOnlyWhenItsBytesChange(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules)
            _, live = self._paths(repo, modules)
            live.write_bytes(live.read_bytes() + b"<!-- hand edit -->")
            self._apply(repo, modules)
            self.assertEqual(len([f for f in live.parent.iterdir() if ".bak_nametags_" in f.name]), 1)
            self._apply(repo, modules)
            self.assertEqual(len([f for f in live.parent.iterdir() if ".bak_nametags_" in f.name]), 1)

    def test_main_ArmoryFile_BacksUpOnlyWhenItsBytesChange(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            armory = self._armory(modules)
            armory.parent.mkdir(parents=True)
            armory.write_text("old", encoding="utf-8")
            self._apply(repo, modules)
            self.assertIn('id="k_sword"', armory.read_text(encoding="utf-8"))
            backups = [f for f in armory.parent.iterdir() if ".bak_nametags_" in f.name]
            self.assertEqual([b.read_text(encoding="utf-8") for b in backups], ["old"])
            self._apply(repo, modules)
            self.assertEqual(len([f for f in armory.parent.iterdir() if ".bak_nametags_" in f.name]), 1)

    def test_main_RepoFile_GetsNoBackup(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules)
            self._apply(repo, modules)
            taom, _ = self._paths(repo, modules)
            self.assertEqual([f for f in taom.parent.iterdir() if ".bak_" in f.name], [])

    def test_main_NoApply_WritesNothingEvenForANamedLanguage(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._run(repo, modules, "--lang", "BR", *self.ALL)
            taom, live = self._paths(repo, modules)
            self.assertFalse(taom.exists())
            self.assertFalse(live.exists())
            self.assertFalse(self._armory(modules).exists())
            self.assertFalse(self._armory(modules).parent.exists())

    def test_main_DryRunFlag_IsStillAcceptedAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._run(repo, modules, "--lang", "BR", "--dry-run", *self.ALL)
            taom, live = self._paths(repo, modules)
            self.assertFalse(taom.exists())
            self.assertFalse(live.exists())
            self.assertFalse(self._armory(modules).exists())

    def test_main_ApplyWithDryRun_Exits2AndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as caught:
                self._run(repo, modules, "--lang", "BR", "--apply", "--dry-run", *self.ALL)
            self.assertEqual(caught.exception.code, 2)
            self.assertFalse(self._paths(repo, modules)[0].exists())

    def test_main_NoModule_Exits2AndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as caught:
                self._run(repo, modules, "--lang", "BR", "--apply")
            self.assertEqual(caught.exception.code, 2)
            taom, live = self._paths(repo, modules)
            self.assertFalse(taom.exists())
            self.assertFalse(live.exists())
            self.assertFalse(self._armory(modules).exists())

    def test_main_ApplyArmoryOnly_LeavesTheTaomAndMapFilesUntouched(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            taom, live = self._paths(repo, modules)
            taom.parent.mkdir(parents=True)
            taom.write_bytes(b"taom bytes")
            live.parent.mkdir(parents=True)
            live.write_bytes(b"map bytes")
            self._run(repo, modules, "--lang", "BR", "--apply", "--module", "Armory")
            self.assertEqual(taom.read_bytes(), b"taom bytes")
            self.assertEqual(live.read_bytes(), b"map bytes")
            self.assertEqual([f.name for f in taom.parent.iterdir()], [taom.name])
            self.assertIn('id="k_sword"', self._armory(modules).read_text(encoding="utf-8"))

    def test_main_ApplyTaomOnly_LeavesTheArmoryAndMapAlone(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._run(repo, modules, "--lang", "BR", "--apply", "--module", "TAOM")
            taom, live = self._paths(repo, modules)
            self.assertTrue(taom.exists())
            self.assertFalse(live.exists())
            self.assertFalse(self._armory(modules).parent.exists())

    def test_main_ArmoryNotNamed_NeverNeedsTheArmoryFolder(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            (modules / "LOTRLOME_Armory" / "ModuleData" / "Languages" / "loc_items.xml").unlink()
            (modules / "LOTRLOME_Armory" / "ModuleData" / "Languages").rmdir()
            self._run(repo, modules, "--lang", "BR", "--apply", "--module", "TAOM")
            self.assertTrue(self._paths(repo, modules)[0].exists())


    def test_build_parser_ParsesTheFlags(self):
        args = rb.build_parser().parse_args(["--lang", "BR", "--dry-run", "--module", "TAOM"])
        self.assertTrue(args.dry_run)
        self.assertFalse(args.apply)
        args = rb.build_parser().parse_args(["--lang", "BR", "--apply", "--module", "TAOM", "--module", "Armory"])
        self.assertTrue(args.apply)
        self.assertFalse(args.dry_run)
        self.assertEqual(args.module, ["TAOM", "Armory"])


if __name__ == "__main__":
    unittest.main()
