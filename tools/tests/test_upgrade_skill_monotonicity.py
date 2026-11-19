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
level, the elite slot a fixed MILITIA_ELITE_BONUS above it (since 2026-09-13), so a militia
promotion is a step the curve never produces and the gate leaves alone. The exemption reads what
a culture BINDS, never a name. Name matching is the bug.
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

    def _run(self, skill_sets=None, game_modules=None):
        reg = ts.Registries({}, {}, {}, {}, {})
        if skill_sets is not None:
            reg.skill_sets = skill_sets
        validator = ts.Validator(self.root, [], reg, game_modules=game_modules)
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


class SkillTemplateEdgeTests(ValidatorGateTests):
    """A templated character's base skills live in a SkillSet these files do not hold.

    The gate judges an edge with a templated side on the character's effective skills: the
    SkillSet with the inline rows laid over it, resolved through
    sync_lord_inline_skills.effective_skills (#626). Whether those inline rows agree with the
    SkillSet stays SKILL_TEMPLATE_MISMATCH's question (validate_moduledata.
    skill_template_mismatch_issues), not the upgrade index's: on v1.4.8 the loader discarded the
    inline block whenever a template was named (44 militia shipped wearing vanilla values that way,
    #523); since v1.5.2 it lays the rows over a copy of the template, so only a DIFFERENT row
    changes anything.
    """

    @staticmethod
    def _templated(troop_id, template, upgrades=(), inline=None):
        rows = "".join(f'\n      <skill id="{k}" value="{v}" />' for k, v in (inline or {}).items())
        targets = "".join(f'\n      <upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
        return (
            f'  <NPCCharacter id="{troop_id}" level="1" default_group="Infantry" culture="Culture.x"\n'
            f'      skill_template="SkillSet.{template}">\n'
            f'    <skills>{rows}</skills>\n'
            f'    <upgrade_targets>{targets}\n'
            f'    </upgrade_targets>\n  </NPCCharacter>\n')

    def _install(self):
        inst = tempfile.mkdtemp(prefix="taom_upgrade_inst_")
        self.addCleanup(shutil.rmtree, inst, True)
        os.makedirs(os.path.join(inst, "Modules"))
        return inst

    def test_the_upgrade_index_leaves_template_parity_to_its_own_pass(self):
        troop = (
            '  <NPCCharacter id="t" level="11" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level11_template_skills">\n'
            '    <skills>\n      <skill id="OneHanded" value="60" />\n    </skills>\n'
            '    <upgrade_targets></upgrade_targets>\n  </NPCCharacter>\n')
        self._write(troop)
        self.assertEqual([], self._run())

    def test_a_template_with_an_empty_block_is_fine(self):
        troop = (
            '  <NPCCharacter id="t" level="11" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level11_template_skills">\n'
            '    <skills></skills>\n    <upgrade_targets></upgrade_targets>\n  </NPCCharacter>\n')
        self._write(troop)
        self.assertEqual([], self._run())

    def test_without_skill_sets_a_templated_edge_is_skipped(self):
        """With no install and no skill_sets the SkillSets cannot be read, so the edge is skipped,
        as every install-bound check is; validate_moduledata.py prints the skip (#626)."""
        self._write(
            '  <NPCCharacter id="p" level="1" default_group="Infantry" culture="Culture.x"\n'
            '      skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills">\n'
            '    <skills></skills>\n'
            '    <upgrade_targets>\n      <upgrade_target id="NPCCharacter.c" />\n'
            '    </upgrade_targets>\n  </NPCCharacter>\n'
            + _troop("c", 11, "Infantry", {k: 0 for k in rb.SKILL_NAMES}))
        self.assertEqual([], self._run())

    def test_a_templated_source_is_judged_on_its_skillset(self):
        self._write(self._templated("p", "src", ["c"])
                    + _troop("c", 11, "Infantry", {k: 50 for k in rb.SKILL_NAMES}))
        issues = self._run(skill_sets={"src": {k: 50 for k in rb.SKILL_NAMES} | {"Polearm": 70}})
        self.assertEqual(1, len(issues))
        self.assertEqual("UPGRADE_SKILL_REGRESSION", issues[0].code)
        self.assertEqual("c", issues[0].entry_id)
        self.assertIn("Polearm 70->50", issues[0].message)

    def test_a_templated_target_reads_its_skillset_for_an_undeclared_skill(self):
        self._write(_troop("p", 1, "Infantry", {k: 40 for k in rb.SKILL_NAMES}, ["c"])
                    + self._templated("c", "tgt"))
        issues = self._run(skill_sets={"tgt": {k: 60 for k in rb.SKILL_NAMES} | {"Bow": 30}})
        self.assertEqual(1, len(issues))
        self.assertEqual("c", issues[0].entry_id)
        self.assertIn("Bow 40->30", issues[0].message)
        self.assertNotIn("undeclared", issues[0].message, "the SkillSet declares Bow")
        self.assertNotIn("Polearm", issues[0].message)

    def test_an_inline_row_overrides_the_template(self):
        self._write(_troop("p", 1, "Infantry", {k: 40 for k in rb.SKILL_NAMES}, ["c"])
                    + self._templated("c", "tgt", inline={"OneHanded": 20}))
        issues = self._run(skill_sets={"tgt": {k: 60 for k in rb.SKILL_NAMES}})
        self.assertEqual(1, len(issues))
        self.assertIn("OneHanded 40->20", issues[0].message)

    def test_skill_sets_are_read_from_the_install_root(self):
        """validate_moduledata.py passes the Modules folder; the SkillSets are read from its parent."""
        inst = self._install()
        native = os.path.join(inst, "Modules", "Native", "ModuleData")
        os.makedirs(native)
        rows = "".join(f'<skill id="{k}" value="{70 if k == "Polearm" else 50}" />' for k in rb.SKILL_NAMES)
        with open(os.path.join(native, "x_skill_sets.xml"), "w", encoding="utf-8") as f:
            f.write(f'<SkillSets><SkillSet id="src">{rows}</SkillSet></SkillSets>\n')
        self._write(self._templated("p", "src", ["c"])
                    + _troop("c", 11, "Infantry", {k: 50 for k in rb.SKILL_NAMES}))
        issues = self._run(game_modules=os.path.join(inst, "Modules"))
        self.assertEqual(1, len(issues), [i.message for i in issues])
        self.assertEqual("c", issues[0].entry_id)
        self.assertIn("Polearm 70->50", issues[0].message)

    def test_an_install_with_no_skill_sets_gives_one_error(self):
        inst = self._install()
        self._write(self._templated("p1", "a", ["c"]) + self._templated("p2", "b", ["c"])
                    + _troop("c", 11, "Infantry", {k: 50 for k in rb.SKILL_NAMES}))
        issues = self._run(game_modules=os.path.join(inst, "Modules"))
        self.assertEqual(1, len(issues), [i.message for i in issues])
        self.assertEqual("(skill sets)", issues[0].entry_id)
        self.assertEqual("UPGRADE_SKILL_REGRESSION", issues[0].code)
        self.assertEqual(ts.Severity.ERROR, issues[0].severity)

    def test_an_install_with_no_vanilla_skill_sets_gives_one_error_beside_the_repo_ones(self):
        """TAOM's own SkillSet files always load, so a root that holds no vanilla SkillSet must
        still be one ERROR: else every vanilla template reads as empty and a real drop passes."""
        inst = self._install()
        with open(os.path.join(self.root, "taom_x_skill_sets.xml"), "w", encoding="utf-8") as f:
            f.write('<SkillSets><SkillSet id="taom_only"><skill id="Bow" value="10" /></SkillSet></SkillSets>\n')
        self._write(self._templated("p", "infantry_heavyinfantry_level16_template_skills", ["c"])
                    + _troop("c", 11, "Infantry", {k: 20 for k in rb.SKILL_NAMES}))
        issues = self._run(game_modules=os.path.join(inst, "Modules"))
        self.assertEqual(1, len(issues), [i.message for i in issues])
        self.assertEqual("(skill sets)", issues[0].entry_id)
        self.assertEqual(ts.Severity.ERROR, issues[0].severity)


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
        self.assertEqual(96, len(bound),
                         "96 troops are bound to a culture militia slot (Cardolan's and Rhudaur's 8 since #978, Ered Luin's 4, Anduin Vale's and Angaladh's 8, Dorwinion's and South Rhovanion's 8, Lothlorien's 4). Change this deliberately.")
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


class TemplatedRecordTests(unittest.TestCase):
    """rebalance_troops reads a templated character as its SkillSet with the inline rows laid over
    it (sync_lord_inline_skills.effective_skills, #626) and never writes one: an inline row that
    differs from the SkillSet is SKILL_TEMPLATE_MISMATCH."""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="taom_templated_rec_")
        self._characters_dir = rb.CHARACTERS_DIR

    def tearDown(self):
        rb.CHARACTERS_DIR = self._characters_dir
        shutil.rmtree(self.dir, ignore_errors=True)

    def _troops_file(self):
        path = os.path.join(self.dir, "troops_test.xml")
        with open(path, "w", encoding="utf-8") as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
                    '  <NPCCharacter id="p" level="1" default_group="Infantry" culture="Culture.gondor"\n'
                    '      name="{=x_p}p" occupation="Soldier" skill_template="SkillSet.t">\n'
                    '    <skills>\n      <skill id="Bow" value="90" />\n    </skills>\n'
                    '    <upgrade_targets>\n      <upgrade_target id="NPCCharacter.c" />\n'
                    '    </upgrade_targets>\n  </NPCCharacter>\n'
                    + _troop("c", 6, "Infantry", {k: 10 for k in rb.SKILL_NAMES})
                    + '</NPCCharacters>\n')
        return path

    def test_the_clamp_refuses_to_raise_a_templated_character(self):
        parent = ClampTests._record("parent", {s: 50 for s in rb.SKILL_NAMES}, ["child"])
        child = ClampTests._record("child", {s: 10 for s in rb.SKILL_NAMES})
        child["template"] = "SkillSet.t"
        child["new"] = None
        child["status"] = "SKILLSET"
        with self.assertRaises(RuntimeError) as ctx:
            rb.clamp_upgrade_monotonicity([parent, child])
        self.assertIn("SKILL_TEMPLATE_MISMATCH", str(ctx.exception))
        self.assertIn("SkillSet.t", str(ctx.exception))

    def test_a_templated_troop_reads_as_its_skillset(self):
        recs = rb.process_file(self._troops_file(), skill_sets={"t": {"Bow": 120, "Polearm": 70}})
        p = next(r for r in recs if r["id"] == "p")
        self.assertEqual("SkillSet.t", p["template"])
        self.assertIsNone(p["new"])
        self.assertTrue(p["status"].startswith("SKILLSET"), p["status"])
        self.assertEqual({s: 0 for s in rb.SKILL_NAMES} | {"Bow": 90, "Polearm": 70}, p["old"])

    def test_a_templated_troop_without_skillsets_is_refused(self):
        with self.assertRaises(RuntimeError) as ctx:
            rb.process_file(self._troops_file())
        self.assertIn("SkillSet.t", str(ctx.exception))

    def test_a_templated_villager_stays_off_the_curve_and_reads_as_its_skillset(self):
        characters = os.path.join(self.dir, "characters")
        os.makedirs(characters)

        def villager(vid, extra):
            return (f'  <NPCCharacter id="{vid}" level="6" default_group="Infantry" culture="Culture.gondor"\n'
                    f'      name="{{=x_{vid}}}{vid}" occupation="Villager"{extra}>\n'
                    f'    <skills>\n      <skill id="Polearm" value="10" />\n    </skills>\n'
                    f'    <Equipments>\n      <EquipmentRoster>\n'
                    f'        <equipment slot="Item0" id="Item.test_fork" />\n'
                    f'      </EquipmentRoster>\n    </Equipments>\n'
                    f'    <upgrade_targets>\n      <upgrade_target id="NPCCharacter.gondor_recruit" />\n'
                    f'    </upgrade_targets>\n  </NPCCharacter>\n')
        with open(os.path.join(characters, "npcs_gondor.xml"), "w", encoding="utf-8") as f:
            f.write('<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
                    + villager("villager_a", "") + villager("villager_b", ' skill_template="SkillSet.v"')
                    + '</NPCCharacters>\n')
        rb.CHARACTERS_DIR = characters
        recs = {r["id"]: r for r in rb.load_external_sources(
            item_classes={"test_fork": "Polearm"}, mounts={}, skill_sets={"v": {"Bow": 40}})}
        self.assertTrue(recs["villager_a"].get("villager"), "control: the fixture must reach the curve")
        b = recs["villager_b"]
        self.assertNotIn("villager", b)
        self.assertTrue(b["external"])
        self.assertEqual("SkillSet.v", b["template"])
        self.assertEqual({s: 0 for s in rb.SKILL_NAMES} | {"Polearm": 10, "Bow": 40}, b["old"])


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
