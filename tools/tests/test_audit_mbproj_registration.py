#!/usr/bin/env python
"""Unit tests for tools/audit_mbproj_registration.py (issues #983, #987).

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

NATIVE_TYPES = '<action_types><action name="act_native"/></action_types>'

# The exact set of ids the engine requests (#987 item 1), kept here as an independent copy so a
# change to the tool's table has to be made twice.
EXACT_REQUESTED = {
    "soln_skins", "soln_item_holsters", "soln_action_sets", "soln_action_types",
    "soln_voice_definitions", "soln_sound_parameter_data",
    "soln_worldmap_color_grades", "soln_soundtrack",
    "soln_movement_sets", "soln_full_movement_sets", "soln_bone_body_types",
    "soln_monster_usage_sets", "soln_item_usage_sets",
}

SUBMODULE_MONSTERS = ('<Module><Xmls><XmlNode><XmlName id="Monsters" path="Monsters/LOTR/m"/></XmlNode>'
                      '</Xmls></Module>')


def put(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(data if isinstance(data, bytes) else data.encode("utf-8"))


class AuditCase(unittest.TestCase):
    def run_audit(self, mbproj_bytes, files=("ModuleData/a.xml",), game_dir=None, extra=None,
                  submodule=None, native_types=NATIVE_TYPES):
        """Audit a module whose project.mbproj holds `mbproj_bytes`; (severity, message) pairs."""
        with tempfile.TemporaryDirectory() as tmp:
            module = os.path.join(tmp, "Mod")
            os.makedirs(os.path.join(module, "ModuleData"))
            put(os.path.join(module, "ModuleData", "project.mbproj"), mbproj_bytes)
            for rel in files:
                put(os.path.join(module, rel.replace("/", os.sep)), "<base/>")
            for rel, text in (extra or {}).items():
                put(os.path.join(module, rel.replace("/", os.sep)), text)
            if submodule is not None:
                put(os.path.join(module, "SubModule.xml"), submodule)
            game = game_dir or os.path.join(tmp, "game")
            if native_types is not None:
                put(os.path.join(game, "Modules", "Native", "ModuleData", "action_types.xml"), native_types)
            return audit.audit_module(module, game)

    def wrap(self, rows):
        return f"<base>{rows}</base>".encode("utf-8")

    def errors(self, findings, tag):
        return [m for sev, m in findings if sev == "ERROR" and tag in m]

    def warns(self, findings, tag):
        return [m for sev, m in findings if sev == "WARN" and tag in m]


class RowShapes(AuditCase):
    def test_name_before_id_is_read(self):
        findings = self.run_audit(self.wrap('<file name="ModuleData/a.xml" id="taom_invented"/>'))
        self.assertTrue(self.errors(findings, "DEAD-ID"), findings)
        self.assertIn("taom_invented", self.errors(findings, "DEAD-ID")[0])

    def test_attribute_order_does_not_hide_a_good_row(self):
        findings = self.run_audit(self.wrap('<file name="ModuleData/a.xml" id="soln_skins"/>'))
        self.assertEqual([], findings)

    def test_missing_name_is_an_error(self):
        findings = self.run_audit(self.wrap('<file id="soln_skins"/>'))
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("soln_skins", errs[0])
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
                '<base><file id="soln_skins" name="ModuleData/a.xml"/></base>').encode("utf-8")
        self.assertEqual([], self.run_audit(body))

    def test_wrong_root_is_an_error(self):
        findings = self.run_audit(b'<root><file id="soln_skins" name="ModuleData/a.xml"/></root>')
        errs = self.errors(findings, "BAD-FILE")
        self.assertEqual(1, len(errs), findings)
        self.assertIn("<root>", errs[0])


class RequestedIdTable(AuditCase):
    """#987 item 1: the ids the engine asks for are an exact table, not Native's own ids."""

    def test_the_table_is_exactly_the_thirteen_requested_ids(self):
        self.assertEqual(EXACT_REQUESTED, set(audit.REQUESTED_IDS))

    def test_every_id_names_its_source(self):
        for file_id, source in audit.REQUESTED_IDS.items():
            with self.subTest(file_id):
                self.assertTrue(source.strip(), file_id)
        for file_id in ("soln_skins", "soln_item_holsters", "soln_action_sets", "soln_action_types",
                        "soln_voice_definitions", "soln_sound_parameter_data"):
            self.assertIn("Module.cs", audit.REQUESTED_IDS[file_id], file_id)
        self.assertIn("MapColorGradeManager", audit.REQUESTED_IDS["soln_worldmap_color_grades"])
        self.assertIn("MBMusicManager", audit.REQUESTED_IDS["soln_soundtrack"])

    def test_the_ids_of_the_uncalled_callbacks_are_not_requested(self):
        # Nothing in v1.5.5 calls the Animations or SoundEventData callback (native slots 0x2b0, 0x2d0).
        for file_id in ("soln_animations", "soln_sound_event_data"):
            with self.subTest(file_id):
                self.assertNotIn(file_id, audit.REQUESTED_IDS)
                findings = self.run_audit(self.wrap(f'<file id="{file_id}" name="ModuleData/a.xml"/>'))
                self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_a_sound_event_data_typed_row_still_passes_by_type(self):
        findings = self.run_audit(self.wrap(
            '<file id="soln_sound_event_data" name="ModuleData/a.xml" type="sound_event_data"/>'))
        self.assertEqual([], findings)

    def test_each_requested_id_is_clean(self):
        for file_id in sorted(EXACT_REQUESTED):
            with self.subTest(file_id):
                self.assertEqual([], self.run_audit(self.wrap(f'<file id="{file_id}" name="ModuleData/a.xml"/>')))

    def test_a_vanilla_only_id_is_dead(self):
        for file_id in ("soln_monsters", "soln_strings", "soln_particle_systems"):
            with self.subTest(file_id):
                findings = self.run_audit(self.wrap(f'<file id="{file_id}" name="ModuleData/a.xml"/>'))
                self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)


class InertRows(AuditCase):
    """#987 item 1: a row nothing requests whose file SubModule.xml also registers loads anyway; the
    row is read by nothing, which is a warning and not a dead file."""

    ROW = '<file id="soln_monsters" name="ModuleData/Monsters/LOTR/m.xml" type="monster"/>'
    FILES = ("ModuleData/Monsters/LOTR/m.xml",)

    def test_a_row_for_a_file_submodule_registers_is_an_inert_warning(self):
        findings = self.run_audit(self.wrap(self.ROW), files=self.FILES, submodule=SUBMODULE_MONSTERS)
        self.assertEqual([], self.errors(findings, "DEAD-ID"), findings)
        warns = self.warns(findings, "INERT-ROW")
        self.assertEqual(1, len(warns), findings)
        self.assertIn("soln_monsters", warns[0])
        self.assertIn("SubModule.xml", warns[0])
        self.assertIn("read by nothing", warns[0])
        self.assertEqual(1, len(findings), findings)

    def test_a_row_is_inert_only_when_the_registered_file_exists(self):
        findings = self.run_audit(self.wrap(self.ROW), files=(), submodule=SUBMODULE_MONSTERS)
        self.assertEqual([], self.warns(findings, "INERT-ROW"), findings)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_a_folder_registration_covers_the_files_in_it(self):
        sub = '<Module><Xmls><XmlNode><XmlName id="Monsters" path="Monsters"/></XmlNode></Xmls></Module>'
        findings = self.run_audit(
            self.wrap('<file id="soln_monsters" name="ModuleData/Monsters/m.xml" type="monster"/>'),
            files=("ModuleData/Monsters/m.xml",), submodule=sub)
        self.assertEqual(1, len(self.warns(findings, "INERT-ROW")), findings)
        self.assertEqual([], self.errors(findings, "DEAD-ID"))

    def test_a_native_kind_row_registered_in_submodule_stays_dead(self):
        # Only a type with a managed twin (monster, item_modifier) is read through the XmlName id.
        # The engine loads an XmlName only for an exact id something requests (MBObjectManager.cs:880).
        for kind in ("action_set", "action_type", "monster_usage_set", "skin"):
            with self.subTest(kind):
                findings = self.run_audit(
                    self.wrap(f'<file id="soln_custom" name="ModuleData/Monsters/LOTR/m.xml" type="{kind}"/>'),
                    files=self.FILES, submodule=SUBMODULE_MONSTERS)
                self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)
                self.assertEqual([], self.warns(findings, "INERT-ROW"))

    def test_another_xmlname_id_does_not_make_a_monster_row_inert(self):
        sub = '<Module><Xmls><XmlNode><XmlName id="monsters" path="Monsters/LOTR/m"/></XmlNode></Xmls></Module>'
        findings = self.run_audit(self.wrap(self.ROW), files=self.FILES, submodule=sub)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)
        self.assertEqual([], self.warns(findings, "INERT-ROW"))

    def test_an_item_modifier_row_is_inert_beside_its_exact_id_only(self):
        row = self.wrap('<file id="soln_mods" name="ModuleData/Monsters/LOTR/m.xml" type="item_modifier"/>')
        sub = '<Module><Xmls><XmlNode><XmlName id="ItemModifiers" path="Monsters/LOTR/m"/></XmlNode></Xmls></Module>'
        self.assertEqual(1, len(self.warns(self.run_audit(row, files=self.FILES, submodule=sub), "INERT-ROW")))
        findings = self.run_audit(row, files=self.FILES, submodule=SUBMODULE_MONSTERS)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_a_monster_row_with_a_wrong_type_name_is_dead_not_inert(self):
        findings = self.run_audit(
            self.wrap('<file id="soln_monsters" name="ModuleData/Monsters/LOTR/m.xml"/>'),
            files=self.FILES, submodule=SUBMODULE_MONSTERS)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_the_dead_id_message_does_not_claim_the_file_never_loads(self):
        findings = self.run_audit(self.wrap(self.ROW), files=self.FILES)
        message = self.errors(findings, "DEAD-ID")[0]
        self.assertNotIn("never loads", message)
        self.assertIn("may still load another way", message)
        self.assertIn('<XmlName id="Monsters">', message)

    def test_a_row_for_an_unregistered_file_stays_dead(self):
        findings = self.run_audit(
            self.wrap('<file id="soln_monsters" name="ModuleData/other.xml"/>'),
            files=("ModuleData/other.xml", "ModuleData/Monsters/LOTR/m.xml"), submodule=SUBMODULE_MONSTERS)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)
        self.assertEqual([], self.warns(findings, "INERT-ROW"))

    def test_no_submodule_keeps_the_row_dead(self):
        findings = self.run_audit(self.wrap(self.ROW), files=self.FILES)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_an_unreadable_submodule_keeps_the_row_dead(self):
        findings = self.run_audit(self.wrap(self.ROW), files=self.FILES, submodule="<Module><Xmls>")
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)

    def test_a_requested_id_is_never_inert(self):
        findings = self.run_audit(
            self.wrap('<file id="soln_skins" name="ModuleData/Monsters/LOTR/m.xml"/>'),
            files=self.FILES, submodule=SUBMODULE_MONSTERS)
        self.assertEqual([], findings)

    def test_a_native_type_row_is_never_inert(self):
        findings = self.run_audit(
            self.wrap('<file id="taom_sounds" name="ModuleData/Monsters/LOTR/m.xml" type="module_sound"/>'),
            files=self.FILES, submodule=SUBMODULE_MONSTERS)
        self.assertEqual([], findings)

    def test_each_of_several_rows_is_judged_alone(self):
        rows = self.ROW + '<file id="soln_other" name="ModuleData/dead.xml"/>'
        findings = self.run_audit(self.wrap(rows), files=self.FILES + ("ModuleData/dead.xml",),
                                  submodule=SUBMODULE_MONSTERS)
        self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)
        self.assertEqual(1, len(self.warns(findings, "INERT-ROW")), findings)


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

    def test_worldmap_color_grades_is_requested(self):
        findings = self.run_audit(
            self.wrap('<file id="soln_worldmap_color_grades" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)


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

    def test_spider_era_rows_stay_dead_beside_an_unrelated_submodule(self):
        findings = self.run_audit(self.wrap("".join(SPIDER_ERA_ROWS[:4])), files=(),
                                  submodule=SUBMODULE_MONSTERS)
        self.assertEqual(4, len(self.errors(findings, "DEAD-ID")), findings)
        self.assertEqual([], self.warns(findings, "INERT-ROW"))

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

    def test_commented_out_row_is_ignored(self):
        findings = self.run_audit(self.wrap(
            '<!-- <file id="taom_invented" name="ModuleData/a.xml"/> -->'
            '<file id="soln_skins" name="ModuleData/a.xml"/>'))
        self.assertEqual([], findings)


class ExistingChecks(AuditCase):
    ROW = '<file id="soln_skins" name="ModuleData/a.xml"/>'

    def test_missing_file_is_a_warning(self):
        findings = self.run_audit(self.wrap('<file id="soln_skins" name="ModuleData/gone.xml"/>'), files=())
        self.assertEqual(["WARN"], [sev for sev, _ in findings])
        self.assertIn("MISSING", findings[0][1])

    def test_duplicate_id_with_xsd_is_merge_risk(self):
        with tempfile.TemporaryDirectory() as game:
            os.makedirs(os.path.join(game, "XmlSchemas"))
            open(os.path.join(game, "XmlSchemas", "soln_skins.xsd"), "w").close()
            findings = self.run_audit(self.wrap(self.ROW + self.ROW), game_dir=game)
        self.assertEqual(["WARN"], [sev for sev, _ in findings])
        self.assertIn("MERGE-RISK", findings[0][1])

    def test_duplicate_id_without_xsd_is_quiet(self):
        self.assertEqual([], self.run_audit(self.wrap(self.ROW + self.ROW)))

    def test_undeclared_action_is_an_error(self):
        sets = '<action_sets><action_set id="s"><action type="act_ghost"/></action_set></action_sets>'
        findings = self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_an_action_declared_by_native_is_clean(self):
        sets = '<action_sets><action_set id="s"><action type="act_native"/></action_set></action_sets>'
        self.assertEqual([], self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets}))

    def test_declared_action_is_clean(self):
        sets = '<action_sets><action_set id="s"><action type="act_ok"/></action_set></action_sets>'
        types = '<action_types><action name="act_ok"/></action_types>'
        findings = self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets,
                                                              "ModuleData/action_types.xml": types})
        self.assertEqual([], findings)

    def test_type_first_declaration_is_found(self):
        sets = '<action_sets><action_set id="s"><action type="act_typed"/></action_set></action_sets>'
        types = '<action_types><action type="actt_kick" name="act_typed"/></action_types>'
        findings = self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets,
                                                              "ModuleData/action_types.xml": types})
        self.assertEqual([], findings)

    def test_attribute_order_of_a_bound_action_does_not_hide_it(self):
        sets = ('<action_sets><action_set id="s"><action animation="a" type="act_ghost"/>'
                '</action_set></action_sets>')
        findings = self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_commented_out_declaration_does_not_count(self):
        sets = '<action_sets><action_set id="s"><action type="act_ghost"/></action_set></action_sets>'
        types = '<action_types><!-- <action name="act_ghost"/> --></action_types>'
        findings = self.run_audit(self.wrap(self.ROW), extra={"ModuleData/action_sets.xml": sets,
                                                              "ModuleData/action_types.xml": types})
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_unparseable_action_types_is_an_error(self):
        sets = '<action_sets><action_set id="s"><action type="act_ok"/></action_set></action_sets>'
        findings = self.run_audit(self.wrap(self.ROW),
                                  extra={"ModuleData/action_sets.xml": sets,
                                         "ModuleData/action_types.xml": "<action_types><action"})
        self.assertTrue(self.errors(findings, "BAD-FILE"), findings)
        self.assertIn("action_types.xml", self.errors(findings, "BAD-FILE")[0])

    def test_unparseable_action_sets_is_an_error(self):
        findings = self.run_audit(self.wrap(self.ROW),
                                  extra={"ModuleData/action_sets.xml": "<action_sets><action"})
        self.assertTrue(self.errors(findings, "BAD-FILE"), findings)
        self.assertIn("action_sets.xml", self.errors(findings, "BAD-FILE")[0])


class SiblingActionTypes(AuditCase):
    """A binding may resolve against another TAOM module's action_types.xml: the engine merges every
    loaded module's action types, and TAOM's dwarf sets bind rows the Armory declares."""

    ROW = '<file id="soln_skins" name="ModuleData/a.xml"/>'

    def audit_with_sibling(self, bound, declared):
        sets = f'<action_sets><action_set id="s"><action type="{bound}"/></action_set></action_sets>'
        with tempfile.TemporaryDirectory() as tmp:
            module = os.path.join(tmp, "Mod")
            sibling = os.path.join(tmp, "Sibling")
            put(os.path.join(module, "ModuleData", "project.mbproj"), self.wrap(self.ROW))
            put(os.path.join(module, "ModuleData", "a.xml"), "<base/>")
            put(os.path.join(module, "ModuleData", "action_sets.xml"), sets)
            put(os.path.join(sibling, "ModuleData", "action_types.xml"),
                f'<action_types><action name="{declared}"/></action_types>')
            put(os.path.join(tmp, "game", "Modules", "Native", "ModuleData", "action_types.xml"), NATIVE_TYPES)
            return audit.audit_module(module, os.path.join(tmp, "game"), extra_type_dirs=[sibling])

    def test_a_binding_declared_by_a_sibling_module_is_clean(self):
        self.assertEqual([], self.audit_with_sibling("act_sib", "act_sib"))

    def test_a_name_no_module_declares_is_still_undeclared(self):
        findings = self.audit_with_sibling("act_ghost", "act_sib")
        self.assertEqual(1, len(self.errors(findings, "UNDECLARED")), findings)

    def test_an_absent_sibling_is_skipped(self):
        sets = '<action_sets><action_set id="s"><action type="act_native"/></action_set></action_sets>'
        with tempfile.TemporaryDirectory() as tmp:
            module = os.path.join(tmp, "Mod")
            put(os.path.join(module, "ModuleData", "project.mbproj"), self.wrap(self.ROW))
            put(os.path.join(module, "ModuleData", "a.xml"), "<base/>")
            put(os.path.join(module, "ModuleData", "action_sets.xml"), sets)
            put(os.path.join(tmp, "game", "Modules", "Native", "ModuleData", "action_types.xml"), NATIVE_TYPES)
            findings = audit.audit_module(module, os.path.join(tmp, "game"),
                                          extra_type_dirs=[os.path.join(tmp, "nope")])
        self.assertEqual([], findings)


class NativeActionTypesNeeded(AuditCase):
    """#987 item 2: Native's action_types.xml is what an action_sets.xml binding resolves against, so
    a module with one cannot be audited without it; that is exit 2, never a pile of false UNDECLAREDs."""

    ROW = '<file id="soln_skins" name="ModuleData/a.xml"/>'
    SETS = {"ModuleData/action_sets.xml":
            '<action_sets><action_set id="s"><action type="act_native"/></action_set></action_sets>'}

    def test_a_missing_native_action_types_is_unavailable(self):
        with self.assertRaises(audit.AuditUnavailable) as caught:
            self.run_audit(self.wrap(self.ROW), extra=self.SETS, native_types=None)
        self.assertIn("action_types.xml", str(caught.exception))

    def test_an_unparseable_native_action_types_is_unavailable(self):
        with self.assertRaises(audit.AuditUnavailable):
            self.run_audit(self.wrap(self.ROW), extra=self.SETS, native_types="<action_types><action")

    def test_a_module_without_action_sets_does_not_need_it(self):
        self.assertEqual([], self.run_audit(self.wrap(self.ROW), native_types=None))


class EncodingEdges(AuditCase):
    """#987 item 3: the files are read as the engine's StreamReader reads them."""

    ROW = '<file id="soln_skins" name="ModuleData/a.xml"/>'
    DOC = f"<base>{ROW}</base>"

    def test_a_bom_less_utf16_project_is_a_startup_throw(self):
        for codec in ("utf-16-le", "utf-16-be"):
            with self.subTest(codec):
                findings = self.run_audit(self.DOC.encode(codec))
                self.assertEqual(1, len(self.errors(findings, "BAD-FILE")), findings)

    def test_a_utf16_project_with_a_bom_loads(self):
        self.assertEqual([], self.run_audit(self.DOC.encode("utf-16")))

    def test_a_utf8_bom_project_loads(self):
        self.assertEqual([], self.run_audit(b"\xef\xbb\xbf" + self.DOC.encode("utf-8")))

    def test_a_latin1_byte_in_an_action_file_is_read_not_raised(self):
        values, problems = self.latin1_values()
        self.assertEqual(([], ["act_�"]), (problems, values))

    def latin1_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "action_sets.xml")
            put(path, b'<action_sets><action_set id="s"><action type="act_\xe9"/></action_set></action_sets>')
            values, problems = audit.action_attrs(path, "type")
        return values, problems


class MainEntry(unittest.TestCase):
    def game(self, tmp, module_rows, native_types=NATIVE_TYPES, extra=None):
        """A scratch install: one module `Mod` and Native's action_types.xml; returns the root."""
        module = os.path.join(tmp, "Modules", "Mod", "ModuleData")
        os.makedirs(module)
        if module_rows is not None:
            put(os.path.join(module, "project.mbproj"), f"<base>{module_rows}</base>")
        if native_types is not None:
            put(os.path.join(tmp, "Modules", "Native", "ModuleData", "action_types.xml"), native_types)
        for rel, text in (extra or {}).items():
            put(os.path.join(tmp, "Modules", "Mod", rel), text)
        return tmp

    def run_main(self, game_dir, module="Mod", stdout=None, repo=None):
        """main() on a scratch install; module=None is the default scope, repo stands in for the repo
        root. Returns (exit code, the printed text, or "" when `stdout` is a caller's stream)."""
        out = stdout or io.StringIO()
        argv = ["audit_mbproj_registration.py", "--game-dir", game_dir] + (["--module", module] if module else [])
        with mock.patch.object(sys, "argv", argv), mock.patch.object(audit, "REPO_ROOT", repo or audit.REPO_ROOT), \
                contextlib.redirect_stdout(out):
            code = audit.main()
        return code, out.getvalue() if isinstance(out, io.StringIO) else ""

    def test_a_narrowed_audit_still_counts_a_sibling_modules_action_types(self):
        sets = '<action_sets><action_set id="s"><action type="act_sib"/></action_set></action_sets>'
        with tempfile.TemporaryDirectory() as tmp:
            game = self.game(tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>',
                             extra={"ModuleData/action_sets.xml": sets})
            put(os.path.join(tmp, "Modules", "Sib", "ModuleData", "action_types.xml"),
                '<action_types><action name="act_sib"/></action_types>')
            with mock.patch.object(audit, "TAOM_MODULES", ("Mod", "Sib")):
                code, out = self.run_main(game)
        self.assertEqual(0, code, out)
        self.assertIn("0 error(s)", out)

    def test_an_error_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, '<file id="taom_invented" name="ModuleData/a.xml"/>'))
        self.assertEqual(1, code, out)
        self.assertIn("DEAD-ID", out)

    def test_a_clean_module_exits_0(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>'))
        self.assertEqual(0, code, out)
        self.assertIn("0 error(s)", out)

    def test_an_inert_row_is_a_warning_and_exits_0(self):
        with tempfile.TemporaryDirectory() as tmp:
            game = self.game(tmp, '<file id="soln_monsters" name="ModuleData/Monsters/LOTR/m.xml" type="monster"/>',
                             extra={"SubModule.xml": SUBMODULE_MONSTERS,
                                    "ModuleData/Monsters/LOTR/m.xml": "<monsters/>"})
            code, out = self.run_main(game)
        self.assertEqual(0, code, out)
        self.assertIn("INERT-ROW", out)
        self.assertIn("0 error(s), 1 warning(s)", out)

    def test_the_spider_era_rows_stay_dead(self):
        spider = ('<file id="soln_spider_monster" name="ModuleData/Monsters/LOTR/lotr_monster_spider.xml" '
                  'type="monster" />'
                  '<file id="soln_spider_action_sets" name="ModuleData/a.xml" type="action_set" />')
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, spider))
        self.assertEqual(1, code, out)
        self.assertEqual(2, out.count("DEAD-ID"), out)

    def test_native_project_mbproj_is_not_read_at_all(self):
        with tempfile.TemporaryDirectory() as tmp:
            game = self.game(tmp, '<file id="soln_monsters" name="ModuleData/a.xml" type="monster"/>')
            put(os.path.join(tmp, "Modules", "Native", "ModuleData", "project.mbproj"),
                '<base><file id="soln_monsters" name="ModuleData/m.xml"/></base>')
            code, out = self.run_main(game)
        self.assertEqual(1, code, out)
        self.assertIn("DEAD-ID", out)

    def test_the_header_counts_the_exact_table(self):
        with tempfile.TemporaryDirectory() as tmp:
            _, out = self.run_main(self.game(tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>'))
        self.assertIn(f"Requested ids: {len(audit.REQUESTED_IDS)} ", out)
        self.assertNotIn("stand-in", out)

    # --- #987 item 2: nothing audited is exit 2 --------------------------------------------------
    def test_a_module_that_does_not_exist_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>'),
                                      module="NoSuchModule")
        self.assertEqual(2, code, out)
        self.assertIn("NoSuchModule", out)
        self.assertIn("is not installed", out)
        self.assertNotIn("No silently-ignored registrations", out)

    def test_a_named_module_without_a_project_mbproj_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, None))
        self.assertEqual(2, code, out)
        self.assertIn("has no", out)
        self.assertIn("project.mbproj", out)
        self.assertNotIn("No silently-ignored registrations", out)

    def test_a_missing_modules_folder_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(tmp)
        self.assertEqual(2, code, out)
        self.assertIn("Modules directory not found", out)

    def test_a_default_scope_missing_a_taom_module_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            put(os.path.join(tmp, "Main", "_Module", "ModuleData", "project.mbproj"),
                '<base><file id="soln_skins" name="ModuleData/a.xml"/></base>')
            put(os.path.join(tmp, "Main", "_Module", "ModuleData", "a.xml"), "<base/>")
            os.makedirs(os.path.join(tmp, "Modules"))
            code, out = self.run_main(tmp, module=None, repo=tmp)
        self.assertEqual(2, code, out)
        self.assertIn("Not audited: TAOM_Map, LOTRLOME_Armory", out)
        self.assertNotIn("No silently-ignored registrations", out)

    def test_a_missing_taom_module_does_not_hide_an_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            put(os.path.join(tmp, "Main", "_Module", "ModuleData", "project.mbproj"),
                '<base><file id="taom_invented" name="ModuleData/a.xml"/></base>')
            os.makedirs(os.path.join(tmp, "Modules"))
            code, out = self.run_main(tmp, module=None, repo=tmp)
        self.assertEqual(1, code, out)
        self.assertIn("Not audited: TAOM_Map, LOTRLOME_Armory", out)

    def test_a_scope_with_nothing_to_audit_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.run_main(self.game(tmp, None), module=None, repo=tmp)
        self.assertEqual(2, code, out)
        self.assertIn("nothing was audited", out)

    def test_an_unreadable_native_action_types_exits_2(self):
        sets = {"ModuleData/action_sets.xml":
                '<action_sets><action_set id="s"><action type="act_native"/></action_set></action_sets>'}
        for native in (None, "<action_types><action"):
            with self.subTest(native), tempfile.TemporaryDirectory() as tmp:
                code, out = self.run_main(self.game(
                    tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>', native_types=native, extra=sets))
                self.assertEqual(2, code, out)
                self.assertIn("action_types.xml", out)
                self.assertNotIn("No silently-ignored registrations", out)

    # --- #987 item 3: a narrow console must not turn a finding into a traceback ------------------
    def test_a_latin1_byte_in_an_action_file_prints_on_a_cp1252_console(self):
        sets = {"ModuleData/action_sets.xml":
                b'<action_sets><action_set id="s"><action type="act_\xe9"/></action_set></action_sets>'}
        raw = io.BytesIO()
        narrow = io.TextIOWrapper(raw, encoding="cp1252", errors="strict")
        with tempfile.TemporaryDirectory() as tmp:
            code, _ = self.run_main(
                self.game(tmp, '<file id="soln_skins" name="ModuleData/a.xml"/>', extra=sets), stdout=narrow)
            narrow.flush()
        self.assertEqual(1, code)
        self.assertIn(b"UNDECLARED", raw.getvalue())
        self.assertIn(b"act_\\ufffd", raw.getvalue())


class AllSkipsOfficialModules(unittest.TestCase):
    """--all skips the game's own modules (SubModule.xml ModuleType Official or OfficialOptional): they
    ship rows nothing requests and are not TAOM's to fix. Third-party mods are still audited."""

    ROW = '<base><file id="soln_unrequested" name="ModuleData/a.xml"/></base>'

    def run_all(self, official_type):
        with tempfile.TemporaryDirectory() as tmp:
            for name, kind in (("Native", official_type), ("ThirdParty", "Community")):
                root = os.path.join(tmp, "Modules", name)
                put(os.path.join(root, "SubModule.xml"), f'<Module><ModuleType value="{kind}"/></Module>')
                put(os.path.join(root, "ModuleData", "project.mbproj"), self.ROW)
                put(os.path.join(root, "ModuleData", "a.xml"), "<base/>")
            out = io.StringIO()
            argv = ["audit_mbproj_registration.py", "--game-dir", tmp, "--all"]
            with mock.patch.object(audit, "REPO_ROOT", tmp), mock.patch.object(sys, "argv", argv), \
                    contextlib.redirect_stdout(out):
                code = audit.main()
            return code, out.getvalue()

    def test_an_official_row_is_not_an_error_but_a_third_party_one_is(self):
        for kind in ("Official", "OfficialOptional"):
            with self.subTest(kind):
                code, out = self.run_all(kind)
                self.assertEqual(1, code, out)
                self.assertNotIn("[Native]", out)
                self.assertIn("[ThirdParty] DEAD-ID", out)
                self.assertEqual(1, out.count("DEAD-ID"), out)

    def test_one_note_names_the_skipped_modules(self):
        _, out = self.run_all("Official")
        self.assertEqual(1, out.count("Skipped official modules"), out)
        self.assertIn("Native", out.split("Skipped official modules")[1].splitlines()[0])

    def test_the_all_scope_audits_taom_from_the_repo_copy(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo, game = os.path.join(tmp, "repo"), os.path.join(tmp, "game")
            put(os.path.join(game, "Modules", "TAOM", "ModuleData", "project.mbproj"),
                '<base><file id="soln_skins" name="ModuleData/a.xml"/></base>')
            put(os.path.join(repo, "Main", "_Module", "ModuleData", "project.mbproj"),
                '<base><file id="taom_invented" name="ModuleData/a.xml"/></base>')
            put(os.path.join(repo, "Main", "_Module", "ModuleData", "a.xml"), "<base/>")
            out = io.StringIO()
            argv = ["audit_mbproj_registration.py", "--game-dir", game, "--all"]
            with mock.patch.object(audit, "REPO_ROOT", repo), mock.patch.object(sys, "argv", argv), \
                    contextlib.redirect_stdout(out):
                code = audit.main()
        self.assertEqual(1, code, out.getvalue())
        self.assertIn("[TAOM] DEAD-ID", out.getvalue())

    def test_the_module_type_value_may_carry_whitespace(self):
        with tempfile.TemporaryDirectory() as tmp:
            put(os.path.join(tmp, "SubModule.xml"), '<Module><ModuleType value=" Official "/></Module>')
            self.assertTrue(audit.is_official(tmp))
            put(os.path.join(tmp, "SubModule.xml"), '<Module><ModuleType value="Community"/></Module>')
            self.assertFalse(audit.is_official(tmp))

    def test_a_named_official_module_is_still_audited(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = os.path.join(tmp, "Modules", "Native")
            put(os.path.join(root, "SubModule.xml"), '<Module><ModuleType value="Official"/></Module>')
            put(os.path.join(root, "ModuleData", "project.mbproj"), self.ROW)
            argv = ["audit_mbproj_registration.py", "--game-dir", tmp, "--module", "Native"]
            out = io.StringIO()
            with mock.patch.object(sys, "argv", argv), contextlib.redirect_stdout(out):
                code = audit.main()
        self.assertEqual(1, code, out.getvalue())


class TaomRepoCopy(unittest.TestCase):
    """#987 item 4: "TAOM" is audited from the repo's Main/_Module, the file authors edit, and the
    deployed Modules/TAOM copy is only compared."""

    GOOD = '<base><file id="soln_skins" name="ModuleData/a.xml"/></base>'
    DEAD = '<base><file id="taom_invented" name="ModuleData/a.xml"/></base>'

    def run_taom(self, repo_mbproj, deployed_mbproj, *args, module="TAOM"):
        with tempfile.TemporaryDirectory() as tmp:
            repo, game = os.path.join(tmp, "repo"), os.path.join(tmp, "game")
            os.makedirs(os.path.join(game, "Modules"))
            if repo_mbproj is not None:
                put(os.path.join(repo, "Main", "_Module", "ModuleData", "project.mbproj"), repo_mbproj)
                put(os.path.join(repo, "Main", "_Module", "ModuleData", "a.xml"), "<base/>")
            if deployed_mbproj is not None:
                put(os.path.join(game, "Modules", "TAOM", "ModuleData", "project.mbproj"), deployed_mbproj)
                put(os.path.join(game, "Modules", "TAOM", "ModuleData", "a.xml"), "<base/>")
            argv = ["audit_mbproj_registration.py", "--game-dir", game] + (["--module", module] if module else []) \
                + list(args)
            out = io.StringIO()
            with mock.patch.object(audit, "REPO_ROOT", repo), mock.patch.object(sys, "argv", argv), \
                    contextlib.redirect_stdout(out):
                code = audit.main()
            return code, out.getvalue()

    def test_an_undeployed_bad_edit_is_caught(self):
        code, out = self.run_taom(self.DEAD, self.GOOD)
        self.assertEqual(1, code, out)
        self.assertIn("DEAD-ID", out)

    def test_an_undeployed_fix_is_what_counts(self):
        code, out = self.run_taom(self.GOOD, self.DEAD)
        self.assertEqual(0, code, out)

    def test_the_output_says_which_copy_was_audited(self):
        _, out = self.run_taom(self.GOOD, self.GOOD)
        self.assertIn("Main/_Module/ModuleData/project.mbproj", out)
        self.assertIn("repo", out)

    def test_identical_copies_are_said_to_match(self):
        _, out = self.run_taom(self.GOOD, self.GOOD)
        self.assertIn("identical", out)
        self.assertNotIn("DEPLOYED-DIFFERS", out)

    def test_differing_copies_warn(self):
        code, out = self.run_taom(self.GOOD, self.DEAD)
        self.assertEqual(0, code, out)
        self.assertIn("DEPLOYED-DIFFERS", out)
        self.assertIn("1 warning(s)", out)

    def test_an_absent_deployed_copy_is_not_an_error(self):
        code, out = self.run_taom(self.GOOD, None)
        self.assertEqual(0, code, out)
        self.assertIn("not deployed", out)

    def test_a_repo_without_the_file_exits_2(self):
        code, out = self.run_taom(None, self.GOOD)
        self.assertEqual(2, code, out)
        self.assertIn("Main/_Module/ModuleData/project.mbproj", out)

    def test_the_default_scope_audits_the_repo_copy_of_taom(self):
        code, out = self.run_taom(self.DEAD, self.GOOD, module=None)
        self.assertEqual(1, code, out)
        self.assertIn("[TAOM]", out)

    def test_the_module_name_in_findings_stays_taom(self):
        _, out = self.run_taom(self.DEAD, self.GOOD)
        self.assertIn("[TAOM] DEAD-ID", out)


if __name__ == "__main__":
    unittest.main()
