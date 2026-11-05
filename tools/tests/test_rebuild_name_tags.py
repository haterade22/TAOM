"""The grammar-tag step inside the two repo-side writers: rebuild_translation_files.py and the translator (#1023).

Nothing here touches the game install or the repo's language files: every main() runs against a temp tree, named to
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

import name_tags as nt  # noqa: E402
import rebuild_translation_files as rb  # noqa: E402
import translate_with_claude as twc  # noqa: E402

TABLE = {
    "languages": {
        "BR": {"plain": "{.NS}", "allowed": ["{.NS}", "{.FS}"], "head_nouns": {}, "pins": {},
               "tags": {"k_erebor": "{.NS}", "k_aldeia": "{.FS}"}},
        "IT": {"plain": "{.FN}", "allowed": ["{.FN}"], "head_nouns": {}, "pins": {}, "tags": {"k_erebor": "{.FN}"}},
    }
}


def doc(rows):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<base>", "  <strings>"]
    lines += [f'    <string id="{i}" text="{t}" />' for i, t in rows]
    lines += ["  </strings>", "</base>", ""]
    return "\r\n".join(lines)


class RebuildTaggingTests(unittest.TestCase):
    def test_tag_entries_AddsTheTagToTableIds(self):
        out = nt.tag_entries("BR", [("k_erebor", "Erebor"), ("x", "X")], TABLE)
        self.assertEqual(out, [("k_erebor", "{.NS}Erebor"), ("x", "X")])

    def test_tag_entries_OtherLanguage_IsUntouched(self):
        entries = [("k_erebor", "Erebor")]
        self.assertEqual(nt.tag_entries("DE", entries, TABLE), entries)

    def test_tag_entries_FromTheCacheValue_LeavesTheCacheValueUntouched(self):
        cache = {"k_erebor": "Erebor"}
        nt.tag_entries("BR", list(cache.items()), TABLE)
        self.assertEqual(cache, {"k_erebor": "Erebor"})

    def test_build_translation_xml_OfTaggedEntries_ParsesAndKeepsTheTag(self):
        xml = rb.build_translation_xml("Portuguese (Brazilian)", nt.tag_entries("BR", [("k_erebor", "Erebor & Co")], TABLE))
        root = ET.fromstring(xml.encode("utf-8"))
        self.assertEqual([e.get("text") for e in root.iter("string")], ["{.NS}Erebor & Co"])


class MapDiffTests(unittest.TestCase):
    def test_summarize_map_diff_CountsTagOnlyChangesSeparately(self):
        current = doc([("a", "Erebor"), ("b", "Aldeia X"), ("c", "Same")])
        new = doc([("a", "{.NS}Erebor"), ("b", "{.FS}Aldeia X"), ("c", "Changed")])
        s = rb.summarize_map_diff(current, new)
        self.assertEqual(s["rows"], 3)
        self.assertEqual(s["tag_only"], 2)
        self.assertEqual(s["other_changes"], [("c", "Same", "Changed")])
        self.assertEqual(s["unchanged"], 0)

    def test_summarize_map_diff_IdenticalFiles_ReportNothing(self):
        d = doc([("a", "{.NS}Erebor")])
        s = rb.summarize_map_diff(d, d)
        self.assertEqual((s["tag_only"], s["other_changes"], s["unchanged"]), (0, [], 1))

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
                mock.patch.object(nt, "load_table", return_value=TABLE), \
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

    def test_main_RepoFile_IsTaggedWithoutTheFlag(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules)
            taom, _ = self._paths(repo, modules)
            self.assertIn('text="{.NS}Erebor"', taom.read_text(encoding="utf-8"))

    def test_main_MapFile_IsNotTaggedWithoutTheFlag(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules)
            _, live = self._paths(repo, modules)
            text = live.read_text(encoding="utf-8")
            self.assertIn('text="Erebor"', text)
            self.assertNotIn("{.NS}", text)

    def test_main_MapFile_IsTaggedWithTheFlag(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules, "--allow-live-write")
            _, live = self._paths(repo, modules)
            self.assertIn('text="{.NS}Erebor"', live.read_text(encoding="utf-8"))

    def test_main_MapFile_BacksUpOnlyWhenItsBytesChange(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._apply(repo, modules)
            _, live = self._paths(repo, modules)
            self._apply(repo, modules, "--allow-live-write")
            self.assertEqual(len([f for f in live.parent.iterdir() if ".bak_nametags_" in f.name]), 1)
            self._apply(repo, modules, "--allow-live-write")
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
            self._run(repo, modules, "--lang", "BR", *self.ALL, "--allow-live-write")
            taom, live = self._paths(repo, modules)
            self.assertFalse(taom.exists())
            self.assertFalse(live.exists())
            self.assertFalse(self._armory(modules).exists())
            self.assertFalse(self._armory(modules).parent.exists())

    def test_main_DryRunFlag_IsStillAcceptedAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            self._run(repo, modules, "--lang", "BR", "--dry-run", *self.ALL, "--allow-live-write")
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

    def test_main_LanguageWithoutTags_NeverLoadsTheTableAndCompletes(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            with mock.patch.object(rb, "REPO_ROOT", repo), mock.patch.object(rb, "GAME_ROOT", modules), \
                    mock.patch.object(nt, "load_table", side_effect=RuntimeError("no table")) as load, \
                    mock.patch("sys.argv", ["rebuild_translation_files.py", "--lang", "DE", "--apply", *self.ALL]), \
                    contextlib.redirect_stdout(io.StringIO()):
                rb.main()
            load.assert_not_called()
            taom = repo / "Main" / "_Module" / "ModuleData" / "Languages" / "DE" / "std_taom_module_strings_deu-DE.xml"
            self.assertIn('text="Erebor"', taom.read_text(encoding="utf-8"))

    def test_main_TaggedLanguage_LoadsTheTableOnce(self):
        with tempfile.TemporaryDirectory() as td:
            repo, modules = self._tree(td)
            with mock.patch.object(rb, "REPO_ROOT", repo), mock.patch.object(rb, "GAME_ROOT", modules), \
                    mock.patch.object(nt, "load_table", return_value=TABLE) as load, \
                    mock.patch("sys.argv", ["rebuild_translation_files.py", "--lang", "BR", "--apply", *self.ALL, "--allow-live-write"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                rb.main()
            self.assertEqual(load.call_count, 1)

    def test_build_parser_ParsesTheFlags(self):
        args = rb.build_parser().parse_args(["--lang", "BR", "--dry-run", "--module", "TAOM", "--allow-live-write"])
        self.assertTrue(args.dry_run and args.allow_live_write)
        self.assertFalse(args.apply)
        args = rb.build_parser().parse_args(["--lang", "BR", "--apply", "--module", "TAOM", "--module", "Armory"])
        self.assertTrue(args.apply)
        self.assertFalse(args.dry_run or args.allow_live_write)
        self.assertEqual(args.module, ["TAOM", "Armory"])


class WriteBackTests(unittest.TestCase):
    def test_write_back_WritesTheTextItIsGiven(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "std_taom_x_por-BR.xml"
            p.write_text(doc([("k_erebor", "Erebor")]), encoding="utf-8", newline="")
            n, unplaced = twc.write_back(p, {"k_erebor": "{.NS}Erebor"}, "Portuguese (Brazilian)")
            self.assertEqual((n, unplaced), (1, []))
            self.assertIn('text="{.NS}Erebor"', p.read_text(encoding="utf-8"))

    def test_absorb_translations_KeepsTheCacheFreeOfTags(self):
        cache, queued = {}, {}
        result = twc.TranslationResult()
        entry = twc.Entry(Path("x"), "k_erebor", "Erebor", "Erebor")
        twc.absorb_translations([entry], {"k_erebor": "Erebor"}, result, cache,
                                lambda e, tr: queued.__setitem__(e.string_id, tr))
        self.assertEqual(cache, {"k_erebor": "Erebor"})


class UntranslatedDetectionTests(unittest.TestCase):
    """A tagged row that is still the English text is untranslated: the tag must not hide it from the translator."""

    def _pair(self, td, english_text, target_text):
        src = Path(td) / "src.xml"
        src.write_text('<strings><string id="k" text="{=k}%s" /></strings>' % english_text, encoding="utf-8")
        tgt = Path(td) / "std_taom_x_por-BR.xml"
        tgt.write_text(doc([("k", target_text)]), encoding="utf-8", newline="")
        return src, tgt

    def test_diff_files_TaggedRowEqualToEnglish_IsStillUntranslated(self):
        with tempfile.TemporaryDirectory() as td:
            src, tgt = self._pair(td, "Old Forest Eaves", "{.NS}Old Forest Eaves")
            self.assertEqual([e.string_id for e in twc._diff_files(src, tgt)], ["k"])

    def test_diff_files_TaggedTranslatedRow_IsTranslated(self):
        with tempfile.TemporaryDirectory() as td:
            src, tgt = self._pair(td, "Old Forest Eaves", "{.NS}Beiras da Floresta Velha")
            self.assertEqual(twc._diff_files(src, tgt), [])

    def test_diff_against_settlement_source_TaggedRowEqualToEnglish_IsStillUntranslated(self):
        with tempfile.TemporaryDirectory() as td:
            settlements = Path(td) / "settlements.xml"
            settlements.write_text('<S><Settlement name="{=k}Old Forest Eaves" /></S>', encoding="utf-8")
            tgt = Path(td) / "loc_settlements.xml"
            tgt.write_text(doc([("k", "{.NS}Old Forest Eaves")]), encoding="utf-8", newline="")
            self.assertEqual([e.string_id for e in twc._diff_against_settlement_source(settlements, tgt)], ["k"])


class TranslatorMainTests(unittest.TestCase):
    """main() with a fake provider: cache and override fills are tagged, fresh model output is not."""

    def _run(self, td, lang_file, entries, cache, overrides, *extra, table=TABLE, repo_root=None, fake=None):
        calls = []

        def fake_model(provider, client, lang_name, batch):
            calls.append([e.string_id for e in batch])
            return ({e.string_id: (fake or {}).get(e.string_id, "Fresco " + e.english_text) for e in batch}, (0, 0))

        out = io.StringIO()
        with mock.patch.object(twc, "discover_entries", return_value=entries), \
                mock.patch.object(twc, "load_cache", return_value=dict(cache)), \
                mock.patch.object(twc, "load_overrides", return_value=dict(overrides)), \
                mock.patch.object(twc, "save_cache"), \
                mock.patch.object(twc, "require_key"), \
                mock.patch.object(twc, "call_model", side_effect=fake_model), \
                mock.patch.object(twc.name_tags, "load_table", return_value=table) as load, \
                mock.patch.object(twc, "REPO_ROOT", repo_root or Path(td) / "repo"), \
                contextlib.redirect_stdout(out):
            code = twc.main(["--lang", "BR", "--module", "all", "--provider", "deepseek", "--apply", *extra])
        return code, out.getvalue(), calls, load

    def _entries(self, f):
        return [twc.Entry(f, "k_erebor", "Erebor", "Erebor"), twc.Entry(f, "k_aldeia", "Village", "Village")]

    def _file(self, td, name="loc_settlements.xml"):
        f = Path(td) / name
        f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
        return f

    def test_main_CacheFill_IsTaggedInARepoFile(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "Main" / "_Module" / "ModuleData" / "Languages" / "BR" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            code, out, calls, _ = self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {}, repo_root=repo)
            self.assertEqual(code, 0)
            text = f.read_text(encoding="utf-8")
            self.assertIn('text="{.NS}Erebor"', text)
            self.assertIn('text="{.FS}Aldeia"', text)
            self.assertEqual(calls, [])

    def test_main_OverrideFill_IsTagged(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "x" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            self._run(td, f, self._entries(f), {}, {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, repo_root=repo)
            self.assertIn('text="{.FS}Aldeia"', f.read_text(encoding="utf-8"))

    def test_main_FreshModelOutput_IsNotTagged_AndIsPrintedWithTheRerunInstructions(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "x" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            code, out, calls, _ = self._run(td, f, self._entries(f), {}, {}, repo_root=repo, fake={"k_aldeia": "Aldeia Nova"})
            text = f.read_text(encoding="utf-8")
            self.assertEqual(calls, [["k_erebor", "k_aldeia"]])
            self.assertIn('text="Aldeia Nova"', text)
            self.assertNotIn("{.", text)
            self.assertIn("k_aldeia", out)
            self.assertIn("generate_name_tags.py --write", out)
            self.assertIn("name_tags.py --lang BR --apply", out)

    def test_main_MixedFile_TagsTheCacheFillAndNotTheFreshRow_InOneWritePass(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "x" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            with mock.patch.object(twc, "write_back", wraps=twc.write_back) as spy:
                self._run(td, f, self._entries(f), {"k_erebor": "Erebor"}, {}, repo_root=repo, fake={"k_aldeia": "Aldeia Nova"})
            text = f.read_text(encoding="utf-8")
            self.assertIn('text="{.NS}Erebor"', text)
            self.assertIn('text="Aldeia Nova"', text)
            self.assertEqual(spy.call_count, 1)

    def test_main_FileOutsideTheRepo_PrintsTheLiveReapplyCommand(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            _, out, _, _ = self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {})
            self.assertIn("name_tags.py --target live-map --apply --allow-live-write", out)

    def test_main_RepoFileOnly_PrintsNoLiveReapplyCommand(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "x" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            _, out, _, _ = self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {}, repo_root=repo)
            self.assertNotIn("--target live-map", out)

    def test_main_NoFreshRows_PrintsNoRerunInstructions(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td) / "repo"
            f = repo / "x" / "std_taom_x_por-BR.xml"
            f.parent.mkdir(parents=True)
            f.write_text(doc([("k_erebor", "Erebor"), ("k_aldeia", "Village")]), encoding="utf-8", newline="")
            _, out, _, _ = self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {}, repo_root=repo)
            self.assertNotIn("generate_name_tags.py", out)

    def test_main_FileOutsideTheRepo_IsNotTaggedWithoutTheFlag(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {})
            self.assertNotIn("{.", f.read_text(encoding="utf-8"))

    def test_main_FileOutsideTheRepo_IsTaggedWithTheFlag(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            self._run(td, f, self._entries(f), {"k_erebor": "Erebor", "k_aldeia": "Aldeia"}, {}, "--allow-live-write")
            self.assertIn('text="{.NS}Erebor"', f.read_text(encoding="utf-8"))

    def test_main_LoadsTheTableOnce(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            _, _, _, load = self._run(td, f, self._entries(f), {"k_erebor": "Erebor"}, {"k_aldeia": "Aldeia"})
            self.assertEqual(load.call_count, 1)

    def test_main_MissingTable_FailsBeforeAnyProviderCall(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            calls = []
            with mock.patch.object(twc, "discover_entries", return_value=self._entries(f)), \
                    mock.patch.object(twc, "load_cache", return_value={}), \
                    mock.patch.object(twc, "load_overrides", return_value={}), \
                    mock.patch.object(twc, "require_key"), \
                    mock.patch.object(twc, "call_model", side_effect=lambda *a: calls.append(a)), \
                    mock.patch.object(twc.name_tags, "load_table", side_effect=FileNotFoundError("no table")), \
                    contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(FileNotFoundError):
                    twc.main(["--lang", "BR", "--module", "all", "--provider", "deepseek", "--apply"])
            self.assertEqual(calls, [])

    def test_main_LanguageWithoutTags_NeverLoadsTheTable(self):
        with tempfile.TemporaryDirectory() as td:
            f = self._file(td)
            out = io.StringIO()
            with mock.patch.object(twc, "discover_entries", return_value=self._entries(f)), \
                    mock.patch.object(twc, "load_cache", return_value={"k_erebor": "Erebor", "k_aldeia": "Aldeia"}), \
                    mock.patch.object(twc, "load_overrides", return_value={}), \
                    mock.patch.object(twc, "save_cache"), \
                    mock.patch.object(twc.name_tags, "load_table") as load, \
                    contextlib.redirect_stdout(out):
                twc.main(["--lang", "DE", "--module", "all", "--provider", "deepseek", "--apply"])
            load.assert_not_called()


if __name__ == "__main__":
    unittest.main()
