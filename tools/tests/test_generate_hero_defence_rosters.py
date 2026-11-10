"""Tests for tools/generate_hero_defence_rosters.py: each filter, the merges, the file output, the drift check and the exit codes."""
import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import generate_hero_defence_rosters as gen  # noqa: E402


def npc(troop_id, culture, level=6, group="Infantry", occupation="Soldier", targets=()):
    upgrades = "".join(f'<upgrade_target id="NPCCharacter.{t}" />' for t in targets)
    body = f"<upgrade_targets>{upgrades}</upgrade_targets>" if targets else ""
    return (f'<NPCCharacter id="{troop_id}" level="{level}" default_group="{group}" '
            f'occupation="{occupation}" culture="Culture.{culture}">{body}</NPCCharacter>')


def write_tree(folder, name, *npcs):
    (folder / f"troops_{name}.xml").write_text("<NPCCharacters>" + "".join(npcs) + "</NPCCharacters>", encoding="utf-8")


class BuildTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.addCleanup(self._temp.cleanup)
        self.folder = Path(self._temp.name)

    def _build(self):
        return gen.build(self.folder)

    def _tree(self, *npcs, name="rohan"):
        base = (npc("base_a", "vlandia", targets=["base_b"]), npc("base_b", "vlandia", level=11))
        write_tree(self.folder, name, *base, *npcs)

    def test_keeps_a_linked_soldier_with_its_level_and_class(self):
        self._tree(npc("rider", "vlandia", level=16, group="Cavalry", targets=["base_a"]))

        self.assertIn({"id": "rider", "level": 16, "class": "Cavalry"}, self._build()["vlandia"])

    def test_keeps_a_troop_that_is_only_an_upgrade_target(self):
        self._tree()

        self.assertIn("base_b", [row["id"] for row in self._build()["vlandia"]])

    def test_sorts_rows_by_level_then_id(self):
        self._tree(npc("a_late", "vlandia", level=31, targets=["base_a"]), npc("a_early", "vlandia", level=1, targets=["base_a"]))

        self.assertEqual(["a_early", "base_a", "base_b", "a_late"], [row["id"] for row in self._build()["vlandia"]])

    def test_skips_the_feral_orc_file(self):
        write_tree(self.folder, "feral_orcs", npc("feral_a", "feral_orcs", targets=["feral_b"]), npc("feral_b", "feral_orcs"))

        self.assertNotIn("feral_orcs", self._build())

    def test_drops_a_troop_of_another_culture_than_its_tree(self):
        self._tree(npc("raider", "vlandia_raiders", targets=["base_a"]))

        self.assertNotIn("raider", [row["id"] for row in self._build()["vlandia"]])

    def test_drops_a_non_soldier(self):
        self._tree(npc("sellsword", "vlandia", occupation="Mercenary", targets=["base_a"]))

        self.assertNotIn("sellsword", [row["id"] for row in self._build()["vlandia"]])

    def test_drops_settlement_militia(self):
        self._tree(npc("rohan_militia_spearman", "vlandia", targets=["base_a"]),
                   npc("rohan_militia_veteran_archer", "vlandia", group="Ranged", targets=["base_a"]))

        ids = [row["id"] for row in self._build()["vlandia"]]
        self.assertNotIn("rohan_militia_spearman", ids)
        self.assertNotIn("rohan_militia_veteran_archer", ids)

    def test_keeps_a_line_unit_with_militia_in_its_name(self):
        self._tree(npc("rohan_westfold_militiaman", "vlandia", targets=["base_a"]))

        self.assertIn("rohan_westfold_militiaman", [row["id"] for row in self._build()["vlandia"]])

    def test_drops_a_troop_with_no_upgrade_edge(self):
        self._tree(npc("hill_troll", "vlandia"))

        self.assertNotIn("hill_troll", [row["id"] for row in self._build()["vlandia"]])

    def test_merges_shaghana_into_aserai(self):
        write_tree(self.folder, "harad",
                   npc("a1", "aserai", targets=["a2"]), npc("a2", "aserai"), npc("a3", "aserai", targets=["s1"]),
                   npc("s1", "shaghana", targets=["s2"]), npc("s2", "shaghana"))

        built = self._build()
        self.assertNotIn("shaghana", built)
        self.assertEqual({"a1", "a2", "a3", "s1", "s2"}, {row["id"] for row in built["aserai"]})

    def test_merges_two_files_of_one_culture(self):
        write_tree(self.folder, "gundabad", npc("g1", "gundabad", targets=["g2"]), npc("g2", "gundabad"))
        write_tree(self.folder, "gundabad_snow", npc("g3", "gundabad", targets=["g4"]), npc("g4", "gundabad"))

        self.assertEqual({"g1", "g2", "g3", "g4"}, {row["id"] for row in self._build()["gundabad"]})

    def test_an_upgrade_edge_from_a_dropped_troop_still_counts(self):
        self._tree(npc("rohan_militia_spearman", "vlandia", targets=["only_target"]), npc("only_target", "vlandia"))

        self.assertIn("only_target", [row["id"] for row in self._build()["vlandia"]])

    def test_an_empty_folder_builds_nothing(self):
        self.assertEqual({}, self._build())

    def test_an_unknown_default_group_raises(self):
        self._tree(npc("wyrm", "vlandia", group="Dragon", targets=["base_a"]))

        with self.assertRaises(ValueError):
            self._build()


class OutputTests(unittest.TestCase):
    ROSTERS = {"b": [{"id": "x", "level": 1, "class": "Infantry"}], "a": [{"id": "y", "level": 6, "class": "Ranged"}]}

    def test_render_sorts_cultures_and_parses_back(self):
        text = gen.render(self.ROSTERS)

        self.assertLess(text.index('"a"'), text.index('"b"'))
        self.assertEqual(self.ROSTERS, json.loads(text))

    def test_render_ends_with_one_newline_and_is_ascii(self):
        text = gen.render(self.ROSTERS)

        self.assertTrue(text.endswith("}\n"))
        self.assertFalse(text.endswith("\n\n"))
        self.assertTrue(text.isascii())

    def test_is_current_ignores_line_endings(self):
        text = gen.render(self.ROSTERS)

        self.assertTrue(gen.is_current(text, text.replace("\n", "\r\n")))

    def test_is_current_finds_a_drifted_row(self):
        text = gen.render(self.ROSTERS)

        self.assertFalse(gen.is_current(text, text.replace('"level": 1', '"level": 2')))


class MainTests(unittest.TestCase):
    EXPECTED = {"vlandia": [{"id": "a", "level": 6, "class": "Infantry"}, {"id": "b", "level": 6, "class": "Infantry"}]}

    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.addCleanup(self._temp.cleanup)
        root = Path(self._temp.name)
        self.troops = root / "troops"
        self.troops.mkdir()
        self.shipped = root / "rosters.json"
        self.out = root / "out.json"
        for name, value in (("TROOPS", self.troops), ("SHIPPED", self.shipped)):
            patcher = mock.patch.object(gen, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def _main(self, *argv):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return gen.main(list(argv))

    def _good_tree(self):
        write_tree(self.troops, "rohan", npc("a", "vlandia", targets=["b"]), npc("b", "vlandia"))

    def test_out_writes_the_list_and_exits_0(self):
        self._good_tree()

        self.assertEqual(0, self._main("--out", str(self.out)))
        self.assertEqual(self.EXPECTED, json.loads(self.out.read_text(encoding="utf-8")))

    def test_check_a_current_list_exits_0(self):
        self._good_tree()
        self.shipped.write_text(gen.render(self.EXPECTED), encoding="utf-8")

        self.assertEqual(0, self._main("--check"))

    def test_check_a_current_list_with_a_bom_exits_0(self):
        self._good_tree()
        self.shipped.write_text("﻿" + gen.render(self.EXPECTED), encoding="utf-8")

        self.assertEqual(0, self._main("--check"))

    def test_check_a_stale_list_exits_1(self):
        self._good_tree()
        self.shipped.write_text(gen.render({"vlandia": self.EXPECTED["vlandia"][:1]}), encoding="utf-8")

        self.assertEqual(1, self._main("--check"))

    def test_check_a_missing_list_exits_1(self):
        self._good_tree()

        self.assertEqual(1, self._main("--check"))

    def test_an_empty_folder_exits_3_and_writes_nothing(self):
        self.assertEqual(3, self._main("--out", str(self.out)))
        self.assertFalse(self.out.exists())

    def test_an_unknown_default_group_exits_3_and_writes_nothing(self):
        write_tree(self.troops, "rohan", npc("a", "vlandia", group="Dragon", targets=["b"]), npc("b", "vlandia"))

        self.assertEqual(3, self._main("--out", str(self.out)))
        self.assertFalse(self.out.exists())

    def test_a_broken_troop_file_exits_3(self):
        (self.troops / "troops_bad.xml").write_text("<NPCCharacters>", encoding="utf-8")

        self.assertEqual(3, self._main("--check"))


if __name__ == "__main__":
    unittest.main()
