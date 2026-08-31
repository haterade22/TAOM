#!/usr/bin/env python3
"""Unit tests for the UPGRADE_SKILL_REGRESSION validator gate and the clamp that feeds it.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_upgrade_skill_monotonicity.py

Synthetic data only, no game install needed.

THE CONTRACT
------------
Upgrading a troop must never lower one of its 8 combat skills. A player reads the troop tree as a
ladder, so a stat going down on promotion is a bug whatever produced it, and three separate things
produced it at once:

  * the level-21 militia baseline leaking into a real line, because militia were detected by a
    name substring. gondor_ano_archer_militia was the only false positive across 871 troops and it
    out-statted its own upgrade target on all 8 skills, -145 total.
  * a default_group that contradicted the carried equipment. dg_warg_red_fang was tagged
    HorseArcher while carrying sword, halberd and shield, so the curve handed it Bow 240 and its
    Cavalry child read as a -200 drop.
  * the Ranged baseline sitting below the Infantry baseline on Polearm and TwoHanded, so an
    Infantry troop branching into Ranged one tier up lost melee.

A skill the target never declares reads as 0 (CharacterObject.GetSkillValue), so an omitted
<skill> element is a drop, not "unchanged". 34 Mordor and Morannon troops shipped that way.

Militia to militia is the one exemption: militia take the level-21 baseline whatever their real
level, so a militia promotion is flat by design. The exemption reads what a culture BINDS, never a
name. Name matching is the bug.
"""
import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebalance_troops as rb  # noqa: E402
import taom_schema as ts       # noqa: E402


def _troop(troop_id, level, group, skills, upgrades=()):
    entries = "\n".join(
        f'      <skill id="{k}" value="{v}" />' for k, v in skills.items())
    targets = "\n".join(
        f'      <upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
    return (
        f'  <NPCCharacter id="{troop_id}" level="{level}" default_group="{group}"\n'
        f'      name="{{=x_{troop_id}}}{troop_id}" occupation="Soldier" culture="Culture.gondor">\n'
        f'    <skills>\n{entries}\n    </skills>\n'
        f'    <upgrade_targets>\n{targets}\n    </upgrade_targets>\n'
        f'  </NPCCharacter>\n'
    )


class ValidatorGateTests(unittest.TestCase):
    """The UPGRADE_SKILL_REGRESSION check in taom_schema.Validator."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="taom_upgrade_gate_")
        os.makedirs(os.path.join(self.root, "troops"))

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def _write(self, troops_xml, cultures_xml='<Cultures />\n'):
        with open(os.path.join(self.root, "troops", "troops_test.xml"), "w", encoding="utf-8") as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n' + troops_xml + '</NPCCharacters>\n')
        with open(os.path.join(self.root, "taom_spcultures.xml"), "w", encoding="utf-8") as f:
            f.write(cultures_xml)

    def _run(self):
        validator = ts.Validator(self.root, [], ts.Registries({}, {}, {}, {}, {}))
        return validator._upgrade_skill_regressions()

    def test_clean_ladder_reports_nothing(self):
        self._write(
            _troop("t1", 11, "Infantry", dict(Athletics=48, Riding=10, OneHanded=60, TwoHanded=55,
                                              Polearm=70, Bow=10, Crossbow=5, Throwing=20), ["t2"])
            + _troop("t2", 16, "Infantry", dict(Athletics=80, Riding=15, OneHanded=90, TwoHanded=85,
                                                Polearm=95, Bow=15, Crossbow=10, Throwing=30)))
        self.assertEqual([], self._run())

    def test_a_single_lowered_skill_is_an_error(self):
        self._write(
            _troop("t1", 11, "Infantry", dict(Athletics=48, Riding=10, OneHanded=60, TwoHanded=55,
                                              Polearm=70, Bow=10, Crossbow=5, Throwing=20), ["t2"])
            + _troop("t2", 16, "Infantry", dict(Athletics=80, Riding=15, OneHanded=90, TwoHanded=85,
                                                Polearm=45, Bow=15, Crossbow=10, Throwing=30)))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("UPGRADE_SKILL_REGRESSION", issues[0].code)
        self.assertEqual("t2", issues[0].entry_id)
        self.assertIn("Polearm 70->45", issues[0].message)

    def test_an_undeclared_skill_counts_as_zero(self):
        """The failure that shipped 34 times: the target simply omits the element."""
        self._write(
            _troop("t1", 11, "Ranged", dict(Athletics=50, Riding=10, OneHanded=30, TwoHanded=25,
                                            Polearm=30, Bow=55, Crossbow=15, Throwing=20), ["t2"])
            + _troop("t2", 16, "Ranged", dict(Athletics=85, OneHanded=70, Bow=85, Throwing=30)))
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertIn("undeclared, reads as 0", issues[0].message)

    def test_the_three_line_skill_form_is_parsed(self):
        """13 of the 16 troop files write <skill> across three lines. A matcher that only handles
        the one-line form reads every troop as having no skills, and then every comparison is
        0 < 0 and the gate passes silently on broken data. That is worse than not having a gate."""
        three_line = (
            '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
            '  <NPCCharacter id="p" level="11" default_group="Infantry" culture="Culture.x">\n'
            '    <skills>\n        <skill\n            id="Polearm"\n            value="70" />\n'
            '    </skills>\n'
            '    <upgrade_targets>\n        <upgrade_target id="NPCCharacter.c" />\n    </upgrade_targets>\n'
            '  </NPCCharacter>\n'
            '  <NPCCharacter id="c" level="16" default_group="Infantry" culture="Culture.x">\n'
            '    <skills>\n        <skill\n            id="Polearm"\n            value="45" />\n'
            '    </skills>\n    <upgrade_targets></upgrade_targets>\n'
            '  </NPCCharacter>\n</NPCCharacters>\n')
        with open(os.path.join(self.root, "troops", "troops_test.xml"), "w", encoding="utf-8") as f:
            f.write(three_line)
        with open(os.path.join(self.root, "taom_spcultures.xml"), "w", encoding="utf-8") as f:
            f.write("<Cultures />\n")
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertIn("Polearm 70->45", issues[0].message)

    def test_militia_to_militia_is_exempt_but_militia_to_a_line_troop_is_not(self):
        cultures = (
            '<Cultures>\n'
            '  <Culture id="x" melee_militia_troop="NPCCharacter.mil"\n'
            '           elite_militia_troop="NPCCharacter.mil_vet" />\n'
            '</Cultures>\n')
        flat = dict(Athletics=95, Riding=15, OneHanded=125, TwoHanded=110,
                    Polearm=130, Bow=15, Crossbow=10, Throwing=50)
        weaker = dict(Athletics=80, Riding=15, OneHanded=90, TwoHanded=85,
                      Polearm=95, Bow=15, Crossbow=10, Throwing=30)
        self._write(
            _troop("mil", 11, "Infantry", flat, ["mil_vet", "line"])
            + _troop("mil_vet", 16, "Infantry", flat)
            + _troop("line", 16, "Infantry", weaker),
            cultures)
        issues = self._run()
        self.assertEqual(["line"], [i.entry_id for i in issues],
                         "militia -> militia is flat by design; militia -> a line troop is not")


class SkillTemplateShadowingTests(ValidatorGateTests):
    """A resolvable skill_template makes the inline <skills> block unreachable.

    BasicCharacterObject.Deserialize only calls DefaultCharacterSkills.Init when the template
    reference came back null (v1.4.8, BasicCharacterObject.cs:337-358), so a character declaring
    both is asserting two different skill sets and the engine silently takes the template. 44
    militia shipped that way for months, wearing vanilla Calradian values while every TAOM tool
    read and rewrote the authored ones (#523).
    """

    def test_declaring_both_a_template_and_inline_skills_is_an_error(self):
        troop = (
            '  <NPCCharacter id="t" level="11" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level11_template_skills">\n'
            '    <skills>\n      <skill id="OneHanded" value="60" />\n    </skills>\n'
            '    <upgrade_targets></upgrade_targets>\n  </NPCCharacter>\n')
        self._write(troop)
        issues = self._run()
        self.assertEqual(1, len(issues))
        self.assertEqual("SKILL_TEMPLATE_SHADOWS_SKILLS", issues[0].code)

    def test_a_template_with_an_empty_block_is_fine(self):
        troop = (
            '  <NPCCharacter id="t" level="11" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level11_template_skills">\n'
            '    <skills></skills>\n    <upgrade_targets></upgrade_targets>\n  </NPCCharacter>\n')
        self._write(troop)
        self.assertEqual([], self._run())

    def test_a_templated_edge_is_not_judged_for_regression(self):
        """Its real skills live in a SkillSet outside these files, so comparing the inline block
        would compare the wrong numbers. The shadowing check owns that case instead."""
        self._write(
            '  <NPCCharacter id="p" level="1" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills">\n'
            '    <skills></skills>\n'
            '    <upgrade_targets>\n      <upgrade_target id="NPCCharacter.c" />\n'
            '    </upgrade_targets>\n  </NPCCharacter>\n'
            + _troop("c", 11, "Infantry", {k: 0 for k in rb.SKILL_NAMES}))
        self.assertEqual([], self._run())


class MilitiaLoaderFailsClosedTests(unittest.TestCase):
    """The militia decision moved from a name heuristic that could not fail to a read of two
    external files. If that read comes back empty every militia is restatted as an ordinary troop,
    roughly a 55% cut across all 60, and the run would otherwise exit 0."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="taom_militia_")
        self._saved = dict(rb._militia_ids_cache)
        rb._militia_ids_cache.clear()

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)
        rb._militia_ids_cache.clear()
        rb._militia_ids_cache.update(self._saved)

    def test_a_missing_binding_file_raises_instead_of_returning_an_empty_set(self):
        with self.assertRaises(RuntimeError) as ctx:
            rb.militia_troop_ids(self.root)
        self.assertIn("restatted as an ordinary troop", str(ctx.exception))

    def test_a_commented_out_binding_does_not_count(self):
        with open(os.path.join(self.root, "taom_spcultures.xml"), "w", encoding="utf-8") as f:
            f.write('<Cultures>\n'
                    '  <Culture id="a" melee_militia_troop="NPCCharacter.real_one" />\n'
                    '  <!-- <Culture id="b" melee_militia_troop="NPCCharacter.commented_out" /> -->\n'
                    '</Cultures>\n')
        with open(os.path.join(self.root, "spcultures.xslt"), "w", encoding="utf-8") as f:
            f.write("<x/>\n")
        ids = rb.militia_troop_ids(self.root)
        self.assertEqual({"real_one"}, ids)

    def test_a_longer_attribute_ending_in_militia_troop_does_not_count(self):
        with open(os.path.join(self.root, "taom_spcultures.xml"), "w", encoding="utf-8") as f:
            f.write('<Cultures>\n'
                    '  <Culture id="a" melee_militia_troop="NPCCharacter.real_one"\n'
                    '           reserve_militia_troop="NPCCharacter.not_a_binding" />\n'
                    '</Cultures>\n')
        with open(os.path.join(self.root, "spcultures.xslt"), "w", encoding="utf-8") as f:
            f.write("<x/>\n")
        self.assertEqual({"real_one"}, rb.militia_troop_ids(self.root))


class MilitiaDetectionTests(unittest.TestCase):
    """is_militia reads culture bindings, not names. This is the reported bug's root cause."""

    def test_the_bound_set_is_the_authority(self):
        bound = rb.militia_troop_ids()
        self.assertEqual(60, len(bound),
                         "60 troops are bound to a culture militia slot. Change this deliberately.")
        self.assertTrue(rb.is_militia("dale_militia_archer"))
        self.assertFalse(
            rb.is_militia("gondor_ano_archer_militia"),
            "gondor_ano_archer_militia is a level-11 Anorien LINE troop. The old name heuristic "
            "matched it and handed it the level-21 militia baseline, which is what made it "
            "out-stat its own upgrade target by 145 points across all 8 skills.")

    def test_both_binding_encodings_are_read(self):
        """taom_spcultures.xml uses an attribute, spcultures.xslt an xsl:attribute element.
        Dale and Rhun use only the second, so missing it silently drops them off the rule."""
        bound = rb.militia_troop_ids()
        self.assertIn("dale_militia_archer", bound)     # xsl:attribute form
        self.assertIn("gondor_militia_archer", bound)   # plain attribute form


class ClampTests(unittest.TestCase):
    """clamp_upgrade_monotonicity raises a target to its source and never the reverse."""

    @staticmethod
    def _record(troop_id, new, upgrades=()):
        return {"id": troop_id, "file": "troops_test.xml", "old": dict(new),
                "new": dict(new), "upgrades": list(upgrades)}

    def test_a_lowered_skill_is_raised_to_the_source(self):
        low = {s: 10 for s in rb.SKILL_NAMES}
        high = {s: 50 for s in rb.SKILL_NAMES}
        records = [self._record("parent", high, ["child"]), self._record("child", low)]
        rb.clamp_upgrade_monotonicity(records)
        self.assertEqual(high, records[1]["final"])
        self.assertEqual(high, records[0]["final"], "the source is never lowered to meet the target")

    def test_a_raise_propagates_down_the_whole_chain(self):
        records = [
            self._record("a", {s: 90 for s in rb.SKILL_NAMES}, ["b"]),
            self._record("b", {s: 10 for s in rb.SKILL_NAMES}, ["c"]),
            self._record("c", {s: 20 for s in rb.SKILL_NAMES}),
        ]
        rb.clamp_upgrade_monotonicity(records)
        self.assertEqual(90, records[2]["final"]["Bow"])

    def test_skills_already_higher_are_left_alone(self):
        parent = {s: 10 for s in rb.SKILL_NAMES}
        child = dict(parent, Bow=200)
        records = [self._record("p", parent, ["c"]), self._record("c", child)]
        rb.clamp_upgrade_monotonicity(records)
        self.assertEqual(200, records[1]["final"]["Bow"])

    def test_a_cycle_is_refused_rather_than_silently_half_clamped(self):
        records = [
            self._record("a", {s: 10 for s in rb.SKILL_NAMES}, ["b"]),
            self._record("b", {s: 10 for s in rb.SKILL_NAMES}, ["a"]),
        ]
        with self.assertRaises(RuntimeError):
            rb.clamp_upgrade_monotonicity(records)


class SkillEntryInsertionTests(unittest.TestCase):
    """The value-only writer could never repair a partial <skills> block; now it can."""

    def test_a_missing_entry_is_cloned_from_the_last_one(self):
        block = ('\n      <skill id="Athletics" value="115" />'
                 '\n      <skill id="OneHanded" value="152" />\n    ')
        out = rb.insert_missing_skill_entries(
            block, {s: 7 for s in rb.SKILL_NAMES}, "t")
        for skill in rb.SKILL_NAMES:
            self.assertIn(f'id="{skill}"', out)
        self.assertIn('<skill id="Riding" value="7" />', out)
        self.assertEqual(115, int(out.split('id="Athletics" value="')[1].split('"')[0]),
                         "existing values are untouched")

    def test_the_three_line_entry_shape_is_preserved(self):
        block = ('\n            <skill\n                id="Athletics"\n                value="45" />\n        ')
        out = rb.insert_missing_skill_entries(block, {s: 3 for s in rb.SKILL_NAMES}, "t")
        self.assertIn('\n            <skill\n                id="Bow"\n                value="3" />', out)

    def test_a_complete_block_is_returned_unchanged(self):
        block = "\n" + "".join(
            f'      <skill id="{s}" value="1" />\n' for s in rb.SKILL_NAMES) + "    "
        self.assertEqual(block, rb.insert_missing_skill_entries(
            block, {s: 9 for s in rb.SKILL_NAMES}, "t"))


if __name__ == "__main__":
    unittest.main()
