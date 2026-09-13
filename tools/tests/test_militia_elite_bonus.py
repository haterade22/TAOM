#!/usr/bin/env python3
"""The elite (veteran) militia stand a step above the basic militia.

Every militia slot a culture binds takes the level-21 baseline whatever the troop's level, which
is deliberate (militia are siege and village defenders). Until 2026-09-13 the four slots shared
it exactly, so a culture's militia archer (level 11) and veteran militia archer (level 16) had
the same eight skills, and the party screen showed a promotion that changed nothing. The elite
slots (`melee_elite_militia_troop`, `ranged_elite_militia_troop`, `elite_militia_troop`) now add
MILITIA_ELITE_BONUS to every skill before cultural modifiers, and the binding reader keeps the
elite ids apart from the basic ones so the bonus follows the binding, never the name.

Run:  python -m unittest tools.tests.test_militia_elite_bonus
"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebalance_troops as rb  # noqa: E402


class EliteMilitiaBindingTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = self._tmp.name
        with open(os.path.join(self.md, "taom_spcultures.xml"), "w", encoding="utf-8") as f:
            f.write('<Cultures>\n'
                    '  <Culture id="x" melee_militia_troop="NPCCharacter.x_mil" ranged_militia_troop="NPCCharacter.x_arc"\n'
                    '           melee_elite_militia_troop="NPCCharacter.x_mil_vet"\n'
                    '           ranged_elite_militia_troop="NPCCharacter.x_arc_vet" />\n'
                    '  <!-- <Culture id="dead" elite_militia_troop="NPCCharacter.commented_out" /> -->\n'
                    '</Cultures>\n')
        with open(os.path.join(self.md, "spcultures.xslt"), "w", encoding="utf-8") as f:
            f.write('<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
                    '  <xsl:attribute name="militia_troop">NPCCharacter.y_mil</xsl:attribute>\n'
                    '  <xsl:attribute name="elite_militia_troop">NPCCharacter.y_mil_vet</xsl:attribute>\n'
                    '</xsl:stylesheet>\n')

    def tearDown(self):
        self._tmp.cleanup()

    def test_both_encodings_split_basic_from_elite(self):
        self.assertEqual(rb.militia_troop_ids(self.md), {"x_mil", "x_arc", "x_mil_vet", "x_arc_vet", "y_mil", "y_mil_vet"})
        self.assertEqual(rb.elite_militia_troop_ids(self.md), {"x_mil_vet", "x_arc_vet", "y_mil_vet"})


class EliteMilitiaBonusTests(unittest.TestCase):
    """Against the real bindings: Gondor binds gondor_militia_archer and gondor_militia_veteran_archer."""

    def test_the_veteran_is_the_basic_militia_plus_the_bonus_on_every_skill(self):
        self.assertIn("gondor_militia_veteran_archer", rb.elite_militia_troop_ids())
        self.assertNotIn("gondor_militia_archer", rb.elite_militia_troop_ids())
        basic = rb.calculate_skills("gondor", 11, "Ranged", "gondor_militia_archer", "Gondor Militia Archer",
                                    weapon_classes={"Bow", "Arrows", "OneHanded"})
        vet = rb.calculate_skills("gondor", 16, "Ranged", "gondor_militia_veteran_archer",
                                  "Gondor Veteran Militia Archer", weapon_classes={"Bow", "Arrows", "OneHanded"})
        self.assertTrue(10 <= rb.MILITIA_ELITE_BONUS <= 20, "the user asked for 10 to 20")
        for skill in rb.SKILL_NAMES:
            self.assertEqual(vet[skill], basic[skill] + rb.MILITIA_ELITE_BONUS, skill)

    def test_a_line_troop_at_the_same_level_is_untouched(self):
        line = rb.calculate_skills("gondor", 16, "Ranged", "gondor_brv_bowman", "Blackroot Vale Bowman",
                                   weapon_classes={"Bow", "Arrows", "OneHanded"})
        self.assertEqual(line["Bow"], rb.RANGED_BASELINES[16]["Bow"] + rb.CULTURAL_MODS["gondor"].get("Bow", 0))


if __name__ == "__main__":
    unittest.main()
