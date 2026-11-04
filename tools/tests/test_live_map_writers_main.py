"""main() of add_map_villages.py and rename_map_settlements.py on a temp TAOM_Map tree (#1023).

add_map_villages writes the BR and IT name with the tag the table has for its id; rename_map_settlements moves the
table's entries to the new id. Nothing here touches the game install or the repo's table.
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
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import add_map_villages as amv  # noqa: E402
import name_tags as nt  # noqa: E402
import rename_map_settlements as rms  # noqa: E402
import test_rename_map_settlements as base  # noqa: E402

P = "Settlements.Settlement.name."
LANGS = amv.LANGS


def table(**tags):
    return {"languages": {
        "BR": {"plain": "{.NS}", "allowed": ["{.NS}", "{.FS}"], "head_nouns": {}, "pins": {}, "tags": dict(tags.get("BR", {}))},
        "IT": {"plain": "{.FN}", "allowed": ["{.FN}"], "head_nouns": {}, "pins": {}, "tags": dict(tags.get("IT", {}))},
    }}


class Tree:
    """A temp Modules/TAOM_Map with a master, a scene and twelve loc files, plus a repo folder and a table file."""

    def __init__(self, td, master, scene, loc, tbl):
        root = Path(td)
        self.map = str(root / "Modules" / "TAOM_Map")
        self.live = self.map + "/ModuleData/settlements.xml"
        self.scene = self.map + "/SceneObj/Main_map/scene.xscene"
        self.repo = root / "repo"
        self.table_path = root / "table.json"
        self.folders = {"gondor_village_a"}  # the scene folders the real install would list; never read here
        for f, text in ((self.live, master), (self.scene, scene)):
            Path(f).parent.mkdir(parents=True, exist_ok=True)
            Path(f).write_bytes(text.encode("utf-8"))
        for lang in LANGS:
            f = Path(self.map + f"/ModuleData/Languages/{lang}/loc_settlements.xml")
            f.parent.mkdir(parents=True, exist_ok=True)
            f.write_bytes(loc.encode("utf-8"))
        (self.repo / "ids").mkdir(parents=True, exist_ok=True)
        (self.repo / "ids" / "gondor.json").write_text(base.JSON, encoding="utf-8")
        self.table_path.write_text(nt.dump_table(tbl), encoding="utf-8")

    def loc(self, lang):
        return Path(self.map + f"/ModuleData/Languages/{lang}/loc_settlements.xml").read_text(encoding="utf-8")

    def table(self):
        return json.loads(self.table_path.read_text(encoding="utf-8"))

    def run(self, module, name, args, extra=()):
        patches = [mock.patch.object(amv, "MAP", self.map), mock.patch.object(amv, "LIVE", self.live),
                   mock.patch.object(amv, "SCENE", self.scene), mock.patch.object(nt, "TABLE_PATH", self.table_path),
                   mock.patch.object(amv, "live_scene_folders", return_value=self.folders),
                   mock.patch("sys.argv", [name, *args]), mock.patch("sys.stdout", io.StringIO()), *extra]
        with contextlib.ExitStack() as stack:
            for p in patches:
                stack.enter_context(p)
            return module.main()


class AddMapVillagesMainTests(unittest.TestCase):
    MASTER = '<Settlements>\n  <Settlement id="town_A" posX="1.0" posY="2.0" culture="Culture.gondor" />\n</Settlements>\n'
    LOC = '<base><strings>\n    <string id="Settlements.Settlement.name.town_A" text="Alpha" />\n  </strings></base>\n'
    VILLAGE = amv.Village("village_X_1", "Aldeia X", "town_A", "cattle_farm", "gondor", "gondor_village_a")

    def _run(self, td, args, tbl):
        tree = Tree(td, self.MASTER, "<scene></scene>", self.LOC, tbl)
        code = tree.run(amv, "add_map_villages.py", args, [mock.patch.object(amv, "VILLAGES", [self.VILLAGE])])
        return code, tree

    def test_main_Apply_TagsTheBrazilianAndItalianRowsFromTheTable(self):
        with tempfile.TemporaryDirectory() as td:
            code, tree = self._run(td, ["--apply"], table(BR={P + "village_X_1": "{.FS}"}, IT={P + "village_X_1": "{.FN}"}))
            self.assertEqual(code, 0)
            self.assertIn('id="Settlements.Settlement.name.village_X_1" text="{.FS}Aldeia X"', tree.loc("BR"))
            self.assertIn('id="Settlements.Settlement.name.village_X_1" text="{.FN}Aldeia X"', tree.loc("IT"))

    def test_main_Apply_OtherLanguages_GetThePlainName(self):
        with tempfile.TemporaryDirectory() as td:
            _, tree = self._run(td, ["--apply"], table(BR={P + "village_X_1": "{.FS}"}))
            self.assertIn('id="Settlements.Settlement.name.village_X_1" text="Aldeia X"', tree.loc("DE"))
            self.assertNotIn("{.", tree.loc("DE"))

    def test_main_Apply_IdTheTableDoesNotKnow_IsWrittenPlain(self):
        with tempfile.TemporaryDirectory() as td:
            _, tree = self._run(td, ["--apply"], table())
            self.assertIn('text="Aldeia X"', tree.loc("BR"))
            self.assertNotIn("{.", tree.loc("BR"))

    def test_main_DryRun_WritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            code, tree = self._run(td, [], table(BR={P + "village_X_1": "{.FS}"}))
            self.assertEqual(code, 0)
            self.assertNotIn("village_X_1", tree.loc("BR"))

    def test_main_Apply_NameAnotherSettlementCarries_RefusesAndWritesNothing(self):
        master = self.MASTER.replace('id="town_A" ', 'id="town_A" name="{=Settlements.Settlement.name.town_A}Aldeia X" ')
        err = io.StringIO()
        with tempfile.TemporaryDirectory() as td:
            tree = Tree(td, master, "<scene></scene>", self.LOC, table())
            code = tree.run(amv, "add_map_villages.py", ["--apply"],
                            [mock.patch.object(amv, "VILLAGES", [self.VILLAGE]), mock.patch("sys.stderr", err)])
            self.assertEqual(code, 1)
            self.assertIn("is already on the map (town_A)", err.getvalue())
            self.assertNotIn("village_X_1", Path(tree.live).read_text(encoding="utf-8"))
            self.assertNotIn("village_X_1", tree.loc("DE"))

    def test_main_Apply_SceneWithNoFolder_RefusesAndWritesNothing(self):
        err = io.StringIO()
        with tempfile.TemporaryDirectory() as td:
            tree = Tree(td, self.MASTER, "<scene></scene>", self.LOC, table())
            code = tree.run(amv, "add_map_villages.py", ["--apply"],
                            [mock.patch.object(amv, "VILLAGES", [self.VILLAGE]), mock.patch("sys.stderr", err),
                             mock.patch.object(amv, "live_scene_folders", return_value=set())])
            self.assertNotEqual(code, 0)
            self.assertIn("scene gondor_village_a has no scene.xscene in any module", err.getvalue())
            self.assertNotIn("village_X_1", Path(tree.live).read_text(encoding="utf-8"))
            self.assertNotIn("village_X_1", tree.loc("DE"))

    def test_main_Apply_LoadsTheTableOnce(self):
        with tempfile.TemporaryDirectory() as td:
            tree = Tree(td, self.MASTER, "<scene></scene>", self.LOC, table())
            with mock.patch.object(nt, "load_table", wraps=nt.load_table) as load:
                tree.run(amv, "add_map_villages.py", ["--apply"], [mock.patch.object(amv, "VILLAGES", [self.VILLAGE])])
            self.assertEqual(load.call_count, 1)


class RenameMapSettlementsMainTests(unittest.TestCase):
    def _tree(self, td):
        tbl = table(BR={P + "castle_village_EW7_3": "{.FS}", P + "town_EW10": "{.NS}"}, IT={P + "castle_village_EW7_3": "{.FN}"})
        return Tree(td, base.MASTER, base.SCENE, base.LOC, tbl)

    def _run(self, tree, args):
        extra = [mock.patch.object(rms, "RENAMES", [base.RENAME]), mock.patch.object(rms, "REMOVALS", []),
                 mock.patch.object(rms, "REPO_ROOT", tree.repo), mock.patch.object(rms, "REPO_ID_FILES", ["ids/gondor.json"])]
        return tree.run(rms, "rename_map_settlements.py", args, extra)

    def test_main_Apply_MovesTheTableEntriesToTheNewId(self):
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            self.assertEqual(self._run(tree, ["--apply"]), 0)
            self.assertEqual(tree.table()["languages"]["BR"]["tags"], {P + "town_EW10": "{.NS}", P + "village_EW10_1": "{.FS}"})
            self.assertEqual(tree.table()["languages"]["IT"]["tags"], {P + "village_EW10_1": "{.FN}"})

    def test_main_DryRun_LeavesTheTableAlone(self):
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            self.assertEqual(self._run(tree, []), 0)
            self.assertIn(P + "castle_village_EW7_3", tree.table()["languages"]["BR"]["tags"])

    def test_main_SecondApply_KeepsTheTableAsItWas(self):
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            self._run(tree, ["--apply"])
            after = tree.table()
            self.assertEqual(self._run(tree, ["--apply"]), 0)
            self.assertEqual(tree.table(), after)

    def test_main_Apply_RenamesTheLocKeyAndKeepsTheRowsText(self):
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            self._run(tree, ["--apply"])
            self.assertIn('id="Settlements.Settlement.name.village_EW10_1" text="Westbrücken-Hof"', tree.loc("DE"))


if __name__ == "__main__":
    unittest.main()
