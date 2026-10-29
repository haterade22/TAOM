#!/usr/bin/env python3
"""`block_src` and the Culture.<vanilla id> rename in promote_borrowed_cultures.py: a source whose data uses
two names. Dale's troops and townsfolk are `dale_*`, but its culture id, strings, lord kits and enlistment
kits are `sturgia`; Rohan's are `rohan_*` and `vlandia`. Neither has child or education templates, so
both borrow Gondor's. `_northman_successor()` builds a target from either.

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
DALE_REALMS = ("anduinvale", "angaladh", "dorwinion")
ROHAN_REALMS = ("southrhovanion",)


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

    def test_a_vlandia_block_is_renamed_but_its_items_are_kept(self):
        cfg = promo.TARGETS["southrhovanion"]
        text = ('<EquipmentRoster id="enlist_vlandia_cavalry_recruit" culture="Culture.vlandia">'
                '<equipment id="Item.vlandia_sword_1_t2" /><equipment id="Item.cts_rohan_helmet1" /></EquipmentRoster>')
        bcfg = promo.block_cfg(cfg, "equipmentsets/taom_enlistment_equipment.xml")
        self.assertEqual(bcfg["src"], "vlandia")
        out = promo.transform(text, "southrhovanion", bcfg)
        self.assertIn('id="enlist_southrhovanion_cavalry_recruit"', out)
        self.assertIn('culture="Culture.southrhovanion"', out)
        self.assertIn("Item.vlandia_sword_1_t2", out)
        self.assertIn("Item.cts_rohan_helmet1", out)
        promo.contamination_check("southrhovanion", bcfg, out)  # passes


class CultureTokenTests(unittest.TestCase):
    """The source npcs, rosters and wanderers carry Culture.sturgia or Culture.vlandia. The whole token is an
    id override, so the bare vanilla id, which also names vanilla assets the clone keeps, is never renamed."""

    def check(self, realms, vid):
        text = (f'<NPCCharacter id="villager_x" culture="Culture.{vid}" />'
                f'<x a="Culture.{vid}_raiders" b="NPCCharacter.tournament_template_{vid}_one_participant_set_v1"'
                f' c="encounter_{vid}" d="BodyProperty.default_character_creation_body_property_{vid}" />'
                f'<!-- vanilla {vid} remapped -->')
        for c in realms:
            out = promo.apply_id_map(text, promo.TARGETS[c]["id_overrides"])
            self.assertIn(f'culture="Culture.{c}"', out)
            for kept in (f"Culture.{vid}_raiders", f"tournament_template_{vid}_one_participant_set_v1",
                         f"encounter_{vid}", f"default_character_creation_body_property_{vid}",
                         f"<!-- vanilla {vid} remapped -->"):
                self.assertIn(kept, out)

    def test_only_the_exact_sturgia_token_is_renamed(self):
        self.check(DALE_REALMS, "sturgia")

    def test_only_the_exact_vlandia_token_is_renamed(self):
        self.check(ROHAN_REALMS, "vlandia")


class NorthmanSuccessorTests(unittest.TestCase):
    """One helper builds a realm from either scaffold; the scaffold decides every name the source uses."""

    def test_the_rohan_scaffold_reads_rohan_and_vlandia(self):
        cfg = promo._northman_successor("rohan", c="testrealm", elite="testrealm_youth", tag="Test")
        self.assertEqual((cfg["src"], cfg["src_short"], cfg["culture_xslt"]), ("rohan", "roh", "vlandia"))
        self.assertEqual(cfg["culture_xslt_keys"], ("aom_vlandia_name", "aom_vlandia_desc"))
        self.assertEqual(cfg["block_src"]["taom_module_strings.xml"], "vlandia")
        self.assertEqual(cfg["block_src"][LORD_KITS], "vlandia")
        self.assertEqual(cfg["block_src"]["taom_education_character_templates.xml"], "gondor")
        self.assertEqual(cfg["id_overrides"], {
            "testrealm_edoras_recruit": "testrealm_levy",
            "testrealm_edoras_golden_hall_rider": "testrealm_youth",
            "Culture.vlandia": "Culture.testrealm",
            "fighter_gondor": "fighter_rohan"})
        self.assertEqual(cfg["lord_face"], "fighter_rohan")
        self.assertEqual(cfg["mercenaries"], ["testrealm_youth_merc"])
        self.assertEqual(cfg["rosters_kept"], r"^testrealm_(bat_template_medium|civ_template_default)_[a-e]$")
        self.assertNotIn("c", cfg)
        self.assertNotIn("elite", cfg)

    def test_the_dale_scaffold_reads_dale_and_sturgia(self):
        cfg = promo._northman_successor("dale", c="testrealm", elite="testrealm_noble")
        self.assertEqual((cfg["src"], cfg["src_short"], cfg["culture_xslt"]), ("dale", "dl", "sturgia"))
        self.assertEqual(cfg["culture_xslt_keys"], ("TAOM_sturgia_culture", "TAOM_sturgia_desc"))
        self.assertEqual(cfg["id_overrides"], {
            "testrealm_recruit": "testrealm_levy", "testrealm_squire": "testrealm_noble",
            "Culture.sturgia": "Culture.testrealm", "fighter_gondor": "fighter_sturgia"})
        self.assertEqual(cfg["feats"], ["taom_dale_plain_speed", "taom_dale_loyalty"])

    def test_a_target_key_wins_over_the_scaffold(self):
        cfg = promo._northman_successor("rohan", c="testrealm", elite="testrealm_youth", feats=["x"])
        self.assertEqual(cfg["feats"], ["x"])
        self.assertEqual(promo.NORTHMAN_SCAFFOLDS["rohan"]["feats"][0], "taom_rohan_infantry_speed")

    def test_rohans_feats_drop_its_cheap_cavalry(self):
        feats = promo.TARGETS["southrhovanion"]["feats"]
        self.assertNotIn("taom_rohan_mounted_cost", feats)
        self.assertNotIn("taom_rohan_mounted_wage", feats)
        self.assertIn("taom_rohan_plain_speed", feats)

    def test_rohans_culture_keys_are_renamed_as_the_source(self):
        rendered = ('  <Culture\n    id="vlandia"\n    name="{=aom_vlandia_name}Rohirrim"\n'
                    '    text="{=aom_vlandia_desc}Riders">\n  </Culture>\n')
        real = promo.render_xslt_culture
        promo.render_xslt_culture = lambda cid: rendered
        try:
            block = promo.source_blocks("culture", "", "rohan", promo.TARGETS["southrhovanion"])
        finally:
            promo.render_xslt_culture = real
        self.assertIn('\n    id="rohan"\n', block)
        self.assertIn('name="{=aom_rohan_name}Rohirrim"', block)
        self.assertIn('text="{=aom_rohan_desc}Riders"', block)
        self.assertNotIn("vlandia", block)


class NorthmanLordTemplatesTests(unittest.TestCase):
    """Dale's and Rohan's cultures list vanilla leaders; a hero keeps its template's culture, so each realm
    cut from them writes and lists its own two lord templates (CultureLordTemplateTests)."""

    def test_every_realm_declares_two_lord_templates_on_its_source_face(self):
        for realms, face in ((DALE_REALMS, "fighter_sturgia"), (ROHAN_REALMS, "fighter_rohan")):
            for c in realms:
                self.assertEqual(len(promo.TARGETS[c]["lord_templates"]), 2, c)
                self.assertEqual(promo.TARGETS[c]["lord_face"], face, c)

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
