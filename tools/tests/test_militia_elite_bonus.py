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

    def test_a_single_quoted_binding_is_read_like_the_validator_reads_it(self):
        # Review 2026-09-18: taom_schema's militia reader takes either quote and this one took
        # only double quotes, so the two could disagree on who is militia.
        with open(os.path.join(self.md, "taom_spcultures.xml"), "a", encoding="utf-8") as f:
            f.write("<Culture id='z' ranged_elite_militia_troop='NPCCharacter.z_arc_vet' />\n")
        rb._militia_ids_cache.clear()
        rb._elite_militia_ids_cache.clear()
        self.assertIn("z_arc_vet", rb.militia_troop_ids(self.md))
        self.assertIn("z_arc_vet", rb.elite_militia_troop_ids(self.md))


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

    def test_the_melee_elite_slot_gets_the_same_step(self):
        basic = rb.calculate_skills("gondor", 11, "Infantry", "gondor_militia_spearman", "Gondor Militia Spearman",
                                    weapon_classes={"Polearm", "Shield"})
        vet = rb.calculate_skills("gondor", 16, "Infantry", "gondor_militia_veteran_spearman",
                                  "Gondor Veteran Militia Spearman", weapon_classes={"Polearm", "Shield"})
        for skill in rb.SKILL_NAMES:
            self.assertEqual(vet[skill], basic[skill] + rb.MILITIA_ELITE_BONUS, skill)

    def test_every_culture_on_disk_carries_the_step(self):
        """Against the committed troop files: each elite militia sits exactly the bonus above its
        basic sibling on every skill the sibling has above the floor (a basic skill floored at 0
        hides part of the step, so there only 0 < diff <= bonus is provable)."""
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
        import re
        import ranged_ladder as rl
        troops = rl.load_ranged_troops()
        spec = rl.load_spec()
        elite = rb.elite_militia_troop_ids()
        token_cls = {"bow": "Bow", "xbow": "Crossbow"}

        def ladder_skills(t):
            # Since #617 a militia archer's Bow or Crossbow is its ranged ladder cell (by tier),
            # not the flat militia step: the ladder owns the skill of every launcher it hands out.
            return {token_cls[m.group(1)] for st in t.sets for i in st.values()
                    for m in [re.match(r"^ladder_.+_(bow|xbow)_t\d+$", i)] if m}

        checked = 0
        for vet_id in sorted(elite):
            basic_id = vet_id.replace("_veteran", "")
            self.assertIn(basic_id, troops, vet_id)
            vet, basic = troops[vet_id].skills, troops[basic_id].skills
            owned = ladder_skills(troops[vet_id]) | ladder_skills(troops[basic_id])
            for skill in sorted(owned):
                for t in (troops[vet_id], troops[basic_id]):
                    if skill in ladder_skills(t):
                        line = rl.line_of(t, spec)
                        self.assertEqual(t.skills.get(skill, 0), rl.skill_cell(line, t.tier, spec), f"{t.id} {skill}")
                self.assertGreaterEqual(vet.get(skill, 0), basic.get(skill, 0), f"{vet_id} {skill}")
            for skill in rb.SKILL_NAMES:
                if skill in owned:
                    continue
                b, v = basic.get(skill, 0), vet.get(skill, 0)
                if b >= rb.MILITIA_ELITE_BONUS:
                    self.assertEqual(v, b + rb.MILITIA_ELITE_BONUS, f"{vet_id} {skill}")
                else:
                    self.assertTrue(b <= v <= b + rb.MILITIA_ELITE_BONUS, f"{vet_id} {skill} {b}->{v}")
            checked += 1
        self.assertEqual(checked, 30)

    def test_elite_ids_survive_a_basic_cache_filled_out_of_band(self):
        """A basic entry without its elite twin (something filled the basic cache directly) is
        re-read, never a KeyError (deep review 2026-09-13, tooling agent)."""
        root = os.path.abspath(rb.MODULEDATA_DIR)
        saved = dict(rb._militia_ids_cache), dict(rb._elite_militia_ids_cache)
        try:
            rb._militia_ids_cache.clear()
            rb._elite_militia_ids_cache.clear()
            rb._militia_ids_cache[root] = {"stale"}
            self.assertIn("gondor_militia_veteran_archer", rb.elite_militia_troop_ids())
            self.assertIn("gondor_militia_archer", rb.militia_troop_ids())   # the stale entry was replaced
        finally:
            rb._militia_ids_cache.clear(); rb._militia_ids_cache.update(saved[0])
            rb._elite_militia_ids_cache.clear(); rb._elite_militia_ids_cache.update(saved[1])

    def test_a_line_troop_at_the_same_level_is_untouched(self):
        line = rb.calculate_skills("gondor", 16, "Ranged", "gondor_brv_bowman", "Blackroot Vale Bowman",
                                   weapon_classes={"Bow", "Arrows", "OneHanded"})
        self.assertEqual(line["Bow"], rb.RANGED_BASELINES[16]["Bow"] + rb.CULTURAL_MODS["gondor"].get("Bow", 0))


if __name__ == "__main__":
    unittest.main()
