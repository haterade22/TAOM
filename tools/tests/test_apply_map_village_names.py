#!/usr/bin/env python3
"""tools/Apply-MapVillageNames.py on a temp BANNERLORD_GAME_DIR tree (#1042).

Run:  python -m unittest tools.tests.test_apply_map_village_names

Only renames are written, the dry run writes nothing, a rename leaves the translation cache, a taken
name or an unreadable file refuses before any write, and a run that fails part-way is finished by
running it again. BR and IT are not asserted: they may carry grammar tags (tools/name_tags.py).
"""
import contextlib
import io
import os
import runpy
import stat
import tempfile
import unittest
from pathlib import Path

P = "Settlements.Settlement.name."
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
UNTAGGED = [lang for lang in LANGS if lang not in ("BR", "IT")]
APPLY = Path(__file__).resolve().parent.parent / "Apply-MapVillageNames.py"


def loc_xml(rows):
    body = "".join(f'    <string id="{P}{i}" text="{t}" />\r\n' for i, t in rows)
    return f'<?xml version="1.0" encoding="utf-8"?>\r\n<base>\r\n  <strings>\r\n{body}  </strings>\r\n</base>\r\n'


@contextlib.contextmanager
def game_dir(td):
    old = os.environ.get("BANNERLORD_GAME_DIR")
    os.environ["BANNERLORD_GAME_DIR"] = td
    try:
        yield
    finally:
        if old is None:
            del os.environ["BANNERLORD_GAME_DIR"]
        else:
            os.environ["BANNERLORD_GAME_DIR"] = old


class ApplyMapVillageNamesTests(unittest.TestCase):
    MASTER = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Old" />'
              f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Old" />'
              f'<Settlement id="village_RU8_2" name="{{={P}village_RU8_2}}Iôrig-Vargh" /></S>')
    ROWS = [("village_RU8_4", "Old"), ("village_RU8_2", "Ыыы")]

    def _tree(self, td, master=None):
        data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
        cache = Path(td) / "cache"
        data.mkdir(parents=True)
        cache.mkdir()
        if master is not False:
            (data / "settlements.xml").write_bytes((self.MASTER if master is None else master).encode("utf-8"))
        for lang in LANGS:
            f = data / "Languages" / lang / "loc_settlements.xml"
            f.parent.mkdir(parents=True)
            f.write_bytes(loc_xml(self.ROWS).encode("utf-8"))
            (cache / f"{lang.lower()}.json").write_bytes(
                ('{\n  "' + P + 'village_RU8_2": "Ыыы",\n  "' + P + 'village_RU8_4": "Altes"\n}').encode("utf-8"))
        return data, cache

    def _main(self, td, argv=("--apply",)):
        with game_dir(td), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            ns = runpy.run_path(str(APPLY), run_name="apply_map_village_names")
            return ns["main"](list(argv), cache_dir=str(Path(td) / "cache"))

    def _read(self, data, cache):
        self.master_after = (data / "settlements.xml").read_text(encoding="utf-8")
        self.cache_after = {lang: (cache / f"{lang.lower()}.json").read_text(encoding="utf-8") for lang in LANGS}
        return {lang: (data / "Languages" / lang / "loc_settlements.xml").read_text(encoding="utf-8") for lang in LANGS}

    def _run(self, td, argv=("--apply",), master=None):
        data, cache = self._tree(td, master)
        self.assertEqual(self._main(td, argv), 0)
        return self._read(data, cache)

    def test_script_LoadingTheModuleRunsNothing(self):
        # Before #1042 the script renamed at import: loading it wrote 13 live files.
        with tempfile.TemporaryDirectory() as td:
            self._tree(td)
            before = {p: p.read_bytes() for p in Path(td).rglob("*") if p.is_file()}
            with game_dir(td), contextlib.redirect_stdout(io.StringIO()):
                runpy.run_path(str(APPLY), run_name="apply_map_village_names")
            after = {p: p.read_bytes() for p in Path(td).rglob("*") if p.is_file()}
        self.assertEqual(before, after)

    def test_script_RenamedId_UpdatesTheMasterAndTheRowsAndLeavesTheCache(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td)
        self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
        self.assertIn('village_RU8_3}Iôrig-Tün"', self.master_after)
        for lang in UNTAGGED:
            self.assertIn('text="Iôrig-Mukh"', out[lang], lang)
        for lang in LANGS:
            self.assertNotIn("village_RU8_4", self.cache_after[lang], lang)

    def test_script_UnchangedName_KeepsItsTranslationAndCacheRow(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td)
        self.assertIn('text="Ыыы"', out["RU"])
        self.assertIn("village_RU8_2", self.cache_after["RU"])

    def test_script_DryRun_WritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, argv=())
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["DE"])
        self.assertIn("village_RU8_4", self.cache_after["DE"])

    def test_script_SecondRun_ChangesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td)
            self.assertEqual(self._main(td), 0)
            first, master1 = self._read(data, cache), self.master_after
            self.assertEqual(self._main(td), 0)
            second, master2 = self._read(data, cache), self.master_after
        self.assertEqual(first, second)
        self.assertEqual(master1, master2)

    def test_script_MasterWithABom_KeepsExactlyOneBom(self):
        with tempfile.TemporaryDirectory() as td:
            self._run(td, master="\ufeff" + self.MASTER)
            raw = (Path(td) / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml").read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf<S>"))

    def test_script_Apply_BacksUpEachChangedFileOutsideTheXmlGlob(self):
        with tempfile.TemporaryDirectory() as td:
            self._run(td)
            data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
            master_baks = list(data.glob("settlements.xml.bak_villagenames_*"))
            de_baks = list((data / "Languages" / "DE").glob("loc_settlements.xml.bak_villagenames_*"))
            self.assertEqual(len(master_baks), 1)
            self.assertIn('village_RU8_4}Old"', master_baks[0].read_text(encoding="utf-8"))
            self.assertEqual(len(de_baks), 1)

    def test_script_LocFileThatDoesNotParse_RefusesAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td)
            (data / "Languages" / "DE" / "loc_settlements.xml").write_bytes(b'<base><strings><string id="x" text="Old" />')
            self.assertEqual(self._main(td), 1)
            out = self._read(data, cache)
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["FR"])
        self.assertIn("village_RU8_4", self.cache_after["FR"])

    def test_script_AWriteThatFails_IsFinishedByARerun(self):
        # The master decides what a run renames, so it is written last.
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td)
            de = data / "Languages" / "DE" / "loc_settlements.xml"
            os.chmod(de, stat.S_IREAD)
            try:
                with self.assertRaises(PermissionError):
                    self._main(td)
            finally:
                os.chmod(de, stat.S_IREAD | stat.S_IWRITE)
            self.assertIn('village_RU8_4}Old"', (data / "settlements.xml").read_text(encoding="utf-8"))
            self.assertEqual(self._main(td), 0)
            out = self._read(data, cache)
        self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
        for lang in LANGS:
            self.assertIn("Iôrig-Mukh", out[lang], lang)
            self.assertNotIn("village_RU8_4", self.cache_after[lang], lang)

    def test_script_NewNameAnotherSettlementCarries_RefusesAndWritesNothing(self):
        master = self.MASTER.replace("</S>", f'<Settlement id="castle_Z1" name="{{={P}castle_Z1}}Iôrig-Mukh" /></S>')
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td, master)
            self.assertEqual(self._main(td), 1)
            out = self._read(data, cache)
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["DE"])
        self.assertIn("village_RU8_4", self.cache_after["DE"])

    def test_script_TwoTableIdsSwappingNames_IsNotAClash(self):
        swapped = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Iôrig-Tün" />'
                   f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Iôrig-Mukh" /></S>')
        with tempfile.TemporaryDirectory() as td:
            self._run(td, master=swapped)
        self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
        self.assertIn('village_RU8_3}Iôrig-Tün"', self.master_after)

    def test_script_MasterWithNoTableId_Exits2(self):
        with tempfile.TemporaryDirectory() as td:
            self._tree(td, master=f'<S><Settlement id="town_Q1" name="{{={P}town_Q1}}Q" /></S>')
            self.assertEqual(self._main(td), 2)

    def test_script_MissingMaster_Exits2(self):
        with tempfile.TemporaryDirectory() as td:
            self._tree(td, master=False)
            self.assertEqual(self._main(td), 2)


if __name__ == "__main__":
    unittest.main()
