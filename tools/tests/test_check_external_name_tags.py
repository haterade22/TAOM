"""check_external_loc_coverage.py also reports whether the live TAOM_Map loc_settlements.xml carries the grammar tags (#1023).

Read only: every test builds its own fake Modules folder under a temp directory.
"""

import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_external_loc_coverage as chk  # noqa: E402

P = "Settlements.Settlement.name."
TABLE = {
    "languages": {
        "BR": {"plain": "{.NS}", "allowed": ["{.NS}", "{.FS}", "{.MS}"], "head_nouns": {}, "pins": {}, "articles": ["O", "A", "Os", "As"],
               "tags": {P + "town_A": "{.NS}", P + "village_A_1": "{.FS}", "taom_clan": "{.NS}", P + "castle_ES1": "{.MS}"}},
        "IT": {"plain": "{.FN}", "allowed": ["{.FN}"], "head_nouns": {}, "pins": {}, "tags": {P + "town_A": "{.FN}"}},
    }
}


def write_loc(modules: Path, lang: str, rows):
    f = modules / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    f.parent.mkdir(parents=True, exist_ok=True)
    body = "".join(f'<string id="{i}" text="{t}" />' for i, t in rows)
    f.write_text(f"<base><strings>{body}</strings></base>", encoding="utf-8")
    return f


def readable_tree(modules: Path):
    """The two sources the coverage count must read (#815: a missing one exits 2), each with no rows, so they
    add nothing to the count. A settlements.xml a test wrote first is kept."""
    s = modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
    if not s.exists():
        s.parent.mkdir(parents=True, exist_ok=True)
        s.write_text("<Settlements />", encoding="utf-8")
    a = modules / "LOTRLOME_Armory" / "ModuleData" / "Languages" / "loc_x.xml"
    a.parent.mkdir(parents=True, exist_ok=True)
    a.write_text("<base><strings></strings></base>", encoding="utf-8")


class TagReportTests(unittest.TestCase):
    def test_tag_report_UntaggedLiveFile_ListsEveryMissingTag(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "Alpha"), (P + "village_A_1", "Aldeia A")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual((r["expected"], r["correct"]), (2, 0))
        self.assertEqual(sorted(r["missing"]), [P + "town_A", P + "village_A_1"])
        self.assertEqual(r["wrong"], [])

    def test_tag_report_TaggedLiveFile_IsComplete(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "{.NS}Alpha"), (P + "village_A_1", "{.FS}Aldeia A")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual((r["expected"], r["correct"], r["missing"], r["wrong"]), (2, 2, [], []))

    def test_tag_report_WrongTag_IsNamedAsWrong(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "{.FS}Alpha"), (P + "village_A_1", "{.FS}Aldeia A")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual(r["wrong"], [P + "town_A"])

    def test_tag_report_OnlyCountsIdsTheLiveFileHolds(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "{.NS}Alpha")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual(r["expected"], 1)

    def test_tag_report_LanguageWithoutTags_IsNone(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertIsNone(chk.tag_report(Path(td), "DE", TABLE))

    def test_tag_report_MissingFile_IsNone(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertIsNone(chk.tag_report(Path(td), "BR", TABLE))

    def test_tag_report_DoesNotWrite(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            f = write_loc(m, "BR", [(P + "town_A", "Alpha")])
            before = f.read_bytes()
            chk.tag_report(m, "BR", TABLE)
            self.assertEqual(f.read_bytes(), before)

    def test_untranslated_Count_IgnoresTheLeadingTag(self):
        # A name that is still English must stay counted as untranslated once it is tagged.
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            s = m / "TAOM_Map" / "ModuleData" / "settlements.xml"
            s.parent.mkdir(parents=True, exist_ok=True)
            s.write_text(f'<S><Settlement name="{{={P}town_A}}Alpha" /></S>', encoding="utf-8")
            write_loc(m, "BR", [(P + "town_A", "{.NS}Alpha")])
            readable_tree(m)
            self.assertEqual(chk.untranslated(m, "BR")[0], 1)


class ArticleLedTests(unittest.TestCase):
    def test_tag_report_TaggedRowThatStillStartsWithAnArticle_IsWrong(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "castle_ES1", "{.MS}O Morannon")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual(r["wrong"], [P + "castle_ES1"])
        self.assertEqual(r["correct"], 0)

    def test_tag_report_TaggedRowWithoutAnArticle_IsCorrect(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "castle_ES1", "{.MS}Morannon")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual((r["correct"], r["wrong"]), (1, []))

    def test_tag_report_UntaggedRowThatStartsWithAnArticle_IsStillMissing(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "castle_ES1", "O Morannon")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual(r["missing"], [P + "castle_ES1"])


class OrphanTests(unittest.TestCase):
    def test_tag_report_RowWithATagTheTableDoesNotCallFor_IsAnOrphan(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "{.NS}Alpha"), (P + "stray", "{.FS}Stray"), (P + "plain", "Plain")])
            r = chk.tag_report(m, "BR", TABLE)
        self.assertEqual(r["orphan"], 1)
        self.assertEqual(r["expected"], 1)

    def test_tag_report_NoOrphans_CountsZero(self):
        with tempfile.TemporaryDirectory() as td:
            m = Path(td)
            write_loc(m, "BR", [(P + "town_A", "{.NS}Alpha")])
            self.assertEqual(chk.tag_report(m, "BR", TABLE)["orphan"], 0)

    def test_tags_failure_Orphans_AreAFailure(self):
        msgs = chk.tags_failure({"BR": {"expected": 1, "correct": 1, "missing": [], "wrong": [], "orphan": 3}})
        self.assertEqual(msgs, ["BR: 3 live row(s) start with a grammar tag the table does not call for"])

    def test_tags_errors_MissingReportOrNoTableName_AreErrors(self):
        reports = {"BR": None, "IT": {"expected": 0, "correct": 0, "missing": [], "wrong": [], "orphan": 0},
                   "DE": {"expected": 2, "correct": 2, "missing": [], "wrong": [], "orphan": 0}}
        msgs = chk.tags_errors(reports)
        self.assertEqual(len(msgs), 2)
        self.assertTrue(msgs[0].startswith("BR:") and msgs[1].startswith("IT:"))


class RequireTagsMainTests(unittest.TestCase):
    """main() on a temp Modules tree, with a baseline that every language meets."""

    def _run(self, td, args, rows_by_lang):
        modules = Path(td) / "Modules"
        for lang, rows in rows_by_lang.items():
            write_loc(modules, lang, rows)
        readable_tree(modules)
        baseline = Path(td) / "baseline.json"
        baseline.write_text(json.dumps({lang: {"taom_map": 0, "armory": 0} for lang in chk.LANGUAGES}), encoding="utf-8")
        old = os.environ.get("BANNERLORD_GAME_MODULES")
        os.environ["BANNERLORD_GAME_MODULES"] = str(modules)
        err = io.StringIO()
        try:
            # The tree holds no language_data.xml, so the #1026 processor check (tested in
            # test_check_external_loc_coverage.py) would exit 2; these tests are about the tag report.
            with mock.patch.object(chk, "BASELINE", baseline), \
                    mock.patch.object(chk, "processor_mismatches", return_value=[]), \
                    mock.patch.object(chk.name_tags, "load_table", return_value=TABLE), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(err):
                code = chk.main(args)
        finally:
            if old is None:
                del os.environ["BANNERLORD_GAME_MODULES"]
            else:
                os.environ["BANNERLORD_GAME_MODULES"] = old
        return code, err.getvalue()

    FULL = {"BR": [(P + "town_A", "{.NS}Alpha"), (P + "village_A_1", "{.FS}Aldeia A")], "IT": [(P + "town_A", "{.FN}Alfa")]}

    def test_main_RequireTags_CompleteFile_Passes(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(self._run(td, ["--require-tags"], self.FULL)[0], 0)

    def test_main_RequireTags_MissingTags_Exits1(self):
        rows = {"BR": [(P + "town_A", "Alpha"), (P + "village_A_1", "Aldeia A")], "IT": self.FULL["IT"]}
        with tempfile.TemporaryDirectory() as td:
            code, err = self._run(td, ["--require-tags"], rows)
        self.assertEqual(code, 1)
        self.assertIn("BR: 2 live name(s) lack their grammar tag", err)

    def test_main_RequireTags_MissingLiveFile_Exits2(self):
        with tempfile.TemporaryDirectory() as td:
            code, err = self._run(td, ["--require-tags"], {"BR": self.FULL["BR"]})
        self.assertEqual(code, 2)
        self.assertIn("IT: no live loc_settlements.xml", err)

    def test_main_RequireTags_FileWithNoTableName_Exits2(self):
        rows = {"BR": self.FULL["BR"], "IT": [("Settlements.Settlement.name.unrelated", "Nada")]}
        with tempfile.TemporaryDirectory() as td:
            code, err = self._run(td, ["--require-tags"], rows)
        self.assertEqual(code, 2)
        self.assertIn("none of the table's names", err)

    def test_main_WithoutTheFlag_MissingTagsAreOnlyReported(self):
        rows = {"BR": [(P + "town_A", "Alpha")], "IT": self.FULL["IT"]}
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(self._run(td, [], rows)[0], 0)

    def test_main_RegressionMessage_NamesTheLiveWriteFlag(self):
        with tempfile.TemporaryDirectory() as td:
            settlements = Path(td) / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"
            settlements.parent.mkdir(parents=True)
            settlements.write_text(f'<S><Settlement name="{{={P}town_A}}Alpha" /></S>', encoding="utf-8")
            code, err = self._run(td, [], {**self.FULL, "BR": [(P + "town_A", "Alpha")]})
        self.assertEqual(code, 1)
        self.assertIn("--allow-live-write", err)

    def test_main_WithoutTheFlag_MissingLiveFileIsNotAnError(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(self._run(td, [], {"BR": self.FULL["BR"]})[0], 0)


class ReportOnlyTests(unittest.TestCase):
    def test_format_tag_report_SaysReportOnlyWhileTagsAreMissing(self):
        line = chk.format_tag_report("BR", {"expected": 10, "correct": 0, "missing": ["a"] * 10, "wrong": [], "orphan": 0})
        self.assertIn("BR", line)
        self.assertIn("0/10", line)
        self.assertIn("report only", line)

    def test_tags_failure_IncompleteReport_IsNamed(self):
        msgs = chk.tags_failure({"BR": {"expected": 10, "correct": 4, "missing": [], "wrong": [], "orphan": 0}, "IT": None})
        self.assertEqual(msgs, ["BR: 6 live name(s) lack their grammar tag"])

    def test_tags_failure_CompleteReports_AreEmpty(self):
        self.assertEqual(chk.tags_failure({"BR": {"expected": 3, "correct": 3, "missing": [], "wrong": [], "orphan": 0}}), [])


if __name__ == "__main__":
    unittest.main()
