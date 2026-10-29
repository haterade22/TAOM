#!/usr/bin/env python3
"""`block_src` and the Culture.sturgia rename in promote_borrowed_cultures.py: a source whose data uses
two names. Dale's troops and townsfolk are `dale_*`, but its culture id, strings, lord kits and enlistment
kits are `sturgia`, and its child and education templates are borrowed from Gondor.

Run:  python -m unittest tools.tests.test_promote_block_src
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import promote_borrowed_cultures as promo  # noqa: E402

LORD_KITS = "equipmentsets/taom_lord_template_equipment.xml"
CFG = {"src": "dale", "src_short": "dl", "short": "av", "race": None, "subs": [("Dale", "Vale")],
       "block_src": {LORD_KITS: "sturgia"}}


class BlockCfgTests(unittest.TestCase):
    def test_a_listed_file_reads_under_its_own_source(self):
        self.assertEqual(promo.block_cfg(CFG, LORD_KITS)["src"], "sturgia")
        self.assertEqual(CFG["src"], "dale")  # the target's own config is not changed

    def test_an_unlisted_file_keeps_the_target_config(self):
        self.assertIs(promo.block_cfg(CFG, "taom_wanderers.xml"), CFG)
        self.assertEqual(promo.block_cfg({"src": "gondor"}, LORD_KITS)["src"], "gondor")

    def test_a_sturgia_block_is_renamed_but_its_items_are_kept(self):
        text = '<EquipmentRoster id="taom_sturgia_lord_battle_male" culture="Culture.sturgia"><equipment id="Item.sturgia_axe_2_t2" /></EquipmentRoster>'
        bcfg = promo.block_cfg(CFG, LORD_KITS)
        out = promo.transform(text, "anduinvale", bcfg)
        self.assertIn('id="taom_anduinvale_lord_battle_male"', out)
        self.assertIn('culture="Culture.anduinvale"', out)
        self.assertIn("Item.sturgia_axe_2_t2", out)
        promo.contamination_check("anduinvale", bcfg, out)  # passes


class SturgiaTokenTests(unittest.TestCase):
    """Dale's npcs, rosters and wanderers carry Culture.sturgia. The whole token is an id override, so
    bare `sturgia`, which also names vanilla assets the clone keeps, is never renamed."""

    def test_only_the_exact_culture_token_is_renamed(self):
        text = ('<NPCCharacter id="villager_anduinvale" culture="Culture.sturgia" />'
                '<x a="Culture.sturgia_raiders" b="NPCCharacter.tournament_template_sturgia_one_participant_set_v1"'
                ' c="encounter_sturgia" d="BodyProperty.default_character_creation_body_property_sturgia" />'
                '<!-- vanilla sturgia remapped -->')
        for c in ("anduinvale", "angaladh"):
            out = promo.apply_id_map(text, promo.TARGETS[c]["id_overrides"])
            self.assertIn(f'culture="Culture.{c}"', out)
            for kept in ("Culture.sturgia_raiders", "tournament_template_sturgia_one_participant_set_v1",
                         "encounter_sturgia", "default_character_creation_body_property_sturgia",
                         "<!-- vanilla sturgia remapped -->"):
                self.assertIn(kept, out)


class DaleLordTemplatesTests(unittest.TestCase):
    """Dale's culture lists vanilla Sturgian leaders; a hero keeps its template's culture, so each
    Dale-cut realm writes and lists its own two lord templates (CultureLordTemplateTests)."""

    def test_both_realms_declare_two_lord_templates_on_dales_face(self):
        for c in ("anduinvale", "angaladh"):
            self.assertEqual(len(promo.TARGETS[c]["lord_templates"]), 2, c)
            self.assertEqual(promo.TARGETS[c]["lord_face"], "fighter_sturgia", c)

    def test_add_lord_templates_appends_before_the_root_close_in_the_target_culture(self):
        body = '<NPCCharacters>\n\t<NPCCharacter id="x" />\n</NPCCharacters>\n'
        cfg = {"lord_templates": [("Bold lord", "curt", 1), ("Cautious lord", "earnest", -1)],
               "lord_face": "fighter_test"}
        out = promo.add_lord_templates(body, "anduinvale", cfg)
        self.assertTrue(out.rstrip().endswith("</NPCCharacters>"))
        self.assertEqual(out.count('culture="Culture.anduinvale"'), 2)
        self.assertEqual(out.count("BodyProperty.fighter_test"), 2)
        self.assertIn('id="spc_anduinvale_lord_2"', out)
        self.assertIn('EquipmentSet id="taom_anduinvale_lord_battle_male"', out)
        self.assertNotIn("\r", out)


if __name__ == "__main__":
    unittest.main()
