#!/usr/bin/env python3
"""Unit tests for tools/sync_ranged_ladder_translations.py (#617): the translated ladder names move
from the retired band ids to the per-tier ids, byte-faithfully.

Run:  python -m unittest discover -s tools/tests -p "test_sync_ranged_ladder_translations.py"
"""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ranged_ladder as rl  # noqa: E402
import sync_ranged_ladder_translations as st  # noqa: E402

EOL = "\r\r\n"   # the translated Armory files end every line this way


def _spec():
    t = {str(i): 10 * i + 40 for i in range(11)}
    return {
        "bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
        "stats": {"anchor_rank": 1,
                  "damage": {"Bow": t, "Crossbow": t, "step": {str(i): 0.0 for i in range(11)}},
                  "accuracy": {"top": {str(i): 80 + i for i in range(11)}, "rank_step": 1, "crossbow_bonus": 0, "max": 99},
                  "speed": {"tier_base": {str(i): 60 + i for i in range(11)}, "rank_step": 1},
                  "skill": {"anchor": {str(i): 20 * i for i in range(11)}, "step": {str(i): 0 for i in range(11)}, "min": 0}},
        "lines": [
            {"id": "elf", "folder": "elf", "files": ["elf"], "ranks": {"overall": 1, "damage": 1, "accuracy": 1},
             "tiers": {"Bow": [2, 5]}, "donor": {"Bow": "elf_bow"}},
            {"id": "ithilien", "folder": "gondor", "files": ["gondor"], "prefixes": ["gondor_ith_"],
             "ranks": {"overall": 1, "damage": 1, "accuracy": 1}, "tiers": {"Bow": [7]}, "donor": {"Bow": "ith_bow"}},
        ],
    }


def _loc(rows, extra_before="", extra_after=""):
    body = "".join(f'    <string id="{i}" text="{t}" />{EOL}' for i, t in rows)
    return (f"<?xml version=\"1.0\" encoding=\"utf-8\"?>{EOL}<base type=\"string\">{EOL}  <strings>{EOL}"
            f"{extra_before}{body}{extra_after}  </strings>{EOL}</base>{EOL}")


class SyncTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.md = self.modules / "LOTRLOME_Armory" / "ModuleData"
        (self.md / "Languages" / "DE").mkdir(parents=True)
        (self.md / "Languages" / "JP").mkdir(parents=True)
        self.de_elf = self.md / "Languages" / "DE" / "loc_elf.xml"
        self.de_elf.write_bytes(_loc(
            [("ladder_elf_bow_e", "[Elb] Langbogen I"), ("ladder_elf_bow_v", "[Elb] Langbogen III"),
             ("ladder_elf_bow_c", "[Elb] Langbogen V")],
            extra_before=f'    <string id="other_key" text="Andere" />{EOL}',
            extra_after=f'    <string id="tail_key" text="Ende &amp; mehr" />{EOL}').encode("utf-8"))
        self.jp_gondor = self.md / "Languages" / "JP" / "loc_gondor.xml"
        self.jp_gondor.write_bytes(_loc([("ladder_gondor_special_bow_x", "イシリエンの弓 IV")]).encode("utf-8"))
        self.spec = root / "spec.json"
        self.spec.write_text(json.dumps(_spec()), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return st.main(["--game-modules", str(self.modules), "--asset-repo", str(self.modules / "none"),
                        "--spec", str(self.spec), *extra])

    def test_dry_run_writes_nothing(self):
        before = self.de_elf.read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual(self.de_elf.read_bytes(), before)

    def test_apply_moves_the_translated_base_to_the_tier_ids_in_place(self):
        self.assertEqual(self._run("--apply"), 0)
        raw = self.de_elf.read_bytes()
        text = raw.decode("utf-8")
        self.assertEqual(text.count("\r\r\n"), text.count("\n"))        # every line keeps its doubled CR
        self.assertNotIn("ladder_elf_bow_e", text)                       # retired rows gone
        self.assertIn('<string id="ladder_elf_bow_t2" text="[Elb] Langbogen II" />', text)   # T2 is band E
        self.assertIn('<string id="ladder_elf_bow_t5" text="[Elb] Langbogen V" />', text)    # T5 is band V
        self.assertLess(text.index("other_key"), text.index("ladder_elf_bow_t2"))
        self.assertLess(text.index("ladder_elf_bow_t5"), text.index("tail_key"))
        self.assertIn("Ende &amp; mehr", text)                            # other rows byte-identical
        jp = self.jp_gondor.read_bytes().decode("utf-8")
        self.assertIn('<string id="ladder_ithilien_bow_t7" text="イシリエンの弓 VII" />', jp)  # renamed line
        self.assertNotIn("gondor_special", jp)

    def test_apply_is_idempotent_and_verify_sees_drift(self):
        self.assertEqual(self._run("--verify"), 1)
        self.assertEqual(self._run("--apply"), 0)
        first = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self.de_elf.read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)

    def test_a_row_that_cannot_be_derived_writes_nothing(self):
        self.jp_gondor.write_bytes(_loc([("ladder_gondor_special_bow_e", "弓 I")]).encode("utf-8"))  # no X band row
        before = self.de_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.de_elf.read_bytes(), before)

    def test_translated_base_strips_only_a_separate_numeral(self):
        self.assertEqual(st.translated_base("[Elb] Langbogen IV"), "[Elb] Langbogen")
        with self.assertRaises(st.SyncError):
            st.translated_base("Bogen der Rohirrim")          # no numeral: never eat a trailing letter

    def test_retired_id_maps_the_split_gondor_lines(self):
        item = rl.LadderItem("ladder_blackroot_bow_t3", "blackroot", "Bow", 3, "R", 1, 1, 1, "d", "gondor")
        self.assertEqual(st.retired_id(item), "ladder_gondor_special_bow_r")


if __name__ == "__main__":
    unittest.main()
