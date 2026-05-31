#!/usr/bin/env python3
"""Unit tests for the ModuleData query API (tools/taom_query.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

Synthetic ModuleData + injected registries (no game install needed). Loads the
real shipped schemas so a malformed schema fails the suite.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import taom_schema as ts  # noqa: E402
import taom_query as tq    # noqa: E402

SCHEMA_DIR = Path(__file__).resolve().parent.parent / "schemas"


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class ModuleDataQueryTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.reg = ts.Registries(
            items={"good_sword", "shared_helm"},
            item_def_files={"shared_helm": ["erebor/head_armors.xml", "iron_hills/head_armors.xml"]},
            npccharacters={"hero_a", "hero_b"},
            cultures={"gondor", "vlandia"},
            party_templates={"good_template"},
        )
        self.q = tq.ModuleDataQuery(self.md, self.reg, ts.load_schemas(SCHEMA_DIR))

    def tearDown(self):
        self._tmp.cleanup()

    def test_item_exists_true_false_and_prefix(self):
        self.assertTrue(self.q.item_exists("good_sword")["exists"])
        self.assertTrue(self.q.item_exists("Item.good_sword")["exists"])  # prefix tolerated
        self.assertFalse(self.q.item_exists("Item.nope")["exists"])
        self.assertTrue(self.q.item_exists("Item.None")["exists"])  # sentinel

    def test_item_exists_reports_duplicate_folders(self):
        self.assertEqual(self.q.item_exists("shared_helm")["duplicate_in"],
                         ["erebor/head_armors.xml", "iron_hills/head_armors.xml"])

    def test_troop_and_culture_exists(self):
        self.assertTrue(self.q.troop_exists("NPCCharacter.hero_a")["exists"])
        self.assertFalse(self.q.troop_exists("ghost")["exists"])
        self.assertTrue(self.q.culture_exists("Culture.gondor")["exists"])
        self.assertFalse(self.q.culture_exists("rohan")["exists"])

    def test_list_cultures_and_sizes(self):
        self.assertEqual(self.q.list_cultures(), ["gondor", "vlandia"])
        self.assertEqual(self.q.registry_sizes()["npccharacters"], 2)

    def test_list_schemas_nonempty(self):
        names = {s["name"] for s in self.q.list_schemas()}
        self.assertIn("taom_npccharacter", names)

    def test_find_references_locates_refs_with_line(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_b" culture="Culture.gondor">
    <upgrade_targets><upgrade_target id="NPCCharacter.hero_a" /></upgrade_targets>
    <Equipments><EquipmentRoster><equipment slot="Item0" id="Item.good_sword" /></EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        r = self.q.find_references("NPCCharacter.hero_a")
        self.assertEqual(r["kind"], "troop")
        self.assertEqual(r["count"], 1)
        self.assertEqual(r["references"][0]["file"], "troops/troops_test.xml")
        self.assertGreater(r["references"][0]["line"], 0)
        # bare id + explicit kind also works
        self.assertEqual(self.q.find_references("good_sword", kind="item")["count"], 1)
        # comment-stripped: a commented ref is not counted
        self.assertEqual(self.q.find_references("Culture.vlandia")["count"], 0)

    def test_party_template_exists(self):
        self.assertTrue(self.q.party_template_exists("good_template")["exists"])
        self.assertTrue(self.q.party_template_exists("PartyTemplate.good_template")["exists"])
        self.assertFalse(self.q.party_template_exists("nope")["exists"])

    def test_find_references_accepts_npccharacter_kind_alias(self):
        # "npccharacter" (the REF_KINDS name) must work like "troop" (the query kind),
        # not silently fall back to an all-prefix search.
        _write(self.md / "taom_partyTemplates.xml", """<?xml version="1.0" encoding="utf-8"?>
<partyTemplates>
  <MBPartyTemplate id="t"><stacks><PartyTemplateStack troop="NPCCharacter.hero_a" /></stacks></MBPartyTemplate>
</partyTemplates>
""")
        r = self.q.find_references("hero_a", kind="npccharacter")
        self.assertEqual(r["kind"], "troop")
        self.assertEqual(r["count"], 1)
        self.assertEqual(r["references"][0]["ref"], "NPCCharacter.hero_a")

    def test_validate_returns_counts_and_issues(self):
        _write(self.md / "troops" / "troops_bad.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_c" culture="Culture.gondor">
    <Equipments><EquipmentRoster><equipment slot="Body" id="Item.missing_armor" /></EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        out = self.q.validate()
        self.assertGreaterEqual(out["error_count"], 1)
        self.assertTrue(any(i["code"] == "BROKEN_ITEM_REF" for i in out["issues"]))
        # code filter narrows
        only = self.q.validate(codes=["UNKNOWN_CULTURE"])
        self.assertTrue(all(i["code"] == "UNKNOWN_CULTURE" for i in only["issues"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
