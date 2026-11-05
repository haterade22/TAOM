#!/usr/bin/env python
"""Unit tests for tools/audit_mbproj_registration.py (issue #983).

Every test builds a SYNTHETIC module in a tempdir and never needs a game install. The
project.mbproj under test is the only file the audit reads in most cases, so each shape of
row is written out verbatim.
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_mbproj_registration as audit  # noqa: E402

VANILLA = {"soln_monsters", "soln_skins"}


class AuditCase(unittest.TestCase):
    def run_audit(self, mbproj_bytes, files=("ModuleData/a.xml",), game_dir=None, extra=None):
        """Audit a module whose project.mbproj holds `mbproj_bytes`; (severity, message) pairs."""
        with tempfile.TemporaryDirectory() as tmp:
            module = os.path.join(tmp, "Mod")
            os.makedirs(os.path.join(module, "ModuleData"))
            with open(os.path.join(module, "ModuleData", "project.mbproj"), "wb") as handle:
                handle.write(mbproj_bytes)
            for rel in files:
                path = os.path.join(module, rel.replace("/", os.sep))
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write("<base/>")
            for rel, text in (extra or {}).items():
                with open(os.path.join(module, rel.replace("/", os.sep)), "w", encoding="utf-8") as handle:
                    handle.write(text)
            return audit.audit_module(module, game_dir or os.path.join(tmp, "game"), VANILLA)

    def wrap(self, rows):
        return f"<base>{rows}</base>".encode("utf-8")

    def errors(self, findings, tag):
        return [m for sev, m in findings if sev == "ERROR" and tag in m]


class RowShapes(AuditCase):
    def test_name_before_id_is_read(self):
        findings = self.run_audit(self.wrap('<file name="ModuleData/a.xml" id="taom_invented"/>'))
        self.assertTrue(self.errors(findings, "DEAD-ID"), findings)
        self.assertIn("taom_invented", self.errors(findings, "DEAD-ID")[0])

    def test_attribute_order_does_not_hide_a_good_row(self):
        findings = self.run_audit(self.wrap('<file name="ModuleData/a.xml" id="soln_monsters"/>'))
        self.assertEqual([], findings)

    def test_missing_name_is_an_error(self):
        findings = self.run_audit(self.wrap('<file id="soln_monsters"/>'))
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("soln_monsters", errs[0])
        self.assertIn("project.mbproj", errs[0])
        self.assertIn("no 'id' or no 'name'", errs[0])

    def test_missing_id_is_an_error(self):
        findings = self.run_audit(self.wrap('<file name="ModuleData/a.xml"/>'))
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("ModuleData/a.xml", errs[0])

    def test_unparseable_file_is_an_error(self):
        findings = self.run_audit(b"<base><file id='x' name='y'></base")
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("project.mbproj", errs[0])

    def test_bogus_declared_encoding_still_loads(self):
        body = ('<?xml version="1.0" encoding="bogus-9"?>'
                '<base><file id="soln_monsters" name="ModuleData/a.xml"/></base>').encode("utf-8")
        self.assertEqual([], self.run_audit(body))

    def test_wrong_root_is_an_error(self):
        findings = self.run_audit(b'<root><file id="soln_monsters" name="ModuleData/a.xml"/></root>')
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("<root>", errs[0])


class DeadIds(AuditCase):
    def test_non_soln_dead_id(self):
        findings = self.run_audit(self.wrap('<file id="taom_invented" name="ModuleData/a.xml"/>'))
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_soln_dead_id_still_flagged(self):
        findings = self.run_audit(self.wrap('<file id="soln_spider_monster" name="ModuleData/a.xml"/>'))
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_native_type_with_unrequested_id_is_not_dead(self):
        findings = self.run_audit(
            self.wrap('<file id="taom_sounds" name="ModuleData/a.xml" type="module_sound"/>'))
        self.assertEqual([], findings)

    def test_unknown_type_does_not_rescue_an_unknown_id(self):
        findings = self.run_audit(
            self.wrap('<file id="taom_invented" name="ModuleData/a.xml" type="not_a_native_type"/>'))
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_worldmap_color_grades_is_dead_on_v1_4_8(self):
        # Nothing in v1.4.8 reads it.
        findings = self.run_audit(
            self.wrap('<file id="soln_worldmap_color_grades" name="ModuleData/a.xml"/>'))
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)


THIRTEEN_NATIVE_TYPES = {
    "hard_coded_sound", "module_sound", "animation_combat_parameters", "native_parameters",
    "decal_set", "face_animation_record", "postfx", "prebaked_animation", "physics_material",
    "sound_files", "skinned_decals", "collision_infos", "sound_event_data",
}

# The six rows that were dead, copied VERBATIM with their `type`. The first four are the
# spider-era rows (lotraom-assets 7b45ffb97, 2026-05-04); the last two are the rows removed on
# 2026-08-28 (project.mbproj.bak-solnfix-20260828). A `type` the engine does not select on
# never rescued any of them, which is what this class pins: do not widen NATIVE_BY_TYPE to
# the types of vanilla's project.mbproj, because these six rows carry exactly those types.
SPIDER_ERA_ROWS = (
    '<file id="soln_spider_action_sets" name="ModuleData/Animations/action_sets_spider.xml" type="action_set" />',
    '<file id="soln_spider_action_types" name="ModuleData/Animations/action_types_spider.xml" type="action_type" />',
    '<file id="soln_spider_monster" name="ModuleData/Monsters/LOTR/lotr_monster_spider.xml" type="monster" />',
    '<file id="soln_spider_monster_usage_set" name="ModuleData/MonsterUsage/LOTR/lotr_monster_usage_spider.xml" type="monster_usage_set" />',
    '<file id="soln_spider_monster" name="ModuleData/Monsters/LOTR/lotr_monster_spider.xml" type="monster" />',
    '<file id="soln_lotr_misc_action_types" name="ModuleData/Animations/action_types_lotr_misc.xml" type="action_type" />',
)


class NativeTypeTable(AuditCase):
    def test_the_table_is_exactly_the_thirteen_types_native_requests(self):
        self.assertEqual(THIRTEEN_NATIVE_TYPES, set(audit.NATIVE_BY_TYPE))

    def test_each_of_the_thirteen_loads_whatever_its_id(self):
        for native_type in sorted(THIRTEEN_NATIVE_TYPES):
            with self.subTest(native_type):
                findings = self.run_audit(self.wrap(
                    f'<file id="taom_custom" name="ModuleData/a.xml" type="{native_type}"/>'))
                self.assertEqual([], findings)

    def test_spider_era_rows_are_dead(self):
        for row in SPIDER_ERA_ROWS:
            with self.subTest(row):
                findings = self.run_audit(self.wrap(row), files=(
                    "ModuleData/Animations/action_sets_spider.xml",
                    "ModuleData/Animations/action_types_spider.xml",
                    "ModuleData/Monsters/LOTR/lotr_monster_spider.xml",
                    "ModuleData/MonsterUsage/LOTR/lotr_monster_usage_spider.xml",
                    "ModuleData/Animations/action_types_lotr_misc.xml"))
                self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_the_whole_spider_era_file_has_four_dead_ids(self):
        findings = self.run_audit(self.wrap("".join(SPIDER_ERA_ROWS[:4])), files=())
        self.assertEqual(4, len(self.errors(findings, "DEAD-ID")), findings)

    def test_types_native_does_not_select_on_never_rescue_a_custom_id(self):
        for native_type in ("skin", "item_holster", "monster", "item", "tag", "action_set"):
            with self.subTest(native_type):
                findings = self.run_audit(self.wrap(
                    f'<file id="taom_custom" name="ModuleData/a.xml" type="{native_type}"/>'))
                self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_no_vanilla_type_vocabulary_is_unioned_in(self):
        self.assertFalse(hasattr(audit, "vanilla_types"))

    def test_soundtrack_id_is_requested(self):
        findings = self.run_audit(self.wrap('<file id="soln_soundtrack" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)

    def test_audit_module_adds_the_managed_ids_itself(self):
        self.assertNotIn("soln_worldmap_color_grades", audit.MANAGED_IDS)  # nothing in v1.4.8 reads it
        findings = self.run_audit(self.wrap(
            '<file id="soln_action_sets" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)

    def test_commented_out_row_is_ignored(self):
        findings = self.run_audit(self.wrap(
            '<!-- <file id="taom_invented" name="ModuleData/a.xml"/> -->'
            '<file id="soln_monsters" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)


class ExistingChecks(AuditCase):
    def test_missing_file_is_a_warning(self):
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/gone.xml"/>'),
                                  files=())
        self.assertEqual(["WARN"], [sev for sev, _ in findings])
        self.assertIn("MISSING", findings[0][1])

    def test_duplicate_id_with_xsd_is_merge_risk(self):
        with tempfile.TemporaryDirectory() as game:
            os.makedirs(os.path.join(game, "XmlSchemas"))
            open(os.path.join(game, "XmlSchemas", "soln_monsters.xsd"), "w").close()
            findings = self.run_audit(self.wrap(
                '<file id="soln_monsters" name="ModuleData/a.xml"/>'
                '<file id="soln_monsters" name="ModuleData/a.xml"/>'), game_dir=game)
        self.assertEqual(["WARN"], [sev for sev, _ in findings])
        self.assertIn("MERGE-RISK", findings[0][1])

    def test_duplicate_id_without_xsd_is_quiet(self):
        findings = self.run_audit(self.wrap(
            '<file id="soln_monsters" name="ModuleData/a.xml"/>'
            '<file id="soln_monsters" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)

    def test_undeclared_action_is_an_error(self):
        sets = '<action_sets><action_set id="s"><action type="act_ghost"/></action_set></action_sets>'
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_declared_action_is_clean(self):
        sets = '<action_sets><action_set id="s"><action type="act_ok"/></action_set></action_sets>'
        types = '<action_types><action name="act_ok"/></action_types>'
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets,
                                         "ModuleData/action_types.xml": types})
        self.assertEqual([], findings)

    def test_type_first_declaration_is_found(self):
        sets = '<action_sets><action_set id="s"><action type="act_typed"/></action_set></action_sets>'
        types = '<action_types><action type="actt_kick" name="act_typed"/></action_types>'
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets,
                                         "ModuleData/action_types.xml": types})
        self.assertEqual([], findings)

    def test_attribute_order_of_a_bound_action_does_not_hide_it(self):
        sets = ('<action_sets><action_set id="s"><action animation="a" type="act_ghost"/>'
                '</action_set></action_sets>')
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_commented_out_declaration_does_not_count(self):
        sets = '<action_sets><action_set id="s"><action type="act_ghost"/></action_set></action_sets>'
        types = '<action_types><!-- <action name="act_ghost"/> --></action_types>'
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets,
                                         "ModuleData/action_types.xml": types})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_unparseable_action_types_is_an_error(self):
        sets = '<action_sets><action_set id="s"><action type="act_ok"/></action_set></action_sets>'
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": sets,
                                         "ModuleData/action_types.xml": "<action_types><action"})
        self.assertTrue(self.errors(findings, "BAD-FILE"), findings)
        self.assertIn("action_types.xml", self.errors(findings, "BAD-FILE")[0])

    def test_unparseable_action_sets_is_an_error(self):
        findings = self.run_audit(self.wrap('<file id="soln_monsters" name="ModuleData/a.xml"/>'),
                                  extra={"ModuleData/action_sets.xml": "<action_sets><action"})
        self.assertTrue(self.errors(findings, "BAD-FILE"), findings)
        self.assertIn("action_sets.xml", self.errors(findings, "BAD-FILE")[0])


class MainEntry(unittest.TestCase):
    def game(self, tmp, native_rows, module_rows):
        """A scratch install: Native's project.mbproj and one module `Mod`; returns the root."""
        native = os.path.join(tmp, "Modules", "Native", "ModuleData")
        module = os.path.join(tmp, "Modules", "Mod", "ModuleData")
        os.makedirs(module)
        if native_rows is not None:
            os.makedirs(native)
            with open(os.path.join(native, "project.mbproj"), "w", encoding="utf-8") as handle:
                handle.write(f"<base>{native_rows}</base>")
        with open(os.path.join(module, "project.mbproj"), "w", encoding="utf-8") as handle:
            handle.write(f"<base>{module_rows}</base>")
        return tmp

    def run_main(self, game_dir):
        out = io.StringIO()
        argv = ["audit_mbproj_registration.py", "--game-dir", game_dir, "--module", "Mod"]
        with mock.patch.object(sys, "argv", argv), contextlib.redirect_stdout(out):
            code = audit.main()
        return code, out.getvalue()

    NATIVE = ('<file id="soln_monsters" name="ModuleData/m.xml"/>'
              '<file id="soln_skins" name="ModuleData/s.xml"/>')

    def test_an_error_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(
                tmp, self.NATIVE, '<file id="taom_invented" name="ModuleData/a.xml"/>'))
        self.assertEqual(1, code, out)
        self.assertIn("DEAD-ID", out)

    def test_a_clean_module_exits_0(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(
                tmp, self.NATIVE, '<file id="soln_monsters" name="ModuleData/a.xml"/>'))
        self.assertEqual(0, code, out)
        self.assertIn("0 error(s)", out)

    def test_the_installed_native_manifests_own_types_never_rescue_a_dead_row(self):
        """The real Native project.mbproj carries types such as monster and action_set. Those are
        vanilla types, not types native loads by, so a spider-era row stays dead."""
        native = ('<file id="soln_monsters" name="ModuleData/m.xml" type="monster"/>'
                  '<file id="soln_action_sets" name="ModuleData/a.xml" type="action_set"/>'
                  '<file id="soln_monster_usage_sets" name="ModuleData/u.xml" type="monster_usage_set"/>')
        spider = ('<file id="soln_spider_monster" name="ModuleData/Monsters/LOTR/lotr_monster_spider.xml" '
                  'type="monster" />'
                  '<file id="soln_spider_action_sets" name="ModuleData/a.xml" type="action_set" />')
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, native, spider))
        self.assertEqual(1, code, out)
        self.assertEqual(2, out.count("DEAD-ID"), out)

    def test_an_unreadable_native_manifest_exits_early_without_a_verdict(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(
                tmp, None, '<file id="taom_invented" name="ModuleData/a.xml"/>'))
        self.assertEqual(0, code, out)
        self.assertIn("Could not read Native/ModuleData/project.mbproj", out)
        self.assertNotIn("DEAD-ID", out)

    def test_the_header_counts_the_real_union(self):
        with tempfile.TemporaryDirectory() as tmp:
            _, out = self.run_main(self.game(
                tmp, self.NATIVE, '<file id="soln_monsters" name="ModuleData/a.xml"/>'))
        # soln_monsters from Native, plus every managed id (soln_skins is both)
        expected = len({"soln_monsters"} | audit.MANAGED_IDS)
        self.assertIn(f"Requested ids: {expected} ", out)


if __name__ == "__main__":
    unittest.main()
